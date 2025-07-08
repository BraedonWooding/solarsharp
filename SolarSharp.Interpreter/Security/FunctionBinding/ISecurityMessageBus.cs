using System;
using System.Threading.Tasks;

namespace SolarSharp.Interpreter.Security.FunctionBinding
{
    /// <summary>
    /// Interface for security event messaging and audit logging.
    /// Provides a way to log security-related events through the message bus system.
    /// </summary>
    public interface ISecurityMessageBus
    {
        /// <summary>
        /// Logs a function access attempt (allowed or denied).
        /// </summary>
        Task LogFunctionAccessAsync(SecurityFunctionAccessEvent accessEvent);

        /// <summary>
        /// Logs a security policy violation.
        /// </summary>
        Task LogPolicyViolationAsync(SecurityPolicyViolationEvent violationEvent);

        /// <summary>
        /// Logs a privilege escalation or context transition.
        /// </summary>
        Task LogPrivilegeTransitionAsync(SecurityPrivilegeTransitionEvent transitionEvent);

        /// <summary>
        /// Logs function aliasing or modification attempts.
        /// </summary>
        Task LogFunctionModificationAsync(SecurityFunctionModificationEvent modificationEvent);
    }

    /// <summary>
    /// Represents a function access event for security logging.
    /// </summary>
    public sealed record SecurityFunctionAccessEvent
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string FunctionName { get; init; } = string.Empty;
        public string SourceFile { get; init; } = string.Empty;
        public string PolicyName { get; init; } = string.Empty;
        public bool AccessGranted { get; init; }
        public string DenialReason { get; init; } = string.Empty;
        public string RequiredModule { get; init; } = string.Empty;
        public string RequiredCapabilities { get; init; } = string.Empty;
        public string ContextId { get; init; } = string.Empty;
        public TimeSpan ExecutionTime { get; init; }

        public static SecurityFunctionAccessEvent Granted(
            string functionName,
            string sourceFile,
            string policyName,
            string contextId,
            TimeSpan executionTime = default
        ) =>
            new()
            {
                FunctionName = functionName,
                SourceFile = sourceFile,
                PolicyName = policyName,
                ContextId = contextId,
                AccessGranted = true,
                ExecutionTime = executionTime,
            };

        public static SecurityFunctionAccessEvent Denied(
            string functionName,
            string sourceFile,
            string policyName,
            string contextId,
            string denialReason,
            string requiredModule = "",
            string requiredCapabilities = ""
        ) =>
            new()
            {
                FunctionName = functionName,
                SourceFile = sourceFile,
                PolicyName = policyName,
                ContextId = contextId,
                AccessGranted = false,
                DenialReason = denialReason,
                RequiredModule = requiredModule,
                RequiredCapabilities = requiredCapabilities,
            };
    }

    /// <summary>
    /// Represents a security policy violation event.
    /// </summary>
    public sealed record SecurityPolicyViolationEvent
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string ViolationType { get; init; } = string.Empty;
        public string SourceFile { get; init; } = string.Empty;
        public string PolicyName { get; init; } = string.Empty;
        public string ViolationDetails { get; init; } = string.Empty;
        public string ContextId { get; init; } = string.Empty;
        public SecurityViolationSeverity Severity { get; init; } = SecurityViolationSeverity.Medium;
    }

    /// <summary>
    /// Represents a privilege transition between execution contexts.
    /// </summary>
    public sealed record SecurityPrivilegeTransitionEvent
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string FromContext { get; init; } = string.Empty;
        public string ToContext { get; init; } = string.Empty;
        public string FromPolicy { get; init; } = string.Empty;
        public string ToPolicy { get; init; } = string.Empty;
        public PrivilegeTransitionType TransitionType { get; init; }
        public string Reason { get; init; } = string.Empty;
        public bool IsEscalation { get; init; }
    }

    /// <summary>
    /// Represents a function modification event (aliasing, overriding, etc.).
    /// </summary>
    public sealed record SecurityFunctionModificationEvent
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string FunctionName { get; init; } = string.Empty;
        public string SourceFile { get; init; } = string.Empty;
        public string ContextId { get; init; } = string.Empty;
        public FunctionModificationType ModificationType { get; init; }
        public string PreviousValue { get; init; } = string.Empty;
        public string NewValue { get; init; } = string.Empty;
        public bool IsGlobalModification { get; init; }
    }

    public enum SecurityViolationSeverity
    {
        Low,
        Medium,
        High,
        Critical,
    }

    public enum PrivilegeTransitionType
    {
        FileToEval,
        EvalToFile,
        CrossFile,
        ContextInheritance,
        PolicyOverride,
    }

    public enum FunctionModificationType
    {
        Alias,
        Override,
        Remove,
        Restore,
    }
}
