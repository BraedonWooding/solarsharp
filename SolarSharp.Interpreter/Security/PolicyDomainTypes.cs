using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Collection of directories to scan for manifests
    /// </summary>
    public sealed record DirectorySet(ImmutableArray<string> Directories) : IEnumerable<string>
    {
        public static DirectorySet Create(params string[] directories) =>
            new DirectorySet(directories.ToImmutableArray());

        public static DirectorySet Create(IEnumerable<string> directories) =>
            new DirectorySet(directories.ToImmutableArray());

        public static DirectorySet Empty
        {
            get { return new DirectorySet(ImmutableArray<string>.Empty); }
        }

        public DirectorySet Add(string directory) => new DirectorySet(Directories.Add(directory));

        public DirectorySet AddRange(IEnumerable<string> directories) =>
            new DirectorySet(Directories.AddRange(directories));

        public IEnumerator<string> GetEnumerator() => Directories.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Directories.Length; }
        }
        public bool IsEmpty
        {
            get { return Directories.IsEmpty; }
        }
    }

    /// <summary>
    /// Collection of signature-based policies
    /// </summary>
    public sealed record SignaturePolicies(ImmutableDictionary<string, SecurityPolicy> Policies)
        : IEnumerable<KeyValuePair<string, SecurityPolicy>>
    {
        public static SignaturePolicies Create() =>
            new SignaturePolicies(ImmutableDictionary<string, SecurityPolicy>.Empty);

        public static SignaturePolicies Create(
            IEnumerable<(string publicKeyToken, SecurityPolicy policy)> policies
        ) =>
            new SignaturePolicies(
                policies.ToImmutableDictionary(p => p.publicKeyToken, p => p.policy)
            );

        public static SignaturePolicies Empty
        {
            get { return new SignaturePolicies(ImmutableDictionary<string, SecurityPolicy>.Empty); }
        }

        public SignaturePolicies Add(string publicKeyToken, SecurityPolicy policy) =>
            new SignaturePolicies(Policies.SetItem(publicKeyToken, policy));

        public SignaturePolicies AddUnsigned(SecurityPolicy policy) =>
            new SignaturePolicies(Policies.SetItem("", policy));

        public SignaturePolicies Remove(string publicKeyToken) =>
            new SignaturePolicies(Policies.Remove(publicKeyToken));

        public bool ContainsKey(string publicKeyToken) => Policies.ContainsKey(publicKeyToken);

        public bool TryGetValue(string publicKeyToken, out SecurityPolicy policy) =>
            Policies.TryGetValue(publicKeyToken, out policy);

        public IEnumerator<KeyValuePair<string, SecurityPolicy>> GetEnumerator() =>
            Policies.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Policies.Count; }
        }
        public bool IsEmpty
        {
            get { return Policies.IsEmpty; }
        }
    }

    /// <summary>
    /// Collection of path-based policies
    /// </summary>
    public sealed record PathPolicies(ImmutableDictionary<string, SecurityPolicy> Policies)
        : IEnumerable<KeyValuePair<string, SecurityPolicy>>
    {
        public static PathPolicies Create() =>
            new PathPolicies(ImmutableDictionary<string, SecurityPolicy>.Empty);

        public static PathPolicies Create(
            IEnumerable<(string pattern, SecurityPolicy policy)> policies
        ) => new PathPolicies(policies.ToImmutableDictionary(p => p.pattern, p => p.policy));

        public static PathPolicies Empty
        {
            get { return new PathPolicies(ImmutableDictionary<string, SecurityPolicy>.Empty); }
        }

        public PathPolicies Add(string pathPattern, SecurityPolicy policy) =>
            new PathPolicies(Policies.SetItem(pathPattern, policy));

        public PathPolicies Remove(string pathPattern) =>
            new PathPolicies(Policies.Remove(pathPattern));

        public bool ContainsKey(string pathPattern) => Policies.ContainsKey(pathPattern);

        public bool TryGetValue(string pathPattern, out SecurityPolicy policy) =>
            Policies.TryGetValue(pathPattern, out policy);

        public IEnumerator<KeyValuePair<string, SecurityPolicy>> GetEnumerator() =>
            Policies.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Policies.Count; }
        }
        public bool IsEmpty
        {
            get { return Policies.IsEmpty; }
        }
    }

    /// <summary>
    /// Collection of manifest files found during scanning
    /// </summary>
    public sealed record ManifestFiles(ImmutableArray<ManifestFile> Files)
        : IEnumerable<ManifestFile>
    {
        public static ManifestFiles Create(params ManifestFile[] files) =>
            new ManifestFiles(files.ToImmutableArray());

        public static ManifestFiles Create(IEnumerable<ManifestFile> files) =>
            new ManifestFiles(files.ToImmutableArray());

        public static ManifestFiles Empty
        {
            get { return new ManifestFiles(ImmutableArray<ManifestFile>.Empty); }
        }

        public ManifestFiles Add(ManifestFile file) => new ManifestFiles(Files.Add(file));

        public ManifestFiles AddRange(IEnumerable<ManifestFile> files) =>
            new ManifestFiles(Files.AddRange(files));

        public ManifestFiles Where(Func<ManifestFile, bool> predicate) =>
            new ManifestFiles(Files.Where(predicate).ToImmutableArray());

        public IEnumerator<ManifestFile> GetEnumerator() => Files.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Files.Length; }
        }
        public bool IsEmpty
        {
            get { return Files.IsEmpty; }
        }
    }

    /// <summary>
    /// Immutable collection of parsed manifests
    /// </summary>
    public sealed record ManifestCollection(ImmutableArray<Manifest> Items) : IEnumerable<Manifest>
    {
        public static ManifestCollection Create(params Manifest[] manifests) =>
            new ManifestCollection(manifests.ToImmutableArray());

        public static ManifestCollection Create(IEnumerable<Manifest> manifests) =>
            new ManifestCollection(manifests.ToImmutableArray());

        public static ManifestCollection Empty
        {
            get { return new ManifestCollection(ImmutableArray<Manifest>.Empty); }
        }

        public ManifestCollection Add(Manifest manifest) =>
            new ManifestCollection(Items.Add(manifest));

        public ManifestCollection AddRange(IEnumerable<Manifest> manifests) =>
            new ManifestCollection(Items.AddRange(manifests));

        public ManifestCollection Where(Func<Manifest, bool> predicate) =>
            new ManifestCollection(Items.Where(predicate).ToImmutableArray());

        public int Count
        {
            get { return Items.Length; }
        }
        public bool IsEmpty
        {
            get { return Items.IsEmpty; }
        }

        public IEnumerator<Manifest> GetEnumerator() => Items.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Collection of validated manifests
    /// </summary>
    public sealed record ValidatedManifests(ImmutableArray<ValidatedManifest> Items)
        : IEnumerable<ValidatedManifest>
    {
        public static ValidatedManifests Create(params ValidatedManifest[] manifests) =>
            new ValidatedManifests(manifests.ToImmutableArray());

        public static ValidatedManifests Create(IEnumerable<ValidatedManifest> manifests) =>
            new ValidatedManifests(manifests.ToImmutableArray());

        public static ValidatedManifests Empty
        {
            get { return new ValidatedManifests(ImmutableArray<ValidatedManifest>.Empty); }
        }

        public ValidatedManifests Add(ValidatedManifest manifest) =>
            new ValidatedManifests(Items.Add(manifest));

        public ValidatedManifests AddRange(IEnumerable<ValidatedManifest> manifests) =>
            new ValidatedManifests(Items.AddRange(manifests));

        public IEnumerator<ValidatedManifest> GetEnumerator() =>
            Items.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Items.Length; }
        }
        public bool IsEmpty
        {
            get { return Items.IsEmpty; }
        }
    }

    /// <summary>
    /// Collection of verified manifests
    /// </summary>
    public sealed record VerifiedManifests(ImmutableArray<VerifiedManifest> Items)
        : IEnumerable<VerifiedManifest>
    {
        public static VerifiedManifests Create(params VerifiedManifest[] manifests) =>
            new VerifiedManifests(manifests.ToImmutableArray());

        public static VerifiedManifests Create(IEnumerable<VerifiedManifest> manifests) =>
            new VerifiedManifests(manifests.ToImmutableArray());

        public static VerifiedManifests Empty
        {
            get { return new VerifiedManifests(ImmutableArray<VerifiedManifest>.Empty); }
        }

        public VerifiedManifests Add(VerifiedManifest manifest) =>
            new VerifiedManifests(Items.Add(manifest));

        public VerifiedManifests AddRange(IEnumerable<VerifiedManifest> manifests) =>
            new VerifiedManifests(Items.AddRange(manifests));

        public IEnumerator<VerifiedManifest> GetEnumerator() =>
            Items.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Items.Length; }
        }
        public bool IsEmpty
        {
            get { return Items.IsEmpty; }
        }
    }

    /// <summary>
    /// Collection of compiled policies ready for runtime use
    /// </summary>
    public sealed record CompiledPolicies(ImmutableArray<CompiledPolicy> Items)
        : IEnumerable<CompiledPolicy>
    {
        public static CompiledPolicies Create(params CompiledPolicy[] policies) =>
            new CompiledPolicies(policies.ToImmutableArray());

        public static CompiledPolicies Create(IEnumerable<CompiledPolicy> policies) =>
            new CompiledPolicies(policies.ToImmutableArray());

        public static CompiledPolicies Empty
        {
            get { return new CompiledPolicies(ImmutableArray<CompiledPolicy>.Empty); }
        }

        public CompiledPolicies Add(CompiledPolicy policy) =>
            new CompiledPolicies(Items.Add(policy));

        public CompiledPolicies AddRange(IEnumerable<CompiledPolicy> policies) =>
            new CompiledPolicies(Items.AddRange(policies));

        public IEnumerator<CompiledPolicy> GetEnumerator() => Items.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get { return Items.Length; }
        }
        public bool IsEmpty
        {
            get { return Items.IsEmpty; }
        }

        public CompiledPolicy First() => Items.First();

        public IEnumerable<CompiledPolicy> Skip(int count) => Items.Skip(count);

        public IEnumerable<T> Select<T>(Func<CompiledPolicy, T> selector) => Items.Select(selector);
    }

    /// <summary>
    /// Rule for directory access that requires specific signing keys
    /// </summary>
    public sealed record DirectoryAccessRule
    {
        /// <summary>
        /// Directory pattern (supports wildcards like /plugins/*)
        /// </summary>
        public string DirectoryPattern { get; init; } = "";

        /// <summary>
        /// File permissions granted if signing key matches
        /// </summary>
        public FilePermissions Permissions { get; init; } = FilePermissions.None;

        /// <summary>
        /// Required signing key fingerprints (SHA256 format)
        /// Empty set means no key restrictions
        /// </summary>
        public ImmutableHashSet<string> RequiredSigningKeys { get; init; } =
            ImmutableHashSet<string>.Empty;

        /// <summary>
        /// Whether to enforce manifest scope (manifests can only define permissions within their directory)
        /// </summary>
        public bool EnforceManifestScope { get; init; } = true;

        /// <summary>
        /// Creates a new rule for a directory with a single required key
        /// </summary>
        public static DirectoryAccessRule Create(
            string pattern,
            FilePermissions permissions,
            string requiredKey
        ) =>
            new DirectoryAccessRule
            {
                DirectoryPattern = pattern,
                Permissions = permissions,
                RequiredSigningKeys = ImmutableHashSet.Create(requiredKey),
            };

        /// <summary>
        /// Creates a new rule for a directory with multiple required keys
        /// </summary>
        public static DirectoryAccessRule Create(
            string pattern,
            FilePermissions permissions,
            params string[] requiredKeys
        ) =>
            new DirectoryAccessRule
            {
                DirectoryPattern = pattern,
                Permissions = permissions,
                RequiredSigningKeys = requiredKeys.ToImmutableHashSet(),
            };

        /// <summary>
        /// Creates a new rule without key restrictions
        /// </summary>
        public static DirectoryAccessRule Create(string pattern, FilePermissions permissions) =>
            new DirectoryAccessRule
            {
                DirectoryPattern = pattern,
                Permissions = permissions,
                RequiredSigningKeys = ImmutableHashSet<string>.Empty,
            };
    }

    /// <summary>
    /// Default instances to eliminate nulls
    /// </summary>
    public static class SecurityPolicyDefaults
    {
        public static readonly SecurityPolicy EmptySecurityPolicy = new SecurityPolicy
        {
            Name = "EmptySecurityPolicy",
            TimeoutMs = 0,
            MaxMemoryMB = 0,
            MaxInstructions = 0,
            AllowExecution = false,
            AllowedModules = CoreModules.None,
            Capabilities = ScriptCapabilities.None,
            FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
            PubSubPermissions = new PubSubPermissions(),
        };

        public static readonly ManifestScanOptions DefaultScanOptions = new ManifestScanOptions
        {
            ManifestFileNames = ManifestScanner.DefaultManifestNames,
            ValidateManifests = true,
            IncludeErrors = true,
        };

        public static readonly SignaturePolicies EmptySignaturePolicies = SignaturePolicies.Empty;
        public static readonly PathPolicies EmptyPathPolicies = PathPolicies.Empty;
        public static readonly DirectorySet EmptyDirectories = DirectorySet.Empty;
    }
}
