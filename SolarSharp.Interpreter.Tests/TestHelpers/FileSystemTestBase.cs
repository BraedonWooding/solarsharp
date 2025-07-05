using System;
using System.IO;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.TestHelpers
{
    /// <summary>
    ///     Base class for tests that require file system access.
    ///     Provides isolated temporary directories for each test.
    /// </summary>
    public abstract class FileSystemTestBase
    {
        /// <summary>
        ///     Gets the temporary directory path unique to this test instance.
        /// </summary>
        protected string TempDir { get; private set; }

        /// <summary>
        ///     Sets up a unique temporary directory for the test.
        /// </summary>
        [SetUp]
        public virtual void BaseSetUp()
        {
            // Create unique directory for this test
            TempDir = Path.Combine(Path.GetTempPath(),
                $"solarsharp_test_{GetType().Name}_{TestContext.CurrentContext.Test.Name}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempDir);
        }

        /// <summary>
        ///     Cleans up the temporary directory after the test.
        /// </summary>
        [TearDown]
        public virtual void BaseTearDown()
        {
            if (Directory.Exists(TempDir))
                try
                {
                    Directory.Delete(TempDir, true);
                }
                catch
                {
                    // Best effort cleanup
                }
        }

        /// <summary>
        ///     Creates a subdirectory within the test's temporary directory.
        /// </summary>
        protected string CreateSubDirectory(string name)
        {
            var path = Path.Combine(TempDir, name);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        ///     Creates a file with the given content in the test's temporary directory.
        /// </summary>
        protected string CreateFile(string filename, string content)
        {
            var path = Path.Combine(TempDir, filename);
            File.WriteAllText(path, content);
            return path;
        }
    }
}