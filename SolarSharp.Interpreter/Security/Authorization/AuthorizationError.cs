using System;

namespace SolarSharp.Interpreter.Security.Authorization
{
    /// <summary>
    /// Represents an authorization failure with context about what operation was denied and why.
    /// This is a domain error type that provides structured information about security violations.
    /// </summary>
    public sealed record AuthorizationError
    {
        /// <summary>
        /// The type of operation that was denied
        /// </summary>
        public OperationType OperationType { get; init; }

        /// <summary>
        /// Human-readable description of why the operation was denied
        /// </summary>
        public string Message { get; init; }

        /// <summary>
        /// The execution context where the denial occurred
        /// </summary>
        public string Context { get; init; }

        /// <summary>
        /// The policy that caused the denial
        /// </summary>
        public string PolicyName { get; init; }

        /// <summary>
        /// Creates a new authorization error
        /// </summary>
        /// <param name="operationType">The type of operation that was denied</param>
        /// <param name="message">Human-readable description of the denial</param>
        /// <param name="context">The execution context where the denial occurred</param>
        /// <param name="policyName">The policy that caused the denial</param>
        public AuthorizationError(
            OperationType operationType,
            string message,
            string context,
            string policyName
        )
        {
            OperationType = operationType;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            PolicyName = policyName ?? throw new ArgumentNullException(nameof(policyName));
        }

        /// <summary>
        /// Creates an authorization error for dynamic code execution denial
        /// </summary>
        public static AuthorizationError DynamicCodeExecutionDenied(
            string context,
            string policyName
        ) =>
            new(
                OperationType.DynamicCodeExecution,
                "Dynamic code execution is not allowed by the current security policy",
                context,
                policyName
            );

        /// <summary>
        /// Creates an authorization error for file access denial
        /// </summary>
        public static AuthorizationError FileAccessDenied(string filePath, string policyName) =>
            new(
                OperationType.FileRead,
                $"File access denied for: {filePath}",
                filePath,
                policyName
            );

        /// <summary>
        /// Creates an authorization error for network access denial
        /// </summary>
        public static AuthorizationError NetworkAccessDenied(string host, string policyName) =>
            new(
                OperationType.NetworkAccess,
                $"Network access denied for: {host}",
                host,
                policyName
            );
    }
}
