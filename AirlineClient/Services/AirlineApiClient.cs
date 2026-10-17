using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AirlineClient.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace AirlineClient.Services;

public sealed class ApiCallException : Exception
{
    public ApiCallException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class AuthSession
{
    private const string StorageKey = "airline-auth";
    private readonly ProtectedSessionStorage _storage;

    public AuthSession(ProtectedSessionStorage storage)
    {
        _storage = storage;
    }

    public string? Token { get; private set; }

    public string? Role { get; private set; }

    public string? Email { get; private set; }

    public string? FullName { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token) && ExpiresAtUtc > DateTime.UtcNow;

    public bool IsDispatcher => IsAuthenticated && string.Equals(Role, "Dispatcher", StringComparison.Ordinal);

    public bool IsPassenger => IsAuthenticated && string.Equals(Role, "Passenger", StringComparison.Ordinal);

    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            var stored = await _storage.GetAsync<StoredAuth>(StorageKey);
            if (stored.Success && stored.Value is { } value && value.ExpiresAtUtc > DateTime.UtcNow && !string.IsNullOrWhiteSpace(value.Token))
            {
        Apply(value);
    }
            else if (stored.Success)
            {
                await ClearAsync();
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (CryptographicException)
        {
            Token = null;
        }
    }

    public async Task SignInAsync(AuthResult result)
    {
        var stored = new StoredAuth
        {
            Token = result.AccessToken,
            Role = result.User.Role,
            Email = result.User.Email,
            FullName = result.User.FullName,
            UserId = result.User.Id,
            ExpiresAtUtc = result.ExpiresAtUtc
        };
        Apply(stored);
        await _storage.SetAsync(StorageKey, stored);
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        Token = null;
        Role = null;
        Email = null;
        FullName = null;
        UserId = Guid.Empty;
        ExpiresAtUtc = default;
        try
        {
            await _storage.DeleteAsync(StorageKey);
        }
        catch (InvalidOperationException)
        {
        }

        Changed?.Invoke();
    }

    private void Apply(StoredAuth value)
    {
        Token = value.Token;
        Role = value.Role;
        Email = value.Email;
        FullName = value.FullName;
        UserId = value.UserId;
        ExpiresAtUtc = value.ExpiresAtUtc;
    }

    public sealed class StoredAuth
    {
        public string Token { get; set; } = "";

        public string Role { get; set; } = "";

        public string Email { get; set; } = "";

        public string FullName { get; set; } = "";

        public Guid UserId { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }
}

public sealed class AirlineApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly AuthSession _session;

    public AirlineApiClient(IHttpClientFactory factory, AuthSession session)
    {
        _http = factory.CreateClient("airline");
        _session = session;
    }

    public Task<AuthResult> LoginAsync(LoginForm form, CancellationToken cancellationToken) =>
        SendAsync<AuthResult>(HttpMethod.Post, "api/auth/login", new { form.Email, form.Password }, authorize: false, cancellationToken);

    public Task<AuthResult> RegisterAsync(RegisterForm form, CancellationToken cancellationToken) =>
        SendAsync<AuthResult>(HttpMethod.Post, "api/auth/register", new { form.Email, form.Password, form.FullName }, authorize: false, cancellationToken);

    public Task<PagedResult<FlightItem>> ListFlightsAsync(int page, int pageSize, string? departure, string? arrival, string? status, string? sortBy, string? sortDir, CancellationToken cancellationToken)
    {
        var query = Query(
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
            ("departureAirport", departure),
            ("arrivalAirport", arrival),
            ("status", status),
            ("sortBy", sortBy),
            ("sortDir", sortDir));
        return SendAsync<PagedResult<FlightItem>>(HttpMethod.Get, "api/flights" + query, null, authorize: false, cancellationToken);
    }

    public Task<FlightItem> GetFlightAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<FlightItem>(HttpMethod.Get, $"api/flights/{id}", null, authorize: false, cancellationToken);

    public Task<FlightItem> CreateFlightAsync(FlightForm form, CancellationToken cancellationToken) =>
        SendAsync<FlightItem>(HttpMethod.Post, "api/flights", FlightBody(form), authorize: true, cancellationToken);

    public Task<FlightItem> UpdateFlightAsync(Guid id, FlightForm form, CancellationToken cancellationToken) =>
        SendAsync<FlightItem>(HttpMethod.Put, $"api/flights/{id}", FlightBody(form), authorize: true, cancellationToken);

    public Task DeleteFlightAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"api/flights/{id}", null, authorize: true, cancellationToken);

    public Task<PagedResult<AircraftItem>> ListAircraftAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        SendAsync<PagedResult<AircraftItem>>(HttpMethod.Get, $"api/aircraft?page={page}&pageSize={pageSize}", null, authorize: false, cancellationToken);

