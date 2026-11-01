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
using NEXUS.Desktop.Services;
using ThemeMode = NEXUS.Core.Enums.ThemeMode;

namespace NEXUS.Desktop.ViewModels;

public class UniversalConversionViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;
    private readonly IFormatDetector _formatDetector;
    private readonly IPresetRepository _presetRepository;

    public ObservableCollection<TaskItem> QueuedItems { get; } = new();
    public ObservableCollection<TaskItem> FilteredQueuedItems { get; } = new();
    public ObservableCollection<string> TargetFormats { get; } = new();
    public ObservableCollection<string> Presets { get; } = new();

    private string _selectedTargetFormat = "png";
    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set => SetProperty(ref _selectedTargetFormat, value);
    }

    private string _selectedCategoryFilter = "All";
    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                ApplyCategoryFilter();
            }
        }
    }

    private TaskItem? _selectedTaskItem;
    public TaskItem? SelectedTaskItem
    {
        get => _selectedTaskItem;
        set => SetProperty(ref _selectedTaskItem, value);
    }

    private string _selectedPreset = "Balanced (Default)";
    public string SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    private string? _outputDestinationDirectory;
    public string? OutputDestinationDirectory
    {
        get => _outputDestinationDirectory;
        set => SetProperty(ref _outputDestinationDirectory, value);
    }

    private double _comparisonSliderValue = 50.0;
    public double ComparisonSliderValue
    {
        get => _comparisonSliderValue;
        set => SetProperty(ref _comparisonSliderValue, value);
    }

    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand ClearQueueCommand { get; }
    public IRelayCommand StartConversionCommand { get; }
    public IRelayCommand BrowseOutputDestinationCommand { get; }
    public IRelayCommand<string> SetCategoryFilterCommand { get; }

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
        ClearQueueCommand = new RelayCommand(() =>
        {
            QueuedItems.Clear();
            FilteredQueuedItems.Clear();
            SelectedTaskItem = null;
            StatusMessage = "Queue cleared.";
        });
        StartConversionCommand = new AsyncRelayCommand(StartConversionAsync);

        BrowseOutputDestinationCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Output Destination Directory" };
            if (dlg.ShowDialog() == true)
            {
                OutputDestinationDirectory = dlg.FolderName;
            }
        });

        SetCategoryFilterCommand = new RelayCommand<string>(cat =>
        {
            if (!string.IsNullOrEmpty(cat)) SelectedCategoryFilter = cat;
        });

        TargetFormats.Add("png");
        TargetFormats.Add("jpg");
        TargetFormats.Add("webp");
        TargetFormats.Add("avif");
        TargetFormats.Add("mp3");
        TargetFormats.Add("wav");
        TargetFormats.Add("flac");
        TargetFormats.Add("mp4");
        TargetFormats.Add("mkv");
        TargetFormats.Add("pdf");
        TargetFormats.Add("tar.gz");
        TargetFormats.Add("zip");

        Presets.Add("Balanced (Default)");
        Presets.Add("Maximum Master Quality");
        Presets.Add("Fast Streaming / Web");
        Presets.Add("Ultra Compressed");

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            var app = System.Windows.Application.Current;
            if (app != null)
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    var existing = QueuedItems.FirstOrDefault(t => t.Id == progress.TaskId);
                    if (existing != null)
                    {
                        existing.Status = progress.Status;
                        existing.ProgressPercentage = progress.ProgressPercentage;
                        existing.SpeedBytesPerSec = progress.SpeedBytesPerSec;

                        if (progress.Status == ProcessingStatus.Completed)
                        {
                            StatusMessage = $"Completed '{existing.Name}'";
                        }
                        else if (progress.Status == ProcessingStatus.Failed)
                        {
                            StatusMessage = $"Failed '{existing.Name}': {progress.Message}";
                        }
                    }
                });
            }
        };
    }

    public void HandleDroppedFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;
            var detection = _formatDetector.DetectFormat(file);
            var info = new FileInfo(file);

            var item = new TaskItem
            {
                Name = info.Name,
                SourceFilePath = file,
                SourceFormat = detection.Extension,
                TargetFormat = SelectedTargetFormat,
                Category = detection.Category,
                FileSizeBytes = info.Length,
                Status = ProcessingStatus.Queued
            };

            QueuedItems.Add(item);
        }

        ApplyCategoryFilter();

        if (SelectedTaskItem == null && FilteredQueuedItems.Count > 0)
        {
            SelectedTaskItem = FilteredQueuedItems[0];
        }

        StatusMessage = $"{QueuedItems.Count} files in conversion queue.";
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
            HandleDroppedFiles(dlg.FileNames);
        }
    }

    private void ApplyCategoryFilter()
    {
        FilteredQueuedItems.Clear();
        foreach (var item in QueuedItems)
        {
            bool match = SelectedCategoryFilter switch
            {
                "All" => true,
                "Images" => item.Category == FileCategory.Image,
                "Audio" => item.Category == FileCategory.Audio,
                "Video" => item.Category == FileCategory.Video,
                "Documents" => item.Category == FileCategory.Document,
                "Archives" => item.Category == FileCategory.Archive,
                _ => true
            };

            if (match) FilteredQueuedItems.Add(item);
        }

        if (SelectedTaskItem != null && !FilteredQueuedItems.Contains(SelectedTaskItem))
        {
            SelectedTaskItem = FilteredQueuedItems.FirstOrDefault();
        }
    }

    private async Task StartConversionAsync()
    {
        if (QueuedItems.Count == 0)
        {
            StatusMessage = "No files in queue. Click 'Import Files...' or drag & drop files here.";
            return;
        }

        var itemsToProcess = QueuedItems.Where(i => i.Status == ProcessingStatus.Queued || i.Status == ProcessingStatus.Failed).ToList();
        if (itemsToProcess.Count == 0)
        {
            StatusMessage = "All queued files have already been processed.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Submitting {itemsToProcess.Count} tasks to processing engine...";

        foreach (var item in itemsToProcess)
        {
            item.TargetFormat = SelectedTargetFormat;
            var dir = !string.IsNullOrWhiteSpace(OutputDestinationDirectory) 
                ? OutputDestinationDirectory 
                : (Path.GetDirectoryName(item.SourceFilePath) ?? "");
            var name = Path.GetFileNameWithoutExtension(item.SourceFilePath);
            item.OutputFilePath = Path.Combine(dir, $"{name}_converted.{SelectedTargetFormat}");
            item.Status = ProcessingStatus.Processing;

            await _taskExecutionService.EnqueueAndProcessTaskAsync(item);
        }

        IsBusy = false;
        StatusMessage = $"{itemsToProcess.Count} tasks running in processing engine.";
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
        set
        {
            if (SetProperty(ref _sourceImagePath, value))
            {
                OnSourceImageChanged(value);
            }
        }
    }

    private string _originalDimensions = "-";
    public string OriginalDimensions
    {
        get => _originalDimensions;
        set => SetProperty(ref _originalDimensions, value);
    }

    private string _originalFileSize = "-";
    public string OriginalFileSize
    {
        get => _originalFileSize;
        set => SetProperty(ref _originalFileSize, value);
    }

    private bool _hasImageSelected;
    public bool HasImageSelected
    {
        get => _hasImageSelected;
        set => SetProperty(ref _hasImageSelected, value);
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

    private string? _lastOutputFilePath;
    public string? LastOutputFilePath
    {
        get => _lastOutputFilePath;
        set => SetProperty(ref _lastOutputFilePath, value);
    }

    private bool _canOpenOutput;
    public bool CanOpenOutput
    {
        get => _canOpenOutput;
        set => SetProperty(ref _canOpenOutput, value);
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "webp", "png", "jpg", "avif", "bmp", "tiff", "gif" };

    public IRelayCommand BrowseImageCommand { get; }
    public IRelayCommand ProcessImageCommand { get; }
    public IRelayCommand OpenOutputFolderCommand { get; }

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

        OpenOutputFolderCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(LastOutputFilePath) && File.Exists(LastOutputFilePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{LastOutputFilePath}\"");
            }
        });

        ProcessImageCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SourceImagePath) || !File.Exists(SourceImagePath))
            {
                StatusMessage = "Please select an image file first.";
                return;
            }

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

            Guid activeTaskId = task.Id;
            LastOutputFilePath = outPath;
            CanOpenOutput = false;
            StatusMessage = "Processing image in Rust engine...";

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
        });

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            if (!string.IsNullOrEmpty(LastOutputFilePath) && progress.Status == ProcessingStatus.Completed)
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.InvokeAsync(() =>
                {
                    CanOpenOutput = true;
                    StatusMessage = $"Image processed successfully: '{Path.GetFileName(LastOutputFilePath)}'";
                });
            }
        };
    }

    public void HandleDroppedFile(string path)
    {
        if (File.Exists(path))
        {
            SourceImagePath = path;
        }
    }

    private void OnSourceImageChanged(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HasImageSelected = false;
            OriginalDimensions = "-";
            OriginalFileSize = "-";
            return;
        }

        try
        {
            var info = new FileInfo(path);
            OriginalFileSize = $"{info.Length / 1024.0:F1} KB";

            using var stream = File.OpenRead(path);
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                System.Windows.Media.Imaging.BitmapCacheOption.None);

            if (decoder.Frames.Count > 0)
            {
                var frame = decoder.Frames[0];
                OriginalDimensions = $"{frame.PixelWidth} × {frame.PixelHeight} px";
                TargetWidth = frame.PixelWidth;
                TargetHeight = frame.PixelHeight;
            }

            HasImageSelected = true;
            StatusMessage = $"Loaded image: {info.Name} ({OriginalDimensions})";
        }
        catch
        {
            HasImageSelected = true;
            OriginalDimensions = "Custom";
            StatusMessage = $"Selected image: {Path.GetFileName(path)}";
        }
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
        set
        {
            if (SetProperty(ref _sourceAudioPath, value))
            {
                OnSourceAudioChanged(value);
            }
        }
    }

    private bool _hasAudioSelected;
    public bool HasAudioSelected
    {
        get => _hasAudioSelected;
        set => SetProperty(ref _hasAudioSelected, value);
    }

    private string _audioFileInfo = "-";
    public string AudioFileInfo
    {
        get => _audioFileInfo;
        set => SetProperty(ref _audioFileInfo, value);
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

    private string? _lastOutputFilePath;
    public string? LastOutputFilePath
    {
        get => _lastOutputFilePath;
        set => SetProperty(ref _lastOutputFilePath, value);
    }

    private bool _canOpenOutput;
    public bool CanOpenOutput
    {
        get => _canOpenOutput;
        set => SetProperty(ref _canOpenOutput, value);
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma" };

    public IRelayCommand BrowseAudioCommand { get; }
    public IRelayCommand ProcessAudioCommand { get; }
    public IRelayCommand OpenOutputFolderCommand { get; }

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

        OpenOutputFolderCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(LastOutputFilePath) && File.Exists(LastOutputFilePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{LastOutputFilePath}\"");
            }
        });

        ProcessAudioCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SourceAudioPath) || !File.Exists(SourceAudioPath))
            {
                StatusMessage = "Please select an audio file first.";
                return;
            }

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

            LastOutputFilePath = outPath;
            CanOpenOutput = false;
            StatusMessage = "Processing audio stream...";

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
        });

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            if (!string.IsNullOrEmpty(LastOutputFilePath) && progress.Status == ProcessingStatus.Completed)
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.InvokeAsync(() =>
                {
                    CanOpenOutput = true;
                    StatusMessage = $"Audio processing completed: '{Path.GetFileName(LastOutputFilePath)}'";
                });
            }
        };
    }

    public void HandleDroppedFile(string path)
    {
        if (File.Exists(path))
        {
            SourceAudioPath = path;
        }
    }

    private void OnSourceAudioChanged(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HasAudioSelected = false;
            AudioFileInfo = "-";
            return;
        }

        var info = new FileInfo(path);
        AudioFileInfo = $"{info.Length / (1024.0 * 1024.0):F2} MB • {info.Extension.ToUpper().TrimStart('.')}";
        HasAudioSelected = true;
        StatusMessage = $"Selected audio: {info.Name} ({AudioFileInfo})";
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
        set
        {
            if (SetProperty(ref _sourceVideoPath, value))
            {
                OnSourceVideoChanged(value);
            }
        }
    }

    private bool _hasVideoSelected;
    public bool HasVideoSelected
    {
        get => _hasVideoSelected;
        set => SetProperty(ref _hasVideoSelected, value);
    }

    private string _videoFileInfo = "-";
    public string VideoFileInfo
    {
        get => _videoFileInfo;
        set => SetProperty(ref _videoFileInfo, value);
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

    private string? _lastOutputFilePath;
    public string? LastOutputFilePath
    {
        get => _lastOutputFilePath;
        set => SetProperty(ref _lastOutputFilePath, value);
    }

    private bool _canOpenOutput;
    public bool CanOpenOutput
    {
        get => _canOpenOutput;
        set => SetProperty(ref _canOpenOutput, value);
    }

    public ObservableCollection<string> SupportedFormats { get; } = new() { "mp4", "mkv", "mov", "avi", "webm" };

    public IRelayCommand BrowseVideoCommand { get; }
    public IRelayCommand ProcessVideoCommand { get; }
    public IRelayCommand OpenOutputFolderCommand { get; }

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

        OpenOutputFolderCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(LastOutputFilePath) && File.Exists(LastOutputFilePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{LastOutputFilePath}\"");
            }
        });

        ProcessVideoCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SourceVideoPath) || !File.Exists(SourceVideoPath))
            {
                StatusMessage = "Please select a video file first.";
                return;
            }

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

            LastOutputFilePath = outPath;
            CanOpenOutput = false;
            StatusMessage = "Encoding video stream...";

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
        });

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            if (!string.IsNullOrEmpty(LastOutputFilePath) && progress.Status == ProcessingStatus.Completed)
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.InvokeAsync(() =>
                {
                    CanOpenOutput = true;
                    StatusMessage = $"Video processed successfully: '{Path.GetFileName(LastOutputFilePath)}'";
                });
            }
        };
    }

    public void HandleDroppedFile(string path)
    {
        if (File.Exists(path))
        {
            SourceVideoPath = path;
        }
    }

    private void OnSourceVideoChanged(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HasVideoSelected = false;
            VideoFileInfo = "-";
            return;
        }

        var info = new FileInfo(path);
        VideoFileInfo = $"{info.Length / (1024.0 * 1024.0):F2} MB • {info.Extension.ToUpper().TrimStart('.')}";
        HasVideoSelected = true;
        StatusMessage = $"Selected video: {info.Name} ({VideoFileInfo})";
    }
}

