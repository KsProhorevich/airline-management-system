using AirlineApi.Services;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public static class AdoNetDemoEndpoints
{
    public static RouteGroupBuilder MapAdoNetDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/demo/ado")
            .WithTags("ADO.NET")
            .RequireAuthorization(Models.AuthorizationPolicies.DispatcherOnly);

        group.MapGet("/flights-by-status", Count)
            .WithName("AdoNetFlightCounts")
            .WithSummary("Подсчёт рейсов прямым SQL")
            .WithDescription("Только Dispatcher. Параметризованный запрос ADO.NET, без произвольного SQL от клиента.")
            .Produces<IReadOnlyList<FlightStatusCount>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> Count(string? status, IAdoNetDemoService demo, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) && status.Length > 32)
        {
            return Results.Problem(title: "Некорректный запрос", detail: "Статус слишком длинный.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(await demo.CountFlightsByStatusAsync(status, cancellationToken));
    }
}
