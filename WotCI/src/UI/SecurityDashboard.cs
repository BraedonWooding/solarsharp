using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Rendering;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Communication;

namespace WotCI.UI
{
    /// <summary>
    /// Real-time security dashboard for monitoring plugin security events
    /// </summary>
    public class SecurityDashboard
    {
        private readonly ISecurityAuditor _auditor;
        private readonly IScriptMessageBus? _messageBus;
        private readonly PluginManager? _pluginManager;
        private readonly List<SecurityDisplayEvent> _recentEvents = new();
        private readonly object _eventsLock = new object();
        private DateTime _lastUpdate = DateTime.UtcNow;

        public SecurityDashboard(ISecurityAuditor auditor, IScriptMessageBus? messageBus = null, PluginManager? pluginManager = null)
        {
            _auditor = auditor ?? throw new ArgumentNullException(nameof(auditor));
            _messageBus = messageBus;
            _pluginManager = pluginManager;
        }

        /// <summary>
        /// Creates the main dashboard layout
        /// </summary>
        public Layout CreateDashboard()
        {
            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Size(3),
                    new Layout("Main").SplitColumns(
                        new Layout("Left").Ratio(2),
                        new Layout("Right").Ratio(1)
                    ),
                    new Layout("Footer").Size(2)
                );

            // Header
            layout["Header"].Update(CreateHeader());

            // Main content
            layout["Left"].SplitRows(
                new Layout("Metrics").Size(8),
                new Layout("Events")
            );

            layout["Left"]["Metrics"].Update(CreateMetricsPanel());
            layout["Left"]["Events"].Update(CreateEventsPanel());
            layout["Right"].Update(CreatePluginsPanel());

            // Footer
            layout["Footer"].Update(CreateFooter());

            return layout;
        }

        /// <summary>
        /// Updates the dashboard with current data
        /// </summary>
        public void UpdateDashboard(Layout layout)
        {
            // Only update if enough time has passed to avoid flickering
            if (DateTime.UtcNow - _lastUpdate < TimeSpan.FromMilliseconds(500))
                return;

            RefreshEvents();

            layout["Header"].Update(CreateHeader());
            layout["Left"]["Metrics"].Update(CreateMetricsPanel());
            layout["Left"]["Events"].Update(CreateEventsPanel());
            layout["Right"].Update(CreatePluginsPanel());
            layout["Footer"].Update(CreateFooter());

            _lastUpdate = DateTime.UtcNow;
        }

        private IRenderable CreateHeader()
        {
            var headerPanel = new Panel(
                new Markup("[bold blue]🛡️  SolarSharp Security Dashboard[/]")
                    .Centered())
                .Border(BoxBorder.Double)
                .BorderColor(Color.Blue);

            return headerPanel;
        }

        private IRenderable CreateMetricsPanel()
        {
            var metrics = _auditor.GetSecurityMetrics();
            var messageBusStats = _messageBus?.GetStats();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Green)
                .Title("[bold green]Security Metrics[/]");

            table.AddColumn("[bold]Metric[/]");
            table.AddColumn("[bold]Value[/]");
            table.AddColumn("[bold]Status[/]");

            // Security metrics
            var violationColor = metrics.ViolationCount == 0 ? "green" : metrics.ViolationCount < 5 ? "yellow" : "red";
            table.AddRow("Security Violations", 
                $"[{violationColor}]{metrics.ViolationCount}[/]", 
                GetStatusIcon(metrics.ViolationCount, 0, 3));

            table.AddRow("Total Events", 
                $"[blue]{metrics.TotalEvents}[/]", 
                GetStatusIcon(metrics.TotalEvents, 0, 100, true));

            table.AddRow("Capability Usage", 
                $"[cyan]{metrics.CapabilityUsageCount}[/]", 
                GetStatusIcon(metrics.CapabilityUsageCount, 0, 50, true));

            table.AddRow("Rate Limit Violations", 
                $"[{(metrics.RateLimitViolations == 0 ? "green" : "red")}]{metrics.RateLimitViolations}[/]", 
                GetStatusIcon(metrics.RateLimitViolations, 0, 2));

            // Message bus metrics
            if (messageBusStats != null)
            {
                table.AddRow("Messages Processed", 
                    $"[magenta]{messageBusStats.TotalMessagesProcessed}[/]", 
                    GetStatusIcon(messageBusStats.TotalMessagesProcessed, 0, 100, true));

                table.AddRow("Active Scripts", 
                    $"[yellow]{messageBusStats.ActiveScripts}[/]", 
                    GetStatusIcon(messageBusStats.ActiveScripts, 0, 10, true));

                table.AddRow("Dropped Messages", 
                    $"[{(messageBusStats.DroppedMessages == 0 ? "green" : "red")}]{messageBusStats.DroppedMessages}[/]", 
                    GetStatusIcon(messageBusStats.DroppedMessages, 0, 5));
            }

