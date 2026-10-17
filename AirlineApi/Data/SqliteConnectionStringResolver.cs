using Microsoft.Data.Sqlite;

namespace AirlineApi.Data;

public static class SqliteConnectionStringResolver
{
    public static string Resolve(string connectionString, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource)
            && !string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));
        }

        return builder.ToString();
    }

    public static void EnsureDataDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource)
            || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
