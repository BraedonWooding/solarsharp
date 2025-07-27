using System;
using System.Linq;
using System.Linq.Expressions;
using SolarSharp.Interpreter.Interop;
using SolarSharp.Interpreter.Loaders;

namespace SolarSharp.Interpreter.Platforms
{
    /// <summary>
    /// A static class offering properties for autodetection of system/platform details
    /// </summary>
    public static class PlatformAutoDetector
    {
        private static bool? m_IsRunningOnAOT;

        private static bool m_AutoDetectionsDone;

        /// <summary>
        /// Gets a value indicating whether this instance is running on mono.
        /// </summary>
        public static bool IsRunningOnMono { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is running on a CLR4 compatible implementation
        /// </summary>
        public static bool IsRunningOnClr4 { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is running on Unity-3D
        /// </summary>
        public static bool IsRunningOnUnity { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance has been built as a Portable Class Library
        /// </summary>
        public static bool IsPortableFramework { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance has been compiled natively in Unity AND is using IL2CPP
        /// </summary>
        public static bool IsUnityIL2CPP { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is running a system using Ahead-Of-Time compilation
        /// and not supporting JIT.
        /// </summary>
        public static bool IsRunningOnAOT
        {
            // We do a lazy eval here, so we can wire out this code by not calling it, if necessary..
            get
            {
#if UNITY_WEBGL || UNITY_IOS || UNITY_TVOS || ENABLE_IL2CPP
                return true;
#else

                if (!m_IsRunningOnAOT.HasValue)
                {
                    try
                    {
                        Expression e = Expression.Constant(5, typeof(int));
                        var lambda = Expression.Lambda<Func<int>>(e);
                        lambda.Compile();
                        m_IsRunningOnAOT = false;
                    }
                    catch (Exception)
                    {
                        m_IsRunningOnAOT = true;
                    }
                }

                return m_IsRunningOnAOT.Value;
#endif
            }
        }

        private static void AutoDetectPlatformFlags()
        {
            if (m_AutoDetectionsDone)
                return;
#if PCL
            IsPortableFramework = true;
#endif
#if ENABLE_DOTNET
            IsRunningOnUnity = true;
            IsUnityNative = true;
#endif
#if UNITY_5 || UNITY_2017_1_OR_NEWER
            IsRunningOnUnity = true;
            IsUnityNative = true;
#endif
#if ENABLE_IL2CPP
            IsUnityIL2CPP = true;
#endif

#if !NETCOREAPP
            IsRunningOnMono = Type.GetType("Mono.Runtime") != null;
#endif

            m_AutoDetectionsDone = true;
        }

        internal static IPlatformAccessor GetDefaultPlatform()
        {
            AutoDetectPlatformFlags();

#if PCL || ENABLE_DOTNET
            return new LimitedPlatformAccessor();
#else
            if (IsRunningOnUnity)
                return new LimitedPlatformAccessor();

#if DOTNET_CORE
            return new DotNetCorePlatformAccessor();
#else
            return new StandardPlatformAccessor();
#endif
#endif
        }

        internal static IScriptLoader GetDefaultScriptLoader()
        {
            AutoDetectPlatformFlags();

            if (IsRunningOnUnity)
                return new UnityAssetsScriptLoader();
#if (DOTNET_CORE)
            return new FileSystemScriptLoader();
#elif (PCL || ENABLE_DOTNET || NETFX_CORE)
            return new InvalidScriptLoader("Portable Framework");
#else
            return new FileSystemScriptLoader();
#endif
        }
    }
}
