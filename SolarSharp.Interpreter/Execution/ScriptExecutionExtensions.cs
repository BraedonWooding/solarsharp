using System;
using System.IO.Abstractions;
using System.Linq;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter.Communication;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Extensions for script execution with manifest support
    /// </summary>
    public static class ScriptExecutionExtensions
    {
        /// <summary>
        /// Executes a file with automatic manifest discovery
        /// </summary>
        public static Result<DynValue, ExecutionError> DoFileWithManifest(
            this Script script,
            string filename,
            ManifestLoader manifestLoader
        )
        {
            if (script == null)
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(filename, "Script instance is null")
                );

            if (string.IsNullOrWhiteSpace(filename))
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(filename ?? "", "Filename cannot be empty")
                );

            if (manifestLoader == null)
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(filename, "Manifest loader is required")
                );

            // Load manifest for the file's directory
            return manifestLoader
                .LoadForFile(filename)
                .MapError(err => err as ExecutionError)
                .Bind(loadResult =>
                {
                    var (manifest, newLoader) = loadResult;

                    // Create execution context with manifest if available
                    var contextResult = manifest.Certificate.HasValue
                        ? CreateIdentityFromManifest(manifest.Manifest, manifest.Certificate.Value)
                            .MapError(err => err as ExecutionError)
                            .Bind(identity =>
                                LuaExecutionContext.CreateWithManifest(
                                    filename,
                                    manifest.Manifest,
                                    identity,
                                    Maybe<LuaExecutionContext>.None,
                                    string.IsNullOrEmpty(manifest.PublicKeyFingerprint)
                                        ? Maybe<string>.None
                                        : Maybe<string>.From(manifest.PublicKeyFingerprint)
                                )
                            )
                        : LuaExecutionContext.CreateFromPath(filename);

                    return contextResult.Bind(context =>
                    {
                        // Register services for the script
                        script.SetService(script.GetService<IMessageBus>());

                        // Execute with context
                        return ExecutionContextManager.WithContext(
                            context,
                            ctx => ExecuteFileInContext(script, filename, ctx)
                        );
                    });
                });
        }

        /// <summary>
        /// Executes a string with parent context (for eval/load)
        /// </summary>
        public static Result<DynValue, ExecutionError> DoStringWithContext(
            this Script script,
            string code,
            string chunkName = null
        )
        {
            if (script == null)
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(chunkName ?? "[string]", "Script instance is null")
                );

            if (code == null)
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(chunkName ?? "[string]", "Code cannot be null")
                );

            // Get current context and create eval context
            return ExecutionContextManager
                .GetCurrent()
                .Bind(parentContext => parentContext.CreateEvalContext())
                .Bind(evalContext =>
                    ExecutionContextManager.WithContext(
                        evalContext,
                        ctx => ExecuteStringInContext(script, code, chunkName ?? "[eval]", ctx)
                    )
                );
        }

        /// <summary>
        /// Creates a default manifest loader for a script
        /// </summary>
        public static ManifestLoader CreateManifestLoader(
            this Script script,
            IFileSystem fileSystem = null
        )
        {
            var fs = fileSystem ?? new FileSystem();
            var certManager = new CertificateManager();

            return new ManifestLoader(fs, certManager);
        }

        /// <summary>
        /// Executes a file within a context
        /// </summary>
        private static Result<DynValue, ExecutionError> ExecuteFileInContext(
            Script script,
            string filename,
            LuaExecutionContext context
        )
        {
            try
            {
                // Traditional imperative call wrapped in Result
                var result = script.DoFile(filename);
                return Result.Success<DynValue, ExecutionError>(result);
            }
            catch (Exception ex)
            {
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(filename, ex.Message)
                );
            }
        }

        /// <summary>
        /// Executes a string within a context
        /// </summary>
        private static Result<DynValue, ExecutionError> ExecuteStringInContext(
            Script script,
            string code,
            string chunkName,
            LuaExecutionContext context
        )
        {
            try
            {
                // Traditional imperative call wrapped in Result
                var result = script.DoString(code, null, chunkName);
                return Result.Success<DynValue, ExecutionError>(result);
            }
            catch (Exception ex)
            {
                return Result.Failure<DynValue, ExecutionError>(
                    new ScriptLoadError(chunkName, ex.Message)
                );
            }
        }

        /// <summary>
        /// Helper to run a file with automatic manifest discovery
        /// </summary>
        public static Result<DynValue, ExecutionError> RunFileWithManifest(
            string filename,
            BasePolicySet basePolicySet,
            IFileSystem fileSystem = null
        )
        {
            var policySet = basePolicySet;
            var script = new Script(policySet);
            var loader = script.CreateManifestLoader(fileSystem);

            return script.DoFileWithManifest(filename, loader);
        }

        /// <summary>
        /// Creates a script identity from manifest and certificate
        /// </summary>
        private static Result<ScriptIdentity, InvalidManifest> CreateIdentityFromManifest(
            Manifest manifest,
            X509Certificate certificate
        )
        {
            // V2.0: Extract identity from first package
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            if (firstPackage.Package == null)
                return Result.Failure<ScriptIdentity, InvalidManifest>(
                    new InvalidManifest("Manifest must contain at least one package")
                );

            var name = firstPackage.Package.Metadata.Name;
            var version = firstPackage.Package.Metadata.Version;

            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<ScriptIdentity, InvalidManifest>(
                    new InvalidManifest("Package name is required")
                );

            if (string.IsNullOrWhiteSpace(version))
                return Result.Failure<ScriptIdentity, InvalidManifest>(
                    new InvalidManifest("Package version is required")
                );

            // Parse version
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
                return Result.Failure<ScriptIdentity, InvalidManifest>(
                    new InvalidManifest($"Invalid version format: {version}")
                );

            // Calculate public key token - use BouncyCastle certificate directly
            var bcCertificate = certificate;
            var publicKeyToken = CertificateManager.CalculatePublicKeyToken(bcCertificate);

            return Result.Success<ScriptIdentity, InvalidManifest>(
                new ScriptIdentity(name, nugetVersion, publicKeyToken)
            );
        }
    }
}
