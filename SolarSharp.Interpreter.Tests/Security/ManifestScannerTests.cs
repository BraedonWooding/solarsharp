using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Security
{
    [TestFixture]
    [Category("Manifest.Integration")]
    public class ManifestScannerTests
    {
        private string _tempDir;
        private ManifestScanner _scanner;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "manifest_scanner_tests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            _scanner = new ManifestScanner(new FileSystem());
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_FindsSingleManifest()
        {
            // Create a simple manifest
            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "test-app-manifest",
                packageName: "TestApp",
                packageVersion: "1.0.0",
                packageDescription: "Test application for manifest scanning"
            );

            var manifestPath = Path.Combine(_tempDir, "manifest.json");
            var json = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true }
            );
            await File.WriteAllTextAsync(manifestPath, json);

            // Scan directory with validation disabled to test scanning logic
            var options = new ManifestScanOptions { ValidateManifests = false };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(results[0].IsSuccess, Is.True);
                Assert.That(results[0].Manifest?.GetFirstPackageName(), Is.EqualTo("TestApp"));
            });
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_FindsMultipleManifests()
        {
            // Create directory structure
            var subDir1 = Path.Combine(_tempDir, "module1");
            var subDir2 = Path.Combine(_tempDir, "module2");
            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);

            // Create manifests
            var manifests = new[]
            {
                (Path.Combine(_tempDir, "manifest.json"), "RootApp", "1.0.0"),
                (Path.Combine(subDir1, "manifest.json"), "Module1", "2.0.0"),
                (Path.Combine(subDir2, ".luamanifest.json"), "Module2", "3.0.0"),
            };

            foreach (var (path, name, version) in manifests)
            {
                var manifest = CreateTestManifest(name, version);
                var json = JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true }
                );
                await File.WriteAllTextAsync(path, json);
            }

            // Scan with validation disabled to test scanning logic
            var options = new ManifestScanOptions { ValidateManifests = false };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results.All(r => r.IsSuccess), Is.True);

            var names = results
                .Where(r => r.IsSuccess && r.Manifest != null)
                .Select(r => r.Manifest.GetFirstPackageName())
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(n => n)
                .ToList();
            Assert.That(names, Is.EqualTo(new[] { "Module1", "Module2", "RootApp" }));
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_RespectsMaxDepth()
        {
            // Create deep directory structure
            var currentDir = _tempDir;
            for (var i = 0; i < 5; i++)
            {
                currentDir = Path.Combine(currentDir, $"level{i}");
                Directory.CreateDirectory(currentDir);

                var manifest = CreateTestManifest($"Level{i}", "1.0.0");
                var json = JsonSerializer.Serialize(manifest);
                await File.WriteAllTextAsync(Path.Combine(currentDir, "manifest.json"), json);
            }

            // Scan with max depth = 2 (depth 0 = _tempDir, depth 1 = level0, depth 2 = level1)
            var options = new ManifestScanOptions { MaxDepth = 2, ValidateManifests = false };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            // Should find manifests at depth 1 and 2 only (level0 and level1)
            Assert.That(results, Has.Count.EqualTo(2));
            var names = results
                .Where(r => r.IsSuccess && r.Manifest != null)
                .Select(r => r.Manifest.GetFirstPackageName())
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(n => n)
                .ToList();
            Assert.That(names, Is.EqualTo(new[] { "Level0", "Level1" }));
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_StopsAtManifest()
        {
            // Create structure where manifest stops recursion
            var pluginDir = Path.Combine(_tempDir, "plugins");
            var plugin1Dir = Path.Combine(pluginDir, "plugin1");
            var plugin1SubDir = Path.Combine(plugin1Dir, "internal");

            Directory.CreateDirectory(plugin1SubDir);

            // Plugin manifest (should stop here)
            var plugin1Result = await WriteManifest(
                Path.Combine(plugin1Dir, "manifest.json"),
                "Plugin1",
                "1.0.0"
            );
            Assert.That(plugin1Result.IsSuccess, Is.True);

            // This shouldn't be found if StopAtManifest is true
            var internalResult = await WriteManifest(
                Path.Combine(plugin1SubDir, "manifest.json"),
                "InternalModule",
                "1.0.0"
            );
            Assert.That(internalResult.IsSuccess, Is.True);

            // Scan with StopAtManifest
            var options = new ManifestScanOptions
            {
                StopAtManifest = true,
                ValidateManifests = false,
            };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            // Should only find Plugin1, not InternalModule since we stop at manifest
            Assert.That(results, Has.Count.EqualTo(1));
            var names = results
                .Where(r => r.IsSuccess && r.Manifest != null)
                .Select(r => r.Manifest.GetFirstPackageName())
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(n => n)
                .ToList();
            Assert.That(names, Is.EqualTo(new[] { "Plugin1" }));
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_ExcludesDirectories()
        {
            // Create directories that should be excluded
            var gitDir = Path.Combine(_tempDir, ".git");
            var nodeDir = Path.Combine(_tempDir, "node_modules");
            var srcDir = Path.Combine(_tempDir, "src");

            Directory.CreateDirectory(gitDir);
            Directory.CreateDirectory(nodeDir);
            Directory.CreateDirectory(srcDir);

            // Add manifests to each
            var gitResult = await WriteManifest(
                Path.Combine(gitDir, "manifest.json"),
                "GitManifest",
                "1.0.0"
            );
            var nodeResult = await WriteManifest(
                Path.Combine(nodeDir, "manifest.json"),
                "NodeManifest",
                "1.0.0"
            );
            var srcResult = await WriteManifest(
                Path.Combine(srcDir, "manifest.json"),
                "SrcManifest",
                "1.0.0"
            );
            Assert.Multiple(() =>
            {
                Assert.That(gitResult.IsSuccess, Is.True);
                Assert.That(nodeResult.IsSuccess, Is.True);
                Assert.That(srcResult.IsSuccess, Is.True);
            });

            // Scan with default exclusions and validation disabled
            var options = new ManifestScanOptions { ValidateManifests = false };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            // Should only find the one in src
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Manifest?.GetFirstPackageName(), Is.EqualTo("SrcManifest"));
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_ValidatesManifests()
        {
            // Create invalid manifest (missing required fields)
            var invalidManifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "invalid-manifest",
                packageName: "Invalid",
                packageVersion: "", // Missing version makes it invalid
                packageDescription: "Invalid test manifest"
            );

            var json = JsonSerializer.Serialize(invalidManifest);
            await File.WriteAllTextAsync(Path.Combine(_tempDir, "manifest.json"), json);

            // Scan with validation
            var options = new ManifestScanOptions { ValidateManifests = true };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(results[0].IsSuccess, Is.False);
                Assert.That(results[0].Error, Is.Not.Null);
            });
            Assert.That(results[0].Error.ErrorType, Is.EqualTo(ManifestErrorType.ValidationFailed));
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_RequiresSignature()
        {
            // Create V2 manifest with a signed content block but no signature
            var unsignedBlock = new SignedContentBlock
            {
                KeyId = "sha256:0000000000000000000000000000000000000000000000000000000000000000",
                Signature = "", // Empty signature means unsigned
                PublicKey = ManifestTestHelpers.GetTestPublicKey(),
                Packages = ImmutableDictionary<string, ManifestPackage>.Empty,
                Policies = ImmutableArray<ManifestPolicy>.Empty,
            };

            var unsignedManifest = new Manifest
            {
                ManifestId = "unsigned-manifest",
                Version = "2.0",
                SignedContent = ImmutableArray.Create(unsignedBlock),
            };

            var json = JsonSerializer.Serialize(
                unsignedManifest,
                new JsonSerializerOptions { WriteIndented = true }
            );
            await File.WriteAllTextAsync(Path.Combine(_tempDir, "manifest.json"), json);

            // Scan requiring signatures
            var options = new ManifestScanOptions
            {
                RequireSignature = true,
                ValidateManifests = false,
            };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(results[0].IsSuccess, Is.False);
                Assert.That(results[0].Error, Is.Not.Null);
                Assert.That(results[0].Error?.ErrorType, Is.EqualTo(ManifestErrorType.NotSigned));
            });
        }

        [Category("Manifest.Integration")]
        [Test]
        public async Task ScanDirectory_HandlesComplexHierarchy()
        {
            // Create a realistic project structure
            var structure = new[]
            {
                "game/manifest.json",
                "game/engine/manifest.json",
                "game/plugins/official/auth/manifest.json",
                "game/plugins/official/combat/manifest.json",
                "game/plugins/community/theme1/manifest.json",
                "game/plugins/community/theme2/manifest.json",
                "game/data/configs/manifest.json",
                "tools/manifest.json",
                "tests/manifest.json",
            };

            var index = 0;
            foreach (var relativePath in structure)
            {
                var fullPath = Path.Combine(_tempDir, relativePath);
                var dir = Path.GetDirectoryName(fullPath);
                Directory.CreateDirectory(dir);

                var manifest = CreateTestManifest($"Component{index}", $"{index}.0.0");
                var json = JsonSerializer.Serialize(manifest);
                await File.WriteAllTextAsync(fullPath, json);
                index++;
            }

            // Scan entire hierarchy without validation to test scanning logic
            var options = new ManifestScanOptions
            {
                ValidateManifests = false,
                RootPath = _tempDir,
            };
            var results = await _scanner.ScanDirectoryAsync(_tempDir, options);

            Assert.That(results, Has.Count.EqualTo(structure.Length));

            // Check relative paths are correct (normalize case for comparison)
            var relativePaths = results
                .Where(r => r.RelativePath != null)
                .Select(r => r.RelativePath.Replace('\\', '/').ToLowerInvariant())
                .OrderBy(p => p)
                .ToList();

            var expectedPaths = structure.Select(p => p.ToLowerInvariant()).OrderBy(p => p);
            Assert.That(relativePaths, Is.EqualTo(expectedPaths));
        }

        [Category("Manifest.Integration")]
        [Test]
        public void ScanDirectory_SynchronousVersion()
        {
            // Create manifest
            var manifest = CreateTestManifest("SyncTest", "1.0.0");
            var json = JsonSerializer.Serialize(manifest);
            File.WriteAllText(Path.Combine(_tempDir, "manifest.json"), json);

            // Use synchronous scan with validation disabled to match the async test expectations
            var options = new ManifestScanOptions { ValidateManifests = false };
            var results = _scanner.ScanDirectory(_tempDir, options);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(results[0].IsSuccess, Is.True);
                Assert.That(results[0].Manifest.GetFirstPackageName(), Is.EqualTo("SyncTest"));
            });
        }

        // Helper methods

        private static Manifest CreateTestManifest(string name, string version)
        {
            return ManifestTestHelpers.CreateV2Manifest(
                manifestId: $"{name.ToLower()}-manifest",
                packageName: name,
                packageVersion: version,
                packageDescription: $"Test manifest for {name}"
            );
        }

        private async Task<Result<string>> WriteManifest(string path, string name, string version)
        {
            try
            {
                var manifest = CreateTestManifest(name, version);
                var json = JsonSerializer.Serialize(manifest);
                await File.WriteAllTextAsync(path, json);
                return Result.Success(path);
            }
            catch (Exception ex)
            {
                return Result.Failure<string>($"Failed to write manifest to {path}: {ex.Message}");
            }
        }
    }
}
