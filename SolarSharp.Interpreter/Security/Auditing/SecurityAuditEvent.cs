#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Security.Auditing
{
    /// <summary>
    /// Base class for all security audit events following functional programming patterns
    /// </summary>
    public abstract record SecurityAuditEvent
    {
        /// <summary>
        /// UTC timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Type of security event for categorization
        /// </summary>
        public SecurityEventType EventType { get; init; }

        /// <summary>
        /// Identifier of the script involved in the event
        /// </summary>
        public string ScriptId { get; init; } = string.Empty;

        /// <summary>
        /// Identity information of the script including name, version, and public key token
        /// </summary>
        public ScriptIdentity? ScriptIdentity { get; init; }

        /// <summary>
        /// Principal or identity information for the operation
        /// </summary>
        public string Principal { get; init; } = string.Empty;

        /// <summary>
        /// Details about the operation being performed
        /// </summary>
        public string Operation { get; init; } = string.Empty;

        /// <summary>
        /// Success or failure status of the operation
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Exception details if applicable
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Capability name involved in the operation (for capability-related events)
        /// </summary>
        public string? CapabilityName { get; init; }

        /// <summary>
        /// Parameters passed to the operation
        /// </summary>
        public ImmutableArray<object> Parameters { get; init; } = ImmutableArray<object>.Empty;

        /// <summary>
        /// Result of the operation (for successful operations)
        /// </summary>
        public object? Result { get; init; }

        /// <summary>
        /// Description of the event (legacy compatibility)
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Immutable metadata for additional context
        /// </summary>
        public ImmutableDictionary<string, object> Metadata { get; init; } =
            ImmutableDictionary<string, object>.Empty;

        /// <summary>
        /// Creates a new security audit event with the specified type
        /// </summary>
        protected SecurityAuditEvent(SecurityEventType eventType)
        {
            EventType = eventType;
        }

        /// <summary>
        /// Creates a copy of this event with additional metadata
        /// </summary>
        public T WithMetadata<T>(string key, object value)
            where T : SecurityAuditEvent
        {
            return (T)this with { Metadata = Metadata.Add(key, value) };
        }

        /// <summary>
        /// Creates a copy of this event with an error message
        /// </summary>
        public T WithError<T>(string errorMessage)
            where T : SecurityAuditEvent
        {
            return (T)this with { Success = false, ErrorMessage = errorMessage };
        }

        /// <summary>
        /// Creates a copy of this event with an exception
        /// </summary>
        public T WithException<T>(Exception exception)
            where T : SecurityAuditEvent
        {
            return (T)this with
            {
                Success = false,
                Exception = exception,
                ErrorMessage = exception.Message,
            };
        }

        /// <summary>
        /// Creates a copy of this event with parameters
        /// </summary>
        public T WithParameters<T>(params object[] parameters)
            where T : SecurityAuditEvent
        {
            return (T)this with { Parameters = parameters.ToImmutableArray() };
        }

        /// <summary>
        /// Creates a copy of this event with a result
        /// </summary>
        public T WithResult<T>(object result)
            where T : SecurityAuditEvent
        {
            return (T)this with { Result = result };
        }

        /// <summary>
        /// Creates a copy of this event with capability name
        /// </summary>
        public T WithCapability<T>(string capabilityName)
            where T : SecurityAuditEvent
        {
            return (T)this with { CapabilityName = capabilityName };
        }

        /// <summary>
        /// Creates a copy of this event with description
        /// </summary>
        public T WithDescription<T>(string description)
            where T : SecurityAuditEvent
        {
            return (T)this with { Description = description };
        }

        /// <summary>
        /// Gets formatted string representation of the event
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "SUCCESS" : "FAILURE";
            var identity = ScriptIdentity.HasValue ? $" [{ScriptIdentity}]" : string.Empty;
            var error = !string.IsNullOrEmpty(ErrorMessage) ? $" - {ErrorMessage}" : string.Empty;

            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {EventType} {status}: {Operation}{identity}{error}";
        }
    }

    /// <summary>
    /// General-purpose security audit event for simple auditing scenarios
    /// </summary>
    public sealed record GeneralSecurityAuditEvent : SecurityAuditEvent
    {
        public GeneralSecurityAuditEvent()
            : base(SecurityEventType.OperationSuccess) { }

        public GeneralSecurityAuditEvent(SecurityEventType eventType)
            : base(eventType) { }

        /// <summary>
        /// Creates a capability usage event
        /// </summary>
        public static GeneralSecurityAuditEvent CreateCapabilityUsage(
            string capabilityName,
            string operation,
            object[] parameters,
            object result,
            bool success
        )
        {
            return new GeneralSecurityAuditEvent(SecurityEventType.CapabilityUsage) with
            {
                CapabilityName = capabilityName,
                Operation = operation,
                Parameters = parameters?.ToImmutableArray() ?? ImmutableArray<object>.Empty,
                Result = result,
                Success = success,
                Description = $"Capability '{capabilityName}' executed operation '{operation}'",
            };
        }

        /// <summary>
        /// Creates a security violation event
        /// </summary>
        public static GeneralSecurityAuditEvent CreateSecurityViolation(
            SecurityEventType eventType,
            string description,
            Exception? exception = null
        )
        {
            return new GeneralSecurityAuditEvent(eventType) with
            {
                Description = description,
                Exception = exception,
                ErrorMessage = exception?.Message,
                Success = false,
            };
        }

        /// <summary>
        /// Creates a rate limit violation event
        /// </summary>
        public static GeneralSecurityAuditEvent CreateRateLimitViolation(
            string resource,
            int currentCount,
            int limit,
            TimeSpan window
        )
        {
            return new GeneralSecurityAuditEvent(SecurityEventType.RateLimitExceeded) with
            {
                Description =
                    $"Rate limit exceeded for '{resource}': {currentCount}/{limit} in {window}",
                Success = false,
                Metadata = ImmutableDictionary<string, object>
                    .Empty.Add("Resource", resource)
                    .Add("CurrentCount", currentCount)
                    .Add("Limit", limit)
                    .Add("Window", window),
            };
        }
    }

    /// <summary>
    /// Extension methods for working with security audit events
    /// </summary>
    public static class SecurityAuditEventExtensions
    {
        /// <summary>
        /// Filters events by success status
        /// </summary>
        public static IEnumerable<T> WhereSuccessful<T>(this IEnumerable<T> events)
            where T : SecurityAuditEvent
        {
            return events.Where(e => e.Success);
        }

        /// <summary>
        /// Filters events by failure status
        /// </summary>
        public static IEnumerable<T> WhereFailures<T>(this IEnumerable<T> events)
            where T : SecurityAuditEvent
        {
            return events.Where(e => !e.Success);
        }

        /// <summary>
        /// Filters events by script identity
        /// </summary>
        public static IEnumerable<T> WhereScriptIdentity<T>(
            this IEnumerable<T> events,
            ScriptIdentity identity
        )
            where T : SecurityAuditEvent
        {
            return events.Where(e =>
                e.ScriptIdentity.HasValue && e.ScriptIdentity.Value.Equals(identity)
            );
        }

        /// <summary>
        /// Filters events by time range
        /// </summary>
        public static IEnumerable<T> WhereTimeRange<T>(
            this IEnumerable<T> events,
            DateTime start,
            DateTime end
        )
            where T : SecurityAuditEvent
        {
            return events.Where(e => e.Timestamp >= start && e.Timestamp <= end);
        }
    }
}
