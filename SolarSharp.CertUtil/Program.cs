using System.CommandLine;
using SolarSharp.CertUtil.Commands;

namespace SolarSharp.CertUtil
{
    /// <summary>
    /// Represents the entry point for certificate utilities.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Entry point of the SolarSharp Certificate and Manifest Utility application.
        /// Initializes and creates a root command with subcommands for generating a CA,
        /// issuing certificates, signing manifests, and verifying certificates or manifests.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application.</param>
        /// <returns>A task representing the asynchronous operation; the result contains the exit code
        /// of the application.</returns>
        private static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("SolarSharp Certificate and Manifest Utility")
            {
                GenerateCaCommand.Create(),
                IssueCertCommand.Create(),
                SignManifestCommand.Create(),
                VerifyCertCommand.Create(),
                VerifyManifestCommand.Create(),
            };

            return await rootCommand.InvokeAsync(args);
        }
    }
}
