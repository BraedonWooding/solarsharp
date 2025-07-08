using System.Runtime.CompilerServices;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// Security-related extensions for the Script class
    /// </summary>
    public static class ScriptSecurityExtensions
    {
        private static readonly ConditionalWeakTable<Script, ScriptSecurityData> _securityData =
            new ConditionalWeakTable<Script, ScriptSecurityData>();

        /// <summary>
        /// Gets or sets the security configuration for this script
        /// </summary>
        public static SecurityPolicy SecurityPolicy(this Script script)
        {
            return _securityData.GetOrCreateValue(script).Configuration;
        }

        /// <summary>
        /// Sets the security configuration for this script
        /// </summary>
        public static void SetSecurityPolicy(this Script script, SecurityPolicy policy)
        {
            _securityData.GetOrCreateValue(script).Configuration = policy;
        }

        /// <summary>
        /// Gets or sets the resource controller for this script
        /// </summary>
        public static ResourceController ResourceController(this Script script)
        {
            return _securityData.GetOrCreateValue(script).ResourceController;
        }

        /// <summary>
        /// Sets the resource controller for this script
        /// </summary>
        public static void SetResourceController(this Script script, ResourceController controller)
        {
            _securityData.GetOrCreateValue(script).ResourceController = controller;
        }

        /// <summary>
        /// Gets or sets the security event handler for this script
        /// </summary>
        public static SecurityEventHandler SecurityEventHandler(this Script script)
        {
            // Try to get from service collection first
            var eventHandler = script.GetService<SecurityEventHandler>();
            if (eventHandler != null)
                return eventHandler;

            // Fall back to stored value
            return _securityData.GetOrCreateValue(script).EventHandler;
        }

        /// <summary>
        /// Sets the security event handler for this script
        /// </summary>
        public static void SetSecurityEventHandler(this Script script, SecurityEventHandler handler)
        {
            _securityData.GetOrCreateValue(script).EventHandler = handler;
        }

        /// <summary>
        /// Gets whether this script has authorization to run (has valid SecurityConfiguration)
        /// </summary>
        public static bool IsAuthorizedToRun(this Script script)
        {
            var data = _securityData.GetOrCreateValue(script);

            // Host-initiated execution is always allowed (bypasses all policy checks)
            if (data.IsHostInitiated)
            {
                return true;
            }

            // Script-initiated execution requires a valid security configuration
            return data.Configuration != null;
        }

        /// <summary>
        /// Gets or sets whether the current execution was initiated by the host
        /// </summary>
        public static bool IsHostInitiatedExecution(this Script script)
        {
            return _securityData.GetOrCreateValue(script).IsHostInitiated;
        }

        /// <summary>
        /// Sets whether the current execution was initiated by the host
        /// </summary>
        public static void SetHostInitiatedExecution(this Script script, bool value)
        {
            _securityData.GetOrCreateValue(script).IsHostInitiated = value;
        }

        /// <summary>
        /// Data holder for security-related properties
        /// </summary>
        private class ScriptSecurityData
        {
            public SecurityPolicy Configuration { get; set; }
            public ResourceController ResourceController { get; set; }
            public SecurityEventHandler EventHandler { get; set; }
            public bool IsHostInitiated { get; set; }
        }
    }
}
