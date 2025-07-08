using System;
using System.IO;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.X509;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Immutable container for a loaded and verified manifest
    /// Unified design for both new domain model and existing integrations
    /// </summary>
    public sealed record LoadedManifest
    {
        /// <summary>
        /// The parsed manifest
        /// </summary>
        public Manifest Manifest { get; init; }

        /// <summary>
        /// The path where this manifest was loaded from
        /// </summary>
        public string ManifestPath { get; init; }

        /// <summary>
        /// The directory this manifest was loaded from
        /// </summary>
        public string Directory { get; init; }

        /// <summary>
        /// When this manifest was loaded
        /// </summary>
        public DateTime LoadedAt { get; init; }

        /// <summary>
        /// The certificate that signed this manifest (if any)
        /// </summary>
        public Maybe<X509Certificate> Certificate { get; init; }

        /// <summary>
        /// The calculated public key token
        /// </summary>
        public byte[] PublicKeyToken { get; init; }

        /// <summary>
        /// Whether this manifest's certificate can override limits
        /// </summary>
        public bool CanOverrideLimits { get; init; }

        /// <summary>
        /// The public key fingerprint that signed this manifest
        /// </summary>
        public string PublicKeyFingerprint { get; init; }

        /// <summary>
        /// Whether this manifest's signature was verified
        /// </summary>
        public bool IsVerified { get; init; }

        public LoadedManifest(
            Manifest manifest,
            string manifestPath,
            DateTime loadedAt,
            Maybe<X509Certificate> certificate = default,
            byte[] publicKeyToken = null,
            bool canOverrideLimits = false,
            string publicKeyFingerprint = "",
            bool isVerified = true
        )
        {
            Manifest = manifest;
            ManifestPath = manifestPath;
            Directory = Path.GetDirectoryName(manifestPath) ?? "";
            LoadedAt = loadedAt;
            Certificate = certificate.HasValue ? certificate : Maybe<X509Certificate>.None;
            PublicKeyToken = publicKeyToken ?? Array.Empty<byte>();
            CanOverrideLimits = canOverrideLimits;
            PublicKeyFingerprint = publicKeyFingerprint;
            IsVerified = isVerified;
        }

        /// <summary>
        /// Creates a LoadedManifest from domain validation results
        /// </summary>
        public static LoadedManifest FromValidationResult(
            Manifest manifest,
            string manifestPath,
            DateTime loadedAt,
            string publicKeyFingerprint,
            bool isVerified
        )
        {
            return new LoadedManifest(
                manifest,
                manifestPath,
                loadedAt,
                Maybe<X509Certificate>.None,
                Array.Empty<byte>(),
                false,
                publicKeyFingerprint,
                isVerified
            );
        }
    }
}
