#nullable enable

using System;
using JetBrains.Annotations;

namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Immutable error type for script execution failures
    /// </summary>
    [PublicAPI]
    public sealed record ScriptError
    {
        /// <summary>
        /// Type of script error
        /// </summary>
        public ScriptErrorType Type { get; init; }

        /// <summary>
        /// Human-readable error message
        /// </summary>
        public string Message { get; init; } = "";

        /// <summary>
        /// Optional inner exception that caused this error
        /// </summary>
        public Exception? InnerException { get; init; }

        /// <summary>
        /// Optional context information about where the error occurred
        /// </summary>
        public string Context { get; init; } = "";

        /// <summary>
        /// Creates a compilation error
        /// </summary>
        public static ScriptError CompilationError(
            string message,
            Exception? innerException = null
        ) =>
            new ScriptError
            {
                Type = ScriptErrorType.Compilation,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a runtime error
        /// </summary>
        public static ScriptError RuntimeError(string message, Exception? innerException = null) =>
            new ScriptError
            {
                Type = ScriptErrorType.Runtime,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a security violation error
        /// </summary>
        public static ScriptError SecurityViolation(
            string message,
            Exception? innerException = null
        ) =>
            new ScriptError
            {
                Type = ScriptErrorType.Security,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a timeout error
        /// </summary>
        public static ScriptError Timeout(string message, Exception? innerException = null) =>
            new ScriptError
            {
                Type = ScriptErrorType.Timeout,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a memory limit error
        /// </summary>
        public static ScriptError MemoryLimit(string message, Exception? innerException = null) =>
            new ScriptError
            {
                Type = ScriptErrorType.MemoryLimit,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a configuration error
        /// </summary>
        public static ScriptError Configuration(string message, Exception? innerException = null) =>
            new ScriptError
            {
                Type = ScriptErrorType.Configuration,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a syntax error
        /// </summary>
        public static ScriptError SyntaxError(string message, Exception? innerException = null) =>
            new ScriptError
            {
                Type = ScriptErrorType.Compilation,
                Message = message,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a resource exhausted error
        /// </summary>
        public static ScriptError ResourceExhausted(
            string message,
            Exception? innerException = null
        ) =>
            new ScriptError
            {
                Type = ScriptErrorType.Timeout,
                Message = message,
                InnerException = innerException,
            };

        public override string ToString() =>
            string.IsNullOrEmpty(Context)
                ? $"{Type}: {Message}"
                : $"{Type} in {Context}: {Message}";
    }

    /// <summary>
    /// Types of script execution errors
    /// </summary>
    [PublicAPI]
    public enum ScriptErrorType
    {
        /// <summary>
        /// Error during script compilation/parsing
        /// </summary>
        Compilation,

        /// <summary>
        /// Error during script execution
        /// </summary>
        Runtime,

        /// <summary>
        /// Security policy violation
        /// </summary>
        Security,

        /// <summary>
        /// Script execution timeout
        /// </summary>
        Timeout,

        /// <summary>
        /// Memory limit exceeded
        /// </summary>
        MemoryLimit,

        /// <summary>
        /// Configuration error
        /// </summary>
        Configuration,
    }
}
