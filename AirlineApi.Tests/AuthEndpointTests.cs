using System.Net.Http.Json;
using AirlineApi.DTOs.Auth;
using AirlineApi.Models;

namespace AirlineApi.Tests;

public sealed class AuthEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_creates_passenger_token_without_password_material()
    {
        _factory.Logs.Clear();
        var email = $"User-{Guid.NewGuid():N}@Mail.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Passenger_1a",
            FullName = "Анна Иванова"
        }, JsonDefaults.Options);

        var body = await response.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using (var document = JsonDocument.Parse(body))
        {
            Assert.False(document.RootElement.TryGetProperty("password", out _));
            Assert.False(document.RootElement.TryGetProperty("passwordHash", out _));
            Assert.False(document.RootElement.GetProperty("user").TryGetProperty("password", out _));
            Assert.False(document.RootElement.GetProperty("user").TryGetProperty("passwordHash", out _));
        }

        Assert.DoesNotContain("Passenger_1a", body, StringComparison.Ordinal);
        var auth = JsonSerializer.Deserialize<AuthResponse>(body, JsonDefaults.Options);
        Assert.NotNull(auth);
        Assert.Equal(email.ToLowerInvariant(), auth.User.Email);
        Assert.Equal(Roles.Passenger, auth.User.Role);
        Assert.Equal("Bearer", auth.TokenType);
        Assert.True(auth.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(50));
        ApiTestHelpers.AssertHasClaim(auth, "sub", auth.User.Id.ToString());
        ApiTestHelpers.AssertHasClaim(auth, "role", Roles.Passenger);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        Assert.Equal("AirlineApi", jwt.Issuer);
        Assert.Contains("AirlineApiClients", jwt.Audiences);
        Assert.DoesNotContain(_factory.Logs.Messages, message => message.Contains("Passenger_1a", StringComparison.Ordinal));
        Assert.DoesNotContain(_factory.Logs.Messages, message => message.Contains(ApiFactory.SigningKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_ignoring_case()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        await _client.RegisterPassengerAsync(email);
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email.ToUpperInvariant(),
            Password = "Passenger_1a",
            FullName = "Другой Пассажир"
        }, JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_rejects_dispatcher_role()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"admin-{Guid.NewGuid():N}@test.local",
            Password = "Passenger_1a",
            FullName = "Самоназначенный Диспетчер",
            Role = "Dispatcher"
        }, JsonDefaults.Options);

        var body = await response.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Dispatcher", body, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"short-{Guid.NewGuid():N}@test.local",
            Password = "abc",
            FullName = "Короткий Пароль"
        }, JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_returns_token_and_rejects_invalid_password_with_the_same_message_as_unknown_email()
    {
        var email = $"login-{Guid.NewGuid():N}@test.local";
        await _client.RegisterPassengerAsync(email);
        var success = await _client.LoginAsync(email, "Passenger_1a");
        ApiTestHelpers.AssertHasClaim(success, "role", Roles.Passenger);

        var invalid = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Wrong_pass_1"
        }, JsonDefaults.Options);
        var unknown = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = $"missing-{Guid.NewGuid():N}@test.local",
            Password = "Wrong_pass_1"
        }, JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        using var invalidJson = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        using var unknownJson = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        Assert.Equal(
            invalidJson.RootElement.GetProperty("detail").GetString(),
            unknownJson.RootElement.GetProperty("detail").GetString());
        Assert.False(invalidJson.RootElement.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Seeded_dispatcher_can_log_in()
    {
        var auth = await _client.LoginAsync(_factory.DispatcherEmail, _factory.DispatcherPassword);
        Assert.Equal(Roles.Dispatcher, auth.User.Role);
        ApiTestHelpers.AssertHasClaim(auth, "role", Roles.Dispatcher);
    }

    [Fact]
    public async Task Malformed_token_is_rejected()
    {
        _client.UseBearer("not-a-jwt");
        var response = await _client.GetAsync("/api/bookings/my");
        var body = await response.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ApiFactory.SigningKey, body, StringComparison.Ordinal);
    }
}
