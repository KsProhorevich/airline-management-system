namespace AirlineApi.Models;

public enum FlightSortField
{
    DepartureTime,
    ArrivalTime,
    FlightNumber
}

public sealed record FlightSearchCriteria(
    int Page,
    int PageSize,
    string? DepartureAirport,
    string? ArrivalAirport,
    FlightStatus? Status,
    FlightSortField SortField,
    bool Descending,
    Guid? AircraftId);

public sealed record BookingSearchCriteria(
    int Page,
    int PageSize,
    Guid? FlightId,
    Guid? PassengerId,
    BookingStatus? Status);
