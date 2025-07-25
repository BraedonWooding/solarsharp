using System;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Tests.Benchmarks
{
    [TestFixture]
    public class MandelBenchmarkTest
    {
        [Test]
        [Category("Benchmark")]
        public void MandelBenchmark_RunsWithUnlimitedTables()
        {
            // Use the benchmark policy set with unlimited tables
            var policySet = Examples.BenchmarkUnlimitedBasePolicySet;
            var policy = Examples.BenchmarkUnlimitedSecurityPolicy;
            Assert.That(policy.MaxTables, Is.EqualTo(SecurityConstants.UnlimitedTables), "Benchmark policy should have unlimited tables");

            var script = new Script(policySet);
            
            // Inject global math functions like the benchmark implementation does
            script.DoString(@"
                -- Make sqrt global for mandel.lua compatibility
                sqrt = math.sqrt
            ");

            // The mandel benchmark creates 65,536+ tables (256x256 grid)
            // This simulates the core pattern
            var mandelSimulation = @"
                -- Simplified mandel pattern that creates many tables
                local size = 256
                local pixels = {}
                
                -- Create a table for each pixel (like mandel.lua does)
                for y = 1, size do
                    pixels[y] = {}
                    for x = 1, size do
                        -- Each complex number is a table in the original
                        pixels[y][x] = { r = x / size, i = y / size }
                    end
                end
                
                -- Verify we created the expected number of tables
                local count = 0
                for y = 1, size do
                    for x = 1, size do
                        if pixels[y][x] then count = count + 1 end
                    end
                end
                
                return count
            ";

            var result = script.DoString(mandelSimulation);
            
            // Should have created 65,536 tables without hitting a limit
            Assert.That(result.Number, Is.EqualTo(65536));
        }

        [Test]
        [Category("Benchmark")]
        public void MandelBenchmark_FailsWithDefaultLimit()
        {
            // Use DesktopBasePolicySet with limited tables
            var script = new Script(
                Examples.DesktopBasePolicySet
                    .WithMaxTables(10_000)  // Not enough for full mandel (needs 65,536+)
            );

            // The same pattern should fail with table limit
            var mandelSimulation = @"
                local size = 256
                local pixels = {}
                
                for y = 1, size do
                    pixels[y] = {}
                    for x = 1, size do
                        pixels[y][x] = { r = x / size, i = y / size }
                    end
                end
            ";

            var ex = Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(mandelSimulation)
            );

            Assert.That(ex.Message, Does.Contain("Table limit exceeded"));
        }

        [Test]
        [Category("Benchmark")]
        [Ignore("Requires actual mandel.lua file with proper global function setup")]
        public void MandelBenchmark_ActualFile()
        {
            // This test would run the actual mandel.lua file if available
            // and if global math functions were properly injected
            var mandelPath = Path.Combine("..", "..", "..", "..", "Benchmark", "Tests", "mandel.lua");
            if (!File.Exists(mandelPath))
            {
                Assert.Ignore($"Benchmark file not found: {mandelPath}");
                return;
            }

            var policySet = Examples.BenchmarkUnlimitedBasePolicySet;
            var script = new Script(policySet);
            
            // Inject global functions that mandel.lua expects
            script.DoString(@"
                -- Make math functions global for Lua 5.1 compatibility
                sqrt = math.sqrt
                abs = math.abs
                sin = math.sin
                cos = math.cos
            ");

            var result = script.DoFile(mandelPath);
            Assert.IsNotNull(result);
        }
    }
}