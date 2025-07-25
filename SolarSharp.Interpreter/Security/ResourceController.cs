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
        
        // Cumulative counters
        private long _instructionCount;
        private int _callDepth;
        private int _tableCount;
        
        // Per-execution counters
        private long _executionInstructionCount;
        private int _executionCallDepth;
        private int _executionTableCount;
        
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
                
                // Always reset per-execution counters
                _executionInstructionCount = 0;
                _executionCallDepth = 0;
                _executionTableCount = 0;
                
                // Reset cumulative counters only if PerExecution mode
                if (_limits.ResourceLimitScope == ResourceLimitScope.PerExecution)
                {
                    _instructionCount = 0;
                    _callDepth = 0;
                    _tableCount = 0;
                }

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

                // Handle timeout based on new semantics
                if (_limits.TimeoutMs == SecurityConstants.DenyLimit)
                {
                    // Timeout set to 0 means deny immediately
                    throw new ExecutionTimeoutException(
                        "Script execution denied by policy (timeout=0)",
                        "StartExecution"
                    );
                }
                else if (_limits.TimeoutMs > 0)
                {
                    // Start timeout timer with positive limit
                    _timeoutTimer.Change(
                        TimeSpan.FromMilliseconds(_limits.TimeoutMs.Value),
                        TimeSpan.FromMilliseconds(-1)
                    );
                }
                else
                {
                    // Disable timer for unlimited timeout (-1 or null)
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
        /// Checks for deny limits (0 value) and throws appropriate exceptions
        /// </summary>
        private void CheckNullLimits()
        {
            // NEW SEMANTICS: 0 = deny, -1 = unlimited, >0 = actual limit
            // Check each limit and throw if set to deny (0)
            
            if (_limits.MaxMemoryMB == SecurityConstants.DenyLimit)
            {
                throw new MemoryExhaustionException(
                    "Memory usage denied by policy (MaxMemoryMB=0)",
                    "StartExecution"
                );
            }
            
            if (_limits.MaxInstructions == SecurityConstants.DenyLimit)
            {
                throw new InstructionLimitExceededException(
                    "Instruction execution denied by policy (MaxInstructions=0)",
                    "StartExecution"
                );
            }
            
            if (_limits.MaxCallDepth == SecurityConstants.DenyLimit)
            {
                throw new CallDepthExceededException(
                    "Function calls denied by policy (MaxCallDepth=0)",
                    "StartExecution"
                );
            }
            
            if (_limits.MaxTables == SecurityConstants.DenyLimit)
            {
                throw new ResourceLimitExceededException(
                    "Table creation denied by policy (MaxTables=0)",
                    "StartExecution"
                );
            }
            
            if (_limits.MaxStringLength == SecurityConstants.DenyLimit)
            {
                throw new ResourceLimitExceededException(
                    "String creation denied by policy (MaxStringLength=0)",
                    "StartExecution"
                );
            }
        }

        /// <summary>
        /// Increments the instruction count and checks against the configured limit.
        /// Tracks both per-execution and cumulative counts based on <see cref="ResourceLimitScope"/>.
        /// Also performs periodic memory and timeout checks based on <see cref="ExecutionLimits.CheckMemoryEveryNInstructions"/>.
        /// Throws appropriate exceptions if any resource limit is exceeded.
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

            // Always increment both counters
            ++_instructionCount;
            ++_executionInstructionCount;

            // Skip check if unlimited (-1)
            if (_limits.MaxInstructions > 0)
            {
                // Choose which counter to check based on ResourceLimitScope
                var countToCheck = _limits.ResourceLimitScope == ResourceLimitScope.PerExecution 
                    ? _executionInstructionCount 
                    : _instructionCount;
                    
                if (countToCheck > _limits.MaxInstructions.Value)
                {
                    var args = new ResourceLimitExceededEventArgs(
                        ResourceType.Instructions,
                        countToCheck,
                        _limits.MaxInstructions.Value
                    );

                    ResourceLimitExceeded?.Invoke(this, args);

                    throw new InstructionLimitExceededException(
                        $"Instruction limit exceeded: {countToCheck} > {_limits.MaxInstructions.Value}",
                        "InstructionCount"
                    );
                }
            }

            // Check memory more frequently in test mode
            var checkInterval = _limits.TestMode ? _limits.CheckMemoryEveryNInstructions : 1000;
            if (_instructionCount % checkInterval != 0)
                return;
            CheckTimeout();
            CheckMemoryUsage();
        }

        /// <summary>
        /// Enters a function call and increments the call depth counter.
        /// Tracks both per-execution and cumulative call depth based on <see cref="ResourceLimitScope"/>.
        /// Throws <see cref="CallDepthExceededException"/> if the maximum call depth is exceeded.
        /// </summary>
        public void EnterFunction()
        {
            if (_executionDepth == 0)
                return;

            // Always increment both counters
            ++_callDepth;
            ++_executionCallDepth;

            // Skip check if unlimited (-1)
            if (_limits.MaxCallDepth > 0)
            {
                // Choose which counter to check based on ResourceLimitScope
                var depthToCheck = _limits.ResourceLimitScope == ResourceLimitScope.PerExecution 
                    ? _executionCallDepth 
                    : _callDepth;
                    
                if (depthToCheck > _limits.MaxCallDepth.Value)
                {
                    var args = new ResourceLimitExceededEventArgs(
                        ResourceType.CallDepth,
                        depthToCheck,
                        _limits.MaxCallDepth.Value
                    );

                    ResourceLimitExceeded?.Invoke(this, args);

                    throw new CallDepthExceededException(
                        $"Call depth limit exceeded: {depthToCheck} > {_limits.MaxCallDepth.Value}",
                        "CallDepth"
                    );
                }
            }
        }

        /// <summary>
        /// Exits a function call and decrements the call depth counters.
        /// Decrements both per-execution and cumulative call depth to maintain accurate tracking.
        /// </summary>
        public void ExitFunction()
        {
            if (_executionDepth == 0)
                return;

            if (_callDepth > 0)
                _callDepth--;
                
            if (_executionCallDepth > 0)
                _executionCallDepth--;
        }

        /// <summary>
        /// Increments table count and checks against the configured limit.
        /// Tracks both per-execution and cumulative counts based on <see cref="ResourceLimitScope"/>.
        /// Throws <see cref="ResourceLimitExceededException"/> if the limit is exceeded.
        /// </summary>
        public void IncrementTableCount()
        {
            if (_executionDepth == 0)
                return;

            // Always increment both counters
            ++_tableCount;
            ++_executionTableCount;

            // Skip check if unlimited (-1)
            if (_limits.MaxTables > 0)
            {
                // Choose which counter to check based on ResourceLimitScope
                var countToCheck = _limits.ResourceLimitScope == ResourceLimitScope.PerExecution 
                    ? _executionTableCount 
                    : _tableCount;
                    
                if (countToCheck > _limits.MaxTables.Value)
                {
                    var args = new ResourceLimitExceededEventArgs(
                        ResourceType.Tables,
                        countToCheck,
                        _limits.MaxTables.Value
                    );

                    ResourceLimitExceeded?.Invoke(this, args);

                    throw new ResourceLimitExceededException(
                        $"Table limit exceeded: {countToCheck} > {_limits.MaxTables.Value}",
                        "TableCount"
                    );
                }
            }
        }

        /// <summary>
        /// Checks string length against limit
        /// </summary>
        public void CheckStringLength(int length)
        {
            if (_executionDepth == 0)
                return;

            // Skip check if unlimited (-1)
            if (_limits.MaxStringLength > 0 && length > _limits.MaxStringLength.Value)
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

            // Check instruction count if not unlimited (-1)
            if (_limits.MaxInstructions > 0 && _instructionCount > _limits.MaxInstructions.Value)
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

            // Check call depth if not unlimited (-1)
            if (_limits.MaxCallDepth > 0 && _callDepth > _limits.MaxCallDepth.Value)
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

            // Skip check if unlimited (-1) or not set
            if (_limits.MaxMemoryMB is null or < 0)
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