    public Task<AircraftItem> GetAircraftAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<AircraftItem>(HttpMethod.Get, $"api/aircraft/{id}", null, authorize: false, cancellationToken);

    public Task<AircraftItem> CreateAircraftAsync(AircraftForm form, CancellationToken cancellationToken) =>
        SendAsync<AircraftItem>(HttpMethod.Post, "api/aircraft", form, authorize: true, cancellationToken);

    public Task<AircraftItem> UpdateAircraftAsync(Guid id, AircraftForm form, CancellationToken cancellationToken) =>
        SendAsync<AircraftItem>(HttpMethod.Put, $"api/aircraft/{id}", form, authorize: true, cancellationToken);

    public Task DeleteAircraftAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"api/aircraft/{id}", null, authorize: true, cancellationToken);

    public Task<List<BookingItem>> ListMyBookingsAsync(CancellationToken cancellationToken) =>
        SendAsync<List<BookingItem>>(HttpMethod.Get, "api/bookings/my", null, authorize: true, cancellationToken);

    public Task<PagedResult<BookingItem>> ListBookingsAsync(int page, CancellationToken cancellationToken) =>
        SendAsync<PagedResult<BookingItem>>(HttpMethod.Get, $"api/bookings?page={page}&pageSize=10", null, authorize: true, cancellationToken);

    public Task<BookingItem> GetBookingAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<BookingItem>(HttpMethod.Get, $"api/bookings/{id}", null, authorize: true, cancellationToken);

    public Task<BookingItem> CreateBookingAsync(BookingForm form, CancellationToken cancellationToken) =>
        SendAsync<BookingItem>(HttpMethod.Post, "api/bookings", new
        {
            flightId = form.FlightId,
            seatCount = form.SeatCount,
            passengerId = form.PassengerId
        }, authorize: true, cancellationToken);

    public Task CancelBookingAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"api/bookings/{id}", null, authorize: true, cancellationToken);

    public Task<PagedResult<PassengerItem>> ListPassengersAsync(string? search, CancellationToken cancellationToken) =>
        SendAsync<PagedResult<PassengerItem>>(HttpMethod.Get, "api/passengers" + Query(("page", "1"), ("pageSize", "50"), ("search", search)), null, authorize: true, cancellationToken);

    private static object FlightBody(FlightForm form) => new
    {
        flightNumber = form.FlightNumber,
        departureAirport = form.DepartureAirport,
        arrivalAirport = form.ArrivalAirport,
        departureTime = DateTime.SpecifyKind(form.DepartureTime, DateTimeKind.Utc),
        arrivalTime = DateTime.SpecifyKind(form.ArrivalTime, DateTimeKind.Utc),
        status = form.Status,
        totalSeats = form.TotalSeats,
        aircraftId = form.AircraftId
    };

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, bool authorize, CancellationToken cancellationToken)
    {
        if (authorize && !_session.IsAuthenticated)
        {
            await _session.ClearAsync();
            throw new ApiCallException("Сеанс истёк или токен недействителен. Войдите снова.", 401);
        }

        using var request = new HttpRequestMessage(method, url);
        if (authorize && !string.IsNullOrWhiteSpace(_session.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _session.ClearAsync();
            throw new ApiCallException("Сеанс истёк или токен недействителен. Войдите снова.", 401);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiCallException(Fallback(response.StatusCode), (int)response.StatusCode);
            }

            return default!;
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiCallException(Describe(text, response.StatusCode), (int)response.StatusCode);
        }

        if (typeof(T) == typeof(object))
        {
            return default!;
        }

        var payload = JsonSerializer.Deserialize<T>(text, Json);
        if (payload is null)
        {
            throw new ApiCallException("API вернул пустой ответ.", (int)response.StatusCode);
        }

        return payload;
    }

    private static string Describe(string body, System.Net.HttpStatusCode status)
    {
        try
        {
            var problem = JsonSerializer.Deserialize<ProblemPayload>(body, Json);
            if (problem?.Errors is { Count: > 0 })
            {
                return string.Join(" ", problem.Errors.SelectMany(pair => pair.Value));
            }

            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem.Title;
            }
        }
        catch (JsonException)
        {
        }

        return Fallback(status);
    }

    private static string Fallback(System.Net.HttpStatusCode status) => status switch
    {
        System.Net.HttpStatusCode.Forbidden => "Недостаточно прав для этой операции.",
        System.Net.HttpStatusCode.NotFound => "Запись не найдена.",
        System.Net.HttpStatusCode.Conflict => "Операция конфликтует с текущими данными.",
        System.Net.HttpStatusCode.BadRequest => "Проверьте введённые данные.",
        _ => "Запрос к API завершился ошибкой."
    };

    private static string Query(params (string Name, string? Value)[] values)
    {
        var parts = values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Name)}={Uri.EscapeDataString(item.Value!)}");
        var query = string.Join("&", parts);
        return query.Length == 0 ? "" : "?" + query;
    }
}
