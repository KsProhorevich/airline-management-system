using AirlineApi.DTOs.Aircraft;
using AirlineApi.DTOs.Common;
using AirlineApi.DTOs.Passengers;
using AirlineApi.Models;
using AirlineApi.Services;

namespace AirlineApi.Tests;

public sealed class AircraftAndPassengerEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AircraftAndPassengerEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_list_includes_fallback_aircraft_and_mutations_require_dispatcher()
    {
        var list = await _client.GetAsync("/api/aircraft");
        var body = await list.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(Aircraft.FallbackRegistration, body, StringComparison.Ordinal);

        var anonymous = await _client.PostAsJsonAsync("/api/aircraft", NewAircraft(), JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var passenger = await _client.RegisterPassengerAsync();
        _client.UseBearer(passenger.AccessToken);
        var forbidden = await _client.PostAsJsonAsync("/api/aircraft", NewAircraft(), JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        _client.UseBearer(dispatcher.AccessToken);
        var created = await _client.PostAsJsonAsync("/api/aircraft", NewAircraft(), JsonDefaults.Options);
        var createdBody = await created.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var aircraft = JsonSerializer.Deserialize<AircraftResponse>(createdBody, JsonDefaults.Options);
        Assert.NotNull(aircraft);

        var passengers = await _client.GetAsync("/api/passengers");
        var passengerBody = await passengers.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.OK, passengers.StatusCode);
        Assert.DoesNotContain("passwordHash", passengerBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", passengerBody, StringComparison.OrdinalIgnoreCase);
        var page = JsonSerializer.Deserialize<PagedResponse<PassengerResponse>>(passengerBody, JsonDefaults.Options);
        Assert.NotNull(page);
        Assert.Contains(page.Items, item => item.Email == passenger.User.Email);
    }

    [Fact]
    public async Task Flight_without_aircraft_uses_fallback_and_delete_is_blocked_while_referenced()
    {
        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        var flight = await _client.CreateFlightAsync(dispatcher.AccessToken);
        Assert.Equal(Aircraft.FallbackId, flight.AircraftId);
        Assert.Equal(Aircraft.FallbackRegistration, flight.AircraftRegistration);

        _client.UseBearer(dispatcher.AccessToken);
        var blocked = await _client.DeleteAsync($"/api/aircraft/{Aircraft.FallbackId}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task Ado_status_query_is_dispatcher_only_and_uses_parameters()
    {
        var passenger = await _client.RegisterPassengerAsync();
        _client.UseBearer(passenger.AccessToken);
        var forbidden = await _client.GetAsync("/api/demo/ado/flights-by-status?status=Scheduled");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var dispatcher = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        await _client.CreateFlightAsync(dispatcher.AccessToken);
        _client.UseBearer(dispatcher.AccessToken);
        var response = await _client.GetAsync("/api/demo/ado/flights-by-status?status=Scheduled");
        var counts = await response.ReadJsonAsync<List<FlightStatusCount>>();
        Assert.Contains(counts, item => item.Status == nameof(FlightStatus.Scheduled) && item.Count >= 1);
    }

    private static UpsertAircraftRequest NewAircraft() => new()
    {
        Registration = $"RA-{Random.Shared.Next(10000, 99999)}",
        Model = "Sukhoi Superjet",
        SeatCapacity = 98
    };
}
