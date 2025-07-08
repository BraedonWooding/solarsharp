#nullable enable
using System.Collections.Immutable;
using System.Linq;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.TestHelpers
{
    /// <summary>
    /// Helper methods for creating V2.0 manifests in tests
    /// </summary>
    public static class ManifestTestHelpers
    {
        /// <summary>
        /// Creates a V2.0 manifest with a single signed content block containing one package and one policy
        /// </summary>
        public static Manifest CreateV2Manifest(
            string manifestId = "test-manifest",
            string packageId = "test-package",
            string packageName = "Test Package",
            string packageVersion = "1.0.0",
            string packageDescription = "Test package description",
            string keyId =
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            string signature = "test-signature",
            ImmutableDictionary<string, string>? files = null,
            ImmutableArray<ManifestPolicy>? policies = null,
            string? publicKey = null
        )
        {
            files ??= ImmutableDictionary<string, string>.Empty;
            policies ??= ImmutableArray.Create(CreateDefaultPolicy(packageId));

            var package = new ManifestPackage
            {
                Files = files,
                Metadata = new PackageMetadata
                {
                    Name = packageName,
                    Version = packageVersion,
                    Description = packageDescription,
                },
            };

            var signedContentBlock = new SignedContentBlock
            {
                KeyId = keyId,
                Signature = signature,
                PublicKey = publicKey ?? GetTestPublicKey(),
                Packages = ImmutableDictionary
                    .Create<string, ManifestPackage>()
                    .Add(packageId, package),
                Policies = policies.Value,
            };

            return new Manifest
            {
                ManifestId = manifestId,
                Version = "2.0",
                SignedContent = ImmutableArray.Create(signedContentBlock),
            };
        }

        /// <summary>
        /// Creates a V2.0 manifest with multiple signed content blocks
        /// </summary>
        public static Manifest CreateMultiBlockV2Manifest(
            string manifestId = "multi-block-manifest",
            params (
                string keyId,
                string signature,
                ImmutableDictionary<string, ManifestPackage> packages,
                ImmutableArray<ManifestPolicy> policies,
                string? publicKey
            )[] blocks
        )
        {
            var signedContentBlocks = blocks
                .Select(block => new SignedContentBlock
                {
                    KeyId = block.keyId,
                    Signature = block.signature,
                    PublicKey = block.publicKey ?? GetTestPublicKey(),
                    Packages = block.packages,
                    Policies = block.policies,
                })
                .ToImmutableArray();

            return new Manifest
            {
                ManifestId = manifestId,
                Version = "2.0",
                SignedContent = signedContentBlocks,
            };
        }

        /// <summary>
        /// Creates a default manifest policy for testing
        /// </summary>
        public static ManifestPolicy CreateDefaultPolicy(
            string packageId,
            string selector = ":file"
        )
        {
            return new ManifestPolicy
            {
                Packages = ImmutableArray.Create(packageId),
                Selector = selector,
                Grant = new PolicyGrant
                {
                    FileRead = ImmutableArray.Create("/app/data/*"),
                    FileWrite = ImmutableArray<string>.Empty,
                    Network = ImmutableArray<string>.Empty,
                    Roles = ImmutableArray<string>.Empty,
                    Capabilities = ImmutableArray<string>.Empty,
                },
                Restrict = new PolicyRestrictions
                {
                    MaxMemory = "64MB",
                    Timeout = "30s",
                    Deny = ImmutableArray<string>.Empty,
                    InheritFromFile = true,
                },
                DenyIfSignedBy = ImmutableArray<string>.Empty,
                DenyAll = false,
            };
        }

        /// <summary>
        /// Creates a restrictive policy that denies execution
        /// </summary>
        public static ManifestPolicy CreateRestrictivePolicy(string packageId)
        {
            return new ManifestPolicy
            {
                Packages = ImmutableArray.Create(packageId),
                Selector = ":file",
                Grant = new PolicyGrant() { Modules = ImmutableArray.Create("basic") },
                Restrict = new PolicyRestrictions
                {
                    MaxMemory = "1MB",
                    Timeout = "1s",
                    Deny = ImmutableArray.Create("eval", "io", "network"),
                    InheritFromFile = false,
                },
                DenyIfSignedBy = ImmutableArray<string>.Empty,
                DenyAll = true,
            };
        }

        /// <summary>
        /// Creates a permissive policy for testing
        /// </summary>
        public static ManifestPolicy CreatePermissivePolicy(string packageId)
        {
            return new ManifestPolicy
            {
                Packages = ImmutableArray.Create(packageId),
                Selector = ":file",
                Grant = new PolicyGrant
                {
                    FileRead = ImmutableArray.Create("/**"),
                    FileWrite = ImmutableArray.Create("/temp/**"),
                    Network = ImmutableArray.Create("*"),
                    Roles = ImmutableArray.Create("admin"),
                    Capabilities = ImmutableArray.Create("eval", "reflection", "io", "network"),
                },
                Restrict = new PolicyRestrictions
                {
                    MaxMemory = "1GB",
                    Timeout = "10m",
                    Deny = ImmutableArray<string>.Empty,
                    InheritFromFile = true,
                },
                DenyIfSignedBy = ImmutableArray<string>.Empty,
                DenyAll = false,
            };
        }

        /// <summary>
        /// Creates a test package with specified files
        /// </summary>
        public static ManifestPackage CreateTestPackage(
            string name = "Test Package",
            string version = "1.0.0",
            string description = "Test package",
            ImmutableDictionary<string, string>? files = null
        )
        {
            files ??= ImmutableDictionary
                .Create<string, string>()
                .Add("test.lua", "sha256:test-file-hash");

            return new ManifestPackage
            {
                Files = files,
                Metadata = new PackageMetadata
                {
                    Name = name,
                    Version = version,
                    Description = description,
                },
            };
        }

        /// <summary>
        /// Creates a legacy V1.0 compatible manifest identity for testing
        /// </summary>
        public static SolarSharp.Interpreter.Security.ScriptIdentityInfo CreateV2IdentityInfo(
            string name = "Test Script",
            string version = "1.0.0",
            string description = "Test script description",
            string publisher = "Test Publisher"
        )
        {
            return new SolarSharp.Interpreter.Security.ScriptIdentityInfo
            {
                Name = name,
                Version = version,
            };
        }

        /// <summary>
        /// Creates files dictionary for testing
        /// </summary>
        public static ImmutableDictionary<string, string> CreateTestFiles(
            params (string path, string hash)[] files
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (var (path, hash) in files)
            {
                var fullHash = hash.StartsWith("sha256:") ? hash : $"sha256:{hash}";
                builder.Add(path, fullHash);
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Creates a security policy dictionary for V1.0 compatibility
        /// </summary>
        public static ImmutableDictionary<string, SecurityPolicy> CreateLegacyPolicyDefinitions(
            params (string name, SecurityPolicy policy)[] policies
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();
            foreach (var (name, policy) in policies)
            {
                builder.Add(name, policy);
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Creates a file policies mapping for V1.0 compatibility
        /// </summary>
        public static ImmutableDictionary<string, string> CreateLegacyFilePolicies(
            params (string filePattern, string policyName)[] mappings
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (var (filePattern, policyName) in mappings)
            {
                builder.Add(filePattern, policyName);
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Gets a default test public key in PEM format for manifest signing tests
        /// </summary>
        public static string GetTestPublicKey()
        {
            return @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0vx7agoebGcQSuuPiLJX
ZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tS
oc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt
7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0
zgdAZHzu6qMQvRL5hajrn1n91CbOpbISO3-61FnQwB_h3BnyGYCtF3MQ_Bz8xgH1
5uq6O1P9_D5WTsQ0QyLvqI3bYhWEKk5Pr5vZmWqKKqQKhP0nHKRl2RbdXKBCTwaq
JQIDAQAB
-----END PUBLIC KEY-----";
        }
    }
}
