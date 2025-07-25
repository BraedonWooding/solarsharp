using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Security.Auditing;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Interface for security auditing and logging
    /// </summary>
    public interface ISecurityAuditor
    {
        /// <summary>
        /// Logs a capability usage event
        /// </summary>
        void LogCapabilityUsage(
            string capabilityName,
            string operation,
            object[] parameters,
            object result,
            bool success
        );

        /// <summary>
        /// Logs a security violation
        /// </summary>
        void LogSecurityViolation(
            string description,
            SecurityEventType eventType = SecurityEventType.AccessDenied,
            Exception exception = null
        );

        /// <summary>
        /// Logs a rate limiting event
        /// </summary>
        void LogRateLimitViolation(string resource, int currentCount, int limit, TimeSpan window);

        /// <summary>
        /// Gets security metrics for analysis
        /// </summary>
        SecurityMetrics GetSecurityMetrics();

        /// <summary>
        /// Gets recent security events
        /// </summary>
        IReadOnlyList<SecurityAuditEvent> GetRecentEvents(int count = 100);
    }

    /// <summary>
    /// Security metrics for monitoring and analysis
    /// </summary>
    public class SecurityMetrics
    {
        public int TotalEvents { get; set; }
        public int ViolationCount { get; set; }
        public int CapabilityUsageCount { get; set; }
        public int RateLimitViolations { get; set; }
        public Dictionary<string, int> EventsByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ViolationsByCapability { get; set; } =
            new Dictionary<string, int>();
        public Dictionary<string, CapabilityUsageStats> CapabilityStats { get; set; } =
            new Dictionary<string, CapabilityUsageStats>();
        public DateTime FirstEvent { get; set; }
        public DateTime LastEvent { get; set; }
        public TimeSpan MonitoringDuration
        {
            get { return LastEvent - FirstEvent; }
        }
    }

    /// <summary>
    /// Default implementation of security auditor
    /// </summary>
    public class SecurityAuditor : ISecurityAuditor
    {
        private readonly List<SecurityAuditEvent> _events = new List<SecurityAuditEvent>();
        private readonly object _lockObject = new object();
        private readonly int _maxEvents;

        public SecurityAuditor(int maxEvents = 10000)
        {
            _maxEvents = maxEvents;
        }

        public void LogCapabilityUsage(
            string capabilityName,
            string operation,
            object[] parameters,
            object result,
            bool success
        )
        {
            var auditEvent = GeneralSecurityAuditEvent.CreateCapabilityUsage(
                capabilityName,
                operation,
                parameters,
                result,
                success
            );

            AddEvent(auditEvent);
        }

        public void LogSecurityViolation(
            string description,
            SecurityEventType eventType = SecurityEventType.AccessDenied,
            Exception exception = null
        )
        {
            var auditEvent = GeneralSecurityAuditEvent.CreateSecurityViolation(
                eventType,
                description,
                exception
            );

            AddEvent(auditEvent);
        }

        public void LogRateLimitViolation(
            string resource,
            int currentCount,
            int limit,
            TimeSpan window
        )
        {
            var auditEvent = GeneralSecurityAuditEvent.CreateRateLimitViolation(
                resource,
                currentCount,
                limit,
                window
            );

            AddEvent(auditEvent);
        }

        public SecurityMetrics GetSecurityMetrics()
        {
            lock (_lockObject)
            {
                var metrics = new SecurityMetrics();

                if (_events.Count == 0)
                    return metrics;

                metrics.TotalEvents = _events.Count;
                metrics.FirstEvent = _events[0].Timestamp;
                metrics.LastEvent = _events[^1].Timestamp;

                foreach (var evt in _events)
                {
                    // Count by event type
                    var eventType = evt.EventType.ToString();
                    metrics.EventsByType[eventType] =
                        metrics.EventsByType.GetValueOrDefault(eventType, 0) + 1;

                    // Count violations
                    if (!evt.Success)
                    {
                        metrics.ViolationCount++;

                        if (!string.IsNullOrEmpty(evt.CapabilityName))
                        {
                            metrics.ViolationsByCapability[evt.CapabilityName] =
                                metrics.ViolationsByCapability.GetValueOrDefault(
                                    evt.CapabilityName,
                                    0
                                ) + 1;
                        }
                    }

                    // Count capability usage
                    if (evt.EventType == SecurityEventType.CapabilityUsage)
                    {
                        metrics.CapabilityUsageCount++;
                    }

                    // Count rate limit violations
                    if (evt.EventType == SecurityEventType.RateLimitExceeded)
                    {
                        metrics.RateLimitViolations++;
                    }
                }

                return metrics;
            }
        }

        public IReadOnlyList<SecurityAuditEvent> GetRecentEvents(int count = 100)
        {
            lock (_lockObject)
            {
                var startIndex = Math.Max(0, _events.Count - count);
                return _events.Skip(startIndex).ToList();
            }
        }

        private void AddEvent(SecurityAuditEvent auditEvent)
        {
            lock (_lockObject)
            {
                _events.Add(auditEvent);

                // Trim events if we exceed the maximum
                if (_events.Count > _maxEvents)
                {
                    _events.RemoveRange(0, _events.Count - _maxEvents);
                }
            }
        }
    }
}
