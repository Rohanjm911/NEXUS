using Microsoft.Data.Sqlite;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Infrastructure.Database;

namespace NEXUS.Infrastructure.Repositories;

public class WatchFolderRepository : IWatchFolderRepository, IAutomationRuleRepository
{
    private readonly DatabaseService _databaseService;

    public WatchFolderRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    // --- Watch Folders ---

    public async Task<IReadOnlyList<WatchFolder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<WatchFolder>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM WatchFolders ORDER BY CreatedAt DESC;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new WatchFolder
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                Path = reader.GetString(reader.GetOrdinal("Path")),
                IsRecursive = reader.GetInt32(reader.GetOrdinal("IsRecursive")) == 1,
                IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
                LastScanAt = reader.IsDBNull(reader.GetOrdinal("LastScanAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastScanAt")), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }

        return list;
    }

    public async Task<WatchFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM WatchFolders WHERE Id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new WatchFolder
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                Path = reader.GetString(reader.GetOrdinal("Path")),
                IsRecursive = reader.GetInt32(reader.GetOrdinal("IsRecursive")) == 1,
                IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
                LastScanAt = reader.IsDBNull(reader.GetOrdinal("LastScanAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastScanAt")), null, System.Globalization.DateTimeStyles.RoundtripKind)
            };
        }

        return null;
    }

    public async Task AddAsync(WatchFolder folder, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO WatchFolders (Id, Path, IsRecursive, IsActive, CreatedAt, LastScanAt)
            VALUES (@id, @path, @rec, @active, @created, @scan);
            """;

        cmd.Parameters.AddWithValue("@id", folder.Id.ToString());
        cmd.Parameters.AddWithValue("@path", folder.Path);
        cmd.Parameters.AddWithValue("@rec", folder.IsRecursive ? 1 : 0);
        cmd.Parameters.AddWithValue("@active", folder.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", folder.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@scan", folder.LastScanAt.HasValue ? folder.LastScanAt.Value.ToString("O") : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(WatchFolder folder, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE WatchFolders SET
                Path = @path,
                IsRecursive = @rec,
                IsActive = @active,
                LastScanAt = @scan
            WHERE Id = @id;
            """;

        cmd.Parameters.AddWithValue("@id", folder.Id.ToString());
        cmd.Parameters.AddWithValue("@path", folder.Path);
        cmd.Parameters.AddWithValue("@rec", folder.IsRecursive ? 1 : 0);
        cmd.Parameters.AddWithValue("@active", folder.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@scan", folder.LastScanAt.HasValue ? folder.LastScanAt.Value.ToString("O") : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM WatchFolders WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // --- Automation Rules ---

    async Task<IReadOnlyList<AutomationRule>> IAutomationRuleRepository.GetAllAsync(CancellationToken cancellationToken)
    {
        var list = new List<AutomationRule>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AutomationRules ORDER BY CreatedAt DESC;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadRule(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<AutomationRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<AutomationRule>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AutomationRules WHERE IsActive = 1 ORDER BY CreatedAt DESC;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadRule(reader));
        }

        return list;
    }

    public async Task AddAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AutomationRules (Id, Name, WatchFolderId, PatternFilter, TargetFormat, PresetId, OutputDirectory, IsActive, CreatedAt)
            VALUES (@id, @name, @wfid, @pat, @tgt, @pid, @out, @active, @created);
            """;

        cmd.Parameters.AddWithValue("@id", rule.Id.ToString());
        cmd.Parameters.AddWithValue("@name", rule.Name);
        cmd.Parameters.AddWithValue("@wfid", rule.WatchFolderId.ToString());
        cmd.Parameters.AddWithValue("@pat", rule.PatternFilter);
        cmd.Parameters.AddWithValue("@tgt", rule.TargetFormat);
        cmd.Parameters.AddWithValue("@pid", rule.PresetId.HasValue ? rule.PresetId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@out", rule.OutputDirectory);
        cmd.Parameters.AddWithValue("@active", rule.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", rule.CreatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE AutomationRules SET
                Name = @name,
                WatchFolderId = @wfid,
                PatternFilter = @pat,
                TargetFormat = @tgt,
                PresetId = @pid,
                OutputDirectory = @out,
                IsActive = @active
            WHERE Id = @id;
            """;

        cmd.Parameters.AddWithValue("@id", rule.Id.ToString());
        cmd.Parameters.AddWithValue("@name", rule.Name);
        cmd.Parameters.AddWithValue("@wfid", rule.WatchFolderId.ToString());
        cmd.Parameters.AddWithValue("@pat", rule.PatternFilter);
        cmd.Parameters.AddWithValue("@tgt", rule.TargetFormat);
        cmd.Parameters.AddWithValue("@pid", rule.PresetId.HasValue ? rule.PresetId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@out", rule.OutputDirectory);
        cmd.Parameters.AddWithValue("@active", rule.IsActive ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static AutomationRule ReadRule(SqliteDataReader reader)
    {
        return new AutomationRule
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            WatchFolderId = Guid.Parse(reader.GetString(reader.GetOrdinal("WatchFolderId"))),
            PatternFilter = reader.GetString(reader.GetOrdinal("PatternFilter")),
            TargetFormat = reader.GetString(reader.GetOrdinal("TargetFormat")),
            PresetId = reader.IsDBNull(reader.GetOrdinal("PresetId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("PresetId"))),
            OutputDirectory = reader.GetString(reader.GetOrdinal("OutputDirectory")),
            IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}

