using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Security.Unit")]
    public class EnumConverterTests
    {
        [Test]
        public void TestScriptCapabilities_ArrayDeserialization()
        {
            const string json = @"{""capabilities"": [""FileWrite"", ""NetworkAccess""]}";
            var policy = JsonSerializer.Deserialize<SecurityPolicy>(
                json,
                ManifestJsonOptions.Default
            );

            Assert.That(
                policy.Capabilities,
                Is.EqualTo(ScriptCapabilities.FileWrite | ScriptCapabilities.NetworkAccess)
            );
        }

        [Test]
        public void TestScriptCapabilities_SingleValueDeserialization()
        {
            var json = @"{""capabilities"": ""FileWrite""}";
            var policy = JsonSerializer.Deserialize<SecurityPolicy>(
                json,
                ManifestJsonOptions.Default
            );

            Assert.That(policy.Capabilities, Is.EqualTo(ScriptCapabilities.FileWrite));
        }

        [Test]
        public void TestScriptCapabilities_NumericDeserialization()
        {
            var json = @"{""capabilities"": 2}";
            var policy = JsonSerializer.Deserialize<SecurityPolicy>(
                json,
                ManifestJsonOptions.Default
            );

            Assert.That(policy.Capabilities, Is.EqualTo(ScriptCapabilities.FileWrite));
        }

        [Test]
        public void TestCoreModules_ArrayDeserialization()
        {
            const string json = @"{""allowedModules"": [""Basic"", ""String""]}";
            var policy = JsonSerializer.Deserialize<SecurityPolicy>(
                json,
                ManifestJsonOptions.Default
            );

            Assert.That(policy.AllowedModules, Is.EqualTo(CoreModules.Basic | CoreModules.String));
        }

        [Test]
        public void TestCoreModules_SingleValueDeserialization()
        {
            var json = @"{""allowedModules"": ""Basic""}";
            var policy = JsonSerializer.Deserialize<SecurityPolicy>(
                json,
                ManifestJsonOptions.Default
            );

            Assert.That(policy.AllowedModules, Is.EqualTo(CoreModules.Basic));
        }

        [Test]
        public void TestManifest_WithCapabilitiesArray()
        {
            var manifestJson =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""test-manifest"",
                ""signed-content"": [
                    {
                        ""key-id"": ""sha256:test123"",
                        ""signature"": ""test-signature"",
                        ""packages"": {
                            ""test-package"": {
                                ""files"": {
                                    ""test.lua"": ""sha256:filehash""
                                },
                                ""metadata"": {
                                    ""name"": ""Test Package"",
                                    ""version"": ""1.0.0""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""test-package""],
                                ""selector"": "":file"",
                                ""capabilities"": {
                                    ""deny-all"": true,
                                    ""capabilities"": [""FileWrite""]
                                }
                            }
                        ]
                    }
                ]
            }";

            var manifest = JsonSerializer.Deserialize<Manifest>(
                manifestJson,
                ManifestJsonOptions.Default
            );

            Assert.IsNotNull(manifest);

            // V2.0 manifests: test the policies directly from signed content
            Assert.IsTrue(manifest.SignedContent.Length > 0);
            var firstBlock = manifest.SignedContent[0];
            Assert.IsTrue(firstBlock.Policies.Length > 0);
            var firstPolicy = firstBlock.Policies[0];
            Assert.Contains("FileWrite", firstPolicy.Capabilities.Capabilities);
        }

        [Test]
        public void TestManifest_WithAllowedModulesArray()
        {
            const string manifestJson =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""test-manifest"",
                ""signed-content"": [
                    {
                        ""key-id"": ""sha256:test123"",
                        ""signature"": ""test-signature"",
                        ""packages"": {
                            ""test-package"": {
                                ""files"": {
                                    ""test.lua"": ""sha256:filehash""
                                },
                                ""metadata"": {
                                    ""name"": ""Test Package"",
                                    ""version"": ""1.0.0""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""test-package""],
                                ""selector"": "":file"",
                                ""modules"": {
                                    ""deny-all"": true,
                                    ""modules"": [""basic""]
                                }
                            }
                        ]
                    }
                ]
            }";

            var manifest = JsonSerializer.Deserialize<Manifest>(
                manifestJson,
                ManifestJsonOptions.Default
            );

            Assert.IsNotNull(manifest);

            // V2.0 manifests: test the policies directly from signed content
            Assert.IsTrue(manifest.SignedContent.Length > 0);
            var firstBlock = manifest.SignedContent[0];
            Assert.IsTrue(firstBlock.Policies.Length > 0);
            var firstPolicy = firstBlock.Policies[0];
            // For V2.0, modules are in the modules restriction section
            Assert.Contains("basic", firstPolicy.Modules.Modules);
        }
    }
}
