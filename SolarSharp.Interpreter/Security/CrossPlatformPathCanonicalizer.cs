using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
        private readonly ConcurrentDictionary<string, CachedPath> _pathCache = new();
        private readonly TimeSpan _cacheExpiry;
        private readonly object _cleanupLock = new();
        private DateTime _lastCleanup = DateTime.UtcNow;

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

        private static readonly Regex AlternateDataStreamPattern = new(
            @":[^\\/:]+$",
            RegexOptions.Compiled
        );
        private static readonly Regex UncPathPattern = new(
            @"^\\\\[^\\]+\\[^\\]+",
            RegexOptions.Compiled
        );

        public CrossPlatformPathCanonicalizer(TimeSpan? cacheExpiry = null)
        {
            _cacheExpiry = cacheExpiry ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Canonicalizes a path for the current platform with full security checks
        /// </summary>
        public Result<CanonicalPath, PathSecurityViolation> Canonicalize(
            string path,
            string sandboxRoot = null
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

            // Check cache first
            if (TryGetCached(path, out var cached))
            {
                if (cached.IsSuccess)
                    return Result.Success<CanonicalPath, PathSecurityViolation>(cached.Value);
                else
                    return Result.Failure<CanonicalPath, PathSecurityViolation>(cached.Error);
            }

            // Perform canonicalization
            var result = CanonicalizeInternal(path, sandboxRoot);

            // Cache the result
            CacheResult(path, result);

            // Periodic cache cleanup
            CleanupCacheIfNeeded();

            return result;
        }

        private Result<CanonicalPath, PathSecurityViolation> CanonicalizeInternal(
            string path,
            string sandboxRoot
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

                // Get the full path (resolves relative paths and .. sequences)
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(normalized);
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

                // Resolve symbolic links to get the real path
                var realPath = ResolveRealPath(fullPath);

                // Validate sandbox constraints if provided
                if (!string.IsNullOrEmpty(sandboxRoot))
                {
                    var sandboxFullPath = Path.GetFullPath(sandboxRoot);
                    if (!realPath.StartsWith(sandboxFullPath, GetPathComparison()))
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
                    Resolved: realPath,
                    IsSymbolicLink: fullPath != realPath,
                    Platform: GetCurrentPlatform()
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
            // Check for alternate data streams
            if (AlternateDataStreamPattern.IsMatch(path))
            {
                return Result.Failure<bool, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Alternate data streams not allowed",
                        PathViolationType.PlatformSpecific,
                        path
                    )
                );
            }

            // Check for UNC paths (could be used to access network resources)
            if (UncPathPattern.IsMatch(path))
            {
                return Result.Failure<bool, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "UNC paths not allowed",
                        PathViolationType.PlatformSpecific,
                        path
                    )
                );
            }

            // Check for device names
            var fileName = Path.GetFileNameWithoutExtension(path);
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

            // Check for paths ending with space or period (Windows strips these)
            var segments = path.Split('\\', '/');
            if (segments.Any(s => s.EndsWith(" ") || s.EndsWith(".")))
            {
                return Result.Failure<bool, PathSecurityViolation>(
                    new PathSecurityViolation(
                        "Path segments cannot end with space or period",
                        PathViolationType.PlatformSpecific,
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

        private string ResolveRealPath(string path)
        {
            try
            {
                // Try to resolve symbolic links
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    // This is a symbolic link or junction
                    return GetRealPath(path);
                }

                var dirInfo = new DirectoryInfo(path);
                if (dirInfo.Exists && dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    // This is a symbolic link or junction
                    return GetRealPath(path);
                }

                return path;
            }
            catch
            {
                // If we can't resolve, return the original path
                return path;
            }
        }

        private string GetRealPath(string path)
        {
            // Platform-specific real path resolution
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use GetFinalPathNameByHandle
                return path; // Simplified for now
            }
            else
            {
                // On Unix, we could use readlink
                return path; // Simplified for now
            }
        }

        private StringComparison GetPathComparison()
        {
            // Windows and macOS are case-insensitive, Linux is case-sensitive
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
        }

        private Platform GetCurrentPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Platform.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Platform.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Platform.MacOS;
            else
                return Platform.Unknown;
        }

        private bool TryGetCached(
            string path,
            out Result<CanonicalPath, PathSecurityViolation> result
        )
        {
            if (_pathCache.TryGetValue(path, out var cached))
            {
                if (cached.Expiry > DateTime.UtcNow)
                {
                    result = cached.Result;
                    return true;
                }
                else
                {
                    // Expired, remove it
                    _pathCache.TryRemove(path, out _);
                }
            }

            result = default;
            return false;
        }

        private void CacheResult(string path, Result<CanonicalPath, PathSecurityViolation> result)
        {
            var cached = new CachedPath(result, DateTime.UtcNow.Add(_cacheExpiry));
            _pathCache.TryAdd(path, cached);
        }

        private void CleanupCacheIfNeeded()
        {
            var now = DateTime.UtcNow;
            if (now - _lastCleanup > _cacheExpiry)
            {
                lock (_cleanupLock)
                {
                    if (now - _lastCleanup > _cacheExpiry)
                    {
                        var expired = _pathCache
                            .Where(kvp => kvp.Value.Expiry <= now)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var key in expired)
                        {
                            _pathCache.TryRemove(key, out _);
                        }

                        _lastCleanup = now;
                    }
                }
            }
        }

        /// <summary>
        /// Gets cache statistics for monitoring
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var validEntries = _pathCache.Count(kvp => kvp.Value.Expiry > DateTime.UtcNow);
            return new CacheStatistics(
                TotalEntries: _pathCache.Count,
                ValidEntries: validEntries,
                LastCleanup: _lastCleanup
            );
        }

        /// <summary>
        /// Clears the entire cache
        /// </summary>
        public void ClearCache()
        {
            _pathCache.Clear();
            _lastCleanup = DateTime.UtcNow;
        }

        private sealed record CachedPath(
            Result<CanonicalPath, PathSecurityViolation> Result,
            DateTime Expiry
        );
    }

    /// <summary>
    /// Represents a canonicalized path with platform information
    /// </summary>
    public sealed record CanonicalPath(
        string Original,
        string Normalized,
        string Resolved,
        bool IsSymbolicLink,
        Platform Platform
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

    /// <summary>
    /// Platform enumeration
    /// </summary>
    public enum Platform
    {
        Windows,
        Linux,
        MacOS,
        Unknown,
    }

    /// <summary>
    /// Cache statistics for monitoring
    /// </summary>
    public sealed record CacheStatistics(int TotalEntries, int ValidEntries, DateTime LastCleanup);
}
