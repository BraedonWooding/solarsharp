using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides comprehensive path security validation to prevent path traversal and other attacks
    /// </summary>
    public static class PathSecurityValidator
    {
        private static readonly CrossPlatformPathCanonicalizer Canonicalizer =
            new CrossPlatformPathCanonicalizer();

        private static readonly ImmutableHashSet<string> DangerousExtensions =
            ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                ".exe",
                ".bat",
                ".cmd",
                ".com",
                ".scr",
                ".pif",
                ".msi",
                ".dll",
                ".sys",
                ".bin",
                ".app"
            );

        private static readonly ImmutableHashSet<string> DangerousFileNames =
            ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                "con",
                "prn",
                "aux",
                "nul",
                "com1",
                "com2",
                "com3",
                "com4",
                "com5",
                "com6",
                "com7",
                "com8",
                "com9",
                "lpt1",
                "lpt2",
                "lpt3",
                "lpt4",
                "lpt5",
                "lpt6",
                "lpt7",
                "lpt8",
                "lpt9"
            );

        private static readonly Regex PathTraversalPattern = new Regex(
            @"(\.\.[\\/]|[\\/]\.\.[\\/]|[\\/]\.\.$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Validates a file path for security issues
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <param name="sandboxRoot">Optional sandbox root directory</param>
        /// <returns>Result indicating if the path is secure</returns>
        public static Result<string, PathSecurityViolation> ValidatePath(
            string path,
            string sandboxRoot = null
        )
        {
            // Basic validation
            if (string.IsNullOrEmpty(path))
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Path cannot be null or empty",
                        PathViolationType.InvalidPath,
                        path ?? ""
                    )
                );

            // First normalize the path for consistent checks
            var normalizedPath = NormalizePath(path);

            // Check for path traversal patterns before canonicalization
            var traversalResult = CheckPathTraversal(path);
            if (traversalResult.IsFailure)
                return traversalResult;

            // Check for dangerous file names (CON, PRN, etc.)
            var dangerousNameResult = CheckDangerousFileName(normalizedPath);
            if (dangerousNameResult.IsFailure)
                return dangerousNameResult;

            // Check for dangerous extensions
            var extensionResult = CheckDangerousExtension(normalizedPath);
            if (extensionResult.IsFailure)
                return extensionResult;

            // Check for control characters
            var controlCharResult = CheckControlCharacters(path);
            if (controlCharResult.IsFailure)
                return controlCharResult;

            // Check for bidirectional text attacks
            var unicodeResult = CheckUnicodeAttacks(path);
            if (unicodeResult.IsFailure)
                return unicodeResult;

            // Use the canonicalizer for sandbox validation if needed
            if (string.IsNullOrEmpty(sandboxRoot))
            {
                return Result.Success<string, PathSecurityViolation>(normalizedPath);
            }

            var canonicalResult = Canonicalizer.Canonicalize(path, sandboxRoot);
            return canonicalResult.IsFailure
                ? Result.Failure<string, PathSecurityViolation>(canonicalResult.Error)
                : Result.Success<string, PathSecurityViolation>(normalizedPath);
        }

        /// <summary>
        /// Normalizes a path for consistent validation
        /// </summary>
        private static string NormalizePath(string path)
        {
            // Replace backslashes with forward slashes for consistent handling
            var normalized = path.Replace('\\', '/');

            // Remove duplicate slashes
            normalized = Regex.Replace(normalized, @"/+", "/");

            // Remove trailing slashes (except for root)
            if (normalized.Length > 1 && normalized.EndsWith("/"))
                normalized = normalized.TrimEnd('/');

            return normalized;
        }

        /// <summary>
        /// Checks for path traversal attempts
        /// </summary>
        private static Result<string, PathSecurityViolation> CheckPathTraversal(string path)
        {
            // Check for obvious path traversal patterns
            if (PathTraversalPattern.IsMatch(path))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Path traversal detected",
                        PathViolationType.PathTraversal,
                        path
                    )
                );
            }

            // Check for encoded path traversal
            var decodedPath = Uri.UnescapeDataString(path);
            if (PathTraversalPattern.IsMatch(decodedPath))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Encoded path traversal detected",
                        PathViolationType.PathTraversal,
                        path
                    )
                );
            }

            // Unicode "path traversal" removed - filesystems don't interpret Unicode lookalikes as actual dots/slashes

            return Result.Success<string, PathSecurityViolation>(path);
        }

        /// <summary>
        /// Checks for dangerous file names (Windows reserved names)
        /// </summary>
        private static Result<string, PathSecurityViolation> CheckDangerousFileName(string path)
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                return Result.Success<string, PathSecurityViolation>(path);

            // Check the base name (without extension) and the full name
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            // Check if the base name is a reserved name
            if (DangerousFileNames.Contains(baseName))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        $"Reserved file name: {baseName}",
                        PathViolationType.DangerousFileName,
                        path
                    )
                );
            }

            // Also check the full filename in case it's exactly a reserved name
            if (DangerousFileNames.Contains(fileName))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        $"Reserved file name: {fileName}",
                        PathViolationType.DangerousFileName,
                        path
                    )
                );
            }

            return Result.Success<string, PathSecurityViolation>(path);
        }

        /// <summary>
        /// Checks for dangerous file extensions
        /// </summary>
        private static Result<string, PathSecurityViolation> CheckDangerousExtension(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return Result.Success<string, PathSecurityViolation>(path);

            if (DangerousExtensions.Contains(extension))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        $"Dangerous file extension: {extension}",
                        PathViolationType.DangerousExtension,
                        path
                    )
                );
            }

            return Result.Success<string, PathSecurityViolation>(path);
        }

        /// <summary>
        /// Checks for Unicode-based attacks
        /// </summary>
        private static Result<string, PathSecurityViolation> CheckUnicodeAttacks(string path)
        {
            // Check for bidirectional text attacks
            if (path.Any(static c => c is >= '\u202A' and <= '\u202E'))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Bidirectional text attack detected",
                        PathViolationType.UnicodeAttack,
                        path
                    )
                );
            }

            // Check for zero-width characters
            if (path.Any(static c => c is '\u200B' or '\u200C' or '\u200D' or '\uFEFF'))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Zero-width character detected",
                        PathViolationType.ZeroWidthCharacter,
                        path
                    )
                );
            }

            return Result.Success<string, PathSecurityViolation>(path);
        }

        /// <summary>
        /// Checks sandbox constraints
        /// </summary>
        private static Result<string, PathSecurityViolation> CheckSandboxConstraints(
            string path,
            string sandboxRoot
        )
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var fullSandboxRoot = Path.GetFullPath(sandboxRoot);

                if (!fullPath.StartsWith(fullSandboxRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<string, PathSecurityViolation>(
                        new PathSecurityViolation(
                            "Path outside sandbox",
                            PathViolationType.SandboxViolation,
                            path
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        $"Path validation error: {ex.Message}",
                        PathViolationType.InvalidPath,
                        path
                    )
                );
            }

            return Result.Success<string, PathSecurityViolation>(path);
        }

        /// <summary>
        /// Checks for control characters and null bytes
        /// </summary>
        private static Result<string, PathSecurityViolation> CheckControlCharacters(string path)
        {
            // Check for null bytes
            if (path.Contains('\0'))
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Null byte detected",
                        PathViolationType.ControlCharacter,
                        path
                    )
                );
            }

            // Check for other dangerous control characters
            var dangerousChars = path.Where(static c =>
                    char.IsControl(c) && c != '\t' && c != '\r' && c != '\n'
                )
                .ToArray();
            if (dangerousChars.Length > 0)
            {
                return Result.Failure<string, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Control characters detected",
                        PathViolationType.ControlCharacter,
                        path
                    )
                );
            }

            return Result.Success<string, PathSecurityViolation>(path);
        }
    }

    /// <summary>
    /// Represents a path security violation
    /// </summary>
    public sealed record PathSecurityViolation(
        string Message,
        PathViolationType ViolationType,
        string Path
    );

    /// <summary>
    /// Types of path security violations
    /// </summary>
    public enum PathViolationType
    {
        InvalidPath,
        PathTraversal,
        DangerousFileName,
        DangerousExtension,
        UnicodeAttack,
        SandboxViolation,
        ControlCharacter,
        ZeroWidthCharacter,
        PlatformSpecific,
    }
}
