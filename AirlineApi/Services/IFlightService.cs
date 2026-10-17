using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Flights;

namespace AirlineApi.Services;

public interface IFlightService
{
    Task<PagedResponse<FlightResponse>> ListAsync(FlightListQuery query, CancellationToken cancellationToken);

    Task<FlightResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<FlightResponse> CreateAsync(CreateFlightRequest request, CancellationToken cancellationToken);

    Task<FlightResponse> UpdateAsync(Guid id, UpdateFlightRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
