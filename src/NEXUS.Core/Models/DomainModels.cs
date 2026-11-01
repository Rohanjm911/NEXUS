using System.ComponentModel;
using System.Runtime.CompilerServices;
using NEXUS.Core.Enums;

namespace NEXUS.Core.Models;

public class TaskItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    public Guid Id { get; set; } = Guid.NewGuid();
    
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    private string _sourceFilePath = string.Empty;
    public string SourceFilePath
    {
        get => _sourceFilePath;
        set { if (_sourceFilePath != value) { _sourceFilePath = value; OnPropertyChanged(); } }
    }

    private string _outputFilePath = string.Empty;
    public string OutputFilePath
    {
        get => _outputFilePath;
        set { if (_outputFilePath != value) { _outputFilePath = value; OnPropertyChanged(); } }
    }

    private string _sourceFormat = string.Empty;
    public string SourceFormat
    {
        get => _sourceFormat;
        set { if (_sourceFormat != value) { _sourceFormat = value; OnPropertyChanged(); } }
    }

    private string _targetFormat = string.Empty;
    public string TargetFormat
    {
        get => _targetFormat;
        set { if (_targetFormat != value) { _targetFormat = value; OnPropertyChanged(); } }
    }

    private FileCategory _category = FileCategory.Unknown;
    public FileCategory Category
    {
        get => _category;
        set { if (_category != value) { _category = value; OnPropertyChanged(); } }
    }

    private ProcessingStatus _status = ProcessingStatus.Queued;
    public ProcessingStatus Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    private double _progressPercentage;
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set { if (Math.Abs(_progressPercentage - value) > 0.001) { _progressPercentage = value; OnPropertyChanged(); } }
    }

    private double _speedBytesPerSec;
    public double SpeedBytesPerSec
    {
        get => _speedBytesPerSec;
        set { if (Math.Abs(_speedBytesPerSec - value) > 0.001) { _speedBytesPerSec = value; OnPropertyChanged(); } }
    }

    private long _fileSizeBytes;
    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set { if (_fileSizeBytes != value) { _fileSizeBytes = value; OnPropertyChanged(); } }
    }

    private long _outputSizeBytes;
    public long OutputSizeBytes
    {
        get => _outputSizeBytes;
        set { if (_outputSizeBytes != value) { _outputSizeBytes = value; OnPropertyChanged(); } }
    }

    private PriorityLevel _priority = PriorityLevel.Normal;
    public PriorityLevel Priority
    {
        get => _priority;
        set { if (_priority != value) { _priority = value; OnPropertyChanged(); } }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string OptionsJson { get; set; } = "{}";

    public TimeSpan? Duration => (CompletedAt ?? DateTime.UtcNow) - (StartedAt ?? CreatedAt);
}

public class ConversionPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public FileCategory Category { get; set; } = FileCategory.Unknown;
    public string TargetFormat { get; set; } = string.Empty;
    public int Quality { get; set; } = 85;
    public string? Resolution { get; set; }
    public string? Bitrate { get; set; }
    public int CompressionLevel { get; set; } = 6;
    public string CustomArguments { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WatchFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;
    public bool IsRecursive { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastScanAt { get; set; }
}

public class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid WatchFolderId { get; set; }
    public string PatternFilter { get; set; } = "*.*";
    public string TargetFormat { get; set; } = string.Empty;
    public Guid? PresetId { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RecentFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public FileCategory Category { get; set; } = FileCategory.Unknown;
    public string Format { get; set; } = string.Empty;
    public DateTime LastOpenedAt { get; set; } = DateTime.UtcNow;
}

public class SystemMetrics
{
    public double CpuUsagePercentage { get; set; }
    public double RamUsagePercentage { get; set; }
    public double AvailableRamMb { get; set; }
    public double TotalRamMb { get; set; }
    public double DiskReadBytesPerSec { get; set; }
    public double DiskWriteBytesPerSec { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class StorageCategoryStats
{
    public FileCategory Category { get; set; }
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public double Percentage { get; set; }
}

public class LogEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Information";
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionDetails { get; set; }
}

public class FormatDetectionResult
{
    public string Extension { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public FileCategory Category { get; set; } = FileCategory.Unknown;
    public bool IsSupportedInput { get; set; }
    public IReadOnlyList<string> RecommendedOutputFormats { get; set; } = Array.Empty<string>();
}

