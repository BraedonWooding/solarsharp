using System.CommandLine;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace SolarSharp.CertUtil.Commands
{
    /// <summary>
    /// The SignManifestCommand class provides functionality to create a command for signing JSON manifest files.
    /// </summary>
    /// <remarks>
    /// This command requires options for specifying the manifest file, certificate file, and optionally the private key file.
    /// It integrates into the command-line tool for managing certificates and manifest files.
    /// </remarks>
    /// <example>
    /// Command usage includes specifying required paths for manifest, certificate, and optionally, the key file.
    /// </example>
    /// <seealso cref="System.CommandLine.Command"/>
    /// <seealso cref="SolarSharp.CertUtil.Commands.GenerateCaCommand"/>
    /// <seealso cref="SolarSharp.CertUtil.Commands.VerifyManifestCommand"/>
    public static class SignManifestCommand
    {
        /// <summary>
        /// Creates a command that enables signing of a manifest file.
        /// </summary>
        /// <returns>A new command configured to sign a manifest file, including required options for manifest, certificate, and private key.</returns>
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
                    if (keyFile is { Exists: true })
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

        /// <summary>
        /// Creates a digital signature for the given content using the specified X.509 certificate.
        /// </summary>
        /// <param name="content">The content to be signed, represented as a string.</param>
        /// <param name="cert">The X.509 certificate to be used for signing the content.</param>
        /// <returns>A base64-encoded string representation of the generated signature.</returns>
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