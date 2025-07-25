using System;
using System.Collections.Concurrent;
using System.IO;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests.Functional
{
    /// <summary>
    /// Interface for manifest caching.
    /// Pure functional interface - no side effects on reads.
    /// </summary>
    public interface IManifestCache
    {
        /// <summary>
        /// Get cached manifest if still valid.
        /// Pure function - no side effects.
        /// </summary>
        Maybe<LoadedManifest> GetIfValid(string filePath);

        /// <summary>
        /// Store manifest in cache.
        /// Side effect - but isolated to caching.
        /// </summary>
        void Store(string filePath, LoadedManifest manifest);

        /// <summary>
        /// Clear cache for a specific file.
        /// Side effect - but isolated to caching.
        /// </summary>
        void Clear(string filePath);

        /// <summary>
        /// Clear entire cache.
        /// Side effect - but isolated to caching.
        /// </summary>
        void ClearAll();
    }

    /// <summary>
    /// Efficient manifest cache using file metadata for validation.
    /// Thread-safe using ConcurrentDictionary.
    /// No complex invalidation logic - just check if file changed.
    /// </summary>
    public class ManifestCache : IManifestCache
    {
        private readonly ConcurrentDictionary<ManifestCacheKey, CachedManifest> _cache = new();

        /// <summary>
        /// Get cached manifest if still valid.
        /// Pure function - checks validity without side effects.
        /// </summary>
        public Maybe<LoadedManifest> GetIfValid(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Maybe<LoadedManifest>.None;

            try
            {
                // Create cache key from current file state
                var currentKey = ManifestCacheKey.FromPath(filePath);
                
                // Try to get cached manifest
                if (_cache.TryGetValue(currentKey, out var cached))
                {
                    // Cache hit - return cached manifest
                    return Maybe<LoadedManifest>.From(cached.Manifest);
                }

                // Check if we have an outdated entry for this path
                var outdatedKey = FindOutdatedKeyForPath(filePath);
                if (outdatedKey.HasValue)
                {
                    // Remove outdated entry
                    _cache.TryRemove(outdatedKey.Value, out _);
                }

                return Maybe<LoadedManifest>.None;
            }
            catch
            {
                // If anything goes wrong, just return cache miss
                return Maybe<LoadedManifest>.None;
            }
        }

        /// <summary>
        /// Store manifest in cache.
        /// Side effect - but isolated to caching.
        /// </summary>
        public void Store(string filePath, LoadedManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(filePath) || manifest == null)
                return;

            try
            {
                var key = ManifestCacheKey.FromPath(filePath);
                var cached = new CachedManifest(manifest, DateTime.UtcNow);
                
                _cache[key] = cached;
            }
            catch
            {
                // If caching fails, just continue without caching
                // This ensures caching never breaks the main functionality
            }
        }

        /// <summary>
        /// Clear cache for a specific file.
        /// </summary>
        public void Clear(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                // Find and remove all entries for this path
                var keysToRemove = new System.Collections.Generic.List<ManifestCacheKey>();
                
                foreach (var kvp in _cache)
                {
                    if (string.Equals(kvp.Key.Path, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
            catch
            {
                // If clearing fails, just continue
            }
        }

        /// <summary>
        /// Clear entire cache.
        /// </summary>
        public void ClearAll()
        {
            try
            {
                _cache.Clear();
            }
            catch
            {
                // If clearing fails, just continue
            }
        }

        /// <summary>
        /// Find outdated cache key for a given path.
        /// Used to clean up old entries when file changes.
        /// </summary>
        private Maybe<ManifestCacheKey> FindOutdatedKeyForPath(string filePath)
        {
            foreach (var kvp in _cache)
            {
                if (string.Equals(kvp.Key.Path, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Maybe<ManifestCacheKey>.From(kvp.Key);
                }
            }

            return Maybe<ManifestCacheKey>.None;
        }
    }

    /// <summary>
    /// Cache key based on file path and metadata.
    /// Immutable record for thread safety.
    /// </summary>
    public record ManifestCacheKey(string Path, long Size, DateTime LastModified)
    {
        /// <summary>
        /// Create cache key from file path.
        /// Pure function - reads file metadata but has no side effects.
        /// </summary>
        public static ManifestCacheKey FromPath(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                return new ManifestCacheKey(
                    Path: path,
                    Size: fileInfo.Exists ? fileInfo.Length : 0,
                    LastModified: fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue);
            }
            catch
            {
                // If we can't read file info, create a key that won't match anything
                return new ManifestCacheKey(path, -1, DateTime.MinValue);
            }
        }

        /// <summary>
        /// Check if this cache key is still valid for the file.
        /// Pure function - no side effects.
        /// </summary>
        public bool IsStillValidForPath()
        {
            try
            {
                var currentKey = FromPath(Path);
                return this.Equals(currentKey);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Cached manifest with metadata.
    /// Immutable record for thread safety.
    /// </summary>
    public record CachedManifest(LoadedManifest Manifest, DateTime CachedAt)
    {
        /// <summary>
        /// Check if cached manifest is still valid.
        /// Could be extended with TTL logic if needed.
        /// </summary>
        public bool IsStillValid() => true; // For now, validity is determined by file metadata
    }

    /// <summary>
    /// No-op manifest cache for scenarios where caching is disabled.
    /// Implements the null object pattern.
    /// </summary>
    public class NoOpManifestCache : IManifestCache
    {
        public static readonly IManifestCache Instance = new NoOpManifestCache();

        public Maybe<LoadedManifest> GetIfValid(string filePath) => Maybe<LoadedManifest>.None;
        public void Store(string filePath, LoadedManifest manifest) { }
        public void Clear(string filePath) { }
        public void ClearAll() { }
    }

    /// <summary>
    /// Extension methods for working with manifest cache functionally.
    /// </summary>
    public static class ManifestCacheExtensions
    {
        /// <summary>
        /// Try to get manifest from cache, or load it if not cached.
        /// </summary>
        public static Result<LoadedManifest, ManifestValidationError> GetOrLoad(
            this IManifestCache cache,
            string filePath,
            Func<string, Result<LoadedManifest, ManifestValidationError>> loader)
        {
            // Try cache first
            var cached = cache.GetIfValid(filePath);
            if (cached.HasValue)
            {
                return Result.Success<LoadedManifest, ManifestValidationError>(cached.Value);
            }

            // Load and cache
            var loadResult = loader(filePath);
            if (loadResult.IsSuccess)
            {
                cache.Store(filePath, loadResult.Value);
            }

            return loadResult;
        }

        /// <summary>
        /// Wrap a manifest validation service with caching.
        /// </summary>
        public static IManifestValidationService WithCaching(
            this IManifestValidationService service,
            IManifestCache cache) =>
            new CachedManifestValidationService(service, cache);
    }

    /// <summary>
    /// Wrapper that adds caching to any IManifestValidationService.
    /// Follows the decorator pattern.
    /// </summary>
    public class CachedManifestValidationService : IManifestValidationService
    {
        private readonly IManifestValidationService _inner;
        private readonly IManifestCache _cache;

        public CachedManifestValidationService(IManifestValidationService inner, IManifestCache cache)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public Result<LoadedManifest, ManifestValidationError> ValidateManifest(
            string scriptPath,
            ITrustStore trustStore,
            string scriptId)
        {
            return _cache.GetOrLoad(scriptPath, 
                path => _inner.ValidateManifest(path, trustStore, scriptId));
        }

        public Result<Manifest, ManifestValidationError> ValidateManifestFromJson(
            string manifestJson,
            string manifestPath,
            ITrustStore trustStore,
            string scriptId)
        {
            // Don't cache JSON validation - it's not file-based
            return _inner.ValidateManifestFromJson(manifestJson, manifestPath, trustStore, scriptId);
        }

        public Result<SignatureValidationResult, ManifestValidationError> ValidateSignature(
            Manifest manifest,
            ITrustStore trustStore,
            string scriptId)
        {
            // Don't cache signature validation - it depends on trust store state
            return _inner.ValidateSignature(manifest, trustStore, scriptId);
        }

        public System.Collections.Generic.IEnumerable<SolarSharp.Interpreter.Security.Manifests.Events.ManifestValidationEvent> GetValidationEvents() =>
            _inner is EventDrivenManifestValidator eventDriven 
                ? eventDriven.GetValidationEvents()
                : System.Linq.Enumerable.Empty<SolarSharp.Interpreter.Security.Manifests.Events.ManifestValidationEvent>();

        public IObservable<SolarSharp.Interpreter.Security.Manifests.Events.ManifestValidationEvent> ValidationEvents =>
            _inner.ValidationEvents;
    }
}