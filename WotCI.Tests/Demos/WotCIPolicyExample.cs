using System;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace WotCI.Tests.Examples
{
    /// <summary>
    /// Example demonstrating WotCI three-tier trust model with the new policy system
    /// </summary>
    [TestFixture]
    public class WotCiPolicyExample
    {
        [Test]
        [Category("Security.Trust")]
        public void WotCI_ThreeTierTrustModel()
        {
            // Create three different certificate public key tokens representing trust tiers
            var systemToken = "1111111111111111111111111111111111111111111111111111111111111111"; // System-level plugins
            var partnerToken = "2222222222222222222222222222222222222222222222222222222222222222"; // Partner plugins
            var communityToken = "3333333333333333333333333333333333333333333333333333333333333333"; // Community plugins

            // Define signature-based policies for each trust tier
            var signaturePolicies = ImmutableDictionary<string, SecurityPolicy>
                .Empty
                // System plugins - highest privileges
                .Add(
                    systemToken,
                    new SecurityPolicy
                    {
                        TimeoutMs = 300_000, // 5 minutes
                        MaxMemoryMB = 1024, // 1GB
                        MaxInstructions = 1_000_000_000_000, // Very high limit
                        MaxCallDepth = 1000,
                        AllowedModules =
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Math
                            | CoreModules.Table
                            | CoreModules.IO
                            | CoreModules.OS_System
                            | CoreModules.OS_Time
                            | CoreModules.Debug,
                        Capabilities =
                            ScriptCapabilities.FileRead
                            | ScriptCapabilities.FileWrite
                            | ScriptCapabilities.NetworkAccess
                            | ScriptCapabilities.EnvironmentAccess,
                        AllowExecution = true,
                        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty.Add(
                            "/*",
                            FilePermissions.ReadWrite
                        ), // Full filesystem access
                        PubSubPermissions = new PubSubPermissions
                        {
                            Publish = ["*"], // Can publish to any topic
                            Subscribe = ["*"], // Can subscribe to any topic
                            Topics = ImmutableDictionary<string, TopicPolicy>.Empty,
                        },
                    }
                )
                // Partner plugins - moderate privileges
                .Add(
                    partnerToken,
                    new SecurityPolicy
                    {
                        TimeoutMs = 60_000, // 1 minute
                        MaxMemoryMB = 256, // 256MB
                        MaxInstructions = 100_000_000,
                        MaxCallDepth = 200,
                        AllowedModules =
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Math
                            | CoreModules.Table
                            | CoreModules.IO
                            | CoreModules.OS_System,
                        Capabilities =
                            ScriptCapabilities.FileRead
                            | ScriptCapabilities.FileWrite
                            | ScriptCapabilities.NetworkAccess,
                        AllowExecution = true,
                        FilePermissions = ImmutableDictionary<string, FilePermissions>
                            .Empty.Add("/game/data/*", FilePermissions.ReadWrite)
                            .Add("/game/config/*", FilePermissions.Read)
                            .Add("/game/logs/*", FilePermissions.ReadWrite),
                        PubSubPermissions = new PubSubPermissions
                        {
                            Publish = ["game.*", "plugin.*"],
                            Subscribe = ["game.*", "system.events.*"],
                            Topics = ImmutableDictionary<string, TopicPolicy>.Empty,
                        },
                    }
                )
                // Community plugins - restricted privileges
                .Add(
                    communityToken,
                    new SecurityPolicy
                    {
                        TimeoutMs = 30_000, // 30 seconds
                        MaxMemoryMB = 128, // 128MB
                        MaxInstructions = 10_000_000,
                        MaxCallDepth = 100,
                        AllowedModules =
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Math
                            | CoreModules.Table,
                        Capabilities = ScriptCapabilities.FileRead,
                        AllowExecution = false, // Cannot execute strings
                        FilePermissions = ImmutableDictionary<string, FilePermissions>
                            .Empty.Add("/game/data/community/*", FilePermissions.Read)
                            .Add("/game/data/user/*", FilePermissions.ReadWrite),
                        PubSubPermissions = new PubSubPermissions
                        {
                            Publish = ["plugin.community.*"],
                            Subscribe = ["game.public.*", "plugin.community.*"],
                            Topics = ImmutableDictionary<string, TopicPolicy>.Empty,
                        },
                    }
                )
                // Unsigned/unknown plugins - minimal privileges
                .Add(
                    "",
                    new SecurityPolicy
                    {
                        TimeoutMs = 5_000, // 5 seconds
                        MaxMemoryMB = 32, // 32MB
                        MaxInstructions = 1_000_000,
                        MaxCallDepth = 50,
                        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
                        Capabilities = ScriptCapabilities.None,
                        AllowExecution = false,
                        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                        PubSubPermissions = new PubSubPermissions(), // No pubsub allowed
                    }
                );

            // Create path-based policies for plugin directories
            var pathPolicies = ImmutableDictionary<string, SecurityPolicy>
                .Empty.Add(
                    "/game/plugins/system/*",
                    new SecurityPolicy
                    {
                        TimeoutMs = 180_000,
                        MaxMemoryMB = 512,
                        MaxInstructions = 500_000_000,
                        MaxCallDepth = 500,
                        AllowedModules =
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Math
                            | CoreModules.Table
                            | CoreModules.IO
                            | CoreModules.OS_System
                            | CoreModules.OS_Time
                            | CoreModules.Debug,
                        Capabilities =
                            ScriptCapabilities.FileRead
                            | ScriptCapabilities.FileWrite
                            | ScriptCapabilities.NetworkAccess,
                        AllowExecution = true,
                        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                        PubSubPermissions = new PubSubPermissions(),
                    }
                )
                .Add(
                    "/game/plugins/community/*",
                    new SecurityPolicy
                    {
                        TimeoutMs = 15_000,
                        MaxMemoryMB = 64,
                        MaxInstructions = 5_000_000,
                        MaxCallDepth = 75,
                        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
                        Capabilities = ScriptCapabilities.None,
                        AllowExecution = false,
                        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                        PubSubPermissions = new PubSubPermissions(),
                    }
                );

            // Example: System plugin manifest with V2.0 format using proper signed content
            var systemPluginManifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "test-coregameengine",
                SignedContent =
                [
                    new SignedContentBlock
                    {
                        KeyId = "system-key",
                        Signature = "dummy-signature",
                        Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(
                            "CoreGameEngine",
                            new ManifestPackage
                            {
                                Metadata = new PackageMetadata
                                {
                                    Name = "CoreGameEngine",
                                    Version = "1.0.0",
                                },
                                Files = ImmutableDictionary<string, string>.Empty.Add(
                                    "init.lua",
                                    "dummy-hash"
                                ),
                            }
                        ),
                        Policies =
                        [
                            new ManifestPolicy
                            {
                                Packages = ["CoreGameEngine"],
                                Selector = "engine-startup",
                                MaxMemory = "1024MB",
                                Timeout = "300s",
                                Modules = ManifestModuleRestriction.None,
                                Capabilities = ManifestCapabilityRestriction.None,
                                Paths = ManifestPathRestriction.None,
                                Hosts = ManifestHostRestriction.None,
                                DenyAll = false,
                                InheritFromFile = true,
                            },
                            new ManifestPolicy
                            {
                                Packages = ["CoreGameEngine"],
                                Selector = "engine-runtime",
                                MaxMemory = "1024MB",
                                Timeout = "300s",
                                Modules = ManifestModuleRestriction.None,
                                Capabilities = ManifestCapabilityRestriction.None,
                                Paths = ManifestPathRestriction.None,
                                Hosts = ManifestHostRestriction.None,
                                DenyAll = false,
                                InheritFromFile = true,
                            },
                        ],
                    },
                ],
            };

            // Example: Partner plugin manifest using V2.0 format
            var partnerPluginManifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "test-combatsystem",
                SignedContent =
                [
                    new SignedContentBlock
                    {
                        KeyId = "partner-key",
                        Signature = "dummy-signature",
                        Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(
                            "CombatSystem",
                            new ManifestPackage
                            {
                                Metadata = new PackageMetadata
                                {
                                    Name = "CombatSystem",
                                    Version = "1.0.0",
                                },
                                Files = ImmutableDictionary<string, string>.Empty,
                            }
                        ),
                        Policies =
                        [
                            new ManifestPolicy
                            {
                                Packages = ["CombatSystem"],
                                Selector = "combat-handler",
                                MaxMemory = "256MB",
                                Timeout = "60s",
                                Modules = new ManifestModuleRestriction
                                {
                                    DenyAll = true,
                                    Modules = ImmutableArray.Create("basic", "string", "math", "table", "io", "os")
                                },
                                Capabilities = new ManifestCapabilityRestriction
                                {
                                    DenyAll = true,
                                    Capabilities = ImmutableArray.Create("FileRead", "FileWrite", "NetworkAccess")
                                },
                                Paths = new ManifestPathRestriction
                                {
                                    DenyAll = true,
                                    Patterns = ImmutableArray.Create("/game/data/*", "/game/config/*", "/game/logs/*")
                                },
                                Hosts = ManifestHostRestriction.None,
                                DenyAll = false,
                                InheritFromFile = true,
                            },
                        ],
                    },
                ],
            };

            // Example: Community plugin manifest with heavy restrictions using V2.0 format
            var communityPluginManifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "test-chatemotes",
                SignedContent =
                [
                    new SignedContentBlock
                    {
                        KeyId = "community-key",
                        Signature = "dummy-signature",
                        Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(
                            "ChatEmotes",
                            new ManifestPackage
                            {
                                Metadata = new PackageMetadata
                                {
                                    Name = "ChatEmotes",
                                    Version = "1.0.0",
                                },
                                Files = ImmutableDictionary<string, string>.Empty,
                            }
                        ),
                        Policies =
                        [
                            new ManifestPolicy
                            {
                                Packages = ["ChatEmotes"],
                                Selector = "emote-processor",
                                MaxMemory = "128MB",
                                Timeout = "30s",
                                Modules = new ManifestModuleRestriction
                                {
                                    DenyAll = true,
                                    Modules = ImmutableArray.Create("basic", "string", "math", "table")
                                },
                                Capabilities = new ManifestCapabilityRestriction
                                {
                                    DenyAll = true,
                                    Capabilities = ImmutableArray.Create("FileRead")
                                },
                                Paths = new ManifestPathRestriction
                                {
                                    DenyAll = true,
                                    Patterns = ImmutableArray.Create("/game/data/community/*", "/game/data/user/*")
                                },
                                Hosts = new ManifestHostRestriction
                                {
                                    DenyAll = true,
                                    Patterns = ImmutableArray<string>.Empty
                                },
                                DenyAll = false,
                                InheritFromFile = true,
                            },
                        ],
                    },
                ],
            };

            // Demonstrate basic policy structures

            // Verify system plugin manifest structure
            Assert.AreEqual("CoreGameEngine", systemPluginManifest.GetFirstPackageName());
            Assert.IsTrue(systemPluginManifest.SignedContent[0].Policies.Any(p => p.Selector == "engine-startup"));
            Assert.IsTrue(systemPluginManifest.SignedContent[0].Policies.Any(p => p.Selector == "engine-runtime"));
            Assert.IsTrue(systemPluginManifest.SignedContent[0].Packages.Values.Any(p => p.Files.ContainsKey("init.lua")));

            // Verify partner plugin manifest structure
            Assert.AreEqual("CombatSystem", partnerPluginManifest.GetFirstPackageName());
            Assert.IsTrue(partnerPluginManifest.SignedContent[0].Policies.Any(p => p.Selector == "combat-handler"));

            // Verify community plugin manifest structure
            Assert.AreEqual("ChatEmotes", communityPluginManifest.GetFirstPackageName());
            Assert.IsTrue(communityPluginManifest.SignedContent[0].Policies.Any(p => p.Selector == "emote-processor"));

            // Verify that policies exist for different trust tiers
            Assert.IsTrue(signaturePolicies.ContainsKey(systemToken));
            Assert.IsTrue(signaturePolicies.ContainsKey(partnerToken));
            Assert.IsTrue(signaturePolicies.ContainsKey(communityToken));
            Assert.IsTrue(signaturePolicies.ContainsKey("")); // Unsigned

            // This demonstrates how the three-tier trust model works:
            // 1. Different policies for system, partner, community and unsigned plugins
            // 2. Manifests define additional granular policies for specific script files
            // 3. SecurityPolicy definitions can be referenced by file patterns
            // 4. Each tier has appropriate capability restrictions
        }
    }

    internal static class TestHelpers
    {
        public static X509Certificate CreateTestCertificate()
        {
            // Create a self-signed certificate for testing using BouncyCastle
            // Generate RSA key pair
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();

            // Create certificate generator
            var certGenerator = new X509V3CertificateGenerator();

            // Set certificate properties
            var subject = new X509Name("CN=Test");
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetIssuerDN(subject); // Self-signed
            certGenerator.SetSerialNumber(BigInteger.One);
            certGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter(DateTime.UtcNow.AddYears(1));
            certGenerator.SetPublicKey(keyPair.Public);

            // Sign certificate with its own private key (self-signed)
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256WithRSA",
                keyPair.Private,
                new SecureRandom()
            );
            return certGenerator.Generate(signatureFactory);
        }
    }
}
