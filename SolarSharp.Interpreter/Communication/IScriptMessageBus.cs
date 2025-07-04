using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Secure message bus for inter-script communication
    /// </summary>
    public interface IScriptMessageBus
    {
        /// <summary>
        /// Subscribes a script to messages of a specific type
        /// </summary>
        void Subscribe(string messageType, string scriptId, Func<ScriptMessage, Task<ScriptMessage>> handler);

        /// <summary>
        /// Unsubscribes a script from a message type
        /// </summary>
        void Unsubscribe(string messageType, string scriptId);

        /// <summary>
        /// Publishes a message to all subscribers
        /// </summary>
        Task<IReadOnlyList<ScriptMessage>> PublishAsync(ScriptMessage message);

        /// <summary>
        /// Sends a direct message to a specific script
        /// </summary>
        Task<ScriptMessage> SendDirectAsync(ScriptMessage message, string targetScriptId);

        /// <summary>
        /// Registers a script with the message bus
        /// </summary>
        void RegisterScript(string scriptId, ScriptCommunicationPolicy policy);

        /// <summary>
        /// Unregisters a script from the message bus
        /// </summary>
        void UnregisterScript(string scriptId);

        /// <summary>
        /// Gets active subscriptions for monitoring
        /// </summary>
        IReadOnlyDictionary<string, IReadOnlyList<string>> GetSubscriptions();

        /// <summary>
        /// Gets communication statistics
        /// </summary>
        MessageBusStats GetStats();
    }

    /// <summary>
    /// Message exchanged between scripts
    /// </summary>
    public class ScriptMessage
    {
        /// <summary>
        /// Unique message identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Message type for routing
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Source script identifier
        /// </summary>
        public string FromScript { get; set; } = string.Empty;

        /// <summary>
        /// Target script identifier (for direct messages)
        /// </summary>
        public string ToScript { get; set; } = string.Empty;

        /// <summary>
        /// Message payload
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Message timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Message priority
        /// </summary>
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;

        /// <summary>
        /// Time-to-live for the message
        /// </summary>
        public TimeSpan? TTL { get; set; }

        /// <summary>
        /// Whether this message requires a response
        /// </summary>
        public bool RequiresResponse { get; set; } = false;

        /// <summary>
        /// Correlation ID for request/response patterns
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Message headers for metadata
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// Digital signature for integrity verification
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// Creates a response message to this message
        /// </summary>
        public ScriptMessage CreateResponse(Dictionary<string, object> responseData = null)
        {
            return new ScriptMessage
            {
                Type = $"{Type}.response",
                FromScript = ToScript,
                ToScript = FromScript,
                Data = responseData ?? new Dictionary<string, object>(),
                CorrelationId = Id,
                Priority = Priority
            };
        }

        /// <summary>
        /// Checks if this message has expired based on TTL
        /// </summary>
        public bool IsExpired => TTL.HasValue && DateTime.UtcNow - Timestamp > TTL.Value;
    }

    /// <summary>
    /// Message priority levels
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Communication policy for a script
    /// </summary>
    public class ScriptCommunicationPolicy
    {
        /// <summary>
        /// Script identifier
        /// </summary>
        public string ScriptId { get; set; } = string.Empty;

        /// <summary>
        /// Message types this script can send
        /// </summary>
        public HashSet<string> CanSendTypes { get; set; } = new();

        /// <summary>
        /// Message types this script can receive
        /// </summary>
        public HashSet<string> CanReceiveTypes { get; set; } = new();

        /// <summary>
        /// Scripts this script can communicate with directly
        /// </summary>
        public HashSet<string> AllowedTargets { get; set; } = new();

        /// <summary>
        /// Scripts that can send direct messages to this script
        /// </summary>
        public HashSet<string> AllowedSenders { get; set; } = new();

        /// <summary>
        /// Maximum message size in bytes
        /// </summary>
        public int MaxMessageSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Maximum messages per minute
        /// </summary>
        public int MaxMessagesPerMinute { get; set; } = 100;

        /// <summary>
        /// Whether messages must be signed
        /// </summary>
        public bool RequireSignature { get; set; } = false;

        /// <summary>
        /// Whether to log all message traffic for this script
        /// </summary>
        public bool EnableAuditLogging { get; set; } = true;

        /// <summary>
        /// Creates a permissive policy for development
        /// </summary>
        public static ScriptCommunicationPolicy Permissive(string scriptId)
        {
            return new ScriptCommunicationPolicy
            {
                ScriptId = scriptId,
                CanSendTypes = new HashSet<string> { "*" },
                CanReceiveTypes = new HashSet<string> { "*" },
                MaxMessageSize = 10 * 1024 * 1024, // 10MB
                MaxMessagesPerMinute = 1000,
                RequireSignature = false
            };
        }

        /// <summary>
        /// Creates a restrictive policy for production
        /// </summary>
        public static ScriptCommunicationPolicy Restrictive(string scriptId)
        {
            return new ScriptCommunicationPolicy
            {
                ScriptId = scriptId,
                MaxMessageSize = 64 * 1024, // 64KB
                MaxMessagesPerMinute = 10,
                RequireSignature = true,
                EnableAuditLogging = true
            };
        }
    }

    /// <summary>
    /// Message bus statistics
    /// </summary>
    public class MessageBusStats
    {
        public int TotalMessagesProcessed { get; set; }
        public int TotalSubscriptions { get; set; }
        public int ActiveScripts { get; set; }
        public Dictionary<string, int> MessagesByType { get; set; } = new();
        public Dictionary<string, int> MessagesByScript { get; set; } = new();
        public int DroppedMessages { get; set; }
        public int ExpiredMessages { get; set; }
        public int PolicyViolations { get; set; }
        public DateTime FirstMessage { get; set; }
        public DateTime LastMessage { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
    }

    /// <summary>
    /// Message handler delegate
    /// </summary>
    public delegate Task<ScriptMessage> MessageHandler(ScriptMessage message);

    /// <summary>
    /// Message validation result
    /// </summary>
    public class MessageValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();

        public static MessageValidationResult Valid() => new() { IsValid = true };
        public static MessageValidationResult Invalid(string error) => new() { IsValid = false, ErrorMessage = error };
    }
}