using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AirlineApi.DTOs.Auth;
using AirlineApi.DTOs.Bookings;
using AirlineApi.DTOs.Flights;
using AirlineApi.Models;

namespace AirlineApi.Tests;

internal static class TestSequence
{
    private static int _counter;

    public static int Next() => Interlocked.Increment(ref _counter);

    public static string FlightNumber() => $"ZZ{Next() % 10000:D4}";

    public static string Airport()
    {
        var value = Next();
        return string.Create(3, value, static (buffer, number) =>
        {
            buffer[0] = (char)('A' + number % 26);
            buffer[1] = (char)('A' + (number / 26) % 26);
            buffer[2] = (char)('A' + (number / 676) % 26);
        });
    }
}

internal static class ApiTestHelpers
{
    public static void UseBearer(this HttpClient client, string? token)
    {
        client.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<string> ReadBodyAsync(this HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync();

    public static async Task<AuthResponse> RegisterPassengerAsync(this HttpClient client, string? email = null)
    {
        email ??= $"passenger-{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Passenger_1a",
            FullName = "Тестовый Пассажир"
        }, JsonDefaults.Options);
        var body = await response.ReadBodyAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var auth = JsonSerializer.Deserialize<AuthResponse>(body, JsonDefaults.Options);
        Assert.NotNull(auth);
        return auth;
    }

    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        }, JsonDefaults.Options);
        var body = await response.ReadBodyAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var auth = JsonSerializer.Deserialize<AuthResponse>(body, JsonDefaults.Options);
        Assert.NotNull(auth);
        return auth;
    }

    public static CreateFlightRequest ValidFlight(int seats = 20, DateTime? departure = null)
    {
        var from = TestSequence.Airport();
        var to = TestSequence.Airport();
        while (to == from)
        {
            to = TestSequence.Airport();
        }

        var start = departure ?? DateTime.UtcNow.AddDays(30).AddMinutes(TestSequence.Next() % 500);
        return new CreateFlightRequest
        {
            FlightNumber = TestSequence.FlightNumber(),
            DepartureAirport = from,
            ArrivalAirport = to,
            DepartureTime = start,
            ArrivalTime = start.AddHours(2),
            Status = FlightStatus.Scheduled,
            TotalSeats = seats
        };
    }

    public static async Task<FlightResponse> CreateFlightAsync(
        this HttpClient client,
        string token,
        Action<CreateFlightRequest>? adjust = null)
    {
        client.UseBearer(token);
        var request = ValidFlight();
        adjust?.Invoke(request);
        var response = await client.PostAsJsonAsync("/api/flights", request, JsonDefaults.Options);
        var body = await response.ReadBodyAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var flight = JsonSerializer.Deserialize<FlightResponse>(body, JsonDefaults.Options);
        Assert.NotNull(flight);
        return flight;
    }

    public static async Task<BookingResponse> CreateBookingAsync(
        this HttpClient client,
        string token,
        Guid flightId,
        int seats = 1,
        Guid? passengerId = null)
    {
        client.UseBearer(token);
        var response = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            FlightId = flightId,
            SeatCount = seats,
            PassengerId = passengerId
        }, JsonDefaults.Options);
        var body = await response.ReadBodyAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var booking = JsonSerializer.Deserialize<BookingResponse>(body, JsonDefaults.Options);
        Assert.NotNull(booking);
        return booking;
    }

    public static async Task<FlightResponse> GetFlightAsync(this HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/flights/{id}");
        var body = await response.ReadBodyAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var flight = JsonSerializer.Deserialize<FlightResponse>(body, JsonDefaults.Options);
        Assert.NotNull(flight);
        return flight;
    }

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.ReadBodyAsync();
        var value = JsonSerializer.Deserialize<T>(body, JsonDefaults.Options);
        Assert.NotNull(value);
        return value;
    }

    public static void AssertHasClaim(AuthResponse auth, string type, string value)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        Assert.Contains(jwt.Claims, claim =>
            claim.Value == value && (claim.Type == type || claim.Type == Map(type)));
    }

    private static string Map(string type) => type switch
    {
        "sub" => ClaimTypes.NameIdentifier,
        "role" => ClaimTypes.Role,
        "email" => ClaimTypes.Email,
        _ => type
    };
}
