using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Desktop.ViewModels;

public class UniversalConversionViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;
    private readonly IFormatDetector _formatDetector;
    private readonly IPresetRepository _presetRepository;

    public ObservableCollection<TaskItem> QueuedItems { get; } = new();
    public ObservableCollection<string> TargetFormats { get; } = new();

    private string _selectedTargetFormat = "png";
    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set => SetProperty(ref _selectedTargetFormat, value);
    }

    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand ClearQueueCommand { get; }
    public IRelayCommand StartConversionCommand { get; }

    public UniversalConversionViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository,
        IFormatDetector formatDetector,
        IPresetRepository presetRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;
        _formatDetector = formatDetector;
        _presetRepository = presetRepository;

        AddFilesCommand = new RelayCommand(AddFiles);
        ClearQueueCommand = new RelayCommand(QueuedItems.Clear);
        StartConversionCommand = new AsyncRelayCommand(StartConversionAsync);

        TargetFormats.Add("png");
        TargetFormats.Add("jpg");
        TargetFormats.Add("webp");
        TargetFormats.Add("mp3");
        TargetFormats.Add("mp4");
        TargetFormats.Add("pdf");
        TargetFormats.Add("tar.gz");
        TargetFormats.Add("zip");
    }

    private void AddFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Select Files to Convert"
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var file in dlg.FileNames)
            {
                var detection = _formatDetector.DetectFormat(file);
                var info = new FileInfo(file);

                QueuedItems.Add(new TaskItem
                {
                    Name = info.Name,
                    SourceFilePath = file,
                    SourceFormat = detection.Extension,
                    TargetFormat = SelectedTargetFormat,
                    Category = detection.Category,
                    FileSizeBytes = info.Length,
                    Status = ProcessingStatus.Queued
                });
            }
        }
    }

    private async Task StartConversionAsync()
    {
        if (QueuedItems.Count == 0) return;

        IsBusy = true;
        StatusMessage = $"Submitting {QueuedItems.Count} tasks to processing engine...";

        var itemsToProcess = QueuedItems.ToList();
        QueuedItems.Clear();

        foreach (var item in itemsToProcess)
        {
            item.TargetFormat = SelectedTargetFormat;
            var dir = Path.GetDirectoryName(item.SourceFilePath) ?? "";
            var name = Path.GetFileNameWithoutExtension(item.SourceFilePath);
            item.OutputFilePath = Path.Combine(dir, $"{name}_converted.{SelectedTargetFormat}");

            await _taskExecutionService.EnqueueAndProcessTaskAsync(item);
        }

        IsBusy = false;
        StatusMessage = $"{itemsToProcess.Count} tasks submitted to engine successfully.";
    }
}

public class ImageStudioViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    private string _sourceImagePath = string.Empty;
    public string SourceImagePath
    {
        get => _sourceImagePath;
        set => SetProperty(ref _sourceImagePath, value);
    }

    private int _targetWidth = 1920;
    public int TargetWidth
    {
        get => _targetWidth;
        set => SetProperty(ref _targetWidth, value);
    }

    private int _targetHeight = 1080;
    public int TargetHeight
    {
        get => _targetHeight;
        set => SetProperty(ref _targetHeight, value);
    }

    private int _quality = 85;
    public int Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    private string _targetFormat = "webp";
    public string TargetFormat
    {
        get => _targetFormat;
        set => SetProperty(ref _targetFormat, value);
    }

    private bool _stripMetadata = true;
    public bool StripMetadata
    {
        get => _stripMetadata;
        set => SetProperty(ref _stripMetadata, value);
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "webp", "png", "jpg", "avif", "bmp", "tiff", "gif" };

    public IRelayCommand BrowseImageCommand { get; }
    public IRelayCommand ProcessImageCommand { get; }

    public ImageStudioViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;

        BrowseImageCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.webp;*.avif;*.bmp;*.tiff;*.gif|All Files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                SourceImagePath = dlg.FileName;
            }
        });

        ProcessImageCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SourceImagePath) || !File.Exists(SourceImagePath)) return;

            var dir = Path.GetDirectoryName(SourceImagePath) ?? "";
            var name = Path.GetFileNameWithoutExtension(SourceImagePath);
            var outPath = Path.Combine(dir, $"{name}_nexus.{TargetFormat}");

            var task = new TaskItem
            {
                Name = $"Process Image: {Path.GetFileName(SourceImagePath)}",
                SourceFilePath = SourceImagePath,
                OutputFilePath = outPath,
                SourceFormat = Path.GetExtension(SourceImagePath).TrimStart('.'),
                TargetFormat = TargetFormat,
                Category = FileCategory.Image,
                Status = ProcessingStatus.Queued,
                FileSizeBytes = new FileInfo(SourceImagePath).Length,
                OptionsJson = JsonSerializer.Serialize(new
                {
                    width = TargetWidth,
                    height = TargetHeight,
                    quality = Quality,
                    strip_metadata = StripMetadata
                })
            };

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
            StatusMessage = $"Image processing submitted to engine: '{Path.GetFileName(outPath)}'";
        });
    }
}

