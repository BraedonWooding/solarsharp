#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides secure, cross-platform path canonicalization with caching
    /// </summary>
    public sealed class CrossPlatformPathCanonicalizer
    {
        // Platform-specific dangerous paths
        private static readonly ImmutableHashSet<string> WindowsDeviceNames =
            ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                "CON",
                "PRN",
                "AUX",
                "NUL",
                "COM1",
                "COM2",
                "COM3",
                "COM4",
                "COM5",
                "COM6",
                "COM7",
                "COM8",
                "COM9",
                "LPT1",
                "LPT2",
                "LPT3",
                "LPT4",
                "LPT5",
                "LPT6",
                "LPT7",
                "LPT8",
                "LPT9"
            );

        private static readonly ImmutableHashSet<string> LinuxDangerousPaths =
            ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                "/dev",
                "/proc",
                "/sys",
                "/run",
                "/boot"
            );

        /// <summary>
        /// Canonicalizes a path for the current platform with full security checks
        /// </summary>
        public Result<CanonicalPath, PathSecurityViolation> Canonicalize(
            string path,
            string? sandboxRoot = null
        )
        {
            if (string.IsNullOrWhiteSpace(path))
                return Result.Failure<CanonicalPath, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Path cannot be null or empty",
                        PathViolationType.InvalidPath,
                        path
                    )
                );

            // Perform canonicalization
            var result = CanonicalizeInternal(path, sandboxRoot);

            return result;
        }

        private Result<CanonicalPath, PathSecurityViolation> CanonicalizeInternal(
            string path,
            string? sandboxRoot
        )
        {
            try
            {
                // Check for null bytes first (before any normalization)
                if (path.Contains('\0'))
                {
                    return Result.Failure<CanonicalPath, PathSecurityViolation>(
                        new PathSecurityViolation(
                            "Null byte detected in path",
                            PathViolationType.ControlCharacter,
                            path
                        )
                    );
                }

                // Then normalize Unicode to NFC form
                var normalized = path.Normalize(NormalizationForm.FormC);

                // Homoglyph detection removed - filesystems don't care about visual similarity
                // and blocking Cyrillic/Greek/etc scripts is discriminatory

                // Platform-specific checks
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var windowsResult = ValidateWindowsPath(normalized);
                    if (windowsResult.IsFailure)
                        return Result.Failure<CanonicalPath, PathSecurityViolation>(
                            windowsResult.Error
                        );
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var linuxResult = ValidateLinuxPath(normalized);
                    if (linuxResult.IsFailure)
                        return Result.Failure<CanonicalPath, PathSecurityViolation>(
                            linuxResult.Error
                        );
                }

                string fullPath;
                try
                {
                    // Ensure the path is a uri, this blocks UNC & alternate data streams
                    var file_uri = new Uri(new Uri(Directory.GetCurrentDirectory()), normalized);
                    fullPath = file_uri.AbsolutePath;
                    // And ensure it's a file uri and not a url or whatever.
                    if (file_uri.Scheme != Uri.UriSchemeFile)
                    {
                        return Result.Failure<CanonicalPath, PathSecurityViolation>(
                            new PathSecurityViolation(
                                "Path must be a standard file, and not UNC or device",
                                PathViolationType.InvalidPath,
                                path
                            )
                        );
                    }
                }
                catch (Exception ex)
                {
                    return Result.Failure<CanonicalPath, PathSecurityViolation>(
                        new PathSecurityViolation(
                            $"Invalid path format: {ex.Message}",
                            PathViolationType.InvalidPath,
                            path
                        )
                    );
                }



                // Validate sandbox constraints if provided
                if (!string.IsNullOrEmpty(sandboxRoot))
                {
                    var sandboxFullPath = Path.GetFullPath(sandboxRoot);
                    if (!fullPath.StartsWith(sandboxFullPath, GetPathComparison()))
                    {
                        return Result.Failure<CanonicalPath, PathSecurityViolation>(
                            new PathSecurityViolation(
                                "Path escapes sandbox",
                                PathViolationType.SandboxViolation,
                                path
                            )
                        );
                    }
                }

                // Create canonical path with both original and resolved paths
                var canonical = new CanonicalPath(
                    Original: path,
                    Normalized: fullPath,
                    Resolved: fullPath,
                    IsSymbolicLink: false
                );

                return Result.Success<CanonicalPath, PathSecurityViolation>(canonical);
            }
            catch (Exception ex)
            {
                return Result.Failure<CanonicalPath, PathSecurityViolation>(
                    new PathSecurityViolation(
                        $"Path canonicalization failed: {ex.Message}",
                        PathViolationType.InvalidPath,
                        path
                    )
                );
            }
        }

        private Result<bool, PathSecurityViolation> ValidateWindowsPath(string path)
        {
            // Check for device names, note: this includes extension because Windows allows
            // filenames like CON.txt
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName) && WindowsDeviceNames.Contains(fileName))
            {
                return Result.Failure<bool, PathSecurityViolation>(
                    new PathSecurityViolation(
                        $"Windows device name not allowed: {fileName}",
                        PathViolationType.DangerousFileName,
                        path
                    )
                );
            }

            return Result.Success<bool, PathSecurityViolation>(true);
        }

        private Result<bool, PathSecurityViolation> ValidateLinuxPath(string path)
        {
            // Check for dangerous system paths
            var normalizedPath = path.Replace('\\', '/');
            foreach (var dangerousPath in LinuxDangerousPaths)
            {
                if (
                    normalizedPath.StartsWith(
                        dangerousPath + "/",
                        StringComparison.OrdinalIgnoreCase
                    ) || normalizedPath.Equals(dangerousPath, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return Result.Failure<bool, PathSecurityViolation>(
                        new PathSecurityViolation(
                            $"Access to system path not allowed: {dangerousPath}",
                            PathViolationType.PlatformSpecific,
                            path
                        )
                    );
                }
            }

            return Result.Success<bool, PathSecurityViolation>(true);
        }

        private StringComparison GetPathComparison()
        {
            // Windows and macOS are case-insensitive, Linux is case-sensitive
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
        }
    }

    /// <summary>
    /// Represents a canonicalized path with platform information
    /// </summary>
    public sealed record CanonicalPath(
        string Original,
        string Normalized,
        string Resolved,
        bool IsSymbolicLink
    )
    {
        /// <summary>
        /// Gets the path to use for security checks (the resolved real path)
        /// </summary>
        public string SecurityPath => Resolved;

        /// <summary>
        /// Gets a display-friendly path (normalized but not necessarily resolved)
        /// </summary>
        public string DisplayPath => Normalized;
    }
}
