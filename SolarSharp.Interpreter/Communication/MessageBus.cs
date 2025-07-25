using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Thread-safe, functional message bus implementation
    /// </summary>
    public sealed class MessageBus : IMessageBus
    {
        private readonly ImmutableDictionary<ScriptIdentity, PubSubPolicySnapshot> _policies;
        private readonly ImmutableDictionary<string, ImmutableList<Subscription>> _subscriptions;
        private readonly MessageFirewall _firewall;
        private readonly MessageBusConfig _config;
        private readonly ImmutableDictionary<ScriptIdentity, RateLimiter> _rateLimiters;

        /// <summary>
        /// Configuration with fixed limits
        /// </summary>
        public MessageBusConfig Config
        {
            get { return _config; }
        }

        private MessageBus(
            MessageBusConfig config,
            ImmutableDictionary<ScriptIdentity, PubSubPolicySnapshot> policies,
            ImmutableDictionary<string, ImmutableList<Subscription>> subscriptions,
            MessageFirewall firewall,
            ImmutableDictionary<ScriptIdentity, RateLimiter> rateLimiters
        )
        {
            _config = config;
            _policies = policies;
            _subscriptions = subscriptions;
            _firewall = firewall;
            _rateLimiters = rateLimiters;
        }

        /// <summary>
        /// Creates a new message bus with the given configuration
        /// </summary>
        public static MessageBus Create(MessageBusConfig? config = null)
        {
            return new MessageBus(
                config ?? MessageBusConfig.Default,
                ImmutableDictionary<ScriptIdentity, PubSubPolicySnapshot>.Empty,
                ImmutableDictionary<string, ImmutableList<Subscription>>.Empty,
                MessageFirewall.Create(
                    ImmutableDictionary<ScriptIdentity, PubSubPolicySnapshot>.Empty
                ),
                ImmutableDictionary<ScriptIdentity, RateLimiter>.Empty
            );
        }

        /// <summary>
        /// Publishes a message to all eligible subscribers
        /// </summary>
        public async Task<UnitResult<PublishError>> PublishAsync(PubSubMessage message)
        {
            // Check if publisher can publish to this topic
            if (!_firewall.CanPublish(message.Source, message.Topic))
            {
                return UnitResult.Failure<PublishError>(
                    new PublishError.TopicNotAllowed(message.Topic)
                );
            }

            // Check rate limit
            if (_rateLimiters.TryGetValue(message.Source, out var rateLimiter))
            {
                if (!rateLimiter.TryAcquire())
                {
                    return UnitResult.Failure<PublishError>(new PublishError.RateLimitExceeded());
                }
            }

            // Check message size
            var messageSize = Encoding.UTF8.GetByteCount(message.Body);
            var maxSize = _config.MaxMessageSizeBytes;

            // Check if topic has specific size limit
            if (_policies.TryGetValue(message.Source, out var policy))
            {
                var topicPolicy = policy.GetTopicPolicy(message.Topic);
                if (topicPolicy?.Constraints != null)
                {
                    // Topic-specific limits can only be lower than global
                    var topicMaxSize = GetTopicMaxSize(topicPolicy);
                    if (topicMaxSize > 0 && topicMaxSize < maxSize)
                    {
                        maxSize = topicMaxSize;
                    }
                }
            }

            if (messageSize > maxSize)
            {
                return UnitResult.Failure<PublishError>(
                    new PublishError.MessageTooLarge(messageSize, maxSize)
                );
            }

            // Get eligible recipients
            var recipients = _firewall.GetEligibleRecipients(message, _subscriptions);

            // Deliver to all recipients concurrently
            var deliveryTasks = recipients.Select(async recipient =>
            {
                try
                {
                    await recipient.Handler(message);
                }
                catch
                {
                    // Swallow handler exceptions to not affect other deliveries
                }
            });

            await Task.WhenAll(deliveryTasks);

            return UnitResult.Success<PublishError>();
        }

        /// <summary>
        /// Sends a request and waits for a reply
        /// </summary>
        public async Task<Result<string, RequestError>> RequestAsync(
            PubSubMessage message,
            TimeSpan? timeout = null
        )
        {
            var effectiveTimeout = timeout ?? _config.DefaultRequestTimeout;
            if (effectiveTimeout > _config.DefaultRequestTimeout)
            {
                effectiveTimeout = _config.DefaultRequestTimeout;
            }

            // Create correlation ID for tracking replies
            var correlationId = Guid.NewGuid().ToString();
            var requestMessage = message.WithReply(correlationId);

            // Set up reply handler
            var replyTcs = new TaskCompletionSource<string>();
            var replyPattern = $"{message.Topic}.reply";

            // Temporary subscription for reply
            var tempBus = Subscribe(
                replyPattern,
                message.Source,
                replyMsg =>
                {
                    if (replyMsg.CorrelationId.Match(id => id == correlationId, () => false))
                    {
                        replyTcs.TrySetResult(replyMsg.Body);
                    }
                    return Task.FromResult(Maybe<string>.None);
                }
            );

            // Publish the request
            var publishResult = await PublishAsync(requestMessage);
            if (publishResult.IsFailure)
            {
                return Result.Failure<string, RequestError>(
                    new RequestError.PublishFailed(publishResult.Error)
                );
            }

            // Wait for reply with timeout
            using var cts = new CancellationTokenSource(effectiveTimeout);
            try
            {
                var replyTask = replyTcs.Task;
                var completedTask = await Task.WhenAny(
                    replyTask,
                    Task.Delay(effectiveTimeout, cts.Token)
                );

                if (completedTask == replyTask)
                {
                    return Result.Success<string, RequestError>(await replyTask);
                }
                else
                {
                    return Result.Failure<string, RequestError>(
                        new RequestError.Timeout(effectiveTimeout)
                    );
                }
            }
            finally
            {
                cts.Cancel();
            }
        }

        /// <summary>
        /// Subscribes to messages matching the pattern, returns new bus instance
        /// </summary>
        public IMessageBus Subscribe(
            string pattern,
            ScriptIdentity subscriber,
            Func<PubSubMessage, Task<Maybe<string>>> handler
        )
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException(
                    "Pattern cannot be null or whitespace",
                    nameof(pattern)
                );

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            // Check if subscriber has permission to subscribe to this pattern
            if (!_policies.TryGetValue(subscriber, out var policy) || !policy.CanSubscribe(pattern))
            {
                throw new InvalidOperationException(
                    $"Script '{subscriber}' is not allowed to subscribe to '{pattern}'"
                );
            }

            var subscription = new Subscription(subscriber, handler);

            var newSubscriptions = _subscriptions.TryGetValue(pattern, out var existing)
                ? _subscriptions.SetItem(pattern, existing.Add(subscription))
                : _subscriptions.Add(pattern, ImmutableList.Create(subscription));

            return new MessageBus(_config, _policies, newSubscriptions, _firewall, _rateLimiters);
        }

        /// <summary>
        /// Unsubscribes from a pattern, returns new bus instance
        /// </summary>
        public IMessageBus Unsubscribe(string pattern, ScriptIdentity subscriber)
        {
            if (!_subscriptions.TryGetValue(pattern, out var subscriptions))
                return this;

            var filteredSubscriptions = subscriptions.RemoveAll(s =>
                s.Subscriber.Equals(subscriber)
            );

            var newSubscriptions = filteredSubscriptions.IsEmpty
                ? _subscriptions.Remove(pattern)
                : _subscriptions.SetItem(pattern, filteredSubscriptions);

            return new MessageBus(_config, _policies, newSubscriptions, _firewall, _rateLimiters);
        }

        /// <summary>
        /// Registers a script with its policy, returns new bus instance
        /// </summary>
        public IMessageBus RegisterScript(ScriptIdentity identity, PubSubPolicySnapshot policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            var newPolicies = _policies.SetItem(identity, policy);
            var newFirewall = MessageFirewall.Create(newPolicies);

            // Create rate limiter for this script
            var rateLimiter = new RateLimiter(_config.RateLimitPerMinute);
            var newRateLimiters = _rateLimiters.SetItem(identity, rateLimiter);

            return new MessageBus(
                _config,
                newPolicies,
                _subscriptions,
                newFirewall,
                newRateLimiters
            );
        }

        /// <summary>
        /// Unregisters a script, returns new bus instance
        /// </summary>
        public IMessageBus UnregisterScript(ScriptIdentity identity)
        {
            var newPolicies = _policies.Remove(identity);
            var newFirewall = MessageFirewall.Create(newPolicies);
            var newRateLimiters = _rateLimiters.Remove(identity);

            // Remove all subscriptions for this script
            var newSubscriptions = _subscriptions;
            foreach (var kvp in _subscriptions)
            {
                var filtered = kvp.Value.RemoveAll(s => s.Subscriber.Equals(identity));
                newSubscriptions = filtered.IsEmpty
                    ? newSubscriptions.Remove(kvp.Key)
                    : newSubscriptions.SetItem(kvp.Key, filtered);
            }

            return new MessageBus(
                _config,
                newPolicies,
                newSubscriptions,
                newFirewall,
                newRateLimiters
            );
        }

        private static int GetTopicMaxSize(TopicPolicy topicPolicy)
        {
            // This would be extended to read size limits from topic policy
            // For now, return 0 to indicate no topic-specific limit
            return 0;
        }

        /// <summary>
        /// Simple rate limiter using token bucket algorithm
        /// </summary>
        private sealed class RateLimiter
        {
            private readonly int _maxTokens;
            private readonly object _lock = new object();
            private int _tokens;
            private DateTime _lastRefill;

            public RateLimiter(int maxTokensPerMinute)
            {
                _maxTokens = maxTokensPerMinute;
                _tokens = maxTokensPerMinute;
                _lastRefill = DateTime.UtcNow;
            }

            public bool TryAcquire()
            {
                lock (_lock)
                {
                    RefillTokens();

                    if (_tokens > 0)
                    {
                        _tokens--;
                        return true;
                    }

                    return false;
                }
            }

            private void RefillTokens()
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastRefill;

                if (elapsed.TotalMinutes >= 1)
                {
                    _tokens = _maxTokens;
                    _lastRefill = now;
                }
                else
                {
                    // Partial refill based on elapsed time
                    var tokensToAdd = (int)(elapsed.TotalMinutes * _maxTokens);
                    _tokens = Math.Min(_tokens + tokensToAdd, _maxTokens);

                    if (tokensToAdd > 0)
                    {
                        _lastRefill = now;
                    }
                }
            }
        }
    }
}
