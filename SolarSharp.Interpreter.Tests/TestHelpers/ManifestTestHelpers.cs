using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Domain;

namespace SolarSharp.Interpreter.Tests.TestHelpers
{
    public static class ManifestTestHelpers
    {
        /// <summary>
        /// Helper method to access the internal trust store of a Script instance for testing
        /// </summary>
        public static ITrustStore GetTrustStore(this Script script)
        {
            var trustStoreField = typeof(Script).GetField("_trustStore", BindingFlags.NonPublic | BindingFlags.Instance);
            return trustStoreField?.GetValue(script) as ITrustStore;
        }

        /// <summary>
        /// Creates a V2.0 manifest for testing (production API replacement)
        /// </summary>
        public static Manifest CreateV2Manifest(
            string manifestId,
            string packageName = null,
            string packageId = null,
            string packageVersion = "1.0.0",
            string packageDescription = "",
            ImmutableDictionary<string, string> files = null,
            ImmutableArray<ManifestPolicy> policies = default,
            string keyId = "",
            string signature = "",
            string publicKey = "",
            string publicKeyToken = "",
            ImmutableArray<string> intermediateCAs = default)
        {
            packageName = packageName ?? packageId ?? "test-package";
            packageId = packageId ?? packageName ?? "test-package";
            
            var manifest = V2ManifestBuilder.CreateUnsigned(manifestId, packageId)
                .WithPackageMetadata(packageId, packageVersion, packageDescription);
                
            // Add files if provided
            if (files != null)
            {
                foreach (var file in files)
                {
                    manifest = manifest.WithFile(packageId, file.Key, file.Value);
                }
            }
            
            // Add policies if provided
            if (!policies.IsDefaultOrEmpty)
            {
                foreach (var policy in policies)
                {
                    manifest = manifest.WithPolicy(policy);
                }
            }

            // Update signature information if provided
            if (!string.IsNullOrEmpty(publicKeyToken) && manifest.SignedContent.Length > 0)
            {
                var updatedBlock = manifest.SignedContent[0] with
                {
                    KeyId = keyId,
                    Signature = signature,
                    PublicKey = publicKey,
                    PublicKeyToken = publicKeyToken,
                    IntermediateCAs = intermediateCAs.IsDefault ? ImmutableArray<string>.Empty : intermediateCAs
                };
                
                manifest = manifest with
                {
                    SignedContent = ImmutableArray.Create(updatedBlock)
                };
            }
            
            return manifest;
        }

        /// <summary>
        /// Creates a test manifest (V2.0)
        /// </summary>
        public static Manifest CreateTestManifest(
            string description = "Test manifest",
            string[] capabilities = null,
            string securityLevel = "Default")
        {
            var manifest = V2ManifestBuilder.CreateUnsigned("test-manifest", "test-package")
                .WithPackageMetadata("test-package", "1.0.0", description);

            if (capabilities != null && capabilities.Length > 0)
            {
                manifest = manifest.WithPolicy(builder => builder
                    .ForPackages("test-package")
                    .DenyAllModulesExcept(capabilities));
            }

            return manifest;
        }

        /// <summary>
        /// Creates a restrictive policy for testing
        /// </summary>
        public static ManifestPolicy CreateRestrictivePolicy(string packageName, string selector = ":file")
        {
            return ManifestPolicyBuilder.Create()
                .ForPackages(packageName)
                .WithSelector(selector)
                .DenySystemModules()
                .DenyDangerousCapabilities()
                .WithMaxMemoryMB(10)
                .WithTimeoutSeconds(5)
                .Build();
        }

        /// <summary>
        /// Creates a permissive policy for testing
        /// </summary>
        public static ManifestPolicy CreatePermissivePolicy(string packageName, string selector = ":file")
        {
            return ManifestPolicyBuilder.Create()
                .ForPackages(packageName)
                .WithSelector(selector)
                .WithMaxMemoryMB(256)
                .WithTimeoutSeconds(30)
                .Build();
        }

        /// <summary>
        /// Creates a default policy for testing
        /// </summary>
        public static ManifestPolicy CreateDefaultPolicy(string packageName, string selector = ":file")
        {
            return ManifestPolicyBuilder.Create()
                .ForPackages(packageName)
                .WithSelector(selector)
                .WithMaxMemoryMB(50)
                .WithTimeoutSeconds(10)
                .Build();
        }

        /// <summary>
        /// Creates test files dictionary
        /// </summary>
        public static ImmutableDictionary<string, string> CreateTestFiles(params (string path, string hash)[] files)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (var (path, hash) in files)
            {
                builder.Add(path, hash);
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Creates a test package
        /// </summary>
        public static ManifestPackage CreateTestPackage(
            string name = "test-package",
            string version = "1.0.0",
            string description = "Test package")
        {
            return new ManifestPackage
            {
                Metadata = new PackageMetadata
                {
                    Name = name,
                    Version = version,
                    Description = description
                },
                Files = ImmutableDictionary<string, string>.Empty
            };
        }

        /// <summary>
        /// Gets the first package name from a manifest
        /// </summary>
        public static string GetFirstPackageName(this Manifest manifest)
        {
            if (manifest.SignedContent.Length > 0 && manifest.SignedContent[0].Packages.Count > 0)
            {
                return manifest.SignedContent[0].Packages.First().Value.Metadata.Name;
            }
            return "unknown";
        }

        /// <summary>
        /// Gets a test public key for testing
        /// </summary>
        public static string GetTestPublicKey()
        {
            var keyPair = new RsaKeyPairGenerator();
            keyPair.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var pair = keyPair.GenerateKeyPair();
            
            using (var textWriter = new System.IO.StringWriter())
            {
                var pemWriter = new PemWriter(textWriter);
                pemWriter.WriteObject(pair.Public);
                return textWriter.ToString();
            }
        }
    }
}