using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;
using Xunit;

namespace WotCI.Tests
{
    public class PluginSecurityTests
    {
        [Fact]
        public void PluginExecution_WithValidManifest_Succeeds()
        {
            // Arrange
            var rootCa = CertificateTestHelpers.GenerateRootCA();
            var partnerCert = CertificateTestHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test");
            
            CertificateTrustStore.ClearTrustedRootCAs();
            CertificateTrustStore.AddTrustedRootCA(rootCa);

            var manifestJson = @"{
                ""version"": ""1.0"",
                ""name"": ""Test Plugin"",
                ""policy"": {
                    ""allowedModules"": [""Basic"", ""String""],
                    ""timeoutMs"": 5000,
                    ""maxMemoryMB"": 10
                }
            }";

            var signedManifest = ManifestSigner.SignManifestJsonWithCertificate(manifestJson, partnerCert);
            var manifest = LuaManifest.ParseManifest(signedManifest, "/plugins/test");

            // Act
            var script = new Script(manifest.Policy.ToSecurityConfiguration());
            var result = script.DoString("return 'Plugin loaded successfully'");

            // Assert
            result.String.Should().Be("Plugin loaded successfully");
        }

        [Fact]
        public void PluginExecution_ExceedsTimeout_Throws()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated()
                .WithTimeout(1) // 1 second timeout
                .AllowModules("basic");

            var script = new Script(config);

            // Act & Assert
            Action action = () => script.DoString(@"
                while true do
                    -- Infinite loop
                end
            ");

            action.Should().Throw<ResourceLimitExceededException>()
                .WithMessage("*limit exceeded*");
        }

        [Fact]
        public void PluginExecution_AccessDeniedModule_Throws()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated()
                .AllowModules("basic", "string"); // No 'io' module

            var script = new Script(config);

            // Act & Assert
            Action action = () => script.DoString(@"
                local f = io.open('test.txt', 'w')
            ");

            action.Should().Throw<ScriptRuntimeException>();
        }

        [Fact]
        public void ManifestValidation_WithTamperedContent_Fails()
        {
            // Arrange
            var rootCa = CertificateTestHelpers.GenerateRootCA();
            var partnerCert = CertificateTestHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test");
            
            CertificateTrustStore.ClearTrustedRootCAs();
            CertificateTrustStore.AddTrustedRootCA(rootCa);

            var manifestJson = @"{
                ""version"": ""1.0"",
                ""name"": ""Test Plugin"",
                ""policy"": {
                    ""allowedModules"": [""Basic""]
                }
            }";

            var signedManifest = ManifestSigner.SignManifestJsonWithCertificate(manifestJson, partnerCert);
            
            // Tamper with the manifest
            var tamperedManifest = signedManifest.Replace("Test Plugin", "Hacked Plugin");

            // Act & Assert
            Action action = () =>
            {
                var manifest = LuaManifest.ParseManifest(tamperedManifest, "/plugins/test");
                ManifestAutoLoader.VerifyManifestSignature(manifest);
            };

            action.Should().Throw<ManifestSignatureException>();
        }

        [Fact]
        public void CertificatePathConstraint_EnforcesPluginBoundaries()
        {
            // Arrange
            var rootCa = CertificateTestHelpers.GenerateRootCA();
            var partnerACert = CertificateTestHelpers.GeneratePartnerCertificate(
                rootCa, "Partner A", "/plugins/partner-a");
            var partnerBCert = CertificateTestHelpers.GeneratePartnerCertificate(
                rootCa, "Partner B", "/plugins/partner-b");

            // Act - Extract path constraints
            var pathA = X509CertificateInfo.ExtractSubjectPath(partnerACert);
            var pathB = X509CertificateInfo.ExtractSubjectPath(partnerBCert);

            // Assert
            pathA.Should().Be("/plugins/partner-a");
            pathB.Should().Be("/plugins/partner-b");
            pathA.Should().NotBe(pathB);

            // Verify isolation
            var constraintsA = new CertificateConstraints { SubjectPath = pathA };
            constraintsA.IsPathAllowed("/plugins/partner-a/file.lua").Should().BeTrue();
            constraintsA.IsPathAllowed("/plugins/partner-b/file.lua").Should().BeFalse();
        }

        [Fact]
        public void AntiPolymorphism_PreventsSelfModifyingCode()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated()
                .WithAntiPolymorphism(policy => policy
                    .AllowOnlyLuaExtension()
                    .PreventDynamicCode())
                .AllowModules("basic", "string");

            var script = new Script(config);

            // Act & Assert - loadstring should be blocked
            Action action = () => script.DoString(@"
                local code = 'return 42'
                local fn = loadstring(code)
                return fn()
            ");

            action.Should().Throw<ScriptRuntimeException>();
        }

        [Fact]
        public void ResourceLimits_MemoryLimit_Enforced()
        {
            // Arrange
            var config = SecurityConfiguration.CreateIsolated()
                .WithMemoryLimit(1) // 1 MB limit
                .AllowModules("basic", "table");

            var script = new Script(config);

            // Act & Assert - Try to allocate too much memory
            Action action = () => script.DoString(@"
                local huge = {}
                for i = 1, 1000000 do
                    huge[i] = string.rep('x', 1000)
                end
            ");

            action.Should().Throw<ScriptRuntimeException>();
        }
    }

}