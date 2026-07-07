using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BerryMindful.Api.Middleware;
using BerryMindful.Api.Workers;
using BerryMindful.Data;
using BerryMindful.Data.Entities;
using BerryMindful.Services.NotificationServices;
using BerryMindful.Services.ReceiptServices;
using Resend;
using ZlEmailProvider;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:Default — set it via `dotnet user-secrets set` in dev.");
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 3, 0))));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Password-reset (and other data-protection) tokens expire after 1 hour, matching
// the copy in the reset email.
builder.Services.Configure<DataProtectionTokenProviderOptions>(o =>
    o.TokenLifespan = TimeSpan.FromHours(1));

builder.Services.AddMemoryCache();

builder.Services.AddScoped<BerryMindful.Services.ReceiptServices.ReceiptService>();
builder.Services.AddScoped<BerryMindful.Services.PantryServices.PantryService>();
builder.Services.AddScoped<BerryMindful.Services.AnalyticsServices.WasteAnalyticsService>();

// Real Vision + Claude scan pipeline when both keys are configured (user-secrets in
// dev, env vars in prod); otherwise fall back to the stub so dev works without keys.
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"];
var visionCredentialsPath = builder.Configuration["GoogleVision:CredentialsPath"];
var visionConfigured = !string.IsNullOrWhiteSpace(visionCredentialsPath)
    || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
var scanPipelineConfigured = !string.IsNullOrWhiteSpace(anthropicApiKey) && visionConfigured;

if (scanPipelineConfigured)
{
    builder.Services.AddSingleton<IOcrService>(new GoogleVisionOcrService(visionCredentialsPath));
    builder.Services.AddSingleton<IReceiptParser>(new ClaudeReceiptParser(anthropicApiKey!));
    builder.Services.AddScoped<IReceiptScanner, VisionClaudeReceiptScanner>();
}
else
{
    builder.Services.AddScoped<IReceiptScanner, StubReceiptScanner>();
}

// Expiry digest emails via ZlEmailProvider + Resend when the key is configured;
// otherwise emails are logged to the console (same fallback pattern as the scanner).
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddScoped<ExpiryNotificationService>();

var resendApiKey = builder.Configuration["Resend:ApiKey"];
var emailConfigured = !string.IsNullOrWhiteSpace(resendApiKey);
if (emailConfigured)
{
    builder.Services.AddHttpClient<ResendClient>();
    builder.Services.Configure<ResendClientOptions>(o => o.ApiToken = resendApiKey!);
    builder.Services.AddTransient<IResend, ResendClient>();
    builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Email"));
    builder.Services.AddTransient<IEmailService, ResendEmailService>();
}
else
{
    builder.Services.AddTransient<IEmailService, LoggingEmailService>();
}

builder.Services.AddHostedService<ExpiryNotificationWorker>();

var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"]
    ?? throw new InvalidOperationException("Missing Jwt:Key — set it via `dotnet user-secrets set` in dev.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        options.Events = new JwtBearerEvents
        {
            // Security-stamp validation (RSS pattern): password resets and logout rotate
            // the stamp, invalidating outstanding access tokens across all devices.
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenStamp = context.Principal?.FindFirstValue("security_stamp");
                if (userId is null || tokenStamp is null)
                {
                    context.Fail("Missing required claims.");
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var currentStamp = await cache.GetOrCreateAsync($"secstamp:{userId}", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.FindByIdAsync(userId);
                    return user?.SecurityStamp;
                });

                if (currentStamp is null || currentStamp != tokenStamp)
                {
                    context.Fail("Token is no longer valid.");
                }
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddPolicy("client", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Auth endpoints: 10 req/min by IP
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
        }));

    // Forgot/reset password: 3/hour per targeted email (extracted by
    // RateLimitKeyMiddleware; falls back to IP when the body has no email)
    o.AddPolicy("password", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Items[RateLimitKeyMiddleware.EmailItemKey] as string
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromHours(1),
            PermitLimit = 3,
        }));

    // Receipt scans: 20/hour per user — the only endpoint with Vision + Claude costs
    o.AddPolicy("scan", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromHours(1),
            PermitLimit = 20,
        }));
});

var app = builder.Build();

if (scanPipelineConfigured)
{
    app.Logger.LogInformation("Receipt scanning: Vision + Claude pipeline active.");
}
else
{
    app.Logger.LogWarning(
        "Receipt scanning: using StubReceiptScanner — set Anthropic:ApiKey and "
        + "GoogleVision:CredentialsPath (dotnet user-secrets) to enable the real pipeline.");
}

if (emailConfigured)
{
    app.Logger.LogInformation("Email: Resend delivery active.");
}
else
{
    app.Logger.LogWarning(
        "Email: logging to console only — set Resend:ApiKey (dotnet user-secrets) to enable delivery.");
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();
app.UseCors("client");
app.UseAuthentication();
app.UseMiddleware<RateLimitKeyMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
