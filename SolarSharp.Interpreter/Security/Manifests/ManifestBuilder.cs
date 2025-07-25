using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Builder for creating V2.0 manifests with fluent API
    /// </summary>
    public class ManifestBuilder
    {
        private string _manifestId;
        private readonly List<SignedContentBlockBuilder> _signedContentBlocks = new();

        public ManifestBuilder()
        {
            _manifestId = $"manifest-{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Sets the manifest ID
        /// </summary>
        public ManifestBuilder WithManifestId(string manifestId)
        {
            _manifestId = manifestId;
            return this;
        }

        /// <summary>
        /// Adds a signed content block
        /// </summary>
        public ManifestBuilder WithSignedContent(Action<SignedContentBlockBuilder> configure)
        {
            var builder = new SignedContentBlockBuilder();
            configure(builder);
            _signedContentBlocks.Add(builder);
            return this;
        }

        /// <summary>
        /// Builds the manifest
        /// </summary>
        public Manifest Build()
        {
            return new Manifest
            {
                Version = "2.0",
                ManifestId = _manifestId,
                SignedContent = _signedContentBlocks.Select(b => b.Build()).ToImmutableArray()
            };
        }
    }

    /// <summary>
    /// Builder for signed content blocks
    /// </summary>
    public class SignedContentBlockBuilder
    {
        private string _keyId = "";
        private string _signature = "";
        private string _publicKey = "";
        private string _publicKeyToken = "";
        private readonly List<string> _intermediateCAs = new();
        private readonly Dictionary<string, ManifestPackage> _packages = new();
        private readonly List<ManifestPolicy> _policies = new();

        /// <summary>
        /// Sets the key ID (SHA256 fingerprint)
        /// </summary>
        public SignedContentBlockBuilder WithKeyId(string keyId)
        {
            _keyId = keyId;
            return this;
        }

        /// <summary>
        /// Sets the signature
        /// </summary>
        public SignedContentBlockBuilder WithSignature(string signature)
        {
            _signature = signature;
            return this;
        }

        /// <summary>
        /// Sets the public key PEM
        /// </summary>
        public SignedContentBlockBuilder WithPublicKey(string publicKey)
        {
            _publicKey = publicKey;
            return this;
        }

        /// <summary>
        /// Sets the public key token
        /// </summary>
        public SignedContentBlockBuilder WithPublicKeyToken(string publicKeyToken)
        {
            _publicKeyToken = publicKeyToken;
            return this;
        }

        /// <summary>
        /// Adds an intermediate CA certificate
        /// </summary>
        public SignedContentBlockBuilder WithIntermediateCA(string caCertPem)
        {
            _intermediateCAs.Add(caCertPem);
            return this;
        }

        /// <summary>
        /// Adds a package
        /// </summary>
        public SignedContentBlockBuilder WithPackage(string packageId, Action<ManifestPackageBuilder> configure)
        {
            var builder = new ManifestPackageBuilder();
            configure(builder);
            _packages[packageId] = builder.Build();
            return this;
        }

        /// <summary>
        /// Adds a policy
        /// </summary>
        public SignedContentBlockBuilder WithPolicy(Action<PolicyBuilder> configure)
        {
            var builder = new PolicyBuilder();
            configure(builder);
            _policies.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// Builds the signed content block
        /// </summary>
        public SignedContentBlock Build()
        {
            return new SignedContentBlock
            {
                KeyId = _keyId,
                Signature = _signature,
                PublicKey = _publicKey,
                PublicKeyToken = _publicKeyToken,
                IntermediateCAs = _intermediateCAs.ToImmutableArray(),
                Packages = _packages.ToImmutableDictionary(),
                Policies = _policies.ToImmutableArray()
            };
        }
    }

    /// <summary>
    /// Builder for manifest packages
    /// </summary>
    public class ManifestPackageBuilder
    {
        private readonly Dictionary<string, string> _files = new();
        private string _name = "";
        private string _version = "";
        private string _description = "";

        /// <summary>
        /// Adds a file with its hash
        /// </summary>
        public ManifestPackageBuilder WithFile(string path, string hash)
        {
            _files[path] = hash.StartsWith("sha256:") ? hash : $"sha256:{hash}";
            return this;
        }

        /// <summary>
        /// Sets package metadata
        /// </summary>
        public ManifestPackageBuilder WithMetadata(string name, string version, string description = "")
        {
            _name = name;
            _version = version;
            _description = description;
            return this;
        }

        /// <summary>
        /// Builds the package
        /// </summary>
        public ManifestPackage Build()
        {
            return new ManifestPackage
            {
                Files = _files.ToImmutableDictionary(),
                Metadata = new PackageMetadata
                {
                    Name = _name,
                    Version = _version,
                    Description = _description
                }
            };
        }
    }

    /// <summary>
    /// Builder for manifest policies within a manifest
    /// </summary>
    public class PolicyBuilder
    {
        private readonly List<string> _packages = new();
        private string _selector = ":file";
        private string _maxMemory = "";
        private string _timeout = "";
        private bool _denyAll = false;
        private bool _inheritFromFile = true;
        private ManifestModuleRestriction _modules = ManifestModuleRestriction.None;
        private ManifestCapabilityRestriction _capabilities = ManifestCapabilityRestriction.None;
        private ManifestPathRestriction _paths = ManifestPathRestriction.None;
        private ManifestHostRestriction _hosts = ManifestHostRestriction.None;

        /// <summary>
        /// Adds packages this policy applies to
        /// </summary>
        public PolicyBuilder ForPackages(params string[] packages)
        {
            _packages.AddRange(packages);
            return this;
        }

        /// <summary>
        /// Sets the selector
        /// </summary>
        public PolicyBuilder WithSelector(string selector)
        {
            _selector = selector;
            return this;
        }

        /// <summary>
        /// Sets max memory limit
        /// </summary>
        public PolicyBuilder WithMaxMemory(string maxMemory)
        {
            _maxMemory = maxMemory;
            return this;
        }

        /// <summary>
        /// Sets timeout
        /// </summary>
        public PolicyBuilder WithTimeout(string timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets deny all flag
        /// </summary>
        public PolicyBuilder WithDenyAll(bool denyAll = true)
        {
            _denyAll = denyAll;
            return this;
        }

        /// <summary>
        /// Sets inherit from file flag
        /// </summary>
        public PolicyBuilder WithInheritFromFile(bool inheritFromFile)
        {
            _inheritFromFile = inheritFromFile;
            return this;
        }

        /// <summary>
        /// Sets module restrictions
        /// </summary>
        public PolicyBuilder WithModuleRestriction(bool denyAll, params string[] modules)
        {
            _modules = new ManifestModuleRestriction
            {
                DenyAll = denyAll,
                Modules = modules.ToImmutableArray()
            };
            return this;
        }

        /// <summary>
        /// Sets capability restrictions
        /// </summary>
        public PolicyBuilder WithCapabilityRestriction(bool denyAll, params string[] capabilities)
        {
            _capabilities = new ManifestCapabilityRestriction
            {
                DenyAll = denyAll,
                Capabilities = capabilities.ToImmutableArray()
            };
            return this;
        }

        /// <summary>
        /// Sets path restrictions
        /// </summary>
        public PolicyBuilder WithPathRestriction(bool denyAll, params string[] patterns)
        {
            _paths = new ManifestPathRestriction
            {
                DenyAll = denyAll,
                Patterns = patterns.ToImmutableArray()
            };
            return this;
        }

        /// <summary>
        /// Sets host restrictions
        /// </summary>
        public PolicyBuilder WithHostRestriction(bool denyAll, params string[] patterns)
        {
            _hosts = new ManifestHostRestriction
            {
                DenyAll = denyAll,
                Patterns = patterns.ToImmutableArray()
            };
            return this;
        }

        /// <summary>
        /// Builds the policy
        /// </summary>
        public ManifestPolicy Build()
        {
            return new ManifestPolicy
            {
                Packages = _packages.ToImmutableArray(),
                Selector = _selector,
                MaxMemory = _maxMemory,
                Timeout = _timeout,
                DenyAll = _denyAll,
                InheritFromFile = _inheritFromFile,
                Modules = _modules,
                Capabilities = _capabilities,
                Paths = _paths,
                Hosts = _hosts
            };
        }
    }
}