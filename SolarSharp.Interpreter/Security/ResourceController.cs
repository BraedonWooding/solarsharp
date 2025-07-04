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

                // Capture baseline memory
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                _baselineMemory = GC.GetTotalMemory(false);

                // Start timers
                _executionTimer.Restart();
                _timeoutTimer.Change(_limits.Timeout, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Stops monitoring resource usage
        /// </summary>
        public void StopExecution()
        {
            var depth = Interlocked.Decrement(ref _executionDepth);
            
            // Only stop timers when we reach the outermost execution level
            if (depth == 0)
            {
                _executionTimer.Stop();
                _timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Checks if execution has timed out
        /// </summary>
        public void CheckTimeout()
        {
            if (_timedOut)
            {
                throw new ExecutionTimeoutException(
                    $"Script execution timeout after {_limits.Timeout}",
                    "CheckTimeout");
            }
        }

        /// <summary>
        /// Increments instruction count and checks limit
        /// </summary>
        public void IncrementInstructionCount()
        {
            if (_executionDepth == 0) return;

            if (++_instructionCount > _limits.MaxInstructions)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Instructions,
                    _instructionCount,
                    _limits.MaxInstructions);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new ResourceLimitExceededException(
                    $"Instruction limit exceeded: {_instructionCount} > {_limits.MaxInstructions}",
                    "InstructionCount");
            }

            // Check timeout periodically
            if (_instructionCount % 1000 == 0)
            {
                CheckTimeout();
                CheckMemoryUsage();
            }
        }

        /// <summary>
        /// Enters a function call and checks depth limit
        /// </summary>
        public void EnterFunction()
        {
            if (_executionDepth == 0) return;

            if (++_callDepth > _limits.MaxCallDepth)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.CallDepth,
                    _callDepth,
                    _limits.MaxCallDepth);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new CallDepthExceededException(
                    $"Call depth limit exceeded: {_callDepth} > {_limits.MaxCallDepth}",
                    "CallDepth");
            }
        }

        /// <summary>
        /// Exits a function call
        /// </summary>
        public void ExitFunction()
        {
            if (_executionDepth == 0) return;
            
            if (_callDepth > 0)
                _callDepth--;
        }

        /// <summary>
        /// Increments table count and checks limit
        /// </summary>
        public void IncrementTableCount()
        {
            if (_executionDepth == 0) return;

            if (++_tableCount > _limits.MaxTables)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Tables,
                    _tableCount,
                    _limits.MaxTables);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new ResourceLimitExceededException(
                    $"Table limit exceeded: {_tableCount} > {_limits.MaxTables}",
                    "TableCount");
            }
        }

        /// <summary>
        /// Checks string length against limit
        /// </summary>
        public void CheckStringLength(int length)
        {
            if (_executionDepth == 0) return;

            if (length > _limits.MaxStringLength)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.StringLength,
                    length,
                    _limits.MaxStringLength);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new ResourceLimitExceededException(
                    $"String length limit exceeded: {length} > {_limits.MaxStringLength}",
                    "StringLength");
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
            if (_executionDepth == 0) return;

            CheckTimeout();

            // Check instruction count
            if (_instructionCount > _limits.MaxInstructions)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Instructions,
                    _instructionCount,
                    _limits.MaxInstructions);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new ResourceLimitExceededException(
                    $"Instruction limit exceeded: {_instructionCount} > {_limits.MaxInstructions}",
                    "InstructionCount");
            }

            // Check call depth
            if (_callDepth > _limits.MaxCallDepth)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.CallDepth,
                    _callDepth,
                    _limits.MaxCallDepth);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new CallDepthExceededException(
                    $"Call depth limit exceeded: {_callDepth} > {_limits.MaxCallDepth}",
                    "CallDepth");
            }

            // Check memory periodically
            CheckMemoryUsage();
        }

        /// <summary>
        /// Checks current memory usage
        /// </summary>
        private void CheckMemoryUsage()
        {
            if (_executionDepth == 0) return;

            var currentMemory = GC.GetTotalMemory(false);
            var usedMemory = currentMemory - _baselineMemory;
            var limitBytes = _limits.MaxMemoryMB * 1024L * 1024L;

            if (usedMemory > limitBytes)
            {
                var args = new ResourceLimitExceededEventArgs(
                    ResourceType.Memory,
                    usedMemory,
                    limitBytes);

                ResourceLimitExceeded?.Invoke(this, args);

                throw new MemoryExhaustionException(
                    $"Memory limit exceeded: {usedMemory / 1024 / 1024}MB > {_limits.MaxMemoryMB}MB",
                    "MemoryUsage");
            }
        }

        /// <summary>
        /// Timeout callback
        /// </summary>
        private void OnTimeout(object state)
        {
            _timedOut = true;
            _executionDepth = 0;
        }

        /// <summary>
        /// Disposes the resource controller
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

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
        ExecutionTime
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
        /// Creates new resource limit exceeded event args
        /// </summary>
        public ResourceLimitExceededEventArgs(ResourceType resourceType, long currentValue, long limit)
        {
            ResourceType = resourceType;
            CurrentValue = currentValue;
            Limit = limit;
        }
    }
}