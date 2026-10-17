using AirlineApi.Models;

namespace AirlineApi.Repositories;

public interface IFlightRepository
{
    Task<Flight?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<bool> FlightNumberExistsAsync(string flightNumber, Guid? exceptId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Flight> Items, int TotalCount)> SearchAsync(
        FlightSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task AddAsync(Flight flight, CancellationToken cancellationToken);

    void Remove(Flight flight);

    Task<int> MaxTotalSeatsAsync(Guid aircraftId, CancellationToken cancellationToken);
}
