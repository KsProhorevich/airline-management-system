using AirlineApi.DTOs.Bookings;
using AirlineApi.DTOs.Common;
using AirlineApi.Models;
using AirlineApi.Services;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Endpoints;

public static class BookingEndpoints
{
    public static RouteGroupBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookings")
            .WithTags("Bookings")
            .RequireAuthorization();

        group.MapGet("/", List)
            .RequireAuthorization(AuthorizationPolicies.DispatcherOnly)
            .WithName("ListBookings")
            .WithSummary("Список бронирований")
            .WithDescription("Только роль Dispatcher. Политика DispatcherOnly. Пассажир получает 403. Фильтры: flightId, passengerId, status. Сортировка фиксирована: дата создания по убыванию, затем идентификатор.")
            .Produces<PagedResponse<BookingResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/my", ListMine)
            .WithName("ListMyBookings")
            .WithSummary("Мои бронирования")
            .WithDescription("Возвращает только бронирования пользователя из claim sub. Идентификатор пассажира из запроса не принимается.")
            .Produces<IReadOnlyList<BookingResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", Get)
            .WithName("GetBooking")
            .WithSummary("Карточка бронирования")
            .WithDescription("Пассажир видит только своё бронирование. Чужое существующее бронирование даёт 403. Диспетчер видит любое.")
            .Produces<BookingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateBookingRequest>>()
            .WithName("CreateBooking")
            .WithSummary("Создать бронирование")
            .WithDescription("Пассажир бронирует только на себя: passengerId из тела игнорируется. Диспетчер обязан передать passengerId существующего пассажира и не может забронировать место на свой токен. Свободные места уменьшаются в транзакции. Повторное активное бронирование того же пассажира на тот же рейс запрещено. Отменённый, вылетевший, прибывший рейс и рейс с наступившим временем вылета не бронируются.")
            .Produces<BookingResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", Cancel)
            .WithName("CancelBooking")
            .WithSummary("Отменить бронирование")
            .WithDescription("Физически строка не удаляется: статус становится Cancelled, места возвращаются на рейс. Пассажир отменяет только своё бронирование. Диспетчер отменяет любое. Повторная отмена даёт 409.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> List(
        [AsParameters] BookingListQuery query,
        IBookingService bookings,
        CancellationToken cancellationToken)
    {
        var result = await bookings.ListAsync(query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListMine(IBookingService bookings, CancellationToken cancellationToken)
    {
        var result = await bookings.ListMineAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Get(Guid id, IBookingService bookings, CancellationToken cancellationToken)
    {
        var result = await bookings.GetAsync(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateBookingRequest request,
        IBookingService bookings,
        CancellationToken cancellationToken)
    {
        var result = await bookings.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/bookings/{result.Id}", result);
    }

    private static async Task<IResult> Cancel(Guid id, IBookingService bookings, CancellationToken cancellationToken)
    {
        await bookings.CancelAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
