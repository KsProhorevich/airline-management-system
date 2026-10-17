using System.ComponentModel.DataAnnotations;
using AirlineApi.Models;

namespace AirlineApi.DTOs.Flights;

public sealed class FlightListQuery
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public Guid? AircraftId { get; set; }

    public string? DepartureAirport { get; set; }

    public string? ArrivalAirport { get; set; }

    public string? Status { get; set; }

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }
}

public sealed class CreateFlightRequest : IValidatableObject
{
    [Required(ErrorMessage = "Укажите номер рейса.")]
    [RegularExpression(@"^[A-Za-z]{2}\d{1,4}$", ErrorMessage = "Номер рейса: две латинские буквы и от 1 до 4 цифр, например SU100.")]
    public string FlightNumber { get; set; } = "";

    [Required(ErrorMessage = "Укажите аэропорт вылета.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Аэропорт вылета: код IATA из трёх латинских букв.")]
    public string DepartureAirport { get; set; } = "";

    [Required(ErrorMessage = "Укажите аэропорт прилёта.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Аэропорт прилёта: код IATA из трёх латинских букв.")]
    public string ArrivalAirport { get; set; } = "";

    [Required(ErrorMessage = "Укажите время вылета.")]
    public DateTime DepartureTime { get; set; }

    [Required(ErrorMessage = "Укажите время прилёта.")]
    public DateTime ArrivalTime { get; set; }

    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    [Range(1, 500, ErrorMessage = "Число мест должно быть от 1 до 500.")]
    public int TotalSeats { get; set; }

    public Guid? AircraftId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(DepartureAirport)
            && !string.IsNullOrWhiteSpace(ArrivalAirport)
            && string.Equals(DepartureAirport.Trim(), ArrivalAirport.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Аэропорт вылета и аэропорт прилёта должны различаться.",
                [nameof(ArrivalAirport)]);
        }

        if (DepartureTime != default && ArrivalTime != default && ArrivalTime <= DepartureTime)
        {
            yield return new ValidationResult(
                "Время прилёта должно быть позже времени вылета.",
                [nameof(ArrivalTime)]);
        }
    }
}

public sealed class UpdateFlightRequest : IValidatableObject
{
    [Required(ErrorMessage = "Укажите номер рейса.")]
    [RegularExpression(@"^[A-Za-z]{2}\d{1,4}$", ErrorMessage = "Номер рейса: две латинские буквы и от 1 до 4 цифр, например SU100.")]
    public string FlightNumber { get; set; } = "";

    [Required(ErrorMessage = "Укажите аэропорт вылета.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Аэропорт вылета: код IATA из трёх латинских букв.")]
    public string DepartureAirport { get; set; } = "";

    [Required(ErrorMessage = "Укажите аэропорт прилёта.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Аэропорт прилёта: код IATA из трёх латинских букв.")]
    public string ArrivalAirport { get; set; } = "";

    [Required(ErrorMessage = "Укажите время вылета.")]
    public DateTime DepartureTime { get; set; }

    [Required(ErrorMessage = "Укажите время прилёта.")]
    public DateTime ArrivalTime { get; set; }

    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    [Range(1, 500, ErrorMessage = "Число мест должно быть от 1 до 500.")]
    public int TotalSeats { get; set; }

    public Guid? AircraftId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(DepartureAirport)
            && !string.IsNullOrWhiteSpace(ArrivalAirport)
            && string.Equals(DepartureAirport.Trim(), ArrivalAirport.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Аэропорт вылета и аэропорт прилёта должны различаться.",
                [nameof(ArrivalAirport)]);
        }

        if (DepartureTime != default && ArrivalTime != default && ArrivalTime <= DepartureTime)
        {
            yield return new ValidationResult(
                "Время прилёта должно быть позже времени вылета.",
                [nameof(ArrivalTime)]);
        }
    }
}

public sealed class FlightResponse
{
    public required Guid Id { get; init; }

    public required string FlightNumber { get; init; }

    public required string DepartureAirport { get; init; }

    public required string ArrivalAirport { get; init; }

    public required DateTime DepartureTime { get; init; }

    public required DateTime ArrivalTime { get; init; }

    public required string Status { get; init; }

    public required int TotalSeats { get; init; }

    public required int AvailableSeats { get; init; }

    public required Guid AircraftId { get; init; }

    public required string AircraftRegistration { get; init; }

    public required string AircraftModel { get; init; }
}
