namespace AirlineClient.Models;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }
}

public sealed class AuthResult
{
    public string AccessToken { get; set; } = "";

    public DateTime ExpiresAtUtc { get; set; }

    public UserInfo User { get; set; } = new();
}

public sealed class UserInfo
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    public string FullName { get; set; } = "";

    public string Role { get; set; } = "";
}

public sealed class FlightItem
{
    public Guid Id { get; set; }

    public string FlightNumber { get; set; } = "";

    public string DepartureAirport { get; set; } = "";

    public string ArrivalAirport { get; set; } = "";

    public DateTime DepartureTime { get; set; }

    public DateTime ArrivalTime { get; set; }

    public string Status { get; set; } = "";

    public int TotalSeats { get; set; }

    public int AvailableSeats { get; set; }

    public Guid AircraftId { get; set; }

    public string AircraftRegistration { get; set; } = "";

    public string AircraftModel { get; set; } = "";
}

public sealed class AircraftItem
{
    public Guid Id { get; set; }

    public string Registration { get; set; } = "";

    public string Model { get; set; } = "";

    public int SeatCapacity { get; set; }
}

public sealed class BookingItem
{
    public Guid Id { get; set; }

    public Guid FlightId { get; set; }

    public string FlightNumber { get; set; } = "";

    public Guid PassengerId { get; set; }

    public string PassengerEmail { get; set; } = "";

    public string PassengerName { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = "";

    public int SeatCount { get; set; }
}

public sealed class PassengerItem
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    public string FullName { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public sealed class ProblemPayload
{
    public string? Title { get; set; }

    public string? Detail { get; set; }

    public int? Status { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}
