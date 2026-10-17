using AirlineApi.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineApi.Services;

public sealed record FlightStatusCount(string Status, int Count);

public interface IAdoNetDemoService
{
    Task<IReadOnlyList<FlightStatusCount>> CountFlightsByStatusAsync(string? status, CancellationToken cancellationToken);

    Task RenameRegistrationAsync(Guid aircraftId, string registration, bool failBeforeCommit, CancellationToken cancellationToken);
}

public sealed class AdoNetDemoService : IAdoNetDemoService
{
    private readonly string _connectionString;
    private readonly ILogger<AdoNetDemoService> _logger;

    public AdoNetDemoService(ApplicationDbContext db, ILogger<AdoNetDemoService> logger)
    {
        _connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("У контекста нет строки подключения.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightStatusCount>> CountFlightsByStatusAsync(string? status, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(status))
        {
            command.CommandText = """
                SELECT Status, COUNT(*)
                FROM Flights
                GROUP BY Status
                ORDER BY Status
                """;
        }
        else
        {
            command.CommandText = """
                SELECT Status, COUNT(*)
                FROM Flights
                WHERE Status = $status
                GROUP BY Status
                ORDER BY Status
                """;
            command.Parameters.AddWithValue("$status", status.Trim());
        }

        var result = new List<FlightStatusCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new FlightStatusCount(reader.GetString(0), reader.GetInt32(1)));
        }

        _logger.LogInformation("ADO.NET flight status query returned {Count} rows", result.Count);
        return result;
    }

    public async Task RenameRegistrationAsync(Guid aircraftId, string registration, bool failBeforeCommit, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                UPDATE Aircraft
                SET Registration = $registration
                WHERE Id = $id
                """;
            var registrationParameter = command.CreateParameter();
            registrationParameter.ParameterName = "$registration";
            registrationParameter.Value = registration.Trim().ToUpperInvariant();
            command.Parameters.Add(registrationParameter);
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "$id";
            idParameter.Value = aircraftId.ToString().ToUpperInvariant();
            command.Parameters.Add(idParameter);
            var updated = await command.ExecuteNonQueryAsync(cancellationToken);
            if (updated != 1)
            {
                throw new InvalidOperationException("Воздушное судно для демонстрации ADO.NET не найдено.");
            }

            if (failBeforeCommit)
            {
                throw new InvalidOperationException("Демонстрационный отказ до фиксации транзакции.");
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("ADO.NET transaction committed for aircraft {AircraftId}", aircraftId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("ADO.NET transaction rolled back for aircraft {AircraftId}", aircraftId);
            throw;
        }
    }
}
