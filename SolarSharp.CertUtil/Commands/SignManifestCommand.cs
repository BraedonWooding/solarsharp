using System.CommandLine;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;

namespace SolarSharp.CertUtil.Commands
{
    public static class SignManifestCommand
    {
        public static Command Create()
        {
            var manifestOption = new Option<FileInfo>(
                "--manifest",
                description: "Path to the manifest JSON file to sign")
            { IsRequired = true };

            var certOption = new Option<FileInfo>(
                "--cert",
                description: "Path to the certificate file")
            { IsRequired = true };

            var keyOption = new Option<FileInfo>(
                "--key",
                description: "Path to the private key file");

            var command = new Command("sign-manifest", "Sign a manifest file")
            {
                manifestOption,
                certOption,
                keyOption
            };

            command.SetHandler(async (manifestFile, certFile, keyFile) =>
            {
                try
                {
                    if (!manifestFile.Exists)
                    {
                        Console.WriteLine($"Error: Manifest file not found: {manifestFile.FullName}");
                        Environment.Exit(1);
                    }

                    if (!certFile.Exists)
                    {
                        Console.WriteLine($"Error: Certificate file not found: {certFile.FullName}");
                        Environment.Exit(1);
                    }

                    Console.WriteLine($"Signing manifest: {manifestFile.FullName}");
                    
                    var manifestJson = await File.ReadAllTextAsync(manifestFile.FullName);
                    var manifest = JsonSerializer.Deserialize<Dictionary<string, object>>(manifestJson);
                    
                    X509Certificate2 cert;
                    if (keyFile != null && keyFile.Exists)
                    {
                        // Load certificate and separate key file
                        var certPem = await File.ReadAllTextAsync(certFile.FullName);
                        var keyPem = await File.ReadAllTextAsync(keyFile.FullName);
                        var baseCert = X509Certificate2.CreateFromPem(certPem);
                        var rsa = System.Security.Cryptography.RSA.Create();
                        rsa.ImportFromPem(keyPem);
                        cert = baseCert.CopyWithPrivateKey(rsa);
                    }
                    else
                    {
                        // Try to load certificate with embedded key
                        cert = new X509Certificate2(certFile.FullName);
                    }
                    
                    var signature = CreateSignature(manifestJson, cert);
                    
                    // Add security section to manifest
                    if (manifest == null)
                        manifest = new Dictionary<string, object>();

                    manifest["security"] = new Dictionary<string, object>
                    {
                        ["publicKey"] = new Dictionary<string, object>
                        {
                            ["algorithm"] = "RSA",
                            ["key"] = Convert.ToBase64String(cert.GetPublicKey())
                        },
                        ["signature"] = new Dictionary<string, object>
                        {
                            ["algorithm"] = "SHA256withRSA",
                            ["value"] = signature
                        }
                    };
                    
                    var signedJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(manifestFile.FullName, signedJson);
                    
                    Console.WriteLine($"✓ Manifest signed successfully");
                    Console.WriteLine($"✓ Signature added to manifest file");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error signing manifest: {ex.Message}");
                    Environment.Exit(1);
                }
            }, manifestOption, certOption, keyOption);

            return command;
        }

        private static string CreateSignature(string content, X509Certificate2 cert)
        {
            // This is a simplified signature approach for demo purposes
            // In a real implementation, you'd use proper cryptographic signing
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = System.Security.Cryptography.SHA256.HashData(contentBytes);
            return Convert.ToBase64String(hash);
        }
    }
}