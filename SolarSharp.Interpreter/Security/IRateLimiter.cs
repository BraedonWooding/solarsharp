using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Interface for rate limiting operations
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Checks if an operation is allowed under current rate limits
        /// </summary>
        bool IsAllowed(string resource, string operation = null);

        /// <summary>
        /// Records an operation for rate limiting purposes
        /// </summary>
        void RecordOperation(string resource, string operation = null);

        /// <summary>
        /// Gets current usage statistics for a resource
        /// </summary>
        RateLimitStats GetStats(string resource);

        /// <summary>
        /// Resets rate limiting for a resource
        /// </summary>
        void Reset(string resource);

        /// <summary>
        /// Gets all current rate limit configurations
        /// </summary>
        IReadOnlyDictionary<string, RateLimitConfig> GetConfigurations();
    }

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public class RateLimitConfig
    {
        /// <summary>
        /// Maximum number of operations allowed
        /// </summary>
        public int MaxOperations { get; set; } = 100;

        /// <summary>
        /// Time window for the rate limit
        /// </summary>
        public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Per-operation limits (optional)
        /// </summary>
        public Dictionary<string, int> OperationLimits { get; set; } = new();

        /// <summary>
        /// Whether to use a sliding window (true) or fixed window (false)
        /// </summary>
        public bool SlidingWindow { get; set; } = true;
    }

    /// <summary>
    /// Rate limiting statistics
    /// </summary>
    public class RateLimitStats
    {
        public string Resource { get; set; } = string.Empty;
        public int CurrentCount { get; set; }
        public int MaxOperations { get; set; }
        public TimeSpan Window { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime LastOperation { get; set; }
        public Dictionary<string, int> OperationCounts { get; set; } = new();
        public bool IsAtLimit => CurrentCount >= MaxOperations;
        public double UtilizationPercentage => MaxOperations > 0 ? (double)CurrentCount / MaxOperations * 100 : 0;
    }

    /// <summary>
    /// Sliding window rate limiter implementation
    /// </summary>
    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly ConcurrentDictionary<string, RateLimitConfig> _configs = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _operations = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<DateTime>>> _operationSpecific = new();
        private readonly object _lockObject = new object();
        private readonly ISecurityAuditor _auditor;

        public SlidingWindowRateLimiter(ISecurityAuditor auditor = null)
        {
            _auditor = auditor;
        }

        /// <summary>
        /// Configures rate limiting for a resource
        /// </summary>
        public void Configure(string resource, RateLimitConfig config)
        {
            _configs[resource] = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool IsAllowed(string resource, string operation = null)
        {
            if (!_configs.TryGetValue(resource, out var config))
            {
                // No rate limiting configured for this resource
                return true;
            }

            lock (_lockObject)
            {
                CleanupExpiredOperations(resource, config);

                var operations = _operations.GetOrAdd(resource, _ => new List<DateTime>());
                var currentCount = operations.Count;

                // Check overall limit
                if (currentCount >= config.MaxOperations)
                {
                    _auditor?.LogRateLimitViolation(resource, currentCount, config.MaxOperations, config.Window);
                    return false;
                }

                // Check operation-specific limit if specified
                if (!string.IsNullOrEmpty(operation) && config.OperationLimits.TryGetValue(operation, out var operationLimit))
                {
                    var opSpecific = _operationSpecific.GetOrAdd(resource, _ => new ConcurrentDictionary<string, List<DateTime>>());
                    var opOperations = opSpecific.GetOrAdd(operation, _ => new List<DateTime>());
                    
                    CleanupExpiredOperations(opOperations, config.Window);
                    
                    if (opOperations.Count >= operationLimit)
                    {
                        _auditor?.LogRateLimitViolation($"{resource}.{operation}", opOperations.Count, operationLimit, config.Window);
                        return false;
                    }
                }

                return true;
            }
        }

        public void RecordOperation(string resource, string operation = null)
        {
            if (!_configs.ContainsKey(resource))
                return;

            var now = DateTime.UtcNow;

            lock (_lockObject)
            {
                // Record overall operation
                var operations = _operations.GetOrAdd(resource, _ => new List<DateTime>());
                operations.Add(now);

                // Record operation-specific if specified
                if (!string.IsNullOrEmpty(operation))
                {
                    var opSpecific = _operationSpecific.GetOrAdd(resource, _ => new ConcurrentDictionary<string, List<DateTime>>());
                    var opOperations = opSpecific.GetOrAdd(operation, _ => new List<DateTime>());
                    opOperations.Add(now);
                }
            }
        }

        public RateLimitStats GetStats(string resource)
        {
            if (!_configs.TryGetValue(resource, out var config))
            {
                return new RateLimitStats { Resource = resource };
            }

            lock (_lockObject)
            {
                CleanupExpiredOperations(resource, config);

                var operations = _operations.GetOrAdd(resource, _ => new List<DateTime>());
                var stats = new RateLimitStats
                {
                    Resource = resource,
                    CurrentCount = operations.Count,
                    MaxOperations = config.MaxOperations,
                    Window = config.Window,
                    WindowStart = DateTime.UtcNow - config.Window,
                    LastOperation = operations.Any() ? operations.Last() : default(DateTime)
                };

                // Add operation-specific counts
                if (_operationSpecific.TryGetValue(resource, out var opSpecific))
                {
                    foreach (var (operation, opOperations) in opSpecific)
                    {
                        CleanupExpiredOperations(opOperations, config.Window);
                        stats.OperationCounts[operation] = opOperations.Count;
                    }
                }

                return stats;
            }
        }

        public void Reset(string resource)
        {
            lock (_lockObject)
            {
                _operations.TryRemove(resource, out _);
                _operationSpecific.TryRemove(resource, out _);
            }
        }

        public IReadOnlyDictionary<string, RateLimitConfig> GetConfigurations()
        {
            return new Dictionary<string, RateLimitConfig>(_configs);
        }

        private void CleanupExpiredOperations(string resource, RateLimitConfig config)
        {
            if (_operations.TryGetValue(resource, out var operations))
            {
                CleanupExpiredOperations(operations, config.Window);
            }
        }

        private void CleanupExpiredOperations(List<DateTime> operations, TimeSpan window)
        {
            var cutoff = DateTime.UtcNow - window;
            operations.RemoveAll(op => op < cutoff);
        }
    }

    /// <summary>
    /// Factory for creating pre-configured rate limiters
    /// </summary>
    public static class RateLimiters
    {
        /// <summary>
        /// Creates a rate limiter with conservative defaults
        /// </summary>
        public static IRateLimiter CreateConservative(ISecurityAuditor auditor = null)
        {
            var limiter = new SlidingWindowRateLimiter(auditor);
            
            // File operations
            limiter.Configure("file", new RateLimitConfig
            {
                MaxOperations = 50,
                Window = TimeSpan.FromMinutes(1),
                OperationLimits = new Dictionary<string, int>
                {
                    ["read"] = 30,
                    ["write"] = 10,
                    ["delete"] = 5
                }
            });

            // Network operations
            limiter.Configure("network", new RateLimitConfig
            {
                MaxOperations = 20,
                Window = TimeSpan.FromMinutes(1)
            });

            // Process execution
            limiter.Configure("process", new RateLimitConfig
            {
                MaxOperations = 5,
                Window = TimeSpan.FromMinutes(1)
            });

            return limiter;
        }

        /// <summary>
        /// Creates a rate limiter with generous defaults for development
        /// </summary>
        public static IRateLimiter CreateDevelopment(ISecurityAuditor auditor = null)
        {
            var limiter = new SlidingWindowRateLimiter(auditor);
            
            limiter.Configure("file", new RateLimitConfig
            {
                MaxOperations = 200,
                Window = TimeSpan.FromMinutes(1)
            });

            limiter.Configure("network", new RateLimitConfig
            {
                MaxOperations = 100,
                Window = TimeSpan.FromMinutes(1)
            });

            limiter.Configure("process", new RateLimitConfig
            {
                MaxOperations = 20,
                Window = TimeSpan.FromMinutes(1)
            });

            return limiter;
        }
    }
}

