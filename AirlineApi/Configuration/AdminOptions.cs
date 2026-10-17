namespace AirlineApi.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; set; } = "";

    public string Password { get; set; } = "";

    public string FullName { get; set; } = "Диспетчер";
}
