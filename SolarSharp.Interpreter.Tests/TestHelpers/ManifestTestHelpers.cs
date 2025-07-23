#nullable enable
using System.Collections.Immutable;
using System.Linq;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.TestHelpers
{
    /// <summary>
    /// Test helper methods for V2.0 manifests to replace removed compatibility properties
    /// </summary>
    public static class ManifestTestHelpers
    {
        /// <summary>
        /// Gets the name of the first package for testing (replaces manifest.Identity.Name)
        /// </summary>
        public static string GetFirstPackageName(this Manifest manifest)
        {
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            return firstPackage.Package?.Metadata?.Name ?? "";
        }
        
        /// <summary>
        /// Gets the version of the first package for testing (replaces manifest.Identity.Version)
        /// </summary>
        public static string GetFirstPackageVersion(this Manifest manifest)
        {
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            return firstPackage.Package?.Metadata?.Version ?? "1.0.0";
        }
        
        /// <summary>
        /// Gets the description of the first package for testing (replaces manifest.Description)
        /// </summary>
        public static string GetFirstPackageDescription(this Manifest manifest)
        {
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            return firstPackage.Package?.Metadata?.Description ?? "";
        }

        /// <summary>
        /// Legacy Description property for testing compatibility
        /// </summary>
        public static string Description(this Manifest manifest) => manifest.GetFirstPackageDescription();

        /// <summary>
        /// Legacy Policy property for testing compatibility - returns the first policy
        /// </summary>
        public static ManifestPolicy? Policy(this Manifest manifest)
        {
            var firstBlock = manifest.SignedContent.FirstOrDefault();
            return firstBlock?.Policies.FirstOrDefault();
        }

        /// <summary>
        /// Legacy FilePolicies property for testing compatibility - returns empty dictionary for V2.0
        /// </summary>
        public static ImmutableDictionary<string, object> FilePolicies(this Manifest manifest) 
            => ImmutableDictionary<string, object>.Empty;

        /// <summary>
        /// Legacy PolicyDefinitions property for testing compatibility - returns empty dictionary for V2.0
        /// </summary>
        public static ImmutableDictionary<string, object> PolicyDefinitions(this Manifest manifest) 
            => ImmutableDictionary<string, object>.Empty;
        
        /// <summary>
        /// Gets the public key token of the first signed block for testing (replaces manifest.Identity.PublicKeyToken)
        /// </summary>
        public static string GetFirstKeyToken(this Manifest manifest)
        {
            var firstBlock = manifest.SignedContent.FirstOrDefault();
            return firstBlock?.GetKeyFingerprint() ?? "";
        }

        /// <summary>
        /// Creates a test identity info for compatibility
        /// </summary>
        public static ScriptIdentityInfo GetTestIdentity(this Manifest manifest)
        {
            return new ScriptIdentityInfo
            {
                Name = manifest.GetFirstPackageName(),
                Version = manifest.GetFirstPackageVersion(),
                PublicKeyToken = manifest.GetFirstKeyToken()
            };
        }

        /// <summary>
        /// Creates a V2.0 manifest for testing
        /// </summary>
        public static Manifest CreateV2Manifest(
            string packageName,
            string packageVersion = "1.0.0",
            string packageDescription = "Test package",
            string manifestId = "test-manifest",
            string packageId = null,
            ImmutableDictionary<string, string> files = default,
            ImmutableArray<ManifestPolicy> policies = default)
        {
            if (policies.IsDefault)
            {
                policies = ImmutableArray.Create(CreateDefaultPolicy());
            }

            packageId = packageId ?? packageName;
            
            if (files == null || files.IsEmpty)
            {
                files = ImmutableDictionary<string, string>.Empty.Add("test.lua", "sha256:test");
            }

            var package = new ManifestPackage
            {
                Files = files,
                Metadata = new PackageMetadata
                {
                    Name = packageName,
                    Version = packageVersion,
                    Description = packageDescription
                }
            };

            var signedBlock = new SignedContentBlock
            {
                KeyId = "sha256:test",
                Signature = "",
                PublicKey = "",
                Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(packageId, package),
                Policies = policies
            };

            return new Manifest
            {
                Version = "2.0",
                ManifestId = manifestId,
                SignedContent = ImmutableArray.Create(signedBlock)
            };
        }

        /// <summary>
        /// Creates a default policy for testing
        /// </summary>
        public static ManifestPolicy CreateDefaultPolicy(string packageName = "*", string selector = ":file")
        {
            return new ManifestPolicy
            {
                Packages = ImmutableArray.Create(packageName),
                Selector = selector,
                Grant = new PolicyGrant
                {
                    Modules = ImmutableArray.Create("basic", "string")
                },
                Restrict = new PolicyRestrictions
                {
                    MaxMemory = "50MB",
                    Timeout = "30s"
                }
            };
        }

        /// <summary>
        /// Creates a permissive policy for testing
        /// </summary>
        public static ManifestPolicy CreatePermissivePolicy(string packageName = "*", string selector = ":file")
        {
            var basePolicy = CreateDefaultPolicy(packageName, selector);
            return basePolicy with
            {
                Grant = basePolicy.Grant with
                {
                    Capabilities = ImmutableArray.Create("FileRead", "FileWrite"),
                    Modules = ImmutableArray.Create("basic", "string", "math")
                },
                Restrict = basePolicy.Restrict with
                {
                    MaxMemory = "100MB",
                    Timeout = "30s"
                }
            };
        }

        /// <summary>
        /// Creates a restrictive policy for testing
        /// </summary>
        public static ManifestPolicy CreateRestrictivePolicy(string packageName = "*", string selector = ":file")
        {
            var basePolicy = CreateDefaultPolicy(packageName, selector);
            return basePolicy with
            {
                Grant = basePolicy.Grant with
                {
                    Modules = ImmutableArray.Create("basic")
                },
                Restrict = basePolicy.Restrict with
                {
                    MaxMemory = "1MB",
                    Timeout = "1s"
                }
            };
        }

        /// <summary>
        /// Creates test files dictionary for manifest
        /// </summary>
        public static ImmutableDictionary<string, string> CreateTestFiles(params (string fileName, string hash)[] files)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (var (fileName, hash) in files)
            {
                builder[fileName] = hash.StartsWith("sha256:") ? hash : $"sha256:{hash}";
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Creates a test package for manifest testing
        /// </summary>
        public static ManifestPackage CreateTestPackage(
            string packageName = "TestPackage",
            string version = "1.0.0",
            string description = "Test package",
            ImmutableDictionary<string, string> files = default)
        {
            if (files == null || files.IsEmpty)
            {
                files = ImmutableDictionary<string, string>.Empty.Add("test.lua", "sha256:test");
            }

            return new ManifestPackage
            {
                Files = files,
                Metadata = new PackageMetadata
                {
                    Name = packageName,
                    Version = version,
                    Description = description
                }
            };
        }

        /// <summary>
        /// Gets a test public key for testing
        /// </summary>
        public static string GetTestPublicKey()
        {
            return "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...\n-----END PUBLIC KEY-----";
        }

        /// <summary>
        /// Creates a deny-all policy for testing
        /// </summary>
        public static ManifestPolicy CreateDenyAllPolicy(string packageName = "*", string selector = ":file")
        {
            return new ManifestPolicy
            {
                Packages = ImmutableArray.Create(packageName),
                Selector = selector,
                DenyAll = true,
                Grant = new PolicyGrant(),
                Restrict = new PolicyRestrictions()
            };
        }
    }
}