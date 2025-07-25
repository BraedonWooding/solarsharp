using System;
using System.IO;
using System.Linq;
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
            // Extract public key token from the first signed content block that matches the fingerprint
            byte[] publicKeyToken = Array.Empty<byte>();
            
            if (manifest.HasSignedContent && !string.IsNullOrEmpty(publicKeyFingerprint))
            {
                var matchingBlock = manifest.SignedContent
                    .FirstOrDefault(block => block.GetKeyFingerprint() == publicKeyFingerprint);
                    
                if (matchingBlock != null && !string.IsNullOrEmpty(matchingBlock.PublicKeyToken))
                {
                    // Convert hex string token to bytes
                    publicKeyToken = HexStringToBytes(matchingBlock.PublicKeyToken);
                }
            }
            
            return new LoadedManifest(
                manifest,
                manifestPath,
                loadedAt,
                Maybe<X509Certificate>.None,
                publicKeyToken,
                false,
                publicKeyFingerprint,
                isVerified
            );
        }
        
        /// <summary>
        /// Converts a hex string to byte array
        /// </summary>
        private static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();
                
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even number of characters");
                
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
