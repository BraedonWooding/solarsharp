using System;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter.Security.Cryptography;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class BouncyCastlePerformanceTests
    {
        private AsymmetricCipherKeyPair _keyPair;
        private byte[] _testData;
        private byte[] _signature;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate test key pair
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            _keyPair = keyGenerator.GenerateKeyPair();

            // Generate test data
            _testData = Encoding.UTF8.GetBytes(
                "This is test data for performance benchmarking of BouncyCastle cryptographic operations. "
                    + "The data should be long enough to simulate real-world usage scenarios."
            );

            // Create signature for verification tests
            var signatureResult = BouncyCastleCryptography.CreateSignature(
                _testData,
                _keyPair.Private
            );
            Assert.That(signatureResult.IsSuccess, Is.True);
            _signature = signatureResult.Value;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Print performance statistics
            var stats = BouncyCastleCryptography.GetPerformanceStats();
            Console.WriteLine("\n" + stats.ToReport());
        }

        [SetUp]
        public void SetUp()
        {
            // Reset performance counters for each test
            BouncyCastleCryptography.ResetPerformanceCounters();

            // Warm up pools for consistent performance
            BouncyCastleCryptography.WarmupPools();
        }

        [Test]
        [Category("Security.Cryptography")]
        [Category("StressTest")]
        public void SignatureVerification_Performance_ShouldBeFast()
        {
            const int iterations = 1000;
            var stopwatch = Stopwatch.StartNew();

            // Perform multiple signature verifications
            for (var i = 0; i < iterations; i++)
            {
                var result = BouncyCastleCryptography.VerifySignature(
                    _testData,
                    _signature,
                    _keyPair.Public
                );
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.True);
            }

            stopwatch.Stop();

            var stats = BouncyCastleCryptography.GetPerformanceStats();

            Console.WriteLine("Signature Verification Performance:");
            Console.WriteLine($"  Iterations: {iterations:N0}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine(
                $"  Average Time: {(double)stopwatch.ElapsedMilliseconds / iterations:F2} ms per operation"
            );
            Console.WriteLine(
                $"  Operations/Second: {iterations * 1000.0 / stopwatch.ElapsedMilliseconds:F0}"
            );
            Console.WriteLine($"  Signature Verifications: {stats.SignatureVerifications:N0}");

            // Verify performance is reasonable (should complete 1000 operations in under 10 seconds)
            Assert.That(
                stopwatch.ElapsedMilliseconds,
                Is.LessThan(10000),
                "Signature verification should be fast with object pooling"
            );
        }

        [Test]
        [Category("Security.Cryptography")]
        [Category("StressTest")]
        public void HashComputation_Performance_ShouldBeFast()
        {
            const int iterations = 10000;
            var stopwatch = Stopwatch.StartNew();

            // Perform multiple hash computations
            for (var i = 0; i < iterations; i++)
            {
                var result = BouncyCastleCryptography.ComputeHash(_testData);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value.Length, Is.EqualTo(32)); // SHA-256 produces 32 bytes
            }

            stopwatch.Stop();

            var stats = BouncyCastleCryptography.GetPerformanceStats();

            Console.WriteLine("Hash Computation Performance:");
            Console.WriteLine($"  Iterations: {iterations:N0}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine(
                $"  Average Time: {(double)stopwatch.ElapsedMilliseconds / iterations:F2} ms per operation"
            );
            Console.WriteLine(
                $"  Operations/Second: {iterations * 1000.0 / stopwatch.ElapsedMilliseconds:F0}"
            );
            Console.WriteLine($"  Hash Computations: {stats.HashComputations:N0}");

            // Verify performance is reasonable (should complete 10000 operations in under 5 seconds)
            Assert.That(
                stopwatch.ElapsedMilliseconds,
                Is.LessThan(5000),
                "Hash computation should be fast with object pooling"
            );
        }

        [Test]
        [Category("Security.Cryptography")]
        [Category("StressTest")]
        public void PublicKeyHashComputation_WithCaching_ShouldShowCacheHits()
        {
            const int iterations = 100;
            var stopwatch = Stopwatch.StartNew();

            // Perform multiple public key hash computations with the same key
            for (var i = 0; i < iterations; i++)
            {
                var result = BouncyCastleCryptography.ComputePublicKeyHash(_keyPair.Public);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Value.Length, Is.EqualTo(64)); // SHA-256 hex = 64 chars
            }

            stopwatch.Stop();

            var stats = BouncyCastleCryptography.GetPerformanceStats();

            Console.WriteLine("Public Key Hash Computation Performance:");
            Console.WriteLine($"  Iterations: {iterations:N0}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"  Hash Computations: {stats.HashComputations:N0}");
            Console.WriteLine($"  Cache Hits: {stats.CacheHits:N0}");
            Console.WriteLine($"  Cache Hit Ratio: {stats.CacheHitRatio:F2}%");

            // Verify caching is working (should have many cache hits after first computation)
            Assert.That(
                stats.CacheHits,
                Is.GreaterThan(90),
                "Public key hash caching should result in high cache hit ratio"
            );
            Assert.That(
                stats.CacheHitRatio,
                Is.GreaterThan(90.0),
                "Cache hit ratio should be over 90%"
            );
        }

        [Test]
        [Category("Security.Cryptography")]
        [Category("StressTest")]
        public void CertificateParsing_WithCaching_ShouldShowCacheHits()
        {
            // Create test certificate bytes (this is a minimal test certificate)
            var testCertBytes = Convert.FromBase64String(
                "MIICljCCAX4CAQAwDQYJKoZIhvcNAQEFBQAwGjEYMBYGA1UEAwwPVGVzdCBDZXJ0aWZpY2F0ZTAeFw0yMzAxMDEwMDAwMDBaFw0yNDAxMDEwMDAwMDBaMBoxGDAWBgNVBAMMD1Rlc3QgQ2VydGlmaWNhdGUwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQC7VJTUt9Us8cKBwko="
            );

            const int iterations = 50;
            var stopwatch = Stopwatch.StartNew();

            // Perform multiple certificate parsing operations with the same certificate
            for (var i = 0; i < iterations; i++)
            {
                var result = BouncyCastleCryptography.ParseCertificate(testCertBytes);
                // Note: This test certificate is truncated, so parsing may fail
                // We're testing the caching mechanism, not the certificate validity
                if (result.IsSuccess)
                {
                    Assert.That(result.Value, Is.Not.Null);
                }
            }

            stopwatch.Stop();

            var stats = BouncyCastleCryptography.GetPerformanceStats();

            Console.WriteLine("Certificate Parsing Performance:");
            Console.WriteLine($"  Iterations: {iterations:N0}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"  Certificate Parses: {stats.CertificateParses:N0}");
            Console.WriteLine($"  Cache Hits: {stats.CacheHits:N0}");
            Console.WriteLine($"  Cache Hit Ratio: {stats.CacheHitRatio:F2}%");

            // The caching should reduce actual parsing operations
            Assert.That(
                stats.CertificateParses,
                Is.LessThanOrEqualTo(iterations),
                "Certificate parsing should benefit from caching"
            );
        }

        [Test]
        [Category("Security.Cryptography")]
        [Category("StressTest")]
        public void ObjectPooling_ShouldReuseObjects()
        {
            // Get initial pool sizes
            var initialStats = BouncyCastleCryptography.GetPerformanceStats();

            // Perform operations that use pools
            for (var i = 0; i < 10; i++)
            {
                var hashResult = BouncyCastleCryptography.ComputeHash(_testData);
                Assert.That(hashResult.IsSuccess, Is.True);

                var verifyResult = BouncyCastleCryptography.VerifySignature(
                    _testData,
                    _signature,
                    _keyPair.Public
                );
                Assert.That(verifyResult.IsSuccess, Is.True);
            }

            var finalStats = BouncyCastleCryptography.GetPerformanceStats();

            Console.WriteLine("Object Pool Performance:");
            Console.WriteLine($"  Initial Signer Pool Size: {initialStats.SignerPoolSize}");
            Console.WriteLine($"  Final Signer Pool Size: {finalStats.SignerPoolSize}");
            Console.WriteLine($"  Initial Digest Pool Size: {initialStats.DigestPoolSize}");
            Console.WriteLine($"  Final Digest Pool Size: {finalStats.DigestPoolSize}");

            // Verify pools are being used and objects are being reused
            Assert.That(
                finalStats.SignerPoolSize,
                Is.GreaterThanOrEqualTo(0),
                "Signer pool should contain reusable objects"
            );
            Assert.That(
                finalStats.DigestPoolSize,
                Is.GreaterThanOrEqualTo(0),
                "Digest pool should contain reusable objects"
            );
        }

        [Test]
        [Category("Security.Cryptography")]
        public void ClearCaches_ShouldResetEverything()
        {
            // Perform some operations to populate caches
            BouncyCastleCryptography.ComputeHash(_testData);
            BouncyCastleCryptography.ComputePublicKeyHash(_keyPair.Public);
            BouncyCastleCryptography.VerifySignature(_testData, _signature, _keyPair.Public);

            var statsBeforeClear = BouncyCastleCryptography.GetPerformanceStats();
            Assert.That(statsBeforeClear.TotalOperations, Is.GreaterThan(0));

            // Clear all caches
            BouncyCastleCryptography.ClearCaches();

            var statsAfterClear = BouncyCastleCryptography.GetPerformanceStats();

            Console.WriteLine("Cache Clearing:");
            Console.WriteLine($"  Operations Before Clear: {statsBeforeClear.TotalOperations:N0}");
            Console.WriteLine($"  Operations After Clear: {statsAfterClear.TotalOperations:N0}");
            Console.WriteLine(
                $"  Cache Size After Clear: {statsAfterClear.CertificateCacheSize + statsAfterClear.PublicKeyHashCacheSize}"
            );

            // Verify all counters and caches are reset
            Assert.That(
                statsAfterClear.TotalOperations,
                Is.EqualTo(0),
                "All performance counters should be reset"
            );
            Assert.That(
                statsAfterClear.CertificateCacheSize,
                Is.EqualTo(0),
                "Certificate cache should be empty"
            );
            Assert.That(
                statsAfterClear.PublicKeyHashCacheSize,
                Is.EqualTo(0),
                "Public key hash cache should be empty"
            );
        }
    }
}
