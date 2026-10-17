using AirlineApi.DTOs.Bookings;
using AirlineApi.DTOs.Common;

namespace AirlineApi.Services;

public interface IBookingService
{
    Task<BookingResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingResponse>> ListMineAsync(CancellationToken cancellationToken);

    Task<PagedResponse<BookingResponse>> ListAsync(BookingListQuery query, CancellationToken cancellationToken);

    Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken);

    Task CancelAsync(Guid id, CancellationToken cancellationToken);
}
