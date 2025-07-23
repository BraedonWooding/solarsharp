using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines execution resource limits for script execution
    /// </summary>
    public class ExecutionLimits
    {
        /// <summary>
        /// Maximum execution time before script is terminated (in milliseconds).
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual timeout
        /// Default: <see cref="SecurityConstants.DefaultTimeoutMs"/> (5 seconds).
        /// </summary>
        public int? TimeoutMs { get; set; } = SecurityConstants.DefaultTimeoutMs;

        /// <summary>
        /// Maximum number of VM instructions before termination.
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual limit
        /// Default: <see cref="SecurityConstants.DefaultMaxInstructions"/> (100,000).
        /// </summary>
        public long? MaxInstructions { get; set; } = SecurityConstants.DefaultMaxInstructions;

        /// <summary>
        /// Maximum memory usage in megabytes.
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual limit
        /// Default: <see cref="SecurityConstants.DefaultMaxMemoryMB"/> (10 MB).
        /// </summary>
        public int? MaxMemoryMB { get; set; } = SecurityConstants.DefaultMaxMemoryMB;

        /// <summary>
        /// Maximum function call depth (stack depth).
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual limit
        /// Default: <see cref="SecurityConstants.DefaultMaxCallDepth"/> (50).
        /// </summary>
        public int? MaxCallDepth { get; set; } = SecurityConstants.DefaultMaxCallDepth;

        /// <summary>
        /// Maximum number of tables that can be created.
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual limit
        /// Default: <see cref="SecurityConstants.DefaultMaxTables"/> (1,000).
        /// </summary>
        public int? MaxTables { get; set; } = SecurityConstants.DefaultMaxTables;

        /// <summary>
        /// Maximum string length for any single string.
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual limit
        /// Default: <see cref="SecurityConstants.DefaultMaxStringLength"/> (1 million characters).
        /// </summary>
        public int? MaxStringLength { get; set; } = SecurityConstants.DefaultMaxStringLength;

        /// <summary>
        /// Maximum number of coroutine resumes allowed.
        /// NEW SEMANTICS: null or -1 = unlimited, 0 = deny immediately, >0 = actual limit
        /// Default: <see cref="SecurityConstants.DefaultMaxCoroutineResumes"/> (10,000).
        /// </summary>
        public int? MaxCoroutineResumes { get; set; } = SecurityConstants.DefaultMaxCoroutineResumes;

        /// <summary>
        /// Defines how resource limits are tracked across multiple executions
        /// </summary>
        public ResourceLimitScope ResourceLimitScope { get; set; } = ResourceLimitScope.PerExecution;

        /// <summary>
        /// Convenience property for timeout as TimeSpan.
        /// Returns null when timeout is unlimited (TimeoutMs == -1 or null).
        /// </summary>
        public TimeSpan? Timeout
        {
            get { return (TimeoutMs == null || TimeoutMs < 0) ? null : TimeSpan.FromMilliseconds(TimeoutMs.Value); }
            set { TimeoutMs = value.HasValue ? (int)value.Value.TotalMilliseconds : -1; }
        }

        /// <summary>
        /// Convenience property for timeout in seconds
        /// </summary>
        public int TimeoutSeconds
        {
            get { return (TimeoutMs == null || TimeoutMs < 0) ? -1 : TimeoutMs.Value / 1000; }
            set { TimeoutMs = value < 0 ? -1 : value * 1000; }
        }

        /// <summary>
        /// Creates execution limits suitable for isolated execution
        /// </summary>
        public static ExecutionLimits Isolated() =>
            new ExecutionLimits
            {
                TimeoutMs = 5000,
                MaxInstructions = 100_000,
                MaxMemoryMB = 10,
                MaxCallDepth = 100,
                MaxTables = 1_000,
                MaxStringLength = 100_000,
                MaxCoroutineResumes = 100,
            };

        /// <summary>
        /// Creates execution limits suitable for configuration processing
        /// </summary>
        public static ExecutionLimits Configuration() =>
            new ExecutionLimits
            {
                TimeoutMs = 10000,
                MaxInstructions = 500_000,
                MaxMemoryMB = 20,
                MaxCallDepth = 200,
                MaxTables = 5_000,
                MaxStringLength = 500_000,
                MaxCoroutineResumes = 500,
            };

        /// <summary>
        /// Creates execution limits suitable for data processing
        /// </summary>
        public static ExecutionLimits DataProcessing() =>
            new ExecutionLimits
            {
                TimeoutMs = 300000, // 5 minutes
                MaxInstructions = 10_000_000,
                MaxMemoryMB = 100,
                MaxCallDepth = 500,
                MaxTables = 10_000,
                MaxStringLength = 1_000_000,
                MaxCoroutineResumes = 10_000,
            };

        /// <summary>
        /// Creates execution limits suitable for trusted automation
        /// </summary>
        public static ExecutionLimits TrustedAutomation() =>
            new ExecutionLimits
            {
                TimeoutMs = 1800000, // 30 minutes
                MaxInstructions = 100_000_000,
                MaxMemoryMB = 500,
                MaxCallDepth = 1000,
                MaxTables = 50_000,
                MaxStringLength = 10_000_000,
                MaxCoroutineResumes = 50_000,
            };

        /// <summary>
        /// Enables test mode for deterministic behaviour
        /// </summary>
        public bool TestMode { get; set; }

        /// <summary>
        /// Forces garbage collection before memory checks for accurate measurement
        /// </summary>
        public bool ForceGCOnMemoryCheck { get; set; }

        /// <summary>
        /// How often to check memory usage (default is 1000 instructions)
        /// </summary>
        public int CheckMemoryEveryNInstructions { get; set; } = 1000;

        /// <summary>
        /// Uses more stable memory measurement techniques in test mode
        /// </summary>
        public bool UseStableMemoryMeasurement { get; set; }
    }
}
