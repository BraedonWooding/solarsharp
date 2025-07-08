using Spectre.Console;
using Spectre.Console.Rendering;

namespace WotCI
{
    /// <summary>
    /// Main game controller that handles the game loop and menu system
    /// </summary>
    public class GameController
    {
        /// <summary>
        /// Represents an instance of the <see cref="GameSimulator"/> used by the
        /// <see cref="GameController"/> to manage the game's state, progress, and simulations.
        /// </summary>
        /// <remarks>
        /// The <c>_game</c> variable is responsible for handling core game mechanics, including:
        /// - Tracking player health, gold, day progression, and defeated enemies.
        /// - Simulating game turns and events.
        /// - Managing game initialization and plugin registration.
        /// This variable is set during the initialization of the <see cref="GameController"/>
        /// and is used throughout its methods to control the flow and logic of the gameplay.
        /// </remarks>
        /// <seealso cref="GameSimulator"/>
        private readonly GameSimulator _game;

        /// <summary>
        /// Manages game plugins, allowing for enabling, disabling, and retrieving available plugins.
        /// Used to control and interact with plugins in the game environment.
        /// </summary>
        private readonly PluginManager _pluginManager;

        /// <summary>
        /// Stores a chronological log of recent activity messages in the game, with a maximum capacity of 10 entries.
        /// </summary>
        /// <remarks>
        /// Each entry in the log includes a timestamp and a user-defined message. The log is maintained by removing
        /// the oldest entries when the maximum capacity is exceeded. It is primarily used to track and display
        /// recent game events within the game controller.
        /// </remarks>
        private readonly List<string> _activityLog = new List<string>();

        /// <summary>
        /// Represents the current state of the game loop.
        /// If true, the game is actively running. If false, the game loop will terminate.
        /// Used to control the main game progression and handle conditions to end the game.
        /// </summary>
        private bool _gameRunning = true;

        /// <summary>
        /// Represents the current day in the game loop.
        /// </summary>
        /// <remarks>
        /// The value of this variable increments daily as the game progresses.
        /// It starts at 1 and is used to track the progression of the game.
        /// </remarks>
        private int _day = 1;

        /// <summary>
        /// Tracks the total number of turns taken in the game.
        /// </summary>
        /// <remarks>
        /// This value is incremented each game turn during the simulation process.
        /// It is used for displaying statistics such as "Total Turns" in the game UI
        /// and in the final game-over summary.
        /// </remarks>
        private int _totalTurns;

        /// <summary>
        /// Represents the main controller for managing the game, including the game loop and the system for interacting with plugins.
        /// </summary>
        public GameController(GameSimulator game, PluginManager pluginManager)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _pluginManager =
                pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        }

        public async Task Run()
        {
            // Load enabled plugins
            var enabledPlugins = _pluginManager
                .GetAvailablePlugins()
                .Where(static p => p.IsEnabled)
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
            if (
                AnsiConsole.Confirm(
                    "[yellow]Would you like to access plugin management before starting the game?[/]"
                )
            )
            {
                await ShowPluginMenu();
            }

            AnsiConsole.WriteLine();

            // Start the main game loop
            while (_gameRunning && _day <= 10) // Run for 10 game days
            {
                await RunGameDay();

                // After each day, ask if user wants to continue or access menus
                if (!_gameRunning || _day > 10 || _game.GetPlayerHealth() <= 0)
                {
                    continue;
                }

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]What would you like to do?[/]")
                        .AddChoices(
                            "[green]Continue to next day[/]",
                            "[blue]Plugin Management[/]",
                            "[red]Quit Game[/]"
                        )
                );

                if (choice.Contains("Continue")) { }
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

            // Game over screen
            ShowGameOverScreen();
        }

        /// <summary>
        /// Executes the events of a single game day, including turn-based simulation, updating the game display, and handling end-of-day logic.
        /// </summary>
        /// <returns>A task representing the asynchronous operation of running a game day.</returns>
        private async Task RunGameDay()
        {
            AnsiConsole.MarkupLine($"[yellow]═══ Day {_day} ═══[/]");

            // Create a live display for the game state
            await AnsiConsole
                .Live(CreateGameDisplay())
                .StartAsync(async ctx =>
                {
                    for (var turn = 1; turn <= 10 && _gameRunning; turn++)
                    {
                        _totalTurns++;

                        // Simulate game events
                        await SimulateGameTurn();

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

        /// <summary>
        /// Displays a plugin management menu, allowing the user to view, enable, disable, or manage plugins.
        /// The menu will repeatedly show until the user chooses to return to the main menu.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of displaying and interacting with the plugin menu.</returns>
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
                var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);

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
                        _ => "grey",
                    };

                    var statusIcon = plugin.IsEnabled
                        ? "[green]✓ Enabled[/]"
                        : "[red]✗ Disabled[/]";
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
                var hostileCount = plugins.Count(static p => p.IsHostile);
                if (hostileCount > 0)
                {
                    var warningPanel = new Panel(
                        $"[red]⚠️  WARNING: {hostileCount} hostile plugins detected! These are for security testing only.[/]"
                    )
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
                                    "[red]This is a HOSTILE plugin that will attempt security violations. Enable for testing?[/]"
                                );
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
                                AnsiConsole.MarkupLine(
                                    $"[red]Failed to enable plugin: {ex.Message}[/]"
                                );
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
            var layout = new Layout("Root").SplitColumns(
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
            var healthColor =
                health > 70 ? "green"
                : health > 30 ? "yellow"
                : "red";

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

        /// <summary>
        /// Simulates a single turn of the game, including random events such as combat, treasure finding,
        /// random scenarios, or resting.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of simulating the game turn.</returns>
        private async Task SimulateGameTurn()
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
                "📚 Studied ancient runes",
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

            AnsiConsole.MarkupLine(
                "[dim]Game session completed. Check plugin logs for security events.[/]"
            );
        }
    }
}
