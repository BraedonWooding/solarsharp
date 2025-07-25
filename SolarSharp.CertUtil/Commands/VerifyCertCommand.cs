using System.CommandLine;
using System.IO.Abstractions;
using Org.BouncyCastle.X509;

namespace SolarSharp.CertUtil.Commands
{
    /// <summary>
    /// Provides a command to verify a certificate.
    /// </summary>
    /// <remarks>
    /// This command validates the authenticity of a given certificate file, with an optional
    /// CA (Certificate Authority) certificate for additional validation.
    /// </remarks>
    public static class VerifyCertCommand
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();

        /// <summary>
        /// Creates a command for verifying a certificate within the command-line utility.
        /// </summary>
        /// <returns>
        /// A <see cref="Command"/> configured for verifying certificates with required options.
        /// </returns>
        public static Command Create()
        {
            var certOption = new Option<FileInfo>(
                "--cert",
                description: "Path to the certificate file to verify"
            )
            {
                IsRequired = true,
            };

            var caCertOption = new Option<FileInfo>(
                "--ca-cert",
                description: "Path to the CA certificate (optional)"
            );

            var command = new Command("verify-cert", "Verify a certificate")
            {
                certOption,
                caCertOption,
            };

            command.SetHandler(
                async (certFile, caCertFile) =>
                {
                    try
                    {
                        if (!_fileSystem.File.Exists(certFile.FullName))
                        {
                            Console.WriteLine(
                                $"Error: Certificate file not found: {certFile.FullName}"
                            );
                            Environment.Exit(1);
                        }

                        var certPem = await _fileSystem.File.ReadAllTextAsync(certFile.FullName);
                        var cert = ParseCertificateFromPem(certPem);

                        Console.WriteLine("Certificate Information:");
                        Console.WriteLine($"  Subject: {cert.SubjectDN}");
                        Console.WriteLine($"  Issuer: {cert.IssuerDN}");
                        Console.WriteLine($"  Valid From: {cert.NotBefore}");
                        Console.WriteLine($"  Valid To: {cert.NotAfter}");
                        Console.WriteLine($"  Serial Number: {cert.SerialNumber}");

                        // Check validity period
                        var now = DateTime.Now;
                        if (now < cert.NotBefore)
                        {
                            Console.WriteLine("✗ Certificate is not yet valid");
                        }
                        else if (now > cert.NotAfter)
                        {
                            Console.WriteLine("✗ Certificate has expired");
                        }
                        else
                        {
                            Console.WriteLine("✓ Certificate is currently valid");
                        }

                        // Extract path constraint from CN if present
                        var subject = cert.SubjectDN.ToString();
                        var parts = subject.Split(',');
                        foreach (var part in parts)
                        {
                            var trimmed = part.Trim();
                            if (!trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                                continue;
                            var cn = trimmed.Substring(3);
                            if (cn.StartsWith("/"))
                            {
                                Console.WriteLine($"  Path Constraint: {cn}");
                            }
                        }

                        // Basic chain validation using BouncyCastle
                        var isChainValid = false;
                        X509Certificate? caCert = null;

                        if (caCertFile != null && _fileSystem.File.Exists(caCertFile.FullName))
                        {
                            var caCertPem = await _fileSystem.File.ReadAllTextAsync(
                                caCertFile.FullName
                            );
                            caCert = ParseCertificateFromPem(caCertPem);
                            Console.WriteLine($"Using CA certificate: {caCert.SubjectDN}");

                            isChainValid = ValidateCertificateChain(cert, caCert);
                        }
                        else
                        {
                            // Self-signed certificate validation
                            isChainValid = ValidateSelfSignedCertificate(cert);
                        }

                        if (isChainValid)
                        {
                            Console.WriteLine("✓ Certificate chain is valid");
                        }
                        else
                        {
                            Console.WriteLine("✗ Certificate chain validation failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error verifying certificate: {ex.Message}");
                        Environment.Exit(1);
                    }
                },
                certOption,
                caCertOption
            );

            return command;
        }

        /// <summary>
        /// Parses a BouncyCastle X.509 certificate from PEM format.
        /// </summary>
        /// <param name="pemContent">The PEM-formatted certificate content.</param>
        /// <returns>A BouncyCastle X509Certificate object.</returns>
        private static X509Certificate ParseCertificateFromPem(string pemContent)
        {
            var parser = new X509CertificateParser();
            var base64Content = pemContent
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var certBytes = Convert.FromBase64String(base64Content);
            return parser.ReadCertificate(certBytes);
        }

        /// <summary>
        /// Validates a certificate chain using BouncyCastle.
        /// </summary>
        /// <param name="cert">The certificate to validate.</param>
        /// <param name="caCert">The CA certificate to validate against.</param>
        /// <returns>True if the certificate chain is valid, false otherwise.</returns>
        private static bool ValidateCertificateChain(X509Certificate cert, X509Certificate caCert)
        {
            try
            {
                // Verify that the certificate was signed by the CA
                cert.Verify(caCert.GetPublicKey());

                // Check if the certificate is issued by this CA
                if (!cert.IssuerDN.Equals(caCert.SubjectDN))
                {
                    return false;
                }

                // Verify CA certificate is valid for the current time
                var now = DateTime.UtcNow;
                if (
                    now < caCert.NotBefore.ToUniversalTime()
                    || now > caCert.NotAfter.ToUniversalTime()
                )
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Validates a self-signed certificate using BouncyCastle.
        /// </summary>
        /// <param name="cert">The self-signed certificate to validate.</param>
        /// <returns>True if the certificate is valid, false otherwise.</returns>
        private static bool ValidateSelfSignedCertificate(X509Certificate cert)
        {
            try
            {
                // Verify self-signed certificate
                cert.Verify(cert.GetPublicKey());

                // Check if issuer equals subject (self-signed)
                return cert.IssuerDN.Equals(cert.SubjectDN);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
