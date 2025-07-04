using SolarSharp.Interpreter.Interop;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines .NET interop security policies
    /// </summary>
    public class InteropSecurity
    {
        /// <summary>
        /// Default interop access mode for user data
        /// </summary>
        public InteropAccessMode DefaultAccessMode { get; set; } = InteropAccessMode.HideMembers;

        /// <summary>
        /// Whether to allow registration of new types
        /// </summary>
        public bool AllowTypeRegistration { get; set; } = false;

        /// <summary>
        /// Whether to allow access to static members
        /// </summary>
        public bool AllowStaticAccess { get; set; } = false;

        /// <summary>
        /// Whether to allow access to private/protected members
        /// </summary>
        public bool AllowPrivateAccess { get; set; } = false;

        /// <summary>
        /// Whether to allow delegate creation
        /// </summary>
        public bool AllowDelegates { get; set; } = false;

        /// <summary>
        /// Creates a configuration with no interop access
        /// </summary>
        public static InteropSecurity NoAccess() => new()
        {
            DefaultAccessMode = InteropAccessMode.HideMembers,
            AllowTypeRegistration = false,
            AllowStaticAccess = false,
            AllowPrivateAccess = false,
            AllowDelegates = false
        };

        /// <summary>
        /// Creates a configuration allowing only safe types
        /// </summary>
        public static InteropSecurity SafeTypesOnly() => new()
        {
            DefaultAccessMode = InteropAccessMode.Reflection,
            AllowTypeRegistration = false,
            AllowStaticAccess = false,
            AllowPrivateAccess = false,
            AllowDelegates = true
        };

        /// <summary>
        /// Creates a configuration with full interop access (use with caution)
        /// </summary>
        public static InteropSecurity FullAccess() => new()
        {
            DefaultAccessMode = InteropAccessMode.Reflection,
            AllowTypeRegistration = true,
            AllowStaticAccess = true,
            AllowPrivateAccess = true,
            AllowDelegates = true
        };
    }
}