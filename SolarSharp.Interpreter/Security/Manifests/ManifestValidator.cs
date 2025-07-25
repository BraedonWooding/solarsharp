using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// DEPRECATED: Legacy V1 manifest validator - needs complete rewrite for V2.0
    /// TODO: Replace with V2.0 manifest validation logic using signed content blocks
    /// </summary>
    [Obsolete("Legacy V1 manifest validator - needs complete rewrite for V2.0")]
    public class ManifestValidator : IManifestValidator
    {
        /// <summary>
        /// Validates that a manifest is complete and well-formed
        /// </summary>
        /// <param name="manifest">The manifest to validate</param>
        /// <returns>Validation result with any errors found</returns>
        public ManifestValidationResult Validate(Manifest manifest)
        {
            return ValidateManifest(manifest);
        }

        /// <summary>
        /// Static validation method for backward compatibility
        /// </summary>
        public static ManifestValidationResult ValidateManifest(Manifest manifest)
        {
            if (manifest == null)
                return ManifestValidationResult.Failure("Manifest cannot be null");

            var errors = new List<string>();

            // TODO: V2.0 - Rewrite to validate signed content blocks
            // For now, just check basic V2.0 structure
            if (manifest.Version != "2.0")
            {
                errors.Add("Only V2.0 manifests are supported");
            }

            if (!manifest.HasSignedContent && manifest.SignedContent.Length == 0)
            {
                errors.Add("V2.0 manifest should have signed content blocks");
            }

            // Validate packages in signed content blocks
            if (manifest.HasSignedContent)
            {
                foreach (var block in manifest.SignedContent)
                {
                    foreach (var (packageId, package) in block.Packages)
                    {
                        // Validate package metadata
                        if (string.IsNullOrEmpty(package.Metadata.Name))
                        {
                            errors.Add($"Package '{packageId}' must have a non-empty name");
                        }

                        if (string.IsNullOrEmpty(package.Metadata.Version))
                        {
                            errors.Add($"Package '{packageId}' must have a non-empty version");
                        }
                    }
                }
            }

            return errors.Any()
                ? ManifestValidationResult.Failure(errors)
                : ManifestValidationResult.Success();
        }

        /// <summary>
        /// Validates a single manifest rule
        /// </summary>
        /// <param name="scope">The scope pattern</param>
        /// <param name="rule">The rule to validate</param>
        /// <returns>Validation result</returns>
        public static ManifestValidationResult ValidateRule(string scope, ManifestRule rule)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(scope))
                errors.Add("Scope cannot be null or empty");

            if (rule == null)
            {
                errors.Add("Rule cannot be null");
                return ManifestValidationResult.Failure(errors);
            }

            if (string.IsNullOrEmpty(rule.Scope))
                errors.Add("Rule.Scope cannot be null or empty");
            else if (rule.Scope != scope)
                errors.Add($"Rule.Scope '{rule.Scope}' does not match key '{scope}'");

            if (rule.Value == null)
                errors.Add("Rule.Value cannot be null");

            // Validate based on target type
            switch (rule.Target)
            {
                case RuleTarget.File:
                    if (rule.Value is string fileAccess && !IsValidFileAccessValue(fileAccess))
                        errors.Add($"Invalid file access value '{fileAccess}'");
                    break;

                case RuleTarget.Action:
                    if (rule.Value is string action && !IsValidActionValue(action))
                        errors.Add($"Invalid action value '{action}'");
                    break;

                case RuleTarget.Resource:
                    if (rule.Value is not (int or long or double or string))
                        errors.Add("Resource rules must have numeric or string values");
                    break;

                case RuleTarget.Capability:
                    if (rule.Value is string capability && !IsValidCapabilityValue(capability))
                        errors.Add($"Invalid capability value '{capability}'");
                    break;

                case RuleTarget.Module:
                    if (rule.Value is string module && !IsValidModuleValue(module))
                        errors.Add($"Invalid module value '{module}'");
                    break;
            }

            return errors.Any()
                ? ManifestValidationResult.Failure(errors)
                : ManifestValidationResult.Success();
        }

        private static bool IsValidFilePermission(FilePermissions permission)
        {
            return Enum.IsDefined(typeof(FilePermissions), permission);
        }

        private static bool IsValidDirectoryPermission(DirectoryPermissions permission)
        {
            return Enum.IsDefined(typeof(DirectoryPermissions), permission);
        }

        private static bool IsValidFileAccessValue(string value)
        {
            return Enum.TryParse<FilePermissions>(value, true, out _);
        }

        private static bool IsValidDirectoryAccessValue(string value)
        {
            return Enum.TryParse<DirectoryPermissions>(value, true, out _);
        }

        private static bool IsValidActionValue(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "allow"
                or "deny"
                or "execute"
                or "modify"
                or "read"
                or "write"
                or "delete"
                or "create" => true,
                _ => false,
            };
        }

        private static bool IsValidCapabilityValue(string value)
        {
            // For now, accept any non-empty string as capabilities are extensible
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsValidModuleValue(string value)
        {
            // Validate against known CoreModules
            return Enum.TryParse<CoreModules>(value, true, out _)
                || !string.IsNullOrWhiteSpace(value); // Allow custom modules
        }
    }

    /// <summary>
    /// Result of manifest validation
    /// </summary>
    public class ManifestValidationResult
    {
        public bool IsValid { get; private set; }
        public List<string> Errors { get; private set; } = new List<string>();

        private ManifestValidationResult(bool isValid, IEnumerable<string> errors = null)
        {
            IsValid = isValid;
            if (errors != null)
                Errors.AddRange(errors);
        }

        public static ManifestValidationResult Success() => new ManifestValidationResult(true);

        public static ManifestValidationResult Failure(string error) =>
            new ManifestValidationResult(false, new[] { error });

        public static ManifestValidationResult Failure(IEnumerable<string> errors) =>
            new ManifestValidationResult(false, errors);

        public override string ToString()
        {
            return IsValid ? "Valid" : $"Invalid: {string.Join(", ", Errors)}";
        }
    }
}
