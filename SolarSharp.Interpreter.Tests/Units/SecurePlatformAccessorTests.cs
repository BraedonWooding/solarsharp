using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Security.Unit")]
    public class SecurePlatformAccessorTests
    {
        private MockFileSystem _mockFileSystem;
        private SecurityPolicy _securityPolicy;
        private SecurePlatformAccessor _accessor;

        [SetUp]
        public void SetUp()
        {
            _mockFileSystem = new MockFileSystem();
            _securityPolicy = new SecurityPolicy
            {
                AllowedModules = CoreModules.IO | CoreModules.Basic,
                Capabilities =
                    ScriptCapabilities.FileRead
                    | ScriptCapabilities.FileWrite
                    | ScriptCapabilities.FileDelete
                    | ScriptCapabilities.EnvironmentAccess,
                DefaultFileAccess = FilePermissions.SandboxedReadWrite,
                DefaultDirectoryAccess = DirectoryPermissions.ListAndCreateFiles,
                AllowEnvironmentAccess = true,
                AllowedEnvironmentVariables = new[] { "PATH", "HOME" }.ToImmutableArray(),
                DirectoryAccessRules = new[]
                {
                    DirectoryAccessRule.Create("/test", FilePermissions.ReadWrite),
                    DirectoryAccessRule.Create("/test/**", FilePermissions.ReadWrite),
                }.ToImmutableArray(),
            };

            _accessor = new SecurePlatformAccessor(_securityPolicy, _mockFileSystem);
        }

        [TearDown]
        public void TearDown()
        {
            _accessor?.Dispose();
        }

        [Category("Module.Unit")]
        [Test]
        public void FilterSupportedCoreModules_ReturnsConfiguredModules()
        {
            var input = CoreModules.IO | CoreModules.Basic | CoreModules.Table;

            var result = _accessor.FilterSupportedCoreModules(input);

            Assert.That(result, Is.EqualTo(_securityPolicy.AllowedModules));
        }

        [Test]
        public void GetEnvironmentVariable_AllowedVariable_ReturnsValue()
        {
            Environment.SetEnvironmentVariable("PATH", "/usr/bin");

            var result = _accessor.GetEnvironmentVariable("PATH");

            Assert.That(result, Is.EqualTo("/usr/bin"));
        }

        [Test]
        public void GetEnvironmentVariable_DisallowedVariable_ReturnsNull()
        {
            Environment.SetEnvironmentVariable("SECRET", "sensitive");

            var result = _accessor.GetEnvironmentVariable("SECRET");

            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetEnvironmentVariable_NoEnvironmentAccess_ThrowsException()
        {
            var restrictivePolicy = new SecurityPolicy
            {
                AllowEnvironmentAccess = false,
                Capabilities = ScriptCapabilities.SafeCompute, // No EnvironmentAccess capability
            };
            using var restrictiveAccessor = new SecurePlatformAccessor(
                restrictivePolicy,
                _mockFileSystem
            );

            Assert.Throws<MissingCapabilityException>(() =>
                restrictiveAccessor.GetEnvironmentVariable("PATH")
            );
        }

        [Test]
        public void OS_Execute_NotAllowed_ThrowsException()
        {
            var restrictivePolicy = new SecurityPolicy
            {
                Capabilities = ScriptCapabilities.SafeCompute, // No ProcessExecution
            };
            using var restrictiveAccessor = new SecurePlatformAccessor(
                restrictivePolicy,
                _mockFileSystem
            );

            Assert.Throws<MissingCapabilityException>(() =>
                restrictiveAccessor.OS_Execute("echo hello")
            );
        }

        [Test]
        public void IO_OpenFile_PathTraversal_ThrowsException()
        {
            var script = new Script(Examples.Common.Desktop);

            Assert.Throws<FilePermissionViolationException>(() =>
                _accessor.IO_OpenFile(script, "../../../etc/passwd", Encoding.UTF8, "r")
            );
        }

        [Test]
        public void IO_OpenFile_DangerousExtension_ThrowsException()
        {
            var script = new Script(Examples.Common.Desktop);

            Assert.Throws<FilePermissionViolationException>(() =>
                _accessor.IO_OpenFile(script, "malware.exe", Encoding.UTF8, "r")
            );
        }

        [Test]
        public void IO_OpenFile_ValidPath_OpensFile()
        {
            var script = new Script(Examples.Common.Desktop);
            var testFile = "/test/data.txt";
            _mockFileSystem.AddFile(testFile, new MockFileData("test content"));

            using var stream = _accessor.IO_OpenFile(script, testFile, Encoding.UTF8, "r");

            Assert.That(stream, Is.Not.Null);
        }

        [Test]
        public void IO_OpenFile_WriteMode_RequiresWriteCapability()
        {
            var readOnlyPolicy = new SecurityPolicy
            {
                Capabilities = ScriptCapabilities.FileRead, // No FileWrite
            };
            using var readOnlyAccessor = new SecurePlatformAccessor(
                readOnlyPolicy,
                _mockFileSystem
            );
            var script = new Script(Examples.Common.Desktop);

            Assert.Throws<MissingCapabilityException>(() =>
                readOnlyAccessor.IO_OpenFile(script, "/test/file.txt", Encoding.UTF8, "w")
            );
        }

        [Test]
        public void OS_FileDelete_ValidPath_DeletesFile()
        {
            var testFile = "/test/delete.txt";
            _mockFileSystem.AddFile(testFile, new MockFileData("content"));

            _accessor.OS_FileDelete(testFile);

            Assert.That(_mockFileSystem.FileExists(testFile), Is.False);
        }

        [Test]
        public void OS_FileDelete_PathTraversal_ThrowsException()
        {
            Assert.Throws<FilePermissionViolationException>(() =>
                _accessor.OS_FileDelete("../../../important/file.txt")
            );
        }

        [Test]
        public void OS_FileDelete_NoDeleteCapability_ThrowsException()
        {
            var restrictivePolicy = new SecurityPolicy
            {
                Capabilities = ScriptCapabilities.FileRead, // No FileDelete
            };
            using var restrictiveAccessor = new SecurePlatformAccessor(
                restrictivePolicy,
                _mockFileSystem
            );

            Assert.Throws<MissingCapabilityException>(() =>
                restrictiveAccessor.OS_FileDelete("/test/file.txt")
            );
        }

        [Test]
        public void OS_FileMove_ValidPaths_MovesFile()
        {
            var sourceFile = "/test/source.txt";
            var destFile = "/test/dest.txt";
            _mockFileSystem.AddFile(sourceFile, new MockFileData("content"));

            _accessor.OS_FileMove(sourceFile, destFile);

            Assert.That(_mockFileSystem.FileExists(sourceFile), Is.False);
            Assert.That(_mockFileSystem.FileExists(destFile), Is.True);
        }

        [Test]
        public void OS_FileMove_PathTraversalSource_ThrowsException()
        {
            Assert.Throws<FilePermissionViolationException>(() =>
                _accessor.OS_FileMove("../../../etc/passwd", "/test/dest.txt")
            );
        }

        [Test]
        public void OS_FileMove_PathTraversalDestination_ThrowsException()
        {
            _mockFileSystem.AddFile("/test/source.txt", new MockFileData("content"));

            Assert.Throws<FilePermissionViolationException>(() =>
                _accessor.OS_FileMove("/test/source.txt", "../../../tmp/dest.txt")
            );
        }

        [Test]
        public void OS_FileExists_ValidPath_ReturnsTrue()
        {
            var testFile = "/test/exists.txt";
            _mockFileSystem.AddFile(testFile, new MockFileData("content"));

            var result = _accessor.OS_FileExists(testFile);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OS_FileExists_NonExistentPath_ReturnsFalse()
        {
            var result = _accessor.OS_FileExists("/test/nonexistent.txt");

            Assert.That(result, Is.False);
        }

        [Test]
        public void OS_FileExists_PathTraversal_ReturnsFalse()
        {
            // Should return false for security reasons, not throw
            var result = _accessor.OS_FileExists("../../../etc/passwd");

            Assert.That(result, Is.False);
        }

        [Test]
        public void IO_GetStandardStream_StdOut_ReturnsStream()
        {
            var stream = _accessor.IO_GetStandardStream(StandardFileType.StdOut);

            Assert.That(stream, Is.Not.Null);
        }

        [Category("Security.Policy")]
        [Test]
        public void IO_GetStandardStream_StdIn_RestrictivePolicy_ReturnsNull()
        {
            var restrictivePolicy = new SecurityPolicy { DefaultFileAccess = FilePermissions.None };
            using var restrictiveAccessor = new SecurePlatformAccessor(
                restrictivePolicy,
                _mockFileSystem
            );

            var stream = restrictiveAccessor.IO_GetStandardStream(StandardFileType.StdIn);

            Assert.That(stream, Is.EqualTo(Stream.Null));
        }

        [Test]
        public void IO_OS_GetTempFilename_ReturnsValidPath()
        {
            var tempFile = _accessor.IO_OS_GetTempFilename();

            Assert.That(tempFile, Is.Not.Null);
            Assert.That(tempFile, Is.Not.Empty);
        }

        [Test]
        public void Constructor_NullConfig_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SecurePlatformAccessor(null, _mockFileSystem)
            );
        }

        [Test]
        public void Constructor_WithLogger_DoesNotThrow()
        {
            var logger = new SecurityLogger();

            Assert.DoesNotThrow(() =>
                new SecurePlatformAccessor(_securityPolicy, logger, _mockFileSystem)
            );
        }

        [Test]
        public void Constructor_NullLogger_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SecurePlatformAccessor(_securityPolicy, null, _mockFileSystem)
            );
        }

        [Category("Security.Unit")]
        [Test]
        public void PathSecurity_Integration_WorksWithAllOperations()
        {
            var script = new Script(Examples.Common.Desktop);
            var validFile = "/test/valid.txt";
            _mockFileSystem.AddFile(validFile, new MockFileData("content"));

            // These should all work with valid paths
            Assert.DoesNotThrow(() => _accessor.OS_FileExists(validFile));
            Assert.DoesNotThrow(() => _accessor.OS_FileDelete(validFile));

            // Recreate file for move test
            _mockFileSystem.AddFile(validFile, new MockFileData("content"));
            Assert.DoesNotThrow(() => _accessor.OS_FileMove(validFile, "/test/moved.txt"));
        }

        [Test]
        public void Dispose_CleansUpResources()
        {
            Assert.DoesNotThrow(() => _accessor.Dispose());

            // Should be able to dispose multiple times
            Assert.DoesNotThrow(() => _accessor.Dispose());
        }
    }
}
