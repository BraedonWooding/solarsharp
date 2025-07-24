using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using Spectre.Console.Testing;

namespace WotCI.Tests
{
    [TestFixture]
    [Category("WotCI.Integration")]
    public class GameSimulatorTests
    {
        private TestConsole _console;
        private GameSimulator _game;

        [SetUp]
        public void SetUp()
        {
            _console = new TestConsole();
            _game = new GameSimulator(_console);
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void Initialize_SetsUpInitialGameState()
        {
            _game.Initialize();

            var gameState = _game.GetGameState();
            gameState.Should().ContainKey("player_health");
            gameState.Should().ContainKey("player_gold");
            gameState.Should().ContainKey("day");
            gameState.Should().ContainKey("enemies_defeated");

            ((int)gameState["player_health"]).Should().Be(100);
            ((int)gameState["player_gold"]).Should().Be(50);
            ((int)gameState["day"]).Should().Be(1);
            ((int)gameState["enemies_defeated"]).Should().Be(0);

            _console.Output.Should().Contain("Wrath of the Continuous Integration - Game Engine Initialized");
            _console.Output.Should().Contain("Starting with 100 health and 50 gold");
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void CreateApi_ReturnsTableWithAllFunctions()
        {
            _game.Initialize();

            var api = _game.CreateApi();

            api.Should().NotBeNull();
            api.Should().BeOfType<Table>();

            // Check state getters exist
            api.Get("getPlayerHealth").Should().NotBeNull();
            api.Get("getPlayerGold").Should().NotBeNull();
            api.Get("getDay").Should().NotBeNull();
            api.Get("getEnemiesDefeated").Should().NotBeNull();

            // Check actions exist
            api.Get("print").Should().NotBeNull();
            api.Get("giveGold").Should().NotBeNull();
            api.Get("heal").Should().NotBeNull();

            // Check event hooks exist
            api.Get("onCombat").Should().NotBeNull();
            api.Get("onShop").Should().NotBeNull();
            api.Get("onDayEnd").Should().NotBeNull();
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void GameApi_GetterFunctions_ReturnCorrectValues()
        {
            _game.Initialize();
            _game.GetGameState()["player_health"] = 75;
            _game.GetGameState()["player_gold"] = 123;
            _game.GetGameState()["day"] = 5;
            _game.GetGameState()["enemies_defeated"] = 10;

            var api = _game.CreateApi();
            var script = new Script(SolarSharp.Interpreter.Security.Examples.DesktopBasePolicySet)
            {
                Globals = { ["api"] = api },
            };

            var health = script.DoString("return api.getPlayerHealth()").Number;
            health.Should().Be(75);

            var gold = script.DoString("return api.getPlayerGold()").Number;
            gold.Should().Be(123);

            var day = script.DoString("return api.getDay()").Number;
            day.Should().Be(5);

            var enemies = script.DoString("return api.getEnemiesDefeated()").Number;
            enemies.Should().Be(10);
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void GameApi_GiveGold_IncreasesGold()
        {
            _game.Initialize();
            var api = _game.CreateApi();
            var script = new Script(SolarSharp.Interpreter.Security.Examples.DesktopBasePolicySet)
            {
                Globals = { ["api"] = api },
            };

            script.DoString("api.giveGold(25)");

            _game.GetGameState()["player_gold"].Should().Be(75); // 50 + 25
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void GameApi_Heal_IncreasesHealthWithCap()
        {
            _game.Initialize();
            _game.GetGameState()["player_health"] = 80;
            var api = _game.CreateApi();
            var script = new Script(SolarSharp.Interpreter.Security.Examples.DesktopBasePolicySet)
            {
                Globals = { ["api"] = api },
            };

            // Small heal
            script.DoString("api.heal(15)");

            _game.GetGameState()["player_health"].Should().Be(95);

            // Large heal (should cap at 100)
            script.DoString("api.heal(20)");

            _game.GetGameState()["player_health"].Should().Be(100);
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void GameApi_Print_OutputsWithPluginPrefix()
        {
            _game.Initialize();
            var api = _game.CreateApi();
            var script = new Script(SolarSharp.Interpreter.Security.Examples.DesktopBasePolicySet)
            {
                Globals = { ["api"] = api },
            };

            script.DoString("api.print('Test message from plugin')");

            _console.Output.Should().Contain("[Plugin] Test message from plugin");
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void Run_SimulatesGameDays()
        {
            _game.Initialize();

            _game.Run();

            var output = _console.Output;
            output.Should().Contain("Day 1 begins");
            output.Should().Contain("Day 2 begins");
            output.Should().Contain("Day 3 begins");
            output.Should().Contain("Game Over!");
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void Run_SimulatesCombatEvents()
        {
            _game.Initialize();

            _game.Run();

            var output = _console.Output;
            output.Should().Contain("Combat encounter!");
            output.Should().Contain("Enemy attacks!");
            output.Should().Contain("Enemy attacks!");
            output.Should().Contain("damage");
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void Run_SimulatesRandomEvents()
        {
            _game.Initialize();

            _game.Run();

            var output = _console.Output;
            // Should contain at least one of these events
            (
                output.Contains("Shop event!")
                || output.Contains("Rendering frame")
                || output.Contains("Playing sound")
            )
                .Should()
                .BeTrue();
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void Run_EndsWhenHealthReachesZero()
        {
            _game.Initialize();
            _game.GetGameState()["player_health"] = 1; // Minimum health to ensure death on first hit

            _game.Run();

            var output = _console.Output;
            output.Should().Contain("You died!");
            output.Should().Contain("Game Over!");

            var finalHealth = (int)_game.GetGameState()["player_health"];
            finalHealth.Should().BeLessOrEqualTo(0);
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void Run_TracksEnemiesDefeated()
        {
            _game.Initialize();

            _game.Run();

            var enemiesDefeated = (int)_game.GetGameState()["enemies_defeated"];
            enemiesDefeated.Should().BeGreaterThan(0);
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void EventCallbacks_CanBeRegisteredThroughApi()
        {
            _game.Initialize();
            var api = _game.CreateApi();

            // Create a simple Lua script that registers callbacks
            var script = new Script(SolarSharp.Interpreter.Security.Examples.DesktopBasePolicySet)
            {
                Globals = { ["game"] = api },
            };

            script.DoString(
                """

                                game.onCombat = function(health, damage)
                                    print('Combat callback: health=' .. health .. ', damage=' .. damage)
                                    return damage * 2  -- Double damage
                                end
                            
                """
            );

            // Note: In the actual implementation, the game would need to check
            // and call these callbacks. This test verifies the API structure.

            api.Get("onCombat").Type.Should().Be(DataType.Function);
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void GetGameState_ReturnsCurrentState()
        {
            _game.Initialize();
            _game.GetGameState()["custom_value"] = "test";

            var state = _game.GetGameState();

            state.Should().ContainKey("player_health");
            state.Should().ContainKey("player_gold");
            state.Should().ContainKey("day");
            state.Should().ContainKey("enemies_defeated");
            state.Should().ContainKey("custom_value");
            state["custom_value"].Should().Be("test");
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void GameSimulation_ProducesRealisticOutput()
        {
            _game.Initialize();

            _game.Run();

            var output = _console.Output;

            // Check for variety of events
            var combatCount = output.Split("Combat encounter!").Length - 1;
            var shopCount = output.Split("Shop event!").Length - 1;
            var renderCount = output.Split("Rendering frame").Length - 1;
            var soundCount = output.Split("Playing sound").Length - 1;

            // Should have multiple events of different types
            combatCount.Should().BeGreaterThan(0);
            (shopCount + renderCount + soundCount).Should().BeGreaterThan(0);

            // Should progress through multiple days
            output.Should().Contain("Day 1");
            output.Should().Contain("Day 2");
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void PluginEnumeration_ShouldDiscoverExistingPlugins()
        {
            _game.Initialize();

            // Get the WotCI project directory
            var currentDir = Directory.GetCurrentDirectory();
            // Navigate from WotCI.Tests/bin/Debug/net6.0 to WotCI/plugins
            var wotciDir = Path.GetFullPath(
                Path.Combine(currentDir, "..", "..", "..", "..", "WotCI")
            );
            var pluginsDir = Path.Combine(wotciDir, "plugins");

            // Verify plugin directories exist
            var userPluginsDir = Path.Combine(pluginsDir, "user");
            var partnerPluginsDirs = new[]
            {
                Path.Combine(pluginsDir, "deadlock-digital"),
                Path.Combine(pluginsDir, "segfault-studios"),
            };

            // Try to enumerate plugins
            var discoveredPlugins = new List<string>();

            // Check user plugins
            if (Directory.Exists(userPluginsDir))
            {
                var userPlugins = Directory.GetFiles(userPluginsDir, "*.lua");
                discoveredPlugins.AddRange(userPlugins);
            }

            // Check partner plugins
            foreach (var partnerDir in partnerPluginsDirs)
            {
                if (Directory.Exists(partnerDir))
                {
                    var partnerPlugins = Directory.GetFiles(partnerDir, "*.lua");
                    discoveredPlugins.AddRange(partnerPlugins);
                }
            }

            // We should discover the sample plugins
            discoveredPlugins
                .Should()
                .NotBeEmpty("Expected to find at least some plugins in the WotCI demo");

            // Verify specific expected plugins exist
            var pluginNames = discoveredPlugins.Select(Path.GetFileName).ToList();

            // These are the sample plugins that should exist in the WotCI demo
            var expectedPlugins = new[] { "my-tweaks.lua", "config.lua" }; // User plugins

            foreach (var expectedPlugin in expectedPlugins)
            {
                pluginNames
                    .Should()
                    .Contain(
                        expectedPlugin,
                        $"Expected to find {expectedPlugin} in user plugins directory"
                    );
            }
        }

        [Category("Game.Unit")]
        [Category("Game.Unit")]
        [Test]
        public void PluginManager_ShouldDetectWorkingDirectoryIssue()
        {
            _game.Initialize();

            // Try to create a PluginManager with the current working directory
            // This should reveal the working directory issue
            try
            {
                var pluginManager = new PluginManager(_game);
                var availablePlugins = pluginManager.GetAvailablePlugins();

                // This should detect the issue where plugins aren't found
                // because PluginManager uses relative paths from the wrong working directory
                availablePlugins
                    .Should()
                    .NotBeEmpty(
                        "PluginManager should find plugins, but it's likely using the wrong working directory. "
                            + $"Current directory: {Directory.GetCurrentDirectory()}. "
                            + "Expected plugins directory: WotCI/plugins/ relative to the WotCI project root."
                    );

                // If we get here and plugins are found, verify some expected plugins exist
                var pluginNames = availablePlugins.Select(p => p.Name).ToList();
                pluginNames.Should().Contain("my-tweaks", "Expected to find my-tweaks plugin");
                pluginNames.Should().Contain("config", "Expected to find config plugin");
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"PluginManager failed to enumerate plugins due to working directory issue: {ex.Message}"
                );
            }
        }
    }
}
