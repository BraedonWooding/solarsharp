using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class AntiPolymorphismTests
    {
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_antipoly_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void TestPreventLuaFileWrites()
        {
            var config = SecurityConfiguration.CreateDataProcessing(); // Use DataProcessing which includes IO module
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                PreventLuaFileWrites = true
            };
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);

            var script = new Script(config, StringExecution.True);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should block writing to .lua files
            Assert.Throws<LuaFileWriteViolationException>(() =>
                script.DoString($@"
                    local f = io.open('{luaFile.Replace('\\', '/')}', 'w')
                "));
        }

        [Test]
        public void TestAllowWritingNonLuaFiles()
        {
            var config = SecurityConfiguration.CreateDataProcessing();
            // Don't replace the policy, just modify the existing one to keep all defaults
            config.AntiPolymorphism.PreventLuaFileWrites = true;
            config.AntiPolymorphism.AllowOnlyLuaExtension = false; // Allow non-lua files
            config.AntiPolymorphism.PreventDynamicCode = false;    // Allow for this test
            config.AntiPolymorphism.BlockManifestAccess = false;   // Allow for this test
            // Clear the default read-only extensions since we're testing writing
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);
            
            // For VFS, also set up a virtual mapping
            if (config.VirtualFileSystem.VirtualMappings == null)
                config.VirtualFileSystem.VirtualMappings = new Dictionary<string, string>();
            config.VirtualFileSystem.VirtualMappings["/temp"] = _tempDir;

            var script = new Script(config, StringExecution.True);

            var dataFile = "/temp/data.txt";  // Use VFS virtual path

            // CreateDataProcessing includes IO module, so this should work
            // Should allow writing to non-.lua files
            script.DoString($@"
                local f = io.open('{dataFile}', 'w')
                f:write('some data')
                f:close()
            ");

            // Check the actual file in the real temp directory
            var realDataFile = Path.Combine(_tempDir, "data.txt");
            Assert.That(File.Exists(realDataFile), Is.True);
            Assert.That(File.ReadAllText(realDataFile), Is.EqualTo("some data"));
        }

        [Test]
        public void TestBlockManifestAccess()
        {
            // Use CreateDesktop which includes IO module
            var config = SecurityConfiguration.CreateDesktop();
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                BlockManifestAccess = true
            };
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);

            var script = new Script(config, StringExecution.True);

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

        [Test]
        public void TestBlockedExtensions()
        {
            // Use CreateDesktop which includes IO module
            var config = SecurityConfiguration.CreateDesktop();
            config.AntiPolymorphism = new AntiPolymorphismPolicy();
            config.AntiPolymorphism.BlockedExtensions.Add(".exe");
            config.AntiPolymorphism.BlockedExtensions.Add(".dll");
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);

            var script = new Script(config, StringExecution.True);

            var exeFile = Path.Combine(_tempDir, "malware.exe");
            File.WriteAllText(exeFile, "fake executable");

            // Should block access to blocked extensions
            Assert.Throws<BlockedExtensionException>(() =>
                script.DoString($@"
                    local f = io.open('{exeFile.Replace('\\', '/')}', 'r')
                "));
        }

        [Test]
        public void TestReadOnlyExtensions()
        {
            var config = SecurityConfiguration.CreateDataProcessing();
            // Don't replace the policy, just modify the existing one
            config.AntiPolymorphism.PreventLuaFileWrites = true;
            config.AntiPolymorphism.AllowOnlyLuaExtension = false; // Allow non-lua files
            config.AntiPolymorphism.PreventDynamicCode = false;    // Allow for this test
            config.AntiPolymorphism.BlockManifestAccess = false;   // Allow for this test
            // Clear default read-only extensions and add only .config
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            config.AntiPolymorphism.ReadOnlyExtensions.Add(".config");
            
            // Ensure non-critical violations throw exceptions
            config.ThrowOnNonCriticalViolations = true;
            
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);
            
            // For VFS, also set up a virtual mapping
            if (config.VirtualFileSystem.VirtualMappings == null)
                config.VirtualFileSystem.VirtualMappings = new Dictionary<string, string>();
            config.VirtualFileSystem.VirtualMappings["/temp"] = _tempDir;

            var script = new Script(config, StringExecution.True);

            // Create file in real temp directory
            var realConfigFile = Path.Combine(_tempDir, "app.config");
            File.WriteAllText(realConfigFile, "config data");
            
            // Use VFS virtual path in script
            var configFile = "/temp/app.config";

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
            var config = SecurityConfiguration.CreateDesktop();
            config.AntiPolymorphism = new AntiPolymorphismPolicy();
            config.AntiPolymorphism.ProtectedFiles.Add("secret.txt");
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);

            var script = new Script(config, StringExecution.True);

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
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);
            // Ensure no AntiPolymorphism policy is set - explicitly disable all flags
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                AllowOnlyLuaExtension = false,
                PreventLuaFileWrites = false,
                PreventDynamicCode = false,
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
            

            var script = new Script(config, StringExecution.True);

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

        [Test]
        public void TestAllowOnlyLuaExtension()
        {
            // This test validates that when AllowOnlyLuaExtension is set, the system still allows
            // reading both .lua and non-.lua files (the restriction is mainly for execution)
            
            // Use a raw configuration with IO module to avoid interference from default policies
            var config = new SecurityConfiguration();
            config.AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                                   CoreModules.Table | CoreModules.IO | CoreModules.Metatables | CoreModules.TableIterators;
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                AllowOnlyLuaExtension = true,
                PreventLuaFileWrites = false,  // Only test this specific flag
                PreventDynamicCode = false,    // Disable other defaults
                BlockManifestAccess = false    // Disable other defaults
            };
            // Clear default lists to avoid interference
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            config.AntiPolymorphism.BlockedExtensions.Clear();
            config.AntiPolymorphism.ProtectedFiles.Clear();
            config.ThrowOnNonCriticalViolations = true;
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);
            config.FileSystem.DefaultFileAccess = SolarSharp.Interpreter.Security.FileAccess.Read;
            config.Capabilities |= ScriptCapabilities.FileRead;
            
            // Disable VFS for raw configuration tests
            config.VirtualFileSystem.Enabled = false;

            var script = new Script(config, StringExecution.True);

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

        [Test]
        public void TestCompleteAntiPolymorphismSetup()
        {
            var config = SecurityConfiguration.CreateDataProcessing();
            // Don't replace the whole policy, just modify the existing one to keep all the flags
            config.AntiPolymorphism.AllowOnlyLuaExtension = false; // Allow writing to non-lua files
            config.AntiPolymorphism.PreventLuaFileWrites = true;
            config.AntiPolymorphism.BlockManifestAccess = true;
            config.AntiPolymorphism.PreventDynamicCode = true;
            // Clear default read-only extensions except .lua
            config.AntiPolymorphism.ReadOnlyExtensions.Clear();
            config.AntiPolymorphism.ReadOnlyExtensions.Add(".lua");
            config.AntiPolymorphism.ReadOnlyExtensions.Add(".luac");
            
            config.ThrowOnNonCriticalViolations = true; // Required for manifest access to throw
            config.SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);
            
            // For VFS, also set up a virtual mapping
            if (config.VirtualFileSystem.VirtualMappings == null)
                config.VirtualFileSystem.VirtualMappings = new Dictionary<string, string>();
            config.VirtualFileSystem.VirtualMappings["/temp"] = _tempDir;

            var script = new Script(config, StringExecution.True);

            // Create test files in real temp directory
            var realLuaFile = Path.Combine(_tempDir, "test.lua");
            var realManifestFile = Path.Combine(_tempDir, "LuaManifest.json");
            var realDataFile = Path.Combine(_tempDir, "data.txt");
            
            File.WriteAllText(realLuaFile, "return 'original'");
            File.WriteAllText(realManifestFile, @"{""version"": ""1.0""}");

            // Use VFS virtual paths in script
            var luaFile = "/temp/test.lua";
            var manifestFile = "/temp/LuaManifest.json";
            var dataFile = "/temp/data.txt";

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

            Assert.That(File.Exists(realDataFile), Is.True);
            Assert.That(File.ReadAllText(realDataFile), Is.EqualTo("legitimate data"));
        }
    }
}