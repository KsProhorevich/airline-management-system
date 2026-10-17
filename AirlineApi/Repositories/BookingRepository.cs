using AirlineApi.Data;
using AirlineApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ApplicationDbContext _db;

    public BookingRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<Booking?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Bookings.AsNoTracking()
            .Include(booking => booking.Flight)
            .Include(booking => booking.Passenger)
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public Task<Booking?> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Bookings
            .Include(booking => booking.Flight)
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Booking>> ListForPassengerAsync(Guid passengerId, CancellationToken cancellationToken)
    {
        return await _db.Bookings.AsNoTracking()
            .Include(booking => booking.Flight)
            .Include(booking => booking.Passenger)
            .Where(booking => booking.PassengerId == passengerId)
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> SearchAsync(
        BookingSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = _db.Bookings.AsNoTracking().AsQueryable();
        if (criteria.FlightId is Guid flightId)
        {
            query = query.Where(booking => booking.FlightId == flightId);
        }

        if (criteria.PassengerId is Guid passengerId)
        {
            query = query.Where(booking => booking.PassengerId == passengerId);
        }

        if (criteria.Status is BookingStatus status)
        {
            query = query.Where(booking => booking.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenBy(booking => booking.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Include(booking => booking.Flight)
            .Include(booking => booking.Passenger)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<bool> ExistsForFlightAsync(Guid flightId, CancellationToken cancellationToken) =>
        _db.Bookings.AsNoTracking().AnyAsync(booking => booking.FlightId == flightId, cancellationToken);

    public Task<bool> HasActiveBookingAsync(Guid passengerId, Guid flightId, CancellationToken cancellationToken) =>
        _db.Bookings.AsNoTracking().AnyAsync(
            booking => booking.PassengerId == passengerId
                && booking.FlightId == flightId
                && booking.Status == BookingStatus.Confirmed,
            cancellationToken);

    public Task AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        _db.Bookings.Add(booking);
        return Task.CompletedTask;
    }
}
