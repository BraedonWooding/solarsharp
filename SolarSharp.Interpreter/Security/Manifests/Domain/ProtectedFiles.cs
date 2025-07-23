#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Digests;

namespace SolarSharp.Interpreter.Security.Manifests.Domain
{
    /// <summary>
    /// Represents a file protected by a manifest with integrity validation
    /// </summary>
    public sealed record ProtectedFile
    {
        public string RelativePath { get; init; } = "";
        public string ExpectedHash { get; init; } = "";
        public string HashAlgorithm { get; init; } = "SHA256";
        public long ExpectedSize { get; init; } = 0;
        public bool ReadOnly { get; init; } = true;
        public string? PolicyName { get; init; } = null;

        /// <summary>
        /// Source manifest path for audit trail
        /// </summary>
        public string SourceManifest { get; init; } = "";

        /// <summary>
        /// Whether this file came from a trusted (signed) manifest
        /// </summary>
        public bool FromTrustedManifest { get; init; } = false;
    }

    /// <summary>
    /// Represents a protected file that has been verified against its expected hash
    /// </summary>
    public sealed record VerifiedProtectedFile
    {
        public ProtectedFile ProtectedFile { get; init; } = new();
        public string ActualHash { get; init; } = "";
        public long ActualSize { get; init; }
        public DateTime VerifiedAt { get; init; } = DateTime.UtcNow;
        public bool IsValid { get; init; }

        public static VerifiedProtectedFile CreateValid(
            ProtectedFile protectedFile,
            string actualHash,
            long actualSize
        ) =>
            new()
            {
                ProtectedFile = protectedFile,
                ActualHash = actualHash,
                ActualSize = actualSize,
                VerifiedAt = DateTime.UtcNow,
                IsValid = true,
            };

        public static VerifiedProtectedFile CreateInvalid(
            ProtectedFile protectedFile,
            string actualHash,
            long actualSize
        ) =>
            new()
            {
                ProtectedFile = protectedFile,
                ActualHash = actualHash,
                ActualSize = actualSize,
                VerifiedAt = DateTime.UtcNow,
                IsValid = false,
            };
    }

    /// <summary>
    /// Immutable collection of protected files for a Script environment
    /// </summary>
    public sealed record ProtectedFiles
    {
        private readonly ImmutableDictionary<string, ProtectedFile> _files;

        public static readonly ProtectedFiles Empty = new(
            ImmutableDictionary<string, ProtectedFile>.Empty
        );

        private ProtectedFiles(ImmutableDictionary<string, ProtectedFile> files)
        {
            _files = files;
        }

        /// <summary>
        /// Gets all protected file paths
        /// </summary>
        public ImmutableHashSet<string> ProtectedPaths => _files.Keys.ToImmutableHashSet();

        /// <summary>
        /// Checks if a file path is protected by any manifest
        /// </summary>
        public bool IsProtected(string filePath) => _files.ContainsKey(NormalizePath(filePath));

        /// <summary>
        /// Gets protection information for a file
        /// </summary>
        public Maybe<ProtectedFile> GetProtection(string filePath) =>
            _files.TryGetValue(NormalizePath(filePath), out var protection)
                ? Maybe<ProtectedFile>.From(protection)
                : Maybe<ProtectedFile>.None;

        /// <summary>
        /// Adds a protected file, returning a new ProtectedFiles instance
        /// </summary>
        public ProtectedFiles AddFile(ProtectedFile protectedFile) =>
            new(_files.SetItem(NormalizePath(protectedFile.RelativePath), protectedFile));

        /// <summary>
        /// Adds multiple protected files from a manifest
        /// </summary>
        public ProtectedFiles AddFiles(IEnumerable<ProtectedFile> protectedFiles) =>
            protectedFiles.Aggregate(this, (current, file) => current.AddFile(file));

        /// <summary>
        /// Removes protection for a file path
        /// </summary>
        public ProtectedFiles RemoveFile(string filePath) =>
            new(_files.Remove(NormalizePath(filePath)));

        /// <summary>
        /// Gets count of protected files
        /// </summary>
        public int Count => _files.Count;

