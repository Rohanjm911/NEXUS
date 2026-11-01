using Microsoft.Data.Sqlite;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Infrastructure.Database;

namespace NEXUS.Infrastructure.Repositories;

public class LogRepository : ILogRepository
{
    private readonly DatabaseService _databaseService;

    public LogRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task LogAsync(string level, string category, string message, string? exceptionDetails = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Logs (Timestamp, Level, Category, Message, ExceptionDetails)
                VALUES (@time, @level, @cat, @msg, @ex);
                """;

            cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@level", level);
            cmd.Parameters.AddWithValue("@cat", category);
            cmd.Parameters.AddWithValue("@msg", message);
            cmd.Parameters.AddWithValue("@ex", (object?)exceptionDetails ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Logging failure should never crash the host application
        }
    }

    public async Task<IReadOnlyList<LogEntry>> GetLogsAsync(int limit = 100, string? minLevel = null, CancellationToken cancellationToken = default)
    {
        var list = new List<LogEntry>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();

        if (string.IsNullOrEmpty(minLevel))
        {
            cmd.CommandText = "SELECT * FROM Logs ORDER BY Id DESC LIMIT @limit;";
        }
        else
        {
            cmd.CommandText = "SELECT * FROM Logs WHERE Level = @minLevel ORDER BY Id DESC LIMIT @limit;";
            cmd.Parameters.AddWithValue("@minLevel", minLevel);
        }

        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new LogEntry
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp")), null, System.Globalization.DateTimeStyles.RoundtripKind),
                Level = reader.GetString(reader.GetOrdinal("Level")),
                Category = reader.GetString(reader.GetOrdinal("Category")),
                Message = reader.GetString(reader.GetOrdinal("Message")),
                ExceptionDetails = reader.IsDBNull(reader.GetOrdinal("ExceptionDetails")) ? null : reader.GetString(reader.GetOrdinal("ExceptionDetails"))
            });
        }

        return list;
    }

    public async Task ClearLogsAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

