using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Interface for security logging
    /// </summary>
    public interface ISecurityLogger
    {
        /// <summary>
        /// Logs a security event
        /// </summary>
        void LogSecurityEvent(SecurityEvent evt);

        /// <summary>
        /// Logs a security violation
        /// </summary>
        void LogViolation(string operation, object args, string reason = null);

        /// <summary>
        /// Logs file access
        /// </summary>
        void LogFileAccess(string filename, string mode, FileAccess access);

        /// <summary>
        /// Logs file operation
        /// </summary>
        void LogFileOperation(string operation, params string[] files);

        /// <summary>
        /// Logs resource usage
        /// </summary>
        void LogResourceUsage(string resource, long current, long limit);
    }

    /// <summary>
    /// Default security logger implementation
    /// </summary>
    public class SecurityLogger : ISecurityLogger
    {
        private readonly List<ISecurityEventHandler> _handlers = new();
        private readonly object _lock = new object();

        /// <summary>
        /// Adds an event handler
        /// </summary>
        public void AddHandler(ISecurityEventHandler handler)
        {
            lock (_lock)
            {
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// Removes an event handler
        /// </summary>
        public void RemoveHandler(ISecurityEventHandler handler)
        {
            lock (_lock)
            {
                _handlers.Remove(handler);
            }
        }

        /// <summary>
        /// Logs a security event
        /// </summary>
        public void LogSecurityEvent(SecurityEvent evt)
        {
            if (evt == null) return;

            lock (_lock)
            {
                foreach (var handler in _handlers)
                {
                    try
                    {
                        handler.HandleSecurityEvent(evt);
                    }
                    catch (Exception ex)
                    {
                        // Don't let handler exceptions break security logging
                        System.Diagnostics.Debug.WriteLine($"Security handler error: {ex}");
                    }
                }
            }

            // Also log to debug output in debug builds
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[SECURITY] {evt.Type}: {evt.Operation} - {string.Join(", ", evt.Arguments ?? Array.Empty<object>())}");
#endif
        }

        /// <summary>
        /// Logs a security violation
        /// </summary>
        public void LogViolation(string operation, object args, string reason = null)
        {
            var evt = new SecurityEvent
            {
                Type = SecurityEventType.AccessDenied,
                Operation = operation,
                Arguments = args is object[] arr ? arr : new[] { args },
                StackTrace = Environment.StackTrace
            };

            if (!string.IsNullOrEmpty(reason))
            {
                evt.Metadata["Reason"] = reason;
            }

            LogSecurityEvent(evt);
        }

        /// <summary>
        /// Logs file access
        /// </summary>
        public void LogFileAccess(string filename, string mode, FileAccess access)
        {
            var evt = new SecurityEvent
            {
                Type = SecurityEventType.FileAccessViolation,
                Operation = "FileAccess",
                Arguments = new object[] { filename, mode, access },
                Metadata = new Dictionary<string, object>
                {
                    ["FileName"] = filename,
                    ["Mode"] = mode,
                    ["Access"] = access.ToString()
                }
            };

            LogSecurityEvent(evt);
        }

        /// <summary>
        /// Logs file operation
        /// </summary>
        public void LogFileOperation(string operation, params string[] files)
        {
            var evt = new SecurityEvent
            {
                Type = SecurityEventType.FileAccessViolation,
                Operation = $"File{operation}",
                Arguments = files,
                Metadata = new Dictionary<string, object>
                {
                    ["Operation"] = operation,
                    ["FileCount"] = files.Length
                }
            };

            LogSecurityEvent(evt);
        }

        /// <summary>
        /// Logs resource usage
        /// </summary>
        public void LogResourceUsage(string resource, long current, long limit)
        {
            var evt = new SecurityEvent
            {
                Type = current > limit ? SecurityEventType.ResourceLimitExceeded : SecurityEventType.PolicyViolation,
                Operation = "ResourceUsage",
                Arguments = new object[] { resource, current, limit },
                Metadata = new Dictionary<string, object>
                {
                    ["Resource"] = resource,
                    ["Current"] = current,
                    ["Limit"] = limit,
                    ["Percentage"] = limit > 0 ? (current * 100.0 / limit) : 0
                }
            };

            LogSecurityEvent(evt);
        }
    }

    /// <summary>
    /// File-based security tracer that logs security events to JSON files
    /// Supports multiple environment variables for configuration:
    /// - SOLARSHARP_SECURITY_TRACE: Original trace directory
    /// - LUA_SANDBOX_LOG_DIR: Alternative log directory
    /// - LUA_SANDBOX_LEARN_MODE: Enable learning mode (logs failures but permits them)
    /// </summary>
    public class FileSecurityTracer : ISecurityEventHandler
    {
        private readonly string _traceDirectory;
        private readonly object _lock = new object();
        private static FileSecurityTracer _instance;
        private static readonly object _instanceLock = new object();
        
        /// <summary>
        /// Whether learning mode is enabled (logs failures but permits them)
        /// </summary>
        public bool LearningMode { get; private set; }

        /// <summary>
        /// Creates a file security tracer for the specified directory
        /// </summary>
        public FileSecurityTracer(string traceDirectory, bool learningMode = false)
        {
            _traceDirectory = traceDirectory ?? throw new ArgumentNullException(nameof(traceDirectory));
            LearningMode = learningMode;
            
            try
            {
                Directory.CreateDirectory(_traceDirectory);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Cannot create trace directory '{_traceDirectory}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets or creates the global file security tracer based on environment variables
        /// Returns null if no environment variables are set
        /// </summary>
        public static FileSecurityTracer GetGlobalTracer()
        {
            if (_instance != null) return _instance;

            lock (_instanceLock)
            {
                if (_instance != null) return _instance;

                // Check multiple environment variables
                var traceDir = Environment.GetEnvironmentVariable("SOLARSHARP_SECURITY_TRACE") 
                            ?? Environment.GetEnvironmentVariable("LUA_SANDBOX_LOG_DIR");
                
                if (string.IsNullOrEmpty(traceDir))
                    return null;

                // Check if learning mode is enabled
                var learnModeStr = Environment.GetEnvironmentVariable("LUA_SANDBOX_LEARN_MODE");
                var learningMode = !string.IsNullOrEmpty(learnModeStr) && 
                                 (learnModeStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                  learnModeStr.Equals("1", StringComparison.OrdinalIgnoreCase));

                try
                {
                    _instance = new FileSecurityTracer(traceDir, learningMode);
                    return _instance;
                }
                catch (Exception ex)
                {
                    // Log error but don't fail - tracing is optional
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize security tracer: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Handles a security event by logging it to a JSON file
        /// In learning mode, security violations are logged but the event is marked as permitted
        /// </summary>
        public void HandleSecurityEvent(SecurityEvent evt)
        {
            if (evt == null) return;

            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.UtcNow;
                    var filename = $"security-trace-{timestamp:yyyy-MM-dd}.jsonl";
                    var filepath = Path.Combine(_traceDirectory, filename);

                    // In learning mode, log the original violation but mark it as permitted for learning
                    var wasViolation = evt.Type == SecurityEventType.AccessDenied || evt.Type == SecurityEventType.PolicyViolation;
                    var permitInLearningMode = LearningMode && wasViolation;

                    var logEntry = new
                    {
                        Timestamp = timestamp,
                        Type = evt.Type.ToString(),
                        Operation = evt.Operation,
                        Arguments = evt.Arguments,
                        Metadata = evt.Metadata,
                        StackTrace = evt.StackTrace,
                        ScriptId = evt.ScriptId,
                        ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                        ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                        LearningMode = LearningMode,
                        PermittedInLearningMode = permitInLearningMode,
                        ViolationHandling = evt.ViolationHandling.ToString(),
                        TerminateExecution = evt.TerminateExecution
                    };

                    var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions 
                    { 
                        WriteIndented = false,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });

                    // Use JSONL format (JSON Lines) - one JSON object per line
                    File.AppendAllText(filepath, json + Environment.NewLine, Encoding.UTF8);

                    // If in learning mode and this was a violation, change the event to permit the operation
                    if (permitInLearningMode)
                    {
                        evt.Type = SecurityEventType.OperationSuccess;
                        evt.Metadata["OriginalViolation"] = "true";
                        evt.Metadata["LearningModePermitted"] = "true";
                        evt.TerminateExecution = false; // Don't terminate in learning mode
                        evt.ViolationHandling = SecurityViolationHandling.Allow; // Allow operation in learning mode
                    }
                    else if (wasViolation)
                    {
                        // Set appropriate violation handling for different types of operations
                        if (evt.Operation?.Contains("Module") == true || evt.Operation?.Contains("Function") == true)
                        {
                            evt.ViolationHandling = SecurityViolationHandling.ReturnNil; // Make functions appear as nil
                        }
                        else if (evt.Operation?.Contains("File") == true || evt.Operation?.Contains("IO") == true)
                        {
                            evt.ViolationHandling = SecurityViolationHandling.ThrowError; // Throw Lua errors for file ops
                        }
                        else
                        {
                            evt.ViolationHandling = SecurityViolationHandling.Deny; // Default deny behavior
                        }
                        evt.TerminateExecution = false; // Don't terminate, handle gracefully
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't let tracing errors break security functionality
                System.Diagnostics.Debug.WriteLine($"Security tracing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an operation should be permitted in learning mode
        /// </summary>
        public bool ShouldPermitInLearningMode(SecurityEventType eventType)
        {
            return LearningMode && (eventType == SecurityEventType.AccessDenied || eventType == SecurityEventType.PolicyViolation);
        }

        /// <summary>
        /// Determines the appropriate violation handling strategy for an operation
        /// </summary>
        public static SecurityViolationHandling GetViolationHandling(string operation, bool learningMode)
        {
            if (learningMode)
                return SecurityViolationHandling.Allow;

            if (operation?.Contains("Module") == true || operation?.Contains("Function") == true)
                return SecurityViolationHandling.ReturnNil; // Make functions appear as nil
            
            if (operation?.Contains("File") == true || operation?.Contains("IO") == true)
                return SecurityViolationHandling.ThrowError; // Throw Lua errors for file ops
            
            return SecurityViolationHandling.Deny; // Default deny behavior
        }

        /// <summary>
        /// Analyzes trace files and generates a manifest suggestion
        /// </summary>
        public ManifestSuggestion AnalyzeTraces(DateTime? fromDate = null)
        {
            var suggestion = new ManifestSuggestion();
            var from = fromDate ?? DateTime.UtcNow.AddDays(-1);

            try
            {
                var traceFiles = Directory.GetFiles(_traceDirectory, "security-trace-*.jsonl")
                    .Where(f => 
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(f), @"security-trace-(\d{4}-\d{2}-\d{2})\.jsonl");
                        if (!match.Success) return false;
                        return DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date) && date >= from.Date;
                    });

                foreach (var file in traceFiles)
                {
                    ProcessTraceFile(file, suggestion);
                }
            }
            catch (Exception ex)
            {
                suggestion.Errors.Add($"Error analyzing traces: {ex.Message}");
            }

            return suggestion;
        }

        private void ProcessTraceFile(string filepath, ManifestSuggestion suggestion)
        {
            foreach (var line in File.ReadAllLines(filepath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    var type = root.GetProperty("Type").GetString();
                    var operation = root.GetProperty("Operation").GetString();

                    // Track denied operations to suggest permissions
                    if (type == "AccessDenied" || type == "PolicyViolation")
                    {
                        suggestion.DeniedOperations.Add($"{operation}: {GetArgumentsSummary(root)}");
                        
                        // Suggest specific permissions based on operation
                        if (operation?.Contains("FileRead") == true)
                        {
                            suggestion.SuggestedFilePermissions.Add("FileRead");
                        }
                        else if (operation?.Contains("FileWrite") == true)
                        {
                            suggestion.SuggestedFilePermissions.Add("FileWrite");
                        }
                        else if (operation?.Contains("Module") == true)
                        {
                            var args = GetArgumentsSummary(root);
                            suggestion.SuggestedModules.Add(args);
                        }
                    }

                    // Track successful operations to understand usage patterns
                    if (type == "OperationSuccess")
                    {
                        suggestion.SuccessfulOperations.Add($"{operation}: {GetArgumentsSummary(root)}");
                    }
                }
                catch (Exception ex)
                {
                    suggestion.Errors.Add($"Error parsing trace line: {ex.Message}");
                }
            }
        }

        private string GetArgumentsSummary(JsonElement root)
        {
            if (root.TryGetProperty("Arguments", out var args) && args.ValueKind == JsonValueKind.Array)
            {
                var argList = new List<string>();
                foreach (var arg in args.EnumerateArray())
                {
                    argList.Add(arg.ToString());
                }
                return string.Join(", ", argList);
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Manifest suggestion based on security trace analysis
    /// </summary>
    public class ManifestSuggestion
    {
        public List<string> DeniedOperations { get; } = new List<string>();
        public List<string> SuccessfulOperations { get; } = new List<string>();
        public HashSet<string> SuggestedFilePermissions { get; } = new HashSet<string>();
        public HashSet<string> SuggestedModules { get; } = new HashSet<string>();
        public List<string> Errors { get; } = new List<string>();

        public void WriteReport(string outputPath)
        {
            var report = new StringBuilder();
            report.AppendLine("# Security Trace Analysis Report");
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine();

            if (DeniedOperations.Any())
            {
                report.AppendLine("## Denied Operations (Security Violations)");
                foreach (var op in DeniedOperations.Take(20)) // Limit output
                {
                    report.AppendLine($"- {op}");
                }
                if (DeniedOperations.Count > 20)
                {
                    report.AppendLine($"... and {DeniedOperations.Count - 20} more");
                }
                report.AppendLine();
            }

            if (SuggestedFilePermissions.Any())
            {
                report.AppendLine("## Suggested File Permissions");
                foreach (var perm in SuggestedFilePermissions)
                {
                    report.AppendLine($"- {perm}");
                }
                report.AppendLine();
            }

            if (SuggestedModules.Any())
            {
                report.AppendLine("## Suggested Modules");
                foreach (var module in SuggestedModules)
                {
                    report.AppendLine($"- {module}");
                }
                report.AppendLine();
            }

            if (SuccessfulOperations.Any())
            {
                report.AppendLine("## Successful Operations (Sample)");
                foreach (var op in SuccessfulOperations.Take(10))
                {
                    report.AppendLine($"- {op}");
                }
                report.AppendLine();
            }

            if (Errors.Any())
            {
                report.AppendLine("## Errors");
                foreach (var error in Errors)
                {
                    report.AppendLine($"- {error}");
                }
            }

            File.WriteAllText(outputPath, report.ToString());
        }
    }
}