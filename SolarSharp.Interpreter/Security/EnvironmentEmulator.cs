using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides secure environment variable emulation for sandboxed scripts
    /// </summary>
    public class EnvironmentEmulator
    {
        private readonly Dictionary<string, string> _emulatedVariables;
        private readonly HashSet<string> _passthroughVariables;
        private readonly List<Regex> _blockedPatterns;
        private readonly List<Regex> _passthroughPatterns;
        private readonly EnvironmentEmulationPolicy _policy;
        private readonly List<string> _allowedVariables;

        public EnvironmentEmulator(EnvironmentEmulationPolicy policy, List<string> allowedVariables = null)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _emulatedVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _passthroughVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _blockedPatterns = new List<Regex>();
            _passthroughPatterns = new List<Regex>();
            _allowedVariables = allowedVariables ?? new List<string>();
            
            Initialize();
        }

        /// <summary>
        /// Gets an environment variable value with security controls applied.
        /// Checks against blocked patterns, validates allowed variables in sandboxed mode,
        /// returns emulated values when available, or allows passthrough for approved variables.
        /// </summary>
        /// <param name="variableName">The name of the environment variable to retrieve</param>
        /// <returns>The variable value, or null if blocked/not allowed</returns>
        public string GetEnvironmentVariable(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return null;

            // Check if variable is explicitly blocked
            if (IsVariableBlocked(variableName))
                return null;

            // In sandboxed mode, check if variable is in the allowed list
            if (_policy.Mode == EnvironmentMode.Sandboxed && _allowedVariables.Count > 0)
            {
                bool isAllowed = false;
                foreach (var pattern in _allowedVariables)
                {
                    if (IsMatch(variableName, pattern))
                    {
                        isAllowed = true;
                        break;
                    }
                }
                
                if (!isAllowed)
                {
                    return null;
                }
            }

            // Check if we have an emulated value
            if (_emulatedVariables.TryGetValue(variableName, out string emulatedValue))
                return emulatedValue;

            // Check if variable is allowed to pass through
            if (IsVariableAllowed(variableName))
                return Environment.GetEnvironmentVariable(variableName);

            // Default: block unknown variables in sandboxed mode
            return _policy.Mode == EnvironmentMode.Sandboxed ? null : Environment.GetEnvironmentVariable(variableName);
        }

        /// <summary>
        /// Gets all available environment variables (emulated + allowed passthrough).
        /// Returns a dictionary containing both emulated variables and real environment
        /// variables that pass security validation.
        /// </summary>
        /// <returns>Dictionary of available environment variables</returns>
        public Dictionary<string, string> GetAllEnvironmentVariables()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Add emulated variables
            foreach (var kvp in _emulatedVariables)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Add allowed passthrough variables
            if (_policy.Mode != EnvironmentMode.Isolated)
            {
                foreach (var variable in _passthroughVariables)
                {
                    var value = Environment.GetEnvironmentVariable(variable);
                    if (value != null)
                        result[variable] = value;
                }

                // Add pattern-matched passthrough variables
                foreach (var envVar in Environment.GetEnvironmentVariables().Keys)
                {
                    var varName = envVar.ToString();
                    if (IsVariableAllowed(varName) && !IsVariableBlocked(varName))
                    {
                        var value = Environment.GetEnvironmentVariable(varName);
                        if (value != null)
                            result[varName] = value;
                    }
                }
            }

            return result;
        }

        private void Initialize()
        {
            // Initialize blocked patterns for security-critical variables
            InitializeBlockedPatterns();
            
            // Initialize emulated variables based on policy
            InitializeEmulatedVariables();
            
            // Initialize passthrough variables and patterns
            InitializePassthroughVariables();
        }

        private void InitializeBlockedPatterns()
        {
            var blockedPatterns = new List<string>();

            // Add default dangerous patterns
            if (_policy.BlockDangerousVariables)
            {
                // Dynamic loader variables (Unix/Linux/macOS)
                blockedPatterns.AddRange(new[]
                {
                    @"^DYLD_.*",           // macOS dynamic loader
                    @"^LD_.*",             // Linux dynamic loader
                    @"^LIBPATH$",          // AIX library path
                    @"^SHLIB_PATH$",       // HP-UX shared library path
                    @"^_$",                // Last executed command
                    @"^IFS$",              // Shell field separator
                    @"^PS[1-4]$",          // Shell prompts (can execute code)
                    @"^PATH_.*",           // Shell path variables
                    @"^BASH_.*",           // Bash-specific variables
                    @"^SHELL_.*",          // Shell-specific variables
                });

                // Windows-specific dangerous variables
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    blockedPatterns.AddRange(new[]
                    {
                        @"^PATHEXT$",      // Executable extensions
                        @"^COMSPEC$",      // Command processor
                    });
                }
            }

            // Add custom blocked patterns from policy
            if (_policy.BlockedVariables != null)
            {
                blockedPatterns.AddRange(_policy.BlockedVariables);
            }

            // Compile patterns
            foreach (var pattern in blockedPatterns)
            {
                try
                {
                    _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
                }
                catch (ArgumentException)
                {
                    // Skip invalid patterns
                }
            }
        }

        private void InitializeEmulatedVariables()
        {
            // Set up default emulated variables for sandboxed mode
            if (_policy.Mode == EnvironmentMode.Sandboxed)
            {
                // Common system variables with safe defaults
                _emulatedVariables["HOME"] = _policy.SandboxHome ?? "/sandbox/home";
                _emulatedVariables["USER"] = _policy.SandboxUser ?? "sandbox_user";
                _emulatedVariables["USERNAME"] = _policy.SandboxUser ?? "sandbox_user"; // Windows
                _emulatedVariables["LOGNAME"] = _policy.SandboxUser ?? "sandbox_user";
                _emulatedVariables["SHELL"] = "/bin/sh";
                _emulatedVariables["TERM"] = "xterm";
                _emulatedVariables["TMPDIR"] = _policy.SandboxTemp ?? "/sandbox/temp";
                _emulatedVariables["TMP"] = _policy.SandboxTemp ?? "/sandbox/temp"; // Windows
                _emulatedVariables["TEMP"] = _policy.SandboxTemp ?? "/sandbox/temp"; // Windows
                _emulatedVariables["PWD"] = _policy.WorkingDirectory ?? "/workspace";
                _emulatedVariables["OLDPWD"] = _policy.WorkingDirectory ?? "/workspace";
                
                // Safe PATH with limited binaries
                _emulatedVariables["PATH"] = _policy.SandboxPath ?? "/sandbox/bin:/usr/bin:/bin";
                
                // Sandbox identification
                _emulatedVariables["SANDBOX_MODE"] = "true";
                _emulatedVariables["SOLARSHARP_SANDBOX"] = "true";
            }

            // Add custom emulated variables from policy
            if (_policy.EmulatedVariables != null)
            {
                foreach (var kvp in _policy.EmulatedVariables)
                {
                    _emulatedVariables[kvp.Key] = kvp.Value;
                }
            }
        }

        private void InitializePassthroughVariables()
        {
            // Safe variables that can be passed through
            var safeVariables = new[]
            {
                "LANG", "LANGUAGE", "LC_ALL", "LC_CTYPE", "LC_NUMERIC", "LC_TIME",
                "LC_COLLATE", "LC_MONETARY", "LC_MESSAGES", "LC_PAPER", "LC_NAME",
                "LC_ADDRESS", "LC_TELEPHONE", "LC_MEASUREMENT", "LC_IDENTIFICATION",
                "TZ", "TIMEZONE"
            };

            foreach (var variable in safeVariables)
            {
                _passthroughVariables.Add(variable);
            }

            // Add policy-specified passthrough variables
            if (_policy.PassthroughVariables != null)
            {
                foreach (var variable in _policy.PassthroughVariables)
                {
                    _passthroughVariables.Add(variable);
                }
            }

            // Compile passthrough patterns
            if (_policy.PassthroughPatterns != null)
            {
                foreach (var pattern in _policy.PassthroughPatterns)
                {
                    try
                    {
                        _passthroughPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
                    }
                    catch (ArgumentException)
                    {
                        // Skip invalid patterns
                    }
                }
            }
        }

        private bool IsVariableBlocked(string variableName)
        {
            foreach (var pattern in _blockedPatterns)
            {
                if (pattern.IsMatch(variableName))
                    return true;
            }
            return false;
        }

        private bool IsVariableAllowed(string variableName)
        {
            // Check explicit passthrough list
            if (_passthroughVariables.Contains(variableName))
                return true;

            // Check passthrough patterns
            foreach (var pattern in _passthroughPatterns)
            {
                if (pattern.IsMatch(variableName))
                    return true;
            }

            return false;
        }
        
        private bool IsMatch(string value, string pattern)
        {
            // Simple wildcard matching
            if (pattern.EndsWith("*"))
            {
                string prefix = pattern.Substring(0, pattern.Length - 1);
                return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            else if (pattern.StartsWith("*"))
            {
                string suffix = pattern.Substring(1);
                return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }
            else if (pattern.Contains("*"))
            {
                // Split on * and check if all parts are present in order
                string[] parts = pattern.Split('*');
                int currentIndex = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (string.IsNullOrEmpty(parts[i]))
                        continue;
                        
                    int index = value.IndexOf(parts[i], currentIndex, StringComparison.OrdinalIgnoreCase);
                    if (index == -1)
                        return false;
                        
                    currentIndex = index + parts[i].Length;
                }
                return true;
            }
            else
            {
                return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Policy configuration for environment variable emulation
    /// </summary>
    public class EnvironmentEmulationPolicy
    {
        public EnvironmentMode Mode { get; set; } = EnvironmentMode.Passthrough;
        public bool BlockDangerousVariables { get; set; } = true;
        
        // Sandbox settings
        public string SandboxHome { get; set; } = "/sandbox/home";
        public string SandboxUser { get; set; } = "sandbox_user";
        public string SandboxTemp { get; set; } = "/sandbox/temp";
        public string SandboxPath { get; set; } = "/sandbox/bin:/usr/bin:/bin";
        public string WorkingDirectory { get; set; } = "/workspace";
        
        // Variable lists
        public List<string> BlockedVariables { get; set; } = new List<string>();
        public Dictionary<string, string> EmulatedVariables { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> PassthroughVariables { get; set; } = new List<string>();
        public List<string> PassthroughPatterns { get; set; } = new List<string>();
    }

    /// <summary>
    /// Environment emulation modes
    /// </summary>
    public enum EnvironmentMode
    {
        /// <summary>
        /// Pass through all environment variables (least secure)
        /// </summary>
        Passthrough,
        
        /// <summary>
        /// Sandboxed mode with emulated variables and strict filtering
        /// </summary>
        Sandboxed,
        
        /// <summary>
        /// Isolated mode with no access to host environment
        /// </summary>
        Isolated
    }
}