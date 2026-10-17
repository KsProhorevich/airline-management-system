using AirlineApi.Configuration;
using AirlineApi.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AirlineApi.Tests;

public sealed class SecurityConfigurationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public SecurityConfigurationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public void Signing_key_rules_reject_empty_short_and_placeholder_values()
    {
        Assert.False(JwtKeyRules.IsAcceptable(null));
        Assert.False(JwtKeyRules.IsAcceptable(""));
        Assert.False(JwtKeyRules.IsAcceptable("short-key"));
        Assert.False(JwtKeyRules.IsAcceptable("CHANGE_ME_THIS_IS_A_PLACEHOLDER_SECRET_VALUE"));
        Assert.True(JwtKeyRules.IsAcceptable(ApiFactory.SigningKey));
        Assert.Contains("SigningKey", JwtKeyRules.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Unexpected_exception_details_are_not_returned_by_the_mapper()
    {
        var mapped = ExceptionProblemMapper.Map(new InvalidOperationException(@"C:\secret\airline.db password=hunter2"));
        Assert.Equal(500, mapped.Status);
        Assert.True(mapped.LogAsError);
        Assert.DoesNotContain("secret", mapped.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", mapped.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unhandled_exception_response_hides_internal_detail()
    {
        var response = await _client.GetAsync("/api/test/unhandled");
        var body = await response.ReadBodyAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("boom-secret-detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short-key")]
    [InlineData("CHANGE_ME_THIS_IS_A_PLACEHOLDER_SECRET_VALUE")]
    public void Host_does_not_start_with_unsafe_signing_key(string signingKey)
    {
        var path = Path.Combine(Path.GetTempPath(), $"airline-invalid-{Guid.NewGuid():N}.db");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={path};Pooling=false",
                    ["Jwt:SigningKey"] = signingKey,
                    ["Jwt:Issuer"] = "AirlineApi",
                    ["Jwt:Audience"] = "AirlineApiClients",
                    ["Jwt:AccessTokenMinutes"] = "60",
                    ["Database:SeedSampleData"] = "false",
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["Admin:Email"] = "",
                    ["Admin:Password"] = ""
                });
            });
        });

        var exception = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(exception);
        var text = Flatten(exception);
        Assert.Contains("SigningKey", text, StringComparison.OrdinalIgnoreCase);
        TryDelete(path);
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        var stack = new Stack<Exception>();
        stack.Push(exception);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    stack.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                stack.Push(current.InnerException);
            }
        }

        return string.Join(Environment.NewLine, messages);
    }

    private static void TryDelete(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                var candidate = path + suffix;
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
