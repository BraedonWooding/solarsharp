using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Text.Json;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;
using Spectre.Console;
using Table = SolarSharp.Interpreter.DataTypes.Table;

namespace WotCI
{
    /// <summary>
    /// Manages plugin lifecycle with individual enable/disable control
    /// </summary>
    public class PluginManager : IDisposable
    {
        private readonly GameSimulator _game;
        private readonly IFileSystem _fileSystem;
        private readonly ConcurrentDictionary<string, PluginInstance> _plugins =
            new ConcurrentDictionary<string, PluginInstance>();
        private readonly object _configLock = new object();
        private string _configPath = string.Empty;
        private string _basePath = string.Empty;
        private PluginConfiguration _config = new PluginConfiguration();

        public event EventHandler<SecurityViolationEventArgs>? SecurityViolation;

        public PluginManager(GameSimulator game)
            : this(game, null, new FileSystem()) { }

        public PluginManager(GameSimulator game, string? basePath)
            : this(game, basePath, new FileSystem()) { }

        public PluginManager(GameSimulator game, string? basePath, IFileSystem fileSystem)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _basePath = basePath ?? DetectWotCiDirectory();
            _configPath = _fileSystem.Path.Combine(_basePath, "plugin-config.json");
            LoadConfiguration();
        }

        private string DetectWotCiDirectory()
        {
            // Try to detect the WotCI directory automatically
            var currentDir = _fileSystem.Directory.GetCurrentDirectory();

            // Case 1: We're already in the WotCI directory
            if (_fileSystem.Directory.Exists(_fileSystem.Path.Combine(currentDir, "plugins")))
            {
                return currentDir;
            }

            // Case 2: We're in a subdirectory (like bin/Debug/net6.0 during tests)
            var searchDir = currentDir;
            for (var i = 0; i < 10; i++) // Limit search depth
            {
                // Look for the WotCI directory
                var wotciPath = _fileSystem.Path.Combine(searchDir, "WotCI");
                if (
                    _fileSystem.Directory.Exists(wotciPath)
                    && _fileSystem.Directory.Exists(_fileSystem.Path.Combine(wotciPath, "plugins"))
                )
                {
                    return wotciPath;
                }

                // Go up one level
                var parent = _fileSystem.DirectoryInfo.New(searchDir).Parent;
                if (parent == null)
                    break;
                searchDir = parent.FullName;
            }

            // Case 3: Default fallback - use current directory and hope for the best
            return currentDir;
        }

        public IReadOnlyCollection<PluginInfo> GetAvailablePlugins()
        {
            var plugins = new List<PluginInfo>();

            // Scan user plugins
            var userPluginsPath = _fileSystem.Path.Combine(_basePath, "plugins", "user");
            if (_fileSystem.Directory.Exists(userPluginsPath))
            {
                foreach (var luaFile in _fileSystem.Directory.GetFiles(userPluginsPath, "*.lua"))
                {
                    var name = _fileSystem.Path.GetFileNameWithoutExtension(luaFile);
                    plugins.Add(
                        new PluginInfo
                        {
                            Name = name,
                            TrustLevel = PluginTrustLevel.User,
                            Path = luaFile,
                            IsEnabled = _config.EnabledPlugins.Contains(name),
                            Description = GetPluginDescription(luaFile),
                            IsHostile = IsHostilePlugin(luaFile),
                        }
                    );
                }
            }

            // Scan partner plugins
            foreach (var partnerDir in new[] { "deadlock-digital", "segfault-studios" })
            {
                var pluginDir = _fileSystem.Path.Combine(_basePath, "plugins", partnerDir);
                if (_fileSystem.Directory.Exists(pluginDir))
                {
                    foreach (var luaFile in _fileSystem.Directory.GetFiles(pluginDir, "*.lua"))
                    {
                        var name =
                            $"{partnerDir}/{_fileSystem.Path.GetFileNameWithoutExtension(luaFile)}";
                        plugins.Add(
                            new PluginInfo
                            {
                                Name = name,
                                TrustLevel = PluginTrustLevel.Partner,
                                Path = luaFile,
                                IsEnabled = _config.EnabledPlugins.Contains(name),
                                Description = GetPluginDescription(luaFile),
                                IsHostile = IsHostilePlugin(luaFile),
                            }
                        );
                    }
                }
            }

            return plugins.AsReadOnly();
        }

        public async Task EnablePluginAsync(string pluginName)
        {
            var pluginInfo = GetAvailablePlugins().FirstOrDefault(p => p.Name == pluginName);
            if (pluginInfo == null)
            {
                throw new ArgumentException($"Plugin not found: {pluginName}");
            }

            if (_plugins.ContainsKey(pluginName))
            {
                AnsiConsole.MarkupLine($"[yellow]Plugin {pluginName} is already running[/]");
                return;
            }

            try
            {
                var instance = await CreatePluginInstanceAsync(pluginInfo);
                if (_plugins.TryAdd(pluginName, instance))
                {
                    lock (_configLock)
                    {
                        _config.EnabledPlugins.Add(pluginName);
                        SaveConfiguration();
                    }

                    AnsiConsole.MarkupLine($"[green]Enabled plugin: {pluginName}[/]");

                    // Start the plugin
                    _ = Task.Run(() => RunPluginAsync(instance));
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Failed to enable plugin {pluginName}: {ex.Message}[/]"
                );
                throw;
            }
        }

