using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace WotCI.Tests
{
    [TestFixture]
    [Category("IntegrationTest")]
    public class VirtualFileSystemTests
    {
        private string _testDir;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"wotci_vfs_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Test]
        public void SimpleVirtualFileSystem_BasicConstruction_Works()
        {
                        var config = SecurityConfiguration.Isolated();

                        var vfs = new SimpleVirtualFileSystem(config);

                        vfs.Should().NotBeNull();
        }

        [Test]
        public void MountMemoryFileSystem_CreatesInMemoryStorage()
        {
                        var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);

                        vfs.MountMemoryFileSystem("/memory");
            var testData = Encoding.UTF8.GetBytes("test content");
            vfs.WriteAllBytes("/memory/test.txt", testData);

                        vfs.FileExists("/memory/test.txt").Should().BeTrue();
            var readData = vfs.ReadAllBytes("/memory/test.txt");
            Encoding.UTF8.GetString(readData).Should().Be("test content");
        }

        [Test]
        public void MemoryFileSystem_IsolatedBetweenMounts()
        {
                        var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);
            
            vfs.MountMemoryFileSystem("/mount1");
            vfs.MountMemoryFileSystem("/mount2");

                        vfs.WriteAllBytes("/mount1/file.txt", Encoding.UTF8.GetBytes("mount1 data"));
            vfs.WriteAllBytes("/mount2/file.txt", Encoding.UTF8.GetBytes("mount2 data"));

                        var data1 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/mount1/file.txt"));
            var data2 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/mount2/file.txt"));
            
            data1.Should().Be("mount1 data");
            data2.Should().Be("mount2 data");
        }

        [Test]
        public void FileExists_ReturnsFalseForNonExistentFile()
        {
                        var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/test");

                        vfs.FileExists("/test/nonexistent.txt").Should().BeFalse();
        }

        [Test]
        public void ReadAllBytes_ThrowsForNonExistentFile()
        {
                        var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/test");

                        Action act = () => vfs.ReadAllBytes("/test/nonexistent.txt");
            act.Should().Throw<FileNotFoundException>();
        }

        [Test]
        public void CertificateConstraint_EnforcesPathRestrictions()
        {
                        var rootCa = TestCertificateHelpers.GenerateRootCA();
            var partnerCert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test-partner");
            
            var config = SecurityConfiguration.Isolated();
            config.Capabilities |= ScriptCapabilities.FileWrite;
            
            var vfs = new SimpleVirtualFileSystem(config, partnerCert);
            vfs.MountMemoryFileSystem("/plugins/test-partner");
            vfs.MountMemoryFileSystem("/plugins/other-partner");

            // Act & Assert - Can write to allowed path
            Action allowedWrite = () => vfs.WriteAllBytes("/plugins/test-partner/file.txt", new byte[0]);
            allowedWrite.Should().NotThrow();

            // Cannot write to restricted path
            Action restrictedWrite = () => vfs.WriteAllBytes("/plugins/other-partner/file.txt", new byte[0]);
            restrictedWrite.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*Certificate constraint violation*");
        }

        [Test]
        public void MountArchive_ReadsZipFiles()
        {
                        var zipPath = Path.Combine(_testDir, "test.zip");
            CreateTestZipFile(zipPath);
            
            var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);

                        vfs.MountArchive("/archive", zipPath);

                        vfs.FileExists("/archive/file1.txt").Should().BeTrue();
            vfs.FileExists("/archive/file2.txt").Should().BeTrue();
            
            var content1 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/archive/file1.txt"));
            content1.Should().Be("Content of file 1");
        }

        [Test]
        public void ArchiveFileSystem_IsReadOnly()
        {
                        var zipPath = Path.Combine(_testDir, "test.zip");
            CreateTestZipFile(zipPath);
            
            var config = SecurityConfiguration.Isolated();
            config.Capabilities |= ScriptCapabilities.FileWrite;
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountArchive("/archive", zipPath);

                        Action writeAction = () => vfs.WriteAllBytes("/archive/newfile.txt", new byte[0]);
            writeAction.Should().Throw<NotSupportedException>()
                .WithMessage("Archive is read-only");
        }

        [Test]
        public void MountPluginDirectory_MapsPhysicalPath()
        {
                        var pluginPath = Path.Combine(_testDir, "plugin");
            Directory.CreateDirectory(pluginPath);
            File.WriteAllText(Path.Combine(pluginPath, "script.lua"), "-- Lua script");
            
            var rootCa = TestCertificateHelpers.GenerateRootCA();
            var cert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test", "/plugins/test");
            
            var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);

                        vfs.MountPluginDirectory(cert, pluginPath);

                        vfs.FileExists("/plugins/test/script.lua").Should().BeTrue();
            var content = Encoding.UTF8.GetString(vfs.ReadAllBytes("/plugins/test/script.lua"));
            content.Should().Be("-- Lua script");
        }

        [Test]
        public void PathNormalization_HandlesVariousFormats()
        {
                        var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/test");
            vfs.WriteAllBytes("/test/file.txt", Encoding.UTF8.GetBytes("test"));

            // Act & Assert - Various path formats should work
            vfs.FileExists("/test/file.txt").Should().BeTrue();
            vfs.FileExists("test/file.txt").Should().BeTrue();
            vfs.FileExists(@"\test\file.txt").Should().BeTrue();
            vfs.FileExists(@"test\file.txt").Should().BeTrue();
        }

        [Test]
        public void NestedMountPoints_ResolvesToMostSpecific()
        {
                        var config = SecurityConfiguration.Isolated();
            var vfs = new SimpleVirtualFileSystem(config);
            
            vfs.MountMemoryFileSystem("/plugins");
            vfs.MountMemoryFileSystem("/plugins/special");
            
            vfs.WriteAllBytes("/plugins/file1.txt", Encoding.UTF8.GetBytes("general"));
            vfs.WriteAllBytes("/plugins/special/file2.txt", Encoding.UTF8.GetBytes("special"));

                        var general = Encoding.UTF8.GetString(vfs.ReadAllBytes("/plugins/file1.txt"));
            var special = Encoding.UTF8.GetString(vfs.ReadAllBytes("/plugins/special/file2.txt"));
            
            general.Should().Be("general");
            special.Should().Be("special");
        }

        [Test]
        public async Task MemoryFileSystemProvider_BasicOperations()
        {
                        var provider = new MemoryFileSystemProvider();

            // Create directory structure
            await provider.CreateDirectoryAsync("folder");
            await provider.WriteFileAsync("file.txt", Encoding.UTF8.GetBytes("root file"));
            await provider.WriteFileAsync("folder/nested.txt", Encoding.UTF8.GetBytes("nested file"));

            // Files exist
            (await provider.ExistsAsync("file.txt")).Should().BeTrue();
            (await provider.ExistsAsync("folder/nested.txt")).Should().BeTrue();
            
            // Content is correct
            var rootContent = await provider.ReadFileAsync("file.txt");
            Encoding.UTF8.GetString(rootContent).Should().Be("root file");
            
            // Directory detection
            (await provider.IsDirectoryAsync("folder")).Should().BeTrue();
            (await provider.IsDirectoryAsync("file.txt")).Should().BeFalse();
        }

        [Test]
        public async Task MemoryFileSystemProvider_DeleteOperations()
        {
                        var provider = new MemoryFileSystemProvider();
            await provider.WriteFileAsync("deleteme.txt", Encoding.UTF8.GetBytes("delete this"));

                        (await provider.ExistsAsync("deleteme.txt")).Should().BeTrue();
            await provider.DeleteFileAsync("deleteme.txt");

                        (await provider.ExistsAsync("deleteme.txt")).Should().BeFalse();
        }

        [Test]
        public async Task PhysicalPathProvider_MapsToRealFileSystem()
        {
                        var basePath = Path.Combine(_testDir, "physical");
            Directory.CreateDirectory(basePath);
            File.WriteAllText(Path.Combine(basePath, "real.txt"), "real file content");
            
            var provider = new PhysicalPathProvider(basePath);

                        var exists = await provider.ExistsAsync("real.txt");
            var content = await provider.ReadFileAsync("real.txt");

                        exists.Should().BeTrue();
            Encoding.UTF8.GetString(content).Should().Be("real file content");
        }

        [Test]
        public void OpenFile_CreatesStreams()
        {
                        var config = SecurityConfiguration.Isolated();
            config.Capabilities |= ScriptCapabilities.FileWrite;
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/streams");

            // Write using stream
            using (var stream = vfs.OpenFile("/streams/test.txt", FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("Stream content");
            }

            // Read using stream
            using (var stream = vfs.OpenFile("/streams/test.txt", FileMode.Open, System.IO.FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                var content = reader.ReadToEnd();
                content.Should().Contain("Stream content");
            }
        }

        private void CreateTestZipFile(string path)
        {
            using (var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry1 = archive.CreateEntry("file1.txt");
                using (var stream = entry1.Open())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write("Content of file 1");
                }

                var entry2 = archive.CreateEntry("file2.txt");
                using (var stream = entry2.Open())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write("Content of file 2");
                }
            }
        }
    }
}