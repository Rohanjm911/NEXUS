using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS.Application.Services;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Services;
using NEXUS.Infrastructure.Database;
using NEXUS.Infrastructure.Repositories;
using NEXUS.Infrastructure.Services;
using NEXUS.Plugins;
using NEXUS.ProcessingBridge;

namespace NEXUS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNexusCoreAndInfrastructure(this IServiceCollection services, string? customDatabasePath = null)
    {
        // Core Services
        services.AddSingleton<IFormatDetector, FormatDetector>();

        // Infrastructure Services & SQLite
        services.AddSingleton(sp => new DatabaseService(
            sp.GetRequiredService<ILogger<DatabaseService>>(),
            customDatabasePath
        ));
        services.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());

        // Repositories
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<IPresetRepository, PresetRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IRecentFilesRepository, RecentFilesRepository>();
        services.AddSingleton<WatchFolderRepository>();
        services.AddSingleton<IWatchFolderRepository>(sp => sp.GetRequiredService<WatchFolderRepository>());
        services.AddSingleton<IAutomationRuleRepository>(sp => sp.GetRequiredService<WatchFolderRepository>());
        services.AddSingleton<ILogRepository, LogRepository>();

        // System Services
        services.AddSingleton<ISystemMetricsService, SystemMetricsService>();
        services.AddSingleton<IStorageAnalyzerService, StorageAnalyzerService>();

        // Processing Bridge (Rust Engine IPC)
        services.AddSingleton<IFileProcessingEngine, NamedPipeEngineClient>();

        // Plugins System
        services.AddSingleton<IPluginManager, PluginManager>();

        // Task Execution Coordinator & Automation
        services.AddSingleton<ITaskExecutionService, TaskExecutionService>();
        services.AddSingleton<IAutomationService, AutomationService>();

        return services;
    }
}
