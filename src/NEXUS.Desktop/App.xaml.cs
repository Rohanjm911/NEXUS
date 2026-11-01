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
