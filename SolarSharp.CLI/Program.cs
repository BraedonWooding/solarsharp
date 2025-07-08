using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarSharp.CLI.Services;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.CLI
{
    /// <summary>
    /// Entry point for the SolarSharp CLI application.
    /// Provides a modern command-line interface using System.CommandLine.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Exit code (0 for success, non-zero for failure).</returns>
        public static async Task<int> Main(string[] args)
        {
            var fileSystem = new FileSystem();

            // Support backward compatibility: if first arg is a .lua file, run it
            if (
                args.Length > 0
                && args[0].EndsWith(".lua", StringComparison.OrdinalIgnoreCase)
                && fileSystem.File.Exists(args[0])
            )
            {
                // Transform "solarsharp script.lua [options]" to "solarsharp run script.lua [options]"
                var newArgs = new string[args.Length + 1];
                newArgs[0] = "run";
                Array.Copy(args, 0, newArgs, 1, args.Length);
                args = newArgs;
            }
            // If no args or only options (starting with -), default to REPL
            else if (args.Length == 0 || (args.Length > 0 && args[0].StartsWith("-")))
            {
                // Check if any args are subcommands
                var subcommands = new[] { "repl", "run", "compile", "hardwire" };
                var hasSubcommand = args.Any(arg => subcommands.Contains(arg));

                if (!hasSubcommand)
                {
                    // Transform to "solarsharp repl [options]"
                    var newArgs = new string[args.Length + 1];
                    newArgs[0] = "repl";
                    Array.Copy(args, 0, newArgs, 1, args.Length);
                    args = newArgs;
                }
            }

            var rootCommand = BuildRootCommand();

            var parser = new CommandLineBuilder(rootCommand)
                .UseHost(
                    _ => Host.CreateDefaultBuilder(),
                    host =>
                    {
                        host.ConfigureServices(
                            (context, services) =>
                            {
                                ConfigureServices(services);
                            }
                        );
                    }
                )
                .UseDefaults()
                .Build();

            return await parser.InvokeAsync(args);
        }

        /// <summary>
        /// Builds the root command with all subcommands and options.
        /// </summary>
        /// <returns>The configured root command.</returns>
        private static RootCommand BuildRootCommand()
        {
            var rootCommand = new RootCommand("SolarSharp - A secure Lua interpreter for .NET")
            {
                Name = "solarsharp",
            };

            // Global options
            var verbosityOption = new Option<LogLevel>(
                new[] { "--verbosity", "-v" },
                getDefaultValue: () => LogLevel.Information,
                description: "Set the verbosity level"
            );

            rootCommand.AddGlobalOption(verbosityOption);

            // Add subcommands
            rootCommand.AddCommand(BuildReplCommand());
            rootCommand.AddCommand(BuildRunCommand());
            rootCommand.AddCommand(BuildCompileCommand());
            rootCommand.AddCommand(BuildHardwireCommand());

            // Default behaviour when no subcommand is specified
            rootCommand.SetHandler(() => { });

            return rootCommand;
        }

        /// <summary>
        /// Builds the REPL (Read-Eval-Print Loop) command.
        /// </summary>
        /// <returns>The configured REPL command.</returns>
        private static Command BuildReplCommand()
        {
            var replCommand = new Command("repl", "Start an interactive Lua REPL session");

            var policyOption = new Option<string>(
                new[] { "--policy", "-p" },
                getDefaultValue: () => "desktop",
                description: $"Example policy: {string.Join(", ", Examples.GetAvailablePolicyNames())}"
            ).FromAmong(Examples.GetAvailablePolicyNames());

            var manifestOption = new Option<FileInfo>(
                new[] { "--manifest", "-m" },
                description: "Path to a security manifest file"
            );

            var timeoutOption = new Option<int?>(
                new[] { "--timeout", "-t" },
                description: "Execution timeout in milliseconds (-1 for no timeout)"
            );

            var memoryOption = new Option<int?>(
                new[] { "--memory", "-M" },
                description: "Memory limit in MB"
            );

            replCommand.AddOption(policyOption);
            replCommand.AddOption(manifestOption);
            replCommand.AddOption(timeoutOption);
            replCommand.AddOption(memoryOption);

            replCommand.SetHandler(async context =>
            {
                var host = context.BindingContext.GetService<IHost>();
                var logger = host.Services.GetRequiredService<ILogger<ReplService>>();
                var replService = host.Services.GetRequiredService<IReplService>();

                var policyName = context.ParseResult.GetValueForOption(policyOption);
                var manifestFile = context.ParseResult.GetValueForOption(manifestOption);
                var timeout = context.ParseResult.GetValueForOption(timeoutOption);
                var memory = context.ParseResult.GetValueForOption(memoryOption);

                var options = new ReplOptions
                {
                    PolicyName = policyName,
                    ManifestPath = manifestFile?.FullName,
                    TimeoutMs = timeout,
                    MemoryMb = memory,
                };

                await replService.RunAsync(options, context.GetCancellationToken());
            });

            return replCommand;
        }

        /// <summary>
        /// Builds the run command for executing Lua scripts.
        /// </summary>
        /// <returns>The configured run command.</returns>
        private static Command BuildRunCommand()
        {
            var runCommand = new Command("run", "Execute a Lua script file");

            var scriptArgument = new Argument<FileInfo>(
                "script",
                description: "Path to the Lua script file to execute"
            );

            var policyOption = new Option<string>(
                new[] { "--policy", "-p" },
                getDefaultValue: () => "desktop",
                description: $"Example policy: {string.Join(", ", Examples.GetAvailablePolicyNames())}"
            ).FromAmong(Examples.GetAvailablePolicyNames());

            var manifestOption = new Option<FileInfo>(
                new[] { "--manifest", "-m" },
                description: "Path to a security manifest file"
            );

            var argsOption = new Option<string[]>(
                new[] { "--args", "-a" },
                description: "Arguments to pass to the script"
            );

            runCommand.AddArgument(scriptArgument);
            runCommand.AddOption(policyOption);
            runCommand.AddOption(manifestOption);
            runCommand.AddOption(argsOption);

            runCommand.SetHandler(async context =>
            {
                var host = context.BindingContext.GetService<IHost>();
                var scriptService = host.Services.GetRequiredService<IScriptService>();
                var logger = host.Services.GetRequiredService<ILogger<ScriptService>>();

                var scriptFile = context.ParseResult.GetValueForArgument(scriptArgument);
                var policyName = context.ParseResult.GetValueForOption(policyOption);
                var manifestFile = context.ParseResult.GetValueForOption(manifestOption);
                var scriptArgs =
                    context.ParseResult.GetValueForOption(argsOption) ?? Array.Empty<string>();

                try
                {
                    var result = await scriptService.RunScriptAsync(
                        scriptFile.FullName,
                        policyName,
                        manifestFile?.FullName,
                        scriptArgs,
                        context.GetCancellationToken()
                    );

                    if (result.Success)
                    {
                        if (result.ReturnValue != null)
                        {
                            Console.WriteLine(result.ReturnValue);
                        }
                    }
                    else
                    {
                        logger.LogError("Script execution failed: {Error}", result.ErrorMessage);
                        context.ExitCode = 1;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error running script");
                    context.ExitCode = 1;
                }
            });

            return runCommand;
        }

        /// <summary>
        /// Builds the compile command for compiling Lua scripts to bytecode.
        /// </summary>
        /// <returns>The configured compile command.</returns>
        private static Command BuildCompileCommand()
        {
            var compileCommand = new Command("compile", "Compile a Lua script to bytecode");

            var inputArgument = new Argument<FileInfo>(
                "input",
                description: "Input Lua script file"
            );

            var outputOption = new Option<FileInfo>(
                new[] { "--output", "-o" },
                description: "Output bytecode file"
            );

            compileCommand.AddArgument(inputArgument);
            compileCommand.AddOption(outputOption);

            compileCommand.SetHandler(async context =>
            {
                var host = context.BindingContext.GetService<IHost>();
                var compileService = host.Services.GetRequiredService<ICompileService>();
                var logger = host.Services.GetRequiredService<ILogger<CompileService>>();

                var inputFile = context.ParseResult.GetValueForArgument(inputArgument);
                var outputFile = context.ParseResult.GetValueForOption(outputOption);

                if (outputFile == null)
                {
                    var fileSystem = host.Services.GetRequiredService<IFileSystem>();
                    var outputPath = fileSystem.Path.ChangeExtension(inputFile.FullName, ".luac");
                    outputFile = new FileInfo(outputPath);
                }

                try
                {
                    await compileService.CompileAsync(
                        inputFile.FullName,
                        outputFile.FullName,
                        context.GetCancellationToken()
                    );

                    logger.LogInformation(
                        "Successfully compiled {Input} to {Output}",
                        inputFile.Name,
                        outputFile.Name
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to compile script");
                    context.ExitCode = 1;
                }
            });

            return compileCommand;
        }

        /// <summary>
        /// Builds the hardwire command for generating C# code from Lua bytecode.
        /// </summary>
        /// <returns>The configured hardwire command.</returns>
        private static Command BuildHardwireCommand()
        {
            var hardwireCommand = new Command(
                "hardwire",
                "Generate C# code from compiled Lua bytecode"
            );

            var inputArgument = new Argument<FileInfo>("input", description: "Input bytecode file");

            var outputArgument = new Argument<FileInfo>("output", description: "Output C# file");

            var namespaceOption = new Option<string>(
                new[] { "--namespace", "-n" },
                getDefaultValue: () => "SolarSharp.Generated",
                description: "C# namespace for generated code"
            );

            var classOption = new Option<string>(
                new[] { "--class", "-c" },
                getDefaultValue: () => "GeneratedScript",
                description: "C# class name for generated code"
            );

            var languageOption = new Option<string>(
                new[] { "--language", "-l" },
                getDefaultValue: () => "cs",
                description: "Output language: cs (C#) or vb (VB.NET)"
            ).FromAmong("cs", "vb");

            var internalsOption = new Option<bool>(
                new[] { "--internals", "-i" },
                getDefaultValue: () => false,
                description: "Include internal SolarSharp types"
            );

            hardwireCommand.AddArgument(inputArgument);
            hardwireCommand.AddArgument(outputArgument);
            hardwireCommand.AddOption(namespaceOption);
            hardwireCommand.AddOption(classOption);
            hardwireCommand.AddOption(languageOption);
            hardwireCommand.AddOption(internalsOption);

            hardwireCommand.SetHandler(async context =>
            {
                var host = context.BindingContext.GetService<IHost>();
                var hardwireService = host.Services.GetRequiredService<IHardwireService>();
                var logger = host.Services.GetRequiredService<ILogger<HardwireService>>();

                var inputFile = context.ParseResult.GetValueForArgument(inputArgument);
                var outputFile = context.ParseResult.GetValueForArgument(outputArgument);
                var ns = context.ParseResult.GetValueForOption(namespaceOption);
                var className = context.ParseResult.GetValueForOption(classOption);
                var language = context.ParseResult.GetValueForOption(languageOption);
                var internals = context.ParseResult.GetValueForOption(internalsOption);

                try
                {
                    await hardwireService.GenerateAsync(
                        inputFile.FullName,
                        outputFile.FullName,
                        ns,
                        className,
                        language,
                        internals,
                        context.GetCancellationToken()
                    );

                    logger.LogInformation(
                        "Successfully generated {Language} code to {Output}",
                        language.ToUpperInvariant(),
                        outputFile.Name
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate code");
                    context.ExitCode = 1;
                }
            });

            return hardwireCommand;
        }

        /// <summary>
        /// Configures dependency injection services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private static void ConfigureServices(IServiceCollection services)
        {
            // File system abstraction
            services.AddSingleton<IFileSystem, FileSystem>();

            // Core services
            services.AddSingleton<ISecurityPolicyFactory>(provider => new SecurityPolicyFactory(
                provider.GetRequiredService<ILogger<SecurityPolicyFactory>>(),
                provider.GetRequiredService<IFileSystem>()
            ));
            services.AddSingleton<IScriptFactory, ScriptFactory>();

            // Command services
            services.AddTransient<IReplService, ReplService>();
            services.AddTransient<IScriptService, ScriptService>();
            services.AddTransient<ICompileService, CompileService>();
            services.AddTransient<IHardwireService, HardwireService>();

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }
    }

    /// <summary>
    /// Options for the REPL service.
    /// </summary>
    public class ReplOptions
    {
        public string PolicyName { get; set; } = "desktop";
        public string ManifestPath { get; set; }
        public int? TimeoutMs { get; set; }
        public int? MemoryMb { get; set; }
    }

    /// <summary>
    /// Service interface for REPL functionality.
    /// </summary>
    public interface IReplService
    {
        Task RunAsync(ReplOptions options, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Service interface for script execution.
    /// </summary>
    public interface IScriptService
    {
        Task<ScriptResult> RunScriptAsync(
            string scriptPath,
            string policyName,
            string manifestPath,
            string[] args,
            CancellationToken cancellationToken
        );
    }

    /// <summary>
    /// Result of script execution.
    /// </summary>
    public class ScriptResult
    {
        public bool Success { get; set; }
        public string ReturnValue { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service interface for script compilation.
    /// </summary>
    public interface ICompileService
    {
        Task CompileAsync(string inputPath, string outputPath, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Service interface for hardwire code generation.
    /// </summary>
    public interface IHardwireService
    {
        Task GenerateAsync(
            string inputPath,
            string outputPath,
            string namespaceName,
            string className,
            string language,
            bool includeInternals,
            CancellationToken cancellationToken
        );
    }

    /// <summary>
    /// Factory for creating security policies.
    /// </summary>
    public interface ISecurityPolicyFactory
    {
        SecurityPolicy Create(string policyName, string manifestPath = null);
    }

    /// <summary>
    /// Factory for creating Script instances.
    /// </summary>
    public interface IScriptFactory
    {
        Script Create(SecurityPolicy policy);
    }
}
