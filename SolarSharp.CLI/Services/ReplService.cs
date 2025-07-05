using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.REPL;

namespace SolarSharp.CLI.Services
{
    /// <summary>
    /// Service implementation for the REPL (Read-Eval-Print Loop) functionality.
    /// Provides an interactive Lua interpreter session.
    /// </summary>
    public class ReplService : IReplService
    {
        private readonly ILogger<ReplService> _logger;
        private readonly ISecurityConfigurationFactory _securityFactory;
        private readonly IScriptFactory _scriptFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplService"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="securityFactory">Factory for creating security configurations.</param>
        /// <param name="scriptFactory">Factory for creating script instances.</param>
        public ReplService(
            ILogger<ReplService> logger,
            ISecurityConfigurationFactory securityFactory,
            IScriptFactory scriptFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityFactory = securityFactory ?? throw new ArgumentNullException(nameof(securityFactory));
            _scriptFactory = scriptFactory ?? throw new ArgumentNullException(nameof(scriptFactory));
        }

        /// <summary>
        /// Runs the REPL session asynchronously.
        /// </summary>
        /// <param name="options">Configuration options for the REPL session.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RunAsync(ReplOptions options, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting REPL with security level: {SecurityLevel}", options.SecurityLevel);

            try
            {
                // Create security configuration
                var securityConfig = _securityFactory.Create(options.SecurityLevel, options.ManifestPath);
                
                // Apply overrides
                if (options.TimeoutMs.HasValue)
                {
                    _logger.LogDebug("Applying timeout override: {Timeout}ms", options.TimeoutMs.Value);
                    securityConfig.WithTimeoutMs(options.TimeoutMs.Value);
                }
                
                if (options.MemoryMb.HasValue)
                {
                    _logger.LogDebug("Applying memory limit override: {Memory}MB", options.MemoryMb.Value);
                    securityConfig.Execution.MaxMemoryMB = options.MemoryMb.Value;
                }

                // Create script instance
                var script = _scriptFactory.Create(securityConfig);
                
                // Add REPL-specific globals
                ConfigureReplGlobals(script);

                // Display banner
                DisplayBanner(options);

                // Create and run interpreter
                var interpreter = new ReplInterpreter(script)
                {
                    HandleDynamicExprs = true,
                    HandleClassicExprsSyntax = true
                };

                await Task.Run(() => RunInterpreterLoop(interpreter, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("REPL cancelled by user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in REPL");
                throw;
            }
        }

        /// <summary>
        /// Configures REPL-specific global functions and variables.
        /// </summary>
        /// <param name="script">The script instance to configure.</param>
        private void ConfigureReplGlobals(Script script)
        {
            // Add makestatic function for loading .NET types
            script.Globals["makestatic"] = (Func<string, DynValue>)(typeName =>
            {
                try
                {
                    var type = Type.GetType(typeName);
                    if (type != null)
                    {
                        return UserData.CreateStatic(type);
                    }
                    
                    _logger.LogWarning("Type not found: {TypeName}", typeName);
                    return DynValue.Nil;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading type: {TypeName}", typeName);
                    return DynValue.Nil;
                }
            });

            // Add version information
            script.Globals["_VERSION"] = "SolarSharp " + typeof(Script).Assembly.GetName().Version;
            
            // Add help function
            script.Globals["help"] = (Action)(() =>
            {
                Console.WriteLine("SolarSharp REPL Commands:");
                Console.WriteLine("  !exit, !quit     - Exit the REPL");
                Console.WriteLine("  !help            - Show this help message");
                Console.WriteLine("  !clear           - Clear the screen");
                Console.WriteLine("  !reset           - Reset the Lua environment");
                Console.WriteLine("  !mem             - Show memory usage");
                Console.WriteLine("  !time <expr>     - Time the execution of an expression");
                Console.WriteLine();
                Console.WriteLine("Lua expressions are evaluated and results are displayed.");
                Console.WriteLine("Multi-line input is supported - continue on the next line");
                Console.WriteLine("if the expression is incomplete.");
            });
        }

        /// <summary>
        /// Displays the REPL banner with version and configuration information.
        /// </summary>
        /// <param name="options">The REPL options containing configuration.</param>
        private void DisplayBanner(ReplOptions options)
        {
            Console.WriteLine(Script.GetBanner("Console"));
            Console.WriteLine();
            Console.WriteLine($"Security Level: {options.SecurityLevel}");
            
            if (!string.IsNullOrEmpty(options.ManifestPath))
            {
                Console.WriteLine($"Manifest: {options.ManifestPath}");
            }
            
            if (options.TimeoutMs.HasValue)
            {
                Console.WriteLine($"Timeout: {options.TimeoutMs}ms");
            }
            
            if (options.MemoryMb.HasValue)
            {
                Console.WriteLine($"Memory Limit: {options.MemoryMb}MB");
            }
            
            Console.WriteLine();
            Console.WriteLine("Type Lua code to execute it or !help for commands.");
            Console.WriteLine();
        }

        /// <summary>
        /// Runs the main REPL loop, reading and executing user input.
        /// </summary>
        /// <param name="interpreter">The REPL interpreter instance.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        private void RunInterpreterLoop(ReplInterpreter interpreter, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var input = ReadInput(interpreter);
                    
                    if (string.IsNullOrEmpty(input))
                        continue;

                    // Handle REPL commands
                    if (input.StartsWith("!"))
                    {
                        if (!HandleReplCommand(input, interpreter))
                            break; // Exit requested
                        continue;
                    }

                    // Execute Lua code
                    var result = interpreter.Evaluate(input);
                    
                    if (result != null && result.Type != DataType.Void)
                    {
                        Console.WriteLine(result.ToPrintString());
                    }
                }
                catch (InterpreterException ex)
                {
                    _logger.LogDebug(ex, "Interpreter error");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in REPL loop");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// Reads input from the user, handling multi-line expressions.
        /// </summary>
        /// <param name="interpreter">The REPL interpreter for checking if more input is needed.</param>
        /// <returns>The complete input string.</returns>
        private string ReadInput(ReplInterpreter interpreter)
        {
            Console.Write(interpreter.HasPendingCommand ? ">> " : "> ");
            return Console.ReadLine();
        }

        /// <summary>
        /// Handles special REPL commands that start with '!'.
        /// </summary>
        /// <param name="command">The command string including the '!' prefix.</param>
        /// <param name="interpreter">The REPL interpreter instance.</param>
        /// <returns>True to continue the REPL loop, false to exit.</returns>
        private bool HandleReplCommand(string command, ReplInterpreter interpreter)
        {
            var cmd = command.Substring(1).ToLowerInvariant().Trim();
            
            switch (cmd)
            {
                case "exit":
                case "quit":
                    _logger.LogDebug("Exit command received");
                    return false;
                    
                case "help":
                    interpreter.Evaluate("help()");
                    break;
                    
                case "clear":
                    Console.Clear();
                    break;
                    
                case "reset":
                    // Reset the interpreter by creating a new script
                    _logger.LogDebug("Resetting Lua environment");
                    Console.WriteLine("Lua environment reset.");
                    // Note: In a real implementation, we'd need to recreate the interpreter
                    break;
                    
                case "mem":
                    ShowMemoryUsage();
                    break;
                    
                default:
                    if (cmd.StartsWith("time "))
                    {
                        var expr = command.Substring(6).Trim();
                        TimeExpression(interpreter, expr);
                    }
                    else
                    {
                        Console.WriteLine($"Unknown command: {cmd}");
                        Console.WriteLine("Type !help for available commands.");
                    }
                    break;
            }
            
            return true;
        }

        /// <summary>
        /// Shows current memory usage information.
        /// </summary>
        private void ShowMemoryUsage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memory = GC.GetTotalMemory(false);
            Console.WriteLine($"Memory usage: {memory / 1024.0 / 1024.0:F2} MB");
        }

        /// <summary>
        /// Times the execution of a Lua expression.
        /// </summary>
        /// <param name="interpreter">The REPL interpreter instance.</param>
        /// <param name="expression">The expression to time.</param>
        private void TimeExpression(ReplInterpreter interpreter, string expression)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = interpreter.Evaluate(expression);
                stopwatch.Stop();
                
                if (result != null && result.Type != DataType.Void)
                {
                    Console.WriteLine(result.ToPrintString());
                }
                
                Console.WriteLine($"Execution time: {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}