using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

// Type alias for backward compatibility

namespace WotCI.Tests;

[TestFixture]
[Category("WotCI.Integration")]
public class VirtualFileSystemTests
{
    private IFileSystem _fileSystem;
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new MockFileSystem();
        _testDir = _fileSystem.Path.Combine(
            _fileSystem.Path.GetTempPath(),
            $"wotci_vfs_test_{Guid.NewGuid()}"
        );
        _fileSystem.Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (_fileSystem.Directory.Exists(_testDir))
            _fileSystem.Directory.Delete(_testDir, true);
    }

    private SecurityPolicy CreateTestSecurityPolicy()
    {
        return new SecurityPolicy
        {
            Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
        };
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void SimpleVirtualFileSystem_BasicConstruction_Works()
    {
        var config = CreateTestSecurityPolicy();

        var vfs = new SimpleVirtualFileSystem(config);

        vfs.Should().NotBeNull();
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void MountMemoryFileSystem_CreatesInMemoryStorage()
    {
        var config = CreateTestSecurityPolicy();
        var vfs = new SimpleVirtualFileSystem(config);

        vfs.MountMemoryFileSystem("/memory");
        var testData = Encoding.UTF8.GetBytes("test content");
        vfs.WriteAllBytes("/memory/test.txt", testData);

        vfs.FileExists("/memory/test.txt").Should().BeTrue();
        var readData = vfs.ReadAllBytes("/memory/test.txt");
        Encoding.UTF8.GetString(readData).Should().Be("test content");
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void MemoryFileSystem_IsolatedBetweenMounts()
    {
        var config = CreateTestSecurityPolicy();
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

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void FileExists_ReturnsFalseForNonExistentFile()
    {
        var config = CreateTestSecurityPolicy();
        var vfs = new SimpleVirtualFileSystem(config);
        vfs.MountMemoryFileSystem("/test");

        vfs.FileExists("/test/nonexistent.txt").Should().BeFalse();
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void ReadAllBytes_ThrowsForNonExistentFile()
    {
        var config = CreateTestSecurityPolicy();
        var vfs = new SimpleVirtualFileSystem(config);
        vfs.MountMemoryFileSystem("/test");

        Action act = () => vfs.ReadAllBytes("/test/nonexistent.txt");
        act.Should().Throw<FileNotFoundException>();
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void CertificateConstraint_EnforcesPathRestrictions()
    {
        var (rootCaCert, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
        var (partnerCert, partnerKey) = TestCertificateHelpers.GeneratePartnerCertificate(
            rootCaCert,
            rootCaKey,
            "Test Partner",
            "/plugins/test-partner"
        );

        // Extract signing key fingerprint (simplified for test)
        var signingKeyFingerprint = "test-key-fingerprint-123";

        // Create security policy with directory access rules
        var config = new SecurityPolicy
        {
            Capabilities = ScriptCapabilities.FileWrite,
            DefaultFileAccess = FilePermissions.None, // Deny by default
            DefaultDirectoryAccess = DirectoryPermissions.None,
            DirectoryAccessRules =
            [
                .. new[]
                {
                    // Only allow the specific signing key to access /plugins/test-partner
                    DirectoryAccessRule.Create(
                        "/plugins/test-partner",
                        FilePermissions.ReadWrite,
                        signingKeyFingerprint
                    ),
                    // Deny access to other partner paths for unsigned scripts
                    DirectoryAccessRule.Create("/plugins/other-partner", FilePermissions.None),
                },
            ],
        };

        var vfs = new SimpleVirtualFileSystem(config);
        vfs.MountMemoryFileSystem("/plugins/test-partner");
        vfs.MountMemoryFileSystem("/plugins/other-partner");

        // Test with unsigned script (no execution context) - should fail for restricted paths
        var restrictedWrite = () => vfs.WriteAllBytes("/plugins/other-partner/file.txt", []);
        restrictedWrite
            .Should()
            .Throw<UnauthorizedAccessException>()
            .WithMessage("*Certificate constraint violation*");

        // Test with unsigned script accessing unrestricted path should also fail if explicitly denied
        var unrestrictedWrite = () => vfs.WriteAllBytes("/plugins/test-partner/file.txt", []);
        unrestrictedWrite
            .Should()
            .Throw<UnauthorizedAccessException>()
            .WithMessage("*Certificate constraint violation*unsigned script*");
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void MountArchive_ReadsZipFiles()
    {
        var realTempDir = Path.Combine(Path.GetTempPath(), $"wotci_archive_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(realTempDir);

        try
        {
            var zipPath = Path.Combine(realTempDir, "test.zip");
            CreateTestZipFile(zipPath);

            var config = CreateTestSecurityPolicy();
            var vfs = new SimpleVirtualFileSystem(config);

            vfs.MountArchive("/archive", zipPath);

            vfs.FileExists("/archive/file1.txt").Should().BeTrue();
            vfs.FileExists("/archive/file2.txt").Should().BeTrue();

            var content1 = Encoding.UTF8.GetString(vfs.ReadAllBytes("/archive/file1.txt"));
            content1.Should().Be("Content of file 1");
        }
        finally
        {
            if (Directory.Exists(realTempDir))
                Directory.Delete(realTempDir, true);
        }
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void ArchiveFileSystem_IsReadOnly()
    {
        var realTempDir = Path.Combine(Path.GetTempPath(), $"wotci_archive_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(realTempDir);

        try
        {
            var zipPath = Path.Combine(realTempDir, "test.zip");
            CreateTestZipFile(zipPath);

            // Define the required capability for file operations
            const ScriptCapabilities fileWriteCapability = ScriptCapabilities.FileWrite;

            // Create security policy with file write permission
            var config = new SecurityPolicy { Capabilities = fileWriteCapability };

            var vfs = new SimpleVirtualFileSystem(config);
            vfs.MountArchive("/archive", zipPath);

            var writeAction = () => vfs.WriteAllBytes("/archive/newfile.txt", []);
            writeAction.Should().Throw<NotSupportedException>().WithMessage("Archive is read-only");
        }
        finally
        {
            if (Directory.Exists(realTempDir))
                Directory.Delete(realTempDir, true);
        }
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void MountPluginDirectory_MapsPhysicalPath()
    {
        var realTempDir = Path.Combine(Path.GetTempPath(), $"wotci_plugin_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(realTempDir);

        try
        {
            var pluginPath = Path.Combine(realTempDir, "plugin");
            Directory.CreateDirectory(pluginPath);
            File.WriteAllText(Path.Combine(pluginPath, "script.lua"), "-- Lua script");

            var (rootCaCert, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
            var (cert, key) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCaCert,
                rootCaKey,
                "Test",
                "/plugins/test"
            );

            var config = CreateTestSecurityPolicy();
            var vfs = new SimpleVirtualFileSystem(config);

            vfs.MountPluginDirectory("test-plugin", pluginPath);

            vfs.FileExists("/plugins/test-plugin/script.lua").Should().BeTrue();
            var content = Encoding.UTF8.GetString(
                vfs.ReadAllBytes("/plugins/test-plugin/script.lua")
            );
            content.Should().Be("-- Lua script");
        }
        finally
        {
            if (Directory.Exists(realTempDir))
                Directory.Delete(realTempDir, true);
        }
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void PathNormalization_HandlesVariousFormats()
    {
        var config = CreateTestSecurityPolicy();
        var vfs = new SimpleVirtualFileSystem(config);
        vfs.MountMemoryFileSystem("/test");
        vfs.WriteAllBytes("/test/file.txt", Encoding.UTF8.GetBytes("test"));

        // Act & Assert - Various path formats should work
        vfs.FileExists("/test/file.txt").Should().BeTrue();
        vfs.FileExists("test/file.txt").Should().BeTrue();
        vfs.FileExists(@"\test\file.txt").Should().BeTrue();
        vfs.FileExists(@"test\file.txt").Should().BeTrue();
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void NestedMountPoints_ResolvesToMostSpecific()
    {
        var config = CreateTestSecurityPolicy();
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

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public async Task MemoryFileSystemProvider_BasicOperations()
    {
        var provider = new MemoryFileSystemProvider();

        // Create directory structure
        await provider.CreateDirectoryAsync("folder");
        await provider.WriteFileAsync("file.txt", Encoding.UTF8.GetBytes("root file"));
        await provider.WriteFileAsync("folder/nested.txt", Encoding.UTF8.GetBytes("nested file"));

        // Files exist
        (await provider.ExistsAsync("file.txt"))
            .Should()
            .BeTrue();
        (await provider.ExistsAsync("folder/nested.txt")).Should().BeTrue();

        // Content is correct
        var rootContent = await provider.ReadFileAsync("file.txt");
        Encoding.UTF8.GetString(rootContent).Should().Be("root file");

        // Directory detection
        (await provider.IsDirectoryAsync("folder"))
            .Should()
            .BeTrue();
        (await provider.IsDirectoryAsync("file.txt")).Should().BeFalse();
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public async Task MemoryFileSystemProvider_DeleteOperations()
    {
        var provider = new MemoryFileSystemProvider();
        await provider.WriteFileAsync("deleteme.txt", Encoding.UTF8.GetBytes("delete this"));

        (await provider.ExistsAsync("deleteme.txt")).Should().BeTrue();
        await provider.DeleteFileAsync("deleteme.txt");

        (await provider.ExistsAsync("deleteme.txt")).Should().BeFalse();
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public async Task PhysicalPathProvider_MapsToRealFileSystem()
    {
        var realTempDir = Path.Combine(Path.GetTempPath(), $"wotci_physical_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(realTempDir);

        try
        {
            var basePath = Path.Combine(realTempDir, "physical");
            Directory.CreateDirectory(basePath);
            File.WriteAllText(Path.Combine(basePath, "real.txt"), "real file content");

            var provider = new PhysicalPathProvider(basePath);

            var exists = await provider.ExistsAsync("real.txt");
            var content = await provider.ReadFileAsync("real.txt");

            exists.Should().BeTrue();
            Encoding.UTF8.GetString(content).Should().Be("real file content");
        }
        finally
        {
            if (Directory.Exists(realTempDir))
                Directory.Delete(realTempDir, true);
        }
    }

    [Category("VFS.Unit")]
    [Category("Plugin.Unit")]
    [Test]
    public void OpenFile_CreatesStreams()
    {
        var config = CreateTestSecurityPolicy();
        var vfs = new SimpleVirtualFileSystem(config);
        vfs.MountMemoryFileSystem("/streams");

        // Write using stream
        using (var stream = vfs.OpenFile("/streams/test.txt", FileMode.Create, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
        {
            writer.WriteLine("Stream content");
        }

        // Read using stream
        using (var stream = vfs.OpenFile("/streams/test.txt", FileMode.Open, FileAccess.Read))
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            content.Should().Contain("Stream content");
        }
    }

    private void CreateTestZipFile(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
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
