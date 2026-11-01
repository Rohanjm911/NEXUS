using Microsoft.Data.Sqlite;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Infrastructure.Database;

namespace NEXUS.Infrastructure.Repositories;

public class PresetRepository : IPresetRepository
{
    private readonly DatabaseService _databaseService;

    public PresetRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<ConversionPreset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<ConversionPreset>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Presets ORDER BY Category ASC, Name ASC;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadPreset(reader));
        }

        if (list.Count == 0)
        {
            await SeedDefaultPresetsAsync(cancellationToken).ConfigureAwait(false);
            return await GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        return list;
    }

    public async Task<IReadOnlyList<ConversionPreset>> GetByCategoryAsync(FileCategory category, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(p => p.Category == category).ToList();
    }

    public async Task<ConversionPreset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Presets WHERE Id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadPreset(reader);
        }

        return null;
    }

    public async Task AddAsync(ConversionPreset preset, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Presets (
                Id, Name, Category, TargetFormat, Quality, Resolution, Bitrate, CompressionLevel, CustomArguments, IsDefault, CreatedAt
            ) VALUES (
                @id, @name, @cat, @tgtFmt, @quality, @res, @bitrate, @compLevel, @args, @isDefault, @created
            );
            """;

        BindPresetParameters(cmd, preset);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ConversionPreset preset, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Presets SET
                Name = @name,
                Category = @cat,
                TargetFormat = @tgtFmt,
                Quality = @quality,
                Resolution = @res,
                Bitrate = @bitrate,
                CompressionLevel = @compLevel,
                CustomArguments = @args,
                IsDefault = @isDefault
            WHERE Id = @id;
            """;

        BindPresetParameters(cmd, preset);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Presets WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDefaultPresetsAsync(CancellationToken cancellationToken)
    {
        var defaults = new List<ConversionPreset>
        {
            // Images
            new() { Name = "Web Optimized (WebP 80%)", Category = FileCategory.Image, TargetFormat = "webp", Quality = 80, CompressionLevel = 6, IsDefault = true },
            new() { Name = "High Quality PNG", Category = FileCategory.Image, TargetFormat = "png", Quality = 100, CompressionLevel = 9, IsDefault = false },
            new() { Name = "Standard JPEG (85%)", Category = FileCategory.Image, TargetFormat = "jpg", Quality = 85, CompressionLevel = 6, IsDefault = false },
            new() { Name = "Next-Gen AVIF (High)", Category = FileCategory.Image, TargetFormat = "avif", Quality = 75, CompressionLevel = 6, IsDefault = false },

            // Audio
            new() { Name = "High Quality MP3 (320kbps)", Category = FileCategory.Audio, TargetFormat = "mp3", Bitrate = "320k", Quality = 90, IsDefault = true },
            new() { Name = "Lossless FLAC", Category = FileCategory.Audio, TargetFormat = "flac", CompressionLevel = 8, Quality = 100, IsDefault = false },
            new() { Name = "Streaming AAC (192kbps)", Category = FileCategory.Audio, TargetFormat = "aac", Bitrate = "192k", Quality = 85, IsDefault = false },
            new() { Name = "Voice Audio (64kbps OGG)", Category = FileCategory.Audio, TargetFormat = "ogg", Bitrate = "64k", Quality = 60, IsDefault = false },

            // Video
            new() { Name = "Web 1080p MP4 (H.264/AAC)", Category = FileCategory.Video, TargetFormat = "mp4", Resolution = "1920x1080", Bitrate = "4000k", Quality = 80, IsDefault = true },
            new() { Name = "Efficient 1080p MKV (HEVC)", Category = FileCategory.Video, TargetFormat = "mkv", Resolution = "1920x1080", Bitrate = "2500k", Quality = 80, IsDefault = false },
            new() { Name = "Open WebM 720p (VP9)", Category = FileCategory.Video, TargetFormat = "webm", Resolution = "1280x720", Bitrate = "1800k", Quality = 75, IsDefault = false },

            // Documents
            new() { Name = "Standard PDF Export", Category = FileCategory.Document, TargetFormat = "pdf", Quality = 90, IsDefault = true },
            new() { Name = "Plain Text Extraction", Category = FileCategory.Document, TargetFormat = "txt", Quality = 100, IsDefault = false },

            // Archives
            new() { Name = "Maximum Compression (7Z Ultra)", Category = FileCategory.Archive, TargetFormat = "7z", CompressionLevel = 9, IsDefault = true },
            new() { Name = "Standard Universal ZIP", Category = FileCategory.Archive, TargetFormat = "zip", CompressionLevel = 6, IsDefault = false },
            new() { Name = "Linux Tarball (TAR.GZ)", Category = FileCategory.Archive, TargetFormat = "tar.gz", CompressionLevel = 6, IsDefault = false }
        };

        foreach (var preset in defaults)
        {
            await AddAsync(preset, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void BindPresetParameters(SqliteCommand cmd, ConversionPreset preset)
    {
        cmd.Parameters.AddWithValue("@id", preset.Id.ToString());
        cmd.Parameters.AddWithValue("@name", preset.Name);
        cmd.Parameters.AddWithValue("@cat", (int)preset.Category);
        cmd.Parameters.AddWithValue("@tgtFmt", preset.TargetFormat);
        cmd.Parameters.AddWithValue("@quality", preset.Quality);
        cmd.Parameters.AddWithValue("@res", (object?)preset.Resolution ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@bitrate", (object?)preset.Bitrate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@compLevel", preset.CompressionLevel);
        cmd.Parameters.AddWithValue("@args", preset.CustomArguments);
        cmd.Parameters.AddWithValue("@isDefault", preset.IsDefault ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", preset.CreatedAt.ToString("O"));
    }

    private static ConversionPreset ReadPreset(SqliteDataReader reader)
    {
        return new ConversionPreset
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Category = (FileCategory)reader.GetInt32(reader.GetOrdinal("Category")),
            TargetFormat = reader.GetString(reader.GetOrdinal("TargetFormat")),
            Quality = reader.GetInt32(reader.GetOrdinal("Quality")),
            Resolution = reader.IsDBNull(reader.GetOrdinal("Resolution")) ? null : reader.GetString(reader.GetOrdinal("Resolution")),
            Bitrate = reader.IsDBNull(reader.GetOrdinal("Bitrate")) ? null : reader.GetString(reader.GetOrdinal("Bitrate")),
            CompressionLevel = reader.GetInt32(reader.GetOrdinal("CompressionLevel")),
            CustomArguments = reader.GetString(reader.GetOrdinal("CustomArguments")),
            IsDefault = reader.GetInt32(reader.GetOrdinal("IsDefault")) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}

