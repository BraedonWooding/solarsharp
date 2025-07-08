using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Comprehensive FluentValidation validator for PolicySet instances.
    /// Enforces security requirements: unique policy scopes, no directory overlaps,
    /// no digest-based file policies, and referential integrity.
    /// </summary>
    public sealed class BasePolicySetValidator : AbstractValidator<PolicySet>
    {
        /// <summary>
        /// Regex pattern for detecting digest-based file policies (SHA256, MD5, etc.)
        /// </summary>
        private static readonly Regex DigestPattern = new Regex(
            @"^[a-fA-F0-9]{32,128}$|sha256:|md5:|sha1:",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Reserved characters that are not allowed in policy names for security reasons
        /// </summary>
        private static readonly char[] ReservedPolicyNameChars =
        {
            ':',
            ';',
            '|',
            '&',
            '$',
            '`',
            '\n',
            '\r',
            '\t',
        };

        public BasePolicySetValidator()
        {
            RuleFor(ps => ps.PolicyDefinitions)
                .NotNull()
                .WithMessage("PolicyDefinitions cannot be null");

            RuleFor(ps => ps.FilePolicies).NotNull().WithMessage("FilePolicies cannot be null");

            RuleFor(ps => ps.FallbackPolicyName)
                .NotEmpty()
                .WithMessage("FallbackPolicyName cannot be null or empty")
                .Must(BeValidPolicyName)
                .WithMessage("FallbackPolicyName contains reserved characters");

            // Validate that fallback policy exists in definitions
            RuleFor(ps => ps)
                .Must(HaveFallbackPolicyInDefinitions)
                .WithMessage("FallbackPolicyName must reference an existing policy definition");

            // Validate policy names in definitions
            RuleForEach(ps => ps.PolicyDefinitions)
                .Must(kvp => BeValidPolicyName(kvp.Key))
                .WithMessage("Policy name '{PropertyValue}' contains reserved characters");

            // Validate that all file policies reference existing policy definitions
            RuleFor(ps => ps)
                .Must(HaveValidFilePolicyReferences)
                .WithMessage("All file policies must reference existing policy definitions");

            // Validate no digest-based file policies
            RuleForEach(ps => ps.FilePolicies)
                .Must(kvp => !IsDigestBasedPattern(kvp.Key))
                .WithMessage("Digest-based file policies are not allowed for security reasons");

            // Validate no overlapping directory patterns
            RuleFor(ps => ps.FilePolicies.Keys)
                .Must(patterns => !HasOverlappingDirectoryPatterns(patterns))
                .WithMessage("File patterns cannot have overlapping directory scopes");

            // Validate only one policy per scope
            RuleFor(ps => ps.FilePolicies.Keys)
                .Must(patterns => !HasDuplicateScopes(patterns))
                .WithMessage("Only one policy allowed per scope pattern");
        }

        /// <summary>
        /// Validates that policy name doesn't contain reserved characters
        /// </summary>
        private static bool BeValidPolicyName(string policyName)
        {
            if (string.IsNullOrWhiteSpace(policyName))
                return false;

            return !policyName.Any(c => ReservedPolicyNameChars.Contains(c));
        }

        /// <summary>
        /// Validates that the fallback policy exists in policy definitions
        /// </summary>
        private static bool HaveFallbackPolicyInDefinitions(PolicySet policySet)
        {
            return policySet.PolicyDefinitions.ContainsKey(policySet.FallbackPolicyName);
        }

        /// <summary>
        /// Validates that all file policies reference existing policy definitions
        /// </summary>
        private static bool HaveValidFilePolicyReferences(PolicySet policySet)
        {
            return policySet.FilePolicies.Values.All(policyName =>
                policySet.PolicyDefinitions.ContainsKey(policyName)
            );
        }

        /// <summary>
        /// Detects digest-based file patterns which are security-sensitive
        /// </summary>
        private static bool IsDigestBasedPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return false;

            // Check for hex digest patterns or explicit digest prefixes
            return DigestPattern.IsMatch(pattern);
        }

        /// <summary>
        /// Validates that directory patterns don't overlap in a way that could cause conflicts
        /// </summary>
        private static bool HasOverlappingDirectoryPatterns(IEnumerable<string> patterns)
        {
            var directoryPatterns = patterns.Where(p => p.Contains("/")).ToList();

            for (int i = 0; i < directoryPatterns.Count; i++)
            {
                for (int j = i + 1; j < directoryPatterns.Count; j++)
                {
                    if (DirectoryPatternsOverlap(directoryPatterns[i], directoryPatterns[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if two directory patterns overlap in a conflicting way
        /// </summary>
        private static bool DirectoryPatternsOverlap(string pattern1, string pattern2)
        {
            // Normalize patterns by removing wildcard suffixes
            var dir1 = pattern1.TrimEnd('*', '/');
            var dir2 = pattern2.TrimEnd('*', '/');

            // Check if one is a prefix of the other (indicating overlap)
            return dir1.StartsWith(dir2 + "/", StringComparison.OrdinalIgnoreCase)
                || dir2.StartsWith(dir1 + "/", StringComparison.OrdinalIgnoreCase)
                || dir1.Equals(dir2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates that there are no duplicate scope patterns
        /// </summary>
        private static bool HasDuplicateScopes(IEnumerable<string> patterns)
        {
            var normalizedPatterns = patterns.Select(NormalizePattern).ToList();

            return normalizedPatterns.Count
                != normalizedPatterns.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        /// <summary>
        /// Normalizes a pattern for scope comparison
        /// </summary>
        private static string NormalizePattern(string pattern)
        {
            return pattern.Trim().ToLowerInvariant();
        }
    }
}
