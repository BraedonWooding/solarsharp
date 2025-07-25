using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// BouncyCastle wrapper to maintain compatibility with AsymmetricAlgorithm interface
    /// </summary>
    public abstract class BouncyCastleAsymmetricAlgorithm : IDisposable
    {
        public abstract AsymmetricKeyParameter PublicKey { get; }
        public abstract int KeySize { get; }
        public abstract byte[] ExportSubjectPublicKeyInfo();

        public virtual void Dispose() { }
    }

    /// <summary>
    /// BouncyCastle RSA wrapper
    /// </summary>
    public sealed class BouncyCastleRsa : BouncyCastleAsymmetricAlgorithm
    {
        private readonly RsaKeyParameters _publicKey;

        public BouncyCastleRsa(RsaKeyParameters publicKey)
        {
            _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        }

        public override AsymmetricKeyParameter PublicKey => _publicKey;
        public override int KeySize => _publicKey.Modulus.BitLength;

        public override byte[] ExportSubjectPublicKeyInfo()
        {
            var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(_publicKey);
            return publicKeyInfo.GetDerEncoded();
        }
    }

    /// <summary>
    /// BouncyCastle ECDSA wrapper
    /// </summary>
    public sealed class BouncyCastleEcdsa : BouncyCastleAsymmetricAlgorithm
    {
        private readonly ECPublicKeyParameters _publicKey;

        public BouncyCastleEcdsa(ECPublicKeyParameters publicKey)
        {
            _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        }

        public override AsymmetricKeyParameter PublicKey => _publicKey;
        public override int KeySize => _publicKey.Parameters.Curve.FieldSize;

        public override byte[] ExportSubjectPublicKeyInfo()
        {
            var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(_publicKey);
            return publicKeyInfo.GetDerEncoded();
        }
    }

    /// <summary>
    /// Functional cryptographic key management using BouncyCastle
    /// </summary>
    public static class CryptoKeyManager
    {
        /// <summary>
        /// Result of parsing a PEM-encoded key
        /// </summary>
        public sealed record ParsedKey(
            BouncyCastleAsymmetricAlgorithm Algorithm,
            int KeySize,
            string Fingerprint
        ) : IDisposable
        {
            public void Dispose() => Algorithm?.Dispose();
        }

        /// <summary>
        /// Parses a PEM-encoded public key
        /// </summary>
        public static Result<ParsedKey, string> ParsePublicKey(string pemKey)
        {
            if (string.IsNullOrWhiteSpace(pemKey))
                return Result.Failure<ParsedKey, string>("Key cannot be empty");

            try
            {
                // Extract PEM content using regex for .NET Standard 2.1 compatibility
                var pemFields = ExtractPemFields(pemKey);
                if (pemFields.Count == 0)
                    return Result.Failure<ParsedKey, string>("No valid PEM data found");

                // Look for public key field
                foreach (var (label, base64Data) in pemFields)
                {
                    var bytes = Convert.FromBase64String(base64Data);

                    switch (label)
                    {
                        case "PUBLIC KEY":
                            return ParseSubjectPublicKeyInfo(bytes);

                        case "RSA PUBLIC KEY":
                            return ParseRsaPublicKey(bytes);

                        case "EC PUBLIC KEY":
                            return ParseEcPublicKey(bytes);

                        default:
                            continue;
                    }
                }

                return Result.Failure<ParsedKey, string>(
                    "No supported public key type found in PEM data"
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<ParsedKey, string>($"Failed to parse PEM key: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates key strength requirements
        /// </summary>
        public static Result<string> ValidateKeyStrength(ParsedKey key)
        {
            const int MinimumRsaKeySize = 2048;
            const int MinimumEcKeySize = 256;

            return key.Algorithm switch
            {
                BouncyCastleRsa _ when key.KeySize < MinimumRsaKeySize => Result.Failure<string>(
                    $"RSA key size {key.KeySize} is too weak. Minimum {MinimumRsaKeySize} bits required"
                ),

                BouncyCastleEcdsa _ when key.KeySize < MinimumEcKeySize => Result.Failure<string>(
                    $"EC key size {key.KeySize} is too weak. Minimum {MinimumEcKeySize} bits required"
                ),

                _ => Result.Success("Key strength validated"),
            };
        }

        /// <summary>
        /// Validates that the key meets PIV compliance requirements
        /// </summary>
        public static Result<string> ValidatePivCompliance(ParsedKey key)
        {
            return key.Algorithm switch
            {
                BouncyCastleRsa _ when key.KeySize != 1024 && key.KeySize != 2048 =>
                    Result.Failure<string>(
                        $"RSA key size {key.KeySize} is not PIV compliant. Only 1024 or 2048 bits allowed"
                    ),

                BouncyCastleEcdsa ecdsa when !IsPivCompliantEcdsaCurve(ecdsa) =>
                    Result.Failure<string>(
                        "ECDSA curve is not PIV compliant. Only P-256 and P-384 curves allowed"
                    ),

                _ => Result.Success("Key is PIV compliant"),
            };
        }

        /// <summary>
        /// Validates key strength for PIV scenarios (allows 1024-bit RSA keys)
        /// </summary>
        public static Result<string> ValidateKeyStrengthForPiv(ParsedKey key)
        {
            const int MinimumEcKeySize = 256;

            return key.Algorithm switch
            {
                // For PIV, allow 1024-bit RSA keys (even though they're weak)
                BouncyCastleRsa _ when key.KeySize < 1024 => Result.Failure<string>(
                    $"RSA key size {key.KeySize} is too weak for PIV. Minimum 1024 bits required"
                ),

                BouncyCastleEcdsa _ when key.KeySize < MinimumEcKeySize => Result.Failure<string>(
                    $"EC key size {key.KeySize} is too weak. Minimum {MinimumEcKeySize} bits required"
                ),

                _ => Result.Success("Key strength validated for PIV"),
            };
        }

        /// <summary>
        /// Checks if an ECDSA key uses a PIV-compliant curve (P-256 or P-384)
        /// </summary>
        private static bool IsPivCompliantEcdsaCurve(BouncyCastleEcdsa ecdsa)
        {
            // PIV compliance allows only P-256 (secp256r1) and P-384 (secp384r1)
            // Key size 256 = P-256, Key size 384 = P-384
            return ecdsa.KeySize is 256 or 384;
        }

        /// <summary>
        /// Extracts PEM fields from a PEM-encoded string
        /// </summary>
        private static List<(string label, string base64Data)> ExtractPemFields(string pemContent)
        {
            var results = new List<(string, string)>();
            var pemPattern = @"-----BEGIN\s+([^-]+)-----\s*(.*?)\s*-----END\s+\1-----";
            var matches = Regex.Matches(
                pemContent,
                pemPattern,
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            );

            foreach (Match match in matches)
            {
                var label = match.Groups[1].Value.Trim();
                var base64Data = match
                    .Groups[2]
                    .Value.Replace("\r", "")
                    .Replace("\n", "")
                    .Replace(" ", "");
                results.Add((label, base64Data));
            }

            return results;
        }

        /// <summary>
        /// Computes a SHA256 fingerprint of the key using BouncyCastle
        /// </summary>
        public static string ComputeFingerprint(BouncyCastleAsymmetricAlgorithm algorithm)
        {
            var keyBytes = algorithm.ExportSubjectPublicKeyInfo();
            var digest = new Sha256Digest();
            var hash = new byte[digest.GetDigestSize()];

            digest.BlockUpdate(keyBytes, 0, keyBytes.Length);
            digest.DoFinal(hash, 0);

            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Parses SubjectPublicKeyInfo (SPKI) format - handles both RSA and EC keys
        /// </summary>
        private static Result<ParsedKey, string> ParseSubjectPublicKeyInfo(byte[] keyBytes)
        {
            try
            {
                var spki = SubjectPublicKeyInfo.GetInstance(Asn1Object.FromByteArray(keyBytes));
                var publicKey = PublicKeyFactory.CreateKey(spki);

                return publicKey switch
                {
                    RsaKeyParameters rsaKey => CreateRsaParsedKey(rsaKey),
                    ECPublicKeyParameters ecKey => CreateEcParsedKey(ecKey),
                    _ => Result.Failure<ParsedKey, string>(
                        $"Unsupported key type: {publicKey.GetType().Name}"
                    ),
                };
            }
            catch (Exception ex)
            {
                return Result.Failure<ParsedKey, string>(
                    $"Failed to parse SubjectPublicKeyInfo: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Parses RSA public key in PKCS#1 format
        /// </summary>
        private static Result<ParsedKey, string> ParseRsaPublicKey(byte[] keyBytes)
        {
            try
            {
                var rsaPublicKey = RsaPublicKeyStructure.GetInstance(
                    Asn1Object.FromByteArray(keyBytes)
                );
                var rsaKeyParams = new RsaKeyParameters(
                    false,
                    rsaPublicKey.Modulus,
                    rsaPublicKey.PublicExponent
                );

                return CreateRsaParsedKey(rsaKeyParams);
            }
            catch (Exception ex)
            {
                return Result.Failure<ParsedKey, string>($"Failed to parse RSA key: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses EC public key
        /// </summary>
        private static Result<ParsedKey, string> ParseEcPublicKey(byte[] keyBytes)
        {
            try
            {
                // For EC PUBLIC KEY format, we need to reconstruct the full SPKI
                // This is a simplified approach - in practice, we'd need the curve parameters
                return Result.Failure<ParsedKey, string>(
                    "EC PUBLIC KEY format requires curve parameters. Use SubjectPublicKeyInfo format instead."
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<ParsedKey, string>($"Failed to parse EC key: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a ParsedKey from RSA key parameters
        /// </summary>
        private static Result<ParsedKey, string> CreateRsaParsedKey(RsaKeyParameters rsaKeyParams)
        {
            var algorithm = new BouncyCastleRsa(rsaKeyParams);
            var keySize = algorithm.KeySize;
            var fingerprint = ComputeFingerprint(algorithm);

            return Result.Success<ParsedKey, string>(
                new ParsedKey(algorithm, keySize, fingerprint)
            );
        }

        /// <summary>
        /// Creates a ParsedKey from EC key parameters
        /// </summary>
        private static Result<ParsedKey, string> CreateEcParsedKey(
            ECPublicKeyParameters ecKeyParams
        )
        {
            var algorithm = new BouncyCastleEcdsa(ecKeyParams);
            var keySize = algorithm.KeySize;
            var fingerprint = ComputeFingerprint(algorithm);

            return Result.Success<ParsedKey, string>(
                new ParsedKey(algorithm, keySize, fingerprint)
            );
        }
    }

    /// <summary>
    /// Immutable collection of trusted keys
    /// </summary>
    public sealed record TrustedKeyStore(
        ImmutableHashSet<string> Fingerprints,
        ImmutableDictionary<string, BouncyCastleAsymmetricAlgorithm> Keys
    )
    {
        public static TrustedKeyStore Empty
        {
            get
            {
                return new TrustedKeyStore(
                    ImmutableHashSet<string>.Empty,
                    ImmutableDictionary<string, BouncyCastleAsymmetricAlgorithm>.Empty
                );
            }
        }

        /// <summary>
        /// Adds a trusted key to the store
        /// </summary>
        public Result<TrustedKeyStore, string> AddTrustedKey(string pemKey)
        {
            var parseResult = CryptoKeyManager.ParsePublicKey(pemKey);
            if (parseResult.IsFailure)
                return Result.Failure<TrustedKeyStore, string>(parseResult.Error);

            var parsedKey = parseResult.Value;
            var validationResult = CryptoKeyManager.ValidateKeyStrengthForPiv(parsedKey);
            if (validationResult.IsFailure)
                return Result.Failure<TrustedKeyStore, string>(validationResult.Error);

            var newStore = new TrustedKeyStore(
                Fingerprints: Fingerprints.Add(parsedKey.Fingerprint),
                Keys: Keys.SetItem(parsedKey.Fingerprint, parsedKey.Algorithm)
            );

            return Result.Success<TrustedKeyStore, string>(newStore);
        }

        /// <summary>
        /// Checks if a key fingerprint is trusted
        /// </summary>
        public bool IsTrusted(string fingerprint) => Fingerprints.Contains(fingerprint);

        /// <summary>
        /// Gets a trusted key by fingerprint
        /// </summary>
        public Maybe<BouncyCastleAsymmetricAlgorithm> GetKey(string fingerprint) =>
            Keys.TryGetValue(fingerprint, out var key)
                ? Maybe<BouncyCastleAsymmetricAlgorithm>.From(key)
                : Maybe<BouncyCastleAsymmetricAlgorithm>.None;
    }
}
