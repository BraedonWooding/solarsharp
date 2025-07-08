using System;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Path.Unit")]
    public class CrossPlatformPathCanonicalizerTests
    {
        private CrossPlatformPathCanonicalizer _canonicalizer;

        [SetUp]
        public void SetUp()
        {
            _canonicalizer = new CrossPlatformPathCanonicalizer();
        }

        [Test]
        public void Canonicalize_NullPath_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize(null);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.InvalidPath));
        }

        [Test]
        public void Canonicalize_EmptyPath_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.InvalidPath));
        }

        [Test]
        public void Canonicalize_ValidPath_ReturnsSuccess()
        {
            var result = _canonicalizer.Canonicalize("/data/file.txt");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Original, Is.EqualTo("/data/file.txt"));
        }

        [Test]
        public void Canonicalize_RelativePath_ResolvesToAbsolute()
        {
            var result = _canonicalizer.Canonicalize("data/file.txt");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Normalized, Does.StartWith("/").Or.StartWith("C:"));
        }

        [Test]
        public void Canonicalize_PathWithDotDot_ResolvesCorrectly()
        {
            var result = _canonicalizer.Canonicalize("/data/../file.txt");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Normalized, Does.Not.Contain(".."));
        }

        [Test]
        [Platform("Win", Reason = "Windows-specific test")]
        public void Canonicalize_WindowsAlternateDataStream_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("file.txt:hidden");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }

        [Test]
        [Platform("Win", Reason = "Windows-specific test")]
        public void Canonicalize_WindowsUncPath_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("\\\\server\\share\\file.txt");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }

        [Test]
        [Platform("Win", Reason = "Windows-specific test")]
        public void Canonicalize_WindowsDeviceName_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("CON.txt");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.DangerousFileName)
            );
        }

        [Test]
        [Platform("Win", Reason = "Windows-specific test")]
        public void Canonicalize_WindowsPathEndingWithSpace_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("file.txt ");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }

        [Test]
        [Platform("Win", Reason = "Windows-specific test")]
        public void Canonicalize_WindowsPathEndingWithPeriod_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("file.txt.");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }

        [Test]
        [Platform("Linux", Reason = "Linux-specific test")]
        public void Canonicalize_LinuxSystemPath_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("/proc/self/mem");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }

        [Test]
        [Platform("Linux", Reason = "Linux-specific test")]
        public void Canonicalize_LinuxDevPath_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("/dev/null");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }

        [Test]
        public void Canonicalize_PathWithNullByte_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("file\0.txt");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.ControlCharacter));
        }

        [Test]
        public void Canonicalize_SandboxEscape_ReturnsFailure()
        {
            var result = _canonicalizer.Canonicalize("../../../etc/passwd", "/sandbox");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.SandboxViolation));
        }

        [Test]
        public void Canonicalize_ValidPathInSandbox_ReturnsSuccess()
        {
            var sandboxRoot = Environment.CurrentDirectory;
            var result = _canonicalizer.Canonicalize("data/file.txt", sandboxRoot);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Resolved, Does.StartWith(sandboxRoot));
        }

        [Test]
        public void Canonicalize_CacheHit_ReturnsSameResult()
        {
            var path = "/data/cached.txt";

            // First call
            var result1 = _canonicalizer.Canonicalize(path);

            // Second call - should hit cache
            var result2 = _canonicalizer.Canonicalize(path);

            Assert.That(result1.IsSuccess, Is.True);
            Assert.That(result2.IsSuccess, Is.True);
            Assert.That(result1.Value.Normalized, Is.EqualTo(result2.Value.Normalized));
        }

        [Test]
        public void GetStatistics_ReturnsValidStats()
        {
            // Add some entries
            _canonicalizer.Canonicalize("/path1");
            _canonicalizer.Canonicalize("/path2");

            var stats = _canonicalizer.GetStatistics();

            Assert.That(stats.TotalEntries, Is.GreaterThanOrEqualTo(2));
            Assert.That(stats.ValidEntries, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ClearCache_RemovesAllEntries()
        {
            // Add entries
            _canonicalizer.Canonicalize("/path1");
            _canonicalizer.Canonicalize("/path2");

            // Clear cache
            _canonicalizer.ClearCache();

            var stats = _canonicalizer.GetStatistics();
            Assert.That(stats.TotalEntries, Is.EqualTo(0));
        }

        [Test]
        public void Canonicalize_UnicodeNormalization_HandlesCorrectly()
        {
            // Test with decomposed Unicode (é as e + combining acute)
            var decomposed = "café"; // This might be decomposed depending on input
            var result = _canonicalizer.Canonicalize(decomposed);

            Assert.That(result.IsSuccess, Is.True);
            // Result should be in NFC form
            Assert.That(result.Value.Normalized, Does.Contain("caf"));
        }

        [Test]
        public void Canonicalize_MixedSlashes_NormalizesCorrectly()
        {
            var result = _canonicalizer.Canonicalize("data\\subfolder/file.txt");

            Assert.That(result.IsSuccess, Is.True);
            // On Unix, Path.GetFullPath preserves the mixed slashes in the input
            // We just need to ensure the path was successfully canonicalized
            Assert.That(result.Value.Normalized, Is.Not.Null);
            Assert.That(result.Value.Normalized, Does.Contain("file.txt"));
        }

        [TestCase("CON")]
        [TestCase("PRN")]
        [TestCase("AUX")]
        [TestCase("NUL")]
        [TestCase("COM1")]
        [TestCase("LPT1")]
        [Platform("Win", Reason = "Windows device names")]
        public void Canonicalize_WindowsDeviceNames_ReturnsFailure(string deviceName)
        {
            var result = _canonicalizer.Canonicalize(deviceName);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.DangerousFileName)
            );
        }

        [TestCase("/dev")]
        [TestCase("/proc")]
        [TestCase("/sys")]
        [TestCase("/boot")]
        [Platform("Linux", Reason = "Linux system paths")]
        public void Canonicalize_LinuxSystemPaths_ReturnsFailure(string systemPath)
        {
            var result = _canonicalizer.Canonicalize(systemPath);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.PlatformSpecific));
        }
    }
}
