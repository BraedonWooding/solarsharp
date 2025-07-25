using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Secure implementation of script message bus
    /// </summary>
    public class ScriptMessageBus : IScriptMessageBus, IDisposable
    {
        private readonly ConcurrentDictionary<string, ScriptCommunicationPolicy> _scripts =
            new ConcurrentDictionary<string, ScriptCommunicationPolicy>();
        private readonly ConcurrentDictionary<
            string,
            ConcurrentDictionary<string, Func<ScriptMessage, Task<ScriptMessage>>>
        > _subscriptions =
            new ConcurrentDictionary<
                string,
                ConcurrentDictionary<string, Func<ScriptMessage, Task<ScriptMessage>>>
            >();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters =
            new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ISecurityAuditor _auditor;
        private readonly MessageBusStats _stats = new MessageBusStats();
        private readonly Timer _cleanupTimer;
        private readonly object _statsLock = new object();

        public ScriptMessageBus(ISecurityAuditor auditor = null)
        {
            _auditor = auditor;

            // Start cleanup timer to remove expired messages and reset rate limiters
            _cleanupTimer = new Timer(
                CleanupCallback,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1)
            );
        }

        public void RegisterScript(string scriptId, ScriptCommunicationPolicy policy)
        {
            if (string.IsNullOrEmpty(scriptId))
                throw new ArgumentException("Script ID cannot be null or empty", nameof(scriptId));

            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            _scripts[scriptId] = policy;
            _rateLimiters[scriptId] = new SemaphoreSlim(
                policy.MaxMessagesPerMinute,
                policy.MaxMessagesPerMinute
            );

            lock (_statsLock)
            {
                _stats.ActiveScripts = _scripts.Count;
            }

            _auditor?.LogCapabilityUsage(
                "message_bus",
                "register_script",
                new object[] { scriptId },
                null,
                true
            );
        }

        public void UnregisterScript(string scriptId)
        {
            if (string.IsNullOrEmpty(scriptId))
                return;

            // Remove from all subscriptions
            foreach (var messageType in _subscriptions.Keys.ToList())
            {
                Unsubscribe(messageType, scriptId);
            }

            // Remove script registration
            _scripts.TryRemove(scriptId, out _);

            if (_rateLimiters.TryRemove(scriptId, out var rateLimiter))
            {
                rateLimiter.Dispose();
            }

            lock (_statsLock)
            {
                _stats.ActiveScripts = _scripts.Count;
            }

            _auditor?.LogCapabilityUsage(
                "message_bus",
                "unregister_script",
                new object[] { scriptId },
                null,
                true
            );
        }

        public void Subscribe(
            string messageType,
            string scriptId,
            Func<ScriptMessage, Task<ScriptMessage>> handler
        )
        {
            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentException(
                    "Message type cannot be null or empty",
                    nameof(messageType)
                );

            if (string.IsNullOrEmpty(scriptId))
                throw new ArgumentException("Script ID cannot be null or empty", nameof(scriptId));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            // Check if script is registered
            if (!_scripts.ContainsKey(scriptId))
                throw new InvalidOperationException(
                    $"Script '{scriptId}' is not registered with the message bus"
                );

            // Check subscription permissions
            var policy = _scripts[scriptId];
            if (!CanReceiveMessageType(policy, messageType))
            {
                throw new MissingCapabilityException(
                    $"Script '{scriptId}' is not allowed to receive messages of type '{messageType}'",
                    "Subscribe",
                    scriptId,
                    messageType
                );
            }

            var subscribers = _subscriptions.GetOrAdd(
                messageType,
                _ => new ConcurrentDictionary<string, Func<ScriptMessage, Task<ScriptMessage>>>()
            );
            subscribers[scriptId] = handler;

            lock (_statsLock)
            {
                _stats.TotalSubscriptions = _subscriptions.Values.Sum(s => s.Count);
            }

            _auditor?.LogCapabilityUsage(
                "message_bus",
                "subscribe",
                new object[] { messageType, scriptId },
                null,
                true
            );
        }

        public void Unsubscribe(string messageType, string scriptId)
        {
            if (string.IsNullOrEmpty(messageType) || string.IsNullOrEmpty(scriptId))
                return;

            if (_subscriptions.TryGetValue(messageType, out var subscribers))
            {
                subscribers.TryRemove(scriptId, out _);

                // Remove empty subscription lists
                if (subscribers.IsEmpty)
                {
                    _subscriptions.TryRemove(messageType, out _);
                }
            }

            lock (_statsLock)
            {
                _stats.TotalSubscriptions = _subscriptions.Values.Sum(s => s.Count);
            }

            _auditor?.LogCapabilityUsage(
                "message_bus",
                "unsubscribe",
                new object[] { messageType, scriptId },
                null,
                true
            );
        }

        public async Task<IReadOnlyList<ScriptMessage>> PublishAsync(ScriptMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var validationResult = ValidateMessage(message);
            if (!validationResult.IsValid)
            {
                lock (_statsLock)
                {
                    _stats.PolicyViolations++;
                }
                throw new MissingCapabilityException(
                    $"Message validation failed: {validationResult.ErrorMessage}",
                    "Validation",
                    validationResult.ErrorMessage
                );
            }

            // Check if sender can send this message type
            if (_scripts.TryGetValue(message.FromScript, out var senderPolicy))
            {
                if (!CanSendMessageType(senderPolicy, message.Type))
                {
                    lock (_statsLock)
                    {
                        _stats.PolicyViolations++;
                    }
                    throw new MissingCapabilityException(
                        $"Script '{message.FromScript}' is not allowed to send messages of type '{message.Type}'",
                        "PublishAsync",
                        message.FromScript,
                        message.Type
                    );
                }

                // Rate limiting
                if (!await CheckRateLimit(message.FromScript))
                {
                    lock (_statsLock)
                    {
                        _stats.DroppedMessages++;
                    }
                    throw new ResourceLimitExceededException(
                        $"Rate limit exceeded for script '{message.FromScript}'",
                        "MessageBus",
                        message.FromScript
                    );
                }
            }

            var responses = new List<ScriptMessage>();
            var startTime = DateTime.UtcNow;

            // Find all matching subscriptions (including wildcards)
            var matchingHandlers =
                new List<(string subscriberId, Func<ScriptMessage, Task<ScriptMessage>> handler)>();

            foreach (var (subscriptionType, subscribers) in _subscriptions)
            {
                if (IsMessageTypeMatch(subscriptionType, message.Type))
                {
                    foreach (var (subscriberId, handler) in subscribers.ToList())
                    {
                        // Check if subscriber can receive from sender
                        if (_scripts.TryGetValue(subscriberId, out var receiverPolicy))
                        {
                            if (!CanReceiveFromSender(receiverPolicy, message.FromScript))
                            {
                                continue; // Skip this subscriber
                            }
                        }

                        matchingHandlers.Add((subscriberId, handler));
                    }
                }
            }

            // Execute all matching handlers
            if (matchingHandlers.Any())
            {
                var tasks = matchingHandlers.Select(h =>
                    HandleMessageSafely(h.handler, message, h.subscriberId)
                );
                var results = await Task.WhenAll(tasks);
                responses.AddRange(results.Where(r => r != null));
            }

            RecordMessageStats(message, DateTime.UtcNow - startTime);

            _auditor?.LogCapabilityUsage(
                "message_bus",
                "publish",
                new object[] { message.Type, message.FromScript, matchingHandlers.Count },
                responses.Count,
                true
            );

            return responses;
        }

        public async Task<ScriptMessage> SendDirectAsync(
            ScriptMessage message,
            string targetScriptId
        )
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrEmpty(targetScriptId))
                throw new ArgumentException(
                    "Target script ID cannot be null or empty",
                    nameof(targetScriptId)
                );

            message.ToScript = targetScriptId;

            var validationResult = ValidateMessage(message);
            if (!validationResult.IsValid)
            {
                lock (_statsLock)
                {
                    _stats.PolicyViolations++;
                }
                throw new MissingCapabilityException(
                    $"Message validation failed: {validationResult.ErrorMessage}",
                    "Validation",
                    validationResult.ErrorMessage
                );
            }

            // Check communication permissions
            if (_scripts.TryGetValue(message.FromScript, out var senderPolicy))
            {
                if (!CanSendToTarget(senderPolicy, targetScriptId))
                {
                    lock (_statsLock)
                    {
                        _stats.PolicyViolations++;
                    }
                    throw new MissingCapabilityException(
                        $"Script '{message.FromScript}' is not allowed to send direct messages to '{targetScriptId}'",
                        "SendDirectAsync",
                        message.FromScript,
                        targetScriptId
                    );
                }

                if (!await CheckRateLimit(message.FromScript))
                {
                    lock (_statsLock)
                    {
                        _stats.DroppedMessages++;
                    }
                    throw new ResourceLimitExceededException(
                        $"Rate limit exceeded for script '{message.FromScript}'",
                        "MessageBus",
                        message.FromScript
                    );
                }
            }

            // Find a handler for the target script (including wildcard subscriptions)
            Func<ScriptMessage, Task<ScriptMessage>> handler = null;

            foreach (var (subscriptionType, subscribers) in _subscriptions)
            {
                if (
                    IsMessageTypeMatch(subscriptionType, message.Type)
                    && subscribers.TryGetValue(targetScriptId, out var foundHandler)
                )
                {
                    handler = foundHandler;
                    break;
                }
            }

            if (handler != null)
            {
                var startTime = DateTime.UtcNow;
                var response = await HandleMessageSafely(handler, message, targetScriptId);

                RecordMessageStats(message, DateTime.UtcNow - startTime);

                _auditor?.LogCapabilityUsage(
                    "message_bus",
                    "send_direct",
                    new object[] { message.Type, message.FromScript, targetScriptId },
                    response != null,
                    true
                );

                return response;
            }

            throw new InvalidOperationException(
                $"No handler found for message type '{message.Type}' on script '{targetScriptId}'"
            );
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> GetSubscriptions()
        {
            var result = new Dictionary<string, IReadOnlyList<string>>();

            foreach (var (messageType, subscribers) in _subscriptions)
            {
                result[messageType] = subscribers.Keys.ToList();
            }

            return result;
        }

        public MessageBusStats GetStats()
        {
            lock (_statsLock)
            {
                return new MessageBusStats
                {
                    TotalMessagesProcessed = _stats.TotalMessagesProcessed,
                    TotalSubscriptions = _stats.TotalSubscriptions,
                    ActiveScripts = _stats.ActiveScripts,
                    MessagesByType = new Dictionary<string, int>(_stats.MessagesByType),
                    MessagesByScript = new Dictionary<string, int>(_stats.MessagesByScript),
                    DroppedMessages = _stats.DroppedMessages,
                    ExpiredMessages = _stats.ExpiredMessages,
                    PolicyViolations = _stats.PolicyViolations,
                    FirstMessage = _stats.FirstMessage,
                    LastMessage = _stats.LastMessage,
                    AverageProcessingTime = _stats.AverageProcessingTime,
                };
            }
        }

        private MessageValidationResult ValidateMessage(ScriptMessage message)
        {
            if (string.IsNullOrEmpty(message.Type))
                return MessageValidationResult.Invalid("Message type cannot be empty");

            if (string.IsNullOrEmpty(message.FromScript))
                return MessageValidationResult.Invalid("FromScript cannot be empty");

            if (message.IsExpired)
                return MessageValidationResult.Invalid("Message has expired");

            // Check message size
            if (_scripts.TryGetValue(message.FromScript, out var policy))
            {
                var messageSize = EstimateMessageSize(message);
                if (messageSize > policy.MaxMessageSize)
                    return MessageValidationResult.Invalid(
                        $"Message size ({messageSize}) exceeds limit ({policy.MaxMessageSize})"
                    );

                // Check signature if required
                if (policy.RequireSignature && string.IsNullOrEmpty(message.Signature))
                    return MessageValidationResult.Invalid(
                        "Message signature required but not provided"
                    );
            }

            return MessageValidationResult.Valid();
        }

        private async Task<ScriptMessage> HandleMessageSafely(
            Func<ScriptMessage, Task<ScriptMessage>> handler,
            ScriptMessage message,
            string handlerId
        )
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30-second timeout
                var task = handler(message);

                if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token)) == task)
                {
                    return await task;
                }
                _auditor?.LogSecurityViolation(
                    $"Message handler timeout for script '{handlerId}'",
                    SecurityEventType.ExecutionTimeout
                );
                return null;
            }
            catch (Exception ex)
            {
                _auditor?.LogSecurityViolation(
                    $"Message handler error for script '{handlerId}': {ex.Message}",
                    SecurityEventType.UnauthorizedOperation,
                    ex
                );
                return null;
            }
        }

        private async Task<bool> CheckRateLimit(string scriptId)
        {
            if (_rateLimiters.TryGetValue(scriptId, out var rateLimiter))
            {
                return await rateLimiter.WaitAsync(0); // Non-blocking check
            }
            return true; // No rate limiting configured
        }

        private bool CanSendMessageType(ScriptCommunicationPolicy policy, string messageType)
        {
            return policy.CanSendTypes.Contains("*") || policy.CanSendTypes.Contains(messageType);
        }

        private bool CanReceiveMessageType(ScriptCommunicationPolicy policy, string messageType)
        {
            return policy.CanReceiveTypes.Contains("*")
                || policy.CanReceiveTypes.Contains(messageType);
        }

        private bool CanSendToTarget(ScriptCommunicationPolicy policy, string targetScript)
        {
            return policy.AllowedTargets.Contains("*")
                || policy.AllowedTargets.Contains(targetScript);
        }

        private bool CanReceiveFromSender(ScriptCommunicationPolicy policy, string senderScript)
        {
            return policy.AllowedSenders.Contains("*")
                || policy.AllowedSenders.Contains(senderScript);
        }

        private bool IsMessageTypeMatch(string subscriptionPattern, string messageType)
        {
            // Exact match
            if (subscriptionPattern == messageType)
                return true;

            // Wildcard match (e.g., "game.*" matches "game.event", "game.update", etc.)
            if (subscriptionPattern.EndsWith("*"))
            {
                var prefix = subscriptionPattern.Substring(0, subscriptionPattern.Length - 1);
                return messageType.StartsWith(prefix);
            }

            return false;
        }

        private int EstimateMessageSize(ScriptMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                return Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 1024; // Fallback estimate
            }
        }

        private void RecordMessageStats(ScriptMessage message, TimeSpan processingTime)
        {
            lock (_statsLock)
            {
                _stats.TotalMessagesProcessed++;
                _stats.MessagesByType[message.Type] =
                    _stats.MessagesByType.GetValueOrDefault(message.Type, 0) + 1;
                _stats.MessagesByScript[message.FromScript] =
                    _stats.MessagesByScript.GetValueOrDefault(message.FromScript, 0) + 1;

                if (_stats.FirstMessage == default)
                    _stats.FirstMessage = DateTime.UtcNow;

                _stats.LastMessage = DateTime.UtcNow;

                // Update average processing time
                var totalTime =
                    _stats.AverageProcessingTime.Ticks * (_stats.TotalMessagesProcessed - 1)
                    + processingTime.Ticks;
                _stats.AverageProcessingTime = new TimeSpan(
                    totalTime / _stats.TotalMessagesProcessed
                );
            }
        }

        private void CleanupCallback(object state)
        {
            try
            {
                // Reset rate limiters every minute
                foreach (var (scriptId, rateLimiter) in _rateLimiters)
                {
                    if (_scripts.TryGetValue(scriptId, out var policy))
                    {
                        // Release all tokens back to the rate limiter
                        var currentCount = rateLimiter.CurrentCount;
                        var toRelease = Math.Max(0, policy.MaxMessagesPerMinute - currentCount);

                        if (toRelease > 0)
                        {
                            rateLimiter.Release(toRelease);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _auditor?.LogSecurityViolation(
                    $"Message bus cleanup error: {ex.Message}",
                    SecurityEventType.UnauthorizedOperation,
                    ex
                );
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();

            foreach (var rateLimiter in _rateLimiters.Values)
            {
                rateLimiter?.Dispose();
            }

            _rateLimiters.Clear();
        }
    }
}
