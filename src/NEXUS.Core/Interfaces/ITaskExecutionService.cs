using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Core.Interfaces;

public interface ITaskExecutionService
{
    Task EnqueueAndProcessTaskAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task EnqueueAndProcessBatchAsync(IEnumerable<TaskItem> tasks, CancellationToken cancellationToken = default);
    Task<bool> CancelTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<bool> RetryTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    
    event EventHandler<IProcessingProgressEvent>? TaskProgressUpdated;
    event EventHandler<TaskItem>? TaskCompleted;
    event EventHandler<TaskItem>? TaskFailed;
}
