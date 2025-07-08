using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines execution resource limits for script execution
    /// </summary>
    public class ExecutionLimits
    {
        /// <summary>
        /// Maximum execution time before script is terminated (in milliseconds)
        /// Use 0 or null for no timeout limit (unlimited execution time).
        /// </summary>
        public int? TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Maximum number of VM instructions before termination
        /// Use 0 or null for no instruction limit (unlimited instructions).
        /// </summary>
        public long? MaxInstructions { get; set; } = 1_000_000;

        /// <summary>
        /// Maximum memory usage in megabytes
        /// Use 0 or null for no memory limit (unlimited memory).
        /// </summary>
        public int? MaxMemoryMB { get; set; } = 50;

        /// <summary>
        /// Maximum function call depth (stack depth)
        /// Use 0 or null for no call depth limit (unlimited recursion).
        /// </summary>
        public int? MaxCallDepth { get; set; } = 1000;

        /// <summary>
        /// Maximum number of tables that can be created
        /// Use 0 or null for no table limit (unlimited tables).
        /// </summary>
        public int? MaxTables { get; set; } = 10_000;

        /// <summary>
        /// Maximum string length for any single string
        /// Use 0 or null for no string length limit (unlimited string size).
        /// </summary>
        public int? MaxStringLength { get; set; } = 1_000_000;

        /// <summary>
        /// Maximum number of coroutine resumes allowed
        /// Use 0 or null for no coroutine resume limit (unlimited resumes).
        /// </summary>
        public int? MaxCoroutineResumes { get; set; } = 10_000;

        /// <summary>
        /// Convenience property for timeout as TimeSpan.
        /// Returns null when timeout is disabled (TimeoutMs == 0).
        /// </summary>
        public TimeSpan? Timeout
        {
            get { return TimeoutMs == 0 ? null : TimeSpan.FromMilliseconds(TimeoutMs ?? 0); }
            set { TimeoutMs = value.HasValue ? (int)value.Value.TotalMilliseconds : 0; }
        }

        /// <summary>
        /// Convenience property for timeout in seconds
        /// </summary>
        public int TimeoutSeconds
        {
            get { return (TimeoutMs ?? 0) / 1000; }
            set { TimeoutMs = value * 1000; }
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
