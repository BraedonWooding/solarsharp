using System;
using System.Linq;
using NUnit.Framework;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class SystemManifestValidationTests
    {
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
        
        [Test]
        public void SystemManifest_None_ShouldBeValid()
        {
            var manifest = SystemManifest.None;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.None is invalid: {validation}");
        }
        
        [Test]
        public void SystemManifest_Unrestricted_ShouldBeValid()
        {
            var manifest = SystemManifest.Unrestricted;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Unrestricted is invalid: {validation}");
        }
        
        [Test]
        public void SystemManifest_Desktop_ShouldBeValid()
        {
            var manifest = SystemManifest.Desktop;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Desktop is invalid: {validation}");
        }
        
        [Test]
        public void SystemManifest_Jailed_ShouldBeValid()
        {
            var manifest = SystemManifest.Jailed;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Jailed is invalid: {validation}");
        }
        
        [Test]
        public void SystemManifest_Game_ShouldBeValid()
        {
            var manifest = SystemManifest.Game;
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.True, 
                $"SystemManifest.Game is invalid: {validation}");
        }
        
        [Test]
        public void SystemManifest_Desktop_ShouldHaveExpectedProperties()
        {
            var manifest = SystemManifest.Desktop;
            
            // Desktop should have PHP-like limits
            Assert.That(manifest.Policy.TimeoutMs, Is.EqualTo(60000), "Desktop should have 60s timeout");
            Assert.That(manifest.Policy.MaxMemoryMB, Is.EqualTo(128), "Desktop should have 128MB memory limit");
            Assert.That(manifest.Policy.EnableChroot, Is.False, "Desktop should not use chroot");
        }
        
        [Test]
        public void SystemManifest_Game_ShouldHaveExpectedProperties()
        {
            var manifest = SystemManifest.Game;
            
            // Game should have very restrictive limits
            Assert.That(manifest.Policy.TimeoutMs, Is.EqualTo(300), "Game should have 300ms timeout");
            Assert.That(manifest.Policy.MaxMemoryMB, Is.EqualTo(25), "Game should have 25MB memory limit");
            Assert.That(manifest.Policy.EnableChroot, Is.True, "Game should use chroot");
        }
        
        [Test]
        public void SystemManifest_Jailed_ShouldHaveExpectedProperties()
        {
            var manifest = SystemManifest.Jailed;
            
            // Jailed should be maximum security
            Assert.That(manifest.Policy.TimeoutMs, Is.EqualTo(5000), "Jailed should have 5s timeout");
            Assert.That(manifest.Policy.MaxMemoryMB, Is.EqualTo(10), "Jailed should have 10MB memory limit");
            Assert.That(manifest.Policy.DefaultFileAccess, Is.EqualTo("none"), "Jailed should deny file access");
            Assert.That(manifest.Policy.DefaultDirectoryAccess, Is.EqualTo("none"), "Jailed should deny directory access");
        }
        
        [Test]
        public void ManifestValidator_ShouldRejectNullManifest()
        {
            var validation = ManifestValidator.ValidateSystemManifest(null);
            
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Contains.Item("Manifest cannot be null"));
        }
        
        [Test]
        public void ManifestValidator_ShouldRejectManifestWithoutPolicy()
        {
            var manifest = new Manifest
            {
                Policy = null
            };
            
            var validation = ManifestValidator.ValidateSystemManifest(manifest);
            
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors, Contains.Item("Policy must be specified for SystemManifest"));
        }
        
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
            
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(e => e.Contains("TimeoutMs")), Is.True);
            Assert.That(validation.Errors.Any(e => e.Contains("MaxMemoryMB")), Is.True);
            Assert.That(validation.Errors.Any(e => e.Contains("DefaultFileAccess")), Is.True);
        }
        
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
            
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(e => e.Contains("DefaultFileAccess") && e.Contains("invalid_value")), Is.True);
        }
        
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
            Assert.That(systemManifest.Policy.TimeoutMs, Is.EqualTo(5000));
            Assert.That(systemManifest.Policy.DefaultFileAccess, Is.EqualTo("read"));
        }
        
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