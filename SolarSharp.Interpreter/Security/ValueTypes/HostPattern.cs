using System;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents a network host pattern for security rules.
    /// Immutable value type following domain-driven design principles.
    /// </summary>
    public sealed record HostPattern
    {
        private static readonly Regex ValidHostPattern = new Regex(
            @"^(\*\.)?([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?$|^\*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets the normalized host pattern string.
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Private constructor to ensure patterns are created through factory methods.
        /// </summary>
        private HostPattern(string pattern)
        {
            Pattern = pattern.ToLowerInvariant();
        }

        /// <summary>
        /// Creates a HostPattern from a string, validating the format.
        /// </summary>
        /// <param name="pattern">The host pattern (e.g., "example.com", "*.example.com", "*")</param>
        /// <returns>A Result containing the HostPattern or error message</returns>
        public static Result<HostPattern, string> Create(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return Result.Failure<HostPattern, string>("Host pattern cannot be null or whitespace");

            var trimmed = pattern.Trim();

            // Basic validation
            if (trimmed.Length > 255)
                return Result.Failure<HostPattern, string>("Host pattern exceeds maximum length of 255 characters");

            // Check for invalid characters
            if (trimmed.Contains("..") || trimmed.StartsWith(".") || trimmed.EndsWith("."))
                return Result.Failure<HostPattern, string>("Host pattern contains invalid dot placement");

            // Allow wildcard patterns
            if (!ValidHostPattern.IsMatch(trimmed))
                return Result.Failure<HostPattern, string>($"Invalid host pattern format: '{pattern}'");

            return Result.Success<HostPattern, string>(new HostPattern(trimmed));
        }

        /// <summary>
        /// Checks if this pattern matches a given host.
        /// </summary>
        /// <param name="host">The host to check</param>
        /// <returns>True if the host matches this pattern</returns>
        public bool Matches(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            var normalizedHost = host.ToLowerInvariant();

            // Exact match
            if (Pattern == normalizedHost)
                return true;

            // Wildcard match "*"
            if (Pattern == "*")
                return true;

            // Subdomain wildcard match "*.example.com"
            if (Pattern.StartsWith("*."))
            {
                var domain = Pattern.Substring(2);
                
                // Check if host ends with the domain
                if (normalizedHost.EndsWith(domain))
                {
                    // Ensure it's a proper subdomain match
                    var prefixLength = normalizedHost.Length - domain.Length;
                    return prefixLength == 0 || normalizedHost[prefixLength - 1] == '.';
                }
            }

            return false;
        }

        /// <summary>
        /// Implicit conversion to string for ease of use.
        /// </summary>
        public static implicit operator string(HostPattern pattern) => pattern?.Pattern ?? string.Empty;

        /// <summary>
        /// Returns the host pattern string.
        /// </summary>
        public override string ToString() => Pattern;

        /// <summary>
        /// Checks if this is a wildcard pattern.
        /// </summary>
        public bool IsWildcard => Pattern == "*" || Pattern.StartsWith("*.");

        /// <summary>
        /// Gets the domain part of a wildcard pattern (e.g., "example.com" from "*.example.com").
        /// </summary>
        public Maybe<string> GetDomain()
        {
            if (Pattern.StartsWith("*."))
                return Maybe<string>.From(Pattern.Substring(2));

            if (Pattern == "*")
                return Maybe<string>.None;

            return Maybe<string>.From(Pattern);
        }
    }
}