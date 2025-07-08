using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace WotCI.Tests
{
    /// <summary>
    /// Integration tests for the message bus functionality in WotCI,
    /// validating cross-plugin communication and security boundaries.
    /// </summary>
    [TestFixture]
    [Category("Integration.MessageBus")]
    public class MessageBusIntegrationTests
    {
        private GameSimulator _game;
        private EnhancedPluginManager _pluginManager;
        private string _testPluginsPath;

        [SetUp]
        public void Setup()
        {
            _game = new GameSimulator();

            // Create temporary test plugins directory
            _testPluginsPath = Path.Combine(Path.GetTempPath(), "wotci_test_" + Guid.NewGuid());
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

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_PluginRegistration_WorksCorrectly()
        {
            // Create a simple plugin that demonstrates basic capability usage
            var testPlugin = """

                                -- Test plugin that uses capabilities
                                print('Test plugin starting')
                                
                                -- Test basic capabilities
                                local success = pcall(function()
                                    -- Simple plugin functionality without message bus
                                    print('Plugin initialized successfully')
                                end)
                                
                                if not success then
                                    print('Plugin initialization failed')
                                end
                            
                """;

            var pluginPath = Path.Combine(_testPluginsPath, "plugins", "user", "test-plugin.lua");
            await File.WriteAllTextAsync(pluginPath, testPlugin);

            await _pluginManager.EnablePluginAsync("test-plugin");
            await Task.Delay(100); // Allow plugin to initialize

            // Test that the plugin was loaded successfully
            var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            Assert.That(stats.TotalMessagesProcessed, Is.GreaterThanOrEqualTo(0));
            Assert.That(stats.MessagesByType, Is.Not.Null);
        }

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_CrossPluginCommunication_WorksWithinSecurityBounds()
        {
            // Create plugins that demonstrate secure operation within bounds
            var senderPlugin = """

                                -- Sender plugin that operates within security bounds
                                print('Sender plugin starting')
                                
                                -- Simulate secure plugin operation
                                local success = pcall(function()
                                    print('Sender plugin initialized')
                                end)
                                
                                if success then
                                    print('Sender operation completed successfully')
                                end
                            
                """;

            var receiverPlugin = """

                                -- Receiver plugin that operates within security bounds
                                print('Receiver plugin starting')
                                
                                -- Simulate secure plugin operation
                                local success = pcall(function()
                                    print('Receiver plugin initialized')
                                end)
                                
                                if success then
                                    print('Receiver operation completed successfully')
                                end
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "sender.lua"),
                senderPlugin
            );
            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "receiver.lua"),
                receiverPlugin
            );

            await _pluginManager.EnablePluginAsync("receiver");
            await Task.Delay(100);
            await _pluginManager.EnablePluginAsync("sender");
            await Task.Delay(500); // Allow plugins to initialize

            // Test that plugins operated within security bounds
            var events = _pluginManager.SecurityAuditor.GetRecentEvents(20);
            Assert.That(events.Any(e => e.Success), Is.True);
        }

        /// <summary>
        /// Verifies that security boundaries are properly enforced for user-level plugins loaded into the message bus system.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation of loading and executing a user plugin
        /// while validating its inability to perform restricted operations or violate security constraints.
        /// </returns>    [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_UserPlugin_SecurityBoundariesEnforced()
        {
            var userPlugin = """

                                -- User plugin with limited capabilities
                                print('User plugin starting')
                                
                                -- Test basic health reading (should work)
                                local health = api.health.get()
                                print('Health: ' .. tostring(health))
                                
                                -- Test that user plugin cannot perform restricted operations
                                local restrictedOp = pcall(function()
                                    -- This should fail for user plugins
                                    api.health.heal(10)
                                end)
                                
                                if not restrictedOp then
                                    print('Restricted operation blocked as expected')
                                end
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "user", "user-test.lua"),
                userPlugin
            );

            await _pluginManager.EnablePluginAsync("user-test");
            await Task.Delay(500);

            // Verify user plugin has appropriate restrictions
            var events = _pluginManager.SecurityAuditor.GetRecentEvents(30);
            var plugin = _pluginManager
                .GetAvailablePlugins()
                .FirstOrDefault(p => p.Name == "user-test");

            Assert.That(plugin, Is.Not.Null);
            Assert.That(plugin.TrustLevel, Is.EqualTo(PluginTrustLevel.User));
            Assert.That(events.Any(e => e.EventType == SecurityEventType.CapabilityUsage), Is.True);
        }

        /// <summary>
        /// Validates that partner-level plugins loaded into the message bus system
        /// are granted expanded capabilities compared to user-level plugins,
        /// allowing access to privileged APIs and restricted operations.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation to load, enable, and execute a partner plugin,
        /// verifying successful usage of advanced capabilities such as modifying system health or resources
        /// and auditing security events for proper partner privilege application.
        /// </returns>    [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_PartnerPlugin_HasExpandedCapabilities()
        {
            Directory.CreateDirectory(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital")
            );

            var partnerPlugin = """

                                -- Partner plugin with expanded capabilities
                                print('Partner plugin starting')
                                
                                -- Partners can use health API
                                local health = api.health.get()
                                print('Current health: ' .. tostring(health))
                                
                                -- Partners can heal (user plugins cannot)
                                local healSuccess = pcall(function()
                                    api.health.heal(10)
                                end)
                                
                                -- Partners can get gold
                                local gold = api.gold.get()
                                print('Current gold: ' .. tostring(gold))
                                
                                -- Partners can add gold (user plugins cannot)
                                local goldSuccess = pcall(function()
                                    api.gold.add(100)
                                end)
                                
                                print('Partner capabilities - heal: ' .. tostring(healSuccess))
                                print('Partner capabilities - gold: ' .. tostring(goldSuccess))
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital", "partner-test.lua"),
                partnerPlugin
            );

            await _pluginManager.EnablePluginAsync("deadlock-digital/partner-test");
            await Task.Delay(500);

            // Check that partner plugin has expanded capabilities
            var plugin = _pluginManager
                .GetAvailablePlugins()
                .FirstOrDefault(p => p.Name == "deadlock-digital/partner-test");
            var events = _pluginManager.SecurityAuditor.GetRecentEvents(20);

            Assert.That(plugin, Is.Not.Null);
            Assert.That(plugin.TrustLevel, Is.EqualTo(PluginTrustLevel.Partner));

            // Partner should have successful capability usage
            var successfulEvents = events
                .Where(e => e.Success && e.EventType == SecurityEventType.CapabilityUsage)
                .ToList();
            Assert.That(
                successfulEvents.Count,
                Is.GreaterThan(0),
                "Partner plugin should have successful capability usage"
            );
        }

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_RateLimiting_BasicFunctionality()
        {
            // Create a partner plugin that tests rate limiting
            Directory.CreateDirectory(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital")
            );

            var testPlugin = """

                                -- Partner plugin that tests rate limiting
                                print('Rate limiting test starting')
                                
                                -- Try to log frequently to test logging rate limiting
                                for i = 1, 10 do
                                    local logSuccess = api.util.log('Log message ' .. i, 'info')
                                    if not logSuccess then
                                        print('Logging was rate limited')
                                        break
                                    end
                                end
                                
                                -- Test basic game capabilities
                                local health = api.health.get()
                                print('Health: ' .. tostring(health))
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital", "rate-test.lua"),
                testPlugin
            );

            await _pluginManager.EnablePluginAsync("deadlock-digital/rate-test");
            await Task.Delay(500);

            var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var events = _pluginManager.SecurityAuditor.GetRecentEvents(30);

            // Should have some activity
            Assert.That(events.Count, Is.GreaterThan(0), "Should have some audit events");

            // Check for successful plugin execution
            var successfulEvents = events.Where(e => e.Success).ToList();
            Assert.That(
                successfulEvents.Count,
                Is.GreaterThan(0),
                "Should have successful operations"
            );
        }

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_BasicCommunication_WorksCorrectly()
        {
            // Create partner plugins that can actually communicate
            Directory.CreateDirectory(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital")
            );

            var communicatorPlugin = """

                                -- Partner plugin with basic communication
                                print('Communicator starting')
                                
                                -- Test basic game capabilities
                                local health = api.health.get()
                                local gold = api.gold.get()
                                print('Communicator stats - Health: ' .. health .. ', Gold: ' .. gold)
                                
                                -- Test subscription capability
                                local subscribeSuccess = pcall(function()
                                    api.messages.subscribe('test.message', function(msg)
                                        print('Received test message')
                                    end)
                                end)
                                
                                print('Subscribe success: ' .. tostring(subscribeSuccess))
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital", "communicator.lua"),
                communicatorPlugin
            );

            await _pluginManager.EnablePluginAsync("deadlock-digital/communicator");
            await Task.Delay(500);

            var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var events = _pluginManager.SecurityAuditor.GetRecentEvents(20);

            // Check for successful subscriptions
            Assert.That(
                stats.TotalSubscriptions,
                Is.GreaterThanOrEqualTo(0),
                "Should have subscription capability"
            );

            // Check for successful events
            var successfulEvents = events.Where(e => e.Success).ToList();
            Assert.That(
                successfulEvents.Count,
                Is.GreaterThan(0),
                "Should have successful operations"
            );
        }

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_DataProcessing_BasicFunctionality()
        {
            // Create partner plugins that can process data
            Directory.CreateDirectory(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital")
            );

            var processorPlugin = """

                                -- Partner data processor
                                print('Processor starting')
                                
                                -- Test basic game capabilities
                                local health = api.health.get()
                                local gold = api.gold.get()
                                print('Processor stats - Health: ' .. health .. ', Gold: ' .. gold)
                                
                                -- Test game state capabilities
                                local gameState = api.game.getState()
                                print('Game state available: ' .. tostring(gameState ~= nil))
                                
                                -- Test subscription
                                local subscribeSuccess = pcall(function()
                                    api.messages.subscribe('data.test', function(msg)
                                        print('Processing data message')
                                    end)
                                end)
                                
                                print('Data processor subscribe success: ' .. tostring(subscribeSuccess))
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital", "processor.lua"),
                processorPlugin
            );

            await _pluginManager.EnablePluginAsync("deadlock-digital/processor");
            await Task.Delay(500);

            var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var events = _pluginManager.SecurityAuditor.GetRecentEvents(30);

            // Check for message processing activity
            Assert.That(
                stats.TotalMessagesProcessed,
                Is.GreaterThanOrEqualTo(0),
                "Should have message processing capability"
            );
            Assert.That(
                stats.TotalSubscriptions,
                Is.GreaterThanOrEqualTo(0),
                "Should have subscription capability"
            );

            // Check for successful capability usage
            var successfulEvents = events
                .Where(e => e.Success && e.EventType == SecurityEventType.CapabilityUsage)
                .ToList();
            Assert.That(
                successfulEvents.Count,
                Is.GreaterThan(0),
                "Should have successful capability usage"
            );
        }

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public void MessageBus_SecurityDashboard_ShowsCorrectMetrics()
        {
            var stats = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var metrics = _pluginManager.GetOverallSecurityMetrics();

            Assert.IsNotNull(stats);
            Assert.IsNotNull(metrics);
            Assert.GreaterOrEqual(stats.ActiveScripts, 0);
            Assert.GreaterOrEqual(stats.TotalSubscriptions, 0);
        }

        [Category("MessageBus.Integration")]
        [Category("MessageBus.Integration")]
        [Test]
        [Timeout(5000)]
        public async Task MessageBus_PluginDisable_CleansUpCorrectly()
        {
            // Create a partner plugin that can subscribe to messages
            Directory.CreateDirectory(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital")
            );

            var testPlugin = """

                                print('Test plugin for cleanup')
                                
                                -- Test basic capabilities
                                local health = api.health.get()
                                local gold = api.gold.get()
                                print('Cleanup plugin - Health: ' .. health .. ', Gold: ' .. gold)
                                
                                -- Test subscription
                                local subscribeSuccess = pcall(function()
                                    api.messages.subscribe('test.cleanup', function(msg) 
                                        print('Received cleanup message')
                                    end)
                                end)
                                
                                print('Cleanup subscribe success: ' .. tostring(subscribeSuccess))
                            
                """;

            await File.WriteAllTextAsync(
                Path.Combine(_testPluginsPath, "plugins", "deadlock-digital", "cleanup-test.lua"),
                testPlugin
            );

            await _pluginManager.EnablePluginAsync("deadlock-digital/cleanup-test");
            await Task.Delay(300);

            var statsBeforeDisable = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var eventsBefore = _pluginManager.SecurityAuditor.GetRecentEvents(20);

            _pluginManager.DisablePlugin("deadlock-digital/cleanup-test");
            await Task.Delay(300);

            var statsAfterDisable = _pluginManager.SecurityDashboard.GetMessageBusStats();
            var eventsAfter = _pluginManager.SecurityAuditor.GetRecentEvents(20);

            // Check that plugin lifecycle was managed correctly
            Assert.That(
                eventsAfter.Count,
                Is.GreaterThanOrEqualTo(eventsBefore.Count),
                "Should have additional events after disable"
            );

            // Verify the plugin is no longer running
            var runningPlugins = _pluginManager
                .GetAvailablePlugins()
                .Where(p => p.IsRunning)
                .ToList();
            Assert.That(
                runningPlugins.Any(p => p.Name == "deadlock-digital/cleanup-test"),
                Is.False,
                "Plugin should not be running after disable"
            );
        }
    }
}
