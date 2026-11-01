using System.Windows.Input;

namespace NEXUS.Desktop.ViewModels;

public class CommandPaletteItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Shortcut { get; set; } = string.Empty;
    public ICommand Command { get; set; } = null!;
}

public class NotificationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info"; // Info, Success, Warning, Danger
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
