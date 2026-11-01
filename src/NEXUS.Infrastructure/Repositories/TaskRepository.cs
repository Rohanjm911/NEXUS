using Microsoft.Data.Sqlite;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Infrastructure.Database;

namespace NEXUS.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly DatabaseService _databaseService;

    public TaskRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<TaskItem>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks ORDER BY CreatedAt DESC;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadTask(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<TaskItem>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var list = new List<TaskItem>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks ORDER BY CreatedAt DESC LIMIT @limit;";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadTask(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<TaskItem>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<TaskItem>();
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks WHERE Status IN (@queued, @processing, @paused) ORDER BY Priority DESC, CreatedAt ASC;";
        cmd.Parameters.AddWithValue("@queued", (int)ProcessingStatus.Queued);
        cmd.Parameters.AddWithValue("@processing", (int)ProcessingStatus.Processing);
        cmd.Parameters.AddWithValue("@paused", (int)ProcessingStatus.Paused);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadTask(reader));
        }

        return list;
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks WHERE Id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadTask(reader);
        }

        return null;
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Tasks (
                Id, Name, SourceFilePath, OutputFilePath, SourceFormat, TargetFormat,
                Category, Status, ProgressPercentage, SpeedBytesPerSec, FileSizeBytes,
                OutputSizeBytes, Priority, ErrorMessage, CreatedAt, StartedAt, CompletedAt, OptionsJson
            ) VALUES (
                @id, @name, @source, @output, @srcFmt, @tgtFmt,
                @cat, @status, @progress, @speed, @fileSize,
                @outSize, @priority, @error, @created, @started, @completed, @options
            );
            """;

        BindTaskParameters(cmd, task);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Tasks SET
                Name = @name,
                SourceFilePath = @source,
                OutputFilePath = @output,
                SourceFormat = @srcFmt,
                TargetFormat = @tgtFmt,
                Category = @cat,
                Status = @status,
                ProgressPercentage = @progress,
                SpeedBytesPerSec = @speed,
                FileSizeBytes = @fileSize,
                OutputSizeBytes = @outSize,
                Priority = @priority,
                ErrorMessage = @error,
                StartedAt = @started,
                CompletedAt = @completed,
                OptionsJson = @options
            WHERE Id = @id;
            """;

        BindTaskParameters(cmd, task);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _databaseService.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE Status IN (@completed, @cancelled, @failed);";
        cmd.Parameters.AddWithValue("@completed", (int)ProcessingStatus.Completed);
        cmd.Parameters.AddWithValue("@cancelled", (int)ProcessingStatus.Cancelled);
        cmd.Parameters.AddWithValue("@failed", (int)ProcessingStatus.Failed);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void BindTaskParameters(SqliteCommand cmd, TaskItem task)
    {
        cmd.Parameters.AddWithValue("@id", task.Id.ToString());
        cmd.Parameters.AddWithValue("@name", task.Name);
        cmd.Parameters.AddWithValue("@source", task.SourceFilePath);
        cmd.Parameters.AddWithValue("@output", task.OutputFilePath);
        cmd.Parameters.AddWithValue("@srcFmt", task.SourceFormat);
        cmd.Parameters.AddWithValue("@tgtFmt", task.TargetFormat);
        cmd.Parameters.AddWithValue("@cat", (int)task.Category);
        cmd.Parameters.AddWithValue("@status", (int)task.Status);
        cmd.Parameters.AddWithValue("@progress", task.ProgressPercentage);
        cmd.Parameters.AddWithValue("@speed", task.SpeedBytesPerSec);
        cmd.Parameters.AddWithValue("@fileSize", task.FileSizeBytes);
        cmd.Parameters.AddWithValue("@outSize", task.OutputSizeBytes);
        cmd.Parameters.AddWithValue("@priority", (int)task.Priority);
        cmd.Parameters.AddWithValue("@error", (object?)task.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", task.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@started", task.StartedAt.HasValue ? task.StartedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@completed", task.CompletedAt.HasValue ? task.CompletedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@options", task.OptionsJson);
    }

    private static TaskItem ReadTask(SqliteDataReader reader)
    {
        return new TaskItem
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            SourceFilePath = reader.GetString(reader.GetOrdinal("SourceFilePath")),
            OutputFilePath = reader.GetString(reader.GetOrdinal("OutputFilePath")),
            SourceFormat = reader.GetString(reader.GetOrdinal("SourceFormat")),
            TargetFormat = reader.GetString(reader.GetOrdinal("TargetFormat")),
            Category = (FileCategory)reader.GetInt32(reader.GetOrdinal("Category")),
            Status = (ProcessingStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            ProgressPercentage = reader.GetDouble(reader.GetOrdinal("ProgressPercentage")),
            SpeedBytesPerSec = reader.GetDouble(reader.GetOrdinal("SpeedBytesPerSec")),
            FileSizeBytes = reader.GetInt64(reader.GetOrdinal("FileSizeBytes")),
            OutputSizeBytes = reader.GetInt64(reader.GetOrdinal("OutputSizeBytes")),
            Priority = (PriorityLevel)reader.GetInt32(reader.GetOrdinal("Priority")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            OptionsJson = reader.GetString(reader.GetOrdinal("OptionsJson"))
        };
    }
}
