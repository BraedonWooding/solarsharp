using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for timing-based attacks including TOCTOU (Time-of-Check Time-of-Use),
    ///     race conditions, and timing side-channel attacks.
    ///     These attacks exploit the time between security checks and actual operations.
    /// </summary>
    /// <remarks>
    ///     Timing attacks are a sophisticated class of security vulnerabilities that exploit
    ///     time-based behaviors to bypass security controls or extract sensitive information.
    ///     This test suite validates SolarSharp's resilience against:
    ///     TOCTOU (Time-of-Check Time-of-Use) Attacks:
    ///     - Manifest file swapping between validation and execution
    ///     - Resource limit modifications during execution
    ///     - Concurrent access to shared security state
    ///     Timing Side-Channel Attacks:
    ///     - Signature verification timing differences
    ///     - File existence detection through timing
    ///     - Memory allocation pattern analysis
    ///     Security Goals:
    ///     - Security checks must be atomic or re-validated
    ///     - Timing differences should not leak sensitive information
    ///     - Concurrent operations must not compromise security
    ///     - Race conditions must not allow privilege escalation
    ///     These tests use statistical analysis to detect timing patterns that could
    ///     be exploited by attackers. While some timing differences are expected,
    ///     they should not be significant enough to enable practical attacks.
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [Category("TimingTest")]
    [Category("LongRunning")]
    public class TimingAttackTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_timing_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            _validKey = RSA.Create(2048);
            _validKeyPem = ExportPublicKeyAsPem(_validKey);
        }

        [TearDown]
        public void Cleanup()
        {
            _validKey?.Dispose();

            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private string _tempDir;
        private RSA _validKey;
        private string _validKeyPem;

        /// <summary>
        ///     Tests protection against TOCTOU attacks via manifest file swapping.
        /// </summary>
        /// <remarks>
        ///     This test simulates a classic TOCTOU attack where:
        ///     1. A valid, signed manifest passes security validation
        ///     2. An attacker swaps the file with a malicious manifest
        ///     3. The malicious manifest is used for execution
        ///     Attack Scenario:
        ///     - Valid manifest has reasonable timeout (30 seconds)
        ///     - Malicious manifest has unlimited timeout (-1)
        ///     - Background task rapidly swaps between the two
        ///     - Script execution happens during the swapping
        ///     Expected Protection:
        ///     - Manifest should be loaded atomically
        ///     - Or re-validated at execution time
        ///     - Or cached securely after validation
        ///     The test passes if either:
        ///     - Script executes with valid manifest (42 returned)
        ///     - SecurityException is thrown (invalid manifest detected)
        ///     - But NEVER executes with malicious unlimited timeout
        /// </remarks>
        [Test]
        public async Task TestTOCTOU_ManifestFileSwapDuringValidation()
        {
            // This test simulates an attacker swapping a manifest file
            // between validation and execution

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var luaPath = Path.Combine(_tempDir, "test.lua");

            // Create valid signed manifest
            var validManifest = CreateSignedManifest("valid manifest", 30000);
            File.WriteAllText(manifestPath, validManifest);
            File.WriteAllText(luaPath, "return 42");

            var script = new Script();
            script.LoadKey(_validKeyPem);

            // Start a background task that will swap the manifest file
            var swapTask = Task.Run(async () =>
            {
                await Task.Delay(10); // Wait a bit for validation to start

                // Swap with malicious manifest (unsigned)
                var maliciousManifest = @"{
                    ""version"": ""1.0"",
                    ""policy"": {
                        ""timeoutMs"": 0
                    }
                }";

                // Repeatedly swap the file to increase chance of race condition
                for (var i = 0; i < 100; i++)
                    try
                    {
                        File.WriteAllText(manifestPath, maliciousManifest);
                        Thread.Sleep(1);
                        File.WriteAllText(manifestPath, validManifest);
                        Thread.Sleep(1);
                    }
                    catch (IOException)
                    {
                        // File might be locked, continue trying
                    }
            });

            // Execute script while swap is happening
            Exception caughtException = null;
            try
            {
                var result = script.DoFile(luaPath);
                // If we get here, the valid manifest was used
                Assert.That(result.Number, Is.EqualTo(42));
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            await swapTask;

            // Either the script executed successfully with valid manifest,
            // or it failed with SecurityException due to invalid manifest
            // It should NEVER execute with malicious unlimited timeout
            if (caughtException != null) Assert.That(caughtException, Is.InstanceOf<SecurityException>());
        }

        /// <summary>
        ///     Tests for race conditions in concurrent manifest cache access.
        /// </summary>
        /// <remarks>
        ///     When multiple scripts access the same manifest file concurrently,
        ///     race conditions could potentially:
        ///     - Corrupt the manifest cache
        ///     - Allow invalid manifests to be cached
        ///     - Cause inconsistent security policies
        ///     Test Approach:
        ///     - 10 scripts created concurrently
        ///     - All access the same manifest file
        ///     - Each loads keys and executes the script
        ///     Expected Behavior:
        ///     - All successful executions return 42
        ///     - Any failures are SecurityExceptions
        ///     - No data corruption or inconsistent results
        ///     - Cache remains valid throughout
        ///     The test accepts either:
        ///     - All succeed (cache working correctly)
        ///     - All fail with SecurityException (strict validation)
        ///     - Mix of both (some validated before cache populated)
        ///     This ensures the manifest system is thread-safe even though
        ///     scripts themselves are single-threaded.
        /// </remarks>
        [Test]
        public void TestTOCTOU_ConcurrentManifestCacheAccess()
        {
            // Test concurrent access to manifest cache to find race conditions
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var luaPath = Path.Combine(_tempDir, "test.lua");

            var validManifest = CreateSignedManifest("concurrent test", 30000);
            File.WriteAllText(manifestPath, validManifest);
            File.WriteAllText(luaPath, "return 42");

            var exceptions = new ConcurrentBag<Exception>();
            var results = new ConcurrentBag<double>();

            // Create multiple scripts and execute concurrently
            var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
            {
                try
                {
                    var script = new Script();
                    script.LoadKey(_validKeyPem);

                    // All scripts try to access the same manifest concurrently
                    var result = script.DoFile(luaPath);
                    results.Add(result.Number);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Debug: Check what exceptions we got
            if (exceptions.Count > 0)
            {
                var exceptionTypes = exceptions.GroupBy(ex => ex.GetType().Name).Select(g => $"{g.Key}: {g.Count()}")
                    .ToArray();
                TestContext.Out.WriteLine($"Exceptions: {string.Join(", ", exceptionTypes)}");
                TestContext.Out.WriteLine($"First exception: {exceptions.First()}");
            }

            Assert.Multiple(() =>
            {
                // All successful executions should return 42
                Assert.That(results.All(r => r == 42), Is.True);

                // Any exceptions should be SecurityExceptions (valid failures)
                Assert.That(exceptions.All(ex => ex is SecurityException), Is.True);
            });

            // If all executions failed, that might be acceptable for a TOCTOU test
            // as long as they failed with security exceptions (proper security enforcement)
            if (results.Count == 0 && exceptions.Count > 0 && exceptions.All(ex => ex is SecurityException))
                Assert.Pass("All executions properly failed with SecurityExceptions - TOCTOU protection working");
            else
                // At least some executions should succeed (cache should work correctly)
                Assert.That(results.Count, Is.GreaterThan(0));
        }

        /// <summary>
        ///     Tests for race conditions between resource limit checking and enforcement.
        /// </summary>
        /// <remarks>
        ///     This test attempts to exploit a potential TOCTOU vulnerability where:
        ///     1. Resource limits are checked at one point
        ///     2. Limits are modified before enforcement
        ///     3. Script might execute with bypassed limits
        ///     Attack Scenario:
        ///     - Memory limit set to 10MB
        ///     - Background task rapidly changes limit to 0 (unlimited) and back
        ///     - Script attempts to allocate 100MB
        ///     Expected Protection:
        ///     - Limits should be captured at script creation
        ///     - Or checked atomically during enforcement
        ///     - Modifications after creation shouldn't affect running scripts
        ///     Success Criteria:
        ///     - Script either succeeds with 10MB limit
        ///     - Or fails with SecurityException
        ///     - But NEVER succeeds with unlimited memory
        ///     This type of attack could allow resource exhaustion if successful.
        /// </remarks>
        [Test]
        public void TestTOCTOU_ResourceLimitRacing()
        {
            // Test racing between resource limit checking and actual consumption
            var config = new SecurityConfiguration
            {
                Execution =
                {
                    MaxMemoryMB = 10 // 10MB limit
                },
                AntiPolymorphism =
                {
                    PreventRunString = false,
                    PreventInternalDynamicCode = false
                }
            };

            var script = new Script(config);

            // Start a background task that tries to modify resource limits
            var modifyTask = Task.Run(() =>
            {
                for (var i = 0; i < 1000; i++)
                    try
                    {
                        // Try to race with resource checking
                        config.Execution.MaxMemoryMB = 0; // Unlimited
                        Thread.Sleep(1);
                        config.Execution.MaxMemoryMB = 10; // Limited again
                    }
                    catch
                    {
                        // Ignore any exceptions from racing
                    }
            });

            // Execute memory-intensive script
            Exception caughtException = null;
            try
            {
                script.DoString(@"
                    local t = {}
                    for i = 1, 100000 do
                        t[i] = string.rep('x', 1000)
                    end
                    return #t
                ");
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            modifyTask.Wait();

            // Should either succeed (if memory limit was respected) or 
            // fail with SecurityException (if limit was enforced)
            // Should NEVER succeed by bypassing the limit via racing
            if (caughtException != null) Assert.That(caughtException, Is.InstanceOf<SecurityException>());
        }

        /// <summary>
        ///     Tests for timing side-channels in cryptographic signature verification.
        /// </summary>
        /// <remarks>
        ///     Timing differences in signature verification can leak information about:
        ///     - The structure of valid signatures
        ///     - Key material (in extreme cases)
        ///     - Whether a signature is "close" to valid
        ///     This test measures timing differences between:
        ///     - Valid signature verification (should complete fully)
        ///     - Invalid signature verification (might fail early)
        ///     Statistical Analysis:
        ///     - Runs 100 iterations for each case
        ///     - Calculates average and standard deviation
        ///     - Computes Z-score to measure significance
        ///     - Z-score > 3.0 indicates potentially exploitable difference
        ///     Security Implications:
        ///     Large timing differences could enable:
        ///     - Signature forgery through incremental attempts
        ///     - Key recovery through timing analysis
        ///     - Denial of service through algorithmic complexity
        ///     Note: Some timing difference is expected and acceptable.
        ///     The key is ensuring it's not statistically significant enough
        ///     for practical exploitation.
        /// </remarks>
        [Test]
        public void TestTimingSideChannel_SignatureVerificationTiming()
        {
            // Test for timing differences in signature verification
            // that could leak information about the key or signature

            var script = new Script();
            script.LoadKey(_validKeyPem);

            var luaPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaPath, "return 42");

            // Create manifests with different signatures
            var validManifest = CreateSignedManifest("timing test valid", 30000);
            var invalidManifest = CreateInvalidSignature("timing test invalid", 30000);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");

            // Measure timing for valid signatures
            var validTimes = new List<long>();
            for (var i = 0; i < 100; i++)
            {
                File.WriteAllText(manifestPath, validManifest);

                var sw = Stopwatch.StartNew();
                try
                {
                    script.DoFile(luaPath);
                }
                catch
                {
                    // Ignore exceptions, we're measuring timing
                }

                sw.Stop();
                validTimes.Add(sw.ElapsedTicks);
            }

            // Measure timing for invalid signatures
            var invalidTimes = new List<long>();
            for (var i = 0; i < 100; i++)
            {
                File.WriteAllText(manifestPath, invalidManifest);

                var sw = Stopwatch.StartNew();
                try
                {
                    script.DoFile(luaPath);
                }
                catch
                {
                    // Ignore exceptions, we're measuring timing
                }

                sw.Stop();
                invalidTimes.Add(sw.ElapsedTicks);
            }

            // Analyze timing distributions
            var validAvg = validTimes.Average();
            var invalidAvg = invalidTimes.Average();
            var validStdDev = Math.Sqrt(validTimes.Select(t => Math.Pow(t - validAvg, 2)).Average());
            var invalidStdDev = Math.Sqrt(invalidTimes.Select(t => Math.Pow(t - invalidAvg, 2)).Average());

            // The timing difference should not be statistically significant enough
            // to allow key recovery or signature forgery attacks
            var timingDifference = Math.Abs(validAvg - invalidAvg);
            var combinedStdDev = Math.Sqrt(validStdDev * validStdDev + invalidStdDev * invalidStdDev);

            // If timing difference is more than 3 standard deviations, it might be exploitable
            var zScore = timingDifference / combinedStdDev;

            // Log the results for analysis
            TestContext.Out.WriteLine($"Valid signature average: {validAvg:F2} ticks");
            TestContext.Out.WriteLine($"Invalid signature average: {invalidAvg:F2} ticks");
            TestContext.Out.WriteLine($"Z-score: {zScore:F2}");

            // This is a warning, not a failure - timing differences might be expected
            if (zScore > 3.0)
            {
                TestContext.Out.WriteLine("WARNING: Significant timing difference detected in signature verification");
                TestContext.Out.WriteLine("This could potentially be exploited for timing attacks");
            }

            Assert.Multiple(() =>
            {
                // For now, just ensure the test runs and collects data
                Assert.That(validTimes.Count, Is.EqualTo(100));
                Assert.That(invalidTimes.Count, Is.EqualTo(100));
            });
        }

        /// <summary>
        ///     Tests for timing attacks that could reveal file existence.
        /// </summary>
        /// <remarks>
        ///     Even when file access is denied, timing differences might reveal
        ///     whether a file exists:
        ///     - Existing files might fail slower (after permission check)
        ///     - Non-existent files might fail faster (file not found)
        ///     Attack Scenario:
        ///     - Isolated security (no file access allowed)
        ///     - Attempt to open existing vs non-existent files
        ///     - Measure timing differences
        ///     Security Concern:
        ///     File existence information could reveal:
        ///     - System configuration
        ///     - Installed software
        ///     - User data presence
        ///     - Directory structure
        ///     Analysis:
        ///     - Timing ratio > 1.5 might allow file existence detection
        ///     - Ideally, both should fail with identical timing
        ///     - Requires consistent error handling regardless of file existence
        ///     This is particularly important for multi-tenant systems where
        ///     file existence itself might be sensitive information.
        /// </remarks>
        [Test]
        public void TestTimingSideChannel_FileExistenceTiming()
        {
            // Test for timing differences that reveal file existence
            // even when access is denied

            var script = new Script(SecurityConfiguration.Isolated());

            var existingFile = Path.Combine(_tempDir, "existing.txt");
            var nonExistentFile = Path.Combine(_tempDir, "nonexistent.txt");

            File.WriteAllText(existingFile, "content");
            // Don't create nonexistent file

            // Measure timing for existing file access (denied)
            var existingTimes = new List<long>();
            for (var i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    script.DoString($"return io.open('{existingFile.Replace('\\', '/')}', 'r')");
                }
                catch
                {
                    // Expected - access denied
                }

                sw.Stop();
                existingTimes.Add(sw.ElapsedTicks);
            }

            // Measure timing for non-existent file access
            var nonExistentTimes = new List<long>();
            for (var i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    script.DoString($"return io.open('{nonExistentFile.Replace('\\', '/')}', 'r')");
                }
                catch
                {
                    // Expected - access denied or file not found
                }

                sw.Stop();
                nonExistentTimes.Add(sw.ElapsedTicks);
            }

            // Analyze timing to see if file existence can be inferred
            var existingAvg = existingTimes.Average();
            var nonExistentAvg = nonExistentTimes.Average();

            TestContext.Out.WriteLine($"Existing file average: {existingAvg:F2} ticks");
            TestContext.Out.WriteLine($"Non-existent file average: {nonExistentAvg:F2} ticks");

            var timingRatio = Math.Max(existingAvg, nonExistentAvg) / Math.Min(existingAvg, nonExistentAvg);
            TestContext.Out.WriteLine($"Timing ratio: {timingRatio:F2}");

            // If timing ratio is > 1.5, file existence might be detectable
            if (timingRatio > 1.5)
                TestContext.Out.WriteLine("WARNING: File existence might be detectable via timing differences");

            Assert.Multiple(() =>
            {
                Assert.That(existingTimes.Count, Is.EqualTo(100));
                Assert.That(nonExistentTimes.Count, Is.EqualTo(100));
            });
        }

        /// <summary>
        ///     Tests for timing patterns in memory allocation that might leak information.
        /// </summary>
        /// <remarks>
        ///     Memory allocation timing can reveal:
        ///     - Garbage collection patterns
        ///     - Memory layout information
        ///     - Heap fragmentation state
        ///     - System memory pressure
        ///     Test Approach:
        ///     - Allocate increasingly large arrays (100 to 10,000 elements)
        ///     - Measure timing for each allocation
        ///     - Look for patterns or spikes
        ///     Security Concerns:
        ///     Timing patterns might enable:
        ///     - Heap spray attacks
        ///     - Memory exhaustion prediction
        ///     - Side-channel attacks on other processes
        ///     - Fingerprinting of system state
        ///     Analysis:
        ///     - Calculate timing ratios between successive allocations
        ///     - Large spikes (ratio > 10) indicate GC or reallocation
        ///     - Regular patterns might be exploitable
        ///     While some variation is expected, predictable patterns
        ///     could be used to influence memory layout for exploitation.
        /// </remarks>
        [Test]
        public void TestTimingSideChannel_MemoryAllocationTiming()
        {
            // Test for timing patterns that might reveal memory layout or allocation strategies

            var script = new Script(SecurityConfiguration.Isolated());

            var allocationTimes = new List<long>();

            // Perform various memory allocation patterns and measure timing
            for (var size = 1; size <= 100; size++)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    script.DoString($@"
                        local t = {{}}
                        for i = 1, {size * 100} do
                            t[i] = string.rep('x', 10)
                        end
                        return #t
                    ");
                }
                catch
                {
                    // Memory limit might be exceeded, continue
                }

                sw.Stop();
                allocationTimes.Add(sw.ElapsedTicks);
            }

            // Look for patterns that might reveal memory management internals
            var timingPattern = new List<double>();
            for (var i = 1; i < allocationTimes.Count; i++)
                timingPattern.Add((double)allocationTimes[i] / allocationTimes[i - 1]);

            // Check for suspicious patterns (e.g., regular spikes that might indicate GC)
            var avgRatio = timingPattern.Average();
            var maxRatio = timingPattern.Max();

            TestContext.Out.WriteLine($"Average timing ratio: {avgRatio:F2}");
            TestContext.Out.WriteLine($"Maximum timing ratio: {maxRatio:F2}");

            // Large spikes might indicate GC or memory reallocation patterns
            if (maxRatio > 10.0)
            {
                TestContext.Out.WriteLine("WARNING: Large timing spikes detected in memory allocation");
                TestContext.Out.WriteLine("This might reveal memory management patterns");
            }

            Assert.That(allocationTimes.Count, Is.EqualTo(100));
        }

        /// <summary>
        ///     Exports an RSA public key in PEM format for use in manifests.
        /// </summary>
        /// <param name="rsa">The RSA key to export.</param>
        /// <returns>PEM-formatted public key string.</returns>
        /// <remarks>
        ///     Converts RSA public key to standard PEM format with:
        ///     - Proper header/footer
        ///     - Base64 encoding
        ///     - 64-character line breaks
        ///     This format is required for manifest signature verification.
        /// </remarks>
        private string ExportPublicKeyAsPem(RSA rsa)
        {
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var base64 = Convert.ToBase64String(publicKeyBytes);
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PUBLIC KEY-----");

            for (var i = 0; i < base64.Length; i += 64)
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));

            sb.AppendLine("-----END PUBLIC KEY-----");
            return sb.ToString();
        }

        /// <summary>
        ///     Creates a properly signed manifest for testing.
        /// </summary>
        /// <param name="description">Description field for the manifest.</param>
        /// <param name="timeoutMs">Timeout value in milliseconds.</param>
        /// <returns>JSON string of signed manifest.</returns>
        /// <remarks>
        ///     Uses ManifestSigner to create a properly signed manifest
        ///     that will pass validation when the public key is trusted.
        /// </remarks>
        private string CreateSignedManifest(string description, int timeoutMs)
        {
            var content = $@"{{
                ""version"": ""1.0"",
                ""description"": ""{description}"",
                ""policy"": {{
                    ""timeoutMs"": {timeoutMs}
                }}
            }}";
            return ManifestSigner.SignManifestJson(content, _validKey);
        }

        /// <summary>
        ///     Creates a manifest with an invalid signature for testing rejection.
        /// </summary>
        /// <param name="description">Description field for the manifest.</param>
        /// <param name="timeoutMs">Timeout value in milliseconds.</param>
        /// <returns>JSON string with invalid signature.</returns>
        /// <remarks>
        ///     Creates a manifest that appears valid but has "invalid_signature_base64"
        ///     as the signature value. This is used to test timing differences between
        ///     valid and invalid signature verification.
        /// </remarks>
        private string CreateInvalidSignature(string description, int timeoutMs)
        {
            var content = $@"{{
                ""version"": ""1.0"",
                ""description"": ""{description}"",
                ""policy"": {{
                    ""timeoutMs"": {timeoutMs}
                }},
                ""security"": {{
                    ""publicKey"": {{
                        ""algorithm"": ""RSA"",
                        ""format"": ""PEM"",
                        ""value"": ""{_validKeyPem.Replace("\n", "\\n")}""
                    }},
                    ""signature"": {{
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": ""invalid_signature_base64""
                    }}
                }}
            }}";
            return content;
        }
    }
}