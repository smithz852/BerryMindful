using Microsoft.Extensions.Logging;
using ZlEmailProvider;

namespace BerryMindful.Services.NotificationServices;

// Dev fallback registered when Resend:ApiKey is not configured — logs emails to the
// console instead of sending them, same pattern as StubReceiptScanner.
public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null)
    {
        logger.LogInformation(
            "Email (not sent — no Resend key) to {To}\nSubject: {Subject}\n{Body}",
            toEmail, subject, textBody);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string resetToken) =>
        SendAsync(toEmail, "Password reset", $"Reset token: {resetToken}");

    public Task SendEmailVerificationAsync(string toEmail, string verificationToken) =>
        SendAsync(toEmail, "Email verification", $"Verification token: {verificationToken}");

    public Task SendEmailChangeConfirmationAsync(string toEmail, string newEmail, string token) =>
        SendAsync(toEmail, "Email change confirmation", $"New email: {newEmail}, token: {token}");

    public Task SendEmailChangeNoticeAsync(string toEmail) =>
        SendAsync(toEmail, "Email change notice", "An email change was requested.");
}
