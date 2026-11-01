using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using NEXUS.Core.Interfaces;

namespace NEXUS.Plugins;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}

public class PluginManager : IPluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly ConcurrentDictionary<string, (IPluginContract Plugin, PluginLoadContext Context)> _loadedPlugins = new();
    private readonly ConcurrentDictionary<string, string> _discoveredPluginPaths = new();

    public IReadOnlyList<IPluginContract> LoadedPlugins => _loadedPlugins.Values.Select(p => p.Plugin).ToList();

    public PluginManager(ILogger<PluginManager> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<IPluginContract>> DiscoverPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        var results = new List<IPluginContract>();
        if (!Directory.Exists(pluginDirectory))
        {
            Directory.CreateDirectory(pluginDirectory);
            return Task.FromResult<IReadOnlyList<IPluginContract>>(results);
        }

        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
        {
            try
            {
                // Inspect assembly metadata
                var assemblyName = AssemblyName.GetAssemblyName(dll);
                var tempContext = new PluginLoadContext(dll);
                var assembly = tempContext.LoadFromAssemblyPath(dll);

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPluginContract).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IPluginContract plugin)
                    {
                        _discoveredPluginPaths[plugin.Id] = dll;
                        results.Add(plugin);
                    }
                }

                tempContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect candidate plugin assembly '{Dll}'", dll);
            }
        }

        return Task.FromResult<IReadOnlyList<IPluginContract>>(results);
    }

    public Task<bool> LoadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (_loadedPlugins.ContainsKey(pluginId)) return Task.FromResult(true);

        if (!_discoveredPluginPaths.TryGetValue(pluginId, out var dllPath) || !File.Exists(dllPath))
        {
            _logger.LogWarning("Plugin '{PluginId}' not found in discovered plugins cache.", pluginId);
            return Task.FromResult(false);
        }

        try
        {
            var context = new PluginLoadContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPluginContract).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                context.Unload();
                return Task.FromResult(false);
            }

            if (Activator.CreateInstance(pluginType) is IPluginContract plugin)
            {
                if (plugin.Initialize())
                {
                    _loadedPlugins[pluginId] = (plugin, context);
                    _logger.LogInformation("Plugin '{Name}' ({Id}) v{Version} loaded successfully.", plugin.Name, plugin.Id, plugin.Version);
                    return Task.FromResult(true);
                }
            }

            context.Unload();
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin '{PluginId}' from '{Path}'", pluginId, dllPath);
            return Task.FromResult(false);
        }
    }

    public Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (_loadedPlugins.TryRemove(pluginId, out var item))
        {
            try
            {
                item.Plugin.Shutdown();
                item.Context.Unload();
                _logger.LogInformation("Plugin '{PluginId}' unloaded.", pluginId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down plugin '{PluginId}'", pluginId);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false);
    }
}

