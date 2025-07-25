using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Pure immutable security policy record - single source of truth for all security policies
    /// Consolidates Policy, ManifestPolicy, and SecurityConfiguration into one clean record
    /// </summary>
    public sealed record SecurityPolicy
    {
        /// <summary>
        /// Policy name for reference within manifests
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore]
        public Maybe<string> Name { get; init; } = Maybe<string>.None;

        /// <summary>
        /// JSON serialization helper for Name property
        /// </summary>
        [JsonPropertyName("name")]
        public string NameForJson
        {
            get => Name.GetValueOrDefault("Unnamed");
            init =>
                Name = string.IsNullOrWhiteSpace(value)
                    ? Maybe<string>.None
                    : Maybe<string>.From(value);
        }

        // Execution limits

        /// <summary>
        /// Execution timeout in milliseconds (0 = no execution)
        /// </summary>
        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; init; } // 0 = no execution

        /// <summary>
        /// Maximum memory usage in MB (0 = no execution)
        /// </summary>
        [JsonPropertyName("maxMemoryMB")]
        public int MaxMemoryMB { get; init; } // 0 = no execution

        /// <summary>
        /// Maximum instruction count (0 = no execution)
        /// </summary>
        [JsonPropertyName("maxInstructions")]
        public long MaxInstructions { get; init; } // 0 = no execution

        /// <summary>
        /// Maximum call depth
        /// </summary>
        [JsonPropertyName("maxCallDepth")]
        public int MaxCallDepth { get; init; } // 0 = no execution

        /// <summary>
        /// Maximum number of tables that can be created.
        /// Set to <see cref="SecurityConstants.UnlimitedTables"/> (0) for unlimited table creation.
        /// Default: <see cref="SecurityConstants.DefaultMaxTables"/> (10,000 tables).
        /// </summary>
        [JsonPropertyName("maxTables")]
        public int MaxTables { get; init; } = SecurityConstants.DefaultMaxTables;

        /// <summary>
        /// Defines how resource limits are tracked across multiple executions
        /// </summary>
        [JsonPropertyName("resourceLimitScope")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ResourceLimitScope ResourceLimitScope { get; init; } = ResourceLimitScope.PerExecution;

        /// <summary>
        /// Whether execution is allowed (false prevents all execution)
        /// </summary>
        [JsonPropertyName("allowExecution")]
        public bool AllowExecution { get; init; } // Default: deny all execution

        // File system access

        /// <summary>
        /// File access permissions by pattern
        /// </summary>
        [JsonPropertyName("filePermissions")]
        public ImmutableDictionary<string, FilePermissions> FilePermissions { get; init; } =
            ImmutableDictionary<string, FilePermissions>.Empty;

        /// <summary>
        /// Default file access level
        /// </summary>
        [JsonPropertyName("defaultFileAccess")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FilePermissions DefaultFileAccess { get; init; } = Security.FilePermissions.None;

        /// <summary>
        /// Path restrictions from manifests (runtime checked)
        /// </summary>
        [JsonIgnore]
        public PathRestriction PathRestrictions { get; init; } = PathRestriction.None;

        /// <summary>
        /// Default directory access level
        /// </summary>
        [JsonPropertyName("defaultDirectoryAccess")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DirectoryPermissions DefaultDirectoryAccess { get; init; } =
            Security.DirectoryPermissions.None;

        /// <summary>
        /// Directory permissions by pattern
        /// </summary>
        [JsonPropertyName("directoryPermissions")]
        public ImmutableDictionary<
            string,
            DirectoryPermissions
        > DirectoryPermissions { get; init; } =
            ImmutableDictionary<string, DirectoryPermissions>.Empty;

        /// <summary>
        /// Whether to allow accessing hidden files (files starting with .)
        /// </summary>
        [JsonPropertyName("allowHiddenFiles")]
        public bool AllowHiddenFiles { get; init; }

        /// <summary>
        /// Maximum file size for read/write operations in bytes (default: 10MB)
        /// </summary>
        [JsonPropertyName("maxFileSize")]
        public long MaxFileSize { get; init; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Whether to enable chroot-style file system isolation
        /// </summary>
        [JsonPropertyName("enableChroot")]
        public bool EnableChroot { get; init; }

        // Network access

        /// <summary>
        /// Whether network access is allowed
        /// </summary>
        [JsonPropertyName("allowNetworkAccess")]
        public bool AllowNetworkAccess { get; init; }

        /// <summary>
        /// Allowed network hosts
        /// </summary>
        [JsonPropertyName("allowedHosts")]
        public ImmutableArray<string> AllowedHosts { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Host restrictions from manifests (runtime checked)
        /// </summary>
        [JsonIgnore]
        public HostRestriction HostRestrictions { get; init; } = HostRestriction.None;

        // Environment access

        /// <summary>
        /// Whether environment variable access is allowed
        /// </summary>
        [JsonPropertyName("allowEnvironmentAccess")]
        public bool AllowEnvironmentAccess { get; init; }

        /// <summary>
        /// Allowed environment variables
        /// </summary>
        [JsonPropertyName("allowedEnvironmentVariables")]
        public ImmutableArray<string> AllowedEnvironmentVariables { get; init; } =
            ImmutableArray<string>.Empty;

        // Modules and capabilities

        /// <summary>
        /// Allowed Lua modules
        /// </summary>
        [JsonPropertyName("allowedModules")]
        public CoreModules AllowedModules { get; init; } = CoreModules.None;

        /// <summary>
        /// Script capabilities
        /// </summary>
        [JsonPropertyName("capabilities")]
        public ScriptCapabilities Capabilities { get; init; } = ScriptCapabilities.None;

        // Pub/sub permissions

        /// <summary>
        /// Pub/sub permissions
        /// </summary>
        [JsonPropertyName("pubSubPermissions")]
        public PubSubPermissions PubSubPermissions { get; init; } = new PubSubPermissions();

        /// <summary>
        /// Environment emulation policy
        /// </summary>
        [JsonPropertyName("environmentEmulation")]
        public EnvironmentEmulationPolicy EnvironmentEmulation { get; init; } =
            new EnvironmentEmulationPolicy();

        /// <summary>
        /// Whether to throw exceptions on non-critical security violations
        /// </summary>
        [JsonPropertyName("throwOnNonCriticalViolations")]
        public bool ThrowOnNonCriticalViolations { get; init; } = false;

        // Token-based access control

        /// <summary>
        /// Public key tokens allowed to read files in manifested directories
        /// </summary>
        [JsonPropertyName("allowReadByToken")]
        public ImmutableHashSet<string> AllowReadByToken { get; init; } =
            ImmutableHashSet<string>.Empty;

        /// <summary>
        /// Public key tokens allowed to write files in manifested directories
        /// </summary>
        [JsonPropertyName("allowWriteByToken")]
        public ImmutableHashSet<string> AllowWriteByToken { get; init; } =
            ImmutableHashSet<string>.Empty;

        /// <summary>
        /// Whether to prevent modification of signed files
        /// </summary>
        [JsonPropertyName("preventSignedModification")]
        public bool PreventSignedModification { get; init; } = true;

        /// <summary>
        /// Directory access rules that require specific signing keys
        /// </summary>
        [JsonPropertyName("directoryAccessRules")]
        public ImmutableArray<DirectoryAccessRule> DirectoryAccessRules { get; init; } =
            ImmutableArray<DirectoryAccessRule>.Empty;

        // Computed properties

        /// <summary>
        /// Checks if execution is prevented by any policy settings.
        /// NEW SEMANTICS: 0 = deny, so any limit set to 0 prevents execution
        /// </summary>
        [JsonIgnore]
        public bool PreventsExecution
        {
            get
            {
                return !AllowExecution
                    || TimeoutMs == SecurityConstants.DenyLimit
                    || MaxMemoryMB == SecurityConstants.DenyLimit
                    || MaxInstructions == SecurityConstants.DenyLimit
                    || MaxCallDepth == SecurityConstants.DenyLimit
                    || MaxTables == SecurityConstants.DenyLimit;
            }
        }

        // The default SecurityPolicy constructor creates a deny-all policy
        // Use Examples class for predefined policies

        // Domain logic methods

        /// <summary>
        /// Creates a new policy with a different name (immutable update)
        /// </summary>
        public SecurityPolicy WithName(string name)
        {
            return this with { Name = Maybe<string>.From(name) };
        }

        /// <summary>
        /// Creates a new policy with minimum limits from this and another policy (intersection).
        /// This implements reducing-only semantics - the result is always more restrictive.
        /// </summary>
        public SecurityPolicy IntersectWith(SecurityPolicy other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            var thisName = Name.GetValueOrDefault("Unknown");
            var otherName = other.Name.GetValueOrDefault("Unknown");

            return this with
            {
                Name = Maybe<string>.From($"{thisName}∩{otherName}"),
                TimeoutMs = IntersectNumericLimit(TimeoutMs, other.TimeoutMs),
                MaxMemoryMB = IntersectNumericLimit(MaxMemoryMB, other.MaxMemoryMB),
                MaxInstructions = IntersectNumericLimit(MaxInstructions, other.MaxInstructions),
                MaxCallDepth = IntersectNumericLimit(MaxCallDepth, other.MaxCallDepth),
                MaxTables = IntersectNumericLimit(MaxTables, other.MaxTables),
                ResourceLimitScope = (ResourceLimitScope)Math.Max((int)ResourceLimitScope, (int)other.ResourceLimitScope), // More restrictive wins
                AllowExecution = AllowExecution && other.AllowExecution,

                // File system - take most restrictive
                DefaultFileAccess = GetMostRestrictiveFileAccess(
                    DefaultFileAccess,
                    other.DefaultFileAccess
                ),
                DefaultDirectoryAccess = GetMostRestrictiveDirectoryAccess(
                    DefaultDirectoryAccess,
                    other.DefaultDirectoryAccess
                ),
                FilePermissions = IntersectFilePermissions(FilePermissions, other.FilePermissions),
                DirectoryPermissions = IntersectDirectoryPermissions(
                    DirectoryPermissions,
                    other.DirectoryPermissions
                ),
                AllowHiddenFiles = AllowHiddenFiles && other.AllowHiddenFiles,
                MaxFileSize = IntersectNumericLimit(MaxFileSize, other.MaxFileSize),
                EnableChroot = EnableChroot || other.EnableChroot,
                PathRestrictions = PathRestrictions.CombineWith(other.PathRestrictions),

                // Network - intersection
                AllowNetworkAccess = AllowNetworkAccess && other.AllowNetworkAccess,
                AllowedHosts = AllowedHosts.Intersect(other.AllowedHosts).ToImmutableArray(),
                HostRestrictions = HostRestrictions.CombineWith(other.HostRestrictions),

                // Environment - intersection
                AllowEnvironmentAccess = AllowEnvironmentAccess && other.AllowEnvironmentAccess,
                AllowedEnvironmentVariables = AllowedEnvironmentVariables
                    .Intersect(other.AllowedEnvironmentVariables)
                    .ToImmutableArray(),

                // Modules and capabilities - intersection
                AllowedModules = AllowedModules & other.AllowedModules, // Bitwise AND for flags enum
                Capabilities = Capabilities & other.Capabilities, // Bitwise AND for flags enum

                // PubSub
                PubSubPermissions = PubSubPermissions.IntersectWith(other.PubSubPermissions),

                // Token access - intersection
                AllowReadByToken = AllowReadByToken.Intersect(other.AllowReadByToken),
                AllowWriteByToken = AllowWriteByToken.Intersect(other.AllowWriteByToken),

                // Security flags - more restrictive wins
                PreventSignedModification =
                    PreventSignedModification || other.PreventSignedModification,

                // Directory access rules - combine both sets
                DirectoryAccessRules = DirectoryAccessRules.AddRange(other.DirectoryAccessRules),
            };
        }

        /// <summary>
        /// Intersects two numeric limits, taking the most restrictive value.
        /// NEW SEMANTICS: 0 = deny (always wins), -1 = unlimited, >0 = actual limit
        /// </summary>
        /// <param name="a">First limit value</param>
        /// <param name="b">Second limit value</param>
        /// <returns>The most restrictive limit</returns>
        private static int IntersectNumericLimit(int a, int b)
        {
            // 0 (deny) always wins
            if (a == SecurityConstants.DenyLimit || b == SecurityConstants.DenyLimit) 
                return SecurityConstants.DenyLimit;
            
            // If either is unlimited (-1), use the other
            if (a == -1) return b;
            if (b == -1) return a;
            
            // Both are positive limits, use the smaller (more restrictive)
            return Math.Min(a, b);
        }

        /// <summary>
        /// Intersects two numeric limits (long version), taking the most restrictive value.
        /// NEW SEMANTICS: 0 = deny (always wins), -1 = unlimited, >0 = actual limit
        /// </summary>
        /// <param name="a">First limit value</param>
        /// <param name="b">Second limit value</param>
        /// <returns>The most restrictive limit</returns>
        private static long IntersectNumericLimit(long a, long b)
        {
            // 0 (deny) always wins
            if (a == 0 || b == 0) 
                return 0;
            
            // If either is unlimited (-1), use the other
            if (a == SecurityConstants.UnlimitedInstructions) return b;
            if (b == SecurityConstants.UnlimitedInstructions) return a;
            
            // Both are positive limits, use the smaller (more restrictive)
            return Math.Min(a, b);
        }

        private static FilePermissions GetMostRestrictiveFileAccess(
            FilePermissions access1,
            FilePermissions access2
        )
        {
            return (FilePermissions)Math.Min((int)access1, (int)access2);
        }

        private static DirectoryPermissions GetMostRestrictiveDirectoryAccess(
            DirectoryPermissions access1,
            DirectoryPermissions access2
        )
        {
            return (DirectoryPermissions)Math.Min((int)access1, (int)access2);
        }

        private static ImmutableDictionary<string, FilePermissions> IntersectFilePermissions(
            ImmutableDictionary<string, FilePermissions> a,
            ImmutableDictionary<string, FilePermissions> b
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, FilePermissions>();

            // Only include permissions that exist in both, with the minimum permission level
            foreach (var kvp in a)
            {
                if (b.TryGetValue(kvp.Key, out var otherPermission))
                {
                    builder[kvp.Key] = (FilePermissions)
                        Math.Min((int)kvp.Value, (int)otherPermission);
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableDictionary<
            string,
            DirectoryPermissions
        > IntersectDirectoryPermissions(
            ImmutableDictionary<string, DirectoryPermissions> a,
            ImmutableDictionary<string, DirectoryPermissions> b
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, DirectoryPermissions>();

            // Only include permissions that exist in both, with the most restrictive permission
            foreach (var kvp in a)
            {
                if (b.TryGetValue(kvp.Key, out var otherPermission))
                {
                    builder[kvp.Key] = GetMostRestrictiveDirectoryAccess(
                        kvp.Value,
                        otherPermission
                    );
                }
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Creates a new builder for constructing SecurityPolicy instances efficiently
        /// Use for complex configurations with many properties
        /// </summary>
        public static SecurityPolicyConfigurationBuilder NewBuilder() =>
            new SecurityPolicyConfigurationBuilder();

        /// <summary>
        /// Creates a builder initialized with this policy's values
        /// Use for modifying existing policies
        /// </summary>
        public SecurityPolicyConfigurationBuilder ToBuilder() =>
            new SecurityPolicyConfigurationBuilder(this);
    }
}
