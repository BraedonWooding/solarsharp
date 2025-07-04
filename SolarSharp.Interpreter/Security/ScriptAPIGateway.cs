using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Secure gateway that controls access between scripts and capabilities
    /// </summary>
    public interface IScriptAPIGateway
    {
        /// <summary>
        /// Registers a capability with the gateway
        /// </summary>
        void RegisterCapability(IScriptCapability capability);

        /// <summary>
        /// Calls a capability operation through the gateway
        /// </summary>
        object CallCapability(string capabilityName, string operation, params object[] parameters);

        /// <summary>
        /// Gets available capabilities for introspection
        /// </summary>
        IReadOnlyList<string> GetAvailableCapabilities();

        /// <summary>
        /// Gets available operations for a capability
        /// </summary>
        IReadOnlyCollection<string> GetCapabilityOperations(string capabilityName);

        /// <summary>
        /// Creates a Lua table that provides secure API access
        /// </summary>
        Table CreateLuaAPI(Script script);
    }

    /// <summary>
    /// Secure API gateway implementation
    /// </summary>
    public class ScriptAPIGateway : IScriptAPIGateway
    {
        private readonly ConcurrentDictionary<string, IScriptCapability> _capabilities = new();
        private readonly IRateLimiter _rateLimiter;
        private readonly ISecurityAuditor _auditor;
        private readonly SecurityConfiguration _config;

        public ScriptAPIGateway(SecurityConfiguration config, IRateLimiter rateLimiter = null, ISecurityAuditor auditor = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rateLimiter = rateLimiter;
            _auditor = auditor;
        }

        public void RegisterCapability(IScriptCapability capability)
        {
            if (capability == null)
                throw new ArgumentNullException(nameof(capability));

            _capabilities[capability.Name] = capability;
            _auditor?.LogCapabilityUsage("gateway", "register", new object[] { capability.Name }, null, true);
        }

        public object CallCapability(string capabilityName, string operation, params object[] parameters)
        {
            if (string.IsNullOrEmpty(capabilityName))
                throw new ArgumentException("Capability name cannot be null or empty", nameof(capabilityName));

            if (string.IsNullOrEmpty(operation))
                throw new ArgumentException("Operation cannot be null or empty", nameof(operation));

            // Check rate limiting
            var rateLimitKey = $"capability.{capabilityName}";
            if (_rateLimiter != null && !_rateLimiter.IsAllowed(rateLimitKey, operation))
            {
                throw new ResourceLimitExceededException($"Rate limit exceeded for capability '{capabilityName}.{operation}'", "CallCapability", capabilityName, operation);
            }

            // Find capability
            if (!_capabilities.TryGetValue(capabilityName, out var capability))
            {
                throw new MissingCapabilityException(
                    $"Capability '{capabilityName}' is not available",
                    "CallCapability", capabilityName, operation);
            }

            try
            {
                // Execute through the capability
                var result = capability.Execute(operation, parameters ?? Array.Empty<object>());

                // Record successful operation for rate limiting
                _rateLimiter?.RecordOperation(rateLimitKey, operation);

                return result;
            }
            catch (Exception ex)
            {
                _auditor?.LogSecurityViolation(
                    $"Capability call failed: {capabilityName}.{operation} - {ex.Message}",
                    SecurityEventType.UnauthorizedOperation,
                    ex);
                throw;
            }
        }

        public IReadOnlyList<string> GetAvailableCapabilities()
        {
            return _capabilities.Keys.ToList();
        }

        public IReadOnlyCollection<string> GetCapabilityOperations(string capabilityName)
        {
            if (_capabilities.TryGetValue(capabilityName, out var capability))
            {
                return capability.SupportedOperations;
            }
            return new List<string>();
        }

        public Table CreateLuaAPI(Script script)
        {
            var api = new Table(script);

            // Create capabilities table
            var capabilitiesTable = new Table(script);
            api["capabilities"] = capabilitiesTable;

            // Add call function
            capabilitiesTable["call"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 2)
                {
                    throw new ArgumentException("capabilities.call requires at least capability name and operation");
                }

                var capabilityName = args[0].CastToString();
                var operation = args[1].CastToString();
                var parameters = new object[Math.Max(0, args.Count - 2)];
                for (int i = 2; i < args.Count; i++)
                {
                    parameters[i - 2] = ConvertFromLua(args[i]);
                }

                var result = CallCapability(capabilityName, operation, parameters);
                return ConvertToLua(result, script);
            });

            // Add list function
            capabilitiesTable["list"] = DynValue.NewCallback((ctx, args) =>
            {
                var capabilities = GetAvailableCapabilities();
                var table = new Table(script);
                
                for (int i = 0; i < capabilities.Count; i++)
                {
                    table[i + 1] = DynValue.NewString(capabilities[i]);
                }
                
                return DynValue.NewTable(table);
            });

            // Add operations function
            capabilitiesTable["operations"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 1)
                {
                    throw new ArgumentException("capabilities.operations requires capability name");
                }

                var capabilityName = args[0].CastToString();
                var operations = GetCapabilityOperations(capabilityName);
                var table = new Table(script);
                
                int index = 1;
                foreach (var operation in operations)
                {
                    table[index++] = DynValue.NewString(operation);
                }
                
                return DynValue.NewTable(table);
            });

            // Add stats function for monitoring
            capabilitiesTable["stats"] = DynValue.NewCallback((ctx, args) =>
            {
                if (args.Count < 1)
                {
                    throw new ArgumentException("capabilities.stats requires capability name");
                }

                var capabilityName = args[0].CastToString();
                if (_capabilities.TryGetValue(capabilityName, out var capability))
                {
                    var stats = capability.GetUsageStats();
                    return ConvertStatsToLua(stats, script);
                }
                
                return DynValue.Nil;
            });

            return api;
        }

        private object ConvertFromLua(DynValue value)
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
                result[key] = value;
            }
            
            return result;
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

        private DynValue ConvertStatsToLua(CapabilityUsageStats stats, Script script)
        {
            var table = new Table(script);
            
            table["name"] = DynValue.NewString(stats.CapabilityName);
            table["totalOperations"] = DynValue.NewNumber(stats.TotalOperations);
            table["failedOperations"] = DynValue.NewNumber(stats.FailedOperations);
            table["blockedOperations"] = DynValue.NewNumber(stats.BlockedOperations);
            table["lastUsed"] = DynValue.NewString(stats.LastUsed.ToString("yyyy-MM-dd HH:mm:ss"));
            table["totalExecutionTime"] = DynValue.NewNumber(stats.TotalExecutionTime.TotalMilliseconds);

            // Operation counts
            var operationsTable = new Table(script);
            foreach (var (operation, count) in stats.OperationCounts)
            {
                operationsTable[operation] = DynValue.NewNumber(count);
            }
            table["operations"] = DynValue.NewTable(operationsTable);

            return DynValue.NewTable(table);
        }
    }

    /// <summary>
    /// Enhanced capability manager that works with the gateway
    /// </summary>
    public class EnhancedCapabilityManager : CapabilityManager
    {
        private readonly IScriptAPIGateway _gateway;
        private readonly ISecurityAuditor _auditor;

        public EnhancedCapabilityManager(SecurityConfiguration config, IScriptAPIGateway gateway, ISecurityAuditor auditor = null) 
            : base(config)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _auditor = auditor;
        }

        /// <summary>
        /// Grants a capability and registers it with the gateway
        /// </summary>
        public void GrantCapability(IScriptCapability capability)
        {
            if (capability == null)
                throw new ArgumentNullException(nameof(capability));

            _gateway.RegisterCapability(capability);
            _auditor?.LogCapabilityUsage("manager", "grant", new object[] { capability.Name }, null, true);
        }

        /// <summary>
        /// Creates the enhanced API table with capability support
        /// </summary>
        public Table CreateEnhancedAPI(Script script)
        {
            return _gateway.CreateLuaAPI(script);
        }
    }
}