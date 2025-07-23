using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents path restrictions in a manifest policy.
    /// Can express patterns like "deny /etc/*" or "deny all except /app/data/*".
    /// </summary>
    public sealed record PathRestriction
    {
        private readonly RestrictionSet<PathPattern> _restriction;

        /// <summary>
        /// Private constructor ensures creation through factory methods.
        /// </summary>
        private PathRestriction(RestrictionSet<PathPattern> restriction)
        {
            _restriction = restriction ?? RestrictionSet<PathPattern>.None;
        }

        /// <summary>
        /// Creates an empty restriction (no paths are restricted).
        /// </summary>
        public static PathRestriction None { get; } = new(RestrictionSet<PathPattern>.None);

        /// <summary>
        /// Creates a restriction that denies all paths.
        /// </summary>
        public static PathRestriction DenyAll { get; } = new(RestrictionSet<PathPattern>.All);

        /// <summary>
        /// Creates a restriction that denies specific path patterns.
        /// </summary>
        public static PathRestriction DenyPaths(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return None;

            var pathPatterns = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(PathPattern.Create)
                .ToArray();

            return new PathRestriction(RestrictionSet<PathPattern>.DenySpecific(pathPatterns));
        }

        /// <summary>
        /// Creates a restriction that denies specific path patterns.
        /// </summary>
        public static PathRestriction DenyPaths(params PathPattern[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return None;

            return new PathRestriction(RestrictionSet<PathPattern>.DenySpecific(patterns));
        }

        /// <summary>
        /// Creates a restriction that denies all paths except those matching the specified patterns.
        /// </summary>
        public static PathRestriction DenyAllExcept(params string[] allowedPatterns)
        {
            if (allowedPatterns == null || allowedPatterns.Length == 0)
                return DenyAll;

            var pathPatterns = allowedPatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(PathPattern.Create)
                .ToArray();

            return new PathRestriction(RestrictionSet<PathPattern>.DenyAllExcept(pathPatterns));
        }

        /// <summary>
        /// Creates a restriction that denies all paths except those matching the specified patterns.
        /// </summary>
        public static PathRestriction DenyAllExcept(params PathPattern[] allowedPatterns)
        {
            if (allowedPatterns == null || allowedPatterns.Length == 0)
                return DenyAll;

            return new PathRestriction(RestrictionSet<PathPattern>.DenyAllExcept(allowedPatterns));
        }

        /// <summary>
        /// Creates a PathRestriction from string representations with validation.
        /// </summary>
        public static Result<PathRestriction, string> Create(bool denyAll, IEnumerable<string> patterns)
        {
            if (patterns == null)
                return Result.Success<PathRestriction, string>(None);

            var patternList = patterns.ToList();
            if (!patternList.Any())
            {
                return Result.Success<PathRestriction, string>(denyAll ? DenyAll : None);
            }

            // Validate and parse patterns
            var parsedPatterns = new List<PathPattern>();
            var errors = new List<string>();

            foreach (var pattern in patternList)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                var result = PathPattern.TryCreate(pattern);
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
                return Result.Failure<PathRestriction, string>(string.Join("; ", errors));

            return Result.Success<PathRestriction, string>(
                denyAll ? DenyAllExcept(parsedPatterns.ToArray()) : DenyPaths(parsedPatterns.ToArray()));
        }

        /// <summary>
        /// Checks if a path is restricted.
        /// </summary>
        public bool IsRestricted(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true; // Empty paths are always restricted

            var normalizedPath = PathNormalizer.NormalizePath(path);

            // For "deny specific", check if any pattern matches
            if (_restriction is DenySpecificRestriction<PathPattern> specific)
            {
                return specific.DeniedItems.Any(pattern => pattern.Matches(normalizedPath));
            }

            // For "deny all except", check if NO pattern matches
            if (_restriction is DenyAllExceptRestriction<PathPattern> allExcept)
            {
                return !allExcept.AllowedItems.Any(pattern => pattern.Matches(normalizedPath));
            }

            return false;
        }

        /// <summary>
        /// Checks if a path is allowed.
        /// </summary>
        public bool IsAllowed(string path) => !IsRestricted(path);

        /// <summary>
        /// Scopes all patterns in this restriction to a specific directory.
        /// </summary>
        public PathRestriction ScopeToDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return this;

            if (_restriction is DenySpecificRestriction<PathPattern> specific)
            {
                var scopedPatterns = specific.DeniedItems
                    .Select(p => p.ScopeToDirectory(directory))
                    .ToArray();
                return DenyPaths(scopedPatterns);
            }

            if (_restriction is DenyAllExceptRestriction<PathPattern> allExcept)
            {
                var scopedPatterns = allExcept.AllowedItems
                    .Select(p => p.ScopeToDirectory(directory))
                    .ToArray();
                return DenyAllExcept(scopedPatterns);
            }

            return this;
        }

        /// <summary>
        /// Combines this restriction with another, taking the most restrictive combination.
        /// </summary>
        public PathRestriction CombineWith(PathRestriction other)
        {
            if (other == null)
                return this;

            return new PathRestriction(_restriction.CombineWith(other._restriction));
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
        /// Gets whether this restriction denies all paths.
        /// </summary>
        public bool DeniesAll => _restriction.DeniesEverything;

        /// <summary>
        /// Gets whether this restriction denies no paths.
        /// </summary>
        public bool DeniesNone => _restriction.DeniesNothing;

        public override string ToString() => _restriction.ToString();
    }

    /// <summary>
    /// Fluent builder extensions for PathRestriction.
    /// </summary>
    public static class PathRestrictionExtensions
    {
        /// <summary>
        /// Creates a path restriction that denies system paths.
        /// </summary>
        public static PathRestriction DenySystemPaths(this PathRestriction _) =>
            PathRestriction.DenyPaths("/etc/*", "/usr/*", "/bin/*", "/sbin/*", "/var/*");

        /// <summary>
        /// Creates a path restriction that allows only application data paths.
        /// </summary>
        public static PathRestriction AllowOnlyAppData(this PathRestriction _) =>
            PathRestriction.DenyAllExcept("/app/data/*", "/app/config/*");

        /// <summary>
        /// Creates a path restriction that denies all except the current directory.
        /// </summary>
        public static PathRestriction AllowOnlyCurrentDirectory(this PathRestriction _) =>
            PathRestriction.DenyAllExcept("./*");

        /// <summary>
        /// Creates a path restriction that denies write to specific paths.
        /// </summary>
        public static PathRestriction DenyWriteTo(this PathRestriction _, params string[] paths) =>
            PathRestriction.DenyPaths(paths);
    }
}