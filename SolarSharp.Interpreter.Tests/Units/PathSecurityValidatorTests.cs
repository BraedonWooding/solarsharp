using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// A test suite for validating the <see cref="PathSecurityValidator"/> behavior and enforcing path security.
    /// </summary>
    /// <remarks>
    /// This test class ensures that the <see cref="PathSecurityValidator"/> correctly identifies and handles various security
    /// threats related to file paths, such as null paths, path traversal, reserved names, dangerous file extensions, and control characters.
    /// It also verifies the normalization and sanitization of valid paths.
    /// </remarks>
    [TestFixture]
    [Category("Path.Unit")]
    public class PathSecurityValidatorTests
    {
        /// <summary>
        /// Validates that a given path is a valid, secure, and usable file path.
        /// </summary>
        /// <remarks>
        /// This test verifies that the method <c>ValidatePath</c> correctly validates a valid file path
        /// by ensuring that no security rules are violated and the path is normalized properly. The path
        /// "data/config.json" is used as a valid input example.
        /// </remarks>
        /// <returns>
        /// Passes the test if the validation succeeds, returning a success result. The success result
        /// confirms the input path "data/config.json" is valid and remains unchanged.
        /// </returns>
        /// <exception cref="PathSecurityViolation">
        /// Indicates that the path failed security checks due to invalid characters, path traversal attempts,
        /// or other security-related issues.
        /// </exception>
        /// <seealso cref="PathSecurityValidator.ValidatePath"/>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_ValidPath_ReturnsSuccess()
        {
            var result = PathSecurityValidator.ValidatePath("data/config.json");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("data/config.json"));
        }

        /// <summary>
        /// Tests the behavior of the <see cref="PathSecurityValidator.ValidatePath"/> method
        /// when null is passed as the path argument.
        /// </summary>
        /// <remarks>
        /// This test ensures that the method properly handles null inputs by returning
        /// a failure result. The failure should indicate that the path is invalid, and the
        /// associated violation type should be <see cref="PathViolationType.InvalidPath"/>.
        /// </remarks>
        /// <returns>
        /// Asserts that the result's <c>IsFailure</c> property evaluates to true, and the
        /// <c>Error.ViolationType</c> is set to <c>PathViolationType.InvalidPath</c>.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_NullPath_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath(null);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.InvalidPath));
        }

        /// <summary>
        /// Validates that an empty path is considered invalid and returns a failure result.
        /// </summary>
        /// <remarks>
        /// This test ensures the `PathSecurityValidator.ValidatePath` method correctly identifies
        /// an empty string as an invalid path and returns an appropriate failure result.
        /// The result is expected to indicate the failure type as `PathViolationType.InvalidPath`.
        /// </remarks>
        /// <example>
        /// This test validates an edge case where the input path is an empty string.
        /// </example>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_EmptyPath_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath("");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.InvalidPath));
        }

        /// <summary>
        /// Tests whether the <c>ValidatePath</c> method correctly identifies
        /// and fails when a path contains traversal sequences such as ".."
        /// which can potentially escape the intended directory scope.
        /// </summary>
        /// <remarks>
        /// This test validates the detection of path traversal vulnerabilities
        /// to ensure the security of the file system by preventing unauthorized
        /// access to parent directories or critical system files.
        /// </remarks>
        /// <example>
        /// A path like "../../../etc/passwd" is used to confirm the detection
        /// of traversal sequences and that a failure result is returned with
        /// a violation type of <c>PathViolationType.PathTraversal</c>.
        /// </example>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_PathTraversal_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath("../../../etc/passwd");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PathTraversal));
        }

        /// <summary>
        /// Validates a given file path to check for traversal attempts involving slashes
        /// that could allow access to directories outside the permitted structure.
        /// </summary>
        /// <returns>
        /// Returns a failure result when the path contains traversal sequences
        /// (e.g., '../', or '..\' mixed with slashes for directory traversal),
        /// with the violation type set to <see cref="PathViolationType.PathTraversal"/>.
        /// </returns>
        /// <remarks>
        /// This test explicitly checks for paths that attempt directory traversal
        /// using sequences of slashes (both forward and backward) to ensure
        /// the validation logic correctly identifies and blocks such patterns.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_PathTraversalWithSlashes_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath("data/../../../etc/passwd");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PathTraversal));
        }

        /// <summary>
        /// Validates a path containing encoded path traversal sequences to ensure its security.
        /// </summary>
        /// <remarks>
        /// This method tests the `PathSecurityValidator.ValidatePath` function by providing a path
        /// that includes URL-encoded path traversal sequences (e.g., "%2E%2E/%2E%2E/etc/passwd").
        /// The purpose is to verify that the validator appropriately identifies the path as a
        /// security threat and returns a failure result.
        /// </remarks>
        /// <returns>
        /// A test asserting that the validation result indicates failure and the violation
        /// type is correctly identified as <see cref="PathViolationType.PathTraversal"/>.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_EncodedPathTraversal_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath("data/%2E%2E%2F%2E%2E%2Fetc/passwd");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PathTraversal));
        }

        /// <summary>
        /// Validates a given file path to ensure it does not contain null bytes, as they may
        /// indicate a control character that could lead to security vulnerabilities, such as
        /// interpreting unexpected file parts or bypassing file validation logic.
        /// </summary>
        /// <remarks>
        /// This test method specifically verifies that the <see cref="PathSecurityValidator.ValidatePath"/>
        /// method correctly identifies and rejects paths containing null characters (`\0`)
        /// by returning a failure result with a violation type of <c>PathViolationType.ControlCharacter</c>.
        /// </remarks>
        /// <returns>
        /// Ensures the validation result marks the path as a failure and assigns the
        /// appropriate <see cref="PathViolationType.ControlCharacter"/> violation type.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_NullByte_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath("file.txt\0");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.ControlCharacter));
        }

        /// <summary>
        /// Tests the path validation logic to ensure detection of bidirectional text attacks
        /// that may manipulate the visual rendering of file paths.
        /// </summary>
        /// <remarks>
        /// A bidirectional text attack involves using special Unicode characters, such as
        /// right-to-left override (\u202E), to disguise malicious file names by altering
        /// how they appear to a user or system.
        /// </remarks>
        /// <returns>
        /// Ensures the path validation logic identifies the attack and returns a failure result
        /// with the appropriate violation type set to <see cref="PathViolationType.UnicodeAttack"/>.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_BidirectionalTextAttack_ReturnsFailure()
        {
            var result = PathSecurityValidator.ValidatePath("file\u202Etxt.json");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.UnicodeAttack));
        }

        /// <summary>
        /// Verifies that the provided path is normalized by converting backslashes to forward slashes.
        /// Ensures consistency in path representation across different operating systems and environments.
        /// </summary>
        /// <remarks>
        /// This method converts all instances of '\\' in the input path to '/' to maintain a uniform path format.
        /// It is designed to prevent security issues arising due to inconsistent path formats while preserving the
        /// logical structure of the original path.
        /// </remarks>
        /// <example>
        /// Input: "data\\config\\file.json"
        /// Output: "data/config/file.json"
        /// </example>
        /// <param name="path">The file path to normalize and validate.</param>
        /// <returns>
        /// A result object indicating success if the path is successfully normalized to use forward slashes.
        /// If the path contains security violations, the method will return a failure with an appropriate violation.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_NormalizesSlashes()
        {
            var result = PathSecurityValidator.ValidatePath("data\\\\config\\\\file.json");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("data/config/file.json"));
        }

        /// <summary>
        /// Validates and sanitizes the specified file path by removing duplicate slashes.
        /// </summary>
        /// <remarks>
        /// This method ensures that the given path is normalized by collapsing redundant slashes
        /// into a single slash. It is part of the path validation process to ensure the format
        /// of the path conforms to the expected structure.
        /// </remarks>
        /// <param name="path">The file path to validate and normalize.</param>
        /// <returns>
        /// A result object containing the sanitized path if successful, or an error indicating
        /// a path security violation if the path fails validation.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_RemovesDuplicateSlashes()
        {
            var result = PathSecurityValidator.ValidatePath("data///config//file.json");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("data/config/file.json"));
        }

        /// <summary>
        /// Validates a given file or directory path and ensures that any trailing slashes are removed from the path string.
        /// </summary>
        /// <remarks>
        /// This method is used to enforce a consistent formatting of file or directory paths by stripping any unnecessary trailing slashes.
        /// This operation helps in preventing path discrepancies and ensuring compatibility with further operations that rely on clean paths.
        /// </remarks>
        /// <param name="path">The path to validate and clean up by removing trailing slashes.</param>
        /// <returns>
        /// A <c>Result</c> object indicating the success or failure of the validation.
        /// On success, the <c>Value</c> of the result will be the normalized path with trailing slashes removed.
        /// On failure, a <c>PathSecurityViolation</c> object represents the reason for the failure.
        /// </returns>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_RemovesTrailingSlashes()
        {
            var result = PathSecurityValidator.ValidatePath("data/config/");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("data/config"));
        }

        /// <summary>
        /// Validates that the given path preserves a leading root slash ('/') when it exists.
        /// Ensures that the root slash is neither removed nor altered during validation.
        /// </summary>
        /// <remarks>
        /// This test is specifically designed to verify that the root slash of an absolute path is preserved
        /// during validation by the <c>PathSecurityValidator.ValidatePath</c> method.
        /// The test uses "/" as input and expects a successful result with the value retained as "/".
        /// </remarks>
        /// <example>
        /// This method does not provide example usage.
        /// </example>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void ValidatePath_PreservesRootSlash()
        {
            var result = PathSecurityValidator.ValidatePath("/");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("/"));
        }

        /// <summary>
        /// Validates a series of paths to ensure they are secure and returns success for valid paths.
        /// </summary>
        /// <param name="path">The file path to validate for security compliance.</param>
        /// <remarks>
        /// This method is designed as part of a test suite to verify that valid paths,
        /// including both relative and absolute paths, pass validation successfully.
        /// It ensures that proper file paths are processed without errors or security violations.
        /// </remarks>
        [TestCase("data/file.txt")]
        [TestCase("logs/2024/app.log")]
        [TestCase("/absolute/path/file.json")]
        [TestCase("simple.txt")]
        public void ValidatePath_ValidPaths_ReturnSuccess(string path)
        {
            var result = PathSecurityValidator.ValidatePath(path);

            Assert.That(result.IsSuccess, Is.True);
        }

        /// <summary>
        /// Validates if the provided path contains path traversal patterns and ensures it fails when such patterns are detected.
        /// </summary>
        /// <param name="path">The file path string to be validated for security and safety issues related to path traversal.</param>
        [TestCase("../file.txt")]
        [TestCase("..\\file.txt")]
        [TestCase("data/../file.txt")]
        [TestCase("data/../../file.txt")]
        [TestCase("/data/../file.txt")]
        public void ValidatePath_PathTraversalVariants_ReturnFailure(string path)
        {
            var result = PathSecurityValidator.ValidatePath(path);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PathTraversal));
        }

        /// <summary>
        /// Validates the specified file path to ensure it does not contain reserved names
        /// (e.g., special device names such as "CON", "PRN") that are disallowed by the system.
        /// If the path contains a reserved name, it returns a failure result indicating
        /// a dangerous file name violation.
        /// </summary>
        /// <param name="path">The file path to validate.</param>
        [TestCase("CON")]
        [TestCase("PRN.txt")]
        [TestCase("AUX.log")]
        [TestCase("COM1.dat")]
        [TestCase("LPT1.cfg")]
        public void ValidatePath_ReservedNames_ReturnFailure(string path)
        {
            var result = PathSecurityValidator.ValidatePath(path);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.DangerousFileName)
            );
        }

        /// <summary>
        /// Validates a file path and ensures that it fails validation if the path contains dangerous file extensions.
        /// </summary>
        /// <param name="path">The file path to validate.</param>
        [TestCase("script.exe")]
        [TestCase("batch.bat")]
        [TestCase("command.cmd")]
        [TestCase("library.dll")]
        [TestCase("driver.sys")]
        [TestCase("mac_program.app")]
        public void ValidatePath_DangerousExtensions_ReturnFailure(string path)
        {
            var result = PathSecurityValidator.ValidatePath(path);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.DangerousExtension)
            );
        }
    }
}
