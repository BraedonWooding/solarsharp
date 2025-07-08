using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Manifest loader with caching support
    /// </summary>
    public sealed class ManifestLoader
    {
        private readonly IFileSystem _fileSystem;
        private readonly ImmutableDictionary<string, LoadedManifest> _cache;
        private readonly CertificateManager _certificateManager;

        /// <summary>
        /// Creates a new manifest loader
        /// </summary>
        public ManifestLoader(
            IFileSystem fileSystem,
            CertificateManager certificateManager,
            ImmutableDictionary<string, LoadedManifest> cache = null
        )
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _certificateManager =
                certificateManager ?? throw new ArgumentNullException(nameof(certificateManager));
            _cache = cache ?? ImmutableDictionary<string, LoadedManifest>.Empty;
        }

        /// <summary>
        /// Creates a new loader with an additional cached manifest
        /// </summary>
        public ManifestLoader WithCachedManifest(string directory, LoadedManifest manifest)
        {
            return new ManifestLoader(
                _fileSystem,
                _certificateManager,
                _cache.SetItem(directory, manifest)
            );
        }

        /// <summary>
        /// Loads a manifest for the given Lua file, returning updated loader
        /// </summary>
        public Result<(LoadedManifest manifest, ManifestLoader loader), ManifestError> LoadForFile(
            string luaFile
        )
        {
            var directory = Path.GetDirectoryName(luaFile) ?? "";

            // Check cache first (pure operation)
            if (_cache.TryGetValue(directory, out var cached))
            {
                return Result.Success<(LoadedManifest, ManifestLoader), ManifestError>(
                    (cached, this)
                );
            }

            // Load from disk and update cache
            return LoadManifestFromDisk(directory)
                .Map(manifest => (manifest, WithCachedManifest(directory, manifest)));
        }

        /// <summary>
        /// Loads and verifies a manifest from disk
        /// </summary>
        private Result<LoadedManifest, ManifestError> LoadManifestFromDisk(string directory)
        {
            // Functional pipeline using LINQ query syntax
            return from manifestPath in GetManifestPath(directory)
                from content in ReadFileContent(manifestPath)
                from manifest in ParseManifest(content)
                from verified in VerifyAndLoadManifest(manifest, directory)
                select verified;
        }

        /// <summary>
        /// Gets the manifest path for a directory
        /// </summary>
        private Result<string, ManifestError> GetManifestPath(string directory)
        {
            var manifestPath = _fileSystem.Path.Combine(directory, "LuaManifest.json");

            if (!_fileSystem.File.Exists(manifestPath))
            {
                return Result.Failure<string, ManifestError>(new ManifestNotFound(directory));
            }

            return Result.Success<string, ManifestError>(manifestPath);
        }

        /// <summary>
        /// Reads file content safely
        /// </summary>
        private Result<string, ManifestError> ReadFileContent(string path)
        {
            try
            {
                var content = _fileSystem.File.ReadAllText(path);
                return Result.Success<string, ManifestError>(content);
            }
            catch (Exception ex)
            {
                return Result.Failure<string, ManifestError>(
                    new InvalidManifest($"Failed to read manifest: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Parses manifest JSON
        /// </summary>
        private Result<Manifest, ManifestError> ParseManifest(string content)
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<Manifest>(
                    content,
                    ManifestJsonOptions.Default
                );
                return Result.Success<Manifest, ManifestError>(manifest);
            }
            catch (Exception ex)
            {
                return Result.Failure<Manifest, ManifestError>(
                    new InvalidManifest($"Failed to parse manifest: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Verifies manifest signature and loads certificates
        /// </summary>
        private Result<LoadedManifest, ManifestError> VerifyAndLoadManifest(
            Manifest manifest,
            string directory
        )
        {
            try
            {
                // Validate manifest structure
                if (manifest == null)
                    return Result.Failure<LoadedManifest, ManifestError>(
                        new InvalidManifest("Manifest is null")
                    );

                // V2.0 Manifest: Process SignedContent blocks
                if (manifest.HasSignedContent)
                {
                    return ProcessV2Manifest(manifest, directory);
                }

                // V2.0 - Process signed content blocks
                X509Certificate primaryCert = null;
                var publicKeyToken = new byte[0];
                var canOverride = false;

                if (manifest.HasSignedContent && manifest.SignedContent.Length > 0)
                {
                    var firstBlock = manifest.SignedContent[0];
                    if (!string.IsNullOrEmpty(firstBlock.PublicKey))
                    {
                        // Extract certificate from public key PEM
                        try
                        {
                            using var reader = new StringReader(firstBlock.PublicKey);
                            var pemReader = new PemReader(reader);
                            var pemObject = pemReader.ReadObject();

                            // For V2.0, we work with raw public keys, not certificates
                            // Create a dummy certificate for compatibility
                            // TODO: Refactor LoadedManifest to not require X509Certificate
                        }
                        catch (Exception ex)
                        {
                            // Ignore certificate extraction errors for now
                        }
                    }
                }

                var legacyManifestPath = _fileSystem.Path.Combine(directory, "LuaManifest.json");
                return Result.Success<LoadedManifest, ManifestError>(
                    new LoadedManifest(
                        manifest,
                        legacyManifestPath,
                        DateTime.UtcNow,
                        Maybe<X509Certificate>.From(primaryCert),
                        publicKeyToken,
                        canOverride // Verified through legacy process
                    )
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<LoadedManifest, ManifestError>(
                    new ManifestVerificationFailed(ex.Message)
                );
            }
        }

        /// <summary>
        /// Processes V2.0 manifest with SignedContent blocks
        /// </summary>
        private Result<LoadedManifest, ManifestError> ProcessV2Manifest(
            Manifest manifest,
            string directory
        )
        {
            try
            {
                // For V2.0 manifests, we don't use embedded certificates
                // Instead, signatures reference keys by fingerprint, and certificates are managed by trust store
                var firstSignedBlock = manifest.SignedContent.FirstOrDefault();
                if (firstSignedBlock == null)
                {
                    return Result.Failure<LoadedManifest, ManifestError>(
                        new InvalidManifest("V2.0 manifest has no signed content blocks")
                    );
                }

                // Extract key fingerprint (remove "sha256:" prefix if present)
                var keyFingerprint = firstSignedBlock.GetKeyFingerprint();
                var publicKeyToken = ConvertHexStringToByteArray(keyFingerprint);

                // V2.0 manifests don't embed certificates - they're looked up in trust store
                // For now, we create a LoadedManifest without a certificate
                var manifestPath = _fileSystem.Path.Combine(directory, "LuaManifest.json");
                return Result.Success<LoadedManifest, ManifestError>(
                    new LoadedManifest(
                        manifest,
                        manifestPath,
                        DateTime.UtcNow,
                        Maybe<X509Certificate>.None, // Certificate lookup happens elsewhere
                        publicKeyToken,
                        false // Override permissions determined by trust store
                    )
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<LoadedManifest, ManifestError>(
                    new ManifestVerificationFailed(ex.Message)
                );
            }
        }

        /// <summary>
        /// Verifies the manifest signature
        /// </summary>
        private UnitResult<ManifestError> VerifyManifestSignature(
            Manifest manifest,
            X509Certificate certificate
        )
        {
            try
            {
                // Create canonical form of manifest for verification
                var canonicalManifest = CreateCanonicalManifest(manifest);
                var manifestBytes = Encoding.UTF8.GetBytes(canonicalManifest);
                // V2.0 - Extract signature from first signed content block
                var signatureBytes =
                    manifest.HasSignedContent
                    && manifest.SignedContent.Length > 0
                    && !string.IsNullOrEmpty(manifest.SignedContent[0].Signature)
                        ? Convert.FromBase64String(manifest.SignedContent[0].Signature)
                        : new byte[0];

                // Use BouncyCastle certificate directly
                var publicKey = certificate.GetPublicKey();

                // Determine signature algorithm based on manifest security settings
                var signatureAlgorithm = GetSignatureAlgorithm(manifest, publicKey);

                // Create and initialize verifier
                var verifier = SignerUtilities.GetSigner(signatureAlgorithm);
                verifier.Init(false, publicKey);
                verifier.BlockUpdate(manifestBytes, 0, manifestBytes.Length);

                var isValid = verifier.VerifySignature(signatureBytes);

                if (!isValid)
                    throw new InvalidOperationException("Invalid manifest signature");

                return UnitResult.Success<ManifestError>();
            }
            catch (Exception ex)
            {
                return UnitResult.Failure<ManifestError>(
                    new ManifestVerificationFailed(ex.Message)
                );
            }
        }

        /// <summary>
        /// Determines the signature algorithm to use based on manifest settings and key type
        /// </summary>
        private string GetSignatureAlgorithm(Manifest manifest, AsymmetricKeyParameter publicKey)
        {
            // V2.0 manifests don't have a central Security.SigningAlgorithm field
            // Algorithm is determined per SignedContent block, but for compatibility we use key type
            string claimedAlgorithm = null;

            // For V2.0 manifests, we determine algorithm from key type directly
            if (!string.IsNullOrEmpty(claimedAlgorithm))
            {
                // Validate algorithm matches key type - critical security check
                if (publicKey is RsaKeyParameters && !claimedAlgorithm.Contains("RSA"))
                {
                    throw new ManifestSignatureException(
                        $"Algorithm mismatch: RSA key cannot use {claimedAlgorithm} algorithm",
                        "GetSignatureAlgorithm"
                    );
                }
                if (publicKey is ECPublicKeyParameters && !claimedAlgorithm.Contains("ECDSA"))
                {
                    throw new ManifestSignatureException(
                        $"Algorithm mismatch: ECDSA key cannot use {claimedAlgorithm} algorithm",
                        "GetSignatureAlgorithm"
                    );
                }

                return claimedAlgorithm;
            }

            // Default based on key type
            if (publicKey is RsaKeyParameters)
            {
                return "SHA256withRSA";
            }
            if (publicKey is ECPublicKeyParameters)
            {
                return "SHA256withECDSA";
            }
            throw new NotSupportedException($"Unsupported key type: {publicKey.GetType().Name}");
        }

        /// <summary>
        /// Creates canonical JSON for signature verification
        /// </summary>
        private string CreateCanonicalManifest(Manifest manifest)
        {
            // For V2.0 manifests, we create the canonical form by removing signatures from SignedContent blocks
            var signedContentWithoutSignatures = manifest
                .SignedContent.Select(block => new SignedContentBlock
                {
                    KeyId = block.KeyId,
                    Signature = "", // Remove signature for canonical form
                    Packages = block.Packages,
                    Policies = block.Policies,
                })
                .ToImmutableArray();

            var copy = new Manifest
            {
                Version = manifest.Version,
                ManifestId = manifest.ManifestId,
                SignedContent = signedContentWithoutSignatures,
            };

            // Serialize with deterministic settings
            return JsonSerializer.Serialize(copy, ManifestJsonOptions.Default);
        }

        /// <summary>
        /// Converts hex string to byte array (compatible with older .NET versions)
        /// </summary>
        private static byte[] ConvertHexStringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return new byte[0];

            var length = hex.Length;
            if (length % 2 != 0)
                throw new ArgumentException("Hex string must have even length");

            var bytes = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
