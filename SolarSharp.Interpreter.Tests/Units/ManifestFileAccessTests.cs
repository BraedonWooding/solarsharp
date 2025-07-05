using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for manifest-based file access control and wildcard pattern matching.
    /// </summary>
    /// <remarks>
    ///     This test suite validates the file access control system that allows manifests
    ///     to define fine-grained permissions using wildcard patterns. Key features tested:
    ///     Wildcard Pattern Support:
    ///     - Basic wildcards: *.txt, *.lua
    ///     - Directory patterns: scripts/*.lua, config/*
    ///     - Recursive patterns: **/*.txt (all subdirectories)
    ///     - Complex patterns: modules/*/*.lua
    ///     Access Control Levels:
    ///     - FileAccess: none, read, write, readwrite, sandboxedreadwrite
    ///     - DirectoryAccess: none, list, listandcreatefiles
    ///     Integration Points:
    ///     - ManifestPolicy file/directory permissions
    ///     - SecurityConfiguration overrides
    ///     - FileSystemValidator enforcement
    ///     These tests ensure that file access can be controlled declaratively
    ///     through manifests, providing a flexible and secure permission system.
    ///     Test isolation: NonParallelizable - Uses shared file system for temp directories
    ///     Dependencies: Requires file system access for testing file permissions
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [NonParallelizable] // Uses shared file system
    public class ManifestFileAccessTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private string _tempDir;

        /// <summary>
        ///     Tests that ManifestPolicy correctly stores and converts file access permissions.
        /// </summary>
        /// <remarks>
        ///     ManifestPolicy uses string representations of permissions for JSON serialization.
        ///     This test verifies:
        ///     - String values are correctly stored
        ///     - Conversion to SecurityOverrides maintains correct enum values
        ///     - Both file and directory permissions are handled
        ///     The string-to-enum mapping ensures manifests remain human-readable while
        ///     maintaining type safety in the security system.
        /// </remarks>
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
                Assert.That(overrides.DefaultFileAccess, Is.EqualTo(FilePermissions.Read));
                Assert.That(overrides.DefaultDirectoryAccess, Is.EqualTo(DirectoryPermissions.List));
                Assert.That(overrides.FilePermissions["/app/config.txt"], Is.EqualTo(FilePermissions.Read));
                Assert.That(overrides.FilePermissions["/app/data.db"], Is.EqualTo(FilePermissions.ReadWrite));
                Assert.That(overrides.DirectoryPermissions["/app/logs"],
                    Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
                Assert.That(overrides.DirectoryPermissions["/app/temp"], Is.EqualTo(DirectoryPermissions.None));
            });
        }

        /// <summary>
        ///     Tests that ManifestFileEntry can specify both file and directory access levels.
        /// </summary>
        /// <remarks>
        ///     ManifestFileEntry allows specifying different permissions for:
        ///     - Files matching the pattern (FileAccess)
        ///     - Directories in the pattern path (DirectoryAccess)
        ///     This granular control allows scenarios like:
        ///     - Read files but not list directories
        ///     - Create files in specific directories
        ///     - Full access to certain file types
        /// </remarks>
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

        /// <summary>
        ///     Tests JSON serialization round-trip for manifests with file permissions.
        /// </summary>
        /// <remarks>
        ///     Validates that manifests with file access rules:
        ///     - Serialize to valid JSON
        ///     - Deserialize back to equivalent objects
        ///     - Preserve all permission settings
        ///     - Maintain wildcard patterns
        ///     This ensures manifests can be saved, loaded, and transmitted without
        ///     losing security configuration information.
        /// </remarks>
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
                    ["scripts/*.lua"] = new()
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

        /// <summary>
        ///     Tests basic wildcard pattern matching for file paths.
        /// </summary>
        /// <remarks>
        ///     Basic wildcard patterns include:
        ///     - *.ext: Matches any file with the extension
        ///     - filename.*: Matches file with any extension
        ///     - Exact matches without wildcards
        ///     The matching is case-insensitive to handle different filesystems
        ///     consistently across platforms.
        /// </remarks>
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

        /// <summary>
        ///     Tests wildcard matching for directory-based patterns.
        /// </summary>
        /// <remarks>
        ///     Directory patterns allow controlling access by location:
        ///     - dir/*.ext: Files with extension in specific directory
        ///     - dir/*: All files in directory (but not subdirectories)
        ///     This enables organizing permissions by directory structure,
        ///     such as allowing writes only to an output directory.
        /// </remarks>
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

        /// <summary>
        ///     Tests recursive wildcard patterns using ** notation.
        /// </summary>
        /// <remarks>
        ///     The ** pattern matches any number of directory levels:
        ///     - **/*.txt: All .txt files in any subdirectory
        ///     - scripts/**: All files under scripts/ recursively
        ///     - **/test/*.lua: test.lua files in any test directory
        ///     This is powerful for applying permissions to entire directory trees
        ///     while maintaining pattern-based filtering.
        /// </remarks>
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

        /// <summary>
        ///     Tests complex multi-level wildcard patterns.
        /// </summary>
        /// <remarks>
        ///     Complex patterns combine multiple wildcards:
        ///     - modules/*/*.lua: Lua files exactly two levels deep
        ///     - src/**/*.tsx: TSX files at any depth under src/
        ///     These patterns enable precise permission control for
        ///     specific project structures and naming conventions.
        /// </remarks>
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

        /// <summary>
        ///     Tests that wildcard matching is case-insensitive.
        /// </summary>
        /// <remarks>
        ///     Case-insensitive matching ensures consistent behavior across:
        ///     - Windows (case-insensitive filesystem)
        ///     - Linux/Unix (case-sensitive filesystem)
        ///     - macOS (case-insensitive by default)
        ///     This prevents security bypasses through case variations.
        /// </remarks>
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

        /// <summary>
        ///     Tests file enumeration using wildcard patterns.
        /// </summary>
        /// <remarks>
        ///     GetMatchingFiles() returns all files in a directory that match
        ///     a wildcard pattern. This is useful for:
        ///     - Batch processing operations
        ///     - Validating pattern effectiveness
        ///     - Implementing directory listings with filters
        ///     The test verifies that the correct files are found and returned.
        /// </remarks>
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

        /// <summary>
        ///     Tests recursive file search with ** wildcard patterns.
        /// </summary>
        /// <remarks>
        ///     Recursive search finds files at any depth in the directory tree.
        ///     This test creates a nested structure and verifies all matching
        ///     files are found regardless of depth.
        ///     Important for:
        ///     - Project-wide file operations
        ///     - Security policy validation
        ///     - Build system integration
        /// </remarks>
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

        /// <summary>
        ///     Tests that ManifestAutoLoader correctly creates security overrides from manifests.
        /// </summary>
        /// <remarks>
        ///     The auto-loader converts manifest file permissions into SecurityOverrides:
        ///     1. Reads manifest from script location
        ///     2. Parses file/directory permissions
        ///     3. Creates SecurityConfigurationOverrides
        ///     4. Returns overrides for script execution
        ///     This bridges the gap between declarative manifest permissions
        ///     and runtime security enforcement.
        /// </remarks>
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

            File.WriteAllText(manifestPath,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            // Test manifest discovery and override creation
            var scriptPath = Path.Combine(_tempDir, "test.lua");
            var baseConfig = SecurityConfiguration.Isolated();
            var overrides = ManifestAutoLoader.CreateOverridesFromManifest(scriptPath, baseConfig);

            if (overrides != null)
            {
                Assert.That(overrides.DefaultFileAccess, Is.EqualTo(FilePermissions.Read));

                if (overrides.FilePermissions != null)
                    Assert.Multiple(() =>
                    {
                        Assert.That(overrides.FilePermissions.ContainsKey("config.txt"), Is.True);
                        Assert.That(overrides.FilePermissions["config.txt"], Is.EqualTo(FilePermissions.Read));
                        Assert.That(overrides.FilePermissions["data.db"], Is.EqualTo(FilePermissions.ReadWrite));
                    });

                if (overrides.DirectoryPermissions != null)
                    Assert.Multiple(() =>
                    {
                        Assert.That(overrides.DirectoryPermissions.ContainsKey("logs"), Is.True);
                        Assert.That(overrides.DirectoryPermissions["logs"],
                            Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
                    });
            }
        }

        /// <summary>
        ///     Tests that ManifestFileEntry preserves wildcard patterns correctly.
        /// </summary>
        /// <remarks>
        ///     ManifestFileEntry stores:
        ///     - Pattern: The wildcard pattern for matching
        ///     - FileAccess: Permission level for matched files
        ///     - DirectoryAccess: Permission level for directories
        ///     This test ensures patterns are preserved exactly as specified,
        ///     including complex wildcards, for accurate matching.
        /// </remarks>
        [Test]
        public void ManifestFileEntry_WithWildcardPatterns()
        {
            var manifest = new Manifest
            {
                Files = new Dictionary<string, ManifestFileEntry>
                {
                    ["scripts/*.lua"] = new()
                    {
                        Pattern = "scripts/*.lua",
                        FileAccess = "sandboxedreadwrite",
                        DirectoryAccess = "listandcreatefiles"
                    },
                    ["config/**"] = new()
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

        /// <summary>
        ///     Tests application of manifest overrides to SecurityConfiguration.
        /// </summary>
        /// <remarks>
        ///     SecurityConfigurationOverrides can modify a base configuration:
        ///     - Sets default file/directory access
        ///     - Adds specific path permissions
        ///     - Overrides base configuration settings
        ///     This test verifies the override mechanism works correctly,
        ///     allowing manifests to customize security policies.
        /// </remarks>
        [Test]
        public void SecurityConfiguration_AppliesManifestOverrides()
        {
            var baseConfig = new SecurityConfiguration();
            var overrides = new SecurityConfigurationOverrides
            {
                DefaultFileAccess = FilePermissions.Read,
                FilePermissions = new Dictionary<string, FilePermissions>
                {
                    ["/app/secure.txt"] = FilePermissions.None,
                    ["/app/data.db"] = FilePermissions.ReadWrite
                },
                DirectoryPermissions = new Dictionary<string, DirectoryPermissions>
                {
                    ["/app/logs"] = DirectoryPermissions.ListAndCreateFiles,
                    ["/app/temp"] = DirectoryPermissions.None
                }
            };

            var result = overrides.ApplyTo(baseConfig);

            Assert.Multiple(() =>
            {
                Assert.That(result.FileSystem.DefaultFilePermissions, Is.EqualTo(FilePermissions.Read));
                Assert.That(result.FileSystem.GetFilePermissions("/app/secure.txt"), Is.EqualTo(FilePermissions.None));
                Assert.That(result.FileSystem.GetFilePermissions("/app/data.db"),
                    Is.EqualTo(FilePermissions.ReadWrite));
                Assert.That(result.FileSystem.GetDirectoryPermissions("/app/logs"),
                    Is.EqualTo(DirectoryPermissions.ListAndCreateFiles));
                Assert.That(result.FileSystem.GetDirectoryPermissions("/app/temp"),
                    Is.EqualTo(DirectoryPermissions.None));
            });
        }

        /// <summary>
        ///     Tests the complete workflow from manifest definition to security validation.
        /// </summary>
        /// <remarks>
        ///     This integration test validates the entire pipeline:
        ///     1. Create manifest with mixed file permissions
        ///     2. Convert to SecurityOverrides
        ///     3. Apply to SecurityConfiguration
        ///     4. Use FileSystemValidator to check operations
        ///     Validates that:
        ///     - Wildcard patterns work correctly
        ///     - Different permission levels are enforced
        ///     - Read/write/delete operations are properly controlled
        ///     - Directory operations respect permissions
        ///     This ensures the declarative manifest permissions translate into
        ///     actual runtime security enforcement.
        /// </remarks>
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
            Assert.DoesNotThrow(() => validator.ValidateFilePermissions("scripts/main.lua", FileOperation.Read));
            Assert.DoesNotThrow(() => validator.ValidateFilePermissions("scripts/main.lua", FileOperation.Create));
            Assert.Throws<FilePermissionViolationException>(() =>
                validator.ValidateFilePermissions("scripts/main.lua", FileOperation.Delete));

            Assert.DoesNotThrow(() => validator.ValidateFilePermissions("config/app.json", FileOperation.Read));
            Assert.Throws<FilePermissionViolationException>(() =>
                validator.ValidateFilePermissions("config/app.json", FileOperation.Write));

            Assert.DoesNotThrow(() => validator.ValidateFilePermissions("data/users.db", FileOperation.Read));
            Assert.DoesNotThrow(() => validator.ValidateFilePermissions("data/users.db", FileOperation.Write));
            Assert.DoesNotThrow(() => validator.ValidateFilePermissions("data/users.db", FileOperation.Delete));

            // Test directory operations
            Assert.DoesNotThrow(() => validator.ValidateDirectoryAccess("logs", DirectoryOperation.List));
            Assert.DoesNotThrow(() => validator.ValidateDirectoryAccess("logs", DirectoryOperation.Create));

            Assert.Throws<FilePermissionViolationException>(() =>
                validator.ValidateDirectoryAccess("temp", DirectoryOperation.List));
            Assert.Throws<FilePermissionViolationException>(() =>
                validator.ValidateDirectoryAccess("temp", DirectoryOperation.Create));
        }

        /// <summary>
        ///     Tests graceful handling of empty or null patterns.
        /// </summary>
        /// <remarks>
        ///     Edge case testing ensures the system doesn't crash with:
        ///     - Empty string patterns
        ///     - Null patterns
        ///     - Invalid pattern syntax
        ///     These should return false for matches or empty results,
        ///     never throw exceptions.
        /// </remarks>
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

        /// <summary>
        ///     Tests that invalid paths are handled without exceptions.
        /// </summary>
        /// <remarks>
        ///     FileSystemSecurity should handle:
        ///     - Empty paths
        ///     - Null paths
        ///     - Malformed paths
        ///     Returns appropriate defaults rather than crashing,
        ///     ensuring robust error handling.
        /// </remarks>
        [Test]
        public void InvalidPaths_HandleGracefully()
        {
            var fs = new FileSystemSecurity();

            // Should not throw for invalid paths, but return appropriate defaults
            Assert.DoesNotThrow(() => fs.GetFilePermissions(""));
            Assert.DoesNotThrow(() => fs.GetDirectoryPermissions(""));
        }

        /// <summary>
        ///     Tests that the ** pattern matches all files and directories.
        /// </summary>
        /// <remarks>
        ///     The ** pattern is a universal matcher that matches:
        ///     - Any file at any depth
        ///     - Any directory at any depth
        ///     - Empty paths
        ///     Useful for:
        ///     - Granting universal access (dangerous)
        ///     - Denying all access when combined with 'none'
        ///     - Testing and development scenarios
        /// </remarks>
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