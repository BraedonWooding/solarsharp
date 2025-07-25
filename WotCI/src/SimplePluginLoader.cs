using System.IO.Abstractions;
using System.Text.Json;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;
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
        private readonly IFileSystem _fileSystem;

        public SimplePluginLoader(GameSimulator game)
            : this(game, AnsiConsole.Console, new FileSystem()) { }

        public SimplePluginLoader(GameSimulator game, IAnsiConsole console)
            : this(game, console, new FileSystem()) { }

        public SimplePluginLoader(GameSimulator game, IAnsiConsole console, IFileSystem fileSystem)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _console = console ?? throw new ArgumentNullException(nameof(console));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public virtual void LoadPluginDirectory(string pluginDir)
        {
            _console.WriteLine($"\nLoading plugins from: {pluginDir}");

            // Check for manifest
            var manifestPath = _fileSystem.Path.Combine(pluginDir, "manifest.json");
            if (!_fileSystem.File.Exists(manifestPath))
            {
                _console.WriteLine($"✗ No manifest found in {pluginDir}");
                return;
            }

            try
            {
                // Read and parse manifest JSON
                var manifestJson = _fileSystem.File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<SimpleManifest>(manifestJson);

                if (manifest == null)
                    throw new InvalidOperationException(
                        $"Failed to deserialize manifest from {manifestPath}"
                    );

                _console.WriteLine(
                    $"✓ Found manifest: {manifest.name} v{manifest.version} by {manifest.author}"
                );

                // Create script with appropriate security
                var script = CreateSecureScript(manifest, pluginDir);

                // Set up game API
                script.Globals["game"] = _game.CreateApi();

                // Load all Lua files (sorted for consistent order)
                var luaFiles = _fileSystem
                    .Directory.GetFiles(pluginDir, "*.lua")
                    .OrderBy(f => _fileSystem.Path.GetFileName(f))
                    .ToArray();
                foreach (var luaFile in luaFiles)
                {
                    try
                    {
                        _console.WriteLine($"  Loading: {_fileSystem.Path.GetFileName(luaFile)}");
                        script.DoFile(luaFile);
                    }
                    catch (Exception ex)
                    {
                        _console.WriteLine(
                            $"  ✗ Failed to load {_fileSystem.Path.GetFileName(luaFile)}: {ex.Message}"
                        );
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
            if (!_fileSystem.Directory.Exists(userDir))
            {
                _console.WriteLine($"User scripts directory not found: {userDir}");
                return;
            }

            _console.WriteLine($"\nLoading user scripts from: {userDir}");

            foreach (var luaFile in _fileSystem.Directory.GetFiles(userDir, "*.lua"))
            {
                try
                {
                    _console.WriteLine($"  Loading: {_fileSystem.Path.GetFileName(luaFile)}");

                    // User scripts get minimal security
                    var script = new Script(Examples.IsolatedBasePolicySet)
                    {
                        Globals =
                        {
                            // Very limited API
                            ["print"] =
                                (Action<string>)(msg => _console.WriteLine($"[User] {msg}")),
                            ["game"] = _game.CreateApi(), // Limited API
                        },
                    };

                    script.DoFile(luaFile);
                }
                catch (Exception ex)
                {
                    _console.WriteLine(
                        $"  ✗ Failed to load {_fileSystem.Path.GetFileName(luaFile)}: {ex.Message}"
                    );
                }
            }
        }

        private Script CreateSecureScript(SimpleManifest manifest, string pluginDir)
        {
            // Create security policy based on manifest
            var policy = Examples.Isolated();

            // Apply manifest policy if available
            if (manifest.policy != null)
            {
                // Apply allowed modules
                if (
                    manifest.policy.allowedModules != null
                    && manifest.policy.allowedModules.Length > 0
                )
                {
                    var modules = CoreModules.None;
                    foreach (var module in manifest.policy.allowedModules)
                    {
                        if (Enum.TryParse<CoreModules>(module, true, out var parsedModule))
                        {
                            modules |= parsedModule;
                        }
                    }
                    // When allowedModules is specified in manifest, use those modules
                    policy = policy with
                    {
                        AllowedModules = modules,
                    };
                }

                // Apply timeout
                if (manifest.policy.timeout > 0)
                {
                    policy = policy with { TimeoutMs = manifest.policy.timeout * 1000 };
                }

                // Apply memory limit
                if (manifest.policy.memoryLimit > 0)
                {
                    policy = policy with { MaxMemoryMB = manifest.policy.memoryLimit };
                }
            }

            // Create a custom BasePolicySet with the policy
            var policySet = new PolicySetBuilder()
                .DefinePolicy("plugin", policy)
                .MapFilePattern("*.lua", "plugin")
                .WithDefaultPolicy("plugin")
                .Build();

            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            if (basePolicySetResult.IsFailure)
            {
                // Fall back to isolated policy set if creation fails
                _console.WriteLine(
                    $"  Warning: Failed to create custom policy set: {basePolicySetResult.Error}"
                );
                return CreateBasicScript();
            }

            var script = new Script(basePolicySetResult.Value)
            {
                Globals =
                {
                    // Set up sandboxed print
                    ["print"] = DynValue.NewCallback(
                        (ctx, args) =>
                        {
                            if (args.Count > 0)
                            {
                                var parts = new string[args.Count];
                                for (var i = 0; i < args.Count; i++)
                                {
                                    parts[i] = args[i].CastToString();
                                }
                                var msg = string.Join(" ", parts);
                                _console.WriteLine($"[Plugin] {msg}");
                            }
                            return DynValue.Nil;
                        }
                    ),
                },
            };

            return script;
        }

        private Script CreateBasicScript()
        {
            return new Script(Examples.IsolatedBasePolicySet)
            {
                Globals =
                {
                    ["print"] = DynValue.NewCallback(
                        (ctx, args) =>
                        {
                            if (args.Count > 0)
                            {
                                var parts = new string[args.Count];
                                for (var i = 0; i < args.Count; i++)
                                {
                                    parts[i] = args[i].CastToString();
                                }
                                var msg = string.Join(" ", parts);
                                _console.WriteLine($"[Plugin] {msg}");
                            }
                            return DynValue.Nil;
                        }
                    ),
                },
            };
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

        // Properties with capital letters for backward compatibility
        public string? Version => version;
        public string? Name => name;
        public string? Author => author;
        public SimpleManifestPolicy? Policy => policy;
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

        // Properties with capital letters for backward compatibility
        public string[]? AllowedModules => allowedModules;
        public string[]? Capabilities => capabilities;
        public int Timeout => timeout;
        public int MemoryLimit => memoryLimit;
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
