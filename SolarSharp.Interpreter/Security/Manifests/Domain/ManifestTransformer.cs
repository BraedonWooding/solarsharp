#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Manifests.Domain
{
    /// <summary>
    /// Functional transformations for extracting domain objects from manifests
    /// </summary>
    public static class ManifestTransformer
    {
        /// <summary>
        /// Transforms a manifest into ProtectedFiles domain objects
        /// </summary>
        public static Result<ProtectedFiles, string> ExtractProtectedFiles(
            Manifest manifest,
            string manifestPath,
            bool fromTrustedManifest = false
        )
        {
            try
            {
                var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? "";
                var protectedFiles = ProtectedFiles.Empty;

                // Transform files from V2.0 signed content blocks
                foreach (var (packageId, package, keyId) in manifest.GetAllPackages())
                {
                    foreach (var (filePath, hash) in package.Files)
                    {
                        var protectedFile = new ProtectedFile
                        {
                            RelativePath = filePath,
                            ExpectedHash = hash.StartsWith("sha256:") ? hash.Substring(7) : hash,
                            HashAlgorithm = "SHA256",
                            ExpectedSize = 0, // Not available in V2.0 format
                            ReadOnly = true,
                            PolicyName = null, // V2.0 uses selector-based policies
                            SourceManifest = manifestPath,
                            FromTrustedManifest = fromTrustedManifest,
                        };

                        protectedFiles = protectedFiles.AddFile(protectedFile);
                    }
                }

                return Result.Success<ProtectedFiles, string>(protectedFiles);
            }
            catch (Exception ex)
            {
                return Result.Failure<ProtectedFiles, string>(
                    $"Failed to extract protected files from manifest {manifestPath}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Extracts security policies from a manifest
        /// </summary>
        public static Result<ImmutableDictionary<string, SecurityPolicy>, string> ExtractPolicies(
            Manifest manifest
        )
        {
            try
            {
                var policies = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();

                // V2.0: Extract policies from signed content blocks
                foreach (var block in manifest.SignedContent)
                {
                    foreach (var manifestPolicy in block.Policies)
                    {
                        var securityPolicy = ConvertManifestPolicyToSecurityPolicy(manifestPolicy);
                        var policyName = manifestPolicy.Selector.StartsWith(":")
                            ? manifestPolicy.Selector.Substring(1)
                            : manifestPolicy.Selector;

                        if (!IsEmptyPolicy(securityPolicy))
                        {
                            policies[policyName] = securityPolicy;
                        }
                    }
                }

                return Result.Success<ImmutableDictionary<string, SecurityPolicy>, string>(
                    policies.ToImmutable()
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<ImmutableDictionary<string, SecurityPolicy>, string>(
                    $"Failed to extract policies from manifest: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Combines ProtectedFiles from multiple manifests
        /// Trusted manifests take precedence over untrusted ones for the same file
        /// </summary>
        public static ProtectedFiles CombineProtectedFiles(
            ProtectedFiles existing,
            ProtectedFiles newFiles
        )
        {
            var combined = existing;

            foreach (var path in newFiles.ProtectedPaths)
            {
                var newFile = newFiles.GetProtection(path);
                var existingFile = existing.GetProtection(path);

                newFile.Match(
                    newProtection =>
                    {
                        existingFile.Match(
                            existingProtection =>
                            {
                                // If new file is from trusted manifest and existing is not,
                                // or if both are trusted/untrusted, new one takes precedence
                                if (
                                    newProtection.FromTrustedManifest
                                    || !existingProtection.FromTrustedManifest
                                )
                                {
                                    combined = combined.AddFile(newProtection);
                                }
                            },
                            () =>
                            {
                                // No existing protection, add new one
                                combined = combined.AddFile(newProtection);
                            }
                        );
                    },
                    () => { /* No new protection to add */
                    }
                );
            }

            return combined;
        }

        /// <summary>
        /// Validates that a manifest's file entries are well-formed
        /// </summary>
        public static Result<bool, string> ValidateFileEntries(Manifest manifest)
        {
            // V2.0: Validate files from signed content blocks
            foreach (var (packageId, package, keyId) in manifest.GetAllPackages())
            {
                foreach (var (filePath, hash) in package.Files)
                {
                    // Validate path is not absolute or contains traversal
                    if (Path.IsPathRooted(filePath))
                        return Result.Failure<bool, string>(
                            $"File path must be relative: {filePath}"
                        );

                    if (filePath.Contains(".."))
                        return Result.Failure<bool, string>(
                            $"File path cannot contain path traversal: {filePath}"
                        );

                    // Validate hash format
                    if (string.IsNullOrWhiteSpace(hash))
                        return Result.Failure<bool, string>(
                            $"File hash cannot be empty: {filePath}"
                        );

                    // V2.0 uses SHA256 by default
                    if (!hash.StartsWith("sha256:") && !IsSupportedHashAlgorithm("SHA256"))
                        return Result.Failure<bool, string>(
                            $"Invalid hash format for file: {filePath}"
                        );
                }
            }

            return Result.Success<bool, string>(true);
        }

        private static bool IsEmptyPolicy(SecurityPolicy policy) =>
            policy == null || policy is { TimeoutMs: 0, MaxMemoryMB: 0, MaxCallDepth: 0 };

        private static bool IsSupportedHashAlgorithm(string algorithm) =>
            algorithm.ToUpperInvariant() switch
            {
                "SHA256" => true,
                "SHA1" => true,
                "MD5" => true,
                _ => false,
            };

        private static SecurityPolicy ConvertManifestPolicyToSecurityPolicy(
            ManifestPolicy manifestPolicy
        )
        {
            var restrict = manifestPolicy.Restrict ?? new PolicyRestrictions();
            var grant = manifestPolicy.Grant ?? new PolicyGrant();

            return new SecurityPolicy
            {
                Name = CSharpFunctionalExtensions.Maybe<string>.From(
                    $"policy_{manifestPolicy.Selector}"
                ),
                TimeoutMs = ParseTimeout(restrict.Timeout),
                MaxMemoryMB = ParseMemory(restrict.MaxMemory),
                MaxInstructions = 1000000, // Default
                MaxCallDepth = 100, // Default
                AllowExecution = !manifestPolicy.DenyAll,
                AllowedModules = ConvertModules(grant.Modules),
                Capabilities = ConvertCapabilities(grant.Capabilities),
                FilePermissions = ConvertFilePermissions(grant),
                PubSubPermissions = new PubSubPermissions(),
            };
        }

        private static int ParseTimeout(string timeoutStr)
        {
            if (string.IsNullOrEmpty(timeoutStr))
                return 30000; // 30 seconds default

            if (timeoutStr.EndsWith("s"))
            {
                if (int.TryParse(timeoutStr.Substring(0, timeoutStr.Length - 1), out var seconds))
                    return seconds * 1000;
            }
            if (timeoutStr.EndsWith("m"))
            {
                if (int.TryParse(timeoutStr.Substring(0, timeoutStr.Length - 1), out var minutes))
                    return minutes * 60 * 1000;
            }

            return 30000; // Default
        }

        private static int ParseMemory(string memoryStr)
        {
            if (string.IsNullOrEmpty(memoryStr))
                return 64; // 64MB default

            if (memoryStr.EndsWith("MB"))
            {
                if (int.TryParse(memoryStr.Substring(0, memoryStr.Length - 2), out var mb))
                    return mb;
            }
            if (memoryStr.EndsWith("GB"))
            {
                if (int.TryParse(memoryStr.Substring(0, memoryStr.Length - 2), out var gb))
                    return gb * 1024;
            }

            return 64; // Default
        }

        private static Modules.CoreModules ConvertModules(ImmutableArray<string> modules)
        {
            var result = Modules.CoreModules.None;

            foreach (var module in modules)
            {
                result |= module.ToLowerInvariant() switch
                {
                    "basic" => Modules.CoreModules.Basic,
                    "string" => Modules.CoreModules.String,
                    "table" => Modules.CoreModules.Table,
                    "math" => Modules.CoreModules.Math,
                    "bit32" => Modules.CoreModules.Bit32,
                    "coroutine" => Modules.CoreModules.Coroutine,
                    "os" => Modules.CoreModules.OS_System | Modules.CoreModules.OS_Time,
                    "os_system" => Modules.CoreModules.OS_System,
                    "os_time" => Modules.CoreModules.OS_Time,
                    "io" => Modules.CoreModules.IO,
                    "debug" => Modules.CoreModules.Debug,
                    "package" => Modules.CoreModules.LoadMethods,
                    "json" => Modules.CoreModules.Json,
                    "dynamic" => Modules.CoreModules.Dynamic,
                    "errhandling" => Modules.CoreModules.ErrorHandling,
                    "pubsub" => Modules.CoreModules.PubSub,
                    _ => Modules.CoreModules.None,
                };
            }

            return result;
        }

        private static ImmutableDictionary<string, FilePermissions> ConvertFilePermissions(
            PolicyGrant grant
        )
        {
            var permissions = ImmutableDictionary.CreateBuilder<string, FilePermissions>();

            foreach (var readPath in grant.FileRead)
            {
                permissions[readPath] = FilePermissions.Read;
            }

            foreach (var writePath in grant.FileWrite)
            {
                permissions[writePath] = permissions.TryGetValue(writePath, out var existing)
                    ? existing | FilePermissions.ReadWrite
                    : FilePermissions.ReadWrite;
            }

            return permissions.ToImmutable();
        }

        private static ScriptCapabilities ConvertCapabilities(ImmutableArray<string> capabilities)
        {
            var result = ScriptCapabilities.None;

            foreach (var capability in capabilities)
            {
                result |= capability.ToLowerInvariant() switch
                {
                    "eval" => ScriptCapabilities.ProcessExecution,
                    "reflection" => ScriptCapabilities.ReflectionAccess,
                    "io" => ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
                    "fileread" => ScriptCapabilities.FileRead,
                    "filewrite" => ScriptCapabilities.FileWrite,
                    "network" => ScriptCapabilities.NetworkAccess,
                    "networkaccess" => ScriptCapabilities.NetworkAccess,
                    "system" => ScriptCapabilities.SystemInformation,
                    "systeminformation" => ScriptCapabilities.SystemInformation,
                    "processexecution" => ScriptCapabilities.ProcessExecution,
                    "reflectionaccess" => ScriptCapabilities.ReflectionAccess,
                    _ => ScriptCapabilities.None,
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Result of manifest transformation containing both policies and protected files
    /// </summary>
    public sealed record ManifestTransformResult
    {
        public ProtectedFiles ProtectedFiles { get; init; } = ProtectedFiles.Empty;
        public ImmutableDictionary<string, SecurityPolicy> Policies { get; init; } =
            ImmutableDictionary<string, SecurityPolicy>.Empty;
        public string SourceManifest { get; init; } = "";
        public bool FromTrustedManifest { get; init; }

        public static Result<ManifestTransformResult, string> Transform(
            Manifest manifest,
            string manifestPath,
            bool fromTrustedManifest
        )
        {
            return ManifestTransformer
                .ValidateFileEntries(manifest)
                .Bind(_ =>
                    ManifestTransformer.ExtractProtectedFiles(
                        manifest,
                        manifestPath,
                        fromTrustedManifest
                    )
                )
                .Bind(protectedFiles =>
                    ManifestTransformer
                        .ExtractPolicies(manifest)
                        .Map(policies => new ManifestTransformResult
                        {
                            ProtectedFiles = protectedFiles,
                            Policies = policies,
                            SourceManifest = manifestPath,
                            FromTrustedManifest = fromTrustedManifest,
                        })
                );
        }
    }
}
