using System;

namespace SolarSharp.Interpreter.Security
{
    // ===============================================
    // CRITICAL SECURITY EXCEPTIONS (Always throw)
    // ===============================================

    #region Resource Exhaustion - Always Critical

    /// <summary>
    /// Thrown when a resource limit is exceeded (memory, instructions, etc.)
    /// </summary>
    public class ResourceLimitExceededException : CriticalSecurityException
    {
        public ResourceLimitExceededException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.ResourceLimitExceeded, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when script execution timeout is reached
    /// </summary>
    public class ExecutionTimeoutException : CriticalSecurityException
    {
        public ExecutionTimeoutException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.ExecutionTimeout, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when memory limit is exceeded
    /// </summary>
    public class MemoryExhaustionException : CriticalSecurityException
    {
        public MemoryExhaustionException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.MemoryExhaustion, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when call depth limit is exceeded
    /// </summary>
    public class CallDepthExceededException : CriticalSecurityException
    {
        public CallDepthExceededException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.ResourceLimitExceeded, operation, arguments)
        {
        }
    }

    #endregion

    #region Path Security - Always Critical

    /// <summary>
    /// Thrown when path traversal attack is detected
    /// </summary>
    public class PathTraversalException : CriticalSecurityException
    {
        public PathTraversalException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when an invalid or malicious path is detected
    /// </summary>
    public class InvalidPathException : CriticalSecurityException
    {
        public InvalidPathException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    #endregion

    #region Manifest Security - Always Critical

    /// <summary>
    /// Thrown when manifest signature verification fails
    /// </summary>
    public class ManifestSignatureException : CriticalSecurityException
    {
        public ManifestSignatureException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.PolicyViolation, operation, arguments)
        {
        }

        public ManifestSignatureException(string message, Exception innerException, string operation, params object[] arguments)
            : base(message, innerException, SecurityEventType.PolicyViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when manifest JSON format is invalid
    /// </summary>
    public class ManifestFormatException : CriticalSecurityException
    {
        public ManifestFormatException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.PolicyViolation, operation, arguments)
        {
        }

        public ManifestFormatException(string message, Exception innerException, string operation, params object[] arguments)
            : base(message, innerException, SecurityEventType.PolicyViolation, operation, arguments)
        {
        }
    }

    #endregion

    #region Write Operations - Always Critical

    /// <summary>
    /// Thrown when unauthorized file write is attempted
    /// </summary>
    public class UnauthorizedFileWriteException : CriticalSecurityException
    {
        public UnauthorizedFileWriteException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when unauthorized process execution is attempted
    /// </summary>
    public class UnauthorizedProcessExecutionException : CriticalSecurityException
    {
        public UnauthorizedProcessExecutionException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.UnauthorizedOperation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when attempting to write to .lua files (anti-polymorphism protection)
    /// </summary>
    public class LuaFileWriteViolationException : CriticalSecurityException
    {
        public LuaFileWriteViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when attempting to write to manifest files (anti-polymorphism protection)
    /// </summary>
    public class ManifestWriteViolationException : CriticalSecurityException
    {
        public ManifestWriteViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    #endregion

    // ===============================================
    // NON-CRITICAL SECURITY EXCEPTIONS (Optional throw)
    // ===============================================

    #region Read Access Restrictions - Non-Critical

    /// <summary>
    /// Thrown when file read access is denied
    /// </summary>
    public class FileReadDeniedException : NonCriticalSecurityException
    {
        public FileReadDeniedException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessDenied, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when directory access is denied
    /// </summary>
    public class DirectoryAccessDeniedException : NonCriticalSecurityException
    {
        public DirectoryAccessDeniedException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessDenied, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when network access is denied
    /// </summary>
    public class NetworkAccessDeniedException : NonCriticalSecurityException
    {
        public NetworkAccessDeniedException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.NetworkAccessDenied, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when environment variable access is denied
    /// </summary>
    public class EnvironmentAccessDeniedException : NonCriticalSecurityException
    {
        public EnvironmentAccessDeniedException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.EnvironmentAccessDenied, operation, arguments)
        {
        }
    }

    #endregion

    #region Anti-Polymorphism Read Restrictions - Non-Critical

    /// <summary>
    /// Thrown when manifest file read is blocked (anti-polymorphism protection)
    /// </summary>
    public class ManifestReadViolationException : NonCriticalSecurityException
    {
        public ManifestReadViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when access to blocked file extension is attempted
    /// </summary>
    public class BlockedExtensionException : NonCriticalSecurityException
    {
        public BlockedExtensionException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when access to protected file is attempted
    /// </summary>
    public class ProtectedFileAccessException : NonCriticalSecurityException
    {
        public ProtectedFileAccessException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    #endregion

    #region Capability Restrictions - Non-Critical

    /// <summary>
    /// Thrown when required capability is missing
    /// </summary>
    public class MissingCapabilityException : NonCriticalSecurityException
    {
        public MissingCapabilityException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.AccessDenied, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when file access is denied (general file access violation)
    /// </summary>
    public class FileAccessViolationException : NonCriticalSecurityException
    {
        public FileAccessViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.FileAccessViolation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when process execution is denied
    /// </summary>
    public class ProcessExecutionViolationException : NonCriticalSecurityException
    {
        public ProcessExecutionViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.UnauthorizedOperation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when command execution is denied
    /// </summary>
    public class CommandExecutionViolationException : NonCriticalSecurityException
    {
        public CommandExecutionViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.UnauthorizedOperation, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when command execution times out
    /// </summary>
    public class CommandTimeoutException : CriticalSecurityException
    {
        public CommandTimeoutException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.ResourceLimitExceeded, operation, arguments)
        {
        }
    }

    /// <summary>
    /// Thrown when metatable manipulation is attempted in secure contexts
    /// </summary>
    public class MetatableViolationException : CriticalSecurityException
    {
        public MetatableViolationException(string message, string operation, params object[] arguments)
            : base(message, SecurityEventType.UnauthorizedOperation, operation, arguments)
        {
        }
    }

    #endregion
}