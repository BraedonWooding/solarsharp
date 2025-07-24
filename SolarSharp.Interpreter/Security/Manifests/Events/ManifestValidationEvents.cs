using System;
using System.Collections.Immutable;

namespace SolarSharp.Interpreter.Security.Manifests.Events
{
    /// <summary>
    /// Base class for all manifest validation events
    /// </summary>
    public abstract record ManifestValidationEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        public string ScriptId { get; init; } = "";
        public string ManifestPath { get; init; } = "";
    }

    /// <summary>
    /// Event raised when manifest discovery begins
    /// </summary>
    public sealed record ManifestDiscoveryStarted : ManifestValidationEvent
    {
        public string ScriptPath { get; init; } = "";
        public string SearchRoot { get; init; } = "";
    }

    /// <summary>
    /// Event raised when a manifest is discovered
    /// </summary>
    public sealed record ManifestDiscovered : ManifestValidationEvent
    {
        public string ManifestContent { get; init; } = "";
        public bool IsSigned { get; init; }
        public ImmutableArray<string> Certificates { get; init; } = ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Event raised when manifest signature validation begins
    /// </summary>
    public sealed record ManifestSignatureValidationStarted : ManifestValidationEvent
    {
        public string SignatureAlgorithm { get; init; } = "";
        public string PublicKeyFingerprint { get; init; } = "";
    }

    /// <summary>
    /// Event raised when manifest signature validation succeeds
    /// </summary>
    public sealed record ManifestSignatureValidated : ManifestValidationEvent
    {
        public string SignatureAlgorithm { get; init; } = "";
        public string PublicKeyFingerprint { get; init; } = "";
        public bool IsTrustedKey { get; init; }
    }

    /// <summary>
    /// Event raised when manifest signature validation fails
    /// </summary>
    public sealed record ManifestSignatureValidationFailed : ManifestValidationEvent
    {
        public string Reason { get; init; } = "";
        public string SignatureAlgorithm { get; init; } = "";
        public string PublicKeyFingerprint { get; init; } = "";
        public ManifestValidationFailureType FailureType { get; init; }
    }

    /// <summary>
    /// Event raised when trust validation begins
    /// </summary>
    public sealed record ManifestTrustValidationStarted : ManifestValidationEvent
    {
        public string PublicKeyFingerprint { get; init; } = "";
        public ImmutableArray<string> TrustedKeyFingerprints { get; init; } =
            ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Event raised when trust validation fails
    /// </summary>
    public sealed record ManifestTrustValidationFailed : ManifestValidationEvent
    {
        public string PublicKeyFingerprint { get; init; } = "";
        public ImmutableArray<string> TrustedKeyFingerprints { get; init; } =
            ImmutableArray<string>.Empty;
        public string Reason { get; init; } = "";
    }

    /// <summary>
    /// Event raised when manifest validation completes successfully
    /// </summary>
    public sealed record ManifestValidationCompleted : ManifestValidationEvent
    {
        public Manifest ValidatedManifest { get; init; } = new Manifest();
        public TimeSpan ValidationDuration { get; init; }
    }

    /// <summary>
    /// Event raised when certificate chain validation begins
    /// </summary>
    public sealed record CertificateChainValidationStarted : ManifestValidationEvent
    {
        public ImmutableArray<string> CertificateChain { get; init; } =
            ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Event raised when certificate chain validation completes
    /// </summary>
    public sealed record CertificateChainValidated : ManifestValidationEvent
    {
        public ImmutableArray<string> ValidatedCertificates { get; init; } =
            ImmutableArray<string>.Empty;
        public ImmutableArray<string> TrustedIntermediateCAs { get; init; } =
            ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Event raised when a key is automatically imported through CA validation
    /// </summary>
    public sealed record ManifestKeyAutoImportedEvent : ManifestValidationEvent
    {
        public string KeyFingerprint { get; init; } = "";
        public string TrustedCA { get; init; } = "";
        public string ImportReason { get; init; } = "";
    }

    /// <summary>
    /// Types of manifest validation failures
    /// </summary>
    public enum ManifestValidationFailureType
    {
        InvalidSignature,
        UntrustedKey,
        ExpiredCertificate,
        InvalidCertificateChain,
        MalformedManifest,
        UnsupportedAlgorithm,
        MissingPublicKey,
    }
}
