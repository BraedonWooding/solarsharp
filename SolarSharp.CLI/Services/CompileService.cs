using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.CLI.Services
{
    /// <summary>
    /// Service implementation for compiling Lua scripts to bytecode.
    /// Handles script compilation and bytecode serialization.
    /// </summary>
    public class CompileService : ICompileService
    {
        private readonly ILogger<CompileService> _logger;
        private readonly IScriptFactory _scriptFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompileService"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="scriptFactory">Factory for creating script instances.</param>
        public CompileService(
            ILogger<CompileService> logger,
            IScriptFactory scriptFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scriptFactory = scriptFactory ?? throw new ArgumentNullException(nameof(scriptFactory));
        }

        /// <summary>
        /// Compiles a Lua script to bytecode asynchronously.
        /// Debug information is always stripped from bytecode dumps.
        /// </summary>
        /// <param name="inputPath">Path to the input Lua script.</param>
        /// <param name="outputPath">Path for the output bytecode file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CompileAsync(
            string inputPath, 
            string outputPath, 
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputPath))
                throw new ArgumentNullException(nameof(inputPath));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            _logger.LogDebug("Compiling script: {InputPath} to {OutputPath}", inputPath, outputPath);

            // Validate input file exists
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Input script not found: {inputPath}");
            }

            try
            {
                await Task.Run(() => CompileScript(inputPath, outputPath), cancellationToken);
                
                _logger.LogInformation("Successfully compiled {InputPath} to {OutputPath}", 
                    Path.GetFileName(inputPath), Path.GetFileName(outputPath));
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Compilation cancelled");
                
                // Clean up partial output file if it exists
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up partial output file");
                    }
                }
                
                throw;
            }
        }

        /// <summary>
        /// Performs the actual script compilation.
        /// Debug information is always stripped from bytecode dumps.
        /// </summary>
        /// <param name="inputPath">Path to the input script.</param>
        /// <param name="outputPath">Path for the output bytecode.</param>
        private void CompileScript(string inputPath, string outputPath)
        {
            // Create a minimal script instance for compilation
            // We use the most restrictive security since we're only compiling
            var script = _scriptFactory.Create(SecurityConfiguration.Isolated());

            try
            {
                // Load and compile the script
                _logger.LogDebug("Loading script from {InputPath}", inputPath);
                var sourceCode = File.ReadAllText(inputPath);
                
                // Compile to function
                var func = script.LoadString(sourceCode, null, inputPath);
                
                if (func == null || func.Function == null)
                {
                    throw new InvalidOperationException("Failed to compile script - no function produced");
                }

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    _logger.LogDebug("Creating output directory: {OutputDir}", outputDir);
                    Directory.CreateDirectory(outputDir);
                }

                // Save bytecode to file
                using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    script.Dump(func, stream);
                }

                // Log compilation statistics
                var inputSize = new FileInfo(inputPath).Length;
                var outputSize = new FileInfo(outputPath).Length;
                var compressionRatio = (1.0 - (double)outputSize / inputSize) * 100;
                
                _logger.LogInformation(
                    "Compilation complete: {InputSize} bytes -> {OutputSize} bytes ({CompressionRatio:F1}% reduction)",
                    inputSize, outputSize, compressionRatio);
                
                // Debug information is always stripped from bytecode dumps in SolarSharp
                _logger.LogInformation("Bytecode saved (debug information stripped)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile script");
                throw new InvalidOperationException($"Compilation failed: {ex.Message}", ex);
            }
        }
    }
}