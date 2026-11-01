using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Desktop.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly IRecentFilesRepository _recentFilesRepository;
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly IFormatDetector _formatDetector;
    private readonly DispatcherTimer _telemetryTimer;

    private double _cpuUsage;
    public double CpuUsage
    {
        get => _cpuUsage;
        set => SetProperty(ref _cpuUsage, value);
    }

    private double _ramUsage;
    public double RamUsage
    {
        get => _ramUsage;
        set => SetProperty(ref _ramUsage, value);
    }

    private string _ramText = string.Empty;
    public string RamText
    {
        get => _ramText;
        set => SetProperty(ref _ramText, value);
    }

    private int _activeCount;
    public int ActiveCount
    {
        get => _activeCount;
        set => SetProperty(ref _activeCount, value);
    }

    private int _completedCount;
    public int CompletedCount
    {
        get => _completedCount;
        set => SetProperty(ref _completedCount, value);
    }

    private int _failedCount;
    public int FailedCount
    {
        get => _failedCount;
        set => SetProperty(ref _failedCount, value);
    }

    private string _processingSpeed = "0.0 MB/s";
    public string ProcessingSpeed
    {
        get => _processingSpeed;
        set => SetProperty(ref _processingSpeed, value);
    }

    public ObservableCollection<TaskItem> RecentTasks { get; } = new();
    public ObservableCollection<RecentFile> RecentFiles { get; } = new();

    public IRelayCommand RefreshCommand { get; }

    public DashboardViewModel(
        ITaskRepository taskRepository,
        ITaskExecutionService taskExecutionService,
        IRecentFilesRepository recentFilesRepository,
        ISystemMetricsService systemMetricsService,
        IFormatDetector formatDetector)
    {
        _taskRepository = taskRepository;
        _taskExecutionService = taskExecutionService;
        _recentFilesRepository = recentFilesRepository;
        _systemMetricsService = systemMetricsService;
        _formatDetector = formatDetector;

        RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);

        _telemetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _telemetryTimer.Tick += async (s, e) => await SampleTelemetryAsync();

        _taskExecutionService.TaskProgressUpdated += async (s, e) =>
        {
            if (e.Status == ProcessingStatus.Completed || e.Status == ProcessingStatus.Failed)
            {
                await RefreshDataAsync();
            }
        };
    }

    public override async Task InitializeAsync()
    {
        _telemetryTimer.Start();
        await RefreshDataAsync();
        await SampleTelemetryAsync();
    }

    public async Task SampleTelemetryAsync()
    {
        try
        {
            var metrics = await _systemMetricsService.GetCurrentMetricsAsync();
            CpuUsage = metrics.CpuUsagePercentage;
            RamUsage = metrics.RamUsagePercentage;
            RamText = $"{metrics.TotalRamMb - metrics.AvailableRamMb:F1} / {metrics.TotalRamMb:F1} GB";
        }
        catch
        {
            // Ignore telemetry exceptions
        }
    }

    public async Task RefreshDataAsync()
    {
        try
        {
            var tasks = await _taskRepository.GetRecentAsync(10);
            RecentTasks.Clear();
            foreach (var t in tasks)
            {
                RecentTasks.Add(t);
            }

            var active = await _taskRepository.GetActiveAsync();
            ActiveCount = active.Count;

            var all = await _taskRepository.GetAllAsync();
            CompletedCount = all.Count(t => t.Status == ProcessingStatus.Completed);
            FailedCount = all.Count(t => t.Status == ProcessingStatus.Failed);

            var recentF = await _recentFilesRepository.GetRecentAsync(8);
            RecentFiles.Clear();
            foreach (var rf in recentF)
            {
                RecentFiles.Add(rf);
            }

            double totalSpeed = active.Sum(t => t.SpeedBytesPerSec);
            ProcessingSpeed = $"{totalSpeed / (1024.0 * 1024.0):F1} MB/s";
        }
        catch
        {
            // Ignore refresh errors
        }
    }

    public async Task HandleDroppedFilesAsync(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0) return;

        foreach (var path in filePaths)
        {
            if (!File.Exists(path)) continue;

            var detection = _formatDetector.DetectFormat(path);
            var fileInfo = new FileInfo(path);
            var defaultTarget = detection.RecommendedOutputFormats.FirstOrDefault() ?? "bin";

            var dir = Path.GetDirectoryName(path) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
            var outputPath = Path.Combine(dir, $"{fileNameWithoutExt}_converted.{defaultTarget}");

            var task = new TaskItem
            {
                Name = $"Convert {Path.GetFileName(path)}",
                SourceFilePath = path,
                OutputFilePath = outputPath,
                SourceFormat = detection.Extension,
                TargetFormat = defaultTarget,
                Category = detection.Category,
                Status = ProcessingStatus.Queued,
                FileSizeBytes = fileInfo.Length,
                Priority = PriorityLevel.Normal
            };

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
            await _recentFilesRepository.AddOrUpdateAsync(new RecentFile
            {
                FilePath = path,
                FileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                Category = detection.Category,
                Format = detection.Extension,
                LastOpenedAt = DateTime.UtcNow
            });
        }

        await RefreshDataAsync();
    }
}
