using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.ProcessingBridge;

namespace NEXUS.Application.Services;

public class TaskExecutionService : ITaskExecutionService, IDisposable
{
    private readonly IFileProcessingEngine _engine;
    private readonly ITaskRepository _taskRepository;
    private readonly IRecentFilesRepository _recentFilesRepository;
    private readonly ILogRepository _logRepository;
    private readonly ILogger<TaskExecutionService> _logger;

    private readonly ConcurrentDictionary<Guid, TaskItem> _activeTasks = new();
    private readonly IDisposable _progressSubscription;

    public event EventHandler<IProcessingProgressEvent>? TaskProgressUpdated;
    public event EventHandler<TaskItem>? TaskCompleted;
    public event EventHandler<TaskItem>? TaskFailed;

    public TaskExecutionService(
        IFileProcessingEngine engine,
        ITaskRepository taskRepository,
        IRecentFilesRepository recentFilesRepository,
        ILogRepository logRepository,
        ILogger<TaskExecutionService> logger)
    {
        _engine = engine;
        _taskRepository = taskRepository;
        _recentFilesRepository = recentFilesRepository;
        _logRepository = logRepository;
        _logger = logger;

        _progressSubscription = _engine.ProgressEvents.Subscribe(OnProgressReceived);
    }

    public async Task EnqueueAndProcessTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enqueuing task {TaskId} ({Name}) for processing.", task.Id, task.Name);

        // Ensure target directory exists
        var outDir = Path.GetDirectoryName(task.OutputFilePath);
        if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        task.Status = ProcessingStatus.Queued;
        task.StartedAt = DateTime.UtcNow;
        _activeTasks[task.Id] = task;

        await _taskRepository.AddAsync(task, cancellationToken).ConfigureAwait(false);
        await _engine.SubmitTaskAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnqueueAndProcessBatchAsync(IEnumerable<TaskItem> tasks, CancellationToken cancellationToken = default)
    {
        foreach (var task in tasks)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await EnqueueAndProcessTaskAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> CancelTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling task {TaskId}", taskId);
        var success = await _engine.CancelTaskAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (_activeTasks.TryRemove(taskId, out var task))
        {
            task.Status = ProcessingStatus.Cancelled;
            await _taskRepository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var dbTask = await _taskRepository.GetByIdAsync(taskId, cancellationToken).ConfigureAwait(false);
            if (dbTask != null)
            {
                dbTask.Status = ProcessingStatus.Cancelled;
                await _taskRepository.UpdateAsync(dbTask, cancellationToken).ConfigureAwait(false);
            }
        }

        return success;
    }

    public async Task<bool> RetryTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrying failed task {TaskId}", taskId);
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (task == null) return false;

        task.Status = ProcessingStatus.Queued;
        task.ProgressPercentage = 0;
        task.ErrorMessage = null;
        task.StartedAt = DateTime.UtcNow;
        task.CompletedAt = null;

        _activeTasks[task.Id] = task;
        await _taskRepository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return await _engine.SubmitTaskAsync(task, cancellationToken).ConfigureAwait(false);
    }

    private void OnProgressReceived(IProcessingProgressEvent progress)
    {
        TaskProgressUpdated?.Invoke(this, progress);

        _ = Task.Run(async () =>
        {
            try
            {
                if (!_activeTasks.TryGetValue(progress.TaskId, out var task))
                {
                    task = await _taskRepository.GetByIdAsync(progress.TaskId).ConfigureAwait(false);
                }

                if (task == null) return;

                task.Status = progress.Status;
                task.ProgressPercentage = progress.ProgressPercentage;
                task.SpeedBytesPerSec = progress.SpeedBytesPerSec;

                if (progress.Status == ProcessingStatus.Completed)
                {
                    task.CompletedAt = DateTime.UtcNow;
                    task.ProgressPercentage = 100.0;
                    if (File.Exists(task.OutputFilePath))
                    {
                        task.OutputSizeBytes = new FileInfo(task.OutputFilePath).Length;
                    }

                    _activeTasks.TryRemove(task.Id, out _);
                    await _taskRepository.UpdateAsync(task).ConfigureAwait(false);

                    // Add to Recent Files
                    await _recentFilesRepository.AddOrUpdateAsync(new RecentFile
                    {
                        FilePath = task.OutputFilePath,
                        FileName = Path.GetFileName(task.OutputFilePath),
                        Category = task.Category,
                        Format = task.TargetFormat,
                        FileSizeBytes = task.OutputSizeBytes,
                        LastOpenedAt = DateTime.UtcNow
                    }).ConfigureAwait(false);

                    await _logRepository.LogAsync(
                        "Information",
                        "TaskExecution",
                        $"Task '{task.Name}' completed successfully. Output: {task.OutputFilePath} ({task.OutputSizeBytes} bytes)"
                    ).ConfigureAwait(false);

                    TaskCompleted?.Invoke(this, task);
                }
                else if (progress.Status == ProcessingStatus.Failed)
                {
                    task.CompletedAt = DateTime.UtcNow;
                    task.ErrorMessage = progress.Message ?? "Unknown engine error";

                    _activeTasks.TryRemove(task.Id, out _);
                    await _taskRepository.UpdateAsync(task).ConfigureAwait(false);

                    await _logRepository.LogAsync(
                        "Error",
                        "TaskExecution",
                        $"Task '{task.Name}' failed: {task.ErrorMessage}"
                    ).ConfigureAwait(false);

                    TaskFailed?.Invoke(this, task);
                }
                else if (progress.Status == ProcessingStatus.Cancelled)
                {
                    _activeTasks.TryRemove(task.Id, out _);
                    await _taskRepository.UpdateAsync(task).ConfigureAwait(false);
                }
                else
                {
                    // Interim progress update to DB
                    await _taskRepository.UpdateAsync(task).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist task progress update for {TaskId}", progress.TaskId);
            }
        });
    }

    public void Dispose()
    {
        _progressSubscription.Dispose();
    }
}
