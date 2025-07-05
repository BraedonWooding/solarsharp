using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, EnhancedPluginInstance> _plugins = new();
        private readonly ISecurityAuditor _auditor;
        private readonly IScriptMessageBus _messageBus;
        private readonly IRateLimiter _rateLimiter;
        private readonly SecurityDashboard _securityDashboard;
        private readonly object _configLock = new object();
        private string _configPath = string.Empty;
        private string _basePath = string.Empty;
        private PluginConfiguration _config = new();

        public EnhancedPluginManager(GameSimulator game, string? basePath = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _basePath = basePath ?? DetectWotCIDirectory();
            _configPath = Path.Combine(_basePath, "plugin-config.json");

            // Initialize security infrastructure
            _auditor = new SecurityAuditor(maxEvents: 1000);
            _messageBus = new ScriptMessageBus(_auditor);
            _rateLimiter = RateLimiters.CreateConservative(_auditor);
            _securityDashboard = new SecurityDashboard(_auditor, _messageBus, null);

            LoadConfiguration();
        }

        /// <summary>
        /// Gets the security dashboard
        /// </summary>
        public SecurityDashboard SecurityDashboard => _securityDashboard;

        /// <summary>
        /// Gets available plugins with enhanced information
        /// </summary>
        public IReadOnlyCollection<EnhancedPluginInfo> GetAvailablePlugins()
        {
            var plugins = new List<EnhancedPluginInfo>();
            
            // Scan user plugins
            var userPluginsPath = Path.Combine(_basePath, "plugins", "user");
            if (Directory.Exists(userPluginsPath))
            {
                foreach (var luaFile in Directory.GetFiles(userPluginsPath, "*.lua"))
                {
                    var name = Path.GetFileNameWithoutExtension(luaFile);
                    var isRunning = _plugins.ContainsKey(name);
                    
                    plugins.Add(new EnhancedPluginInfo
                    {
                        Name = name,
                        TrustLevel = PluginTrustLevel.User,
                        Path = luaFile,
                        IsEnabled = _config.EnabledPlugins.Contains(name),
                        IsRunning = isRunning,
                        Description = GetPluginDescription(luaFile),
                        IsHostile = IsHostilePlugin(luaFile),
                        SecurityMetrics = isRunning ? GetPluginSecurityMetrics(name) : null
                    });
                }
            }

            // Scan partner plugins
            foreach (var partnerDir in new[] { "deadlock-digital", "segfault-studios" })
            {
                var pluginDir = Path.Combine(_basePath, "plugins", partnerDir);
                if (Directory.Exists(pluginDir))
                {
                    foreach (var luaFile in Directory.GetFiles(pluginDir, "*.lua"))
                    {
                        var name = $"{partnerDir}/{Path.GetFileNameWithoutExtension(luaFile)}";
                        var isRunning = _plugins.ContainsKey(name);
                        
                        plugins.Add(new EnhancedPluginInfo
                        {
                            Name = name,
                            TrustLevel = PluginTrustLevel.Partner,
                            Path = luaFile,
                            IsEnabled = _config.EnabledPlugins.Contains(name),
                            IsRunning = isRunning,
                            Description = GetPluginDescription(luaFile),
                            IsHostile = IsHostilePlugin(luaFile),
                            SecurityMetrics = isRunning ? GetPluginSecurityMetrics(name) : null
                        });
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
                _auditor.LogSecurityViolation($"Failed to enable plugin {pluginName}: {ex.Message}", 
                    SecurityEventType.UnauthorizedOperation, ex);
                AnsiConsole.MarkupLine($"[red]✗ Failed to enable plugin {pluginName}: {ex.Message}[/]");
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
                
                _auditor.LogCapabilityUsage("plugin_manager", "disable", new object[] { pluginName }, null, true);
                AnsiConsole.MarkupLine($"[yellow]✓ Disabled plugin: {pluginName}[/]");
            }
        }

        /// <summary>
        /// Shows the security dashboard
        /// </summary>
        public async Task ShowSecurityDashboardAsync()
        {
            var interactiveDashboard = new InteractiveSecurityDashboard(_securityDashboard);
            await interactiveDashboard.RunAsync();
        }

        /// <summary>
        /// Gets security statistics for all plugins
        /// </summary>
        public SecurityMetrics GetOverallSecurityMetrics()
        {
            return _auditor.GetSecurityMetrics();
        }

        private Task<EnhancedPluginInstance> CreateEnhancedPluginInstanceAsync(EnhancedPluginInfo pluginInfo)
        {
            // Create security configuration based on trust level
            var securityConfig = CreateSecurityConfiguration(pluginInfo.TrustLevel);
            
            // Create script with enhanced security
            var script = new Script(securityConfig);
            
            // Create API gateway and register capabilities
            var gateway = new ScriptAPIGateway(securityConfig, _rateLimiter, _auditor);
            var apiFacade = new EnhancedGameAPIFacade(_game, gateway, _messageBus, _auditor, _rateLimiter);
            
            // Setup capabilities based on trust level
            apiFacade.SetupCapabilities(pluginInfo.TrustLevel);
            
            // Register script with message bus
            var scriptId = pluginInfo.Name ?? throw new ArgumentException("Plugin name cannot be null");
            var communicationPolicy = CreateCommunicationPolicy(scriptId, pluginInfo.TrustLevel);
            _messageBus.RegisterScript(scriptId, communicationPolicy);
            
            // Create enhanced API
            var enhancedAPI = apiFacade.CreateEnhancedAPI(script, scriptId, pluginInfo.TrustLevel);
            script.Globals["api"] = enhancedAPI;
            
            // Apply reflection protection
            var reflectionProtection = securityConfig.CreateReflectionProtection(_auditor);
            reflectionProtection.SanitizeGlobalEnvironment(script.Globals);
            
            var instance = new EnhancedPluginInstance
            {
                Info = pluginInfo,
                Script = script,
                SecurityConfig = securityConfig,
                APIGateway = gateway,
                APIFacade = apiFacade,
                ReflectionProtection = reflectionProtection,
                CancellationTokenSource = new CancellationTokenSource(),
                StartTime = DateTime.UtcNow
            };

            return Task.FromResult(instance);
        }

        private SecurityConfiguration CreateSecurityConfiguration(PluginTrustLevel trustLevel)
        {
            return trustLevel switch
            {
                PluginTrustLevel.User => SecurityConfiguration.Isolated()
                    .WithTimeout(TimeSpan.FromSeconds(30))
                    .WithMemoryLimitMB(10)
                    .WithInstructionLimit(100_000),
                        
                PluginTrustLevel.Partner => SecurityConfiguration.DataProcessing()
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .WithMemoryLimitMB(50)
                    .WithInstructionLimit(1_000_000),
                        
                PluginTrustLevel.System => SecurityConfiguration.Automation(),
                
                _ => throw new ArgumentOutOfRangeException(nameof(trustLevel))
            };
        }

        private ScriptCommunicationPolicy CreateCommunicationPolicy(string scriptId, PluginTrustLevel trustLevel)
        {
            return trustLevel switch
            {
                PluginTrustLevel.User => ScriptCommunicationPolicy.Restrictive(scriptId),
                PluginTrustLevel.Partner => new ScriptCommunicationPolicy
                {
                    ScriptId = scriptId,
                    CanSendTypes = new HashSet<string> { "game.*", "ui.*", "data.*" },
                    CanReceiveTypes = new HashSet<string> { "game.*", "ui.*", "system.config" },
                    MaxMessageSize = 1024 * 1024, // 1MB
                    MaxMessagesPerMinute = 100,
                    RequireSignature = false,
                    EnableAuditLogging = true
                },
                PluginTrustLevel.System => ScriptCommunicationPolicy.Permissive(scriptId),
                _ => ScriptCommunicationPolicy.Restrictive(scriptId)
            };
        }

        private async Task RunEnhancedPluginAsync(EnhancedPluginInstance instance)
        {
            try
            {
                _auditor.LogCapabilityUsage("plugin_manager", "start", 
                    new object[] { instance.Info?.Name ?? "Unknown" }, null, true);
                
                // Load and execute the plugin file
                var luaCode = await File.ReadAllTextAsync(instance.Info?.Path ?? throw new InvalidOperationException("Plugin path is null"));
                
                // Execute in a controlled environment with enhanced security
                await Task.Run(() => 
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
                        _auditor.LogSecurityViolation($"Plugin execution error: {instance.Info.Name} - {ex.Message}", 
                            SecurityEventType.UnauthorizedOperation, ex);
                        if (instance.Info?.Name != null) DisablePlugin(instance.Info.Name);
                    }
                }, instance.CancellationTokenSource?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _auditor.LogCapabilityUsage("plugin_manager", "stop", 
                    new object[] { instance.Info?.Name ?? "Unknown" }, null, true);
            }
            catch (Exception ex)
            {
                _auditor.LogSecurityViolation($"Plugin failure: {instance.Info?.Name ?? "Unknown"} - {ex.Message}", 
                    SecurityEventType.UnauthorizedOperation, ex);
            }
        }

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
                    _auditor?.LogSecurityViolation($"Plugin update error: {instance.Info?.Name ?? "Unknown"} - {ex.Message}", 
                        SecurityEventType.UnauthorizedOperation, ex);
                    break;
                }
                
                Thread.Sleep(1000); // Update every second
            }
        }

        private PluginSecurityMetrics? GetPluginSecurityMetrics(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var instance))
                return null;

            var auditEvents = _auditor.GetRecentEvents(100)
                .Where(e => e.Description.Contains(pluginName) || e.CapabilityName.Contains(pluginName))
                .ToList();

            return new PluginSecurityMetrics
            {
                PluginName = pluginName,
                ViolationCount = auditEvents.Count(e => !e.Success),
                CapabilityUsageCount = auditEvents.Count(e => e.EventType == SecurityEventType.CapabilityUsage),
                LastActivity = auditEvents.Any() ? auditEvents.Max(e => e.Timestamp) : instance.StartTime,
                ExecutionTime = DateTime.UtcNow - instance.StartTime
            };
        }

        private string DetectWotCIDirectory()
        {
            var currentDir = Directory.GetCurrentDirectory();
            
            if (Directory.Exists(Path.Combine(currentDir, "plugins")))
                return currentDir;
            
            var searchDir = currentDir;
            for (int i = 0; i < 10; i++)
            {
                var wotciPath = Path.Combine(searchDir, "WotCI");
                if (Directory.Exists(wotciPath) && Directory.Exists(Path.Combine(wotciPath, "plugins")))
                    return wotciPath;
                
                var parent = Directory.GetParent(searchDir);
                if (parent == null) break;
                searchDir = parent.FullName;
            }
            
            return currentDir;
        }

        private string GetPluginDescription(string filePath)
        {
            try
            {
                var lines = File.ReadLines(filePath).Take(10);
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("-- ") && 
                        !line.Contains("By ") && 
                        !line.Contains("Plugin"))
                    {
                        return line.TrimStart().Substring(3).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log non-critical error - description is optional
                _auditor?.LogCapabilityUsage("plugin_loader", "description_read_error", 
                    new object[] { filePath, ex.Message }, null, false);
            }
            
            return "No description available";
        }

        private bool IsHostilePlugin(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                return content.Contains("HOSTILE") || 
                       content.Contains("malicious") ||
                       content.Contains("attack") ||
                       content.Contains("exploit") ||
                       Path.GetFileName(filePath).Contains("hostile");
            }
            catch (Exception ex)
            {
                // Log error and assume plugin is safe if we can't read it
                _auditor?.LogCapabilityUsage("plugin_loader", "hostile_check_error", 
                    new object[] { filePath, ex.Message }, null, false);
            }
            
            return false;
        }

        private bool HasUpdateFunction(string luaCode)
        {
            return luaCode.Contains("function update") || 
                   luaCode.Contains("while true") ||
                   luaCode.Contains("timer") ||
                   luaCode.Contains("delay");
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = System.Text.Json.JsonSerializer.Deserialize<PluginConfiguration>(json) ?? new PluginConfiguration();
                }
                else
                {
                    _config = new PluginConfiguration();
                }
            }
            catch (Exception ex)
            {
                // Log error and use default configuration
                _auditor?.LogSecurityViolation($"Failed to load plugin configuration: {ex.Message}",
                    SecurityEventType.PolicyViolation, ex);
                AnsiConsole.MarkupLine($"[yellow]⚠ Failed to load plugin configuration: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[yellow]  Using default configuration[/]");
                _config = new PluginConfiguration();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                _auditor.LogSecurityViolation($"Failed to save plugin configuration: {ex.Message}", 
                    SecurityEventType.UnauthorizedOperation, ex);
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
        public SecurityConfiguration? SecurityConfig { get; set; }
        public IScriptAPIGateway? APIGateway { get; set; }
        public EnhancedGameAPIFacade? APIFacade { get; set; }
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
    /// Security metrics for a specific plugin
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