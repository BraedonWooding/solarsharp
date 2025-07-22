namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Central location for security-related constants and limits
    /// </summary>
    public static class SecurityConstants
    {
        /// <summary>
        /// Memory limit constants
        /// </summary>
        public static class Memory
        {
            /// <summary>1KB in bytes</summary>
            public const int OneKB = 1024;

            /// <summary>1MB in bytes</summary>
            public const int OneMB = 1024 * 1024;

            /// <summary>10MB in bytes</summary>
            public const int TenMB = 10 * OneMB;

            /// <summary>100MB in bytes</summary>
            public const int HundredMB = 100 * OneMB;

            /// <summary>1GB in bytes</summary>
            public const int OneGB = 1024 * OneMB;

            /// <summary>Default script memory limit</summary>
            public const int DefaultScriptMemoryLimit = 256 * OneMB;

            /// <summary>Default buffer size for operations</summary>
            public const int DefaultBufferSize = OneKB;

            /// <summary>Maximum allowed string builder capacity</summary>
            public const int MaxStringBuilderCapacity = OneKB;
        }

        /// <summary>
        /// Timeout constants in milliseconds
        /// </summary>
        public static class Timeouts
        {
            /// <summary>5 seconds</summary>
            public const int FiveSeconds = 5000;

            /// <summary>30 seconds</summary>
            public const int ThirtySeconds = 30000;

            /// <summary>1 minute</summary>
            public const int OneMinute = 60000;

            /// <summary>5 minutes</summary>
            public const int FiveMinutes = 300000;

            /// <summary>30 minutes</summary>
            public const int ThirtyMinutes = 1800000;

            /// <summary>2 minutes - default script timeout</summary>
            public const int DefaultScriptTimeout = 120000;

            /// <summary>Maximum allowed timeout (24 hours)</summary>
            public const int MaxTimeout = 24 * 60 * 60 * 1000;
        }

        /// <summary>
        /// Cryptographic constants
        /// </summary>
        public static class Crypto
        {
            /// <summary>Minimum key size in bits</summary>
            public const int MinimumKeySize = 256;

            /// <summary>Default RSA key size</summary>
            public const int DefaultRsaKeySize = 2048;

            /// <summary>Default ECC curve size</summary>
            public const int DefaultEccCurveSize = 384;

            /// <summary>Minimum RSA key size for production</summary>
            public const int MinimumRsaKeySize = 2048;

            /// <summary>Maximum certificate chain depth</summary>
            public const int MaxCertificateChainDepth = 5;
        }

        /// <summary>
        /// VM execution limits
        /// </summary>
        public static class VMExecution
        {
            /// <summary>Default processor stack size</summary>
            public const int DefaultStackSize = 131072;

            /// <summary>Default call depth limit</summary>
            public const int DefaultCallDepth = 100;

            /// <summary>Maximum coroutine resumes</summary>
            public const int MaxCoroutineResumes = 500;

            /// <summary>Resource check interval (instruction count)</summary>
            public const int ResourceCheckInterval = 1000;

            /// <summary>Default instruction limit</summary>
            public const long DefaultInstructionLimit = 100_000_000_000L; // 100 billion

            /// <summary>Default rate limit for operations</summary>
            public const int DefaultRateLimit = 1000;
        }

        /// <summary>
        /// Lua-specific constants
        /// </summary>
        public static class Lua
        {
            /// <summary>Maximum captures in pattern matching</summary>
            public const int MaxCaptures = 32;

            /// <summary>Maximum string escape value</summary>
            public const int MaxStringEscape = 255;

            /// <summary>Default string buffer size</summary>
            public const int DefaultStringBufferSize = 1024;
        }

        /// <summary>
        /// File system security constants
        /// </summary>
        public static class FileSystem
        {
            /// <summary>Maximum path length</summary>
            public const int MaxPathLength = 260;

            /// <summary>Maximum file size for reading (10MB)</summary>
            public const int MaxFileSize = 10 * Memory.OneMB;

            /// <summary>Maximum directory search depth</summary>
            public const int MaxDirectorySearchDepth = 10;
        }

        /// <summary>
        /// Message bus constants
        /// </summary>
        public static class MessageBus
        {
            /// <summary>Default messages per minute rate limit</summary>
            public const int DefaultMessagesPerMinute = 60;

            /// <summary>Maximum message size in bytes</summary>
            public const int MaxMessageSize = Memory.OneKB * 64; // 64KB

            /// <summary>Message handler timeout in seconds</summary>
            public const int MessageHandlerTimeoutSeconds = 30;

            /// <summary>Cleanup timer interval in minutes</summary>
            public const int CleanupIntervalMinutes = 1;
        }

        /// <summary>
        /// Plugin-specific constants
        /// </summary>
        public static class Plugins
        {
            /// <summary>User plugin timeout in seconds</summary>
            public const int UserPluginTimeoutSeconds = 30;

            /// <summary>Partner plugin timeout in minutes</summary>
            public const int PartnerPluginTimeoutMinutes = 5;

            /// <summary>User plugin memory limit in MB</summary>
            public const int UserPluginMemoryLimitMB = 5;

            /// <summary>Partner plugin memory limit in MB</summary>
            public const int PartnerPluginMemoryLimitMB = 50;

            /// <summary>User plugin instruction limit</summary>
            public const long UserPluginInstructionLimit = 50_000L;

            /// <summary>Plugin update interval in milliseconds</summary>
            public const int PluginUpdateInterval = 1000;
        }

        /// <summary>
        /// Game-specific constants for WotCI (Wrath of the Continuous Integration) demo
        /// </summary>
        public static class WotCIGame
        {
            /// <summary>Maximum player health</summary>
            public const int MaxHealth = 100;

            /// <summary>Starting health</summary>
            public const int StartingHealth = 50;

            /// <summary>Maximum gold amount</summary>
            public const int MaxGold = 1000;

            /// <summary>Starting gold amount</summary>
            public const int StartingGold = 100;

            /// <summary>Inventory slot count</summary>
            public const int InventorySlots = 10;

            /// <summary>Combat damage range minimum</summary>
            public const int MinDamage = 5;

            /// <summary>Combat damage range maximum</summary>
            public const int MaxDamage = 20;

            /// <summary>Shop item cost range minimum</summary>
            public const int MinItemCost = 10;

            /// <summary>Shop item cost range maximum</summary>
            public const int MaxItemCost = 50;
        }

        /// <summary>
        /// Security dashboard constants
        /// </summary>
        public static class Dashboard
        {
            /// <summary>Dashboard update interval in milliseconds</summary>
            public const int UpdateIntervalMs = 500;

            /// <summary>Maximum recent events to display</summary>
            public const int MaxRecentEvents = 10;

            /// <summary>Maximum events to fetch from auditor</summary>
            public const int MaxEventsFetch = 50;

            /// <summary>Plugin name truncation length</summary>
            public const int PluginNameMaxLength = 15;

            /// <summary>Event description truncation length</summary>
            public const int EventDescriptionMaxLength = 40;
        }

        /// <summary>
        /// Rate limiting thresholds
        /// </summary>
        public static class RateLimits
        {
            /// <summary>Default operations per minute</summary>
            public const int DefaultOperationsPerMinute = 100;

            /// <summary>File operations per minute</summary>
            public const int FileOperationsPerMinute = 30;

            /// <summary>Network operations per minute</summary>
            public const int NetworkOperationsPerMinute = 20;

            /// <summary>Logging operations per minute</summary>
            public const int LoggingOperationsPerMinute = 200;
        }
    }
}
