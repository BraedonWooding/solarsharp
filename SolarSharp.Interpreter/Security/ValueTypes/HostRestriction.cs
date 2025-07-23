using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents host restrictions in a manifest policy.
    /// Can express patterns like "deny evil.com" or "deny all except *.example.com".
    /// </summary>
    public sealed record HostRestriction
    {
        private readonly RestrictionSet<HostPattern> _restriction;

        /// <summary>
        /// Private constructor ensures creation through factory methods.
        /// </summary>
        private HostRestriction(RestrictionSet<HostPattern> restriction)
        {
            _restriction = restriction ?? RestrictionSet<HostPattern>.None;
        }

        /// <summary>
        /// Creates an empty restriction (no hosts are restricted).
        /// </summary>
        public static HostRestriction None { get; } = new(RestrictionSet<HostPattern>.None);

        /// <summary>
        /// Creates a restriction that denies all hosts.
        /// </summary>
        public static HostRestriction DenyAll { get; } = new(RestrictionSet<HostPattern>.All);

        /// <summary>
        /// Creates a restriction that denies specific host patterns.
        /// </summary>
        public static HostRestriction DenyHosts(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return None;

            var results = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(HostPattern.Create)
                .ToList();

            // Collect any errors
            var errors = results.Where(r => r.IsFailure).Select(r => r.Error).ToList();
            if (errors.Any())
            {
                throw new ArgumentException($"Invalid host patterns: {string.Join("; ", errors)}");
            }

            var hostPatterns = results.Select(r => r.Value).ToArray();
            return new HostRestriction(RestrictionSet<HostPattern>.DenySpecific(hostPatterns));
        }

        /// <summary>
        /// Creates a restriction that denies specific host patterns.
        /// </summary>
        public static HostRestriction DenyHosts(params HostPattern[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return None;

            return new HostRestriction(RestrictionSet<HostPattern>.DenySpecific(patterns));
        }

        /// <summary>
        /// Creates a restriction that denies all hosts except those matching the specified patterns.
        /// </summary>
        public static HostRestriction DenyAllExcept(params string[] allowedPatterns)
        {
            if (allowedPatterns == null || allowedPatterns.Length == 0)
                return DenyAll;

            var results = allowedPatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(HostPattern.Create)
                .ToList();

            // Collect any errors
            var errors = results.Where(r => r.IsFailure).Select(r => r.Error).ToList();
            if (errors.Any())
            {
                throw new ArgumentException($"Invalid host patterns: {string.Join("; ", errors)}");
            }

            var hostPatterns = results.Select(r => r.Value).ToArray();
            return new HostRestriction(RestrictionSet<HostPattern>.DenyAllExcept(hostPatterns));
        }

        /// <summary>
        /// Creates a restriction that denies all hosts except those matching the specified patterns.
        /// </summary>
        public static HostRestriction DenyAllExcept(params HostPattern[] allowedPatterns)
        {
            if (allowedPatterns == null || allowedPatterns.Length == 0)
                return DenyAll;

            return new HostRestriction(RestrictionSet<HostPattern>.DenyAllExcept(allowedPatterns));
        }

        /// <summary>
        /// Creates a HostRestriction from string representations with validation.
        /// </summary>
        public static Result<HostRestriction, string> Create(bool denyAll, IEnumerable<string> patterns)
        {
            if (patterns == null)
                return Result.Success<HostRestriction, string>(None);

            var patternList = patterns.ToList();
            if (!patternList.Any())
            {
                return Result.Success<HostRestriction, string>(denyAll ? DenyAll : None);
            }

            // Validate and parse patterns
            var parsedPatterns = new List<HostPattern>();
            var errors = new List<string>();

            foreach (var pattern in patternList)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                var result = HostPattern.Create(pattern);
                if (result.IsSuccess)
                {
                    parsedPatterns.Add(result.Value);
                }
                else
                {
                    errors.Add(result.Error);
                }
            }

            if (errors.Any())
                return Result.Failure<HostRestriction, string>(string.Join("; ", errors));

            return Result.Success<HostRestriction, string>(
                denyAll ? DenyAllExcept(parsedPatterns.ToArray()) : DenyHosts(parsedPatterns.ToArray()));
        }

        /// <summary>
        /// Checks if a host is restricted.
        /// </summary>
        public bool IsRestricted(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return true; // Empty hosts are always restricted

            // For "deny specific", check if any pattern matches
            if (_restriction is DenySpecificRestriction<HostPattern> specific)
            {
                return specific.DeniedItems.Any(pattern => pattern.Matches(host));
            }

            // For "deny all except", check if NO pattern matches
            if (_restriction is DenyAllExceptRestriction<HostPattern> allExcept)
            {
                return !allExcept.AllowedItems.Any(pattern => pattern.Matches(host));
            }

            return false;
        }

        /// <summary>
        /// Checks if a host is allowed.
        /// </summary>
        public bool IsAllowed(string host) => !IsRestricted(host);

        /// <summary>
        /// Converts to allowed hosts for SecurityPolicy.
        /// For "deny specific", returns None (can't enumerate all allowed).
        /// For "deny all except", returns the allowed patterns.
        /// </summary>
        public Maybe<ImmutableArray<string>> ToAllowedHosts()
        {
            if (_restriction is DenyAllExceptRestriction<HostPattern> allExcept)
            {
                var patterns = allExcept.AllowedItems.Select(p => p.Pattern).ToImmutableArray();
                return Maybe<ImmutableArray<string>>.From(patterns);
            }

            // Can't convert "deny specific" to an allowed list
            return Maybe<ImmutableArray<string>>.None;
        }

        /// <summary>
        /// Combines this restriction with another, taking the most restrictive combination.
        /// </summary>
        public HostRestriction CombineWith(HostRestriction other)
        {
            if (other == null)
                return this;

            return new HostRestriction(_restriction.CombineWith(other._restriction));
        }

        /// <summary>
        /// Converts to a JSON-friendly representation.
        /// </summary>
        public (bool DenyAll, ImmutableArray<string> Patterns) ToJsonPattern()
        {
            var (denyAll, patterns) = _restriction.ToJsonPattern();
            var patternStrings = patterns.Select(p => p.Pattern).ToImmutableArray();
            return (denyAll, patternStrings);
        }

        /// <summary>
        /// Gets whether this restriction denies all hosts.
        /// </summary>
        public bool DeniesAll => _restriction.DeniesEverything;

        /// <summary>
        /// Gets whether this restriction denies no hosts.
        /// </summary>
        public bool DeniesNone => _restriction.DeniesNothing;

        public override string ToString() => _restriction.ToString();
    }

    /// <summary>
    /// Fluent builder extensions for HostRestriction.
    /// </summary>
    public static class HostRestrictionExtensions
    {
        /// <summary>
        /// Creates a host restriction that allows only local hosts.
        /// </summary>
        public static HostRestriction AllowOnlyLocal(this HostRestriction _) =>
            HostRestriction.DenyAllExcept("localhost", "127.0.0.1", "::1");

        /// <summary>
        /// Creates a host restriction that allows only a specific domain and its subdomains.
        /// </summary>
        public static HostRestriction AllowOnlyDomain(this HostRestriction _, string domain) =>
            HostRestriction.DenyAllExcept(domain, $"*.{domain}");

        /// <summary>
        /// Creates a host restriction that denies known tracking domains.
        /// </summary>
        public static HostRestriction DenyTrackingDomains(this HostRestriction _) =>
            HostRestriction.DenyHosts(
                "*.google-analytics.com",
                "*.doubleclick.net",
                "*.facebook.com",
                "*.twitter.com");

        /// <summary>
        /// Creates a host restriction that allows only HTTPS-capable hosts (by convention).
        /// </summary>
        public static HostRestriction AllowOnlySecureHosts(this HostRestriction _, params string[] trustedHosts) =>
            HostRestriction.DenyAllExcept(trustedHosts);
    }
}