using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS.Application;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Infrastructure.Database;
using NEXUS.Infrastructure.Repositories;
using Xunit;

namespace NEXUS.UnitTests;

public class DatabaseAndRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DatabaseService _dbService;
    private readonly TaskRepository _taskRepo;
    private readonly PresetRepository _presetRepo;
    private readonly SettingsRepository _settingsRepo;

    public DatabaseAndRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"nexus_test_{Guid.NewGuid():N}.db");
        _dbService = new DatabaseService(NullLogger<DatabaseService>.Instance, _testDbPath);
        _taskRepo = new TaskRepository(_dbService);
        _presetRepo = new PresetRepository(_dbService);
        _settingsRepo = new SettingsRepository(_dbService);

        // Initialize schema
        _dbService.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore temporary cleanup errors
        }
    }

    [Fact]
    public async Task Initialize_CreatesDatabaseAndTables()
    {
        Assert.True(File.Exists(_testDbPath));

        using var conn = await _dbService.CreateConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Tasks', 'Presets', 'Settings', 'Logs');";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task TaskRepository_AddAndRetrieveTask_Success()
    {
        var task = new TaskItem
        {
            Name = "Convert Sample Image",
            SourceFilePath = "C:/test/sample.png",
            OutputFilePath = "C:/test/sample.webp",
            SourceFormat = "png",
            TargetFormat = "webp",
            Category = FileCategory.Image,
            Status = ProcessingStatus.Queued,
            FileSizeBytes = 10240,
            Priority = PriorityLevel.High
        };

        await _taskRepo.AddAsync(task);

        var retrieved = await _taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(task.Name, retrieved.Name);
        Assert.Equal(task.SourceFormat, retrieved.SourceFormat);
        Assert.Equal(task.TargetFormat, retrieved.TargetFormat);
        Assert.Equal(task.Priority, retrieved.Priority);
        Assert.Equal(ProcessingStatus.Queued, retrieved.Status);
    }

    [Fact]
    public async Task TaskRepository_UpdateAndClearCompleted_Success()
    {
        var task = new TaskItem
        {
            Name = "Video Compression",
            SourceFilePath = "C:/video.mp4",
            OutputFilePath = "C:/video_compressed.mp4",
            SourceFormat = "mp4",
            TargetFormat = "mp4",
            Category = FileCategory.Video,
            Status = ProcessingStatus.Processing,
            ProgressPercentage = 50.0
        };

        await _taskRepo.AddAsync(task);

        task.Status = ProcessingStatus.Completed;
        task.ProgressPercentage = 100.0;
        task.CompletedAt = DateTime.UtcNow;
        await _taskRepo.UpdateAsync(task);

        var activeTasks = await _taskRepo.GetActiveAsync();
        Assert.DoesNotContain(activeTasks, t => t.Id == task.Id);

        await _taskRepo.ClearCompletedAsync();
        var retrievedAfterClear = await _taskRepo.GetByIdAsync(task.Id);
        Assert.Null(retrievedAfterClear);
    }

    [Fact]
    public async Task PresetRepository_SeedsDefaultsAndFiltersByCategory()
    {
        var presets = await _presetRepo.GetAllAsync();
        Assert.NotEmpty(presets);

        var imagePresets = await _presetRepo.GetByCategoryAsync(FileCategory.Image);
        Assert.NotEmpty(imagePresets);
        Assert.All(imagePresets, p => Assert.Equal(FileCategory.Image, p.Category));

        var audioPresets = await _presetRepo.GetByCategoryAsync(FileCategory.Audio);
        Assert.NotEmpty(audioPresets);
        Assert.All(audioPresets, p => Assert.Equal(FileCategory.Audio, p.Category));
    }

    [Fact]
    public async Task SettingsRepository_StoresAndRetrievesTypedData()
    {
        await _settingsRepo.SetSettingAsync("General.Theme", ThemeMode.ObsidianDark);
        await _settingsRepo.SetSettingAsync("General.MaxConcurrency", 4);

        var theme = await _settingsRepo.GetSettingAsync("General.Theme", ThemeMode.Light);
        var concurrency = await _settingsRepo.GetSettingAsync("General.MaxConcurrency", 1);

        Assert.Equal(ThemeMode.ObsidianDark, theme);
        Assert.Equal(4, concurrency);
    }

    [Fact]
    public void DependencyInjection_ResolvesAllCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexusCoreAndInfrastructure(_testDbPath);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IFormatDetector>());
        Assert.NotNull(provider.GetService<IDatabaseService>());
        Assert.NotNull(provider.GetService<ITaskRepository>());
        Assert.NotNull(provider.GetService<IPresetRepository>());
        Assert.NotNull(provider.GetService<ISettingsRepository>());
        Assert.NotNull(provider.GetService<IRecentFilesRepository>());
        Assert.NotNull(provider.GetService<IWatchFolderRepository>());
        Assert.NotNull(provider.GetService<ILogRepository>());
        Assert.NotNull(provider.GetService<ISystemMetricsService>());
        Assert.NotNull(provider.GetService<IStorageAnalyzerService>());
    }
}

