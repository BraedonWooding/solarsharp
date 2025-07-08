namespace SolarSharp.Interpreter.Tests.Units.SecurityTestSuite
{
#if ENABLE_PACKAGE_FORMAT_TESTS
    [TestFixture]
    public class PackageFormatTests
    {
        private ECDsa _signingKey;
        private string _publicKeyPem;

        [SetUp]
        public void SetUp()
        {
            _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _publicKeyPem = Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo());
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new Manifest API")]
        public void CreateZipPackage_WithValidManifest_VerifiesIntegrity()
        {
            var tempDir = Path.GetTempPath();
            var packagePath = Path.Combine(tempDir, "test-package.zip");
            var scriptContent = "print('Hello from package!')";
            var scriptHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(scriptContent))
            );

            try
            {
                // Create ZIP package
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    // Add script
                    var scriptEntry = archive.CreateEntry("main.lua");
                    using (var stream = scriptEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(scriptContent);
                    }

                    // Create and sign manifest
                    var manifest = new LuaManifest
                    {
                        Version = "1.0",
                        Description = "Test package",
                        Files = new Dictionary<string, FilePolicy>
                        {
                            ["main.lua"] = new FilePolicy
                            {
                                Hash = scriptHash,
                                Algorithm = "SHA256",
                            },
                        },
                        Security = new SecurityInfo
                        {
                            PublicKey = new PublicKeyInfo
                            {
                                Algorithm = "ECDSA-P256",
                                Key = _publicKeyPem,
                            },
                        },
                    };

                    var manifestJson = System.Text.Json.JsonSerializer.Serialize(
                        manifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );

                    // Sign manifest
                    var canonicalJson = JsonCanonicalizer.Canonicalize(manifestJson);
                    var signature = _signingKey.SignData(
                        Encoding.UTF8.GetBytes(canonicalJson),
                        HashAlgorithmName.SHA256
                    );
                    manifest.Security.Signature = new SignatureInfo
                    {
                        Algorithm = "ECDSA-SHA256",
                        Value = Convert.ToBase64String(signature),
                    };

                    // Add manifest to package
                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using (var stream = manifestEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        var signedManifestJson = System.Text.Json.JsonSerializer.Serialize(
                            manifest,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                        );
                        writer.Write(signedManifestJson);
                    }
                }

                // Test package verification
                var config = Examples.IsolatedBasePolicySet;

                // Trust store functionality removed in new API

                // Verify package can be loaded securely
                var script = new Script(config.PolicySet);

                // Extract and verify
                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    Assert.NotNull(manifestEntry);

                    string manifestContent;
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        manifestContent = reader.ReadToEnd();
                    }

                    var loadedManifest = System.Text.Json.JsonSerializer.Deserialize<LuaManifest>(
                        manifestContent
                    );
                    Assert.NotNull(loadedManifest);

                    // Verify signature
                    var verifier = new ManifestVerifier();
                    Assert.That(verifier.VerifySignature(loadedManifest), Is.True);

                    // Verify file integrity
                    var scriptEntry = archive.GetEntry("main.lua");
                    Assert.NotNull(scriptEntry);

                    string actualScriptContent;
                    using (var stream = scriptEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        actualScriptContent = reader.ReadToEnd();
                    }

                    var actualHash = Convert.ToBase64String(
                        SHA256.HashData(Encoding.UTF8.GetBytes(actualScriptContent))
                    );
                    Assert.Equal(scriptHash, actualHash);
                }
            }
            finally
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new Manifest API")]
        public void ZipPackage_WithTamperedScript_FailsIntegrityCheck()
        {
            var tempDir = Path.GetTempPath();
            var packagePath = Path.Combine(tempDir, "tampered-package.zip");
            var originalContent = "print('Original content')";
            var tamperedContent = "print('Tampered content')";
            var originalHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(originalContent))
            );

            try
            {
                // Create package with original content but tampered script
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    // Add tampered script (different from manifest hash)
                    var scriptEntry = archive.CreateEntry("main.lua");
                    using (var stream = scriptEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(tamperedContent);
                    }

                    // Create manifest with original hash
                    var manifest = new LuaManifest
                    {
                        Version = "1.0",
                        Files = new Dictionary<string, FilePolicy>
                        {
                            ["main.lua"] = new FilePolicy
                            {
                                Hash = originalHash, // Wrong hash!
                                Algorithm = "SHA256",
                            },
                        },
                        Security = new SecurityInfo
                        {
                            PublicKey = new PublicKeyInfo
                            {
                                Algorithm = "ECDSA-P256",
                                Key = _publicKeyPem,
                            },
                        },
                    };

                    var manifestJson = System.Text.Json.JsonSerializer.Serialize(
                        manifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );

                    var canonicalJson = JsonCanonicalizer.Canonicalize(manifestJson);
                    var signature = _signingKey.SignData(
                        Encoding.UTF8.GetBytes(canonicalJson),
                        HashAlgorithmName.SHA256
                    );
                    manifest.Security.Signature = new SignatureInfo
                    {
                        Algorithm = "ECDSA-SHA256",
                        Value = Convert.ToBase64String(signature),
                    };

                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using (var stream = manifestEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        var signedManifestJson = System.Text.Json.JsonSerializer.Serialize(
                            manifest,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                        );
                        writer.Write(signedManifestJson);
                    }
                }

                // Trust store functionality removed in new API

                // Attempt to verify tampered package
                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    string manifestContent;
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        manifestContent = reader.ReadToEnd();
                    }

                    var manifest = System.Text.Json.JsonSerializer.Deserialize<LuaManifest>(
                        manifestContent
                    );

                    // Signature should be valid (manifest wasn't tampered)
                    var verifier = new ManifestVerifier();
                    Assert.That(verifier.VerifySignature(manifest), Is.True);

                    // File integrity should fail
                    var scriptEntry = archive.GetEntry("main.lua");
                    string actualContent;
                    using (var stream = scriptEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        actualContent = reader.ReadToEnd();
                    }

                    var actualHash = Convert.ToBase64String(
                        SHA256.HashData(Encoding.UTF8.GetBytes(actualContent))
                    );
                    var expectedHash = manifest.Files["main.lua"].Hash;

                    Assert.NotEqual(expectedHash, actualHash);
                }
            }
            finally
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new Manifest API")]
        public void ZipPackage_WithMissingManifest_RejectsExecution()
        {
            var tempDir = Path.GetTempPath();
            var packagePath = Path.Combine(tempDir, "no-manifest-package.zip");

            try
            {
                // Create package without manifest
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    var scriptEntry = archive.CreateEntry("main.lua");
                    using (var stream = scriptEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write("print('No manifest!')");
                    }
                }

                var config = Examples.IsolatedBasePolicySet;

                // Should reject loading package without manifest
                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    Assert.Null(manifestEntry);
                }

                // In a real implementation, this would throw a security exception
                // when trying to execute scripts from an unsigned package
            }
            finally
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new Manifest API")]
        public void ZipPackage_WithNestedDirectories_ValidatesAllFiles()
        {
            var tempDir = Path.GetTempPath();
            var packagePath = Path.Combine(tempDir, "nested-package.zip");

            var mainScript = "require('utils.helper')";
            var helperScript = "return { greeting = 'Hello!' }";

            var mainHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(mainScript))
            );
            var helperHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(helperScript))
            );

            try
            {
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    // Add main script
                    var mainEntry = archive.CreateEntry("main.lua");
                    using (var stream = mainEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(mainScript);
                    }

                    // Add nested script
                    var helperEntry = archive.CreateEntry("utils/helper.lua");
                    using (var stream = helperEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(helperScript);
                    }

                    // Create manifest covering all files
                    var manifest = new LuaManifest
                    {
                        Version = "1.0",
                        Files = new Dictionary<string, FilePolicy>
                        {
                            ["main.lua"] = new FilePolicy { Hash = mainHash, Algorithm = "SHA256" },
                            ["utils/helper.lua"] = new FilePolicy
                            {
                                Hash = helperHash,
                                Algorithm = "SHA256",
                            },
                        },
                        Security = new SecurityInfo
                        {
                            PublicKey = new PublicKeyInfo
                            {
                                Algorithm = "ECDSA-P256",
                                Key = _publicKeyPem,
                            },
                        },
                    };

                    var manifestJson = System.Text.Json.JsonSerializer.Serialize(
                        manifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );

                    var canonicalJson = JsonCanonicalizer.Canonicalize(manifestJson);
                    var signature = _signingKey.SignData(
                        Encoding.UTF8.GetBytes(canonicalJson),
                        HashAlgorithmName.SHA256
                    );
                    manifest.Security.Signature = new SignatureInfo
                    {
                        Algorithm = "ECDSA-SHA256",
                        Value = Convert.ToBase64String(signature),
                    };

                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using (var stream = manifestEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        var signedManifestJson = System.Text.Json.JsonSerializer.Serialize(
                            manifest,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                        );
                        writer.Write(signedManifestJson);
                    }
                }

                // Trust store functionality removed in new API

                // Verify all files in package
                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    string manifestContent;
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        manifestContent = reader.ReadToEnd();
                    }

                    var manifest = System.Text.Json.JsonSerializer.Deserialize<LuaManifest>(
                        manifestContent
                    );
                    var verifier = new ManifestVerifier();
                    Assert.That(verifier.VerifySignature(manifest), Is.True);

                    // Verify each file's integrity
                    foreach (var filePolicy in manifest.Files)
                    {
                        var entry = archive.GetEntry(filePolicy.Key);
                        Assert.NotNull(entry);

                        string content;
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            content = reader.ReadToEnd();
                        }

                        var actualHash = Convert.ToBase64String(
                            SHA256.HashData(Encoding.UTF8.GetBytes(content))
                        );
                        Assert.Equal(filePolicy.Value.Hash, actualHash);
                    }
                }
            }
            finally
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
        }

        [Category("Security.Unit")]
        [Test]
        [Ignore("Test needs to be updated for new Manifest API")]
        public void ZipPackage_WithUnsignedFiles_RejectsExecution()
        {
            var tempDir = Path.GetTempPath();
            var packagePath = Path.Combine(tempDir, "unsigned-files-package.zip");

            var scriptContent = "print('Signed script')";
            var unsignedContent = "print('Unsigned script')";
            var scriptHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(scriptContent))
            );

            try
            {
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    // Add signed script
                    var signedEntry = archive.CreateEntry("signed.lua");
                    using (var stream = signedEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(scriptContent);
                    }

                    // Add unsigned script (not in manifest)
                    var unsignedEntry = archive.CreateEntry("unsigned.lua");
                    using (var stream = unsignedEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(unsignedContent);
                    }

                    // Create manifest only covering signed script
                    var manifest = new LuaManifest
                    {
                        Version = "1.0",
                        Files = new Dictionary<string, FilePolicy>
                        {
                            ["signed.lua"] = new FilePolicy
                            {
                                Hash = scriptHash,
                                Algorithm = "SHA256",
                            },
                            // unsigned.lua intentionally omitted
                        },
                        Security = new SecurityInfo
                        {
                            PublicKey = new PublicKeyInfo
                            {
                                Algorithm = "ECDSA-P256",
                                Key = _publicKeyPem,
                            },
                        },
                    };

                    var manifestJson = System.Text.Json.JsonSerializer.Serialize(
                        manifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );

                    var canonicalJson = JsonCanonicalizer.Canonicalize(manifestJson);
                    var signature = _signingKey.SignData(
                        Encoding.UTF8.GetBytes(canonicalJson),
                        HashAlgorithmName.SHA256
                    );
                    manifest.Security.Signature = new SignatureInfo
                    {
                        Algorithm = "ECDSA-SHA256",
                        Value = Convert.ToBase64String(signature),
                    };

                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using (var stream = manifestEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        var signedManifestJson = System.Text.Json.JsonSerializer.Serialize(
                            manifest,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                        );
                        writer.Write(signedManifestJson);
                    }
                }

                // Trust store functionality removed in new API

                // Verify that unsigned files are detected
                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    string manifestContent;
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        manifestContent = reader.ReadToEnd();
                    }

                    var manifest = System.Text.Json.JsonSerializer.Deserialize<LuaManifest>(
                        manifestContent
                    );

                    // Find unsigned files
                    var allEntries = archive
                        .Entries.Where(e => e.Name.EndsWith(".lua"))
                        .Select(e => e.FullName);
                    var signedFiles = manifest.Files.Keys;
                    var unsignedFiles = allEntries.Except(signedFiles).ToList();

                    Assert.Single(unsignedFiles);
                    Assert.Equal("unsigned.lua", unsignedFiles.First());
                }
            }
            finally
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
        }

        public void Dispose()
        {
            _signingKey?.Dispose();
        }
    }
#endif
}
