using System;
using System.Collections.Generic;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Communication;
using SolarSharp.Interpreter.DataTypes;
using WotCI.Security;

namespace WotCI.API
{
    /// <summary>
    /// Enhanced game API facade that provides secure, capability-based access to game functionality
    /// </summary>
    public class EnhancedGameAPIFacade
    {
        private readonly GameSimulator _game;
        private readonly IScriptAPIGateway _gateway;
        private readonly IScriptMessageBus? _messageBus;
        private readonly ISecurityAuditor? _auditor;
        private readonly IRateLimiter? _rateLimiter;

        public EnhancedGameAPIFacade(
            GameSimulator game, 
            IScriptAPIGateway gateway,
            IScriptMessageBus? messageBus = null,
            ISecurityAuditor? auditor = null,
            IRateLimiter? rateLimiter = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _messageBus = messageBus;
            _auditor = auditor;
            _rateLimiter = rateLimiter;
        }

        /// <summary>
        /// Sets up capabilities for a plugin based on its trust level
        /// </summary>
        public void SetupCapabilities(PluginTrustLevel trustLevel)
        {
            // Register core game capabilities
            _gateway.RegisterCapability(new HealthCapability(_game, trustLevel, _auditor));
            _gateway.RegisterCapability(new InventoryCapability(_game, trustLevel, _auditor));
            _gateway.RegisterCapability(new CombatCapability(_game, trustLevel, _auditor));
            _gateway.RegisterCapability(new GameStateCapability(_game, trustLevel, _auditor));

            // Register message bus capability if available
            if (_messageBus != null)
            {
                _gateway.RegisterCapability(new MessageBusCapability(_messageBus, trustLevel, _auditor));
            }

            _auditor?.LogCapabilityUsage("api_facade", "setup_capabilities", 
                new object[] { trustLevel.ToString() }, null, true);
        }

        /// <summary>
        /// Creates the enhanced Lua API for a script
        /// </summary>
        public Table CreateEnhancedAPI(Script script, string scriptId, PluginTrustLevel trustLevel)
        {
            var api = _gateway.CreateLuaAPI(script);
            
            // Add convenience functions that wrap capability calls
            AddConvenienceFunctions(api, script, trustLevel);

            // Add messaging functions if message bus is available
            if (_messageBus != null)
            {
                AddMessagingFunctions(api, script, scriptId, trustLevel);
            }

            // Add utility functions
            AddUtilityFunctions(api, script, trustLevel);

            return api;
        }

        private void AddConvenienceFunctions(Table api, Script script, PluginTrustLevel trustLevel)
        {
            // Health convenience functions
            var healthTable = new Table(script);
            api["health"] = healthTable;

            healthTable["get"] = DynValue.NewCallback((ctx, args) =>
            {
                var result = _gateway.CallCapability("health", "read");
                return DynValue.NewNumber((int)result);
            });

            if (trustLevel >= PluginTrustLevel.Partner)
            {
                healthTable["heal"] = DynValue.NewCallback((ctx, args) =>
                {
                    if (args.Count < 1) throw new ArgumentException("heal requires amount parameter");
                    var amount = (int)args[0].Number;
                    var result = _gateway.CallCapability("health", "heal", amount);
                    return ConvertToLua(result, script);
                });

                healthTable["set"] = DynValue.NewCallback((ctx, args) =>
                {
                    if (args.Count < 1) throw new ArgumentException("set requires health parameter");
                    var health = (int)args[0].Number;
                    var result = _gateway.CallCapability("health", "modify", health);
                    return ConvertToLua(result, script);
                });
            }

            // Gold convenience functions
            var goldTable = new Table(script);
            api["gold"] = goldTable;

            goldTable["get"] = DynValue.NewCallback((ctx, args) =>
            {
                var result = _gateway.CallCapability("inventory", "get_gold");
                if (result is Dictionary<string, object> dict && dict.TryGetValue("gold", out var gold))
                {
                    return DynValue.NewNumber((int)gold);
                }
                return DynValue.NewNumber(0);
            });

            if (trustLevel >= PluginTrustLevel.Partner)
            {
                goldTable["add"] = DynValue.NewCallback((ctx, args) =>
                {
                    if (args.Count < 1) throw new ArgumentException("add requires amount parameter");
                    var amount = (int)args[0].Number;
                    var result = _gateway.CallCapability("inventory", "add_gold", amount);
                    return ConvertToLua(result, script);
                });
            }

            // Game state convenience functions
            var gameTable = new Table(script);
            api["game"] = gameTable;

            gameTable["getStats"] = DynValue.NewCallback((ctx, args) =>
            {
                var result = _gateway.CallCapability("combat", "get_stats");
                return ConvertToLua(result, script);
            });

            gameTable["getState"] = DynValue.NewCallback((ctx, args) =>
            {
                var keys = args.Count > 0 ? 
                    new object[] { args[0].CastToString() } : 
                    Array.Empty<object>();
                var result = _gateway.CallCapability("gamestate", "read_filtered", keys);
                return ConvertToLua(result, script);
            });

            gameTable["getSnapshot"] = DynValue.NewCallback((ctx, args) =>
            {
                var result = _gateway.CallCapability("gamestate", "get_snapshot");
                if (result is ReadOnlyScriptState snapshot)
                {
                    return DynValue.NewTable(snapshot.ToLuaTable(script));
                }
                return DynValue.Nil;
            });
        }

