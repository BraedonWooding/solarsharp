#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Validates signed content manifests and their individual blocks
    /// </summary>
    public class SignedContentValidator
    {
        public SignedContentValidator() { }

        /// <summary>
        /// Validates a complete signed content manifest
        /// </summary>
        public Result<ValidatedSignedContentManifest, ManifestValidationError> ValidateManifest(
            Manifest manifest,
            ITrustStore trustStore,
            string manifestPath
        )
        {
            if (manifest.SignedContent.IsEmpty)
            {
                return ManifestValidationError.InvalidFormat(
                    "Manifest must contain at least one signed content block",
                    manifestPath
                );
            }

            var validatedBlocks = new List<ValidatedSignedContentBlock>();
            var allPackageIds = new HashSet<string>();

            foreach (var block in manifest.SignedContent)
            {
                var blockResult = ValidateSignedContentBlock(block, trustStore, manifestPath);
                if (blockResult.IsFailure)
                {
                    return blockResult.Error;
                }

                var validatedBlock = blockResult.Value;
                validatedBlocks.Add(validatedBlock);

                // Check for package ID conflicts within the same block
                foreach (var packageId in block.Packages.Keys)
                {
                    if (allPackageIds.Contains(packageId))
                    {
                        return ManifestValidationError.InvalidFormat(
                            $"Package ID '{packageId}' is defined in multiple signed content blocks",
                            manifestPath
                        );
                    }
                    allPackageIds.Add(packageId);
                }

                // Validate policy package references are local to this block
                var policyValidation = ValidatePolicyReferences(block, manifestPath);
                if (policyValidation.IsFailure)
                {
                    return policyValidation.Error;
                }
            }

            return Result.Success<ValidatedSignedContentManifest, ManifestValidationError>(
                new ValidatedSignedContentManifest
                {
                    ManifestId = manifest.ManifestId,
                    Version = manifest.Version,
                    ValidatedBlocks = validatedBlocks.ToImmutableArray(),
                    ValidationTimestamp = DateTime.UtcNow,
                }
            );
        }

        /// <summary>
        /// Validates a single signed content block
        /// </summary>
        private Result<
            ValidatedSignedContentBlock,
            ManifestValidationError
        > ValidateSignedContentBlock(
            SignedContentBlock block,
            ITrustStore trustStore,
            string manifestPath
        )
        {
            // Validate key ID format
            if (string.IsNullOrEmpty(block.KeyId))
            {
                return ManifestValidationError.InvalidFormat(
                    "Signed content block must have a key-id",
                    manifestPath
                );
            }

            if (!block.KeyId.StartsWith("sha256:") || block.KeyId.Length != 71) // "sha256:" + 64 hex chars
            {
                return ManifestValidationError.InvalidFormat(
                    $"Invalid key-id format: {block.KeyId}. Expected sha256:HEXSTRING",
                    manifestPath
                );
            }

            // Validate signature presence
            if (string.IsNullOrEmpty(block.Signature))
            {
                return ManifestValidationError.InvalidSignature(
                    "Signed content block must have a signature",
                    manifestPath
                );
            }

            // Verify the signature
            var signatureResult = VerifyBlockSignature(block, trustStore, manifestPath);
            if (signatureResult.IsFailure)
            {
                return signatureResult.Error;
            }

            // Validate packages
            if (block.Packages.IsEmpty)
            {
                return ManifestValidationError.InvalidFormat(
                    "Signed content block must contain at least one package",
                    manifestPath
                );
            }

            foreach (var (packageId, package) in block.Packages)
            {
                var packageValidation = ValidatePackage(packageId, package, manifestPath);
                if (packageValidation.IsFailure)
                {
                    return packageValidation.Error;
                }
            }

            return Result.Success<ValidatedSignedContentBlock, ManifestValidationError>(
                new ValidatedSignedContentBlock
                {
                    KeyId = block.KeyId,
                    KeyFingerprint = block.KeyId.Substring(7), // Remove "sha256:" prefix
                    Packages = block.Packages,
                    Policies = block.Policies,
                    SignatureValid = signatureResult.Value.IsValid,
                    IsTrusted = signatureResult.Value.IsTrusted,
                    ValidationTimestamp = DateTime.UtcNow,
                }
            );
        }

        /// <summary>
        /// Verifies the cryptographic signature of a signed content block
        /// </summary>
        private Result<SignatureValidationResult, ManifestValidationError> VerifyBlockSignature(
            SignedContentBlock block,
            ITrustStore trustStore,
            string manifestPath
        )
        {
            try
            {
                // Get the content to verify (everything except key-id and signature)
                var signedContent = block.GetSignedContent();
                var contentJson = JsonSerializer.Serialize(
                    signedContent,
                    ManifestJsonOptions.Default
                );
                var canonicalContent = JsonCanonicalizer.Canonicalize(contentJson);

                // Verify signature using the unified service
                var publicKeyPem = trustStore.GetPublicKey(block.KeyId);
                if (publicKeyPem.IsFailure)
                {
                    return ManifestValidationError.UntrustedKey(
                        $"Public key not found in trust store: {block.KeyId}",
                        manifestPath
                    );
                }

                // Parse the PEM-encoded public key to get AsymmetricKeyParameter
                var parseResult = UnifiedSignatureVerificationService.ParsePublicKey(
                    publicKeyPem.Value
                );
                if (parseResult.IsFailure)
                {
                    return ManifestValidationError.InvalidSignature(
                        $"Failed to parse public key: {parseResult.Error}",
                        manifestPath
                    );
                }

                var verificationResult = UnifiedSignatureVerificationService.VerifyRsaSignature(
                    System.Text.Encoding.UTF8.GetBytes(canonicalContent),
                    Convert.FromBase64String(block.Signature),
                    parseResult.Value
                );

                if (verificationResult.IsFailure || !verificationResult.Value)
                {
                    return ManifestValidationError.InvalidSignature(
                        $"Signature verification failed for block with key {block.KeyId}",
                        manifestPath
                    );
                }

                var isTrusted = trustStore.IsTrustedKey(block.KeyId);

                return Result.Success<SignatureValidationResult, ManifestValidationError>(
                    new SignatureValidationResult
                    {
                        IsValid = true,
                        IsTrusted = isTrusted,
                        SignatureAlgorithm = "RSA-SHA256",
                        PublicKeyFingerprint = block.KeyId.Substring(7),
                        ValidationDuration = TimeSpan.FromMilliseconds(10), // Placeholder
                    }
                );
            }
            catch (Exception ex)
            {
                return ManifestValidationError.UnexpectedError(
                    $"Error verifying signature: {ex.Message}",
                    ex,
                    manifestPath
                );
            }
        }

        /// <summary>
        /// Validates a package definition
        /// </summary>
        private Result<bool, ManifestValidationError> ValidatePackage(
            string packageId,
            ManifestPackage package,
            string manifestPath
        )
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return ManifestValidationError.InvalidFormat(
                    "Package ID cannot be empty",
                    manifestPath
                );
            }

            if (package.Files.IsEmpty)
            {
                return ManifestValidationError.InvalidFormat(
                    $"Package '{packageId}' must contain at least one file",
                    manifestPath
                );
            }

            foreach (var (fileName, hash) in package.Files)
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return ManifestValidationError.InvalidFormat(
                        $"File name cannot be empty in package '{packageId}'",
                        manifestPath
                    );
                }

                if (string.IsNullOrEmpty(hash) || !hash.StartsWith("sha256:"))
                {
                    return ManifestValidationError.InvalidFormat(
                        $"Invalid file hash for '{fileName}' in package '{packageId}'. Expected sha256:HEXSTRING",
                        manifestPath
                    );
                }
            }

            // Validate package metadata
            if (string.IsNullOrEmpty(package.Metadata.Name))
            {
                return ManifestValidationError.InvalidFormat(
                    $"Package '{packageId}' must have a non-empty name",
                    manifestPath
                );
            }

            if (string.IsNullOrEmpty(package.Metadata.Version))
            {
                return ManifestValidationError.InvalidFormat(
                    $"Package '{packageId}' must have a non-empty version",
                    manifestPath
                );
            }

            return Result.Success<bool, ManifestValidationError>(true);
        }

        /// <summary>
        /// Validates that policy package references are local to the block
        /// </summary>
        private Result<bool, ManifestValidationError> ValidatePolicyReferences(
            SignedContentBlock block,
            string manifestPath
        )
        {
            var packageIds = block.Packages.Keys.ToHashSet();

            foreach (var policy in block.Policies)
            {
                foreach (var packageRef in policy.Packages)
                {
                    if (packageRef == "*")
                    {
                        continue; // Wildcard is always valid
                    }

                    if (!packageIds.Contains(packageRef))
                    {
                        return ManifestValidationError.InvalidFormat(
                            $"Policy references unknown package '{packageRef}'. Package references must be local to the signed content block.",
                            manifestPath
                        );
                    }
                }
            }

            return Result.Success<bool, ManifestValidationError>(true);
        }
    }

    /// <summary>
    /// A validated signed content manifest with verified signatures
    /// </summary>
    public sealed record ValidatedSignedContentManifest
    {
        public string ManifestId { get; init; } = "";
        public string Version { get; init; } = "";
        public ImmutableArray<ValidatedSignedContentBlock> ValidatedBlocks { get; init; } =
            ImmutableArray<ValidatedSignedContentBlock>.Empty;
        public DateTime ValidationTimestamp { get; init; }
    }

    /// <summary>
    /// A validated signed content block with verified signature
    /// </summary>
    public sealed record ValidatedSignedContentBlock
    {
        public string KeyId { get; init; } = "";
        public string KeyFingerprint { get; init; } = "";
        public ImmutableDictionary<string, ManifestPackage> Packages { get; init; } =
            ImmutableDictionary<string, ManifestPackage>.Empty;
        public ImmutableArray<ManifestPolicy> Policies { get; init; } =
            ImmutableArray<ManifestPolicy>.Empty;
        public bool SignatureValid { get; init; }
        public bool IsTrusted { get; init; }
        public DateTime ValidationTimestamp { get; init; }
    }
}
