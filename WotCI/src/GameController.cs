using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace WotCI
{
    /// <summary>
    /// Main game controller that handles the game loop and menu system
    /// </summary>
    public class GameController
    {
        private readonly GameSimulator _game;
        private readonly PluginManager _pluginManager;
        private readonly List<string> _activityLog = new();
        private bool _gameRunning = true;
        private int _day = 1;
        private int _totalTurns = 0;

        public GameController(GameSimulator game, PluginManager pluginManager)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        }

        public async Task Run()
        {
            // Load enabled plugins
            var enabledPlugins = _pluginManager.GetAvailablePlugins()
                .Where(p => p.IsEnabled)
                .ToList();
                
            if (enabledPlugins.Any())
            {
                AnsiConsole.MarkupLine("[green]Loading enabled plugins...[/]");
                foreach (var plugin in enabledPlugins)
                {
                    var hostileWarning = plugin.IsHostile ? " [red](HOSTILE)[/]" : "";
                    AnsiConsole.MarkupLine($"  [blue]▶[/] {plugin.Name}{hostileWarning}");
                }
                AnsiConsole.WriteLine();
            }

            // Ask if user wants to access plugin management before starting
            if (AnsiConsole.Confirm("[yellow]Would you like to access plugin management before starting the game?[/]"))
            {
                await ShowPluginMenu();
            }

            AnsiConsole.WriteLine();

            // Start the main game loop
            while (_gameRunning && _day <= 10) // Run for 10 game days
            {
                await RunGameDay();
                
                // After each day, ask if user wants to continue or access menus
                if (_gameRunning && _day <= 10 && _game.GetPlayerHealth() > 0)
                {
                    var choice = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[yellow]What would you like to do?[/]")
                            .AddChoices(new[] {
                                "[green]Continue to next day[/]",
                                "[blue]Plugin Management[/]",
                                "[red]Quit Game[/]"
                            }));
                    
                    if (choice.Contains("Continue"))
                    {
                        continue;
                    }
                    else if (choice.Contains("Plugin"))
                    {
                        await ShowPluginMenu();
                    }
                    else if (choice.Contains("Quit"))
                    {
                        if (ConfirmQuit())
                        {
                            _gameRunning = false;
                        }
                    }
                }
            }

            // Game over screen
            ShowGameOverScreen();
        }

        private async Task RunGameDay()
        {
            AnsiConsole.MarkupLine($"[yellow]═══ Day {_day} ═══[/]");
            
            // Create a live display for the game state
            await AnsiConsole.Live(CreateGameDisplay())
                .StartAsync(async ctx =>
                {
                    for (int turn = 1; turn <= 10 && _gameRunning; turn++)
                    {
                        _totalTurns++;
                        
                        // Simulate game events
                        await SimulateGameTurn(turn);
                        
                        // Update display
                        ctx.UpdateTarget(CreateGameDisplay());
                        
                        // Wait between turns
                        await Task.Delay(800);
                        
                        // Check for exit condition
                        if (_game.GetPlayerHealth() <= 0)
                        {
                            _gameRunning = false;
                            break;
                        }
                    }
                });
            
            if (_gameRunning)
            {
                _day++;
                AnsiConsole.WriteLine();
            }
        }

        private async Task ShowPluginMenu()
        {
            while (_gameRunning)
            {
                AnsiConsole.Clear();
                
                var headerPanel = new Panel(new Markup("[bold yellow]🎮 Plugin Management 🎮[/]"))
                    .Border(BoxBorder.Double)
                    .BorderColor(Color.Yellow);
                AnsiConsole.Write(headerPanel);
                AnsiConsole.WriteLine();
                
                // Get available plugins
                var plugins = _pluginManager.GetAvailablePlugins().ToList();
                
                // Create status table
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Grey);
                
                table.AddColumn("[bold]Plugin Name[/]");
                table.AddColumn("[bold]Trust Level[/]");
                table.AddColumn("[bold]Status[/]");
                table.AddColumn("[bold]Type[/]");
                table.AddColumn("[bold]Description[/]");
                
                foreach (var plugin in plugins)
                {
                    var trustColor = plugin.TrustLevel switch
                    {
                        PluginTrustLevel.System => "green",
                        PluginTrustLevel.Partner => "yellow", 
                        PluginTrustLevel.User => "blue",
                        _ => "grey"
                    };
                    
                    var statusIcon = plugin.IsEnabled ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]";
                    var typeIcon = plugin.IsHostile ? "[red]⚠️ HOSTILE[/]" : "[green]✅ Safe[/]";
                    
                    table.AddRow(
                        $"[{trustColor}]{plugin.Name}[/]",
                        $"[{trustColor}]{plugin.TrustLevel}[/]",
                        statusIcon,
                        typeIcon,
                        plugin.Description ?? "No description"
                    );
                }
                
                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                
                // Warning about hostile plugins
                var hostileCount = plugins.Count(p => p.IsHostile);
                if (hostileCount > 0)
                {
                    var warningPanel = new Panel($"[red]⚠️  WARNING: {hostileCount} hostile plugins detected! These are for security testing only.[/]")
                        .Border(BoxBorder.Heavy)
                        .BorderColor(Color.Red);
                    AnsiConsole.Write(warningPanel);
                    AnsiConsole.WriteLine();
                }
                
                // Menu options
                var choices = new List<string>();
                
                foreach (var plugin in plugins)
                {
                    if (plugin.IsEnabled)
                    {
                        choices.Add($"[red]Disable[/] {plugin.Name}");
                    }
                    else
                    {
                        var hostileWarning = plugin.IsHostile ? " [red](HOSTILE!)[/]" : "";
                        choices.Add($"[green]Enable[/] {plugin.Name}{hostileWarning}");
                    }
                }
                
                choices.Add("[blue]Back to Main Menu[/]");
                
                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold]Select an action:[/]")
                        .PageSize(20)
                        .AddChoices(choices)
                );
                
                if (selection.Contains("Back"))
                {
                    break;
                }
                else
                {
                    // Handle enable/disable
                    var pluginName = ExtractPluginName(selection);
                    var plugin = plugins.FirstOrDefault(p => p.Name == pluginName);
                    
                    if (plugin != null)
                    {
                        if (selection.Contains("Enable"))
                        {
                            if (plugin.IsHostile)
                            {
                                var confirm = AnsiConsole.Confirm(
                                    $"[red]This is a HOSTILE plugin that will attempt security violations. Enable for testing?[/]");
                                if (!confirm)
                                {
                                    continue;
                                }
                            }
                            
                            try
                            {
                                await _pluginManager.EnablePluginAsync(pluginName);
                                AnsiConsole.MarkupLine($"[green]✓ Plugin enabled: {pluginName}[/]");
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]Failed to enable plugin: {ex.Message}[/]");
                            }
                        }
                        else if (selection.Contains("Disable"))
                        {
                            _pluginManager.DisablePlugin(pluginName);
                            AnsiConsole.MarkupLine($"[yellow]✓ Plugin disabled: {pluginName}[/]");
                        }
                        
                        AnsiConsole.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                    }
                }
            }
        }

        private string ExtractPluginName(string selection)
        {
            // Extract plugin name from selection like "[red]Disable[/] plugin-name" 
            var parts = selection.Split("] ");
            if (parts.Length >= 2)
            {
                return parts[1].Replace("[red](HOSTILE!)[/]", "").Trim();
            }
            return "";
        }

        private bool ConfirmQuit()
        {
            return AnsiConsole.Confirm("[red]Are you sure you want to quit the game?[/]");
        }

        private IRenderable CreateGameDisplay()
        {
            var layout = new Layout("Root")
                .SplitColumns(
                    new Layout("Left").Size(40),
                    new Layout("Right")
                );
            
            // Character stats panel
            var statsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Green)
                .Title("[bold green]Character Stats[/]");
            
            statsTable.AddColumn("Stat");
            statsTable.AddColumn("Value");
            
            var health = _game.GetPlayerHealth();
            var healthColor = health > 70 ? "green" : health > 30 ? "yellow" : "red";
            
            statsTable.AddRow("Health", $"[{healthColor}]{health}/100[/]");
            statsTable.AddRow("Gold", $"[yellow]{_game.GetPlayerGold()}[/]");
            statsTable.AddRow("Day", $"[blue]{_day}[/]");
            statsTable.AddRow("Enemies", $"[red]{_game.GetEnemiesDefeated()}[/]");
            statsTable.AddRow("Total Turns", $"[grey]{_totalTurns}[/]");
            
            layout["Left"].Update(statsTable);
            
            // Activity log panel
            var logPanel = new Panel(GetRecentActivity())
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue)
                .Header("[bold blue]Activity Log[/]");
            
            layout["Right"].Update(logPanel);
            
            return layout;
        }

        private async Task SimulateGameTurn(int turn)
        {
            var random = new Random();
            
            // Random events each turn
            var eventType = random.Next(1, 5);
            
            switch (eventType)
            {
                case 1: // Combat
                    await SimulateCombat();
                    break;
                case 2: // Find gold
                    await SimulateTreasure();
                    break;
                case 3: // Random event
                    await SimulateRandomEvent();
                    break;
                case 4: // Rest/heal
                    await SimulateRest();
                    break;
            }
        }

        private async Task SimulateCombat()
        {
            var random = new Random();
            var enemies = new[] { "Goblin", "Orc", "Skeleton", "Wolf", "Bandit" };
            var enemy = enemies[random.Next(enemies.Length)];
            
            AddToActivityLog($"⚔️ Encountered {enemy}!");
            
            var damage = random.Next(5, 20);
            var currentHealth = _game.GetPlayerHealth();
            _game.SetPlayerHealth(currentHealth - damage);
            
            AddToActivityLog($"   Took {damage} damage");
            
            // Small chance to find gold after combat
            if (random.NextDouble() < 0.3)
            {
                var gold = random.Next(5, 15);
                _game.GiveGold(gold);
                AddToActivityLog($"   Found {gold} gold!");
            }
            
            await Task.Delay(100);
        }

        private async Task SimulateTreasure()
        {
            var random = new Random();
            var treasures = new[] { "chest", "pouch", "hidden stash", "fallen adventurer" };
            var treasure = treasures[random.Next(treasures.Length)];
            
            var gold = random.Next(10, 30);
            _game.GiveGold(gold);
            
            AddToActivityLog($"💰 Found {treasure} with {gold} gold!");
            await Task.Delay(100);
        }

        private async Task SimulateRandomEvent()
        {
            var random = new Random();
            var events = new[]
            {
                "🌟 Found a magic spring (+10 health)",
                "🍄 Ate strange mushroom (-5 health)", 
                "🦋 Peaceful moment in nature (+5 health)",
                "🕳️ Fell into a pit (-10 health)",
                "🎯 Practiced combat skills",
                "📚 Studied ancient runes"
            };
            
            var selectedEvent = events[random.Next(events.Length)];
            AddToActivityLog(selectedEvent);
            
            // Apply event effects
            if (selectedEvent.Contains("+10 health"))
            {
                _game.SetPlayerHealth(_game.GetPlayerHealth() + 10);
            }
            else if (selectedEvent.Contains("-5 health"))
            {
                _game.SetPlayerHealth(_game.GetPlayerHealth() - 5);
            }
            else if (selectedEvent.Contains("+5 health"))
            {
                _game.SetPlayerHealth(_game.GetPlayerHealth() + 5);
            }
            else if (selectedEvent.Contains("-10 health"))
            {
                _game.SetPlayerHealth(_game.GetPlayerHealth() - 10);
            }
            
            await Task.Delay(100);
        }

        private async Task SimulateRest()
        {
            AddToActivityLog("😴 Resting...");
            
            var healing = new Random().Next(3, 8);
            var currentHealth = _game.GetPlayerHealth();
            _game.SetPlayerHealth(currentHealth + healing);
            
            AddToActivityLog($"   Recovered {healing} health");
            await Task.Delay(100);
        }

        private void AddToActivityLog(string message)
        {
            _activityLog.Add($"[dim]{DateTime.Now:HH:mm:ss}[/] {message}");
            
            // Keep only last 10 entries
            if (_activityLog.Count > 10)
            {
                _activityLog.RemoveAt(0);
            }
        }

        private string GetRecentActivity()
        {
            if (!_activityLog.Any())
            {
                return "[dim]No recent activity[/]";
            }
            
            return string.Join("\n", _activityLog.TakeLast(8));
        }

        private void ShowGameOverScreen()
        {
            AnsiConsole.Clear();
            
            var gameOverPanel = new Panel(new FigletText("GAME OVER").Centered().Color(Color.Red))
                .Border(BoxBorder.Double)
                .BorderColor(Color.Red);
            AnsiConsole.Write(gameOverPanel);
            AnsiConsole.WriteLine();
            
            // Final stats
            var finalStats = new Table()
                .Border(TableBorder.Heavy)
                .BorderColor(Color.Yellow)
                .Title("[bold yellow]Final Statistics[/]");
            
            finalStats.AddColumn("Metric");
            finalStats.AddColumn("Value");
            
            finalStats.AddRow("Final Health", $"{_game.GetPlayerHealth()}/100");
            finalStats.AddRow("Gold Collected", $"{_game.GetPlayerGold()}");
            finalStats.AddRow("Enemies Defeated", $"{_game.GetEnemiesDefeated()}");
            finalStats.AddRow("Total Turns", $"{_totalTurns}");
            
            var outcome = _game.GetPlayerHealth() > 0 ? "Victory!" : "Defeat";
            var outcomeColor = _game.GetPlayerHealth() > 0 ? "green" : "red";
            finalStats.AddRow("Outcome", $"[{outcomeColor}]{outcome}[/]");
            
            AnsiConsole.Write(finalStats);
            AnsiConsole.WriteLine();
            
            AnsiConsole.MarkupLine("[dim]Game session completed. Check plugin logs for security events.[/]");
        }
    }
}