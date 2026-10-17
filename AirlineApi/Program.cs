using AirlineApi.Configuration;
using AirlineApi.Data;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddAirlineServices();

var app = builder.Build();
var jwt = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
app.Logger.LogInformation(
    "Airline API starting. Environment: {Environment}. JWT issuer {Issuer}, audience {Audience}, lifetime {Minutes} minutes",
    app.Environment.EnvironmentName,
    jwt.Issuer,
    jwt.Audience,
    jwt.AccessTokenMinutes);

app.UseExceptionHandler();

var urls = app.Configuration["ASPNETCORE_URLS"] ?? "";
if (urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Airline API v1");
        options.DocumentTitle = "Airline API";
    });
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseCors(ServiceRegistration.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapAirlineEndpoints();

await DatabaseInitializer.InitializeAsync(app.Services);
app.Run();

public partial class Program;
