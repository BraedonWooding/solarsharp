using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Loaders;

namespace SolarSharp.CLI.Services
{
    /// <summary>
    /// Service implementation for executing Lua scripts.
    /// Handles script loading, security configuration, and execution.
    /// </summary>
    public class ScriptService : IScriptService
    {
        /// <summary>
        /// Logger instance for the <see cref="ScriptService"/> class.
        /// Used to log diagnostic and error information during script execution,
        /// including debug information, error handling, and execution status.
        /// </summary>
        private readonly ILogger<ScriptService> _logger;

        /// <summary>
        /// Factory instance responsible for creating security policies.
        /// Provides the logic to generate security settings based on a specified
        /// example policy name and optional manifest path.
        /// </summary>
        private readonly ISecurityPolicyFactory _securityFactory;

        /// <summary>
        /// Factory instance for creating and initializing scripts used in the ScriptService.
        /// This field is responsible for generating script instances configured with the required security settings
        /// and other contextual features for execution.
        /// </summary>
        private readonly IScriptFactory _scriptFactory;

        /// <summary>
        /// File system abstraction for file operations.
        /// </summary>
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptService"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="securityFactory">Factory for creating security policies.</param>
        /// <param name="scriptFactory">Factory for creating script instances.</param>
        /// <param name="fileSystem">File system abstraction.</param>
        public ScriptService(
            ILogger<ScriptService> logger,
            ISecurityPolicyFactory securityFactory,
            IScriptFactory scriptFactory,
            IFileSystem fileSystem
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityFactory =
                securityFactory ?? throw new ArgumentNullException(nameof(securityFactory));
            _scriptFactory =
                scriptFactory ?? throw new ArgumentNullException(nameof(scriptFactory));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Executes a Lua script file asynchronously.
        /// </summary>
        /// <param name="scriptPath">Path to the script file to execute.</param>
        /// <param name="policyName">Example policy name for script execution.</param>
        /// <param name="manifestPath">Optional path to a security manifest.</param>
        /// <param name="args">Arguments to pass to the script.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of script execution.</returns>
        public async Task<ScriptResult> RunScriptAsync(
            string scriptPath,
            string policyName,
            string manifestPath,
            string[] args,
            CancellationToken cancellationToken
        )
        {
            if (string.IsNullOrEmpty(scriptPath))
                throw new ArgumentNullException(nameof(scriptPath));

            _logger.LogDebug(
                "Running script: {ScriptPath} with policy: {PolicyName}",
                scriptPath,
                policyName
            );

            // Validate script file exists
            if (!_fileSystem.File.Exists(scriptPath))
            {
                _logger.LogError("Script file not found: {ScriptPath}", scriptPath);
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"Script file not found: {scriptPath}",
                };
            }

            try
            {
                // Create security configuration
                var securityConfig = _securityFactory.Create(policyName, manifestPath);

                // Create script instance
                var script = _scriptFactory.Create(securityConfig);

                // Configure script globals
                ConfigureScriptGlobals(script, scriptPath, args);

                // Execute script asynchronously
                var result = await Task.Run(
                    () => ExecuteScript(script, scriptPath),
                    cancellationToken
                );

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Script execution cancelled");
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = "Script execution was cancelled",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing script");
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                };
            }
        }

        /// <summary>
        /// Configures global variables and functions for the script.
        /// </summary>
        /// <param name="script">The script instance to configure.</param>
        /// <param name="scriptPath">Path to the script being executed.</param>
        /// <param name="args">Command-line arguments for the script.</param>
        private void ConfigureScriptGlobals(Script script, string scriptPath, string[] args)
        {
            // Set up arg table (Lua convention)
            var argTable = new Table
            {
                // arg[0] is the script name
                [0] = scriptPath,
            };

            // arg[1], arg[2], etc. are the command-line arguments
            for (var i = 0; i < args.Length; i++)
            {
                argTable[i + 1] = args[i];
            }

            script.Globals["arg"] = argTable;

            // Also set up ... (varargs) for the script
            if (args.Length > 0)
            {
                script.Globals["..."] = DynValue.NewTuple(
                    args.Select(static a => DynValue.NewString(a)).ToArray()
                );
            }

            // Set script directory as working directory for relative paths
            var scriptDir = _fileSystem.Path.GetDirectoryName(
                _fileSystem.Path.GetFullPath(scriptPath)
            );
            if (!string.IsNullOrEmpty(scriptDir))
            {
                // Cast to ScriptLoaderBase to access ModulePaths property
                if (script.Options.ScriptLoader is ScriptLoaderBase scriptLoader)
                {
                    scriptLoader.ModulePaths = new[] { scriptDir };
                }
            }

            _logger.LogDebug("Configured script with {ArgCount} arguments", args.Length);
        }

        /// <summary>
        /// Executes the script and captures the result.
        /// </summary>
        /// <param name="script">The configured script instance.</param>
        /// <param name="scriptPath">Path to the script file.</param>
        /// <returns>The execution result.</returns>
        private ScriptResult ExecuteScript(Script script, string scriptPath)
        {
            try
            {
                _logger.LogDebug("Loading and executing script: {ScriptPath}", scriptPath);

                // Use DoFile to execute the script with proper manifest discovery
                var result = script.DoFile(scriptPath);

                _logger.LogDebug("Script execution completed successfully");

                return new ScriptResult
                {
                    Success = true,
                    ReturnValue = result.Type != DataType.Void ? result.ToPrintString() : null,
                };
            }
            catch (ScriptRuntimeException ex)
            {
                _logger.LogError("Script runtime error: {Message}", ex.Message);

                // Format error with source location if available
                var errorMessage = FormatScriptError(script, ex);

                return new ScriptResult { Success = false, ErrorMessage = errorMessage };
            }
            catch (SyntaxErrorException ex)
            {
                _logger.LogError("Script syntax error: {Message}", ex.Message);

                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"Syntax error: {ex.Message}",
                };
            }
            catch (InterpreterException ex)
            {
                _logger.LogError("Interpreter error: {Message}", ex.Message);

                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"Interpreter error: {ex.Message}",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during script execution");

                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.GetType().Name}: {ex.Message}",
                };
            }
        }

        /// <summary>
        /// Formats a script runtime exception with source location information.
        /// </summary>
        /// <param name="script">The script instance.</param>
        /// <param name="ex">The script runtime exception.</param>
        /// <returns>A formatted error message.</returns>
        private string FormatScriptError(Script script, ScriptRuntimeException ex)
        {
            var message = ex.Message;

            // Add call stack information if available
            if (ex.CallStack == null || !ex.CallStack.Any())
                return message;
            message += "\n\nCall stack:";
            foreach (var frame in ex.CallStack.Take(10)) // Limit stack depth
            {
                if (frame.Location != null)
                {
                    message +=
                        $"\n  at {frame.Name ?? "<anonymous>"} ({frame.Location.FormatLocation(script)})";
                }
                else
                {
                    message += $"\n  at {frame.Name ?? "<anonymous>"}";
                }
            }

            if (ex.CallStack.Count > 10)
            {
                message += $"\n  ... and {ex.CallStack.Count - 10} more";
            }

            return message;
        }
    }
}
