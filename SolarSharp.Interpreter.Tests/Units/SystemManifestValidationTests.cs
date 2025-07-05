using System;
using System.Linq;
using NUnit.Framework;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for validating the built-in SystemManifest configurations.
    /// </summary>
    /// <remarks>
    /// SystemManifests are pre-configured security manifests that provide standard
    /// security levels for common use cases. This test suite ensures that:
    /// 
    /// 1. All built-in SystemManifests are valid and well-formed
    /// 2. Each manifest has appropriate settings for its intended use
    /// 3. The validation system correctly identifies invalid manifests
    /// 4. Manifest promotion from regular to system manifests works correctly
    /// 
    /// SystemManifest Types:
    /// - None: No restrictions (dangerous, for testing only)
    /// - Unrestricted: Minimal restrictions (legacy compatibility)
    /// - Desktop: PHP-like limits for development (60s, 128MB)
    /// - Jailed: Maximum security for untrusted code (5s, 10MB)
    /// - Game: Optimized for game scripts (300ms, 25MB, chroot)
    /// 
    /// These tests are critical for ensuring the security defaults are correct
    /// and that the manifest system works as expected out of the box.
    /// 
    /// Test isolation: Parallelizable - uses static data validation
    /// Dependencies: None
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    public class SystemManifestValidationTests
    {
        /// <summary>
        /// Validates that all built-in SystemManifests pass validation checks.
        /// </summary>
        /// <remarks>
        /// This test uses reflection to find all SystemManifest static properties
        /// and validates each one. This ensures that:
        /// - No SystemManifest is accidentally broken during development
        /// - All required fields are present and valid
        /// - The manifests can be safely used by applications
        /// 
        /// If this test fails, it indicates a serious issue with the built-in
        /// security configurations that must be fixed immediately.
        /// </remarks>
        [Test]
        public void AllSystemManifests_ShouldBeValid()
        {
            // Get validation results for all system manifests
            var results = ManifestValidator.ValidateAllSystemManifests();
            
            // Assert that we found some manifests to test
            Assert.That(results.Count, Is.GreaterThan(0), "No system manifests found to validate");
            
            // Check each manifest
            foreach (var kvp in results)
            {
                var manifestName = kvp.Key;
                var validation = kvp.Value;
                
                Assert.That(validation.IsValid, Is.True, 
                    $"SystemManifest.{manifestName} is invalid: {string.Join(", ", validation.Errors)}");
            }
        }
        
        /// <summary>
        /// Validates that SystemManifest.None is properly configured.
        /// </summary>
        /// <remarks>
        /// The None manifest represents no security restrictions. While dangerous,
        /// it's useful for:
        /// - Testing and development
        /// - Fully trusted environments
        /// - Backward compatibility
        /// 
        /// Even though it has no restrictions, it must still be a valid manifest
        /// structure to prevent errors when used.
        /// </remarks>
        [Test]
        public void SystemManifest_None_ShouldBeValid()
        {
            var manifest = SystemManifest.None;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.None is invalid: {validation}");
        }
        
        /// <summary>
        /// Validates that SystemManifest.Unrestricted is properly configured.
        /// </summary>
        /// <remarks>
        /// The Unrestricted manifest provides minimal security for legacy compatibility.
        /// It should:
        /// - Have very high or no limits
        /// - Allow most operations
        /// - Still maintain basic manifest structure
        /// 
        /// This is a step up from None but still not recommended for untrusted code.
        /// </remarks>
        [Test]
        public void SystemManifest_Unrestricted_ShouldBeValid()
        {
            var manifest = SystemManifest.Unrestricted;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Unrestricted is invalid: {validation}");
        }
        
        /// <summary>
        /// Validates that SystemManifest.Desktop is properly configured.
        /// </summary>
        /// <remarks>
        /// The Desktop manifest is designed for development environments and follows
        /// PHP-like resource limits:
        /// - 60 second timeout (standard web request timeout)
        /// - 128MB memory limit (typical PHP memory_limit)
        /// - No chroot (for development convenience)
        /// 
        /// This provides reasonable protection while remaining developer-friendly.
        /// </remarks>
        [Test]
        public void SystemManifest_Desktop_ShouldBeValid()
        {
            var manifest = SystemManifest.Desktop;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Desktop is invalid: {validation}");
        }
        
        /// <summary>
        /// Validates that SystemManifest.Jailed is properly configured.
        /// </summary>
        /// <remarks>
        /// The Jailed manifest provides maximum security for running untrusted code:
        /// - Short timeout (5 seconds)
        /// - Low memory limit (10MB)
        /// - No file or directory access
        /// - Minimal capabilities
        /// 
        /// This is the recommended setting for user-provided scripts or any
        /// situation where security is paramount.
        /// </remarks>
        [Test]
        public void SystemManifest_Jailed_ShouldBeValid()
        {
            var manifest = SystemManifest.Jailed;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Jailed is invalid: {validation}");
        }
        
        /// <summary>
        /// Validates that SystemManifest.Game is properly configured.
        /// </summary>
        /// <remarks>
        /// The Game manifest is optimized for game scripting scenarios:
        /// - Very short timeout (300ms) for frame-based execution
        /// - Moderate memory (25MB) for game logic
        /// - Chroot enabled for additional isolation
        /// - Limited to game-appropriate modules
        /// 
        /// This balances performance requirements with security for game mods
        /// and user-generated content.
        /// </remarks>
        [Test]
        public void SystemManifest_Game_ShouldBeValid()
        {
            var manifest = SystemManifest.Game;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Game is invalid: {validation}");
        }
        
        /// <summary>
        /// Verifies that Desktop manifest has the expected PHP-like configuration.
        /// </summary>
        /// <remarks>
        /// This test ensures the Desktop manifest maintains its documented behavior
        /// of providing PHP-like limits. These specific values are important for:
        /// - Compatibility with web development practices
        /// - Predictable behavior for developers
        /// - Adequate resources for development tasks
        /// </remarks>
        [Test]
        public void SystemManifest_Desktop_ShouldHaveExpectedProperties()
        {
            var manifest = SystemManifest.Desktop;
            Assert.Multiple(() =>
            {

                // Desktop should have PHP-like limits
                Assert.That(manifest.Policy.TimeoutMs, Is.EqualTo(60000), "Desktop should have 60s timeout");
                Assert.That(manifest.Policy.MaxMemoryMB, Is.EqualTo(128), "Desktop should have 128MB memory limit");
                Assert.That(manifest.Policy.EnableChroot, Is.False, "Desktop should not use chroot");
            });
        }
        
        /// <summary>
        /// Verifies that Game manifest has the expected performance-focused configuration.
        /// </summary>
        /// <remarks>
        /// The Game manifest's specific limits are tuned for real-time game execution:
        /// - 300ms timeout allows script to complete within a few game frames
        /// - 25MB is enough for game logic without allowing DoS
        /// - Chroot provides additional isolation for untrusted game mods
        /// 
        /// These values are based on typical game engine requirements.
        /// </remarks>
        [Test]
        public void SystemManifest_Game_ShouldHaveExpectedProperties()
        {
            var manifest = SystemManifest.Game;
            Assert.Multiple(() =>
            {

                // Game should have very restrictive limits
                Assert.That(manifest.Policy.TimeoutMs, Is.EqualTo(300), "Game should have 300ms timeout");
                Assert.That(manifest.Policy.MaxMemoryMB, Is.EqualTo(25), "Game should have 25MB memory limit");
                Assert.That(manifest.Policy.EnableChroot, Is.True, "Game should use chroot");
            });
        }
        
        /// <summary>
        /// Verifies that Jailed manifest enforces maximum security restrictions.
        /// </summary>
        /// <remarks>
        /// The Jailed manifest should be the most restrictive configuration:
        /// - 5 second timeout prevents long-running scripts
        /// - 10MB memory prevents resource exhaustion
        /// - No file/directory access prevents data exfiltration
        /// - Minimal modules reduce attack surface
        /// 
        /// Any relaxation of these limits could compromise security for
        /// untrusted code execution scenarios.
        /// </remarks>
        [Test]
        public void SystemManifest_Jailed_ShouldHaveExpectedProperties()
        {
            var manifest = SystemManifest.Jailed;
            Assert.Multiple(() =>
            {

                // Jailed should be maximum security
                Assert.That(manifest.Policy.TimeoutMs, Is.EqualTo(5000), "Jailed should have 5s timeout");
                Assert.That(manifest.Policy.MaxMemoryMB, Is.EqualTo(10), "Jailed should have 10MB memory limit");
                Assert.That(manifest.Policy.DefaultFileAccess, Is.EqualTo("none"), "Jailed should deny file access");
                Assert.That(manifest.Policy.DefaultDirectoryAccess, Is.EqualTo("none"), "Jailed should deny directory access");
            });
        }
        
        /// <summary>
        /// Tests that the validator properly rejects null manifests.
        /// </summary>
        /// <remarks>
        /// Null manifest validation is a basic safety check to prevent
        /// NullReferenceExceptions in the validation logic. The validator
        /// should return a clear error message rather than crashing.
        /// </remarks>
        [Test]
        public void ManifestValidator_ShouldRejectNullManifest()
        {
            var validation = ManifestValidator.ValidateSystemManifest(null);
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors, Contains.Item("Manifest cannot be null"));
            });
        }
        
        /// <summary>
        /// Tests that SystemManifests must have a policy section.
        /// </summary>
        /// <remarks>
        /// SystemManifests require a policy section because they define
        /// security constraints. A manifest without a policy would have
        /// undefined behavior and could be dangerous. This test ensures
        /// the validator catches this critical omission.
        /// </remarks>
        [Test]
        public void ManifestValidator_ShouldRejectManifestWithoutPolicy()
        {
            var manifest = new Manifest
            {
                Policy = null
            };
            
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors, Contains.Item("Policy must be specified for SystemManifest"));
            });
        }
        
        /// <summary>
        /// Tests that SystemManifest policies must have all required fields.
        /// </summary>
        /// <remarks>
        /// A complete policy must specify:
        /// - TimeoutMs: Execution time limit
        /// - MaxMemoryMB: Memory usage limit
        /// - DefaultFileAccess: File system access policy
        /// - Other security constraints
        /// 
        /// Missing any of these could lead to undefined or insecure behavior.
        /// The validator must catch all missing required fields.
        /// </remarks>
        [Test]
        public void ManifestValidator_ShouldRejectIncompletePolicy()
        {
            var manifest = new Manifest
            {
                Policy = new ManifestPolicy
                {
                    // Missing required fields
                }
            };
            
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors.Any(e => e.Contains("TimeoutMs")), Is.True);
                Assert.That(validation.Errors.Any(e => e.Contains("MaxMemoryMB")), Is.True);
                Assert.That(validation.Errors.Any(e => e.Contains("DefaultFileAccess")), Is.True);
            });
        }
        
        /// <summary>
        /// Tests that file access values must be from the valid set of options.
        /// </summary>
        /// <remarks>
        /// File access values must be one of:
        /// - "none": No access allowed
        /// - "read": Read-only access
        /// - "write": Write-only access
        /// - "readwrite": Full read/write access
        /// - "sandboxedreadwrite": Read/write within sandbox
        /// 
        /// Invalid values like "invalid_value" could cause security bypasses
        /// if not properly validated. This test ensures only valid values
        /// are accepted.
        /// </remarks>
        [Test]
        public void ManifestValidator_ShouldRejectInvalidFileAccessValues()
        {
            var manifest = new Manifest
            {
                Policy = new ManifestPolicy
                {
                    TimeoutMs = 1000,
                    MaxMemoryMB = 10,
                    MaxInstructions = 1000000,
                    DefaultFileAccess = "invalid_value",
                    DefaultDirectoryAccess = "none",
                    AntiPolymorphism = true
                }
            };
            
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Errors.Any(e => e.Contains("DefaultFileAccess") && e.Contains("invalid_value")), Is.True);
            });
        }
        
        /// <summary>
        /// Tests promotion of regular manifests to SystemManifests.
        /// </summary>
        /// <remarks>
        /// SystemManifest.FromManifest() allows creating a SystemManifest from
        /// a regular Manifest object. This is useful for:
        /// - Testing custom configurations
        /// - Creating new standard configurations
        /// - Runtime manifest generation
        /// 
        /// The promotion should preserve all settings while adding the
        /// SystemManifest type safety and validation.
        /// </remarks>
        [Test]
        public void SystemManifest_FromManifest_ShouldPromoteValidManifest()
        {
            var validManifest = new Manifest
            {
                Policy = new ManifestPolicy
                {
                    TimeoutMs = 5000,
                    MaxMemoryMB = 50,
                    MaxInstructions = 1000000,
                    DefaultFileAccess = "read",
                    DefaultDirectoryAccess = "list",
                    AntiPolymorphism = true
                }
            };
            
            var systemManifest = SystemManifest.FromManifest(validManifest);
            
            Assert.That(systemManifest, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(systemManifest.Policy.TimeoutMs, Is.EqualTo(5000));
                Assert.That(systemManifest.Policy.DefaultFileAccess, Is.EqualTo("read"));
            });
        }
        
        /// <summary>
        /// Tests that invalid manifests cannot be promoted to SystemManifests.
        /// </summary>
        /// <remarks>
        /// SystemManifest.FromManifest() must validate the input manifest
        /// before promotion. This prevents invalid configurations from
        /// being used as system-level defaults. The method should throw
        /// ArgumentException with details about what validation failed.
        /// </remarks>
        [Test]
        public void SystemManifest_FromManifest_ShouldRejectInvalidManifest()
        {
            var invalidManifest = new Manifest
            {
                Policy = new ManifestPolicy
                {
                    // Missing required TimeoutMs
                    MaxMemoryMB = 50,
                    DefaultFileAccess = "read",
                    DefaultDirectoryAccess = "list"
                }
            };
            
            Assert.Throws<ArgumentException>(() => SystemManifest.FromManifest(invalidManifest));
        }
        
        /// <summary>
        /// Verifies that all SystemManifests have unique descriptions.
        /// </summary>
        /// <remarks>
        /// Each SystemManifest should have a unique description to:
        /// - Help developers choose the right configuration
        /// - Provide clear documentation
        /// - Avoid confusion between similar manifests
        /// 
        /// Duplicate descriptions would indicate a copy-paste error or
        /// unclear differentiation between security levels.
        /// </remarks>
        [Test]
        public void SystemManifests_ShouldHaveUniqueDescriptions()
        {
            var manifests = new[]
            {
                SystemManifest.None,
                SystemManifest.Unrestricted,
                SystemManifest.Desktop,
                SystemManifest.Jailed,
                SystemManifest.Game
            };
            
            var descriptions = manifests.Select(m => m.Description).ToList();
            var uniqueDescriptions = descriptions.Distinct().ToList();
            
            Assert.That(uniqueDescriptions.Count, Is.EqualTo(descriptions.Count), 
                "All system manifests should have unique descriptions");
        }
    }
}