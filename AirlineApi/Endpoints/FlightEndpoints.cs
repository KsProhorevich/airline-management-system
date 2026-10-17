using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Flights;
using AirlineApi.Models;
using AirlineApi.Services;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public static class FlightEndpoints
{
    public static RouteGroupBuilder MapFlightEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/flights").WithTags("Flights");

        group.MapGet("/", List)
            .AllowAnonymous()
            .WithName("ListFlights")
            .WithSummary("Список рейсов")
            .WithDescription("Доступно без аутентификации. Параметры: page (от 1), pageSize (1..50), departureAirport, arrivalAirport, status, sortBy (departureTime, arrivalTime, flightNumber), sortDir (asc или desc). Сортировка по умолчанию: departureTime asc, затем номер рейса. Неизвестное поле сортировки отклоняется и не подставляется в SQL.")
            .Produces<PagedResponse<FlightResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", Get)
            .AllowAnonymous()
            .WithName("GetFlight")
            .WithSummary("Карточка рейса")
            .Produces<FlightResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateFlightRequest>>()
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("CreateFlight")
            .WithSummary("Создать рейс")
            .WithDescription("Только роль Dispatcher. Политика DispatcherOnly. Число свободных мест устанавливается равным totalSeats и клиентом не передаётся.")
            .Produces<FlightResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", Update)
            .AddEndpointFilter<ValidationFilter<UpdateFlightRequest>>()
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("UpdateFlight")
            .WithSummary("Изменить рейс")
            .WithDescription("Только роль Dispatcher. Вместимость нельзя уменьшить ниже уже занятых мест. Свободные места пересчитываются на сервере.")
            .Produces<FlightResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", Delete)
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("DeleteFlight")
            .WithSummary("Удалить рейс")
            .WithDescription("Только роль Dispatcher. Удаление запрещено, если на рейс есть любое бронирование, включая отменённое.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> List(
        [AsParameters] FlightListQuery query,
        IFlightService flights,
        CancellationToken cancellationToken)
    {
        var result = await flights.ListAsync(query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Get(Guid id, IFlightService flights, CancellationToken cancellationToken)
    {
        var result = await flights.GetAsync(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateFlightRequest request,
        IFlightService flights,
        CancellationToken cancellationToken)
    {
        var result = await flights.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/flights/{result.Id}", result);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateFlightRequest request,
        IFlightService flights,
        CancellationToken cancellationToken)
    {
        var result = await flights.UpdateAsync(id, request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Delete(Guid id, IFlightService flights, CancellationToken cancellationToken)
    {
        await flights.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
