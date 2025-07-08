using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;

namespace SolarSharp.CertUtil.Commands
{
    /// <summary>
    /// Represents the command for verifying the signature of a manifest JSON file.
    /// </summary>
    /// <remarks>
    /// This static class is part of the command-line interface for providing functionality to verify the
    /// integrity and signature of a manifest JSON file. The command is integrated into the main tool as one of the
    /// supported operations for certificate and manifest management.
    /// </remarks>
    /// <seealso cref="System.CommandLine.Command"/>
    /// <seealso cref="SolarSharp.CertUtil.Commands.SignManifestCommand"/>
    /// <seealso cref="SolarSharp.CertUtil.Commands.GenerateCaCommand"/>
    public static class VerifyManifestCommand
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();

        /// <summary>
        /// Creates and returns a command for verifying a manifest signature.
        /// The command expects a required option to specify the path to the manifest
        /// JSON file that needs to be verified.
        /// </summary>
        /// <returns>
        /// A <see cref="System.CommandLine.Command"/> configured for verifying a manifest signature.
        /// </returns>
        public static Command Create()
        {
            var manifestOption = new Option<FileInfo>(
                "--manifest",
                description: "Path to the manifest JSON file to verify"
            )
            {
                IsRequired = true,
            };

            var command = new Command("verify-manifest", "Verify a manifest signature")
            {
                manifestOption,
            };

            command.SetHandler(
                async manifestFile =>
                {
                    try
                    {
                        if (!_fileSystem.File.Exists(manifestFile.FullName))
                        {
                            Console.WriteLine(
                                $"Error: Manifest file not found: {manifestFile.FullName}"
                            );
                            Environment.Exit(1);
                        }

                        var manifestJson = await _fileSystem.File.ReadAllTextAsync(
                            manifestFile.FullName
                        );
                        var manifest = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            manifestJson
                        );

                        Console.WriteLine("Manifest Information:");

                        // Basic manifest info
                        if (manifest?.TryGetValue("name", out var name) == true)
                            Console.WriteLine($"  Name: {name}");

                        if (manifest?.TryGetValue("version", out var version) == true)
                            Console.WriteLine($"  Version: {version}");

                        if (manifest?.TryGetValue("author", out var author) == true)
                            Console.WriteLine($"  Author: {author}");

                        // Policy information
                        if (manifest?.TryGetValue("policy", out var policyObj) == true)
                        {
                            var policy = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                policyObj.ToString()!
                            );
                            Console.WriteLine("  Policy:");

                            if (policy?.TryGetValue("timeout", out var timeout) == true)
                                Console.WriteLine($"    Timeout: {timeout} seconds");

                            if (policy?.TryGetValue("memoryLimit", out var memoryLimit) == true)
                                Console.WriteLine($"    Memory Limit: {memoryLimit} MB");

                            if (policy?.TryGetValue("allowedModules", out var modulesObj) == true)
                            {
                                var modules = JsonSerializer.Deserialize<string[]>(
                                    modulesObj.ToString()!
                                );
                                Console.WriteLine(
                                    $"    Modules: {string.Join(", ", modules ?? [])}"
                                );
                            }

                            if (
                                policy?.TryGetValue("capabilities", out var capabilitiesObj) == true
                            )
                            {
                                var capabilities = JsonSerializer.Deserialize<string[]>(
                                    capabilitiesObj.ToString()!
                                );
                                Console.WriteLine(
                                    $"    Capabilities: {string.Join(", ", capabilities ?? [])}"
                                );
                            }
                        }

                        // Security information
                        if (manifest?.TryGetValue("security", out var securityObj) == true)
                        {
                            var security = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                securityObj.ToString()!
                            );

                            if (security?.TryGetValue("signature", out var signatureObj) == true)
                            {
                                var signature = JsonSerializer.Deserialize<
                                    Dictionary<string, object>
                                >(signatureObj.ToString()!);

                                if (signature?.TryGetValue("algorithm", out var algorithm) == true)
                                    Console.WriteLine($"  Signature Algorithm: {algorithm}");

                                Console.WriteLine("  ✓ Signature Present: Yes");

                                // In a real implementation, you would verify the signature here
                                Console.WriteLine(
                                    "  Note: Signature verification not implemented in demo"
                                );
                            }
                            else
                            {
                                Console.WriteLine("  ✗ Signature Present: No");
                            }

                            if (security?.TryGetValue("publicKey", out var publicKeyObj) == true)
                            {
                                var publicKey = JsonSerializer.Deserialize<
                                    Dictionary<string, object>
                                >(publicKeyObj.ToString()!);

                                if (
                                    publicKey?.TryGetValue("algorithm", out var keyAlgorithm)
                                    == true
                                )
                                    Console.WriteLine($"  Public Key Algorithm: {keyAlgorithm}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("  ✗ No security section found");
                        }

                        Console.WriteLine("✓ Manifest parsing completed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error verifying manifest: {ex.Message}");
                        Environment.Exit(1);
                    }
                },
                manifestOption
            );

            return command;
        }
    }
}
