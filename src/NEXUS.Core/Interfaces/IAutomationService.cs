using NEXUS.Core.Models;

namespace NEXUS.Core.Interfaces;

public interface IAutomationService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RegisterWatchFolderAsync(WatchFolder folder, CancellationToken cancellationToken = default);
    Task UnregisterWatchFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
    bool IsRunning { get; }
    IReadOnlyList<WatchFolder> ActiveWatchers { get; }
}

