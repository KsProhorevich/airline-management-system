using AirlineApi.Data;
using AirlineApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Repositories;

public sealed class FlightRepository : IFlightRepository
{
    private readonly ApplicationDbContext _db;

    public FlightRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<Flight?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking
            ? _db.Flights.Include(flight => flight.Aircraft)
            : _db.Flights.AsNoTracking().Include(flight => flight.Aircraft);
        return query.FirstOrDefaultAsync(flight => flight.Id == id, cancellationToken);
    }

    public Task<bool> FlightNumberExistsAsync(string flightNumber, Guid? exceptId, CancellationToken cancellationToken)
    {
        var query = _db.Flights.AsNoTracking().Where(flight => flight.FlightNumber == flightNumber);
        if (exceptId is Guid id)
        {
            query = query.Where(flight => flight.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Flight> Items, int TotalCount)> SearchAsync(
        FlightSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = _db.Flights.AsNoTracking().AsQueryable();
        if (criteria.AircraftId is Guid aircraftId)
        {
            query = query.Where(flight => flight.AircraftId == aircraftId);
        }
        if (!string.IsNullOrWhiteSpace(criteria.DepartureAirport))
        {
            query = query.Where(flight => flight.DepartureAirport == criteria.DepartureAirport);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ArrivalAirport))
        {
            query = query.Where(flight => flight.ArrivalAirport == criteria.ArrivalAirport);
        }

        if (criteria.Status is FlightStatus status)
        {
            query = query.Where(flight => flight.Status == status);
        }

        query = (criteria.SortField, criteria.Descending) switch
        {
            (FlightSortField.ArrivalTime, false) => query.OrderBy(flight => flight.ArrivalTime).ThenBy(flight => flight.FlightNumber),
            (FlightSortField.ArrivalTime, true) => query.OrderByDescending(flight => flight.ArrivalTime).ThenBy(flight => flight.FlightNumber),
            (FlightSortField.FlightNumber, false) => query.OrderBy(flight => flight.FlightNumber),
            (FlightSortField.FlightNumber, true) => query.OrderByDescending(flight => flight.FlightNumber),
            (_, true) => query.OrderByDescending(flight => flight.DepartureTime).ThenBy(flight => flight.FlightNumber),
            _ => query.OrderBy(flight => flight.DepartureTime).ThenBy(flight => flight.FlightNumber)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Include(flight => flight.Aircraft)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task AddAsync(Flight flight, CancellationToken cancellationToken)
    {
        _db.Flights.Add(flight);
        return Task.CompletedTask;
    }

    public void Remove(Flight flight) => _db.Flights.Remove(flight);

    public async Task<int> MaxTotalSeatsAsync(Guid aircraftId, CancellationToken cancellationToken)
    {
        var seats = await _db.Flights.AsNoTracking()
            .Where(flight => flight.AircraftId == aircraftId)
            .Select(flight => (int?)flight.TotalSeats)
            .MaxAsync(cancellationToken);
        return seats ?? 0;
    }
}
