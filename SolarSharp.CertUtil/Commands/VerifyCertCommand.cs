using System.CommandLine;
using System.Security.Cryptography.X509Certificates;

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
                description: "Path to the certificate file to verify")
            { IsRequired = true };

            var caCertOption = new Option<FileInfo>(
                "--ca-cert",
                description: "Path to the CA certificate (optional)");

            var command = new Command("verify-cert", "Verify a certificate")
            {
                certOption,
                caCertOption
            };

            command.SetHandler(async (certFile, caCertFile) =>
            {
                try
                {
                    if (!certFile.Exists)
                    {
                        Console.WriteLine($"Error: Certificate file not found: {certFile.FullName}");
                        Environment.Exit(1);
                    }

                    var certPem = await File.ReadAllTextAsync(certFile.FullName);
                    var cert = X509Certificate2.CreateFromPem(certPem);
                    
                    Console.WriteLine("Certificate Information:");
                    Console.WriteLine($"  Subject: {cert.Subject}");
                    Console.WriteLine($"  Issuer: {cert.Issuer}");
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
                    var subject = cert.Subject;
                    var parts = subject.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (!trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)) continue;
                        var cn = trimmed.Substring(3);
                        if (cn.StartsWith("/"))
                        {
                            Console.WriteLine($"  Path Constraint: {cn}");
                        }
                    }
                    
                    // Basic chain validation
                    using var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                    if (caCertFile.Exists)
                    {
                        var caCertPem = await File.ReadAllTextAsync(caCertFile.FullName);
                        var caCert = X509Certificate2.CreateFromPem(caCertPem);
                        chain.ChainPolicy.ExtraStore.Add(caCert);
                        Console.WriteLine($"Using CA certificate: {caCert.Subject}");
                    }

                    if (chain.Build(cert))
                    {
                        Console.WriteLine("✓ Certificate chain is valid");
                    }
                    else
                    {
                        Console.WriteLine("✗ Certificate chain validation failed:");
                        foreach (var status in chain.ChainStatus)
                        {
                            Console.WriteLine($"  - {status.Status}: {status.StatusInformation}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error verifying certificate: {ex.Message}");
                    Environment.Exit(1);
                }
            }, certOption, caCertOption);

            return command;
        }
    }
}