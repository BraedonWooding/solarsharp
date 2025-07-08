using System;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Represents a security event that occurred during script execution
    /// </summary>
    public class SecurityEvent
    {
        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Type of security event
        /// </summary>
        public SecurityEventType Type { get; set; }

        /// <summary>
        /// Operation that triggered the event
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Arguments passed to the operation
        /// </summary>
        public object[] Arguments { get; set; }

        /// <summary>
        /// Script identifier
        /// </summary>
        public string ScriptId { get; set; }

        /// <summary>
        /// Exception if any
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Stack trace at the time of event
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Additional details about the event
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Whether to terminate script execution
        /// </summary>
        public bool TerminateExecution { get; set; }

        /// <summary>
        /// How to handle this security violation
        /// </summary>
        public SecurityViolationHandling ViolationHandling { get; set; } =
            SecurityViolationHandling.Deny;
    }

    /// <summary>
    /// Handles security events
    /// </summary>
    public interface ISecurityEventHandler
    {
        /// <summary>
        /// Handles a security event
        /// </summary>
        void HandleSecurityEvent(SecurityEvent evt);
    }

    /// <summary>
    /// Default security event handler
    /// </summary>
    public class SecurityEventHandler : ISecurityEventHandler
    {
        /// <summary>
        /// Event raised when a security event occurs
        /// </summary>
        public event EventHandler<SecurityEventArgs> SecurityEventOccurred;

        /// <summary>
        /// Raised when any security violation occurs
        /// </summary>
        public event EventHandler<SecurityEventArgs> SecurityViolation;

        /// <summary>
        /// Raised when a resource is accessed (memory, CPU, etc)
        /// </summary>
        public event EventHandler<ResourceAccessEventArgs> ResourceAccess;

        /// <summary>
        /// Raised when file system access is attempted
        /// </summary>
        public event EventHandler<FileAccessEventArgs> FileAccess;

        /// <summary>
        /// Raised when network access is attempted
        /// </summary>
        public event EventHandler<NetworkAccessEventArgs> NetworkAccess;

        /// <summary>
        /// Raised when environment variable access is attempted
        /// </summary>
        public event EventHandler<EnvironmentAccessEventArgs> EnvironmentAccess;

        /// <summary>
        /// Handles a security event
        /// </summary>
        public void HandleSecurityEvent(SecurityEvent evt)
        {
            SecurityEventOccurred?.Invoke(this, new SecurityEventArgs(evt));
            SecurityViolation?.Invoke(this, new SecurityEventArgs(evt));
        }

        /// <summary>
        /// Raises a resource access event
        /// </summary>
        public void RaiseResourceAccess(string resourceType, long amount, string operation = null)
        {
            ResourceAccess?.Invoke(
                this,
                new ResourceAccessEventArgs
                {
                    ResourceType = resourceType,
                    Amount = amount,
                    Operation = operation,
                }
            );
        }

        /// <summary>
        /// Raises a file access event
        /// </summary>
        public void RaiseFileAccess(
            string path,
            FileAccessType accessType,
            bool allowed,
            bool isDirectory = false
        )
        {
            FileAccess?.Invoke(
                this,
                new FileAccessEventArgs
                {
                    Path = path,
                    AccessType = accessType,
                    Allowed = allowed,
                    IsDirectory = isDirectory,
                }
            );
        }

        /// <summary>
        /// Raises a network access event
        /// </summary>
        public void RaiseNetworkAccess(string host, int port, bool allowed)
        {
            NetworkAccess?.Invoke(
                this,
                new NetworkAccessEventArgs
                {
                    Host = host,
                    Port = port,
                    Allowed = allowed,
                }
            );
        }

        /// <summary>
        /// Raises an environment access event
        /// </summary>
        public void RaiseEnvironmentAccess(string variable, bool allowed)
        {
            EnvironmentAccess?.Invoke(
                this,
                new EnvironmentAccessEventArgs { Variable = variable, Allowed = allowed }
            );
        }
    }

    /// <summary>
    /// Event arguments for security events
    /// </summary>
    public class SecurityEventArgs : EventArgs
    {
        /// <summary>
        /// The security event
        /// </summary>
        public SecurityEvent Event { get; }

        /// <summary>
        /// The type of security event (convenience property)
        /// </summary>
        public SecurityEventType EventType
        {
            get { return Event?.Type ?? SecurityEventType.PolicyViolation; }
        }

        /// <summary>
        /// Details about the event (convenience property)
        /// </summary>
        public string Details
        {
            get
            {
                return Event?.Metadata?.ContainsKey("details") == true
                    ? Event.Metadata["details"]?.ToString()
                    : Event?.Operation;
            }
        }

        /// <summary>
        /// Creates new security event args
        /// </summary>
        public SecurityEventArgs(SecurityEvent evt)
        {
            Event = evt ?? throw new ArgumentNullException(nameof(evt));
        }
    }

    /// <summary>
    /// Event args for resource access events
    /// </summary>
    public class ResourceAccessEventArgs : EventArgs
    {
        public string ResourceType { get; set; }
        public long Amount { get; set; }
        public string Operation { get; set; }
        public long ExecutionTimeMs { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long InstructionCount { get; set; }
    }

    /// <summary>
    /// Event args for file access events
    /// </summary>
    public class FileAccessEventArgs : EventArgs
    {
        public string Path { get; set; }
        public FileAccessType AccessType { get; set; }
        public bool Allowed { get; set; }
        public bool IsDirectory { get; set; }
    }

    /// <summary>
    /// Types of file access
    /// </summary>
    public enum FileAccessType
    {
        Read,
        Write,
        Execute,
        Delete,
        Create,
        List,
    }

    /// <summary>
    /// Event args for network access events
    /// </summary>
    public class NetworkAccessEventArgs : EventArgs
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Allowed { get; set; }
    }

    /// <summary>
    /// Event args for environment access events
    /// </summary>
    public class EnvironmentAccessEventArgs : EventArgs
    {
        public string Variable { get; set; }
        public bool Allowed { get; set; }
    }

    /// <summary>
    /// How to handle security violations
    /// </summary>
    public enum SecurityViolationHandling
    {
        /// <summary>
        /// Deny the operation and return error/nil (default)
        /// </summary>
        Deny,

        /// <summary>
        /// Allow the operation to proceed
        /// </summary>
        Allow,

        /// <summary>
        /// Pretend the function/module doesn't exist (return nil)
        /// </summary>
        ReturnNil,

        /// <summary>
        /// Throw a Lua error explaining the security restriction
        /// </summary>
        ThrowError,

        /// <summary>
        /// Terminate script execution immediately
        /// </summary>
        Terminate,
    }
}
