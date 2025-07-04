using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using Spectre.Console.Testing;
using Xunit;
using WotCI;

namespace WotCI.Tests
{
    public class GameSimulatorTests
    {
        private readonly TestConsole _console;
        private readonly GameSimulator _game;

        public GameSimulatorTests()
        {
            _console = new TestConsole();
            _game = new GameSimulator(_console);
        }

        [Fact]
        public void Initialize_SetsUpInitialGameState()
        {
            // Act
            _game.Initialize();

            // Assert
            var gameState = _game.GetGameState();
            gameState.Should().ContainKey("player_health");
            gameState.Should().ContainKey("player_gold");
            gameState.Should().ContainKey("day");
            gameState.Should().ContainKey("enemies_defeated");
            
            ((int)gameState["player_health"]).Should().Be(100);
            ((int)gameState["player_gold"]).Should().Be(50);
            ((int)gameState["day"]).Should().Be(1);
            ((int)gameState["enemies_defeated"]).Should().Be(0);
            
            _console.Output.Should().Contain("🎮 Wrath of the CI King - Game Engine Initialized");
            _console.Output.Should().Contain("Starting with 100 health and 50 gold");
        }

        [Fact]
        public void CreateApi_ReturnsTableWithAllFunctions()
        {
            // Arrange
            _game.Initialize();

            // Act
            var api = _game.CreateApi();

            // Assert
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

        [Fact]
        public void GameApi_GetterFunctions_ReturnCorrectValues()
        {
            // Arrange
            _game.Initialize();
            _game.GetGameState()["player_health"] = 75;
            _game.GetGameState()["player_gold"] = 123;
            _game.GetGameState()["day"] = 5;
            _game.GetGameState()["enemies_defeated"] = 10;
            
            var api = _game.CreateApi();
            var script = new Script();
            script.Globals["api"] = api;

            // Act & Assert
            var health = script.DoString("return api.getPlayerHealth()").Number;
            health.Should().Be(75);

            var gold = script.DoString("return api.getPlayerGold()").Number;
            gold.Should().Be(123);

            var day = script.DoString("return api.getDay()").Number;
            day.Should().Be(5);

            var enemies = script.DoString("return api.getEnemiesDefeated()").Number;
            enemies.Should().Be(10);
        }

        [Fact]
        public void GameApi_GiveGold_IncreasesGold()
        {
            // Arrange
            _game.Initialize();
            var api = _game.CreateApi();
            var script = new Script();
            script.Globals["api"] = api;

            // Act
            script.DoString("api.giveGold(25)");

            // Assert
            _game.GetGameState()["player_gold"].Should().Be(75); // 50 + 25
        }

        [Fact]
        public void GameApi_Heal_IncreasesHealthWithCap()
        {
            // Arrange
            _game.Initialize();
            _game.GetGameState()["player_health"] = 80;
            var api = _game.CreateApi();
            var script = new Script();
            script.Globals["api"] = api;

            // Act - Small heal
            script.DoString("api.heal(15)");

            // Assert
            _game.GetGameState()["player_health"].Should().Be(95);

            // Act - Large heal (should cap at 100)
            script.DoString("api.heal(20)");

            // Assert
            _game.GetGameState()["player_health"].Should().Be(100);
        }

        [Fact]
        public void GameApi_Print_OutputsWithPluginPrefix()
        {
            // Arrange
            _game.Initialize();
            var api = _game.CreateApi();
            var script = new Script();
            script.Globals["api"] = api;

            // Act
            script.DoString("api.print('Test message from plugin')");

            // Assert
            _console.Output.Should().Contain("[Plugin] Test message from plugin");
        }

        [Fact]
        public void Run_SimulatesGameDays()
        {
            // Arrange
            _game.Initialize();

            // Act
            _game.Run();

            // Assert
            var output = _console.Output;
            output.Should().Contain("🌅 Day 1 begins");
            output.Should().Contain("🌅 Day 2 begins");
            output.Should().Contain("🌅 Day 3 begins");
            output.Should().Contain("🎮 Game Over!");
        }

        [Fact]
        public void Run_SimulatesCombatEvents()
        {
            // Arrange
            _game.Initialize();

            // Act
            _game.Run();

            // Assert
            var output = _console.Output;
            output.Should().Contain("⚔️ Combat encounter!");
            output.Should().Contain("Enemy attacks!");
            output.Should().Contain("Enemy attacks!");
            output.Should().Contain("damage");
        }

        [Fact]
        public void Run_SimulatesRandomEvents()
        {
            // Arrange
            _game.Initialize();

            // Act
            _game.Run();

            // Assert
            var output = _console.Output;
            // Should contain at least one of these events
            (output.Contains("💰 Shop event!") ||
             output.Contains("🎨 Rendering frame") ||
             output.Contains("🔊 Playing sound")).Should().BeTrue();
        }

        [Fact]
        public void Run_EndsWhenHealthReachesZero()
        {
            // Arrange
            _game.Initialize();
            _game.GetGameState()["player_health"] = 1; // Minimum health to ensure death on first hit

            // Act
            _game.Run();

            // Assert
            var output = _console.Output;
            output.Should().Contain("💀 You died!");
            output.Should().Contain("🎮 Game Over!");
            
            var finalHealth = (int)_game.GetGameState()["player_health"];
            finalHealth.Should().BeLessOrEqualTo(0);
        }

        [Fact]
        public void Run_TracksEnemiesDefeated()
        {
            // Arrange
            _game.Initialize();

            // Act
            _game.Run();

            // Assert
            var enemiesDefeated = (int)_game.GetGameState()["enemies_defeated"];
            enemiesDefeated.Should().BeGreaterThan(0);
        }

        [Fact]
        public void EventCallbacks_CanBeRegisteredThroughApi()
        {
            // Arrange
            _game.Initialize();
            var api = _game.CreateApi();
            
            // Create a simple Lua script that registers callbacks
            var script = new Script();
            script.Globals["game"] = api;
            
            script.DoString(@"
                game.onCombat = function(health, damage)
                    print('Combat callback: health=' .. health .. ', damage=' .. damage)
                    return damage * 2  -- Double damage
                end
            ");

            // Note: In the actual implementation, the game would need to check
            // and call these callbacks. This test verifies the API structure.

            // Assert
            api.Get("onCombat").Type.Should().Be(DataType.Function);
        }

        [Fact]
        public void GetGameState_ReturnsCurrentState()
        {
            // Arrange
            _game.Initialize();
            _game.GetGameState()["custom_value"] = "test";

            // Act
            var state = _game.GetGameState();

            // Assert
            state.Should().ContainKey("player_health");
            state.Should().ContainKey("player_gold");
            state.Should().ContainKey("day");
            state.Should().ContainKey("enemies_defeated");
            state.Should().ContainKey("custom_value");
            state["custom_value"].Should().Be("test");
        }

        [Fact]
        public void GameSimulation_ProducesRealisticOutput()
        {
            // Arrange
            _game.Initialize();

            // Act
            _game.Run();

            // Assert
            var output = _console.Output;
            
            // Check for variety of events
            var combatCount = CountOccurrences(output, "⚔️ Combat encounter!");
            var shopCount = CountOccurrences(output, "💰 Shop event!");
            var renderCount = CountOccurrences(output, "🎨 Rendering frame");
            var soundCount = CountOccurrences(output, "🔊 Playing sound");
            
            // Should have multiple events of different types
            combatCount.Should().BeGreaterThan(0);
            (shopCount + renderCount + soundCount).Should().BeGreaterThan(0);
            
            // Should progress through multiple days
            output.Should().Contain("Day 1");
            output.Should().Contain("Day 2");
        }

        [Fact]
        public void PluginEnumeration_ShouldDiscoverExistingPlugins()
        {
            // Arrange
            _game.Initialize();

            // Get the WotCI project directory
            var currentDir = Directory.GetCurrentDirectory();
            // Navigate from WotCI.Tests/bin/Debug/net6.0 to WotCI/plugins
            var wotciDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "WotCI"));
            var pluginsDir = Path.Combine(wotciDir, "plugins");

            // Verify plugin directories exist
            var userPluginsDir = Path.Combine(pluginsDir, "user");
            var partnerPluginsDirs = new[]
            {
                Path.Combine(pluginsDir, "deadlock-digital"),
                Path.Combine(pluginsDir, "segfault-studios")
            };

            // Act - Try to enumerate plugins
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

            // Assert - We should discover the sample plugins
            discoveredPlugins.Should().NotBeEmpty("Expected to find at least some plugins in the WotCI demo");
            
            // Verify specific expected plugins exist
            var pluginNames = discoveredPlugins.Select(Path.GetFileName).ToList();

            // These are the sample plugins that should exist in the WotCI demo
            var expectedPlugins = new[] { "my-tweaks.lua", "config.lua" }; // User plugins
            
            foreach (var expectedPlugin in expectedPlugins)
            {
                pluginNames.Should().Contain(expectedPlugin, 
                    $"Expected to find {expectedPlugin} in user plugins directory");
            }
        }

        [Fact] 
        public void PluginManager_ShouldDetectWorkingDirectoryIssue()
        {
            // Arrange
            _game.Initialize();
            
            // Act - Try to create a PluginManager with the current working directory
            // This should reveal the working directory issue
            try
            {
                var pluginManager = new PluginManager(_game);
                var availablePlugins = pluginManager.GetAvailablePlugins();
                
                // Assert - This should detect the issue where plugins aren't found
                // because PluginManager uses relative paths from the wrong working directory
                availablePlugins.Should().NotBeEmpty(
                    "PluginManager should find plugins, but it's likely using the wrong working directory. " +
                    $"Current directory: {Directory.GetCurrentDirectory()}. " +
                    "Expected plugins directory: WotCI/plugins/ relative to the WotCI project root.");
                
                // If we get here and plugins are found, verify some expected plugins exist
                var pluginNames = availablePlugins.Select(p => p.Name).ToList();
                pluginNames.Should().Contain("my-tweaks", "Expected to find my-tweaks plugin");
                pluginNames.Should().Contain("config", "Expected to find config plugin");
            }
            catch (Exception ex)
            {
                throw new Exception($"PluginManager failed to enumerate plugins due to working directory issue: {ex.Message}");
            }
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }
    }
}