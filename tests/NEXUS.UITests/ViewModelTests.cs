using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS.Application;
using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;
using NEXUS.Core.Services;
using NEXUS.Desktop.Services;
using NEXUS.Desktop.ViewModels;
using NEXUS.Infrastructure.Database;
using NEXUS.Infrastructure.Repositories;
using NEXUS.Infrastructure.Services;
using Xunit;

namespace NEXUS.UnitTests;

public class ViewModelTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly ServiceProvider _serviceProvider;
    private readonly ITaskRepository _taskRepo;
    private readonly IRecentFilesRepository _recentFilesRepo;
    private readonly ISystemMetricsService _systemMetrics;
    private readonly IFormatDetector _formatDetector;

    public ViewModelTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"nexus_vm_test_{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexusCoreAndInfrastructure(_testDbPath);

        // Services
        services.AddSingleton<IThemeService, ThemeService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UniversalConversionViewModel>();
        services.AddTransient<ImageStudioViewModel>();
        services.AddTransient<ImageUpscalerViewModel>();
        services.AddTransient<AudioStudioViewModel>();
        services.AddTransient<VideoStudioViewModel>();
        services.AddTransient<PdfStudioViewModel>();
        services.AddTransient<ArchiveManagerViewModel>();
        services.AddTransient<DevToolkitViewModel>();
        services.AddTransient<ProcessingCenterViewModel>();
        services.AddTransient<AutomationCenterViewModel>();
        services.AddTransient<StorageAnalyzerViewModel>();
        services.AddTransient<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        _taskRepo = _serviceProvider.GetRequiredService<ITaskRepository>();
        _recentFilesRepo = _serviceProvider.GetRequiredService<IRecentFilesRepository>();
        _systemMetrics = _serviceProvider.GetRequiredService<ISystemMetricsService>();
        _formatDetector = _serviceProvider.GetRequiredService<IFormatDetector>();

        _serviceProvider.GetRequiredService<IDatabaseService>().InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch { }
    }

    [Fact]
    public async Task MainViewModel_NavigationAndCommandPalette_WorksCorrectly()
    {
        var vm = _serviceProvider.GetRequiredService<MainViewModel>();
        await vm.InitializeAsync();

        Assert.Equal("Dashboard", vm.CurrentViewName);
        Assert.NotNull(vm.CurrentViewModel);
        Assert.IsType<DashboardViewModel>(vm.CurrentViewModel);

        // Navigate to Image Studio
        vm.NavigateToView("Image");
        Assert.Equal("Image", vm.CurrentViewName);
        Assert.IsType<ImageStudioViewModel>(vm.CurrentViewModel);

        // Test Command Palette Filter
        vm.IsCommandPaletteOpen = true;
        vm.CommandPaletteQuery = "audio";
        Assert.NotEmpty(vm.FilteredCommands);
        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Audio"));
    }

    [Fact]
    public async Task DashboardViewModel_HandlesDroppedFilesAndStatistics()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();

        // Create temporary test image file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_drop_{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        try
        {
            await vm.HandleDroppedFilesAsync(new string[] { tempFile });

            var recentTasks = await _taskRepo.GetRecentAsync(5);
            Assert.Contains(recentTasks, t => t.SourceFilePath == tempFile);

            var recentFiles = await _recentFilesRepo.GetRecentAsync(5);
            Assert.Contains(recentFiles, f => f.FilePath == tempFile);

            await vm.RefreshDataAsync();
            Assert.True(vm.ActiveCount >= 1);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void DevToolkitViewModel_OperationsExecuteCorrectly()
    {
        var vm = _serviceProvider.GetRequiredService<DevToolkitViewModel>();

        // 1. JSON formatting
        vm.InputData = "{\"name\":\"nexus\",\"version\":1}";
        vm.FormatJsonCommand.Execute(null);
        Assert.Contains("\n", vm.OutputData);
        Assert.Contains("\"name\": \"nexus\"", vm.OutputData);

        // 2. Base64 encode
        vm.InputData = "Project Nexus";
        vm.Base64EncodeCommand.Execute(null);
        Assert.Equal("UHJvamVjdCBOZXh1cw==", vm.OutputData);

        // 3. Base64 decode
        vm.InputData = "UHJvamVjdCBOZXh1cw==";
        vm.Base64DecodeCommand.Execute(null);
        Assert.Equal("Project Nexus", vm.OutputData);

        // 4. SHA-256
        vm.InputData = "Project Nexus";
        vm.ComputeSha256Command.Execute(null);
        Assert.NotEmpty(vm.OutputData);
        Assert.Equal(64, vm.OutputData.Length);

        // 5. MD5
        vm.InputData = "Project Nexus";
        vm.ComputeMd5Command.Execute(null);
        Assert.NotEmpty(vm.OutputData);
        Assert.Equal(32, vm.OutputData.Length);

        // 6. SHA-512
        vm.InputData = "Project Nexus";
        vm.ComputeSha512Command.Execute(null);
        Assert.NotEmpty(vm.OutputData);
        Assert.Equal(128, vm.OutputData.Length);

        // 7. Validate JSON
        vm.InputData = "{\"status\": \"ok\", \"code\": 200}";
        vm.ValidateJsonCommand.Execute(null);
        Assert.Contains("strictly VALID", vm.OutputData);
    }

    [Fact]
    public void UniversalConversionViewModel_QueueManagement_WorksCorrectly()
    {
        var vm = _serviceProvider.GetRequiredService<UniversalConversionViewModel>();
        Assert.NotEmpty(vm.TargetFormats);
        Assert.Contains("webp", vm.TargetFormats);
        Assert.Contains("mp3", vm.TargetFormats);
        Assert.Contains("tar.gz", vm.TargetFormats);

        vm.SelectedTargetFormat = "webp";
        Assert.Equal("webp", vm.SelectedTargetFormat);
    }

    [Fact]
    public async Task SettingsViewModel_LoadsAndPersistsConfiguration_Successfully()
    {
        var vm = _serviceProvider.GetRequiredService<SettingsViewModel>();
        await vm.InitializeAsync();

        vm.MaxConcurrency = 8;
        Assert.Equal(8, vm.MaxConcurrency);

        var settingsRepo = _serviceProvider.GetRequiredService<ISettingsRepository>();
        var persistedConcurrency = await settingsRepo.GetSettingAsync("Performance.MaxConcurrency", 4);
        Assert.Equal(8, persistedConcurrency);
    }
}
