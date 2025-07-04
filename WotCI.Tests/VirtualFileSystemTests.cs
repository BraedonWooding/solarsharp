using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SolarSharp.Interpreter.Security;
using Xunit;
using WotCI;

namespace WotCI.Tests
{
    public class VirtualFileSystemTests : IDisposable
    {
        private readonly string _testDir;

        public VirtualFileSystemTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"wotci_vfs_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Fact]
        public void SimpleVirtualFileSystem_BasicConstruction_Works()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();

            // Act
            var vfs = new SimpleVirtualFileSystem(config);

            // Assert
            vfs.Should().NotBeNull();
        }

        [Fact]
        public void MountMemoryFileSystem_CreatesInMemoryStorage()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);

            // Act
            vfs.MountMemoryFileSystem("/memory");
            var testData = Encoding.UTF8.GetBytes("test content");
            vfs.WriteAllBytes("/memory/test.txt", testData);

            // Assert
            vfs.FileExists("/memory/test.txt").Should().BeTrue();
            var readData = vfs.ReadAllBytes("/memory/test.txt");
            Encoding.UTF8.GetString(readData).Should().Be("test content");
        }

        [Fact]
        public void MemoryFileSystem_IsolatedBetweenMounts()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);
            
            vfs.MountMemoryFileSystem("/mount1");
            vfs.MountMemoryFileSystem("/mount2");

            // Act
            vfs.WriteAllBytes("/mount1/file.txt", Encoding.UTF8.GetBytes("mount1 data"));
            vfs.WriteAllBytes("/mount2/file.txt", Encoding.UTF8.GetBytes("mount2 data"));

            // Assert
            var data1 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/mount1/file.txt"));
            var data2 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/mount2/file.txt"));
            
            data1.Should().Be("mount1 data");
            data2.Should().Be("mount2 data");
        }

        [Fact]
        public void FileExists_ReturnsFalseForNonExistentFile()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/test");

            // Act & Assert
            vfs.FileExists("/test/nonexistent.txt").Should().BeFalse();
        }

        [Fact]
        public void ReadAllBytes_ThrowsForNonExistentFile()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/test");

            // Act & Assert
            Action act = () => vfs.ReadAllBytes("/test/nonexistent.txt");
            act.Should().Throw<FileNotFoundException>();
        }

        [Fact]
        public void CertificateConstraint_EnforcesPathRestrictions()
        {
            // Arrange
            var rootCa = TestCertificateHelpers.GenerateRootCA();
            var partnerCert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test-partner");
            
            var config = SecurityConfiguration.CreateIsolated();
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

        [Fact]
        public void MountArchive_ReadsZipFiles()
        {
            // Arrange
            var zipPath = Path.Combine(_testDir, "test.zip");
            CreateTestZipFile(zipPath);
            
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);

            // Act
            vfs.MountArchive("/archive", zipPath);

            // Assert
            vfs.FileExists("/archive/file1.txt").Should().BeTrue();
            vfs.FileExists("/archive/file2.txt").Should().BeTrue();
            
            var content1 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/archive/file1.txt"));
            content1.Should().Be("Content of file 1");
        }

        [Fact]
        public void ArchiveFileSystem_IsReadOnly()
        {
            // Arrange
            var zipPath = Path.Combine(_testDir, "test.zip");
            CreateTestZipFile(zipPath);
            
            var config = SecurityConfiguration.CreateIsolated();
            config.Capabilities |= ScriptCapabilities.FileWrite;
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountArchive("/archive", zipPath);

            // Act & Assert
            Action writeAction = () => vfs.WriteAllBytes("/archive/newfile.txt", new byte[0]);
            writeAction.Should().Throw<NotSupportedException>()
                .WithMessage("Archive is read-only");
        }

        [Fact]
        public void MountPluginDirectory_MapsPhysicalPath()
        {
            // Arrange
            var pluginPath = Path.Combine(_testDir, "plugin");
            Directory.CreateDirectory(pluginPath);
            File.WriteAllText(Path.Combine(pluginPath, "script.lua"), "-- Lua script");
            
            var rootCa = TestCertificateHelpers.GenerateRootCA();
            var cert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test", "/plugins/test");
            
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);

            // Act
            vfs.MountPluginDirectory(cert, pluginPath);

            // Assert
            vfs.FileExists("/plugins/test/script.lua").Should().BeTrue();
            var content = Encoding.UTF8.GetString(vfs.ReadAllBytes("/plugins/test/script.lua"));
            content.Should().Be("-- Lua script");
        }

        [Fact]
        public void PathNormalization_HandlesVariousFormats()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/test");
            vfs.WriteAllBytes("/test/file.txt", Encoding.UTF8.GetBytes("test"));

            // Act & Assert - Various path formats should work
            vfs.FileExists("/test/file.txt").Should().BeTrue();
            vfs.FileExists("test/file.txt").Should().BeTrue();
            vfs.FileExists(@"\test\file.txt").Should().BeTrue();
            vfs.FileExists(@"test\file.txt").Should().BeTrue();
        }

        [Fact]
        public void NestedMountPoints_ResolvesToMostSpecific()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            var vfs = new SimpleVirtualFileSystem(config);
            
            vfs.MountMemoryFileSystem("/plugins");
            vfs.MountMemoryFileSystem("/plugins/special");
            
            vfs.WriteAllBytes("/plugins/file1.txt", Encoding.UTF8.GetBytes("general"));
            vfs.WriteAllBytes("/plugins/special/file2.txt", Encoding.UTF8.GetBytes("special"));

            // Act & Assert
            var general = Encoding.UTF8.GetString(vfs.ReadAllBytes("/plugins/file1.txt"));
            var special = Encoding.UTF8.GetString(vfs.ReadAllBytes("/plugins/special/file2.txt"));
            
            general.Should().Be("general");
            special.Should().Be("special");
        }

        [Fact]
        public async Task MemoryFileSystemProvider_BasicOperations()
        {
            // Arrange
            var provider = new MemoryFileSystemProvider();

            // Act - Create directory structure
            await provider.CreateDirectoryAsync("folder");
            await provider.WriteFileAsync("file.txt", Encoding.UTF8.GetBytes("root file"));
            await provider.WriteFileAsync("folder/nested.txt", Encoding.UTF8.GetBytes("nested file"));

            // Assert - Files exist
            (await provider.ExistsAsync("file.txt")).Should().BeTrue();
            (await provider.ExistsAsync("folder/nested.txt")).Should().BeTrue();
            
            // Assert - Content is correct
            var rootContent = await provider.ReadFileAsync("file.txt");
            Encoding.UTF8.GetString(rootContent).Should().Be("root file");
            
            // Assert - Directory detection
            (await provider.IsDirectoryAsync("folder")).Should().BeTrue();
            (await provider.IsDirectoryAsync("file.txt")).Should().BeFalse();
        }

        [Fact]
        public async Task MemoryFileSystemProvider_DeleteOperations()
        {
            // Arrange
            var provider = new MemoryFileSystemProvider();
            await provider.WriteFileAsync("deleteme.txt", Encoding.UTF8.GetBytes("delete this"));

            // Act
            (await provider.ExistsAsync("deleteme.txt")).Should().BeTrue();
            await provider.DeleteFileAsync("deleteme.txt");

            // Assert
            (await provider.ExistsAsync("deleteme.txt")).Should().BeFalse();
        }

        [Fact]
        public async Task PhysicalPathProvider_MapsToRealFileSystem()
        {
            // Arrange
            var basePath = Path.Combine(_testDir, "physical");
            Directory.CreateDirectory(basePath);
            File.WriteAllText(Path.Combine(basePath, "real.txt"), "real file content");
            
            var provider = new PhysicalPathProvider(basePath);

            // Act
            var exists = await provider.ExistsAsync("real.txt");
            var content = await provider.ReadFileAsync("real.txt");

            // Assert
            exists.Should().BeTrue();
            Encoding.UTF8.GetString(content).Should().Be("real file content");
        }

        [Fact]
        public void OpenFile_CreatesStreams()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated();
            config.Capabilities |= ScriptCapabilities.FileWrite;
            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountMemoryFileSystem("/streams");

            // Act - Write using stream
            using (var stream = vfs.OpenFile("/streams/test.txt", FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("Stream content");
            }

            // Assert - Read using stream
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