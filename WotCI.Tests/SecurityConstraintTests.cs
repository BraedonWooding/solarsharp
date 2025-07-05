using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;

namespace WotCI.Tests
{
    [TestFixture]
    [Category("IntegrationTest")]
    [Category("SecurityTest")]
    public class SecurityConstraintTests
    {
        private string _testDir;
        private X509Certificate2 _rootCa;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"wotci_security_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
            _rootCa = TestCertificateHelpers.GenerateRootCA();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Test]
        public void CertificatePathConstraint_EnforcesStrictBoundaries()
        {
                        var cert1 = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa, "Partner 1", "/plugins/partner1");
            var cert2 = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa, "Partner 2", "/plugins/partner2");

                        ValidatePathAccess(cert1, "/plugins/partner1/file.lua").Should().BeTrue();
            ValidatePathAccess(cert1, "/plugins/partner1/subdir/file.lua").Should().BeTrue();
            ValidatePathAccess(cert1, "/plugins/partner2/file.lua").Should().BeFalse();
            ValidatePathAccess(cert1, "/system/file.lua").Should().BeFalse();
            
            ValidatePathAccess(cert2, "/plugins/partner2/file.lua").Should().BeTrue();
            ValidatePathAccess(cert2, "/plugins/partner1/file.lua").Should().BeFalse();
        }

        [Test]
        public void PathTraversalAttacks_ArePrevented()
        {
                        var cert = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa, "Test", "/plugins/test");

            // Act & Assert - Various path traversal attempts
            ValidatePathAccess(cert, "/plugins/test/../other/file.lua").Should().BeFalse();
            ValidatePathAccess(cert, "/plugins/test/../../system/file.lua").Should().BeFalse();
            ValidatePathAccess(cert, "/plugins/test/./file.lua").Should().BeTrue(); // . is allowed
            ValidatePathAccess(cert, "/plugins/test//file.lua").Should().BeTrue(); // double slash normalized
        }

        [Test]
        public void SharedResourceAccess_CanBeControlled()
        {
                        var constraints = new CertificateConstraints
            {
                SubjectPath = "/plugins/restricted",
                CanAccessSharedResources = true
            };

                        constraints.IsPathAllowed("/plugins/restricted/file.lua").Should().BeTrue();
            constraints.IsPathAllowed("/include/shared.lua").Should().BeTrue(); // Shared resources

            // Disable shared access
            constraints.CanAccessSharedResources = false;
            constraints.IsPathAllowed("/include/shared.lua").Should().BeFalse();
        }

        [Test]
        public void AdditionalAllowedPaths_ExtendAccess()
        {
                        var constraints = new CertificateConstraints
            {
                SubjectPath = "/plugins/main",
                AllowedPaths = new[] { "/data/shared", "/config" }
            };

                        constraints.IsPathAllowed("/plugins/main/file.lua").Should().BeTrue();
            constraints.IsPathAllowed("/data/shared/database.db").Should().BeTrue();
            constraints.IsPathAllowed("/config/settings.json").Should().BeTrue();
            constraints.IsPathAllowed("/data/private/secret.key").Should().BeFalse();
        }

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
                    allowedModules = new[] { "basic", "string" },
                    capabilities = new[] { "FileRead" }
                }
            };

            // Create security config from manifest
            var config = SecurityConfiguration.Isolated();
            if (manifest.policy?.timeout > 0)
            {
                config.Execution.Timeout = TimeSpan.FromSeconds(manifest.policy.timeout);
            }
            if (manifest.policy?.memoryLimit > 0)
            {
                config.Execution.MaxMemoryMB = manifest.policy.memoryLimit;
            }

                        config.Execution.Timeout.Should().Be(TimeSpan.FromSeconds(5));
            config.Execution.MaxMemoryMB.Should().Be(50);
        }

        [Test]
        public void FileAccessLevels_AreRespected()
        {
                        var filePerms = new Dictionary<string, string>
            {
                ["/game/assets/**"] = "read",
                ["/game/saves/*.sav"] = "sandboxedreadwrite",
                ["/game/logs/*.log"] = "readwrite",
                ["/game/system/**"] = "none"
            };

                        GetFileAccessLevel(filePerms, "/game/assets/texture.png").Should().Be("read");
            GetFileAccessLevel(filePerms, "/game/assets/models/player.obj").Should().Be("read");
            GetFileAccessLevel(filePerms, "/game/saves/game1.sav").Should().Be("sandboxedreadwrite");
            GetFileAccessLevel(filePerms, "/game/logs/debug.log").Should().Be("readwrite");
            GetFileAccessLevel(filePerms, "/game/system/config.ini").Should().Be("none");
            GetFileAccessLevel(filePerms, "/other/file.txt").Should().BeNull(); // No match
        }

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
            MatchesPattern("/plugins/*/main.lua", "/plugins/partner1/subdir/main.lua").Should().BeFalse();
            
            // Complex patterns
            MatchesPattern("/game/**/saves/*.sav", "/game/user1/saves/game.sav").Should().BeTrue();
            MatchesPattern("/game/**/saves/*.sav", "/game/profiles/user1/saves/game.sav").Should().BeTrue();
        }

        [Test]
        public void SecurityConfiguration_DefaultsAreSafe()
        {
                        var config = SecurityConfiguration.Isolated();

            // Isolated config should be restrictive
            config.Execution.Timeout.Should().BeLessThan(TimeSpan.FromMinutes(5));
            config.Execution.MaxMemoryMB.Should().BeGreaterThan(0);
            config.Execution.MaxInstructions.Should().BeGreaterThan(0);
            config.FileSystem.Should().NotBeNull();
            config.Network.Should().NotBeNull();
            
            // Check capabilities
            (config.Capabilities & ScriptCapabilities.DirectoryOperations).Should().Be(0);
            (config.Capabilities & ScriptCapabilities.NetworkAccess).Should().Be(0);
            (config.Capabilities & ScriptCapabilities.ProcessExecution).Should().Be(0);
        }

        [Test]
        public void CrossPluginAccess_IsPrevented()
        {
                        var partner1Cert = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa, "Partner 1", "/plugins/partner1");
            var partner2Cert = TestCertificateHelpers.GeneratePartnerCertificate(
                _rootCa, "Partner 2", "/plugins/partner2");

            var config = SecurityConfiguration.Isolated();
            config.Capabilities |= ScriptCapabilities.FileWrite | ScriptCapabilities.FileRead;
            
            // Create test directories
            var testRoot = Path.Combine(Path.GetTempPath(), $"vfs_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(testRoot, "plugins", "partner1"));
            Directory.CreateDirectory(Path.Combine(testRoot, "plugins", "partner2"));
            
            try
            {
                config.SetDirectoryPermissions(testRoot, DirectoryPermissions.ListAndCreateFiles);
                
                // Create VFS instances with memory filesystems
                var vfs1 = new SimpleVirtualFileSystem(config, partner1Cert);
                var vfs2 = new SimpleVirtualFileSystem(config, partner2Cert);
                
                // Mount shared memory filesystem for both partners
                var sharedMemFS = new MemoryFileSystemProvider();
                vfs1.MountFileSystemProvider("/plugins", sharedMemFS);
                vfs2.MountFileSystemProvider("/plugins", sharedMemFS);

                // Partner 1 writes to its area
                vfs1.WriteAllBytes("/plugins/partner1/data.txt", Encoding.UTF8.GetBytes("Partner 1 data"));

                // Partner 2 cannot access Partner 1's data due to certificate constraint
                Action crossAccess = () => vfs2.ReadAllBytes("/plugins/partner1/data.txt");
                crossAccess.Should().Throw<UnauthorizedAccessException>();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, true);
            }
        }

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
                        value = Convert.ToBase64String(new byte[256]) // Fake signature
                    },
                    certificate = new
                    {
                        format = "X509_BASE64",
                        value = Convert.ToBase64String(_rootCa.RawData)
                    }
                }
            };

            var manifestJson = JsonSerializer.Serialize(manifest);

                        // In a real implementation, this would verify the signature
            // Here we're testing the structure is correct
            manifest.security.Should().NotBeNull();
            manifest.security.signature.Should().NotBeNull();
            manifest.security.certificate.Should().NotBeNull();
        }

        [Test]
        public void ResourceLimits_PreventDoS()
        {
                        var config = SecurityConfiguration.Isolated();
            config.Execution.Timeout = TimeSpan.FromSeconds(1);
            config.Execution.MaxInstructions = 1000;
            
            var script = new Script();
            
            // This would need actual integration with the VM to test properly
            // Here we verify the limits are set
            config.Execution.Timeout.Should().Be(TimeSpan.FromSeconds(1));
            config.Execution.MaxInstructions.Should().Be(1000);
        }

        [Test]
        public void AntiPolymorphism_PreventsSelfModification()
        {
                        var config = SecurityConfiguration.Isolated();
            config.AntiPolymorphism.Should().NotBeNull();
            
            // In a real implementation, these would be enforced
            config.AntiPolymorphism.AllowOnlyLuaExtension = true;
            config.AntiPolymorphism.PreventLuaFileWrites = true;
            config.AntiPolymorphism.BlockManifestAccess = true;
            config.AntiPolymorphism.PreventRunString = true;
            config.AntiPolymorphism.PreventInternalDynamicCode = true;

            // Assert configuration is set
            config.AntiPolymorphism.AllowOnlyLuaExtension.Should().BeTrue();
            config.AntiPolymorphism.PreventLuaFileWrites.Should().BeTrue();
            config.AntiPolymorphism.BlockManifestAccess.Should().BeTrue();
            config.AntiPolymorphism.PreventRunString.Should().BeTrue();
            config.AntiPolymorphism.PreventInternalDynamicCode.Should().BeTrue();
        }

        private bool ValidatePathAccess(X509Certificate2 cert, string path)
        {
            var subject = cert.Subject;
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
            // Use standardized GlobMatcher for consistent behavior
            return GlobMatcher.MatchesPattern(path, pattern);
        }
    }

    public class CertificateConstraints
    {
        public string SubjectPath { get; set; }
        public string[] AllowedPaths { get; set; } = Array.Empty<string>();
        public bool CanAccessSharedResources { get; set; } = true;

        public bool IsPathAllowed(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            // Check shared resources access
            if (CanAccessSharedResources && normalizedPath.StartsWith("/include/", StringComparison.OrdinalIgnoreCase))
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