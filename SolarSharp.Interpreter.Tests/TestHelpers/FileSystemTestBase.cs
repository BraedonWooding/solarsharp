using System;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.TestHelpers
{
    /// <summary>
    ///     Base class for tests that require file system access.
    ///     Provides isolated file system abstraction for each test.
    /// </summary>
    public abstract class FileSystemTestBase
    {
        /// <summary>
        ///     Gets the file system abstraction for testing.
        /// </summary>
        private IFileSystem FileSystem { get; set; }

        /// <summary>
        ///     Gets the temporary directory path unique to this test instance.
        /// </summary>
        private string TempDir { get; set; }

        /// <summary>
        ///     Sets up a mock file system for the test.
        /// </summary>
        [SetUp]
        public virtual void BaseSetUp()
        {
            // Create a mock file system for testing
            FileSystem = new MockFileSystem();

            // Create unique directory for this test
            TempDir = FileSystem.Path.Combine(
                FileSystem.Path.GetTempPath(),
                $"solarsharp_test_{GetType().Name}_{TestContext.CurrentContext.Test.Name}_{Guid.NewGuid():N}"
            );
            FileSystem.Directory.CreateDirectory(TempDir);
        }

        /// <summary>
        ///     Cleans up the file system after the test.
        /// </summary>
        [TearDown]
        public virtual void BaseTearDown()
        {
            if (FileSystem.Directory.Exists(TempDir))
                try
                {
                    FileSystem.Directory.Delete(TempDir, true);
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
            var path = FileSystem.Path.Combine(TempDir, name);
            FileSystem.Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        ///     Creates a file with the given content in the test's temporary directory.
        /// </summary>
        protected string CreateFile(string filename, string content)
        {
            var path = FileSystem.Path.Combine(TempDir, filename);
            FileSystem.File.WriteAllText(path, content);
            return path;
        }
    }
}
