namespace NEXUS.Core.Enums;

public enum FileCategory
{
    Unknown = 0,
    Image = 1,
    Audio = 2,
    Video = 3,
    Document = 4,
    Archive = 5,
    Code = 6
}

public enum ProcessingStatus
{
    Queued = 0,
    Processing = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum PriorityLevel
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum ThemeMode
{
    ObsidianDark = 0,
    Light = 1,
    SystemAdaptive = 2
}
