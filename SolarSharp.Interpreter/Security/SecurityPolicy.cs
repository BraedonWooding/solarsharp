using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;

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
        /// Checks if execution is prevented by any policy settings
        /// </summary>
        [JsonIgnore]
        public bool PreventsExecution
        {
            get
            {
                return !AllowExecution
                    || TimeoutMs == 0
                    || MaxMemoryMB == 0
                    || MaxInstructions == 0;
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
                TimeoutMs = Math.Min(TimeoutMs, other.TimeoutMs),
                MaxMemoryMB = Math.Min(MaxMemoryMB, other.MaxMemoryMB),
                MaxInstructions = Math.Min(MaxInstructions, other.MaxInstructions),
                MaxCallDepth = Math.Min(MaxCallDepth, other.MaxCallDepth),
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
                MaxFileSize = Math.Min(MaxFileSize, other.MaxFileSize),
                EnableChroot = EnableChroot || other.EnableChroot,

                // Network - intersection
                AllowNetworkAccess = AllowNetworkAccess && other.AllowNetworkAccess,
                AllowedHosts = AllowedHosts.Intersect(other.AllowedHosts).ToImmutableArray(),

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
