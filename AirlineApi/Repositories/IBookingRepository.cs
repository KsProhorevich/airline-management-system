using AirlineApi.Models;

namespace AirlineApi.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<Booking?> GetTrackedAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Booking>> ListForPassengerAsync(Guid passengerId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Booking> Items, int TotalCount)> SearchAsync(
        BookingSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<bool> ExistsForFlightAsync(Guid flightId, CancellationToken cancellationToken);

    Task<bool> HasActiveBookingAsync(Guid passengerId, Guid flightId, CancellationToken cancellationToken);

    Task AddAsync(Booking booking, CancellationToken cancellationToken);
}
