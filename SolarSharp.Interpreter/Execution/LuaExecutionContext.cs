using System;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Immutable execution context for a Lua script.
    /// The source file path (with optional selectors like :eval) is the primary security context.
    /// </summary>
    public sealed class LuaExecutionContext : IEquatable<LuaExecutionContext>
    {
        /// <summary>
        /// Source file being executed (primary security context).
        /// May include selectors like :eval for dynamic code execution.
        /// </summary>
        public string SourceFile { get; }

        /// <summary>
        /// Optional identity of the executing script (if manifest present)
        /// </summary>
        public Maybe<ScriptIdentity> Identity { get; }

        /// <summary>
        /// Optional manifest for the executing script
        /// </summary>
        public Maybe<Manifest> Manifest { get; }

        /// <summary>
        /// Parent context if this is a nested execution
        /// </summary>
        public Maybe<LuaExecutionContext> Parent { get; }

        /// <summary>
        /// Fingerprint of the key that signed the manifest (if manifest present and signed)
        /// </summary>
        public Maybe<string> SigningKeyFingerprint { get; }

        private LuaExecutionContext(
            string sourceFile,
            Maybe<ScriptIdentity> identity,
            Maybe<Manifest> manifest,
            Maybe<LuaExecutionContext> parent,
            Maybe<string> signingKeyFingerprint = default
        )
        {
            SourceFile = sourceFile ?? throw new ArgumentNullException(nameof(sourceFile));
            Identity = identity;
            Manifest = manifest;
            Parent = parent;
            SigningKeyFingerprint = signingKeyFingerprint;
        }

        /// <summary>
        /// Creates a new execution context from source file path only.
        /// This is the primary way to create contexts for security policy resolution.
        /// </summary>
        public static Result<LuaExecutionContext, ExecutionError> CreateFromPath(
            string sourceFile,
            Maybe<LuaExecutionContext> parent = default
        )
        {
            if (string.IsNullOrWhiteSpace(sourceFile))
                return Result.Failure<LuaExecutionContext, ExecutionError>(
                    new ScriptLoadError(sourceFile ?? "", "Source file path cannot be empty")
                );

            return Result.Success<LuaExecutionContext, ExecutionError>(
                new LuaExecutionContext(
                    sourceFile,
                    Maybe<ScriptIdentity>.None,
                    Maybe<Manifest>.None,
                    parent,
                    Maybe<string>.None
                )
            );
        }

        /// <summary>
        /// Creates a new execution context with manifest and identity
        /// </summary>
        public static Result<LuaExecutionContext, ExecutionError> CreateWithManifest(
            string sourceFile,
            Manifest manifest,
            ScriptIdentity identity,
            Maybe<LuaExecutionContext> parent = default,
            Maybe<string> signingKeyFingerprint = default
        )
        {
            if (string.IsNullOrWhiteSpace(sourceFile))
                return Result.Failure<LuaExecutionContext, ExecutionError>(
                    new ScriptLoadError(sourceFile ?? "", "Source file path cannot be empty")
                );

            if (manifest == null)
                return Result.Failure<LuaExecutionContext, ExecutionError>(
                    new InvalidManifest("Manifest cannot be null")
                );

            return Result.Success<LuaExecutionContext, ExecutionError>(
                new LuaExecutionContext(
                    sourceFile,
                    Maybe<ScriptIdentity>.From(identity),
                    Maybe<Manifest>.From(manifest),
                    parent,
                    signingKeyFingerprint
                )
            );
        }

        /// <summary>
        /// Creates a context for host-initiated execution (e.g., Script.DoString from C#)
        /// </summary>
        public static LuaExecutionContext CreateForHostExecution()
        {
            // Use ":eval" as the source file for host-initiated execution
            // This allows BasePolicySet to match patterns like ":eval"
            return new LuaExecutionContext(
                ":eval",
                Maybe<ScriptIdentity>.None,
                Maybe<Manifest>.None,
                Maybe<LuaExecutionContext>.None,
                Maybe<string>.None
            );
        }

        /// <summary>
        /// Creates a new context with updated values (builder pattern)
        /// </summary>
        public LuaExecutionContext With(
            string sourceFile = null,
            Maybe<ScriptIdentity>? identity = null,
            Maybe<Manifest>? manifest = null,
            Maybe<LuaExecutionContext>? parent = null,
            Maybe<string>? signingKeyFingerprint = null
        ) =>
            new LuaExecutionContext(
                sourceFile ?? SourceFile,
                identity ?? Identity,
                manifest ?? Manifest,
                parent ?? Parent,
                signingKeyFingerprint ?? SigningKeyFingerprint
            );

        /// <summary>
        /// Creates a child context for eval'd code.
        /// Appends :eval selector to the source file path for policy resolution.
        /// </summary>
        public Result<LuaExecutionContext, ExecutionError> CreateEvalContext()
        {
            // The source file for eval contexts includes the :eval suffix
            // This allows BasePolicySet to apply different policies to eval'd code
            var evalSourceFile = SourceFile.EndsWith(":eval")
                ? SourceFile // Already has :eval suffix (chained eval)
                : $"{SourceFile}:eval"; // Add :eval suffix

            return Result.Success<LuaExecutionContext, ExecutionError>(
                With(sourceFile: evalSourceFile, parent: Maybe<LuaExecutionContext>.From(this))
            );
        }

        public bool Equals(LuaExecutionContext other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return SourceFile == other.SourceFile
                && Identity.Equals(other.Identity)
                && Manifest.Equals(other.Manifest)
                && Parent.Equals(other.Parent)
                && SigningKeyFingerprint.Equals(other.SigningKeyFingerprint);
        }

        public override bool Equals(object obj)
        {
            return obj is LuaExecutionContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SourceFile, Identity, Manifest, Parent, SigningKeyFingerprint);
        }

        public override string ToString()
        {
            return Identity.HasValue
                ? $"LuaContext({Identity.Value.Name}@{SourceFile})"
                : $"LuaContext({SourceFile})";
        }

        public static bool operator ==(LuaExecutionContext left, LuaExecutionContext right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LuaExecutionContext left, LuaExecutionContext right)
        {
            return !Equals(left, right);
        }
    }
}
