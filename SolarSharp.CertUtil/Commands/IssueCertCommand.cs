using System.CommandLine;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
                description: "Path to CA certificate file")
            { IsRequired = true };

            var caKeyOption = new Option<FileInfo>(
                "--ca-key", 
                description: "Path to CA private key file")
            { IsRequired = true };

            var subjectOption = new Option<string>(
                "--subject",
                description: "Certificate subject (e.g., 'CN=My Certificate, O=My Org')")
            { IsRequired = true };

            var constraintOption = new Option<string>(
                "--constraint",
                description: "Optional path constraint for the certificate");

            var outputOption = new Option<string>(
                "--output",
                description: "Output filename prefix",
                getDefaultValue: () => "issued-cert");

            var command = new Command("issue-cert", "Issue a certificate from a CA")
            {
                caCertOption,
                caKeyOption,
                subjectOption,
                constraintOption,
                outputOption
            };

            command.SetHandler((caCertFile, caKeyFile, subject, constraint, output) =>
            {
                try
                {
                    if (!caCertFile.Exists)
                    {
                        Console.WriteLine($"Error: CA certificate file not found: {caCertFile.FullName}");
                        Environment.Exit(1);
                    }

                    if (!caKeyFile.Exists)
                    {
                        Console.WriteLine($"Error: CA key file not found: {caKeyFile.FullName}");
                        Environment.Exit(1);
                    }

                    Console.WriteLine($"Issuing certificate with subject: {subject}");
                    
                    var caCert = LoadCertificateWithKey(caCertFile.FullName, caKeyFile.FullName);
                    var issuedCert = IssueCertificate(caCert, subject, constraint);
                    
                    var certPath = $"{output}.crt";
                    var keyPath = $"{output}.key";
                    
                    SaveCertificate(certPath, issuedCert);
                    SavePrivateKey(keyPath, issuedCert);
                    
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
            }, caCertOption, caKeyOption, subjectOption, constraintOption, outputOption);

            return command;
        }

        /// <summary>
        /// Loads an X.509 certificate and associates it with a private key.
        /// </summary>
        /// <param name="certPath">Path to the certificate file in PEM format.</param>
        /// <param name="keyPath">Path to the private key file in PEM format.</param>
        /// <returns>An X509Certificate2 instance with the private key loaded.</returns>
        private static X509Certificate2 LoadCertificateWithKey(string certPath, string keyPath)
        {
            // Load certificate
            var certPem = File.ReadAllText(certPath);
            var cert = X509Certificate2.CreateFromPem(certPem);
            
            // Load private key
            var keyPem = File.ReadAllText(keyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(keyPem);
            
            return cert.CopyWithPrivateKey(rsa);
        }

        /// Issues a new X.509 certificate signed by the provided Certificate Authority (CA) certificate.
        /// <param name="caCert">The certificate of the Certificate Authority used to sign the new certificate.</param>
        /// <param name="subject">The distinguished name (DN) of the new certificate to be issued.</param>
        /// <param name="constraint">An optional value representing a custom constraint to be added to the new certificate. If null or empty, no constraint is added.</param>
        /// <returns>The newly issued X.509 certificate with the private key.</returns>
        private static X509Certificate2 IssueCertificate(X509Certificate2 caCert, string subject, string? constraint)
        {
            var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                subject,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Certificate extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
            
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement,
                    true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            // Add custom constraint if provided
            if (!string.IsNullOrEmpty(constraint))
            {
                var oid = new Oid("1.3.6.1.4.1.37476.9000.200.100.1", "PathConstraint");
                request.CertificateExtensions.Add(
                    new X509Extension(oid, System.Text.Encoding.UTF8.GetBytes(constraint), false));
            }

            var certificate = request.Create(
                caCert,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(5),
                Guid.NewGuid().ToByteArray());

            return certificate.CopyWithPrivateKey(rsa);
        }

        /// <summary>
        /// Saves a given X509Certificate2 object to a specified file path in PEM format.
        /// </summary>
        /// <param name="path">The file path where the certificate will be saved.</param>
        /// <param name="cert">The X509Certificate2 object that represents the certificate to be saved.</param>
        private static void SaveCertificate(string path, X509Certificate2 cert)
        {
            var pemBuilder = new System.Text.StringBuilder();
            pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
            pemBuilder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            pemBuilder.AppendLine("-----END CERTIFICATE-----");
            File.WriteAllText(path, pemBuilder.ToString());
        }

        /// <summary>
        /// Saves the RSA private key from a given certificate to a file in PEM format.
        /// </summary>
        /// <param name="path">The file path where the private key will be saved.</param>
        /// <param name="cert">The certificate containing the RSA private key.</param>
        /// <exception cref="InvalidOperationException">Thrown if the certificate does not contain an RSA private key.</exception>
        private static void SavePrivateKey(string path, X509Certificate2 cert)
        {
            var rsa = cert.GetRSAPrivateKey();
            if (rsa == null)
            {
                throw new InvalidOperationException("Certificate does not contain an RSA private key");
            }
            
            var pemBuilder = new System.Text.StringBuilder();
            pemBuilder.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
            pemBuilder.AppendLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks));
            pemBuilder.AppendLine("-----END RSA PRIVATE KEY-----");
            File.WriteAllText(path, pemBuilder.ToString());
        }
    }
}