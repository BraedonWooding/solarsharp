using System;
using System.IO;
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
    /// Unit tests for the SecurityConfigurationFactory class.
    /// Tests various security levels and manifest loading scenarios.
    /// </summary>
    [TestFixture]
    [Category("UnitTest")]
    [Category("CLI")]
    public class SecurityConfigurationFactoryTests
    {
        private SecurityConfigurationFactory _factory;
        private Mock<ILogger<SecurityConfigurationFactory>> _loggerMock;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<SecurityConfigurationFactory>>();
            _factory = new SecurityConfigurationFactory(_loggerMock.Object);
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_cli_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void Create_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SecurityConfigurationFactory(null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
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
            // Desktop uses default SecurityConfiguration values
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
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("dangerous")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
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
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unknown security level")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public void Create_WithValidManifest_LoadsConfigurationFromManifest()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "test.manifest.json");
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""string"", ""table""],
                    ""capabilities"": [""FileRead""],
                    ""timeoutMs"": 30000,
                    ""maxMemoryMB"": 50
                }
            }";
            File.WriteAllText(manifestPath, manifestContent);

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
            var nonExistentPath = Path.Combine(_tempDir, "nonexistent.json");

            // Act
            Action act = () => _factory.Create("desktop", nonExistentPath);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Failed to load manifest:*")
                .WithInnerException<FileNotFoundException>();
        }

        [Test]
        public void Create_WithInvalidManifestJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "invalid.json");
            File.WriteAllText(manifestPath, "{ invalid json }");

            // Act
            Action act = () => _factory.Create("desktop", manifestPath);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Failed to load manifest:*");
        }

        [Test]
        public void Create_WithManifestContainingUnknownModule_LogsWarning()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "unknown_module.json");
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""unknown_module""]
                }
            }";
            File.WriteAllText(manifestPath, manifestContent);

            // Act
            var config = _factory.Create("desktop", manifestPath);

            // Assert
            config.Should().NotBeNull();
            config.AllowedModules.Should().HaveFlag(CoreModules.Basic);
            
            // Verify warning was logged for unknown module
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unknown module name")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public void Create_WithManifestContainingFileWriteCapability_EnablesFileWrite()
        {
            // Arrange
            var manifestPath = Path.Combine(_tempDir, "filewrite.json");
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";
            File.WriteAllText(manifestPath, manifestContent);

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