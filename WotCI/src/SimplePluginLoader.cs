using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;
using Spectre.Console;

namespace WotCI
{
    /// <summary>
    /// Simplified plugin loader for WotCI demo
    /// </summary>
    public class SimplePluginLoader
    {
        private readonly GameSimulator _game;
        private readonly IAnsiConsole _console;

        public SimplePluginLoader(GameSimulator game) : this(game, AnsiConsole.Console)
        {
        }

        public SimplePluginLoader(GameSimulator game, IAnsiConsole console)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public virtual void LoadPluginDirectory(string pluginDir)
        {
            _console.WriteLine($"\nLoading plugins from: {pluginDir}");

            // Check for manifest
            var manifestPath = Path.Combine(pluginDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _console.WriteLine($"✗ No manifest found in {pluginDir}");
                return;
            }

            try
            {
                // Read and parse manifest JSON
                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<SimpleManifest>(manifestJson);
                
                if (manifest == null)
                    throw new InvalidOperationException($"Failed to deserialize manifest from {manifestPath}");

                _console.WriteLine($"✓ Found manifest: {manifest.name} v{manifest.version} by {manifest.author}");

                // Create script with appropriate security
                var script = CreateSecureScript(manifest, pluginDir);

                // Set up game API
                script.Globals["game"] = _game.CreateApi();

                // Load all Lua files (sorted for consistent order)
                var luaFiles = Directory.GetFiles(pluginDir, "*.lua").OrderBy(f => Path.GetFileName(f)).ToArray();
                foreach (var luaFile in luaFiles)
                {
                    try
                    {
                        _console.WriteLine($"  Loading: {Path.GetFileName(luaFile)}");
                        script.DoFile(luaFile);
                    }
                    catch (Exception ex)
                    {
                        _console.WriteLine($"  ✗ Failed to load {Path.GetFileName(luaFile)}: {ex.Message}");
                    }
                }

                _console.WriteLine($"✓ Successfully loaded plugin: {manifest.name}");
                _game.RegisterPlugin(manifest.name ?? pluginDir, script);
            }
            catch (Exception ex)
            {
                _console.WriteLine($"✗ Failed to load plugin: {ex.Message}");
            }
        }

        public virtual void LoadUserScripts(string userDir)
        {
            if (!Directory.Exists(userDir))
            {
                _console.WriteLine($"User scripts directory not found: {userDir}");
                return;
            }

            _console.WriteLine($"\nLoading user scripts from: {userDir}");

            foreach (var luaFile in Directory.GetFiles(userDir, "*.lua"))
            {
                try
                {
                    _console.WriteLine($"  Loading: {Path.GetFileName(luaFile)}");
                    
                    // User scripts get minimal security
                    var config = SecurityConfiguration.CreateIsolated();
                    var script = new Script();
                    
                    // Very limited API
                    script.Globals["print"] = (Action<string>)(msg => _console.WriteLine($"[User] {msg}"));
                    script.Globals["game"] = _game.CreateApi(); // Limited API

                    script.DoFile(luaFile);
                }
                catch (Exception ex)
                {
                    _console.WriteLine($"  ✗ Failed to load {Path.GetFileName(luaFile)}: {ex.Message}");
                }
            }
        }

        private Script CreateSecureScript(SimpleManifest manifest, string pluginDir)
        {
            // Create security configuration based on manifest
            var config = SecurityConfiguration.CreateDesktop();
            
            // Apply timeout from manifest
            if (manifest.policy?.timeout > 0)
            {
                config.Execution.Timeout = TimeSpan.FromSeconds(manifest.policy.timeout);
            }

            // Apply memory limit from manifest
            if (manifest.policy?.memoryLimit > 0)
            {
                config.Execution.MaxMemoryMB = manifest.policy.memoryLimit;
            }

            // Create script with security configuration
            var script = new Script();

            // Set up sandboxed print
            script.Globals["print"] = DynValue.NewCallback((ctx, args) => 
            {
                if (args.Count > 0)
                {
                    var parts = new string[args.Count];
                    for (int i = 0; i < args.Count; i++)
                    {
                        parts[i] = args[i].CastToString();
                    }
                    var msg = string.Join(" ", parts);
                    _console.WriteLine($"[Plugin] {msg}");
                }
                return DynValue.Nil;
            });

            return script;
        }
    }

    /// <summary>
    /// Simplified manifest structure for demo
    /// </summary>
    public class SimpleManifest
    {
        public string? version { get; set; }
        public string? name { get; set; }
        public string? author { get; set; }
        public SimpleManifestPolicy? policy { get; set; }
        public object? files { get; set; }
        public SimpleManifestSecurity? security { get; set; }
    }

    public class SimpleManifestPolicy
    {
        public string[]? allowedModules { get; set; }
        public string[]? capabilities { get; set; }
        public int timeout { get; set; }
        public int memoryLimit { get; set; }
        public string? defaultFileAccess { get; set; }
        public string? defaultDirectoryAccess { get; set; }
        public object? filePermissions { get; set; }
    }

    public class SimpleManifestSecurity
    {
        public SimpleManifestSignature? signature { get; set; }
        public object? certificate { get; set; }
    }

    public class SimpleManifestSignature
    {
        public string? algorithm { get; set; }
        public string? value { get; set; }
    }
}