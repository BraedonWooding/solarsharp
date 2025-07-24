using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using SolarSharp.Interpreter.Security.Events;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Events;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for manifest permission scoping functionality ensuring manifests
    /// can only define permissions within their own directory.
    /// </summary>
    [TestFixture]
    [Category("Security.ManifestScoping")]
    public class ManifestPermissionScopingTests
    {
        private IFileSystem _fileSystem;
        private EventDrivenManifestValidator _validator;
        private IEventPublisher<ManifestValidationEvent> _eventPublisher;
        private ISignatureValidator _signatureValidator;
        private IManifestDiscoveryService _discoveryService;
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _fileSystem = new MockFileSystem();
            _tempDir = "/test/manifests";
            _fileSystem.Directory.CreateDirectory(_tempDir);

            _eventPublisher = new MockEventPublisher<ManifestValidationEvent>();
            _signatureValidator = new MockSignatureValidator();
            _discoveryService = new MockManifestDiscoveryService();

            _validator = new EventDrivenManifestValidator(
                _fileSystem,
                _discoveryService,
                _signatureValidator
            );
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_FilePermissionsInSameDirectory_Succeeds()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var filePolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("script.lua", "config.json")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-same-dir",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for same directory permissions",
                policies: ImmutableArray.Create(filePolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_FilePermissionsOutsideDirectory_Fails()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "app", "LuaManifest.json");
            _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));

            var filePolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("../secret.txt")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-outside-dir",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for outside directory permissions",
                policies: ImmutableArray.Create(filePolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.Type,
                    Is.EqualTo(ManifestValidationErrorType.PermissionScopeViolation)
                );
                Assert.That(result.Error.Message, Does.Contain("outside manifest directory"));
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_AbsolutePathOutsideDirectory_Fails()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "app", "LuaManifest.json");
            _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));

            var filePolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("/etc/passwd")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-absolute-path",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for absolute path permissions",
                policies: ImmutableArray.Create(filePolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.Type,
                    Is.EqualTo(ManifestValidationErrorType.PermissionScopeViolation)
                );
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_DirectoryPermissionsInScope_Succeeds()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var directoryPolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("data/*", "cache/*")
                },
                Capabilities = new ManifestCapabilityRestriction
                {
                    DenyAll = true,
                    Capabilities = ImmutableArray.Create("directory-operations")
                },
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-directory-perms",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for directory permissions",
                policies: ImmutableArray.Create(directoryPolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_DirectoryPermissionsOutsideScope_Fails()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "module", "LuaManifest.json");
            _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));

            var directoryPolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("../../system/*")
                },
                Capabilities = new ManifestCapabilityRestriction
                {
                    DenyAll = true,
                    Capabilities = ImmutableArray.Create("directory-operations")
                },
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-directory-outside",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for directory outside scope",
                policies: ImmutableArray.Create(directoryPolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.Type,
                    Is.EqualTo(ManifestValidationErrorType.PermissionScopeViolation)
                );
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_WildcardPatternsWithinScope_Succeeds()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var filePolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("*.lua", "data/*.json")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-wildcard-patterns",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for wildcard patterns",
                policies: ImmutableArray.Create(filePolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_WildcardPatternsOutsideScope_Fails()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "plugin", "LuaManifest.json");
            _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));

            var filePolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("../*.conf")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-wildcard-outside",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for wildcard outside scope",
                policies: ImmutableArray.Create(filePolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsFailure, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_EmptyManifestPath_ReturnsError()
        {
            // Arrange
            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "empty-path-test",
                packageName: "EmptyPathTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest with empty path"
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, "");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.Type,
                    Is.EqualTo(ManifestValidationErrorType.InvalidFormat)
                );
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_NullPolicy_Succeeds()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-null-policy",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest with no policies",
                policies: ImmutableArray<ManifestPolicy>.Empty
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_CurrentDirectoryPattern_Succeeds()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var filePolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create(".", "./data")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-current-dir",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for current directory permissions",
                policies: ImmutableArray.Create(filePolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_MixedValidAndInvalidPatterns_FailsOnFirst()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var mixedPolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create(
                        "valid.lua",
                        "../invalid.lua",
                        "alsoValid.lua"
                    )
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-mixed-patterns",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for mixed valid/invalid patterns",
                policies: ImmutableArray.Create(mixedPolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert
            Assert.That(result.IsFailure, Is.True);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void ValidateManifestPermissions_WindowsStylePaths_HandledCorrectly()
        {
            // Arrange
            var manifestPath = @"C:\test\app\LuaManifest.json";
            var windowsPathPolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":file",
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create(@"scripts\main.lua", @"..\system\config.ini")
                },
                Capabilities = ManifestCapabilityRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "scoping-test-windows-paths",
                packageName: "ScopingTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for Windows-style paths",
                policies: ImmutableArray.Create(windowsPathPolicy)
            );

            // Act
            var result = InvokeValidateManifestPermissions(manifest, manifestPath);

            // Assert - second pattern should fail
            Assert.That(result.IsFailure, Is.True);
        }

        // Helper method to invoke the internal ValidateManifestPermissions method
        private Result<Manifest, ManifestValidationError> InvokeValidateManifestPermissions(
            Manifest manifest,
            string manifestPath
        )
        {
            var method = typeof(EventDrivenManifestValidator).GetMethod(
                "ValidateManifestPermissions",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );

            return (Result<Manifest, ManifestValidationError>)
                method.Invoke(_validator, new object[] { manifest, manifestPath });
        }

        // Mock implementations
        private class MockEventPublisher<T> : IEventPublisher<T>
            where T : class
        {
            public Task<UnitResult<EventPublishingError>> PublishAsync(
                T domainEvent,
                string correlationId = null,
                CancellationToken cancellationToken = default
            )
            {
                return Task.FromResult(UnitResult.Success<EventPublishingError>());
            }
        }

        private class MockSignatureValidator : ISignatureValidator
        {
            public Result<CryptographicValidationResult, string> ValidateSignature(
                Manifest manifest,
                string manifestPath
            )
            {
                return Result.Success<CryptographicValidationResult, string>(
                    new CryptographicValidationResult
                    {
                        IsValid = true,
                        SignatureAlgorithm = "RSA-SHA256",
                        PublicKeyFingerprint = "mock-fingerprint",
                    }
                );
            }
        }

        private class MockManifestDiscoveryService : IManifestDiscoveryService
        {
            public Maybe<string> DiscoverManifestPath(string scriptPath) => Maybe<string>.None;
        }
    }
}