public class AudioStudioViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    private string _sourceAudioPath = string.Empty;
    public string SourceAudioPath
    {
        get => _sourceAudioPath;
        set => SetProperty(ref _sourceAudioPath, value);
    }

    private string _bitrate = "320k";
    public string Bitrate
    {
        get => _bitrate;
        set => SetProperty(ref _bitrate, value);
    }

    private string _sampleRate = "44100";
    public string SampleRate
    {
        get => _sampleRate;
        set => SetProperty(ref _sampleRate, value);
    }

    private string _targetFormat = "mp3";
    public string TargetFormat
    {
        get => _targetFormat;
        set => SetProperty(ref _targetFormat, value);
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma" };

    public IRelayCommand BrowseAudioCommand { get; }
    public IRelayCommand ProcessAudioCommand { get; }

    public AudioStudioViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;

        BrowseAudioCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a;*.wma|All Files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                SourceAudioPath = dlg.FileName;
            }
        });

        ProcessAudioCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SourceAudioPath) || !File.Exists(SourceAudioPath)) return;

            var dir = Path.GetDirectoryName(SourceAudioPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(SourceAudioPath);
            var outPath = Path.Combine(dir, $"{name}_nexus.{TargetFormat}");

            var task = new TaskItem
            {
                Name = $"Process Audio: {Path.GetFileName(SourceAudioPath)}",
                SourceFilePath = SourceAudioPath,
                OutputFilePath = outPath,
                SourceFormat = Path.GetExtension(SourceAudioPath).TrimStart('.'),
                TargetFormat = TargetFormat,
                Category = FileCategory.Audio,
                Status = ProcessingStatus.Queued,
                FileSizeBytes = new FileInfo(SourceAudioPath).Length,
                OptionsJson = JsonSerializer.Serialize(new { bitrate = Bitrate, sample_rate = SampleRate })
            };

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
            StatusMessage = $"Audio processing submitted: '{Path.GetFileName(outPath)}'";
        });
    }
}

public class VideoStudioViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    private string _sourceVideoPath = string.Empty;
    public string SourceVideoPath
    {
        get => _sourceVideoPath;
        set => SetProperty(ref _sourceVideoPath, value);
    }

    private string _resolution = "1920x1080";
    public string Resolution
    {
        get => _resolution;
        set => SetProperty(ref _resolution, value);
    }

    private string _targetFormat = "mp4";
    public string TargetFormat
    {
        get => _targetFormat;
        set => SetProperty(ref _targetFormat, value);
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "mp4", "mkv", "mov", "avi", "webm" };

    public IRelayCommand BrowseVideoCommand { get; }
    public IRelayCommand ProcessVideoCommand { get; }

    public VideoStudioViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;

        BrowseVideoCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mkv;*.mov;*.avi;*.webm|All Files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                SourceVideoPath = dlg.FileName;
            }
        });

        ProcessVideoCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SourceVideoPath) || !File.Exists(SourceVideoPath)) return;

            var dir = Path.GetDirectoryName(SourceVideoPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(SourceVideoPath);
            var outPath = Path.Combine(dir, $"{name}_nexus.{TargetFormat}");

            var task = new TaskItem
            {
                Name = $"Process Video: {Path.GetFileName(SourceVideoPath)}",
                SourceFilePath = SourceVideoPath,
                OutputFilePath = outPath,
                SourceFormat = Path.GetExtension(SourceVideoPath).TrimStart('.'),
                TargetFormat = TargetFormat,
                Category = FileCategory.Video,
                Status = ProcessingStatus.Queued,
                FileSizeBytes = new FileInfo(SourceVideoPath).Length,
                OptionsJson = JsonSerializer.Serialize(new { resolution = Resolution })
            };

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
            StatusMessage = $"Video processing submitted: '{Path.GetFileName(outPath)}'";
        });
    }
}

