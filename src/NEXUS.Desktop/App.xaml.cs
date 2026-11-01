using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS.Application;
using NEXUS.Core.Interfaces;
using NEXUS.Desktop.Services;
using NEXUS.Desktop.ViewModels;

namespace NEXUS.Desktop;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexus_crash.log");
                System.IO.File.WriteAllText(logPath, $"Unhandled AppDomain Exception:\n{ex?.Message}\n\nStack:\n{ex?.StackTrace}\n\nInner:\n{ex?.InnerException}");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexus_crash.log");
                System.IO.File.AppendAllText(logPath, $"Dispatcher Exception:\n{e.Exception.Message}\n\nStack:\n{e.Exception.StackTrace}\n\nInner:\n{e.Exception.InnerException}\n-----------------------------------\n");
            }
            catch { }
            e.Handled = true;
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // 1. Initialize SQLite Database
            var db = ServiceProvider.GetRequiredService<IDatabaseService>();
            await db.InitializeAsync();

            // Apply Saved Theme (Emerald Dark / Light / Adaptive)
            try
            {
                var settings = ServiceProvider.GetRequiredService<ISettingsRepository>();
                var savedTheme = await settings.GetSettingAsync("General.Theme", NEXUS.Core.Enums.ThemeMode.ObsidianDark);
                var themeService = ServiceProvider.GetRequiredService<IThemeService>();
                themeService.ApplyTheme(savedTheme);
            }
            catch { }

            // 2. Warm up Engine in background
            try
            {
                var engine = ServiceProvider.GetService<IFileProcessingEngine>();
                _ = engine?.CheckHealthAsync();
            }
            catch { }

            // 3. Launch Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexus_crash.log");
            System.IO.File.WriteAllText(logPath, $"Startup Exception:\n{ex.Message}\n\nStack:\n{ex.StackTrace}\n\nInner:\n{ex.InnerException}");
            MessageBox.Show($"NEXUS Startup Exception:\n{ex.Message}\n\nStack:\n{ex.StackTrace}", "NEXUS Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch { }
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();

        // Core and Infrastructure
        services.AddNexusCoreAndInfrastructure();

        // Services
        services.AddSingleton<IThemeService, ThemeService>();

        // ViewModels (Singletons ensure tabs retain file lists, queue state, and settings across navigation)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<UniversalConversionViewModel>();
        services.AddSingleton<ImageStudioViewModel>();
        services.AddSingleton<ImageUpscalerViewModel>();
        services.AddSingleton<AudioStudioViewModel>();
        services.AddSingleton<VideoStudioViewModel>();
        services.AddSingleton<PdfStudioViewModel>();
        services.AddSingleton<ArchiveManagerViewModel>();
        services.AddSingleton<DevToolkitViewModel>();
        services.AddSingleton<ProcessingCenterViewModel>();
        services.AddSingleton<AutomationCenterViewModel>();
        services.AddSingleton<StorageAnalyzerViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