        public void DisablePlugin(string pluginName)
        {
            if (_plugins.TryRemove(pluginName, out var instance))
            {
                instance.Dispose();

                lock (_configLock)
                {
                    _config.EnabledPlugins.Remove(pluginName);
                    SaveConfiguration();
                }

                AnsiConsole.MarkupLine($"[yellow]Disabled plugin: {pluginName}[/]");
            }
        }

        private Task<PluginInstance> CreatePluginInstanceAsync(PluginInfo pluginInfo)
        {
            var config = CreateSecurityPolicy(pluginInfo);
            var script = new Script(Examples.DesktopBasePolicySet);

            // Apply security configuration
            // Note: This is where we'd integrate with SolarSharp's security system
            // For now, we'll simulate the security boundaries

            var instance = new PluginInstance
            {
                Info = pluginInfo,
                Script = script,
                Config = config,
                CancellationTokenSource = new CancellationTokenSource(),
                StartTime = DateTime.UtcNow,
            };

            // Set up game API with restrictions based on trust level
            SetupGameApi(script, pluginInfo.TrustLevel);

            return Task.FromResult(instance);
        }

        private SecurityPolicy CreateSecurityPolicy(PluginInfo pluginInfo)
        {
            return pluginInfo.TrustLevel switch
            {
                PluginTrustLevel.User => Examples.Isolated() with
                {
                    TimeoutMs = WotCISecurityConstants.Plugins.UserPluginTimeoutSeconds * 1000,
                    MaxMemoryMB = WotCISecurityConstants.Plugins.UserPluginMemoryLimitMB,
                    MaxInstructions = WotCISecurityConstants.Plugins.UserPluginInstructionLimit,
                },

                PluginTrustLevel.Partner => Examples.Desktop() with
                {
                    TimeoutMs = WotCISecurityConstants.Plugins.PartnerPluginTimeoutMinutes * 60 * 1000,
                    MaxMemoryMB = WotCISecurityConstants.Plugins.PartnerPluginMemoryLimitMB,
                },

                PluginTrustLevel.System => Examples.Automation(),

                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private void SetupGameApi(Script script, PluginTrustLevel trustLevel)
        {
            var api = new Table
            {
                // Basic API available to all plugins
                ["print"] =
                    (Action<string>)(msg => AnsiConsole.MarkupLine($"[cyan][Plugin][/] {msg}")),
                ["log"] =
                    (Action<string>)(
                        msg => AnsiConsole.MarkupLine($"[dim][{DateTime.Now:HH:mm:ss}][/] {msg}")
                    ),
            };

            // Game state access (read-only for users, read-write for partners)
            if (trustLevel >= PluginTrustLevel.Partner)
            {
                api["getPlayerHealth"] = (Func<int>)(() => _game.GetPlayerHealth());
                api["setPlayerHealth"] = (Action<int>)(health => _game.SetPlayerHealth(health));
                api["getPlayerGold"] = (Func<int>)(() => _game.GetPlayerGold());
                api["giveGold"] = (Action<int>)(amount => _game.GiveGold(amount));
            }
            else
            {
                // User plugins get limited read-only access
                api["getPlayerHealth"] = (Func<int>)(() => _game.GetPlayerHealth());
                api["getPlayerGold"] = (Func<int>)(() => _game.GetPlayerGold());
            }

            // File system access is handled by security configuration
            if (trustLevel >= PluginTrustLevel.Partner)
            {
                api["writeLog"] =
                    (Action<string>)(
                        message =>
                        {
                            try
                            {
                                var logPath = _fileSystem.Path.Combine(
                                    "game",
                                    "logs",
                                    "plugin.log"
                                );
                                _fileSystem.File.AppendAllText(
                                    logPath,
                                    $"[{DateTime.Now}] {message}\n"
                                );
                            }
                            catch (Exception ex)
                            {
                                OnSecurityViolation("File access violation", ex);
                            }
                        }
                    );
            }

            script.Globals["game"] = api;
        }

        private async Task RunPluginAsync(PluginInstance instance)
        {
            try
            {
                AnsiConsole.MarkupLine(
                    $"[blue]Starting plugin: {instance.Info?.Name ?? "Unknown"}[/]"
                );

                // Load and execute the plugin file
                var luaCode = await _fileSystem.File.ReadAllTextAsync(
                    instance.Info?.Path
                        ?? throw new InvalidOperationException("Plugin path is null")
                );

                // Execute plugin startup code
                await Task.Run(
                    () => instance.Script?.DoString(luaCode),
                    instance.CancellationTokenSource?.Token ?? CancellationToken.None
                );

                // If this is a long-running plugin, run the update loop
                if (IsLongRunningPlugin(luaCode))
                {
                    await RunPluginUpdateLoopAsync(instance);
                }
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Plugin stopped: {instance.Info?.Name ?? "Unknown"}[/]"
                );
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Plugin failed: {instance.Info?.Name ?? "Unknown"} - {ex.Message}[/]"
                );
                OnSecurityViolation($"Plugin failure: {instance.Info?.Name ?? "Unknown"}", ex);
            }
        }