public class PdfStudioViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    public ObservableCollection<string> PdfFiles { get; } = new();

    public IRelayCommand AddPdfCommand { get; }
    public IRelayCommand ClearPdfCommand { get; }
    public IRelayCommand MergePdfCommand { get; }

    public PdfStudioViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;

        AddPdfCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "PDF Documents (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var f in dlg.FileNames) PdfFiles.Add(f);
            }
        });

        ClearPdfCommand = new RelayCommand(PdfFiles.Clear);

        MergePdfCommand = new AsyncRelayCommand(async () =>
        {
            if (PdfFiles.Count < 2)
            {
                StatusMessage = "Please add at least 2 PDF documents to merge.";
                return;
            }

            var first = PdfFiles[0];
            var dir = Path.GetDirectoryName(first) ?? "";
            var outPath = Path.Combine(dir, $"Merged_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            var task = new TaskItem
            {
                Name = $"Merge {PdfFiles.Count} PDFs",
                SourceFilePath = first,
                OutputFilePath = outPath,
                SourceFormat = "pdf",
                TargetFormat = "pdf",
                Category = FileCategory.Document,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { files = PdfFiles.ToArray(), operation = "merge" })
            };

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
            PdfFiles.Clear();
            StatusMessage = "PDF Merge task submitted to processing engine.";
        });
    }
}

public class ArchiveManagerViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    public ObservableCollection<string> FilesToArchive { get; } = new();
    private string _archiveFormat = "tar.gz";
    public string ArchiveFormat
    {
        get => _archiveFormat;
        set => SetProperty(ref _archiveFormat, value);
    }

    public ObservableCollection<string> SupportedArchiveFormats { get; } = new() { "tar.gz", "tar", "zip" };

    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand ClearFilesCommand { get; }
    public IRelayCommand CreateArchiveCommand { get; }

    public ArchiveManagerViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;

        AddFilesCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Select Files for Archive" };
            if (dlg.ShowDialog() == true)
            {
                foreach (var f in dlg.FileNames) FilesToArchive.Add(f);
            }
        });

        ClearFilesCommand = new RelayCommand(FilesToArchive.Clear);

        CreateArchiveCommand = new AsyncRelayCommand(async () =>
        {
            if (FilesToArchive.Count == 0) return;

            var first = FilesToArchive[0];
            var dir = Path.GetDirectoryName(first) ?? "";
            var ext = ArchiveFormat == "tar.gz" ? "tar.gz" : ArchiveFormat;
            var outPath = Path.Combine(dir, $"Archive_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");

            var task = new TaskItem
            {
                Name = $"Create {ArchiveFormat.ToUpper()} Archive",
                SourceFilePath = first,
                OutputFilePath = outPath,
                SourceFormat = "files",
                TargetFormat = ArchiveFormat,
                Category = FileCategory.Archive,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { files = FilesToArchive.ToArray(), operation = "create" })
            };

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
            FilesToArchive.Clear();
            StatusMessage = "Archive creation task submitted to engine.";
        });
    }
}

public class DevToolkitViewModel : ViewModelBase
{
    private string _inputData = string.Empty;
    public string InputData
    {
        get => _inputData;
        set => SetProperty(ref _inputData, value);
    }

    private string _outputData = string.Empty;
    public string OutputData
    {
        get => _outputData;
        set => SetProperty(ref _outputData, value);
    }

    public IRelayCommand FormatJsonCommand { get; }
    public IRelayCommand ValidateJsonCommand { get; }
    public IRelayCommand Base64EncodeCommand { get; }
    public IRelayCommand Base64DecodeCommand { get; }
    public IRelayCommand ComputeSha256Command { get; }
    public IRelayCommand ComputeSha512Command { get; }
    public IRelayCommand ComputeMd5Command { get; }
    public IRelayCommand InspectFileHexCommand { get; }
    public IRelayCommand ComputeFileChecksumsCommand { get; }

