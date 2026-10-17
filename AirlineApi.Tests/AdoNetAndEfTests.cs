using AirlineApi.Data;
using AirlineApi.Models;
using AirlineApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AirlineApi.Tests;

public sealed class AdoNetAndEfTests
{
    [Fact]
    public async Task Parameterized_query_counts_only_the_requested_status()
    {
        await using var scope = await TempDatabase.CreateAsync();
        scope.Context.Flights.Add(Flight("SU100", FlightStatus.Scheduled));
        scope.Context.Flights.Add(Flight("SU210", FlightStatus.Delayed));
        await scope.Context.SaveChangesAsync();
        var demo = new AdoNetDemoService(scope.Context, NullLogger<AdoNetDemoService>.Instance);

        var injected = await demo.CountFlightsByStatusAsync("Scheduled' OR '1'='1", CancellationToken.None);
        var scheduled = await demo.CountFlightsByStatusAsync(nameof(FlightStatus.Scheduled), CancellationToken.None);

        Assert.Empty(injected);
        var row = Assert.Single(scheduled);
        Assert.Equal(nameof(FlightStatus.Scheduled), row.Status);
        Assert.Equal(1, row.Count);
    }

    [Fact]
    public async Task Failed_transaction_rolls_back_registration_change()
    {
        await using var scope = await TempDatabase.CreateAsync();
        var aircraft = await scope.Context.Aircraft.SingleAsync(item => item.Id == Aircraft.FallbackId);
        var original = aircraft.Registration;
        var demo = new AdoNetDemoService(scope.Context, NullLogger<AdoNetDemoService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            demo.RenameRegistrationAsync(aircraft.Id, "RA-9999", failBeforeCommit: true, CancellationToken.None));

        await using (var check = scope.Open())
        {
            var stored = await check.Aircraft.AsNoTracking().SingleAsync(item => item.Id == aircraft.Id);
            Assert.Equal(original, stored.Registration);
        }

        await demo.RenameRegistrationAsync(aircraft.Id, "RA-9999", failBeforeCommit: false, CancellationToken.None);
        await using var committed = scope.Open();
        var updated = await committed.Aircraft.AsNoTracking().SingleAsync(item => item.Id == aircraft.Id);
        Assert.Equal("RA-9999", updated.Registration);
    }

    [Fact]
    public async Task Ef_demo_tracks_states_and_bulk_update_delete()
    {
        await using var scope = await TempDatabase.CreateAsync();

        var notes = await EfCoreFeatureDemo.RunAsync(scope.Context, CancellationToken.None);

        Assert.Contains(EntityState.Added.ToString(), notes);
        Assert.Contains(EntityState.Unchanged.ToString(), notes);
        Assert.Contains(EntityState.Modified.ToString(), notes);
        Assert.Contains("Found", notes);
        Assert.Contains("ExecuteUpdate", notes);
        Assert.Contains("Deleted", notes);
    }

    private static Flight Flight(string number, FlightStatus status) => new()
    {
        Id = Guid.NewGuid(),
        FlightNumber = number,
        DepartureAirport = "SVO",
        ArrivalAirport = "LED",
        DepartureTime = DateTime.UtcNow.AddDays(3),
        ArrivalTime = DateTime.UtcNow.AddDays(3).AddHours(2),
        Status = status,
        TotalSeats = 20,
        AvailableSeats = 20,
        AircraftId = Aircraft.FallbackId
    };

    private sealed class TempDatabase : IAsyncDisposable
    {
        private readonly string _path;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private TempDatabase(ApplicationDbContext context, DbContextOptions<ApplicationDbContext> options, string path)
        {
            Context = context;
            _options = options;
            _path = path;
        }

        public ApplicationDbContext Context { get; }

        public static async Task<TempDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"airline-ado-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={path};Pooling=false")
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.MigrateAsync();
            return new TempDatabase(context, options, path);
        }

        public ApplicationDbContext Open() => new(_options);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var file = _path + suffix;
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
