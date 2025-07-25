using System;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter.Security;
using Spectre.Console.Testing;

// Type alias for backward compatibility

namespace WotCI.Tests
{
    [TestFixture]
    [Category("WotCI.Integration")]
    public class IntegrationTests
    {
        private IFileSystem _fileSystem;
        private string _testDir;
        private string _pluginsDir;
        private string _certsDir;

        [SetUp]
        public void SetUp()
        {
            // Use real file system for integration tests since SolarSharp Script.DoFile needs real files
            _fileSystem = new FileSystem();
            _testDir = Path.Combine(Path.GetTempPath(), $"wotci_integration_{Guid.NewGuid()}");
            _pluginsDir = Path.Combine(_testDir, "plugins");
            _certsDir = Path.Combine(_testDir, "certs");

            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(_pluginsDir);
            Directory.CreateDirectory(_certsDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void FullPluginLoadingScenario_WithCertificates()
        {
            // Create certificate hierarchy
            var (rootCa, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
            var (deadlockCert, deadlockKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa,
                rootCaKey,
                "Deadlock Digital",
                "/plugins/deadlock-digital"
            );
            var (segfaultCert, segfaultKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa,
                rootCaKey,
                "Segfault Studios",
                "/plugins/segfault-studios"
            );

            // Save certificates
            SaveCertificate(Path.Combine(_certsDir, "root-ca.crt"), rootCa);
            SaveCertificate(Path.Combine(_certsDir, "deadlock.crt"), deadlockCert);
            SaveCertificate(Path.Combine(_certsDir, "segfault.crt"), segfaultCert);

            // Create plugin directories
            var deadlockDir = CreatePluginStructure(
                "deadlock-digital",
                "Deadlock Digital Plugin",
                """

                                print('Deadlock Digital plugin loaded!')
                                game.onCombat = function(health, damage)
                                    print('Deadlock: Combat enhancement active')
                                    return damage * 0.9  -- Reduce damage by 10%
                                end
                            
                """
            );

            var segfaultDir = CreatePluginStructure(
                "segfault-studios",
                "Segfault Studios Plugin",
                """

                                print('Segfault Studios plugin loaded!')
                                game.onShop = function(gold)
                                    print('Segfault: Shop bonus active')
                                    game.giveGold(10)  -- Bonus gold in shops
                                end
                            
                """
            );

            // Load plugins
            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();

            var loader = new SimplePluginLoader(game, console, _fileSystem);

            loader.LoadPluginDirectory(deadlockDir);
            loader.LoadPluginDirectory(segfaultDir);

            var outputText = console.Output;
            outputText
                .Should()
                .Contain("✓ Found manifest: Deadlock Digital Plugin v1.0.0 by Test Author");
            outputText
                .Should()
                .Contain("✓ Found manifest: Segfault Studios Plugin v1.0.0 by Test Author");
            outputText.Should().Contain("[Plugin] Deadlock Digital plugin loaded!");
            outputText.Should().Contain("[Plugin] Segfault Studios plugin loaded!");
            outputText.Should().Contain("✓ Successfully loaded plugin: Deadlock Digital Plugin");
            outputText.Should().Contain("✓ Successfully loaded plugin: Segfault Studios Plugin");
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void MixedTrustedAndUntrustedPlugins()
        {
            var trustedDir = CreatePluginStructure(
                "trusted-plugin",
                "Trusted Plugin",
                """

                                print('Trusted plugin with full access')
                                local file = io.open('/tmp/test.txt', 'w')
                                if file then
                                    file:write('Trusted plugin was here')
                                    file:close()
                                    print('File written successfully')
                                else
                                    print('Could not write file')
                                end
                            
                """
            );

            var untrustedDir = Path.Combine(_pluginsDir, "user");
            Directory.CreateDirectory(untrustedDir);
            File.WriteAllText(
                Path.Combine(untrustedDir, "user-script.lua"),
                """

                                print('User script running')
                                -- This should have limited access
                                local success, err = pcall(function()
                                    io.open('/tmp/hack.txt', 'w')
                                end)
                                if not success then
                                    print('User script correctly restricted: ' .. tostring(err))
                                else
                                    print('ERROR: User script has too much access!')
                                end
                            
                """
            );

            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();
            var loader = new SimplePluginLoader(game, console, _fileSystem);

            loader.LoadPluginDirectory(trustedDir);
            loader.LoadUserScripts(untrustedDir);

            var outputText = console.Output;
            outputText.Should().Contain("Trusted plugin with full access");
            outputText.Should().Contain("User script running");
            // Note: Actual file access restrictions depend on Script security implementation
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void PluginWithVirtualFileSystem()
        {
            var (rootCa, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
            var (cert, certKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa,
                rootCaKey,
                "VFS Test Partner",
                "/plugins/vfs-test"
            );

            var pluginDir = CreatePluginStructure(
                "vfs-test",
                "VFS Test Plugin",
                """

                                print('VFS Test Plugin starting')
                                
                                -- Try to write to allowed path
                                local success1 = pcall(function()
                                    local f = io.open('/plugins/vfs-test/data.txt', 'w')
                                    if f then
                                        f:write('Plugin data')
                                        f:close()
                                        print('Wrote to allowed path')
                                    end
                                end)
                                
                                -- Try to write to restricted path
                                local success2 = pcall(function()
                                    local f = io.open('/plugins/other/data.txt', 'w')
                                    if f then
                                        f:write('Should not work')
                                        f:close()
                                        print('ERROR: Wrote to restricted path!')
                                    end
                                end)
                                
                                if not success2 then
                                    print('Correctly blocked access to restricted path')
                                end
                            
                """
            );

            // Setup VFS with certificate constraints
            var config = new SecurityPolicy
            {
                Capabilities = ScriptCapabilities.FileWrite | ScriptCapabilities.FileRead,
            };

            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/plugins/vfs-test");
            vfs.MountMemoryFileSystem("/plugins/other");

            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();
            var loader = new SimplePluginLoader(game, console, _fileSystem);

            loader.LoadPluginDirectory(pluginDir);

            var outputText = console.Output;
            outputText.Should().Contain("VFS Test Plugin starting");
            // Note: Actual VFS integration would require modifying the loader
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void PluginInteraction_SharedGameState()
        {
            var plugin1Dir = CreatePluginStructure(
                "plugin1",
                "Plugin 1",
                """

                                print('Plugin 1: Setting shared data')
                                game.sharedData = { message = 'Hello from Plugin 1' }
                                game.heal(20)
                            
                """
            );

            var plugin2Dir = CreatePluginStructure(
                "plugin2",
                "Plugin 2",
                """

                                print('Plugin 2: Reading shared data')
                                if game.sharedData and game.sharedData.message then
                                    print('Plugin 2 received: ' .. game.sharedData.message)
                                end
                                local health = game.getPlayerHealth()
                                print('Current health: ' .. health)
                            
                """
            );

            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();
            game.GetGameState()["player_health"] = 50; // Start with low health

            var loader = new SimplePluginLoader(game, console, _fileSystem);

            loader.LoadPluginDirectory(plugin1Dir);
            loader.LoadPluginDirectory(plugin2Dir);

            var outputText = console.Output;
            outputText.Should().Contain("Plugin 1: Setting shared data");
            outputText.Should().Contain("Plugin 2: Reading shared data");
            outputText.Should().Contain("Current health: 70"); // 50 + 20 from heal
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void PluginErrorHandling_DoesNotCrashOthers()
        {
            var goodPlugin1 = CreatePluginStructure(
                "good1",
                "Good Plugin 1",
                """

                                print('Good Plugin 1 loaded successfully')
                                game.giveGold(100)
                            
                """
            );

            var badPlugin = CreatePluginStructure(
                "bad",
                "Bad Plugin",
                """

                                print('Bad Plugin starting')
                                error('Intentional error in bad plugin!')
                                print('This should not print')
                            
                """
            );

            var goodPlugin2 = CreatePluginStructure(
                "good2",
                "Good Plugin 2",
                """

                                print('Good Plugin 2 loaded successfully')
                                local gold = game.getPlayerGold()
                                print('Current gold: ' .. gold)
                            
                """
            );

            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();

            var loader = new SimplePluginLoader(game, console, _fileSystem);

            loader.LoadPluginDirectory(goodPlugin1);
            loader.LoadPluginDirectory(badPlugin);
            loader.LoadPluginDirectory(goodPlugin2);

            var outputText = console.Output;
            outputText.Should().Contain("Good Plugin 1 loaded successfully");
            outputText.Should().Contain("Bad Plugin starting");
            outputText.Should().Contain("Failed to load");
            outputText.Should().Contain("Intentional error");
            outputText.Should().Contain("Good Plugin 2 loaded successfully");
            outputText.Should().Contain("Current gold: 150"); // 50 initial + 100 from plugin1
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void ManifestSecurity_DifferentPolicies()
        {
            // High security plugin
            var highSecPlugin = CreatePluginWithPolicy(
                "high-sec",
                "High Security Plugin",
                new SimpleManifestPolicy
                {
                    timeout = 5,
                    memoryLimit = 10,
                    allowedModules = ["basic", "string"],
                    capabilities = ["FileRead"],
                },
                """

                                    print('High security plugin running')
                                    -- Limited to basic operations
                                    local x = 1 + 1
                                    print('Math result: ' .. x)
                                
                """
            );

            // Low security plugin
            var lowSecPlugin = CreatePluginWithPolicy(
                "low-sec",
                "Low Security Plugin",
                new SimpleManifestPolicy
                {
                    timeout = 60,
                    memoryLimit = 200,
                    allowedModules = ["basic", "string", "table", "math", "io", "os"],
                    capabilities = ["FileRead", "FileWrite", "DirectoryOperations"],
                },
                """

                                    print('Low security plugin running')
                                    -- More capabilities available
                                    local t = {}
                                    for i = 1, 10 do
                                        table.insert(t, i * i)
                                    end
                                    print('Table operations completed')
                                
                """
            );

            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();
            var loader = new SimplePluginLoader(game, console, _fileSystem);

            loader.LoadPluginDirectory(highSecPlugin);
            loader.LoadPluginDirectory(lowSecPlugin);

            var outputText = console.Output;
            outputText.Should().Contain("High security plugin running");
            outputText.Should().Contain("Math result: 2");
            outputText.Should().Contain("Low security plugin running");
            outputText.Should().Contain("Table operations completed");
        }

        [Category("Plugin.Integration")]
        [Category("Plugin.Integration")]
        [Test]
        public void RealWorldScenario_GameWithMultiplePlugins()
        {
            // Create a realistic game scenario
            var combatPlugin = CreatePluginStructure(
                "combat-enhancer",
                "Combat Enhancer",
                """

                                print('Combat Enhancer loaded')
                                
                                local combatBonus = 1.2
                                local defenseBonus = 0.8
                                
                                game.onCombat = function(health, damage)
                                    print('Combat Enhancer: Applying bonuses')
                                    return math.floor(damage * defenseBonus)
                                end
                                
                                -- Track combat stats
                                if not game.combatStats then
                                    game.combatStats = { encounters = 0, totalDamage = 0 }
                                end
                            
                """
            );

            var uiPlugin = CreatePluginStructure(
                "ui-improvements",
                "UI Improvements",
                """

                                print('UI Improvements loaded')
                                
                                -- Add UI helpers
                                game.showStatus = function()
                                    print('=== Player Status ===')
                                    print('Health: ' .. game.getPlayerHealth() .. '/100')
                                    print('Gold: ' .. game.getPlayerGold())
                                    print('Day: ' .. game.getDay())
                                    print('Enemies Defeated: ' .. game.getEnemiesDefeated())
                                    print('===================')
                                end
                                
                                -- Show status on load
                                game.showStatus()
                            
                """
            );

            var achievementPlugin = CreatePluginStructure(
                "achievements",
                "Achievement System",
                """

                                print('Achievement System loaded')
                                
                                -- Check for achievements
                                game.checkAchievements = function()
                                    local gold = game.getPlayerGold()
                                    local enemies = game.getEnemiesDefeated()
                                    
                                    if gold >= 200 then
                                        print('Achievement Unlocked: Wealthy Warrior!')
                                    end
                                    
                                    if enemies >= 10 then
                                        print('Achievement Unlocked: Monster Slayer!')
                                    end
                                end
                                
                                -- Hook into day end
                                game.onDayEnd = function()
                                    print('Achievement System: Checking achievements...')
                                    game.checkAchievements()
                                end
                            
                """
            );

            // Run the game with all plugins
            var console = new TestConsole();
            var game = new GameSimulator(console);
            game.Initialize();

            var loader = new SimplePluginLoader(game, console, _fileSystem);

            // Load all plugins
            loader.LoadPluginDirectory(combatPlugin);
            loader.LoadPluginDirectory(uiPlugin);
            loader.LoadPluginDirectory(achievementPlugin);

            // Simulate some game actions
            game.GetGameState()["player_gold"] = 250;
            game.GetGameState()["enemies_defeated"] = 15;

            var outputText = console.Output;

            // All plugins loaded
            outputText.Should().Contain("Combat Enhancer loaded");
            outputText.Should().Contain("UI Improvements loaded");
            outputText.Should().Contain("Achievement System loaded");

            // UI plugin showed status
            outputText.Should().Contain("=== Player Status ===");
            outputText.Should().Contain("Health: 100/100");

            // Plugins can interact with game state
            outputText.Should().Contain("Successfully loaded plugin: Combat Enhancer");
            outputText.Should().Contain("Successfully loaded plugin: UI Improvements");
            outputText.Should().Contain("Successfully loaded plugin: Achievement System");
        }

        private string CreatePluginStructure(string name, string displayName, string luaCode)
        {
            var pluginDir = Path.Combine(_pluginsDir, name);
            Directory.CreateDirectory(pluginDir);

            var manifest = new SimpleManifest
            {
                version = "1.0.0",
                name = displayName,
                author = "Test Author",
                policy = new SimpleManifestPolicy
                {
                    allowedModules = ["basic", "string", "table", "math", "io"],
                    capabilities = ["FileRead", "FileWrite"],
                    timeout = 30,
                    memoryLimit = 100,
                },
            };

            File.WriteAllText(
                Path.Combine(pluginDir, "manifest.json"),
                JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true }
                )
            );

            File.WriteAllText(Path.Combine(pluginDir, "main.lua"), luaCode.Trim());

            return pluginDir;
        }

        private string CreatePluginWithPolicy(
            string name,
            string displayName,
            SimpleManifestPolicy policy,
            string luaCode
        )
        {
            var pluginDir = Path.Combine(_pluginsDir, name);
            Directory.CreateDirectory(pluginDir);

            var manifest = new SimpleManifest
            {
                version = "1.0.0",
                name = displayName,
                author = "Test Author",
                policy = policy,
            };

            File.WriteAllText(
                Path.Combine(pluginDir, "manifest.json"),
                JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true }
                )
            );

            File.WriteAllText(Path.Combine(pluginDir, "main.lua"), luaCode.Trim());

            return pluginDir;
        }

        private void SaveCertificate(string path, X509Certificate cert)
        {
            var pemBuilder = new StringBuilder();
            pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
            pemBuilder.AppendLine(
                Convert.ToBase64String(cert.GetEncoded(), Base64FormattingOptions.InsertLineBreaks)
            );
            pemBuilder.AppendLine("-----END CERTIFICATE-----");
            File.WriteAllText(path, pemBuilder.ToString());
        }
    }
}
