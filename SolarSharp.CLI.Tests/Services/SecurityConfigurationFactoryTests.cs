using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using SolarSharp.CLI.Services;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using System.Collections.Immutable;
using System.Text.Json;

namespace SolarSharp.CLI.Tests.Services
{
    /// <summary>
    /// Unit tests for the SecurityPolicyFactory class.
    /// Tests various security levels and manifest loading scenarios.
    /// </summary>
    [TestFixture]
    [Category("CLI.Unit")]
    [Category("CLI")]
    public class SecurityPolicyFactoryTests
    {
        private SecurityPolicyFactory _factory;
        private Mock<ILogger<SecurityPolicyFactory>> _loggerMock;
        private IFileSystem _fileSystem;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<SecurityPolicyFactory>>();
            _fileSystem = new MockFileSystem();
            _tempDir = _fileSystem.Path.Combine(
                _fileSystem.Path.GetTempPath(),
                $"solarsharp_cli_test_{Guid.NewGuid()}"
            );
            _fileSystem.Directory.CreateDirectory(_tempDir);
            _factory = new SecurityPolicyFactory(_loggerMock.Object, _fileSystem);
        }

        [TearDown]
        public void TearDown()
        {
            if (_fileSystem.Directory.Exists(_tempDir))
            {
                _fileSystem.Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void Create_WithNullLogger_ThrowsArgumentNullException()
        {
            Action act = () => new SecurityPolicyFactory(null, _fileSystem);

            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Test]
        public void Create_WithIsolatedLevel_ReturnsIsolatedConfiguration()
        {
            var config = _factory.Create("isolated");

            config.Should().NotBeNull();
            config.TimeoutMs.Should().Be(1000); // Isolated default (1 second)
            config.MaxMemoryMB.Should().Be(10); // Isolated default
            config.AllowedModules.Should().HaveFlag(CoreModules.Basic);
            config.AllowedModules.Should().NotHaveFlag(CoreModules.IO);
        }

        [Test]
        public void Create_WithDesktopLevel_ReturnsDesktopConfiguration()
        {
            var config = _factory.Create("desktop");

            config.Should().NotBeNull();

            config.TimeoutMs.Should().BeGreaterThan(0);
            config.DefaultFileAccess.Should().NotBe(FilePermissions.None);
        }

        [Test]
        public void Create_WithAutomationLevel_ReturnsAutomationConfiguration()
        {
            var config = _factory.Create("automation");

            config.Should().NotBeNull();
            config.AllowedModules.Should().HaveFlag(CoreModules.IO);
            config.AllowedModules.Should().HaveFlag(CoreModules.OS_Time | CoreModules.OS_System);
        }

        [Test]
        public void Create_WithNoneLevel_ReturnsUnrestrictedConfiguration()
        {
            // Act
            var config = _factory.Create("none");

            // Assert
            config.Should().NotBeNull();
            config.AllowedModules.Should().Be(CoreModules.Preset_Complete | CoreModules.PubSub);
            config.DefaultFileAccess.Should().Be(FilePermissions.ReadWrite);
            config.DefaultDirectoryAccess.Should().Be(DirectoryPermissions.ListAndCreateFiles);
            config.TimeoutMs.Should().Be(-1); // No timeout
            config.MaxMemoryMB.Should().Be(-1); // No memory limit

            // Note: Warning about "none" policy being dangerous would be logged
            // by the policy itself or during script execution, not by the factory
        }

        [Test]
        public void Create_WithUnknownLevel_ThrowsArgumentException()
        {
            // Act
            Action act = () => _factory.Create("unknown");

            // Assert
            act.Should()
                .Throw<ArgumentException>()
                .WithMessage("Unknown policy name: unknown*")
                .WithParameterName("policyName");
        }

        [Test]
        public void Create_WithValidManifest_LoadsConfigurationFromManifest()
        {
            // Arrange
            var manifestPath = _fileSystem.Path.Combine(_tempDir, "LuaManifest.json");
            
            // Create a V2.0 manifest directly
            var manifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "test-manifest",
                SignedContent = ImmutableArray.Create(new SignedContentBlock
                {
                    KeyId = "sha256:test",
                    Signature = "",
                    PublicKey = "",
                    Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add("test-package", new ManifestPackage
                    {
                        Files = ImmutableDictionary<string, string>.Empty.Add("test.lua", "sha256:test"),
                        Metadata = new PackageMetadata
                        {
                            Name = "Test Package",
                            Version = "1.0.0",
                            Description = "Test package"
                        }
                    }),
                    Policies = ImmutableArray.Create(new ManifestPolicy
                    {
                        Packages = ImmutableArray.Create("test-package"),
                        Selector = ":file",
                        Grant = new PolicyGrant
                        {
                            Modules = ImmutableArray.Create("basic", "string", "table"),
                            Capabilities = ImmutableArray.Create("FileRead")
                        },
                        Restrict = new PolicyRestrictions
                        {
                            Timeout = "30s",
                            MaxMemory = "50MB"
                        }
                    })
                })
            };
            
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            _fileSystem.File.WriteAllText(manifestPath, manifestJson);

            // Act
            var config = _factory.Create("desktop", manifestPath);

            // Assert
            config.Should().NotBeNull();
            config.TimeoutMs.Should().Be(30000);
            config.MaxMemoryMB.Should().Be(50);
            config.AllowedModules.Should().HaveFlag(CoreModules.Basic);
            config.AllowedModules.Should().HaveFlag(CoreModules.String);
            config.AllowedModules.Should().HaveFlag(CoreModules.Table);
            config.AllowedModules.Should().NotHaveFlag(CoreModules.IO);
            config.DefaultFileAccess.Should().HaveFlag(FilePermissions.Read);
        }

        [Test]
        public void Create_WithNonExistentManifest_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonExistentPath = _fileSystem.Path.Combine(_tempDir, "nonexistent.json");

            // Act
            Action act = () => _factory.Create("desktop", nonExistentPath);

            // Assert
            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Failed to load manifest:*")
                .WithInnerException<FileNotFoundException>();
        }

