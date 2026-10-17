using System.ComponentModel.DataAnnotations;

namespace AirlineClient.Models;

public sealed class LoginForm
{
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Укажите пароль.")]
    public string Password { get; set; } = "";
}

public sealed class RegisterForm
{
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Укажите пароль.")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать не меньше 8 символов.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Укажите имя.")]
    [MinLength(2, ErrorMessage = "Имя должно содержать не меньше 2 символов.")]
    public string FullName { get; set; } = "";
}

public sealed class FlightForm : IValidatableObject
{
    [Required(ErrorMessage = "Укажите номер рейса.")]
    [RegularExpression(@"^[A-Za-z]{2}\d{1,4}$", ErrorMessage = "Номер рейса: две латинские буквы и от 1 до 4 цифр.")]
    public string FlightNumber { get; set; } = "";

    [Required(ErrorMessage = "Укажите аэропорт вылета.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Код аэропорта вылета: три латинские буквы.")]
    public string DepartureAirport { get; set; } = "";

    [Required(ErrorMessage = "Укажите аэропорт прилёта.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Код аэропорта прилёта: три латинские буквы.")]
    public string ArrivalAirport { get; set; } = "";

    public DateTime DepartureTime { get; set; } = DateTime.UtcNow.AddDays(7);

    public DateTime ArrivalTime { get; set; } = DateTime.UtcNow.AddDays(7).AddHours(2);

    public string Status { get; set; } = "Scheduled";

    [Range(1, 500, ErrorMessage = "Число мест должно быть от 1 до 500.")]
    public int TotalSeats { get; set; } = 100;

    public Guid? AircraftId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(DepartureAirport?.Trim(), ArrivalAirport?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("Аэропорты вылета и прилёта должны различаться.", [nameof(ArrivalAirport)]);
        }

        if (ArrivalTime <= DepartureTime)
        {
            yield return new ValidationResult("Время прилёта должно быть позже времени вылета.", [nameof(ArrivalTime)]);
        }
    }
}

public sealed class AircraftForm
{
    [Required(ErrorMessage = "Укажите регистрационный номер.")]
    [RegularExpression(@"^[A-Za-z0-9-]{3,16}$", ErrorMessage = "Регистрационный номер: 3–16 латинских букв, цифр или дефисов.")]
    public string Registration { get; set; } = "";

    [Required(ErrorMessage = "Укажите модель.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Модель должна содержать от 2 до 80 символов.")]
    public string Model { get; set; } = "";

    [Range(1, 500, ErrorMessage = "Вместимость должна быть от 1 до 500.")]
    public int SeatCapacity { get; set; } = 180;
}

public sealed class BookingForm
{
    [Required(ErrorMessage = "Выберите рейс.")]
    public Guid? FlightId { get; set; }

    [Range(1, 9, ErrorMessage = "Число мест должно быть от 1 до 9.")]
    public int SeatCount { get; set; } = 1;

    public Guid? PassengerId { get; set; }
}
