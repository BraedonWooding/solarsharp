namespace WotCI
{
    /// <summary>
    /// Security constants specific to the WotCI plugin system.
    /// </summary>
    public static class WotCISecurityConstants
    {
        /// <summary>
        /// Plugin-specific security constants.
        /// </summary>
        public static class Plugins
        {
            /// <summary>
            /// Timeout for user-level plugins in seconds.
            /// User plugins have tight time constraints to prevent abuse.
            /// </summary>
            public const int UserPluginTimeoutSeconds = 5;

            /// <summary>
            /// Memory limit for user-level plugins in megabytes.
            /// User plugins are restricted to minimal memory usage.
            /// </summary>
            public const int UserPluginMemoryLimitMB = 32;

            /// <summary>
            /// Instruction limit for user-level plugins.
            /// User plugins can only execute a limited number of instructions.
            /// </summary>
            public const long UserPluginInstructionLimit = 100_000;

            /// <summary>
            /// Timeout for partner-level plugins in minutes.
            /// Partner plugins are allowed longer execution times for complex operations.
            /// </summary>
            public const int PartnerPluginTimeoutMinutes = 2;

            /// <summary>
            /// Memory limit for partner-level plugins in megabytes.
            /// Partner plugins have more generous memory allowances.
            /// </summary>
            public const int PartnerPluginMemoryLimitMB = 256;

            /// <summary>
            /// Update interval for plugin refresh checks in milliseconds.
            /// Controls how often the plugin system checks for updates.
            /// </summary>
            public const int PluginUpdateInterval = 5000;
        }

        /// <summary>
        /// Dashboard-specific security constants.
        /// </summary>
        public static class Dashboard
        {
            /// <summary>
            /// Update interval for dashboard refresh in milliseconds.
            /// Controls how often the security dashboard updates its display.
            /// </summary>
            public const int UpdateIntervalMs = 1000;

            /// <summary>
            /// Maximum number of historical events to display.
            /// Limits the event history to prevent memory growth.
            /// </summary>
            public const int MaxEventHistory = 100;

            /// <summary>
            /// Width of the dashboard panel in characters.
            /// </summary>
            public const int PanelWidth = 80;

            /// <summary>
            /// Maximum length of event messages in the dashboard.
            /// Longer messages are truncated for display.
            /// </summary>
            public const int MaxEventMessageLength = 60;

            /// <summary>
            /// Maximum number of recent events to display in the dashboard.
            /// </summary>
            public const int MaxRecentEvents = 20;

            /// <summary>
            /// Maximum number of events to fetch from the auditor.
            /// </summary>
            public const int MaxEventsFetch = 50;
        }
    }
}