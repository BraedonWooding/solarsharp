using System.CommandLine;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
                description: "Name for the CA certificate")
            { IsRequired = true };

            var outputOption = new Option<DirectoryInfo>(
                "--output",
                description: "Output directory for certificates",
                getDefaultValue: static () => new DirectoryInfo(Directory.GetCurrentDirectory()));

            var command = new Command("generate-ca", "Generate a root CA certificate")
            {
                nameOption,
                outputOption
            };

            command.SetHandler(static (name, outputDir) =>
            {
                try
                {
                    Console.WriteLine($"Generating root CA: {name}");
                    
                    if (!outputDir.Exists)
                        outputDir.Create();
                    
                    var rootCa = GenerateRootCa(name);
                    var certPath = Path.Combine(outputDir.FullName, "root-ca.crt");
                    var keyPath = Path.Combine(outputDir.FullName, "root-ca.key");
                    
                    SaveCertificate(certPath, rootCa);
                    SavePrivateKey(keyPath, rootCa);
                    
                    Console.WriteLine($"✓ Root CA certificate saved to: {certPath}");
                    Console.WriteLine($"✓ Root CA private key saved to: {keyPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating CA: {ex.Message}");
                    Environment.Exit(1);
                }
            }, nameOption, outputOption);

            return command;
        }

        /// <summary>
        /// Generates a self-signed root Certificate Authority (CA) certificate.
        /// </summary>
        /// <param name="caName">The name of the CA certificate to be generated. This name will appear in the certificate's distinguished name.</param>
        /// <returns>
        /// A generated <see cref="X509Certificate2"/> object representing the root CA certificate.
        /// </returns>
        private static X509Certificate2 GenerateRootCa(string caName)
        {
            var rsa = RSA.Create(4096);
            var request = new CertificateRequest(
                $"CN={caName}, O={caName}, C=US",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Root CA extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 1, true));
            
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));

            return new X509Certificate2(certificate.Export(X509ContentType.Pfx), "", 
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }

        /// <summary>
        /// Saves the specified X.509 certificate to a file in PEM format.
        /// </summary>
        /// <param name="path">The file path where the certificate will be saved.</param>
        /// <param name="cert">The X.509 certificate to save.</param>
        private static void SaveCertificate(string path, X509Certificate2 cert)
        {
            var pemBuilder = new System.Text.StringBuilder();
            pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
            pemBuilder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            pemBuilder.AppendLine("-----END CERTIFICATE-----");
            File.WriteAllText(path, pemBuilder.ToString());
        }

        /// <summary>
        /// Saves the private key of a given X509Certificate to a specified file path in PEM format.
        /// </summary>
        /// <param name="path">The file path where the private key will be saved.</param>
        /// <param name="cert">The X509Certificate2 object containing the private key to save.</param>
        /// <exception cref="InvalidOperationException">Thrown if the certificate does not contain an RSA private key.</exception>
        private static void SavePrivateKey(string path, X509Certificate2 cert)
        {
            var rsa = cert.GetRSAPrivateKey();
            if (rsa == null)
            {
                throw new InvalidOperationException("Certificate does not contain an RSA private key");
            }
            
            // Export as PEM
            var pemBuilder = new System.Text.StringBuilder();
            pemBuilder.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
            pemBuilder.AppendLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks));
            pemBuilder.AppendLine("-----END RSA PRIVATE KEY-----");
            File.WriteAllText(path, pemBuilder.ToString());
        }
    }
}