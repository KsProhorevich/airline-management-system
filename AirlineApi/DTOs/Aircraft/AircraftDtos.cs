using System.ComponentModel.DataAnnotations;

namespace AirlineApi.DTOs.Aircraft;

public sealed class AircraftListQuery
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

public sealed class UpsertAircraftRequest
{
    [Required(ErrorMessage = "Укажите регистрационный номер.")]
    [RegularExpression(@"^[A-Za-z0-9-]{3,16}$", ErrorMessage = "Регистрационный номер: 3–16 латинских букв, цифр или дефисов.")]
    public string Registration { get; set; } = "";

    [Required(ErrorMessage = "Укажите модель.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Модель должна содержать от 2 до 80 символов.")]
    public string Model { get; set; } = "";

    [Range(1, 500, ErrorMessage = "Вместимость должна быть от 1 до 500.")]
    public int SeatCapacity { get; set; }
}

public sealed class AircraftResponse
{
    public required Guid Id { get; init; }

    public required string Registration { get; init; }

    public required string Model { get; init; }

    public required int SeatCapacity { get; init; }
}