        private void AddMessagingFunctions(Table api, Script script, string scriptId, PluginTrustLevel trustLevel)
        {
            var msgTable = new Table(script);
            api["messages"] = msgTable;

            msgTable["send"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 2) throw new ArgumentException("send requires messageType and data parameters");
                
                var messageType = args[0].CastToString();
                var data = ConvertFromLua(args[1]);
                
                var message = new ScriptMessage
                {
                    Type = messageType,
                    FromScript = scriptId,
                    Data = data is Dictionary<string, object> dict ? dict : new Dictionary<string, object> { ["value"] = data ?? DBNull.Value }
                };

                var result = _gateway.CallCapability("message_bus", "publish", message);
                return DynValue.NewBoolean(true);
            });

            msgTable["sendTo"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 3) throw new ArgumentException("sendTo requires targetScript, messageType, and data parameters");
                
                var targetScript = args[0].CastToString();
                var messageType = args[1].CastToString();
                var data = ConvertFromLua(args[2]);
                
                var message = new ScriptMessage
                {
                    Type = messageType,
                    FromScript = scriptId,
                    ToScript = targetScript,
                    Data = data is Dictionary<string, object> dict ? dict : new Dictionary<string, object> { ["value"] = data ?? DBNull.Value }
                };

                var result = _gateway.CallCapability("message_bus", "send_direct", message, targetScript);
                return ConvertToLua(result, script);
            });

            msgTable["subscribe"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 2) throw new ArgumentException("subscribe requires messageType and handler parameters");
                
                var messageType = args[0].CastToString();
                var handler = args[1];
                
                if (handler.Type != DataType.Function)
                    throw new ArgumentException("Handler must be a function");

                // Create a wrapper that converts between C# and Lua
                Func<ScriptMessage, System.Threading.Tasks.Task<ScriptMessage?>> wrappedHandler = (msg) =>
                {
                    try
                    {
                        var luaMessage = ConvertMessageToLua(msg, script);
                        var result = script.Call(handler, luaMessage);
                        
                        if (result.Type == DataType.Table)
                        {
                            var responseData = ConvertTableFromLua(result.Table);
                            return System.Threading.Tasks.Task.FromResult<ScriptMessage?>(msg.CreateResponse(responseData));
                        }
                        
                        return System.Threading.Tasks.Task.FromResult<ScriptMessage?>(null); // No response
                    }
                    catch (Exception ex)
                    {
                        _auditor?.LogSecurityViolation($"Message handler error: {ex.Message}", 
                            SecurityEventType.UnauthorizedOperation, ex);
                        return System.Threading.Tasks.Task.FromResult<ScriptMessage?>(null);
                    }
                };

                var result = _gateway.CallCapability("message_bus", "subscribe", messageType, scriptId, wrappedHandler);
                return DynValue.NewBoolean(true);
            });
        }

        private void AddUtilityFunctions(Table api, Script script, PluginTrustLevel trustLevel)
        {
            var utilTable = new Table(script);
            api["util"] = utilTable;

            // Logging function with rate limiting
            utilTable["log"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 1) return DynValue.Nil;
                
                var message = args[0].CastToString();
                var level = args.Count > 1 ? args[1].CastToString() : "info";

                // Rate limit logging
                if (_rateLimiter?.IsAllowed("logging", level) == false)
                {
                    return DynValue.NewBoolean(false);
                }

                _auditor?.LogCapabilityUsage("logging", level, new object[] { message }, null, true);
                _rateLimiter?.RecordOperation("logging", level);

                // In a real implementation, this would integrate with the game's logging system
                Console.WriteLine($"[{level.ToUpper()}] {message}");
                
                return DynValue.NewBoolean(true);
            });

            // Safe wait function
            utilTable["wait"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 1) return DynValue.Nil;
                
                var seconds = Math.Min(args[0].Number, 5.0); // Max 5 second wait
                System.Threading.Thread.Sleep((int)(seconds * 1000));
                
                return DynValue.Nil;
            });

            // Get trust level
            utilTable["getTrustLevel"] = DynValue.NewCallback((ctx, args) =>
            {
                return DynValue.NewString(trustLevel.ToString());
            });

            // Safe random function
            var random = new Random();
            utilTable["random"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count == 0)
                    return DynValue.NewNumber(random.NextDouble());
                if (args.Count == 1)
                    return DynValue.NewNumber(random.Next((int)args[0].Number));
                if (args.Count == 2)
                    return DynValue.NewNumber(random.Next((int)args[0].Number, (int)args[1].Number));
                
                return DynValue.NewNumber(random.NextDouble());
            });
        }

        private DynValue ConvertToLua(object value, Script script)
        {
            return value switch
            {
                null => DynValue.Nil,
                bool b => DynValue.NewBoolean(b),
                int i => DynValue.NewNumber(i),
                double d => DynValue.NewNumber(d),
                float f => DynValue.NewNumber(f),
                string s => DynValue.NewString(s),
                Dictionary<string, object> dict => ConvertDictionaryToLua(dict, script),
                _ => DynValue.FromObject(script, value)
            };
        }

        private DynValue ConvertDictionaryToLua(Dictionary<string, object> dict, Script script)
        {
            var table = new Table(script);
            foreach (var (key, value) in dict)
            {
                table[key] = ConvertToLua(value, script);
            }
            return DynValue.NewTable(table);
        }

        private object? ConvertFromLua(DynValue value)
        {
            return value.Type switch
            {
                DataType.Nil => null,
                DataType.Boolean => value.Boolean,
                DataType.Number => value.Number,
                DataType.String => value.String,
                DataType.Table => ConvertTableFromLua(value.Table),
                _ => value.ToObject()
            };
        }

        private Dictionary<string, object> ConvertTableFromLua(Table table)
        {
            var result = new Dictionary<string, object>();
            foreach (var pair in table)
            {
                var key = pair.Key.CastToString();
                var value = ConvertFromLua(pair.Value);
                if (value != null)
                {
                    result[key] = value;
                }
            }
            return result;
        }

        private DynValue ConvertMessageToLua(ScriptMessage message, Script script)
        {
            var table = new Table(script);
            table["id"] = DynValue.NewString(message.Id);
            table["type"] = DynValue.NewString(message.Type);
            table["from"] = DynValue.NewString(message.FromScript);
            table["to"] = DynValue.NewString(message.ToScript ?? "");
            table["timestamp"] = DynValue.NewString(message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            table["data"] = ConvertDictionaryToLua(message.Data, script);
            
            return DynValue.NewTable(table);
        }
    }

    /// <summary>
    /// Message bus capability for inter-script communication
    /// </summary>
    public class MessageBusCapability : ScriptCapabilityBase
    {
        private readonly IScriptMessageBus _messageBus;
        private readonly PluginTrustLevel _trustLevel;
        private static readonly HashSet<string> _supportedOps = new() { "publish", "send_direct", "subscribe", "unsubscribe" };

        public MessageBusCapability(IScriptMessageBus messageBus, PluginTrustLevel trustLevel, ISecurityAuditor? auditor = null) 
            : base("message_bus", auditor)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _trustLevel = trustLevel;
        }

        public override IReadOnlySet<string> SupportedOperations => _supportedOps;

        protected override bool IsOperationAllowed(string operation, object[] parameters)
        {
            return operation switch
            {
                "publish" => _trustLevel >= PluginTrustLevel.Partner, // Partners can publish
                "send_direct" => _trustLevel >= PluginTrustLevel.Partner, // Partners can send direct
                "subscribe" => true, // All can subscribe
                "unsubscribe" => true, // All can unsubscribe
                _ => false
            };
        }

        protected override object ExecuteOperation(string operation, object[] parameters)
        {
            return operation switch
            {
                "publish" => ExecutePublishSync(parameters),
                "send_direct" => ExecuteSendDirectSync(parameters),
                "subscribe" => ExecuteSubscribe(parameters),
                "unsubscribe" => ExecuteUnsubscribe(parameters),
                _ => throw new InvalidOperationException($"Unknown operation: {operation}")
            };
        }

        public override ValidationResult ValidateParameters(string operation, object[] parameters)
        {
            return operation switch
            {
                "publish" when parameters?.Length > 0 && parameters[0] is ScriptMessage => ValidationResult.Valid(),
                "publish" => ValidationResult.Invalid("Publish requires a ScriptMessage parameter"),
                "send_direct" when parameters?.Length > 1 && parameters[0] is ScriptMessage && parameters[1] is string => ValidationResult.Valid(),
                "send_direct" => ValidationResult.Invalid("Send direct requires ScriptMessage and target script ID"),
                "subscribe" when parameters?.Length > 2 => ValidationResult.Valid(),
                "subscribe" => ValidationResult.Invalid("Subscribe requires message type, script ID, and handler"),
                "unsubscribe" when parameters?.Length > 1 => ValidationResult.Valid(),
                "unsubscribe" => ValidationResult.Invalid("Unsubscribe requires message type and script ID"),
                _ => ValidationResult.Invalid($"Unknown operation: {operation}")
            };
        }

        private object ExecutePublishSync(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is ScriptMessage message)
            {
                // Use GetAwaiter().GetResult() which provides better exception handling than .Result
                var responses = _messageBus.PublishAsync(message).GetAwaiter().GetResult();
                return responses;
            }
            throw new ArgumentException("Invalid publish parameters");
        }

        private object ExecuteSendDirectSync(object[] parameters)
        {
            if (parameters?.Length > 1 && parameters[0] is ScriptMessage message && parameters[1] is string targetId)
            {
                // Use GetAwaiter().GetResult() which provides better exception handling than .Result
                var response = _messageBus.SendDirectAsync(message, targetId).GetAwaiter().GetResult();
                return response;
            }
            throw new ArgumentException("Invalid send direct parameters");
        }

        private object ExecuteSubscribe(object[] parameters)
        {
            if (parameters?.Length > 2 && 
                parameters[0] is string messageType && 
                parameters[1] is string scriptId && 
                parameters[2] is Func<ScriptMessage, System.Threading.Tasks.Task<ScriptMessage>> handler)
            {
                _messageBus.Subscribe(messageType, scriptId, handler);
                return true;
            }
            throw new ArgumentException("Invalid subscribe parameters");
        }

        private object ExecuteUnsubscribe(object[] parameters)
        {
            if (parameters?.Length > 1 && parameters[0] is string messageType && parameters[1] is string scriptId)
            {
                _messageBus.Unsubscribe(messageType, scriptId);
                return true;
            }
            throw new ArgumentException("Invalid unsubscribe parameters");
        }
    }
}