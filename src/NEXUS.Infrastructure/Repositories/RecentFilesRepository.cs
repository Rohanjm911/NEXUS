using Microsoft.Data.Sqlite;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Infrastructure.Database;

namespace NEXUS.Infrastructure.Repositories;

public class RecentFilesRepository : IRecentFilesRepository
{
    private readonly DatabaseService _databaseService;

    public RecentFilesRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<RecentFile>> GetRecentAsync(int limit = 15, CancellationToken cancellationToken = default)
    {
        var list = new List<RecentFile>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM RecentFiles ORDER BY LastOpenedAt DESC LIMIT @limit;";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new RecentFile
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                FileName = reader.GetString(reader.GetOrdinal("FileName")),
                FileSizeBytes = reader.GetInt64(reader.GetOrdinal("FileSizeBytes")),
                Category = (FileCategory)reader.GetInt32(reader.GetOrdinal("Category")),
                Format = reader.GetString(reader.GetOrdinal("Format")),
                LastOpenedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastOpenedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }

        return list;
    }

    public async Task AddOrUpdateAsync(RecentFile file, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO RecentFiles (Id, FilePath, FileName, FileSizeBytes, Category, Format, LastOpenedAt)
            VALUES (@id, @path, @name, @size, @cat, @fmt, @opened)
            ON CONFLICT(FilePath) DO UPDATE SET
                FileName = excluded.FileName,
                FileSizeBytes = excluded.FileSizeBytes,
                Category = excluded.Category,
                Format = excluded.Format,
                LastOpenedAt = excluded.LastOpenedAt;
            """;

        cmd.Parameters.AddWithValue("@id", file.Id.ToString());
        cmd.Parameters.AddWithValue("@path", file.FilePath);
        cmd.Parameters.AddWithValue("@name", file.FileName);
        cmd.Parameters.AddWithValue("@size", file.FileSizeBytes);
        cmd.Parameters.AddWithValue("@cat", (int)file.Category);
        cmd.Parameters.AddWithValue("@fmt", file.Format);
        cmd.Parameters.AddWithValue("@opened", file.LastOpenedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM RecentFiles;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

