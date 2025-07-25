using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Immutable collection of host patterns.
    /// </summary>
    public sealed record HostPatternSet : IEnumerable<HostPattern>
    {
        private readonly ImmutableHashSet<HostPattern> _patterns;

        /// <summary>
        /// Gets the patterns in this set.
        /// </summary>
        public ImmutableHashSet<HostPattern> Patterns => _patterns;

        /// <summary>
        /// Gets the number of patterns in the set.
        /// </summary>
        public int Count => _patterns.Count;

        /// <summary>
        /// Gets whether the set is empty.
        /// </summary>
        public bool IsEmpty => _patterns.IsEmpty;

        /// <summary>
        /// Private constructor ensures creation through factory methods.
        /// </summary>
        private HostPatternSet(ImmutableHashSet<HostPattern> patterns)
        {
            _patterns = patterns ?? ImmutableHashSet<HostPattern>.Empty;
        }

        /// <summary>
        /// Gets an empty HostPatternSet.
        /// </summary>
        public static HostPatternSet Empty { get; } = new(ImmutableHashSet<HostPattern>.Empty);

        /// <summary>
        /// Creates a HostPatternSet from string patterns.
        /// </summary>
        /// <param name="patterns">The host pattern strings</param>
        /// <returns>A Result containing the HostPatternSet or error messages</returns>
        public static Result<HostPatternSet, string> Create(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return Result.Success<HostPatternSet, string>(Empty);

            var results = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(HostPattern.Create)
                .ToList();

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                var errors = string.Join("; ", failures.Select(f => f.Error));
                return Result.Failure<HostPatternSet, string>(errors);
            }

            var patternSet = results
                .Select(r => r.Value)
                .ToImmutableHashSet();

            return Result.Success<HostPatternSet, string>(new HostPatternSet(patternSet));
        }

        /// <summary>
        /// Creates a HostPatternSet from HostPattern objects.
        /// </summary>
        /// <param name="patterns">The HostPattern objects</param>
        /// <returns>A new HostPatternSet</returns>
        public static HostPatternSet Create(IEnumerable<HostPattern> patterns)
        {
            if (patterns == null)
                return Empty;

            var patternSet = patterns.ToImmutableHashSet();
            return patternSet.IsEmpty ? Empty : new HostPatternSet(patternSet);
        }

        /// <summary>
        /// Creates a HostPatternSet from strings, ignoring invalid patterns.
        /// </summary>
        /// <param name="patterns">The pattern strings</param>
        /// <returns>A new HostPatternSet containing only valid patterns</returns>
        public static HostPatternSet CreateIgnoringInvalid(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return Empty;

            var validPatterns = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(HostPattern.Create)
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToImmutableHashSet();

            return validPatterns.IsEmpty ? Empty : new HostPatternSet(validPatterns);
        }

        /// <summary>
        /// Adds a pattern to this set.
        /// </summary>
        /// <param name="pattern">The pattern string to add</param>
        /// <returns>A Result containing the new HostPatternSet or error message</returns>
        public Result<HostPatternSet, string> Add(string pattern)
        {
            return HostPattern.Create(pattern)
                .Map(hostPattern => new HostPatternSet(_patterns.Add(hostPattern)));
        }

        /// <summary>
        /// Adds a HostPattern to this set.
        /// </summary>
        /// <param name="pattern">The HostPattern to add</param>
        /// <returns>A new HostPatternSet with the pattern added</returns>
        public HostPatternSet Add(HostPattern pattern)
        {
            if (pattern == null)
                return this;

            return new HostPatternSet(_patterns.Add(pattern));
        }

        /// <summary>
        /// Adds multiple patterns to this set.
        /// </summary>
        /// <param name="patterns">The patterns to add</param>
        /// <returns>A Result containing the new HostPatternSet or error messages</returns>
        public Result<HostPatternSet, string> AddRange(IEnumerable<string> patterns)
        {
            if (patterns == null)
                return Result.Success<HostPatternSet, string>(this);

            var results = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(HostPattern.Create)
                .ToList();

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                var errors = string.Join("; ", failures.Select(f => f.Error));
                return Result.Failure<HostPatternSet, string>(errors);
            }

            var newPatterns = results.Select(r => r.Value);
            return Result.Success<HostPatternSet, string>(
                new HostPatternSet(_patterns.Union(newPatterns)));
        }

        /// <summary>
        /// Removes a pattern from this set.
        /// </summary>
        /// <param name="pattern">The pattern to remove</param>
        /// <returns>A new HostPatternSet with the pattern removed</returns>
        public HostPatternSet Remove(HostPattern pattern)
        {
            if (pattern == null)
                return this;

            return new HostPatternSet(_patterns.Remove(pattern));
        }

        /// <summary>
        /// Checks if any pattern in this set matches the given host.
        /// </summary>
        /// <param name="host">The host to check</param>
        /// <returns>True if any pattern matches the host</returns>
        public bool MatchesHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            return _patterns.Any(pattern => pattern.Matches(host));
        }

        /// <summary>
        /// Combines this set with another set.
        /// </summary>
        /// <param name="other">The other set to combine with</param>
        /// <returns>A new HostPatternSet containing patterns from both sets</returns>
        public HostPatternSet Union(HostPatternSet other)
        {
            if (other == null || other.IsEmpty)
                return this;

            if (IsEmpty)
                return other;

            return new HostPatternSet(_patterns.Union(other._patterns));
        }

        /// <summary>
        /// Creates a new set containing only patterns that exist in both sets.
        /// </summary>
        /// <param name="other">The other set to intersect with</param>
        /// <returns>A new HostPatternSet containing only common patterns</returns>
        public HostPatternSet Intersect(HostPatternSet other)
        {
            if (other == null || other.IsEmpty || IsEmpty)
                return Empty;

            var intersection = _patterns.Intersect(other._patterns);
            return intersection.IsEmpty ? Empty : new HostPatternSet(intersection);
        }

        /// <summary>
        /// Gets all wildcard patterns in this set.
        /// </summary>
        /// <returns>A new HostPatternSet containing only wildcard patterns</returns>
        public HostPatternSet GetWildcardPatterns()
        {
            var wildcards = _patterns.Where(p => p.IsWildcard).ToImmutableHashSet();
            return wildcards.IsEmpty ? Empty : new HostPatternSet(wildcards);
        }

        /// <summary>
        /// Gets all non-wildcard (exact) patterns in this set.
        /// </summary>
        /// <returns>A new HostPatternSet containing only exact patterns</returns>
        public HostPatternSet GetExactPatterns()
        {
            var exact = _patterns.Where(p => !p.IsWildcard).ToImmutableHashSet();
            return exact.IsEmpty ? Empty : new HostPatternSet(exact);
        }

        /// <summary>
        /// Converts denied hosts to allowed hosts format for SecurityPolicy.
        /// This implements Option C: for each denied host, create an allowed list that excludes it.
        /// </summary>
        /// <returns>A set of allowed hosts that excludes the denied patterns</returns>
        public Maybe<HostPatternSet> ConvertToAllowedHosts()
        {
            // If empty, all hosts are allowed
            if (IsEmpty)
                return Maybe<HostPatternSet>.None;

            // If "*" is denied, no hosts are allowed
            if (_patterns.Any(p => p.Pattern == "*"))
                return Maybe<HostPatternSet>.From(Empty);

            // For specific denials, we need to maintain the denial list
            // and check at runtime (can't easily invert wildcards)
            return Maybe<HostPatternSet>.None;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the patterns.
        /// </summary>
        public IEnumerator<HostPattern> GetEnumerator() => _patterns.GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through the patterns.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns a string representation of the pattern set.
        /// </summary>
        public override string ToString()
        {
            if (IsEmpty)
                return "HostPatternSet { }";

            var patterns = string.Join(", ", _patterns.Select(p => $"\"{p}\""));
            return $"HostPatternSet {{ {patterns} }}";
        }
    }
}