public class PdfStudioViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    public ObservableCollection<string> PdfFiles { get; } = new();

    private string? _lastOutputFilePath;
    public string? LastOutputFilePath
    {
        get => _lastOutputFilePath;
        set => SetProperty(ref _lastOutputFilePath, value);
    }

    private bool _canOpenOutput;
    public bool CanOpenOutput
    {
        get => _canOpenOutput;
        set => SetProperty(ref _canOpenOutput, value);
    }

    public IRelayCommand AddPdfCommand { get; }
    public IRelayCommand<string> RemovePdfCommand { get; }
    public IRelayCommand ClearPdfCommand { get; }
    public IRelayCommand MergePdfCommand { get; }
    public IRelayCommand OpenOutputFolderCommand { get; }

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
                HandleDroppedFiles(dlg.FileNames);
            }
        });

        RemovePdfCommand = new RelayCommand<string>(path =>
        {
            if (path != null && PdfFiles.Contains(path))
            {
                PdfFiles.Remove(path);
                StatusMessage = $"{PdfFiles.Count} PDF documents queued.";
            }
        });

        ClearPdfCommand = new RelayCommand(() =>
        {
            PdfFiles.Clear();
            StatusMessage = "PDF list cleared.";
        });

        OpenOutputFolderCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(LastOutputFilePath) && File.Exists(LastOutputFilePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{LastOutputFilePath}\"");
            }
        });

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

            LastOutputFilePath = outPath;
            CanOpenOutput = false;
            StatusMessage = "Merging PDF documents...";

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
        });

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            if (!string.IsNullOrEmpty(LastOutputFilePath) && progress.Status == ProcessingStatus.Completed)
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.InvokeAsync(() =>
                {
                    CanOpenOutput = true;
                    StatusMessage = $"PDF merge completed: '{Path.GetFileName(LastOutputFilePath)}'";
                });
            }
        };
    }

    public void HandleDroppedFiles(IEnumerable<string> files)
    {
        int added = 0;
        foreach (var f in files)
        {
            if (File.Exists(f) && string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (!PdfFiles.Contains(f))
                {
                    PdfFiles.Add(f);
                    added++;
                }
            }
        }
        StatusMessage = $"{PdfFiles.Count} PDF documents queued.";
    }
}

