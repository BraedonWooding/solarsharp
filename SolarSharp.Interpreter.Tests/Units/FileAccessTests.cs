using System;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Provides unit tests for verifying file and directory permission handling within the file system.
    /// </summary>
    /// <remarks>
    /// This test class is part of the security module and is designed to validate various aspects of file and directory access controls.
    /// The tests evaluate permission settings, inheritance mechanisms, wildcard matching for file and directory operations, and overall file system security rules.
    /// </remarks>
    /// <example>
    /// This class is utilized in a testing context to confirm expected behaviors of file access operations and permissions management.
    /// Common scenarios tested include permission inheritance, compatibility checks, wildcard pattern matching, and validation of security configurations.
    /// </example>
    [TestFixture]
    [Category("SecurityTest")]
    public class FilePermissionsTests
    {
        /// <summary>
        /// Temporary directory path used for file system operations, created during setup
        /// for test isolation and automatically cleaned up after the tests are completed.
        /// </summary>
        private string _tempDir;

        /// <summary>
        /// Represents a private instance of the <see cref="FileSystemSecurity"/> class used for testing
        /// file and directory permission configurations and operations within the unit tests.
        /// This variable is initialized during the setup phase of the tests and is used for validating
        /// and manipulating access control specific to files and directories.
        /// </summary>
        private FileSystemSecurity _fileSystem;

        /// <summary>
        /// Sets up the required environment and resources needed for testing file and directory
        /// permissions within the context of the FilePermissionsTests class. This method:
        /// - Creates a unique temporary directory for each test run.
        /// - Initializes an instance of the FileSystemSecurity class to configure and verify
        /// file system behaviors during tests.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            
            _fileSystem = new FileSystemSecurity();
        }

        /// <summary>
        /// Cleans up resources and temporary data created during the test execution.
        /// </summary>
        /// <remarks>
        /// Deletes the temporary directory created in the setup phase to ensure a clean state
        /// after each test execution. It verifies if the directory exists before attempting to delete it.
        /// This method is executed after each test in the test fixture.
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        /// <summary>
        /// Tests that specific file permissions are correctly set on a file.
        /// </summary>
        /// <remarks>
        /// This test verifies that when file permissions are assigned to a file through the FileSystemSecurity class,
        /// the permissions are applied accurately and can be retrieved for validation.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the retrieved file permissions do not match the expected value after setting them.
        /// </exception>
        [Test]
        public void SetFilePermissions_SetsSpecificFilePermissions()
        {
            var filePath = Path.Combine(_tempDir, "test.txt");
            

            _fileSystem.SetFilePermissions(filePath, FilePermissions.Read);

            Assert.That(_fileSystem.GetFilePermissions(filePath), Is.EqualTo(FilePermissions.Read));

        }

        /// <summary>
        /// Verifies that the specific directory permissions can be set for a specified directory path
        /// and ensures the permissions are applied correctly.
        /// </summary>
        /// <remarks>
        /// This test creates a subdirectory under a temporary directory and applies specified
        /// directory permissions using the <see cref="FileSystemSecurity.SetDirectoryPermissions"/> method.
        /// It then validates that the applied permissions match the expected permissions using
        /// <see cref="FileSystemSecurity.GetDirectoryPermissions"/>.
        /// </remarks>
        /// <example>
        /// This test checks and asserts that the "List" permission is correctly set and verified
        /// for a directory within the file system.
        /// </example>
        [Test]
        public void SetDirectoryAccess_SetsSpecificDirectoryPermissions()
        {
            var dirPath = Path.Combine(_tempDir, "subdir");

            _fileSystem.SetDirectoryPermissions(dirPath, DirectoryPermissions.List);

            Assert.That(_fileSystem.GetDirectoryPermissions(dirPath), Is.EqualTo(DirectoryPermissions.List));
        }

        /// <summary>
        /// Validates that the <see cref="FileSystemSecurity.GetFilePermissions"/> method
        /// returns the default file permission <see cref="FilePermissions.SandboxedReadWrite"/>
        /// when file permissions are not explicitly specified for a given file.
        /// </summary>
        [Test]
        public void GetFilePermissions_ReturnsDefaultForUnspecifiedFiles()
        {
            var filePath = Path.Combine(_tempDir, "unspecified.txt");

            var access = _fileSystem.GetFilePermissions(filePath);

            Assert.That(access, Is.EqualTo(FilePermissions.SandboxedReadWrite));
        }

        /// <summary>
        /// Validates that retrieving permissions for directories
        /// without explicitly assigned permissions returns the default value
        /// of <see cref="DirectoryPermissions.ListAndCreateFiles"/>.
        /// </summary>
        /// <remarks>
        /// This test ensures that the system applies default permissions to
        /// unspecified directories, specifically verifying that the returned
        /// permissions match the expected behavior defined by the application.
        /// </remarks>
        [Test]
        public void GetDirectoryPermissions_ReturnsDefaultForUnspecifiedDirectories()
        {
            var dirPath = Path.Combine(_tempDir, "unspecified");

            var access = _fileSystem.GetDirectoryPermissions(dirPath);

            Assert.That(access, Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
        }

        /// <summary>
        /// Tests that the file permissions for a specific file within a directory
        /// correctly inherit a "denied access" state when the parent directory
        /// has its permissions set to "None".
        /// </summary>
        /// <remarks>
        /// This method ensures the behavior of file permission inheritance when
        /// the parent directory denies all access. It verifies that the inherited
        /// permissions prevent any file access by default.
        /// </remarks>
        /// <example>
        /// This method is useful for scenarios where strict security policies
        /// enforce inheritance of restricted access settings from parent directories.
        /// </example>
        [Test]
        public void GetFilePermissions_InheritsDeniedAccessFromParentDirectory()
        {
            var parentDir = Path.Combine(_tempDir, "parent");
            var filePath = Path.Combine(parentDir, "file.txt");

            _fileSystem.SetDirectoryPermissions(parentDir, DirectoryPermissions.None);

            var access = _fileSystem.GetFilePermissions(filePath);

            Assert.That(access, Is.EqualTo(FilePermissions.None));
        }

        /// <summary>
        /// Verifies that a file operation is allowed when the file has the appropriate read permissions.
        /// </summary>
        /// <remarks>
        /// This test ensures that when a file is granted the read permission, the read operation is permitted by the implemented file system security.
        /// It validates the correct behavior of the <see cref="FileSystemSecurity.IsFileOperationPermitted"/> method for the read operation.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the result of the read permission check does not match the expected value.
        /// </exception>
        [Test]
        public void IsFileOperationAllowed_ReadAllowedWithReadPermission()
        {
            var filePath = Path.Combine(_tempDir, "readable.txt");
            _fileSystem.SetFilePermissions(filePath, FilePermissions.Read);

            var allowed = _fileSystem.IsFileOperationPermitted(filePath, FileOperation.Read);

            Assert.That(allowed, Is.True);
        }

        /// <summary>
        /// Verifies that a write operation is not permitted when a file has read-only permissions set.
        /// </summary>
        /// <remarks>
        /// This test sets the file permissions of a specified file to read-only and attempts to perform
        /// a write operation on it. The method asserts that the write operation is blocked as expected.
        /// </remarks>
        [Test]
        public void IsFileOperationAllowed_WriteBlockedWithReadOnlyPermission()
        {
            var filePath = Path.Combine(_tempDir, "readonly.txt");
            _fileSystem.SetFilePermissions(filePath, FilePermissions.Read);

            var allowed = _fileSystem.IsFileOperationPermitted(filePath, FileOperation.Write);

            Assert.That(allowed, Is.False);
        }

        /// <summary>
        /// Verifies that file creation operations are only permitted when the file
        /// has a minimum permission level of SandboxedReadWrite or higher.
        /// This method checks the behavior of the file system security by asserting
        /// that file creation is not allowed for files with Read-only permission
        /// but is permitted for files that have SandboxedReadWrite permission.
        /// Test Scenario:
        /// - If a file is configured with Read-only permissions, the file creation
        /// operation should be denied.
        /// - If a file is configured with SandboxedReadWrite permissions, the file
        /// creation operation should be allowed.
        /// Assertions:
        /// - Ensures that the creation of a file with Read permissions is denied.
        /// - Ensures that the creation of a file with SandboxedReadWrite permissions is allowed.
        /// Usage Context:
        /// Designed for testing scenarios where file access permissions dictate
        /// the ability to perform creation operations in a secured file system environment.
        /// </summary>
        [Test]
        public void IsFileOperationAllowed_CreateRequiresSandboxedOrHigher()
        {
            var filePath1 = Path.Combine(_tempDir, "read-only.txt");
            var filePath2 = Path.Combine(_tempDir, "sandboxed.txt");

            _fileSystem.SetFilePermissions(filePath1, FilePermissions.Read);
            _fileSystem.SetFilePermissions(filePath2, FilePermissions.SandboxedReadWrite);

            Assert.Multiple(() =>
            {
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath1, FileOperation.Create), Is.False);
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath2, FileOperation.Create), Is.True);
            });
        }

        /// <summary>
        /// Tests if a file delete operation is permitted based on the file's permission level.
        /// Delete operations are only allowed when the file has Full ReadWrite permissions.
        /// </summary>
        /// <remarks>
        /// This test validates that files with SandboxedReadWrite permissions do not allow delete operations,
        /// while files with ReadWrite permissions do.
        /// </remarks>
        /// <example>
        /// Configures file permissions and verifies the allowed or denied delete operation
        /// results using assertions based on specific permissions set for the test files.
        /// </example>
        [Test]
        public void IsFileOperationAllowed_DeleteRequiresFullReadWrite()
        {
            var filePath1 = Path.Combine(_tempDir, "sandboxed.txt");
            var filePath2 = Path.Combine(_tempDir, "full-access.txt");

            _fileSystem.SetFilePermissions(filePath1, FilePermissions.SandboxedReadWrite);
            _fileSystem.SetFilePermissions(filePath2, FilePermissions.ReadWrite);

            Assert.Multiple(() =>
            {
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath1, FileOperation.Delete), Is.False);
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath2, FileOperation.Delete), Is.True);
            });
        }

        /// <summary>
        /// Validates that file operations on a specific file require appropriate directory-level access.
        /// </summary>
        /// <remarks>
        /// This method ensures that even if a file has specific permissions (e.g., ReadWrite), its parent directory's permissions (e.g., None)
        /// can restrict allowed operations. Operations such as reading or writing to the file will be blocked if the directory denies access.
        /// </remarks>
        /// <param name="parentDir">Path to the parent directory containing the target file.</param>
        /// <param name="filePath">Path to the specific file being validated for operation permissions.</param>
        /// <param name="_fileSystem">The instance of the FileSystemSecurity class used to manage file and directory permissions.</param>
        [Test]
        public void IsFileOperationAllowed_RequiresDirectoryAccess()
        {
            var parentDir = Path.Combine(_tempDir, "restricted");
            var filePath = Path.Combine(parentDir, "file.txt");

            _fileSystem.SetDirectoryPermissions(parentDir, DirectoryPermissions.None);
            _fileSystem.SetFilePermissions(filePath, FilePermissions.ReadWrite);

            Assert.Multiple(() =>
            {
                // File has ReadWrite but directory has None - should block all operations
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath, FileOperation.Read), Is.False);
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath, FileOperation.Write), Is.False);
            });
        }

        /// <summary>
        /// Verifies that a create file operation within directories with "List and Create Files" permissions is allowed,
        /// and that reading is permitted in "List only" directories but creating files is not.
        /// </summary>
        /// <remarks>
        /// This test focuses on specific directory and file permissions to ensure that create operations
        /// are affected by the permissions set on the parent directory. It uses the DirectoryPermissions.List
        /// and FilePermissions.SandboxedReadWrite for validating behavior. The test case evaluates scenarios
        /// where reading a file is allowed but creating it is blocked due to insufficient directory permissions.
        /// </remarks>
        /// <seealso cref="SolarSharp.Interpreter.Security.DirectoryPermissions"/>
        /// <seealso cref="SolarSharp.Interpreter.Security.FilePermissions"/>
        [Test]
        public void IsFileOperationAllowed_CreateRequiresListAndCreateFiles()
        {
            var parentDir = Path.Combine(_tempDir, "listonly");
            var filePath = Path.Combine(parentDir, "newfile.txt");

            _fileSystem.SetDirectoryPermissions(parentDir, DirectoryPermissions.List);
            _fileSystem.SetFilePermissions(filePath, FilePermissions.SandboxedReadWrite);

            Assert.Multiple(() =>
            {
                // Can read but not create in list-only directory
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath, FileOperation.Read), Is.True);
                Assert.That(_fileSystem.IsFileOperationPermitted(filePath, FileOperation.Create), Is.False);
            });
        }

        /// <summary>
        /// Validates the compatibility between file permissions and directory permissions
        /// using the <c>IsCompatibleWith</c> extension method. Ensures that file access
        /// operations conform to the restrictions or allowances set by directory access permissions.
        /// </summary>
        /// <remarks>
        /// The method performs multiple assertions to verify the expected behavior of the
        /// compatibility check. It tests allowed and denied scenarios based on different
        /// levels of file and directory permissions.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if any of the compatibility validations fail to match the expected outcome.
        /// </exception>
        [Test]
        public void IsCompatibleWith_ValidatesFileDirectoryCompatibility()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FilePermissions.Read.IsCompatibleWith(DirectoryPermissions.List), Is.True);
                Assert.That(FilePermissions.SandboxedReadWrite.IsCompatibleWith(DirectoryPermissions.ListAndCreateFiles), Is.True);
                Assert.That(FilePermissions.SandboxedReadWrite.IsCompatibleWith(DirectoryPermissions.List), Is.False);
                Assert.That(FilePermissions.Read.IsCompatibleWith(DirectoryPermissions.None), Is.False);
            });
        }

        /// <summary>
        /// Asserts that the <see cref="string.MatchesWildcard"/> extension method correctly evaluates wildcard patterns
        /// for simple, non-recursive scenarios. Validates that file paths are matched against patterns such as
        /// extensions and prefixed directories.
        /// </summary>
        /// <remarks>
        /// This test checks whether specific file paths match against given wildcard patterns like "*.txt"
        /// or "data/*.lua". It also ensures incorrect patterns do not falsely return matches.
        /// </remarks>
        /// <example>
        /// Examples include matching "test.txt" with "*.txt", "data/file.lua" with "data/*.lua", and verifying
        /// that incompatible matches (e.g., "*.lua" for "test.txt") return false.
        /// </example>
        [Test]
        public void MatchesWildcard_SimplePatterns()
        {
            Assert.Multiple(() =>
            {
                Assert.That("test.txt".MatchesWildcard("*.txt"), Is.True);
                Assert.That("data/file.lua".MatchesWildcard("data/*.lua"), Is.True);
                Assert.That("test.txt".MatchesWildcard("*.lua"), Is.False);
            });
        }

        /// <summary>
        /// Tests the behavior of a string path matching recursive wildcard patterns.
        /// </summary>
        /// <remarks>
        /// Verifies that the path correctly matches recursive wildcard patterns (**),
        /// and ensures the correct results for valid and invalid inputs
        /// when matching against the specified wildcard patterns.
        /// </remarks>
        [Test]
        public void MatchesWildcard_RecursivePatterns()
        {
            Assert.Multiple(static () =>
            {
                Assert.That("deep/nested/file.txt".MatchesWildcard("**/*.txt"), Is.True);
                Assert.That("deep/nested/path/file.lua".MatchesWildcard("deep/**"), Is.True);
                Assert.That("other/file.txt".MatchesWildcard("deep/**"), Is.False);
            });
        }

        /// <summary>
        /// Validates whether a specified directory path matches a given wildcard pattern.
        /// </summary>
        /// <remarks>
        /// This method is used to determine if different directory paths conform to specified patterns
        /// with support for recursive wildcards (**).
        /// </remarks>
        /// <param name="path">
        /// The directory path to be checked against the wildcard pattern.
        /// </param>
        /// <param name="pattern">
        /// The wildcard pattern that supports recursive matching of directories.
        /// </param>
        /// <returns>
        /// Returns true if the directory path matches the given wildcard pattern; otherwise, false.
        /// </returns>
        [Test]
        public void MatchesWildcard_DirectoryPatterns()
        {
            Assert.Multiple(() =>
            {
                Assert.That("scripts/main.lua".MatchesWildcard("scripts/**"), Is.True);
                Assert.That("scripts/utils/helper.lua".MatchesWildcard("scripts/**"), Is.True);
                Assert.That("other/main.lua".MatchesWildcard("scripts/**"), Is.False);
            });
        }

        /// <summary>
        /// Validates that the <see cref="SecurityConfiguration.SetFilePermissions"/> method
        /// correctly updates the file system permissions for a specified file.
        /// </summary>
        /// <remarks>
        /// This test ensures that when file permissions are set via the <see cref="SetFilePermissions"/> method,
        /// the specified permissions are accurately reflected within the <see cref="FileSystemSecurity"/> instance.
        /// </remarks>
        [Test]
        public void SecurityConfiguration_SetFilePermissions_UpdatesFileSystem()
        {
            var config = new SecurityConfiguration();
            var filePath = Path.Combine(_tempDir, "test.txt");

            config.SetFilePermissions(filePath, FilePermissions.Read);

            Assert.That(config.FileSystem.GetFilePermissions(filePath), Is.EqualTo(FilePermissions.Read));
        }

        /// <summary>
        /// Tests the SecurityConfiguration class to ensure that setting directory access permissions
        /// updates the corresponding FileSystem behavior.
        /// </summary>
        /// <remarks>
        /// This method verifies that when directory access permissions are set using the
        /// <see cref="SolarSharp.Interpreter.Security.SecurityConfiguration.SetDirectoryPermissions"/> method,
        /// the changes are reflected correctly in the FileSystem's directory permissions.
        /// </remarks>
        /// <example>
        /// This test is designed to assert that a specified directory, after being configured with specific
        /// <see cref="SolarSharp.Interpreter.Security.DirectoryPermissions"/>, behaves as expected when queried through
        /// the <see cref="SolarSharp.Interpreter.Security.FileSystemSecurity.GetDirectoryPermissions"/> method.
        /// </example>
        /// <seealso cref="SolarSharp.Interpreter.Security.SecurityConfiguration"/>
        /// <seealso cref="SolarSharp.Interpreter.Security.DirectoryPermissions"/>
        /// <seealso cref="SolarSharp.Interpreter.Security.FileSystemSecurity"/>
        [Test]
        public void SecurityConfiguration_SetDirectoryAccess_UpdatesFileSystem()
        {
            var config = new SecurityConfiguration();
            var dirPath = Path.Combine(_tempDir, "testdir");

            config.SetDirectoryPermissions(dirPath, DirectoryPermissions.List);

            Assert.That(config.FileSystem.GetDirectoryPermissions(dirPath), Is.EqualTo(DirectoryPermissions.List));
        }

        /// <summary>
        /// Verifies that the fluent API methods of the <see cref="SecurityConfiguration"/> class chain correctly.
        /// </summary>
        /// <remarks>
        /// This test initializes a <see cref="SecurityConfiguration"/> instance and chains multiple calls to its methods.
        /// It then verifies that the resulting configuration contains the expected file and directory permissions,
        /// as well as default permissions for files and directories.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the configured permissions do not match the expected values.
        /// </exception>
        [Test]
        public void SecurityConfiguration_FluentAPI_ChainsCorrectly()
        {
            var config = new SecurityConfiguration()
                .SetFilePermissions("/app/config.txt", FilePermissions.Read)
                .SetDirectoryPermissions("/app/data", DirectoryPermissions.ListAndCreateFiles)
                .WithDefaultFilePermissions(FilePermissions.Read)
                .WithDefaultDirectoryPermissions(DirectoryPermissions.List);

            Assert.Multiple(() =>
            {
                Assert.That(config.FileSystem.GetFilePermissions("/app/config.txt"), Is.EqualTo(FilePermissions.Read));
                Assert.That(config.FileSystem.GetDirectoryPermissions("/app/data"), Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
                Assert.That(config.FileSystem.DefaultFilePermissions, Is.EqualTo(FilePermissions.Read));
                Assert.That(config.FileSystem.DefaultDirectoryPermissions, Is.EqualTo(DirectoryPermissions.List));
            });
        }

        /// <summary>
        /// Validates that the FileSystemValidator allows valid file operations
        /// based on the permissions assigned to a file.
        /// </summary>
        /// <remarks>
        /// This method assigns specific permissions to a file and uses
        /// FileSystemValidator to verify that read and write operations are allowed
        /// when corresponding permissions are in place. The test ensures that
        /// no exceptions are thrown for valid operations.
        /// </remarks>
        [Test]
        public void FileSystemValidator_AllowsValidOperations()
        {
            var filePath = Path.Combine(_tempDir, "valid.txt");
            _fileSystem.SetFilePermissions(filePath, FilePermissions.ReadWrite);

            var validator = new FileSystemValidator(_fileSystem);

            Assert.DoesNotThrow(() => validator.ValidateFilePermissions(filePath, FileOperation.Read));
            Assert.DoesNotThrow(() => validator.ValidateFilePermissions(filePath, FileOperation.Write));
        }

        /// <summary>
        /// Ensures that invalid file operations are correctly blocked based on the assigned file permissions.
        /// </summary>
        /// <remarks>
        /// This method verifies that a file operation adheres to the permissions set for a file.
        /// For example, if a file has Read permissions, a Write operation should throw a
        /// <see cref="FilePermissionViolationException"/>.
        /// It validates that operations permitted by the file's permissions succeed and that
        /// restricted operations are explicitly denied, ensuring robust security measures are enforced.
        /// </remarks>
        /// <exception cref="FilePermissionViolationException">
        /// Thrown when the attempted file operation violates the file's associated permissions.
        /// </exception>
        [Test]
        public void FileSystemValidator_BlocksInvalidOperations()
        {
            var filePath = Path.Combine(_tempDir, "readonly.txt");
            _fileSystem.SetFilePermissions(filePath, FilePermissions.Read);

            var validator = new FileSystemValidator(_fileSystem);

            Assert.DoesNotThrow(() => validator.ValidateFilePermissions(filePath, FileOperation.Read));
            Assert.Throws<FilePermissionViolationException>(() => validator.ValidateFilePermissions(filePath, FileOperation.Write));
        }

        /// <summary>
        /// Verifies that the file system validation logic correctly identifies and blocks
        /// attempted path traversal operations to ensure system security.
        /// </summary>
        /// <remarks>
        /// This test ensures that attempts to access restricted areas of the file system,
        /// such as parent directories outside of the designated sandbox, are detected and
        /// appropriately denied by throwing a <see cref="PathTraversalException"/>.
        /// </remarks>
        /// <example>
        /// The validator is initialized with a <see cref="FileSystemSecurity"/> object that
        /// defines a sandbox root directory. Paths attempting to navigate outside the
        /// sandbox (e.g., using `../..` constructs) are validated and flagged as security risks.
        /// </example>
        /// <exception cref="PathTraversalException">
        /// Thrown when a path traversal attempt is detected during file system validation.
        /// </exception>
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
                validator.ValidateFilePermissions("../../../etc/passwd", FileOperation.Read));
            Assert.Throws<PathTraversalException>(() =>
                validator.ValidateFilePermissions("..\\..\\windows\\system32", FileOperation.Read));
        }

        /// <summary>
        /// Tests the functionality of the <see cref="FileSystemValidator"/> to correctly validate directory operations
        /// against specific permissions set on directories.
        /// </summary>
        /// <remarks>
        /// This method validates the following scenarios:
        /// - Ensures that the "List" operation can be performed on a directory with "List" permissions.
        /// - Ensures that attempting to perform the "Create" operation on a directory with "List" permissions
        /// properly throws a <see cref="FilePermissionViolationException"/>.
        /// </remarks>
        /// <exception cref="FilePermissionViolationException">
        /// Thrown if an operation is attempted on a directory without the required permissions.
        /// </exception>
        [Test]
        public void FileSystemValidator_ValidatesDirectoryOperations()
        {
            var dirPath = Path.Combine(_tempDir, "listonly");
            _fileSystem.SetDirectoryPermissions(dirPath, DirectoryPermissions.List);

            var validator = new FileSystemValidator(_fileSystem);

            Assert.DoesNotThrow(() => validator.ValidateDirectoryAccess(dirPath, DirectoryOperation.List));
            Assert.Throws<FilePermissionViolationException>(() => validator.ValidateDirectoryAccess(dirPath, DirectoryOperation.Create));
        }

        /// <summary>
        /// Tests the conversion of string representations of file permissions into their corresponding
        /// <see cref="FilePermissions"/> enum values.
        /// </summary>
        /// <remarks>
        /// Validates proper parsing for known permission strings such as "none", "read", "readwrite", and
        /// "sandboxedreadwrite". For invalid strings, ensures a default permission is assigned.
        /// </remarks>
        /// <example>
        /// This method ensures that:
        /// - "none" is parsed as <see cref="FilePermissions.None"/>
        /// - "read" is parsed as <see cref="FilePermissions.Read"/>
        /// - "readwrite" is parsed as <see cref="FilePermissions.ReadWrite"/>
        /// - "sandboxedreadwrite" is parsed as <see cref="FilePermissions.SandboxedReadWrite"/>
        /// - Invalid strings default to <see cref="FilePermissions.SandboxedReadWrite"/>
        /// </example>
        /// <seealso cref="FileAccessExtensions.ParseFilePermissions(string)"/>
        [Test]
        public void ParseFilePermissions_ConvertsStringsCorrectly()
        {
            Assert.Multiple(() =>
            {
                Assert.That("none".ParseFilePermissions(), Is.EqualTo(FilePermissions.None));
                Assert.That("read".ParseFilePermissions(), Is.EqualTo(FilePermissions.Read));
                Assert.That("readwrite".ParseFilePermissions(), Is.EqualTo(FilePermissions.ReadWrite));
                Assert.That("sandboxedreadwrite".ParseFilePermissions(), Is.EqualTo(FilePermissions.SandboxedReadWrite));
                Assert.That("invalid".ParseFilePermissions(), Is.EqualTo(FilePermissions.SandboxedReadWrite)); // Default
            });
        }

        /// <summary>
        /// Tests the functionality of the <see cref="FileAccessExtensions.ParseDirectoryAccess"/> method
        /// to ensure it correctly converts string representations to corresponding
        /// <see cref="DirectoryPermissions"/> enum values.
        /// </summary>
        /// <remarks>
        /// Verifies multiple cases for mapping input strings to enum values:
        /// - "none" maps to <see cref="DirectoryPermissions.None"/>
        /// - "list" maps to <see cref="DirectoryPermissions.List"/>
        /// - "listandcreatefiles" maps to <see cref="DirectoryPermissions.ListAndCreateFiles"/>
        /// - Invalid strings default to <see cref="DirectoryPermissions.ListAndCreateFiles"/>
        /// </remarks>
        /// <seealso cref="DirectoryPermissions"/>
        [Test]
        public void ParseDirectoryAccess_ConvertsStringsCorrectly()
        {
            Assert.Multiple(() =>
            {
                Assert.That("none".ParseDirectoryAccess(), Is.EqualTo(DirectoryPermissions.None));
                Assert.That("list".ParseDirectoryAccess(), Is.EqualTo(DirectoryPermissions.List));
                Assert.That("listandcreatefiles".ParseDirectoryAccess(), Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
                Assert.That("invalid".ParseDirectoryAccess(), Is.EqualTo(DirectoryPermissions.ListAndCreateFiles)); // Default
            });
        }

        /// <summary>
        /// Tests the correct conversion of enumeration values to their corresponding manifest string representations.
        /// </summary>
        /// <remarks>
        /// Verifies that file and directory permissions enumerations are accurately transformed
        /// into manifest-compliant string representations. Specifically, the test ensures that
        /// each enumeration value for file and directory permissions, such as None, Read,
        /// ReadWrite, and SandboxReadWrite for files, and None, List, ListAndCreateFiles for directories,
        /// maps to the expected lowercase string values.
        /// </remarks>
        [Test]
        public void ToManifestString_ConvertsEnumsCorrectly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FilePermissions.None.ToManifestString(), Is.EqualTo("none"));
                Assert.That(FilePermissions.Read.ToManifestString(), Is.EqualTo("read"));
                Assert.That(FilePermissions.ReadWrite.ToManifestString(), Is.EqualTo("readwrite"));
                Assert.That(FilePermissions.SandboxedReadWrite.ToManifestString(), Is.EqualTo("sandboxedreadwrite"));

                Assert.That(DirectoryPermissions.None.ToManifestString(), Is.EqualTo("none"));
                Assert.That(DirectoryPermissions.List.ToManifestString(), Is.EqualTo("list"));
                Assert.That(DirectoryPermissions.ListAndCreateFiles.ToManifestString(), Is.EqualTo("listandcreatefiles"));
            });
        }

        /// <summary>
        /// Validates that the default access permissions in the file system security configuration
        /// provide sandboxed read-write access to files and list-and-create access to directories.
        /// </summary>
        /// <remarks>
        /// Ensures that the default permissions set in the `FileSystemSecurity` class are configured as follows:
        /// - Files are given `SandboxedReadWrite` level access by default.
        /// - Directories are given `ListAndCreateFiles` level access by default.
        /// This test guarantees that the default permissions are correctly established to provide a secure,
        /// sandboxed environment suitable for standard operations without granting more access than necessary.
        /// </remarks>
        [Test]
        public void DefaultAccess_ProvidesSandboxedReadWriteAndListAndCreateFiles()
        {
            var fs = new FileSystemSecurity();

            Assert.Multiple(() =>
            {
                Assert.That(fs.DefaultFilePermissions, Is.EqualTo(FilePermissions.SandboxedReadWrite));
                Assert.That(fs.DefaultDirectoryPermissions, Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
            });
        }

        /// <summary>
        /// This test verifies that the default access permissions of the file system
        /// security configuration allow reading, writing, and creating files, but
        /// do not permit deletion. These permissions represent common operations
        /// that are typically allowed under default access settings.
        /// </summary>
        [Test]
        public void DefaultAccess_AllowsCommonOperations()
        {
            var fs = new FileSystemSecurity();
            var filePath = Path.Combine(_tempDir, "default.txt");

            Assert.Multiple(() =>
            {
                Assert.That(fs.IsFileOperationPermitted(filePath, FileOperation.Read), Is.True);
                Assert.That(fs.IsFileOperationPermitted(filePath, FileOperation.Write), Is.True);
                Assert.That(fs.IsFileOperationPermitted(filePath, FileOperation.Create), Is.True);
                Assert.That(fs.IsFileOperationPermitted(filePath, FileOperation.Delete), Is.False); // Requires full ReadWrite
            });
        }

        /// <summary>
        /// Verifies that a child directory inherits restrictive access permissions
        /// from its parent directory if no explicit permissions are set for the child.
        /// </summary>
        /// <remarks>
        /// This method tests the behavior of directory permission inheritance within
        /// the file system security context. It ensures that when a parent directory
        /// is configured with restrictive permissions, any child directories default
        /// to the same restrictive permissions unless explicitly overridden.
        /// </remarks>
        /// <example>
        /// This test ensures functionality by setting permissions on a parent directory,
        /// and then verifying that a child directory inherits those permissions.
        /// </example>
        [Test]
        public void GetDirectoryAccess_InheritsFromParent()
        {
            var parentDir = Path.Combine(_tempDir, "parent");
            var childDir = Path.Combine(parentDir, "child");

            _fileSystem.SetDirectoryPermissions(parentDir, DirectoryPermissions.List);

            // Child should inherit more restrictive parent access
            var childAccess = _fileSystem.GetDirectoryPermissions(childDir);
            Assert.That(childAccess, Is.EqualTo(DirectoryPermissions.List));
        }

        /// <summary>
        /// Tests that a child directory does not inherit less restrictive permissions
        /// from its parent directory when specific permissions are explicitly set
        /// for the child directory.
        /// </summary>
        /// <remarks>
        /// This test ensures that the directory permission inheritance functionality
        /// adheres to the principle of respecting explicitly assigned permissions
        /// for child directories. Specifically, if a parent directory has less restrictive
        /// permissions and a child directory has more restrictive permissions, the
        /// child directory should maintain its assigned permissions.
        /// </remarks>
        /// <example>
        /// The test sets up a parent directory with less restrictive permissions
        /// (e.g., `ListAndCreateFiles`) and a child directory with more restrictive
        /// permissions (e.g., `List`). It then verifies that the child directory
        /// retains its more restrictive access settings.
        /// </example>
        /// <related>FileSystemSecurity, DirectoryPermissions</related>
        [Test]
        public void GetDirectoryAccess_DoesNotInheritLessRestrictive()
        {
            var parentDir = Path.Combine(_tempDir, "parent");
            var childDir = Path.Combine(parentDir, "child");

            // Set parent to full access, child to restricted
            _fileSystem.SetDirectoryPermissions(parentDir, DirectoryPermissions.ListAndCreateFiles);
            _fileSystem.SetDirectoryPermissions(childDir, DirectoryPermissions.List);

            // Child should keep its own more restrictive setting
            var childAccess = _fileSystem.GetDirectoryPermissions(childDir);
            Assert.That(childAccess, Is.EqualTo(DirectoryPermissions.List));
        }
    }
}