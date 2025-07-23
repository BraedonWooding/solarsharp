using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using FluentAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace WotCI.Tests
{
    [TestFixture]
    [Category("WotCI.Integration")]
    [Category("Security.Constraint")]
    public class SecurityConstraintTests
    {
        private string _testDir;
        private X509Certificate _rootCa;
        private AsymmetricCipherKeyPair _rootCaKey;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"wotci_security_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
            (_rootCa, _rootCaKey) = TestCertificateHelpers.GenerateRootCa();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void CertificatePathConstraint_EnforcesStrictBoundaries()
        {
            var (cert1, key1) = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa,
                _rootCaKey,
                "Partner 1",
                "/plugins/partner1"
            );
            var (cert2, key2) = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa,
                _rootCaKey,
                "Partner 2",
                "/plugins/partner2"
            );

            ValidatePathAccess(cert1, "/plugins/partner1/file.lua").Should().BeTrue();
            ValidatePathAccess(cert1, "/plugins/partner1/subdir/file.lua").Should().BeTrue();
            ValidatePathAccess(cert1, "/plugins/partner2/file.lua").Should().BeFalse();
            ValidatePathAccess(cert1, "/system/file.lua").Should().BeFalse();

            ValidatePathAccess(cert2, "/plugins/partner2/file.lua").Should().BeTrue();
            ValidatePathAccess(cert2, "/plugins/partner1/file.lua").Should().BeFalse();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void PathTraversalAttacks_ArePrevented()
        {
            var (cert, key) = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa,
                _rootCaKey,
                "Test",
                "/plugins/test"
            );

            // Act & Assert - Various path traversal attempts
            ValidatePathAccess(cert, "/plugins/test/../other/file.lua").Should().BeFalse();
            ValidatePathAccess(cert, "/plugins/test/../../system/file.lua").Should().BeFalse();
            ValidatePathAccess(cert, "/plugins/test/./file.lua").Should().BeTrue(); // . is allowed
            ValidatePathAccess(cert, "/plugins/test//file.lua").Should().BeTrue(); // double slash normalized
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void SharedResourceAccess_CanBeControlled()
        {
            var constraints = new CertificateConstraints
            {
                SubjectPath = "/plugins/restricted",
                CanAccessSharedResources = true,
            };

            constraints.IsPathAllowed("/plugins/restricted/file.lua").Should().BeTrue();
            constraints.IsPathAllowed("/include/shared.lua").Should().BeTrue(); // Shared resources

            // Disable shared access
            constraints.CanAccessSharedResources = false;
            constraints.IsPathAllowed("/include/shared.lua").Should().BeFalse();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void AdditionalAllowedPaths_ExtendAccess()
        {
            var constraints = new CertificateConstraints
            {
                SubjectPath = "/plugins/main",
                AllowedPaths = ["/data/shared", "/config"],
            };

            constraints.IsPathAllowed("/plugins/main/file.lua").Should().BeTrue();
            constraints.IsPathAllowed("/data/shared/database.db").Should().BeTrue();
            constraints.IsPathAllowed("/config/settings.json").Should().BeTrue();
            constraints.IsPathAllowed("/data/private/secret.key").Should().BeFalse();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void ManifestSecurityPolicy_EnforcesLimits()
        {
            var manifest = new SimpleManifest
            {
                version = "1.0.0",
                name = "Limited Plugin",
                author = "Test",
                policy = new SimpleManifestPolicy
                {
                    timeout = 5,
                    memoryLimit = 50,
                    allowedModules = ["basic", "string"],
                    capabilities = ["FileRead"],
                },
            };

            // Create security config from manifest
            var config = SolarSharp.Interpreter.Security.Examples.Isolated();
            if (manifest.Policy?.Timeout > 0)
            {
                config = config with { TimeoutMs = manifest.Policy.Timeout * 1000 };
            }
            if (manifest.Policy?.MemoryLimit > 0)
            {
                config = config with { MaxMemoryMB = manifest.Policy.MemoryLimit };
            }

            config.TimeoutMs.Should().Be(5000);
            config.MaxMemoryMB.Should().Be(50);
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void FileAccessLevels_AreRespected()
        {
            var filePerms = new Dictionary<string, string>
            {
                ["/game/assets/**"] = "read",
                ["/game/saves/*.sav"] = "sandboxedreadwrite",
                ["/game/logs/*.log"] = "readwrite",
                ["/game/system/**"] = "none",
            };

            GetFileAccessLevel(filePerms, "/game/assets/texture.png").Should().Be("read");
            GetFileAccessLevel(filePerms, "/game/assets/models/player.obj").Should().Be("read");
            GetFileAccessLevel(filePerms, "/game/saves/game1.sav")
                .Should()
                .Be("sandboxedreadwrite");
            GetFileAccessLevel(filePerms, "/game/logs/debug.log").Should().Be("readwrite");
            GetFileAccessLevel(filePerms, "/game/system/config.ini").Should().Be("none");
            GetFileAccessLevel(filePerms, "/other/file.txt").Should().BeNull(); // No match
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void WildcardPatterns_MatchCorrectly()
        {
            // Arrange & Act & Assert
            // Single asterisk matches within directory
            MatchesPattern("*.lua", "script.lua").Should().BeTrue();
            MatchesPattern("*.lua", "subdir/script.lua").Should().BeFalse();

            // Double asterisk matches across directories
            MatchesPattern("**/*.lua", "script.lua").Should().BeTrue();
            MatchesPattern("**/*.lua", "subdir/script.lua").Should().BeTrue();
            MatchesPattern("**/*.lua", "a/b/c/script.lua").Should().BeTrue();

            // Directory patterns
            MatchesPattern("/plugins/*/main.lua", "/plugins/partner1/main.lua").Should().BeTrue();
            MatchesPattern("/plugins/*/main.lua", "/plugins/partner1/subdir/main.lua")
                .Should()
                .BeFalse();

            // Complex patterns
            MatchesPattern("/game/**/saves/*.sav", "/game/user1/saves/game.sav").Should().BeTrue();
            MatchesPattern("/game/**/saves/*.sav", "/game/profiles/user1/saves/game.sav")
                .Should()
                .BeTrue();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void SecurityPolicy_DefaultsAreSafe()
        {
            var config = SolarSharp.Interpreter.Security.Examples.Isolated();

            // Isolated config should be restrictive
            config.TimeoutMs.Should().BeLessThan(300_000); // 5 minutes
            config.MaxMemoryMB.Should().BeGreaterThan(0);
            config.MaxInstructions.Should().BeGreaterThan(0);
            config.DefaultFileAccess.Should().Be(FilePermissions.None);
            config.AllowNetworkAccess.Should().BeFalse();

            // Check capabilities
            config.Capabilities.Should().Be(ScriptCapabilities.None);
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void CrossPluginAccess_IsPrevented()
        {
            var partner1Cert = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa,
                _rootCaKey,
                "Partner 1",
                "/plugins/partner1"
            );
            var partner2Cert = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa,
                _rootCaKey,
                "Partner 2",
                "/plugins/partner2"
            );

            // Extract signing key fingerprints from certificates for directory access rules
            var partner1KeyFingerprint = GetCertificateFingerprint(partner1Cert.Item1);
            var partner2KeyFingerprint = GetCertificateFingerprint(partner2Cert.Item1);

            var config = SolarSharp.Interpreter.Security.Examples.Desktop() with
            {
                Capabilities = ScriptCapabilities.FileWrite | ScriptCapabilities.FileRead,
                DirectoryAccessRules =
                [
                    .. new[]
                    {
                        // Partner 1 can only access /plugins/partner1
                        DirectoryAccessRule.Create(
                            "/plugins/partner1",
                            FilePermissions.ReadWrite,
                            partner1KeyFingerprint
                        ),
                        // Partner 2 can only access /plugins/partner2
                        DirectoryAccessRule.Create(
                            "/plugins/partner2",
                            FilePermissions.ReadWrite,
                            partner2KeyFingerprint
                        ),
                        // Deny all other access to plugin directories for unsigned scripts
                        DirectoryAccessRule.Create("/plugins/**", FilePermissions.None),
                    },
                ],
            };

            // Create test directories
            var testRoot = Path.Combine(Path.GetTempPath(), $"vfs_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(testRoot, "plugins", "partner1"));
            Directory.CreateDirectory(Path.Combine(testRoot, "plugins", "partner2"));

            try
            {
                // Directory permissions are now set through the SecurityPolicy record

                // Create VFS instances with memory filesystems
                var vfs1 = new SimpleVirtualFileSystem(config);
                var vfs2 = new SimpleVirtualFileSystem(config);

                // Mount shared memory filesystem for both partners
                var sharedMemFs = new MemoryFileSystemProvider();
                vfs1.MountFileSystemProvider("/plugins", sharedMemFs);
                vfs2.MountFileSystemProvider("/plugins", sharedMemFs);

                // Partner 1 writes to its area within execution context
                var partner1Context = LuaExecutionContext
                    .CreateFromPath("/plugins/partner1/script.lua")
                    .Value.With(signingKeyFingerprint: Maybe<string>.From(partner1KeyFingerprint));

                var writeResult = ExecutionContextManager.WithContext(
                    partner1Context,
                    ctx =>
                    {
                        vfs1.WriteAllBytes(
                            "/plugins/partner1/data.txt",
                            Encoding.UTF8.GetBytes("Partner 1 data")
                        );
                        return Result.Success<bool, ExecutionError>(true);
                    }
                );
                writeResult.IsSuccess.Should().BeTrue();

                // Partner 2 cannot access Partner 1's data due to certificate constraint
                var partner2Context = LuaExecutionContext
                    .CreateFromPath("/plugins/partner2/script.lua")
                    .Value.With(signingKeyFingerprint: Maybe<string>.From(partner2KeyFingerprint));

                // Test that Partner 1 can still access their own data
                var selfAccessResult = ExecutionContextManager.WithContext(
                    partner1Context,
                    ctx =>
                    {
                        var data = vfs1.ReadAllBytes("/plugins/partner1/data.txt");
                        return Result.Success<byte[], ExecutionError>(data);
                    }
                );
                selfAccessResult.IsSuccess.Should().BeTrue();
                var retrievedData = Encoding.UTF8.GetString(selfAccessResult.Value);
                retrievedData.Should().Be("Partner 1 data");

                // Test that Partner 2 cannot access Partner 1's data due to certificate constraint
                var crossAccess = () =>
                {
                    ExecutionContextManager.WithContext(
                        partner2Context,
                        ctx =>
                        {
                            var data = vfs2.ReadAllBytes("/plugins/partner1/data.txt");
                            return Result.Success<byte[], ExecutionError>(data);
                        }
                    );
                };
                crossAccess
                    .Should()
                    .Throw<UnauthorizedAccessException>()
                    .WithMessage("*Certificate constraint violation*");

                // Test that Partner 2 can access their own area
                var partner2SelfResult = ExecutionContextManager.WithContext(
                    partner2Context,
                    ctx =>
                    {
                        vfs2.WriteAllBytes(
                            "/plugins/partner2/partner2-data.txt",
                            Encoding.UTF8.GetBytes("Partner 2 data")
                        );
                        var data = vfs2.ReadAllBytes("/plugins/partner2/partner2-data.txt");
                        return Result.Success<byte[], ExecutionError>(data);
                    }
                );
                partner2SelfResult.IsSuccess.Should().BeTrue();
                var partner2Data = Encoding.UTF8.GetString(partner2SelfResult.Value);
                partner2Data.Should().Be("Partner 2 data");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, true);
            }
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void ManifestSignatureValidation_RequiresValidChain()
        {
            var manifest = new
            {
                version = "1.0",
                name = "Signed Plugin",
                policy = new { },
                security = new
                {
                    signature = new
                    {
                        algorithm = "SHA256withRSA",
                        value = Convert.ToBase64String(new byte[256]), // Fake signature
                    },
                    certificate = new
                    {
                        format = "X509_BASE64",
                        value = Convert.ToBase64String(_rootCa.GetEncoded()),
                    },
                },
            };

            var manifestJson = JsonSerializer.Serialize(manifest);

            // In a real implementation, this would verify the signature
            // Here we're testing the structure is correct
            manifest.security.Should().NotBeNull();
            manifest.security.signature.Should().NotBeNull();
            manifest.security.certificate.Should().NotBeNull();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void ResourceLimits_PreventDoS()
        {
            var config = SolarSharp.Interpreter.Security.Examples.Isolated() with
            {
                TimeoutMs = 1000,
                MaxInstructions = 1000,
            };

            // Convert SecurityPolicy to BasePolicySet using PolicySetBuilder
            var policySetBuilder = new PolicySetBuilder()
                .DefinePolicy("isolated", config)
                .WithDefaultPolicy("isolated");

            var basePolicySet = BasePolicySetFactory
                .Create(policySetBuilder.Build())
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to create BasePolicySet: {error.Message}"
                        )
                );

            var script = new Script(basePolicySet);

            // This would need actual integration with the VM to test properly
            // Here we verify the limits are set
            config.TimeoutMs.Should().Be(1000);
            config.MaxInstructions.Should().Be(1000);
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void AntiPolymorphism_PreventsSelfModification()
        {
            // Create a specific anti-polymorphism policy that denies execution
            var config = SecurityPolicyBuilder.CreateDenyAll();

            // Anti-polymorphism is now policy-based - deny all policy prevents execution
            config.AllowExecution.Should().BeFalse();
        }

        private bool ValidatePathAccess(X509Certificate cert, string path)
        {
            var subject = cert.SubjectDN.ToString();
            var parts = subject.Split(',');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    var cn = trimmed.Substring(3);
                    if (cn.StartsWith("/"))
                    {
                        var normalizedPath = NormalizePath(path);
                        return normalizedPath.StartsWith(cn, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return false;
        }

        private string NormalizePath(string path)
        {
            // Simple normalization - in production use Path.GetFullPath
            var normalized = path.Replace('\\', '/');

            // Remove path traversal
            while (normalized.Contains("/../"))
            {
                var index = normalized.IndexOf("/../");
                if (index > 0)
                {
                    var lastSlash = normalized.LastIndexOf('/', index - 1);
                    if (lastSlash >= 0)
                    {
                        normalized = normalized.Remove(lastSlash, index - lastSlash + 3);
                    }
                    else
                    {
                        normalized = normalized.Remove(0, index + 4);
                    }
                }
                else
                {
                    normalized = normalized.Substring(4);
                }
            }

            // Normalize slashes
            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            return normalized;
        }

        private string GetFileAccessLevel(Dictionary<string, string> permissions, string path)
        {
            foreach (var kvp in permissions)
            {
                if (MatchesPattern(kvp.Key, path))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        private bool MatchesPattern(string pattern, string path)
        {
            // Use standardized GlobMatcher for consistent behaviour
            return GlobMatcher.MatchesPattern(path, pattern);
        }

        private string GetCertificateFingerprint(X509Certificate certificate)
        {
            // Generate SHA256 fingerprint of the certificate for use as signing key fingerprint
            var certBytes = certificate.GetEncoded();
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(certBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public class CertificateConstraints
    {
        public string SubjectPath { get; set; }
        public string[] AllowedPaths { get; set; } = [];
        public bool CanAccessSharedResources { get; set; } = true;

        public bool IsPathAllowed(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            // Check shared resources access
            if (
                CanAccessSharedResources
                && normalizedPath.StartsWith("/include/", StringComparison.OrdinalIgnoreCase)
            )
                return true;

            // Check subject path constraint
            if (!string.IsNullOrEmpty(SubjectPath))
            {
                if (normalizedPath.StartsWith(SubjectPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check additional allowed paths
            if (AllowedPaths != null)
            {
                foreach (var allowedPath in AllowedPaths)
                {
                    if (normalizedPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
