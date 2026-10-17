using AirlineApi.DTOs.Aircraft;
using AirlineApi.DTOs.Common;
using AirlineApi.Models;
using AirlineApi.Services;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public static class AircraftEndpoints
{
    public static RouteGroupBuilder MapAircraftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/aircraft").WithTags("Aircraft");

        group.MapGet("/", List)
            .AllowAnonymous()
            .WithName("ListAircraft")
            .WithSummary("Список воздушных судов")
            .Produces<PagedResponse<AircraftResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", Get)
            .AllowAnonymous()
            .WithName("GetAircraft")
            .WithSummary("Карточка воздушного судна")
            .Produces<AircraftResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<UpsertAircraftRequest>>()
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("CreateAircraft")
            .WithSummary("Создать воздушное судно")
            .WithDescription("Только роль Dispatcher.")
            .Produces<AircraftResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", Update)
            .AddEndpointFilter<ValidationFilter<UpsertAircraftRequest>>()
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("UpdateAircraft")
            .WithSummary("Изменить воздушное судно")
            .Produces<AircraftResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", Delete)
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("DeleteAircraft")
            .WithSummary("Удалить воздушное судно")
            .WithDescription("Запрещено, если на судно назначены рейсы. Резервное судно RA-0001 не удаляется.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> List([AsParameters] AircraftListQuery query, IAircraftService aircraft, CancellationToken cancellationToken)
    {
        return Results.Ok(await aircraft.ListAsync(query, cancellationToken));
    }

    private static async Task<IResult> Get(Guid id, IAircraftService aircraft, CancellationToken cancellationToken)
    {
        return Results.Ok(await aircraft.GetAsync(id, cancellationToken));
    }

    private static async Task<IResult> Create(UpsertAircraftRequest request, IAircraftService aircraft, CancellationToken cancellationToken)
    {
        var created = await aircraft.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/aircraft/{created.Id}", created);
    }

    private static async Task<IResult> Update(Guid id, UpsertAircraftRequest request, IAircraftService aircraft, CancellationToken cancellationToken)
    {
        return Results.Ok(await aircraft.UpdateAsync(id, request, cancellationToken));
    }

    private static async Task<IResult> Delete(Guid id, IAircraftService aircraft, CancellationToken cancellationToken)
    {
        await aircraft.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
