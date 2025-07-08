#nullable enable

using System;
using System.Collections.Immutable;
using JetBrains.Annotations;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// Immutable record containing information about a script manifest.
    /// Provides read-only access to manifest metadata, security settings, and validation status.
    /// </summary>
    [PublicAPI]
    public sealed record ManifestInfo
    {
        /// <summary>
        /// Version identifier of the manifest format and script.
        /// Follows semantic versioning conventions (e.g., "1.0.0", "2.1.3-beta").
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable description of the script's purpose and functionality.
        /// Used for display in security prompts and management interfaces.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Name or identifier of the script author or organization.
        /// Used for trust decisions and security validation.
        /// </summary>
        public string Author { get; init; } = string.Empty;

        /// <summary>
        /// Indicates whether the manifest has a valid cryptographic signature.
        /// Signed manifests provide stronger security guarantees and trust validation.
        /// </summary>
        public bool IsSigned { get; init; }

        /// <summary>
        /// Indicates whether the manifest signature has been verified against a trusted certificate.
        /// Only meaningful when <see cref="IsSigned"/> is true.
        /// </summary>
        public bool IsSignatureValid { get; init; }

        /// <summary>
        /// Directory path where the manifest file is located.
        /// Used for resolving relative paths and security context determination.
        /// </summary>
        public string Directory { get; init; } = string.Empty;

        /// <summary>
        /// Collection of policy scopes defined in this manifest.
        /// The effective security policy will be the most restrictive combination of all applicable policies.
        /// </summary>
        public PolicyScopeCollection PolicyScopes { get; init; } = PolicyScopeCollection.Empty;

        /// <summary>
        /// Timestamp when the manifest was created or last modified.
        /// Used for cache validation and security auditing.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.MinValue;

        /// <summary>
        /// Timestamp when the manifest expires (if applicable).
        /// Expired manifests may be subject to additional security restrictions.
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; init; }

        /// <summary>
        /// List of Lua modules explicitly requested by the manifest.
        /// These modules will be available to the script if permitted by the security level.
        /// </summary>
        public ImmutableArray<string> RequestedModules { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// List of custom capabilities requested by the manifest.
        /// Capabilities follow the format "domain.action" (e.g., "file.read", "network.http").
        /// </summary>
        public ImmutableArray<string> RequestedCapabilities { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// File access policies defined in the manifest.
        /// Specifies which files and directories the script should be allowed to access.
        /// </summary>
        public ImmutableArray<ManifestFilePolicy> FilePolicies { get; init; } =
            ImmutableArray<ManifestFilePolicy>.Empty;

        /// <summary>
        /// Additional metadata key-value pairs from the manifest.
        /// Used for custom properties and application-specific information.
        /// </summary>
        public ImmutableDictionary<string, string> Metadata { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// SHA-256 hash of the manifest content (excluding signature).
        /// Used for integrity verification and change detection.
        /// </summary>
        public string ContentHash { get; init; } = string.Empty;

        /// <summary>
        /// Information about the certificate used to sign the manifest (if applicable).
        /// Null for unsigned manifests or when signature validation is not available.
        /// </summary>
        public ManifestCertificateInfo? CertificateInfo { get; init; }

        /// <summary>
        /// Indicates whether the manifest has expired based on the current date.
        /// </summary>
        public bool IsExpired
        {
            get { return ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow; }
        }

        /// <summary>
        /// Indicates whether the manifest is considered valid and trustworthy.
        /// Combines signature validation, expiration status, and policy compliance.
        /// </summary>
        public bool IsValid
        {
            get { return (!IsSigned || IsSignatureValid) && !IsExpired; }
        }

        /// <summary>
        /// Creates an empty manifest info instance representing no manifest.
        /// Used when scripts are executed without manifest files.
        /// </summary>
        public static readonly ManifestInfo Empty = new ManifestInfo();

        /// <summary>
        /// Creates a manifest info instance for an unsigned manifest with basic information.
        /// </summary>
        /// <param name="version">Manifest version</param>
        /// <param name="description">Script description</param>
        /// <param name="directory">Manifest directory path</param>
        /// <returns>Unsigned manifest info instance</returns>
        public static ManifestInfo CreateUnsigned(
            string version,
            string description,
            string directory = ""
        )
        {
            return new ManifestInfo
            {
                Version = version ?? string.Empty,
                Description = description ?? string.Empty,
                Directory = directory ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                IsSigned = false,
                IsSignatureValid = false,
            };
        }

        /// <summary>
        /// Creates a manifest info instance for a signed manifest with certificate validation.
        /// </summary>
        /// <param name="version">Manifest version</param>
        /// <param name="description">Script description</param>
        /// <param name="certificateInfo">Certificate information</param>
        /// <param name="isSignatureValid">Whether the signature is valid</param>
        /// <param name="directory">Manifest directory path</param>
        /// <returns>Signed manifest info instance</returns>
        public static ManifestInfo CreateSigned(
            string version,
            string description,
            ManifestCertificateInfo certificateInfo,
            bool isSignatureValid,
            string directory = ""
        )
        {
            return new ManifestInfo
            {
                Version = version ?? string.Empty,
                Description = description ?? string.Empty,
                Directory = directory ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                IsSigned = true,
                IsSignatureValid = isSignatureValid,
                CertificateInfo = certificateInfo,
            };
        }

        /// <summary>
        /// Returns a human-readable summary of the manifest information.
        /// </summary>
        /// <returns>Formatted string describing the manifest</returns>
        public override string ToString()
        {
            var status = IsValid ? "Valid" : "Invalid";
            var signature = IsSigned
                ? (IsSignatureValid ? "Signed (Valid)" : "Signed (Invalid)")
                : "Unsigned";
            var expiry = IsExpired ? " (Expired)" : "";

            return $"{Description} v{Version} - {status}, {signature}, Policies: {PolicyScopes.Scopes.Length}{expiry}";
        }
    }

    /// <summary>
    /// Immutable record containing file access policy information from a manifest.
    /// </summary>
    [PublicAPI]
    public sealed record ManifestFilePolicy
    {
        /// <summary>
        /// File path pattern or glob expression (e.g., "*.txt", "/config/**").
        /// Supports standard glob patterns with wildcards and recursive matching.
        /// </summary>
        public string Pattern { get; init; } = string.Empty;

        /// <summary>
        /// Type of access being requested (Read, Write, ReadWrite, Execute).
        /// </summary>
        public FileAccessType AccessType { get; init; } = FileAccessType.Read;

        /// <summary>
        /// Policy scope for this file access pattern.
        /// Used for determining which policy restrictions apply to this file pattern.
        /// </summary>
        public PolicyScope Scope { get; init; } = PolicyScope.File("", Examples.Isolated());

        /// <summary>
        /// Optional description explaining why this file access is needed.
        /// Used in security prompts and audit logs.
        /// </summary>
        public string Justification { get; init; } = string.Empty;

        /// <summary>
        /// Creates a read-only file policy for the specified pattern.
        /// </summary>
        /// <param name="pattern">File path pattern</param>
        /// <param name="policy">Security policy for this access</param>
        /// <param name="justification">Optional justification</param>
        /// <returns>Read-only file policy</returns>
        public static ManifestFilePolicy ReadOnly(
            string pattern,
            SecurityPolicy? policy = null,
            string justification = ""
        ) =>
            new ManifestFilePolicy
            {
                Pattern = pattern,
                AccessType = FileAccessType.Read,
                Scope = PolicyScope.File(pattern, policy ?? Examples.Isolated()),
                Justification = justification,
            };

        /// <summary>
        /// Creates a read-write file policy for the specified pattern.
        /// </summary>
        /// <param name="pattern">File path pattern</param>
        /// <param name="policy">Security policy for this access</param>
        /// <param name="justification">Optional justification</param>
        /// <returns>Read-write file policy</returns>
        public static ManifestFilePolicy ReadWrite(
            string pattern,
            SecurityPolicy? policy = null,
            string justification = ""
        ) =>
            new ManifestFilePolicy
            {
                Pattern = pattern,
                AccessType = FileAccessType.ReadWrite,
                Scope = PolicyScope.File(pattern, policy ?? Examples.Configuration()),
                Justification = justification,
            };
    }

    /// <summary>
    /// Types of file access that can be requested in manifest policies.
    /// </summary>
    [PublicAPI]
    public enum FileAccessType
    {
        /// <summary>
        /// Read-only access to files and directories.
        /// </summary>
        Read = 0,

        /// <summary>
        /// Write-only access (create, modify, delete).
        /// </summary>
        Write = 1,

        /// <summary>
        /// Both read and write access.
        /// </summary>
        ReadWrite = 2,

        /// <summary>
        /// Execute permission for executable files.
        /// </summary>
        Execute = 3,
    }

    /// <summary>
    /// Immutable record containing information about the certificate used to sign a manifest.
    /// </summary>
    [PublicAPI]
    public sealed record ManifestCertificateInfo
    {
        /// <summary>
        /// Subject name from the certificate (typically the organization or individual name).
        /// </summary>
        public string Subject { get; init; } = string.Empty;

        /// <summary>
        /// Issuer name from the certificate (the certificate authority).
        /// </summary>
        public string Issuer { get; init; } = string.Empty;

        /// <summary>
        /// Certificate thumbprint (SHA-1 hash) for unique identification.
        /// </summary>
        public string Thumbprint { get; init; } = string.Empty;

        /// <summary>
        /// Date when the certificate becomes valid.
        /// </summary>
        public DateTimeOffset ValidFrom { get; init; }

        /// <summary>
        /// Date when the certificate expires.
        /// </summary>
        public DateTimeOffset ValidTo { get; init; }

        /// <summary>
        /// Indicates whether the certificate is currently valid (not expired).
        /// </summary>
        public bool IsCurrentlyValid
        {
            get { return DateTimeOffset.UtcNow >= ValidFrom && DateTimeOffset.UtcNow <= ValidTo; }
        }

        /// <summary>
        /// Returns a human-readable description of the certificate.
        /// </summary>
        /// <returns>Formatted certificate information</returns>
        public override string ToString() =>
            $"{Subject} (Issued by: {Issuer}, Valid: {ValidFrom:yyyy-MM-dd} - {ValidTo:yyyy-MM-dd})";
    }
}
