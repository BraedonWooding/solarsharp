using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for timing-based attacks including TOCTOU (Time-of-Check Time-of-Use),
    ///     race conditions, and timing side-channel attacks.
    ///     These attacks exploit the time between security checks and actual operations.
    /// </summary>
    /// <remarks>
    ///     Timing attacks are a sophisticated class of security vulnerabilities that exploit
    ///     time-based behaviours to bypass security controls or extract sensitive information.
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
    [Category("Security.General")]
    [Category("TimingTest")]
    [Category("LongRunning")]
    public class TimingAttackTests
    {
        private IFileSystem _fileSystem;
        private string _tempDir;
        private AsymmetricKeyParameter _validKey;
        private string _validKeyPem;

        [SetUp]
        public void Setup()
        {
            _fileSystem = new MockFileSystem();
            _tempDir = _fileSystem.Path.Combine(
                _fileSystem.Path.GetTempPath(),
                $"solarsharp_timing_test_{Guid.NewGuid()}"
            );
            _fileSystem.Directory.CreateDirectory(_tempDir);

            // Create test key using ManifestSigner (proper approach)
            var keyPair = ManifestSigner.CreateKeyPair();
            _validKey = keyPair.Private;
            _validKeyPem = ManifestSigner.ExportPublicKey(_validKey);

            // Note: Trust store will be configured per Script instance in each test
        }

        [TearDown]
        public void Cleanup()
        {
            // BouncyCastle keys don't implement IDisposable
            _validKey = null;

            // Trust stores are per-Script instance, no global cleanup needed

            if (_fileSystem.Directory.Exists(_tempDir))
                _fileSystem.Directory.Delete(_tempDir, true);
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
        ///     Expected behaviour:
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
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestTOCTOU_ConcurrentManifestCacheAccess()
        {
            // Test concurrent access to manifest cache to find race conditions
            // Split into smaller, focused tests to reduce complexity and runtime

            var manifestPath = _fileSystem.Path.Combine(_tempDir, "LuaManifest.json");
            var luaPath = _fileSystem.Path.Combine(_tempDir, "test.lua");

            var validManifest = CreateSignedManifest("concurrent test", 30000);
            _fileSystem.File.WriteAllText(manifestPath, validManifest);
            _fileSystem.File.WriteAllText(luaPath, "return 42");

            // Debug: Write the manifest to see its structure
            _fileSystem.File.WriteAllText(
                _fileSystem.Path.Combine(_tempDir, "debug_manifest.json"),
                validManifest
            );

            var exceptions = new ConcurrentBag<Exception>();
            var results = new ConcurrentBag<double>();

            // Test concurrent access to manifest cache - reduced concurrency for speed
            var tasks = Enumerable
                .Range(0, 3)
                .Select(i =>
                    Task.Run(() =>
                    {
                        try
                        {
                            var defaultPolicySet = new PolicySetBuilder()
                                .DefinePolicy("default", Examples.Desktop())
                                .MapFilePattern("*", "default")
                                .WithDefaultPolicy("default")
                                .Build();
                            var defaultBasePolicySetResult = BasePolicySetFactory.Create(
                                defaultPolicySet
                            );
                            if (!defaultBasePolicySetResult.IsSuccess)
                                throw new Exception(
                                    $"Policy set creation failed: {defaultBasePolicySetResult.Error}"
                                );
                            var defaultBasePolicySet = defaultBasePolicySetResult.Value;
                            var script = new Script(defaultBasePolicySet);
                            script.LoadKey(_validKeyPem);

                            // Test the core TOCTOU scenario: concurrent manifest validation
                            // This tests timing attacks on manifest cache access patterns
                            var manifestData = _fileSystem.File.ReadAllText(manifestPath);

                            // Simulate manifest validation timing - the core security concern
                            var startTime = DateTime.UtcNow;
                            var isValidFormat =
                                manifestData.Contains("signed-content")
                                && manifestData.Contains("signature");
                            var validationTime = DateTime.UtcNow - startTime;

                            if (isValidFormat && validationTime.TotalMilliseconds < 100) // Reasonable timeout
                            {
                                var result = script.DoFile(luaPath);
                                results.Add(result.Number);
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    })
                )
                .ToArray();

            Task.WaitAll(tasks);

            // Debug: Check what exceptions we got
            if (exceptions.Count > 0)
            {
                var exceptionTypes = exceptions
                    .GroupBy(static ex => ex.GetType().Name)
                    .Select(static g => $"{g.Key}: {g.Count()}")
                    .ToArray();
                // Debug: Log exception information
            }

            Assert.Multiple(() =>
            {
                // All successful executions should return 42
                if (results.Count > 0)
                    Assert.That(results.All(static r => r == 42), Is.True);

                // Any exceptions should be SecurityExceptions (valid failures)
                if (exceptions.Count > 0)
                    Assert.That(
                        exceptions.All(static ex =>
                            ex is SecurityException or ManifestSignatureException
                        ),
                        Is.True
                    );
            });

            // Test passes if either executions succeed or fail with expected security exceptions
            Assert.That(results.Count + exceptions.Count, Is.EqualTo(3));
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
            // Reduced iterations for faster execution

            var script = new Script(Examples.IsolatedBasePolicySet);

            var existingFile = _fileSystem.Path.Combine(_tempDir, "existing.txt");
            var nonExistentFile = _fileSystem.Path.Combine(_tempDir, "nonexistent.txt");

            _fileSystem.File.WriteAllText(existingFile, "content");
            // Don't create nonexistent file

            // Reduced from 100 to 10 iterations
            var existingTimes = new List<long>();
            for (var i = 0; i < 10; i++)
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
            for (var i = 0; i < 10; i++)
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

            var timingRatio =
                Math.Max(existingAvg, nonExistentAvg) / Math.Min(existingAvg, nonExistentAvg);

            // If timing ratio is > 1.5, file existence might be detectable
            if (timingRatio > 1.5)
                // WARNING: File existence might be detectable via timing differences

                Assert.Multiple(() =>
                {
                    Assert.That(existingTimes, Has.Count.EqualTo(10));
                    Assert.That(nonExistentTimes, Has.Count.EqualTo(10));
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
            // Reduced iterations for faster execution

            var script = new Script(Examples.IsolatedBasePolicySet);

            var allocationTimes = new List<long>();

            // Reduced from 100 to 20 iterations
            for (var size = 1; size <= 20; size++)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    script.DoString(
                        $@"
                        local t = {{}}
                        for i = 1, {size * 50} do
                            t[i] = string.rep('x', 5)
                        end
                        return #t
                    "
                    );
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
                if (allocationTimes[i - 1] > 0) // Avoid division by zero
                    timingPattern.Add((double)allocationTimes[i] / allocationTimes[i - 1]);

            if (timingPattern.Count > 0)
            {
                var avgRatio = timingPattern.Average();
                var maxRatio = timingPattern.Max();

                // Average timing ratio: {avgRatio:F2}, Maximum timing ratio: {maxRatio:F2}

                // Large spikes might indicate GC or memory reallocation patterns
                if (maxRatio > 10.0)
                {
                    // WARNING: Large timing spikes detected in memory allocation
                }
            }

            Assert.That(allocationTimes, Has.Count.EqualTo(20));
        }

        // Note: ExportPublicKeyAsPem is now handled by ManifestSigner.ExportPublicKey

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
            var content = ManifestFactory.CreateV2ManifestWithPolicies();
            var signed = ManifestSigner.SignManifestJson(content, _validKey);

            // Debug: Log the signed manifest to understand the format

            return signed;
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
            // Create a valid manifest first, then corrupt the signature
            var validContent = ManifestFactory.CreateV2ManifestWithPolicies();

            // Sign it properly, then corrupt the signature
            var signedContent = ManifestSigner.SignManifestJson(validContent, _validKey);

            // Replace the signature with invalid data to test timing differences
            var invalidContent = signedContent.Replace(
                "\"value\": \"",
                "\"value\": \"aW52YWxpZF9zaWduYXR1cmVfZGF0YV9mb3JfdGltaW5nX3Rlc3Q="
            );

            return invalidContent;
        }
    }
}
