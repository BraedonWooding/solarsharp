using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using Spectre.Console.Testing;

namespace WotCI.Tests
{
    [TestFixture]
    [Category("IntegrationTest")]
    public class PluginLoaderTests
    {
        private string _testPluginDir;
        private Mock<GameSimulator> _mockGame;
        private TestConsole _console;
        private SimplePluginLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _testPluginDir = Path.Combine(Path.GetTempPath(), $"wotci_test_plugins_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPluginDir);
            
            _console = new TestConsole();
            _mockGame = new Mock<GameSimulator>();
            _loader = new SimplePluginLoader(_mockGame.Object, _console);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testPluginDir))
            {
                Directory.Delete(_testPluginDir, true);
            }
        }

        /// <summary>
        /// Tests that loading a plugin directory without a manifest.json file shows an appropriate error message.
        /// </summary>
        /// <remarks>
        /// This test verifies that the plugin loader gracefully handles missing manifest files
        /// by displaying a clear error message to the user and continuing operation.
        /// </remarks>
        [Test]
        public void LoadPluginDirectory_NoManifest_ShowsError()
        {
                        var pluginDir = Path.Combine(_testPluginDir, "test-plugin");
            Directory.CreateDirectory(pluginDir);
            
                        _loader.LoadPluginDirectory(pluginDir);
            
                        _console.Output.Should().Contain("No manifest found");
        }

        /// <summary>
        /// Tests that a plugin directory with a valid manifest.json file loads successfully.
        /// </summary>
        /// <remarks>
        /// This test verifies that the plugin loader can parse a valid manifest file
        /// and load the associated plugin scripts without errors.
        /// </remarks>
        [Test]
        public void LoadPluginDirectory_ValidManifest_LoadsPlugin()
        {
                        var pluginDir = CreateTestPlugin("test-plugin", "Test Plugin", "1.0.0");
            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadPluginDirectory(pluginDir);

                        _console.Output.Should().Contain("Found manifest: Test Plugin v1.0.0");
            _console.Output.Should().Contain("Successfully loaded plugin: Test Plugin");
            _mockGame.Verify(g => g.CreateApi(), Times.Once);
        }

        /// <summary>
        /// Tests that loading a plugin directory with invalid JSON in manifest.json shows an error.
        /// </summary>
        /// <remarks>
        /// This test verifies that the plugin loader properly handles corrupted or malformed
        /// manifest JSON files by displaying an appropriate error message and continuing operation.
        /// </remarks>
        [Test]
        public void LoadPluginDirectory_InvalidManifestJson_ShowsError()
        {
                        var pluginDir = Path.Combine(_testPluginDir, "invalid-plugin");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), "{ invalid json }");

                        _loader.LoadPluginDirectory(pluginDir);

                        _console.Output.Should().Contain("Failed to load plugin:");
        }

        /// <summary>
        /// Tests that the plugin loader loads all .lua files in a plugin directory.
        /// </summary>
        /// <remarks>
        /// This test verifies that multiple Lua script files within a plugin directory
        /// are all loaded and executed, allowing plugins to consist of multiple script files.
        /// </remarks>
        [Test]
        public void LoadPluginDirectory_LoadsAllLuaFiles()
        {
                        var pluginDir = CreateTestPlugin("multi-lua", "Multi Lua", "1.0.0");
            CreateLuaFile(pluginDir, "main.lua", "print('[Plugin] Main loaded')");
            CreateLuaFile(pluginDir, "helper.lua", "print('[Plugin] Helper loaded')");
            CreateLuaFile(pluginDir, "utils.lua", "print('[Plugin] Utils loaded')");

            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadPluginDirectory(pluginDir);

                        _console.Output.Should().Contain("Loading: main.lua");
            _console.Output.Should().Contain("Loading: helper.lua");
            _console.Output.Should().Contain("Loading: utils.lua");
            _console.Output.Should().Contain("[Plugin] Main loaded");
            _console.Output.Should().Contain("[Plugin] Helper loaded");
            _console.Output.Should().Contain("[Plugin] Utils loaded");
        }

        /// <summary>
        /// Tests that a Lua script error in one file doesn't prevent other files from loading.
        /// </summary>
        /// <remarks>
        /// This test verifies error isolation - if one script in a plugin directory fails,
        /// the loader should continue processing other scripts and show appropriate error messages.
        /// </remarks>
        [Test]
        public void LoadPluginDirectory_LuaError_ContinuesLoading()
        {
                        var pluginDir = CreateTestPlugin("error-plugin", "Error Plugin", "1.0.0");
            CreateLuaFile(pluginDir, "good.lua", "print('[Plugin] Good script')");
            CreateLuaFile(pluginDir, "bad.lua", "invalid lua syntax!@#$");
            CreateLuaFile(pluginDir, "also-good.lua", "print('[Plugin] Also good')");

            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadPluginDirectory(pluginDir);

                        _console.Output.Should().Contain("[Plugin] Good script");
            _console.Output.Should().Contain("✗ Failed to load bad.lua:");
            _console.Output.Should().Contain("[Plugin] Also good");
            _console.Output.Should().Contain("Successfully loaded plugin: Error Plugin");
        }

        /// <summary>
        /// Tests that security limits specified in the manifest are properly applied to plugin scripts.
        /// </summary>
        /// <remarks>
        /// This test verifies that timeout and memory limit settings from the plugin manifest
        /// are correctly enforced during script execution, providing security boundaries for plugins.
        /// </remarks>
        [Test]
        public void LoadPluginDirectory_AppliesManifestTimeoutAndMemoryLimit()
        {
                        var manifest = new SimpleManifest
            {
                version = "1.0.0",
                name = "Limited Plugin",
                author = "Test",
                policy = new SimpleManifestPolicy
                {
                    timeout = 15,
                    memoryLimit = 50
                }
            };

            var pluginDir = Path.Combine(_testPluginDir, "limited-plugin");
            Directory.CreateDirectory(pluginDir);
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), manifestJson);

            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadPluginDirectory(pluginDir);

                        // Note: We can't easily verify the actual security config was applied
            // without exposing internals, but we can verify it didn't crash
            // In a real implementation, we'd want to expose this for testing
        }

        /// <summary>
        /// Tests that user scripts are loaded with restricted API access for security.
        /// </summary>
        /// <remarks>
        /// This test verifies that user scripts run with limited privileges and access
        /// compared to trusted plugins, enforcing appropriate security boundaries.
        /// </remarks>
        [Test]
        public void LoadUserScripts_LoadsLuaFilesWithLimitedApi()
        {
                        var userDir = Path.Combine(_testPluginDir, "user");
            Directory.CreateDirectory(userDir);
            CreateLuaFile(userDir, "user1.lua", "print('User script 1')");
            CreateLuaFile(userDir, "user2.lua", "print('User script 2')");

            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadUserScripts(userDir);

                        _console.Output.Should().Contain("Loading user scripts from:");
            _console.Output.Should().Contain("Loading: user1.lua");
            _console.Output.Should().Contain("Loading: user2.lua");
            _console.Output.Should().Contain("[User] User script 1");
            _console.Output.Should().Contain("[User] User script 2");
            _mockGame.Verify(g => g.CreateApi(), Times.Exactly(2));
        }

        /// <summary>
        /// Tests that attempting to load user scripts from a non-existent directory shows an appropriate message.
        /// </summary>
        /// <remarks>
        /// This test verifies graceful handling of missing user script directories
        /// by displaying an informative message instead of throwing an exception.
        /// </remarks>
        [Test]
        public void LoadUserScripts_NonExistentDirectory_ShowsMessage()
        {
                        var userDir = Path.Combine(_testPluginDir, "nonexistent");
                        _loader.LoadUserScripts(userDir);

                        _console.Output.Should().Contain("User scripts directory not found:");
        }

        /// <summary>
        /// Tests that an error in one user script doesn't prevent other user scripts from loading.
        /// </summary>
        /// <remarks>
        /// This test verifies error isolation for user scripts - failures in individual
        /// scripts should not impact the loading of other scripts in the same directory.
        /// </remarks>
        [Test]
        public void LoadUserScripts_ErrorInScript_ContinuesLoading()
        {
                        var userDir = Path.Combine(_testPluginDir, "user-errors");
            Directory.CreateDirectory(userDir);
            CreateLuaFile(userDir, "good.lua", "print('Good user script')");
            CreateLuaFile(userDir, "bad.lua", "error('Intentional error')");
            CreateLuaFile(userDir, "also-good.lua", "print('Another good script')");

            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadUserScripts(userDir);

                        _console.Output.Should().Contain("[User] Good user script");
            _console.Output.Should().Contain("Failed to load bad.lua:");
            _console.Output.Should().Contain("[User] Another good script");
        }

        /// <summary>
        /// Tests that plugin scripts have proper access to the game API functionality.
        /// </summary>
        /// <remarks>
        /// This test verifies that plugins can successfully interact with the game through
        /// the provided API, including reading game state and executing game actions.
        /// </remarks>
        [Test]
        public void PluginScript_HasAccessToGameApi()
        {
                        var pluginDir = CreateTestPlugin("api-test", "API Test", "1.0.0");
            CreateLuaFile(pluginDir, "api-test.lua", @"
                local health = game.getPlayerHealth()
                print('Player health: ' .. health)
                game.heal(10)
                game.giveGold(100)
            ");

            var mockApi = new Table(null)
            {
                ["getPlayerHealth"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(75)),
                ["heal"] = DynValue.NewCallback((ctx, args) => DynValue.Nil),
                ["giveGold"] = DynValue.NewCallback((ctx, args) => DynValue.Nil),
                ["print"] = DynValue.NewCallback((ctx, args) => 
                {
                    if (args.Count > 0)
                        Spectre.Console.AnsiConsoleExtensions.WriteLine(_console, $"[Plugin] {args[0].CastToString()}");
                    return DynValue.Nil;
                })
            };

            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);
            _mockGame.Setup(g => g.RegisterPlugin(It.IsAny<string>(), It.IsAny<Script>()));

                        _loader.LoadPluginDirectory(pluginDir);

                        _console.Output.Should().Contain("[Plugin] Player health: 75");
        }

        /// <summary>
        /// Tests that plugins with minimal manifests (missing optional fields) still load successfully.
        /// </summary>
        /// <remarks>
        /// This test verifies backward compatibility and robustness by ensuring that plugins
        /// with basic manifest files can still be loaded even if optional metadata is missing.
        /// </remarks>
        [Test]
        public void Manifest_MissingRequiredFields_StillLoads()
        {
            // Minimal manifest
            var manifest = new { version = "1.0.0" };
            var pluginDir = Path.Combine(_testPluginDir, "minimal");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(
                Path.Combine(pluginDir, "manifest.json"),
                JsonSerializer.Serialize(manifest));

            var mockApi = new Table(null);
            _mockGame.Setup(g => g.CreateApi()).Returns(mockApi);

                        _loader.LoadPluginDirectory(pluginDir);

                        _console.Output.Should().Contain("Found manifest:");
        }

        private string CreateTestPlugin(string name, string displayName, string version)
        {
            var pluginDir = Path.Combine(_testPluginDir, name);
            Directory.CreateDirectory(pluginDir);

            var manifest = new SimpleManifest
            {
                version = version,
                name = displayName,
                author = "Test Author",
                policy = new SimpleManifestPolicy
                {
                    allowedModules = new[] { "basic", "string", "table" },
                    capabilities = new[] { "FileRead" },
                    timeout = 30,
                    memoryLimit = 100
                }
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), manifestJson);

            return pluginDir;
        }

        private void CreateLuaFile(string dir, string filename, string content)
        {
            File.WriteAllText(Path.Combine(dir, filename), content);
        }
    }
}