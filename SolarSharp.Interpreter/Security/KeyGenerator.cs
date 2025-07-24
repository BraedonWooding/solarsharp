using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides utilities for generating RSA key pairs and computing fingerprints.
    /// This class provides cryptographic key generation for manifest signing.
    /// </summary>
    public static class KeyGenerator
    {
        /// <summary>
        /// Generates a new RSA key pair with the specified key size.
        /// </summary>
        /// <param name="keySize">The size of the RSA key in bits. Default is 2048.</param>
        /// <returns>A tuple containing the private key and public key parameters.</returns>
        public static (RsaKeyParameters privateKey, RsaKeyParameters publicKey) GenerateRsaKeyPair(int keySize = 2048)
        {
            var keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), keySize));
            var keyPair = keyGen.GenerateKeyPair();
            return ((RsaKeyParameters)keyPair.Private, (RsaKeyParameters)keyPair.Public);
        }

        /// <summary>
        /// Computes the SHA256 fingerprint of a public key.
        /// </summary>
        /// <param name="publicKey">The public key to compute the fingerprint for.</param>
        /// <returns>The fingerprint as a lowercase hexadecimal string without the "sha256:" prefix.</returns>
        public static string ComputeSha256Fingerprint(AsymmetricKeyParameter publicKey)
        {
            var publicKeyInfo = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            var publicKeyDer = publicKeyInfo.GetDerEncoded();
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(publicKeyDer);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Computes a public key token from a PEM-encoded public key.
        /// The token is the first 128 bits (16 bytes) of the SHA256 hash of the base64 DER bytes.
        /// </summary>
        /// <param name="publicKeyPem">The PEM-encoded public key.</param>
        /// <returns>The public key token as a lowercase hexadecimal string.</returns>
        public static string ComputePublicKeyToken(string publicKeyPem)
        {
            // Extract base64 content from PEM
            var lines = publicKeyPem.Split('\n');
            var sb = new StringBuilder();
            var inKey = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("-----BEGIN"))
                {
                    inKey = true;
                    continue;
                }
                if (trimmedLine.StartsWith("-----END"))
                {
                    break;
                }
                if (inKey && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    sb.Append(trimmedLine);
                }
            }
            
            var base64Key = sb.ToString();
            var keyBytes = Convert.FromBase64String(base64Key);

            // Generate SHA256 hash
            using (var sha256 = SHA256.Create())
            {
                var fullHash = sha256.ComputeHash(keyBytes);
                
                // Take first 16 bytes (128 bits) for token
                var tokenBytes = new byte[16];
                Array.Copy(fullHash, tokenBytes, 16);
                
                // Convert to hex string
                var hexString = new StringBuilder(tokenBytes.Length * 2);
                foreach (var b in tokenBytes)
                {
                    hexString.AppendFormat("{0:x2}", b);
                }
                return hexString.ToString();
            }
        }

        /// <summary>
        /// Converts a public key to PEM format.
        /// </summary>
        /// <param name="publicKey">The public key to convert.</param>
        /// <returns>The PEM-encoded public key as a string.</returns>
        public static string PublicKeyToPem(AsymmetricKeyParameter publicKey)
        {
            using (var sw = new StringWriter())
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(publicKey);
                pemWriter.Writer.Flush();
                return sw.ToString();
            }
        }

        /// <summary>
        /// Converts a private key to PEM format.
        /// </summary>
        /// <param name="privateKey">The private key to convert.</param>
        /// <returns>The PEM-encoded private key as a string.</returns>
        public static string PrivateKeyToPem(AsymmetricKeyParameter privateKey)
        {
            using (var sw = new StringWriter())
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(privateKey);
                pemWriter.Writer.Flush();
                return sw.ToString();
            }
        }

        /// <summary>
        /// Creates a test RSA key pair and returns both the key parameters and PEM representations.
        /// </summary>
        /// <param name="keySize">The size of the RSA key in bits. Default is 2048.</param>
        /// <returns>A tuple containing the key parameters and their PEM representations.</returns>
        public static (RsaKeyParameters privateKey, RsaKeyParameters publicKey, string privateKeyPem, string publicKeyPem) GenerateTestKeyPair(int keySize = 2048)
        {
            var (privateKey, publicKey) = GenerateRsaKeyPair(keySize);
            var privateKeyPem = PrivateKeyToPem(privateKey);
            var publicKeyPem = PublicKeyToPem(publicKey);
            return (privateKey, publicKey, privateKeyPem, publicKeyPem);
        }
    }
}