using AirlineClient.Components;
using AirlineClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddHttpClient("airline", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Api:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:5080";
    }

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<AirlineApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