        [Test]
        public void Create_WithInvalidManifestJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var manifestPath = _fileSystem.Path.Combine(_tempDir, "invalid.json");
            _fileSystem.File.WriteAllText(manifestPath, "{ invalid json }");

            // Act
            Action act = () => _factory.Create("desktop", manifestPath);

            // Assert
            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Failed to load manifest:*");
        }

        [Test]
        public void Create_WithManifestContainingUnknownModule_LogsWarning()
        {
            // Arrange
            var manifestPath = _fileSystem.Path.Combine(_tempDir, "LuaManifest.json");
            
            // Create a V2.0 manifest with an unknown module
            var manifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "test-manifest",
                SignedContent = ImmutableArray.Create(new SignedContentBlock
                {
                    KeyId = "sha256:test",
                    Signature = "",
                    PublicKey = "",
                    Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add("test-package", new ManifestPackage
                    {
                        Files = ImmutableDictionary<string, string>.Empty.Add("test.lua", "sha256:test"),
                        Metadata = new PackageMetadata
                        {
                            Name = "Test Package",
                            Version = "1.0.0",
                            Description = "Test package"
                        }
                    }),
                    Policies = ImmutableArray.Create(new ManifestPolicy
                    {
                        Packages = ImmutableArray.Create("test-package"),
                        Selector = ":file",
                        Grant = new PolicyGrant
                        {
                            Modules = ImmutableArray.Create("basic", "unknown_module")
                        },
                        Restrict = new PolicyRestrictions()
                    })
                })
            };
            
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            _fileSystem.File.WriteAllText(manifestPath, manifestJson);

            // Act
            var config = _factory.Create("desktop", manifestPath);

            // Assert
            config.Should().NotBeNull();
            config.AllowedModules.Should().HaveFlag(CoreModules.Basic);

            // Verify warning was logged for unknown module
            _loggerMock.Verify(
                x =>
                    x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unknown module name")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()
                    ),
                Times.Once
            );
        }

        [Test]
        public void Create_WithManifestContainingFileWriteCapability_EnablesFileWrite()
        {
            // Arrange
            var manifestPath = _fileSystem.Path.Combine(_tempDir, "filewrite.json");
            // Create a V2.0 manifest with FileWrite capability
            var manifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "test-manifest",
                SignedContent = ImmutableArray.Create(new SignedContentBlock
                {
                    KeyId = "sha256:test",
                    Signature = "",
                    PublicKey = "",
                    Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add("test-package", new ManifestPackage
                    {
                        Files = ImmutableDictionary<string, string>.Empty.Add("test.lua", "sha256:test"),
                        Metadata = new PackageMetadata
                        {
                            Name = "Test Package",
                            Version = "1.0.0",
                            Description = "Test package"
                        }
                    }),
                    Policies = ImmutableArray.Create(new ManifestPolicy
                    {
                        Packages = ImmutableArray.Create("test-package"),
                        Selector = ":file",
                        Grant = new PolicyGrant
                        {
                            Capabilities = ImmutableArray.Create("FileWrite")
                        },
                        Restrict = new PolicyRestrictions()
                    })
                })
            };
            
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            _fileSystem.File.WriteAllText(manifestPath, manifestJson);

            // Act
            var config = _factory.Create("isolated", manifestPath);

            // Assert
            config.DefaultFileAccess.Should().HaveFlag(FilePermissions.ReadWrite);
        }

        [Test]
        public void Create_WithCaseInsensitiveValues_WorksCorrectly()
        {
            // Act
            var config1 = _factory.Create("ISOLATED");
            var config2 = _factory.Create("Desktop");
            var config3 = _factory.Create("AuToMaTiOn");

            // Assert
            config1.Should().NotBeNull();
            config2.Should().NotBeNull();
            config3.Should().NotBeNull();

            config1.TimeoutMs.Should().Be(1000); // Isolated timeout (1 second)
            config3.AllowedModules.Should().HaveFlag(CoreModules.OS_System); // Automation includes OS
        }
    }
}
