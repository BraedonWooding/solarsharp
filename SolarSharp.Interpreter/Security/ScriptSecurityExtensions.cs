using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// Security-related extensions for the Script class
    /// </summary>
    public static class ScriptSecurityExtensions
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Script, ScriptSecurityData> _securityData = new();

        /// <summary>
        /// Gets or sets the security configuration for this script
        /// </summary>
        public static SecurityConfiguration SecurityConfiguration(this Script script)
        {
            return _securityData.GetOrCreateValue(script).Configuration;
        }

        /// <summary>
        /// Sets the security configuration for this script
        /// </summary>
        public static void SetSecurityConfiguration(this Script script, SecurityConfiguration configuration)
        {
            _securityData.GetOrCreateValue(script).Configuration = configuration;
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
            return data.Configuration != null;
        }

        /// <summary>
        /// Data holder for security-related properties
        /// </summary>
        private class ScriptSecurityData
        {
            public SecurityConfiguration Configuration { get; set; }
            public ResourceController ResourceController { get; set; }
            public SecurityEventHandler EventHandler { get; set; }
        }
    }
}