using NEXUS.Core.Enums;

namespace NEXUS.Core.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
    public FileCategory Category { get; set; } = FileCategory.Unknown;
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Queued;
    public double ProgressPercentage { get; set; }
    public double SpeedBytesPerSec { get; set; }
    public long FileSizeBytes { get; set; }
    public long OutputSizeBytes { get; set; }
    public PriorityLevel Priority { get; set; } = PriorityLevel.Normal;
    public string? ErrorMessage { get; set; }
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
