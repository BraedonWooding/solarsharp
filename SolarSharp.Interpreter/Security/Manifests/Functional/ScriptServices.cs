using System;
using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests.Functional
{
    /// <summary>
    /// Immutable container for all script execution services.
    /// Uses dependency injection pattern with pure functional composition.
    /// </summary>
    public record ScriptServices(
        IManifestValidationService ManifestValidator,
        IManifestCache ManifestCache,
        ITrustStore TrustStore,
        BasePolicySet BasePolicySet,
        IFileSystem FileSystem)
    {
        /// <summary>
        /// Create default services for a BasePolicySet.
        /// Pure factory method - no side effects.
        /// </summary>
        public static ScriptServices Create(BasePolicySet basePolicySet) => Create(basePolicySet, enableCaching: true);

        /// <summary>
        /// Create services with caching control.
        /// Pure factory method - no side effects.
        /// </summary>
        public static ScriptServices Create(BasePolicySet basePolicySet, bool enableCaching)
        {
            var fileSystem = new FileSystem();
            var discoveryService = ManifestDiscovery.CreateDefault();
            var signatureValidator = new DefaultSignatureValidator();
            var trustStore = ScriptTrustStore.Empty;
            
            var manifestValidator = new EventDrivenManifestValidator(
                fileSystem,
                discoveryService,
                signatureValidator);

            var manifestCache = enableCaching
                ? (IManifestCache)new ManifestCache()
                : NoOpManifestCache.Instance;

            // Wrap validator with caching if enabled
            var cachedValidator = enableCaching
                ? manifestValidator.WithCaching(manifestCache)
                : manifestValidator;

            return new ScriptServices(
                ManifestValidator: cachedValidator,
                ManifestCache: manifestCache,
                TrustStore: trustStore,
                BasePolicySet: basePolicySet,
                FileSystem: fileSystem);
        }

        /// <summary>
        /// Create services with custom trust store.
        /// </summary>
        public ScriptServices WithTrustStore(ITrustStore trustStore) =>
            this with { TrustStore = trustStore };

        /// <summary>
        /// Create services with custom manifest cache.
        /// </summary>
        public ScriptServices WithCache(IManifestCache cache) =>
            this with { ManifestCache = cache };

        /// <summary>
        /// Create services with custom file system.
        /// </summary>
        public ScriptServices WithFileSystem(IFileSystem fileSystem) =>
            this with { FileSystem = fileSystem };
    }

    /// <summary>
    /// Pure functional execution pipeline.
    /// Transforms: (filename, globalContext, services) → DynValue
    /// All operations are pure functions until the final execution step.
    /// </summary>
    public static class ExecutionPipeline
    {
        /// <summary>
        /// Execute a file through the complete pipeline.
        /// Pure functional pipeline until the final execution step.
        /// </summary>
        public static Result<DynValue, FunctionalExecutionError> ExecuteFile(
            string filename,
            Table globalContext,
            ScriptServices services,
            Script script)
        {
            var contextBuilder = ExecutionContext.ForFile(filename);
            var manifestResult = LoadManifestIntoContext(contextBuilder, services);
            if (manifestResult.IsFailure) return Result.Failure<DynValue, FunctionalExecutionError>(manifestResult.Error);
            
            var contextResult = BuildExecutionContext(manifestResult.Value);
            if (contextResult.IsFailure) return Result.Failure<DynValue, FunctionalExecutionError>(contextResult.Error);
            
            var policyResult = ResolvePolicyForContext(contextResult.Value, services);
            if (policyResult.IsFailure) return Result.Failure<DynValue, FunctionalExecutionError>(policyResult.Error);
            
            return ExecuteWithPolicy(policyResult.Value.Context, policyResult.Value.Policy, globalContext, script);
        }

        /// <summary>
        /// Execute eval code through the pipeline.
        /// </summary>
        public static Result<DynValue, FunctionalExecutionError> ExecuteEval(
            string code,
            LuaExecutionContext parentContext,
            Table globalContext,
            ScriptServices services,
            Script script)
        {
            var contextBuilder = ExecutionContext.ForEval(code, parentContext);
            var contextResult = BuildExecutionContext(contextBuilder);
            if (contextResult.IsFailure) return Result.Failure<DynValue, FunctionalExecutionError>(contextResult.Error);
            
            var policyResult = ResolvePolicyForContext(contextResult.Value, services);
            if (policyResult.IsFailure) return Result.Failure<DynValue, FunctionalExecutionError>(policyResult.Error);
            
            return ExecuteEvalWithPolicy(policyResult.Value.Context, policyResult.Value.Policy, code, globalContext, script);
        }

        /// <summary>
        /// Load manifest information into execution context builder.
        /// Pure function - no side effects beyond caching.
        /// </summary>
        private static Result<ExecutionContextBuilder, FunctionalExecutionError> LoadManifestIntoContext(
            ExecutionContextBuilder contextBuilder,
            ScriptServices services)
        {
            try
            {
                // Try to load manifest for the file
                var manifestResult = services.ManifestValidator.ValidateManifest(
                    contextBuilder.SourceFile,
                    services.TrustStore,
                    Guid.NewGuid().ToString());

                return manifestResult.Match(
                    // Manifest found and valid - add to context
                    loadedManifest => Result.Success<ExecutionContextBuilder, FunctionalExecutionError>(
                        contextBuilder.WithManifest(loadedManifest)),
                    // Manifest not found or invalid - continue without manifest
                    error => error.Type == ManifestValidationErrorType.NotFound
                        ? Result.Success<ExecutionContextBuilder, FunctionalExecutionError>(contextBuilder)
                        : Result.Failure<ExecutionContextBuilder, FunctionalExecutionError>(
                            new FunctionalExecutionError($"Manifest validation failed: {error.Message}", 
                                ConvertManifestValidationError(error))));
            }
            catch (Exception ex)
            {
                return Result.Failure<ExecutionContextBuilder, FunctionalExecutionError>(
                    new FunctionalExecutionError($"Failed to load manifest: {ex.Message}"));
            }
        }

        /// <summary>
        /// Build the final execution context.
        /// Pure function - no side effects.
        /// </summary>
        private static Result<LuaExecutionContext, FunctionalExecutionError> BuildExecutionContext(
            ExecutionContextBuilder contextBuilder)
        {
            return contextBuilder.Build()
                .MapError(error => new FunctionalExecutionError(error.Message));
        }

        /// <summary>
        /// Resolve security policy for execution context.
        /// Pure function - no side effects.
        /// </summary>
        private static Result<PolicyResolutionResult, FunctionalExecutionError> ResolvePolicyForContext(
            LuaExecutionContext context,
            ScriptServices services)
        {
            var policyResult = PolicyResolution.ResolveForFile(
                context.SourceFile,
                services.BasePolicySet,
                services.TrustStore,
                services.ManifestValidator,
                services.ManifestCache);

            return policyResult.Match(
                policy => Result.Success<PolicyResolutionResult, FunctionalExecutionError>(
                    new PolicyResolutionResult(context, policy)),
                error => {
                    // Check if we have the original manifest validation error and convert to appropriate exception
                    var originalException = error.OriginalError != null 
                        ? ConvertManifestValidationError(error.OriginalError)
                        : ExtractManifestException(error.Message);
                    
                    return Result.Failure<PolicyResolutionResult, FunctionalExecutionError>(
                        new FunctionalExecutionError($"Policy resolution failed: {error.Message}", originalException));
                });
        }

        /// <summary>
        /// Execute file with resolved policy.
        /// This is where side effects happen - actual script execution.
        /// </summary>
        private static Result<DynValue, FunctionalExecutionError> ExecuteWithPolicy(
            LuaExecutionContext context,
            SecurityPolicy policy,
            Table globalContext,
            Script script)
        {
            try
            {
                // Apply the security policy to the script
                script.ApplySecurityPolicy(policy);

                // Load and execute the file
                var result = script.LoadFileInternal(
                    context.SourceFile,
                    globalContext,
                    friendlyFilename: null,
                    skipPolicyResolution: true); // We already resolved policy

                var executionResult = script.Call(result);
                return Result.Success<DynValue, FunctionalExecutionError>(executionResult);
            }
            catch (Exception ex)
            {
                return Result.Failure<DynValue, FunctionalExecutionError>(
                    new FunctionalExecutionError($"Execution failed: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Execute eval code with resolved policy.
        /// </summary>
        private static Result<DynValue, FunctionalExecutionError> ExecuteEvalWithPolicy(
            LuaExecutionContext context,
            SecurityPolicy policy,
            string code,
            Table globalContext,
            Script script)
        {
            try
            {
                // Apply the security policy to the script
                script.ApplySecurityPolicy(policy);

                // Load and execute the string
                var result = script.LoadStringInternal(code, globalContext, context.SourceFile);
                var executionResult = script.Call(result);
                
                return Result.Success<DynValue, FunctionalExecutionError>(executionResult);
            }
            catch (Exception ex)
            {
                return Result.Failure<DynValue, FunctionalExecutionError>(
                    new FunctionalExecutionError($"Eval execution failed: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Convert ManifestValidationError to appropriate exception type.
        /// </summary>
        private static Exception ConvertManifestValidationError(ManifestValidationError error)
        {
            return error.Type switch
            {
                ManifestValidationErrorType.UntrustedKey => new ManifestSignatureException(error.Message, "ManifestValidation"),
                ManifestValidationErrorType.InvalidSignature => new ManifestSignatureException(error.Message, "ManifestValidation"),
                ManifestValidationErrorType.InvalidFormat => new ManifestFormatException(error.Message, "ManifestValidation"),
                ManifestValidationErrorType.UnsupportedAlgorithm => new ManifestSignatureException(error.Message, "ManifestValidation"),
                ManifestValidationErrorType.CertificateValidationFailed => new ManifestSignatureException(error.Message, "ManifestValidation"),
                ManifestValidationErrorType.PermissionScopeViolation => new ManifestFormatException(error.Message, "ManifestValidation"),
                _ => new ManifestFormatException(error.Message, "ManifestValidation")
            };
        }

        /// <summary>
        /// Extract the appropriate manifest exception from error message (fallback).
        /// </summary>
        private static Exception ExtractManifestException(string errorMessage)
        {
            // Check for specific manifest validation error patterns
            if (errorMessage.Contains("Manifest is signed with an untrusted key") || 
                errorMessage.Contains("UntrustedKey"))
            {
                return new ManifestSignatureException(errorMessage, "ManifestValidation");
            }
            
            if (errorMessage.Contains("Invalid signature") ||
                errorMessage.Contains("signature validation failed") ||
                errorMessage.Contains("InvalidSignature"))
            {
                return new ManifestSignatureException(errorMessage, "ManifestValidation");
            }
            
            if (errorMessage.Contains("Invalid JSON format") ||
                errorMessage.Contains("JSON") ||
                errorMessage.Contains("format"))
            {
                return new ManifestFormatException(errorMessage, "ManifestValidation");
            }
            
            // Default to generic manifest format exception
            return new ManifestFormatException(errorMessage, "ManifestValidation");
        }
    }

    /// <summary>
    /// Result of policy resolution with context.
    /// </summary>
    public record PolicyResolutionResult(LuaExecutionContext Context, SecurityPolicy Policy);

    /// <summary>
    /// Functional execution error.
    /// </summary>
    public record FunctionalExecutionError(string Message, Exception OriginalException = null);

    /// <summary>
    /// Extension methods for functional composition.
    /// </summary>
    public static class PipelineExtensions
    {

        /// <summary>
        /// Convert execution context error to execution error.
        /// </summary>
        public static FunctionalExecutionError ToFunctionalExecutionError(this ExecutionContextError error) =>
            new FunctionalExecutionError(error.Message);
    }
}