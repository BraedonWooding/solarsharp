using System;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines environment access security policies
    /// </summary>
    public class EnvironmentSecurity
    {
        /// <summary>
        /// Whether environment variable access is allowed
        /// </summary>
        public bool AllowAccess { get; set; }

        /// <summary>
        /// Environment variables that can be accessed
        /// </summary>
        public List<string> AllowedVariables { get; set; } = new List<string>();

        /// <summary>
        /// Environment variables that are explicitly denied
        /// </summary>
        public List<string> DeniedVariables { get; set; } = new List<string>();

        /// <summary>
        /// Whether to allow reading system information (OS, architecture, etc.)
        /// </summary>
        public bool AllowSystemInfo { get; set; }

        /// <summary>
        /// Creates a configuration with no environment access
        /// </summary>
        public static EnvironmentSecurity NoAccess() =>
            new EnvironmentSecurity { AllowAccess = false };

        /// <summary>
        /// Creates a configuration with limited access to specific variables
        /// </summary>
        public static EnvironmentSecurity LimitedAccess(params string[] allowedVars) =>
            new EnvironmentSecurity
            {
                AllowAccess = true,
                AllowedVariables = new List<string>(allowedVars),
            };

        /// <summary>
        /// Creates a configuration with safe environment access
        /// </summary>
        public static EnvironmentSecurity SafeAccess() =>
            new EnvironmentSecurity
            {
                AllowAccess = true,
                AllowedVariables = new List<string> { "TEMP", "TMP", "USER", "HOME", "PATH" },
                DeniedVariables = new List<string>
                {
                    "PASSWORD",
                    "SECRET",
                    "KEY",
                    "TOKEN",
                    "API_KEY",
                    "CREDENTIALS",
                },
                AllowSystemInfo = true,
            };

        /// <summary>
        /// Checks if a variable name matches any pattern in a list
        /// </summary>
        internal bool MatchesPattern(string variableName, List<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                // Simple pattern matching: * matches any characters
                if (pattern.Contains("*"))
                {
                    var parts = pattern.Split('*');
                    if (parts.Length == 2)
                    {
                        if (parts[0].Length > 0 && !variableName.StartsWith(parts[0]))
                            continue;
                        if (parts[1].Length > 0 && !variableName.EndsWith(parts[1]))
                            continue;
                        return true;
                    }
                }
                else if (variableName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
