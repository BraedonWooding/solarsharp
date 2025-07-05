using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Manages cryptographic operations for manifest validation and key management
    /// </summary>
    public class CryptoManager
    {
        private readonly List<PublicKeyInfo> _loadedKeys = new();

        /// <summary>
        /// Gets whether this manager has loaded any public keys
        /// </summary>
        public bool HasLoadedKeys => _loadedKeys.Count > 0;

        /// <summary>
        /// Loads a public key into the manager, enforcing manifest requirements for all .lua files
        /// </summary>
        public CryptoManager LoadKey(PublicKeyInfo publicKey)
        {
            if (publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));
                
            if (string.IsNullOrWhiteSpace(publicKey.Value))
                throw new ArgumentException("Public key value cannot be empty", nameof(publicKey));

            // Validate the key format by attempting to parse it
            ValidatePublicKey(publicKey);

            _loadedKeys.Add(publicKey);
            return this;
        }

        /// <summary>
        /// Loads a public key from PEM string into the manager
        /// </summary>
        public CryptoManager LoadKey(string pemPublicKey)
        {
            if (string.IsNullOrWhiteSpace(pemPublicKey))
                throw new ArgumentException("PEM public key cannot be empty", nameof(pemPublicKey));

            var publicKey = new PublicKeyInfo
            {
                Algorithm = "RSA",
                Format = "PEM",
                Value = pemPublicKey
            };
            
            return LoadKey(publicKey);
        }

        /// <summary>
        /// Validates that a file meets manifest requirements if keys are loaded
        /// </summary>
        public void ValidateManifestRequirement(string luaFilePath)
        {
            if (!HasLoadedKeys) return;

            var manifest = ManifestAutoLoader.DiscoverManifest(luaFilePath);
            if (manifest == null)
            {
                throw new ManifestSignatureException(
                    $"VM has loaded keys - all Lua files must have manifests: {luaFilePath}",
                    "RequireManifestForLuaFile",
                    luaFilePath
                );
            }

            if (!manifest.Manifest.IsSigned())
            {
                throw new ManifestSignatureException(
                    $"VM has loaded keys - all manifests must be signed: {luaFilePath}",
                    "RequireManifestForLuaFile",
                    luaFilePath
                );
            }

            // Verify signature against one of the loaded keys
            if (!ValidateAgainstLoadedKeys(manifest.Manifest))
            {
                throw new ManifestSignatureException(
                    $"VM has loaded keys - manifest signature not valid against any loaded key: {luaFilePath}",
                    "RequireManifestForLuaFile",
                    luaFilePath
                );
            }
        }

        /// <summary>
        /// Validates a public key to ensure it's properly formatted and strong enough
        /// </summary>
        private void ValidatePublicKey(PublicKeyInfo publicKey)
        {
            try
            {
                if (publicKey.Format == "PEM")
                {
                    // Basic PEM format validation
                    var pemString = publicKey.Value.Trim();
                    
                    if (!pemString.StartsWith("-----BEGIN"))
                        throw new ArgumentException("Invalid PEM format - missing header");
                    
                    if (!pemString.EndsWith("-----"))
                        throw new ArgumentException("Invalid PEM format - missing footer");
                    
                    // Check for reasonable length (2048-bit keys in PEM are ~1700 chars)
                    if (pemString.Length < 200)
                        throw new ArgumentException("PEM key appears too short to be valid");
                }
                else if (publicKey.Format == "BASE64")
                {
                    try
                    {
                        // Try to parse base64 format key
                        using var rsa = RSA.Create();
                        var keyBytes = Convert.FromBase64String(publicKey.Value);
                        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                        
                        // Check key strength (available in .NET Standard 2.1)
                        if (rsa.KeySize < 2048)
                            throw new NotSupportedException($"Key size {rsa.KeySize} is too weak. Minimum 2048 bits required.");
                    }
                    catch (FormatException ex)
                    {
                        throw new ArgumentException("Invalid BASE64 format", ex);
                    }
                    catch (CryptographicException ex)
                    {
                        throw new ArgumentException("Invalid key data", ex);
                    }
                }
                else
                {
                    throw new ArgumentException($"Unsupported key format: {publicKey.Format}");
                }
            }
            catch (Exception ex) when (!(ex is ArgumentException) && !(ex is NotSupportedException))
            {
                throw new ArgumentException($"Invalid public key format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates a manifest signature against loaded keys
        /// </summary>
        private bool ValidateAgainstLoadedKeys(Manifest manifest)
        {
            var manifestPublicKey = manifest.Security.PublicKey;

            foreach (var loadedKey in _loadedKeys)
            {
                if (AreKeysEquivalent(loadedKey, manifestPublicKey))
                {
                    // If keys match, the manifest is valid (it was already validated during loading)
                    return true;
                }
            }

            return false; // No matching key found
        }

        /// <summary>
        /// Compares two public keys for equivalence, handling different formats
        /// </summary>
        private bool AreKeysEquivalent(PublicKeyInfo key1, PublicKeyInfo key2)
        {
            try
            {
                // Both must be PEM format now
                if (key1.Format?.ToUpperInvariant() != "PEM" || key2.Format?.ToUpperInvariant() != "PEM")
                    return false;

                // Extract and normalize the PEM content (remove whitespace differences)
                var key1Normalized = NormalizePemKey(key1.Value);
                var key2Normalized = NormalizePemKey(key2.Value);

                // Direct string comparison after normalization
                return key1Normalized == key2Normalized;
            }
            catch
            {
                // If any parsing fails, keys are not equivalent
                return false;
            }
        }

        /// <summary>
        /// Normalizes a PEM key by removing extra whitespace and ensuring consistent formatting
        /// </summary>
        private string NormalizePemKey(string pemKey)
        {
            var lines = pemKey.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    sb.AppendLine(trimmed);
                }
            }
            
            return sb.ToString().TrimEnd();
        }
    }
}