#nullable enable

using System;
using JetBrains.Annotations;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// Represents the result of script execution
    /// </summary>
    [PublicAPI]
    public sealed record ScriptResult
    {
        /// <summary>
        /// Whether the script execution was successful
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// The return value from the script (if successful)
        /// </summary>
        public object? Value { get; init; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string ErrorMessage { get; init; } = "";

        /// <summary>
        /// Type of error that occurred
        /// </summary>
        public ScriptErrorType ErrorType { get; init; } = ScriptErrorType.None;

        /// <summary>
        /// Inner exception that caused the error (if any)
        /// </summary>
        public Exception? InnerException { get; init; }

        /// <summary>
        /// Whether the script execution failed
        /// </summary>
        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

        /// <summary>
        /// Creates a successful script result
        /// </summary>
        /// <param name="value">The return value from the script</param>
        /// <returns>A successful ScriptResult</returns>
        public static ScriptResult Success(object? value = null) =>
            new ScriptResult
            {
                IsSuccess = true,
                Value = value,
                ErrorType = ScriptErrorType.None,
            };

        /// <summary>
        /// Creates a failed script result
        /// </summary>
        /// <param name="errorMessage">Error message describing the failure</param>
        /// <param name="errorType">Type of error that occurred</param>
        /// <param name="innerException">Optional inner exception</param>
        /// <returns>A failed ScriptResult</returns>
        public static ScriptResult Failure(
            string errorMessage,
            ScriptErrorType errorType = ScriptErrorType.RuntimeError,
            Exception? innerException = null
        ) =>
            new ScriptResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorType = errorType,
                InnerException = innerException,
            };

        /// <summary>
        /// Creates a syntax error result
        /// </summary>
        /// <param name="errorMessage">Syntax error message</param>
        /// <param name="innerException">Optional inner exception</param>
        /// <returns>A failed ScriptResult with syntax error type</returns>
        public static ScriptResult SyntaxError(
            string errorMessage,
            Exception? innerException = null
        ) => Failure(errorMessage, ScriptErrorType.SyntaxError, innerException);

        /// <summary>
        /// Creates a security violation result
        /// </summary>
        /// <param name="errorMessage">Security violation message</param>
        /// <param name="innerException">Optional inner exception</param>
        /// <returns>A failed ScriptResult with security violation type</returns>
        public static ScriptResult SecurityViolation(
            string errorMessage,
            Exception? innerException = null
        ) => Failure(errorMessage, ScriptErrorType.SecurityViolation, innerException);

        /// <summary>
        /// Creates a resource exhausted result
        /// </summary>
        /// <param name="errorMessage">Resource exhaustion message</param>
        /// <param name="innerException">Optional inner exception</param>
        /// <returns>A failed ScriptResult with resource exhausted type</returns>
        public static ScriptResult ResourceExhausted(
            string errorMessage,
            Exception? innerException = null
        ) => Failure(errorMessage, ScriptErrorType.ResourceExhausted, innerException);

        /// <summary>
        /// Gets the value as the specified type, or throws if not successful
        /// </summary>
        /// <typeparam name="T">Type to cast the value to</typeparam>
        /// <returns>The value cast to the specified type</returns>
        /// <exception cref="InvalidOperationException">Thrown if the result is not successful</exception>
        /// <exception cref="InvalidCastException">Thrown if the value cannot be cast to the specified type</exception>
        public T GetValue<T>()
        {
            if (!IsSuccess)
                throw new InvalidOperationException(
                    $"Cannot get value from failed result: {ErrorMessage}"
                );

            return (T)Value!;
        }

        /// <summary>
        /// Gets the value as the specified type, or returns default if not successful
        /// </summary>
        /// <typeparam name="T">Type to cast the value to</typeparam>
        /// <param name="defaultValue">Default value to return if not successful</param>
        /// <returns>The value cast to the specified type, or the default value</returns>
        public T? GetValueOrDefault<T>(T? defaultValue = default)
        {
            if (!IsSuccess || Value is not T typedValue)
                return defaultValue;

            return typedValue;
        }
    }
}
