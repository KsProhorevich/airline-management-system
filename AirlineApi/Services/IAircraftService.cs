using AirlineApi.DTOs.Aircraft;
using AirlineApi.DTOs.Common;

namespace AirlineApi.Services;

public interface IAircraftService
{
    Task<PagedResponse<AircraftResponse>> ListAsync(AircraftListQuery query, CancellationToken cancellationToken);

    Task<AircraftResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AircraftResponse> CreateAsync(UpsertAircraftRequest request, CancellationToken cancellationToken);

    Task<AircraftResponse> UpdateAsync(Guid id, UpsertAircraftRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
