using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Communication;
using SolarSharp.Interpreter.Security;
using Spectre.Console;
using WotCI.API;
using WotCI.UI;

namespace WotCI
{
    /// <summary>
    /// Enhanced plugin manager that integrates with the new security infrastructure
    /// </summary>
    public class EnhancedPluginManager : IDisposable
    {
        private readonly GameSimulator _game;
        private readonly IFileSystem _fileSystem;
        private readonly ConcurrentDictionary<string, EnhancedPluginInstance> _plugins =
            new ConcurrentDictionary<string, EnhancedPluginInstance>();
        private readonly IScriptMessageBus _messageBus;
        private readonly IRateLimiter _rateLimiter;
        private readonly object _configLock = new object();
        private string _configPath;
        private string _basePath;
        private PluginConfiguration _config = new PluginConfiguration();

        public EnhancedPluginManager(GameSimulator game, string? basePath = null)
            : this(game, basePath, new FileSystem()) { }

        public EnhancedPluginManager(GameSimulator game, string? basePath, IFileSystem fileSystem)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _basePath = basePath ?? DetectWotCiDirectory();
            _configPath = _fileSystem.Path.Combine(_basePath, "plugin-config.json");

            // Initialize security infrastructure
            SecurityAuditor = new SecurityAuditor(maxEvents: 1000);
            _messageBus = new ScriptMessageBus(SecurityAuditor);
            _rateLimiter = RateLimiters.CreateConservative(SecurityAuditor);
            SecurityDashboard = new SecurityDashboard(SecurityAuditor, _messageBus);

            LoadConfiguration();
        }

        /// <summary>
        /// Gets the security dashboard
        /// </summary>
        public SecurityDashboard SecurityDashboard { get; }

        /// <summary>
        /// Gets the security auditor for testing purposes
        /// </summary>
        public ISecurityAuditor SecurityAuditor { get; }

