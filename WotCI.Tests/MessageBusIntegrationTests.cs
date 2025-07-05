using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Communication;
using SolarSharp.Interpreter.Security;
using WotCI;
using WotCI.API;

namespace WotCI.Tests
{
    /// <summary>
    /// Integration tests for the message bus functionality in WotCI,
    /// validating cross-plugin communication and security boundaries.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    [Category("MessageBus")]
    public class MessageBusIntegrationTests
    {
        private GameSimulator _game;
        private EnhancedPluginManager _pluginManager;
        private string _testPluginsPath;
        private SecurityAuditor _auditor;

        [SetUp]
        public void Setup()
        {
            _game = new GameSimulator();
            _auditor = new SecurityAuditor();
            
            // Create temporary test plugins directory
            _testPluginsPath = Path.Combine(Path.GetTempPath(), "wotci_test_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testPluginsPath);
            Directory.CreateDirectory(Path.Combine(_testPluginsPath, "plugins"));
            Directory.CreateDirectory(Path.Combine(_testPluginsPath, "plugins", "user"));
            Directory.CreateDirectory(Path.Combine(_testPluginsPath, "plugins", "partner"));
            
            _pluginManager = new EnhancedPluginManager(_game, _testPluginsPath);
        }

        [TearDown]
        public void TearDown()
        {
            _pluginManager?.Dispose();
            
            // Clean up test directory
            if (Directory.Exists(_testPluginsPath))
            {
                Directory.Delete(_testPluginsPath, true);
            }
        }

        [Test]
        public async Task MessageBus_PluginRegistration_WorksCorrectly()
        {
                        var testPlugin = @"
                -- Test plugin that registers with message bus
                api.util.log('Test plugin starting', 'info')
                
                -- Subscribe to test messages
                api.messages.subscribe('test.message', function(msg)
                    api.util.log('Received: ' .. msg.data.content, 'info')
                    return { received = true }
                end)
                
                -- Send a test message
                api.messages.send('test.registration', {
                    pluginId = 'test-plugin',
                    status = 'ready'
                })
            ";
            
            var pluginPath = Path.Combine(_testPluginsPath, "plugins", "user", "test-plugin.lua");
            await File.WriteAllTextAsync(pluginPath, testPlugin);

                        await _pluginManager.EnablePluginAsync("test-plugin");
            await Task.Delay(100); // Allow plugin to initialize

                        var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            Assert.Greater(stats.TotalMessagesProcessed, 0);
            Assert.Contains("test.message", stats.MessagesByType.Keys);
        }

        [Test]
        public async Task MessageBus_CrossPluginCommunication_WorksWithinSecurityBounds()
        {
                        var senderPlugin = @"
                -- Sender plugin
                api.util.log('Sender starting', 'info')
                
                api.util.wait(0.5)
                
                -- Send message to receiver
                local responses = api.messages.send('plugin.communicate', {
                    from = 'sender',
                    message = 'Hello from sender!'
                })
                
                api.util.log('Message sent', 'info')
            ";
            
            var receiverPlugin = @"
                -- Receiver plugin
                api.util.log('Receiver starting', 'info')
                
                -- Subscribe to messages
                api.messages.subscribe('plugin.communicate', function(msg)
                    api.util.log('Received from ' .. msg.data.from .. ': ' .. msg.data.message, 'info')
                    
                    -- Send response
                    api.messages.send('plugin.response', {
                        from = 'receiver',
                        originalSender = msg.data.from,
                        response = 'Message received!'
                    })
                    
                    return { status = 'processed' }
                end)
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "sender.lua"), 
                senderPlugin);
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "receiver.lua"), 
                receiverPlugin);

                        await _pluginManager.EnablePluginAsync("receiver");
            await Task.Delay(100);
            await _pluginManager.EnablePluginAsync("sender");
            await Task.Delay(500); // Allow communication

                        var events = _auditor.GetRecentEvents(20);
            Assert.IsTrue(events.Any(e => 
                e.CapabilityName == "message_bus" && 
                e.Operation == "publish" &&
                e.Success));
        }

        [Test]
        public async Task MessageBus_UserPlugin_CannotPublishRestrictedMessages()
        {
                        var maliciousPlugin = @"
                -- Malicious plugin trying to send admin messages
                api.util.log('Malicious plugin starting', 'info')
                
                -- Try to send admin command (should fail)
                local success, err = pcall(function()
                    api.messages.send('system.admin.command', {
                        command = 'shutdown',
                        force = true
                    })
                end)
                
                if not success then
                    api.util.log('Admin message blocked as expected: ' .. tostring(err), 'info')
                else
                    api.util.log('SECURITY BREACH: Admin message was sent!', 'error')
                end
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "malicious.lua"), 
                maliciousPlugin);

                        await _pluginManager.EnablePluginAsync("malicious");
            await Task.Delay(200);

                        var violations = _auditor.GetRecentEvents(10)
                .Where(e => e.EventType == SecurityEventType.UnauthorizedOperation)
                .ToList();
            
            // Should have security violations for trying to send unauthorized messages
            Assert.Greater(violations.Count, 0);
        }

        [Test]
        public async Task MessageBus_PartnerPlugin_HasExpandedCapabilities()
        {
                        Directory.CreateDirectory(Path.Combine(_testPluginsPath, "plugins", "deadlock-digital"));
            
            var partnerPlugin = @"
                -- Partner plugin with expanded capabilities
                api.util.log('Partner plugin starting', 'info')
                
                -- Partners can publish game events
                local success1 = pcall(function()
                    api.messages.send('game.event', {
                        type = 'custom_achievement',
                        player = 'test',
                        achievement = 'partner_feature'
                    })
                end)
                
                -- Partners can send UI updates
                local success2 = pcall(function()
                    api.messages.send('ui.update', {
                        element = 'partner_widget',
                        visible = true
                    })
                end)
                
                api.util.log('Partner capabilities test - game.event: ' .. tostring(success1), 'info')
                api.util.log('Partner capabilities test - ui.update: ' .. tostring(success2), 'info')
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital", "partner-test.lua"), 
                partnerPlugin);

                        await _pluginManager.EnablePluginAsync("deadlock-digital/partner-test");
            await Task.Delay(200);

                        var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            Assert.IsTrue(stats.MessagesByType.ContainsKey("game.event") || 
                         stats.MessagesByType.ContainsKey("ui.update"));
        }

        [Test]
        public async Task MessageBus_RateLimiting_EnforcedCorrectly()
        {
                        var spammerPlugin = @"
                -- Plugin that tries to spam messages
                api.util.log('Spammer starting', 'info')
                
                local sent = 0
                local blocked = 0
                
                -- Try to send many messages quickly
                for i = 1, 50 do
                    local success = pcall(function()
                        api.messages.send('test.spam', {
                            index = i,
                            timestamp = os.time()
                        })
                    end)
                    
                    if success then
                        sent = sent + 1
                    else
                        blocked = blocked + 1
                    end
                end
                
                api.util.log('Sent: ' .. sent .. ', Blocked: ' .. blocked, 'info')
                
                -- Report results
                api.messages.send('test.rate.result', {
                    sent = sent,
                    blocked = blocked
                })
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "spammer.lua"), 
                spammerPlugin);

                        await _pluginManager.EnablePluginAsync("spammer");
            await Task.Delay(1000); // Allow rate limiting to kick in

                        var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            Assert.Greater(stats.DroppedMessages, 0, "Rate limiting should have dropped some messages");
        }

        [Test]
        public async Task MessageBus_CoordinatorPattern_WorksCorrectly()
        {
            // Create a simple coordinator and worker plugins
            var coordinatorPlugin = @"
                -- Simple coordinator
                api.util.log('Coordinator starting', 'info')
                
                local workers = {}
                
                -- Accept worker registration
                api.messages.subscribe('worker.register', function(msg)
                    workers[msg.data.workerId] = {
                        id = msg.data.workerId,
                        capabilities = msg.data.capabilities
                    }
                    api.util.log('Registered worker: ' .. msg.data.workerId, 'info')
                    return { status = 'registered' }
                end)
                
                -- Distribute tasks
                api.messages.subscribe('task.submit', function(msg)
                    -- Find suitable worker
                    for id, worker in pairs(workers) do
                        api.messages.send('task.assign', {
                            workerId = id,
                            task = msg.data.task
                        })
                        return { assigned = id }
                    end
                    return { error = 'No workers available' }
                end)
            ";
            
            var workerPlugin = @"
                -- Simple worker
                local workerId = 'worker_' .. api.util.random(1000, 9999)
                api.util.log('Worker ' .. workerId .. ' starting', 'info')
                
                -- Register with coordinator
                api.messages.send('worker.register', {
                    workerId = workerId,
                    capabilities = {'compute', 'analyze'}
                })
                
                -- Accept tasks
                api.messages.subscribe('task.assign', function(msg)
                    if msg.data.workerId == workerId then
                        api.util.log('Processing task: ' .. tostring(msg.data.task), 'info')
                        
                        -- Simulate work
                        api.util.wait(0.1)
                        
                        -- Report completion
                        api.messages.send('task.complete', {
                            workerId = workerId,
                            task = msg.data.task,
                            result = 'processed'
                        })
                    end
                end)
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "coordinator.lua"), 
                coordinatorPlugin);
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "worker.lua"), 
                workerPlugin);

                        await _pluginManager.EnablePluginAsync("coordinator");
            await Task.Delay(100);
            await _pluginManager.EnablePluginAsync("worker");
            await Task.Delay(500); // Allow registration and task processing

                        var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            Assert.Greater(stats.TotalMessagesProcessed, 0);
            Assert.IsTrue(stats.MessagesByType.ContainsKey("worker.register"));
        }

        [Test]
        public async Task MessageBus_DataPipeline_ProcessesCorrectly()
        {
            // Create a simple data pipeline
            var producerPlugin = @"
                -- Data producer
                api.util.log('Producer starting', 'info')
                
                -- Generate data
                for i = 1, 5 do
                    api.messages.send('data.raw', {
                        id = i,
                        value = api.util.random(1, 100),
                        timestamp = os.time()
                    })
                    api.util.wait(0.1)
                end
            ";
            
            var processorPlugin = @"
                -- Data processor
                api.util.log('Processor starting', 'info')
                
                local processed = 0
                
                api.messages.subscribe('data.raw', function(msg)
                    -- Process data
                    local processed_value = msg.data.value * 2
                    
                    -- Send processed data
                    api.messages.send('data.processed', {
                        originalId = msg.data.id,
                        originalValue = msg.data.value,
                        processedValue = processed_value,
                        processedAt = os.time()
                    })
                    
                    processed = processed + 1
                    api.util.log('Processed item ' .. msg.data.id, 'info')
                end)
            ";
            
            var consumerPlugin = @"
                -- Data consumer
                api.util.log('Consumer starting', 'info')
                
                local results = {}
                
                api.messages.subscribe('data.processed', function(msg)
                    table.insert(results, msg.data)
                    api.util.log('Consumed processed item ' .. msg.data.originalId, 'info')
                    
                    -- After collecting some results, generate report
                    if #results >= 3 then
                        api.messages.send('data.report', {
                            itemCount = #results,
                            totalProcessed = #results
                        })
                    end
                end)
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "producer.lua"), 
                producerPlugin);
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "processor.lua"), 
                processorPlugin);
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "consumer.lua"), 
                consumerPlugin);

                        await _pluginManager.EnablePluginAsync("consumer");
            await _pluginManager.EnablePluginAsync("processor");
            await Task.Delay(100);
            await _pluginManager.EnablePluginAsync("producer");
            await Task.Delay(1000); // Allow pipeline to process

                        var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            Assert.IsTrue(stats.MessagesByType.ContainsKey("data.raw"));
            Assert.IsTrue(stats.MessagesByType.ContainsKey("data.processed"));
            Assert.Greater(stats.TotalMessagesProcessed, 5); // At least producer messages
        }

        [Test]
        public void MessageBus_SecurityDashboard_ShowsCorrectMetrics()
        {
                        var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var metrics = _pluginManager.GetOverallSecurityMetrics();

                        Assert.IsNotNull(stats);
            Assert.IsNotNull(metrics);
            Assert.GreaterOrEqual(stats.ActiveScripts, 0);
            Assert.GreaterOrEqual(stats.TotalSubscriptions, 0);
        }

        [Test]
        public async Task MessageBus_PluginDisable_CleansUpCorrectly()
        {
                        var testPlugin = @"
                api.util.log('Test plugin for cleanup', 'info')
                
                -- Subscribe to multiple message types
                api.messages.subscribe('test.cleanup1', function(msg) end)
                api.messages.subscribe('test.cleanup2', function(msg) end)
                api.messages.subscribe('test.cleanup3', function(msg) end)
            ";
            
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "cleanup-test.lua"), 
                testPlugin);

                        await _pluginManager.EnablePluginAsync("cleanup-test");
            await Task.Delay(100);
            
            var statsBeforeDisable = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var subscriptionsBefore = statsBeforeDisable.TotalSubscriptions;
            
            _pluginManager.DisablePlugin("cleanup-test");
            await Task.Delay(100);
            
            var statsAfterDisable = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var subscriptionsAfter = statsAfterDisable.TotalSubscriptions;

                        Assert.Less(subscriptionsAfter, subscriptionsBefore, 
                "Subscriptions should be cleaned up when plugin is disabled");
        }
    }
}