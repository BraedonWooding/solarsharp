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
            _factory = new SecurityPolicyFactory(_loggerMock.Object);
            _fileSystem = new MockFileSystem();
            _tempDir = _fileSystem.Path.Combine(
                _fileSystem.Path.GetTempPath(),
                $"solarsharp_cli_test_{Guid.NewGuid()}"
            );
            _fileSystem.Directory.CreateDirectory(_tempDir);
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
            // Arrange & Act
            Action act = () => new SecurityPolicyFactory(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Test]
        public void Create_WithIsolatedLevel_ReturnsIsolatedConfiguration()
        {
            // Act
            var config = _factory.Create("isolated");

            // Assert
            config.Should().NotBeNull();
            config.Execution.TimeoutMs.Should().Be(5000); // Isolated default
            config.Execution.MaxMemoryMB.Should().Be(10); // Isolated default
            config.AllowedModules.Should().HaveFlag(CoreModules.Basic);
            config.AllowedModules.Should().NotHaveFlag(CoreModules.IO);
        }

        [Test]
        public void Create_WithDesktopLevel_ReturnsDesktopConfiguration()
        {
            // Act
            var config = _factory.Create("desktop");

            // Assert
            config.Should().NotBeNull();
            // Desktop uses default SecurityPolicy values
            config.Execution.TimeoutMs.Should().BeGreaterThan(0);
            config.FileSystem.DefaultFileAccess.Should().NotBe(FileAccess.None);
        }

        [Test]
        public void Create_WithAutomationLevel_ReturnsAutomationConfiguration()
        {
            // Act
            var config = _factory.Create("automation");

            // Assert
            config.Should().NotBeNull();
            config.AllowedModules.Should().HaveFlag(CoreModules.IO);
            config.AllowedModules.Should().HaveFlag(CoreModules.OS);
        }

        [Test]
        public void Create_WithNoneLevel_ReturnsUnrestrictedConfiguration()
        {
            // Act
            var config = _factory.Create("none");

            // Assert
            config.Should().NotBeNull();
            config.AllowedModules.Should().Be(CoreModules.Preset_Complete);
            config.FileSystem.DefaultFileAccess.Should().Be(FileAccess.ReadWrite);
            config.FileSystem.DefaultDirectoryAccess.Should().Be(DirectoryAccess.Full);
            config.Execution.TimeoutMs.Should().Be(-1); // No timeout
            config.Execution.MaxMemoryMB.Should().Be(-1); // No memory limit

            // Verify warning was logged
            _loggerMock.Verify(
                x =>
                    x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("dangerous")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()
                    ),
                Times.Once
            );
        }

        [Test]
        public void Create_WithUnknownLevel_ReturnsDesktopConfigurationAndLogsWarning()
        {
            // Act
            var config = _factory.Create("unknown");

            // Assert
            config.Should().NotBeNull();

            // Verify warning was logged
            _loggerMock.Verify(
                x =>
                    x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>(
                            (v, t) => v.ToString().Contains("Unknown security level")
                        ),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()
                    ),
                Times.Once
            );
        }

        [Test]
        public void Create_WithValidManifest_LoadsConfigurationFromManifest()
        {
            // Arrange
            var manifestPath = _fileSystem.Path.Combine(_tempDir, "test.manifest.json");
            var manifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""string"", ""table""],
                    ""capabilities"": [""FileRead""],
                    ""timeoutMs"": 30000,
                    ""maxMemoryMB"": 50
                }
            }";
            _fileSystem.File.WriteAllText(manifestPath, manifestContent);

            // Act
            var config = _factory.Create("desktop", manifestPath);

            // Assert
            config.Should().NotBeNull();
            config.Execution.TimeoutMs.Should().Be(30000);
            config.Execution.MaxMemoryMB.Should().Be(50);
            config.AllowedModules.Should().HaveFlag(CoreModules.Basic);
            config.AllowedModules.Should().HaveFlag(CoreModules.String);
            config.AllowedModules.Should().HaveFlag(CoreModules.Table);
            config.AllowedModules.Should().NotHaveFlag(CoreModules.IO);
            config.FileSystem.DefaultFileAccess.Should().HaveFlag(FileAccess.Read);
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
            var manifestPath = _fileSystem.Path.Combine(_tempDir, "unknown_module.json");
            var manifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""unknown_module""]
                }
            }";
            _fileSystem.File.WriteAllText(manifestPath, manifestContent);

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
            var manifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";
            _fileSystem.File.WriteAllText(manifestPath, manifestContent);

            // Act
            var config = _factory.Create("isolated", manifestPath);

            // Assert
            config.FileSystem.DefaultFileAccess.Should().HaveFlag(FileAccess.Write);
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

            config1.Execution.TimeoutMs.Should().Be(5000); // Isolated timeout
            config3.AllowedModules.Should().HaveFlag(CoreModules.OS); // Automation includes OS
        }
    }
}
