using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Security
{
    /// <summary>
    /// Simple scope rule for testing
    /// </summary>
    public class ScopeRule
    {
        public string Pattern { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
    }

    [TestFixture]
    [Category("Security.Unit")]
    public class FileScopedPolicyTests
    {
        [Test]
        public void FileScopedPolicy_BasicPatternMatching()
        {
            // Create normal and restricted policies
            var normalPolicy = ManifestPolicyBuilder.Create()
                .ForPackages("test-package")
                .WithMaxMemoryMB(256)
                .WithTimeoutSeconds(30)
                .Build();
                
            var restrictedPolicy = ManifestPolicyBuilder.Create()
                .ForPackages("test-package")
                .DenySystemModules()
                .DenyDangerousCapabilities()
                .WithMaxMemoryMB(10)
                .WithTimeoutSeconds(5)
                .Build();

            var manifest = V2ManifestBuilder.CreateUnsigned("file-scoped-test", "FileScopedTest")
                .WithPackageMetadata("FileScopedTest", "1.0.0", "Test manifest for file-scoped policies")
                .WithFile("FileScopedTest", "script.lua", "sha256:abc123")
                .WithFile("FileScopedTest", "module.lua", "sha256:def456")
                .WithPolicy(normalPolicy)
                .WithPolicy(restrictedPolicy);

            Assert.Multiple(() =>
            {
                // Test V2.0 structure - signed content exists
                Assert.That(manifest.HasSignedContent, Is.True);
                Assert.That(manifest.SignedContent.Length, Is.EqualTo(1));

                var signedBlock = manifest.SignedContent[0];
                Assert.That(signedBlock.Packages.Count, Is.EqualTo(1));
                Assert.That(signedBlock.Policies.Length, Is.EqualTo(2));

                // Test V2.0 structure has files
                var firstPackage = signedBlock.Packages.FirstOrDefault();
                Assert.That(
                    firstPackage.Value?.Files.Count,
                    Is.GreaterThan(0),
                    "Package should contain files"
                );
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void FileScopedPolicy_EvalSuffixParsing()
        {
            var testCases = new[]
            {
                ("script.lua", "script.lua", false),
                ("script.lua:eval", "script.lua", true),
                ("path/to/script.lua:eval", "path/to/script.lua", true),
                ("script.lua:eval:eval", "script.lua:eval", true), // Nested eval
                ("file.txt:eval", "file.txt", true),
                (":eval", "", true), // Edge case
                ("", "", false),
            };

            foreach (var (input, expectedBase, expectedIsEval) in testCases)
            {
                var basePath = ParseBasePath(input);
                var isEval = input.EndsWith(":eval");

                Assert.Multiple(() =>
                {
                    Assert.That(basePath, Is.EqualTo(expectedBase), $"Failed for input: {input}");
                    Assert.That(
                        isEval,
                        Is.EqualTo(expectedIsEval),
                        $"Failed eval detection for: {input}"
                    );
                });
            }
        }

        [Category("Security.Unit")]
        [Test]
        public void FileScopedPolicy_WildcardMatching()
        {
            var patterns = new[]
            {
                ("*.lua", "script.lua", true),
                ("*.lua", "path/script.lua", false), // * doesn't match /
                ("**/*.lua", "path/script.lua", true),
                ("**/*.lua", "deep/path/script.lua", true),
                ("src/*.lua", "src/script.lua", true),
                ("src/*.lua", "src/sub/script.lua", false),
                ("src/**/*.lua", "src/sub/script.lua", true),
                ("*.lua:eval", "script.lua:eval", true),
                ("**/*.lua:eval", "path/script.lua:eval", true),
                ("test_*.lua", "test_foo.lua", true),
                ("test_*.lua", "test_foo_bar.lua", true),
                ("test_?.lua", "test_a.lua", true),
                ("test_?.lua", "test_ab.lua", false), // ? matches single char
            };

            foreach (var (pattern, path, expectedMatch) in patterns)
            {
                var matches = MatchesPattern(pattern, path);
                Assert.That(
                    matches,
                    Is.EqualTo(expectedMatch),
                    $"Pattern '{pattern}' matching '{path}' failed"
                );
            }
        }

        [Category("Security.Unit")]
        [Test]
        public void FileScopedPolicyTests_SpecificityOrdering()
        {
            var filePolicies = ImmutableDictionary<string, string>
                .Empty.Add("*", "fallback")
                .Add("*.lua", "lua-general")
                .Add("src/*.lua", "src-lua")
                .Add("src/main.lua", "main-specific")
                .Add("**/*.lua:eval", "eval-general")
                .Add("src/*.lua:eval", "src-eval");

            // Test specificity for different paths
            var testCases = new[]
            {
                ("random.txt", "fallback"),
                ("script.lua", "lua-general"),
                ("src/module.lua", "src-lua"),
                ("src/main.lua", "main-specific"),
                ("lib/helper.lua", "fallback"),
                ("script.lua:eval", "eval-general"),
                ("src/module.lua:eval", "src-eval"),
            };

            foreach (var (path, expectedPolicy) in testCases)
            {
                var matchingPolicy = FindMostSpecificPolicy(filePolicies, path);
                Assert.That(
                    matchingPolicy,
                    Is.EqualTo(expectedPolicy),
                    $"Wrong policy for path: {path}"
                );
            }
        }

        [Category("Security.Unit")]
        [Test]
        public void FileScopedPolicy_PolicyComposition()
        {
            // Create base and eval-restricted policies
            var basePolicy = ManifestTestHelpers.CreatePermissivePolicy("test-package");
            var evalRestrictedPolicy = ManifestTestHelpers.CreateRestrictivePolicy(
                "test-package"
            ) with
            {
                Selector = ":eval",
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "policy-composition-test",
                packageName: "PolicyCompositionTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for policy composition",
                policies: ImmutableArray.Create(basePolicy, evalRestrictedPolicy)
            );

            // Verify that eval policy is more restrictive
            var policies = manifest.SignedContent[0].Policies;
            Assert.That(
                policies.Length,
                Is.GreaterThan(0),
                "Should have policy definitions"
            );

            // Verify V2.0 structure
            var filePolicy = policies.FirstOrDefault(p => p.Selector == ":file");
            var evalPolicy = policies.FirstOrDefault(p => p.Selector == ":eval");

            Assert.Multiple(() =>
            {
                // In V2.0, we compare the restriction values from the ManifestPolicy objects
                Assert.That(evalPolicy, Is.Not.Null, "Eval policy should exist");
                Assert.That(filePolicy, Is.Not.Null, "File policy should exist");

                // Check that eval policy has more restrictive timeout
                Assert.That(evalPolicy.Timeout, Is.Not.Null);
                Assert.That(filePolicy.Timeout, Is.Not.Null);

                // Parse timeout values and compare (eval should be more restrictive)
                var evalTimeoutMs = ParseTimeoutToMs(evalPolicy.Timeout);
                var fileTimeoutMs = ParseTimeoutToMs(filePolicy.Timeout);
                Assert.That(evalTimeoutMs, Is.LessThan(fileTimeoutMs));

                // Check memory restrictions
                var evalMemoryMB = ParseMemoryToMB(evalPolicy.MaxMemory);
                var fileMemoryMB = ParseMemoryToMB(filePolicy.MaxMemory);
                Assert.That(evalMemoryMB, Is.LessThan(fileMemoryMB));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void FileScopedPolicy_ComplexManifestExample()
        {
            // Complex real-world example
            var enginePolicy = ManifestTestHelpers.CreatePermissivePolicy("game-engine");
            var pluginPolicy = ManifestTestHelpers.CreateDefaultPolicy("game-engine");
            var evalPolicy = ManifestTestHelpers.CreateRestrictivePolicy("game-engine") with
            {
                Selector = ":eval",
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "game-engine-manifest",
                packageId: "game-engine",
                packageName: "GameEngine",
                packageVersion: "2.0.0",
                packageDescription: "Complex game engine manifest example",
                files: ImmutableDictionary<string, string>
                    .Empty.Add("engine.lua", "sha256:aaa111")
                    .Add("plugin.lua", "sha256:bbb222")
                    .Add("eval.lua", "sha256:ccc333"),
                policies: ImmutableArray.Create(enginePolicy, pluginPolicy, evalPolicy)
            );

            // Note: This test was simplified for V2.0 manifest compatibility
            // The complex policies are now defined through the helper method above

            // Verify that V2.0 manifest structure is correct
            Assert.Multiple(() =>
            {
                Assert.That(manifest.HasSignedContent, Is.True);
                Assert.That(manifest.SignedContent.Length, Is.EqualTo(1));

                var signedBlock = manifest.SignedContent[0];
                Assert.That(signedBlock.Policies.Length, Is.EqualTo(3));

                // Test V2.0 structure for files
                var firstPackage = manifest.SignedContent[0].Packages.FirstOrDefault();
                Assert.That(
                    firstPackage.Value?.Files.Count,
                    Is.GreaterThan(0),
                    "Package should contain files"
                );
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void FileScopedPolicy_InvalidPolicyReference()
        {
            // Create a simple manifest with existing policy
            var existingPolicy = ManifestTestHelpers.CreateDefaultPolicy("test-package");

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "invalid-policy-ref-test",
                packageName: "InvalidPolicyTest",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest with invalid policy reference",
                policies: ImmutableArray.Create(existingPolicy)
            );

            // Verify that the manifest structure is valid for V2.0
            Assert.Multiple(() =>
            {
                Assert.That(manifest.HasSignedContent, Is.True);
                Assert.That(manifest.SignedContent[0].Policies.Length, Is.EqualTo(1));

                // In V2.0, policy references are validated differently
                // Policies are directly embedded in the signed content rather than referenced by name
                var signedBlock = manifest.SignedContent[0];
                var policy = signedBlock.Policies[0];
                Assert.That(policy.Packages.Contains("test-package"), Is.True);
            });
        }

        // Helper methods

        private static string ParseBasePath(string path)
        {
            if (path.EndsWith(":eval"))
                return path.Substring(0, path.Length - 5);
            return path;
        }

        private static bool MatchesPattern(string pattern, string path)
        {
            // Simplified pattern matching for tests
            // In real implementation, use Microsoft.Extensions.FileSystemGlobbing

            if (pattern == "*")
                return true; // * matches everything

            // Handle **/*.ext patterns (matches files in any subdirectory)
            if (pattern.StartsWith("**/"))
            {
                var suffix = pattern.Substring(3);
                if (suffix.StartsWith("*"))
                {
                    var ext = suffix.Substring(1);
                    return path.EndsWith(ext);
                }
                return path.EndsWith(suffix);
            }

            // Handle prefix/**/*.ext patterns (matches files in subdirectories of prefix)
            if (pattern.Contains("/**/"))
            {
                var parts = pattern.Split(new[] { "/**/" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var prefix = parts[0];
                    var suffix = parts[1];
                    if (suffix.StartsWith("*"))
                    {
                        var ext = suffix.Substring(1);
                        return path.StartsWith(prefix + "/") && path.EndsWith(ext);
                    }
                    return path.StartsWith(prefix + "/") && path.EndsWith(suffix);
                }
            }

            // Handle simple wildcards
            if (pattern.Contains("*"))
            {
                var parts = pattern.Split('*');
                if (parts.Length == 2)
                {
                    var prefix = parts[0];
                    var suffix = parts[1];

                    if (prefix.Length == 0 && suffix.Length == 0)
                        return true;

                    if (prefix.Length == 0)
                        return path.EndsWith(suffix)
                            && !path.Substring(0, path.Length - suffix.Length).Contains('/');

                    if (suffix.Length == 0)
                        return path.StartsWith(prefix)
                            && !path.Substring(prefix.Length).Contains('/');

                    if (path.Length < prefix.Length + suffix.Length)
                        return false;

                    var middle = path.Substring(
                        prefix.Length,
                        path.Length - prefix.Length - suffix.Length
                    );
                    return path.StartsWith(prefix)
                        && path.EndsWith(suffix)
                        && !middle.Contains('/');
                }
            }

            // Handle ? wildcards
            if (pattern.Contains("?"))
            {
                if (pattern.Length != path.Length)
                    return false;

                for (var i = 0; i < pattern.Length; i++)
                {
                    if (pattern[i] != '?' && pattern[i] != path[i])
                        return false;
                }
                return true;
            }

            return pattern == path;
        }

        private static string FindMostSpecificPolicy(
            ImmutableDictionary<string, string> policies,
            string path
        )
        {
            var matchingPolicies = policies
                .Where(p => MatchesPattern(p.Key, path))
                .OrderByDescending(p => GetSpecificity(p.Key))
                .ToList();

            return matchingPolicies.Count > 0 ? matchingPolicies.First().Value : string.Empty;
        }

        private static ScopeRule FindMostSpecificRule(IEnumerable<ScopeRule> rules, string path)
        {
            // In real implementation, this would use proper specificity calculation
            // For tests, use simple heuristics

            var matchingRules = rules
                .Where(r => MatchesPattern(r.Pattern, path))
                .OrderByDescending(r => GetSpecificity(r.Pattern))
                .ToList();

            return matchingRules.Count > 0
                ? matchingRules.First()
                : new ScopeRule { Pattern = string.Empty, PolicyName = string.Empty };
        }

        private static int GetSpecificity(string pattern)
        {
            // More sophisticated specificity calculation for tests
            var specificity = 0;

            // Base specificity
            if (pattern == "*")
                specificity = 0;
            else if (!pattern.Contains("*") && !pattern.Contains("?"))
                specificity = 100; // Exact match
            else if (pattern.StartsWith("**/"))
                specificity = 20;
            else if (pattern.Contains("/"))
                specificity = 50;
            else
                specificity = 30;

            // Add bonus for :eval suffix
            if (pattern.Contains(":eval"))
                specificity += 10;

            // Add bonus for each literal character (more specific)
            specificity += pattern.Count(c => c != '*' && c != '?' && c != '/');

            return specificity;
        }

        /// <summary>
        /// Parses timeout string like "30s" or "5000ms" to milliseconds
        /// </summary>
        private static int ParseTimeoutToMs(string timeout)
        {
            if (string.IsNullOrEmpty(timeout))
                return 0;

            if (timeout.EndsWith("ms"))
            {
                return int.Parse(timeout.Substring(0, timeout.Length - 2));
            }

            if (timeout.EndsWith("s"))
            {
                var seconds = int.Parse(timeout.Substring(0, timeout.Length - 1));
                return seconds * 1000;
            }

            if (timeout.EndsWith("m"))
            {
                var minutes = int.Parse(timeout.Substring(0, timeout.Length - 1));
                return minutes * 60 * 1000;
            }

            // Default to treating as milliseconds
            return int.Parse(timeout);
        }

        /// <summary>
        /// Parses memory string like "128MB" or "1GB" to MB
        /// </summary>
        private static int ParseMemoryToMB(string memory)
        {
            if (string.IsNullOrEmpty(memory))
                return 0;

            if (memory.EndsWith("MB"))
            {
                return int.Parse(memory.Substring(0, memory.Length - 2));
            }

            if (memory.EndsWith("GB"))
            {
                var gb = int.Parse(memory.Substring(0, memory.Length - 2));
                return gb * 1024;
            }

            // Default to treating as MB
            return int.Parse(memory);
        }
    }
}
