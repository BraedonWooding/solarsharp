using System;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Operations
{
    /// <summary>
    /// Operations for working with PolicySet instances
    /// </summary>
    public static class PolicySetOperations
    {
        /// <summary>
        /// Resolves the effective security policy for a given file path
        /// </summary>
        public static Result<SecurityPolicy, string> ResolvePolicy(
            this PolicySet policySet,
            string filePath
        )
        {
            var policyNameMaybe = FindBestPatternMatch(policySet, filePath);

            var policyName = policyNameMaybe.HasValue
                ? policyNameMaybe.Value
                : policySet.FallbackPolicyName;

            return GetPolicyByName(policySet, policyName);
        }

        /// <summary>
        /// Gets a policy by name from the definitions
        /// </summary>
        public static Result<SecurityPolicy, string> GetPolicyByName(
            this PolicySet policySet,
            string name
        )
        {
            return policySet.PolicyDefinitions.TryGetValue(name, out var policy)
                ? Result.Success<SecurityPolicy, string>(policy)
                : Result.Failure<SecurityPolicy, string>(
                    $"Policy '{name}' not found in policy definitions"
                );
        }

        /// <summary>
        /// Finds all pattern matches for a file path
        /// TODO: This will be used for runtime composition with mutual exclusion validation
        /// </summary>
        public static ImmutableArray<(string Pattern, string PolicyName)> FindAllPatternMatches(
            this PolicySet policySet,
            string filePath
        )
        {
            return policySet
                .FilePolicies.Where(kvp => IsPatternMatch(filePath, kvp.Key))
                .Select(kvp => (Pattern: kvp.Key, PolicyName: kvp.Value))
                .ToImmutableArray();
        }

        /// <summary>
        /// Finds the best pattern match for a file path (temporary compatibility method)
        /// </summary>
        public static Maybe<string> FindBestPatternMatch(this PolicySet policySet, string filePath)
        {
            var matches = FindAllPatternMatches(policySet, filePath);

            // For now, just return the first match if any exist
            // This maintains backward compatibility while we prepare for runtime composition
            return matches.Any()
                ? Maybe<string>.From(matches.First().PolicyName)
                : Maybe<string>.None;
        }

        /// <summary>
        /// Resolves the policy name that would apply to a given pattern
        /// </summary>
        public static Result<string, PolicyValidationError> ResolvePolicyNameForPattern(
            this PolicySet policySet,
            string scopePattern
        )
        {
            var policyNameMaybe = FindBestPatternMatch(policySet, scopePattern);

            return policyNameMaybe.HasValue
                ? Result.Success<string, PolicyValidationError>(policyNameMaybe.Value)
                : Result.Success<string, PolicyValidationError>(policySet.FallbackPolicyName);
        }

        /// <summary>
        /// Creates a new PolicySet with an additional policy definition
        /// </summary>
        public static PolicySet WithPolicyDefinition(
            this PolicySet policySet,
            string name,
            SecurityPolicy policy
        )
        {
            return policySet with
            {
                PolicyDefinitions = policySet.PolicyDefinitions.SetItem(name, policy),
            };
        }

        /// <summary>
        /// Creates a new PolicySet with an additional file policy mapping
        /// </summary>
        public static PolicySet WithFilePolicy(
            this PolicySet policySet,
            string pattern,
            string policyName
        )
        {
            return policySet with
            {
                FilePolicies = policySet.FilePolicies.SetItem(pattern, policyName),
            };
        }

        /// <summary>
        /// Creates a new PolicySet with a different default policy name
        /// </summary>
        public static PolicySet WithDefaultPolicy(this PolicySet policySet, string policyName)
        {
            return policySet with { FallbackPolicyName = policyName };
        }

        /// <summary>
        /// Checks if a file path matches a pattern
        /// </summary>
        private static bool IsPatternMatch(string filePath, string pattern)
        {
            // Policy pattern matching rules:
            // - * matches everything (including eval contexts)
            // - *:* matches everything (alternative syntax)
            // - *:eval matches all eval contexts (string execution and eval inside scripts)
            // - *:file matches all file operations but excludes eval
            // - *.ext matches files with that extension
            // - *.ext:eval matches eval contexts for files with that extension

            // Handle universal patterns first
            if (pattern == "*" || pattern == "*:*")
                return true;

            // Handle eval-specific patterns
            if (pattern == "*:eval")
                return filePath.Contains(":eval");

            // Handle file-only patterns (excludes eval)
            if (pattern == "*:file")
                return !filePath.Contains(":eval");

            // Check for context-specific patterns (e.g., "*.lua:eval")
            if (pattern.Contains(":"))
            {
                var parts = pattern.Split(':', 2);
                var filePattern = parts[0];
                var context = parts[1];

                // Handle filePath that might start with : (like ":eval")
                string baseFilePath;
                bool hasFileContext;
                if (filePath.StartsWith(":"))
                {
                    // File path like ":eval" - no file part, just context
                    baseFilePath = "";
                    hasFileContext = true;
                }
                else if (filePath.Contains(":"))
                {
                    // File path like "script.lua:eval" - extract file part
                    baseFilePath = filePath.Split(':', 2)[0];
                    hasFileContext = true;
                }
                else
                {
                    // File path like "script.lua" - no context
                    baseFilePath = filePath;
                    hasFileContext = false;
                }

                // Check if the file part matches
                var fileMatches = false;
                if (filePattern == "*")
                {
                    fileMatches = true;
                }
                else if (filePattern == "" && baseFilePath == "")
                {
                    // Both pattern and filepath have no file part (e.g., pattern ":eval" matches path ":eval")
                    fileMatches = true;
                }
                else if (filePattern.StartsWith("*"))
                {
                    var extension = filePattern.Substring(1);
                    fileMatches = baseFilePath.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase
                    );
                }
                else if (filePattern.EndsWith("/*"))
                {
                    var directory = filePattern.Substring(0, filePattern.Length - 2);
                    fileMatches = baseFilePath.StartsWith(
                        directory + "/",
                        StringComparison.OrdinalIgnoreCase
                    );
                }
                else
                {
                    fileMatches = baseFilePath.Equals(
                        filePattern,
                        StringComparison.OrdinalIgnoreCase
                    );
                }

                // Check if the context matches
                var contextMatches = false;
                if (context == "*")
                {
                    contextMatches = true;
                }
                else if (context == "eval")
                {
                    contextMatches = filePath.Contains(":eval") || filePath == ":eval";
                }
                else if (context == "file")
                {
                    contextMatches = !hasFileContext;
                }
                else
                {
                    contextMatches =
                        filePath.EndsWith(":" + context, StringComparison.OrdinalIgnoreCase)
                        || filePath == ":" + context;
                }

                return fileMatches && contextMatches;
            }

            // Check extension patterns like *.lua (no context specified, matches files without context)
            if (pattern.StartsWith("*"))
            {
                var extension = pattern.Substring(1);
                // Only match if there's no context (i.e., no ":" in filePath)
                return !filePath.Contains(":")
                    && filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.EndsWith("/*"))
            {
                var directory = pattern.Substring(0, pattern.Length - 2);
                // Only match if there's no context
                return !filePath.Contains(":")
                    && filePath.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase);
            }

            // Exact match (no context)
            return !filePath.Contains(":")
                && filePath.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
