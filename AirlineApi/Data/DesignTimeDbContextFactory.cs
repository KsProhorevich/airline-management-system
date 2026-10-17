using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AirlineApi.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var dataDirectory = Path.Combine(root, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={Path.Combine(dataDirectory, "airline.db")}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
