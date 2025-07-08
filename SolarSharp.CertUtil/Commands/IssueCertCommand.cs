using System.CommandLine;
using System.IO.Abstractions;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace SolarSharp.CertUtil.Commands
{
    /// <summary>
    /// Represents the command for issuing a certificate from a Certificate Authority (CA).
    /// </summary>
    /// <remarks>
    /// The IssueCertCommand is a static class that provides the functionality to create and configure
    /// a command-line interface for generating a signed certificate utilizing an existing CA certificate and private key.
    /// </remarks>
    public static class IssueCertCommand
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();

        /// <summary>
        /// Creates a command for issuing a certificate from a Certificate Authority (CA).
        /// </summary>
        /// <returns>
        /// A configured <see cref="Command"/> instance for the "issue-cert" operation.
        /// </returns>
        public static Command Create()
        {
            var caCertOption = new Option<FileInfo>(
                "--ca-cert",
                description: "Path to CA certificate file"
            )
            {
                IsRequired = true,
            };

            var caKeyOption = new Option<FileInfo>(
                "--ca-key",
                description: "Path to CA private key file"
            )
            {
                IsRequired = true,
            };

            var subjectOption = new Option<string>(
                "--subject",
                description: "Certificate subject (e.g., 'CN=My Certificate, O=My Org')"
            )
            {
                IsRequired = true,
            };

            var constraintOption = new Option<string>(
                "--constraint",
                description: "Optional path constraint for the certificate"
            );

            var outputOption = new Option<string>(
                "--output",
                description: "Output filename prefix",
                getDefaultValue: () => "issued-cert"
            );

            var command = new Command("issue-cert", "Issue a certificate from a CA")
            {
                caCertOption,
                caKeyOption,
                subjectOption,
                constraintOption,
                outputOption,
            };

            command.SetHandler(
                (caCertFile, caKeyFile, subject, constraint, output) =>
                {
                    try
                    {
                        if (!_fileSystem.File.Exists(caCertFile.FullName))
                        {
                            Console.WriteLine(
                                $"Error: CA certificate file not found: {caCertFile.FullName}"
                            );
                            Environment.Exit(1);
                        }

                        if (!_fileSystem.File.Exists(caKeyFile.FullName))
                        {
                            Console.WriteLine(
                                $"Error: CA key file not found: {caKeyFile.FullName}"
                            );
                            Environment.Exit(1);
                        }

                        Console.WriteLine($"Issuing certificate with subject: {subject}");

                        var (caCert, caPrivateKey) = LoadCertificateWithKey(
                            caCertFile.FullName,
                            caKeyFile.FullName
                        );
                        var (issuedCert, issuedPrivateKey) = IssueCertificate(
                            caCert,
                            caPrivateKey,
                            subject,
                            constraint
                        );

                        var certPath = $"{output}.crt";
                        var keyPath = $"{output}.key";

                        SaveCertificate(certPath, issuedCert);
                        SavePrivateKey(keyPath, issuedPrivateKey);

                        Console.WriteLine($"✓ Certificate saved to: {certPath}");
                        Console.WriteLine($"✓ Private key saved to: {keyPath}");

                        if (!string.IsNullOrEmpty(constraint))
                        {
                            Console.WriteLine($"✓ Path constraint applied: {constraint}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error issuing certificate: {ex.Message}");
                        Environment.Exit(1);
                    }
                },
                caCertOption,
                caKeyOption,
                subjectOption,
                constraintOption,
                outputOption
            );

            return command;
        }

        /// <summary>
        /// Loads an X.509 certificate and associates it with a private key using BouncyCastle.
        /// </summary>
        /// <param name="certPath">Path to the certificate file in PEM format.</param>
        /// <param name="keyPath">Path to the private key file in PEM format.</param>
        /// <returns>A tuple containing the BouncyCastle certificate and private key.</returns>
        private static (
            X509Certificate cert,
            AsymmetricKeyParameter privateKey
        ) LoadCertificateWithKey(string certPath, string keyPath)
        {
            // Load certificate using BouncyCastle
            var certPem = _fileSystem.File.ReadAllText(certPath);
            var certParser = new X509CertificateParser();
            var cert = certParser.ReadCertificate(
                Convert.FromBase64String(
                    certPem
                        .Replace("-----BEGIN CERTIFICATE-----", "")
                        .Replace("-----END CERTIFICATE-----", "")
                        .Replace("\n", "")
                        .Replace("\r", "")
                )
            );

            // Load private key using BouncyCastle
            var keyPem = _fileSystem.File.ReadAllText(keyPath);
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

        /// Issues a new X.509 certificate signed by the provided Certificate Authority (CA) certificate using BouncyCastle.
        /// <param name="caCert">The BouncyCastle certificate of the Certificate Authority used to sign the new certificate.</param>
        /// <param name="caPrivateKey">The CA's private key for signing.</param>
        /// <param name="subject">The distinguished name (DN) of the new certificate to be issued.</param>
        /// <param name="constraint">An optional value representing a custom constraint to be added to the new certificate. If null or empty, no constraint is added.</param>
        /// <returns>A tuple containing the newly issued certificate and its private key.</returns>
        private static (X509Certificate cert, AsymmetricKeyParameter privateKey) IssueCertificate(
            X509Certificate caCert,
            AsymmetricKeyParameter caPrivateKey,
            string subject,
            string? constraint
        )
        {
            // Generate key pair for the new certificate
            var random = new SecureRandom();
            var rsaGenerator = new RsaKeyPairGenerator();
            rsaGenerator.Init(new KeyGenerationParameters(random, 2048));
            var keyPair = rsaGenerator.GenerateKeyPair();

            // Create certificate generator
            var certGenerator = new X509V3CertificateGenerator();

            // Set certificate properties
            certGenerator.SetSerialNumber(BigInteger.ValueOf(DateTime.UtcNow.Ticks));
            certGenerator.SetIssuerDN(caCert.SubjectDN);
            certGenerator.SetSubjectDN(new X509Name(subject));
            certGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter(DateTime.UtcNow.AddYears(5));
            certGenerator.SetPublicKey(keyPair.Public);

            // Add extensions
            certGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                true,
                new BasicConstraints(false)
            );

            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyAgreement)
            );

            certGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false,
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(keyPair.Public)
            );

            // Add custom constraint if provided
            if (!string.IsNullOrEmpty(constraint))
            {
                var constraintOid = new DerObjectIdentifier("1.3.6.1.4.1.37476.9000.200.100.1");
                certGenerator.AddExtension(constraintOid, false, new DerUtf8String(constraint));
            }

            // Sign the certificate
            var signatureFactory = new Asn1SignatureFactory("SHA256withRSA", caPrivateKey, random);
            var certificate = certGenerator.Generate(signatureFactory);

            return (certificate, keyPair.Private);
        }

        /// <summary>
        /// Saves a BouncyCastle X.509 certificate to a specified file path in PEM format.
        /// </summary>
        /// <param name="path">The file path where the certificate will be saved.</param>
        /// <param name="cert">The BouncyCastle X509Certificate object that represents the certificate to be saved.</param>
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
        /// <param name="privateKey">The BouncyCastle private key.</param>
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
