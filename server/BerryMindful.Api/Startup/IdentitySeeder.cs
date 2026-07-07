using BerryMindful.Data;
using BerryMindful.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace BerryMindful.Api.Startup;

public static class IdentitySeeder
{
    /// <summary>
    /// Idempotent: creates the Admin role if missing and promotes every account
    /// listed under Admin:Emails. An email listed before its account exists is
    /// only warned about — restart (or wait for the next deploy) after signup.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var email in config.GetSection("Admin:Emails").Get<string[]>() ?? [])
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                logger.LogWarning("Admin seed: no account exists yet for {Email}", email);
                continue;
            }

            if (!await userManager.IsInRoleAsync(user, Roles.Admin))
            {
                await userManager.AddToRoleAsync(user, Roles.Admin);
                logger.LogInformation("Admin seed: granted Admin to {Email}", email);
            }
        }
    }
}
