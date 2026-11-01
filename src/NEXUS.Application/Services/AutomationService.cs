using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Application.Services;

public class AutomationService : IAutomationService, IAsyncDisposable
{
    private readonly IWatchFolderRepository _watchFolderRepository;
    private readonly IAutomationRuleRepository _ruleRepository;
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly IFormatDetector _formatDetector;
    private readonly ILogRepository _logRepository;
    private readonly ILogger<AutomationService> _logger;

    private readonly ConcurrentDictionary<Guid, (WatchFolder Folder, FileSystemWatcher Watcher)> _watchers = new();
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public IReadOnlyList<WatchFolder> ActiveWatchers => _watchers.Values.Select(w => w.Folder).ToList();

    public AutomationService(
        IWatchFolderRepository watchFolderRepository,
        IAutomationRuleRepository ruleRepository,
        ITaskExecutionService taskExecutionService,
        IFormatDetector formatDetector,
        ILogRepository logRepository,
        ILogger<AutomationService> logger)
    {
        _watchFolderRepository = watchFolderRepository;
        _ruleRepository = ruleRepository;
        _taskExecutionService = taskExecutionService;
        _formatDetector = formatDetector;
        _logRepository = logRepository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;
        _isRunning = true;

        var folders = await _watchFolderRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var folder in folders.Where(f => f.IsActive))
        {
            await RegisterWatchFolderAsync(folder, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("NEXUS Automation Service started with {Count} active watch folders.", _watchers.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = false;
        foreach (var key in _watchers.Keys.ToList())
        {
            if (_watchers.TryRemove(key, out var item))
            {
                item.Watcher.EnableRaisingEvents = false;
                item.Watcher.Dispose();
            }
        }

        _logger.LogInformation("NEXUS Automation Service stopped.");
        return Task.CompletedTask;
    }

    public Task RegisterWatchFolderAsync(WatchFolder folder, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folder.Path))
        {
            _logger.LogWarning("Watch folder path '{Path}' does not exist on disk. Skipping.", folder.Path);
            return Task.CompletedTask;
        }

        if (_watchers.ContainsKey(folder.Id))
        {
            UnregisterWatchFolderAsync(folder.Id, cancellationToken);
        }

        try
        {
            var watcher = new FileSystemWatcher(folder.Path)
            {
                IncludeSubdirectories = folder.IsRecursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) => OnFileDetected(folder, e.FullPath);

            _watchers[folder.Id] = (folder, watcher);
            _logger.LogInformation("Watch folder registered: {Path}", folder.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileSystemWatcher for '{Path}'", folder.Path);
        }

        return Task.CompletedTask;
    }

    public Task UnregisterWatchFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        if (_watchers.TryRemove(folderId, out var item))
        {
            item.Watcher.EnableRaisingEvents = false;
            item.Watcher.Dispose();
            _logger.LogInformation("Watch folder unregistered: {Path}", item.Folder.Path);
        }

        return Task.CompletedTask;
    }

    private void OnFileDetected(WatchFolder folder, string filePath)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait briefly for file write completion
                await Task.Delay(500).ConfigureAwait(false);

                if (!File.Exists(filePath)) return;

                // Ensure file is readable / unlocked
                int attempts = 0;
                while (attempts < 5)
                {
                    try
                    {
                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        break;
                    }
                    catch (IOException)
                    {
                        attempts++;
                        await Task.Delay(400).ConfigureAwait(false);
                    }
                }

                var rules = await _ruleRepository.GetActiveRulesAsync().ConfigureAwait(false);
                var matchingRule = rules.FirstOrDefault(r => r.WatchFolderId == folder.Id);

                var detection = _formatDetector.DetectFormat(filePath);
                var targetFormat = matchingRule?.TargetFormat ?? detection.RecommendedOutputFormats.FirstOrDefault() ?? "bin";
                var outDir = matchingRule != null && !string.IsNullOrWhiteSpace(matchingRule.OutputDirectory)
                    ? matchingRule.OutputDirectory
                    : Path.Combine(Path.GetDirectoryName(filePath) ?? "", "NEXUS_Processed");

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                var outPath = Path.Combine(outDir, $"{fileNameWithoutExt}_auto.{targetFormat}");

                var task = new TaskItem
                {
                    Name = $"Automated: {Path.GetFileName(filePath)}",
                    SourceFilePath = filePath,
                    OutputFilePath = outPath,
                    SourceFormat = detection.Extension,
                    TargetFormat = targetFormat,
                    Category = detection.Category,
                    Status = ProcessingStatus.Queued,
                    FileSizeBytes = new FileInfo(filePath).Length,
                    Priority = PriorityLevel.Normal
                };

                await _logRepository.LogAsync(
                    "Information",
                    "Automation",
                    $"Watch folder '{folder.Path}' triggered automation for file: {filePath}"
                ).ConfigureAwait(false);

                await _taskExecutionService.EnqueueAndProcessTaskAsync(task).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing automated watch folder event for '{File}'", filePath);
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
