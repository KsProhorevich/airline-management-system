using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Flights;
using AirlineApi.Models;

namespace AirlineApi.Tests;

public sealed class FlightEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public FlightEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_can_list_and_get_flight_but_cannot_create_one()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var created = await _client.CreateFlightAsync(dispatcher.AccessToken);
        _client.UseBearer(null);

        var list = await _client.GetAsync("/api/flights?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.ReadJsonAsync<PagedResponse<FlightResponse>>();
        Assert.Contains(page.Items, item => item.Id == created.Id);

        var details = await _client.GetFlightAsync(created.Id);
        Assert.Equal(created.FlightNumber, details.FlightNumber);
        Assert.Equal(created.TotalSeats, details.AvailableSeats);

        var missing = await _client.GetAsync($"/api/flights/{Guid.NewGuid()}");
        var missingBody = await missing.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stack", missingBody, StringComparison.OrdinalIgnoreCase);

        var denied = await _client.PostAsJsonAsync("/api/flights", ApiTestHelpers.ValidFlight(), JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Passenger_cannot_create_or_delete_flight_and_dispatcher_can_update()
    {
        var passenger = await _client.RegisterPassengerAsync();
        _client.UseBearer(passenger.AccessToken);
        var forbidden = await _client.PostAsJsonAsync("/api/flights", ApiTestHelpers.ValidFlight(), JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);

        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.TotalSeats = 40);
        var update = new UpdateFlightRequest
        {
            FlightNumber = flight.FlightNumber,
            DepartureAirport = flight.DepartureAirport,
            ArrivalAirport = flight.ArrivalAirport,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime.AddMinutes(30),
            Status = FlightStatus.Delayed,
            TotalSeats = 42
        };
        _client.UseBearer(dispatcher.AccessToken);
        var updatedResponse = await _client.PutAsJsonAsync($"/api/flights/{flight.Id}", update, JsonDefaults.Options);
        var updated = await updatedResponse.ReadJsonAsync<FlightResponse>();
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.Equal("Delayed", updated.Status);
        Assert.Equal(42, updated.TotalSeats);
        Assert.Equal(42, updated.AvailableSeats);

        _client.UseBearer(passenger.AccessToken);
        var deleteForbidden = await _client.DeleteAsync($"/api/flights/{flight.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteForbidden.StatusCode);

        _client.UseBearer(dispatcher.AccessToken);
        var deleted = await _client.DeleteAsync($"/api/flights/{flight.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        _client.UseBearer(null);
        var afterDelete = await _client.GetAsync($"/api/flights/{flight.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Invalid_flight_payload_is_rejected()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        _client.UseBearer(dispatcher.AccessToken);
        var sameAirport = ApiTestHelpers.ValidFlight();
        sameAirport.ArrivalAirport = sameAirport.DepartureAirport;
        var sameAirportResponse = await _client.PostAsJsonAsync("/api/flights", sameAirport, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.BadRequest, sameAirportResponse.StatusCode);
        Assert.Equal("application/problem+json", sameAirportResponse.Content.Headers.ContentType?.MediaType);

        var badTimes = ApiTestHelpers.ValidFlight();
        badTimes.ArrivalTime = badTimes.DepartureTime.AddHours(-1);
        var badTimesResponse = await _client.PostAsJsonAsync("/api/flights", badTimes, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.BadRequest, badTimesResponse.StatusCode);

        var badNumber = ApiTestHelpers.ValidFlight();
        badNumber.FlightNumber = "S100";
        var badNumberResponse = await _client.PostAsJsonAsync("/api/flights", badNumber, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.BadRequest, badNumberResponse.StatusCode);
    }

    [Fact]
    public async Task Duplicate_flight_number_returns_conflict()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var number = TestSequence.FlightNumber();
        await _client.CreateFlightAsync(dispatcher.AccessToken, request => request.FlightNumber = number);
        _client.UseBearer(dispatcher.AccessToken);
        var duplicate = ApiTestHelpers.ValidFlight();
        duplicate.FlightNumber = number.ToLowerInvariant();
        var response = await _client.PostAsJsonAsync("/api/flights", duplicate, JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task List_supports_pagination_filtering_and_allowlisted_sorting()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var hub = TestSequence.Airport();
        var elsewhere = TestSequence.Airport();
        while (elsewhere == hub)
        {
            elsewhere = TestSequence.Airport();
        }

        var baseTime = DateTime.UtcNow.AddDays(80);
        var earlyNumber = TestSequence.FlightNumber();
        var middleNumber = TestSequence.FlightNumber();
        var lateNumber = TestSequence.FlightNumber();
        await _client.CreateFlightAsync(dispatcher.AccessToken, request =>
        {
            request.FlightNumber = middleNumber;
            request.DepartureAirport = hub;
            request.ArrivalAirport = elsewhere;
            request.DepartureTime = baseTime.AddHours(2);
            request.ArrivalTime = baseTime.AddHours(4);
            request.Status = FlightStatus.Scheduled;
        });
        await _client.CreateFlightAsync(dispatcher.AccessToken, request =>
        {
            request.FlightNumber = lateNumber;
            request.DepartureAirport = hub;
            request.ArrivalAirport = elsewhere;
            request.DepartureTime = baseTime.AddHours(5);
            request.ArrivalTime = baseTime.AddHours(7);
            request.Status = FlightStatus.Delayed;
        });
        await _client.CreateFlightAsync(dispatcher.AccessToken, request =>
        {
            request.FlightNumber = earlyNumber;
            request.DepartureAirport = hub;
            request.ArrivalAirport = elsewhere;
            request.DepartureTime = baseTime;
            request.ArrivalTime = baseTime.AddHours(1);
            request.Status = FlightStatus.Scheduled;
        });

        _client.UseBearer(null);
        var sorted = await _client.GetAsync(
            $"/api/flights?departureAirport={hub}&arrivalAirport={elsewhere}&page=1&pageSize=2&sortBy=flightNumber&sortDir=desc");
        var page = await sorted.ReadJsonAsync<PagedResponse<FlightResponse>>();
        var expected = new[] { earlyNumber, middleNumber, lateNumber }.OrderByDescending(number => number).ToArray();
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(expected.Take(2), page.Items.Select(item => item.FlightNumber));

        var second = await _client.GetAsync(
            $"/api/flights?departureAirport={hub}&page=2&pageSize=2&sortBy=flightNumber&sortDir=desc");
        var secondPage = await second.ReadJsonAsync<PagedResponse<FlightResponse>>();
        Assert.Equal(expected.Skip(2), secondPage.Items.Select(item => item.FlightNumber));

        var byStatus = await _client.GetAsync($"/api/flights?departureAirport={hub}&status=Delayed&page=1&pageSize=10");
        var delayed = await byStatus.ReadJsonAsync<PagedResponse<FlightResponse>>();
        Assert.Equal(1, delayed.TotalCount);
        Assert.Equal(lateNumber, delayed.Items.Single().FlightNumber);

        var defaultSort = await _client.GetAsync($"/api/flights?departureAirport={hub}&page=1&pageSize=10");
        var defaultPage = await defaultSort.ReadJsonAsync<PagedResponse<FlightResponse>>();
        Assert.Equal(new[] { earlyNumber, middleNumber, lateNumber }, defaultPage.Items.Select(item => item.FlightNumber));

        var badSort = await _client.GetAsync("/api/flights?sortBy=availableSeats;drop&page=1&pageSize=10");
        var badSortBody = await badSort.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        Assert.Contains("departureTime", badSortBody, StringComparison.Ordinal);
        Assert.DoesNotContain("sqlite", badSortBody, StringComparison.OrdinalIgnoreCase);

        var badPage = await _client.GetAsync($"/api/flights?departureAirport={hub}&page=0&pageSize=10");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        var badSize = await _client.GetAsync($"/api/flights?departureAirport={hub}&page=1&pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, badSize.StatusCode);
        var badStatus = await _client.GetAsync($"/api/flights?departureAirport={hub}&status=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [Fact]
    public async Task Allowed_origin_receives_cors_header_and_foreign_origin_does_not()
    {
        var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/flights");
        allowed.Headers.Add("Origin", "http://localhost:5173");
        var allowedResponse = await _client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.Equal("http://localhost:5173", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var blocked = new HttpRequestMessage(HttpMethod.Get, "/api/flights");
        blocked.Headers.Add("Origin", "http://evil.example");
        var blockedResponse = await _client.SendAsync(blocked);
        Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
