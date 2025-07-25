using System;
using System.Collections.Immutable;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Security.Unit")]
    public class DirectoryAccessRuleCacheTests
    {
        private DirectoryAccessRuleCache _cache;
        private ImmutableArray<DirectoryAccessRule> _testRules;

        [SetUp]
        public void SetUp()
        {
            _testRules = new[]
            {
                DirectoryAccessRule.Create("/public", FilePermissions.Read),
                DirectoryAccessRule.Create("/secure", FilePermissions.ReadWrite, "trusted-key"),
                DirectoryAccessRule.Create("/temp/**", FilePermissions.SandboxedReadWrite),
                DirectoryAccessRule.Create("/logs/*/app", FilePermissions.Read),
            }.ToImmutableArray();

            _cache = new DirectoryAccessRuleCache(_testRules, TimeSpan.FromMinutes(1));
        }

        [Test]
        public void GetEffectivePermissions_NoMatchingRules_ReturnsFallback()
        {
            var result = _cache.GetEffectivePermissions("/unmatched/path", null);

            Assert.That(result, Is.EqualTo(FilePermissions.SandboxedReadWrite));
        }

        [Test]
        public void GetEffectivePermissions_MatchingRuleNoKey_ReturnsPermissions()
        {
            var result = _cache.GetEffectivePermissions("/public/file.txt", null);

            Assert.That(result, Is.EqualTo(FilePermissions.Read));
        }

        [Test]
        public void GetEffectivePermissions_RequiredKeyProvided_ReturnsPermissions()
        {
            var result = _cache.GetEffectivePermissions("/secure/file.txt", "trusted-key");

            Assert.That(result, Is.EqualTo(FilePermissions.ReadWrite));
        }

        [Test]
        public void GetEffectivePermissions_RequiredKeyMissing_ReturnsNone()
        {
            var result = _cache.GetEffectivePermissions("/secure/file.txt", null);

            Assert.That(result, Is.EqualTo(FilePermissions.None));
        }

        [Test]
        public void GetEffectivePermissions_RequiredKeyWrong_ReturnsNone()
        {
            var result = _cache.GetEffectivePermissions("/secure/file.txt", "wrong-key");

            Assert.That(result, Is.EqualTo(FilePermissions.None));
        }

        [Test]
        public void GetEffectivePermissions_WildcardPattern_Matches()
        {
            var result = _cache.GetEffectivePermissions("/temp/sub/dir/file.txt", null);

            Assert.That(result, Is.EqualTo(FilePermissions.SandboxedReadWrite));
        }

        [Test]
        public void GetEffectivePermissions_NestedWildcardPattern_Matches()
        {
            var result = _cache.GetEffectivePermissions("/logs/2024/app/debug.log", null);

            Assert.That(result, Is.EqualTo(FilePermissions.Read));
        }

        [Test]
        public void GetEffectivePermissions_CacheHit_ReturnsCachedResult()
        {
            var path = "/public/cached.txt";

            // First call - should cache the result
            var result1 = _cache.GetEffectivePermissions(path, null);

            // Second call - should return cached result
            var result2 = _cache.GetEffectivePermissions(path, null);

            Assert.That(result1, Is.EqualTo(FilePermissions.Read));
            Assert.That(result2, Is.EqualTo(FilePermissions.Read));
        }

        [Test]
        public void GetEffectivePermissions_DifferentKeys_CachesSeparately()
        {
            var path = "/secure/test.txt";

            var result1 = _cache.GetEffectivePermissions(path, "trusted-key");
            var result2 = _cache.GetEffectivePermissions(path, "wrong-key");
            var result3 = _cache.GetEffectivePermissions(path, null);

            Assert.That(result1, Is.EqualTo(FilePermissions.ReadWrite));
            Assert.That(result2, Is.EqualTo(FilePermissions.None));
            Assert.That(result3, Is.EqualTo(FilePermissions.None));
        }

        [Test]
        public void ClearExpiredEntries_RemovesExpiredEntries()
        {
            // Create cache with very short expiry
            var shortCache = new DirectoryAccessRuleCache(_testRules, TimeSpan.FromMilliseconds(1));

            // Add entry
            shortCache.GetEffectivePermissions("/public/test.txt", null);

            // Wait for expiry
            System.Threading.Thread.Sleep(10);

            // Clear expired entries
            shortCache.ClearExpiredEntries();

            // Verify cache is working (no exceptions)
            var result = shortCache.GetEffectivePermissions("/public/test2.txt", null);
            Assert.That(result, Is.EqualTo(FilePermissions.Read));
        }

        [Test]
        public void GetStatistics_ReturnsCorrectCounts()
        {
            // Add some entries to cache
            _cache.GetEffectivePermissions("/public/file1.txt", null);
            _cache.GetEffectivePermissions("/secure/file2.txt", "trusted-key");

            var stats = _cache.GetStatistics();

            Assert.That(stats.RuleCount, Is.EqualTo(4));
            Assert.That(stats.PathCacheSize, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Clear_RemovesAllCachedEntries()
        {
            // Add entries
            _cache.GetEffectivePermissions("/public/file1.txt", null);
            _cache.GetEffectivePermissions("/secure/file2.txt", "trusted-key");

            // Clear cache
            _cache.Clear();

            // Verify cache is empty
            var stats = _cache.GetStatistics();
            Assert.That(stats.PathCacheSize, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_EmptyRules_HandlesCorrectly()
        {
            var emptyCache = new DirectoryAccessRuleCache(
                ImmutableArray<DirectoryAccessRule>.Empty
            );

            var result = emptyCache.GetEffectivePermissions("/any/path", null);

            Assert.That(result, Is.EqualTo(FilePermissions.SandboxedReadWrite));
        }

        [Test]
        public void GetEffectivePermissions_MultipleMatchingRules_ReturnsLastMatch()
        {
            var overlappingRules = new[]
            {
                DirectoryAccessRule.Create("/test", FilePermissions.Read),
                DirectoryAccessRule.Create("/test", FilePermissions.ReadWrite),
            }.ToImmutableArray();

            var overlappingCache = new DirectoryAccessRuleCache(overlappingRules);

            var result = overlappingCache.GetEffectivePermissions("/test/file.txt", null);

            // Should return the last matching rule's permissions
            Assert.That(result, Is.EqualTo(FilePermissions.ReadWrite));
        }
    }
}
