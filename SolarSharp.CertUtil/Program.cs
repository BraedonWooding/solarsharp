using System.CommandLine;
using SolarSharp.CertUtil.Commands;

namespace SolarSharp.CertUtil
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("SolarSharp Certificate and Manifest Utility")
            {
                GenerateCaCommand.Create(),
                IssueCertCommand.Create(),
                SignManifestCommand.Create(),
                VerifyCertCommand.Create(),
                VerifyManifestCommand.Create()
            };

            return await rootCommand.InvokeAsync(args);
        }
    }
}