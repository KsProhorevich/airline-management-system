using AirlineApi.Data;
using AirlineApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Repositories;

public sealed class AircraftRepository : IAircraftRepository
{
    private readonly ApplicationDbContext _db;

    public AircraftRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<Aircraft?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? _db.Aircraft.AsQueryable() : _db.Aircraft.AsNoTracking();
        return query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<Aircraft?> GetFirstAsync(CancellationToken cancellationToken) =>
        _db.Aircraft.AsNoTracking().OrderBy(item => item.Registration).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> RegistrationExistsAsync(string registration, Guid? exceptId, CancellationToken cancellationToken)
    {
        var query = _db.Aircraft.AsNoTracking().Where(item => item.Registration == registration);
        if (exceptId is Guid id)
        {
            query = query.Where(item => item.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasFlightsAsync(Guid aircraftId, CancellationToken cancellationToken) =>
        _db.Flights.AsNoTracking().AnyAsync(flight => flight.AircraftId == aircraftId, cancellationToken);

    public async Task<(IReadOnlyList<Aircraft> Items, int TotalCount)> SearchAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.Aircraft.AsNoTracking().OrderBy(item => item.Registration);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task AddAsync(Aircraft aircraft, CancellationToken cancellationToken)
    {
        _db.Aircraft.Add(aircraft);
        return Task.CompletedTask;
    }

    public void Remove(Aircraft aircraft) => _db.Aircraft.Remove(aircraft);
}
