using AirlineApi.DTOs.Auth;
using AirlineApi.Services;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithName("RegisterPassenger")
            .WithSummary("Регистрация пассажира")
            .WithDescription("Публичная регистрация всегда создаёт роль Passenger. Роль Dispatcher выбрать нельзя. В ответе нет пароля и хеша пароля.")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login")
            .WithSummary("Вход и выдача JWT")
            .WithDescription("Проверяет email и пароль. Возвращает JWT с claims sub, email и role. Срок действия задаётся Jwt:AccessTokenMinutes.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<IResult> Register(RegisterRequest request, IAuthService auth, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return Results.Json(result, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(LoginRequest request, IAuthService auth, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}
