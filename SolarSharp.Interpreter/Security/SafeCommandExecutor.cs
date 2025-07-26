using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Executes system commands safely with strict validation and sandboxing
    /// </summary>
    public class SafeCommandExecutor
    {
        private readonly SafeCommandPolicy _policy;
        private readonly Dictionary<string, CommandDefinition> _allowedCommands;
        private readonly List<Regex> _blockedArgumentPatterns;
        private readonly EnvironmentEmulator _environmentEmulator;
        private readonly VirtualFileSystemMapper _fileSystemMapper;

        // Pre-compiled regex patterns for common security checks
        private static readonly Regex ShellMetaCharsPattern = new Regex(
            @"[;&|`$()]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex EvalPattern = new Regex(
            @"--eval|--execute",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex ShortEvalPattern = new Regex(
            @"-[ec]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex EscapeSequencePattern = new Regex(
            @"\\[nrt]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex CommandSubstitutionPattern = new Regex(
            @"\$\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex BacktickPattern = new Regex(
            @"`.*`",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex DeviceAccessPattern = new Regex(
            @">\s*/dev/",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex OutputRedirectionPattern = new Regex(
            @"2>&1",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex DeviceInputPattern = new Regex(
            @"</dev/",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public SafeCommandExecutor(
            SafeCommandPolicy policy,
            EnvironmentEmulator environmentEmulator = null,
            VirtualFileSystemMapper fileSystemMapper = null
        )
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _allowedCommands = new Dictionary<string, CommandDefinition>(
                StringComparer.OrdinalIgnoreCase
            );
            _blockedArgumentPatterns = new List<Regex>();
            _environmentEmulator = environmentEmulator;
            _fileSystemMapper = fileSystemMapper;

            Initialize();
        }

        /// <summary>
        /// Executes a command safely with validation and sandboxing
        /// </summary>
        public async Task<CommandResult> ExecuteAsync(
            string commandLine,
            CancellationToken cancellationToken = default
        )
        {
            if (!_policy.Enabled)
                throw new CommandExecutionViolationException(
                    "Command execution is disabled",
                    "CommandExecution"
                );

            // Parse command line
            var (command, arguments) = ParseCommandLine(commandLine);

            // Validate command is allowed
            ValidateCommand(command, arguments);

            // Execute with sandbox restrictions
            return await ExecuteCommandSafely(command, arguments, cancellationToken);
        }

        /// <summary>
        /// Checks if a command is allowed without executing it
        /// </summary>
        public bool IsCommandAllowed(string commandLine)
        {
            try
            {
                var (command, arguments) = ParseCommandLine(commandLine);
                ValidateCommand(command, arguments);
                return true;
            }
            catch (SecurityException)
            {
                return false;
            }
        }

        private void Initialize()
        {
            // Initialize safe commands
            InitializeSafeCommands();

            // Initialize blocked argument patterns
            InitializeBlockedPatterns();
        }

        private void InitializeSafeCommands()
        {
            // Text processing commands (always safe)
            var safeCommands = new Dictionary<string, CommandDefinition>
            {
                ["echo"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(1),
                },
                ["printf"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(1),
                },
                ["cat"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    RequiresFileAccess = true,
                },
                ["wc"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                },
                ["sort"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(10),
                },
                ["uniq"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(10),
                },
                ["head"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                },
                ["tail"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                },
                ["cut"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                },
                ["tr"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                },
                ["grep"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(10),
                },
                ["awk"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(10),
                    BlockedArguments = new[] { "-f", "--file" },
                },
                ["sed"] = new CommandDefinition
                {
                    Category = CommandCategory.Safe,
                    MaxExecutionTime = TimeSpan.FromSeconds(10),
                    BlockedArguments = new[] { "-f", "--file" },
                },
            };

            // Filesystem commands (conditionally safe)
            var filesystemCommands = new Dictionary<string, CommandDefinition>
            {
                ["ls"] = new CommandDefinition
                {
                    Category = CommandCategory.Filesystem,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    RequiresFileAccess = true,
                },
                ["find"] = new CommandDefinition
                {
                    Category = CommandCategory.Filesystem,
                    MaxExecutionTime = TimeSpan.FromSeconds(30),
                    RequiresFileAccess = true,
                    BlockedArguments = new[] { "-exec", "-execdir" },
                },
                ["stat"] = new CommandDefinition
                {
                    Category = CommandCategory.Filesystem,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    RequiresFileAccess = true,
                },
                ["file"] = new CommandDefinition
                {
                    Category = CommandCategory.Filesystem,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    RequiresFileAccess = true,
                },
                ["du"] = new CommandDefinition
                {
                    Category = CommandCategory.Filesystem,
                    MaxExecutionTime = TimeSpan.FromSeconds(30),
                    RequiresFileAccess = true,
                },
                ["df"] = new CommandDefinition
                {
                    Category = CommandCategory.Filesystem,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                },
            };

            // Development commands (version info only)
            var developmentCommands = new Dictionary<string, CommandDefinition>
            {
                ["git"] = new CommandDefinition
                {
                    Category = CommandCategory.Development,
                    MaxExecutionTime = TimeSpan.FromSeconds(10),
                    AllowedArguments = new[] { "status", "log", "diff", "--version" },
                },
                ["npm"] = new CommandDefinition
                {
                    Category = CommandCategory.Development,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    AllowedArguments = new[] { "--version" },
                },
                ["node"] = new CommandDefinition
                {
                    Category = CommandCategory.Development,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    AllowedArguments = new[] { "--version" },
                },
                ["python"] = new CommandDefinition
                {
                    Category = CommandCategory.Development,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    AllowedArguments = new[] { "--version" },
                },
                ["python3"] = new CommandDefinition
                {
                    Category = CommandCategory.Development,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    AllowedArguments = new[] { "--version" },
                },
                ["dotnet"] = new CommandDefinition
                {
                    Category = CommandCategory.Development,
                    MaxExecutionTime = TimeSpan.FromSeconds(5),
                    AllowedArguments = new[] { "--version", "--info" },
                },
            };

            // Add commands based on policy
            if (_policy.AllowedCategories.HasFlag(CommandCategory.Safe))
                AddCommands(safeCommands);

            if (_policy.AllowedCategories.HasFlag(CommandCategory.Filesystem))
                AddCommands(filesystemCommands);

            if (_policy.AllowedCategories.HasFlag(CommandCategory.Development))
                AddCommands(developmentCommands);

            // Add custom commands from policy
            if (_policy.CustomCommands != null)
            {
                AddCommands(_policy.CustomCommands);
            }
        }

        private void AddCommands(Dictionary<string, CommandDefinition> commands)
        {
            foreach (var kvp in commands)
            {
                _allowedCommands[kvp.Key] = kvp.Value;
            }
        }

        private void InitializeBlockedPatterns()
        {
            // Add pre-compiled default patterns
            _blockedArgumentPatterns.Add(ShellMetaCharsPattern);
            _blockedArgumentPatterns.Add(EvalPattern);
            _blockedArgumentPatterns.Add(ShortEvalPattern);
            _blockedArgumentPatterns.Add(EscapeSequencePattern);
            _blockedArgumentPatterns.Add(CommandSubstitutionPattern);
            _blockedArgumentPatterns.Add(BacktickPattern);
            _blockedArgumentPatterns.Add(DeviceAccessPattern);
            _blockedArgumentPatterns.Add(OutputRedirectionPattern);
            _blockedArgumentPatterns.Add(DeviceInputPattern);

            // Add custom patterns from policy
            if (_policy.BlockedArgumentPatterns != null)
            {
                foreach (var pattern in _policy.BlockedArgumentPatterns)
                {
                    try
                    {
                        _blockedArgumentPatterns.Add(
                            new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
                        );
                    }
                    catch (ArgumentException)
                    {
                        // Skip invalid patterns
                    }
                }
            }
        }

        private (string command, string[] arguments) ParseCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                throw new ArgumentException("Command line cannot be empty");

            // Simple parsing - split on spaces (could be enhanced for quoted arguments)
            var parts = commandLine
                .Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                throw new ArgumentException("No command specified");

            var command = parts[0];
            var arguments = parts.Skip(1).ToArray();

            return (command, arguments);
        }

        private void ValidateCommand(string command, string[] arguments)
        {
            // Check if command is in allowed list
            if (!_allowedCommands.TryGetValue(command, out var commandDef))
            {
                throw new CommandExecutionViolationException(
                    $"Command '{command}' is not allowed",
                    "CommandExecution",
                    command
                );
            }

            // Validate arguments count
            if (arguments.Length > _policy.MaxArguments)
            {
                throw new CommandExecutionViolationException(
                    $"Too many arguments: {arguments.Length} > {_policy.MaxArguments}",
                    "CommandExecution"
                );
            }

            // Validate argument content
            ValidateArguments(commandDef, arguments);
        }

        private void ValidateArguments(CommandDefinition commandDef, string[] arguments)
        {
            foreach (var argument in arguments)
            {
                // Check argument length
                if (argument.Length > _policy.MaxArgumentLength)
                {
                    throw new CommandExecutionViolationException(
                        $"Argument too long: {argument.Length} > {_policy.MaxArgumentLength}",
                        "CommandExecution"
                    );
                }

                // Check for blocked patterns
                foreach (var pattern in _blockedArgumentPatterns)
                {
                    if (pattern.IsMatch(argument))
                    {
                        throw new CommandExecutionViolationException(
                            $"Argument contains blocked pattern: {argument}",
                            "CommandExecution"
                        );
                    }
                }

                // Check command-specific blocked arguments
                if (
                    commandDef.BlockedArguments != null
                    && commandDef.BlockedArguments.Contains(
                        argument,
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                {
                    throw new CommandExecutionViolationException(
                        $"Argument '{argument}' is not allowed for this command",
                        "CommandExecution"
                    );
                }
            }

            // Check if only specific arguments are allowed
            if (commandDef.AllowedArguments != null)
            {
                foreach (var argument in arguments)
                {
                    if (
                        !commandDef.AllowedArguments.Contains(
                            argument,
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    {
                        throw new CommandExecutionViolationException(
                            $"Argument '{argument}' is not in allowed list for this command",
                            "CommandExecution"
                        );
                    }
                }
            }
        }

        private async Task<CommandResult> ExecuteCommandSafely(
            string command,
            string[] arguments,
            CancellationToken cancellationToken
        )
        {
            var commandDef = _allowedCommands[command];
            var timeout = commandDef.MaxExecutionTime ?? _policy.DefaultMaxExecutionTime;

            using var process = new Process();

            // Configure process
            process.StartInfo.FileName = command;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;

            process.StartInfo.AddArguments(arguments);

            // Set working directory
            if (_fileSystemMapper != null)
            {
                process.StartInfo.WorkingDirectory = _fileSystemMapper.GetWorkingDirectory();
            }

            // Set environment variables
            SetSafeEnvironment(process.StartInfo);

            // Execute with timeout
            var result = new CommandResult();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            try
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null && outputBuilder.Length < _policy.MaxOutputSize)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null && errorBuilder.Length < _policy.MaxOutputSize)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Use Task.Run for compatibility with older .NET versions
                await Task.Run(
                    () =>
                    {
                        while (!process.HasExited && !combinedCts.Token.IsCancellationRequested)
                        {
                            Thread.Sleep(100);
                        }
                    },
                    combinedCts.Token
                );

                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString();
                result.Error = errorBuilder.ToString();
                result.Success = process.ExitCode == 0;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Kill process on timeout
                try
                {
                    process.Kill();
                }
                catch { }

                throw new CommandTimeoutException(
                    $"Command execution timed out after {timeout.TotalSeconds}s",
                    "CommandExecution"
                );
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private void SetSafeEnvironment(ProcessStartInfo startInfo)
        {
            // Clear environment and set safe variables
            startInfo.Environment.Clear();

            if (_environmentEmulator != null)
            {
                var safeEnv = _environmentEmulator.GetAllEnvironmentVariables();
                foreach (var kvp in safeEnv)
                {
                    startInfo.Environment[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // Minimal safe environment
                startInfo.Environment["PATH"] = "/usr/bin:/bin";
                startInfo.Environment["HOME"] = "/tmp";
            }
        }
    }

    /// <summary>
    /// Policy for safe command execution
    /// </summary>
    public class SafeCommandPolicy
    {
        public bool Enabled { get; set; } = false;
        public CommandCategory AllowedCategories { get; set; } = CommandCategory.Safe;
        public int MaxArguments { get; set; } = 10;
        public int MaxArgumentLength { get; set; } = 1000;
        public int MaxOutputSize { get; set; } = 1024 * 1024; // 1MB
        public TimeSpan DefaultMaxExecutionTime { get; set; } = TimeSpan.FromSeconds(5);
        public List<string> BlockedArgumentPatterns { get; set; }
        public Dictionary<string, CommandDefinition> CustomCommands { get; set; }
    }

    /// <summary>
    /// Command categories for permission control
    /// </summary>
    [Flags]
    public enum CommandCategory
    {
        None = 0,
        Safe = 1, // Text processing, always safe
        Filesystem = 2, // File system inspection
        Development = 4, // Development tools (version info only)
        Custom = 8, // Custom user-defined commands
    }

    /// <summary>
    /// Definition of an allowed command
    /// </summary>
    public class CommandDefinition
    {
        public CommandCategory Category { get; set; }
        public TimeSpan? MaxExecutionTime { get; set; }
        public bool RequiresFileAccess { get; set; }
        public string[] AllowedArguments { get; set; } // If set, only these arguments allowed
        public string[] BlockedArguments { get; set; } // These arguments are blocked
    }

    /// <summary>
    /// Result of command execution
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }
}
