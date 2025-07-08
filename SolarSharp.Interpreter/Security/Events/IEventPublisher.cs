#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Events
{
    /// <summary>
    /// Event publishing interface for domain events with async support and correlation tracking
    /// </summary>
    /// <typeparam name="TEvent">Type of domain event to publish</typeparam>
    public interface IEventPublisher<in TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Publishes a domain event asynchronously with correlation tracking
        /// </summary>
        /// <param name="domainEvent">The domain event to publish</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Result indicating success or failure of publishing</returns>
        Task<UnitResult<EventPublishingError>> PublishAsync(
            TEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Generic event publisher for all domain events
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes any domain event asynchronously with correlation tracking
        /// </summary>
        /// <typeparam name="TEvent">Type of domain event to publish</typeparam>
        /// <param name="domainEvent">The domain event to publish</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Result indicating success or failure of publishing</returns>
        Task<UnitResult<EventPublishingError>> PublishAsync<TEvent>(
            TEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
            where TEvent : class;
    }

    /// <summary>
    /// Error type for event publishing failures
    /// </summary>
    public sealed record EventPublishingError
    {
        /// <summary>
        /// Error message describing the publishing failure
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Original exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Type of event that failed to publish
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
        /// Creates a new event publishing error
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="eventType">Type of event that failed</param>
        /// <param name="correlationId">Optional correlation ID</param>
        /// <param name="exception">Optional original exception</param>
        public EventPublishingError(
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
        /// Creates an event publishing error from an exception
        /// </summary>
        public static EventPublishingError FromException(
            Exception exception,
            string eventType,
            string? correlationId = null
        )
        {
            return new EventPublishingError(exception.Message, eventType, correlationId, exception);
        }

        /// <summary>
        /// Creates a timeout error for event publishing
        /// </summary>
        public static EventPublishingError Timeout(string eventType, string? correlationId = null)
        {
            return new EventPublishingError("Event publishing timed out", eventType, correlationId);
        }

        /// <summary>
        /// Creates a serialization error for event publishing
        /// </summary>
        public static EventPublishingError SerializationError(
            string eventType,
            string? correlationId = null,
            Exception? exception = null
        )
        {
            return new EventPublishingError(
                "Failed to serialize event for publishing",
                eventType,
                correlationId,
                exception
            );
        }
    }

    /// <summary>
    /// Extension methods for event publishers
    /// </summary>
    public static class EventPublisherExtensions
    {
        /// <summary>
        /// Publishes an event and ignores the result (fire-and-forget)
        /// </summary>
        public static Task PublishAndForgetAsync<TEvent>(
            this IEventPublisher publisher,
            TEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
            where TEvent : class
        {
            return publisher
                .PublishAsync(domainEvent, correlationId, cancellationToken)
                .ContinueWith(
                    _ => { },
                    cancellationToken,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default
                );
        }

        /// <summary>
        /// Publishes multiple events in sequence
        /// </summary>
        public static async Task<UnitResult<EventPublishingError>> PublishAllAsync<TEvent>(
            this IEventPublisher publisher,
            TEvent[] events,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
            where TEvent : class
        {
            foreach (var domainEvent in events)
            {
                var result = await publisher.PublishAsync(
                    domainEvent,
                    correlationId,
                    cancellationToken
                );
                if (result.IsFailure)
                    return result;
            }

            return UnitResult.Success<EventPublishingError>();
        }
    }
}
