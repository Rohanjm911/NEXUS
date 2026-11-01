using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NEXUS.Core.Interfaces;

namespace NEXUS.Infrastructure.Database;

public class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public DatabaseService(ILogger<DatabaseService> logger, string? customDatabasePath = null)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(customDatabasePath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var nexusDir = Path.Combine(appData, "NEXUS");
            Directory.CreateDirectory(nexusDir);
            DatabasePath = Path.Combine(nexusDir, "nexus.db");
        }
        else
        {
            var dir = Path.GetDirectoryName(customDatabasePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            DatabasePath = customDatabasePath;
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<SqliteConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA synchronous = NORMAL;";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing NEXUS SQLite Database at {Path}", DatabasePath);

        using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SourceFilePath TEXT NOT NULL,
                OutputFilePath TEXT NOT NULL,
                SourceFormat TEXT NOT NULL,
                TargetFormat TEXT NOT NULL,
                Category INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                ProgressPercentage REAL NOT NULL,
                SpeedBytesPerSec REAL NOT NULL,
                FileSizeBytes INTEGER NOT NULL,
                OutputSizeBytes INTEGER NOT NULL,
                Priority INTEGER NOT NULL,
                ErrorMessage TEXT,
                CreatedAt TEXT NOT NULL,
                StartedAt TEXT,
                CompletedAt TEXT,
                OptionsJson TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_tasks_status ON Tasks(Status);
            CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON Tasks(CreatedAt);

            CREATE TABLE IF NOT EXISTS Presets (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Category INTEGER NOT NULL,
                TargetFormat TEXT NOT NULL,
                Quality INTEGER NOT NULL,
                Resolution TEXT,
                Bitrate TEXT,
                CompressionLevel INTEGER NOT NULL,
                CustomArguments TEXT NOT NULL,
                IsDefault INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS WatchFolders (
                Id TEXT PRIMARY KEY,
                Path TEXT NOT NULL UNIQUE,
                IsRecursive INTEGER NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastScanAt TEXT
            );

            CREATE TABLE IF NOT EXISTS AutomationRules (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                WatchFolderId TEXT NOT NULL,
                PatternFilter TEXT NOT NULL,
                TargetFormat TEXT NOT NULL,
                PresetId TEXT,
                OutputDirectory TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (WatchFolderId) REFERENCES WatchFolders(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS RecentFiles (
                Id TEXT PRIMARY KEY,
                FilePath TEXT NOT NULL UNIQUE,
                FileName TEXT NOT NULL,
                FileSizeBytes INTEGER NOT NULL,
                Category INTEGER NOT NULL,
                Format TEXT NOT NULL,
                LastOpenedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_recent_opened ON RecentFiles(LastOpenedAt);

            CREATE TABLE IF NOT EXISTS Logs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                Level TEXT NOT NULL,
                Category TEXT NOT NULL,
                Message TEXT NOT NULL,
                ExceptionDetails TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON Logs(Timestamp);
            """;

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = schemaSql;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        transaction.Commit();
        _logger.LogInformation("NEXUS SQLite Database schema verified and initialized successfully.");
    }

    public async Task BackupAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        var destDir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var destConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var source = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new SqliteConnection(destConnectionString);
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

        source.BackupDatabase(destination);
        _logger.LogInformation("Database backup completed to {DestPath}", destinationFilePath);
    }

    public async Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Backup file does not exist", backupFilePath);
        }

        var backupConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupFilePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var source = new SqliteConnection(backupConnectionString);
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var destination = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(destination);
        _logger.LogInformation("Database successfully restored from {BackupPath}", backupFilePath);
    }
}