    public DevToolkitViewModel()
    {
        FormatJsonCommand = new RelayCommand(() =>
        {
            try
            {
                var parsed = JsonDocument.Parse(InputData);
                OutputData = JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                OutputData = $"Invalid JSON: {ex.Message}";
            }
        });

        ValidateJsonCommand = new RelayCommand(() =>
        {
            try
            {
                using var doc = JsonDocument.Parse(InputData);
                OutputData = $"JSON is strictly VALID.\nRoot Element Type: {doc.RootElement.ValueKind}";
            }
            catch (Exception ex)
            {
                OutputData = $"JSON VALIDATION FAILED:\n{ex.Message}";
            }
        });

        Base64EncodeCommand = new RelayCommand(() =>
        {
            try
            {
                OutputData = Convert.ToBase64String(Encoding.UTF8.GetBytes(InputData));
            }
            catch (Exception ex)
            {
                OutputData = $"Encoding Error: {ex.Message}";
            }
        });

        Base64DecodeCommand = new RelayCommand(() =>
        {
            try
            {
                OutputData = Encoding.UTF8.GetString(Convert.FromBase64String(InputData));
            }
            catch (Exception ex)
            {
                OutputData = $"Decoding Error: {ex.Message}";
            }
        });

        ComputeSha256Command = new RelayCommand(() =>
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(InputData));
            OutputData = Convert.ToHexString(bytes).ToLowerInvariant();
        });

        ComputeSha512Command = new RelayCommand(() =>
        {
            var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(InputData));
            OutputData = Convert.ToHexString(bytes).ToLowerInvariant();
        });

        ComputeMd5Command = new RelayCommand(() =>
        {
            var bytes = MD5.HashData(Encoding.UTF8.GetBytes(InputData));
            OutputData = Convert.ToHexString(bytes).ToLowerInvariant();
        });

        ComputeFileChecksumsCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select File for Checksum Verification" };
            if (dlg.ShowDialog() == true)
            {
                var fileBytes = File.ReadAllBytes(dlg.FileName);
                var md5 = Convert.ToHexString(MD5.HashData(fileBytes)).ToLowerInvariant();
                var sha256 = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
                var sha512 = Convert.ToHexString(SHA512.HashData(fileBytes)).ToLowerInvariant();

                OutputData = $"FILE CHECKSUM VERIFICATION REPORT\n" +
                             $"Path: {dlg.FileName}\n" +
                             $"Size: {fileBytes.Length:N0} bytes\n\n" +
                             $"MD5:     {md5}\n" +
                             $"SHA-256: {sha256}\n" +
                             $"SHA-512: {sha512}";
            }
        });

        InspectFileHexCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select File for Hex Viewer" };
            if (dlg.ShowDialog() == true)
            {
                const int maxBytes = 4096;
                using var fs = File.OpenRead(dlg.FileName);
                var buffer = new byte[maxBytes];
                int read = fs.Read(buffer, 0, maxBytes);

                var sb = new StringBuilder();
                sb.AppendLine($"HEX DUMP: {dlg.FileName} (Showing first {read} bytes)\n");
                sb.AppendLine("Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  |ASCII|");
                sb.AppendLine("----------------------------------------------------------------------");

                for (int i = 0; i < read; i += 16)
                {
                    sb.Append($"{i:X8}  ");
                    int lineLength = Math.Min(16, read - i);

                    for (int j = 0; j < 16; j++)
                    {
                        if (j == 8) sb.Append(' ');
                        if (j < lineLength)
                            sb.Append($"{buffer[i + j]:X2} ");
                        else
                            sb.Append("   ");
                    }

                    sb.Append(" |");
                    for (int j = 0; j < lineLength; j++)
                    {
                        byte b = buffer[i + j];
                        sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                    }
                    sb.AppendLine("|");
                }

                OutputData = sb.ToString();
            }
        });
    }
}

public class ProcessingCenterViewModel : ViewModelBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskExecutionService _taskExecutionService;

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ClearCompletedCommand { get; }
    public IRelayCommand<TaskItem> CancelTaskCommand { get; }
    public IRelayCommand<TaskItem> RetryTaskCommand { get; }

    public ProcessingCenterViewModel(
        ITaskRepository taskRepository,
        ITaskExecutionService taskExecutionService)
    {
        _taskRepository = taskRepository;
        _taskExecutionService = taskExecutionService;

        RefreshCommand = new AsyncRelayCommand(RefreshTasksAsync);
        ClearCompletedCommand = new AsyncRelayCommand(async () =>
        {
            await _taskRepository.ClearCompletedAsync();
            await RefreshTasksAsync();
        });

        CancelTaskCommand = new AsyncRelayCommand<TaskItem>(async task =>
        {
            if (task == null) return;
            await _taskExecutionService.CancelTaskAsync(task.Id);
            await RefreshTasksAsync();
        });

        RetryTaskCommand = new AsyncRelayCommand<TaskItem>(async task =>
        {
            if (task == null) return;
            await _taskExecutionService.RetryTaskAsync(task.Id);
            await RefreshTasksAsync();
        });

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            var app = System.Windows.Application.Current;
            if (app != null)
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    var existing = Tasks.FirstOrDefault(t => t.Id == progress.TaskId);
                    if (existing != null)
                    {
                        existing.Status = progress.Status;
                        existing.ProgressPercentage = progress.ProgressPercentage;
                        existing.SpeedBytesPerSec = progress.SpeedBytesPerSec;
                    }
                });
            }
        };
    }

    public override async Task InitializeAsync() => await RefreshTasksAsync();

    public async Task RefreshTasksAsync()
    {
        var items = await _taskRepository.GetAllAsync();
        Tasks.Clear();
        foreach (var item in items) Tasks.Add(item);
    }
}

