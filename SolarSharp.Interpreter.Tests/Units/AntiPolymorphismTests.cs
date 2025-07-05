using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for anti-polymorphism security policies that prevent script self-modification attacks.
    ///     Anti-polymorphism measures protect against malicious scripts that attempt to modify their own
    ///     behavior or write to executable files, which could lead to code injection or persistence attacks.
    ///     These tests validate:
    ///     - Prevention of writes to .lua files
    ///     - Blocking access to manifest files
    ///     - Extension-based access controls
    ///     - Read-only file protections
    /// </summary>
    /// <remarks>
    ///     Test isolation: NonParallelizable - Uses shared file system for testing file access controls
    ///     Dependencies: Requires file system access for testing security policies
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [NonParallelizable] // Uses shared file system
    public class AntiPolymorphismTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_antipoly_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private string _tempDir;

        /// <summary>
        ///     Tests that the anti-polymorphism policy correctly prevents scripts from writing to .lua files.
        ///     This prevents malicious scripts from modifying other Lua scripts, which could be used for
        ///     code injection or creating persistent backdoors in the file system.
        /// </summary>
        [Test]
        public void TestPreventLuaFileWrites()
        {
            var config = SecurityConfiguration.DataProcessing() // Use DataProcessing which includes IO module
                .SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles)
                .AllowInternalDynamicCode()
                .WithAntiPolymorphism(p => p.PreventLuaFileWrites = true);

            var script = new Script(config);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should block writing to .lua files
            Assert.Throws<LuaFileWriteViolationException>(() =>
                script.DoString($@"
                    local f = io.open('{luaFile.Replace('\\', '/')}', 'w')
                "));
        }

        /// <summary>
        ///     Verifies that while .lua file writes are blocked, scripts can still write to other file types.
        ///     This ensures that legitimate data processing operations (writing logs, configs, output files)
        ///     are not impacted by the anti-polymorphism protections.
        /// </summary>
        [Test]
        public void TestAllowWritingNonLuaFiles()
        {
            var config = SecurityConfiguration.DataProcessing()
                .SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles)
                .AllowInternalDynamicCode()
                .WithAntiPolymorphism(p =>
                {
                    p.PreventLuaFileWrites = true;
                    p.AllowOnlyLuaExtension = false; // Allow non-lua files
                    p.BlockManifestAccess = false; // Allow for this test
                    p.ReadOnlyExtensions.Clear(); // Clear the default read-only extensions
                })
                .WithVirtualFileSystem(vfs =>
                {
                    if (vfs.VirtualMappings == null)
                        vfs.VirtualMappings = new Dictionary<string, string>();
                    vfs.VirtualMappings["/temp"] = _tempDir;
                });

            var script = new Script(config);

            var dataFile = "/temp/data.txt"; // Use VFS virtual path

            // CreateDataProcessing includes IO module, so this should work
            // Should allow writing to non-.lua files
            script.DoString($@"
                local f = io.open('{dataFile}', 'w')
                f:write('some data')
                f:close()
            ");

            // Check the actual file in the real temp directory
            var realDataFile = Path.Combine(_tempDir, "data.txt");
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(realDataFile), Is.True);
                Assert.That(File.ReadAllText(realDataFile), Is.EqualTo("some data"));
            });
        }

        /// <summary>
        ///     Tests that manifest files are properly protected from script access.
        ///     Manifest files contain security policies and should not be readable by scripts
        ///     to prevent information disclosure or tampering attempts.
        /// </summary>
        [Test]
        public void TestBlockManifestAccess()
        {
            var config = new SecurityConfiguration()
                .SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles)
                .AllowInternalDynamicCode()
                .WithStrictViolations(true)
                .WithAntiPolymorphism(static p => p.BlockManifestAccess = true);

            var script = new Script(config);

            var manifestFile = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestFile, @"{""version"": ""1.0""}");

            // Should block reading manifest files
            Assert.Throws<ManifestReadViolationException>(() =>
                script.DoString($@"
                    local f = io.open('{manifestFile.Replace('\\', '/')}', 'r')
                    local content = f:read('*a')
                    f:close()
                    return content
                "));
        }

        /// <summary>
        ///     Tests that files with blocked extensions (e.g., .exe, .dll) cannot be accessed by scripts.
        ///     This prevents scripts from reading or executing potentially malicious binary files
        ///     that could be used for privilege escalation or system compromise.
        /// </summary>
        [Test]
        public void TestBlockedExtensions()
        {
            // Use CreateDesktop which includes IO module
            var config = new SecurityConfiguration
            {
                AntiPolymorphism = new AntiPolymorphismPolicy()
            };
            config.AntiPolymorphism.BlockedExtensions.Add(".exe");
            config.AntiPolymorphism.BlockedExtensions.Add(".dll");
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var exeFile = Path.Combine(_tempDir, "malware.exe");
            File.WriteAllText(exeFile, "fake executable");

            // Should block access to blocked extensions
            Assert.Throws<BlockedExtensionException>(() =>
                script.DoString($@"
                    local f = io.open('{exeFile.Replace('\\', '/')}', 'r')
                "));
        }

        /// <summary>
        ///     Verifies that files with read-only extensions can be read but not written to.
        ///     This allows scripts to access configuration files and other read-only resources
        ///     while preventing modification that could lead to persistent configuration changes.
        /// </summary>
        [Test]
        public void TestReadOnlyExtensions()
        {
            var config = SecurityConfiguration.DataProcessing();
            // Don't replace the policy, just modify the existing one
            config.AntiPolymorphism.PreventLuaFileWrites = true;
            config.AntiPolymorphism.AllowOnlyLuaExtension = false; // Allow non-lua files
            config.AntiPolymorphism.BlockManifestAccess = false; // Allow for this test
            // Clear default read-only extensions and add only .config
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            config.AntiPolymorphism.ReadOnlyExtensions.Add(".config");

            // Ensure non-critical violations throw exceptions
            config.ThrowOnNonCriticalViolations = true;

            config.SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles);

            // For VFS, also set up a virtual mapping
            config.VirtualFileSystem.VirtualMappings ??= new Dictionary<string, string>();
            config.VirtualFileSystem.VirtualMappings["/temp"] = _tempDir;

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            // Create file in real temp directory
            var realConfigFile = Path.Combine(_tempDir, "app.config");
            File.WriteAllText(realConfigFile, "config data");

            // Use VFS virtual path in script
            const string configFile = "/temp/app.config";

            // CreateDataProcessing includes IO module
            // Should allow reading config files
            var result = script.DoString($@"
                local f = io.open('{configFile}', 'r')
                local content = f:read('*a')
                f:close()
                return content
            ");
            Assert.That(result.String, Is.EqualTo("config data"));

            // Should block writing to read-only extensions
            Assert.Throws<BlockedExtensionException>(() =>
                script.DoString($@"
                    local f = io.open('{configFile}', 'w')
                "));
        }

        [Test]
        public void TestProtectedFiles()
        {
            // Use CreateDesktop which includes IO module
            var config = new SecurityConfiguration
            {
                AntiPolymorphism = new AntiPolymorphismPolicy()
            };
            config.AntiPolymorphism.ProtectedFiles.Add("secret.txt");
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var secretFile = Path.Combine(_tempDir, "secret.txt");
            File.WriteAllText(secretFile, "secret data");

            // Should block access to protected files
            Assert.Throws<ProtectedFileAccessException>(() =>
                script.DoString($@"
                    local f = io.open('{secretFile.Replace('\\', '/')}', 'r')
                "));
        }

        [Test]
        public void TestAntiPolymorphismWithoutPolicy()
        {
            // Test that without anti-polymorphism policy, files can be accessed normally
            // Use raw SecurityConfiguration instead of CreateDataProcessing to avoid default policy
            var config = new SecurityConfiguration();
            config.SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles);
            // Ensure no AntiPolymorphism policy is set - explicitly disable all flags
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                AllowOnlyLuaExtension = false,
                PreventLuaFileWrites = false,
                PreventRunString = false,
                PreventInternalDynamicCode = false,
                BlockManifestAccess = false
            };
            // Clear the default read-only extensions list
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            config.AntiPolymorphism.BlockedExtensions.Clear();
            config.AntiPolymorphism.ProtectedFiles.Clear();

            // Add necessary capabilities
            config.Capabilities |= ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite;

            // Add necessary modules including IO, Metatables, and TableIterators
            config.AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math |
                                    CoreModules.Table | CoreModules.IO | CoreModules.Metatables |
                                    CoreModules.TableIterators;

            // Disable VFS for raw configuration tests
            config.VirtualFileSystem.Enabled = false;


            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var luaFile = Path.Combine(_tempDir, "test.lua");
            var manifestFile = Path.Combine(_tempDir, "LuaManifest.json");

            // Should allow writing to .lua files when policy is not set
            script.DoString($@"
                local f = io.open('{luaFile.Replace('\\', '/')}', 'w')
                f:write('return 42')
                f:close()
            ");

            Assert.That(File.Exists(luaFile), Is.True);

            // Should allow reading manifest when policy is not set
            File.WriteAllText(manifestFile, @"{""version"": ""1.0""}");
            var result = script.DoString($@"
                local f = io.open('{manifestFile.Replace('\\', '/')}', 'r')
                local content = f:read('*a')
                f:close()
                return content
            ");

            Assert.That(result.String, Does.Contain("version"));
        }

        /// <summary>
        ///     Tests the AllowOnlyLuaExtension policy which restricts script execution to .lua files only.
        ///     This test validates that when enabled, the policy still allows reading both .lua and non-.lua files
        ///     (the restriction primarily applies to execution), ensuring data processing operations remain functional.
        /// </summary>
        [Test]
        public void TestAllowOnlyLuaExtension()
        {
            // This test validates that when AllowOnlyLuaExtension is set, the system still allows
            // reading both .lua and non-.lua files (the restriction is mainly for execution)

            // Use a raw configuration with IO module to avoid interference from default policies
            var config = new SecurityConfiguration
            {
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math |
                                 CoreModules.Table | CoreModules.IO | CoreModules.Metatables |
                                 CoreModules.TableIterators,
                AntiPolymorphism = new AntiPolymorphismPolicy
                {
                    AllowOnlyLuaExtension = true,
                    PreventLuaFileWrites = false, // Only test this specific flag
                    PreventRunString = false, // Disable other defaults
                    PreventInternalDynamicCode = false, // Disable other defaults
                    BlockManifestAccess = false // Disable other defaults
                }
            };
            // Clear default lists to avoid interference
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            config.AntiPolymorphism.BlockedExtensions.Clear();
            config.AntiPolymorphism.ProtectedFiles.Clear();
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles);
            config.FileSystem.DefaultFilePermissions = FilePermissions.Read;
            config.Capabilities |= ScriptCapabilities.FileRead;

            // Disable VFS for raw configuration tests
            config.VirtualFileSystem.Enabled = false;

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var luaFile = Path.Combine(_tempDir, "test.lua");
            var txtFile = Path.Combine(_tempDir, "data.txt");

            File.WriteAllText(luaFile, "return 42");
            File.WriteAllText(txtFile, "some data");


            // Should allow reading .lua files
            var result = script.DoString($@"
                local f = io.open('{luaFile.Replace('\\', '/')}', 'r')
                local content = f:read('*a')
                f:close()
                return content
            ");
            Assert.That(result.String, Is.EqualTo("return 42"));

            // Should allow reading non-.lua files for data (the restriction is mainly for execution)
            result = script.DoString($@"
                local f = io.open('{txtFile.Replace('\\', '/')}', 'r')
                local content = f:read('*a')
                f:close()
                return content
            ");
            Assert.That(result.String, Is.EqualTo("some data"));
        }

        /// <summary>
        ///     Comprehensive integration test that validates all anti-polymorphism protections working together.
        ///     Tests multiple security controls simultaneously including:
        ///     - Lua file write prevention
        ///     - Manifest access blocking
        ///     - Read-only file protections
        ///     - Dynamic code execution controls
        ///     This ensures the complete security posture functions correctly in realistic scenarios.
        /// </summary>
        [Test]
        public void TestCompleteAntiPolymorphismSetup()
        {
            var config = SecurityConfiguration.DataProcessing()
                .SetDirectoryPermissions(_tempDir, DirectoryPermissions.ListAndCreateFiles)
                .AllowRunString() // Allow DoString() for testing
                .AllowInternalDynamicCode() // Allow load() functions
                .WithAntiPolymorphism(static p =>
                {
                    p.AllowOnlyLuaExtension = false; // Allow writing to non-lua files
                    p.PreventLuaFileWrites = true;
                    p.BlockManifestAccess = true;
                    // Clear default read-only extensions except .lua
                    p.ReadOnlyExtensions.Clear();
                    p.ReadOnlyExtensions.Add(".lua");
                    p.ReadOnlyExtensions.Add(".luac");
                });

            config.ThrowOnNonCriticalViolations = true; // Required for manifest access to throw

            // For VFS, also set up a virtual mapping
            config.VirtualFileSystem.VirtualMappings ??= new Dictionary<string, string>();
            config.VirtualFileSystem.VirtualMappings["/temp"] = _tempDir;

            var script = new Script(config);

            // Create test files in the real temp directory
            var realLuaFile = Path.Combine(_tempDir, "test.lua");
            var realManifestFile = Path.Combine(_tempDir, "LuaManifest.json");
            var realDataFile = Path.Combine(_tempDir, "data.txt");

            File.WriteAllText(realLuaFile, "return 'original'");
            File.WriteAllText(realManifestFile, @"{""version"": ""1.0""}");

            // Use VFS virtual paths in script
            const string luaFile = "/temp/test.lua";
            const string manifestFile = "/temp/LuaManifest.json";
            const string dataFile = "/temp/data.txt";

            // Should block writing to .lua files
            Assert.Throws<LuaFileWriteViolationException>(() =>
                script.DoString($@"
                    local f = io.open('{luaFile}', 'w')
                    f:write('malicious')
                    f:close()
                "));

            // Should block reading manifest files
            Assert.Throws<ManifestReadViolationException>(() =>
                script.DoString($@"
                    local f = io.open('{manifestFile}', 'r')
                    f:close()
                    return 'should not reach here'
                "));

            // Should allow writing to data files
            script.DoString($@"
                local f = io.open('{dataFile}', 'w')
                f:write('legitimate data')
                f:close()
            ");

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(realDataFile), Is.True);
                Assert.That(File.ReadAllText(realDataFile), Is.EqualTo("legitimate data"));
            });
        }
    }
}