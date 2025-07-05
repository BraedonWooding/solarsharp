using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SolarSharp.Interpreter.CoreLib;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.IO;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Tree.Expressions;
using SolarSharp.Interpreter.Tree.Fast_Interface;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// This class implements a MoonSharp scripting session. Multiple Script objects can coexist in the same program but cannot share
    /// data among themselves unless some mechanism is put in place.
    /// </summary>
    public class Script : IScriptPrivateResource
    {
        /// <summary>
        /// The version of the MoonSharp engine
        /// </summary>
        public const string VERSION = "2.0.0.0";

        /// <summary>
        /// The Lua version being supported
        /// </summary>
        public const string LUA_VERSION = "5.2";
        private readonly Processor m_MainProcessor = null;
        private readonly ByteCode m_ByteCode;
        private readonly List<SourceCode> m_Sources = new();
        private readonly Table m_GlobalTable;
        private IDebugger m_Debugger;
        private readonly Table[] m_TypeMetatables = new Table[(int)LuaTypeExtensions.MaxMetaTypes];
        private readonly List<Manifest> m_Manifests = new();
        private SystemManifest m_CompiledManifest;
        private readonly SecurityLogger m_SecurityLogger = new();
        private readonly CryptoManager m_CryptoManager = new();
        
        /// <summary>
        /// Gets the platform accessor for this script instance.
        /// </summary>
        public IPlatformAccessor Platform { get; private set; }

        /// <summary>
        /// Initializes the <see cref="Script"/> class.
        /// </summary>
        static Script()
        {
            GlobalOptions = new ScriptGlobalOptions();

            DefaultOptions = new ScriptOptions()
            {
                DebugPrint = static s => GlobalOptions.Platform.DefaultPrint(s),
                DebugInput = static s => GlobalOptions.Platform.DefaultInput(s),
                CheckThreadAccess = true,
                ScriptLoader = PlatformAutoDetector.GetDefaultScriptLoader(),
                TailCallOptimizationThreshold = 65536
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with default desktop manifest.
        /// Uses Desktop manifest: 60s timeout, 128MB memory limit, no chroot.
        /// String execution is enabled by default for backward compatibility.
        /// Can be overridden by environment variables.
        /// </summary>
        public Script()
            : this(manifest: GetDefaultManifestFromEnvironment())
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with SecurityConfiguration.
        /// SecurityConfiguration provides a fluent C# API for building security policies programmatically.
        /// </summary>
        /// <param name="securityConfig">Security configuration to apply</param>
        public Script(SecurityConfiguration securityConfig)
        {
            if (securityConfig == null)
                throw new ArgumentNullException(nameof(securityConfig), "SecurityConfiguration is required - scripts cannot run without authorization");
            
            // Initialize platform accessor before anything else that might need it
            Platform = GlobalOptions.Platform ?? PlatformAutoDetector.GetDefaultPlatform();
            
            // Initialize core components
            Options = new ScriptOptions(DefaultOptions);
            PerformanceStats = new PerformanceStatistics();
            Registry = new Table(this);

            m_ByteCode = new ByteCode(this);
            m_GlobalTable = new Table(this).RegisterCoreModules(securityConfig.AllowedModules);
            m_MainProcessor = new Processor(this, m_GlobalTable, m_ByteCode);

            // Apply security configuration directly
            ApplySecurityConfiguration(securityConfig);

            // Auto-register security tracer if environment variables are set
            RegisterSecurityTracerIfEnabled();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with a security policy.
        /// Accepts either Manifest or SecurityConfiguration objects.
        /// </summary>
        /// <param name="securityPolicy">Security policy to apply (defaults to SystemManifest.Desktop if null)</param>
        public Script(ISecurityPolicy securityPolicy)
            : this(securityPolicy?.ToManifest() ?? SystemManifest.Desktop)
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with specified manifest.
        /// String execution is enabled by default when explicitly providing a manifest.
        /// </summary>
        /// <param name="manifest">Manifest to apply (defaults to SystemManifest.Desktop if null)</param>
        public Script(Manifest manifest)
            : this(manifest, true)
        {
        }

        /// <summary>
        /// Internal constructor with full control over initialization
        /// </summary>
        private Script(Manifest manifest, bool initialize)
        {
            // Use Desktop as default manifest
            manifest = manifest ?? SystemManifest.Desktop;
            
            // Use Desktop security configuration as base - manifests contain policy, SecurityConfiguration is the implementation
            var securityConfig = new SecurityConfiguration();
            
            // Apply manifest policy to security configuration
            if (manifest.Policy != null)
            {
                if (manifest.Policy.PreventRunString.HasValue)
                    securityConfig.AntiPolymorphism.PreventRunString = manifest.Policy.PreventRunString.Value;
                if (manifest.Policy.PreventInternalDynamicCode.HasValue)
                    securityConfig.AntiPolymorphism.PreventInternalDynamicCode = manifest.Policy.PreventInternalDynamicCode.Value;
                if (manifest.Policy.PreventLuaFileWrites.HasValue)
                    securityConfig.AntiPolymorphism.PreventLuaFileWrites = manifest.Policy.PreventLuaFileWrites.Value;
                if (manifest.Policy.AllowOnlyLuaExtension.HasValue)
                    securityConfig.AntiPolymorphism.AllowOnlyLuaExtension = manifest.Policy.AllowOnlyLuaExtension.Value;
            }
            
            // Initialize platform accessor before anything else that might need it
            Platform = GlobalOptions.Platform ?? PlatformAutoDetector.GetDefaultPlatform();
            
            // Initialize core components
            Options = new ScriptOptions(DefaultOptions);
            PerformanceStats = new PerformanceStatistics();
            Registry = new Table(this);

            m_ByteCode = new ByteCode(this);
            m_GlobalTable = new Table(this).RegisterCoreModules(securityConfig.AllowedModules);
            m_MainProcessor = new Processor(this, m_GlobalTable, m_ByteCode);

            // Store manifest
            m_Manifests.Add(manifest);
            
            // Use the manifest directly to avoid validation issues
            m_CompiledManifest = manifest as SystemManifest ?? SystemManifest.Desktop;

            // If manifest is signed, automatically load its key
            if (manifest.IsSigned())
            {
                LoadKey(manifest.Security.PublicKey);
            }

            // Store and apply security configuration
            this.SetSecurityConfiguration(securityConfig);
            ApplySecurityConfiguration(securityConfig);

            // Auto-register security tracer if environment variables are set
            RegisterSecurityTracerIfEnabled();
        }



        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with security overrides.
        /// Base security is Configuration-level with specified overrides applied.
        /// </summary>
        /// <param name="configureOverrides">Action to configure security overrides</param>
        public Script(Action<SecurityConfigurationOverrides> configureOverrides)
            : this(new SecurityConfiguration().WithOverrides(configureOverrides))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with application name and optional configuration.
        /// Automatically whitelists platform-specific application data directories.
        /// </summary>
        /// <param name="applicationName">Application name for data directory whitelisting</param>
        /// <param name="timeoutMs">Execution timeout in milliseconds (default: 30000)</param>
        public Script(string applicationName, int timeoutMs = 30000)
            : this(overrides => {
                overrides.ApplicationName = applicationName;
                overrides.TimeoutMs = timeoutMs;
            })
        {
        }


        /// <summary>
        /// Gets or sets the script loader which will be used as the value of the
        /// ScriptLoader property for all newly created scripts.
        /// </summary>
        public static ScriptOptions DefaultOptions { get; private set; }

        /// <summary>
        /// Gets access to the script options. 
        /// </summary>
        public ScriptOptions Options { get; private set; }

        /// <summary>
        /// Gets the global options, that is options which cannot be customized per-script.
        /// </summary>
        public static ScriptGlobalOptions GlobalOptions { get; private set; }

        /// <summary>
        /// Gets access to performance statistics.
        /// </summary>
        public PerformanceStatistics PerformanceStats { get; private set; }
        
        /// <summary>
        /// Gets the security logger for monitoring security events
        /// </summary>
        /// <returns>The security logger</returns>
        public ISecurityLogger GetSecurityLogger()
        {
            return m_SecurityLogger;
        }


        /// <summary>
        /// Gets the default global table for this script. Unless a different table is intentionally passed (or setfenv has been used)
        /// execution uses this table.
        /// </summary>
        public Table Globals
        {
            get { return m_GlobalTable; }
        }

        /// <summary>
        /// Loads a string containing a Lua/MoonSharp function.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="funcFriendlyName">Name of the function used to report errors, etc.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadFunction(string code, Table globalTable = null, string funcFriendlyName = null)
        {
            this.CheckScriptOwnership(globalTable);

            var chunkName = $"libfunc_{funcFriendlyName ?? m_Sources.Count.ToString()}";

            SourceCode source = new(chunkName, code, m_Sources.Count, this);

            m_Sources.Add(source);

            int address = Loader_Fast.LoadFunction(this, source, m_ByteCode, globalTable != null || m_GlobalTable != null);

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            return MakeClosure(address, globalTable ?? m_GlobalTable);
        }

        private void SignalByteCodeChange()
        {
            m_Debugger?.SetByteCode(m_ByteCode.Code.Select(s => s.ToString()).ToArray());
        }

        private void SignalSourceCodeChange(SourceCode source)
        {
            m_Debugger?.SetSourceCode(source);
        }

        /// <summary>
        /// Applies security configuration to this script instance
        /// </summary>
        private void ApplySecurityConfiguration(SecurityConfiguration config)
        {
            // Store security configuration as the primary source of truth
            this.SetSecurityConfiguration(config);

            // Initialize security event handler first
            var eventHandler = new SecurityEventHandler();
            this.SetSecurityEventHandler(eventHandler);

            // Register the event handler with the security logger
            m_SecurityLogger.AddHandler(eventHandler);

            // Configure platform accessor with security restrictions using shared logger
            Platform = new SecurePlatformAccessor(config, m_SecurityLogger);

            // Configure interop security
            UserData.DefaultAccessMode = config.Interop.DefaultAccessMode;

            // Initialize resource controller for execution limits
            var resourceController = new ResourceController(config.Execution);
            this.SetResourceController(resourceController);

            // Initialize VFS if chroot is enabled
            if (config.EnableChroot)
            {
                // VFS will be created when needed by SecurePlatformAccessor
                // The config contains the necessary information
            }
        }

        /// <summary>
        /// Converts a manifest to SecurityConfiguration for compatibility
        /// </summary>
        private SecurityConfiguration ConvertManifestToSecurityConfig(Manifest manifest)
        {
            // If manifest has a policy, convert it to overrides
            if (manifest.Policy != null)
            {
                var overrides = manifest.Policy.ToSecurityOverrides();
                return overrides.ApplyTo(new SecurityConfiguration());
            }
            
            // Otherwise use default configuration
            return new SecurityConfiguration();
        }

        /// <summary>
        /// Recompiles all manifests into a single system manifest for fast runtime checks
        /// </summary>
        private void RecompileManifest()
        {
            if (!m_Manifests.Any())
            {
                m_CompiledManifest = SystemManifest.Desktop;
                return;
            }

            // For now, if we have only one manifest and it's already a SystemManifest, use it directly
            // This avoids issues with the ManifestComposer creating rules with null values
            if (m_Manifests.Count == 1 && m_Manifests[0] is SystemManifest systemManifest)
            {
                m_CompiledManifest = systemManifest;
                return;
            }

            // Use ManifestComposer to properly compose all manifests
            var composer = new ManifestComposer();
            var composedManifest = composer.Compose(m_Manifests);
            
            // Promote the composed manifest to a SystemManifest with validation
            m_CompiledManifest = SystemManifest.FromManifest(composedManifest);
        }

        /// <summary>
        /// Adds a manifest to the script at runtime
        /// </summary>
        /// <param name="manifest">The manifest to add</param>
        /// <param name="trustLevel">Trust level for the manifest</param>
        public void AddManifest(Manifest manifest, TrustLevel trustLevel = TrustLevel.Untrusted)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
                
            // Validate manifest signatures if present
            if (manifest.Security?.Signature != null)
            {
                // Verify signature and determine actual trust level
                var actualTrustLevel = ManifestTrustStore.GetTrustLevel(manifest);
                
                // Can't elevate trust beyond what the signature allows
                if (actualTrustLevel < trustLevel)
                {
                    trustLevel = actualTrustLevel;
                }
            }
            
            // Set the trust level on the manifest
            manifest.TrustLevel = trustLevel;
            
            // Add to manifest list
            m_Manifests.Add(manifest);
            
            // Recompile manifests
            RecompileManifest();
            
            // Update security configuration
            var newConfig = ConvertManifestToSecurityConfig(m_CompiledManifest);
            ApplySecurityConfiguration(newConfig);
        }


        /// <summary>
        /// Loads a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadString(string code, Table globalTable = null, string codeFriendlyName = null)
        {
            if (IsStringExecutionPrevented())
            {
                throw new UnauthorizedProcessExecutionException(
                    "External string loading is disabled. Set PreventRunString to false in the manifest policy to enable.",
                    "LoadString"
                );
            }

            this.CheckScriptOwnership(globalTable);

            if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
            {
                code = code[StringModule.BASE64_DUMP_HEADER.Length..];
                byte[] data = Convert.FromBase64String(code);
                using MemoryStream ms = new(data);
                return LoadStream(ms, globalTable, codeFriendlyName);
            }

            string chunkName = string.Format("{0}", codeFriendlyName ?? "chunk_" + m_Sources.Count.ToString());

            SourceCode source = new(codeFriendlyName ?? chunkName, code, m_Sources.Count, this);

            m_Sources.Add(source);

            int address = Loader_Fast.LoadChunk(this,
                source,
                m_ByteCode);

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            return MakeClosure(address, globalTable ?? m_GlobalTable);
        }

        /// <summary>
        /// Loads a Lua/MoonSharp script from a System.IO.Stream. NOTE: This will *NOT* close the stream!
        /// </summary>
        /// <param name="stream">The stream containing code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadStream(Stream stream, Table globalTable = null, string codeFriendlyName = null)
        {
            this.CheckScriptOwnership(globalTable);

            Stream codeStream = new UndisposableStream(stream);

            if (!Processor.IsDumpStream(codeStream))
            {
                using StreamReader sr = new(codeStream);
                string scriptCode = sr.ReadToEnd();
                return LoadStringInternal(scriptCode, globalTable, codeFriendlyName);
            }
            else
            {
                string chunkName = string.Format("{0}", codeFriendlyName ?? "dump_" + m_Sources.Count.ToString());

                SourceCode source = new(codeFriendlyName ?? chunkName,
                    string.Format("-- This script was decoded from a binary dump - dump_{0}", m_Sources.Count),
                    m_Sources.Count, this);

                m_Sources.Add(source);

                int address = m_MainProcessor.Undump(codeStream, m_Sources.Count - 1, globalTable ?? m_GlobalTable, out bool hasUpvalues);

                SignalSourceCodeChange(source);
                SignalByteCodeChange();

                if (hasUpvalues)
                    return MakeClosure(address, globalTable ?? m_GlobalTable);
                else
                    return MakeClosure(address);
            }
        }

        /// <summary>
        /// Dumps on the specified stream.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="stream">The stream.</param>
        /// <exception cref="ArgumentException">
        /// function arg is not a function!
        /// or
        /// stream is readonly!
        /// or
        /// function arg has upvalues other than _ENV
        /// </exception>
        public void Dump(DynValue function, Stream stream)
        {
            this.CheckScriptOwnership(function);

            if (function.Type != DataType.Function)
                throw new ArgumentException("function arg is not a function!");

            if (!stream.CanWrite)
                throw new ArgumentException("stream is readonly!");

            Closure.UpvaluesType upvaluesType = function.Function.GetUpvaluesType();

            if (upvaluesType == Closure.UpvaluesType.Closure)
                throw new ArgumentException("function arg has upvalues other than _ENV");

            UndisposableStream outStream = new(stream);
            m_MainProcessor.Dump(outStream, function.Function.EntryPointByteCodeLocation, upvaluesType == Closure.UpvaluesType.Environment);
        }


        /// <summary>
        /// Loads a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="filename">The code.</param>
        /// <param name="globalContext">The global table to bind to this chunk.</param>
        /// <param name="friendlyFilename">The filename to be used in error messages.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadFile(string filename, Table globalContext = null, string friendlyFilename = null)
        {
            this.CheckScriptOwnership(globalContext);

#pragma warning disable 618
            filename = Options.ScriptLoader.ResolveFileName(filename, globalContext ?? m_GlobalTable);
#pragma warning restore 618

            // Validate manifest requirement if keys are loaded and file is a .lua file
            if (filename.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                ValidateManifestRequirement(filename);
            }

            object code = Options.ScriptLoader.LoadFile(filename, globalContext ?? m_GlobalTable);
            switch (code)
            {
                case string v: return LoadStringInternal(v, globalContext, friendlyFilename ?? filename);
                case byte[] bytes: using (MemoryStream ms = new(bytes)) return LoadStream(ms, globalContext, friendlyFilename ?? filename);
                case Stream stream: using (stream) return LoadStream(stream, globalContext, friendlyFilename ?? filename);
                case null: throw new InvalidCastException("Unexpected null from IScriptLoader.LoadFile");
                default: throw new InvalidCastException(string.Format("Unsupported return type from IScriptLoader.LoadFile : {0}", code.GetType()));
            }
        }


        /// <summary>
        /// Loads and executes a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
        {
            if (IsStringExecutionPrevented())
            {
                throw new UnauthorizedProcessExecutionException(
                    "External string execution is disabled. Set PreventRunString to false in the manifest policy to enable.",
                    "DoString"
                );
            }

            DynValue func = LoadString(code, globalContext, codeFriendlyName);
            return Call(func);
        }

        /// <summary>
        /// Internal version of DoString that bypasses StringExecution control (for VM internal use)
        /// </summary>
        internal DynValue DoStringInternal(string code, Table globalContext = null, string codeFriendlyName = null)
        {
            DynValue func = LoadStringInternal(code, globalContext, codeFriendlyName);
            return Call(func);
        }

        /// <summary>
        /// Internal version of LoadString that bypasses StringExecution control (for VM internal use)
        /// </summary>
        internal DynValue LoadStringInternal(string code, Table globalTable = null, string codeFriendlyName = null)
        {
            this.CheckScriptOwnership(globalTable);

            if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
            {
                code = code[StringModule.BASE64_DUMP_HEADER.Length..];
                byte[] data = Convert.FromBase64String(code);
                using MemoryStream ms = new(data);
                return LoadStream(ms, globalTable, codeFriendlyName);
            }

            string chunkName = string.Format("{0}", codeFriendlyName ?? "chunk_" + m_Sources.Count.ToString());

            SourceCode source = new(codeFriendlyName ?? chunkName, code, m_Sources.Count, this);

            m_Sources.Add(source);

            int address = Loader_Fast.LoadChunk(this, source, m_ByteCode);

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            return MakeClosure(address, globalTable ?? m_GlobalTable);
        }


        /// <summary>
        /// Loads and executes a stream containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing the loaded chunk.
        /// </returns>
        public DynValue DoStream(Stream stream, Table globalContext = null, string codeFriendlyName = null)
        {
            DynValue func = LoadStream(stream, globalContext, codeFriendlyName);
            return Call(func);
        }


        /// <summary>
        /// Loads and executes a file containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
        {
            DynValue func = LoadFile(filename, globalContext, codeFriendlyName);
            return Call(func);
        }


        /// <summary>
        /// Runs a Lua file with secure defaults and automatic manifest discovery.
        /// Uses Configuration-level security unless overridden by manifest.
        /// </summary>
        /// <param name="filename">The filename to execute</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunFile(string filename)
        {
            return RunFile(filename, new SecurityConfiguration());
        }

        /// <summary>
        /// Runs a Lua file with specified security configuration and manifest discovery.
        /// Manifest overrides take precedence over base configuration.
        /// </summary>
        /// <param name="filename">The filename to execute</param>
        /// <param name="baseConfig">Base security configuration</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunFile(string filename, SecurityConfiguration baseConfig)
        {
            return RunFile(filename, baseConfig, null);
        }

        /// <summary>
        /// Runs a Lua file with full configuration control.
        /// </summary>
        /// <param name="filename">The filename to execute</param>
        /// <param name="baseConfig">Base security configuration</param>
        /// <param name="explicitOverrides">Explicit overrides (highest precedence)</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunFile(string filename, SecurityConfiguration baseConfig, Action<SecurityConfigurationOverrides> explicitOverrides)
        {
            // Discover manifest
            var manifestOverrides = ManifestAutoLoader.CreateOverridesFromManifest(filename, baseConfig);
            
            // Apply explicit overrides if provided
            var explicitOverridesObj = explicitOverrides != null 
                ? SecurityConfigurationOverrides.FromAction(explicitOverrides) 
                : null;

            // Resolve final configuration: Base → Manifest → Explicit
            var finalConfig = SecurityConfiguration.ResolveConfiguration(baseConfig, manifestOverrides, explicitOverridesObj);
            
            // File scripts now use default file access levels configured in SecurityConfiguration
            // The granular file access model replaces the simple WritePolicy system

            var script = new Script(finalConfig);
            return script.DoFile(filename);
        }

        /// <summary>
        /// Runs Lua code from string with secure defaults.
        /// Uses Configuration-level security with chroot to current directory.
        /// </summary>
        /// <param name="code">The Lua/MoonSharp code to execute</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunString(string code)
        {
            return RunString(code, Environment.CurrentDirectory);
        }

        /// <summary>
        /// Runs Lua code from string with specified application directory as chroot.
        /// Automatic manifest discovery from application directory.
        /// </summary>
        /// <param name="code">The Lua/MoonSharp code to execute</param>
        /// <param name="applicationDirectory">Directory that appears as "/" to the script</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunString(string code, string applicationDirectory)
        {
            return RunString(code, applicationDirectory, new SecurityConfiguration());
        }

        /// <summary>
        /// Runs Lua code from string with full configuration control.
        /// </summary>
        /// <param name="code">The Lua/MoonSharp code to execute</param>
        /// <param name="applicationDirectory">Directory that appears as "/" to the script</param>
        /// <param name="baseConfig">Base security configuration</param>
        /// <param name="explicitOverrides">Optional explicit overrides</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunString(string code, string applicationDirectory, SecurityConfiguration baseConfig, Action<SecurityConfigurationOverrides> explicitOverrides = null)
        {
            // Discover manifest from application directory
            var manifestPath = Path.Combine(applicationDirectory, "LuaManifest.json");
            var manifestOverrides = File.Exists(manifestPath) 
                ? ManifestAutoLoader.CreateOverridesFromManifest(manifestPath, baseConfig)
                : null;
            
            // Apply explicit overrides if provided
            var explicitOverridesObj = explicitOverrides != null 
                ? SecurityConfigurationOverrides.FromAction(explicitOverrides) 
                : null;

            // Resolve final configuration: Base → Manifest → Explicit
            var finalConfig = SecurityConfiguration.ResolveConfiguration(baseConfig, manifestOverrides, explicitOverridesObj);
            
            // Ensure chroot is enabled and set to application directory for string scripts
            if (finalConfig.EnableChroot)
            {
                finalConfig.SetDirectoryPermissions(Path.GetFullPath(applicationDirectory), DirectoryPermissions.ListAndCreateFiles);
            }

            var script = new Script(finalConfig);
            return script.DoString(code);
        }


        /// <summary>
        /// Creates a closure from a bytecode address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="envTable">The env table to create a 0-upvalue</param>
        /// <returns></returns>
        private DynValue MakeClosure(int address, Table envTable = null)
        {
            this.CheckScriptOwnership(envTable);
            Closure c;

            if (envTable == null)
            {
                var meta = m_MainProcessor.FindMeta(ref address);

                // if we find the meta for a new chunk, we use the value in the meta for the _ENV upvalue
                c = meta is { NumVal2: (int)OpCodeMetadataType.ChunkEntrypoint }
                    ? new Closure(this, address,
                        new SymbolRef[] { SymbolRef.Upvalue(WellKnownSymbols.ENV, 0) },
                        new DynValue[] { meta.Value })
                    : new Closure(this, address, new SymbolRef[0], new DynValue[0]);
            }
            else
            {
                var syms = new SymbolRef[] {
                    new() { i_Env = null, i_Index= 0, i_Name = WellKnownSymbols.ENV, i_Type =  SymbolRefType.DefaultEnv },
                };

                var vals = new DynValue[] {
                    DynValue.NewTable(envTable)
                };

                c = new Closure(this, address, syms, vals);
            }

            return DynValue.NewClosure(c);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <returns>
        /// The return value(s) of the function call.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue function)
        {
            return Call(function, new DynValue[0]);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns>
        /// The return value(s) of the function call.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue function, params DynValue[] args)
        {
            this.CheckScriptOwnership(function);
            this.CheckScriptOwnership(args);

            if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
            {
                DynValue metafunction = m_MainProcessor.GetMetamethod(function, "__call");

                if (metafunction != null)
                {
                    DynValue[] metaargs = new DynValue[args.Length + 1];
                    metaargs[0] = function;
                    for (int i = 0; i < args.Length; i++)
                        metaargs[i + 1] = args[i];

                    function = metafunction;
                    args = metaargs;
                }
                else
                {
                    throw new ArgumentException("function is not a function and has no __call metamethod.");
                }
            }
            else if (function.Type == DataType.ClrFunction)
            {
                return function.Callback.ClrCallback(CreateDynamicExecutionContext(), new CallbackArguments(args, false));
            }

            return m_MainProcessor.Call(function, args);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns>
        /// The return value(s) of the function call.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue function, params object[] args)
        {
            DynValue[] dargs = new DynValue[args.Length];

            for (int i = 0; i < dargs.Length; i++)
                dargs[i] = DynValue.FromObject(this, args[i]);

            return Call(function, dargs);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(object function)
        {
            return Call(DynValue.FromObject(this, function));
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called </param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(object function, params object[] args)
        {
            return Call(DynValue.FromObject(this, function), args);
        }

        /// <summary>
        /// Creates a coroutine pointing at the specified function.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns>
        /// The coroutine handle.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
        public DynValue CreateCoroutine(DynValue function)
        {
            this.CheckScriptOwnership(function);

            if (function.Type == DataType.Function)
                return m_MainProcessor.Coroutine_Create(function.Function);
            else if (function.Type == DataType.ClrFunction)
                return DynValue.NewCoroutine(new Coroutine(function.Callback));
            else
                throw new ArgumentException("function is not of DataType.Function or DataType.ClrFunction");
        }

        /// <summary>
        /// Creates a new coroutine, recycling buffers from a dead coroutine to skip slower buffer creation in Mono.
        /// </summary>
        /// <param name="coroutine">The <see cref="Coroutine"/> to recycle. This coroutine's state must be <see cref="CoroutineState.Dead"/></param>
        /// <param name="function">The function</param>
        /// <returns>
        /// The new coroutine handle.
        /// </returns>
        public DynValue RecycleCoroutine(Coroutine coroutine, DynValue function)
        {
            this.CheckScriptOwnership(coroutine);
            this.CheckScriptOwnership(function);

            if (coroutine == null || coroutine.Type != Coroutine.CoroutineType.Coroutine)
                throw new InvalidOperationException("coroutine is not CoroutineType.Coroutine");
            if (function == null || function.Type != DataType.Function)
                throw new InvalidOperationException("function is not DataType.Function");
            if (coroutine.State != CoroutineState.Dead)
                throw new InvalidOperationException("coroutine's state must be CoroutineState.Dead to recycle");

            return coroutine.Recycle(m_MainProcessor, function.Function);
        }

        /// <summary>
        /// Creates a coroutine pointing at the specified function.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns>
        /// The coroutine handle.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
        public DynValue CreateCoroutine(object function)
        {
            return CreateCoroutine(DynValue.FromObject(this, function));
        }

        /// <summary>
        /// Gets or sets a value indicating whether the debugger is enabled.
        /// Note that unless a debugger attached, this property returns a 
        /// value which might not reflect the real status of the debugger.
        /// Use this property if you want to disable the debugger for some 
        /// executions.
        /// </summary>
        public bool DebuggerEnabled
        {
            get { return m_MainProcessor.DebuggerEnabled; }
            set { m_MainProcessor.DebuggerEnabled = value; }
        }


        /// <summary>
        /// Attaches a debugger. This usually should be called by the debugger itself and not by user code.
        /// </summary>
        /// <param name="debugger">The debugger object.</param>
        public void AttachDebugger(IDebugger debugger)
        {
            DebuggerEnabled = true;
            m_Debugger = debugger;
            m_MainProcessor.AttachDebugger(debugger);

            foreach (SourceCode src in m_Sources)
                SignalSourceCodeChange(src);

            SignalByteCodeChange();
        }

        /// <summary>
        /// Gets the source code.
        /// </summary>
        /// <param name="sourceCodeID">The source code identifier.</param>
        /// <returns></returns>
        public SourceCode GetSourceCode(int sourceCodeID)
        {
            return m_Sources[sourceCodeID];
        }


        /// <summary>
        /// Gets the source code count.
        /// </summary>
        /// <value>
        /// The source code count.
        /// </value>
        public int SourceCodeCount
        {
            get { return m_Sources.Count; }
        }



        /// <summary>
        /// Loads a module as per the "require" Lua function. http://www.lua.org/pil/8.1.html
        /// </summary>
        /// <param name="modname">The module name</param>
        /// <param name="globalContext">The global context.</param>
        /// <returns></returns>
        /// <exception cref="ScriptRuntimeException">Raised if module is not found</exception>
        public DynValue RequireModule(string modname, Table globalContext = null)
        {
            this.CheckScriptOwnership(globalContext);

            Table globals = globalContext ?? m_GlobalTable;
            string filename = Options.ScriptLoader.ResolveModuleName(modname, globals) ?? throw new ScriptRuntimeException("module '{0}' not found", modname);
            DynValue func = LoadFile(filename, globalContext, filename);
            return func;
        }

        /// <summary>
        /// Gets a type metatable.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public Table GetTypeMetatable(DataType type)
        {
            int t = (int)type;

            if (t >= 0 && t < m_TypeMetatables.Length)
                return m_TypeMetatables[t];

            return null;
        }

        /// <summary>
        /// Sets a type metatable.
        /// </summary>
        /// <param name="type">The type. Must be Nil, Boolean, Number, String, or Function</param>
        /// <param name="metatable">The metatable.</param>
        /// <exception cref="ArgumentException">Specified type not supported :  + type.ToString()</exception>
        public void SetTypeMetatable(DataType type, Table metatable)
        {
            this.CheckScriptOwnership(metatable);

            int t = (int)type;

            m_TypeMetatables[t] = t >= 0 && t < m_TypeMetatables.Length
                ? metatable
                : throw new ArgumentException("Specified type not supported : " + type.ToString());
        }


        /// <summary>
        /// Warms up the parser/lexer structures so that MoonSharp operations start faster.
        /// </summary>
        public static void WarmUp()
        {
            var config = new SecurityConfiguration
            {
                AllowedModules = CoreModules.Basic
            };
            Script s = new(config);
            s.LoadString("return 1;");
        }


        /// <summary>
        /// Creates a new dynamic expression.
        /// </summary>
        /// <param name="code">The code of the expression.</param>
        /// <returns></returns>
        public DynamicExpression CreateDynamicExpression(string code)
        {
            DynamicExprExpression dee = Loader_Fast.LoadDynamicExpr(this, new SourceCode("__dynamic", code, -1, this));
            return new DynamicExpression(this, code, dee);
        }

        /// <summary>
        /// Creates a new dynamic expression which is actually quite static, returning always the same constant value.
        /// </summary>
        /// <param name="code">The code of the not-so-dynamic expression.</param>
        /// <param name="constant">The constant to return.</param>
        /// <returns></returns>
        public DynamicExpression CreateConstantDynamicExpression(string code, DynValue constant)
        {
            this.CheckScriptOwnership(constant);

            return new DynamicExpression(this, code, constant);
        }

        /// <summary>
        /// Gets an execution context exposing only partial functionality, which should be used for
        /// those cases where the execution engine is not really running - for example for dynamic expression
        /// or calls from CLR to CLR callbacks
        /// </summary>
        internal ScriptExecutionContext CreateDynamicExecutionContext()
        {
            return new ScriptExecutionContext(m_MainProcessor, null);
        }

        /// <summary>
        /// MoonSharp (like Lua itself) provides a registry, a predefined table that can be used by any CLR code to 
        /// store whatever Lua values it needs to store. 
        /// Any CLR code can store data into this table, but it should take care to choose keys 
        /// that are different from those used by other libraries, to avoid collisions. 
        /// Typically, you should use as key a string GUID, a string containing your library name, or a 
        /// userdata with the address of a CLR object in your code.
        /// </summary>
        public Table Registry
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a banner string with copyright info, link to website, version, etc.
        /// </summary>
        public static string GetBanner(string subproduct = null)
        {
            subproduct = subproduct != null ? subproduct + " " : "";

            StringBuilder sb = new();
            sb.AppendLine(string.Format("SolarSharp {0}{1} [{2}]", subproduct, VERSION, GlobalOptions.Platform.GetPlatformName()));
            sb.AppendLine("A secure Lua interpreter for .NET");
            sb.AppendLine("Based on MoonSharp - Copyright (C) 2014-2016 Marco Mastropaolo");
            return sb.ToString();
        }

        Script IScriptPrivateResource.OwnerScript => this;

        /// <summary>
        /// Loads a public key into the VM, enforcing manifest requirements for all .lua files
        /// </summary>
        public Script LoadKey(PublicKeyInfo publicKey)
        {
            m_CryptoManager.LoadKey(publicKey);
            return this;
        }

        /// <summary>
        /// Loads a public key from PEM string into the VM
        /// </summary>
        public Script LoadKey(string pemPublicKey)
        {
            m_CryptoManager.LoadKey(pemPublicKey);
            return this;
        }


        /// <summary>
        /// Gets whether this VM has loaded any public keys
        /// </summary>
        public bool HasLoadedKeys => m_CryptoManager.HasLoadedKeys;


        /// <summary>
        /// Validates that a file meets manifest requirements if keys are loaded
        /// </summary>
        private void ValidateManifestRequirement(string luaFilePath)
        {
            m_CryptoManager.ValidateManifestRequirement(luaFilePath);
        }


        /// <summary>
        /// Gets the default manifest from environment variables
        /// </summary>
        private static Manifest GetDefaultManifestFromEnvironment()
        {
            var defaultManifestName = Environment.GetEnvironmentVariable("LUA_SANDBOX_DEFAULT_SYSTEM_MANIFEST");
            
            if (string.IsNullOrEmpty(defaultManifestName))
                return SystemManifest.Desktop; // Default fallback

            // Use Enum.TryParse for consistent string-to-enum conversion
            if (Enum.TryParse<SystemManifestType>(defaultManifestName, true, out var manifestType))
            {
                return manifestType switch
                {
                    SystemManifestType.None => SystemManifest.None,
                    SystemManifestType.Unrestricted => SystemManifest.Unrestricted,
                    SystemManifestType.Desktop => SystemManifest.Desktop,
                    SystemManifestType.Jailed => SystemManifest.Jailed,
                    SystemManifestType.Game => SystemManifest.Game,
                    _ => SystemManifest.Desktop
                };
            }
            
            return SystemManifest.Desktop; // Invalid name defaults to Desktop
        }
        
        /// <summary>
        /// Enum for SystemManifest types to support Enum.TryParse
        /// </summary>
        private enum SystemManifestType
        {
            None,
            Unrestricted,
            Desktop,
            Jailed,
            Game
        }

        /// <summary>
        /// Registers the security tracer if environment variables indicate it should be enabled
        /// </summary>
        private void RegisterSecurityTracerIfEnabled()
        {
            // Check if auto-start is enabled
            var autoStartStr = Environment.GetEnvironmentVariable("LUA_SANDBOX_AUTO_START");
            var autoStart = !string.IsNullOrEmpty(autoStartStr) && 
                           (autoStartStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                            autoStartStr.Equals("1", StringComparison.OrdinalIgnoreCase));

            if (!autoStart)
                return; // Auto-start not enabled

            // Try to get the global tracer (which handles both old and new environment variables)
            var tracer = FileSecurityTracer.GetGlobalTracer();
            if (tracer != null)
            {
                // Add the tracer to our security logger
                m_SecurityLogger.AddHandler(tracer);
            }
        }

        /// <summary>
        /// Checks if external string execution (RunString) is prevented based on the security configuration
        /// </summary>
        private bool IsStringExecutionPrevented()
        {
            var securityConfig = this.SecurityConfiguration();
            if (securityConfig?.AntiPolymorphism == null)
            {
                // No security configuration means no authorization to run
                return true;
            }
            
            return securityConfig.AntiPolymorphism.PreventRunString;
        }

        /// <summary>
        /// Checks if internal dynamic code loading is prevented based on the security configuration
        /// </summary>
        internal bool IsInternalDynamicCodePrevented()
        {
            var securityConfig = this.SecurityConfiguration();
            if (securityConfig?.AntiPolymorphism == null)
            {
                // No security configuration means no authorization to run
                return true;
            }
            
            return securityConfig.AntiPolymorphism.PreventInternalDynamicCode;
        }


    }
}