            return table;
        }

        private IRenderable CreateEventsPanel()
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .Title("[bold yellow]Recent Security Events[/]");

            table.AddColumn("[bold]Time[/]");
            table.AddColumn("[bold]Type[/]");
            table.AddColumn("[bold]Description[/]");
            table.AddColumn("[bold]Severity[/]");

            lock (_eventsLock)
            {
                foreach (var evt in _recentEvents.TakeLast(SolarSharp.Interpreter.Security.SecurityConstants.Dashboard.MaxRecentEvents).Reverse())
                {
                    var severityColor = evt.Severity switch
                    {
                        SecuritySeverity.Critical => "red",
                        SecuritySeverity.High => "orange1",
                        SecuritySeverity.Medium => "yellow",
                        SecuritySeverity.Low => "green",
                        SecuritySeverity.Info => "blue",
                        _ => "white"
                    };

                    var severityIcon = evt.Severity switch
                    {
                        SecuritySeverity.Critical => "🚨",
                        SecuritySeverity.High => "⚠️",
                        SecuritySeverity.Medium => "⚡",
                        SecuritySeverity.Low => "ℹ️",
                        SecuritySeverity.Info => "📝",
                        _ => "❓"
                    };

                    table.AddRow(
                        $"[dim]{evt.Timestamp:HH:mm:ss}[/]",
                        $"[{severityColor}]{evt.Type}[/]",
                        TruncateText(evt.Description, 40),
                        $"[{severityColor}]{severityIcon} {evt.Severity}[/]"
                    );
                }

                if (!_recentEvents.Any())
                {
                    table.AddRow("[dim]--:--:--[/]", "[dim]No events[/]", "[dim]System running normally[/]", "[green]✅ OK[/]");
                }
            }

            return table;
        }

        private IRenderable CreatePluginsPanel()
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Purple)
                .Title("[bold magenta]Plugin Status[/]");

            table.AddColumn("[bold]Plugin[/]");
            table.AddColumn("[bold]Trust[/]");
            table.AddColumn("[bold]Status[/]");

            if (_pluginManager != null)
            {
                var plugins = _pluginManager.GetAvailablePlugins();
                
                foreach (var plugin in plugins.OrderBy(p => p.TrustLevel).ThenBy(p => p.Name))
                {
                    var trustColor = plugin.TrustLevel switch
                    {
                        PluginTrustLevel.System => "green",
                        PluginTrustLevel.Partner => "yellow",
                        PluginTrustLevel.User => "blue",
                        _ => "grey"
                    };

                    var statusIcon = plugin.IsEnabled ? 
                        (plugin.IsHostile ? "[red]🔥 HOSTILE[/]" : "[green]✅ Active[/]") : 
                        "[grey]⏸️ Disabled[/]";

                    var pluginName = (plugin.Name?.Length ?? 0) > 15 ? 
                        plugin.Name?.Substring(0, 12) + "..." : 
                        plugin.Name ?? "Unknown";

                    table.AddRow(
                        pluginName,
                        $"[{trustColor}]{plugin.TrustLevel}[/]",
                        statusIcon
                    );
                }
            }
            else
            {
                table.AddRow("[dim]No plugin[/]", "[dim]manager[/]", "[dim]available[/]");
            }

            return table;
        }

        private IRenderable CreateFooter()
        {
            var uptimeText = $"Uptime: {DateTime.UtcNow - _lastUpdate:hh\\:mm\\:ss}";
            var timestampText = $"Last Update: {_lastUpdate:HH:mm:ss}";
            var helpText = "[dim]Press 'q' to quit dashboard, 'r' to refresh, 'p' for plugin management[/]";

            var grid = new Grid()
                .AddColumn()
                .AddColumn()
                .AddColumn();

            grid.AddRow(uptimeText, timestampText.PadLeft(20), helpText.PadLeft(40));

            return new Panel(grid)
                .Border(BoxBorder.None)
                .Padding(0, 0);
        }

        private void RefreshEvents()
        {
            lock (_eventsLock)
            {
                _recentEvents.Clear();
                
                var auditEvents = _auditor.GetRecentEvents(SolarSharp.Interpreter.Security.SecurityConstants.Dashboard.MaxEventsFetch);
                foreach (var evt in auditEvents)
                {
                    _recentEvents.Add(new SecurityDisplayEvent
                    {
                        Timestamp = evt.Timestamp,
                        Type = evt.EventType.ToString(),
                        Description = evt.Description,
                        Severity = DetermineSeverity(evt.EventType, evt.Success)
                    });
                }

                // Sort by timestamp descending
                _recentEvents.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                
                // Ensure we don't keep more than the maximum allowed events
                if (_recentEvents.Count > SolarSharp.Interpreter.Security.SecurityConstants.Dashboard.MaxEventsFetch)
                {
                    _recentEvents.RemoveRange(SolarSharp.Interpreter.Security.SecurityConstants.Dashboard.MaxEventsFetch, 
                        _recentEvents.Count - SolarSharp.Interpreter.Security.SecurityConstants.Dashboard.MaxEventsFetch);
                }
            }
        }

        private SecuritySeverity DetermineSeverity(SecurityEventType eventType, bool success)
        {
            if (!success)
            {
                return eventType switch
                {
                    SecurityEventType.FileAccessViolation => SecuritySeverity.High,
                    SecurityEventType.UnauthorizedOperation => SecuritySeverity.Critical,
                    SecurityEventType.PolicyViolation => SecuritySeverity.High,
                    SecurityEventType.ResourceLimitExceeded => SecuritySeverity.Medium,
                    SecurityEventType.ExecutionTimeout => SecuritySeverity.Medium,
                    SecurityEventType.MemoryExhaustion => SecuritySeverity.High,
                    SecurityEventType.RateLimitExceeded => SecuritySeverity.Medium,
                    _ => SecuritySeverity.Low
                };
            }

            return eventType switch
            {
                SecurityEventType.CapabilityUsage => SecuritySeverity.Info,
                SecurityEventType.CapabilityGranted => SecuritySeverity.Info,
                SecurityEventType.CapabilityRevoked => SecuritySeverity.Low,
                SecurityEventType.SecurityConfigurationChanged => SecuritySeverity.Medium,
                _ => SecuritySeverity.Info
            };
        }

        private string GetStatusIcon(int value, int goodThreshold, int badThreshold, bool higherIsBetter = false)
        {
            if (higherIsBetter)
            {
                return value >= badThreshold ? "[green]✅[/]" : 
                       value >= goodThreshold ? "[yellow]⚠️[/]" : "[red]❌[/]";
            }
            else
            {
                return value <= goodThreshold ? "[green]✅[/]" : 
                       value <= badThreshold ? "[yellow]⚠️[/]" : "[red]❌[/]";
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? "";

            return text.Substring(0, maxLength - 3) + "...";
        }
    }

    /// <summary>
    /// Security event for display purposes
    /// </summary>
    public class SecurityDisplayEvent
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SecuritySeverity Severity { get; set; }
    }

    /// <summary>
    /// Security severity levels for display
    /// </summary>
    public enum SecuritySeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Interactive security dashboard controller
    /// </summary>
    public class InteractiveSecurityDashboard
    {
        private readonly SecurityDashboard _dashboard;
        private readonly GameController? _gameController;
        private bool _isRunning = false;

        public InteractiveSecurityDashboard(SecurityDashboard dashboard, GameController? gameController = null)
        {
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _gameController = gameController;
        }

        /// <summary>
        /// Runs the interactive dashboard
        /// </summary>
        public async System.Threading.Tasks.Task RunAsync()
        {
            _isRunning = true;
            var layout = _dashboard.CreateDashboard();

            await AnsiConsole.Live(layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .StartAsync(async ctx =>
                {
                    while (_isRunning)
                    {
                        _dashboard.UpdateDashboard(layout);
                        ctx.UpdateTarget(layout);

                        // Check for user input (non-blocking)
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            await HandleKeyInput(key);
                        }

                        await System.Threading.Tasks.Task.Delay(1000); // Update every second
                    }
                });
        }

        /// <summary>
        /// Stops the dashboard
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
        }

        private System.Threading.Tasks.Task HandleKeyInput(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.Q:
                    _isRunning = false;
                    break;
                    
                case ConsoleKey.R:
                    // Force refresh - already happens automatically
                    break;
                    
                case ConsoleKey.P:
                    if (_gameController != null)
                    {
                        Stop();
                        // This would return to plugin management
                        // Implementation depends on GameController structure
                    }
                    break;
                    
                case ConsoleKey.H:
                    ShowHelp();
                    break;
            }
            
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void ShowHelp()
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold]Security Dashboard Help[/]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[yellow]Keyboard Shortcuts:[/]");
            AnsiConsole.MarkupLine("  [green]Q[/] - Quit dashboard");
            AnsiConsole.MarkupLine("  [green]R[/] - Refresh display");
            AnsiConsole.MarkupLine("  [green]P[/] - Plugin management");
            AnsiConsole.MarkupLine("  [green]H[/] - Show this help");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[yellow]Security Severity Levels:[/]");
            AnsiConsole.MarkupLine("  🚨 [red]Critical[/] - Immediate security threat");
            AnsiConsole.MarkupLine("  ⚠️ [orange1]High[/] - Significant security concern");
            AnsiConsole.MarkupLine("  ⚡ [yellow]Medium[/] - Moderate security issue");
            AnsiConsole.MarkupLine("  ℹ️ [blue]Low[/] - Minor security note");
            AnsiConsole.MarkupLine("  📝 [green]Info[/] - Informational event");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Press any key to return to dashboard...");
            Console.ReadKey(true);
        }
    }
}