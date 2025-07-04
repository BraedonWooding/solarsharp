using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Utility for signing Lua manifests
    /// </summary>
    public static class ManifestSigner
    {
        /// <summary>
        /// Signs a manifest file with the provided private key
        /// </summary>
        public static void SignManifest(string manifestPath, AsymmetricAlgorithm privateKey, string algorithm = "RSA")
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

            var json = File.ReadAllText(manifestPath);
            var signedJson = SignManifestJson(json, privateKey, algorithm);
            
            File.WriteAllText(manifestPath, signedJson);
        }

        /// <summary>
        /// Signs a manifest JSON string with the provided private key
        /// </summary>
        public static string SignManifestJson(string json, AsymmetricAlgorithm privateKey, string algorithm = "RSA")
        {
            // Parse the manifest
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Create a mutable copy
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Copy all existing properties
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "security")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }

                // Add or update security section
                writer.WritePropertyName("security");
                writer.WriteStartObject();

                // Add public key
                writer.WritePropertyName("publicKey");
                writer.WriteStartObject();
                writer.WriteString("algorithm", algorithm.ToUpperInvariant());
                writer.WriteString("format", "PEM");
                writer.WriteString("value", ExportPublicKey(privateKey, algorithm));
                writer.WriteEndObject();

                // Prepare for signature - need to close objects first
                writer.WriteEndObject(); // security
                writer.WriteEndObject(); // root
                writer.Flush();
            }

            // Get the JSON without signature for canonicalization
            var unsignedJson = Encoding.UTF8.GetString(stream.ToArray());
            
            // Canonicalize the JSON
            var canonicalJson = JsonCanonicalizer.Canonicalize(unsignedJson);
            var dataToSign = Encoding.UTF8.GetBytes(canonicalJson);

            // Sign the canonical JSON
            byte[] signature;
            string signatureAlgorithm;

            switch (privateKey)
            {
                case RSA rsa:
                    signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    signatureAlgorithm = "SHA256withRSA";
                    break;

                case ECDsa ecdsa:
                    signature = ecdsa.SignData(dataToSign, HashAlgorithmName.SHA256);
                    // Determine specific curve for algorithm name
                    if (ecdsa.KeySize == 384)
                    {
                        signatureAlgorithm = "SHA256withECDSA-P384";
                    }
                    else
                    {
                        signatureAlgorithm = "SHA256withECDSA-P256";
                    }
                    break;

                default:
                    throw new NotSupportedException($"Algorithm not supported: {privateKey.GetType().Name}");
            }

            // Now add the signature to the JSON
            using var finalStream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(finalStream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Copy all properties including security
                using var docWithKey = JsonDocument.Parse(unsignedJson);
                foreach (var property in docWithKey.RootElement.EnumerateObject())
                {
                    if (property.Name == "security")
                    {
                        writer.WritePropertyName("security");
                        writer.WriteStartObject();

                        // Copy existing security properties
                        foreach (var secProp in property.Value.EnumerateObject())
                        {
                            writer.WritePropertyName(secProp.Name);
                            secProp.Value.WriteTo(writer);
                        }

                        // Add signature
                        writer.WritePropertyName("signature");
                        writer.WriteStartObject();
                        writer.WriteString("algorithm", signatureAlgorithm);
                        writer.WriteString("value", Convert.ToBase64String(signature));
                        writer.WriteEndObject();

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
                writer.Flush();
            }

            return Encoding.UTF8.GetString(finalStream.ToArray());
        }

        /// <summary>
        /// Creates a new key pair for signing manifests
        /// </summary>
        public static AsymmetricAlgorithm CreateKeyPair(string algorithm = "RSA", int keySize = 2048)
        {
            switch (algorithm.ToUpperInvariant())
            {
                case "RSA":
                    var rsa = RSA.Create(keySize);
                    return rsa;

                case "ECDSA":
                case "ECDSA-P256":
                    // PIV cards only support P-256 (secp256r1) curve
                    if (keySize != 256)
                        throw new ArgumentException($"PIV-compatible ECDSA only supports 256-bit keys (P-256 curve), got: {keySize}");
                    
                    var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                    return ecdsa;

                default:
                    throw new NotSupportedException($"Algorithm not supported: {algorithm}");
            }
        }

        /// <summary>
        /// Exports a public key in BASE64 format
        /// </summary>
        private static string ExportPublicKeyAsBase64(AsymmetricAlgorithm key, string algorithm)
        {
            byte[] publicKeyBytes = key switch
            {
                RSA rsa => rsa.ExportSubjectPublicKeyInfo(),
                ECDsa ecdsa => ecdsa.ExportSubjectPublicKeyInfo(),
                _ => throw new NotSupportedException($"Key type not supported: {key.GetType().Name}")
            };

            return Convert.ToBase64String(publicKeyBytes);
        }

        /// <summary>
        /// Exports a public key in PEM format (for compatibility)
        /// </summary>
        public static string ExportPublicKey(AsymmetricAlgorithm key, string algorithm = "RSA")
        {
            byte[] publicKeyBytes;
            string pemType;

            switch (key)
            {
                case RSA rsa:
                    publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
                    pemType = "PUBLIC KEY";
                    break;

                case ECDsa ecdsa:
                    publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
                    pemType = "PUBLIC KEY";
                    break;

                default:
                    throw new NotSupportedException($"Key type not supported: {key.GetType().Name}");
            }

            var base64 = Convert.ToBase64String(publicKeyBytes);
            var sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {pemType}-----");
            
            // Add line breaks every 64 characters
            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }
            
            sb.AppendLine($"-----END {pemType}-----");
            return sb.ToString();
        }

        /// <summary>
        /// Loads a private key from PEM format
        /// </summary>
        public static AsymmetricAlgorithm LoadPrivateKeyFromPem(string pemContent)
        {
            var base64 = ExtractBase64FromPem(pemContent);
            var keyBytes = Convert.FromBase64String(base64);

            // Try RSA first
            try
            {
                var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                return rsa;
            }
            catch { }

            // Try ECDSA
            try
            {
                var ecdsa = ECDsa.Create();
                ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);
                return ecdsa;
            }
            catch { }

            throw new NotSupportedException("Unable to load private key. Ensure it's in PKCS#8 format.");
        }

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

        /// <summary>
        /// Signs a manifest with an X.509 certificate and embeds the certificate chain
        /// </summary>
        /// <param name="manifestPath">Path to the manifest file</param>
        /// <param name="certificate">X.509 certificate with private key</param>
        /// <param name="intermediates">Optional intermediate certificates</param>
        public static void SignManifestWithCertificate(string manifestPath, X509Certificate2 certificate, X509Certificate2Collection intermediates = null)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

            var json = File.ReadAllText(manifestPath);
            var signedJson = SignManifestJsonWithCertificate(json, certificate, intermediates);
            
            File.WriteAllText(manifestPath, signedJson);
        }

        /// <summary>
        /// Signs a manifest JSON string with an X.509 certificate
        /// </summary>
        /// <param name="json">Manifest JSON content</param>
        /// <param name="certificate">X.509 certificate with private key</param>
        /// <param name="intermediates">Optional intermediate certificates</param>
        /// <returns>Signed manifest JSON with embedded certificate chain</returns>
        public static string SignManifestJsonWithCertificate(string json, X509Certificate2 certificate, X509Certificate2Collection intermediates = null)
        {
            if (!certificate.HasPrivateKey)
                throw new ArgumentException("Certificate must have a private key for signing");

            // Build certificate chain
            var certificateChain = new List<string>();
            
            // Add leaf certificate (the signing certificate)
            certificateChain.Add(ConvertCertificateToPem(certificate));
            
            // Add intermediate certificates
            if (intermediates != null)
            {
                foreach (var intermediate in intermediates.Cast<X509Certificate2>())
                {
                    certificateChain.Add(ConvertCertificateToPem(intermediate));
                }
            }

            // Parse the manifest
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Create a mutable copy with certificate chain
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Copy all existing properties except security
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "security")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }

                // Add security section with certificate chain
                writer.WritePropertyName("security");
                writer.WriteStartObject();

                // Add certificate chain
                writer.WritePropertyName("certificateChain");
                writer.WriteStartArray();
                foreach (var certPem in certificateChain)
                {
                    writer.WriteStringValue(certPem);
                }
                writer.WriteEndArray();

                writer.WriteEndObject(); // security
                writer.WriteEndObject(); // root
                writer.Flush();
            }

            // Get the JSON without signature for canonicalization
            var unsignedJson = Encoding.UTF8.GetString(stream.ToArray());
            
            // Canonicalize the JSON
            var canonicalJson = JsonCanonicalizer.Canonicalize(unsignedJson);
            var dataToSign = Encoding.UTF8.GetBytes(canonicalJson);

            // Sign with the certificate's private key
            byte[] signature;
            string signatureAlgorithm;

            using (var privateKey = certificate.GetRSAPrivateKey() ?? (AsymmetricAlgorithm)certificate.GetECDsaPrivateKey())
            {
                switch (privateKey)
                {
                    case RSA rsa:
                        signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        signatureAlgorithm = "SHA256withRSA";
                        break;

                    case ECDsa ecdsa:
                        signature = ecdsa.SignData(dataToSign, HashAlgorithmName.SHA256);
                        // Determine specific curve for algorithm name
                        if (ecdsa.KeySize == 384)
                        {
                            signatureAlgorithm = "SHA256withECDSA-P384";
                        }
                        else
                        {
                            signatureAlgorithm = "SHA256withECDSA-P256";
                        }
                        break;

                    default:
                        throw new NotSupportedException($"Certificate key algorithm not supported: {privateKey.GetType().Name}");
                }
            }

            // Now add the signature to the JSON
            using var finalStream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(finalStream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Copy all properties including security
                using var docWithCert = JsonDocument.Parse(unsignedJson);
                foreach (var property in docWithCert.RootElement.EnumerateObject())
                {
                    if (property.Name == "security")
                    {
                        writer.WritePropertyName("security");
                        writer.WriteStartObject();

                        // Copy existing security properties
                        foreach (var secProp in property.Value.EnumerateObject())
                        {
                            writer.WritePropertyName(secProp.Name);
                            secProp.Value.WriteTo(writer);
                        }

                        // Add signature
                        writer.WritePropertyName("signature");
                        writer.WriteStartObject();
                        writer.WriteString("algorithm", signatureAlgorithm);
                        writer.WriteString("value", Convert.ToBase64String(signature));
                        writer.WriteEndObject();

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
                writer.Flush();
            }

            return Encoding.UTF8.GetString(finalStream.ToArray());
        }

        /// <summary>
        /// Converts an X.509 certificate to PEM format
        /// </summary>
        private static string ConvertCertificateToPem(X509Certificate2 certificate)
        {
            var base64 = Convert.ToBase64String(certificate.RawData);
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            
            // Add line breaks every 64 characters
            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }
            
            sb.AppendLine("-----END CERTIFICATE-----");
            return sb.ToString();
        }

        /// <summary>
        /// Loads an X.509 certificate from PEM format
        /// </summary>
        public static X509Certificate2 LoadCertificateFromPem(string pemContent, string privateKeyPem = null)
        {
            var certificate = new X509Certificate2(Encoding.UTF8.GetBytes(pemContent));
            
            if (!string.IsNullOrEmpty(privateKeyPem))
            {
                // Load private key and associate with certificate
                var privateKey = LoadPrivateKeyFromPem(privateKeyPem);
                
                switch (privateKey)
                {
                    case RSA rsa:
                        certificate = certificate.CopyWithPrivateKey(rsa);
                        break;
                    case ECDsa ecdsa:
                        certificate = certificate.CopyWithPrivateKey(ecdsa);
                        break;
                }
            }
            
            return certificate;
        }

        /// <summary>
        /// Creates a self-signed certificate for testing
        /// </summary>
        public static X509Certificate2 CreateSelfSignedCertificate(string subjectName, string subjectPath = null, int validDays = 365)
        {
            var distinguishedName = new X500DistinguishedName($"CN={subjectPath ?? subjectName}");
            
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            // Add key usage extensions
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            
            // Add extended key usage
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.3") }, // Code signing
                    true));
            
            // Create the certificate
            var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(validDays));
            
            // Export and reimport to make it work properly on all platforms
            var exported = certificate.Export(X509ContentType.Pfx, "temp");
            return new X509Certificate2(exported, "temp", X509KeyStorageFlags.Exportable);
        }
    }
}