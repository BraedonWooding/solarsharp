using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Manifests.Domain
{
    /// <summary>
    /// Domain interface for managing trusted keys and certificates
    /// Each Script instance has its own trust store
    /// </summary>
    public interface ITrustStore
    {
        /// <summary>
        /// Gets all trusted key fingerprints
        /// </summary>
        ImmutableHashSet<string> TrustedKeyFingerprints { get; }

        /// <summary>
        /// Gets all trusted certificate fingerprints
        /// </summary>
        ImmutableHashSet<string> TrustedCertificateFingerprints { get; }

        /// <summary>
        /// Adds a trusted public key in PEM format
        /// </summary>
        /// <param name="publicKeyPem">PEM-encoded public key</param>
        /// <returns>Success result or validation error</returns>
        Result<ITrustStore, TrustStoreError> AddTrustedKey(string publicKeyPem);

        /// <summary>
        /// Adds a trusted certificate in PEM format
        /// </summary>
        /// <param name="certificatePem">PEM-encoded certificate</param>
        /// <returns>Success result or validation error</returns>
        Result<ITrustStore, TrustStoreError> AddTrustedCertificate(string certificatePem);

        /// <summary>
        /// Checks if a public key is trusted by fingerprint
        /// </summary>
        /// <param name="publicKeyFingerprint">SHA-256 fingerprint of the public key</param>
        /// <returns>True if the key is trusted</returns>
        bool IsTrustedKey(string publicKeyFingerprint);

        /// <summary>
        /// Checks if a certificate is trusted by fingerprint
        /// </summary>
        /// <param name="certificateFingerprint">SHA-256 fingerprint of the certificate</param>
        /// <returns>True if the certificate is trusted</returns>
        bool IsTrustedCertificate(string certificateFingerprint);

        /// <summary>
        /// Gets a public key by its fingerprint
        /// </summary>
        /// <param name="keyFingerprint">SHA-256 fingerprint (with or without sha256: prefix)</param>
        /// <returns>PEM-encoded public key or error if not found</returns>
        Result<string, TrustStoreError> GetPublicKey(string keyFingerprint);

        /// <summary>
        /// Validates a certificate chain against trusted CAs
        /// </summary>
        /// <param name="certificateChain">Certificate chain to validate</param>
        /// <returns>Validation result with trusted intermediate CAs</returns>
        Result<CertificateChainValidationResult, TrustStoreError> ValidateCertificateChain(
            ImmutableArray<string> certificateChain
        );

        /// <summary>
        /// Creates a new trust store with the same trusted keys and certificates
        /// </summary>
        /// <returns>New trust store instance</returns>
        ITrustStore Clone();

        /// <summary>
        /// Gets whether this trust store has any trusted keys or certificates
        /// </summary>
        bool IsEmpty { get; }
    }

    /// <summary>
    /// Result of certificate chain validation
    /// </summary>
    public sealed record CertificateChainValidationResult
    {
        public ImmutableArray<string> ValidatedCertificates { get; init; } =
            ImmutableArray<string>.Empty;
        public ImmutableArray<string> TrustedIntermediateCAs { get; init; } =
            ImmutableArray<string>.Empty;
        public bool IsValid { get; init; }
        public string ValidationPath { get; init; } = "";
    }

    /// <summary>
    /// Trust store operation errors
    /// </summary>
    public sealed record TrustStoreError
    {
        public TrustStoreErrorType Type { get; init; }
        public string Message { get; init; } = "";
        public string Context { get; init; } = "";

        public static TrustStoreError InvalidPublicKey(string message, string context = "") =>
            new()
            {
                Type = TrustStoreErrorType.InvalidPublicKey,
                Message = message,
                Context = context,
            };

        public static TrustStoreError InvalidCertificate(string message, string context = "") =>
            new()
            {
                Type = TrustStoreErrorType.InvalidCertificate,
                Message = message,
                Context = context,
            };

        public static TrustStoreError CertificateChainValidationFailed(
            string message,
            string context = ""
        ) =>
            new()
            {
                Type = TrustStoreErrorType.CertificateChainValidationFailed,
                Message = message,
                Context = context,
            };

        public static TrustStoreError UnsupportedKeyType(string message, string context = "") =>
            new()
            {
                Type = TrustStoreErrorType.UnsupportedKeyType,
                Message = message,
                Context = context,
            };

        public static TrustStoreError KeyNotFound(string message, string context = "") =>
            new()
            {
                Type = TrustStoreErrorType.KeyNotFound,
                Message = message,
                Context = context,
            };
    }

    /// <summary>
    /// Types of trust store errors
    /// </summary>
    public enum TrustStoreErrorType
    {
        InvalidPublicKey,
        InvalidCertificate,
        CertificateChainValidationFailed,
        UnsupportedKeyType,
        KeyNotFound,
    }
}
