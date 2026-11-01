using System.Text.Json;
using Microsoft.Data.Sqlite;
using NEXUS.Core.Interfaces;
using NEXUS.Infrastructure.Database;

namespace NEXUS.Infrastructure.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly DatabaseService _databaseService;

    public SettingsRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @key LIMIT 1;";
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result == null || result is DBNull)
        {
            return defaultValue;
        }

        var json = result.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);

        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings (Key, Value, UpdatedAt)
            VALUES (@key, @val, @updated)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAt = excluded.UpdatedAt;
            """;

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", json);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }

        return dict;
    }
}
