using System;
using System.Collections.Generic;
using System.IO;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using Spectre.Console;

namespace WotCI
{
    public class GameSimulator
    {
        private readonly Dictionary<string, Script> _plugins = new();
        private readonly Dictionary<string, object> _gameState = new();
        private readonly Random _random = new();
        private readonly IAnsiConsole _console;

        public GameSimulator() : this(AnsiConsole.Console)
        {
        }

        public GameSimulator(IAnsiConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public virtual void Initialize()
        {
            _console.WriteLine("Initializing game world...");
            _console.WriteLine("🎮 Wrath of the CI King - Game Engine Initialized");
            _console.WriteLine("Starting with 100 health and 50 gold");

            // Create game directories
            Directory.CreateDirectory("game/assets");
            Directory.CreateDirectory("game/saves");
            Directory.CreateDirectory("game/logs");

            // Initialize game state
            _gameState["player_health"] = 100;
            _gameState["player_level"] = 1;
            _gameState["player_gold"] = 50;
            _gameState["enemies_defeated"] = 0;
            _gameState["game_time"] = 0;
            _gameState["day"] = 1;

            // Create some mock game assets
            File.WriteAllText("game/assets/config.lua", @"
return {
    combat = {
        damage_multiplier = 1.0,
        critical_chance = 0.1,
        block_chance = 0.2
    },
    graphics = {
        particle_density = 1.0,
        shadow_quality = 'medium',
        antialiasing = true
    },
    audio = {
        master_volume = 0.8,
        effects_volume = 0.7,
        music_volume = 0.5
    }
}
");

            // Create a save file
            File.WriteAllText("game/saves/autosave.sav", @"{
    ""player"": {
        ""health"": 100,
        ""level"": 1,
        ""experience"": 0,
        ""inventory"": []
    },
    ""world"": {
        ""current_location"": ""town"",
        ""time_of_day"": ""morning""
    }
}");

            _console.WriteLine("Game world initialized.");
        }

        public virtual void RegisterPlugin(string name, Script script)
        {
            _plugins[name] = script;
            
            // Set up game API for plugins
            script.Globals["game"] = script.DoString(@"
                local game = {}
                
                function game.log(message)
                    print('[' .. os.date('%Y-%m-%d %H:%M:%S') .. '] ' .. tostring(message))
                end
                
                function game.get_player_health()
                    return 100  -- Mock value
                end
                
                function game.set_player_health(health)
                    -- Mock implementation
                    game.log('Player health set to: ' .. health)
                end
                
                function game.get_game_time()
                    return os.time()
                end
                
                function game.save_data(key, value)
                    -- Mock save
                    game.log('Saved data: ' .. key .. ' = ' .. tostring(value))
                end
                
                function game.load_data(key)
                    -- Mock load
                    return nil
                end
                
                return game
            ");

            // Initialize the plugin
            try
            {
                script.DoString(@"
                    if initialize then
                        initialize()
                    end
                ");
            }
            catch (Exception ex)
            {
                _console.WriteLine($"  Plugin initialization error: {ex.Message}");
            }
        }

        public virtual void Run()
        {
            _console.WriteLine("\n=== Game Simulation Started ===\n");

            // Simulate game days
            while ((int)_gameState["player_health"] > 0 && (int)_gameState["day"] <= 5)
            {
                var day = (int)_gameState["day"];
                _console.WriteLine($"\n🌅 Day {day} begins");
                
                // Simulate daily events
                SimulateCombat();
                
                // Check if player died after combat
                if ((int)_gameState["player_health"] <= 0)
                {
                    _console.WriteLine("💀 You died!");
                    break;
                }
                
                // Random event - either shop, graphics, or audio
                var eventType = _random.Next(3);
                switch (eventType)
                {
                    case 0:
                        _console.WriteLine("💰 Shop event!");
                        _console.WriteLine("  You found a merchant selling healing potions.");
                        break;
                    case 1:
                        SimulateGraphicsUpdate();
                        break;
                    case 2:
                        SimulateAudioEvent();
                        break;
                }
                
                // End of day
                _gameState["day"] = day + 1;
                System.Threading.Thread.Sleep(10); // Minimal delay for testing
            }

            // Game over
            _console.WriteLine("\n🎮 Game Over!");
            _console.WriteLine($"Final player health: {_gameState["player_health"]}");
            _console.WriteLine($"Enemies defeated: {_gameState["enemies_defeated"]}");
            _console.WriteLine($"Plugins loaded: {_plugins.Count}");

            // Write game log
            var logPath = "game/logs/session.log";
            var logContent = $@"Game session completed
Time: {DateTime.Now}
Player Level: {_gameState["player_level"]}
Enemies Defeated: {_gameState["enemies_defeated"]}
Plugins Active: {string.Join(", ", _plugins.Keys)}
";
            File.WriteAllText(logPath, logContent);
            _console.WriteLine($"\nSession log written to: {logPath}");
        }

        private void SimulateCombat()
        {
            _console.WriteLine("⚔️ Combat encounter!");

            var playerHealth = (int)_gameState["player_health"];
            var damage = _random.Next(5, 15); // Reduced damage for longer games

            // Apply damage
            playerHealth -= damage;
            _gameState["player_health"] = Math.Max(0, playerHealth);
            
            _console.WriteLine($"  Enemy attacks! You took {damage} damage! Health: {_gameState["player_health"]}");
            
            // If survived, increment enemies defeated
            if (playerHealth > 0)
            {
                _gameState["enemies_defeated"] = (int)_gameState["enemies_defeated"] + 1;
            }

            // Call combat plugins
            foreach (var (name, script) in _plugins)
            {
                if (name.Contains("combat") || name.Contains("ai"))
                {
                    try
                    {
                        var result = script.DoString($@"
                            if on_combat then
                                return on_combat({playerHealth}, {damage})
                            end
                            return nil
                        ");
                        
                        if (result != null && !result.IsNil())
                        {
                            _console.WriteLine($"  Plugin '{name}' modified combat");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log non-critical error - plugin combat handling is optional
                        _console.WriteLine($"  [yellow]Warning: Plugin '{name}' combat handler error: {ex.Message}[/]");
                    }
                }
            }
        }

        private void SimulateGraphicsUpdate()
        {
            _console.WriteLine("🎨 Rendering frame...");

            // Call graphics plugins
            foreach (var (name, script) in _plugins)
            {
                if (name.Contains("graphics") || name.Contains("fx"))
                {
                    try
                    {
                        script.DoString(@"
                            if on_render then
                                on_render()
                            end
                        ");
                        _console.WriteLine($"  Plugin '{name}' applied visual effects");
                    }
                    catch (Exception ex)
                    {
                        // Log non-critical error - plugin render handling is optional
                        _console.WriteLine($"  [yellow]Warning: Plugin '{name}' render handler error: {ex.Message}[/]");
                    }
                }
            }
        }

        private void SimulateAudioEvent()
        {
            var sounds = new[] { "sword_clash", "spell_cast", "footsteps", "ambient_wind" };
            var sound = sounds[_random.Next(sounds.Length)];
            
            _console.WriteLine($"🔊 Playing sound: {sound}");

            // Call audio plugins
            foreach (var (name, script) in _plugins)
            {
                if (name.Contains("sound") || name.Contains("audio"))
                {
                    try
                    {
                        script.DoString($@"
                            if on_sound then
                                on_sound('{sound}')
                            end
                        ");
                        _console.WriteLine($"  Plugin '{name}' processed audio");
                    }
                    catch (Exception ex)
                    {
                        // Log non-critical error - plugin sound handling is optional
                        _console.WriteLine($"  [yellow]Warning: Plugin '{name}' sound handler error: {ex.Message}[/]");
                    }
                }
            }
        }

        // Public methods for plugin API
        public virtual int GetPlayerHealth()
        {
            return (int)(_gameState.ContainsKey("player_health") ? _gameState["player_health"] : 100);
        }

        public virtual void SetPlayerHealth(int health)
        {
            _gameState["player_health"] = Math.Max(0, Math.Min(100, health));
        }

        public virtual int GetPlayerGold()
        {
            return (int)(_gameState.ContainsKey("player_gold") ? _gameState["player_gold"] : 50);
        }

        public virtual void GiveGold(int amount)
        {
            var currentGold = GetPlayerGold();
            _gameState["player_gold"] = Math.Max(0, currentGold + amount);
        }

        public virtual int GetDay()
        {
            return (int)(_gameState.ContainsKey("day") ? _gameState["day"] : 1);
        }

        public virtual int GetEnemiesDefeated()
        {
            return (int)(_gameState.ContainsKey("enemies_defeated") ? _gameState["enemies_defeated"] : 0);
        }

        public virtual Dictionary<string, object> GetGameState()
        {
            return _gameState;
        }

        public virtual SolarSharp.Interpreter.DataTypes.Table CreateApi()
        {
            var api = new SolarSharp.Interpreter.DataTypes.Table(null);
            
            // Game state getters
            api["getPlayerHealth"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(GetPlayerHealth()));
            api["getPlayerGold"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(GetPlayerGold()));
            api["getDay"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(GetDay()));
            api["getEnemiesDefeated"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(GetEnemiesDefeated()));
            
            // Game actions
            api["print"] = DynValue.NewCallback((ctx, args) => 
            {
                if (args.Count > 0)
                    _console.WriteLine($"[Plugin] {args[0].CastToString()}");
                return DynValue.Nil;
            });
            api["giveGold"] = DynValue.NewCallback((ctx, args) => 
            {
                if (args.Count > 0)
                    GiveGold((int)args[0].Number);
                return DynValue.Nil;
            });
            api["heal"] = DynValue.NewCallback((ctx, args) => 
            {
                if (args.Count > 0)
                {
                    var health = GetPlayerHealth() + (int)args[0].Number;
                    SetPlayerHealth(health);
                }
                return DynValue.Nil;
            });
            
            // Event registration
            api["onCombat"] = DynValue.Nil;
            api["onShop"] = DynValue.Nil;
            api["onDayEnd"] = DynValue.Nil;
            
            return api;
        }
    }
}