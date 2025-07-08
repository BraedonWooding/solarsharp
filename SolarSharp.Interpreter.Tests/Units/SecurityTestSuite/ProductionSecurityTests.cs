namespace SolarSharp.Interpreter.Tests.Units.SecurityTestSuite
{
#if ENABLE_PRODUCTION_SECURITY_TESTS
    [TestFixture]
    public class ProductionSecurityTests
    {
        private ECDsa _signingKey;
        private string _publicKeyPem;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _publicKeyPem = Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo());
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void ResourceLimits_UnderHighLoad_PerformanceRemainsSteady()
        {
            var config = SecurityPolicy
                .Isolated()
                .WithTimeout(TimeSpan.FromMilliseconds(1000))
                .WithMemoryLimitMB(10)
                .WithInstructionLimit(100000)
                .WithScriptingLimits(limits => limits.WithMaxCallDepth(50));

            var measurements = new List<long>();
            const int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                var script = new Script(config);
                var sw = Stopwatch.StartNew();

                script.DoString(
                    @"
                    local sum = 0
                    for i = 1, 1000 do
                        sum = sum + i
                    end
                    return sum
                "
                );

                sw.Stop();
                measurements.Add(sw.ElapsedMilliseconds);
            }

            // Performance should be consistent
            var average = measurements.Average();
            var maxDeviation = measurements.Max(m => Math.Abs(m - average));

            // Allow up to 50% deviation from average (generous for CI environments)
            Assert.True(
                maxDeviation < average * 0.5,
                $"Performance inconsistent: avg={average}ms, max deviation={maxDeviation}ms"
            );
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void MemoryLimits_UnderPressure_DoesNotLeak()
        {
            var config = SecurityPolicy.Isolated().WithMemoryLimitMB(5);

            var initialMemory = GC.GetTotalMemory(true);

            // Create and dispose many scripts
            for (int i = 0; i < 100; i++)
            {
                var script = new Script(config);

                try
                {
                    script.DoString(
                        @"
                        local bigTable = {}
                        for i = 1, 1000 do
                            bigTable[i] = string.rep('x', 100)
                        end
                    "
                    );
                }
                catch (ScriptRuntimeException)
                {
                    // Expected - memory limit exceeded
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryGrowth = finalMemory - initialMemory;

            // Memory growth should be reasonable (less than 10MB)
            Assert.True(
                memoryGrowth < 10 * 1024 * 1024,
                $"Memory leak detected: {memoryGrowth / 1024 / 1024}MB growth"
            );
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void ConcurrentScriptExecution_WithSeparateConfigs_RemainsIsolated()
        {
            var config1 = SecurityPolicy
                .Isolated()
                .WithExecutionLimits(limits => limits.WithTimeoutMs(2000))
                .WithModules(CoreModules.Basic | CoreModules.OS);

            var config2 = SecurityPolicy
                .Isolated()
                .WithExecutionLimits(limits => limits.WithTimeoutMs(1000))
                .WithModules(CoreModules.Basic | CoreModules.OS);

            var results = new List<bool>();
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                var useConfig1 = i % 2 == 0;
                var config = useConfig1 ? config1 : config2;

                tasks.Add(
                    Task.Run(() =>
                    {
                        try
                        {
                            var script = new Script(config);
                            var result = script.DoString(
                                @"
                            local start = os.clock()
                            while os.clock() - start < 0.5 do
                                -- Busy wait
                            end
                            return true
                        "
                            );

                            lock (results)
                            {
                                results.Add(result.Boolean);
                            }
                        }
                        catch (Exception)
                        {
                            lock (results)
                            {
                                results.Add(false);
                            }
                        }
                    })
                );
            }

            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));

            // All scripts should complete successfully
            Assert.That(
                results.Count >= 8,
                $"Expected at least 8 completions, got {results.Count}",
                Is.True
            );
            Assert.That(
                results.Count(r => r, Is.True) >= 8,
                "Most scripts should complete successfully"
            );
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void ManifestVerification_WithLargeManifest_PerformsEfficiently()
        {
            var largeManifest = CreateLargeManifest(1000); // 1000 files
            var manifestJson = System.Text.Json.JsonSerializer.Serialize(largeManifest);

            var sw = Stopwatch.StartNew();

            // Canonicalize large manifest
            var canonical = JsonCanonicalizer.Canonicalize(manifestJson);

            sw.Stop();

            // Should complete in reasonable time (< 1 second)
            Assert.True(
                sw.ElapsedMilliseconds < 1000,
                $"Large manifest canonicalization too slow: {sw.ElapsedMilliseconds}ms"
            );

            // Verify signature
            var signature = _signingKey.SignData(
                Encoding.UTF8.GetBytes(canonical),
                HashAlgorithmName.SHA256
            );
            largeManifest.Security.Signature = new SignatureInfo
            {
                Algorithm = "ECDSA-SHA256",
                Value = Convert.ToBase64String(signature),
            };

            // Trust store functionality removed in new API

            sw.Restart();
            var verifier = new ManifestVerifier();
            var isValid = verifier.VerifySignature(largeManifest);
            sw.Stop();

            Assert.That(isValid, Is.True);
            Assert.True(
                sw.ElapsedMilliseconds < 500,
                $"Large manifest verification too slow: {sw.ElapsedMilliseconds}ms"
            );
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void FileSystemSandbox_WithHighVolumeAccess_MaintainsRestrictions()
        {
            var allowedDir = Path.Combine(_tempDir, "allowed");
            var blockedDir = Path.Combine(_tempDir, "blocked");

            Directory.CreateDirectory(allowedDir);
            Directory.CreateDirectory(blockedDir);

            // Create many files in both directories
            for (int i = 0; i < 100; i++)
            {
                File.WriteAllText(Path.Combine(allowedDir, $"file{i}.txt"), $"Content {i}");
                File.WriteAllText(Path.Combine(blockedDir, $"file{i}.txt"), $"Blocked {i}");
            }

            var config = SecurityPolicy
                .Isolated()
                .WithFileSystemPolicy(fs =>
                    fs.AllowDirectoryAccess(allowedDir, FileSystemRights.Read)
                )
                .WithModules(CoreModules.Basic | CoreModules.OS);

            var script = new Script(config);

            // High volume access to allowed directory should work
            var allowedScript =
                $@"
                local files = {{}}
                for i = 0, 99 do
                    local file = io.open('{allowedDir.Replace("\\", "/")}/file' .. i .. '.txt', 'r')
                    if file then
                        files[i] = file:read('*all')
                        file:close()
                    end
                end
                return #files
            ";

            var result = script.DoString(allowedScript);
            Assert.Equal(100, result.Number);

            // Access to blocked directory should fail
            var blockedScript =
                $@"
                local file = io.open('{blockedDir.Replace("\\", "/")}/file0.txt', 'r')
                return file ~= nil
            ";

            var blockedResult = script.DoString(blockedScript);
            Assert.That(blockedResult.Boolean, Is.False);
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void SecurityPolicy_WithComplexPolicy_BuildsEfficiently()
        {
            var sw = Stopwatch.StartNew();

            // Build complex configuration
            var config = SecurityPolicy
                .Isolated()
                .WithExecutionLimits(limits =>
                    limits
                        .WithTimeoutMs(30000)
                        .WithMemoryLimitMB(100)
                        .WithInstructionLimit(1000000)
                        .WithCallDepthLimit(100)
                )
                .WithFileSystemPolicy(fs =>
                    fs.AllowDirectoryAccess("/tmp", FileSystemRights.ReadWrite)
                        .AllowDirectoryAccess("/var/log", FileSystemRights.Read)
                        .AllowDirectoryAccess(_tempDir, FileSystemRights.ReadWrite)
                )
                .WithModulePolicy(modules =>
                    modules
                        .AllowModule("basic")
                        .AllowModule("string")
                        .AllowModule("table")
                        .AllowModule("math")
                        .AllowModule("io")
                        .AllowModule("os")
                )
                .WithAntiPolymorphism(anti =>
                    anti.AllowOnlyLuaExtension().PreventLuaFileWrites().PreventDynamicCode()
                )
                .WithModules(CoreModules.Basic | CoreModules.OS);

            sw.Stop();

            // Configuration building should be fast
            Assert.True(
                sw.ElapsedMilliseconds < 100,
                $"Complex configuration building too slow: {sw.ElapsedMilliseconds}ms"
            );

            // Verify configuration is functional
            var script = new Script(config);
            var result = script.DoString("return 'Configuration works'");
            Assert.Equal("Configuration works", result.String);
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void ErrorHandling_UnderStress_RemainsRobust()
        {
            var config = SecurityPolicy
                .Isolated()
                .WithExecutionLimits(limits => limits.WithTimeoutMs(100).WithInstructionLimit(1000))
                .WithModules(CoreModules.Basic | CoreModules.OS);

            var errorCount = 0;
            var successCount = 0;

            // Generate many different types of errors
            var errorScripts = new[]
            {
                "while true do end", // Timeout
                "local function f() f() end f()", // Stack overflow
                "error('Test error')", // Lua error
                "require('nonexistent')", // Module error
                "local t = {} for i = 1, 10000 do t[i] = {} end", // Memory pressure
            };

            for (int i = 0; i < 200; i++)
            {
                try
                {
                    var script = new Script(config);
                    var errorScript = errorScripts[i % errorScripts.Length];
                    script.DoString(errorScript);
                    successCount++;
                }
                catch (Exception)
                {
                    errorCount++;
                }
            }

            // Most should error (that's expected), but system should remain stable
            Assert.That(errorCount > 150, "Expected most scripts to error", Is.True);
            Assert.That(
                errorCount + successCount == 200,
                "All scripts should complete (success or error, Is.True)"
            );
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new BasePolicySet API")]
        public void LongRunningScripts_WithPeriodicChecks_EnforceTimeouts()
        {
            var config = SecurityPolicy
                .Isolated()
                .WithExecutionLimits(limits => limits.WithTimeoutMs(1000))
                .WithModules(CoreModules.Basic | CoreModules.OS);

            var script = new Script(config);
            var sw = Stopwatch.StartNew();

            Assert.Throws<ScriptRuntimeException>(() =>
            {
                script.DoString(
                    @"
                    local start = os.clock()
                    while os.clock() - start < 2 do
                        -- Busy wait longer than timeout
                    end
                "
                );
            });

            sw.Stop();

            // Should timeout close to the configured limit
            Assert.That(
                sw.ElapsedMilliseconds >= 900,
                "Timeout should be close to configured limit",
                Is.True
            );
            Assert.That(sw.ElapsedMilliseconds <= 1500, "Timeout should not be too late", Is.True);
        }

        private LuaManifest CreateLargeManifest(int fileCount)
        {
            var files = new Dictionary<string, FilePolicy>();

            for (int i = 0; i < fileCount; i++)
            {
                var content = $"print('File {i}')";
                var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

                files[$"scripts/file{i}.lua"] = new FilePolicy
                {
                    Hash = hash,
                    Algorithm = "SHA256",
                };
            }

            return new LuaManifest
            {
                Version = "1.0",
                Description = $"Large manifest with {fileCount} files",
                Files = files,
                Security = new SecurityInfo
                {
                    PublicKey = new PublicKeyInfo { Algorithm = "ECDSA-P256", Key = _publicKeyPem },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            _signingKey?.Dispose();

            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
#endif
}
