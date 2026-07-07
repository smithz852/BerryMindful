using System.Security.Claims;
using BerryMindful.Data;
using BerryMindful.Data.Entities;
using BerryMindful.Services.AdminServices;
using BerryMindful.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BerryMindful.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(
    AdminService adminService,
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IMemoryCache cache) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers()
        => Ok(await adminService.GetUsersAsync());

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
        => Ok(await adminService.GetStatsAsync());

    [HttpGet("signups")]
    public async Task<ActionResult<List<WeeklySignupsDto>>> GetSignups([FromQuery] int weeks = 12)
        => Ok(await adminService.GetSignupsAsync(weeks));

    [HttpPost("users/{id}/admin-role")]
    public async Task<IActionResult> GrantAdmin(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        // No stamp rotation: the new role lands in the target's JWT at their
        // next login/refresh, so nothing is over-granted in the meantime.
        if (!await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            await userManager.AddToRoleAsync(user, Roles.Admin);
        }

        return NoContent();
    }

    [HttpDelete("users/{id}/admin-role")]
    public async Task<IActionResult> RevokeAdmin(string id)
    {
        if (id == CurrentUserId)
        {
            return BadRequest(new { error = "You can't remove your own admin role." });
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            await userManager.RemoveFromRoleAsync(user, Roles.Admin);

            // Revocation must not wait out the 15-min access token: rotating the
            // stamp invalidates outstanding access tokens (see OnTokenValidated),
            // and revoking refresh tokens forces a fresh login.
            await userManager.UpdateSecurityStampAsync(user);
            cache.Remove($"secstamp:{user.Id}");
            await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        }

        return NoContent();
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (id == CurrentUserId)
        {
            return BadRequest(new { error = "You can't delete your own account." });
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        // DB cascades remove Receipts, PantryItems, NotificationLogs, and
        // RefreshTokens. Uploaded receipt image files are orphaned (v1 gap).
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        // Evict the cached stamp so a still-live access token can't ride out
        // the 30s cache window after the account is gone.
        cache.Remove($"secstamp:{user.Id}");
        return NoContent();
    }
}
