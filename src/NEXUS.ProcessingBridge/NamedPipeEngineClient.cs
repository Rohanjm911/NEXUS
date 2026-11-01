using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.ProcessingBridge;

public class ProcessingProgressEvent : IProcessingProgressEvent
{
    public Guid TaskId { get; set; }
    public ProcessingStatus Status { get; set; }
    public double ProgressPercentage { get; set; }
    public double SpeedBytesPerSec { get; set; }
    public long ProcessedBytes { get; set; }
    public long TotalBytes { get; set; }
    public string? Message { get; set; }
}

public class RustProgressUpdate
{
    public string task_id { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public double progress_percentage { get; set; }
    public double speed_bytes_per_sec { get; set; }
    public ulong processed_bytes { get; set; }
    public ulong total_bytes { get; set; }
    public string? message { get; set; }
}

public class NamedPipeEngineClient : IFileProcessingEngine, IDisposable, IAsyncDisposable
{
    private const string PipeName = "nexus-engine";
    private readonly ILogger<NamedPipeEngineClient> _logger;
    private readonly ObservableSubject<IProcessingProgressEvent> _progressSubject = new();
    private Process? _engineProcess;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public IObservable<IProcessingProgressEvent> ProgressEvents => _progressSubject;

    public NamedPipeEngineClient(ILogger<NamedPipeEngineClient> logger)
    {
        _logger = logger;
    }

    public async Task EnsureEngineRunningAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_engineProcess != null && !_engineProcess.HasExited)
            {
                return;
            }

            // Look for existing nexus-engine processes
            var existing = Process.GetProcessesByName("nexus-engine");
            if (existing.Length > 0)
            {
                _engineProcess = existing[0];
                return;
            }

            // Locate binary
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexus-engine.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "processing-engine", "target", "debug", "nexus-engine.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "processing-engine", "target", "debug", "nexus-engine.exe"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "processing-engine", "target", "debug", "nexus-engine.exe"))
            };

            string? exePath = possiblePaths.FirstOrDefault(File.Exists);
            if (exePath == null)
            {
                _logger.LogWarning("nexus-engine.exe binary not found in standard paths.");
                return;
            }

            _logger.LogInformation("Starting NEXUS Processing Engine from {Path}", exePath);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _engineProcess = Process.Start(psi);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false); // Allow named pipe server to initialize
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false);

            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1000, cancellationToken).ConfigureAwait(false);

            var req = new { command = "ping" };
            var json = JsonSerializer.Serialize(req) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);

            await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

            using var reader = new StreamReader(pipe, Encoding.UTF8);
            var responseLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            return !string.IsNullOrEmpty(responseLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for NEXUS engine.");
            return false;
        }
    }

    public async Task<bool> SubmitTaskAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureEngineRunningAsync(cancellationToken).ConfigureAwait(false);

            // Connect in background to stream progress for this task
            _ = Task.Run(async () =>
            {
                try
                {
                    using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(3000, cancellationToken).ConfigureAwait(false);

                    var req = new
                    {
                        command = "submit",
                        task = new
                        {
                            id = task.Id.ToString(),
                            name = task.Name,
                            source_path = task.SourceFilePath,
                            output_path = task.OutputFilePath,
                            source_format = task.SourceFormat,
                            target_format = task.TargetFormat,
                            category = ((int)task.Category).ToString(),
                            options_json = task.OptionsJson
                        }
                    };

                    var json = JsonSerializer.Serialize(req) + "\n";
                    var bytes = Encoding.UTF8.GetBytes(json);

                    await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
                            var update = JsonSerializer.Deserialize<RustProgressUpdate>(line);
                            if (update != null && Guid.TryParse(update.task_id, out var taskId))
                            {
                                var status = update.status.ToLowerInvariant() switch
                                {
                                    "processing" => ProcessingStatus.Processing,
                                    "completed" => ProcessingStatus.Completed,
                                    "failed" => ProcessingStatus.Failed,
                                    "cancelled" => ProcessingStatus.Cancelled,
                                    _ => ProcessingStatus.Processing
                                };

                                _progressSubject.OnNext(new ProcessingProgressEvent
                                {
                                    TaskId = taskId,
                                    Status = status,
                                    ProgressPercentage = update.progress_percentage,
                                    SpeedBytesPerSec = update.speed_bytes_per_sec,
                                    ProcessedBytes = (long)update.processed_bytes,
                                    TotalBytes = (long)update.total_bytes,
                                    Message = update.message
                                });

                                if (status == ProcessingStatus.Completed || status == ProcessingStatus.Failed || status == ProcessingStatus.Cancelled)
                                {
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore malformed progress line
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error streaming task progress for {TaskId}", task.Id);
                    _progressSubject.OnNext(new ProcessingProgressEvent
                    {
                        TaskId = task.Id,
                        Status = ProcessingStatus.Failed,
                        ProgressPercentage = 0,
                        Message = $"IPC Connection error: {ex.Message}"
                    });
                }
            }, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit task to engine: {TaskId}", task.Id);
            return false;
        }
    }

    public async Task<bool> CancelTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1000, cancellationToken).ConfigureAwait(false);

            var req = new
            {
                command = "cancel",
                task_id = taskId.ToString()
            };

            var json = JsonSerializer.Serialize(req) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);

            await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel task {TaskId}", taskId);
            return false;
        }
    }

    public Task<bool> PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false); // Pause not supported for single-pass stream transforms
    }

    public Task<bool> ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public void Dispose()
    {
        _progressSubject.Dispose();
        _connectionLock.Dispose();

        try
        {
            if (_engineProcess != null && !_engineProcess.HasExited)
            {
                _engineProcess.Kill(entireProcessTree: true);
                _engineProcess.Dispose();
            }
        }
        catch
        {
            // Ignore termination errors
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
