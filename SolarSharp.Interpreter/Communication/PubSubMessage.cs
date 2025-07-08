using System;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Immutable message for publish/subscribe communication
    /// </summary>
    public readonly struct PubSubMessage : IEquatable<PubSubMessage>
    {
        /// <summary>
        /// Unique message identifier
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Message topic/channel
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Identity of the message sender
        /// </summary>
        public ScriptIdentity Source { get; }

        /// <summary>
        /// JSON message body
        /// </summary>
        public string Body { get; }

        /// <summary>
        /// Message timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Correlation ID for request/response patterns
        /// </summary>
        public Maybe<string> CorrelationId { get; }

        /// <summary>
        /// Whether this message expects a reply
        /// </summary>
        public bool ExpectsReply { get; }

        private PubSubMessage(
            string id,
            string topic,
            ScriptIdentity source,
            string body,
            DateTimeOffset timestamp,
            Maybe<string> correlationId,
            bool expectsReply
        )
        {
            Id = id;
            Topic = topic;
            Source = source;
            Body = body;
            Timestamp = timestamp;
            CorrelationId = correlationId;
            ExpectsReply = expectsReply;
        }

        /// <summary>
        /// Creates a new message
        /// </summary>
        public static PubSubMessage Create(string topic, ScriptIdentity source, string body)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or whitespace", nameof(topic));

            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body cannot be null or whitespace", nameof(body));

            return new PubSubMessage(
                Guid.NewGuid().ToString(),
                topic,
                source,
                body,
                DateTimeOffset.UtcNow,
                Maybe<string>.None,
                false
            );
        }

        /// <summary>
        /// Creates a new message with a reply expectation
        /// </summary>
        public PubSubMessage WithReply(string correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
                throw new ArgumentException(
                    "Correlation ID cannot be null or whitespace",
                    nameof(correlationId)
                );

            return new PubSubMessage(
                Id,
                Topic,
                Source,
                Body,
                Timestamp,
                Maybe<string>.From(correlationId),
                true
            );
        }

        /// <summary>
        /// Creates a reply to this message
        /// </summary>
        public PubSubMessage CreateReply(ScriptIdentity replySource, string replyBody)
        {
            if (!ExpectsReply)
                throw new InvalidOperationException(
                    "Cannot create reply for message that doesn't expect one"
                );

            if (string.IsNullOrWhiteSpace(replyBody))
                throw new ArgumentException(
                    "Reply body cannot be null or whitespace",
                    nameof(replyBody)
                );

            return new PubSubMessage(
                Guid.NewGuid().ToString(),
                $"{Topic}.reply",
                replySource,
                replyBody,
                DateTimeOffset.UtcNow,
                CorrelationId,
                false
            );
        }

        public bool Equals(PubSubMessage other)
        {
            return Id == other.Id
                && Topic == other.Topic
                && Source.Equals(other.Source)
                && Body == other.Body
                && Timestamp.Equals(other.Timestamp)
                && CorrelationId.Equals(other.CorrelationId)
                && ExpectsReply == other.ExpectsReply;
        }

        public override bool Equals(object obj)
        {
            return obj is PubSubMessage other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Topic?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ Source.GetHashCode();
                hashCode = (hashCode * 397) ^ (Body?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ Timestamp.GetHashCode();
                hashCode = (hashCode * 397) ^ CorrelationId.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpectsReply.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            var correlation = CorrelationId.Match(id => $", CorrelationId={id}", () => "");

            return $"Message(Id={Id}, Topic={Topic}, Source={Source}, Timestamp={Timestamp:O}{correlation})";
        }

        public static bool operator ==(PubSubMessage left, PubSubMessage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PubSubMessage left, PubSubMessage right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Errors that can occur during publishing
    /// </summary>
    public abstract class PublishError
    {
        public abstract string Message { get; }

        public class MessageTooLarge : PublishError
        {
            public int ActualSize { get; }
            public int MaxSize { get; }

            public MessageTooLarge(int actualSize, int maxSize)
            {
                ActualSize = actualSize;
                MaxSize = maxSize;
            }

            public override string Message
            {
                get
                {
                    return $"Message size {ActualSize} bytes exceeds maximum of {MaxSize} bytes";
                }
            }
        }

        public class RateLimitExceeded : PublishError
        {
            public override string Message
            {
                get { return "Rate limit exceeded"; }
            }
        }

        public class TopicNotAllowed : PublishError
        {
            public string Topic { get; }

            public TopicNotAllowed(string topic)
            {
                Topic = topic;
            }

            public override string Message
            {
                get { return $"Not allowed to publish to topic '{Topic}'"; }
            }
        }

        public class InvalidMessage : PublishError
        {
            public string Details { get; }

            public InvalidMessage(string details)
            {
                Details = details;
            }

            public override string Message
            {
                get { return $"Invalid message: {Details}"; }
            }
        }
    }

    /// <summary>
    /// Errors that can occur during request operations
    /// </summary>
    public abstract class RequestError
    {
        public abstract string Message { get; }

        public class Timeout : RequestError
        {
            public TimeSpan Duration { get; }

            public Timeout(TimeSpan duration)
            {
                Duration = duration;
            }

            public override string Message
            {
                get { return $"Request timed out after {Duration.TotalSeconds:F1} seconds"; }
            }
        }

        public class NoHandlers : RequestError
        {
            public string Topic { get; }

            public NoHandlers(string topic)
            {
                Topic = topic;
            }

            public override string Message
            {
                get { return $"No handlers registered for topic '{Topic}'"; }
            }
        }

        public class PublishFailed : RequestError
        {
            public PublishError InnerError { get; }

            public PublishFailed(PublishError innerError)
            {
                InnerError = innerError;
            }

            public override string Message
            {
                get { return $"Failed to publish request: {InnerError.Message}"; }
            }
        }
    }
}
