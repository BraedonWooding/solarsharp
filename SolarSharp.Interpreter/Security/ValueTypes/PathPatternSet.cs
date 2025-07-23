using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Immutable collection of path patterns.
    /// </summary>
    public sealed record PathPatternSet : IEnumerable<PathPattern>
    {
        private readonly ImmutableHashSet<PathPattern> _patterns;

        /// <summary>
        /// Gets the patterns in this set.
        /// </summary>
        public ImmutableHashSet<PathPattern> Patterns => _patterns;

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
        private PathPatternSet(ImmutableHashSet<PathPattern> patterns)
        {
            _patterns = patterns ?? ImmutableHashSet<PathPattern>.Empty;
        }

        /// <summary>
        /// Gets an empty PathPatternSet.
        /// </summary>
        public static PathPatternSet Empty { get; } = new(ImmutableHashSet<PathPattern>.Empty);

        /// <summary>
        /// Creates a PathPatternSet from string patterns.
        /// </summary>
        /// <param name="patterns">The path pattern strings</param>
        /// <returns>A new PathPatternSet</returns>
        public static PathPatternSet Create(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return Empty;

            var patternSet = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(PathPattern.Create)
                .ToImmutableHashSet();

            return new PathPatternSet(patternSet);
        }

        /// <summary>
        /// Creates a PathPatternSet from PathPattern objects.
        /// </summary>
        /// <param name="patterns">The PathPattern objects</param>
        /// <returns>A new PathPatternSet</returns>
        public static PathPatternSet Create(IEnumerable<PathPattern> patterns)
        {
            if (patterns == null)
                return Empty;

            var patternSet = patterns.ToImmutableHashSet();
            return patternSet.IsEmpty ? Empty : new PathPatternSet(patternSet);
        }

        /// <summary>
        /// Tries to create a PathPatternSet from strings, returning a Result.
        /// </summary>
        /// <param name="patterns">The pattern strings to validate and add</param>
        /// <returns>Success with PathPatternSet or Failure with error messages</returns>
        public static Result<PathPatternSet, string> TryCreate(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return Result.Success<PathPatternSet, string>(Empty);

            var results = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(PathPattern.TryCreate)
                .ToList();

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                var errors = string.Join("; ", failures.Select(f => f.Error));
                return Result.Failure<PathPatternSet, string>(errors);
            }

            var patternSet = results
                .Select(r => r.Value)
                .ToImmutableHashSet();

            return Result.Success<PathPatternSet, string>(new PathPatternSet(patternSet));
        }

        /// <summary>
        /// Adds a pattern to this set.
        /// </summary>
        /// <param name="pattern">The pattern string to add</param>
        /// <returns>A new PathPatternSet with the pattern added</returns>
        public PathPatternSet Add(string pattern)
        {
            var pathPattern = PathPattern.Create(pattern);
            return new PathPatternSet(_patterns.Add(pathPattern));
        }

        /// <summary>
        /// Adds a PathPattern to this set.
        /// </summary>
        /// <param name="pattern">The PathPattern to add</param>
        /// <returns>A new PathPatternSet with the pattern added</returns>
        public PathPatternSet Add(PathPattern pattern)
        {
            if (pattern == null)
                return this;

            return new PathPatternSet(_patterns.Add(pattern));
        }

        /// <summary>
        /// Adds multiple patterns to this set.
        /// </summary>
        /// <param name="patterns">The patterns to add</param>
        /// <returns>A new PathPatternSet with the patterns added</returns>
        public PathPatternSet AddRange(IEnumerable<string> patterns)
        {
            if (patterns == null)
                return this;

            var newPatterns = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(PathPattern.Create);

            return new PathPatternSet(_patterns.Union(newPatterns));
        }

        /// <summary>
        /// Removes a pattern from this set.
        /// </summary>
        /// <param name="pattern">The pattern to remove</param>
        /// <returns>A new PathPatternSet with the pattern removed</returns>
        public PathPatternSet Remove(PathPattern pattern)
        {
            if (pattern == null)
                return this;

            return new PathPatternSet(_patterns.Remove(pattern));
        }

        /// <summary>
        /// Checks if any pattern in this set matches the given path.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if any pattern matches the path</returns>
        public bool MatchesPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return _patterns.Any(pattern => pattern.Matches(path));
        }

        /// <summary>
        /// Scopes all patterns in this set to a specific directory.
        /// </summary>
        /// <param name="directory">The directory to scope to</param>
        /// <returns>A new PathPatternSet with all patterns scoped to the directory</returns>
        public PathPatternSet ScopeToDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return this;

            var scopedPatterns = _patterns
                .Select(p => p.ScopeToDirectory(directory))
                .ToImmutableHashSet();

            return new PathPatternSet(scopedPatterns);
        }

        /// <summary>
        /// Combines this set with another set.
        /// </summary>
        /// <param name="other">The other set to combine with</param>
        /// <returns>A new PathPatternSet containing patterns from both sets</returns>
        public PathPatternSet Union(PathPatternSet other)
        {
            if (other == null || other.IsEmpty)
                return this;

            if (IsEmpty)
                return other;

            return new PathPatternSet(_patterns.Union(other._patterns));
        }

        /// <summary>
        /// Creates a new set containing only patterns that exist in both sets.
        /// </summary>
        /// <param name="other">The other set to intersect with</param>
        /// <returns>A new PathPatternSet containing only common patterns</returns>
        public PathPatternSet Intersect(PathPatternSet other)
        {
            if (other == null || other.IsEmpty || IsEmpty)
                return Empty;

            var intersection = _patterns.Intersect(other._patterns);
            return intersection.IsEmpty ? Empty : new PathPatternSet(intersection);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the patterns.
        /// </summary>
        public IEnumerator<PathPattern> GetEnumerator() => _patterns.GetEnumerator();

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
                return "PathPatternSet { }";

            var patterns = string.Join(", ", _patterns.Select(p => $"\"{p}\""));
            return $"PathPatternSet {{ {patterns} }}";
        }
    }
}