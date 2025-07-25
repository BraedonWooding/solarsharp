using System;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Immutable configuration for the message bus with fixed limits
    /// </summary>
    public readonly struct MessageBusConfig : IEquatable<MessageBusConfig>
    {
        /// <summary>
        /// Maximum messages per minute per script
        /// </summary>
        public int RateLimitPerMinute { get; }

        /// <summary>
        /// Maximum message body size in bytes
        /// </summary>
        public int MaxMessageSizeBytes { get; }

        /// <summary>
        /// Default timeout for request operations
        /// </summary>
        public TimeSpan DefaultRequestTimeout { get; }

        /// <summary>
        /// Creates a new message bus configuration
        /// </summary>
        /// <param name="rateLimitPerMinute">Rate limit (default: 100)</param>
        /// <param name="maxMessageSizeBytes">Max message size (default: 65536)</param>
        /// <param name="defaultRequestTimeout">Request timeout (default: 5 seconds)</param>
        public MessageBusConfig(
            int rateLimitPerMinute = 100,
            int maxMessageSizeBytes = 65536,
            TimeSpan? defaultRequestTimeout = null
        )
        {
            if (rateLimitPerMinute <= 0)
                throw new ArgumentException(
                    "Rate limit must be positive",
                    nameof(rateLimitPerMinute)
                );

            if (maxMessageSizeBytes <= 0)
                throw new ArgumentException(
                    "Max message size must be positive",
                    nameof(maxMessageSizeBytes)
                );

            var timeout = defaultRequestTimeout ?? TimeSpan.FromSeconds(5);
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentException(
                    "Timeout must be positive",
                    nameof(defaultRequestTimeout)
                );

            RateLimitPerMinute = rateLimitPerMinute;
            MaxMessageSizeBytes = maxMessageSizeBytes;
            DefaultRequestTimeout = timeout;
        }

        /// <summary>
        /// Default configuration suitable for most applications
        /// </summary>
        public static MessageBusConfig Default { get; } = new MessageBusConfig();

        /// <summary>
        /// Configuration for high-throughput scenarios
        /// </summary>
        public static MessageBusConfig HighThroughput { get; } =
            new MessageBusConfig(
                rateLimitPerMinute: 1000,
                maxMessageSizeBytes: 1048576, // 1MB
                defaultRequestTimeout: TimeSpan.FromSeconds(30)
            );

        /// <summary>
        /// Configuration for restricted environments
        /// </summary>
        public static MessageBusConfig Restricted { get; } =
            new MessageBusConfig(
                rateLimitPerMinute: 10,
                maxMessageSizeBytes: 8192, // 8KB
                defaultRequestTimeout: TimeSpan.FromSeconds(2)
            );

        public bool Equals(MessageBusConfig other)
        {
            return RateLimitPerMinute == other.RateLimitPerMinute
                && MaxMessageSizeBytes == other.MaxMessageSizeBytes
                && DefaultRequestTimeout.Equals(other.DefaultRequestTimeout);
        }

        public override bool Equals(object obj)
        {
            return obj is MessageBusConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RateLimitPerMinute;
                hashCode = (hashCode * 397) ^ MaxMessageSizeBytes;
                hashCode = (hashCode * 397) ^ DefaultRequestTimeout.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"MessageBusConfig(RateLimit={RateLimitPerMinute}/min, MaxSize={MaxMessageSizeBytes}B, Timeout={DefaultRequestTimeout.TotalSeconds}s)";
        }

        public static bool operator ==(MessageBusConfig left, MessageBusConfig right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MessageBusConfig left, MessageBusConfig right)
        {
            return !left.Equals(right);
        }
    }
}
