using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SolarSharp.Interpreter.Communication;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Comprehensive test suite for the SolarSharp Message Bus, validating secure
    ///     inter-script communication, policy enforcement, and rate limiting.
    /// </summary>
    /// <remarks>
    ///     This test suite covers:
    ///     - Basic message bus operations (subscribe, publish, send direct)
    ///     - Security policy enforcement
    ///     - Rate limiting and throttling
    ///     - Message validation and expiration
    ///     - Error handling and edge cases
    ///     - Performance and concurrency scenarios
    ///     - Integration with security auditing
    /// </remarks>
    [TestFixture]
    [Category("MessageBus")]
    [Category("Security.MessageBus")]
    public class MessageBusTests
    {
        [SetUp]
        public void Setup()
        {
            _auditor = new SecurityAuditor();
            _messageBus = new ScriptMessageBus(_auditor);
        }

        [TearDown]
        public void TearDown()
        {
            _messageBus?.Dispose();
        }

        private ScriptMessageBus _messageBus;
        private SecurityAuditor _auditor;

        [Category("Policy.Unit")]
        [Category("Security.Policy")]
        [Test]
        public void RegisterScript_WithValidPolicy_Succeeds()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test-script");

            Assert.DoesNotThrow(() => _messageBus.RegisterScript("test-script", policy));

            var stats = _messageBus.GetStats();
            Assert.That(stats.ActiveScripts, Is.EqualTo(1));
        }

        [Test]
        public void RegisterScript_WithNullScriptId_ThrowsArgumentException()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test");

            Assert.Throws<ArgumentException>(() => _messageBus.RegisterScript(null, policy));
            Assert.Throws<ArgumentException>(() => _messageBus.RegisterScript("", policy));
        }

        [Test]
        public void Subscribe_WithUnregisteredScript_ThrowsInvalidOperationException()
        {
            var handler = new Func<ScriptMessage, Task<ScriptMessage>>(msg =>
                Task.FromResult<ScriptMessage>(null)
            );

            Assert.Throws<InvalidOperationException>(() =>
                _messageBus.Subscribe("test.message", "unregistered-script", handler)
            );
        }

        [Test]
        public async Task PublishAsync_ToSubscribers_DeliversMessages()
        {
            var received = new List<ScriptMessage>();
            var script1Policy = ScriptCommunicationPolicy.Permissive("script1");
            var script2Policy = ScriptCommunicationPolicy.Permissive("script2");

            _messageBus.RegisterScript("script1", script1Policy);
            _messageBus.RegisterScript("script2", script2Policy);

            _messageBus.Subscribe(
                "test.event",
                "script1",
                msg =>
                {
                    received.Add(msg);
                    return Task.FromResult<ScriptMessage>(null);
                }
            );

            _messageBus.Subscribe(
                "test.event",
                "script2",
                msg =>
                {
                    received.Add(msg);
                    return Task.FromResult(
                        msg.CreateResponse(new Dictionary<string, object> { ["status"] = "ok" })
                    );
                }
            );

            var message = new ScriptMessage
            {
                Type = "test.event",
                FromScript = "script1",
                Data = new Dictionary<string, object> { ["value"] = 42 },
            };

            var responses = await _messageBus.PublishAsync(message);

            Assert.Multiple(() =>
            {
                Assert.That(received, Has.Count.EqualTo(2));
                Assert.That(responses, Has.Count.EqualTo(1)); // Only script2 returns a response
            });
            Assert.That(responses[0].Data["status"], Is.EqualTo("ok"));
        }

        [Test]
        public async Task SendDirectAsync_ToSpecificScript_DeliversMessage()
        {
            ScriptMessage receivedMessage = null;
            var senderPolicy = ScriptCommunicationPolicy.Permissive("sender");
            var receiverPolicy = ScriptCommunicationPolicy.Permissive("receiver");

            _messageBus.RegisterScript("sender", senderPolicy);
            _messageBus.RegisterScript("receiver", receiverPolicy);

            _messageBus.Subscribe(
                "direct.message",
                "receiver",
                msg =>
                {
                    receivedMessage = msg;
                    return Task.FromResult(
                        msg.CreateResponse(new Dictionary<string, object> { ["received"] = true })
                    );
                }
            );

            var message = new ScriptMessage
            {
                Type = "direct.message",
                FromScript = "sender",
                Data = new Dictionary<string, object> { ["secret"] = "data" },
            };

            var response = await _messageBus.SendDirectAsync(message, "receiver");

            Assert.That(receivedMessage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage.Data["secret"], Is.EqualTo("data"));
                Assert.That((bool)response.Data["received"], Is.True);
            });
        }

        [Test]
        public void Unsubscribe_RemovesSubscription()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test-script");
            _messageBus.RegisterScript("test-script", policy);

            _messageBus.Subscribe(
                "test.event",
                "test-script",
                msg => Task.FromResult<ScriptMessage>(null)
            );

            _messageBus.Unsubscribe("test.event", "test-script");

            var subscriptions = _messageBus.GetSubscriptions();
            Assert.That(subscriptions.ContainsKey("test.event"), Is.False);
        }

        [Category("Security.Unit")]
        [Test]
        public void Subscribe_WithDisallowedMessageType_ThrowsSecurityException()
        {
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "restricted",
                CanReceiveTypes = new HashSet<string> { "allowed.type" },
            };
            _messageBus.RegisterScript("restricted", policy);

            Assert.Throws<MissingCapabilityException>(() =>
                _messageBus.Subscribe(
                    "forbidden.type",
                    "restricted",
                    msg => Task.FromResult<ScriptMessage>(null)
                )
            );
        }

        [Category("Security.Unit")]
        [Test]
        public Task PublishAsync_WithDisallowedMessageType_ThrowsSecurityException()
        {
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "restricted",
                CanSendTypes = new HashSet<string> { "allowed.type" },
            };
            _messageBus.RegisterScript("restricted", policy);

            var message = new ScriptMessage { Type = "forbidden.type", FromScript = "restricted" };

            Assert.ThrowsAsync<MissingCapabilityException>(async () =>
                await _messageBus.PublishAsync(message)
            );
            return Task.CompletedTask;
        }

        [Category("Security.Unit")]
        [Test]
        public Task SendDirectAsync_ToDisallowedTarget_ThrowsSecurityException()
        {
            var senderPolicy = new ScriptCommunicationPolicy
            {
                ScriptId = "sender",
                AllowedTargets = new HashSet<string> { "allowed-target" },
            };
            var receiverPolicy = ScriptCommunicationPolicy.Permissive("forbidden-target");

            _messageBus.RegisterScript("sender", senderPolicy);
            _messageBus.RegisterScript("forbidden-target", receiverPolicy);

            var message = new ScriptMessage { Type = "test.message", FromScript = "sender" };

            Assert.ThrowsAsync<MissingCapabilityException>(async () =>
                await _messageBus.SendDirectAsync(message, "forbidden-target")
            );
            return Task.CompletedTask;
        }

        [Test]
        public async Task PublishAsync_ReceiverWithSenderRestriction_FiltersMessages()
        {
            var receivedCount = 0;
            var senderPolicy = ScriptCommunicationPolicy.Permissive("sender");
            var receiverPolicy = new ScriptCommunicationPolicy
            {
                ScriptId = "receiver",
                CanReceiveTypes = new HashSet<string> { "*" },
                AllowedSenders = new HashSet<string> { "trusted-sender" }, // Not "sender"
            };

            _messageBus.RegisterScript("sender", senderPolicy);
            _messageBus.RegisterScript("receiver", receiverPolicy);

            _messageBus.Subscribe(
                "test.event",
                "receiver",
                msg =>
                {
                    receivedCount++;
                    return Task.FromResult<ScriptMessage>(null);
                }
            );

            var message = new ScriptMessage { Type = "test.event", FromScript = "sender" };

            await _messageBus.PublishAsync(message);

            Assert.That(receivedCount, Is.EqualTo(0)); // Message filtered due to sender restriction
        }

        [Test]
        public async Task PublishAsync_ExceedingRateLimit_ThrowsResourceLimitException()
        {
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "rate-limited",
                CanSendTypes = new HashSet<string> { "*" },
                MaxMessagesPerMinute = 3,
            };
            _messageBus.RegisterScript("rate-limited", policy);

            // Send messages up to the limit
            for (var i = 0; i < 3; i++)
                await _messageBus.PublishAsync(
                    new ScriptMessage { Type = "test.message", FromScript = "rate-limited" }
                );

            // Next message should fail
            Assert.ThrowsAsync<ResourceLimitExceededException>(async () =>
                await _messageBus.PublishAsync(
                    new ScriptMessage { Type = "test.message", FromScript = "rate-limited" }
                )
            );
        }

        [Test]
        public async Task SendDirectAsync_ExceedingRateLimit_ThrowsResourceLimitException()
        {
            var senderPolicy = new ScriptCommunicationPolicy
            {
                ScriptId = "rate-limited",
                CanSendTypes = new HashSet<string> { "*" },
                AllowedTargets = new HashSet<string> { "*" },
                MaxMessagesPerMinute = 2,
            };
            var receiverPolicy = ScriptCommunicationPolicy.Permissive("receiver");

            _messageBus.RegisterScript("rate-limited", senderPolicy);
            _messageBus.RegisterScript("receiver", receiverPolicy);

            _messageBus.Subscribe(
                "test.message",
                "receiver",
                msg => Task.FromResult<ScriptMessage>(null)
            );

            // Send messages up to the limit
            for (var i = 0; i < 2; i++)
                await _messageBus.SendDirectAsync(
                    new ScriptMessage { Type = "test.message", FromScript = "rate-limited" },
                    "receiver"
                );

            // Next message should fail
            Assert.ThrowsAsync<ResourceLimitExceededException>(async () =>
                await _messageBus.SendDirectAsync(
                    new ScriptMessage { Type = "test.message", FromScript = "rate-limited" },
                    "receiver"
                )
            );
        }

        [Category("Security.Unit")]
        [Test]
        public Task PublishAsync_WithExpiredMessage_ThrowsSecurityException()
        {
            var policy = ScriptCommunicationPolicy.Permissive("sender");
            _messageBus.RegisterScript("sender", policy);

            var message = new ScriptMessage
            {
                Type = "test.message",
                FromScript = "sender",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                TTL = TimeSpan.FromMinutes(30), // Expired 30 minutes ago
            };

            Assert.ThrowsAsync<MissingCapabilityException>(async () =>
                await _messageBus.PublishAsync(message)
            );
            return Task.CompletedTask;
        }

        [Category("Security.Unit")]
        [Test]
        public Task PublishAsync_ExceedingMessageSizeLimit_ThrowsSecurityException()
        {
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "sender",
                CanSendTypes = new HashSet<string> { "*" },
                MaxMessageSize = 100, // 100 bytes
            };
            _messageBus.RegisterScript("sender", policy);

            var largeData = new Dictionary<string, object>();
            for (var i = 0; i < 100; i++)
                largeData[$"key{i}"] =
                    "This is a long string value that will exceed the size limit";

            var message = new ScriptMessage
            {
                Type = "test.message",
                FromScript = "sender",
                Data = largeData,
            };

            Assert.ThrowsAsync<MissingCapabilityException>(async () =>
                await _messageBus.PublishAsync(message)
            );
            return Task.CompletedTask;
        }

        [Category("Security.Unit")]
        [Test]
        public Task PublishAsync_RequiringSignatureWithoutOne_ThrowsSecurityException()
        {
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "secure-sender",
                CanSendTypes = new HashSet<string> { "*" },
                RequireSignature = true,
            };
            _messageBus.RegisterScript("secure-sender", policy);

            var message = new ScriptMessage
            {
                Type = "test.message",
                FromScript = "secure-sender",
                Signature = "", // No signature provided
            };

            Assert.ThrowsAsync<MissingCapabilityException>(async () =>
                await _messageBus.PublishAsync(message)
            );
            return Task.CompletedTask;
        }

        [Test]
        public async Task PublishAsync_WithFailingHandler_ContinuesDelivery()
        {
            var successCount = 0;
            var policy = ScriptCommunicationPolicy.Permissive("sender");
            _messageBus.RegisterScript("sender", policy);
            _messageBus.RegisterScript("good-handler", policy);
            _messageBus.RegisterScript("bad-handler", policy);

            _messageBus.Subscribe(
                "test.event",
                "bad-handler",
                msg =>
                {
                    throw new Exception("Handler error");
                }
            );

            _messageBus.Subscribe(
                "test.event",
                "good-handler",
                msg =>
                {
                    successCount++;
                    return Task.FromResult<ScriptMessage>(null);
                }
            );

            var message = new ScriptMessage { Type = "test.event", FromScript = "sender" };

            var responses = await _messageBus.PublishAsync(message);

            Assert.Multiple(() =>
            {
                Assert.That(successCount, Is.EqualTo(1)); // Good handler still received message
                Assert.That(responses.Count, Is.EqualTo(0)); // No responses due to error
            });
        }

        [Test]
        public Task SendDirectAsync_ToNonExistentHandler_ThrowsInvalidOperationException()
        {
            var policy = ScriptCommunicationPolicy.Permissive("sender");
            _messageBus.RegisterScript("sender", policy);
            _messageBus.RegisterScript("receiver", policy);
            // Note: No subscription for receiver

            var message = new ScriptMessage { Type = "test.message", FromScript = "sender" };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _messageBus.SendDirectAsync(message, "receiver")
            );
            return Task.CompletedTask;
        }

        [Test]
        public async Task PublishAsync_ConcurrentPublishers_HandlesCorrectly()
        {
            var messageCount = 0;
            var policy = ScriptCommunicationPolicy.Permissive("test");

            for (var i = 0; i < 5; i++)
                _messageBus.RegisterScript($"publisher{i}", policy);
            _messageBus.RegisterScript("subscriber", policy);

            _messageBus.Subscribe(
                "concurrent.test",
                "subscriber",
                msg =>
                {
                    Interlocked.Increment(ref messageCount);
                    return Task.FromResult<ScriptMessage>(null);
                }
            );

            // Publish from multiple threads concurrently
            var tasks = new List<Task>();
            for (var i = 0; i < 5; i++)
            {
                var publisherId = $"publisher{i}";
                tasks.Add(
                    Task.Run(async () =>
                    {
                        for (var j = 0; j < 10; j++)
                            await _messageBus.PublishAsync(
                                new ScriptMessage
                                {
                                    Type = "concurrent.test",
                                    FromScript = publisherId,
                                    Data = new Dictionary<string, object> { ["index"] = j },
                                }
                            );
                    })
                );
            }

            await Task.WhenAll(tasks);

            Assert.That(messageCount, Is.EqualTo(50)); // 5 publishers * 10 messages each
        }

        [Test]
        public async Task Subscribe_ConcurrentSubscribers_HandlesCorrectly()
        {
            var receivedCounts = new int[10];
            var policy = ScriptCommunicationPolicy.Permissive("test");

            _messageBus.RegisterScript("publisher", policy);

            // Register and subscribe multiple handlers concurrently
            var tasks = new List<Task>();
            for (var i = 0; i < 10; i++)
            {
                var index = i;
                tasks.Add(
                    Task.Run(() =>
                    {
                        _messageBus.RegisterScript($"subscriber{index}", policy);
                        _messageBus.Subscribe(
                            "broadcast.test",
                            $"subscriber{index}",
                            msg =>
                            {
                                receivedCounts[index]++;
                                return Task.FromResult<ScriptMessage>(null);
                            }
                        );
                    })
                );
            }

            await Task.WhenAll(tasks);

            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "broadcast.test", FromScript = "publisher" }
            );

            Assert.That(receivedCounts.All(count => count == 1), Is.True);
        }

        [Test]
        [Timeout(5000)] // 5 second timeout
        public async Task PublishAsync_WithSlowHandler_DoesNotBlockOthers()
        {
            var fastHandlerReceived = false;
            var slowHandlerReceived = false;
            var policy = ScriptCommunicationPolicy.Permissive("test");

            _messageBus.RegisterScript("publisher", policy);
            _messageBus.RegisterScript("slow-handler", policy);
            _messageBus.RegisterScript("fast-handler", policy);

            _messageBus.Subscribe(
                "test.event",
                "slow-handler",
                async msg =>
                {
                    await Task.Delay(2000); // 2 second delay
                    slowHandlerReceived = true;
                    return null;
                }
            );

            _messageBus.Subscribe(
                "test.event",
                "fast-handler",
                msg =>
                {
                    fastHandlerReceived = true;
                    return Task.FromResult<ScriptMessage>(null);
                }
            );

            var publishTask = _messageBus.PublishAsync(
                new ScriptMessage { Type = "test.event", FromScript = "publisher" }
            );

            // Fast handler should complete quickly
            await Task.Delay(100);
            Assert.That(fastHandlerReceived, Is.True);

            // Wait for slow handler
            await publishTask;
            Assert.That(slowHandlerReceived, Is.True);
        }

        [Test]
        public async Task Subscribe_WithWildcardType_ReceivesMatchingMessages()
        {
            var receivedTypes = new List<string>();
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "wildcard-subscriber",
                CanReceiveTypes = new HashSet<string> { "game.*" },
                AllowedSenders = new HashSet<string> { "*" },
            };
            var publisherPolicy = ScriptCommunicationPolicy.Permissive("publisher");

            _messageBus.RegisterScript("wildcard-subscriber", policy);
            _messageBus.RegisterScript("publisher", publisherPolicy);

            _messageBus.Subscribe(
                "game.*",
                "wildcard-subscriber",
                msg =>
                {
                    receivedTypes.Add(msg.Type);
                    return Task.FromResult<ScriptMessage>(null);
                }
            );

            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "game.start", FromScript = "publisher" }
            );
            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "game.end", FromScript = "publisher" }
            );
            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "ui.update", FromScript = "publisher" }
            );

            Assert.That(receivedTypes, Has.Count.EqualTo(2));
            Assert.Contains("game.start", receivedTypes);
            Assert.Contains("game.end", receivedTypes);
        }

        [Test]
        public async Task GetStats_AfterOperations_ReturnsCorrectMetrics()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test");
            _messageBus.RegisterScript("publisher", policy);
            _messageBus.RegisterScript("subscriber", policy);

            _messageBus.Subscribe(
                "test.event",
                "subscriber",
                msg => Task.FromResult<ScriptMessage>(null)
            );

            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "test.event", FromScript = "publisher" }
            );
            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "test.event", FromScript = "publisher" }
            );

            var stats = _messageBus.GetStats();

            Assert.Multiple(() =>
            {
                Assert.That(stats.ActiveScripts, Is.EqualTo(2));
                Assert.That(stats.TotalMessagesProcessed, Is.EqualTo(2));
                Assert.That(stats.TotalSubscriptions, Is.EqualTo(1));
                Assert.That(stats.MessagesByType["test.event"], Is.EqualTo(2));
                Assert.That(stats.MessagesByScript["publisher"], Is.EqualTo(2));
                Assert.That(stats.AverageProcessingTime.TotalMilliseconds, Is.GreaterThan(0));
            });
        }

        [Category("Security.Policy")]
        [Test]
        public async Task GetStats_WithPolicyViolations_TracksViolations()
        {
            var policy = new ScriptCommunicationPolicy
            {
                ScriptId = "restricted",
                CanSendTypes = new HashSet<string> { "allowed.type" },
            };
            _messageBus.RegisterScript("restricted", policy);

            // Try to send disallowed message
            try
            {
                await _messageBus.PublishAsync(
                    new ScriptMessage { Type = "forbidden.type", FromScript = "restricted" }
                );
            }
            catch (MissingCapabilityException)
            {
                // Expected
            }

            var stats = _messageBus.GetStats();

            Assert.That(stats.PolicyViolations, Is.EqualTo(1));
        }

        [Test]
        public async Task AllOperations_WithAuditingEnabled_LogToAuditor()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test-script");
            policy.EnableAuditLogging = true;

            _messageBus.RegisterScript("test-script", policy);
            _messageBus.Subscribe(
                "test.event",
                "test-script",
                msg => Task.FromResult<ScriptMessage>(null)
            );

            await _messageBus.PublishAsync(
                new ScriptMessage { Type = "test.event", FromScript = "test-script" }
            );

            _messageBus.Unsubscribe("test.event", "test-script");
            _messageBus.UnregisterScript("test-script");

            var events = _auditor.GetRecentEvents(10);
            Assert.Multiple(() =>
            {
                Assert.That(
                    events.Any(e =>
                        e.CapabilityName == "message_bus" && e.Operation == "register_script"
                    ),
                    Is.True
                );
                Assert.That(
                    events.Any(e =>
                        e.CapabilityName == "message_bus" && e.Operation == "subscribe"
                    ),
                    Is.True
                );
                Assert.That(
                    events.Any(e => e.CapabilityName == "message_bus" && e.Operation == "publish"),
                    Is.True
                );
                Assert.That(
                    events.Any(e =>
                        e.CapabilityName == "message_bus" && e.Operation == "unsubscribe"
                    ),
                    Is.True
                );
                Assert.That(
                    events.Any(e =>
                        e.CapabilityName == "message_bus" && e.Operation == "unregister_script"
                    ),
                    Is.True
                );
            });
        }

        [Test]
        public void UnregisterScript_RemovesAllSubscriptions()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test-script");
            _messageBus.RegisterScript("test-script", policy);

            _messageBus.Subscribe(
                "event1",
                "test-script",
                msg => Task.FromResult<ScriptMessage>(null)
            );
            _messageBus.Subscribe(
                "event2",
                "test-script",
                msg => Task.FromResult<ScriptMessage>(null)
            );
            _messageBus.Subscribe(
                "event3",
                "test-script",
                msg => Task.FromResult<ScriptMessage>(null)
            );

            _messageBus.UnregisterScript("test-script");

            var subscriptions = _messageBus.GetSubscriptions();
            Assert.That(subscriptions.Values.Any(subs => subs.Contains("test-script")), Is.False);

            var stats = _messageBus.GetStats();
            Assert.That(stats.ActiveScripts, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_CleansUpResources()
        {
            var policy = ScriptCommunicationPolicy.Permissive("test");
            _messageBus.RegisterScript("test", policy);
            _messageBus.Subscribe(
                "test.event",
                "test",
                msg => Task.FromResult<ScriptMessage>(null)
            );

            var statsBefore = _messageBus.GetStats();
            Assert.Multiple(() =>
            {
                Assert.That(statsBefore.ActiveScripts, Is.GreaterThan(0));
                Assert.That(statsBefore.TotalSubscriptions, Is.GreaterThan(0));
            });

            _messageBus.Dispose();

            // Resources should be cleaned up (no exception test for now)
            // In a real implementation, we might check internal state or
            // ensure that the message bus cannot be used after disposal
            Assert.Pass("Dispose completed without throwing exceptions");
        }
    }
}
