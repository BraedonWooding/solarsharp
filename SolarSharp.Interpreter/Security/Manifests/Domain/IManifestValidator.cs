#nullable enable

using System;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests.Events;

namespace SolarSharp.Interpreter.Security.Manifests.Domain
{
    /// <summary>
    /// Domain service for manifest validation with event-driven architecture
    /// </summary>
    public interface IManifestValidationService
    {
        /// <summary>
        /// Discovers and validates a manifest for a script path
        /// </summary>
        /// <param name="scriptPath">Path to the script file</param>
        /// <param name="trustStore">Trust store containing trusted keys and certificates</param>
        /// <param name="scriptId">Unique identifier for the script instance</param>
        /// <returns>Validation result with loaded manifest or error</returns>
        Result<LoadedManifest, ManifestValidationError> ValidateManifest(
            string scriptPath,
            ITrustStore trustStore,
            string scriptId
        );

        /// <summary>
        /// Validates a manifest from JSON content
        /// </summary>
        /// <param name="manifestJson">JSON content of the manifest</param>
        /// <param name="manifestPath">Path where the manifest was loaded from</param>
        /// <param name="trustStore">Trust store for validation</param>
        /// <param name="scriptId">Unique identifier for the script instance</param>
        /// <returns>Validation result</returns>
        Result<Manifest, ManifestValidationError> ValidateManifestFromJson(
            string manifestJson,
            string manifestPath,
            ITrustStore trustStore,
            string scriptId
        );

        /// <summary>
        /// Validates a manifest signature
        /// </summary>
        /// <param name="manifest">Manifest to validate</param>
        /// <param name="trustStore">Trust store for validation</param>
        /// <param name="scriptId">Unique identifier for the script instance</param>
        /// <returns>Validation result</returns>
        Result<SignatureValidationResult, ManifestValidationError> ValidateSignature(
            Manifest manifest,
            ITrustStore trustStore,
            string scriptId
        );

        /// <summary>
        /// Event stream for manifest validation events
        /// </summary>
        IObservable<ManifestValidationEvent> ValidationEvents { get; }
    }

    /// <summary>
    /// Result of signature validation
    /// </summary>
    public sealed record SignatureValidationResult
    {
        public bool IsValid { get; init; }
        public bool IsTrusted { get; init; }
        public string SignatureAlgorithm { get; init; } = "";
        public string PublicKeyFingerprint { get; init; } = "";
        public ImmutableArray<string> ValidatedCertificates { get; init; } =
            ImmutableArray<string>.Empty;
        public TimeSpan ValidationDuration { get; init; }
    }

    /// <summary>
    /// Manifest validation errors
    /// </summary>
    public sealed record ManifestValidationError
    {
        public ManifestValidationErrorType Type { get; init; }
        public string Message { get; init; } = "";
        public string Context { get; init; } = "";
        public string ManifestPath { get; init; } = "";
        public Exception? InnerException { get; init; }

        public static ManifestValidationError NotFound(string manifestPath, string context = "") =>
            new()
            {
                Type = ManifestValidationErrorType.NotFound,
                Message = "Manifest not found",
                Context = context,
                ManifestPath = manifestPath,
            };

        public static ManifestValidationError InvalidFormat(
            string message,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.InvalidFormat,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
            };

        public static ManifestValidationError InvalidSignature(
            string message,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.InvalidSignature,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
            };

        public static ManifestValidationError UntrustedKey(
            string message,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.UntrustedKey,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
            };

        public static ManifestValidationError CertificateValidationFailed(
            string message,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.CertificateValidationFailed,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
            };

        public static ManifestValidationError UnsupportedAlgorithm(
            string message,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.UnsupportedAlgorithm,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
            };

        public static ManifestValidationError UnexpectedError(
            string message,
            Exception? innerException,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.UnexpectedError,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
                InnerException = innerException,
            };

        public static ManifestValidationError PermissionScopeViolation(
            string message,
            string manifestPath,
            string context = ""
        ) =>
            new()
            {
                Type = ManifestValidationErrorType.PermissionScopeViolation,
                Message = message,
                Context = context,
                ManifestPath = manifestPath,
            };
    }

    /// <summary>
    /// Types of manifest validation errors
    /// </summary>
    public enum ManifestValidationErrorType
    {
        NotFound,
        InvalidFormat,
        InvalidSignature,
        UntrustedKey,
        CertificateValidationFailed,
        UnsupportedAlgorithm,
        UnexpectedError,
        PermissionScopeViolation,
    }
}
