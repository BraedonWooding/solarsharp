#nullable enable

using System;
using System.Collections.Immutable;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Events;

namespace SolarSharp.Interpreter.Security.Auditing
{
    /// <summary>
    /// Event raised when a manifest is successfully validated
    /// </summary>
    public sealed record ManifestValidatedEvent : SecurityAuditEvent
    {
        /// <summary>
        /// Path to the manifest file
        /// </summary>
        public string ManifestPath { get; init; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the manifest content
        /// </summary>
        public string ManifestHash { get; init; } = string.Empty;

        /// <summary>
        /// Version of the manifest format
        /// </summary>
        public string ManifestVersion { get; init; } = string.Empty;

        /// <summary>
        /// Signature algorithm used for validation
        /// </summary>
        public string SignatureAlgorithm { get; init; } = string.Empty;

        /// <summary>
        /// Public key fingerprint used for validation
        /// </summary>
        public string PublicKeyFingerprint { get; init; } = string.Empty;

        /// <summary>
        /// Whether the manifest was signed
        /// </summary>
        public bool IsSigned { get; init; }

        /// <summary>
        /// Whether the signing key is trusted
        /// </summary>
        public bool IsTrustedKey { get; init; }

        /// <summary>
        /// Certificate chain used for validation
        /// </summary>
        public ImmutableArray<string> CertificateChain { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Validation duration
        /// </summary>
        public TimeSpan ValidationDuration { get; init; }

        /// <summary>
        /// Validated manifest object
        /// </summary>
        public Manifest? ValidatedManifest { get; init; }

        /// <summary>
        /// Trust level assigned based on validation
        /// </summary>
        public string AssignedTrustLevel { get; init; } = string.Empty;

        /// <summary>
        /// Validation warnings (non-fatal issues)
        /// </summary>
        public ImmutableArray<string> ValidationWarnings { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Creates a new manifest validated event
        /// </summary>
        public ManifestValidatedEvent()
            : base(SecurityEventType.ManifestValidated)
        {
            Success = true;
            Operation = "manifest_validated";
        }

        /// <summary>
        /// Creates a new manifest validated event with specified parameters
        /// </summary>
        public ManifestValidatedEvent(
            string scriptId,
            string manifestPath,
            string manifestHash,
            string manifestVersion,
            bool isSigned,
            bool isTrustedKey,
            string signatureAlgorithm = "",
            string publicKeyFingerprint = "",
            TimeSpan validationDuration = default,
            string assignedTrustLevel = "",
            string principal = "",
            ScriptIdentity? scriptIdentity = null,
            Manifest? validatedManifest = null,
            ImmutableArray<string> certificateChain = default,
            ImmutableArray<string> validationWarnings = default
        )
            : this()
        {
            ScriptId = scriptId;
            ManifestPath = manifestPath;
            ManifestHash = manifestHash;
            ManifestVersion = manifestVersion;
            IsSigned = isSigned;
            IsTrustedKey = isTrustedKey;
            SignatureAlgorithm = signatureAlgorithm;
            PublicKeyFingerprint = publicKeyFingerprint;
            ValidationDuration = validationDuration;
            AssignedTrustLevel = assignedTrustLevel;
            Principal = principal;
            ScriptIdentity = scriptIdentity;
            ValidatedManifest = validatedManifest;
            CertificateChain = certificateChain.IsDefault
                ? ImmutableArray<string>.Empty
                : certificateChain;
            ValidationWarnings = validationWarnings.IsDefault
                ? ImmutableArray<string>.Empty
                : validationWarnings;
        }

        /// <summary>
        /// Creates a copy with additional validation warning
        /// </summary>
        public ManifestValidatedEvent WithWarning(string warning)
        {
            return this with { ValidationWarnings = ValidationWarnings.Add(warning) };
        }

        /// <summary>
        /// Creates a copy with certificate chain
        /// </summary>
        public ManifestValidatedEvent WithCertificateChain(ImmutableArray<string> certificateChain)
        {
            return this with { CertificateChain = certificateChain };
        }

        /// <summary>
        /// Creates a copy with validated manifest
        /// </summary>
        public ManifestValidatedEvent WithValidatedManifest(Manifest manifest)
        {
            return this with { ValidatedManifest = manifest };
        }
    }

    /// <summary>
    /// Event raised when manifest validation fails
    /// </summary>
    public sealed record ManifestValidationFailedEvent : SecurityAuditEvent
    {
        /// <summary>
        /// Path to the manifest file
        /// </summary>
        public string ManifestPath { get; init; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the manifest content (if available)
        /// </summary>
        public string? ManifestHash { get; init; }

        /// <summary>
        /// Version of the manifest format (if parseable)
        /// </summary>
        public string? ManifestVersion { get; init; }

        /// <summary>
        /// Failure reason
        /// </summary>
        public string FailureReason { get; init; } = string.Empty;

        /// <summary>
        /// Type of validation failure
        /// </summary>
        public ManifestValidationFailureType FailureType { get; init; } =
            ManifestValidationFailureType.InvalidSignature;

        /// <summary>
        /// Signature algorithm attempted (if applicable)
        /// </summary>
        public string? SignatureAlgorithm { get; init; }

        /// <summary>
        /// Public key fingerprint attempted (if applicable)
        /// </summary>
        public string? PublicKeyFingerprint { get; init; }

        /// <summary>
        /// Certificate chain attempted (if applicable)
        /// </summary>
        public ImmutableArray<string> CertificateChain { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Validation duration before failure
        /// </summary>
        public TimeSpan ValidationDuration { get; init; }

        /// <summary>
        /// Security risk assessment score (0-100)
        /// </summary>
        public int RiskScore { get; init; }

        /// <summary>
        /// Additional validation errors
        /// </summary>
        public ImmutableArray<string> ValidationErrors { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Whether this was a retry attempt
        /// </summary>
        public bool IsRetry { get; init; }

        /// <summary>
        /// Creates a new manifest validation failed event
        /// </summary>
        public ManifestValidationFailedEvent()
            : base(SecurityEventType.ManifestValidationFailed)
        {
            Success = false;
            Operation = "manifest_validation_failed";
        }

        /// <summary>
        /// Creates a new manifest validation failed event with specified parameters
        /// </summary>
        public ManifestValidationFailedEvent(
            string scriptId,
            string manifestPath,
            string failureReason,
            ManifestValidationFailureType failureType,
            TimeSpan validationDuration = default,
            int riskScore = 0,
            string principal = "",
            ScriptIdentity? scriptIdentity = null,
            string? manifestHash = null,
            string? manifestVersion = null,
            string? signatureAlgorithm = null,
            string? publicKeyFingerprint = null,
            ImmutableArray<string> certificateChain = default,
            ImmutableArray<string> validationErrors = default,
            bool isRetry = false,
            Exception? exception = null
        )
            : this()
        {
            ScriptId = scriptId;
            ManifestPath = manifestPath;
            FailureReason = failureReason;
            FailureType = failureType;
            ValidationDuration = validationDuration;
            RiskScore = riskScore;
            Principal = principal;
            ScriptIdentity = scriptIdentity;
            ManifestHash = manifestHash;
            ManifestVersion = manifestVersion;
            SignatureAlgorithm = signatureAlgorithm;
            PublicKeyFingerprint = publicKeyFingerprint;
            CertificateChain = certificateChain.IsDefault
                ? ImmutableArray<string>.Empty
                : certificateChain;
            ValidationErrors = validationErrors.IsDefault
                ? ImmutableArray<string>.Empty
                : validationErrors;
            IsRetry = isRetry;
            Exception = exception;
            ErrorMessage = failureReason;
        }

        /// <summary>
        /// Creates a copy with additional validation error
        /// </summary>
        public ManifestValidationFailedEvent WithValidationError(string error)
        {
            return this with { ValidationErrors = ValidationErrors.Add(error) };
        }

        /// <summary>
        /// Creates a copy with certificate chain
        /// </summary>
        public ManifestValidationFailedEvent WithCertificateChain(
            ImmutableArray<string> certificateChain
        )
        {
            return this with { CertificateChain = certificateChain };
        }

        /// <summary>
        /// Creates a copy with updated risk score
        /// </summary>
        public ManifestValidationFailedEvent WithRiskScore(int score)
        {
            return this with { RiskScore = Math.Max(0, Math.Min(100, score)) };
        }

        /// <summary>
        /// Creates a copy marked as retry
        /// </summary>
        public ManifestValidationFailedEvent AsRetry()
        {
            return this with { IsRetry = true };
        }
    }

    /// <summary>
    /// Factory methods for creating manifest validation events
    /// </summary>
    public static class ManifestValidationEventFactory
    {
        /// <summary>
        /// Creates a validated event for successful manifest validation
        /// </summary>
        public static ManifestValidatedEvent CreateValidated(
            string scriptId,
            string manifestPath,
            string manifestHash,
            string manifestVersion,
            bool isSigned,
            bool isTrustedKey,
            string assignedTrustLevel,
            TimeSpan validationDuration,
            string principal = ""
        )
        {
            return new ManifestValidatedEvent(
                scriptId,
                manifestPath,
                manifestHash,
                manifestVersion,
                isSigned,
                isTrustedKey,
                assignedTrustLevel: assignedTrustLevel,
                validationDuration: validationDuration,
                principal: principal
            );
        }

        /// <summary>
        /// Creates a failed event for manifest validation failure
        /// </summary>
        public static ManifestValidationFailedEvent CreateFailed(
            string scriptId,
            string manifestPath,
            string failureReason,
            ManifestValidationFailureType failureType,
            TimeSpan validationDuration,
            int riskScore = 0,
            string principal = ""
        )
        {
            return new ManifestValidationFailedEvent(
                scriptId,
                manifestPath,
                failureReason,
                failureType,
                validationDuration,
                riskScore,
                principal
            );
        }

        /// <summary>
        /// Creates a failed event with exception details
        /// </summary>
        public static ManifestValidationFailedEvent CreateFailedWithException(
            string scriptId,
            string manifestPath,
            ManifestValidationFailureType failureType,
            Exception exception,
            TimeSpan validationDuration,
            int riskScore = 0,
            string principal = ""
        )
        {
            return new ManifestValidationFailedEvent(
                scriptId,
                manifestPath,
                exception.Message,
                failureType,
                validationDuration,
                riskScore,
                principal,
                exception: exception
            );
        }

        /// <summary>
        /// Creates a signed manifest validated event
        /// </summary>
        public static ManifestValidatedEvent CreateSignedValidated(
            string scriptId,
            string manifestPath,
            string manifestHash,
            string manifestVersion,
            string signatureAlgorithm,
            string publicKeyFingerprint,
            bool isTrustedKey,
            string assignedTrustLevel,
            TimeSpan validationDuration,
            ImmutableArray<string> certificateChain,
            string principal = ""
        )
        {
            return new ManifestValidatedEvent(
                scriptId,
                manifestPath,
                manifestHash,
                manifestVersion,
                true,
                isTrustedKey,
                signatureAlgorithm,
                publicKeyFingerprint,
                validationDuration,
                assignedTrustLevel,
                principal,
                certificateChain: certificateChain
            );
        }

        /// <summary>
        /// Creates an unsigned manifest validated event
        /// </summary>
        public static ManifestValidatedEvent CreateUnsignedValidated(
            string scriptId,
            string manifestPath,
            string manifestHash,
            string manifestVersion,
            string assignedTrustLevel,
            TimeSpan validationDuration,
            string principal = ""
        )
        {
            return new ManifestValidatedEvent(
                scriptId,
                manifestPath,
                manifestHash,
                manifestVersion,
                false,
                false,
                assignedTrustLevel: assignedTrustLevel,
                validationDuration: validationDuration,
                principal: principal
            );
        }
    }
}
