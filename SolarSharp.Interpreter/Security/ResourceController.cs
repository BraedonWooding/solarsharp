using System;
using System.Diagnostics;
using System.Threading;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Controls and monitors resource usage during script execution
    /// </summary>
    public class ResourceController : IDisposable
    {
        private readonly ExecutionLimits _limits;
        private readonly Timer _timeoutTimer;
        private readonly Stopwatch _executionTimer;
        private long _instructionCount;
        private int _callDepth;
        private int _tableCount;
        private long _baselineMemory;
        private volatile int _executionDepth;
        private volatile bool _isDisposed;
        private volatile bool _timedOut;

        /// <summary>
        /// Event raised when a resource limit is exceeded
        /// </summary>
        public event EventHandler<ResourceLimitExceededEventArgs> ResourceLimitExceeded;

        /// <summary>
        /// Creates a new resource controller
        /// </summary>
        public ResourceController(ExecutionLimits limits)
        {
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
            _executionTimer = new Stopwatch();
            _timeoutTimer = new Timer(OnTimeout, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Starts monitoring resource usage for a new execution
        /// </summary>
        public void StartExecution()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ResourceController));

            Interlocked.Increment(ref _executionDepth);

            // Only initialize on the first execution level
            if (_executionDepth == 1)
            {
                _timedOut = false;
                _instructionCount = 0;
                _callDepth = 0;
                _tableCount = 0;

                // Check for null limits that should fail instantly
                CheckNullLimits();

                // Capture baseline memory
                if (_limits.TestMode && _limits.UseStableMemoryMeasurement)
                {
                    // More aggressive GC for test mode
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                    _baselineMemory = GC.GetTotalMemory(true);
                }
                else
                {
                    // Normal baseline capture
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                    _baselineMemory = GC.GetTotalMemory(false);
                }

                // Start timers
                _executionTimer.Restart();

                // Start a timeout timer if timeout is enabled and not unlimited (0)
                if (_limits.TimeoutMs is > 0)
                {
                    _timeoutTimer.Change(
                        TimeSpan.FromMilliseconds(_limits.TimeoutMs.Value),
                        TimeSpan.FromMilliseconds(-1)
                    );
                }
                else
                {
                    // Disable timer for infinite timeout (0 or null)
                    _timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Stops monitoring resource usage
        /// </summary>
        public void StopExecution()
        {
            var depth = Interlocked.Decrement(ref _executionDepth);

            // Only stop timers when we reach the outermost execution level
            if (depth != 0)
                return;
            _executionTimer.Stop();
            _timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Checks if execution has timed out
        /// </summary>
        private void CheckTimeout()
        {
            if (_timedOut)
            {
                throw new ExecutionTimeoutException(
                    $"Script execution timeout after {_limits.TimeoutMs}ms",
                    "CheckTimeout"
                );
            }
        }

        /// <summary>
        /// Checks for null limits and handles them as unlimited
        /// </summary>
        private void CheckNullLimits()
        {
            // According to functional programming principles:
            // - Null means "no limit" (unlimited), not "execution denied"
            // - We should handle optional values gracefully
            // - No exceptions should be thrown for null values

            // This method is now a no-op since null values are handled
            // as unlimited throughout the rest of the code.
            // The method is kept for backward compatibility but does nothing.
        }

        /// <summary>
        /// Increments the instruction count and checks the limit
        /// </summary>
        public void IncrementInstructionCount()
        {
            if (_executionDepth == 0)
                return;

            // Check timeout immediately if already timed out
            if (_timedOut)
            {
                throw new ExecutionTimeoutException(
                    $"Script execution timeout after {_limits.TimeoutMs}ms",
                    "InstructionCount"
                );
            }

            ++_instructionCount;

            // Skip check if unlimited (0)
            if (_limits.MaxInstructions is > 0 && _instructionCount > _limits.MaxInstructions.Value)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Instructions,
                    _instructionCount,
                    _limits.MaxInstructions.Value
                );

                ResourceLimitExceeded?.Invoke(this, args);

                throw new InstructionLimitExceededException(
                    $"Instruction limit exceeded: {_instructionCount} > {_limits.MaxInstructions.Value}",
                    "InstructionCount"
                );
            }

            // Check memory more frequently in test mode
            var checkInterval = _limits.TestMode ? _limits.CheckMemoryEveryNInstructions : 1000;
            if (_instructionCount % checkInterval != 0)
                return;
            CheckTimeout();
            CheckMemoryUsage();
        }

        /// <summary>
        /// Enters a function call and checks depth limit
        /// </summary>
        public void EnterFunction()
        {
            if (_executionDepth == 0)
                return;

            ++_callDepth;

            // Skip check if unlimited (0)
            if (_limits.MaxCallDepth is not > 0 || _callDepth <= _limits.MaxCallDepth.Value)
                return;
            var args = new ResourceLimitExceededEventArgs(
                ResourceType.CallDepth,
                _callDepth,
                _limits.MaxCallDepth.Value
            );

            ResourceLimitExceeded?.Invoke(this, args);

            throw new CallDepthExceededException(
                $"Call depth limit exceeded: {_callDepth} > {_limits.MaxCallDepth.Value}",
                "CallDepth"
            );
        }

        /// <summary>
        /// Exits a function call
        /// </summary>
        public void ExitFunction()
        {
            if (_executionDepth == 0)
                return;

            if (_callDepth > 0)
                _callDepth--;
        }

        /// <summary>
        /// Increments table count and checks limit
        /// </summary>
        public void IncrementTableCount()
        {
            if (_executionDepth == 0)
                return;

            ++_tableCount;

            // Skip check if unlimited (0)
            if (_limits.MaxTables is > 0 && _tableCount > _limits.MaxTables.Value)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Tables,
                    _tableCount,
                    _limits.MaxTables.Value
                );

                ResourceLimitExceeded?.Invoke(this, args);

                throw new ResourceLimitExceededException(
                    $"Table limit exceeded: {_tableCount} > {_limits.MaxTables.Value}",
                    "TableCount"
                );
            }
        }

        /// <summary>
        /// Checks string length against limit
        /// </summary>
        public void CheckStringLength(int length)
        {
            if (_executionDepth == 0)
                return;

            // Skip check if unlimited (0)
            if (_limits.MaxStringLength is > 0 && length > _limits.MaxStringLength.Value)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.StringLength,
                    length,
                    _limits.MaxStringLength.Value
                );

                ResourceLimitExceeded?.Invoke(this, args);

                throw new ResourceLimitExceededException(
                    $"String length limit exceeded: {length} > {_limits.MaxStringLength.Value}",
                    "StringLength"
                );
            }
        }

        /// <summary>
        /// Updates the current instruction count (called from VM)
        /// </summary>
        public void UpdateInstructionCount(long count)
        {
            _instructionCount = count;
        }

        /// <summary>
        /// Updates the current call depth (called from VM)
        /// </summary>
        public void UpdateCallDepth(int depth)
        {
            _callDepth = depth;
        }

        /// <summary>
        /// Checks all resource limits (called periodically from VM)
        /// </summary>
        public void CheckResourceLimits()
        {
            if (_executionDepth == 0)
                return;

            CheckTimeout();

            // Check instruction count if not unlimited (0)
            if (_limits.MaxInstructions is > 0 && _instructionCount > _limits.MaxInstructions.Value)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Instructions,
                    _instructionCount,
                    _limits.MaxInstructions.Value
                );

                ResourceLimitExceeded?.Invoke(this, args);

                throw new InstructionLimitExceededException(
                    $"Instruction limit exceeded: {_instructionCount} > {_limits.MaxInstructions.Value}",
                    "InstructionCount"
                );
            }

            // Check call depth if not unlimited (0)
            if (_limits.MaxCallDepth is > 0 && _callDepth > _limits.MaxCallDepth.Value)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.CallDepth,
                    _callDepth,
                    _limits.MaxCallDepth.Value
                );

                ResourceLimitExceeded?.Invoke(this, args);

                throw new CallDepthExceededException(
                    $"Call depth limit exceeded: {_callDepth} > {_limits.MaxCallDepth.Value}",
                    "CallDepth"
                );
            }

            // Check memory periodically
            CheckMemoryUsage();
        }

        /// <summary>
        /// Checks current memory usage
        /// </summary>
        private void CheckMemoryUsage()
        {
            if (_executionDepth == 0)
                return;

            // Skip check if unlimited (0)
            if (!_limits.MaxMemoryMB.HasValue || _limits.MaxMemoryMB.Value <= 0)
                return;

            // Force GC in test mode if requested
            if (_limits.TestMode && _limits.ForceGCOnMemoryCheck)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            }

            // Get memory measurement
            var currentMemory =
                _limits.TestMode && _limits.UseStableMemoryMeasurement
                    ? GC.GetTotalMemory(true) // Forces GC and waits for accurate measurement
                    : GC.GetTotalMemory(false); // Normal fast measurement

            var usedMemory = currentMemory - _baselineMemory;
            var limitBytes = _limits.MaxMemoryMB.Value * 1024L * 1024L;

            if (usedMemory > limitBytes)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Memory,
                    usedMemory,
                    limitBytes
                );

                ResourceLimitExceeded?.Invoke(this, args);

                throw new MemoryExhaustionException(
                    $"Memory limit exceeded: {usedMemory / 1024 / 1024}MB > {_limits.MaxMemoryMB.Value}MB",
                    "MemoryUsage"
                );
            }
        }

        /// <summary>
        /// Timeout callback
        /// </summary>
        private void OnTimeout(object state)
        {
            _timedOut = true;
            // Do NOT set _executionDepth = 0 here! That would disable all checks
        }

        /// <summary>
        /// Disposes the resource controller
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _timeoutTimer?.Dispose();
            StopExecution();
        }
    }

    /// <summary>
    /// Types of resources that can be limited
    /// </summary>
    public enum ResourceType
    {
        /// <summary>
        /// VM instructions executed
        /// </summary>
        Instructions,

        /// <summary>
        /// Memory usage
        /// </summary>
        Memory,

        /// <summary>
        /// Function call depth
        /// </summary>
        CallDepth,

        /// <summary>
        /// Number of tables created
        /// </summary>
        Tables,

        /// <summary>
        /// String length
        /// </summary>
        StringLength,

        /// <summary>
        /// Execution time
        /// </summary>
        ExecutionTime,
    }

    /// <summary>
    /// Event arguments for resource limit exceeded events
    /// </summary>
    public class ResourceLimitExceededEventArgs : EventArgs
    {
        /// <summary>
        /// The type of resource that exceeded its limit
        /// </summary>
        public ResourceType ResourceType { get; }

        /// <summary>
        /// The current value of the resource
        /// </summary>
        public long CurrentValue { get; }

        /// <summary>
        /// The limit that was exceeded
        /// </summary>
        public long Limit { get; }

        /// <summary>
        /// Creates a new resource limit exceeded event args
        /// </summary>
        public ResourceLimitExceededEventArgs(
            ResourceType resourceType,
            long currentValue,
            long limit
        )
        {
            ResourceType = resourceType;
            CurrentValue = currentValue;
            Limit = limit;
        }
    }
}
