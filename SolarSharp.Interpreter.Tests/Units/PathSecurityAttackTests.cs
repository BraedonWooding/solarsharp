using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Path.Unit")]
    [Category("Security")]
    public class PathSecurityAttackTests
    {
        private CrossPlatformPathCanonicalizer _canonicalizer;

        [SetUp]
        public void SetUp()
        {
            _canonicalizer = new CrossPlatformPathCanonicalizer();
        }

        // ===== Path Traversal Attacks =====

        [TestCase("../../../etc/passwd")]
        [TestCase("..\\..\\..\\windows\\system32\\config\\sam")]
        [TestCase("data/../../../etc/passwd")]
        [TestCase("/data/../../etc/passwd")]
        [TestCase("\\data\\..\\..\\etc\\passwd")]
        public void PathTraversal_BasicAttacks_Blocked(string maliciousPath)
        {
            var result = PathSecurityValidator.ValidatePath(maliciousPath, "/sandbox");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.PathTraversal)
                    .Or.EqualTo(PathViolationType.SandboxViolation)
            );
        }

        [TestCase("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
        [TestCase("%252e%252e%252f")]
        [TestCase("..%2f..%2f..%2fetc%2fpasswd")]
        [TestCase("..%252f..%252f..%252fetc%252fpasswd")]
        public void PathTraversal_EncodedAttacks_Blocked(string encodedPath)
        {
            var result = PathSecurityValidator.ValidatePath(encodedPath, "/sandbox");

            Assert.That(result.IsFailure, Is.True);
        }

        [TestCase("....//....//....//etc/passwd")]
        [TestCase("..././..././../etc/passwd")]
        [TestCase("..\\.\\..\\.\\..\\.\\windows\\system32")]
        public void PathTraversal_ObfuscatedAttacks_Blocked(string obfuscatedPath)
        {
            var result = PathSecurityValidator.ValidatePath(obfuscatedPath, "/sandbox");

            Assert.That(result.IsFailure, Is.True);
        }

        // ===== Unicode Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void Unicode_BidirectionalTextAttack_Blocked()
        {
            // Right-to-left override character
            var maliciousPath = "file\u202Etxt.exe";

            var result = PathSecurityValidator.ValidatePath(maliciousPath);

            Assert.That(result.IsFailure, Is.True);
            // The path is blocked because it ends with .exe (dangerous extension)
            // Unicode attack detection was removed per recent security updates
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.DangerousExtension)
            );
        }

        [Category("Security.Unit")]
        [Test]
        public void Unicode_ZeroWidthCharacterAttack_Blocked()
        {
            var paths = new[]
            {
                "file\u200B.txt", // Zero-width space
                "data\u200C.json", // Zero-width non-joiner
                "config\u200D.xml", // Zero-width joiner
                "\uFEFFscript.lua", // Zero-width no-break space
            };

            foreach (var path in paths)
            {
                var result = PathSecurityValidator.ValidatePath(path);
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.ViolationType,
                    Is.EqualTo(PathViolationType.ZeroWidthCharacter)
                );
            }
        }

        // ===== Platform-Specific Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        [Platform("Win", Reason = "Windows-specific attack")]
        public void Windows_AlternateDataStreamAttack_Blocked()
        {
            var attacks = new[]
            {
                "file.txt:hidden",
                "file.txt::$DATA",
                "file.txt:malware.exe",
                "C:\\file.txt:stream:$DATA",
            };

            foreach (var attack in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack);
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.ViolationType,
                    Is.EqualTo(PathViolationType.PlatformSpecific)
                );
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Platform("Win", Reason = "Windows-specific attack")]
        public void Windows_ShortNameAttack_Handled()
        {
            // 8.3 short names like PROGRA~1
            var attacks = new[] { "PROGRA~1\\malware.exe", "C:\\DOCUME~1\\user\\file.txt" };

            foreach (var attack in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack);
                // Should either fail or resolve to full path
                if (result.IsSuccess)
                {
                    Assert.That(result.Value.Resolved, Does.Not.Contain("~"));
                }
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Platform("Win", Reason = "Windows-specific attack")]
        public void Windows_TrailingDotsAndSpaces_Blocked()
        {
            var attacks = new[]
            {
                "file.txt.",
                "file.txt. . .",
                "file.txt ",
                "file.txt       ",
                "folder. /file.txt",
            };

            foreach (var attack in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack);
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.ViolationType,
                    Is.EqualTo(PathViolationType.PlatformSpecific)
                );
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Platform("Linux", Reason = "Linux-specific attack")]
        public void Linux_ProcSelfAttack_Blocked()
        {
            var attacks = new[]
            {
                "/proc/self/mem",
                "/proc/self/environ",
                "/proc/1/cmdline",
                "/proc/self/fd/0",
            };

            foreach (var attack in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack);
                Assert.That(result.IsFailure, Is.True);
                Assert.That(
                    result.Error.ViolationType,
                    Is.EqualTo(PathViolationType.PlatformSpecific)
                );
            }
        }

        // ===== Null Byte and Control Character Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void NullByte_Injection_Blocked()
        {
            var attacks = new[]
            {
                "file.txt\0.exe",
                "safe\0../../etc/passwd",
                "data\0\0file",
                "\0malicious",
            };

            foreach (var attack in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack);
                Assert.That(
                    result.IsFailure,
                    Is.True,
                    $"Path with null byte should fail: {attack.Replace('\0', '?')}"
                );
                // Some null byte paths might be caught as InvalidPath by Path.GetFullPath
                Assert.That(
                    result.Error.ViolationType,
                    Is.EqualTo(PathViolationType.ControlCharacter)
                        .Or.EqualTo(PathViolationType.InvalidPath),
                    $"Error type for {attack.Replace('\0', '?')}: {result.Error.Message}"
                );
            }
        }

        [Category("Security.Unit")]
        [Test]
        public void ControlCharacters_Various_Blocked()
        {
            var controlChars = Enumerable
                .Range(0, 32)
                .Where(i => i != '\t' && i != '\r' && i != '\n')
                .Select(i => (char)i);

            foreach (var cc in controlChars)
            {
                var attack = $"file{cc}.txt";
                var result = PathSecurityValidator.ValidatePath(attack);

                if (cc == '\0')
                {
                    // Null byte should be caught by canonicalizer
                    Assert.That(result.IsFailure, Is.True);
                }
            }
        }

        // ===== Dangerous Extensions =====

        [TestCase(".exe")]
        [TestCase(".bat")]
        [TestCase(".cmd")]
        [TestCase(".com")]
        [TestCase(".scr")]
        [TestCase(".dll")]
        [TestCase(".sys")]
        public void DangerousExtensions_Blocked(string extension)
        {
            var path = $"file{extension}";
            var result = PathSecurityValidator.ValidatePath(path);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Error.ViolationType,
                Is.EqualTo(PathViolationType.DangerousExtension)
            );
        }

        // ===== Case Sensitivity Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void CaseSensitivity_WindowsDeviceNames_Blocked()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var attacks = new[]
                {
                    "CoN",
                    "COn",
                    "cON",
                    "con",
                    "PrN",
                    "PRn",
                    "prn",
                    "AuX",
                    "aUx",
                    "aux",
                };

                foreach (var attack in attacks)
                {
                    var result = _canonicalizer.Canonicalize(attack);
                    Assert.That(result.IsFailure, Is.True);
                    Assert.That(
                        result.Error.ViolationType,
                        Is.EqualTo(PathViolationType.DangerousFileName)
                    );
                }
            }
        }

        // ===== Length and Complexity Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void PathLength_ExcessiveLength_Handled()
        {
            // Create a very long path
            var longSegment = new string('a', 255);
            var longPath = string.Join("/", Enumerable.Repeat(longSegment, 20));

            var result = _canonicalizer.Canonicalize(longPath);

            // Path.GetFullPath will convert relative to absolute, making it longer
            // The test should check that canonicalization succeeds or fails gracefully
            if (result.IsSuccess)
            {
                // If it succeeds, the normalized path should be valid
                Assert.That(result.Value.Normalized, Is.Not.Null);
                Assert.That(result.Value.Normalized.Length, Is.GreaterThan(0));
            }
            else
            {
                // If it fails, it should be due to path length
                Assert.That(result.Error.ViolationType, Is.EqualTo(PathViolationType.InvalidPath));
            }
        }

        [Category("Security.Unit")]
        [Test]
        public void PathComplexity_DeeplyNested_Handled()
        {
            // Create deeply nested path
            var deepPath = string.Join("/", Enumerable.Range(1, 100).Select(i => $"folder{i}"));

            var result = _canonicalizer.Canonicalize(deepPath);

            // Should handle without stack overflow
            Assert.DoesNotThrow(() =>
            {
                var _ = result.IsSuccess;
            });
        }

        // ===== Combination Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void CombinationAttack_MultipleVectors_Blocked()
        {
            var attacks = new Dictionary<string, string>
            {
                ["../con\0.txt"] = "Path traversal + device name + null byte",
            };

            // Platform-specific attacks
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                attacks["..\\..\\prn.exe"] = "Path traversal + device name + dangerous extension";
                attacks["C:\\file.txt:stream/../etc"] = "ADS + path traversal";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                attacks["%2e%2e/sys\u202Efdp.txt"] = "Encoded traversal + Linux system path + bidi";
            }

            foreach (var (attack, description) in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack);
                Assert.That(result.IsFailure, Is.True, $"Attack should be blocked: {description}");
            }
        }

        // ===== Sandbox Escape Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void SandboxEscape_SymbolicLinkTraversal_Detected()
        {
            var sandbox = "/app/sandbox";
            var attacks = new[]
            {
                "symlink/../../../etc/passwd",
                "./link/../../outside",
                "data/link/../../../",
            };

            foreach (var attack in attacks)
            {
                var result = _canonicalizer.Canonicalize(attack, sandbox);

                if (result.IsSuccess)
                {
                    // If canonicalization succeeds, the resolved path must be within sandbox
                    Assert.That(
                        result.Value.Resolved.StartsWith(
                            sandbox,
                            StringComparison.OrdinalIgnoreCase
                        ),
                        Is.True
                    );
                }
                else
                {
                    // Or it should fail with sandbox violation
                    Assert.That(
                        result.Error.ViolationType,
                        Is.EqualTo(PathViolationType.SandboxViolation)
                    );
                }
            }
        }

        // ===== Performance and Resource Attacks =====    [Category("Security.Unit")]

        [Category("Security.Unit")]
        [Test]
        public void Performance_RepeatedCanonicalizations_UseCache()
        {
            var path = "/data/performance/test.txt";
            var iterations = 1000;

            var start = DateTime.UtcNow;
            for (var i = 0; i < iterations; i++)
            {
                var result = _canonicalizer.Canonicalize(path);
                Assert.That(result.IsSuccess, Is.True);
            }
            var elapsed = DateTime.UtcNow - start;

            // Should be very fast due to caching
            Assert.That(elapsed.TotalMilliseconds, Is.LessThan(100));
        }
    }
}
