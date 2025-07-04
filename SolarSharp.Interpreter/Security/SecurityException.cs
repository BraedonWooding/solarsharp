using System;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Abstract base class for all security exceptions. Forces concrete exception types.
    /// </summary>
    public abstract class SecurityException : InterpreterException
    {
        /// <summary>
        /// The type of security violation
        /// </summary>
        public SecurityEventType ViolationType { get; set; }

        /// <summary>
        /// The operation that was attempted
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Arguments passed to the operation
        /// </summary>
        public object[] Arguments { get; set; }

        /// <summary>
        /// Creates a new security exception with detailed information
        /// </summary>
        protected SecurityException(string message, SecurityEventType violationType, string operation, params object[] arguments) 
            : base(message)
        {
            ViolationType = violationType;
            Operation = operation;
            Arguments = arguments;
        }

        /// <summary>
        /// Creates a new security exception with inner exception
        /// </summary>
        protected SecurityException(string message, Exception innerException, SecurityEventType violationType, string operation, params object[] arguments) 
            : base(message, innerException)
        {
            ViolationType = violationType;
            Operation = operation;
            Arguments = arguments;
        }
    }

    /// <summary>
    /// Critical security exceptions that always throw regardless of configuration.
    /// These represent fundamental security boundary violations that can compromise system integrity.
    /// </summary>
    public abstract class CriticalSecurityException : SecurityException
    {
        protected CriticalSecurityException(string message, SecurityEventType violationType, string operation, params object[] arguments) 
            : base(message, violationType, operation, arguments)
        {
        }

        protected CriticalSecurityException(string message, Exception innerException, SecurityEventType violationType, string operation, params object[] arguments) 
            : base(message, innerException, violationType, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Non-critical security exceptions that only throw if explicitly opted-in via configuration.
    /// These represent access restrictions that can be handled gracefully by returning nil/error.
    /// </summary>
    public abstract class NonCriticalSecurityException : SecurityException
    {
        protected NonCriticalSecurityException(string message, SecurityEventType violationType, string operation, params object[] arguments) 
            : base(message, violationType, operation, arguments)
        {
        }

        protected NonCriticalSecurityException(string message, Exception innerException, SecurityEventType violationType, string operation, params object[] arguments) 
            : base(message, innerException, violationType, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Types of security events
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// Access to a resource was denied
        /// </summary>
        AccessDenied,

        /// <summary>
        /// A resource limit was exceeded
        /// </summary>
        ResourceLimitExceeded,

        /// <summary>
        /// Suspicious activity was detected
        /// </summary>
        SuspiciousActivity,

        /// <summary>
        /// A security policy was violated
        /// </summary>
        PolicyViolation,

        /// <summary>
        /// Unauthorized operation attempted
        /// </summary>
        UnauthorizedOperation,

        /// <summary>
        /// Execution timeout occurred
        /// </summary>
        ExecutionTimeout,

        /// <summary>
        /// Memory limit exceeded
        /// </summary>
        MemoryExhaustion,

        /// <summary>
        /// File access violation
        /// </summary>
        FileAccessViolation,

        /// <summary>
        /// Network access denied
        /// </summary>
        NetworkAccessDenied,

        /// <summary>
        /// Environment access denied
        /// </summary>
        EnvironmentAccessDenied,
        
        /// <summary>
        /// File access was denied
        /// </summary>
        FileAccessDenied,
        
        /// <summary>
        /// Environment variable access
        /// </summary>
        EnvironmentAccess,
        
        /// <summary>
        /// Process execution event
        /// </summary>
        ProcessExecution,
        
        /// <summary>
        /// Operation completed successfully (used for learning mode tracking)
        /// </summary>
        OperationSuccess,
        
        /// <summary>
        /// Capability usage event
        /// </summary>
        CapabilityUsage,
        
        /// <summary>
        /// Rate limit exceeded
        /// </summary>
        RateLimitExceeded,
        
        /// <summary>
        /// Capability granted to script
        /// </summary>
        CapabilityGranted,
        
        /// <summary>
        /// Capability revoked from script
        /// </summary>
        CapabilityRevoked,
        
        /// <summary>
        /// Security configuration changed
        /// </summary>
        SecurityConfigurationChanged,
        
        /// <summary>
        /// Reflection access denied
        /// </summary>
        ReflectionAccessDenied,
        
        /// <summary>
        /// Metatable violation
        /// </summary>
        MetatableViolation
    }
}