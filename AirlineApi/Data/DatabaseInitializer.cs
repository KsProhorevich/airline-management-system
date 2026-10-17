using System.Net.Mail;
using System.Text.RegularExpressions;
using AirlineApi.Configuration;
using AirlineApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirlineApi.Data;

public static class DatabaseInitializer
{
    private static readonly Regex PasswordPattern = new(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly SemaphoreSlim MigrationGate = new(1, 1);

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var adminOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AirlineApi.Data.DatabaseInitializer");

        if (databaseOptions.ApplyMigrationsOnStartup)
        {
            var connectionString = db.Database.GetConnectionString();
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                SqliteConnectionStringResolver.EnsureDataDirectory(connectionString);
            }

            await MigrationGate.WaitAsync(cancellationToken);
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            finally
            {
                MigrationGate.Release();
            }

            logger.LogInformation("Database migrations applied");
        }

        await EnsureDispatcherAsync(db, adminOptions, passwordHasher, logger, cancellationToken);
        await EnsureFallbackAircraftAsync(db, logger, cancellationToken);
        if (databaseOptions.SeedSampleData)
        {
            await SeedFlightsAsync(db, logger, cancellationToken);
        }
    }

    private static async Task EnsureDispatcherAsync(
        ApplicationDbContext db,
        AdminOptions options,
        IPasswordHasher<User> passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var email = options.Email?.Trim() ?? "";
        var password = options.Password ?? "";
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin credentials are not configured. Dispatcher account was not created.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Задайте оба значения Admin:Email и Admin:Password либо оставьте оба пустыми.");
        }

        if (!IsEmail(email))
        {
            throw new InvalidOperationException("Admin:Email имеет некорректный формат.");
        }

        if (!PasswordPattern.IsMatch(password))
        {
            throw new InvalidOperationException("Admin:Password должен содержать от 8 до 100 символов, включая латинскую букву и цифру.");
        }

        var normalizedEmail = email.ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Role, Roles.Dispatcher, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Admin:Email уже занят пользователем {existing.Id} с другой ролью. Автоматическая смена роли не выполняется.");
            }

            logger.LogInformation("Dispatcher account already exists for user {UserId}", existing.Id);
            return;
        }

        var fullName = string.IsNullOrWhiteSpace(options.FullName) ? "Диспетчер" : options.FullName.Trim();
        var dispatcher = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = fullName,
            Role = Roles.Dispatcher,
            CreatedAt = DateTime.UtcNow
        };
        dispatcher.PasswordHash = passwordHasher.HashPassword(dispatcher, password);
        db.Users.Add(dispatcher);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Dispatcher account created for user {UserId}", dispatcher.Id);
    }

    private static async Task EnsureFallbackAircraftAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Aircraft.AnyAsync(item => item.Id == Aircraft.FallbackId, cancellationToken))
        {
            return;
        }

        db.Aircraft.Add(new Aircraft
        {
            Id = Aircraft.FallbackId,
            Registration = Aircraft.FallbackRegistration,
            Model = "Резервное судно",
            SeatCapacity = 500
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Fallback aircraft ensured");
    }

    private static async Task SeedFlightsAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Flights.AnyAsync(cancellationToken))
        {
            return;
        }

        var airbusId = Guid.Parse("22222222-2222-2222-2222-222222222202");
        var boeingId = Guid.Parse("22222222-2222-2222-2222-222222222203");
        if (!await db.Aircraft.AnyAsync(item => item.Id == airbusId, cancellationToken))
        {
            db.Aircraft.Add(new Aircraft
            {
                Id = airbusId,
                Registration = "RA-73001",
                Model = "Airbus A320",
                SeatCapacity = 180
            });
        }

        if (!await db.Aircraft.AnyAsync(item => item.Id == boeingId, cancellationToken))
        {
            db.Aircraft.Add(new Aircraft
            {
                Id = boeingId,
                Registration = "RA-73002",
                Model = "Boeing 737-800",
                SeatCapacity = 160
            });
        }

        db.Flights.AddRange(
            Sample("11111111-1111-1111-1111-111111111101", "SU100", "SVO", "LED", new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 9, 30, 0, DateTimeKind.Utc), FlightStatus.Scheduled, 180, airbusId),
            Sample("11111111-1111-1111-1111-111111111102", "SU210", "LED", "AER", new DateTime(2026, 12, 2, 14, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 2, 18, 20, 0, DateTimeKind.Utc), FlightStatus.Delayed, 160, boeingId),
            Sample("11111111-1111-1111-1111-111111111103", "DP405", "SVO", "KZN", new DateTime(2026, 12, 3, 6, 40, 0, DateTimeKind.Utc), new DateTime(2026, 12, 3, 8, 10, 0, DateTimeKind.Utc), FlightStatus.Boarding, 100, airbusId),
            Sample("11111111-1111-1111-1111-111111111104", "UT288", "SVX", "SVO", new DateTime(2026, 12, 4, 11, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 4, 13, 15, 0, DateTimeKind.Utc), FlightStatus.Cancelled, 140, boeingId),
            Sample("11111111-1111-1111-1111-111111111105", "SU050", "SVO", "AER", new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), FlightStatus.Departed, 120, airbusId));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sample flights seeded");
    }

    private static Flight Sample(
        string id,
        string number,
        string departure,
        string arrival,
        DateTime departureTime,
        DateTime arrivalTime,
        FlightStatus status,
        int seats,
        Guid aircraftId) => new()
    {
        Id = Guid.Parse(id),
        FlightNumber = number,
        DepartureAirport = departure,
        ArrivalAirport = arrival,
        DepartureTime = departureTime,
        ArrivalTime = arrivalTime,
        Status = status,
        TotalSeats = seats,
        AvailableSeats = seats,
        Version = 0,
        AircraftId = aircraftId
    };

    private static bool IsEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return address.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
