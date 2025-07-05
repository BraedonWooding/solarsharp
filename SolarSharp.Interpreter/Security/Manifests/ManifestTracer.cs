using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Traces script execution to auto-generate minimal manifests
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
        /// Generates a manifest based on observed behavior
        /// </summary>
        public Manifest GenerateManifest()
        {
            var builder = new ManifestBuilder()
                .WithVersion("1.0")
                .WithDescription($"Auto-generated manifest from trace on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
                .WithType("traced");

            // Add resource limits based on observed usage
            if (_traceData.MaxExecutionTime > 0)
            {
                // Add 20% buffer
                var timeout = (int)(_traceData.MaxExecutionTime * 1.2 / 1000);
                builder.WithTimeoutSeconds(Math.Max(timeout, 5)); // Minimum 5 seconds
            }

            if (_traceData.MaxMemoryUsed > 0)
            {
                // Add 50% buffer for memory
                var memoryMB = (int)(_traceData.MaxMemoryUsed * 1.5 / (1024 * 1024));
                builder.WithMemoryLimitMB(Math.Max(memoryMB, 10)); // Minimum 10MB
            }

            if (_traceData.MaxInstructionCount > 0)
            {
                // Add 50% buffer for instructions
                var instructions = (long)(_traceData.MaxInstructionCount * 1.5);
                builder.WithMaxInstructions(instructions);
            }

            // Add modules that were actually used
            if (_traceData.UsedModules.Any())
            {
                foreach (var module in _traceData.UsedModules)
                {
                    if (Enum.TryParse<CoreModules>(module, out var coreModule))
                    {
                        builder.AllowModule(coreModule);
                    }
                }
            }

            // Add file access rules based on observed patterns
            AddFileAccessRules(builder);

            // Add network rules if needed
            if (_traceData.NetworkHosts.Any())
            {
                builder.AllowNetworkAccess()
                       .WithAllowedHosts(_traceData.NetworkHosts.ToArray());
            }

            // Add environment rules if needed
            if (_traceData.EnvironmentVariables.Any())
            {
                builder.AllowEnvironmentAccess()
                       .WithAllowedEnvironmentVariables(_traceData.EnvironmentVariables.ToArray());
            }

            // Add capabilities based on observed usage
            if (_traceData.UsedCapabilities.Any())
            {
                foreach (var cap in _traceData.UsedCapabilities)
                {
                    if (Enum.TryParse<ScriptCapabilities>(cap, out var capability))
                    {
                        builder.AllowCapability(capability);
                    }
                }
            }

            return builder.Build();
        }

        /// <summary>
        /// Saves the generated manifest to a file
        /// </summary>
        public void SaveManifest(string path)
        {
            var manifest = GenerateManifest();
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            System.IO.File.WriteAllText(path, json);
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
                EnvironmentVariablesRead = _traceData.EnvironmentVariables.Count
            };
        }

        // Event handlers

        /// <summary>
        /// Handles security events for manifest tracing
        /// </summary>
        public void HandleSecurityEvent(SecurityEvent evt)
        {
            if (evt == null || !_isTracing) return;

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
            _traceData.MaxInstructionCount = Math.Max(_traceData.MaxInstructionCount, e.InstructionCount);
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
        /// Adds file access rules based on observed patterns
        /// </summary>
        private void AddFileAccessRules(ManifestBuilder builder)
        {
            // Analyze file access patterns
            var patterns = AnalyzeFilePatterns();

            foreach (var pattern in patterns)
            {
                if (pattern.Value.HasFlag(FileAccessType.Write))
                {
                    builder.AddFileRule(pattern.Key, FilePermissions.ReadWrite);
                }
                else if (pattern.Value.HasFlag(FileAccessType.Read))
                {
                    builder.AddFileRule(pattern.Key, FilePermissions.Read);
                }
            }

            // Analyze directory patterns
            var dirPatterns = AnalyzeDirectoryPatterns();

            foreach (var pattern in dirPatterns)
            {
                if (pattern.Value.HasFlag(FileAccessType.Write))
                {
                    builder.AddDirectoryRule(pattern.Key, DirectoryPermissions.ListAndCreateFiles);
                }
                else if (pattern.Value.HasFlag(FileAccessType.Read))
                {
                    builder.AddDirectoryRule(pattern.Key, DirectoryPermissions.List);
                }
            }
        }

        /// <summary>
        /// Analyzes file access to create patterns
        /// </summary>
        private Dictionary<string, FileAccessType> AnalyzeFilePatterns()
        {
            var patterns = new Dictionary<string, FileAccessType>();

            // Group files by directory and extension
            var groups = _traceData.FileAccess
                .GroupBy(f => System.IO.Path.GetExtension(f.Key))
                .ToList();

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
                var extension = System.IO.Path.GetExtension(file.Key);
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
                for (int i = 1; i <= parts.Length; i++)
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
            public Dictionary<string, FileAccessType> FileAccess { get; } = new();
            public Dictionary<string, FileAccessType> DirectoryAccess { get; } = new();
            public HashSet<string> UsedModules { get; } = new();
            public HashSet<string> UsedCapabilities { get; } = new();
            public HashSet<string> NetworkHosts { get; } = new();
            public HashSet<string> EnvironmentVariables { get; } = new();
            public HashSet<string> DeniedFileAccess { get; } = new();
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
        Delete = 8
    }
}