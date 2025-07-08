#nullable enable

using System;
using System.Collections.Immutable;
using System.Text;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Security.Auditing
{
    /// <summary>
    /// Event raised when eval execution is authorized
    /// </summary>
    public sealed record EvalExecutionAuthorizedEvent : SecurityAuditEvent
    {
        /// <summary>
        /// The Lua code being executed
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Security policy that authorized the execution
        /// </summary>
        public string AuthorizingPolicy { get; init; } = string.Empty;

        /// <summary>
        /// Trust level of the script
        /// </summary>
        public string TrustLevel { get; init; } = string.Empty;

        /// <summary>
        /// Size of the code being executed in bytes
        /// </summary>
        public long CodeSize { get; init; }

        /// <summary>
        /// Execution context information
        /// </summary>
        public string? ExecutionContext { get; init; }

        /// <summary>
        /// Maximum execution time allowed
        /// </summary>
        public TimeSpan? ExecutionTimeout { get; init; }

        /// <summary>
        /// Resource limits applied to the execution
        /// </summary>
        public ImmutableDictionary<string, object> ResourceLimits { get; init; } =
            ImmutableDictionary<string, object>.Empty;

        /// <summary>
        /// Creates a new eval execution authorized event
        /// </summary>
        public EvalExecutionAuthorizedEvent()
            : base(SecurityEventType.EvalExecutionAuthorized)
        {
            Success = true;
            Operation = "eval_execution_authorized";
        }

        /// <summary>
        /// Creates a new eval execution authorized event with specified parameters
        /// </summary>
        public EvalExecutionAuthorizedEvent(
            string scriptId,
            string code,
            string authorizingPolicy,
            string trustLevel,
            string principal = "",
            ScriptIdentity? scriptIdentity = null,
            string? executionContext = null,
            TimeSpan? executionTimeout = null,
            ImmutableDictionary<string, object>? resourceLimits = null
        )
            : this()
        {
            ScriptId = scriptId;
            Code = code;
            AuthorizingPolicy = authorizingPolicy;
            TrustLevel = trustLevel;
            Principal = principal;
            ScriptIdentity = scriptIdentity;
            ExecutionContext = executionContext;
            ExecutionTimeout = executionTimeout;
            ResourceLimits = resourceLimits ?? ImmutableDictionary<string, object>.Empty;
            CodeSize = Encoding.UTF8.GetByteCount(code);
        }

        /// <summary>
        /// Creates a copy with additional resource limit
        /// </summary>
        public EvalExecutionAuthorizedEvent WithResourceLimit(string resourceType, object limit)
        {
            return this with { ResourceLimits = ResourceLimits.Add(resourceType, limit) };
        }

        /// <summary>
        /// Creates a copy with execution context
        /// </summary>
        public EvalExecutionAuthorizedEvent WithExecutionContext(string context)
        {
            return this with { ExecutionContext = context };
        }

        /// <summary>
        /// Creates a copy with execution timeout
        /// </summary>
        public EvalExecutionAuthorizedEvent WithExecutionTimeout(TimeSpan timeout)
        {
            return this with { ExecutionTimeout = timeout };
        }
    }

    /// <summary>
    /// Event raised when eval execution is denied
    /// </summary>
    public sealed record EvalExecutionDeniedEvent : SecurityAuditEvent
    {
        /// <summary>
        /// The Lua code that was denied execution
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Reason for denial
        /// </summary>
        public string DenialReason { get; init; } = string.Empty;

        /// <summary>
        /// Security policy that denied the execution
        /// </summary>
        public string DenyingPolicy { get; init; } = string.Empty;

        /// <summary>
        /// Trust level of the script
        /// </summary>
        public string TrustLevel { get; init; } = string.Empty;

        /// <summary>
        /// Size of the code that was denied in bytes
        /// </summary>
        public long CodeSize { get; init; }

        /// <summary>
        /// Type of security violation that occurred
        /// </summary>
        public SecurityViolationType ViolationType { get; init; } =
            SecurityViolationType.PolicyViolation;

        /// <summary>
        /// Execution context information
        /// </summary>
        public string? ExecutionContext { get; init; }

        /// <summary>
        /// Security risk assessment score (0-100)
        /// </summary>
        public int RiskScore { get; init; }

        /// <summary>
        /// Creates a new eval execution denied event
        /// </summary>
        public EvalExecutionDeniedEvent()
            : base(SecurityEventType.EvalExecutionDenied)
        {
            Success = false;
            Operation = "eval_execution_denied";
        }

        /// <summary>
        /// Creates a new eval execution denied event with specified parameters
        /// </summary>
        public EvalExecutionDeniedEvent(
            string scriptId,
            string code,
            string denialReason,
            string denyingPolicy,
            string trustLevel,
            SecurityViolationType violationType = SecurityViolationType.PolicyViolation,
            int riskScore = 0,
            string principal = "",
            ScriptIdentity? scriptIdentity = null,
            string? executionContext = null,
            Exception? exception = null
        )
            : this()
        {
            ScriptId = scriptId;
            Code = code;
            DenialReason = denialReason;
            DenyingPolicy = denyingPolicy;
            TrustLevel = trustLevel;
            ViolationType = violationType;
            RiskScore = riskScore;
            Principal = principal;
            ScriptIdentity = scriptIdentity;
            ExecutionContext = executionContext;
            Exception = exception;
            ErrorMessage = denialReason;
            CodeSize = Encoding.UTF8.GetByteCount(code);
        }

        /// <summary>
        /// Creates a copy with execution context
        /// </summary>
        public EvalExecutionDeniedEvent WithExecutionContext(string context)
        {
            return this with { ExecutionContext = context };
        }

        /// <summary>
        /// Creates a copy with updated risk score
        /// </summary>
        public EvalExecutionDeniedEvent WithRiskScore(int score)
        {
            return this with { RiskScore = Math.Max(0, Math.Min(100, score)) };
        }
    }

    /// <summary>
    /// Types of security violations for eval execution
    /// </summary>
    public enum SecurityViolationType
    {
        /// <summary>
        /// General policy violation
        /// </summary>
        PolicyViolation,

        /// <summary>
        /// Insufficient trust level
        /// </summary>
        InsufficientTrustLevel,

        /// <summary>
        /// Code contains suspicious patterns
        /// </summary>
        SuspiciousCode,

        /// <summary>
        /// Code exceeds size limits
        /// </summary>
        CodeSizeExceeded,

        /// <summary>
        /// Resource limits would be exceeded
        /// </summary>
        ResourceLimitExceeded,

        /// <summary>
        /// Execution context violation
        /// </summary>
        ExecutionContextViolation,

        /// <summary>
        /// Unsigned code not allowed
        /// </summary>
        UnsignedCodeDenied,

        /// <summary>
        /// Untrusted signature
        /// </summary>
        UntrustedSignature,
    }

    /// <summary>
    /// Factory methods for creating eval execution events
    /// </summary>
    public static class EvalExecutionEventFactory
    {
        /// <summary>
        /// Creates an authorized event for successful eval execution
        /// </summary>
        public static EvalExecutionAuthorizedEvent CreateAuthorized(
            string scriptId,
            string code,
            string authorizingPolicy,
            string trustLevel,
            string principal = ""
        )
        {
            return new EvalExecutionAuthorizedEvent(
                scriptId,
                code,
                authorizingPolicy,
                trustLevel,
                principal
            );
        }

        /// <summary>
        /// Creates a denied event for failed eval execution
        /// </summary>
        public static EvalExecutionDeniedEvent CreateDenied(
            string scriptId,
            string code,
            string denialReason,
            string denyingPolicy,
            string trustLevel,
            SecurityViolationType violationType = SecurityViolationType.PolicyViolation,
            string principal = ""
        )
        {
            return new EvalExecutionDeniedEvent(
                scriptId,
                code,
                denialReason,
                denyingPolicy,
                trustLevel,
                violationType,
                0,
                principal
            );
        }

        /// <summary>
        /// Creates a denied event with exception details
        /// </summary>
        public static EvalExecutionDeniedEvent CreateDeniedWithException(
            string scriptId,
            string code,
            string denyingPolicy,
            string trustLevel,
            Exception exception,
            SecurityViolationType violationType = SecurityViolationType.PolicyViolation,
            string principal = ""
        )
        {
            return new EvalExecutionDeniedEvent(
                scriptId,
                code,
                exception.Message,
                denyingPolicy,
                trustLevel,
                violationType,
                0,
                principal,
                null,
                null,
                exception
            );
        }
    }
}
