namespace WotCI.Security
{
    /// <summary>
    /// Provides explicit permission checking methods for plugin trust levels
    /// </summary>
    public static class PluginPermissionChecks
    {
        /// <summary>
        /// Checks if the actual plugin trust level meets or exceeds the required level
        /// </summary>
        /// <param name="required">The required plugin trust level</param>
        /// <param name="actual">The actual plugin trust level</param>
        /// <returns>True if the actual level meets or exceeds the required level</returns>
        public static bool HasPluginTrustLevel(PluginTrustLevel required, PluginTrustLevel actual)
        {
            return required switch
            {
                PluginTrustLevel.User => true, // User level is always met
                PluginTrustLevel.Partner => actual is PluginTrustLevel.Partner or PluginTrustLevel.System,
                PluginTrustLevel.System => actual == PluginTrustLevel.System,
                _ => false,
            };
        }
    }
}
