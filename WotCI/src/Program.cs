using Spectre.Console;

namespace WotCI
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Show the game header
            var titlePanel = new Panel(new FigletText("WotCI").Centered().Color(Color.Gold1))
                .Border(BoxBorder.Double)
                .BorderColor(Color.Gold1);
            AnsiConsole.Write(titlePanel);

            var subtitlePanel = new Panel(
                new Markup("[bold]Wrath of the CI King - ProgressQuest Style Game[/]").Centered()
            )
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue);
            AnsiConsole.Write(subtitlePanel);
            AnsiConsole.WriteLine();

            // Initialize game components
            var game = new GameSimulator();
            game.Initialize();

            var pluginManager = new PluginManager(game);

            try
            {
                // Start the main game loop with integrated menu system
                await RunGameWithMenus(game, pluginManager);
                return 0;
            }
            finally
            {
                pluginManager.Dispose();
            }
        }

        private static async Task RunGameWithMenus(GameSimulator game, PluginManager pluginManager)
        {
            var gameController = new GameController(game, pluginManager);
            await gameController.Run();
        }
    }
}
