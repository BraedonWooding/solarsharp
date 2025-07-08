using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Pure functional transformers for the policy pipeline
    /// </summary>
    public static class PolicyTransformers
    {
        /// <summary>
        /// Transform: DirectorySet → ManifestFiles
        /// </summary>
        public static async Task<Result<ManifestFiles, ScanError>> ScanDirectories(
            DirectorySet directories,
            IFileSystem fileSystem,
            ManifestScanOptions options
        )
        {
            try
            {
                var scanner = new ManifestScanner(fileSystem);
                var allFiles = ManifestFiles.Empty;

                foreach (var directory in directories)
                {
                    var scanResult = await scanner.ScanDirectoryAsync(directory, options);
                    var files = scanResult
                        .Where(r => r.IsSuccess)
                        .Select(r => new ManifestFile(r.FilePath, r.DirectoryPath, r.RelativePath));
                    allFiles = allFiles.AddRange(files);
                }

                return Result.Success<ManifestFiles, ScanError>(allFiles);
            }
            catch (Exception ex)
            {
                return Result.Failure<ManifestFiles, ScanError>(new ScanError(ex.Message));
            }
        }

        /// <summary>
        /// Transform: ManifestFile → Result<Manifest, ParseError>
        /// </summary>
        public static async Task<Result<Manifest, ParseError>> ParseManifest(
            ManifestFile file,
            IFileSystem fileSystem,
            JsonSerializerOptions jsonOptions
        )
        {
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(file.FilePath);
                var manifest = JsonSerializer.Deserialize<Manifest>(content, jsonOptions);

                if (manifest == null)
                    return Result.Failure<Manifest, ParseError>(
                        new ParseError(file.FilePath, "Manifest deserialized to null")
                    );

                return Result.Success<Manifest, ParseError>(manifest);
            }
            catch (Exception ex)
            {
                return Result.Failure<Manifest, ParseError>(
                    new ParseError(file.FilePath, ex.Message)
                );
            }
        }

        /// <summary>
        /// Transform: ManifestFiles → Result<ManifestCollection, ParseError>
        /// </summary>
        public static async Task<Result<ManifestCollection, ParseError>> ParseManifests(
            ManifestFiles files,
            IFileSystem fileSystem,
            JsonSerializerOptions jsonOptions
        )
        {
            var parseResults = new List<Manifest>();

            foreach (var file in files)
            {
                var result = await ParseManifest(file, fileSystem, jsonOptions);
                if (result.IsSuccess)
                {
                    parseResults.Add(result.Value);
                }
                else
                {
                    // Return the first error encountered with proper file path
                    return Result.Failure<ManifestCollection, ParseError>(result.Error);
                }
            }

            return Result.Success<ManifestCollection, ParseError>(
                ManifestCollection.Create(parseResults)
            );
        }

        /// <summary>
        /// Transform: Manifest → Result<ValidatedManifest, ValidationError>
        /// </summary>
        public static Result<ValidatedManifest, ValidationError> ValidateManifest(
            Manifest manifest,
            IManifestValidator validator,
            string directoryPath = ""
        )
        {
            var result = validator.Validate(manifest);

            if (result.IsValid)
                return Result.Success<ValidatedManifest, ValidationError>(
                    new ValidatedManifest(manifest)
                );
            return Result.Failure<ValidatedManifest, ValidationError>(
                new ValidationError(directoryPath, result.Errors)
            );
        }

        /// <summary>
        /// Transform: ManifestFiles → Result<ValidatedManifests, ValidationError>
        /// </summary>
        public static async Task<
            Result<ValidatedManifests, ValidationError>
        > ValidateManifestsFromFiles(
            ManifestFiles files,
            IFileSystem fileSystem,
            JsonSerializerOptions jsonOptions,
            IManifestValidator validator
        )
        {
            var validated = new List<ValidatedManifest>();

            foreach (var file in files)
            {
                var parseResult = await ParseManifest(file, fileSystem, jsonOptions);
                if (!parseResult.IsSuccess)
                {
                    // Convert ParseError to ValidationError with proper directory context
                    return Result.Failure<ValidatedManifests, ValidationError>(
                        new ValidationError(file.DirectoryPath, new[] { parseResult.Error.Message })
                    );
                }

                var validateResult = ValidateManifest(
                    parseResult.Value,
                    validator,
                    file.DirectoryPath
                );
                if (!validateResult.IsSuccess)
                {
                    // Return the first validation error encountered
                    return Result.Failure<ValidatedManifests, ValidationError>(
                        validateResult.Error
                    );
                }

                validated.Add(validateResult.Value);
            }

            return Result.Success<ValidatedManifests, ValidationError>(
                ValidatedManifests.Create(validated)
            );
        }

        /// <summary>
        /// Transform: ManifestCollection → ValidatedManifests
        /// </summary>
        public static ValidatedManifests ValidateManifests(
            ManifestCollection manifests,
            IManifestValidator validator
        )
        {
            var validated = manifests
                .Select(m => ValidateManifest(m, validator))
                .Where(r => r.IsSuccess)
                .Select(r => r.Value);

            return ValidatedManifests.Create(validated);
        }

        /// <summary>
        /// Transform: ValidatedManifest → Result<VerifiedManifest, SignatureError>
        /// </summary>
        public static async Task<Result<VerifiedManifest, SignatureError>> VerifySignature(
            ValidatedManifest validated,
            ISignatureVerifier verifier,
            string directoryPath = ""
        )
        {
            var verifyResult = await verifier.VerifySignatureAsync(validated.Manifest);

            if (verifyResult is { IsSuccess: true, Value: true })
                return Result.Success<VerifiedManifest, SignatureError>(
                    new VerifiedManifest(validated)
                );
            return Result.Failure<VerifiedManifest, SignatureError>(
                new SignatureError(
                    directoryPath,
                    verifyResult.IsSuccess
                        ? "Signature verification returned false"
                        : verifyResult.Error ?? "Signature invalid"
                )
            );
        }

        /// <summary>
        /// Transform: ValidatedManifests → VerifiedManifests
        /// </summary>
        public static async Task<VerifiedManifests> VerifySignatures(
            ValidatedManifests validated,
            ISignatureVerifier verifier
        )
        {
            var verificationTasks = validated.Select(v => VerifySignature(v, verifier)).ToArray();
            var verified = await Task.WhenAll(verificationTasks);
            var verifiedArray = verified.Where(r => r.IsSuccess).Select(r => r.Value);

            return VerifiedManifests.Create(verifiedArray);
        }

        /// <summary>
        /// Transform: VerifiedManifest → Result<CompiledPolicy, CompileError>
        /// </summary>
        public static Result<CompiledPolicy, CompileError> CompilePolicy(VerifiedManifest verified)
        {
            try
            {
                // V2.0: Extract policies from signed content blocks
                var policies = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();
                var scopeRules = new List<ScopeRule>();

                foreach (var block in verified.Manifest.SignedContent)
                {
                    foreach (var manifestPolicy in block.Policies)
                    {
                        var securityPolicy = ConvertManifestPolicyToSecurityPolicy(manifestPolicy);
                        var policyName = manifestPolicy.Selector.StartsWith(":")
                            ? manifestPolicy.Selector.Substring(1)
                            : manifestPolicy.Selector;

                        policies[policyName] = securityPolicy;

                        // Create scope rules for files in packages this policy applies to
                        foreach (var packageId in manifestPolicy.Packages)
                        {
                            if (
                                block.Packages.TryGetValue(packageId, out var package)
                                || packageId == "*"
                            )
                            {
                                if (packageId == "*")
                                {
                                    // Apply to all packages in this block
                                    foreach (var (_, pkg) in block.Packages)
                                    {
                                        foreach (var filePath in pkg.Files.Keys)
                                        {
                                            scopeRules.Add(
                                                new ScopeRule
                                                {
                                                    Pattern = filePath,
                                                    PolicyName = policyName,
                                                }
                                            );
                                        }
                                    }
                                }
                                else
                                {
                                    // Apply to specific package files
                                    foreach (var filePath in package.Files.Keys)
                                    {
                                        scopeRules.Add(
                                            new ScopeRule
                                            {
                                                Pattern = filePath,
                                                PolicyName = policyName,
                                            }
                                        );
                                    }
                                }
                            }
                        }
                    }
                }

                var manifestName = verified.Manifest.ManifestId ?? "unnamed";
                var compiled = new CompiledPolicy(
                    manifestName,
                    PolicyCollection.Create(policies.ToImmutable()),
                    ScopeRuleCollection.Create(scopeRules),
                    ConvertToScriptIdentityInfo(null) // V2.0 doesn't have Identity property
                );

                return Result.Success<CompiledPolicy, CompileError>(compiled);
            }
            catch (Exception ex)
            {
                return Result.Failure<CompiledPolicy, CompileError>(
                    new CompileError(verified.Manifest.ManifestId ?? "unnamed", ex.Message)
                );
            }
        }

        /// <summary>
        /// Transform: VerifiedManifests → CompiledPolicies
        /// </summary>
        public static CompiledPolicies CompilePolicies(VerifiedManifests verified)
        {
            var compiled = verified
                .Select(CompilePolicy)
                .Where(r => r.IsSuccess)
                .Select(r => r.Value);

            return CompiledPolicies.Create(compiled);
        }

        /// <summary>
        /// Transform: CompiledPolicies → Result<UnionedPolicy, UnionError>
        /// </summary>
        public static Result<UnionedPolicy, UnionError> UnionPolicies(CompiledPolicies policies)
        {
            if (policies.IsEmpty)
                return Result.Failure<UnionedPolicy, UnionError>(
                    new UnionError("No policies to union")
                );

            try
            {
                var firstPolicy = policies.First();
                var unionedPolicies = firstPolicy.Policies;
                var unionedRules = firstPolicy.ScopeRules;

                foreach (var policy in policies.Skip(1))
                {
                    unionedPolicies = IntersectPolicies(unionedPolicies, policy.Policies);
                    unionedRules = new ScopeRuleCollection(
                        unionedRules.Rules.AddRange(policy.ScopeRules.Rules)
                    );
                }

                var directories = DirectoryCollection.Create(policies.Select(p => p.Directory));
                var unioned = new UnionedPolicy(directories, unionedPolicies, unionedRules);

                return Result.Success<UnionedPolicy, UnionError>(unioned);
            }
            catch (Exception ex)
            {
                return Result.Failure<UnionedPolicy, UnionError>(new UnionError(ex.Message));
            }
        }

        /// <summary>
        /// Transform: UnionedPolicy → PolicyStore
        /// </summary>
        public static PolicyStore CreateStore(UnionedPolicy policy) => new PolicyStore(policy);

        /// <summary>
        /// Intersects two policy collections, taking the most restrictive values
        /// </summary>
        private static PolicyCollection IntersectPolicies(
            PolicyCollection first,
            PolicyCollection second
        )
        {
            var intersected = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();

            foreach (var kvp in first.Policies)
            {
                if (second.Policies.TryGetValue(kvp.Key, out var otherPolicy))
                {
                    var combined = new SecurityPolicy
                    {
                        Name = kvp.Value.Name,
                        TimeoutMs = Math.Min(kvp.Value.TimeoutMs, otherPolicy.TimeoutMs),
                        MaxMemoryMB = Math.Min(kvp.Value.MaxMemoryMB, otherPolicy.MaxMemoryMB),
                        MaxInstructions = Math.Min(
                            kvp.Value.MaxInstructions,
                            otherPolicy.MaxInstructions
                        ),
                        MaxCallDepth = Math.Min(kvp.Value.MaxCallDepth, otherPolicy.MaxCallDepth),
                        AllowExecution = kvp.Value.AllowExecution && otherPolicy.AllowExecution,
                        AllowedModules = kvp.Value.AllowedModules & otherPolicy.AllowedModules,
                        Capabilities = kvp.Value.Capabilities & otherPolicy.Capabilities,
                        FilePermissions = kvp.Value.FilePermissions,
                        PubSubPermissions = kvp.Value.PubSubPermissions,
                    };
                    intersected[kvp.Key] = combined;
                }
                else
                {
                    intersected[kvp.Key] = kvp.Value;
                }
            }

            return new PolicyCollection(intersected.ToImmutable());
        }

        /// <summary>
        /// Extracts policy name from scope rule using proper domain logic
        /// </summary>
        private static string ExtractPolicyName(ScopeRule rule)
        {
            return rule.PolicyName;
        }

        /// <summary>
        /// Transform: PolicyStore → CompiledPolicyRules
        /// </summary>
        public static CompiledPolicyRules CreateCompiledRules(
            PolicyStore store,
            SignaturePolicies signaturePolicies,
            PathPolicies pathPolicies,
            SecurityPolicy fallbackPolicy
        )
        {
            // Create V2.0 manifest with SignedContent containing the legacy policies
            var manifestPolicies = store
                .Policy.Policies.Policies.Select(kvp =>
                    ConvertSecurityPolicyToManifestPolicy(kvp.Key, kvp.Value)
                )
                .ToImmutableArray();

            // Create packages from scope rules - group files by pattern into packages
            var manifestPackages = store
                .Policy.ScopeRules.Rules.GroupBy(r => ExtractPackageFromPattern(r.Pattern))
                .ToImmutableDictionary(
                    g => g.Key,
                    g => new ManifestPackage
                    {
                        Files = g.ToImmutableDictionary(
                            rule => rule.Pattern,
                            rule => "sha256:placeholder"
                        ), // Placeholder hash
                        Metadata = new PackageMetadata
                        {
                            Name = g.Key,
                            Version = "1.0.0",
                            Description = $"Legacy package for {g.Key}",
                        },
                    }
                );

            var signedContentBlock = new SignedContentBlock
            {
                KeyId = "legacy",
                Signature = "",
                Packages = manifestPackages,
                Policies = manifestPolicies,
            };

            var manifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "legacy_converted",
                SignedContent = ImmutableArray.Create(signedContentBlock),
            };

            return new CompiledPolicyRules(
                signaturePolicies.Policies,
                pathPolicies.Policies,
                manifest,
                fallbackPolicy
            );
        }

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

        /// <summary>
        /// Creates empty ScriptIdentityInfo for V2.0 manifests
        /// </summary>
        private static ScriptIdentityInfo ConvertToScriptIdentityInfo(object unused)
        {
            // V2.0 manifests don't have a central identity - identity comes from packages
            return new ScriptIdentityInfo();
        }

        /// <summary>
        /// Converts SecurityPolicy to ManifestPolicy for V2.0 format
        /// </summary>
        private static ManifestPolicy ConvertSecurityPolicyToManifestPolicy(
            string policyName,
            SecurityPolicy securityPolicy
        )
        {
            return new ManifestPolicy
            {
                Packages = ImmutableArray.Create("*"), // Apply to all packages
                Selector = ":file", // Default selector
                Grant = new PolicyGrant
                {
                    FileRead = ConvertFilePermissionsToFileRead(securityPolicy.FilePermissions),
                    FileWrite = ConvertFilePermissionsToFileWrite(securityPolicy.FilePermissions),
                    Network = ConvertHostPermissions(securityPolicy.AllowedHosts),
                    Roles = ImmutableArray<string>.Empty,
                    Capabilities = ConvertCapabilitiesToStrings(securityPolicy.Capabilities),
                },
                Restrict = new PolicyRestrictions
                {
                    MaxMemory = $"{securityPolicy.MaxMemoryMB}MB",
                    Timeout = $"{securityPolicy.TimeoutMs}ms",
                    Deny = ImmutableArray<string>.Empty,
                    InheritFromFile = true,
                },
                DenyIfSignedBy = ImmutableArray<string>.Empty,
                DenyAll = !securityPolicy.AllowExecution,
            };
        }

        /// <summary>
        /// Extracts package name from file pattern
        /// </summary>
        private static string ExtractPackageFromPattern(string pattern)
        {
            // Simple package extraction - use directory name or "default"
            if (pattern.Contains("/"))
            {
                var parts = pattern.Split('/');
                return parts[0] != "*" ? parts[0] : "default";
            }
            return "default";
        }

        /// <summary>
        /// Converts file permissions to read paths
        /// </summary>
        private static ImmutableArray<string> ConvertFilePermissionsToFileRead(
            ImmutableDictionary<string, FilePermissions> filePermissions
        )
        {
            return filePermissions
                .Where(kvp => (kvp.Value & FilePermissions.Read) != 0)
                .Select(kvp => kvp.Key)
                .ToImmutableArray();
        }

        /// <summary>
        /// Converts file permissions to write paths
        /// </summary>
        private static ImmutableArray<string> ConvertFilePermissionsToFileWrite(
            ImmutableDictionary<string, FilePermissions> filePermissions
        )
        {
            return filePermissions
                .Where(kvp =>
                    (kvp.Value & (FilePermissions.ReadWrite | FilePermissions.SandboxedReadWrite))
                    != 0
                )
                .Select(kvp => kvp.Key)
                .ToImmutableArray();
        }

        /// <summary>
        /// Converts host permissions to network array
        /// </summary>
        private static ImmutableArray<string> ConvertHostPermissions(
            ImmutableArray<string> allowedHosts
        )
        {
            return allowedHosts;
        }

        /// <summary>
        /// Converts script capabilities to capability strings
        /// </summary>
        private static ImmutableArray<string> ConvertCapabilitiesToStrings(
            ScriptCapabilities capabilities
        )
        {
            var result = new List<string>();

            if ((capabilities & ScriptCapabilities.ProcessExecution) != 0)
                result.Add("eval");
            if ((capabilities & ScriptCapabilities.ReflectionAccess) != 0)
                result.Add("reflection");
            if (
                (capabilities & ScriptCapabilities.FileRead) != 0
                || (capabilities & ScriptCapabilities.FileWrite) != 0
            )
                result.Add("io");
            if ((capabilities & ScriptCapabilities.NetworkAccess) != 0)
                result.Add("network");
            if ((capabilities & ScriptCapabilities.SystemInformation) != 0)
                result.Add("system");

            return result.ToImmutableArray();
        }
    }

    /// <summary>
    /// Interface for manifest validation
    /// </summary>
    public interface IManifestValidator
    {
        ManifestValidationResult Validate(Manifest manifest);
    }
}
