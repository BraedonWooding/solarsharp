using System;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents a normalized file path pattern for security rules.
    /// Immutable value type following domain-driven design principles.
    /// </summary>
    public sealed record PathPattern
    {
        /// <summary>
        /// Gets the normalized pattern string.
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Private constructor to ensure patterns are created through factory method.
        /// </summary>
        private PathPattern(string pattern)
        {
            Pattern = PathNormalizer.NormalizePath(pattern);
        }

        /// <summary>
        /// Creates a new PathPattern from a string.
        /// </summary>
        /// <param name="pattern">The path pattern (e.g., "/app/data/*", "*.lua")</param>
        /// <returns>A normalized PathPattern</returns>
        public static PathPattern Create(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Path pattern cannot be null or whitespace", nameof(pattern));

            return new PathPattern(pattern);
        }

        /// <summary>
        /// Tries to create a PathPattern, returning a Result.
        /// </summary>
        /// <param name="pattern">The path pattern to validate and create</param>
        /// <returns>Success with PathPattern or Failure with error message</returns>
        public static Result<PathPattern, string> TryCreate(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return Result.Failure<PathPattern, string>("Path pattern cannot be null or whitespace");

            try
            {
                return Result.Success<PathPattern, string>(Create(pattern));
            }
            catch (Exception ex)
            {
                return Result.Failure<PathPattern, string>($"Invalid path pattern: {ex.Message}");
            }
        }

        /// <summary>
        /// Implicit conversion to string for ease of use.
        /// </summary>
        public static implicit operator string(PathPattern pattern) => pattern?.Pattern ?? string.Empty;

        /// <summary>
        /// Checks if this pattern matches a given path.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path matches this pattern</returns>
        public bool Matches(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var normalizedPath = PathNormalizer.NormalizePath(path);
            return GlobMatcher.MatchesPattern(normalizedPath, Pattern);
        }

        /// <summary>
        /// Scopes this pattern to a specific directory.
        /// </summary>
        /// <param name="directory">The directory to scope to</param>
        /// <returns>A new PathPattern scoped to the directory</returns>
        public PathPattern ScopeToDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return this;

            var normalizedDir = PathNormalizer.NormalizePath(directory);
            
            // If pattern is already absolute (starts with /), return as-is
            if (Pattern.StartsWith("/"))
                return this;

            // Combine directory and pattern
            var scopedPattern = PathNormalizer.NormalizePath(
                System.IO.Path.Combine(normalizedDir, Pattern));
            
            return new PathPattern(scopedPattern);
        }

        /// <summary>
        /// Returns the normalized pattern string.
        /// </summary>
        public override string ToString() => Pattern;
    }
}