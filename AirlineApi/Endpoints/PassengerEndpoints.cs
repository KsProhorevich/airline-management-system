using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Passengers;
using AirlineApi.Models;
using AirlineApi.Services;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public static class PassengerEndpoints
{
    public static RouteGroupBuilder MapPassengerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/passengers")
            .WithTags("Passengers")
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly);

        group.MapGet("/", List)
            .WithName("ListPassengers")
            .WithSummary("Список пассажиров")
            .WithDescription("Только Dispatcher. Пароли и хеши не возвращаются.")
            .Produces<PagedResponse<PassengerResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> List(
        [AsParameters] PassengerListQuery query,
        IPassengerQueryService passengers,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await passengers.ListAsync(query, cancellationToken));
    }
}
