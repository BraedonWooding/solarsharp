using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SolarSharp.Hardwire;
using SolarSharp.Hardwire.Languages;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.CLI.Services
{
    /// <summary>
    /// Service implementation for hardwire code generation.
    /// Converts compiled Lua bytecode into C# or VB.NET source code.
    /// </summary>
    public class HardwireService : IHardwireService
    {
        private readonly ILogger<HardwireService> _logger;
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="HardwireService"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="fileSystem">File system abstraction.</param>
        public HardwireService(ILogger<HardwireService> logger, IFileSystem fileSystem)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Generates C# or VB.NET code from compiled Lua bytecode.
        /// </summary>
        /// <param name="inputPath">Path to the input bytecode file.</param>
        /// <param name="outputPath">Path for the output source code file.</param>
        /// <param name="namespaceName">Namespace for the generated code.</param>
        /// <param name="className">Class name for the generated code.</param>
        /// <param name="language">Target language (cs or vb).</param>
        /// <param name="includeInternals">Whether to include internal SolarSharp types.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task GenerateAsync(
            string inputPath,
            string outputPath,
            string namespaceName,
            string className,
            string language,
            bool includeInternals,
            CancellationToken cancellationToken
        )
        {
            if (string.IsNullOrEmpty(inputPath))
                throw new ArgumentNullException(nameof(inputPath));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));
            if (string.IsNullOrEmpty(namespaceName))
                throw new ArgumentNullException(nameof(namespaceName));
            if (string.IsNullOrEmpty(className))
                throw new ArgumentNullException(nameof(className));

            _logger.LogDebug(
                "Generating {Language} code from {InputPath}",
                language.ToUpperInvariant(),
                inputPath
            );

            // Validate input file exists
            if (!_fileSystem.File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Input bytecode file not found: {inputPath}");
            }

            try
            {
                await Task.Run(
                    () =>
                        GenerateCode(
                            inputPath,
                            outputPath,
                            namespaceName,
                            className,
                            language,
                            includeInternals
                        ),
                    cancellationToken
                );

                _logger.LogInformation(
                    "Successfully generated {Language} code to {OutputPath}",
                    language.ToUpperInvariant(),
                    _fileSystem.Path.GetFileName(outputPath)
                );
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Code generation cancelled");

                // Clean up partial output file if it exists
                if (_fileSystem.File.Exists(outputPath))
                {
                    try
                    {
                        _fileSystem.File.Delete(outputPath);
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
        /// Performs the actual code generation.
        /// </summary>
        /// <param name="inputPath">Path to the input bytecode.</param>
        /// <param name="outputPath">Path for the output source code.</param>
        /// <param name="namespaceName">Namespace for the generated code.</param>
        /// <param name="className">Class name for the generated code.</param>
        /// <param name="language">Target language.</param>
        /// <param name="includeInternals">Whether to include internals.</param>
        private void GenerateCode(
            string inputPath,
            string outputPath,
            string namespaceName,
            string className,
            string language,
            bool includeInternals
        )
        {
            try
            {
                // Create appropriate code generation language
                HardwireCodeGenerationLanguage codeGenLanguage = language.ToLowerInvariant() switch
                {
                    "cs" => new CSharpHardwireCodeGenerationLanguage(),
                    "vb" => new VbNetHardwireCodeGenerationLanguage(),
                    _ => throw new ArgumentException(
                        $"Unsupported language: {language}. Supported languages are 'cs' and 'vb'."
                    ),
                };

                _logger.LogDebug("Using {Language} code generator", codeGenLanguage.GetType().Name);

                // Create a logger adapter for hardwire
                var codeGenLogger = new HardwireLogger(_logger);

                // For hardwiring, we expect the input file to be a Lua script that returns
                // a serialized table dump from UserData.GetDescriptionOfRegisteredTypes()
                Table hardwireTable;

                // First check if it's a bytecode dump or a Lua source file
                var isBytecode = false;
                using (var testStream = _fileSystem.File.OpenRead(inputPath))
                {
                    // Check if it starts with bytecode magic number
                    if (testStream.Length >= 4)
                    {
                        var buffer = new byte[4];
                        testStream.Read(buffer, 0, 4);
                        // Lua bytecode starts with ESC, 'L', 'u', 'a' (0x1B4C7561)
                        isBytecode =
                            buffer[0] == 0x1B
                            && buffer[1] == 0x4C
                            && buffer[2] == 0x75
                            && buffer[3] == 0x61;
                    }
                }

                var script = new Script(Examples.DesktopBasePolicySet);
                DynValue result;

                if (isBytecode)
                {
                    using var stream = _fileSystem.File.OpenRead(inputPath);
                    result = script.LoadStream(stream, null, inputPath);
                }
                else
                {
                    // Load as Lua source
                    result = script.DoFile(inputPath);
                }

                // The script should return a table
                if (result.Type != DataType.Table)
                {
                    throw new InvalidOperationException(
                        "Input file must return a table (from UserData.GetDescriptionOfRegisteredTypes). "
                            + $"Got {result.Type} instead."
                    );
                }

                hardwireTable = result.Table;

                // Ensure output directory exists
                var outputDir = _fileSystem.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !_fileSystem.Directory.Exists(outputDir))
                {
                    _logger.LogDebug("Creating output directory: {OutputDir}", outputDir);
                    _fileSystem.Directory.CreateDirectory(outputDir);
                }

                // Generate code using HardwireGenerator
                var generator = new HardwireGenerator(
                    namespaceName,
                    className,
                    codeGenLogger,
                    codeGenLanguage
                );

                if (includeInternals)
                {
                    generator.AllowInternals = true;
                }

                generator.BuildCodeModel(hardwireTable);
                var generatedCode = generator.GenerateSourceCode();

                // Write generated code to file
                _fileSystem.File.WriteAllText(outputPath, generatedCode);

                // Log generation statistics
                var outputSize = _fileSystem.FileInfo.New(outputPath).Length;
                _logger.LogInformation(
                    "Generated {Language} code: {OutputSize} bytes, {Lines} lines",
                    language.ToUpperInvariant(),
                    outputSize,
                    _fileSystem.File.ReadAllLines(outputPath).Length
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate code");
                throw new InvalidOperationException($"Code generation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Logger adapter for hardwire code generation.
        /// Implements ICodeGenerationLogger to bridge to ILogger.
        /// </summary>
        private class HardwireLogger : ICodeGenerationLogger
        {
            private readonly ILogger<HardwireService> _logger;

            public HardwireLogger(ILogger<HardwireService> logger)
            {
                _logger = logger;
            }

            public void LogError(string message)
            {
                _logger.LogError("Hardwire: {Message}", message);
            }

            public void LogWarning(string message)
            {
                _logger.LogWarning("Hardwire: {Message}", message);
            }

            public void LogMinor(string message)
            {
                _logger.LogDebug("Hardwire: {Message}", message);
            }
        }
    }
}
