using System;
using System.CommandLine;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Cli.Commands
{
    /// <summary>
    /// Command to scan directories for manifest files
    /// </summary>
    public class ScanManifestsCommand : Command
    {
        public ScanManifestsCommand()
            : base("scan-manifests", "Recursively scan directories for SolarSharp manifest files")
        {
            var pathArgument = new Argument<string>(
                "path",
                getDefaultValue: () => ".",
                description: "Directory path to scan"
            );

            var maxDepthOption = new Option<int?>(
                new[] { "--max-depth", "-d" },
                description: "Maximum directory depth to scan"
            );

            var validateOption = new Option<bool>(
                new[] { "--validate", "-v" },
                getDefaultValue: () => true,
                description: "Validate manifests after loading"
            );

            var requireSignatureOption = new Option<bool>(
                new[] { "--require-signature", "-s" },
                getDefaultValue: () => false,
                description: "Require all manifests to be signed"
            );

            var showErrorsOption = new Option<bool>(
                new[] { "--show-errors", "-e" },
                getDefaultValue: () => true,
                description: "Show manifests that failed to load"
            );

            var verboseOption = new Option<bool>(
                new[] { "--verbose" },
                getDefaultValue: () => false,
                description: "Show detailed information"
            );

            AddArgument(pathArgument);
            AddOption(maxDepthOption);
            AddOption(validateOption);
            AddOption(requireSignatureOption);
            AddOption(showErrorsOption);
            AddOption(verboseOption);

            this.SetHandler(
                ExecuteAsync,
                pathArgument,
                maxDepthOption,
                validateOption,
                requireSignatureOption,
                showErrorsOption,
                verboseOption
            );
        }

        private async Task<int> ExecuteAsync(
            string path,
            int? maxDepth,
            bool validate,
            bool requireSignature,
            bool showErrors,
            bool verbose
        )
        {
            try
            {
                // Resolve path
                var fullPath = Path.GetFullPath(path);

                if (!Directory.Exists(fullPath))
                {
                    Console.Error.WriteLine($"Error: Directory not found: {fullPath}");
                    return 1;
                }

                Console.WriteLine($"Scanning {fullPath} for manifest files...");

                // Configure scan options
                var options = new ManifestScanOptions
                {
                    MaxDepth = maxDepth,
                    ValidateManifests = validate,
                    RequireSignature = requireSignature,
                    IncludeErrors = showErrors,
                    RootPath = fullPath,
                };

                // Create scanner and scan
                var fileSystem = new FileSystem();
                var scanner = new ManifestScanner(fileSystem);
                var results = await scanner.ScanDirectoryAsync(fullPath, options);

                // Display results
                Console.WriteLine($"\nFound {results.Count} manifest(s):\n");

                var successCount = 0;
                var errorCount = 0;

                foreach (var result in results.OrderBy(r => r.RelativePath ?? r.FilePath))
                {
                    if (result.IsSuccess)
                    {
                        successCount++;
                        DisplaySuccessResult(result, verbose);
                    }
                    else if (showErrors)
                    {
                        errorCount++;
                        DisplayErrorResult(result, verbose);
                    }
                }

                // Summary
                Console.WriteLine("\nSummary:");
                Console.WriteLine($"  Successful: {successCount}");
                if (showErrors)
                    Console.WriteLine($"  Failed: {errorCount}");
                Console.WriteLine($"  Total: {results.Count}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private void DisplaySuccessResult(ManifestScanResult result, bool verbose)
        {
            var manifest = result.Manifest;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✓ ");
            Console.ResetColor();

            Console.Write($"{result.RelativePath ?? result.FilePath}");

            // V2.0: Display first package info
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            if (firstPackage.Package != null)
            {
                var name = firstPackage.Package.Metadata.Name;
                var version = firstPackage.Package.Metadata.Version;
                if (!string.IsNullOrEmpty(name))
                {
                    Console.Write($" [{name} v{version}]");
                }
            }

            if (manifest.IsSigned())
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(" [Signed]");
                Console.ResetColor();
            }

            Console.WriteLine();

            if (verbose)
            {
                // Show V2.0 signed content information
                if (manifest.SignedContent.Length > 0)
                {
                    Console.WriteLine(
                        $"    Signed content blocks: {manifest.SignedContent.Length}"
                    );
                    foreach (var block in manifest.SignedContent)
                    {
                        Console.WriteLine($"      - Key ID: {block.KeyId}");
                        Console.WriteLine($"        Packages: {block.Packages.Count}");
                        Console.WriteLine($"        Policies: {block.Policies.Length}");
                    }
                }

                // Show packages and files
                var allPackages = manifest.GetAllPackages().ToList();
                if (allPackages.Count > 0)
                {
                    Console.WriteLine($"    Packages: {allPackages.Count}");
                    foreach (var (packageId, package, keyId) in allPackages)
                    {
                        Console.WriteLine($"      - {packageId} (signed by {keyId})");
                        Console.WriteLine($"        Files: {package.Files.Count}");
                    }
                }

                Console.WriteLine();
            }
        }

        private void DisplayErrorResult(ManifestScanResult result, bool verbose)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("✗ ");
            Console.ResetColor();

            Console.Write($"{result.RelativePath ?? result.FilePath}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($" [{result.Error.ErrorType}]");
            Console.ResetColor();

            Console.WriteLine($": {result.Error.Message}");

            if (verbose && result.ValidationErrors?.Count > 0)
            {
                Console.WriteLine("    Validation errors:");
                foreach (var error in result.ValidationErrors)
                {
                    Console.WriteLine($"      - {error}");
                }
            }
        }
    }
}
