using System;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Validates manifests for completeness and correctness
    /// </summary>
    public static class ManifestValidator
    {
        /// <summary>
        /// Validates that a manifest meets the requirements to be a SystemManifest
        /// </summary>
        /// <param name="manifest">The manifest to validate</param>
        /// <returns>Validation result with any errors found</returns>
        public static ManifestValidationResult ValidateSystemManifest(Manifest manifest)
        {
            if (manifest == null)
                return ManifestValidationResult.Failure("Manifest cannot be null");

            var errors = new List<string>();

            // Special handling for None SystemManifest
            if (manifest is SystemManifest systemManifest && systemManifest._isNoneManifest)
            {
                // None SystemManifest should have no policy and no rules - denial is implicit
                if (manifest.Policy != null)
                    errors.Add("None SystemManifest must not have a policy - denial is implicit");
                    
                if (manifest.Rules.Any())
                    errors.Add("None SystemManifest must not have rules - denial is implicit");
                    
                return errors.Any() ? ManifestValidationResult.Failure(errors) : ManifestValidationResult.Success();
            }

            // For all other SystemManifests, validate policy exists
            if (manifest.Policy == null)
            {
                errors.Add("Policy must be specified for SystemManifest");
                return ManifestValidationResult.Failure(errors);
            }

            // Check that essential resource limits are defined
            if (manifest.Policy.TimeoutMs == null)
                errors.Add("TimeoutMs must be specified for SystemManifest");
            else if (manifest.Policy.TimeoutMs <= 0 && manifest.Policy.TimeoutMs != -1)
                errors.Add("TimeoutMs must be positive or -1 for no limit");

            if (manifest.Policy.MaxMemoryMB == null)
                errors.Add("MaxMemoryMB must be specified for SystemManifest");
            else if (manifest.Policy.MaxMemoryMB <= 0 && manifest.Policy.MaxMemoryMB != -1)
                errors.Add("MaxMemoryMB must be positive or -1 for no limit");

            if (manifest.Policy.MaxInstructions == null)
                errors.Add("MaxInstructions must be specified for SystemManifest");
            else if (manifest.Policy.MaxInstructions <= 0 && manifest.Policy.MaxInstructions != -1)
                errors.Add("MaxInstructions must be positive or -1 for no limit");

            // Check that file access policy is defined
            if (string.IsNullOrEmpty(manifest.Policy.DefaultFileAccess))
                errors.Add("DefaultFileAccess must be specified for SystemManifest");
            else if (!IsValidFileAccessValue(manifest.Policy.DefaultFileAccess))
                errors.Add($"DefaultFileAccess '{manifest.Policy.DefaultFileAccess}' is not a valid value");

            if (string.IsNullOrEmpty(manifest.Policy.DefaultDirectoryAccess))
                errors.Add("DefaultDirectoryAccess must be specified for SystemManifest");
            else if (!IsValidDirectoryAccessValue(manifest.Policy.DefaultDirectoryAccess))
                errors.Add($"DefaultDirectoryAccess '{manifest.Policy.DefaultDirectoryAccess}' is not a valid value");

            // Anti-polymorphism is implemented through specific rules, not a policy boolean

            // SystemManifest must have some form of access control defined
            if (!manifest.Rules.Any() && 
                string.IsNullOrEmpty(manifest.Policy.DefaultFileAccess) && 
                string.IsNullOrEmpty(manifest.Policy.DefaultDirectoryAccess))
            {
                errors.Add("SystemManifest must have at least one rule or default access policies");
            }

            // Validate individual rules
            foreach (var rule in manifest.Rules)
            {
                var ruleValidation = ValidateRule(rule.Key, rule.Value);
                if (!ruleValidation.IsValid)
                {
                    errors.AddRange(ruleValidation.Errors.Select(e => $"Rule '{rule.Key}': {e}"));
                }
            }

            // Validate file permissions if specified
            if (manifest.Policy.FilePermissions != null)
            {
                foreach (var permission in manifest.Policy.FilePermissions)
                {
                    if (!IsValidFileAccessValue(permission.Value))
                        errors.Add($"File permission '{permission.Key}' has invalid access value '{permission.Value}'");
                }
            }

            // Validate directory permissions if specified
            if (manifest.Policy.DirectoryPermissions != null)
            {
                foreach (var permission in manifest.Policy.DirectoryPermissions)
                {
                    if (!IsValidDirectoryAccessValue(permission.Value))
                        errors.Add($"Directory permission '{permission.Key}' has invalid access value '{permission.Value}'");
                }
            }

            return errors.Any() ? ManifestValidationResult.Failure(errors) : ManifestValidationResult.Success();
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

            return errors.Any() ? ManifestValidationResult.Failure(errors) : ManifestValidationResult.Success();
        }

        /// <summary>
        /// Validates all system manifests
        /// </summary>
        /// <returns>Dictionary of validation results keyed by manifest name</returns>
        public static Dictionary<string, ManifestValidationResult> ValidateAllSystemManifests()
        {
            var results = new Dictionary<string, ManifestValidationResult>();

            // Get all system manifest properties using reflection
            var systemManifestType = typeof(SystemManifest);
            var manifestProperties = systemManifestType.GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(SystemManifest));

            foreach (var property in manifestProperties)
            {
                try
                {
                    var manifest = (SystemManifest)property.GetValue(null);
                    results[property.Name] = ValidateSystemManifest(manifest);
                }
                catch (Exception ex)
                {
                    results[property.Name] = ManifestValidationResult.Failure($"Exception accessing {property.Name}: {ex.Message}");
                }
            }

            return results;
        }

        private static bool IsValidFileAccessValue(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "none" or "read" or "readwrite" or "sandboxedreadwrite" => true,
                _ => false
            };
        }

        private static bool IsValidDirectoryAccessValue(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "none" or "list" or "listandcreatefiles" => true,
                _ => false
            };
        }

        private static bool IsValidActionValue(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "allow" or "deny" or "execute" or "modify" or "read" or "write" or "delete" or "create" => true,
                _ => false
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
            return Enum.TryParse<Modules.CoreModules>(value, true, out _) || 
                   !string.IsNullOrWhiteSpace(value); // Allow custom modules
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