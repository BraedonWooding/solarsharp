using System.CommandLine;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;

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
        private static readonly IFileSystem _fileSystem = new FileSystem();

        /// <summary>
        /// Creates a command that enables signing of a manifest file.
        /// </summary>
        /// <returns>A new command configured to sign a manifest file, including required options for manifest, certificate, and private key.</returns>
        public static Command Create()
        {
            var manifestOption = new Option<FileInfo>(
                "--manifest",
                description: "Path to the manifest JSON file to sign"
            )
            {
                IsRequired = true,
            };

            var certOption = new Option<FileInfo>(
                "--cert",
                description: "Path to the certificate file"
            )
            {
                IsRequired = true,
            };

            var keyOption = new Option<FileInfo>(
                "--key",
                description: "Path to the private key file"
            );

            var command = new Command("sign-manifest", "Sign a manifest file")
            {
                manifestOption,
                certOption,
                keyOption,
            };

            command.SetHandler(
                async (manifestFile, certFile, keyFile) =>
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

                        if (!_fileSystem.File.Exists(certFile.FullName))
                        {
                            Console.WriteLine(
                                $"Error: Certificate file not found: {certFile.FullName}"
                            );
                            Environment.Exit(1);
                        }

                        Console.WriteLine($"Signing manifest: {manifestFile.FullName}");

                        var manifestJson = await _fileSystem.File.ReadAllTextAsync(
                            manifestFile.FullName
                        );
                        var manifest = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            manifestJson
                        );

                        X509Certificate cert;
                        AsymmetricKeyParameter privateKey;

                        if (keyFile != null && _fileSystem.File.Exists(keyFile.FullName))
                        {
                            // Load certificate and separate key file
                            var certPem = await _fileSystem.File.ReadAllTextAsync(
                                certFile.FullName
                            );
                            var keyPem = await _fileSystem.File.ReadAllTextAsync(keyFile.FullName);

                            (cert, privateKey) = LoadCertificateAndKey(certPem, keyPem);
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "Private key file is required for signing. Use --key option."
                            );
                        }

                        var signature = CreateSignature(manifestJson, privateKey);

                        // Add security section to manifest
                        if (manifest == null)
                            manifest = new Dictionary<string, object>();

                        manifest["security"] = new Dictionary<string, object>
                        {
                            ["publicKey"] = new Dictionary<string, object>
                            {
                                ["algorithm"] = "RSA",
                                ["value"] = ExportPublicKeyAsPem(cert),
                                ["format"] = "PEM",
                            },
                            ["signature"] = new Dictionary<string, object>
                            {
                                ["algorithm"] = "SHA256withRSA",
                                ["value"] = signature,
                            },
                        };

                        var signedJson = JsonSerializer.Serialize(
                            manifest,
                            new JsonSerializerOptions { WriteIndented = true }
                        );
                        await _fileSystem.File.WriteAllTextAsync(manifestFile.FullName, signedJson);

                        Console.WriteLine("✓ Manifest signed successfully");
                        Console.WriteLine("✓ Signature added to manifest file");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error signing manifest: {ex.Message}");
                        Environment.Exit(1);
                    }
                },
                manifestOption,
                certOption,
                keyOption
            );

            return command;
        }

        /// <summary>
        /// Loads a BouncyCastle certificate and private key from PEM content.
        /// </summary>
        /// <param name="certPem">The certificate PEM content.</param>
        /// <param name="keyPem">The private key PEM content.</param>
        /// <returns>A tuple containing the certificate and private key.</returns>
        private static (
            X509Certificate cert,
            AsymmetricKeyParameter privateKey
        ) LoadCertificateAndKey(string certPem, string keyPem)
        {
            // Parse certificate
            var parser = new X509CertificateParser();
            var base64Cert = certPem
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();
            var certBytes = Convert.FromBase64String(base64Cert);
            var cert = parser.ReadCertificate(certBytes);

            // Parse private key
            var keyReader = new PemReader(new StringReader(keyPem));
            var keyObj = keyReader.ReadObject();

            AsymmetricKeyParameter privateKey;
            if (keyObj is AsymmetricCipherKeyPair keyPair)
            {
                privateKey = keyPair.Private;
            }
            else if (keyObj is AsymmetricKeyParameter key)
            {
                privateKey = key;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported key format: {keyObj?.GetType().Name}"
                );
            }

            return (cert, privateKey);
        }

        /// <summary>
        /// Exports a BouncyCastle certificate's public key as PEM format.
        /// </summary>
        /// <param name="cert">The BouncyCastle certificate.</param>
        /// <returns>PEM-formatted public key string.</returns>
        private static string ExportPublicKeyAsPem(X509Certificate cert)
        {
            var publicKeyInfo = cert.CertificateStructure.SubjectPublicKeyInfo;
            var publicKeyBytes = publicKeyInfo.GetEncoded();

            var pemBuilder = new StringBuilder();
            pemBuilder.AppendLine("-----BEGIN PUBLIC KEY-----");
            pemBuilder.AppendLine(
                Convert.ToBase64String(publicKeyBytes, Base64FormattingOptions.InsertLineBreaks)
            );
            pemBuilder.AppendLine("-----END PUBLIC KEY-----");

            return pemBuilder.ToString();
        }

        /// <summary>
        /// Creates a digital signature for the given content using BouncyCastle RSA signing.
        /// </summary>
        /// <param name="content">The content to be signed, represented as a string.</param>
        /// <param name="privateKey">The BouncyCastle private key for signing.</param>
        /// <returns>A base64-encoded string representation of the generated signature.</returns>
        private static string CreateSignature(string content, AsymmetricKeyParameter privateKey)
        {
            if (privateKey is not RsaPrivateCrtKeyParameters)
            {
                throw new InvalidOperationException(
                    "Only RSA private keys are supported for signing"
                );
            }

            // Create RSA signer with SHA256
            var signer = new RsaDigestSigner(new Sha256Digest());
            signer.Init(true, privateKey); // true for signing

            // Sign the content
            var contentBytes = Encoding.UTF8.GetBytes(content);
            signer.BlockUpdate(contentBytes, 0, contentBytes.Length);
            var signature = signer.GenerateSignature();

            return Convert.ToBase64String(signature);
        }
    }
}
