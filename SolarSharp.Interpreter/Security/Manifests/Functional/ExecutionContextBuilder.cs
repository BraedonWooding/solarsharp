using System;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests.Domain;

namespace SolarSharp.Interpreter.Security.Manifests.Functional
{
    /// <summary>
    /// Fluent, immutable builder for execution contexts.
    /// Pure functional approach with no mutable state.
    /// </summary>
    public static class ExecutionContext
    {
        /// <summary>
        /// Start building an execution context for a file.
        /// </summary>
        public static ExecutionContextBuilder ForFile(string filePath) => 
            new ExecutionContextBuilder().WithSourceFile(filePath);
        
        /// <summary>
        /// Start building an execution context for eval.
        /// </summary>
        public static ExecutionContextBuilder ForEval(string code, LuaExecutionContext parent) =>
            new ExecutionContextBuilder()
                .WithSourceCode(code)
                .WithParent(parent)
                .WithSourceFile($"{parent.SourceFile}:eval");
    }

    /// <summary>
    /// Immutable builder for LuaExecutionContext.
    /// All operations return new instances - no mutation.
    /// </summary>
    public record ExecutionContextBuilder(
        string SourceFile = "",
        string SourceCode = "",
        Maybe<ScriptIdentity> Identity = default,
        Maybe<Manifest> Manifest = default,
        Maybe<LuaExecutionContext> Parent = default,
        Maybe<string> SigningKeyFingerprint = default)
    {
        /// <summary>
        /// Set the source file path.
        /// </summary>
        public ExecutionContextBuilder WithSourceFile(string sourceFile) =>
            this with { SourceFile = sourceFile };

        /// <summary>
        /// Set the source code (for eval contexts).
        /// </summary>
        public ExecutionContextBuilder WithSourceCode(string sourceCode) =>
            this with { SourceCode = sourceCode };

        /// <summary>
        /// Add manifest information from a loaded manifest.
        /// </summary>
        public ExecutionContextBuilder WithManifest(LoadedManifest loadedManifest) =>
            this with 
            { 
                Manifest = Maybe<Manifest>.From(loadedManifest.Manifest),
                SigningKeyFingerprint = Maybe<string>.From(loadedManifest.PublicKeyFingerprint)
            };

        /// <summary>
        /// Add manifest directly.
        /// </summary>
        public ExecutionContextBuilder WithManifest(Manifest manifest) =>
            this with { Manifest = Maybe<Manifest>.From(manifest) };

        /// <summary>
        /// Add script identity.
        /// </summary>
        public ExecutionContextBuilder WithIdentity(ScriptIdentity identity) =>
            this with { Identity = Maybe<ScriptIdentity>.From(identity) };

        /// <summary>
        /// Add parent execution context.
        /// </summary>
        public ExecutionContextBuilder WithParent(LuaExecutionContext parent) =>
            this with { Parent = Maybe<LuaExecutionContext>.From(parent) };

        /// <summary>
        /// Add signing key fingerprint.
        /// </summary>
        public ExecutionContextBuilder WithSigningKeyFingerprint(string fingerprint) =>
            this with { SigningKeyFingerprint = Maybe<string>.From(fingerprint) };

        /// <summary>
        /// Build the final LuaExecutionContext.
        /// Pure function - no side effects.
        /// </summary>
        public Result<LuaExecutionContext, ExecutionContextError> Build()
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(SourceFile))
            {
                return Result.Failure<LuaExecutionContext, ExecutionContextError>(
                    new ExecutionContextError("SourceFile is required"));
            }

            try
            {
                // Use factory method to create execution context
                var contextResult = Manifest.HasValue
                    ? LuaExecutionContext.CreateWithManifest(
                        SourceFile,
                        Manifest.Value,
                        Identity.GetValueOrDefault(),
                        Parent,
                        SigningKeyFingerprint)
                    : LuaExecutionContext.CreateFromPath(SourceFile, Parent);

                return contextResult.MapError(execError => new ExecutionContextError(execError.Message));
            }
            catch (Exception ex)
            {
                return Result.Failure<LuaExecutionContext, ExecutionContextError>(
                    new ExecutionContextError($"Failed to build execution context: {ex.Message}"));
            }
        }

        /// <summary>
        /// Build the context or throw on error (for contexts that must succeed).
        /// </summary>
        public LuaExecutionContext BuildOrThrow()
        {
            return Build().Match(
                success => success,
                error => throw new InvalidOperationException(
                    $"Failed to build execution context: {error.Message}"));
        }
    }

    /// <summary>
    /// Error in execution context building.
    /// </summary>
    public record ExecutionContextError(string Message);

    /// <summary>
    /// Extension methods for working with execution contexts functionally.
    /// </summary>
    public static class ExecutionContextExtensions
    {

        /// <summary>
        /// Create a child eval context from a parent context.
        /// </summary>
        public static ExecutionContextBuilder CreateEvalChild(
            this LuaExecutionContext parent,
            string evalCode) =>
            ExecutionContext.ForEval(evalCode, parent);

        /// <summary>
        /// Check if execution context has a manifest.
        /// </summary>
        public static bool HasManifest(this LuaExecutionContext context) =>
            context.Manifest.HasValue;

        /// <summary>
        /// Check if execution context is signed.
        /// </summary>
        public static bool IsSigned(this LuaExecutionContext context) =>
            context.Identity.HasValue && 
            context.Identity.Value.PublicKeyToken != null &&
            context.Identity.Value.PublicKeyToken.Length > 0;

        /// <summary>
        /// Get the manifest or throw if not present.
        /// </summary>
        public static Manifest GetManifestOrThrow(this LuaExecutionContext context) =>
            context.Manifest.Match(
                manifest => manifest,
                () => throw new InvalidOperationException("Execution context has no manifest"));

        /// <summary>
        /// Try to get the manifest.
        /// </summary>
        public static Maybe<Manifest> TryGetManifest(this LuaExecutionContext context) =>
            context.Manifest;
    }
}