        private async Task RunPluginUpdateLoopAsync(PluginInstance instance)
        {
            var cancellationToken =
                instance.CancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Call plugin update function if it exists
                    try
                    {
                        await Task.Run(
                            () => instance.Script?.Call(instance.Script?.Globals["update"]),
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        // Log non-critical error - update function is optional
                        Debug.WriteLine(
                            $"Plugin update error for {instance.Info?.Name ?? "Unknown"}: {ex.Message}"
                        );
                    }

                    // Use async delay instead of Thread.Sleep
                    await Task.Delay(
                        WotCISecurityConstants.Plugins.PluginUpdateInterval,
                        cancellationToken
                    );
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, plugin is stopping
            }
            catch (Exception ex)
            {
                OnSecurityViolation($"Plugin update loop error: {instance.Info?.Name}", ex);
                if (instance.Info?.Name != null)
                    DisablePlugin(instance.Info.Name);
            }
        }

        private void OnSecurityViolation(string description, Exception exception)
        {
            var args = new SecurityViolationEventArgs
            {
                Description = description,
                Exception = exception,
                Timestamp = DateTime.UtcNow,
            };

            SecurityViolation?.Invoke(this, args);

            AnsiConsole.MarkupLine($"[red]SECURITY VIOLATION: {description}[/]");
            if (exception != null)
            {
                AnsiConsole.MarkupLine($"[red]   {exception.Message}[/]");
            }
        }

        private string GetPluginDescription(string filePath)
        {
            try
            {
                var lines = _fileSystem.File.ReadLines(filePath).Take(10);
                foreach (var line in lines)
                {
                    if (
                        line.TrimStart().StartsWith("-- ")
                        && !line.Contains("By ")
                        && !line.Contains("Plugin")
                    )
                    {
                        return line.TrimStart().Substring(3).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log non-critical error - description is optional
                Debug.WriteLine($"Failed to read plugin description from {filePath}: {ex.Message}");
            }

            return "No description available";
        }

        private bool IsHostilePlugin(string filePath)
        {
            try
            {
                var content = _fileSystem.File.ReadAllText(filePath);
                return content.Contains("HOSTILE")
                    || content.Contains("malicious")
                    || content.Contains("attack")
                    || content.Contains("exploit")
                    || _fileSystem.Path.GetFileName(filePath).Contains("hostile");
            }
            catch (Exception ex)
            {
                // Log error and assume plugin is safe if we can't read it
                Debug.WriteLine(
                    $"Failed to check if plugin is hostile from {filePath}: {ex.Message}"
                );
            }

            return false;
        }

        private bool IsLongRunningPlugin(string luaCode)
        {
            return luaCode.Contains("function update")
                || luaCode.Contains("while true")
                || luaCode.Contains("timer")
                || luaCode.Contains("delay");
        }

        private void LoadConfiguration()
        {
            try
            {
                if (_fileSystem.File.Exists(_configPath))
                {
                    var json = _fileSystem.File.ReadAllText(_configPath);
                    _config =
                        JsonSerializer.Deserialize<PluginConfiguration>(json)
                        ?? new PluginConfiguration();
                }
                else
                {
                    _config = new PluginConfiguration();
                }
            }
            catch (Exception ex)
            {
                // Log error and use default configuration
                AnsiConsole.MarkupLine(
                    $"[yellow]Failed to load plugin configuration: {ex.Message}[/]"
                );
                AnsiConsole.MarkupLine("[yellow]  Using default configuration[/]");
                _config = new PluginConfiguration();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    _config,
                    new JsonSerializerOptions { WriteIndented = true }
                );
                _fileSystem.File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Failed to save plugin configuration: {ex.Message}[/]"
                );
            }
        }

        public void Dispose()
        {
            foreach (var plugin in _plugins.Values)
            {
                plugin.Dispose();
            }
            _plugins.Clear();
        }
    }

    public class PluginInstance : IDisposable
    {
        public PluginInfo? Info { get; set; }
        public Script? Script { get; set; }
        public SecurityPolicy? Config { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public DateTime StartTime { get; set; }

        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
            // Script will be garbage collected
        }
    }

    public class PluginInfo
    {
        public string? Name { get; set; }
        public PluginTrustLevel TrustLevel { get; set; }
        public string? Path { get; set; }
        public bool IsEnabled { get; set; }
        public string? Description { get; set; }
        public bool IsHostile { get; set; }
    }

    public enum PluginTrustLevel
    {
        User = 0,
        Partner = 1,
        System = 2,
    }

    public class PluginConfiguration
    {
        public HashSet<string> EnabledPlugins { get; set; } = new HashSet<string>();
    }

    public class SecurityViolationEventArgs : EventArgs
    {
        public string? Description { get; set; }
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