        /// <summary>
        /// Normalizes file paths for consistent lookup
        /// </summary>
        private static string NormalizePath(string path) =>
            Path.GetFullPath(path).Replace('\\', '/');
    }

    /// <summary>
    /// Service for validating protected files against their expected hashes
    /// </summary>
    public static class ProtectedFileValidator
    {
        /// <summary>
        /// Validates a file against its protection metadata
        /// </summary>
        public static Result<VerifiedProtectedFile, string> ValidateFile(
            ProtectedFile protectedFile,
            string actualFilePath
        )
        {
            try
            {
                if (!File.Exists(actualFilePath))
                    return Result.Failure<VerifiedProtectedFile, string>(
                        $"Protected file not found: {actualFilePath}"
                    );

                var fileInfo = new FileInfo(actualFilePath);
                var actualSize = fileInfo.Length;

                // Validate size first (faster check)
                if (protectedFile.ExpectedSize > 0 && actualSize != protectedFile.ExpectedSize)
                    return Result.Failure<VerifiedProtectedFile, string>(
                        $"File size mismatch for {actualFilePath}: expected {protectedFile.ExpectedSize}, got {actualSize}"
                    );

                // Calculate hash
                var actualHash = CalculateFileHash(actualFilePath, protectedFile.HashAlgorithm);

                // Validate hash
                if (
                    !string.Equals(
                        actualHash,
                        protectedFile.ExpectedHash,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return Result.Success<VerifiedProtectedFile, string>(
                        VerifiedProtectedFile.CreateInvalid(protectedFile, actualHash, actualSize)
                    );
                }

                return Result.Success<VerifiedProtectedFile, string>(
                    VerifiedProtectedFile.CreateValid(protectedFile, actualHash, actualSize)
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<VerifiedProtectedFile, string>(
                    $"Error validating protected file {actualFilePath}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Calculates file hash using the specified algorithm
        /// </summary>
        private static string CalculateFileHash(string filePath, string algorithm)
        {
            using var stream = File.OpenRead(filePath);

            return algorithm.ToUpperInvariant() switch
            {
                "SHA256" => CalculateSHA256(stream),
                "SHA1" => CalculateSHA1(stream),
                "MD5" => CalculateMD5(stream),
                _ => throw new NotSupportedException($"Hash algorithm not supported: {algorithm}"),
            };
        }

        private static string CalculateSHA256(Stream stream)
        {
            var digest = new Sha256Digest();
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                digest.BlockUpdate(buffer, 0, bytesRead);
            }

            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            // Using StringBuilder for efficient hex conversion in .NET Standard 2.1
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string CalculateSHA1(Stream stream)
        {
            var digest = new Sha1Digest();
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                digest.BlockUpdate(buffer, 0, bytesRead);
            }

            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            // Using StringBuilder for efficient hex conversion in .NET Standard 2.1
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string CalculateMD5(Stream stream)
        {
            var digest = new MD5Digest();
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                digest.BlockUpdate(buffer, 0, bytesRead);
            }

            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            // Using StringBuilder for efficient hex conversion in .NET Standard 2.1
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Errors that can occur during protected file operations
    /// </summary>
    public sealed record ProtectedFileError
    {
        public string Message { get; init; } = "";
        public string FilePath { get; init; } = "";
        public string Context { get; init; } = "";

        public static ProtectedFileError HashMismatch(
            string filePath,
            string expected,
            string actual
        ) =>
            new()
            {
                Message = $"Hash mismatch: expected {expected}, got {actual}",
                FilePath = filePath,
                Context = "HashValidation",
            };

        public static ProtectedFileError SizeMismatch(
            string filePath,
            long expected,
            long actual
        ) =>
            new()
            {
                Message = $"Size mismatch: expected {expected}, got {actual}",
                FilePath = filePath,
                Context = "SizeValidation",
            };

        public static ProtectedFileError FileNotFound(string filePath) =>
            new()
            {
                Message = "Protected file not found",
                FilePath = filePath,
                Context = "FileAccess",
            };

        public static ProtectedFileError ReadOnlyViolation(string filePath) =>
            new()
            {
                Message = "Attempt to modify read-only protected file",
                FilePath = filePath,
                Context = "WriteProtection",
            };
    }
}
