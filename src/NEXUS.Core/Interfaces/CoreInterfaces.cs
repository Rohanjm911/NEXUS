using NEXUS.Core.Enums;
using NEXUS.Core.Models;

namespace NEXUS.Core.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task BackupAsync(string destinationFilePath, CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);
    string DatabasePath { get; }
}

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearCompletedAsync(CancellationToken cancellationToken = default);
}

public interface IPresetRepository
{
    Task<IReadOnlyList<ConversionPreset>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversionPreset>> GetByCategoryAsync(FileCategory category, CancellationToken cancellationToken = default);
    Task<ConversionPreset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ConversionPreset preset, CancellationToken cancellationToken = default);
    Task UpdateAsync(ConversionPreset preset, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default);
    Task SetSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default);
}

public interface IWatchFolderRepository
{
    Task<IReadOnlyList<WatchFolder>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WatchFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(WatchFolder folder, CancellationToken cancellationToken = default);
    Task UpdateAsync(WatchFolder folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IAutomationRuleRepository
{
    Task<IReadOnlyList<AutomationRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AutomationRule rule, CancellationToken cancellationToken = default);
    Task UpdateAsync(AutomationRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IRecentFilesRepository
{
    Task<IReadOnlyList<RecentFile>> GetRecentAsync(int limit = 15, CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(RecentFile file, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ILogRepository
{
    Task LogAsync(string level, string category, string message, string? exceptionDetails = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogEntry>> GetLogsAsync(int limit = 100, string? minLevel = null, CancellationToken cancellationToken = default);
    Task ClearLogsAsync(CancellationToken cancellationToken = default);
}

public interface IProcessingProgressEvent
{
    Guid TaskId { get; }
    ProcessingStatus Status { get; }
    double ProgressPercentage { get; }
    double SpeedBytesPerSec { get; }
    long ProcessedBytes { get; }
    long TotalBytes { get; }
    string? Message { get; }
}

public interface IFileProcessingEngine
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<bool> SubmitTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task<bool> CancelTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<bool> PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<bool> ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    IObservable<IProcessingProgressEvent> ProgressEvents { get; }
}

public interface ISystemMetricsService
{
    Task<SystemMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default);
}

public interface IStorageAnalyzerService
{
    Task<IReadOnlyList<StorageCategoryStats>> AnalyzeDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}

public interface IFormatDetector
{
    FormatDetectionResult DetectFormat(string filePath);
    bool IsSupportedSource(string filePath);
    IReadOnlyList<string> GetSupportedTargetFormats(FileCategory category);
}

public interface IPluginContract
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    IReadOnlyList<string> SupportedFormats { get; }
    bool Initialize();
    void Shutdown();
}

public interface IPluginManager
{
    Task<IReadOnlyList<IPluginContract>> DiscoverPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default);
    Task<bool> LoadPluginAsync(string pluginId, CancellationToken cancellationToken = default);
    Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);
    IReadOnlyList<IPluginContract> LoadedPlugins { get; }
}

