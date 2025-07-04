using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Manages script capabilities and permission checking
    /// </summary>
    public interface ICapabilityManager
    {
        /// <summary>
        /// Checks if the script has the required capability
        /// </summary>
        void CheckCapability(ScriptCapabilities required, string operation);

        /// <summary>
        /// Grants a capability to the script
        /// </summary>
        void GrantCapability(ScriptCapabilities capability);

        /// <summary>
        /// Revokes a capability from the script
        /// </summary>
        void RevokeCapability(ScriptCapabilities capability);

        /// <summary>
        /// Gets the currently active capabilities
        /// </summary>
        ScriptCapabilities GetActiveCapabilities();
    }

    /// <summary>
    /// Default implementation of capability manager
    /// </summary>
    public class CapabilityManager : ICapabilityManager
    {
        private readonly SecurityConfiguration _config;
        private ScriptCapabilities _grantedCapabilities;

        /// <summary>
        /// Creates a new capability manager
        /// </summary>
        public CapabilityManager(SecurityConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _grantedCapabilities = config.Capabilities;
        }

        /// <summary>
        /// Checks if the script has the required capability
        /// </summary>
        public void CheckCapability(ScriptCapabilities required, string operation)
        {
            if (required == ScriptCapabilities.None)
                return;

            if ((_grantedCapabilities & required) != required)
            {
                throw new MissingCapabilityException(
                    $"Operation '{operation}' requires capability '{required}' which is not granted.",
                    operation);
            }
        }

        /// <summary>
        /// Grants a capability to the script
        /// </summary>
        public void GrantCapability(ScriptCapabilities capability)
        {
            // Only allow granting capabilities that were originally configured
            if ((capability & _config.Capabilities) == capability)
            {
                _grantedCapabilities |= capability;
            }
            else
            {
                throw new MissingCapabilityException(
                    $"Cannot grant capability '{capability}' as it exceeds configured security level.",
                    "GrantCapability");
            }
        }

        /// <summary>
        /// Revokes a capability from the script
        /// </summary>
        public void RevokeCapability(ScriptCapabilities capability)
        {
            _grantedCapabilities &= ~capability;
        }

        /// <summary>
        /// Gets the currently active capabilities
        /// </summary>
        public ScriptCapabilities GetActiveCapabilities()
        {
            return _grantedCapabilities;
        }
    }
}