using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BerryMindful.Data;

// Design-time factory so `dotnet ef` can build the model without the API host.
// The connection string here is only used for migration generation, never at runtime.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                "server=localhost;port=3306;database=berrymindful;user=root;password=design-time-only",
                new MySqlServerVersion(new Version(8, 3, 0)))
            .Options;

        return new AppDbContext(options);
    }
}
