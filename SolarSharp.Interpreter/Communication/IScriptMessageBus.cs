using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Interface defining a secure message bus for inter-script communication
    /// </summary>
    public interface IScriptMessageBus
    {
        /// <summary>
        /// Subscribes a script to messages of a specified type, associating it with a handler for processing incoming messages.
        /// </summary>
        /// <param name="messageType">The type of message to subscribe to.</param>
        /// <param name="scriptId">The identifier of the script subscribing to the message.</param>
        /// <param name="handler">The function to handle messages of the specified type.</param>
        void Subscribe(
            string messageType,
            string scriptId,
            Func<ScriptMessage, Task<ScriptMessage>> handler
        );

        /// <summary>
        /// Unsubscribes a script from messages of a specific type
        /// </summary>
        /// <param name="messageType">The type of message to unsubscribe from</param>
        /// <param name="callback">The callback function that was subscribed</param>
        void Unsubscribe(string messageType, string scriptId);

        /// <summary>
        /// Publishes a message asynchronously to all subscribers and returns a list of responses received.
        /// </summary>
        /// <param name="message">The message to be published to subscribers.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a read-only list of responses from the subscribers.</returns>
        Task<IReadOnlyList<ScriptMessage>> PublishAsync(ScriptMessage message);

        /// <summary>
        /// Sends a direct message to a specified target script asynchronously.
        /// </summary>
        /// <param name="message">The message to be sent, containing necessary data and metadata.</param>
        /// <param name="targetScriptId">The identifier of the target script to receive the message.</param>
        /// <returns>A task representing the asynchronous operation, which returns the response message from the target script.</returns>
        Task<ScriptMessage> SendDirectAsync(ScriptMessage message, string targetScriptId);

        /// <summary>
        /// Registers a script to the system, making it available for use
        /// </summary>
        void RegisterScript(string scriptId, ScriptCommunicationPolicy policy);

        /// <summary>
        /// Unregisters a script from the message bus, removing its subscriptions
        /// and associated resources.
        /// </summary>
        /// <param name="scriptId">The unique identifier of the script to be unregistered.</param>
        void UnregisterScript(string scriptId);

        /// <summary>
        /// Retrieves a read-only dictionary containing active subscriptions.
        /// The dictionary maps message types to lists of subscriber script IDs.
        /// </summary>
        /// <returns>
        /// A read-only dictionary where keys represent message types and values are read-only lists of subscriber script IDs.
        /// </returns>
        IReadOnlyDictionary<string, IReadOnlyList<string>> GetSubscriptions();

        /// <summary>
        /// Retrieves statistical data related to the specified criteria or context.
        /// </summary>
        /// <returns>
        /// A statistical dataset or summary based on the provided parameters or system state.
        /// </returns>
        MessageBusStats GetStats();
    }

    /// <summary>
    /// Represents a message sent or received within a script execution context.
    /// </summary>
    public class ScriptMessage
    {
        /// <summary>
        /// Represents the identifier of an entity.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Represents the category or classification of an object or entity.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the script that sent the message.
        /// </summary>
        public string FromScript { get; set; } = string.Empty;

        /// <summary>
        /// Converts the current object into its script-based representation
        /// </summary>
        public string ToScript { get; set; } = string.Empty;

        /// <summary>
        /// Represents the data associated with an entity or operation.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// The date and time when the message was created or last modified
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indicates the level of importance assigned to a task or item.
        /// </summary>
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;

        /// <summary>
        /// Time-to-live duration for the message. Indicates how long the message remains valid.
        /// If the message is older than this duration, it is considered expired.
        /// </summary>
        public TimeSpan? TTL { get; set; }

        /// <summary>
        /// Indicates whether the message requires a response from the recipient.
        /// </summary>
        public bool RequiresResponse { get; set; } = false;

        /// <summary>
        /// Identifier used to correlate related messages across distributed systems or components
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Custom headers associated with the message
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Digital signature used to verify the authenticity and integrity of data
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// Creates a response based on the provided input parameters.
        /// </summary>
        /// <param name="statusCode">The HTTP status code for the response.</param>
        /// <param name="message">The message to include in the response body.</param>
        /// <returns>A formatted response object containing the provided status code and message.</returns>
        public ScriptMessage CreateResponse(Dictionary<string, object> responseData = null)
        {
            return new ScriptMessage
            {
                Type = $"{Type}.response",
                FromScript = ToScript,
                ToScript = FromScript,
                Data = responseData ?? new Dictionary<string, object>(),
                CorrelationId = Id,
                Priority = Priority,
            };
        }

        /// <summary>
        /// Indicates whether the current object or element has expired.
        /// </summary>
        public bool IsExpired
        {
            get { return TTL.HasValue && DateTime.UtcNow - Timestamp > TTL.Value; }
        }
    }

    /// <summary>
    /// Represents the priority levels of a message.
    /// </summary>
    public enum MessagePriority
    {
        /// <summary>
        /// Represents the lowest priority level for a message, typically used for non-urgent or background tasks.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Represents the default priority level for messages in the system.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// Represents a high priority level for messages that require prompt attention or processing.
        /// </summary>
        High = 2,

        /// <summary>
        /// Represents the highest level of message priority, indicating that the message
        /// requires immediate attention and processing.
        /// </summary>
        Critical = 3,
    }

    /// <summary>
    /// Defines the communication policy for a script, including constraints on
    /// message types, size, targets, senders, and other security-related rules.
    /// </summary>
    public class ScriptCommunicationPolicy
    {
        /// <summary>
        /// Identifier for the script
        /// </summary>
        public string ScriptId { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the types can be sent based on current configurations or restrictions.
        /// </summary>
        public HashSet<string> CanSendTypes { get; set; } = new HashSet<string>();

        /// <summary>
        /// Defines the set of message types that the script is allowed to receive.
        /// </summary>
        public HashSet<string> CanReceiveTypes { get; set; } = new HashSet<string>();

        /// <summary>
        /// Specifies a set of target script identifiers that the script is allowed to communicate with.
        /// </summary>
        public HashSet<string> AllowedTargets { get; set; } = new HashSet<string>();

        /// <summary>
        /// Specifies a set of scripts that are allowed to send messages to the script associated with this communication policy.
        /// </summary>
        public HashSet<string> AllowedSenders { get; set; } = new HashSet<string>();

        /// <summary>
        /// Specifies the maximum allowable size for a message.
        /// </summary>
        public int MaxMessageSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Specifies the maximum number of messages that can be sent per minute under the communication policy.
        /// </summary>
        public int MaxMessagesPerMinute { get; set; } = 100;

        /// <summary>
        /// Indicates whether messages must include a valid signature for validation.
        /// </summary>
        public bool RequireSignature { get; set; }

        /// <summary>
        /// Indicates whether audit logging is enabled for the communication policy.
        /// </summary>
        public bool EnableAuditLogging { get; set; } = true;

        /// <summary>
        /// Creates a permissive communication policy for a specified script, mainly intended for development use.
        /// </summary>
        /// <param name="scriptId">The unique identifier of the script for which the policy is created.</param>
        /// <returns>A <see cref="ScriptCommunicationPolicy"/> object configured with permissive rules.</returns>
        public static ScriptCommunicationPolicy Permissive(string scriptId)
        {
            return new ScriptCommunicationPolicy
            {
                ScriptId = scriptId,
                CanSendTypes = new HashSet<string> { "*" },
                CanReceiveTypes = new HashSet<string> { "*" },
                AllowedTargets = new HashSet<string> { "*" },
                AllowedSenders = new HashSet<string> { "*" },
                MaxMessageSize = 10 * 1024 * 1024, // 10MB
                MaxMessagesPerMinute = 1000,
                RequireSignature = false,
            };
        }

        /// <summary>
        /// Creates a restrictive policy for production
        /// </summary>
        /// <param name="scriptId">The unique identifier for the script</param>
        /// <returns>A ScriptCommunicationPolicy object configured with restrictive settings</returns>
        public static ScriptCommunicationPolicy Restrictive(string scriptId)
        {
            return new ScriptCommunicationPolicy
            {
                ScriptId = scriptId,
                MaxMessageSize = 64 * 1024, // 64KB
                MaxMessagesPerMinute = 10,
                RequireSignature = true,
                EnableAuditLogging = true,
            };
        }
    }

    /// <summary>
    /// Provides statistics and metrics for monitoring the performance and behaviour
    /// of a message bus system.
    /// </summary>
    public class MessageBusStats
    {
        /// <summary>
        /// Represents the total number of messages processed.
        /// </summary>
        public int TotalMessagesProcessed { get; set; }

        /// <summary>
        /// Represents the total number of subscriptions.
        /// </summary>
        public int TotalSubscriptions { get; set; }

        /// <summary>
        /// Represents a collection of scripts that are currently active.
        /// </summary>
        public int ActiveScripts { get; set; }

        /// <summary>
        /// Tracks the count of processed messages categorized by their type.
        /// </summary>
        public Dictionary<string, int> MessagesByType { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Tracks the count of messages processed per originating script.
        /// </summary>
        public Dictionary<string, int> MessagesByScript { get; set; } =
            new Dictionary<string, int>();

        /// <summary>
        /// Represents the count of messages that were dropped or not processed.
        /// </summary>
        public int DroppedMessages { get; set; }

        /// <summary>
        /// Represents the total number of messages that have expired and were not processed by the system.
        /// </summary>
        public int ExpiredMessages { get; set; }

        /// <summary>
        /// Tracks the number of policy violations that occurred during message processing or publishing attempts.
        /// </summary>
        public int PolicyViolations { get; set; }

        /// <summary>
        /// Timestamp of the first message processed by the message bus
        /// </summary>
        public DateTime FirstMessage { get; set; }

        /// <summary>
        /// Represents the most recent message in a conversation or thread
        /// </summary>
        public DateTime LastMessage { get; set; }

        /// <summary>
        /// Represents the average time taken to process a single message in the message bus.
        /// </summary>
        public TimeSpan AverageProcessingTime { get; set; }
    }

    /// <summary>
    /// Delegate for processing messages in a message bus system
    /// </summary>
    /// <param name="message">The message to be processed</param>
    /// <returns>A task representing the asynchronous operation, containing an optional response message</returns>
    public delegate Task<ScriptMessage> MessageHandler(ScriptMessage message);

    /// <summary>
    /// Represents the result of a message validation process, including validity status, error details, and potential warnings.
    /// </summary>
    public class MessageValidationResult
    {
        /// <summary>
        /// Indicates whether the current state or data is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Describes the error message associated with a failed validation result.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// List of warnings associated with the validation result.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Represents a successful message validation result.
        /// </summary>
        /// <returns>A valid message validation result with no errors or warnings.</returns>
        public static MessageValidationResult Valid() =>
            new MessageValidationResult { IsValid = true };

        /// <summary>
        /// Creates an invalid message validation result with the specified error message.
        /// </summary>
        /// <param name="error">The error message indicating why the message is invalid.</param>
        /// <returns>A <see cref="MessageValidationResult"/> instance representing an invalid state with the given error message.</returns>
        public static MessageValidationResult Invalid(string error) =>
            new MessageValidationResult { IsValid = false, ErrorMessage = error };
    }
}
