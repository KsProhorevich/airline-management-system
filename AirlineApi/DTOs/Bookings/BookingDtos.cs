using System.ComponentModel.DataAnnotations;

namespace AirlineApi.DTOs.Bookings;

public sealed class CreateBookingRequest
{
    [Required(ErrorMessage = "Укажите рейс.")]
    public Guid FlightId { get; set; }

    [Range(1, 9, ErrorMessage = "Число мест должно быть от 1 до 9.")]
    public int SeatCount { get; set; } = 1;

    public Guid? PassengerId { get; set; }
}

public sealed class BookingListQuery
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public Guid? FlightId { get; set; }

    public Guid? PassengerId { get; set; }

    public string? Status { get; set; }
}

public sealed class BookingResponse
{
    public required Guid Id { get; init; }

    public required Guid FlightId { get; init; }

    public required string FlightNumber { get; init; }

    public required Guid PassengerId { get; init; }

    public required string PassengerEmail { get; init; }

    public required string PassengerName { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required string Status { get; init; }

    public required int SeatCount { get; init; }
}
