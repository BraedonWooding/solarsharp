#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Auditing;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Security.Events
{
    /// <summary>
    /// Event sourcing store for security audit trails using immutable event streams
    /// </summary>
    public sealed class SecurityEventStore
    {
        private readonly object _lock = new object();
        private ImmutableArray<SecurityEventRecord> _events =
            ImmutableArray<SecurityEventRecord>.Empty;
        private readonly IEventPublisher? _eventPublisher;

        /// <summary>
        /// Gets all events in the store (immutable snapshot)
        /// </summary>
        public ImmutableArray<SecurityEventRecord> Events
        {
            get
            {
                lock (_lock)
                {
                    return _events;
                }
            }
        }

        /// <summary>
        /// Gets the total number of events in the store
        /// </summary>
        public int EventCount => _events.Length;

        /// <summary>
        /// Creates a new security event store
        /// </summary>
        /// <param name="eventPublisher">Optional event publisher for reactive updates</param>
        public SecurityEventStore(IEventPublisher? eventPublisher = null)
        {
            _eventPublisher = eventPublisher;
        }

        /// <summary>
        /// Appends a security audit event to the immutable event stream
        /// </summary>
        /// <param name="auditEvent">The security audit event to append</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Result indicating success or failure of append operation</returns>
        public async Task<Result<EventRecord, EventStoreError>> AppendEventAsync(
            SecurityAuditEvent auditEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var eventRecord = new SecurityEventRecord(
                    EventId: Guid.NewGuid(),
                    StreamId: auditEvent.ScriptId,
                    EventNumber: _events.Length + 1,
                    EventType: auditEvent.GetType().Name,
                    Event: auditEvent,
                    CorrelationId: correlationId,
                    AppendedAt: DateTime.UtcNow
                );

                lock (_lock)
                {
                    _events = _events.Add(eventRecord);
                }

                // Publish event appended notification reactively
                if (_eventPublisher != null)
                {
                    await _eventPublisher.PublishAsync(
                        new EventAppendedNotification(eventRecord),
                        correlationId,
                        cancellationToken
                    );
                }

                return Result.Success<EventRecord, EventStoreError>(eventRecord);
            }
            catch (Exception ex)
            {
                return Result.Failure<EventRecord, EventStoreError>(
                    EventStoreError.FromException(ex, auditEvent.GetType().Name, correlationId)
                );
            }
        }

        /// <summary>
        /// Gets events for a specific script identity using functional filtering
        /// </summary>
        /// <param name="identity">The script identity to filter by</param>
        /// <returns>Immutable array of events for the specified identity</returns>
        public ImmutableArray<SecurityEventRecord> GetEventsForScript(ScriptIdentity identity)
        {
            var publicKeyToken = CertificateManager.TokenToHex(identity.PublicKeyToken);

            return _events
                .Where(record =>
                    record.Event.ScriptIdentity.HasValue
                    && CertificateManager.TokenToHex(
                        record.Event.ScriptIdentity.Value.PublicKeyToken
                    ) == publicKeyToken
                )
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets events for a specific script ID using functional filtering
        /// </summary>
        /// <param name="scriptId">The script ID to filter by</param>
        /// <returns>Immutable array of events for the specified script ID</returns>
        public ImmutableArray<SecurityEventRecord> GetEventsForScriptId(string scriptId)
        {
            return _events.Where(record => record.StreamId == scriptId).ToImmutableArray();
        }

        /// <summary>
        /// Gets events within a time range using functional filtering
        /// </summary>
        /// <param name="start">Start time (inclusive)</param>
        /// <param name="end">End time (inclusive)</param>
        /// <returns>Immutable array of events within the time range</returns>
        public ImmutableArray<SecurityEventRecord> GetEventsByTimeRange(
            DateTime start,
            DateTime end
        )
        {
            return _events
                .Where(record => record.Event.Timestamp >= start && record.Event.Timestamp <= end)
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets events by event type using functional filtering
        /// </summary>
        /// <typeparam name="TEvent">Type of security audit event</typeparam>
        /// <returns>Immutable array of events of the specified type</returns>
        public ImmutableArray<SecurityEventRecord> GetEventsByType<TEvent>()
            where TEvent : SecurityAuditEvent
        {
            return _events.Where(record => record.Event is TEvent).ToImmutableArray();
        }

        /// <summary>
        /// Gets events by correlation ID using functional filtering
        /// </summary>
        /// <param name="correlationId">The correlation ID to filter by</param>
        /// <returns>Immutable array of events with the specified correlation ID</returns>
        public ImmutableArray<SecurityEventRecord> GetEventsByCorrelationId(string correlationId)
        {
            return _events
                .Where(record => record.CorrelationId == correlationId)
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets security violations (failed events) using functional filtering
        /// </summary>
        /// <returns>Immutable array of security violation events</returns>
        public ImmutableArray<SecurityEventRecord> GetSecurityViolations()
        {
            return _events.Where(record => !record.Event.Success).ToImmutableArray();
        }

        /// <summary>
        /// Gets event statistics for monitoring and analysis
        /// </summary>
        /// <returns>Event statistics summary</returns>
        public EventStoreStatistics GetStatistics()
        {
            var events = _events; // Atomic snapshot

            var totalEvents = events.Length;
            var successfulEvents = events.Count(record => record.Event.Success);
            var failedEvents = events.Count(record => !record.Event.Success);

            var eventsByType = events
                .GroupBy(record => record.EventType)
                .ToImmutableDictionary(group => group.Key, group => group.Count());

            var oldestEvent = events.FirstOrDefault()?.AppendedAt;
            var newestEvent = events.LastOrDefault()?.AppendedAt;

            return new EventStoreStatistics(
                TotalEvents: totalEvents,
                SuccessfulEvents: successfulEvents,
                FailedEvents: failedEvents,
                EventsByType: eventsByType,
                OldestEvent: oldestEvent,
                NewestEvent: newestEvent,
                GeneratedAt: DateTime.UtcNow
            );
        }

        /// <summary>
        /// Clears all events from the store (for testing purposes only)
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _events = ImmutableArray<SecurityEventRecord>.Empty;
            }
        }
    }

    /// <summary>
    /// Immutable record representing an event in the event store
    /// </summary>
    public sealed record SecurityEventRecord : EventRecord
    {
        /// <summary>
        /// The actual security audit event
        /// </summary>
        public SecurityAuditEvent Event { get; init; }

        /// <summary>
        /// Creates a new security event record
        /// </summary>
        public SecurityEventRecord(
            Guid EventId,
            string StreamId,
            long EventNumber,
            string EventType,
            SecurityAuditEvent Event,
            string? CorrelationId = null,
            DateTime? AppendedAt = null
        )
            : base(EventId, StreamId, EventNumber, EventType, CorrelationId, AppendedAt)
        {
            this.Event = Event;
        }
    }

    /// <summary>
    /// Base record for event store entries
    /// </summary>
    public abstract record EventRecord
    {
        /// <summary>
        /// Unique identifier for this event
        /// </summary>
        public Guid EventId { get; init; }

        /// <summary>
        /// Stream identifier (typically script ID)
        /// </summary>
        public string StreamId { get; init; } = string.Empty;

        /// <summary>
        /// Sequential event number in the stream
        /// </summary>
        public long EventNumber { get; init; }

        /// <summary>
        /// Type name of the event
        /// </summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>
        /// Optional correlation ID for tracing
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Timestamp when the event was appended to the store
        /// </summary>
        public DateTime AppendedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new event record
        /// </summary>
        protected EventRecord(
            Guid EventId,
            string StreamId,
            long EventNumber,
            string EventType,
            string? CorrelationId = null,
            DateTime? AppendedAt = null
        )
        {
            this.EventId = EventId;
            this.StreamId = StreamId;
            this.EventNumber = EventNumber;
            this.EventType = EventType;
            this.CorrelationId = CorrelationId;
            this.AppendedAt = AppendedAt ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Statistics about the event store contents
    /// </summary>
    public sealed record EventStoreStatistics
    {
        /// <summary>
        /// Total number of events in the store
        /// </summary>
        public int TotalEvents { get; init; }

        /// <summary>
        /// Number of successful events
        /// </summary>
        public int SuccessfulEvents { get; init; }

        /// <summary>
        /// Number of failed events
        /// </summary>
        public int FailedEvents { get; init; }

        /// <summary>
        /// Count of events by type
        /// </summary>
        public ImmutableDictionary<string, int> EventsByType { get; init; } =
            ImmutableDictionary<string, int>.Empty;

        /// <summary>
        /// Timestamp of the oldest event
        /// </summary>
        public DateTime? OldestEvent { get; init; }

        /// <summary>
        /// Timestamp of the newest event
        /// </summary>
        public DateTime? NewestEvent { get; init; }

        /// <summary>
        /// When these statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate =>
            TotalEvents > 0 ? (double)SuccessfulEvents / TotalEvents * 100 : 0;

        /// <summary>
        /// Creates new event store statistics
        /// </summary>
        public EventStoreStatistics(
            int TotalEvents,
            int SuccessfulEvents,
            int FailedEvents,
            ImmutableDictionary<string, int> EventsByType,
            DateTime? OldestEvent = null,
            DateTime? NewestEvent = null,
            DateTime? GeneratedAt = null
        )
        {
            this.TotalEvents = TotalEvents;
            this.SuccessfulEvents = SuccessfulEvents;
            this.FailedEvents = FailedEvents;
            this.EventsByType = EventsByType;
            this.OldestEvent = OldestEvent;
            this.NewestEvent = NewestEvent;
            this.GeneratedAt = GeneratedAt ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Error type for event store operations
    /// </summary>
    public sealed record EventStoreError
    {
        /// <summary>
        /// Error message describing the failure
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Original exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Type of event that caused the error
        /// </summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>
        /// Correlation ID for tracing, if available
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new event store error
        /// </summary>
        public EventStoreError(
            string message,
            string eventType,
            string? correlationId = null,
            Exception? exception = null
        )
        {
            Message = message;
            EventType = eventType;
            CorrelationId = correlationId;
            Exception = exception;
        }

        /// <summary>
        /// Creates an event store error from an exception
        /// </summary>
        public static EventStoreError FromException(
            Exception exception,
            string eventType,
            string? correlationId = null
        )
        {
            return new EventStoreError(exception.Message, eventType, correlationId, exception);
        }
    }

    /// <summary>
    /// Notification event published when an event is appended to the store
    /// </summary>
    public sealed record EventAppendedNotification
    {
        /// <summary>
        /// The event record that was appended
        /// </summary>
        public SecurityEventRecord EventRecord { get; init; }

        /// <summary>
        /// Timestamp when the notification was created
        /// </summary>
        public DateTime NotificationTime { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new event appended notification
        /// </summary>
        public EventAppendedNotification(SecurityEventRecord eventRecord)
        {
            EventRecord = eventRecord;
        }
    }
}
