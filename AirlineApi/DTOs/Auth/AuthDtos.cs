using System.ComponentModel.DataAnnotations;

namespace AirlineApi.DTOs.Auth;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    [StringLength(256, ErrorMessage = "Email не длиннее 256 символов.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Укажите пароль.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен содержать от 8 до 100 символов.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$", ErrorMessage = "Пароль должен содержать от 8 до 100 символов, включая хотя бы одну латинскую букву и одну цифру.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Укажите имя.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 200 символов.")]
    public string FullName { get; set; } = "";

    [StringLength(32, ErrorMessage = "Название роли слишком длинное.")]
    public string? Role { get; set; }
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Укажите пароль.")]
    [StringLength(100)]
    public string Password { get; set; } = "";
}

public sealed class UserResponse
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public required string Role { get; init; }
}

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public string TokenType { get; init; } = "Bearer";

    public required DateTime ExpiresAtUtc { get; init; }

    public required UserResponse User { get; init; }
}
