using System.Linq;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Path.Unit")]
    public class PathNormalizerTests
    {
        [Test]
        public void NormalizePath_NullPath_ReturnsNull()
        {
            var result = PathNormalizer.NormalizePath(null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void NormalizePath_EmptyPath_ReturnsEmpty()
        {
            var result = PathNormalizer.NormalizePath("");

            Assert.That(result, Is.EqualTo(""));
        }

        [Test]
        public void NormalizePath_RelativePath_PreservesAsRelative()
        {
            var result = PathNormalizer.NormalizePath("data/config.json");

            Assert.That(result, Is.EqualTo("data/config.json"));
        }

        [Test]
        public void NormalizePath_AbsolutePath_PreservesLeadingSlash()
        {
            var result = PathNormalizer.NormalizePath("/data/config.json");

            Assert.That(result, Is.EqualTo("/data/config.json"));
        }

        [Test]
        public void NormalizePath_BackslashesToForwardSlashes()
        {
            var result = PathNormalizer.NormalizePath("data\\config\\file.json");

            Assert.That(result, Is.EqualTo("data/config/file.json"));
        }

        [Test]
        public void NormalizePath_MixedSlashes_ConvertsToForward()
        {
            var result = PathNormalizer.NormalizePath("data/config\\file.json");

            Assert.That(result, Is.EqualTo("data/config/file.json"));
        }

        [Test]
        public void NormalizePath_RemovesDuplicateSlashes()
        {
            var result = PathNormalizer.NormalizePath("//data///config//file.json");

            Assert.That(result, Is.EqualTo("/data/config/file.json"));
        }

        [Test]
        public void NormalizePath_RemovesTrailingSlash()
        {
            var result = PathNormalizer.NormalizePath("/data/config/");

            Assert.That(result, Is.EqualTo("/data/config"));
        }

        [Test]
        public void NormalizePath_PreservesRootSlash()
        {
            var result = PathNormalizer.NormalizePath("/");

            Assert.That(result, Is.EqualTo("/"));
        }

        [Test]
        public void NormalizePath_HandlesComplexPath()
        {
            var result = PathNormalizer.NormalizePath("\\\\data\\\\config//file.json/");

            Assert.That(result, Is.EqualTo("/data/config/file.json"));
        }

        [Test]
        public void ToAbsolutePath_NullPath_ReturnsNull()
        {
            var result = PathNormalizer.ToAbsolutePath(null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ToAbsolutePath_EmptyPath_ReturnsEmpty()
        {
            var result = PathNormalizer.ToAbsolutePath("");

            Assert.That(result, Is.EqualTo(""));
        }

        [Test]
        public void ToAbsolutePath_RelativePath_MakesAbsolute()
        {
            var result = PathNormalizer.ToAbsolutePath("data/config.json");

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.EndWith("/data/config.json"));
        }

        [Test]
        public void ToAbsolutePath_AbsolutePath_NormalizesOnly()
        {
            var result = PathNormalizer.ToAbsolutePath("/data/config.json");

            Assert.That(result, Is.EqualTo("/data/config.json"));
        }

        [Test]
        public void ToAbsolutePath_WindowsAbsolutePath_NormalizesCorrectly()
        {
            var result = PathNormalizer.ToAbsolutePath("C:\\data\\config.json");

            Assert.That(result, Does.StartWith("/C/data/config.json"));
        }

        [TestCase("simple.txt", "simple.txt")]
        [TestCase("data/file.json", "data/file.json")]
        [TestCase("/absolute/path", "/absolute/path")]
        [TestCase("data\\windows\\path", "data/windows/path")]
        [TestCase("//double//slash", "/double/slash")]
        [TestCase("trailing/slash/", "trailing/slash")]
        public void NormalizePath_VariousPaths_NormalizesCorrectly(string input, string expected)
        {
            var result = PathNormalizer.NormalizePath(input);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void NormalizePath_PerformanceTest_HandlesLargePath()
        {
            var largePath = string.Join("/", Enumerable.Repeat("segment", 1000));

            var result = PathNormalizer.NormalizePath(largePath);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("segment"));
        }

        [Test]
        public void ToAbsolutePath_HandlesInvalidCharacters()
        {
            // Test with characters that might cause issues in path resolution
            var result = PathNormalizer.ToAbsolutePath("data/file\ttab.txt");

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("file\ttab.txt"));
        }
    }
}
