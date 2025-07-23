using System;
using System.IO;
using System.Text;
using System.Text.Json;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

// Simple test to verify untrusted manifest signature behavior
// This test creates a manifest signed with an untrusted key and checks if it's properly rejected

class DebugPathTest
{
    static void Main()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_debug_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            Console.WriteLine("=== DEBUG: Testing Untrusted Manifest Signature ===");
            Console.WriteLine($"Test directory: {tempDir}");

            // Create a simple Lua script
            var scriptPath = Path.Combine(tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");
            Console.WriteLine($"Created script: {scriptPath}");

            // Generate two key pairs
            var trustedKeyPair = ManifestSigner.CreateKeyPair();
            var untrustedKeyPair = ManifestSigner.CreateKeyPair();

            // Get public keys
            var trustedPublicKey = ManifestSigner.ExportPublicKey(trustedKeyPair.Public);
            var untrustedPublicKey = ManifestSigner.ExportPublicKey(untrustedKeyPair.Public);

            Console.WriteLine("\nKey Status:");
            Console.WriteLine("- Generated trusted key pair");
            Console.WriteLine("- Generated untrusted key pair");

            // Create manifest JSON
            var manifestData = new
            {
                version = "1.0",
                files = new Dictionary<string, object>
                {
                    ["test.lua"] = new
                    {
                        hash = ComputeFileHash(scriptPath),
                        hashAlgorithm = "SHA256",
                        size = new FileInfo(scriptPath).Length,
                        readOnly = true
                    }
                }
            };

            var manifestJson = JsonSerializer.Serialize(manifestData, new JsonSerializerOptions { WriteIndented = true });
            
            // Sign manifest with UNTRUSTED key
            var signedManifest = ManifestSigner.SignManifestJson(manifestJson, untrustedKeyPair.Private);
            
            // Save to disk
            var manifestPath = Path.Combine(tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);
            Console.WriteLine($"\nManifest signed with UNTRUSTED key and saved to: {manifestPath}");

            // Parse to check signature details
            var signedDoc = JsonDocument.Parse(signedManifest);
            if (signedDoc.RootElement.TryGetProperty("signature", out var sigProp))
            {
                Console.WriteLine($"Manifest has V1.0 signature format");
                if (signedDoc.RootElement.TryGetProperty("keyId", out var keyIdProp))
                {
                    Console.WriteLine($"Signature keyId: {keyIdProp.GetString()}");
                }
            }

            // Create Script with only trusted key
            var script = new Script(SecurityPolicy.CreatePermissive());
            script.LoadKey(trustedPublicKey);
            Console.WriteLine($"\nLoaded ONLY trusted key into Script trust store");

            // Test loading the script
            Console.WriteLine($"\n=== Testing script load ===");
            try
            {
                var result = script.LoadFile(scriptPath);
                
                // If we reach here, the untrusted manifest was accepted
                Console.WriteLine($"\n*** BUG CONFIRMED ***");
                Console.WriteLine($"Script loaded successfully despite untrusted manifest signature!");
                Console.WriteLine($"Loaded value type: {result.Type}");
                
                // Try to execute it
                var execResult = script.Call(result);
                Console.WriteLine($"Execution result: {execResult}");
            }
            catch (ScriptRuntimeException ex)
            {
                Console.WriteLine($"\nGOOD: Script rejected with ScriptRuntimeException");
                Console.WriteLine($"Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nException thrown: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    static string ComputeFileHash(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }
    }
}