public class ArchiveManagerViewModel : ViewModelBase
{
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ITaskRepository _taskRepository;

    public ObservableCollection<string> FilesToArchive { get; } = new();

    private bool _isExtractMode;
    public bool IsExtractMode
    {
        get => _isExtractMode;
        set => SetProperty(ref _isExtractMode, value);
    }

    private string _archiveFormat = "tar.gz";
    public string ArchiveFormat
    {
        get => _archiveFormat;
        set => SetProperty(ref _archiveFormat, value);
    }

    private string _extractSourcePath = string.Empty;
    public string ExtractSourcePath
    {
        get => _extractSourcePath;
        set => SetProperty(ref _extractSourcePath, value);
    }

    private string _extractDestinationPath = string.Empty;
    public string ExtractDestinationPath
    {
        get => _extractDestinationPath;
        set => SetProperty(ref _extractDestinationPath, value);
    }

    private string? _lastOutputFilePath;
    public string? LastOutputFilePath
    {
        get => _lastOutputFilePath;
        set => SetProperty(ref _lastOutputFilePath, value);
    }

    private bool _canOpenOutput;
    public bool CanOpenOutput
    {
        get => _canOpenOutput;
        set => SetProperty(ref _canOpenOutput, value);
    }

    public ObservableCollection<string> SupportedArchiveFormats { get; } = new() { "tar.gz", "tar", "zip" };

    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand<string> RemoveFileCommand { get; }
    public IRelayCommand ClearFilesCommand { get; }
    public IRelayCommand CreateArchiveCommand { get; }
    public IRelayCommand BrowseExtractSourceCommand { get; }
    public IRelayCommand BrowseExtractDestinationCommand { get; }
    public IRelayCommand ExtractArchiveCommand { get; }
    public IRelayCommand ToggleModeCommand { get; }
    public IRelayCommand OpenOutputFolderCommand { get; }

