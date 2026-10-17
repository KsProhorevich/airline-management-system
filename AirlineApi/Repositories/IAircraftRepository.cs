using AirlineApi.Models;

namespace AirlineApi.Repositories;

public interface IAircraftRepository
{
    Task<Aircraft?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<Aircraft?> GetFirstAsync(CancellationToken cancellationToken);

    Task<bool> RegistrationExistsAsync(string registration, Guid? exceptId, CancellationToken cancellationToken);

    Task<bool> HasFlightsAsync(Guid aircraftId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Aircraft> Items, int TotalCount)> SearchAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task AddAsync(Aircraft aircraft, CancellationToken cancellationToken);

    void Remove(Aircraft aircraft);
}
