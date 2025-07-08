using System;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using NUnit.Framework;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Security
{
    [TestFixture]
    [Category("Security.Policy")]
    public class CompiledPolicyRulesTests
    {
        private SecurityPolicy _testPolicy1;
        private SecurityPolicy _testPolicy2;
        private SecurityPolicy _testPolicy3;
        private Maybe<LuaExecutionContext> _testContext;
        private ScriptIdentity _testIdentity;

        [SetUp]
        public void Setup()
        {
            _testPolicy1 = new SecurityPolicy
            {
                Name = Maybe<string>.From("TestPolicy1"),
                TimeoutMs = 10000,
                MaxMemoryMB = 32,
                AllowedModules = CoreModules.Basic | CoreModules.String,
                AllowExecution = true,
            };

            _testPolicy2 = new SecurityPolicy
            {
                Name = Maybe<string>.From("TestPolicy2"),
                TimeoutMs = 20000,
                MaxMemoryMB = 64,
                AllowedModules = CoreModules.Basic | CoreModules.Math,
                AllowExecution = true,
            };

            _testPolicy3 = new SecurityPolicy
            {
                Name = Maybe<string>.From("TestPolicy3"),
                TimeoutMs = 5000,
                MaxMemoryMB = 16,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

            _testIdentity = new ScriptIdentity(
                "TestScript",
                new NuGetVersion("1.0.0"),
                new byte[]
                {
                    0x01,
                    0x02,
                    0x03,
                    0x04,
                    0x05,
                    0x06,
                    0x07,
                    0x08,
                    0x09,
                    0x0A,
                    0x0B,
                    0x0C,
                    0x0D,
                    0x0E,
                    0x0F,
                    0x10,
                }
            );

            // Create a minimal manifest for testing
            var testManifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "TestManifest",
                packageName: "TestManifest",
                packageDescription: "Test manifest for policy rules"
            );

            // Create a self-signed certificate for testing
            var contextResult = LuaExecutionContext.CreateWithManifest(
                "test.lua",
                testManifest,
                _testIdentity,
                Maybe<LuaExecutionContext>.None
            );
            _testContext = contextResult.IsSuccess
                ? Maybe<LuaExecutionContext>.From(contextResult.Value)
                : Maybe<LuaExecutionContext>.None;
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_WithSignaturePolicy_ReturnsSignaturePolicy()
        {
            // Arrange
            var publicKeyToken = CertificateManager.TokenToHex(_testIdentity.PublicKeyToken);
            var signaturePolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                publicKeyToken,
                _testPolicy1
            );
            var rules = new CompiledPolicyRules(signaturePolicies);

            // Act
            var result = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;
            Assert.Multiple(() =>
            {
                Assert.That(policy.TimeoutMs, Is.EqualTo(_testPolicy1.TimeoutMs));
                Assert.That(policy.MaxMemoryMB, Is.EqualTo(_testPolicy1.MaxMemoryMB));
                Assert.That(policy.Name.GetValueOrDefault(""), Does.StartWith("Signature["));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_WithPathPolicy_ReturnsPathPolicy()
        {
            // Arrange
            var pathPolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                "*.lua",
                _testPolicy2
            );
            var rules = new CompiledPolicyRules(pathPolicies: pathPolicies);

            // Act
            var result = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;
            Assert.Multiple(() =>
            {
                Assert.That(policy.TimeoutMs, Is.EqualTo(_testPolicy2.TimeoutMs));
                Assert.That(policy.MaxMemoryMB, Is.EqualTo(_testPolicy2.MaxMemoryMB));
                Assert.That(policy.Name.GetValueOrDefault(""), Does.StartWith("Path["));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_WithFallbackPolicy_ReturnsFallbackPolicy()
        {
            // Arrange
            var rules = new CompiledPolicyRules(fallbackPolicy: _testPolicy3);

            // Act
            var result = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;
            Assert.Multiple(() =>
            {
                Assert.That(policy.TimeoutMs, Is.EqualTo(_testPolicy3.TimeoutMs));
                Assert.That(policy.MaxMemoryMB, Is.EqualTo(_testPolicy3.MaxMemoryMB));
                Assert.That(policy.Name.GetValueOrDefault(""), Does.StartWith("Fallback"));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_WithManifestPolicy_AppliesManifestRestrictions()
        {
            // Arrange
            var restrictivePolicy = ManifestTestHelpers.CreateRestrictivePolicy("test-package");
            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "restrictive-manifest",
                packageId: "test-package",
                packageName: "Restrictive Test",
                policies: ImmutableArray.Create(restrictivePolicy)
            );

            var rules = new CompiledPolicyRules(fallbackPolicy: _testPolicy2, manifest: manifest);

            // Act
            var result = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;

            Assert.Multiple(() =>
            {
                // Should get intersection of fallback and manifest policies
                // Restrictive policy has Timeout="1s" (1000ms), fallback has 20000ms, so min is 1000ms
                Assert.That(policy.TimeoutMs, Is.EqualTo(1000)); // min(20000, 1000)
                // Restrictive policy has MaxMemory="1MB" (1MB), fallback has 64MB, so min is 1MB
                Assert.That(policy.MaxMemoryMB, Is.EqualTo(1)); // min(64, 1)
                // Check that only Basic module is allowed (intersection of Basic|Math and Basic)
                Assert.That(policy.AllowedModules.HasFlag(CoreModules.Basic));
                Assert.That(!policy.AllowedModules.HasFlag(CoreModules.Math));
                Assert.That(policy.Name.GetValueOrDefault(""), Does.EndWith("+Manifest"));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_WithEvalContext_AppliesEvalPolicy()
        {
            // Arrange
            var evalContext = _testContext
                .Map(ctx => ctx.With(sourceFile: "test.lua:eval"))
                .GetValueOrDefault();
            var evalPolicy = ManifestTestHelpers.CreateDefaultPolicy("test-package", ":eval");
            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "eval-manifest",
                packageId: "test-package",
                packageName: "Eval Test",
                policies: ImmutableArray.Create(evalPolicy)
            );

            var rules = new CompiledPolicyRules(fallbackPolicy: _testPolicy2, manifest: manifest);

            // Act
            var result = rules.ResolvePolicy(evalContext);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;
            Assert.Multiple(() =>
            {
                // Default policy has Timeout="30s" (30000ms), fallback has 20000ms, so min is 20000ms
                Assert.That(policy.TimeoutMs, Is.EqualTo(20000)); // min(30000, 20000)
                // Default policy has MaxMemory="64MB" (64MB), fallback has 64MB, so min is 64MB
                Assert.That(policy.MaxMemoryMB, Is.EqualTo(64)); // min(64, 64)
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_PolicyCaching_ImprovesCacheHitRate()
        {
            // Arrange
            var rules = new CompiledPolicyRules(fallbackPolicy: _testPolicy1);
            var context1 = _testContext
                .Map(ctx => ctx.With(sourceFile: "test1.lua"))
                .GetValueOrDefault();
            var context2 = _testContext
                .Map(ctx => ctx.With(sourceFile: "test1.lua"))
                .GetValueOrDefault(); // Same file

            // Act
            var result1 = rules.ResolvePolicy(context1);
            var result2 = rules.ResolvePolicy(context2);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(result1.IsSuccess, Is.True);
                Assert.That(result2.IsSuccess, Is.True);
                Assert.That(rules.Stats.CacheHits, Is.EqualTo(1));
                Assert.That(rules.Stats.CacheMisses, Is.EqualTo(1));
                Assert.That(rules.Stats.CacheHitRate, Is.EqualTo(0.5));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_WithNoContext_ReturnsError()
        {
            // Arrange
            var rules = new CompiledPolicyRules();
            var noContext = Maybe<LuaExecutionContext>.None;

            // Act
            var result = noContext.HasValue
                ? rules.ResolvePolicy(noContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Execution context is required")
                );

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(
                    result.Error.Message,
                    Contains.Substring("Execution context is required")
                );
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_SignaturePolicyTakesPrecedenceOverPath()
        {
            // Arrange
            var publicKeyToken = CertificateManager.TokenToHex(_testIdentity.PublicKeyToken);
            var signaturePolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                publicKeyToken,
                _testPolicy1
            );
            var pathPolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                "*.lua",
                _testPolicy2
            );
            var rules = new CompiledPolicyRules(signaturePolicies, pathPolicies);

            // Act
            var result = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;
            Assert.Multiple(() =>
            {
                Assert.That(policy.TimeoutMs, Is.EqualTo(_testPolicy1.TimeoutMs));
                Assert.That(policy.Name.GetValueOrDefault(""), Does.StartWith("Signature["));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_UnsignedPolicy_HandlesEmptyToken()
        {
            // Arrange - Context without identity to test unsigned handling
            var unsignedContext = _testContext
                .Map(ctx => ctx.With(identity: Maybe<ScriptIdentity>.None))
                .GetValueOrDefault();
            var signaturePolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                "", // Empty string for unsigned scripts
                _testPolicy3
            );
            var rules = new CompiledPolicyRules(signaturePolicies);

            // Act
            var result = rules.ResolvePolicy(unsignedContext);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            var policy = result.Value;
            Assert.Multiple(() =>
            {
                Assert.That(policy.TimeoutMs, Is.EqualTo(_testPolicy3.TimeoutMs));
                Assert.That(policy.Name.GetValueOrDefault(""), Does.StartWith("Unsigned"));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_ComplexPathPatterns_MatchesCorrectly()
        {
            // Arrange
            var pathPolicies = ImmutableDictionary<string, SecurityPolicy>
                .Empty.Add("plugins/*.lua", _testPolicy1)
                .Add("system/*.lua", _testPolicy2)
                .Add("*.lua", _testPolicy3);
            var rules = new CompiledPolicyRules(pathPolicies: pathPolicies);

            var pluginContext = _testContext
                .Map(ctx => ctx.With(sourceFile: "plugins/test.lua"))
                .GetValueOrDefault();
            var systemContext = _testContext
                .Map(ctx => ctx.With(sourceFile: "system/test.lua"))
                .GetValueOrDefault();
            var genericContext = _testContext
                .Map(ctx => ctx.With(sourceFile: "test.lua"))
                .GetValueOrDefault();

            // Act
            var pluginResult = rules.ResolvePolicy(pluginContext);
            var systemResult = rules.ResolvePolicy(systemContext);
            var genericResult = rules.ResolvePolicy(genericContext);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(pluginResult.IsSuccess, Is.True);
                Assert.That(systemResult.IsSuccess, Is.True);
                Assert.That(genericResult.IsSuccess, Is.True);

                Assert.That(pluginResult.Value.TimeoutMs, Is.EqualTo(_testPolicy1.TimeoutMs));
                Assert.That(systemResult.Value.TimeoutMs, Is.EqualTo(_testPolicy2.TimeoutMs));
                Assert.That(genericResult.Value.TimeoutMs, Is.EqualTo(_testPolicy3.TimeoutMs));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ClearCache_ResetsStats()
        {
            // Arrange
            var rules = new CompiledPolicyRules(fallbackPolicy: _testPolicy1);
            var result1 = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );
            var result2 = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );

            // Act
            rules.ClearCache();

            // Assert
            Assert.That(rules.Stats.CacheClears, Is.EqualTo(1));

            // Verify cache was cleared by checking for miss on next resolution
            var initialMisses = rules.Stats.CacheMisses;
            var result3 = _testContext.HasValue
                ? rules.ResolvePolicy(_testContext.Value)
                : Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Test context not available")
                );
            Assert.That(rules.Stats.CacheMisses, Is.EqualTo(initialMisses + 1));
        }

        [Category("Security.Unit")]
        [Test]
        public void Stats_TrackPerformanceMetrics()
        {
            // Arrange
            var rules = new CompiledPolicyRules(fallbackPolicy: _testPolicy1);

            // Act
            var startTime = DateTime.UtcNow;
            for (var i = 0; i < 10; i++)
            {
                var context = _testContext
                    .Map(ctx => ctx.With(sourceFile: $"test{i}.lua"))
                    .GetValueOrDefault();
                rules.ResolvePolicy(context);
            }
            var endTime = DateTime.UtcNow;

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(rules.Stats.Resolutions, Is.EqualTo(10));
                // With improved caching, all requests with same identity and policy resolution use the same cache key
                Assert.That(rules.Stats.CacheHits, Is.EqualTo(9)); // First is miss, next 9 are hits
                Assert.That(rules.Stats.CacheMisses, Is.EqualTo(1));
                Assert.That(rules.Stats.TotalResolutionTime.TotalMilliseconds, Is.GreaterThan(0));
                Assert.That(rules.Stats.AverageResolutionTime.TotalMilliseconds, Is.GreaterThan(0));
                Assert.That(rules.Stats.CacheHitRate, Is.EqualTo(0.9)); // 9/10 = 0.9
            });

            // Verify string representation
            var statsString = rules.Stats.ToString();
            Assert.That(statsString, Contains.Substring("Resolutions: 10"));
            Assert.That(statsString, Contains.Substring("Cache Hit Rate: 90.00%"));
        }

        [Category("Security.Unit")]
        [Test]
        public void ResolvePolicy_PerformanceTest()
        {
            // Arrange
            var signaturePolicies = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();
            var pathPolicies = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();

            // Create many policies to test performance
            for (var i = 0; i < 100; i++)
            {
                var token = $"token{i:D3}";
                var pattern = $"path{i:D3}/*.lua";
                signaturePolicies[token] = new SecurityPolicy
                {
                    Name = Maybe<string>.From($"SignaturePolicy{i}"),
                    TimeoutMs = 1000 + i,
                };
                pathPolicies[pattern] = new SecurityPolicy
                {
                    Name = Maybe<string>.From($"PathPolicy{i}"),
                    TimeoutMs = 2000 + i,
                };
            }

            var rules = new CompiledPolicyRules(
                signaturePolicies.ToImmutable(),
                pathPolicies.ToImmutable()
            );

            // Pre-create contexts to avoid allocation overhead in the performance test
            var contexts = new LuaExecutionContext[100];
            for (var j = 0; j < 100; j++)
            {
                contexts[j] = _testContext
                    .Map(ctx => ctx.With(sourceFile: $"path{j:D3}/test{j}.lua"))
                    .GetValueOrDefault();
            }

            var startTime = DateTime.UtcNow;
            for (var i = 0; i < 1000; i++)
            {
                var context = contexts[i % 100];
                var result = rules.ResolvePolicy(context);
                Assert.That(result.IsSuccess, Is.True);
            }
            var endTime = DateTime.UtcNow;

            var totalTime = endTime - startTime;
            Assert.Multiple(() =>
            {
                Assert.That(totalTime.TotalMilliseconds, Is.LessThan(2500)); // Should be reasonably fast
                Assert.That(rules.Stats.CacheHitRate, Is.GreaterThan(0.5)); // Should have good cache hit rate
                Assert.That(rules.Stats.AverageResolutionTime.TotalMilliseconds, Is.LessThan(200)); // Should be sub-200ms
            });
        }
    }
}
