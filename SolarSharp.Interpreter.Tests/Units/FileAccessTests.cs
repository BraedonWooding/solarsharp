using System;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using FileAccess = SolarSharp.Interpreter.Security.FileAccess;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class FileAccessTests
    {
        private string _tempDir;
        private FileSystemSecurity _fileSystem;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            
            _fileSystem = new FileSystemSecurity();
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
        public void SetFileAccess_SetsSpecificFilePermissions()
        {
            var filePath = Path.Combine(_tempDir, "test.txt");
            
            _fileSystem.SetFileAccess(filePath, FileAccess.Read);

            Assert.That(_fileSystem.GetFileAccess(filePath), Is.EqualTo(FileAccess.Read));
        }

        [Test]
        public void SetDirectoryAccess_SetsSpecificDirectoryPermissions()
        {
            var dirPath = Path.Combine(_tempDir, "subdir");

            _fileSystem.SetDirectoryAccess(dirPath, DirectoryAccess.List);

            Assert.That(_fileSystem.GetDirectoryAccess(dirPath), Is.EqualTo(DirectoryAccess.List));
        }

        [Test]
        public void GetFileAccess_ReturnsDefaultForUnspecifiedFiles()
        {
            var filePath = Path.Combine(_tempDir, "unspecified.txt");

            var access = _fileSystem.GetFileAccess(filePath);

            Assert.That(access, Is.EqualTo(FileAccess.SandboxedReadWrite));
        }

        [Test]
        public void GetDirectoryAccess_ReturnsDefaultForUnspecifiedDirectories()
        {
            var dirPath = Path.Combine(_tempDir, "unspecified");

            var access = _fileSystem.GetDirectoryAccess(dirPath);

            Assert.That(access, Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
        }

        [Test]
        public void GetFileAccess_InheritsDeniedAccessFromParentDirectory()
        {
            var parentDir = Path.Combine(_tempDir, "parent");
            var filePath = Path.Combine(parentDir, "file.txt");

            _fileSystem.SetDirectoryAccess(parentDir, DirectoryAccess.None);

            var access = _fileSystem.GetFileAccess(filePath);

            Assert.That(access, Is.EqualTo(FileAccess.None));
        }

        [Test]
        public void IsFileOperationAllowed_ReadAllowedWithReadAccess()
        {
            var filePath = Path.Combine(_tempDir, "readable.txt");
            _fileSystem.SetFileAccess(filePath, FileAccess.Read);

            var allowed = _fileSystem.IsFileOperationAllowed(filePath, FileOperation.Read);

            Assert.IsTrue(allowed);
        }

        [Test]
        public void IsFileOperationAllowed_WriteBlockedWithReadOnlyAccess()
        {
            var filePath = Path.Combine(_tempDir, "readonly.txt");
            _fileSystem.SetFileAccess(filePath, FileAccess.Read);

            var allowed = _fileSystem.IsFileOperationAllowed(filePath, FileOperation.Write);

            Assert.IsFalse(allowed);
        }

        [Test]
        public void IsFileOperationAllowed_CreateRequiresSandboxedOrHigher()
        {
            var filePath1 = Path.Combine(_tempDir, "read-only.txt");
            var filePath2 = Path.Combine(_tempDir, "sandboxed.txt");

            _fileSystem.SetFileAccess(filePath1, FileAccess.Read);
            _fileSystem.SetFileAccess(filePath2, FileAccess.SandboxedReadWrite);

            Assert.IsFalse(_fileSystem.IsFileOperationAllowed(filePath1, FileOperation.Create));
            Assert.IsTrue(_fileSystem.IsFileOperationAllowed(filePath2, FileOperation.Create));
        }

        [Test]
        public void IsFileOperationAllowed_DeleteRequiresFullReadWrite()
        {
            var filePath1 = Path.Combine(_tempDir, "sandboxed.txt");
            var filePath2 = Path.Combine(_tempDir, "full-access.txt");

            _fileSystem.SetFileAccess(filePath1, FileAccess.SandboxedReadWrite);
            _fileSystem.SetFileAccess(filePath2, FileAccess.ReadWrite);

            Assert.IsFalse(_fileSystem.IsFileOperationAllowed(filePath1, FileOperation.Delete));
            Assert.IsTrue(_fileSystem.IsFileOperationAllowed(filePath2, FileOperation.Delete));
        }

        [Test]
        public void IsFileOperationAllowed_RequiresDirectoryAccess()
        {
            var parentDir = Path.Combine(_tempDir, "restricted");
            var filePath = Path.Combine(parentDir, "file.txt");

            _fileSystem.SetDirectoryAccess(parentDir, DirectoryAccess.None);
            _fileSystem.SetFileAccess(filePath, FileAccess.ReadWrite);

            // File has ReadWrite but directory has None - should block all operations
            Assert.IsFalse(_fileSystem.IsFileOperationAllowed(filePath, FileOperation.Read));
            Assert.IsFalse(_fileSystem.IsFileOperationAllowed(filePath, FileOperation.Write));
        }

        [Test]
        public void IsFileOperationAllowed_CreateRequiresListAndCreateFiles()
        {
            var parentDir = Path.Combine(_tempDir, "listonly");
            var filePath = Path.Combine(parentDir, "newfile.txt");

            _fileSystem.SetDirectoryAccess(parentDir, DirectoryAccess.List);
            _fileSystem.SetFileAccess(filePath, FileAccess.SandboxedReadWrite);

            // Can read but not create in list-only directory
            Assert.IsTrue(_fileSystem.IsFileOperationAllowed(filePath, FileOperation.Read));
            Assert.IsFalse(_fileSystem.IsFileOperationAllowed(filePath, FileOperation.Create));
        }

        [Test]
        public void IsCompatibleWith_ValidatesFileDirectoryCompatibility()
        {
            Assert.IsTrue(FileAccess.Read.IsCompatibleWith(DirectoryAccess.List));
            Assert.IsTrue(FileAccess.SandboxedReadWrite.IsCompatibleWith(DirectoryAccess.ListAndCreateFiles));
            Assert.IsFalse(FileAccess.SandboxedReadWrite.IsCompatibleWith(DirectoryAccess.List));
            Assert.IsFalse(FileAccess.Read.IsCompatibleWith(DirectoryAccess.None));
        }

        [Test]
        public void MatchesWildcard_SimplePatterns()
        {
            Assert.IsTrue("test.txt".MatchesWildcard("*.txt"));
            Assert.IsTrue("data/file.lua".MatchesWildcard("data/*.lua"));
            Assert.IsFalse("test.txt".MatchesWildcard("*.lua"));
        }

        [Test]
        public void MatchesWildcard_RecursivePatterns()
        {
            Assert.IsTrue("deep/nested/file.txt".MatchesWildcard("**/*.txt"));
            Assert.IsTrue("deep/nested/path/file.lua".MatchesWildcard("deep/**"));
            Assert.IsFalse("other/file.txt".MatchesWildcard("deep/**"));
        }

        [Test]
        public void MatchesWildcard_DirectoryPatterns()
        {
            Assert.IsTrue("scripts/main.lua".MatchesWildcard("scripts/**"));
            Assert.IsTrue("scripts/utils/helper.lua".MatchesWildcard("scripts/**"));
            Assert.IsFalse("other/main.lua".MatchesWildcard("scripts/**"));
        }

        [Test]
        public void SecurityConfiguration_SetFileAccess_UpdatesFileSystem()
        {
            var config = new SecurityConfiguration();
            var filePath = Path.Combine(_tempDir, "test.txt");

            config.SetFileAccess(filePath, FileAccess.Read);

            Assert.That(config.FileSystem.GetFileAccess(filePath), Is.EqualTo(FileAccess.Read));
        }

        [Test]
        public void SecurityConfiguration_SetDirectoryAccess_UpdatesFileSystem()
        {
            var config = new SecurityConfiguration();
            var dirPath = Path.Combine(_tempDir, "testdir");

            config.SetDirectoryAccess(dirPath, DirectoryAccess.List);

            Assert.That(config.FileSystem.GetDirectoryAccess(dirPath), Is.EqualTo(DirectoryAccess.List));
        }

        [Test]
        public void SecurityConfiguration_FluentAPI_ChainsCorrectly()
        {
            var config = new SecurityConfiguration()
                .SetFileAccess("/app/config.txt", FileAccess.Read)
                .SetDirectoryAccess("/app/data", DirectoryAccess.ListAndCreateFiles)
                .WithDefaultFileAccess(FileAccess.Read)
                .WithDefaultDirectoryAccess(DirectoryAccess.List);

            Assert.That(config.FileSystem.GetFileAccess("/app/config.txt"), Is.EqualTo(FileAccess.Read));
            Assert.That(config.FileSystem.GetDirectoryAccess("/app/data"), Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
            Assert.That(config.FileSystem.DefaultFileAccess, Is.EqualTo(FileAccess.Read));
            Assert.That(config.FileSystem.DefaultDirectoryAccess, Is.EqualTo(DirectoryAccess.List));
        }

        [Test]
        public void FileSystemValidator_AllowsValidOperations()
        {
            var filePath = Path.Combine(_tempDir, "valid.txt");
            _fileSystem.SetFileAccess(filePath, FileAccess.ReadWrite);

            var validator = new FileSystemValidator(_fileSystem);

            Assert.DoesNotThrow(() => validator.ValidateFileAccess(filePath, FileOperation.Read));
            Assert.DoesNotThrow(() => validator.ValidateFileAccess(filePath, FileOperation.Write));
        }

        [Test]
        public void FileSystemValidator_BlocksInvalidOperations()
        {
            var filePath = Path.Combine(_tempDir, "readonly.txt");
            _fileSystem.SetFileAccess(filePath, FileAccess.Read);

            var validator = new FileSystemValidator(_fileSystem);

            Assert.DoesNotThrow(() => validator.ValidateFileAccess(filePath, FileOperation.Read));
            Assert.Throws<FileAccessViolationException>(() => validator.ValidateFileAccess(filePath, FileOperation.Write));
        }

        [Test]
        public void FileSystemValidator_BlocksPathTraversal()
        {
            // Configure a sandbox to enable path traversal detection
            var sandboxedFileSystem = new FileSystemSecurity
            {
                SandboxRoot = _tempDir // Set the temp directory as the sandbox root
            };
            var validator = new FileSystemValidator(sandboxedFileSystem);

            Assert.Throws<PathTraversalException>(() =>
                validator.ValidateFileAccess("../../../etc/passwd", FileOperation.Read));
            Assert.Throws<PathTraversalException>(() =>
                validator.ValidateFileAccess("..\\..\\windows\\system32", FileOperation.Read));
        }

        [Test]
        public void FileSystemValidator_ValidatesDirectoryOperations()
        {
            var dirPath = Path.Combine(_tempDir, "listonly");
            _fileSystem.SetDirectoryAccess(dirPath, DirectoryAccess.List);

            var validator = new FileSystemValidator(_fileSystem);

            Assert.DoesNotThrow(() => validator.ValidateDirectoryAccess(dirPath, DirectoryOperation.List));
            Assert.Throws<FileAccessViolationException>(() => validator.ValidateDirectoryAccess(dirPath, DirectoryOperation.Create));
        }

        [Test]
        public void ParseFileAccess_ConvertsStringsCorrectly()
        {
            Assert.That("none".ParseFileAccess(), Is.EqualTo(FileAccess.None));
            Assert.That("read".ParseFileAccess(), Is.EqualTo(FileAccess.Read));
            Assert.That("readwrite".ParseFileAccess(), Is.EqualTo(FileAccess.ReadWrite));
            Assert.That("sandboxedreadwrite".ParseFileAccess(), Is.EqualTo(FileAccess.SandboxedReadWrite));
            Assert.That("invalid".ParseFileAccess(), Is.EqualTo(FileAccess.SandboxedReadWrite)); // Default
        }

        [Test]
        public void ParseDirectoryAccess_ConvertsStringsCorrectly()
        {
            Assert.That("none".ParseDirectoryAccess(), Is.EqualTo(DirectoryAccess.None));
            Assert.That("list".ParseDirectoryAccess(), Is.EqualTo(DirectoryAccess.List));
            Assert.That("listandcreatefiles".ParseDirectoryAccess(), Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
            Assert.That("invalid".ParseDirectoryAccess(), Is.EqualTo(DirectoryAccess.ListAndCreateFiles)); // Default
        }

        [Test]
        public void ToManifestString_ConvertsEnumsCorrectly()
        {
            Assert.That(FileAccess.None.ToManifestString(), Is.EqualTo("none"));
            Assert.That(FileAccess.Read.ToManifestString(), Is.EqualTo("read"));
            Assert.That(FileAccess.ReadWrite.ToManifestString(), Is.EqualTo("readwrite"));
            Assert.That(FileAccess.SandboxedReadWrite.ToManifestString(), Is.EqualTo("sandboxedreadwrite"));

            Assert.That(DirectoryAccess.None.ToManifestString(), Is.EqualTo("none"));
            Assert.That(DirectoryAccess.List.ToManifestString(), Is.EqualTo("list"));
            Assert.That(DirectoryAccess.ListAndCreateFiles.ToManifestString(), Is.EqualTo("listandcreatefiles"));
        }

        [Test]
        public void DefaultAccess_ProvidesSandboxedReadWriteAndListAndCreateFiles()
        {
            var fs = new FileSystemSecurity();

            Assert.That(fs.DefaultFileAccess, Is.EqualTo(FileAccess.SandboxedReadWrite));
            Assert.That(fs.DefaultDirectoryAccess, Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
        }

        [Test]
        public void DefaultAccess_AllowsCommonOperations()
        {
            var fs = new FileSystemSecurity();
            var filePath = Path.Combine(_tempDir, "default.txt");

            Assert.IsTrue(fs.IsFileOperationAllowed(filePath, FileOperation.Read));
            Assert.IsTrue(fs.IsFileOperationAllowed(filePath, FileOperation.Write));
            Assert.IsTrue(fs.IsFileOperationAllowed(filePath, FileOperation.Create));
            Assert.IsFalse(fs.IsFileOperationAllowed(filePath, FileOperation.Delete)); // Requires full ReadWrite
        }

        [Test]
        public void GetDirectoryAccess_InheritsFromParent()
        {
            var parentDir = Path.Combine(_tempDir, "parent");
            var childDir = Path.Combine(parentDir, "child");

            _fileSystem.SetDirectoryAccess(parentDir, DirectoryAccess.List);

            // Child should inherit more restrictive parent access
            var childAccess = _fileSystem.GetDirectoryAccess(childDir);
            Assert.That(childAccess, Is.EqualTo(DirectoryAccess.List));
        }

        [Test]
        public void GetDirectoryAccess_DoesNotInheritLessRestrictive()
        {
            var parentDir = Path.Combine(_tempDir, "parent");
            var childDir = Path.Combine(parentDir, "child");

            // Set parent to full access, child to restricted
            _fileSystem.SetDirectoryAccess(parentDir, DirectoryAccess.ListAndCreateFiles);
            _fileSystem.SetDirectoryAccess(childDir, DirectoryAccess.List);

            // Child should keep its own more restrictive setting
            var childAccess = _fileSystem.GetDirectoryAccess(childDir);
            Assert.That(childAccess, Is.EqualTo(DirectoryAccess.List));
        }
    }
}