using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Events;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Event-driven implementation of manifest validation
    /// </summary>
    public sealed class EventDrivenManifestValidator : IManifestValidationService
    {
        private readonly IFileSystem _fileSystem;
        private readonly IManifestDiscoveryService _discoveryService;
        private readonly ISignatureValidator _signatureValidator;
        private readonly List<ManifestValidationEvent> _events = new();

        public EventDrivenManifestValidator(
            IFileSystem fileSystem,
            IManifestDiscoveryService discoveryService,
            ISignatureValidator signatureValidator
        )
        {
            _fileSystem = fileSystem;
            _discoveryService = discoveryService;
            _signatureValidator = signatureValidator;
        }

        public IObservable<ManifestValidationEvent> ValidationEvents =>
            throw new NotImplementedException(
                "Reactive extensions not available - use GetValidationEvents() instead"
            );

        public IEnumerable<ManifestValidationEvent> GetValidationEvents() => _events.AsReadOnly();

        public Result<LoadedManifest, ManifestValidationError> ValidateManifest(
            string scriptPath,
            ITrustStore trustStore,
            string scriptId
        )
        {
            var stopwatch = Stopwatch.StartNew();

            return DiscoverManifest(scriptPath, scriptId)
                .Bind(manifestPath => LoadManifestFromPath(manifestPath, trustStore, scriptId))
                .Tap(loadedManifest =>
                    EmitValidationCompleted(
                        loadedManifest.Manifest,
                        stopwatch.Elapsed,
                        scriptId,
                        loadedManifest.ManifestPath
                    )
                )
                .MapError(error => error);
        }

        public Result<Manifest, ManifestValidationError> ValidateManifestFromJson(
            string manifestJson,
            string manifestPath,
            ITrustStore trustStore,
            string scriptId
        )
        {
            return ParseManifest(manifestJson, manifestPath)
                .Bind(manifest => ValidateManifestPermissions(manifest, manifestPath))
                .Bind(manifest =>
                    ValidateManifestSignature(manifest, trustStore, scriptId, manifestPath)
                        .Map(_ => manifest)
                );
        }

        public Result<SignatureValidationResult, ManifestValidationError> ValidateSignature(
            Manifest manifest,
            ITrustStore trustStore,
            string scriptId
        )
        {
            return ValidateManifestSignature(manifest, trustStore, scriptId, "");
        }

        private Result<string, ManifestValidationError> DiscoverManifest(
            string scriptPath,
            string scriptId
        )
        {
            EmitDiscoveryStarted(scriptPath, scriptId);

            return _discoveryService
                .DiscoverManifestPath(scriptPath)
                .ToResult(ManifestValidationError.NotFound(scriptPath, "DiscoverManifest"))
                .Tap(manifestPath => EmitManifestDiscovered(manifestPath, scriptId));
        }

        private Result<LoadedManifest, ManifestValidationError> LoadManifestFromPath(
            string manifestPath,
            ITrustStore trustStore,
            string scriptId
        )
        {
            try
            {
                if (!_fileSystem.File.Exists(manifestPath))
                {
                    return Result.Failure<LoadedManifest, ManifestValidationError>(
                        ManifestValidationError.NotFound(manifestPath, "LoadManifestFromPath")
                    );
                }

                var content = _fileSystem.File.ReadAllText(manifestPath);
                return ParseManifest(content, manifestPath)
                    .Bind(manifest => ValidateManifestPermissions(manifest, manifestPath))
                    .Bind(manifest =>
                        ValidateManifestSignature(manifest, trustStore, scriptId, manifestPath)
                            .Map(validationResult =>
                                LoadedManifest.FromValidationResult(
                                    manifest,
                                    manifestPath,
                                    DateTime.UtcNow,
                                    validationResult.PublicKeyFingerprint,
                                    validationResult.IsValid
                                )
                            )
                    );
            }
            catch (PathTraversalException)
            {
                // Re-throw path traversal exceptions as-is
                throw;
            }
            catch (ManifestFormatException)
            {
                // Re-throw manifest format exceptions as-is
                throw;
            }
            catch (Exception ex)
            {
                return Result.Failure<LoadedManifest, ManifestValidationError>(
                    ManifestValidationError.UnexpectedError(
                        ex.Message,
                        ex,
                        manifestPath,
                        "LoadManifestFromPath"
                    )
                );
            }
        }

        private Result<Manifest, ManifestValidationError> ParseManifest(
            string manifestJson,
            string manifestPath
        )
        {
            try
            {
                // No includes supported - each manifest must be self-contained
                // Check for includes field and reject if present
                if (manifestJson.Contains("\"includes\""))
                {
                    return Result.Failure<Manifest, ManifestValidationError>(
                        ManifestValidationError.InvalidFormat(
                            "Manifests with includes are not supported. Manifests must be self-contained.",
                            manifestPath
                        )
                    );
                }

                var manifest = JsonSerializer.Deserialize<Manifest>(
                    manifestJson,
                    ManifestJsonOptions.Default
                );
                if (manifest == null)
                    return Result.Failure<Manifest, ManifestValidationError>(
                        ManifestValidationError.InvalidFormat(
                            "Manifest deserialization returned null",
                            manifestPath
                        )
                    );

                // Check manifest version - support both V1.0 and V2.0
                if (manifest.Version != "1.0" && manifest.Version != "2.0")
                {
                    return Result.Failure<Manifest, ManifestValidationError>(
                        ManifestValidationError.InvalidFormat(
                            $"Unsupported manifest version: {manifest.Version}. Only versions 1.0 and 2.0 are supported.",
                            manifestPath
                        )
                    );
                }

                return Result.Success<Manifest, ManifestValidationError>(manifest);
            }
            catch (PathTraversalException)
            {
                // Re-throw path traversal exceptions as-is
                throw;
            }
            catch (ManifestFormatException)
            {
                // Re-throw manifest format exceptions as-is
                throw;
            }
            catch (JsonException ex)
            {
                // Check if the error is related to signature/security fields
                if (
                    manifestJson.Contains("\"security\":")
                    && (
                        ex.Message.Contains("signature")
                        || ex.Message.Contains("algorithm")
                        || ex.Message.Contains("SHA256withRSA")
                        || ex.Message.Contains("SignatureType")
                    )
                )
                {
                    return Result.Failure<Manifest, ManifestValidationError>(
                        ManifestValidationError.InvalidSignature(
                            $"Invalid signature format: {ex.Message}",
                            manifestPath
                        )
                    );
                }

                // Check if error is related to invalid capabilities
                if (
                    ex.Message.Contains("CommandExecution")
                    || ex.Message.Contains("ScriptCapabilities")
                )
                {
                    // Check if manifest has a signature section to determine error type
                    if (
                        manifestJson.Contains("\"signature\":")
                        && manifestJson.Contains("\"security\":")
                    )
                    {
                        return Result.Failure<Manifest, ManifestValidationError>(
                            ManifestValidationError.InvalidSignature(
                                $"Invalid capabilities in signed manifest: {ex.Message}",
                                manifestPath
                            )
                        );
                    }

                    // Unsigned manifest with invalid capabilities is a format error
                    return Result.Failure<Manifest, ManifestValidationError>(
                        ManifestValidationError.InvalidFormat(
                            $"Invalid capabilities: {ex.Message}",
                            manifestPath
                        )
                    );
                }

                return Result.Failure<Manifest, ManifestValidationError>(
                    ManifestValidationError.InvalidFormat(
                        $"Invalid JSON format: {ex.Message}",
                        manifestPath
                    )
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<Manifest, ManifestValidationError>(
                    ManifestValidationError.UnexpectedError(
                        $"Unexpected error parsing manifest: {ex.Message}",
                        ex,
                        manifestPath
                    )
                );
            }
        }

        internal Result<Manifest, ManifestValidationError> ValidateManifestPermissions(
            Manifest manifest,
            string manifestPath
        )
        {
            // Get the manifest's directory
            var manifestDirectory = _fileSystem.Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrEmpty(manifestDirectory))
            {
                return Result.Failure<Manifest, ManifestValidationError>(
                    ManifestValidationError.InvalidFormat(
                        "Cannot determine manifest directory",
                        manifestPath
                    )
                );
            }

            var normalizedBaseDir = NormalizePath(manifestDirectory);

            // V2.0: Validate file permissions from signed content blocks
            foreach (var block in manifest.SignedContent)
            {
                foreach (var policy in block.Policies)
                {
                    // Validate path restrictions are scoped to manifest directory
                    if (policy.Paths != null && policy.Paths.Patterns.Length > 0)
                    {
                        foreach (var pattern in policy.Paths.Patterns)
                        {
                            if (!IsPatternScopedToDirectory(pattern, normalizedBaseDir))
                            {
                                return Result.Failure<Manifest, ManifestValidationError>(
                                    ManifestValidationError.PermissionScopeViolation(
                                        $"Path pattern '{pattern}' references location outside manifest directory",
                                        manifestPath
                                    )
                                );
                            }
                        }
                    }
                }
            }

            return Result.Success<Manifest, ManifestValidationError>(manifest);
        }

        private bool IsPatternScopedToDirectory(string pattern, string baseDirectory)
        {
            // Normalize the pattern
            var normalizedPattern = NormalizePath(pattern);

            // Check for parent directory traversal
            if (pattern.Contains(".."))
            {
                // Any pattern with .. is trying to escape the directory
                return false;
            }

            // Absolute paths are not allowed - manifests can only reference relative paths
            if (IsAbsolutePath(pattern))
            {
                return false;
            }

            // Relative paths without .. are automatically scoped (will be resolved relative to manifest directory)
            return true;
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // Replace backslashes with forward slashes
            path = path.Replace('\\', '/');

            // Ensure absolute paths start with /
            if (!path.StartsWith("/") && !path.StartsWith("./") && !path.StartsWith("../"))
            {
                // For Windows absolute paths like C:\, convert to /C/
                if (path.Length >= 2 && path[1] == ':')
                {
                    path = "/" + path[0] + path.Substring(2);
                }
            }

            // Remove trailing slashes except for root
            if (path.Length > 1 && path.EndsWith("/"))
                path = path.TrimEnd('/');

            return path;
        }

        private bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Unix-style absolute paths
            if (path.StartsWith("/"))
                return true;

            // Windows-style absolute paths (C:\, D:/, etc.)
            if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
                return true;

            return false;
        }

        private Result<
            SignatureValidationResult,
            ManifestValidationError
        > ValidateManifestSignature(
            Manifest manifest,
            ITrustStore trustStore,
            string scriptId,
            string manifestPath
        )
        {
            var stopwatch = Stopwatch.StartNew();

            if (!manifest.IsSigned())
            {
                // Unsigned manifests are always allowed and will use fallback policies
                // This allows untrusted manifests as long as they have no signature at all
                return Result.Success<SignatureValidationResult, ManifestValidationError>(
                    new SignatureValidationResult
                    {
                        IsValid = true,
                        IsTrusted = false, // Unsigned manifests are not trusted, will use fallback policy
                        SignatureAlgorithm = "None",
                        PublicKeyFingerprint = "",
                        ValidationDuration = stopwatch.Elapsed,
                    }
                );
            }

            // Manifest is signed - ALWAYS validate against trust store
            // Security requirement: If there is a signature on a manifest, and we don't have the key in trust, reject it always

            // Validate signature cryptographically FIRST
            return _signatureValidator
                .ValidateSignature(manifest, manifestPath)
                .MapError(error =>
                    ManifestValidationError.InvalidSignature(
                        error,
                        manifestPath,
                        "ValidateManifestSignature"
                    )
                )
                .Bind(cryptoResult =>
                    ValidateTrust(cryptoResult, trustStore, scriptId, manifestPath)
                )
                .Tap(result => EmitSignatureValidated(result, scriptId, manifestPath))
                .TapError(error => EmitSignatureValidationFailed(error, scriptId, manifestPath));
        }

        private Result<SignatureValidationResult, ManifestValidationError> ValidateTrust(
            CryptographicValidationResult cryptoResult,
            ITrustStore trustStore,
            string scriptId,
            string manifestPath
        )
        {
            EmitTrustValidationStarted(
                cryptoResult.PublicKeyFingerprint,
                trustStore,
                scriptId,
                manifestPath
            );

            if (!cryptoResult.IsValid)
            {
                return Result.Failure<SignatureValidationResult, ManifestValidationError>(
                    ManifestValidationError.InvalidSignature(
                        "Cryptographic signature validation failed",
                        manifestPath,
                        "ValidateTrust"
                    )
                );
            }

            var isTrusted = trustStore.IsTrustedKey(cryptoResult.PublicKeyFingerprint);
            if (!isTrusted)
            {
                EmitTrustValidationFailed(
                    cryptoResult.PublicKeyFingerprint,
                    trustStore,
                    scriptId,
                    manifestPath
                );
                return Result.Failure<SignatureValidationResult, ManifestValidationError>(
                    ManifestValidationError.UntrustedKey(
                        $"Manifest is signed with an untrusted key. Key fingerprint: {cryptoResult.PublicKeyFingerprint}",
                        manifestPath,
                        "ValidateTrust"
                    )
                );
            }

            // Perform PIV validation on the trusted key
            if (trustStore is ScriptTrustStore scriptTrustStore)
            {
                var publicKey = scriptTrustStore.GetPublicKeyByFingerprint(
                    cryptoResult.PublicKeyFingerprint
                );
                if (publicKey != null)
                {
                    try
                    {
                        // Validate PIV compliance
                        var pivValidationResult = ValidatePivComplianceForKey(publicKey);
                        if (pivValidationResult.IsFailure)
                        {
                            return Result.Failure<
                                SignatureValidationResult,
                                ManifestValidationError
                            >(
                                ManifestValidationError.InvalidSignature(
                                    $"PIV compliance validation failed: {pivValidationResult.Error}",
                                    manifestPath,
                                    "ValidateTrust"
                                )
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        return Result.Failure<SignatureValidationResult, ManifestValidationError>(
                            ManifestValidationError.InvalidSignature(
                                $"PIV compliance validation error: {ex.Message}",
                                manifestPath,
                                "ValidateTrust"
                            )
                        );
                    }
                }
            }

            return Result.Success<SignatureValidationResult, ManifestValidationError>(
                new SignatureValidationResult
                {
                    IsValid = true,
                    IsTrusted = true,
                    SignatureAlgorithm = cryptoResult.SignatureAlgorithm,
                    PublicKeyFingerprint = cryptoResult.PublicKeyFingerprint,
                    ValidationDuration = cryptoResult.ValidationDuration,
                }
            );
        }

        private void EmitDiscoveryStarted(string scriptPath, string scriptId)
        {
            _events.Add(
                new ManifestDiscoveryStarted
                {
                    ScriptId = scriptId,
                    ScriptPath = scriptPath,
                    SearchRoot = Path.GetDirectoryName(scriptPath) ?? "",
                }
            );
        }

        private void EmitManifestDiscovered(string manifestPath, string scriptId)
        {
            string content;
            try
            {
                content = _fileSystem.File.Exists(manifestPath)
                    ? _fileSystem.File.ReadAllText(manifestPath)
                    : "";
            }
            catch
            {
                content = "";
            }
            var isSigned = content.Contains("\"signature\"");

            _events.Add(
                new ManifestDiscovered
                {
                    ScriptId = scriptId,
                    ManifestPath = manifestPath,
                    ManifestContent = content,
                    IsSigned = isSigned,
                }
            );
        }

        private void EmitTrustValidationStarted(
            string publicKeyFingerprint,
            ITrustStore trustStore,
            string scriptId,
            string manifestPath
        )
        {
            _events.Add(
                new ManifestTrustValidationStarted
                {
                    ScriptId = scriptId,
                    ManifestPath = manifestPath,
                    PublicKeyFingerprint = publicKeyFingerprint,
                    TrustedKeyFingerprints = trustStore.TrustedKeyFingerprints.ToImmutableArray(),
                }
            );
        }

        private void EmitTrustValidationFailed(
            string publicKeyFingerprint,
            ITrustStore trustStore,
            string scriptId,
            string manifestPath
        )
        {
            _events.Add(
                new ManifestTrustValidationFailed
                {
                    ScriptId = scriptId,
                    ManifestPath = manifestPath,
                    PublicKeyFingerprint = publicKeyFingerprint,
                    TrustedKeyFingerprints = trustStore.TrustedKeyFingerprints.ToImmutableArray(),
                    Reason = "Key is not in trust store",
                }
            );
        }

        private void EmitSignatureValidated(
            SignatureValidationResult result,
            string scriptId,
            string manifestPath
        )
        {
            _events.Add(
                new ManifestSignatureValidated
                {
                    ScriptId = scriptId,
                    ManifestPath = manifestPath,
                    SignatureAlgorithm = result.SignatureAlgorithm,
                    PublicKeyFingerprint = result.PublicKeyFingerprint,
                    IsTrustedKey = result.IsTrusted,
                }
            );
        }

        private void EmitSignatureValidationFailed(
            ManifestValidationError error,
            string scriptId,
            string manifestPath
        )
        {
            _events.Add(
                new ManifestSignatureValidationFailed
                {
                    ScriptId = scriptId,
                    ManifestPath = manifestPath,
                    Reason = error.Message,
                    FailureType = error.Type switch
                    {
                        ManifestValidationErrorType.InvalidSignature =>
                            ManifestValidationFailureType.InvalidSignature,
                        ManifestValidationErrorType.UntrustedKey =>
                            ManifestValidationFailureType.UntrustedKey,
                        ManifestValidationErrorType.UnsupportedAlgorithm =>
                            ManifestValidationFailureType.UnsupportedAlgorithm,
                        _ => ManifestValidationFailureType.InvalidSignature,
                    },
                }
            );
        }

        private void EmitValidationCompleted(
            Manifest manifest,
            TimeSpan duration,
            string scriptId,
            string manifestPath
        )
        {
            _events.Add(
                new ManifestValidationCompleted
                {
                    ScriptId = scriptId,
                    ManifestPath = manifestPath,
                    ValidatedManifest = manifest,
                    ValidationDuration = duration,
                }
            );
        }

        /// <summary>
        /// Validates PIV compliance for the given public key
        /// </summary>
        private Result<string> ValidatePivComplianceForKey(AsymmetricKeyParameter publicKey)
        {
            try
            {
                // Convert AsymmetricKeyParameter to BouncyCastleAsymmetricAlgorithm wrapper
                BouncyCastleAsymmetricAlgorithm algorithm;
                switch (publicKey)
                {
                    case RsaKeyParameters rsaKey:
                        algorithm = new BouncyCastleRsa(rsaKey);
                        break;
                    case ECPublicKeyParameters ecKey:
                        algorithm = new BouncyCastleEcdsa(ecKey);
                        break;
                    default:
                        return Result.Failure<string>(
                            $"Unsupported key type for PIV validation: {publicKey.GetType().Name}"
                        );
                }

                // Create ParsedKey object and validate PIV compliance
                var keySize = algorithm.KeySize;
                var fingerprint = CryptoKeyManager.ComputeFingerprint(algorithm);
                var parsedKey = new CryptoKeyManager.ParsedKey(algorithm, keySize, fingerprint);

                return CryptoKeyManager.ValidatePivCompliance(parsedKey);
            }
            catch (Exception ex)
            {
                return Result.Failure<string>($"PIV compliance validation error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result of cryptographic signature validation
    /// </summary>
    public sealed record CryptographicValidationResult
    {
        public bool IsValid { get; init; }
        public string SignatureAlgorithm { get; init; } = "";
        public string PublicKeyFingerprint { get; init; } = "";
        public TimeSpan ValidationDuration { get; init; }
    }

    /// <summary>
    /// Service for discovering manifests in the file system
    /// </summary>
    public interface IManifestDiscoveryService
    {
        Maybe<string> DiscoverManifestPath(string scriptPath);
    }

    /// <summary>
    /// Service for cryptographic signature validation
    /// </summary>
    public interface ISignatureValidator
    {
        Result<CryptographicValidationResult, string> ValidateSignature(
            Manifest manifest,
            string manifestPath
        );
    }
}
