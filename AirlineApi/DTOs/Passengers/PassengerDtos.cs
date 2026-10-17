namespace AirlineApi.DTOs.Passengers;

public sealed class PassengerListQuery
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? Search { get; set; }
}

public sealed class PassengerResponse
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public required DateTime CreatedAt { get; init; }
}
