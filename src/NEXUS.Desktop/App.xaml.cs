using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS.Application;
using NEXUS.Core.Interfaces;
using NEXUS.Desktop.ViewModels;

namespace NEXUS.Desktop;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // 1. Initialize SQLite Database
        var db = ServiceProvider.GetRequiredService<IDatabaseService>();
        await db.InitializeAsync();

        // 2. Launch Main Window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();

        // Core and Infrastructure
        services.AddNexusCoreAndInfrastructure();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UniversalConversionViewModel>();
        services.AddTransient<ImageStudioViewModel>();
        services.AddTransient<AudioStudioViewModel>();
        services.AddTransient<VideoStudioViewModel>();
        services.AddTransient<PdfStudioViewModel>();
        services.AddTransient<ArchiveManagerViewModel>();
        services.AddTransient<DevToolkitViewModel>();
        services.AddTransient<ProcessingCenterViewModel>();
        services.AddTransient<AutomationCenterViewModel>();
        services.AddTransient<StorageAnalyzerViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
