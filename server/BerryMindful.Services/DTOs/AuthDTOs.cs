using System.ComponentModel.DataAnnotations;

namespace BerryMindful.Services.DTOs;

public record SignupRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

public record UserDto(string Id, string Email, bool NotificationsEnabled, bool IsAdmin);

public record AuthResponse(string AccessToken, UserDto User);
