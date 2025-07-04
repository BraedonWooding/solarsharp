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
        /// Use 0 for no timeout limit
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Maximum number of VM instructions before termination
        /// </summary>
        public long MaxInstructions { get; set; } = 1_000_000;

        /// <summary>
        /// Maximum memory usage in megabytes
        /// </summary>
        public int MaxMemoryMB { get; set; } = 50;

        /// <summary>
        /// Maximum function call depth (stack depth)
        /// </summary>
        public int MaxCallDepth { get; set; } = 1000;

        /// <summary>
        /// Maximum number of tables that can be created
        /// </summary>
        public int MaxTables { get; set; } = 10_000;

        /// <summary>
        /// Maximum string length for any single string
        /// </summary>
        public int MaxStringLength { get; set; } = 1_000_000;

        /// <summary>
        /// Maximum number of coroutine resumes allowed
        /// </summary>
        public int MaxCoroutineResumes { get; set; } = 10_000;

        /// <summary>
        /// Convenience property for timeout as TimeSpan
        /// </summary>
        public TimeSpan Timeout
        {
            get => TimeoutMs == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(TimeoutMs);
            set => TimeoutMs = value == TimeSpan.Zero ? 0 : (int)value.TotalMilliseconds;
        }

        /// <summary>
        /// Convenience property for timeout in seconds
        /// </summary>
        public int TimeoutSeconds
        {
            get => TimeoutMs / 1000;
            set => TimeoutMs = value * 1000;
        }

        /// <summary>
        /// Creates execution limits suitable for isolated execution
        /// </summary>
        public static ExecutionLimits Isolated() => new()
        {
            TimeoutMs = 5000,
            MaxInstructions = 100_000,
            MaxMemoryMB = 10,
            MaxCallDepth = 100,
            MaxTables = 1_000,
            MaxStringLength = 100_000,
            MaxCoroutineResumes = 100
        };

        /// <summary>
        /// Creates execution limits suitable for configuration processing
        /// </summary>
        public static ExecutionLimits Configuration() => new()
        {
            TimeoutMs = 10000,
            MaxInstructions = 500_000,
            MaxMemoryMB = 20,
            MaxCallDepth = 200,
            MaxTables = 5_000,
            MaxStringLength = 500_000,
            MaxCoroutineResumes = 500
        };

        /// <summary>
        /// Creates execution limits suitable for data processing
        /// </summary>
        public static ExecutionLimits DataProcessing() => new()
        {
            TimeoutMs = 300000, // 5 minutes
            MaxInstructions = 10_000_000,
            MaxMemoryMB = 100,
            MaxCallDepth = 500,
            MaxTables = 10_000,
            MaxStringLength = 1_000_000,
            MaxCoroutineResumes = 10_000
        };

        /// <summary>
        /// Creates execution limits suitable for trusted automation
        /// </summary>
        public static ExecutionLimits TrustedAutomation() => new()
        {
            TimeoutMs = 1800000, // 30 minutes
            MaxInstructions = 100_000_000,
            MaxMemoryMB = 500,
            MaxCallDepth = 1000,
            MaxTables = 50_000,
            MaxStringLength = 10_000_000,
            MaxCoroutineResumes = 50_000
        };
    }
}