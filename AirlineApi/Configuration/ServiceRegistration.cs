using System.Text.Json;
using System.Text.Json.Serialization;
using AirlineApi.Data;
using AirlineApi.Endpoints;
using AirlineApi.Middleware;
using AirlineApi.Models;
using AirlineApi.OpenApi;
using AirlineApi.Repositories;
using AirlineApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace AirlineApi.Configuration;

public static class ServiceRegistration
{
    public const string CorsPolicy = "Frontend";

    public static void AddAirlineServices(this WebApplicationBuilder builder)
    {
        var configuredConnection = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw new InvalidOperationException("Задайте строку подключения ConnectionStrings:DefaultConnection.");
        }

        var connectionString = SqliteConnectionStringResolver.Resolve(configuredConnection, builder.Environment.ContentRootPath);
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => JwtKeyRules.IsAcceptable(options.SigningKey), JwtKeyRules.FailureMessage)
            .ValidateOnStart();
        builder.Services.AddOptions<AdminOptions>()
            .Bind(builder.Configuration.GetSection(AdminOptions.SectionName));
        builder.Services.AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName));

        var requireHttpsMetadata = !builder.Environment.IsDevelopment()
            && !builder.Environment.IsEnvironment("Testing");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                JwtBearerConfigurator.Configure(options, jwtOptions.Value, requireHttpsMetadata);
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DispatcherOnly, policy =>
                policy.RequireAuthenticatedUser().RequireRole(Roles.Dispatcher));
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, policy =>
            {
                var allowed = origins.Length == 0 ? ["http://localhost:5080"] : origins;
                policy.WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Airline API",
                Version = "v1",
                Description =
                    "Лабораторная работа №5, вариант 10: рейсы и бронирования. " +
                    "Роли: Dispatcher и Passenger. " +
                    "Для защищённых операций нажмите Authorize и вставьте JWT из POST /api/auth/login без префикса Bearer."
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Вставьте JWT, полученный из POST /api/auth/login. Префикс Bearer Swagger добавит сам."
            });
            options.OperationFilter<AuthorizeOperationFilter>();
        });

        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IAircraftRepository, AircraftRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IFlightRepository, FlightRepository>();
        builder.Services.AddScoped<IBookingRepository, BookingRepository>();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IFlightService, FlightService>();
        builder.Services.AddScoped<IBookingService, BookingService>();
        builder.Services.AddScoped<IAircraftService, AircraftService>();
        builder.Services.AddScoped<IPassengerQueryService, PassengerQueryService>();
        builder.Services.AddScoped<IAdoNetDemoService, AdoNetDemoService>();
    }

    public static WebApplication MapAirlineEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().ExcludeFromDescription();
        app.MapAuthEndpoints();
        app.MapFlightEndpoints();
        app.MapBookingEndpoints();
        app.MapAircraftEndpoints();
        app.MapPassengerEndpoints();
        app.MapAdoNetDemoEndpoints();
        if (app.Configuration.GetValue<bool>("Testing:ExposeUnhandledEndpoint"))
        {
            app.MapGet("/api/test/unhandled", () =>
            {
                throw new InvalidOperationException("boom-secret-detail");
            }).AllowAnonymous().ExcludeFromDescription();
        }

        return app;
    }
}
