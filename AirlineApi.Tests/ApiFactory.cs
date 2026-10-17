using AirlineApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AirlineApi.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "unit-test-signing-key-not-for-production-01";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"airline-tests-{Guid.NewGuid():N}.db");

    public ApiFactory()
    {
        DispatcherEmail = $"dispatcher-{Guid.NewGuid():N}@test.local";
        DispatcherPassword = $"Dp_{Guid.NewGuid():N}a1";
    }

    public string DispatcherEmail { get; }

    public string DispatcherPassword { get; }

    public CollectingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath};Pooling=false",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = "AirlineApi",
                ["Jwt:Audience"] = "AirlineApiClients",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Admin:Email"] = DispatcherEmail,
                ["Admin:Password"] = DispatcherPassword,
                ["Admin:FullName"] = "Test Dispatcher",
                ["Database:SeedSampleData"] = "false",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["Testing:ExposeUnhandledEndpoint"] = "true"
            });
        });
        builder.ConfigureServices(services =>
        {
            var registrations = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(ApplicationDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                    || descriptor.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var registration in registrations)
            {
                services.Remove(registration);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath};Pooling=false"));
        });
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(Logs);
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                var candidate = _dbPath + suffix;
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
