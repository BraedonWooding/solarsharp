using System;
using System.Runtime;
using System.Threading;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units.ResourceLimits
{
    /// <summary>
    ///     Base class for resource limit tests providing JIT warm-up, memory stabilization,
    ///     and test isolation to ensure deterministic behavior.
    /// </summary>
    /// <remarks>
    ///     This base class addresses several sources of non-determinism:
    ///     - JIT compilation effects through warm-up
    ///     - GC timing through forced collection and stabilization
    ///     - Memory measurement variations through multiple collection passes
    ///     - Test order dependencies through proper setup/teardown
    ///     Tests inheriting from this class should be marked with [NonParallelizable]
    ///     and appropriate [Order] attributes to ensure proper execution.
    /// </remarks>
    public abstract class ResourceLimitTestBase
    {
        private static bool _warmupComplete;
        private static readonly object _warmupLock = new();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Ensure warm-up happens once per test run
            lock (_warmupLock)
            {
                if (!_warmupComplete)
                {
                    WarmUpJIT();
                    _warmupComplete = true;
                }
            }
        }

        [SetUp]
        public void BaseSetUp()
        {
            // Force GC and wait for stability
            StabilizeMemory();

            // Record test environment
            LogTestEnvironment();
        }

        [TearDown]
        public void BaseTearDown()
        {
            // Clean up thoroughly
            StabilizeMemory();
        }

        private static void WarmUpJIT()
        {
            TestContext.WriteLine("Warming up JIT compiler...");

            // Run sample scripts to warm up JIT
            var warmupConfig = new SecurityConfiguration()
                .WithMemoryLimitMB(10)
                .WithInstructionLimit(100000)
                .WithModules(CoreModules.Basic | CoreModules.Table | CoreModules.String | CoreModules.Math);

            var warmupScript = new Script(warmupConfig);

            // Execute various operations to trigger JIT
            try
            {
                warmupScript.DoString(@"
                    -- Arithmetic operations
                    local sum = 0
                    for i = 1, 1000 do sum = sum + i end
                    
                    -- Table operations  
                    local t = {}
                    for i = 1, 100 do t[i] = i * 2 end
                    
                    -- String operations (non-recursive)
                    local s = 'test'
                    for i = 1, 10 do s = tostring(i) end
                    
                    -- Function calls
                    local function f(x) return x + 1 end
                    for i = 1, 100 do f(i) end
                ");

                TestContext.WriteLine("JIT warm-up completed successfully");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"JIT warm-up failed (non-critical): {ex.Message}");
            }
        }

        /// <summary>
        ///     Stabilizes memory by forcing GC until memory usage stabilizes
        /// </summary>
        protected static void StabilizeMemory()
        {
            // Multiple GC passes with verification
            long previousMemory;
            var currentMemory = GC.GetTotalMemory(false);
            var stabilizationAttempts = 0;

            do
            {
                previousMemory = currentMemory;

                // Force aggressive collection
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.WaitForFullGCComplete();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                currentMemory = GC.GetTotalMemory(false);
                stabilizationAttempts++;

                // Wait a bit for memory to settle
                Thread.Sleep(10);
            } while (Math.Abs(currentMemory - previousMemory) > 1024 * 1024 // 1MB threshold
                     && stabilizationAttempts < 10);

            if (stabilizationAttempts >= 10)
                TestContext.WriteLine($"Warning: Memory did not stabilize after {stabilizationAttempts} attempts");
        }

        /// <summary>
        ///     Gets a stable memory measurement after forcing GC
        /// </summary>
        protected static long GetStableMemoryUsage()
        {
            StabilizeMemory();
            return GC.GetTotalMemory(false);
        }

        private void LogTestEnvironment()
        {
            TestContext.WriteLine($"Test Environment for {TestContext.CurrentContext.Test.Name}:");
            TestContext.WriteLine($"  Platform: {Environment.OSVersion}");
            TestContext.WriteLine($"  .NET Version: {Environment.Version}");
            TestContext.WriteLine($"  Processor Count: {Environment.ProcessorCount}");
            TestContext.WriteLine($"  Available Memory: {GC.GetTotalMemory(false):N0} bytes");
            TestContext.WriteLine($"  GC Mode: {(GCSettings.IsServerGC ? "Server" : "Workstation")}");
            TestContext.WriteLine($"  CI Environment: {Environment.GetEnvironmentVariable("CI") ?? "false"}");
            TestContext.WriteLine(
                $"  GitHub Actions: {Environment.GetEnvironmentVariable("GITHUB_ACTIONS") ?? "false"}");
        }

        /// <summary>
        ///     Gets platform-appropriate memory limit for testing
        /// </summary>
        protected static int GetPlatformMemoryLimit(int defaultLimit = 10)
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                // Conservative limits for CI
                return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? 2 : 5;
            return defaultLimit; // More generous for local testing
        }
    }
}