using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Traces script execution to auto-generate minimal manifests
    /// NOTE: This tracer was designed for V1.0 manifests. V2.0 manifest tracing should use a different approach.
    /// </summary>
    public class ManifestTracer : ISecurityEventHandler
    {
        private readonly Script _script;
        private readonly TraceData _traceData = new TraceData();
        private bool _isTracing;

        /// <summary>
        /// Creates a new manifest tracer for a script
        /// </summary>
        public ManifestTracer(Script script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
        }

        /// <summary>
        /// Enables tracing on the script
        /// </summary>
        public void EnableTracing()
        {
            if (_isTracing)
                return;

            _isTracing = true;

            // Hook into script security logger
            var logger = _script.GetSecurityLogger();
            if (logger is SecurityLogger securityLogger)
            {
                securityLogger.AddHandler(this);
            }
        }

        /// <summary>
        /// Disables tracing
        /// </summary>
        public void DisableTracing()
        {
            if (!_isTracing)
                return;

            _isTracing = false;

            // Unhook from security logger
            var logger = _script.GetSecurityLogger();
            if (logger is SecurityLogger securityLogger)
            {
                securityLogger.RemoveHandler(this);
            }
        }

        /// <summary>
        /// Generates a V2.0 manifest based on observed behaviour
        /// </summary>
        public Manifest GenerateManifest()
        {
            // Create V2.0 manifest using the new format
            var manifestId = $"traced-manifest-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            // For now, create a minimal V2.0 manifest structure
            // TODO: This should be enhanced to properly use V2.0 signed content blocks
            // when the V2.0 manifest system is fully implemented
            return V2ManifestBuilder.CreateUnsigned(manifestId, "traced-package");
        }

        /// <summary>
        /// Saves the generated manifest to a file
        /// </summary>
        public void SaveManifest(string path)
        {
            var manifest = GenerateManifest();
            var json = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }
            );
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Gets trace statistics
        /// </summary>
        public TraceStatistics GetStatistics()
        {
            return new TraceStatistics
            {
                ExecutionTimeMs = _traceData.MaxExecutionTime,
                MemoryUsedBytes = _traceData.MaxMemoryUsed,
                InstructionCount = _traceData.MaxInstructionCount,
                FilesAccessed = _traceData.FileAccess.Count,
                DirectoriesAccessed = _traceData.DirectoryAccess.Count,
                ModulesUsed = _traceData.UsedModules.Count,
                NetworkHostsAccessed = _traceData.NetworkHosts.Count,
                EnvironmentVariablesRead = _traceData.EnvironmentVariables.Count,
            };
        }

        // Event handlers

        /// <summary>
        /// Handles security events for manifest tracing
        /// </summary>
        public void HandleSecurityEvent(SecurityEvent evt)
        {
            if (evt == null || !_isTracing)
                return;

            // Track security events to understand what was attempted
            if (evt.Type == SecurityEventType.FileAccessDenied)
            {
                _traceData.DeniedFileAccess.Add(evt.Operation ?? "Unknown");
            }
            // Add more event type handling as needed
        }

        private void OnSecurityEvent(object sender, SecurityEventArgs e)
        {
            // Track security events to understand what was attempted
            if (e.EventType == SecurityEventType.FileAccessDenied)
            {
                _traceData.DeniedFileAccess.Add(e.Details);
            }
        }

        private void OnResourceAccess(object sender, ResourceAccessEventArgs e)
        {
            _traceData.MaxExecutionTime = Math.Max(_traceData.MaxExecutionTime, e.ExecutionTimeMs);
            _traceData.MaxMemoryUsed = Math.Max(_traceData.MaxMemoryUsed, e.MemoryUsedBytes);
            _traceData.MaxInstructionCount = Math.Max(
                _traceData.MaxInstructionCount,
                e.InstructionCount
            );
        }

        private void OnFileAccess(object sender, FileAccessEventArgs e)
        {
            if (e.IsDirectory)
            {
                _traceData.DirectoryAccess[e.Path] = e.AccessType;
            }
            else
            {
                _traceData.FileAccess[e.Path] = e.AccessType;
            }
        }

        private void OnNetworkAccess(object sender, NetworkAccessEventArgs e)
        {
            _traceData.NetworkHosts.Add(e.Host);
        }

        private void OnEnvironmentAccess(object sender, EnvironmentAccessEventArgs e)
        {
            _traceData.EnvironmentVariables.Add(e.VariableName);
        }

        /// <summary>
        /// Legacy method for V1.0 compatibility - file access rules are now handled in V2.0 format
        /// </summary>
        [Obsolete(
            "V1.0 manifest builder methods are not compatible with V2.0 signed content format"
        )]
        private void AddFileAccessRules(object builder)
        {
            // File access rules are now handled through V2.0 signed content blocks
            // This method is kept for legacy compatibility but does nothing
        }

        /// <summary>
        /// Analyzes file access to create patterns
        /// </summary>
        private Dictionary<string, FileAccessType> AnalyzeFilePatterns()
        {
            var patterns = new Dictionary<string, FileAccessType>();

            // Group files by directory and extension
            var groups = _traceData.FileAccess.GroupBy(f => Path.GetExtension(f.Key)).ToList();

            foreach (var group in groups)
            {
                var extension = group.Key;
                var accessType = group.Select(g => g.Value).Aggregate((a, b) => a | b);

                if (!string.IsNullOrEmpty(extension))
                {
                    patterns[$"*{extension}"] = accessType;
                }
            }

            // Add specific files that don't fit patterns
            foreach (var file in _traceData.FileAccess)
            {
                var extension = Path.GetExtension(file.Key);
                if (string.IsNullOrEmpty(extension))
                {
                    patterns[file.Key] = file.Value;
                }
            }

            return patterns;
        }

        /// <summary>
        /// Analyzes directory access to create patterns
        /// </summary>
        private Dictionary<string, FileAccessType> AnalyzeDirectoryPatterns()
        {
            var patterns = new Dictionary<string, FileAccessType>();

            // Find common directory roots
            var roots = new Dictionary<string, FileAccessType>();

            foreach (var dir in _traceData.DirectoryAccess)
            {
                var parts = dir.Key.Split('/', '\\');

                // Create patterns for each directory level
                for (var i = 1; i <= parts.Length; i++)
                {
                    var pattern = string.Join("/", parts.Take(i));
                    if (!roots.ContainsKey(pattern))
                    {
                        roots[pattern] = dir.Value;
                    }
                    else
                    {
                        roots[pattern] |= dir.Value;
                    }
                }
            }

            // Convert to wildcard patterns
            foreach (var root in roots)
            {
                patterns[root.Key + "/**"] = root.Value;
            }

            return patterns;
        }

        /// <summary>
        /// Internal trace data storage
        /// </summary>
        private class TraceData
        {
            public long MaxExecutionTime { get; set; }
            public long MaxMemoryUsed { get; set; }
            public long MaxInstructionCount { get; set; }
            public Dictionary<string, FileAccessType> FileAccess { get; } =
                new Dictionary<string, FileAccessType>();
            public Dictionary<string, FileAccessType> DirectoryAccess { get; } =
                new Dictionary<string, FileAccessType>();
            public HashSet<string> UsedModules { get; } = new HashSet<string>();
            public HashSet<string> UsedCapabilities { get; } = new HashSet<string>();
            public HashSet<string> NetworkHosts { get; } = new HashSet<string>();
            public HashSet<string> EnvironmentVariables { get; } = new HashSet<string>();
            public HashSet<string> DeniedFileAccess { get; } = new HashSet<string>();
        }
    }

    /// <summary>
    /// Statistics from manifest tracing
    /// </summary>
    public class TraceStatistics
    {
        public long ExecutionTimeMs { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long InstructionCount { get; set; }
        public int FilesAccessed { get; set; }
        public int DirectoriesAccessed { get; set; }
        public int ModulesUsed { get; set; }
        public int NetworkHostsAccessed { get; set; }
        public int EnvironmentVariablesRead { get; set; }
    }

    /// <summary>
    /// Event args for resource access events
    /// </summary>
    public class ResourceAccessEventArgs : EventArgs
    {
        public long ExecutionTimeMs { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long InstructionCount { get; set; }
    }

    /// <summary>
    /// Event args for file access events
    /// </summary>
    public class FileAccessEventArgs : EventArgs
    {
        public string Path { get; set; }
        public FileAccessType AccessType { get; set; }
        public bool IsDirectory { get; set; }
    }

    /// <summary>
    /// Event args for network access events
    /// </summary>
    public class NetworkAccessEventArgs : EventArgs
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Operation { get; set; }
    }

    /// <summary>
    /// Event args for environment access events
    /// </summary>
    public class EnvironmentAccessEventArgs : EventArgs
    {
        public string VariableName { get; set; }
        public bool IsRead { get; set; }
    }

    /// <summary>
    /// File access types for tracing
    /// </summary>
    [Flags]
    public enum FileAccessType
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        Delete = 8,
    }
}
