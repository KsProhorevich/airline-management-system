namespace AirlineApi.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; set; } = true;

    public bool SeedSampleData { get; set; } = true;
}
