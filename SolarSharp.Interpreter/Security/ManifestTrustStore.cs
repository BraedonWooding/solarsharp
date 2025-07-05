using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using SolarSharp.Interpreter.Security.Manifests;
using ManifestNS = SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    using Manifest_Manifest = ManifestNS.Manifest;
    using TrustLevel = ManifestNS.TrustLevel;

    /// <summary>
    /// Manages trusted public keys for manifest signature verification
    /// </summary>
    public static class ManifestTrustStore
    {
        internal static readonly ThreadLocal<HashSet<string>> _trustedKeyFingerprints = new(() => new HashSet<string>());
        internal static readonly object _lock = new();
        
        // Global shared store as fallback for tests that expect cross-thread access
        internal static readonly HashSet<string> _globalTrustedKeyFingerprints = new HashSet<string>();

        /// <summary>
        /// Adds a trusted public key from PEM format
        /// </summary>
        /// <param name="publicKeyPem">Public key in PEM format</param>
        public static void AddTrustedKey(string publicKeyPem)
        {
            if (string.IsNullOrWhiteSpace(publicKeyPem))
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyPem));

            var fingerprint = GenerateKeyFingerprint(publicKeyPem);
            lock (_lock)
            {
                _trustedKeyFingerprints.Value.Add(fingerprint);
                _globalTrustedKeyFingerprints.Add(fingerprint);
            }
        }

        /// <summary>
        /// Adds a trusted public key from a file
        /// </summary>
        /// <param name="keyFilePath">Path to PEM file containing public key</param>
        public static void AddTrustedKeyFromFile(string keyFilePath)
        {
            if (!File.Exists(keyFilePath))
                throw new FileNotFoundException($"Key file not found: {keyFilePath}");

            var pemContent = File.ReadAllText(keyFilePath);
            AddTrustedKey(pemContent);
        }

        /// <summary>
        /// Removes a trusted public key
        /// </summary>
        /// <param name="publicKeyPem">Public key in PEM format to remove</param>
        /// <returns>True if key was removed, false if it wasn't in the trust store</returns>
        public static bool RemoveTrustedKey(string publicKeyPem)
        {
            if (string.IsNullOrWhiteSpace(publicKeyPem))
                return false;

            var fingerprint = GenerateKeyFingerprint(publicKeyPem);
            lock (_lock)
            {
                var removed1 = _trustedKeyFingerprints.Value.Remove(fingerprint);
                var removed2 = _globalTrustedKeyFingerprints.Remove(fingerprint);
                return removed1 || removed2;
            }
        }

        /// <summary>
        /// Clears all trusted keys from the trust store
        /// </summary>
        public static void ClearTrustedKeys()
        {
            lock (_lock)
            {
                _trustedKeyFingerprints.Value.Clear();
                _globalTrustedKeyFingerprints.Clear();
            }
        }

        /// <summary>
        /// Checks if a manifest is signed by a trusted key
        /// </summary>
        /// <param name="manifest">Manifest to check</param>
        /// <returns>True if manifest is signed by a trusted key</returns>
        public static bool IsManifestTrusted(Manifest_Manifest manifest)
        {
            if (manifest?.Security?.PublicKey?.Value == null)
                return false;

            if (!HasSignature(manifest))
                return false;

            try
            {
                var keyFingerprint = GenerateKeyFingerprint(manifest.Security.PublicKey.Value);
                return _trustedKeyFingerprints.Value.Contains(keyFingerprint) || 
                       _globalTrustedKeyFingerprints.Contains(keyFingerprint);
            }
            catch
            {
                // Invalid key format
                return false;
            }
        }

        /// <summary>
        /// Checks if a manifest has a valid signature (regardless of trust)
        /// </summary>
        /// <param name="manifest">Manifest to check</param>
        /// <returns>True if manifest has a signature</returns>
        public static bool HasSignature(Manifest_Manifest manifest)
        {
            return manifest?.Security?.Signature?.Value != null && 
                   !string.IsNullOrWhiteSpace(manifest.Security.Signature.Value);
        }

        /// <summary>
        /// Gets the trust level of a manifest
        /// </summary>
        /// <param name="manifest">Manifest to evaluate</param>
        /// <returns>Trust level of the manifest</returns>
        public static TrustLevel GetTrustLevel(Manifest_Manifest manifest)
        {
            if (manifest?.Security == null)
                return TrustLevel.Untrusted;

            if (!HasSignature(manifest))
                return TrustLevel.Untrusted;

            // Check certificate-based trust first
            if (manifest.Security.UsesCertificates())
            {
                return CertificateTrustStore.GetCertificateTrustLevel(manifest);
            }

            // Fall back to raw key trust
            if (IsManifestTrusted(manifest))
                return TrustLevel.Trusted;

            return TrustLevel.Untrusted;
        }

        /// <summary>
        /// Gets count of trusted keys in the store
        /// </summary>
        public static int TrustedKeyCount => _trustedKeyFingerprints.Value.Count + _globalTrustedKeyFingerprints.Count;

        /// <summary>
        /// Creates a scoped trust store that automatically cleans up when disposed
        /// </summary>
        /// <returns>A disposable trust store scope</returns>
        public static TrustStoreScope CreateScope()
        {
            return new TrustStoreScope();
        }

        /// <summary>
        /// Generates a fingerprint for a public key for comparison
        /// </summary>
        private static string GenerateKeyFingerprint(string publicKey)
        {
            string base64Key;
            
            // Handle both PEM and BASE64 formats
            if (publicKey.Contains("-----BEGIN"))
            {
                // PEM format - extract base64 content
                base64Key = ExtractBase64FromPem(publicKey);
            }
            else
            {
                // Assume it's already base64
                base64Key = publicKey;
            }
            
            var keyBytes = Convert.FromBase64String(base64Key);
            
            // Generate SHA256 fingerprint
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(keyBytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Extracts base64 content from PEM format
        /// </summary>
        private static string ExtractBase64FromPem(string pem)
        {
            var lines = pem.Split('\n');
            var sb = new StringBuilder();
            bool inKey = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("-----BEGIN"))
                {
                    inKey = true;
                }
                else if (trimmedLine.StartsWith("-----END"))
                {
                    break;
                }
                else if (inKey && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    sb.Append(trimmedLine);
                }
            }

            return sb.ToString();
        }
    }


    /// <summary>
    /// Provides a scoped trust store that automatically cleans up when disposed
    /// </summary>
    public class TrustStoreScope : IDisposable
    {
        private readonly HashSet<string> _originalKeys;
        private readonly HashSet<string> _originalGlobalKeys;
        private bool _disposed;

        internal TrustStoreScope()
        {
            // Save the current state and clear
            lock (ManifestTrustStore._lock)
            {
                _originalKeys = new HashSet<string>(ManifestTrustStore._trustedKeyFingerprints.Value);
                _originalGlobalKeys = new HashSet<string>(ManifestTrustStore._globalTrustedKeyFingerprints);
                ManifestTrustStore._trustedKeyFingerprints.Value.Clear();
                ManifestTrustStore._globalTrustedKeyFingerprints.Clear();
            }
        }

        /// <summary>
        /// Adds a trusted public key for the scope of this instance
        /// </summary>
        /// <param name="publicKeyPem">Public key in PEM format</param>
        public void AddTrustedKey(string publicKeyPem)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrustStoreScope));
            
            ManifestTrustStore.AddTrustedKey(publicKeyPem);
        }

        /// <summary>
        /// Adds a trusted public key from a file for the scope of this instance
        /// </summary>
        /// <param name="keyFilePath">Path to PEM file containing public key</param>
        public void AddTrustedKeyFromFile(string keyFilePath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrustStoreScope));
            
            ManifestTrustStore.AddTrustedKeyFromFile(keyFilePath);
        }

        /// <summary>
        /// Disposes the scope and restores the original trust store state
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Restore original state
                lock (ManifestTrustStore._lock)
                {
                    ManifestTrustStore._trustedKeyFingerprints.Value.Clear();
                    ManifestTrustStore._globalTrustedKeyFingerprints.Clear();
                    
                    foreach (var key in _originalKeys)
                    {
                        ManifestTrustStore._trustedKeyFingerprints.Value.Add(key);
                    }
                    
                    foreach (var key in _originalGlobalKeys)
                    {
                        ManifestTrustStore._globalTrustedKeyFingerprints.Add(key);
                    }
                }
                
                _disposed = true;
            }
        }
    }
}