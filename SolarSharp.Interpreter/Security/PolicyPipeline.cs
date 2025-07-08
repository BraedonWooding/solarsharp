using System;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Default implementation of the policy pipeline
    /// </summary>
    public class PolicyPipeline : IPolicyPipeline
    {
        private readonly IFileSystem _fileSystem;
        private readonly IManifestValidator _validator;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly JsonSerializerOptions _jsonOptions;

        public PolicyPipeline(
            IFileSystem fileSystem,
            IManifestValidator validator,
            ISignatureVerifier signatureVerifier,
            JsonSerializerOptions jsonOptions
        )
        {
            _fileSystem = fileSystem;
            _validator = validator;
            _signatureVerifier = signatureVerifier;
            _jsonOptions = jsonOptions;
        }

        public async Task<Result<PolicyStore, PipelineError>> ProcessDirectories(
            DirectorySet directories,
            ISignatureVerifier signatureVerifier,
            ManifestScanOptions options
        )
        {
            // Use provided verifier or fallback to injected one
            var verifier = signatureVerifier ?? _signatureVerifier;

            try
            {
                // DirectorySet → ManifestFiles
                var scanResult = await PolicyTransformers.ScanDirectories(
                    directories,
                    _fileSystem,
                    options
                );
                if (!scanResult.IsSuccess)
                    return Result.Failure<PolicyStore, PipelineError>(
                        new PipelineError($"Scan failed: {scanResult.Error.Message}")
                    );

                // ManifestFiles → Manifests
                var manifestsResult = await PolicyTransformers.ParseManifests(
                    scanResult.Value,
                    _fileSystem,
                    _jsonOptions
                );
                if (!manifestsResult.IsSuccess)
                    return Result.Failure<PolicyStore, PipelineError>(
                        new PipelineError(
                            $"Manifest parsing failed: {manifestsResult.Error.Message}"
                        )
                    );

                // Manifests → ValidatedManifests
                var validated = PolicyTransformers.ValidateManifests(
                    manifestsResult.Value,
                    _validator
                );

                // ValidatedManifests → VerifiedManifests
                var verified = await PolicyTransformers.VerifySignatures(validated, verifier);

                // VerifiedManifests → CompiledPolicies
                var compiled = PolicyTransformers.CompilePolicies(verified);

                // CompiledPolicies → UnionedPolicy
                var unionResult = PolicyTransformers.UnionPolicies(compiled);
                if (!unionResult.IsSuccess)
                    return Result.Failure<PolicyStore, PipelineError>(
                        new PipelineError($"Union failed: {unionResult.Error.Message}")
                    );

                // UnionedPolicy → PolicyStore
                var store = PolicyTransformers.CreateStore(unionResult.Value);

                return Result.Success<PolicyStore, PipelineError>(store);
            }
            catch (Exception ex)
            {
                return Result.Failure<PolicyStore, PipelineError>(
                    new PipelineError($"Pipeline failed: {ex.Message}")
                );
            }
        }

        public CompiledPolicyRules CreateCompiledRules(
            PolicyStore store,
            SignaturePolicies signaturePolicies,
            PathPolicies pathPolicies,
            SecurityPolicy fallbackPolicy
        )
        {
            return new CompiledPolicyRules(
                signaturePolicies?.Policies,
                pathPolicies?.Policies,
                null, // No manifest
                fallbackPolicy
            );
        }
    }
}