        /// <summary>
        /// Gets available plugins with enhanced information
        /// </summary>
        public IReadOnlyCollection<EnhancedPluginInfo> GetAvailablePlugins()
        {
            var plugins = new List<EnhancedPluginInfo>();

            // Scan user plugins
            var userPluginsPath = _fileSystem.Path.Combine(_basePath, "plugins", "user");
            if (_fileSystem.Directory.Exists(userPluginsPath))
            {
                foreach (var luaFile in _fileSystem.Directory.GetFiles(userPluginsPath, "*.lua"))
                {
                    var name = _fileSystem.Path.GetFileNameWithoutExtension(luaFile);
                    var isRunning = _plugins.ContainsKey(name);

                    plugins.Add(
                        new EnhancedPluginInfo
                        {
                            Name = name,
                            TrustLevel = PluginTrustLevel.User,
                            Path = luaFile,
                            IsEnabled = _config.EnabledPlugins.Contains(name),
                            IsRunning = isRunning,
                            Description = GetPluginDescription(luaFile),
                            IsHostile = IsHostilePlugin(luaFile),
                            SecurityMetrics = isRunning ? GetPluginSecurityMetrics(name) : null,
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
                        var isRunning = _plugins.ContainsKey(name);

                        plugins.Add(
                            new EnhancedPluginInfo
                            {
                                Name = name,
                                TrustLevel = PluginTrustLevel.Partner,
                                Path = luaFile,
                                IsEnabled = _config.EnabledPlugins.Contains(name),
                                IsRunning = isRunning,
                                Description = GetPluginDescription(luaFile),
                                IsHostile = IsHostilePlugin(luaFile),
                                SecurityMetrics = isRunning ? GetPluginSecurityMetrics(name) : null,
                            }
                        );
                    }
                }
            }

            return plugins.AsReadOnly();
        }

        /// <summary>
        /// Enables a plugin with enhanced security
        /// </summary>
        public async Task EnablePluginAsync(string pluginName)
        {
            var pluginInfo = GetAvailablePlugins().FirstOrDefault(p => p.Name == pluginName);
            if (pluginInfo == null)
                throw new ArgumentException($"Plugin not found: {pluginName}");

            if (_plugins.ContainsKey(pluginName))
            {
                AnsiConsole.MarkupLine($"[yellow]Plugin {pluginName} is already running[/]");
                return;
            }

            try
            {
                var instance = await CreateEnhancedPluginInstanceAsync(pluginInfo);
                if (_plugins.TryAdd(pluginName, instance))
                {
                    lock (_configLock)
                    {
                        _config.EnabledPlugins.Add(pluginName);
                        SaveConfiguration();
                    }

                    AnsiConsole.MarkupLine($"[green]✓ Enabled plugin: {pluginName}[/]");

                    // Start the plugin
                    _ = Task.Run(() => RunEnhancedPluginAsync(instance));
                }
            }
            catch (Exception ex)
            {
                SecurityAuditor.LogSecurityViolation(
                    $"Failed to enable plugin {pluginName}: {ex.Message}",
                    SecurityEventType.UnauthorizedOperation,
                    ex
                );
                AnsiConsole.MarkupLine(
                    $"[red]✗ Failed to enable plugin {pluginName}: {ex.Message}[/]"
                );
                throw;
            }
        }

        /// <summary>
        /// Disables a plugin
        /// </summary>
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

                SecurityAuditor.LogCapabilityUsage(
                    "plugin_manager",
                    "disable",
                    [pluginName],
                    null,
                    true
                );
                AnsiConsole.MarkupLine($"[yellow]✓ Disabled plugin: {pluginName}[/]");
            }
        }

        /// <summary>
        /// Shows the security dashboard
        /// </summary>
        public async Task ShowSecurityDashboardAsync()
        {
            var interactiveDashboard = new InteractiveSecurityDashboard(SecurityDashboard);
            await interactiveDashboard.RunAsync();
        }

        /// <summary>
        /// Gets security statistics for all plugins
        /// </summary>
        public SecurityMetrics GetOverallSecurityMetrics()
        {
            return SecurityAuditor.GetSecurityMetrics();
        }

        /// <summary>
        /// Creates an enhanced plugin instance asynchronously with configured security, capabilities, and reflection protection.
        /// </summary>
        /// <param name="pluginInfo">The metadata and trust level of the plugin used to configure the instance.</param>
        /// <returns>A task that represents the asynchronous operation, containing the created enhanced plugin instance.</returns>
        private Task<EnhancedPluginInstance> CreateEnhancedPluginInstanceAsync(
            EnhancedPluginInfo pluginInfo
        )
        {
            // Create security configuration based on trust level
            var securityConfig = CreateSecurityConfiguration(pluginInfo.TrustLevel);

            // Create script with enhanced security
            var basePolicySet = Examples.DesktopBasePolicySet;
            var script = new Script(basePolicySet);

            // Create API gateway and register capabilities
            var gateway = new ScriptAPIGateway(securityConfig, _rateLimiter, SecurityAuditor);
            var apiFacade = new EnhancedGameApiFacade(
                _game,
                gateway,
                _messageBus,
                SecurityAuditor,
                _rateLimiter
            );

            // Setup capabilities based on trust level
            apiFacade.SetupCapabilities(pluginInfo.TrustLevel);

            // Register script with message bus
            var scriptId =
                pluginInfo.Name ?? throw new ArgumentException("Plugin name cannot be null");
            var communicationPolicy = CreateCommunicationPolicy(scriptId, pluginInfo.TrustLevel);
            _messageBus.RegisterScript(scriptId, communicationPolicy);

            // Create enhanced API
            var enhancedApi = apiFacade.CreateEnhancedApi(script, scriptId, pluginInfo.TrustLevel);
            script.Globals["api"] = enhancedApi;

            // Apply reflection protection
            var reflectionProtection = securityConfig.CreateReflectionProtection(SecurityAuditor);
            reflectionProtection.SanitizeGlobalEnvironment(script.Globals);

            var instance = new EnhancedPluginInstance
            {
                Info = pluginInfo,
                Script = script,
                SecurityConfig = securityConfig,
                ApiGateway = gateway,
                ApiFacade = apiFacade,
                ReflectionProtection = reflectionProtection,
                CancellationTokenSource = new CancellationTokenSource(),
                StartTime = DateTime.UtcNow,
            };

            return Task.FromResult(instance);
        }

        /// <summary>
        /// Creates a security configuration based on the specified plugin trust level.
        /// </summary>
        /// <param name="trustLevel">The trust level of the plugin, determining the security constraints and resource limits.</param>
        /// <returns>A <see cref="SecurityPolicy"/> object configured according to the specified trust level.</returns>
        private static SecurityPolicy CreateSecurityConfiguration(PluginTrustLevel trustLevel)
        {
            return trustLevel switch
            {
                PluginTrustLevel.User => Examples.Isolated() with
                {
                    TimeoutMs = 30 * 1000, // 30 seconds
                    MaxMemoryMB = 10,
                    MaxInstructions = 100_000,
                },

                PluginTrustLevel.Partner => Examples.DataProcessing() with
                {
                    TimeoutMs = 5 * 60 * 1000, // 5 minutes
                    MaxMemoryMB = 50,
                    MaxInstructions = 1_000_000,
                },

                PluginTrustLevel.System => Examples.Automation(),

                _ => throw new ArgumentOutOfRangeException(nameof(trustLevel)),
            };
        }

        /// <summary>
        /// Creates a communication policy for a script based on its trust level and script ID.
        /// </summary>
        /// <param name="scriptId">The unique identifier of the script for which the communication policy is being created.</param>
        /// <param name="trustLevel">The trust level of the script, determining the communication permissions and restrictions.</param>
        /// <returns>A <see cref="ScriptCommunicationPolicy"/> instance that defines the communication rules for the given script.</returns>
        private static ScriptCommunicationPolicy CreateCommunicationPolicy(
            string scriptId,
            PluginTrustLevel trustLevel
        )
        {
            return trustLevel switch
            {
                PluginTrustLevel.User => ScriptCommunicationPolicy.Restrictive(scriptId),
                PluginTrustLevel.Partner => new ScriptCommunicationPolicy
                {
                    ScriptId = scriptId,
                    CanSendTypes = ["game.*", "ui.*", "data.*"],
                    CanReceiveTypes = ["game.*", "ui.*", "system.config"],
                    MaxMessageSize = 1024 * 1024, // 1MB
                    MaxMessagesPerMinute = 100,
                    RequireSignature = false,
                    EnableAuditLogging = true,
                },
                PluginTrustLevel.System => ScriptCommunicationPolicy.Permissive(scriptId),
                _ => ScriptCommunicationPolicy.Restrictive(scriptId),
            };
        }

        /// <summary>
        /// Executes the enhanced plugin asynchronously in a controlled and secure environment.
        /// </summary>
        /// <param name="instance">The enhanced plugin instance to execute.</param>
        /// <returns>A task that represents the asynchronous operation of running the plugin.</returns>
        private async Task RunEnhancedPluginAsync(EnhancedPluginInstance instance)
        {
            try
            {
                SecurityAuditor.LogCapabilityUsage(
                    "plugin_manager",
                    "start",
                    [instance.Info?.Name ?? "Unknown"],
                    null,
                    true
                );

                // Load and execute the plugin file
                var luaCode = await _fileSystem.File.ReadAllTextAsync(
                    instance.Info?.Path
                        ?? throw new InvalidOperationException("Plugin path is null")
                );

                // Execute in a controlled environment with enhanced security
                await Task.Run(
                    () =>
                    {
                        try
                        {
                            instance.Script?.DoString(luaCode);

                            // Keep plugin alive if it has update function
                            if (HasUpdateFunction(luaCode))
                            {
                                RunPluginUpdateLoop(instance);
                            }
                        }
                        catch (Exception ex)
                        {
                            SecurityAuditor.LogSecurityViolation(
                                $"Plugin execution error: {instance.Info.Name} - {ex.Message}",
                                SecurityEventType.UnauthorizedOperation,
                                ex
                            );
                            if (instance.Info?.Name != null)
                                DisablePlugin(instance.Info.Name);
                        }
                    },
                    instance.CancellationTokenSource?.Token ?? CancellationToken.None
                );
            }
            catch (OperationCanceledException)
            {
                SecurityAuditor.LogCapabilityUsage(
                    "plugin_manager",
                    "stop",
                    [instance.Info?.Name ?? "Unknown"],
                    null,
                    true
                );
            }
            catch (Exception ex)
            {
                SecurityAuditor.LogSecurityViolation(
                    $"Plugin failure: {instance.Info?.Name ?? "Unknown"} - {ex.Message}",
                    SecurityEventType.UnauthorizedOperation,
                    ex
                );
            }
        }

        /// <summary>
        /// Continuously executes the update loop for a plugin until cancellation is requested or an error occurs.
        /// </summary>
        /// <param name="instance">The instance of the enhanced plugin to run the update loop for.</param>
        private void RunPluginUpdateLoop(EnhancedPluginInstance instance)
        {
            while (!(instance.CancellationTokenSource?.Token.IsCancellationRequested ?? true))
            {
                try
                {
                    instance.Script?.Call(instance.Script?.Globals["update"]);
                }
                catch (Exception ex)
                {
                    SecurityAuditor?.LogSecurityViolation(
                        $"Plugin update error: {instance.Info?.Name ?? "Unknown"} - {ex.Message}",
                        SecurityEventType.UnauthorizedOperation,
                        ex
                    );
                    break;
                }

                Thread.Sleep(1000); // Update every second
            }
        }

        private PluginSecurityMetrics? GetPluginSecurityMetrics(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var instance))
                return null;

            var auditEvents = SecurityAuditor
                .GetRecentEvents()
                .Where(e =>
                    e.Description.Contains(pluginName) || e.CapabilityName.Contains(pluginName)
                )
                .ToList();

            return new PluginSecurityMetrics
            {
                PluginName = pluginName,
                ViolationCount = auditEvents.Count(e => !e.Success),
                CapabilityUsageCount = auditEvents.Count(e =>
                    e.EventType == SecurityEventType.CapabilityUsage
                ),
                LastActivity = auditEvents.Any()
                    ? auditEvents.Max(e => e.Timestamp)
                    : instance.StartTime,
                ExecutionTime = DateTime.UtcNow - instance.StartTime,
            };
        }

        private string DetectWotCiDirectory()
        {
            var currentDir = _fileSystem.Directory.GetCurrentDirectory();

            if (_fileSystem.Directory.Exists(_fileSystem.Path.Combine(currentDir, "plugins")))
                return currentDir;

            var searchDir = currentDir;
            for (var i = 0; i < 10; i++)
            {
                var wotciPath = _fileSystem.Path.Combine(searchDir, "WotCI");
                if (
                    _fileSystem.Directory.Exists(wotciPath)
                    && _fileSystem.Directory.Exists(_fileSystem.Path.Combine(wotciPath, "plugins"))
                )
                    return wotciPath;

                var parent = _fileSystem.DirectoryInfo.New(searchDir).Parent;
                if (parent == null)
                    break;
                searchDir = parent.FullName;
            }

            return currentDir;
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
                SecurityAuditor?.LogCapabilityUsage(
                    "plugin_loader",
                    "description_read_error",
                    [filePath, ex.Message],
                    null,
                    false
                );
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
                SecurityAuditor?.LogCapabilityUsage(
                    "plugin_loader",
                    "hostile_check_error",
                    [filePath, ex.Message],
                    null,
                    false
                );
            }

            return false;
        }

        private bool HasUpdateFunction(string luaCode)
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
                SecurityAuditor?.LogSecurityViolation(
                    $"Failed to load plugin configuration: {ex.Message}",
                    SecurityEventType.PolicyViolation,
                    ex
                );
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
                SecurityAuditor.LogSecurityViolation(
                    $"Failed to save plugin configuration: {ex.Message}",
                    SecurityEventType.UnauthorizedOperation,
                    ex
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
            (_messageBus as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Enhanced plugin instance with security features
    /// </summary>
    public class EnhancedPluginInstance : IDisposable
    {
        public EnhancedPluginInfo? Info { get; set; }
        public Script? Script { get; set; }
        public SecurityPolicy? SecurityConfig { get; set; }
        public IScriptAPIGateway? ApiGateway { get; set; }
        public EnhancedGameApiFacade? ApiFacade { get; set; }
        public ReflectionProtection? ReflectionProtection { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public DateTime StartTime { get; set; }

        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Enhanced plugin information with security metrics
    /// </summary>
    public class EnhancedPluginInfo : PluginInfo
    {
        public bool IsRunning { get; set; }
        public PluginSecurityMetrics? SecurityMetrics { get; set; }
    }

    /// <summary>
    /// Represents security metrics associated with a specific plugin, including violation counts,
    /// capability usage, last activity, and cumulative execution time.
    /// </summary>
    public class PluginSecurityMetrics
    {
        public string PluginName { get; set; } = string.Empty;
        public int ViolationCount { get; set; }
        public int CapabilityUsageCount { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}
