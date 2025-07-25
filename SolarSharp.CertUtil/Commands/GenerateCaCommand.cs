using System.CommandLine;
using System.IO.Abstractions;
using System.Text;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace SolarSharp.CertUtil.Commands
{
    /// <summary>
    /// Represents a command for generating a Certificate Authority (CA) certificate.
    /// </summary>
    /// <remarks>
    /// This class provides a command-line interface for creating a CA certificate.
    /// It includes options to specify the name of the certificate and the output directory for the generated files.
    /// The generated output includes a CA certificate file and a private key file.
    /// </remarks>
    public static class GenerateCaCommand
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();

        /// <summary>
        /// Creates a command to generate a Certificate Authority (CA) certificate.
        /// </summary>
        /// <returns>
        /// A <see cref="Command"/> that allows the user to generate a CA certificate.
        /// </returns>
        public static Command Create()
        {
            var nameOption = new Option<string>(
                "--name",
                description: "Name for the CA certificate"
            )
            {
                IsRequired = true,
            };

            var outputOption = new Option<DirectoryInfo>(
                "--output",
                description: "Output directory for certificates",
                getDefaultValue: () =>
                    new DirectoryInfo(_fileSystem.Directory.GetCurrentDirectory())
            );

            var command = new Command("generate-ca", "Generate a root CA certificate")
            {
                nameOption,
                outputOption,
            };

            command.SetHandler(
                (name, outputDir) =>
                {
                    try
                    {
                        Console.WriteLine($"Generating root CA: {name}");

                        if (!_fileSystem.Directory.Exists(outputDir.FullName))
                            _fileSystem.Directory.CreateDirectory(outputDir.FullName);

                        var (rootCa, privateKey) = GenerateRootCa(name);
                        var certPath = _fileSystem.Path.Combine(outputDir.FullName, "root-ca.crt");
                        var keyPath = _fileSystem.Path.Combine(outputDir.FullName, "root-ca.key");

                        SaveCertificate(certPath, rootCa);
                        SavePrivateKey(keyPath, privateKey);

                        Console.WriteLine($"✓ Root CA certificate saved to: {certPath}");
                        Console.WriteLine($"✓ Root CA private key saved to: {keyPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error generating CA: {ex.Message}");
                        Environment.Exit(1);
                    }
                },
                nameOption,
                outputOption
            );

            return command;
        }

        /// <summary>
        /// Generates a self-signed root Certificate Authority (CA) certificate using BouncyCastle.
        /// </summary>
        /// <param name="caName">The name of the CA certificate to be generated. This name will appear in the certificate's distinguished name.</param>
        /// <returns>
        /// A tuple containing the generated BouncyCastle X509Certificate and its private key.
        /// </returns>
        private static (X509Certificate cert, AsymmetricKeyParameter privateKey) GenerateRootCa(
            string caName
        )
        {
            // Generate RSA key pair using BouncyCastle
            var random = new SecureRandom();
            var rsaGenerator = new RsaKeyPairGenerator();
            rsaGenerator.Init(new KeyGenerationParameters(random, 4096));
            var keyPair = rsaGenerator.GenerateKeyPair();

            // Create certificate generator
            var certGenerator = new X509V3CertificateGenerator();

            // Set certificate properties
            var subject = new X509Name($"CN={caName}, O={caName}, C=US");
            certGenerator.SetSerialNumber(BigInteger.ValueOf(DateTime.UtcNow.Ticks));
            certGenerator.SetIssuerDN(subject); // Self-signed, so issuer = subject
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter(DateTime.UtcNow.AddYears(10));
            certGenerator.SetPublicKey(keyPair.Public);

            // Add Root CA extensions
            certGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                true,
                new BasicConstraints(true) // CA certificate with no path length constraint
            );

            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign)
            );

            certGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false,
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(keyPair.Public)
            );

            // Sign the certificate (self-signed)
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256withRSA",
                keyPair.Private,
                random
            );
            var certificate = certGenerator.Generate(signatureFactory);

            return (certificate, keyPair.Private);
        }

        /// <summary>
        /// Saves a BouncyCastle X.509 certificate to a file in PEM format.
        /// </summary>
        /// <param name="path">The file path where the certificate will be saved.</param>
        /// <param name="cert">The BouncyCastle X509Certificate to save.</param>
        private static void SaveCertificate(string path, X509Certificate cert)
        {
            var pemBuilder = new StringBuilder();
            pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
            pemBuilder.AppendLine(
                Convert.ToBase64String(cert.GetEncoded(), Base64FormattingOptions.InsertLineBreaks)
            );
            pemBuilder.AppendLine("-----END CERTIFICATE-----");
            _fileSystem.File.WriteAllText(path, pemBuilder.ToString());
        }

        /// <summary>
        /// Saves a BouncyCastle RSA private key to a file in PEM format.
        /// </summary>
        /// <param name="path">The file path where the private key will be saved.</param>
        /// <param name="privateKey">The BouncyCastle private key to save.</param>
        /// <exception cref="InvalidOperationException">Thrown if the key is not an RSA private key.</exception>
        private static void SavePrivateKey(string path, AsymmetricKeyParameter privateKey)
        {
            if (privateKey is not RsaPrivateCrtKeyParameters rsaKey)
            {
                throw new InvalidOperationException("Private key is not an RSA key");
            }

            // Convert BouncyCastle RSA key to PKCS#1 format
            var rsaPrivateKeyStructure = new RsaPrivateKeyStructure(
                rsaKey.Modulus,
                rsaKey.PublicExponent,
                rsaKey.Exponent,
                rsaKey.P,
                rsaKey.Q,
                rsaKey.DP,
                rsaKey.DQ,
                rsaKey.QInv
            );

            var pemBuilder = new StringBuilder();
            pemBuilder.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
            pemBuilder.AppendLine(
                Convert.ToBase64String(
                    rsaPrivateKeyStructure.GetEncoded(),
                    Base64FormattingOptions.InsertLineBreaks
                )
            );
            pemBuilder.AppendLine("-----END RSA PRIVATE KEY-----");
            _fileSystem.File.WriteAllText(path, pemBuilder.ToString());
        }
    }
}
