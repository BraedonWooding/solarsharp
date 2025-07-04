using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides cryptographic validation for manifests
    /// </summary>
    public static class CryptographicValidator
    {
        /// <summary>
        /// Validates the signature of a manifest
        /// </summary>
        public static bool ValidateSignature(Manifest.Manifest manifest)
        {
            if (!manifest.IsSigned()) return false;

            try
            {
                var publicKeyPem = manifest.Security.PublicKey.Value;
                var signatureBase64 = manifest.Security.Signature.Value;
                var signatureAlgorithm = manifest.Security.Signature.Algorithm;
                
                // Validate signature algorithm is allowed
                if (!IsAllowedSignatureAlgorithm(signatureAlgorithm))
                {
                    return false;
                }
                
                // Get signable content using same canonicalization as ManifestSigner
                var signableContent = GetSignableContentUsingCanonicalizer(manifest);
                
                // Parse PEM public key
                var publicKeyBytes = ParsePemPublicKey(publicKeyPem);
                var signature = Convert.FromBase64String(signatureBase64);
                
                // Verify signature based on algorithm
                var algorithm = manifest.Security.PublicKey.Algorithm;
                var data = Encoding.UTF8.GetBytes(signableContent);
                
                switch (algorithm?.ToUpperInvariant())
                {
                    case "RSA":
                        using (var rsa = RSA.Create())
                        {
                            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                            
                            // PIV validation: Only RSA 1024 and 2048 are supported
                            var keySize = rsa.KeySize;
                            if (keySize != 1024 && keySize != 2048)
                            {
                                return false; // PIV only supports RSA 1024/2048
                            }
                            
                            return rsa.VerifyData(
                                data,
                                signature,
                                HashAlgorithmName.SHA256,
                                RSASignaturePadding.Pkcs1
                            );
                        }
                        
                    case "ECDSA":
                        using (var ecdsa = ECDsa.Create())
                        {
                            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                            
                            // PIV validation: Only P-256 and P-384 curves are supported
                            var keySize = ecdsa.KeySize;
                            if (keySize != 256 && keySize != 384)
                            {
                                return false; // PIV only supports P-256 and P-384
                            }
                            
                            return ecdsa.VerifyData(
                                data,
                                signature,
                                HashAlgorithmName.SHA256
                            );
                        }
                        
                    default:
                        return false; // Unsupported algorithm
                }
            }
            catch (Exception)
            {
                return false; // Any crypto exception = invalid signature
            }
        }

        /// <summary>
        /// Gets the signable content using the same canonicalization as ManifestSigner
        /// </summary>
        private static string GetSignableContentUsingCanonicalizer(Manifest.Manifest manifest)
        {
            // Recreate the JSON without the signature section, using the same approach as ManifestSigner
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Write all properties in order
                if (!string.IsNullOrEmpty(manifest.Version))
                {
                    writer.WriteString("version", manifest.Version);
                }
                
                if (!string.IsNullOrEmpty(manifest.Description))
                {
                    writer.WriteString("description", manifest.Description);
                }
                
                if (!string.IsNullOrEmpty(manifest.Type))
                {
                    writer.WriteString("type", manifest.Type);
                }

                if (manifest.Policy != null)
                {
                    writer.WritePropertyName("policy");
                    JsonSerializer.Serialize(writer, manifest.Policy);
                }

                if (manifest.Files != null && manifest.Files.Count > 0)
                {
                    writer.WritePropertyName("files");
                    JsonSerializer.Serialize(writer, manifest.Files);
                }

                if (manifest.Includes != null && manifest.Includes.Count > 0)
                {
                    writer.WritePropertyName("includes");
                    JsonSerializer.Serialize(writer, manifest.Includes);
                }

                // Add security section without signature
                writer.WritePropertyName("security");
                writer.WriteStartObject();
                
                if (manifest.Security.PublicKey != null)
                {
                    writer.WritePropertyName("publicKey");
                    JsonSerializer.Serialize(writer, manifest.Security.PublicKey);
                }
                
                writer.WriteEndObject(); // security
                writer.WriteEndObject(); // root
                writer.Flush();
            }

            var unsignedJson = Encoding.UTF8.GetString(stream.ToArray());
            
            // Use the same canonicalization as ManifestSigner
            return Manifest.JsonCanonicalizer.Canonicalize(unsignedJson);
        }

        /// <summary>
        /// Gets the signable content of a manifest (excluding the signature itself) - Legacy method
        /// </summary>
        private static string GetSignableContent(Manifest.Manifest manifest)
        {
            // Create a copy of the manifest without the signature for signing
            var signable = new
            {
                version = manifest.Version,
                description = manifest.Description,
                type = manifest.Type,
                policy = manifest.Policy,
                files = manifest.Files,
                includes = manifest.Includes,
                security = new
                {
                    publicKey = manifest.Security.PublicKey
                    // Exclude signature from signed content
                }
            };

            return JsonSerializer.Serialize(signable, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Parses a PEM-formatted RSA public key
        /// </summary>
        private static byte[] ParsePemPublicKey(string pemKey)
        {
            if (string.IsNullOrWhiteSpace(pemKey))
                throw new ArgumentException("PEM key cannot be null or empty", nameof(pemKey));

            // Remove PEM headers and footers, and whitespace
            var base64Key = pemKey
                .Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                .Replace("-----END RSA PUBLIC KEY-----", "")
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "");

            return Convert.FromBase64String(base64Key);
        }

        /// <summary>
        /// Validates that a PEM key is properly formatted
        /// </summary>
        public static bool IsValidPemKey(string pemKey)
        {
            if (string.IsNullOrWhiteSpace(pemKey)) return false;

            try
            {
                var keyBytes = ParsePemPublicKey(pemKey);
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(keyBytes, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a signature algorithm is allowed
        /// </summary>
        private static bool IsAllowedSignatureAlgorithm(string algorithm)
        {
            if (string.IsNullOrWhiteSpace(algorithm)) return false;
            
            var upperAlgorithm = algorithm.ToUpperInvariant();
            
            // Only allow SHA256-based algorithms
            return upperAlgorithm == "SHA256WITHRSA" || 
                   upperAlgorithm == "SHA256WITHECDSA" ||
                   upperAlgorithm == "SHA256WITHECDSA-P256" ||
                   upperAlgorithm == "SHA256WITHECDSA-P384";
        }
    }
}