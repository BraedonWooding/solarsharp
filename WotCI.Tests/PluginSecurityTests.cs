using System;
using FluentAssertions;
using NUnit.Framework;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace WotCI.Tests
{
    [TestFixture]
    [Category("IntegrationTest")]
    [Category("SecurityTest")]
    public class PluginSecurityTests
    {
        [Test]
        public void PluginExecution_WithValidManifest_Succeeds()
        {
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

                        var script = new Script(manifest.Manifest);
            var result = script.DoString("return 'Plugin loaded successfully'");

                        result.String.Should().Be("Plugin loaded successfully");
        }

        [Test]
        public void PluginExecution_ExceedsTimeout_Throws()
        {
                        var config = SecurityConfiguration.Isolated()
                .WithTimeoutMs(1) // 1 second timeout
                .AllowModules("basic");

            var script = new Script(config);

                        Action action = () => script.DoString(@"
                while true do
                    -- Infinite loop
                end
            ");

            action.Should().Throw<ResourceLimitExceededException>()
                .WithMessage("*limit exceeded*");
        }

        [Test]
        public void PluginExecution_AccessDeniedModule_Throws()
        {
                        var config = SecurityConfiguration.Isolated()
                .WithModules(CoreModules.Basic | CoreModules.String); // No 'io' module

            var script = new Script(config);

                        Action action = () => script.DoString(@"
                local f = io.open('test.txt', 'w')
            ");

            action.Should().Throw<ScriptRuntimeException>();
        }

        [Test]
        public void ManifestValidation_WithTamperedContent_Fails()
        {
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

                        Action action = () =>
            {
                var manifest = LuaManifest.ParseManifest(tamperedManifest, "/plugins/test");
                ManifestAutoLoader.VerifyManifestSignature(manifest);
            };

            action.Should().Throw<ManifestSignatureException>();
        }

        [Test]
        public void CertificatePathConstraint_EnforcesPluginBoundaries()
        {
                        var rootCa = CertificateTestHelpers.GenerateRootCA();
            var partnerACert = CertificateTestHelpers.GeneratePartnerCertificate(
                rootCa, "Partner A", "/plugins/partner-a");
            var partnerBCert = CertificateTestHelpers.GeneratePartnerCertificate(
                rootCa, "Partner B", "/plugins/partner-b");

            // Extract path constraints
            var pathA = X509CertificateInfo.ExtractSubjectPath(partnerACert);
            var pathB = X509CertificateInfo.ExtractSubjectPath(partnerBCert);

                        pathA.Should().Be("/plugins/partner-a");
            pathB.Should().Be("/plugins/partner-b");
            pathA.Should().NotBe(pathB);

            // Verify isolation
            var constraintsA = new CertificateConstraints { SubjectPath = pathA };
            constraintsA.IsPathAllowed("/plugins/partner-a/file.lua").Should().BeTrue();
            constraintsA.IsPathAllowed("/plugins/partner-b/file.lua").Should().BeFalse();
        }

        [Test]
        public void AntiPolymorphism_PreventsSelfModifyingCode()
        {
                        var config = SecurityConfiguration.Isolated()
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

        [Test]
        public void ResourceLimits_MemoryLimit_Enforced()
        {
                        var config = SecurityConfiguration.Isolated()
                .WithMemoryLimitMB(1) // 1 MB limit
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