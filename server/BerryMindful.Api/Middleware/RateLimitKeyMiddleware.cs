using System.Text.Json;

namespace BerryMindful.Api.Middleware;

// The "password" rate-limit policy partitions by the email being targeted, but the
// rate limiter runs before model binding — so this middleware peeks at the JSON body
// on the password endpoints and stashes the normalized email for the policy to key on.
public class RateLimitKeyMiddleware(RequestDelegate next)
{
    public const string EmailItemKey = "RateLimitEmail";

    private static readonly string[] PerEmailPaths =
    [
        "/auth/forgot-password",
        "/auth/reset-password",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            && PerEmailPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            context.Request.EnableBuffering();
            try
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(
                    context.Request.Body, cancellationToken: context.RequestAborted);
                if (body.ValueKind == JsonValueKind.Object
                    && body.TryGetProperty("email", out var email)
                    && email.ValueKind == JsonValueKind.String)
                {
                    context.Items[EmailItemKey] = email.GetString()!.Trim().ToLowerInvariant();
                }
            }
            catch (JsonException)
            {
                // Malformed body — model binding will reject it; fall back to IP keying
            }
            context.Request.Body.Position = 0;
        }

        await next(context);
    }
}
