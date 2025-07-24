using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using SolarSharp.Interpreter.CoreLib;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Interop;
using SolarSharp.Interpreter.IO;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Capabilities;
using SolarSharp.Interpreter.Security.FunctionBinding;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Security.Operations;
using SolarSharp.Interpreter.Security.ValueTypes;
using SolarSharp.Interpreter.Tree.Fast_Interface;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// This class implements a MoonSharp scripting session. Multiple Script objects can coexist in the same program but cannot share
    /// data among themselves unless some mechanism is put in place.
    /// </summary>
    public class Script
    {
        /// <summary>
        /// The version of the MoonSharp engine
        /// </summary>
        public const string VERSION = "2.0.0.0";

        /// <summary>
        /// The Lua version being supported
        /// </summary>
        public const string LUA_VERSION = "5.2";
        private readonly Processor _mainProcessor;
        private readonly ByteCode _byteCode;
        private readonly List<SourceCode> _sources = new List<SourceCode>();
        private IDebugger _debugger;
        private readonly Table[] _typeMetatables = new Table[(int)LuaTypeExtensions.MaxMetaTypes];
        private readonly List<Manifest> _manifests = new List<Manifest>();
        private readonly Dictionary<Manifest, string> _manifestDirectories = new Dictionary<Manifest, string>();
        private Manifest _compiledManifest;
        private readonly SecurityLogger _securityLogger = new SecurityLogger();
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private SecurityPolicy _activePolicy;
        private ProtectedFiles _protectedFiles = ProtectedFiles.Empty;

        /// <summary>
        /// Internal access to the trust store for testing purposes
        /// </summary>
        internal ITrustStore TrustStore { get; private set; } = ScriptTrustStore.Empty;
        private IManifestValidationService _manifestValidator;
        private ContextualFunctionRegistry _functionRegistry = new ContextualFunctionRegistry();
        
        /// <summary>
        /// Cached script services for functional architecture
        /// </summary>
        private Security.Manifests.Functional.ScriptServices _scriptServices;

        /// <summary>
        /// Gets the platform accessor for this script instance.
        /// </summary>
        public IPlatformAccessor Platform { get; private set; }

        /// <summary>
        /// Gets the script identity if loaded from a manifest
        /// </summary>
        public Maybe<ScriptIdentity> Identity { get; } = Maybe<ScriptIdentity>.None;

        /// <summary>
        /// Initializes the <see cref="Script"/> class.
        /// </summary>
        static Script()
        {
            GlobalOptions = new ScriptGlobalOptions();

            DefaultOptions = new ScriptOptions
            {
                DebugPrint = static s => GlobalOptions.Platform.DefaultPrint(s),
                DebugInput = static s => GlobalOptions.Platform.DefaultInput(s),
                CheckThreadAccess = true,
                ScriptLoader = PlatformAutoDetector.GetDefaultScriptLoader(),
                TailCallOptimizationThreshold = 65536,
            };
        }

        /// <summary>
        /// Gets or sets the base policy set for this script.
        /// Required - scripts cannot run without authorization.
        /// </summary>
        public BasePolicySet BasePolicySet { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with BasePolicySet.
        /// BasePolicySet provides validated, immutable security policies that ensure no invalid Scripts can exist.
        /// </summary>
        /// <param name="basePolicySet">Base policy set to apply - cannot be null and must contain executable policies</param>
        public Script(BasePolicySet basePolicySet)
        {
            BasePolicySet =
                basePolicySet
                ?? throw new ArgumentNullException(
                    nameof(basePolicySet),
                    "BasePolicySet is required - scripts cannot run without authorization"
                );

            // Initialize platform accessor before anything else that might need it
            Platform = GlobalOptions.Platform ?? PlatformAutoDetector.GetDefaultPlatform();

            // Initialize core components
            Options = new ScriptOptions(DefaultOptions);
            PerformanceStats = new PerformanceStatistics();
            Registry = new Table();

            // Initialize security infrastructure first
            var securityMessageBus = new DefaultSecurityMessageBus();
            SetService(securityMessageBus);

            // Load modules based on policy - security enforcement happens at two levels:
            // 1. Module registration - only load modules allowed by policy
            // 2. Function call time - additional enforcement through SecurityBoundFunction attributes
            _byteCode = new ByteCode(this);

            // Get allowed modules from the base policy set
            var moduleResolver = new ModuleCapabilityResolver();
            var allowedModules = moduleResolver.GetMaximalModuleSet(BasePolicySet);

            // Register only the modules allowed by policy
            Globals = new Table().RegisterCoreModules(this, allowedModules);

            _mainProcessor = new Processor(this, Globals, _byteCode);

            // Store the base policy set
            SetService(BasePolicySet);

            // Get default policy for applying security settings
            var defaultPolicy = BasePolicySet
                .GetDefaultPolicy()
                .Match(static policy => policy, static error =>
                        throw new InvalidOperationException(
                            $"Failed to get default policy: {error.Message}"
                        )
                );

            // Convert BasePolicySet to path policies for SecurityPolicyResolver
            var pathPolicies = new Dictionary<string, SecurityPolicy>();
            foreach (var (pattern, policyName) in BasePolicySet.PolicySet.FilePolicies)
            {
                var policyResult = BasePolicySet.PolicySet.GetPolicyByName(policyName);
                if (policyResult.IsSuccess)
                {
                    pathPolicies[pattern] = policyResult.Value;
                }
            }

            // Convert BasePolicySet to signature policies for SecurityPolicyResolver
            var signaturePolicies = new Dictionary<string, SecurityPolicy>();
            foreach (var (publicKeyToken, policyName) in BasePolicySet.PolicySet.SignaturePolicies)
            {
                var policyResult = BasePolicySet.PolicySet.GetPolicyByName(policyName);
                if (policyResult.IsSuccess)
                {
                    signaturePolicies[publicKeyToken] = policyResult.Value;
                }
            }

            // Initialize security policy resolver with BasePolicySet patterns and default policy as fallback
            var policyResolver = new SecurityPolicyResolver(
                signatureDefaultPolicies: signaturePolicies,
                pathDefaultPolicies: pathPolicies,
                fallbackDefaultPolicy: defaultPolicy
            );
            SetService(policyResolver);

            // Initialize security auditor
            var auditor = new SecurityAuditor(); // Use existing SecurityAuditor instead
            SetService<ISecurityAuditor>(auditor);

            // Initialize security checker with policy resolver
            // TODO: Fix policyResolver reference after SecurityPolicyResolver is fixed
            // _securityChecker = new SecurityFunctionChecker(policyResolver, auditor);
            // SetService(_securityChecker);

            // Initialize SecurityFirewall with BouncyCastle cryptographic verification
            // TODO: Fix SecurityFirewall compilation errors before enabling
            // var securityFirewall = new SecurityFirewall(this, policyResolver, auditor);
            // SetService(securityFirewall);

            // Set and apply security policy
            this.SetSecurityPolicy(defaultPolicy);
            ApplySecurityPolicy(defaultPolicy);

            // Auto-register security tracer if environment variables are set
            RegisterSecurityTracerIfEnabled();

            // Initialize manifest validator with default implementations
            var fileSystem = new FileSystem();
            var discoveryService = new DefaultManifestDiscoveryService(fileSystem);
            var signatureValidator = new DefaultSignatureValidator();
            _manifestValidator = new EventDrivenManifestValidator(
                fileSystem,
                discoveryService,
                signatureValidator
            );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Script"/> class with a manifest.
        /// </summary>
        /// <param name="manifest">Manifest to apply</param>
        /// <param name="basePolicySet">Base policy set to use as foundation</param>
        public Script(Manifest manifest, BasePolicySet basePolicySet)
            : this(basePolicySet)
        {
            _manifests.Add(manifest);
            _compiledManifest = manifest;

            // Apply manifest-specific configuration
            // Note: Keep using BasePolicySet's default policy for main execution

            // Update SecurityPolicyResolver to include manifest's FilePolicies
            UpdateSecurityPolicyResolverWithManifest(manifest, basePolicySet);

            // Extract identity from first package in first signed content block if available
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            if (firstPackage != default)
            {
                var (packageId, package, keyId) = firstPackage;
                var name = !string.IsNullOrEmpty(package.Metadata.Name)
                    ? package.Metadata.Name
                    : packageId;
                var version = NuGetVersion.Parse(
                    string.IsNullOrEmpty(package.Metadata.Version)
                        ? "1.0.0"
                        : package.Metadata.Version
                );

                // Use key fingerprint as public key token
                var publicKeyToken =
                    !string.IsNullOrEmpty(keyId) && keyId.StartsWith("sha256:")
                        ? ConvertHexStringToBytes(keyId.Substring(7)).Take(16).ToArray()
                        : new byte[16];

                Identity = Maybe<ScriptIdentity>.From(
                    new ScriptIdentity(name, version, publicKeyToken)
                );
            }
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
            return _securityLogger;
        }

        /// <summary>
        /// Gets a service from the script's service container
        /// </summary>
        /// <typeparam name="T">The service type</typeparam>
        /// <returns>The service instance or null if not found</returns>
        public T GetService<T>()
            where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return service as T;
            }
            return null;
        }

        /// <summary>
        /// Sets a service in the script's service container
        /// </summary>
        /// <typeparam name="T">The service type</typeparam>
        /// <param name="service">The service instance</param>
        public void SetService<T>(T service)
            where T : class
        {
            _services[typeof(T)] = service;
        }

        /// <summary>
        /// Gets the default global table for this script. Unless a different table is intentionally passed (or setfenv has been used)
        /// execution uses this table.
        /// </summary>
        public Table Globals { get; }

        /// <summary>
        /// Converts a hex string to byte array (helper for older .NET versions)
        /// </summary>
        private static byte[] ConvertHexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();

            // Validate that the string contains only valid hex characters
            if (!IsValidHexString(hex))
            {
                // For non-hex strings (like test values), generate a deterministic hash
                return System.Text.Encoding.UTF8.GetBytes(hex).Take(16).ToArray();
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Validates if a string contains only valid hexadecimal characters
        /// </summary>
        private static bool IsValidHexString(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return false;

            return hex.All(static c => Uri.IsHexDigit(c));
        }

        /// <summary>
        /// Checks if a V2.0 manifest has any signed content blocks
        /// </summary>
        private bool IsManifestSigned(Manifest manifest)
        {
            return manifest.SignedContent.Any(static block => !string.IsNullOrEmpty(block.Signature));
        }

        /// <summary>
        /// Parses a timeout string from manifest format (e.g., "5s", "1000ms", "2m")
        /// </summary>
        private static int ParseTimeoutString(string timeout)
        {
            var normalized = timeout.ToLowerInvariant().Trim();
            
            // Handle TimeSpan format (00:00:00.00)
            if (normalized.Contains(':'))
            {
                if (TimeSpan.TryParse(normalized, out var timespan))
                {
                    return (int)timespan.TotalMilliseconds;
                }
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Expected format: HH:MM:SS or value with suffix (ms, s, m)", "ParseTimeout");
            }
            
            // Handle suffix formats
            if (normalized.EndsWith("ms"))
            {
                if (int.TryParse(normalized.Replace("ms", ""), out var ms))
                    return ms;
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Could not parse milliseconds value", "ParseTimeout");
            }
            else if (normalized.EndsWith("s"))
            {
                if (int.TryParse(normalized.Replace("s", ""), out var seconds))
                    return seconds * 1000;
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Could not parse seconds value", "ParseTimeout");
            }
            else if (normalized.EndsWith("m"))
            {
                if (int.TryParse(normalized.Replace("m", ""), out var minutes))
                    return minutes * 60 * 1000;
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Could not parse minutes value", "ParseTimeout");
            }
            
            throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Expected suffix: ms, s, or m", "ParseTimeout");
        }

        /// <summary>
        /// Parses a memory string from manifest format (e.g., "1MB", "512KB", "2GB")
        /// </summary>
        private static int ParseMemoryString(string memory)
        {
            var normalized = memory.ToUpperInvariant().Trim();
            
            if (normalized.EndsWith("KB"))
            {
                if (int.TryParse(normalized.Replace("KB", ""), out var kb))
                    return kb / 1024; // Convert to MB
                throw new ManifestFormatException($"Invalid memory format: '{memory}'. Could not parse KB value", "ParseMemory");
            }
            else if (normalized.EndsWith("MB"))
            {
                if (int.TryParse(normalized.Replace("MB", ""), out var mb))
                    return mb;
                throw new ManifestFormatException($"Invalid memory format: '{memory}'. Could not parse MB value", "ParseMemory");
            }
            else if (normalized.EndsWith("GB"))
            {
                if (int.TryParse(normalized.Replace("GB", ""), out var gb))
                    return gb * 1024;
                throw new ManifestFormatException($"Invalid memory format: '{memory}'. Could not parse GB value", "ParseMemory");
            }
            
            throw new ManifestFormatException($"Invalid memory format: '{memory}'. Expected suffix: KB, MB, or GB", "ParseMemory");
        }

        /// <summary>
        /// Derives an aggregate SecurityPolicy from all policies in a V2.0 manifest
        /// </summary>
        private static SecurityPolicy DeriveAggregatePolicyFromManifest(Manifest manifest, string manifestDirectory)
        {
            // For empty manifests, return a restrictive default
            if (!manifest.SignedContent.Any())
            {
                return Examples.Isolated();
            }

            // Convert manifest to effective policy using ManifestPolicyConverter
            var result = ManifestPolicyConverter.ConvertToSecurityPolicy(manifest, manifestDirectory);
            
            if (result.IsFailure)
            {
                // If conversion fails, return restrictive default and log the error
                Console.Error.WriteLine($"Failed to convert manifest policy: {result.Error}");
                return Examples.Isolated();
            }

            return result.Value;
        }

        /// <summary>
        /// Updates the SecurityPolicyResolver to include manifest's FilePolicies
        /// </summary>
        private void UpdateSecurityPolicyResolverWithManifest(
            Manifest manifest,
            BasePolicySet basePolicySet
        )
        {
            // Get the default policy from basePolicySet as fallback
            var defaultPolicy = basePolicySet
                .GetDefaultPolicy()
                .Match(
                    policy => policy,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to get default policy: {error.Message}"
                        )
                );

            // Start with BasePolicySet path policies
            var pathPolicies = new Dictionary<string, SecurityPolicy>();
            foreach (var (pattern, policyName) in basePolicySet.PolicySet.FilePolicies)
            {
                var policyResult = basePolicySet.PolicySet.GetPolicyByName(policyName);
                if (policyResult.IsSuccess)
                {
                    pathPolicies[pattern] = policyResult.Value;
                }
            }

            // Extract file policies from V2.0 manifest's signed content blocks
            foreach (var block in manifest.SignedContent)
            {
                foreach (var policy in block.Policies)
                {
                    // TODO: Update to use ManifestPolicyConverter.ConvertToSecurityPolicy
                    // This whole section needs to be rewritten for V2.0 manifest structure
                    // var securityPolicy = ConvertManifestPolicyToSecurityPolicy(policy);
                    // Need to apply policies to files...
                }
            }

            // Create new SecurityPolicyResolver with merged policies
            var policyResolver = new SecurityPolicyResolver(
                signatureDefaultPolicies: null,
                pathDefaultPolicies: pathPolicies,
                fallbackDefaultPolicy: defaultPolicy
            );

            SetService(policyResolver);
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
        public DynValue LoadFunction(string code) =>
            LoadFunction(code, Maybe<Table>.None, Maybe<string>.None);

        public DynValue LoadFunction(string code, Table globalTable) =>
            LoadFunction(
                code,
                globalTable != null ? Maybe<Table>.From(globalTable) : Maybe<Table>.None,
                Maybe<string>.None
            );

        public DynValue LoadFunction(string code, Table globalTable, string funcFriendlyName) =>
            LoadFunction(
                code,
                globalTable != null ? Maybe<Table>.From(globalTable) : Maybe<Table>.None,
                funcFriendlyName != null ? Maybe<string>.From(funcFriendlyName) : Maybe<string>.None
            );

        private DynValue LoadFunction(
            string code,
            Maybe<Table> globalTable,
            Maybe<string> funcFriendlyName
        )
        {
            // Script ownership check removed

            var chunkName =
                $"libfunc_{funcFriendlyName.GetValueOrDefault(_sources.Count.ToString())}";

            var source = new SourceCode(chunkName, code, _sources.Count, this);

            _sources.Add(source);

            var address = Loader_Fast.LoadFunction(
                this,
                source,
                _byteCode,
                globalTable.HasValue || Globals != null
            );

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            return MakeClosure(address, globalTable.GetValueOrDefault(Globals));
        }

        private void SignalByteCodeChange()
        {
            _debugger?.SetByteCode(_byteCode.Code.Select(s => s.ToString()).ToArray());
        }

        private void SignalSourceCodeChange(SourceCode source)
        {
            _debugger?.SetSourceCode(source);
        }

        /// <summary>
        /// Temporarily applies a security policy for the duration of an operation.
        /// Used for eval contexts to apply eval-specific resource limits.
        /// </summary>
        /// <param name="temporaryPolicy">The policy to apply temporarily</param>
        /// <param name="operation">The operation to execute with the temporary policy</param>
        /// <returns>The result of the operation</returns>
        internal T WithTemporaryPolicy<T>(SecurityPolicy temporaryPolicy, Func<T> operation)
        {
            if (temporaryPolicy == null)
                throw new ArgumentNullException(nameof(temporaryPolicy));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            // Store the current policy to restore later
            var originalPolicy = _activePolicy;

            try
            {
                // Temporarily apply the new policy
                ApplySecurityPolicy(temporaryPolicy);

                // Execute the operation with the temporary policy
                return operation();
            }
            finally
            {
                // Always restore the original policy
                if (originalPolicy != null)
                {
                    ApplySecurityPolicy(originalPolicy);
                }
            }
        }

        /// <summary>
        /// Applies security policy to this script instance
        /// </summary>
        /// <summary>
        /// Get or create script services for functional architecture.
        /// Lazy initialization to avoid overhead for scripts that don't use manifests.
        /// </summary>
        private Security.Manifests.Functional.ScriptServices GetOrCreateScriptServices()
        {
            if (_scriptServices == null)
            {
                _scriptServices = Security.Manifests.Functional.ScriptServices
                    .Create(BasePolicySet)
                    .WithTrustStore(TrustStore);
            }
            return _scriptServices;
        }

        /// <summary>
        /// Apply security policy to the script instance.
        /// Made internal for functional architecture access.
        /// </summary>
        internal void ApplySecurityPolicy(SecurityPolicy policy)
        {
            // Capture the previous policy before updating
            var previousPolicy = _activePolicy;
            
            // Store security policy as the primary source of truth
            _activePolicy = policy;

            // Initialize security event handler first
            var eventHandler = new SecurityEventHandler();
            SetService(eventHandler);

            // Register the event handler with the security logger
            _securityLogger.AddHandler(eventHandler);

            // Configure platform accessor with security restrictions using shared logger
            Platform = new SecurePlatformAccessor(policy, _securityLogger);

            // Configure interop security based on policy
            // Note: We always use LazyOptimized for built-in types to ensure proper functionality
            // Security is enforced at the capability level, not by hiding members
            UserData.DefaultAccessMode = InteropAccessMode.LazyOptimized;

            // Initialize resource controller for execution limits
            // NEW SEMANTICS: -1 = unlimited (→ null), 0 = deny, >0 = actual limit
            var limits = new ExecutionLimits
            {
                TimeoutMs = policy.TimeoutMs == -1 ? null : policy.TimeoutMs,
                MaxMemoryMB = policy.MaxMemoryMB == -1 ? null : policy.MaxMemoryMB,
                MaxInstructions = policy.MaxInstructions == -1 ? null : policy.MaxInstructions,
                MaxCallDepth = policy.MaxCallDepth == -1 ? null : policy.MaxCallDepth,
                MaxTables = policy.MaxTables == -1 ? null : policy.MaxTables,
                ResourceLimitScope = policy.ResourceLimitScope,
                MaxStringLength = SecurityConstants.DefaultMaxStringLength >= 0 ? SecurityConstants.DefaultMaxStringLength : null,
                MaxCoroutineResumes = SecurityConstants.DefaultMaxCoroutineResumes >= 0 ? SecurityConstants.DefaultMaxCoroutineResumes : null,

                // Enable test mode for deterministic behavior in tests
                TestMode = Environment.GetEnvironmentVariable("SOLARSHARP_TEST_MODE") == "true",
                ForceGCOnMemoryCheck =
                    Environment.GetEnvironmentVariable("SOLARSHARP_FORCE_GC") == "true",
                CheckMemoryEveryNInstructions = int.TryParse(
                    Environment.GetEnvironmentVariable("SOLARSHARP_MEMORY_CHECK_INTERVAL"),
                    out var interval
                )
                    ? interval
                    : 1000,
                UseStableMemoryMeasurement =
                    Environment.GetEnvironmentVariable("SOLARSHARP_STABLE_MEMORY") == "true",
            };

            // Check if we should preserve the existing ResourceController for cumulative tracking
            var existingController = this.ResourceController();
            var shouldPreserveController = existingController != null && 
                                          policy.ResourceLimitScope == ResourceLimitScope.Cumulative &&
                                          previousPolicy != null &&
                                          previousPolicy.ResourceLimitScope == ResourceLimitScope.Cumulative;
            
            if (shouldPreserveController)
            {
                // Keep existing controller to preserve cumulative counters
                // The existing controller will continue to track cumulative usage
            }
            else
            {
                // Create new ResourceController
                var resourceController = new ResourceController(limits);
                this.SetResourceController(resourceController);
                
            }

            // Subscribe to resource limit events
            var activeController = this.ResourceController();
            if (activeController != null)
            {
                activeController.ResourceLimitExceeded += (sender, args) =>
                {
                    var ev = new SecurityEvent
                    {
                        Type = SecurityEventType.ResourceLimitExceeded,
                        Operation = $"ResourceLimit_{args.ResourceType}",
                        Arguments = new object[] { args.CurrentValue, args.Limit },
                        Details =
                            $"{args.ResourceType} limit exceeded: {args.CurrentValue} > {args.Limit}",
                        TerminateExecution = true,
                        ViolationHandling = SecurityViolationHandling.ThrowError,
                    };
                    _securityLogger.LogSecurityEvent(ev);
                };
            }

            // Initialize VFS if chroot is enabled
            if (policy.EnableChroot)
            {
                // VFS will be created when needed by SecurePlatformAccessor
                // The policy contains the necessary information
            }
        }

        /// <summary>
        /// Recompiles all manifests into a single manifest for fast runtime checks
        /// </summary>
        private void RecompileManifest()
        {
            if (!_manifests.Any())
            {
                // Create a minimal V2.0 manifest for compatibility
                _compiledManifest = new Manifest
                {
                    Version = "2.0",
                    ManifestId = Guid.NewGuid().ToString(),
                    SignedContent = ImmutableArray<SignedContentBlock>.Empty,
                };
                return;
            }

            // For now, if we have only one manifest, use it directly
            if (_manifests.Count == 1)
            {
                _compiledManifest = _manifests[0];
                return;
            }

            // TODO: V2.0 manifest composition needs to be implemented
            // For now, use the first manifest as the primary
            _compiledManifest = _manifests[0];
        }

        /// <summary>
        /// Adds a manifest to the script at runtime
        /// </summary>
        /// <param name="manifest">The manifest to add</param>
        public void AddManifest(Manifest manifest)
        {
            AddManifest(manifest, ".", skipValidation: false);
        }

        /// <summary>
        /// Adds a manifest to the script at runtime
        /// </summary>
        /// <param name="manifest">The manifest to add</param>
        /// <param name="manifestDirectory">The directory containing the manifest</param>
        /// <param name="skipValidation">Whether to skip signature validation (used when already validated)</param>
        internal void AddManifest(Manifest manifest, string manifestDirectory, bool skipValidation)
        {
            // Manifest is a record type, cannot be null in functional design

            // Get the default policy from BasePolicySet for comparison
            var defaultPolicyResult = BasePolicySet.GetDefaultPolicy();
            if (defaultPolicyResult.IsSuccess)
            {
                var defaultPolicy = defaultPolicyResult.Value;

                // Check if untrusted manifest is trying to increase timeout
                // For V2.0 manifests, derive aggregate policy from all signed content blocks
                var aggregatePolicy = DeriveAggregatePolicyFromManifest(manifest, manifestDirectory);
                
                if (
                    !IsManifestSigned(manifest)
                    && aggregatePolicy.TimeoutMs > defaultPolicy.TimeoutMs
                )
                {
                    throw new ManifestFormatException(
                        $"Untrusted manifest cannot increase timeout from {defaultPolicy.TimeoutMs}ms to {aggregatePolicy.TimeoutMs}ms",
                        "AddManifest"
                    );
                }
            }

            // Validate manifest signatures if present using the script's trust store
            if (!skipValidation && IsManifestSigned(manifest))
            {
                // Use the EventDrivenManifestValidator to verify signature with script's trust store
                var scriptId = Guid.NewGuid().ToString();
                var validationResult = _manifestValidator.ValidateSignature(
                    manifest,
                    TrustStore,
                    scriptId
                );
                if (validationResult.IsFailure)
                {
                    throw new ManifestSignatureException(
                        $"Manifest signature validation failed: {validationResult.Error}",
                        "AddManifest"
                    );
                }
            }

            // Add to manifest list with directory
            _manifests.Add(manifest);
            _manifestDirectories[manifest] = manifestDirectory;

            // Recompile manifests
            RecompileManifest();

            // Update security configuration - derive aggregate policy from compiled manifest
            var newPolicy =
                _compiledManifest != null
                    ? DeriveAggregatePolicyFromManifest(_compiledManifest, manifestDirectory)
                    : Examples.Isolated();
            ApplySecurityPolicy(newPolicy);
        }

        /// <summary>
        /// Loads a string containing a Lua/MoonSharp script.
        /// When called from C# host application, this is ALWAYS ALLOWED.
        /// Security restrictions only apply to dynamic execution from within Lua.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="chunkname">Name of the chunk - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadString(string code) =>
            LoadString(code, Maybe<Table>.None, Maybe<string>.None);

        public DynValue LoadString(string code, Table globalTable) =>
            LoadString(
                code,
                globalTable != null ? Maybe<Table>.From(globalTable) : Maybe<Table>.None,
                Maybe<string>.None
            );

        public DynValue LoadString(string code, Table globalTable, string chunkname) =>
            LoadString(
                code,
                globalTable != null ? Maybe<Table>.From(globalTable) : Maybe<Table>.None,
                chunkname != null ? Maybe<string>.From(chunkname) : Maybe<string>.None
            );

        private DynValue LoadString(
            string code,
            Maybe<Table> globalTable,
            Maybe<string> chunkname
        )
        {
            if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
            {
                code = code[StringModule.BASE64_DUMP_HEADER.Length..];
                var data = Convert.FromBase64String(code);
                using var ms = new MemoryStream(data);
                return LoadStream(
                    ms,
                    globalTable.GetValueOrDefault(),
                    chunkname.GetValueOrDefault()
                );
            }

            var chunkName = $"{chunkname.GetValueOrDefault() ?? "chunk_" + _sources.Count}";

            var source = new SourceCode(
                chunkname.GetValueOrDefault() ?? chunkName,
                code,
                _sources.Count,
                this
            );

            _sources.Add(source);

            var address = Loader_Fast.LoadChunk(this, source, _byteCode);

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            return MakeClosure(address, globalTable.GetValueOrDefault() ?? Globals);
        }

        /// <summary>
        /// Loads a Lua/MoonSharp script from a System.IO.Stream. NOTE: This will *NOT* close the stream!
        /// </summary>
        /// <param name="stream">The stream containing code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="chunkname">Name of the chunk - used to report errors, etc.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadStream(
            Stream stream,
            Table globalTable = null,
            string chunkname = null
        )
        {
            // Script ownership check removed

            Stream codeStream = new UndisposableStream(stream);

            if (!Processor.IsDumpStream(codeStream))
            {
                using var sr = new StreamReader(codeStream);
                var scriptCode = sr.ReadToEnd();
                return LoadStringInternal(scriptCode, globalTable, chunkname);
            }
            var chunkName = $"{chunkname ?? "dump_" + _sources.Count}";

            var source = new SourceCode(
                chunkname ?? chunkName,
                $"-- This script was decoded from a binary dump - dump_{_sources.Count}",
                _sources.Count,
                this
            );

            _sources.Add(source);

            var address = _mainProcessor.Undump(
                codeStream,
                _sources.Count - 1,
                globalTable ?? Globals,
                out var hasUpvalues
            );

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            if (hasUpvalues)
                return MakeClosure(address, globalTable ?? Globals);
            return MakeClosure(address);
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
            // Script ownership check removed

            if (function.Type != DataType.Function)
                throw new ArgumentException("function arg is not a function!");

            if (!stream.CanWrite)
                throw new ArgumentException("stream is readonly!");

            var upvaluesType = function.Function.GetUpvaluesType();

            if (upvaluesType == Closure.UpvaluesType.Closure)
                throw new ArgumentException("function arg has upvalues other than _ENV");

            var outStream = new UndisposableStream(stream);
            _mainProcessor.Dump(
                outStream,
                function.Function.EntryPointByteCodeLocation,
                upvaluesType == Closure.UpvaluesType.Environment
            );
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
        public DynValue LoadFile(
            string filename,
            Table globalContext = null,
            string friendlyFilename = null
        ) => LoadFileInternal(filename, globalContext, friendlyFilename, skipPolicyResolution: false);

        internal DynValue LoadFileInternal(
            string filename,
            Table globalContext = null,
            string friendlyFilename = null,
            bool skipPolicyResolution = false
        )
        {
            // Script ownership check removed

#pragma warning disable 618
            filename = Options.ScriptLoader.ResolveFileName(filename, globalContext ?? Globals);
#pragma warning restore 618

            // Use new event-driven manifest validation
            var scriptId = Guid.NewGuid().ToString();
            var manifestValidationResult = _manifestValidator.ValidateManifest(
                filename,
                TrustStore,
                scriptId
            );

            // Check if we should use SecurityPolicyResolver when no manifest is found
            var shouldUsePolicyResolver = false;

            manifestValidationResult.Match(
                loadedManifest =>
                {
                    // Process certificate chain and add valid intermediate CAs
                    ProcessManifestCertificateChain(loadedManifest.Manifest);

                    // Extract and add protected files from the manifest
                    ProcessManifestProtectedFiles(
                        loadedManifest.Manifest,
                        loadedManifest.ManifestPath,
                        fromTrusted: true
                    );

                    // Add the manifest to this Script instance (skip validation - already done)
                    var manifestDir = Path.GetDirectoryName(loadedManifest.ManifestPath) ?? ".";
                    AddManifest(loadedManifest.Manifest, manifestDir, skipValidation: true);
                },
                error =>
                {
                    // Handle manifest validation errors by throwing appropriate exceptions
                    switch (error.Type)
                    {
                        case ManifestValidationErrorType.UntrustedKey:
                            throw new ManifestSignatureException(error.Message, "LoadFile");
                        case ManifestValidationErrorType.InvalidSignature:
                            throw new ManifestSignatureException(error.Message, "LoadFile");
                        case ManifestValidationErrorType.InvalidFormat:
                            throw new ManifestFormatException(error.Message, "LoadFile");
                        case ManifestValidationErrorType.NotFound:
                            // No manifest found - this is okay unless keys are loaded
                            if (!TrustStore.IsEmpty)
                                throw new ManifestSignatureException(
                                    $"Manifest required for {filename} but none found",
                                    "LoadFile"
                                );

                            // Flag that we should use policy resolver since no individual manifest found
                            shouldUsePolicyResolver = true;
                            break;
                        default:
                            throw new ManifestSignatureException(error.Message, "LoadFile");
                    }
                }
            );

            // Legacy validation for backward compatibility
            ValidateManifestRequirement(filename);

            // Validate protected file integrity if the file is covered by a manifest
            ValidateProtectedFileIntegrity(filename);

            // Apply security policy using SecurityPolicyResolver if available
            var resolver = GetService<SecurityPolicyResolver>();
            if (!skipPolicyResolution && resolver != null && (shouldUsePolicyResolver || manifestValidationResult.IsSuccess))
            {
                return LoadFileWithPolicyResolution(
                    filename,
                    globalContext,
                    friendlyFilename,
                    resolver
                );
            }

            var code = Options.ScriptLoader.LoadFile(filename, globalContext ?? Globals);
            switch (code)
            {
                case string v:
                    return LoadStringInternal(v, globalContext, friendlyFilename ?? filename);
                case byte[] bytes:
                    using (var ms = new MemoryStream(bytes))
                        return LoadStream(ms, globalContext, friendlyFilename ?? filename);
                case Stream stream:
                    using (stream)
                        return LoadStream(stream, globalContext, friendlyFilename ?? filename);
                case null:
                    throw new InvalidCastException("Unexpected null from IScriptLoader.LoadFile");
                default:
                    throw new InvalidCastException(
                        $"Unsupported return type from IScriptLoader.LoadFile : {code.GetType()}"
                    );
            }
        }

        /// <summary>
        /// Loads a file with policy resolution using SecurityPolicyResolver
        /// </summary>
        private DynValue LoadFileWithPolicyResolution(
            string filename,
            Table globalContext,
            string friendlyFilename,
            SecurityPolicyResolver resolver
        )
        {
            // Create execution context for the file
            var contextResult = LuaExecutionContext.CreateFromPath(
                filename,
                Maybe<LuaExecutionContext>.None
            );
            if (contextResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Failed to create execution context: {contextResult.Error}"
                );
            }

            var executionContext = contextResult.Value;

            // If this script has manifests, use the first one to provide manifest context
            if (_manifests.Count > 0)
            {
                var manifestContext = LuaExecutionContext.CreateWithManifest(
                    filename,
                    _manifests[0],
                    executionContext.Identity.GetValueOrDefault(
                        new ScriptIdentity("Unknown", NuGetVersion.Parse("1.0.0"), new byte[16])
                    ),
                    Maybe<LuaExecutionContext>.None
                );

                if (manifestContext.IsSuccess)
                {
                    executionContext = manifestContext.Value;
                }
            }

            // Resolve policy for this file
            var policyResult = resolver.ResolvePolicy(executionContext);
            if (policyResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve policy for {filename}: {policyResult.Error.Message}"
                );
            }

            var policy = policyResult.Value;

            // Apply the resolved policy temporarily
            var originalPolicy = _activePolicy;
            try
            {
                ApplySecurityPolicy(policy);

                // Validate protected file integrity if the file is covered by a manifest
                ValidateProtectedFileIntegrity(filename);

                // Load and execute the file with the resolved policy
                var code = Options.ScriptLoader.LoadFile(filename, globalContext ?? Globals);
                switch (code)
                {
                    case string v:
                        return LoadStringInternal(v, globalContext, friendlyFilename ?? filename);
                    case byte[] bytes:
                        using (var ms = new MemoryStream(bytes))
                            return LoadStream(ms, globalContext, friendlyFilename ?? filename);
                    case Stream stream:
                        using (stream)
                            return LoadStream(stream, globalContext, friendlyFilename ?? filename);
                    case null:
                        throw new InvalidCastException(
                            "Unexpected null from IScriptLoader.LoadFile"
                        );
                    default:
                        throw new InvalidCastException(
                            $"Unsupported return type from IScriptLoader.LoadFile : {code.GetType()}"
                        );
                }
            }
            finally
            {
                // Restore original policy
                ApplySecurityPolicy(originalPolicy);
            }
        }

        /// <summary>
        /// Loads and executes a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="chunkname">Name of the chunk - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoString(
            string code,
            Table globalContext = null,
            string chunkname = null
        )
        {
            // DoString always executes in :eval context for proper security scoping
            // Create execution context for eval
            var contextResult = LuaExecutionContext.CreateFromPath(":eval");
            if (contextResult.IsFailure)
            {
                throw new ScriptRuntimeException(
                    $"Failed to create eval context: {contextResult.Error}"
                );
            }

            var executionContext = contextResult.Value;

            // Get the policy resolver from services
            var resolver = this.GetService<SecurityPolicyResolver>();
            if (resolver == null)
            {
                throw new InvalidOperationException("SecurityPolicyResolver not found in services");
            }
            
            // Resolve policy for :eval context
            // This will check patterns like *, *:eval, :eval and combine matching policies
            var policyResult = resolver.ResolvePolicy(executionContext);
            if (policyResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve policy for :eval: {policyResult.Error.Message}"
                );
            }

            var policy = policyResult.Value;

            // Apply the resolved policy temporarily
            var originalPolicy = _activePolicy;
            try
            {
                ApplySecurityPolicy(policy);

                // Execute within the eval context with proper policy
                var executionResult = ExecutionContextManager.WithContext(
                    executionContext,
                    _ =>
                    {
                        // Mark this as host-initiated execution
                        this.SetHostInitiatedExecution(true);
                        try
                        {
                            var func = LoadStringInternal(code, globalContext, chunkname ?? ":eval");
                            var result = Call(func);
                            return Result.Success<DynValue, ExecutionError>(result);
                        }
                        catch (Exception ex)
                        {
                            // Convert any exception to the appropriate script error type
                            // This maintains consistent error handling across the API
                            throw ex switch
                            {
                                SyntaxErrorException => ex,
                                ResourceLimitExceededException => ex,
                                MemoryExhaustionException => ex,
                                ExecutionTimeoutException => ex,
                                CallDepthExceededException => ex,
                                InstructionLimitExceededException => ex,
                                ScriptRuntimeException => ex,
                                SecurityException => ex,
                                _ => new ScriptRuntimeException($"Script execution failed: {ex.Message}", ex),
                            };
                        }
                        finally
                        {
                            // Reset the host-initiated flag after execution completes
                            this.SetHostInitiatedExecution(false);
                        }
                    }
                );

                return executionResult.Match(
                    success => success,
                    error =>
                        throw new ScriptRuntimeException($"Script execution failed: {error.Message}")
                );
            }
            finally
            {
                // Restore original policy
                ApplySecurityPolicy(originalPolicy);
            }
        }

        /// <summary>
        /// Converts functional script error to appropriate exception for public API
        /// </summary>
        private Exception CreateExceptionFromError(ScriptError error)
        {
            return error.Type switch
            {
                Execution.ScriptErrorType.Compilation => new SyntaxErrorException(
                    null,
                    error.Message
                ),
                Execution.ScriptErrorType.Security => new ResourceLimitExceededException(
                    error.Message,
                    "SecurityViolation"
                ),
                Execution.ScriptErrorType.MemoryLimit => new MemoryExhaustionException(
                    error.Message,
                    "MemoryLimit"
                ),
                Execution.ScriptErrorType.Timeout => new ExecutionTimeoutException(
                    error.Message,
                    "Timeout"
                ),
                Execution.ScriptErrorType.Runtime => new ScriptRuntimeException(
                    error.Message,
                    error.InnerException
                ),
                Execution.ScriptErrorType.Configuration => new ArgumentException(
                    error.Message,
                    error.InnerException
                ),
                _ => new ScriptRuntimeException(error.Message, error.InnerException),
            };
        }

        /// <summary>
        /// Internal version of DoString that bypasses StringExecution control (for VM internal use)
        /// </summary>
        internal DynValue DoStringInternal(
            string code,
            Table globalContext = null,
            string chunkname = null
        )
        {
            var func = LoadStringInternal(code, globalContext, chunkname);
            return Call(func);
        }

        /// <summary>
        /// Internal version of LoadString that bypasses StringExecution control (for VM internal use)
        /// </summary>
        internal DynValue LoadStringInternal(
            string code,
            Table globalTable = null,
            string chunkname = null
        )
        {
            // Script ownership check removed

            if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
            {
                code = code[StringModule.BASE64_DUMP_HEADER.Length..];
                var data = Convert.FromBase64String(code);
                using var ms = new MemoryStream(data);
                return LoadStream(ms, globalTable, chunkname);
            }

            var chunkName = $"{chunkname ?? "chunk_" + _sources.Count}";

            var source = new SourceCode(chunkname ?? chunkName, code, _sources.Count, this);

            _sources.Add(source);

            var address = Loader_Fast.LoadChunk(this, source, _byteCode);

            SignalSourceCodeChange(source);
            SignalByteCodeChange();

            return MakeClosure(address, globalTable ?? Globals);
        }

        /// <summary>
        /// Loads and executes a stream containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="chunkname">Name of the chunk - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing the loaded chunk.
        /// </returns>
        public DynValue DoStream(
            Stream stream,
            Table globalContext = null,
            string chunkname = null
        )
        {
            var func = LoadStream(stream, globalContext, chunkname);
            return Call(func);
        }

        /// <summary>
        /// Loads and executes a file containing a Lua/MoonSharp script.
        /// When called from C# host application, this is ALWAYS ALLOWED.
        /// The file's security policy is determined by its path and manifest.
        /// Uses functional manifest architecture for better performance and maintainability.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the chunk - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoFile(
            string filename,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            // Use new functional manifest architecture
            var services = GetOrCreateScriptServices();
            
            var result = Security.Manifests.Functional.ExecutionPipeline.ExecuteFile(
                filename,
                globalContext,
                services,
                this);

            return result.Match(
                success => success,
                error => error.OriginalException != null 
                    ? throw error.OriginalException 
                    : throw new ScriptRuntimeException($"Script execution failed: {error.Message}")
            );
        }

        /// <summary>
        /// Legacy DoFile method for backward compatibility during transition.
        /// Will be removed once all tests pass with the new architecture.
        /// </summary>
        internal DynValue DoFileLegacy(
            string filename,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            if (_manifestValidator == null)
                throw new InvalidOperationException("Manifest validator is null!");

            // Create execution context for the file
            var contextResult = LuaExecutionContext.CreateFromPath(filename);
            if (contextResult.IsFailure)
            {
                throw new ScriptRuntimeException(
                    $"Failed to create execution context: {contextResult.Error}"
                );
            }

            // Execute within the file's context
            var executionResult = ExecutionContextManager.WithContext(
                contextResult.Value,
                context =>
                {
                    // Resolve and apply security policy for the entire execution
                    var resolver = GetService<SecurityPolicyResolver>();
                    SecurityPolicy originalPolicy = null;
                    
                    if (resolver != null)
                    {
                        var policyResult = resolver.ResolvePolicy(context);
                        if (policyResult.IsSuccess)
                        {
                            originalPolicy = _activePolicy;
                            ApplySecurityPolicy(policyResult.Value);
                        }
                    }
                    
                    try
                    {
                        var func = LoadFileInternal(filename, globalContext, codeFriendlyName, skipPolicyResolution: true);
                        var result = Call(func);
                        return Result.Success<DynValue, ExecutionError>(result);
                    }
                    finally
                    {
                        // Restore original policy if we changed it
                        if (originalPolicy != null)
                        {
                            ApplySecurityPolicy(originalPolicy);
                        }
                    }
                }
            );

            return executionResult.Match(
                success => success,
                error =>
                    throw new ScriptRuntimeException($"Script execution failed: {error.Message}")
            );
        }

        /// <summary>
        /// Runs a Lua file with specified security configuration and manifest discovery.
        /// Manifest overrides take precedence over base configuration.
        /// </summary>
        /// <param name="filename">The filename to execute</param>
        /// <param name="basePolicySet">Base policy set configuration</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunFile(string filename, BasePolicySet basePolicySet)
        {
            return RunFile(filename, basePolicySet, null);
        }

        /// <summary>
        /// Runs a Lua file with full configuration control.
        /// </summary>
        /// <param name="filename">The filename to execute</param>
        /// <param name="basePolicySet">Base policy set configuration</param>
        /// <param name="policyModifier">Optional policy modifier function</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunFile(
            string filename,
            BasePolicySet basePolicySet,
            Func<SecurityPolicy, SecurityPolicy> policyModifier = null
        )
        {
            // Apply policy modifications if provided
            var finalBasePolicySet = basePolicySet;
            if (policyModifier != null)
            {
                var defaultPolicyResult = basePolicySet.GetDefaultPolicy();
                if (defaultPolicyResult.IsFailure)
                {
                    throw new InternalErrorException(
                        $"Failed to get default policy: {defaultPolicyResult.Error.Message}"
                    );
                }

                var modifiedPolicy = policyModifier(defaultPolicyResult.Value);
                var updateResult = basePolicySet.WithDefaultPolicy(modifiedPolicy);
                if (updateResult.IsFailure)
                {
                    throw new InternalErrorException(
                        $"Failed to update default policy: {updateResult.Error.Message}"
                    );
                }

                finalBasePolicySet = updateResult.Value;
            }

            // Create script with the final policy set
            var script = new Script(finalBasePolicySet);
            return script.DoFile(filename);
        }

        /// <summary>
        /// Runs Lua code from string with full configuration control.
        /// </summary>
        /// <param name="code">The Lua/MoonSharp code to execute</param>
        /// <param name="applicationDirectory">Directory that appears as "/" to the script</param>
        /// <param name="basePolicySet">Base policy set configuration</param>
        /// <param name="policyModifier">Optional policy modifier function</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunString(
            string code,
            string applicationDirectory,
            BasePolicySet basePolicySet,
            Func<SecurityPolicy, SecurityPolicy> policyModifier = null
        ) => RunString(code, applicationDirectory, basePolicySet, policyModifier, new FileSystem());

        /// <summary>
        /// Runs Lua code from string with full configuration control.
        /// </summary>
        /// <param name="code">The Lua/MoonSharp code to execute</param>
        /// <param name="applicationDirectory">Directory that appears as "/" to the script</param>
        /// <param name="basePolicySet">Base policy set configuration</param>
        /// <param name="policyModifier">Optional policy modifier function</param>
        /// <param name="fileSystem">File system abstraction to use</param>
        /// <returns>A DynValue containing the result of the processing of the executed script</returns>
        public static DynValue RunString(
            string code,
            string applicationDirectory,
            BasePolicySet basePolicySet,
            Func<SecurityPolicy, SecurityPolicy> policyModifier,
            IFileSystem fileSystem
        )
        {
            // Apply policy modifications if provided, with chroot enabled for application directory
            var defaultPolicy = basePolicySet
                .GetDefaultPolicy()
                .Match(
                    policy => policy,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to get default policy: {error.Message}"
                        )
                );
            var finalPolicy = policyModifier?.Invoke(defaultPolicy) ?? defaultPolicy;

            // Enable chroot for string scripts to application directory
            if (!string.IsNullOrEmpty(applicationDirectory))
            {
                finalPolicy = finalPolicy with
                {
                    EnableChroot = true,
                    DirectoryPermissions = finalPolicy.DirectoryPermissions.SetItem(
                        fileSystem.Path.GetFullPath(applicationDirectory),
                        DirectoryPermissions.ListAndCreateFiles
                    ),
                };
            }

            var finalBasePolicySet = basePolicySet
                .WithDefaultPolicy(finalPolicy)
                .Match(
                    policy => policy,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to update default policy: {error.Message}"
                        )
                );
            var script = new Script(finalBasePolicySet);
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
            // Script ownership check removed
            Closure c;

            if (envTable == null)
            {
                var meta = _mainProcessor.FindMeta(ref address);

                // if we find the meta for a new chunk, we use the value in the meta for the _ENV upvalue
                c = meta is { NumVal2: (int)OpCodeMetadataType.ChunkEntrypoint }
                    ? new Closure(
                        this,
                        address,
                        new[] { SymbolRef.Upvalue(WellKnownSymbols.ENV, 0) },
                        new[] { meta.Value }
                    )
                    : new Closure(this, address, new SymbolRef[0], new DynValue[0]);
            }
            else
            {
                var syms = new[]
                {
                    new SymbolRef
                    {
                        i_Env = null,
                        i_Index = 0,
                        i_Name = WellKnownSymbols.ENV,
                        i_Type = SymbolRefType.DefaultEnv,
                    },
                };

                var vals = new[] { DynValue.NewTable(envTable) };

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
            // Script ownership check removed
            // Script ownership check removed

            if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
            {
                var metafunction = _mainProcessor.GetMetamethod(function, "__call");

                if (metafunction != null)
                {
                    var metaargs = new DynValue[args.Length + 1];
                    metaargs[0] = function;
                    for (var i = 0; i < args.Length; i++)
                        metaargs[i + 1] = args[i];

                    function = metafunction;
                    args = metaargs;
                }
                else
                {
                    throw new ArgumentException(
                        "function is not a function and has no __call metamethod."
                    );
                }
            }
            else if (function.Type == DataType.ClrFunction)
            {
                return function.Callback.ClrCallback(
                    CreateDynamicExecutionContext(),
                    new CallbackArguments(args, false)
                );
            }

            return _mainProcessor.Call(function, args);
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
            var dargs = new DynValue[args.Length];

            for (var i = 0; i < dargs.Length; i++)
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
            // Script ownership check removed

            if (function.Type == DataType.Function)
                return _mainProcessor.Coroutine_Create(function.Function);
            if (function.Type == DataType.ClrFunction)
                return DynValue.NewCoroutine(new Coroutine(function.Callback));
            throw new ArgumentException(
                "function is not of DataType.Function or DataType.ClrFunction"
            );
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
            // Script ownership check removed
            // Script ownership check removed

            if (coroutine == null || coroutine.Type != Coroutine.CoroutineType.Coroutine)
                throw new InvalidOperationException("coroutine is not CoroutineType.Coroutine");
            if (function == null || function.Type != DataType.Function)
                throw new InvalidOperationException("function is not DataType.Function");
            if (coroutine.State != CoroutineState.Dead)
                throw new InvalidOperationException(
                    "coroutine's state must be CoroutineState.Dead to recycle"
                );

            return coroutine.Recycle(_mainProcessor, function.Function);
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
            get { return _mainProcessor.DebuggerEnabled; }
            set { _mainProcessor.DebuggerEnabled = value; }
        }

        /// <summary>
        /// Attaches a debugger. This usually should be called by the debugger itself and not by user code.
        /// </summary>
        /// <param name="debugger">The debugger object.</param>
        public void AttachDebugger(IDebugger debugger)
        {
            DebuggerEnabled = true;
            _debugger = debugger;
            _mainProcessor.AttachDebugger(debugger);

            foreach (var src in _sources)
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
            return _sources[sourceCodeID];
        }

        /// <summary>
        /// Gets the source code count.
        /// </summary>
        /// <value>
        /// The source code count.
        /// </value>
        public int SourceCodeCount
        {
            get { return _sources.Count; }
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
            // Script ownership check removed

            var globals = globalContext ?? Globals;
            var filename =
                Options.ScriptLoader.ResolveModuleName(modname, globals)
                ?? throw new ScriptRuntimeException("module '{0}' not found", modname);
            var func = LoadFile(filename, globalContext, filename);
            return func;
        }

        /// <summary>
        /// Gets a type metatable.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public Table GetTypeMetatable(DataType type)
        {
            var t = (int)type;

            if (t >= 0 && t < _typeMetatables.Length)
                return _typeMetatables[t];

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
            // Script ownership check removed

            var t = (int)type;

            _typeMetatables[t] =
                t >= 0 && t < _typeMetatables.Length
                    ? metatable
                    : throw new ArgumentException("Specified type not supported : " + type);
        }

        /// <summary>
        /// Warms up the parser/lexer structures so that MoonSharp operations start faster.
        /// </summary>
        /// <param name="basePolicySet">Base policy set to use for warmup</param>
        public static void WarmUp(BasePolicySet basePolicySet)
        {
            var s = new Script(basePolicySet);
            s.LoadString("return 1;");
        }

        /// <summary>
        /// Creates a new dynamic expression.
        /// </summary>
        /// <param name="code">The code of the expression.</param>
        /// <returns></returns>
        public DynamicExpression CreateDynamicExpression(string code)
        {
            var dee = Loader_Fast.LoadDynamicExpr(
                this,
                new SourceCode("__dynamic", code, -1, this)
            );
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
            // Script ownership check removed

            return new DynamicExpression(this, code, constant);
        }

        /// <summary>
        /// Gets an execution context exposing only partial functionality, which should be used for
        /// those cases where the execution engine is not really running - for example for dynamic expression
        /// or calls from CLR to CLR callbacks
        /// </summary>
        internal ScriptExecutionContext CreateDynamicExecutionContext()
        {
            return new ScriptExecutionContext(_mainProcessor, null);
        }

        /// <summary>
        /// MoonSharp (like Lua itself) provides a registry, a predefined table that can be used by any CLR code to
        /// store whatever Lua values it needs to store.
        /// Any CLR code can store data into this table, but it should take care to choose keys
        /// that are different from those used by other libraries, to avoid collisions.
        /// Typically, you should use as key a string GUID, a string containing your library name, or a
        /// userdata with the address of a CLR object in your code.
        /// </summary>
        public Table Registry { get; private set; }

        /// <summary>
        /// Gets a banner string with copyright info, link to website, version, etc.
        /// </summary>
        public static string GetBanner(string subproduct = null)
        {
            subproduct = subproduct != null ? subproduct + " " : "";

            var sb = new StringBuilder();
            sb.AppendLine(
                $"SolarSharp {subproduct}{VERSION} [{GlobalOptions.Platform.GetPlatformName()}]"
            );
            sb.AppendLine("A secure Lua interpreter for .NET");
            sb.AppendLine("Based on MoonSharp - Copyright (C) 2014-2016 Marco Mastropaolo");
            return sb.ToString();
        }

        /// <summary>
        /// Loads a public key into the VM, enforcing manifest requirements for all .lua files
        /// </summary>
        public Script LoadKey(string pemPublicKey)
        {
            var newTrustStoreResult = TrustStore.AddTrustedKey(pemPublicKey);
            if (newTrustStoreResult.IsFailure)
                throw new ArgumentException(
                    $"Failed to add trusted key: {newTrustStoreResult.Error.Message}"
                );

            TrustStore = newTrustStoreResult.Value;
            return this;
        }

        /// <summary>
        /// Adds a trusted certificate to the VM, validating it and extracting the public key
        /// </summary>
        public Script AddTrustedCertificate(string certificatePem)
        {
            var newTrustStoreResult = TrustStore.AddTrustedCertificate(certificatePem);
            if (newTrustStoreResult.IsFailure)
                throw new ArgumentException(
                    $"Failed to add trusted certificate: {newTrustStoreResult.Error.Message}"
                );

            TrustStore = newTrustStoreResult.Value;
            return this;
        }

        /// <summary>
        /// Gets whether this VM has loaded any public keys
        /// </summary>
        public bool HasLoadedKeys
        {
            get { return !TrustStore.IsEmpty; }
        }

        /// <summary>
        /// Gets the count of files protected by manifests
        /// </summary>
        public int ProtectedFileCount => _protectedFiles.Count;

        /// <summary>
        /// Checks if a file path is protected by any loaded manifest
        /// </summary>
        public bool IsFileProtected(string filePath) => _protectedFiles.IsProtected(filePath);

        /// <summary>
        /// Validates a protected file against its expected hash
        /// </summary>
        public Result<VerifiedProtectedFile, string> ValidateProtectedFile(string filePath)
        {
            var protectionResult = _protectedFiles.GetProtection(filePath);
            if (protectionResult.HasNoValue)
            {
                return Result.Failure<VerifiedProtectedFile, string>(
                    "File is not protected by any manifest"
                );
            }

            return ProtectedFileValidator.ValidateFile(protectionResult.Value, filePath);
        }

        /// <summary>
        /// Processes certificate information from V2.0 manifest signed content blocks
        /// </summary>
        private void ProcessManifestCertificateChain(Manifest manifest)
        {
            // V2.0 manifests don't have a centralized certificate chain
            // Instead, certificates are embedded in the signature verification process
            // For now, we'll extract any certificate-related information from key IDs

            foreach (var block in manifest.SignedContent)
            {
                if (!string.IsNullOrEmpty(block.KeyId))
                {
                    // The key ID in V2.0 format is typically a SHA256 fingerprint
                    // Additional certificate processing would happen during signature validation
                    // This is a placeholder for future certificate chain processing
                }
            }
        }

        /// <summary>
        /// Processes protected files from a manifest, adding them to the Script's protected files collection
        /// </summary>
        private void ProcessManifestProtectedFiles(
            Manifest manifest,
            string manifestPath,
            bool fromTrusted
        )
        {
            var transformResult = ManifestTransformResult.Transform(
                manifest,
                manifestPath,
                fromTrusted
            );

            transformResult.Match(
                result =>
                {
                    // Combine with existing protected files (trusted manifests take precedence)
                    _protectedFiles = ManifestTransformer.CombineProtectedFiles(
                        _protectedFiles,
                        result.ProtectedFiles
                    );
                },
                error =>
                {
                    // Log error but don't fail script execution
                    // Protected files are an enhancement, not a requirement
                    Debug.WriteLine(
                        $"Failed to process protected files from {manifestPath}: {error}"
                    );
                }
            );
        }

        /// <summary>
        /// Validates that a file meets manifest requirements if keys are loaded
        /// </summary>
        private void ValidateManifestRequirement(string luaFilePath)
        {
            if (!HasLoadedKeys)
                return; // No keys loaded, no manifest requirement

            // Check if a manifest exists for this file
            if (_manifests.Count == 0)
                throw new ManifestSignatureException(
                    $"Manifest required for {luaFilePath} but none loaded",
                    "ValidateManifestRequirement"
                );

            // Additional validation logic would go here
        }

        /// <summary>
        /// Validates protected file integrity if the file is covered by a manifest
        /// </summary>
        private void ValidateProtectedFileIntegrity(string filePath)
        {
            if (!IsFileProtected(filePath))
                return; // File not protected by any manifest, no validation needed

            var validationResult = ValidateProtectedFile(filePath);
            validationResult.Match(
                verifiedFile =>
                {
                    if (!verifiedFile.IsValid)
                    {
                        throw new ManifestSignatureException(
                            $"File integrity validation failed for {filePath}: "
                                + $"expected hash {verifiedFile.ProtectedFile.ExpectedHash}, "
                                + $"got {verifiedFile.ActualHash}",
                            "ValidateProtectedFileIntegrity"
                        );
                    }
                },
                error =>
                    throw new ManifestSignatureException(
                        $"Protected file validation failed for {filePath}: {error}",
                        "ValidateProtectedFileIntegrity"
                    )
            );
        }

        /// <summary>
        /// Registers the security tracer if environment variables indicate it should be enabled
        /// </summary>
        private void RegisterSecurityTracerIfEnabled()
        {
            // Check if auto-start is enabled
            var autoStartStr = Environment.GetEnvironmentVariable("LUA_SANDBOX_AUTO_START");
            var autoStart =
                !string.IsNullOrEmpty(autoStartStr)
                && (
                    autoStartStr.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || autoStartStr.Equals("1", StringComparison.OrdinalIgnoreCase)
                );

            if (!autoStart)
                return;

            // Try to get the global tracer (which handles both old and new environment variables)
            var tracer = FileSecurityTracer.GetGlobalTracer();
            if (tracer != null)
            {
                // Add the tracer to our security logger
                _securityLogger.AddHandler(tracer);
            }
        }
    }
}