    public ArchiveManagerViewModel(
        ITaskExecutionService taskExecutionService,
        ITaskRepository taskRepository)
    {
        _taskExecutionService = taskExecutionService;
        _taskRepository = taskRepository;

        ToggleModeCommand = new RelayCommand(() =>
        {
            IsExtractMode = !IsExtractMode;
            StatusMessage = IsExtractMode ? "Extract Archive mode active." : "Create Archive mode active.";
        });

        AddFilesCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Select Files for Archive" };
            if (dlg.ShowDialog() == true)
            {
                HandleDroppedFiles(dlg.FileNames);
            }
        });

        RemoveFileCommand = new RelayCommand<string>(file =>
        {
            if (file != null && FilesToArchive.Contains(file))
            {
                FilesToArchive.Remove(file);
                StatusMessage = $"{FilesToArchive.Count} files in archive queue.";
            }
        });

        ClearFilesCommand = new RelayCommand(() =>
        {
            FilesToArchive.Clear();
            StatusMessage = "Archive file queue cleared.";
        });

        BrowseExtractSourceCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Archive Files (*.tar.gz;*.tar;*.zip)|*.tar.gz;*.tar;*.zip|All Files (*.*)|*.*",
                Title = "Select Archive to Extract"
            };
            if (dlg.ShowDialog() == true)
            {
                ExtractSourcePath = dlg.FileName;
                if (string.IsNullOrWhiteSpace(ExtractDestinationPath))
                {
                    var dir = Path.GetDirectoryName(dlg.FileName) ?? "";
                    var name = Path.GetFileNameWithoutExtension(dlg.FileName);
                    if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                    {
                        name = Path.GetFileNameWithoutExtension(name);
                    }
                    ExtractDestinationPath = Path.Combine(dir, $"{name}_extracted");
                }
            }
        });

        BrowseExtractDestinationCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Destination Folder" };
            if (dlg.ShowDialog() == true)
            {
                ExtractDestinationPath = dlg.FolderName;
            }
        });

        OpenOutputFolderCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(LastOutputFilePath))
            {
                if (Directory.Exists(LastOutputFilePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{LastOutputFilePath}\"");
                }
                else if (File.Exists(LastOutputFilePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{LastOutputFilePath}\"");
                }
            }
        });

        CreateArchiveCommand = new AsyncRelayCommand(async () =>
        {
            if (FilesToArchive.Count == 0)
            {
                StatusMessage = "Please add files to create an archive.";
                return;
            }

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

            LastOutputFilePath = outPath;
            CanOpenOutput = false;
            StatusMessage = $"Creating {ArchiveFormat.ToUpper()} archive in Rust engine...";

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
        });

        ExtractArchiveCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(ExtractSourcePath) || !File.Exists(ExtractSourcePath))
            {
                StatusMessage = "Please select a valid archive file to extract.";
                return;
            }

            var dest = string.IsNullOrWhiteSpace(ExtractDestinationPath)
                ? Path.Combine(Path.GetDirectoryName(ExtractSourcePath) ?? "", "Extracted")
                : ExtractDestinationPath;

            var task = new TaskItem
            {
                Name = $"Extract {Path.GetFileName(ExtractSourcePath)}",
                SourceFilePath = ExtractSourcePath,
                OutputFilePath = dest,
                SourceFormat = Path.GetExtension(ExtractSourcePath).TrimStart('.'),
                TargetFormat = "directory",
                Category = FileCategory.Archive,
                Status = ProcessingStatus.Queued,
                OptionsJson = JsonSerializer.Serialize(new { operation = "extract" })
            };

            LastOutputFilePath = dest;
            CanOpenOutput = false;
            StatusMessage = "Extracting archive safely...";

            await _taskExecutionService.EnqueueAndProcessTaskAsync(task);
        });

        _taskExecutionService.TaskProgressUpdated += (s, progress) =>
        {
            if (!string.IsNullOrEmpty(LastOutputFilePath) && progress.Status == ProcessingStatus.Completed)
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.InvokeAsync(() =>
                {
                    CanOpenOutput = true;
                    StatusMessage = $"Archive operation completed: '{Path.GetFileName(LastOutputFilePath)}'";
                });
            }
        };
    }

    public void HandleDroppedFiles(IEnumerable<string> files)
    {
        var list = files.ToList();
        if (list.Count == 1 && (list[0].EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                                list[0].EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ||
                                list[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            IsExtractMode = true;
            ExtractSourcePath = list[0];
            var dir = Path.GetDirectoryName(list[0]) ?? "";
            var name = Path.GetFileNameWithoutExtension(list[0]);
            ExtractDestinationPath = Path.Combine(dir, $"{name}_extracted");
            StatusMessage = $"Archive loaded for extraction: {Path.GetFileName(list[0])}";
            return;
        }

        foreach (var f in list)
        {
            if (File.Exists(f) && !FilesToArchive.Contains(f))
            {
                FilesToArchive.Add(f);
            }
        }
        StatusMessage = $"{FilesToArchive.Count} files in archive queue.";
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
    private readonly IThemeService _themeService;

    private int _themeIndex;
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            if (SetProperty(ref _themeIndex, value))
            {
                var mode = (ThemeMode)value;
                _themeService.ApplyTheme(mode);
                _ = _settingsRepository.SetSettingAsync("General.Theme", mode);
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

    public SettingsViewModel(ISettingsRepository settingsRepository, IThemeService themeService)
    {
        _settingsRepository = settingsRepository;
        _themeService = themeService;
    }

    public override async Task InitializeAsync()
    {
        var theme = await _settingsRepository.GetSettingAsync("General.Theme", NEXUS.Core.Enums.ThemeMode.ObsidianDark);
        ThemeIndex = (int)theme;

        MaxConcurrency = await _settingsRepository.GetSettingAsync("Performance.MaxConcurrency", 4);
    }
}
