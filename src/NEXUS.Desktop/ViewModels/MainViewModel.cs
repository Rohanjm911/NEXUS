using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Core.Interfaces;

namespace NEXUS.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITaskRepository _taskRepository;
    private readonly ISystemMetricsService _systemMetricsService;

    private ViewModelBase _currentViewModel = null!;
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    private string _currentViewName = "Dashboard";
    public string CurrentViewName
    {
        get => _currentViewName;
        set => SetProperty(ref _currentViewName, value);
    }

    private bool _isCommandPaletteOpen;
    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set => SetProperty(ref _isCommandPaletteOpen, value);
    }

    private string _commandPaletteQuery = string.Empty;
    public string CommandPaletteQuery
    {
        get => _commandPaletteQuery;
        set
        {
            if (SetProperty(ref _commandPaletteQuery, value))
            {
                FilterCommands();
            }
        }
    }

    private double _quickCpuPercentage;
    public double QuickCpuPercentage
    {
        get => _quickCpuPercentage;
        set => SetProperty(ref _quickCpuPercentage, value);
    }

    private double _quickRamPercentage;
    public double QuickRamPercentage
    {
        get => _quickRamPercentage;
        set => SetProperty(ref _quickRamPercentage, value);
    }

    private int _activeTasksCount;
    public int ActiveTasksCount
    {
        get => _activeTasksCount;
        set => SetProperty(ref _activeTasksCount, value);
    }

    public ObservableCollection<NotificationItem> Notifications { get; } = new();
    public ObservableCollection<CommandPaletteItem> AllCommands { get; } = new();
    public ObservableCollection<CommandPaletteItem> FilteredCommands { get; } = new();

    public IRelayCommand ToggleCommandPaletteCommand { get; }
    public IRelayCommand<string> NavigateCommand { get; }

    public MainViewModel(
        IServiceProvider serviceProvider,
        ITaskRepository taskRepository,
        ISystemMetricsService systemMetricsService)
    {
        _serviceProvider = serviceProvider;
        _taskRepository = taskRepository;
        _systemMetricsService = systemMetricsService;

        ToggleCommandPaletteCommand = new RelayCommand(() =>
        {
            IsCommandPaletteOpen = !IsCommandPaletteOpen;
            if (IsCommandPaletteOpen)
            {
                CommandPaletteQuery = string.Empty;
            }
        });

        NavigateCommand = new RelayCommand<string>(NavigateToView);

        InitializeCommands();
    }

    public override async Task InitializeAsync()
    {
        NavigateToView("Dashboard");
        await RefreshGlobalMetricsAsync();
    }

    public void AddNotification(string title, string message, string type = "Info")
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Notifications.Insert(0, new NotificationItem
            {
                Title = title,
                Message = message,
                Type = type
            });

            if (Notifications.Count > 5)
            {
                Notifications.RemoveAt(Notifications.Count - 1);
            }
        });
    }

    public async Task RefreshGlobalMetricsAsync()
    {
        try
        {
            var metrics = await _systemMetricsService.GetCurrentMetricsAsync();
            QuickCpuPercentage = metrics.CpuUsagePercentage;
            QuickRamPercentage = metrics.RamUsagePercentage;

            var active = await _taskRepository.GetActiveAsync();
            ActiveTasksCount = active.Count;
        }
        catch
        {
            // Ignore telemetry sampling errors
        }
    }

    public void NavigateToView(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName)) return;

        CurrentViewName = viewName;
        ViewModelBase targetVm = viewName switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "Universal" => _serviceProvider.GetRequiredService<UniversalConversionViewModel>(),
            "Image" => _serviceProvider.GetRequiredService<ImageStudioViewModel>(),
            "Audio" => _serviceProvider.GetRequiredService<AudioStudioViewModel>(),
            "Video" => _serviceProvider.GetRequiredService<VideoStudioViewModel>(),
            "Pdf" => _serviceProvider.GetRequiredService<PdfStudioViewModel>(),
            "Archive" => _serviceProvider.GetRequiredService<ArchiveManagerViewModel>(),
            "DevTools" => _serviceProvider.GetRequiredService<DevToolkitViewModel>(),
            "Processing" => _serviceProvider.GetRequiredService<ProcessingCenterViewModel>(),
            "Automation" => _serviceProvider.GetRequiredService<AutomationCenterViewModel>(),
            "Storage" => _serviceProvider.GetRequiredService<StorageAnalyzerViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsViewModel>(),
            _ => _serviceProvider.GetRequiredService<DashboardViewModel>()
        };

        CurrentViewModel = targetVm;
        _ = targetVm.InitializeAsync();
        IsCommandPaletteOpen = false;
    }

    private void InitializeCommands()
    {
        AllCommands.Clear();
        AllCommands.Add(new CommandPaletteItem { Title = "Go to Command Center", Description = "Overview, quick drop & telemetry", Category = "Navigation", Shortcut = "Ctrl+1", Command = new RelayCommand(() => NavigateToView("Dashboard")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Universal Studio", Description = "Convert any file format", Category = "Navigation", Shortcut = "Ctrl+2", Command = new RelayCommand(() => NavigateToView("Universal")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Image Studio", Description = "Resize, crop, convert & compress images", Category = "Navigation", Shortcut = "Ctrl+3", Command = new RelayCommand(() => NavigateToView("Image")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Audio Studio", Description = "Audio trim, bitrate & convert", Category = "Navigation", Shortcut = "Ctrl+4", Command = new RelayCommand(() => NavigateToView("Audio")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Video Studio", Description = "Video compression, trim & scale", Category = "Navigation", Shortcut = "Ctrl+5", Command = new RelayCommand(() => NavigateToView("Video")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open PDF Studio", Description = "Merge, split & convert PDFs", Category = "Navigation", Shortcut = "Ctrl+6", Command = new RelayCommand(() => NavigateToView("Pdf")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Archive Manager", Description = "Inspect & extract ZIP, 7Z, TAR", Category = "Navigation", Shortcut = "Ctrl+7", Command = new RelayCommand(() => NavigateToView("Archive")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Developer Toolkit", Description = "JSON, Base64, Hashes & Hex viewer", Category = "Navigation", Shortcut = "Ctrl+8", Command = new RelayCommand(() => NavigateToView("DevTools")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Processing Center", Description = "Manage active queue & tasks", Category = "Navigation", Shortcut = "Ctrl+9", Command = new RelayCommand(() => NavigateToView("Processing")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Automation Center", Description = "Watch folders & background rules", Category = "Navigation", Command = new RelayCommand(() => NavigateToView("Automation")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Storage Analyzer", Description = "Disk breakdown by file types", Category = "Navigation", Command = new RelayCommand(() => NavigateToView("Storage")) });
        AllCommands.Add(new CommandPaletteItem { Title = "Open Settings", Description = "Theme, performance & preferences", Category = "Navigation", Shortcut = "Ctrl+,", Command = new RelayCommand(() => NavigateToView("Settings")) });

        FilterCommands();
    }

    private void FilterCommands()
    {
        FilteredCommands.Clear();
        var q = CommandPaletteQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        foreach (var cmd in AllCommands)
        {
            if (string.IsNullOrEmpty(q) ||
                cmd.Title.ToLowerInvariant().Contains(q) ||
                cmd.Description.ToLowerInvariant().Contains(q) ||
                cmd.Category.ToLowerInvariant().Contains(q))
            {
                FilteredCommands.Add(cmd);
            }
        }
    }
}
