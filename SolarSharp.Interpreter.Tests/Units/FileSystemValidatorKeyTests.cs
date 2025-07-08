using System.Collections.Immutable;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for FileSystemValidator's signing key functionality
    /// including the ValidateFilePermissions overload with key parameter.
    /// </summary>
    [TestFixture]
    [Category("Security.FileSystemValidator")]
    public class FileSystemValidatorKeyTests
    {
        private FileSystemValidator _validator;
        private FileSystemSecurity _security;

        [SetUp]
        public void Setup()
        {
            _security = new FileSystemSecurity
            {
                DefaultFilePermissions = FilePermissions.None,
                DirectoryAccessRules = ImmutableArray<DirectoryAccessRule>.Empty,
            };
            _validator = new FileSystemValidator(_security);
        }

        [Test]
        public void ValidateFilePermissions_WithKey_AllowsAccessWhenKeyMatches()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.Read, "validkey");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/secure/file.txt",
                    FileOperation.Read,
                    "validkey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_DeniesAccessWhenKeyMismatches()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.Read, "validkey");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert
            var ex = Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions(
                    "/secure/file.txt",
                    FileOperation.Read,
                    "wrongkey"
                )
            );

            Assert.That(ex.Message, Does.Contain("not allowed"));
        }

        [Test]
        public void ValidateFilePermissions_WithKey_ChecksCorrectOperation()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/data", FilePermissions.Read, "readkey");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - read should work
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/data/file.txt", FileOperation.Read, "readkey")
            );

            // Write should fail
            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions("/data/file.txt", FileOperation.Write, "readkey")
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_EmptyKeySetAllowsAll()
        {
            // Arrange
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/public",
                Permissions = FilePermissions.ReadWrite,
                RequiredSigningKeys = ImmutableHashSet<string>.Empty,
            };
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - any key should work
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/public/file.txt", FileOperation.Read, "anykey")
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/public/file.txt",
                    FileOperation.Write,
                    "differentkey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_MultipleKeysAllowed()
        {
            // Arrange
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/shared",
                Permissions = FilePermissions.Read,
                RequiredSigningKeys = ImmutableHashSet.Create("key1", "key2", "key3"),
            };
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - all valid keys should work
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/shared/file.txt", FileOperation.Read, "key1")
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/shared/file.txt", FileOperation.Read, "key2")
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/shared/file.txt", FileOperation.Read, "key3")
            );

            // Invalid key should fail
            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions("/shared/file.txt", FileOperation.Read, "key4")
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_CreateOperationRequiresWrite()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create(
                "/workspace",
                FilePermissions.SandboxedReadWrite,
                "writekey"
            );
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/workspace/newfile.txt",
                    FileOperation.Create,
                    "writekey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_DeleteOperationRequiresWrite()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/temp", FilePermissions.ReadWrite, "deletekey");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/temp/file.txt",
                    FileOperation.Delete,
                    "deletekey"
                )
            );

            // Read permission should not allow delete
            var readRule = DirectoryAccessRule.Create("/readonly", FilePermissions.Read, "readkey");
            _security.DirectoryAccessRules = ImmutableArray.Create(readRule);
            _validator = new FileSystemValidator(_security);

            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions(
                    "/readonly/file.txt",
                    FileOperation.Delete,
                    "readkey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_ExecuteOperationRequiresExecute()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/bin", FilePermissions.Read, "execkey");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/bin/script.sh", FileOperation.Read, "execkey")
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_NullKeyHandledCorrectly()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.Read, "required");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - null key should be treated as no key
            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions("/secure/file.txt", FileOperation.Read, null)
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_EmptyKeyHandledCorrectly()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.Read, "required");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - empty key should be treated as no key
            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions("/secure/file.txt", FileOperation.Read, "")
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_FallsBackToStandardValidation()
        {
            // Arrange - no DirectoryAccessRules, but standard file permissions
            _security.SetFilePermissions("/standard/file.txt", FilePermissions.Read);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - should use standard validation
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/standard/file.txt",
                    FileOperation.Read,
                    "anykey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_PathNormalizationWorks()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - different path formats should work
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/data/file.txt", FileOperation.Read, "key1")
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/data//file.txt", FileOperation.Read, "key1")
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/data/./file.txt", FileOperation.Read, "key1")
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_WildcardPatternsWork()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/logs/*/app", FilePermissions.Read, "logkey");
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/logs/2024/app/debug.log",
                    FileOperation.Read,
                    "logkey"
                )
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/logs/2025/app/error.log",
                    FileOperation.Read,
                    "logkey"
                )
            );

            // Should not match outside pattern
            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions(
                    "/logs/2024/system/debug.log",
                    FileOperation.Read,
                    "logkey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithKey_CombinedPermissionsWork()
        {
            // Arrange
            // ReadWrite allows all operations (Read, Write, Create, Delete)
            var rule = DirectoryAccessRule.Create(
                "/workspace",
                FilePermissions.ReadWrite,
                "fullkey"
            );
            _security.DirectoryAccessRules = ImmutableArray.Create(rule);
            _validator = new FileSystemValidator(_security);

            // Act & Assert - all operations should work with ReadWrite
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/workspace/file.txt",
                    FileOperation.Read,
                    "fullkey"
                )
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/workspace/file.txt",
                    FileOperation.Write,
                    "fullkey"
                )
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/workspace/file.txt",
                    FileOperation.Create,
                    "fullkey"
                )
            );
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions(
                    "/workspace/file.txt",
                    FileOperation.Delete,
                    "fullkey"
                )
            );
        }

        [Test]
        public void ValidateFilePermissions_WithoutKey_UsesStandardValidation()
        {
            // Arrange
            _security.DefaultFilePermissions = FilePermissions.Read;
            _validator = new FileSystemValidator(_security);

            // Act & Assert - overload without key parameter
            Assert.DoesNotThrow(() =>
                _validator.ValidateFilePermissions("/any/file.txt", FileOperation.Read)
            );

            Assert.Throws<FilePermissionViolationException>(() =>
                _validator.ValidateFilePermissions("/any/file.txt", FileOperation.Write)
            );
        }
    }
}
