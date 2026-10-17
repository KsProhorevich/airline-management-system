using AirlineApi.DTOs.Bookings;
using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Flights;
using AirlineApi.Models;

namespace AirlineApi.Tests;

public sealed class BookingEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public BookingEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Passenger_books_only_for_self_and_cannot_read_or_cancel_foreign_booking()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var first = await _client.RegisterPassengerAsync();
        var second = await _client.RegisterPassengerAsync();
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 8);

        var booking = await _client.CreateBookingAsync(first.AccessToken, flight.Id, seats: 2, passengerId: second.User.Id);
        Assert.Equal(first.User.Id, booking.PassengerId);
        Assert.Equal(flight.FlightNumber, booking.FlightNumber);
        Assert.Equal("Confirmed", booking.Status);

        _client.UseBearer(null);
        var anonymous = await _client.GetFlightAsync(flight.Id);
        Assert.Equal(6, anonymous.AvailableSeats);

        _client.UseBearer(second.AccessToken);
        var foreign = await _client.GetAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        var foreignCancel = await _client.DeleteAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, foreignCancel.StatusCode);
        var foreignList = await _client.GetAsync("/api/bookings/my");
        var foreignMine = await foreignList.ReadJsonAsync<List<BookingResponse>>();
        Assert.DoesNotContain(foreignMine, item => item.Id == booking.Id);

        _client.UseBearer(first.AccessToken);
        var own = await _client.GetAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var mine = await _client.GetAsync("/api/bookings/my");
        var ownMine = await mine.ReadJsonAsync<List<BookingResponse>>();
        Assert.Contains(ownMine, item => item.Id == booking.Id);

        var allForbidden = await _client.GetAsync("/api/bookings");
        Assert.Equal(HttpStatusCode.Forbidden, allForbidden.StatusCode);

        _client.UseBearer(dispatcher.AccessToken);
        var dispatcherView = await _client.GetAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.OK, dispatcherView.StatusCode);
        var list = await _client.GetAsync($"/api/bookings?passengerId={first.User.Id}&page=1&pageSize=10");
        var page = await list.ReadJsonAsync<PagedResponse<BookingResponse>>();
        Assert.Contains(page.Items, item => item.Id == booking.Id);

        var missing = await _client.GetAsync($"/api/bookings/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Booking_requires_existing_bookable_flight_and_prevents_overbooking()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var passenger = await _client.RegisterPassengerAsync();
        _client.UseBearer(passenger.AccessToken);
        var missingFlight = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = Guid.NewGuid(),
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.NotFound, missingFlight.StatusCode);

        var full = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 2);
        await _client.CreateBookingAsync(passenger.AccessToken, full.Id, seats: 2);
        _client.UseBearer(passenger.AccessToken);
        var overflow = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = full.Id,
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);
        _client.UseBearer(null);
        var after = await _client.GetFlightAsync(full.Id);
        Assert.Equal(0, after.AvailableSeats);
        Assert.InRange(after.AvailableSeats, 0, after.TotalSeats);

        var cancelled = await _client.CreateFlightAsync(dispatcher.AccessToken, request =>
        {
            request.Status = FlightStatus.Cancelled;
        });
        _client.UseBearer(passenger.AccessToken);
        var cancelledResponse = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = cancelled.Id,
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Conflict, cancelledResponse.StatusCode);

        var departed = await _client.CreateFlightAsync(dispatcher.AccessToken, request =>
        {
            request.DepartureTime = DateTime.UtcNow.AddHours(-5);
            request.ArrivalTime = DateTime.UtcNow.AddHours(-3);
            request.Status = FlightStatus.Departed;
        });
        _client.UseBearer(passenger.AccessToken);
        var departedResponse = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = departed.Id,
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Conflict, departedResponse.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_booking_is_rejected_and_cancel_restores_seats()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var passenger = await _client.RegisterPassengerAsync();
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 5);
        var booking = await _client.CreateBookingAsync(passenger.AccessToken, flight.Id, seats: 2);
        _client.UseBearer(passenger.AccessToken);
        var duplicate = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = flight.Id,
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var cancelled = await _client.DeleteAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        _client.UseBearer(null);
        var restored = await _client.GetFlightAsync(flight.Id);
        Assert.Equal(5, restored.AvailableSeats);

        _client.UseBearer(passenger.AccessToken);
        var secondCancel = await _client.DeleteAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);

        var again = await _client.CreateBookingAsync(passenger.AccessToken, flight.Id, seats: 1);
        Assert.Equal("Confirmed", again.Status);
    }

    [Fact]
    public async Task Dispatcher_must_name_a_passenger_and_can_cancel_any_booking()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var passenger = await _client.RegisterPassengerAsync();
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 6);
        _client.UseBearer(dispatcher.AccessToken);
        var missingPassenger = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = flight.Id,
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.BadRequest, missingPassenger.StatusCode);

        var created = await _client.CreateBookingAsync(dispatcher.AccessToken, flight.Id, seats: 1, passengerId: passenger.User.Id);
        Assert.Equal(passenger.User.Id, created.PassengerId);

        var deletedFlight = await _client.DeleteAsync($"/api/flights/{flight.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deletedFlight.StatusCode);

        var cancel = await _client.DeleteAsync($"/api/bookings/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        _client.UseBearer(null);
        var flightAfter = await _client.GetFlightAsync(flight.Id);
        Assert.Equal(6, flightAfter.AvailableSeats);
    }

    [Fact]
    public async Task Reducing_capacity_below_booked_seats_is_rejected()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var passenger = await _client.RegisterPassengerAsync();
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 5);
        await _client.CreateBookingAsync(passenger.AccessToken, flight.Id, seats: 2);
        _client.UseBearer(dispatcher.AccessToken);
        var update = new UpdateFlightRequest
        {
            FlightNumber = flight.FlightNumber,
            DepartureAirport = flight.DepartureAirport,
            ArrivalAirport = flight.ArrivalAirport,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            Status = FlightStatus.Scheduled,
            TotalSeats = 1
        };
        var response = await _client.PutAsJsonAsync($"/api/flights/{flight.Id}", update, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _client.UseBearer(null);
        var current = await _client.GetFlightAsync(flight.Id);
        Assert.Equal(3, current.AvailableSeats);
        Assert.Equal(5, current.TotalSeats);
    }

    [Fact]
    public async Task Parallel_bookings_do_not_oversell_the_last_seat()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var passenger = await _client.RegisterPassengerAsync();
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 1);
        using var first = _factory.CreateClient();
        using var second = _factory.CreateClient();
        first.UseBearer(passenger.AccessToken);
        second.UseBearer(passenger.AccessToken);
        var payload = new CreateBookingRequest { FlightId = flight.Id, SeatCount = 1 };
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/bookings", payload, JsonDefaults.Options),
            second.PostAsJsonAsync("/api/bookings", payload, JsonDefaults.Options));
        var codes = responses.Select(response => response.StatusCode).ToArray();
        Assert.Equal(1, codes.Count(code => code == HttpStatusCode.Created));
        Assert.Equal(1, codes.Count(code => code == HttpStatusCode.Conflict));

        _client.UseBearer(null);
        var current = await _client.GetFlightAsync(flight.Id);
        Assert.Equal(0, current.AvailableSeats);
        Assert.InRange(current.AvailableSeats, 0, current.TotalSeats);

        _client.UseBearer(passenger.AccessToken);
        var mine = await _client.GetFromJsonAsync<List<BookingResponse>>("/api/bookings/my", JsonDefaults.Options);
        Assert.NotNull(mine);
        var confirmedSeats = mine.Where(item => item.FlightId == flight.Id && item.Status == "Confirmed").Sum(item => item.SeatCount);
        Assert.Equal(1, confirmedSeats);
    }

    [Fact]
    public async Task Anonymous_booking_request_is_unauthorized()
    {
        _client.UseBearer(null);
        var response = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = Guid.NewGuid(),
            SeatCount = 1
        }, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
