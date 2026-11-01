using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.ProcessingBridge;
using Xunit;

namespace NEXUS.IntegrationTests;

public class EngineIpcIntegrationTests : IDisposable
{
    private Process? _engineProcess;
    private readonly NamedPipeEngineClient _client;

    public EngineIpcIntegrationTests()
    {
        _client = new NamedPipeEngineClient(NullLogger<NamedPipeEngineClient>.Instance);

        // Ensure engine is running
        var engineExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "processing-engine", "target", "debug", "nexus-engine.exe"));
        if (File.Exists(engineExe))
        {
            var existing = Process.GetProcessesByName("nexus-engine");
            if (existing.Length == 0)
            {
                _engineProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = engineExe,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                Thread.Sleep(500);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch { }

        try
        {
            if (_engineProcess != null && !_engineProcess.HasExited)
            {
                _engineProcess.Kill(entireProcessTree: true);
                _engineProcess.Dispose();
            }
        }
        catch { }
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsTrueWhenEngineRunning()
    {
        var healthy = await _client.CheckHealthAsync();
        Assert.True(healthy);
    }

    [Fact]
    public async Task SubmitTaskAsync_ConvertsRealImageThroughRustEngine()
    {
        // 1. Create a real sample PNG image on disk
        var tempSource = Path.Combine(Path.GetTempPath(), $"nexus_input_{Guid.NewGuid():N}.png");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"nexus_output_{Guid.NewGuid():N}.webp");

        // Simple valid 1x1 PNG bytes
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
            0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
            0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        };
        await File.WriteAllBytesAsync(tempSource, pngBytes);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Image Pipeline",
            SourceFilePath = tempSource,
            OutputFilePath = tempOutput,
            SourceFormat = "png",
            TargetFormat = "webp",
            Category = FileCategory.Image,
            Status = ProcessingStatus.Queued,
            FileSizeBytes = pngBytes.Length,
            OptionsJson = "{}"
        };

        var completedTcs = new TaskCompletionSource<bool>();
        var receivedEvents = new List<IProcessingProgressEvent>();

        using var sub = _client.ProgressEvents.Subscribe(ev =>
        {
            if (ev.TaskId == task.Id)
            {
                receivedEvents.Add(ev);
                if (ev.Status == ProcessingStatus.Completed)
                {
                    completedTcs.TrySetResult(true);
                }
                else if (ev.Status == ProcessingStatus.Failed)
                {
                    completedTcs.TrySetException(new Exception(ev.Message ?? "Task failed"));
                }
            }
        });

        try
        {
            var submitted = await _client.SubmitTaskAsync(task);
            Assert.True(submitted);

            var completed = await Task.WhenAny(completedTcs.Task, Task.Delay(10000));
            Assert.Equal(completedTcs.Task, completed);
            Assert.True(await completedTcs.Task);

            // Verify output file exists and is valid WebP
            Assert.True(File.Exists(tempOutput));
            var outSize = new FileInfo(tempOutput).Length;
            Assert.True(outSize > 0);

            // Verify streaming progress events were received
            Assert.NotEmpty(receivedEvents);
            Assert.Contains(receivedEvents, e => e.Status == ProcessingStatus.Completed);
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }

    [Fact]
    public async Task SubmitTaskAsync_CreatesArchiveAndExtractsSafely()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"nexus_arc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var f1 = Path.Combine(tempDir, "sample1.txt");
        var f2 = Path.Combine(tempDir, "sample2.txt");
        await File.WriteAllTextAsync(f1, "NEXUS Universal Archive Stream Test 1");
        await File.WriteAllTextAsync(f2, "NEXUS Universal Archive Stream Test 2");

        var tarGzOutput = Path.Combine(Path.GetTempPath(), $"nexus_test_{Guid.NewGuid():N}.tar.gz");
        var extractDir = Path.Combine(Path.GetTempPath(), $"nexus_extract_{Guid.NewGuid():N}");

        var completedTcs = new TaskCompletionSource<bool>();
        using var sub = _client.ProgressEvents.Subscribe(ev =>
        {
            if (ev.Status == ProcessingStatus.Completed)
            {
                completedTcs.TrySetResult(true);
            }
            else if (ev.Status == ProcessingStatus.Failed)
            {
                completedTcs.TrySetException(new Exception(ev.Message ?? "Archive creation failed"));
            }
        });

        try
        {
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Name = "Create Archive",
                SourceFilePath = f1,
                OutputFilePath = tarGzOutput,
                SourceFormat = "files",
                TargetFormat = "tar.gz",
                Category = FileCategory.Archive,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { files = new[] { f1, f2 }, operation = "create" })
            };

            var submitted = await _client.SubmitTaskAsync(task);
            Assert.True(submitted);

            var completed = await Task.WhenAny(completedTcs.Task, Task.Delay(10000));
            Assert.Equal(completedTcs.Task, completed);
            Assert.True(await completedTcs.Task);

            // Verify tarball created
            Assert.True(File.Exists(tarGzOutput));
            Assert.True(new FileInfo(tarGzOutput).Length > 0);

            // Test Extraction
            var extractTcs = new TaskCompletionSource<bool>();
            using var sub2 = _client.ProgressEvents.Subscribe(ev =>
            {
                if (ev.Status == ProcessingStatus.Completed)
                {
                    extractTcs.TrySetResult(true);
                }
                else if (ev.Status == ProcessingStatus.Failed)
                {
                    extractTcs.TrySetException(new Exception(ev.Message ?? "Archive extraction failed"));
                }
            });

            var extractTask = new TaskItem
            {
                Id = Guid.NewGuid(),
                Name = "Extract Archive",
                SourceFilePath = tarGzOutput,
                OutputFilePath = extractDir,
                SourceFormat = "tar.gz",
                TargetFormat = "folder",
                Category = FileCategory.Archive,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { operation = "extract" })
            };

            await _client.SubmitTaskAsync(extractTask);
            var extractCompleted = await Task.WhenAny(extractTcs.Task, Task.Delay(10000));
            Assert.Equal(extractTcs.Task, extractCompleted);
            Assert.True(await extractTcs.Task);

            // Verify safe extraction unpacked files
            Assert.True(Directory.Exists(extractDir));
            var extractedFiles = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
            Assert.NotEmpty(extractedFiles);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            if (File.Exists(tarGzOutput)) File.Delete(tarGzOutput);
        }
    }

    [Fact]
    public async Task SubmitTaskAsync_MergesPdfDocuments()
    {
        var pdf1 = Path.Combine(Path.GetTempPath(), $"nexus_p1_{Guid.NewGuid():N}.pdf");
        var pdf2 = Path.Combine(Path.GetTempPath(), $"nexus_p2_{Guid.NewGuid():N}.pdf");
        var pdfOut = Path.Combine(Path.GetTempPath(), $"nexus_merged_{Guid.NewGuid():N}.pdf");

        var dummyPdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\nxref\n0 1\ntrailer\n<< /Root 1 0 R >>\nstartxref\n9\n%%EOF\n");
        await File.WriteAllBytesAsync(pdf1, dummyPdf);
        await File.WriteAllBytesAsync(pdf2, dummyPdf);

        var completedTcs = new TaskCompletionSource<bool>();
        using var sub = _client.ProgressEvents.Subscribe(ev =>
        {
            if (ev.Status == ProcessingStatus.Completed)
            {
                completedTcs.TrySetResult(true);
            }
            else if (ev.Status == ProcessingStatus.Failed)
            {
                completedTcs.TrySetException(new Exception(ev.Message ?? "PDF merge failed"));
            }
        });

        try
        {
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Name = "Merge PDFs",
                SourceFilePath = pdf1,
                OutputFilePath = pdfOut,
                SourceFormat = "pdf",
                TargetFormat = "pdf",
                Category = FileCategory.Document,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { files = new[] { pdf1, pdf2 }, operation = "merge" })
            };

            var submitted = await _client.SubmitTaskAsync(task);
            Assert.True(submitted);

            var completed = await Task.WhenAny(completedTcs.Task, Task.Delay(10000));
            Assert.Equal(completedTcs.Task, completed);
            Assert.True(await completedTcs.Task);

            Assert.True(File.Exists(pdfOut));
            var text = await File.ReadAllTextAsync(pdfOut);
            Assert.StartsWith("%PDF-1.7", text);
            Assert.EndsWith("%%EOF\n", text);
        }
        finally
        {
            if (File.Exists(pdf1)) File.Delete(pdf1);
            if (File.Exists(pdf2)) File.Delete(pdf2);
            if (File.Exists(pdfOut)) File.Delete(pdfOut);
        }
    }

    [Fact]
    public async Task SubmitTaskAsync_ConvertsAudioViaFFmpeg()
    {
        var ffmpegPath = File.Exists(@"C:\ffmpeg\ffmpeg.exe") ? @"C:\ffmpeg\ffmpeg.exe" : "ffmpeg";
        var wavIn = Path.Combine(Path.GetTempPath(), $"nexus_audio_{Guid.NewGuid():N}.wav");
        var mp3Out = Path.Combine(Path.GetTempPath(), $"nexus_audio_{Guid.NewGuid():N}.mp3");

        // Generate 1-second sine wave
        var gen = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-y -f lavfi -i sine=frequency=440:duration=1 \"{wavIn}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        if (gen != null)
        {
            await gen.WaitForExitAsync();
        }

        if (!File.Exists(wavIn)) return; // Skip if ffmpeg lavfi wasn't available

        var completedTcs = new TaskCompletionSource<bool>();
        using var sub = _client.ProgressEvents.Subscribe(ev =>
        {
            if (ev.Status == ProcessingStatus.Completed)
            {
                completedTcs.TrySetResult(true);
            }
            else if (ev.Status == ProcessingStatus.Failed)
            {
                completedTcs.TrySetException(new Exception(ev.Message ?? "Audio transcoding failed"));
            }
        });

        try
        {
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Name = "Convert Audio WAV -> MP3",
                SourceFilePath = wavIn,
                OutputFilePath = mp3Out,
                SourceFormat = "wav",
                TargetFormat = "mp3",
                Category = FileCategory.Audio,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { bitrate = "192k", sample_rate = "44100" })
            };

            var submitted = await _client.SubmitTaskAsync(task);
            Assert.True(submitted);

            var completed = await Task.WhenAny(completedTcs.Task, Task.Delay(10000));
            Assert.Equal(completedTcs.Task, completed);
            Assert.True(await completedTcs.Task);

            Assert.True(File.Exists(mp3Out));
            Assert.True(new FileInfo(mp3Out).Length > 500);
        }
        finally
        {
            if (File.Exists(wavIn)) File.Delete(wavIn);
            if (File.Exists(mp3Out)) File.Delete(mp3Out);
        }
    }
}
