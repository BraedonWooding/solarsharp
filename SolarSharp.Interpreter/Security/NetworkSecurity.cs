using System;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines network access security policies
    /// </summary>
    public class NetworkSecurity
    {
        /// <summary>
        /// Whether any network access is allowed
        /// </summary>
        public bool AllowAccess { get; set; } = false;

        /// <summary>
        /// Allowed network operations
        /// </summary>
        public NetworkOperations AllowedOperations { get; set; } = NetworkOperations.None;

        /// <summary>
        /// Allowed hosts/domains (supports wildcards)
        /// </summary>
        public List<string> AllowedHosts { get; set; } = new();

        /// <summary>
        /// Allowed ports (empty = all ports allowed for allowed hosts)
        /// </summary>
        public List<int> AllowedPorts { get; set; } = new();

        /// <summary>
        /// Maximum request timeout
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum response size in bytes
        /// </summary>
        public long MaxResponseSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Creates a configuration with no network access
        /// </summary>
        public static NetworkSecurity NoAccess() => new() { AllowAccess = false };

        /// <summary>
        /// Creates a configuration with HTTP-only access
        /// </summary>
        public static NetworkSecurity HttpOnly() => new()
        {
            AllowAccess = true,
            AllowedOperations = NetworkOperations.HttpGet | NetworkOperations.HttpPost,
            AllowedPorts = new List<int> { 80, 443 }
        };

        /// <summary>
        /// Creates a configuration with full network access (use with caution)
        /// </summary>
        public static NetworkSecurity FullAccess() => new()
        {
            AllowAccess = true,
            AllowedOperations = NetworkOperations.All,
            RequestTimeout = TimeSpan.FromMinutes(5),
            MaxResponseSize = 100 * 1024 * 1024 // 100MB
        };
    }

    /// <summary>
    /// Network operations that can be allowed or denied
    /// </summary>
    [Flags]
    public enum NetworkOperations
    {
        /// <summary>
        /// No network operations allowed
        /// </summary>
        None = 0,

        /// <summary>
        /// HTTP GET requests
        /// </summary>
        HttpGet = 1 << 0,

        /// <summary>
        /// HTTP POST requests
        /// </summary>
        HttpPost = 1 << 1,

        /// <summary>
        /// HTTP PUT requests
        /// </summary>
        HttpPut = 1 << 2,

        /// <summary>
        /// HTTP DELETE requests
        /// </summary>
        HttpDelete = 1 << 3,

        /// <summary>
        /// TCP socket connections
        /// </summary>
        TcpConnect = 1 << 4,

        /// <summary>
        /// UDP packet sending
        /// </summary>
        UdpSend = 1 << 5,

        /// <summary>
        /// All network operations
        /// </summary>
        All = HttpGet | HttpPost | HttpPut | HttpDelete | TcpConnect | UdpSend
    }
}