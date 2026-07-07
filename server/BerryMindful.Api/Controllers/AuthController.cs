using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BerryMindful.Data;
using BerryMindful.Data.Entities;
using BerryMindful.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using ZlEmailProvider;

namespace BerryMindful.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableRateLimiting("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    IMemoryCache cache,
    IConfiguration config,
    IEmailService emailService,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    [HttpPost("signup")]
    public async Task<ActionResult<AuthResponse>> Signup(SignupRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Unauthorized(new { error = "Account temporarily locked. Try again in a few minutes." });
        }
        if (!result.Succeeded)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (rawToken is null)
        {
            return Unauthorized(new { error = "No refresh token." });
        }

        var tokenHash = HashToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Unauthorized(new { error = "Refresh token invalid or expired." });
        }

        // Rotate: revoke the used token, issue a fresh pair
        stored.RevokedAt = DateTime.UtcNow;
        var response = await IssueTokensAsync(stored.User);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is not null)
        {
            // Rotating the stamp invalidates all outstanding access tokens across devices
            await userManager.UpdateSecurityStampAsync(user);
            cache.Remove($"secstamp:{user.Id}");

            await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        }

        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = config["Notifications:AppBaseUrl"] ?? "http://localhost:5173";
            var link = $"{baseUrl}/reset-password"
                + $"?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

            try
            {
                // Composed here rather than via ZlEmailProvider's SendPasswordResetAsync,
                // whose subject/body copy is RSS-branded.
                await emailService.SendAsync(
                    user.Email!,
                    "Reset your BerryMindful password",
                    "Click the link below to reset your BerryMindful password. "
                    + "This link expires in 1 hour.\n\n"
                    + link + "\n\n"
                    + "If you didn't request this, you can safely ignore this email.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Swallow so the response stays indistinguishable from the no-account case
                logger.LogError(ex, "Failed to send password reset email to user {UserId}", user.Id);
            }
        }

        // Always 204 — the response must not reveal whether an account exists
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Same error as a bad token — don't reveal whether an account exists
            return BadRequest(new { error = "Invalid or expired reset link." });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                return BadRequest(new { error = "Invalid or expired reset link." });
            }
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        // ResetPasswordAsync rotated the security stamp — evict the cached stamp and
        // revoke refresh tokens so every existing session is signed out immediately
        cache.Remove($"secstamp:{user.Id}");
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new UserDto(
            user.Id,
            user.Email!,
            user.NotificationsEnabled,
            await userManager.IsInRoleAsync(user, Roles.Admin)));
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = GenerateJwtToken(user, roles);

        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
        });
        await db.SaveChangesAsync();

        Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            Expires = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime),
        });

        return new AuthResponse(accessToken, new UserDto(
            user.Id,
            user.Email!,
            user.NotificationsEnabled,
            roles.Contains(Roles.Admin)));
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new("security_stamp", user.SecurityStamp!),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
