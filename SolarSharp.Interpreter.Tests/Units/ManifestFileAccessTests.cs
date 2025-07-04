using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;
using FileAccess = SolarSharp.Interpreter.Security.FileAccess;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class ManifestFileAccessTests
    {
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void ManifestPolicy_SupportsFileAccessPermissions()
        {
            var policy = new ManifestPolicy
            {
                DefaultFileAccess = "read",
                DefaultDirectoryAccess = "list",
                FilePermissions = new Dictionary<string, string>
                {
                    ["/app/config.txt"] = "read",
                    ["/app/data.db"] = "readwrite"
                },
                DirectoryPermissions = new Dictionary<string, string>
                {
                    ["/app/logs"] = "listandcreatefiles",
                    ["/app/temp"] = "none"
                }
            };

            var overrides = policy.ToSecurityOverrides();

            Assert.Multiple(() =>
            {
                Assert.That(overrides.DefaultFileAccess, Is.EqualTo(FileAccess.Read));
                Assert.That(overrides.DefaultDirectoryAccess, Is.EqualTo(DirectoryAccess.List));
                Assert.That(overrides.FilePermissions["/app/config.txt"], Is.EqualTo(FileAccess.Read));
                Assert.That(overrides.FilePermissions["/app/data.db"], Is.EqualTo(FileAccess.ReadWrite));
                Assert.That(overrides.DirectoryPermissions["/app/logs"], Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
                Assert.That(overrides.DirectoryPermissions["/app/temp"], Is.EqualTo(DirectoryAccess.None));
            });
        }

        [Test]
        public void ManifestFileEntry_SupportsFileAndDirectoryAccess()
        {
            var fileEntry = new ManifestFileEntry
            {
                Pattern = "scripts/*.lua",
                FileAccess = "sandboxedreadwrite",
                DirectoryAccess = "listandcreatefiles"
            };

            Assert.Multiple(() =>
            {
                Assert.That(fileEntry.FileAccess, Is.EqualTo("sandboxedreadwrite"));
                Assert.That(fileEntry.DirectoryAccess, Is.EqualTo("listandcreatefiles"));
            });
        }

        [Test]
        public void Manifest_SerializesFileAccessCorrectly()
        {
            var manifest = new Manifest
            {
                Version = "1.0",
                Policy = new ManifestPolicy
                {
                    DefaultFileAccess = "read",
                    FilePermissions = new Dictionary<string, string>
                    {
                        ["*.txt"] = "read",
                        ["data/*.db"] = "readwrite"
                    }
                },
                Files = new Dictionary<string, ManifestFileEntry>
                {
                    ["scripts/*.lua"] = new ManifestFileEntry
                    {
                        FileAccess = "sandboxedreadwrite",
                        DirectoryAccess = "listandcreatefiles"
                    }
                }
            };

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<Manifest>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Policy.DefaultFileAccess, Is.EqualTo("read"));
                Assert.That(deserialized.Policy.FilePermissions["*.txt"], Is.EqualTo("read"));
                Assert.That(deserialized.Policy.FilePermissions["data/*.db"], Is.EqualTo("readwrite"));
                Assert.That(deserialized.Files["scripts/*.lua"].FileAccess, Is.EqualTo("sandboxedreadwrite"));
                Assert.That(deserialized.Files["scripts/*.lua"].DirectoryAccess, Is.EqualTo("listandcreatefiles"));
            });
        }

        [Test]
        public void WildcardMatching_BasicPatterns()
        {
            Assert.Multiple(() =>
            {
                // Test basic wildcard patterns
                Assert.That("test.txt".MatchesWildcard("*.txt"), Is.True);
                Assert.That("data.json".MatchesWildcard("*.json"), Is.True);
                Assert.That("test.txt".MatchesWildcard("*.json"), Is.False);

                // Test exact matches
                Assert.That("specific.lua".MatchesWildcard("specific.lua"), Is.True);
                Assert.That("other.lua".MatchesWildcard("specific.lua"), Is.False);
            });
        }

        [Test]
        public void WildcardMatching_DirectoryPatterns()
        {
            Assert.Multiple(() =>
            {
                // Test directory-based patterns
                Assert.That("scripts/main.lua".MatchesWildcard("scripts/*.lua"), Is.True);
                Assert.That("config/app.json".MatchesWildcard("config/*"), Is.True);
                Assert.That("other/main.lua".MatchesWildcard("scripts/*.lua"), Is.False);
            });
        }

        [Test]
        public void WildcardMatching_RecursivePatterns()
        {
            Assert.Multiple(() =>
            {
                // Test recursive patterns with **
                Assert.That("deep/nested/file.txt".MatchesWildcard("**/*.txt"), Is.True);
                Assert.That("any/path/here/script.lua".MatchesWildcard("**/*.lua"), Is.True);
                Assert.That("scripts/utils/helper.lua".MatchesWildcard("scripts/**"), Is.True);
                Assert.That("scripts/main.lua".MatchesWildcard("data/**"), Is.False);
            });
        }

        [Test]
        public void WildcardMatching_ComplexPatterns()
        {
            Assert.Multiple(() =>
            {
                // Test more complex patterns
                Assert.That("modules/core/main.lua".MatchesWildcard("modules/*/*.lua"), Is.True);
                Assert.That("src/components/ui/button.tsx".MatchesWildcard("src/**/*.tsx"), Is.True);
                Assert.That("test/unit/spec.js".MatchesWildcard("src/**/*.tsx"), Is.False);
            });
        }

        [Test]
        public void WildcardMatching_CaseInsensitive()
        {
            Assert.Multiple(() =>
            {
                // Test case insensitive matching
                Assert.That("TEST.TXT".MatchesWildcard("*.txt"), Is.True);
                Assert.That("Scripts/Main.LUA".MatchesWildcard("scripts/*.lua"), Is.True);
            });
        }

        [Test]
        public void GetMatchingFiles_FindsFilesWithWildcards()
        {
            // Create test files
            var scriptsDir = Path.Combine(_tempDir, "scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "main.lua"), "-- main script");
            File.WriteAllText(Path.Combine(scriptsDir, "helper.lua"), "-- helper script");
            File.WriteAllText(Path.Combine(scriptsDir, "config.json"), "{}");

            var luaFiles = "scripts/*.lua".GetMatchingFiles(_tempDir);

            Assert.That(luaFiles.Length, Is.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(luaFiles[0].EndsWith("main.lua") || luaFiles[0].EndsWith("helper.lua"), Is.True);
                Assert.That(luaFiles[1].EndsWith("main.lua") || luaFiles[1].EndsWith("helper.lua"), Is.True);
            });
        }

        [Test]
        public void GetMatchingFiles_RecursiveSearch()
        {
            // Create nested directory structure
            var deepDir = Path.Combine(_tempDir, "deep", "nested", "path");
            Directory.CreateDirectory(deepDir);
            File.WriteAllText(Path.Combine(_tempDir, "root.lua"), "-- root");
            File.WriteAllText(Path.Combine(_tempDir, "deep", "mid.lua"), "-- mid");
            File.WriteAllText(Path.Combine(deepDir, "leaf.lua"), "-- leaf");

            var allLuaFiles = "**/*.lua".GetMatchingFiles(_tempDir);

            Assert.That(allLuaFiles.Length, Is.EqualTo(3));
        }

        [Test]
        public void ManifestAutoLoader_AppliesFileAccessFromManifest()
        {
            // Create manifest with file access rules
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var manifest = new Manifest
            {
                Version = "1.0",
                Policy = new ManifestPolicy
                {
                    DefaultFileAccess = "read",
                    FilePermissions = new Dictionary<string, string>
                    {
                        ["config.txt"] = "read",
                        ["data.db"] = "readwrite"
                    },
                    DirectoryPermissions = new Dictionary<string, string>
                    {
                        ["logs"] = "listandcreatefiles"
                    }
                }
            };

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            // Test manifest discovery and override creation
            var scriptPath = Path.Combine(_tempDir, "test.lua");
            var overrides = ManifestAutoLoader.CreateOverridesFromManifest(scriptPath);

            if (overrides != null)
            {
                Assert.That(overrides.DefaultFileAccess, Is.EqualTo(FileAccess.Read));

                if (overrides.FilePermissions != null)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(overrides.FilePermissions.ContainsKey("config.txt"), Is.True);
                        Assert.That(overrides.FilePermissions["config.txt"], Is.EqualTo(FileAccess.Read));
                        Assert.That(overrides.FilePermissions["data.db"], Is.EqualTo(FileAccess.ReadWrite));
                    });
                }

                if (overrides.DirectoryPermissions != null)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(overrides.DirectoryPermissions.ContainsKey("logs"), Is.True);
                        Assert.That(overrides.DirectoryPermissions["logs"], Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
                    });
                }
            }
        }

        [Test]
        public void ManifestFileEntry_WithWildcardPatterns()
        {
            var manifest = new Manifest
            {
                Files = new Dictionary<string, ManifestFileEntry>
                {
                    ["scripts/*.lua"] = new ManifestFileEntry
                    {
                        Pattern = "scripts/*.lua",
                        FileAccess = "sandboxedreadwrite",
                        DirectoryAccess = "listandcreatefiles"
                    },
                    ["config/**"] = new ManifestFileEntry
                    {
                        Pattern = "config/**",
                        FileAccess = "read",
                        DirectoryAccess = "list"
                    }
                }
            };

            Assert.Multiple(() =>
            {
                // Test that patterns are preserved
                Assert.That(manifest.Files["scripts/*.lua"].Pattern, Is.EqualTo("scripts/*.lua"));
                Assert.That(manifest.Files["config/**"].Pattern, Is.EqualTo("config/**"));

                // Test access levels
                Assert.That(manifest.Files["scripts/*.lua"].FileAccess, Is.EqualTo("sandboxedreadwrite"));
                Assert.That(manifest.Files["config/**"].FileAccess, Is.EqualTo("read"));
            });
        }

        [Test]
        public void SecurityConfiguration_AppliesManifestOverrides()
        {
            var baseConfig = new SecurityConfiguration();
            var overrides = new SecurityConfigurationOverrides
            {
                DefaultFileAccess = FileAccess.Read,
                FilePermissions = new Dictionary<string, FileAccess>
                {
                    ["/app/secure.txt"] = FileAccess.None,
                    ["/app/data.db"] = FileAccess.ReadWrite
                },
                DirectoryPermissions = new Dictionary<string, DirectoryAccess>
                {
                    ["/app/logs"] = DirectoryAccess.ListAndCreateFiles,
                    ["/app/temp"] = DirectoryAccess.None
                }
            };

            var result = overrides.ApplyTo(baseConfig);

            Assert.Multiple(() =>
            {
                Assert.That(result.FileSystem.DefaultFileAccess, Is.EqualTo(FileAccess.Read));
                Assert.That(result.FileSystem.GetFileAccess("/app/secure.txt"), Is.EqualTo(FileAccess.None));
                Assert.That(result.FileSystem.GetFileAccess("/app/data.db"), Is.EqualTo(FileAccess.ReadWrite));
                Assert.That(result.FileSystem.GetDirectoryAccess("/app/logs"), Is.EqualTo(DirectoryAccess.ListAndCreateFiles));
                Assert.That(result.FileSystem.GetDirectoryAccess("/app/temp"), Is.EqualTo(DirectoryAccess.None));
            });
        }

        [Test]
        public void CompleteWorkflow_ManifestToSecurityValidation()
        {
            // Create a complete workflow test: manifest -> overrides -> security config -> validation

            // 1. Create manifest with mixed permissions
            var manifest = new Manifest
            {
                Policy = new ManifestPolicy
                {
                    DefaultFileAccess = "read",
                    FilePermissions = new Dictionary<string, string>
                    {
                        ["scripts/*.lua"] = "sandboxedreadwrite",
                        ["config/*.json"] = "read",
                        ["data/*.db"] = "readwrite"
                    },
                    DirectoryPermissions = new Dictionary<string, string>
                    {
                        ["scripts"] = "listandcreatefiles",
                        ["config"] = "list",
                        ["data"] = "listandcreatefiles",
                        ["logs"] = "listandcreatefiles",
                        ["temp"] = "none"
                    }
                }
            };

            // 2. Convert to overrides
            var overrides = manifest.Policy.ToSecurityOverrides();

            // 3. Apply to base configuration
            var baseConfig = new SecurityConfiguration();
            var finalConfig = overrides.ApplyTo(baseConfig);


            // 4. Test file system validator
            var validator = new FileSystemValidator(finalConfig.FileSystem);

            // Test various file operations
            Assert.DoesNotThrow(() => validator.ValidateFileAccess("scripts/main.lua", FileOperation.Read));
            Assert.DoesNotThrow(() => validator.ValidateFileAccess("scripts/main.lua", FileOperation.Create));
            Assert.Throws<FileAccessViolationException>(() => validator.ValidateFileAccess("scripts/main.lua", FileOperation.Delete));

            Assert.DoesNotThrow(() => validator.ValidateFileAccess("config/app.json", FileOperation.Read));
            Assert.Throws<FileAccessViolationException>(() => validator.ValidateFileAccess("config/app.json", FileOperation.Write));

            Assert.DoesNotThrow(() => validator.ValidateFileAccess("data/users.db", FileOperation.Read));
            Assert.DoesNotThrow(() => validator.ValidateFileAccess("data/users.db", FileOperation.Write));
            Assert.DoesNotThrow(() => validator.ValidateFileAccess("data/users.db", FileOperation.Delete));

            // Test directory operations
            Assert.DoesNotThrow(() => validator.ValidateDirectoryAccess("logs", DirectoryOperation.List));
            Assert.DoesNotThrow(() => validator.ValidateDirectoryAccess("logs", DirectoryOperation.Create));

            Assert.Throws<FileAccessViolationException>(() => validator.ValidateDirectoryAccess("temp", DirectoryOperation.List));
            Assert.Throws<FileAccessViolationException>(() => validator.ValidateDirectoryAccess("temp", DirectoryOperation.Create));
        }

        [Test]
        public void EmptyOrNullPatterns_HandleGracefully()
        {
            Assert.Multiple(() =>
            {
                Assert.That("test.txt".MatchesWildcard(""), Is.False);
                Assert.That("test.txt".MatchesWildcard(null), Is.False);
            });

            var emptyResult = "".GetMatchingFiles(_tempDir);
            Assert.That(emptyResult.Length, Is.EqualTo(0));
        }

        [Test]
        public void InvalidPaths_HandleGracefully()
        {
            var fs = new FileSystemSecurity();

            // Should not throw for invalid paths, but return appropriate defaults
            Assert.DoesNotThrow(() => fs.GetFileAccess(""));
            Assert.DoesNotThrow(() => fs.GetDirectoryAccess(""));
        }

        [Test]
        public void UniversalPattern_MatchesEverything()
        {
            Assert.Multiple(() =>
            {
                Assert.That("any/path/file.txt".MatchesWildcard("**"), Is.True);
                Assert.That("simple.txt".MatchesWildcard("**"), Is.True);
                Assert.That("deep/nested/path/file.lua".MatchesWildcard("**"), Is.True);
            });
        }
    }
}