public class AutomationCenterViewModel : ViewModelBase
{
    private readonly IWatchFolderRepository _watchFolderRepository;
    private readonly IAutomationService _automationService;
    private readonly IAutomationRuleRepository _ruleRepository;

    public ObservableCollection<WatchFolder> WatchFolders { get; } = new();

    public IRelayCommand AddFolderCommand { get; }
    public IRelayCommand<WatchFolder> ToggleFolderCommand { get; }
    public IRelayCommand<WatchFolder> RemoveFolderCommand { get; }

    public AutomationCenterViewModel(
        IWatchFolderRepository watchFolderRepository,
        IAutomationService automationService,
        IAutomationRuleRepository ruleRepository)
    {
        _watchFolderRepository = watchFolderRepository;
        _automationService = automationService;
        _ruleRepository = ruleRepository;

        AddFolderCommand = new AsyncRelayCommand(async () =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Folder to Watch" };
            if (dlg.ShowDialog() == true)
            {
                var folder = new WatchFolder { Path = dlg.FolderName, IsActive = true };
                await _watchFolderRepository.AddAsync(folder);
                await _automationService.RegisterWatchFolderAsync(folder);
                await RefreshFoldersAsync();
            }
        });

        ToggleFolderCommand = new AsyncRelayCommand<WatchFolder>(async folder =>
        {
            if (folder == null) return;
            folder.IsActive = !folder.IsActive;
            await _watchFolderRepository.UpdateAsync(folder);
            if (folder.IsActive)
                await _automationService.RegisterWatchFolderAsync(folder);
            else
                await _automationService.UnregisterWatchFolderAsync(folder.Id);
            await RefreshFoldersAsync();
        });

        RemoveFolderCommand = new AsyncRelayCommand<WatchFolder>(async folder =>
        {
            if (folder == null) return;
            await _automationService.UnregisterWatchFolderAsync(folder.Id);
            await _watchFolderRepository.DeleteAsync(folder.Id);
            await RefreshFoldersAsync();
        });
    }

    public override async Task InitializeAsync()
    {
        if (!_automationService.IsRunning)
        {
            await _automationService.StartAsync();
        }
        await RefreshFoldersAsync();
    }

    public async Task RefreshFoldersAsync()
    {
        var folders = await _watchFolderRepository.GetAllAsync();
        WatchFolders.Clear();
        foreach (var f in folders) WatchFolders.Add(f);
    }
}

public class StorageAnalyzerViewModel : ViewModelBase
{
    private readonly IStorageAnalyzerService _storageAnalyzerService;

    public ObservableCollection<StorageCategoryStats> Stats { get; } = new();

    private string _selectedDirectory = string.Empty;
    public string SelectedDirectory
    {
        get => _selectedDirectory;
        set => SetProperty(ref _selectedDirectory, value);
    }

    public IRelayCommand ScanCommand { get; }

    public StorageAnalyzerViewModel(IStorageAnalyzerService storageAnalyzerService)
    {
        _storageAnalyzerService = storageAnalyzerService;

        ScanCommand = new AsyncRelayCommand(async () =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Directory to Analyze" };
            if (dlg.ShowDialog() == true)
            {
                SelectedDirectory = dlg.FolderName;
                IsBusy = true;
                StatusMessage = "Analyzing storage breakdown...";

                var results = await _storageAnalyzerService.AnalyzeDirectoryAsync(SelectedDirectory);
                Stats.Clear();
                foreach (var r in results) Stats.Add(r);

                IsBusy = false;
                StatusMessage = $"Analysis complete. Found {Stats.Sum(s => s.TotalFiles)} files.";
            }
        });
    }
}

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepository;

    private int _themeIndex;
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            if (SetProperty(ref _themeIndex, value))
            {
                _ = _settingsRepository.SetSettingAsync("General.Theme", (NEXUS.Core.Enums.ThemeMode)value);
            }
        }
    }

    private int _maxConcurrency = 4;
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        set
        {
            if (SetProperty(ref _maxConcurrency, value))
            {
                _ = _settingsRepository.SetSettingAsync("Performance.MaxConcurrency", value);
            }
        }
    }

    public SettingsViewModel(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public override async Task InitializeAsync()
    {
        var theme = await _settingsRepository.GetSettingAsync("General.Theme", NEXUS.Core.Enums.ThemeMode.ObsidianDark);
        ThemeIndex = (int)theme;

        MaxConcurrency = await _settingsRepository.GetSettingAsync("Performance.MaxConcurrency", 4);
    }
}
