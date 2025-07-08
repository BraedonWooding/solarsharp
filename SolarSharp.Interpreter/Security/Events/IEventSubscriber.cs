#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Events
{
    /// <summary>
    /// Event subscription interface for typed domain event handling
    /// </summary>
    /// <typeparam name="TEvent">Type of domain event to handle</typeparam>
    public interface IEventSubscriber<in TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Handles a domain event asynchronously using functional composition
        /// </summary>
        /// <param name="domainEvent">The domain event to handle</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Result indicating success or failure of handling</returns>
        Task<UnitResult<EventHandlingError>> HandleAsync(
            TEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Event filter interface for pure functional event filtering
    /// </summary>
    /// <typeparam name="TEvent">Type of domain event to filter</typeparam>
    public interface IEventFilter<in TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Determines whether an event should be processed (pure function)
        /// </summary>
        /// <param name="domainEvent">The domain event to filter</param>
        /// <returns>True if the event should be processed, false otherwise</returns>
        bool ShouldProcess(TEvent domainEvent);
    }

    /// <summary>
    /// Event transformer interface for pure functional event transformation
    /// </summary>
    /// <typeparam name="TInputEvent">Type of input domain event</typeparam>
    /// <typeparam name="TOutputEvent">Type of output domain event</typeparam>
    public interface IEventTransformer<in TInputEvent, TOutputEvent>
        where TInputEvent : class
        where TOutputEvent : class
    {
        /// <summary>
        /// Transforms an input event to an output event (pure function)
        /// </summary>
        /// <param name="inputEvent">The input domain event</param>
        /// <returns>Result containing the transformed event or error</returns>
        Result<TOutputEvent, EventTransformationError> Transform(TInputEvent inputEvent);
    }

    /// <summary>
    /// Event aggregator interface for combining multiple events into domain insights
    /// </summary>
    /// <typeparam name="TEvent">Type of domain event to aggregate</typeparam>
    /// <typeparam name="TAggregate">Type of aggregate result</typeparam>
    public interface IEventAggregator<in TEvent, TAggregate>
        where TEvent : class
        where TAggregate : class
    {
        /// <summary>
        /// Aggregates multiple events into a single result (pure function)
        /// </summary>
        /// <param name="events">The domain events to aggregate</param>
        /// <returns>Result containing the aggregated result or error</returns>
        Result<TAggregate, EventAggregationError> Aggregate(TEvent[] events);
    }

    /// <summary>
    /// Error type for event handling failures
    /// </summary>
    public sealed record EventHandlingError
    {
        /// <summary>
        /// Error message describing the handling failure
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Original exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Type of event that failed to be handled
        /// </summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>
        /// Correlation ID for tracing, if available
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Handler type that failed
        /// </summary>
        public string HandlerType { get; init; } = string.Empty;

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new event handling error
        /// </summary>
        public EventHandlingError(
            string message,
            string eventType,
            string handlerType,
            string? correlationId = null,
            Exception? exception = null
        )
        {
            Message = message;
            EventType = eventType;
            HandlerType = handlerType;
            CorrelationId = correlationId;
            Exception = exception;
        }
    }

    /// <summary>
    /// Error type for event transformation failures
    /// </summary>
    public sealed record EventTransformationError
    {
        /// <summary>
        /// Error message describing the transformation failure
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Original exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Type of input event that failed transformation
        /// </summary>
        public string InputEventType { get; init; } = string.Empty;

        /// <summary>
        /// Type of output event that was expected
        /// </summary>
        public string OutputEventType { get; init; } = string.Empty;

        /// <summary>
        /// Creates a new event transformation error
        /// </summary>
        public EventTransformationError(
            string message,
            string inputEventType,
            string outputEventType,
            Exception? exception = null
        )
        {
            Message = message;
            InputEventType = inputEventType;
            OutputEventType = outputEventType;
            Exception = exception;
        }
    }

    /// <summary>
    /// Error type for event aggregation failures
    /// </summary>
    public sealed record EventAggregationError
    {
        /// <summary>
        /// Error message describing the aggregation failure
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Original exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Type of events that failed aggregation
        /// </summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>
        /// Number of events that were being aggregated
        /// </summary>
        public int EventCount { get; init; }

        /// <summary>
        /// Creates a new event aggregation error
        /// </summary>
        public EventAggregationError(
            string message,
            string eventType,
            int eventCount,
            Exception? exception = null
        )
        {
            Message = message;
            EventType = eventType;
            EventCount = eventCount;
            Exception = exception;
        }
    }

    /// <summary>
    /// Functional event handling extensions
    /// </summary>
    public static class EventHandlingExtensions
    {
        /// <summary>
        /// Chains event handlers using functional composition
        /// </summary>
        public static IEventSubscriber<TEvent> Chain<TEvent>(
            this IEventSubscriber<TEvent> first,
            IEventSubscriber<TEvent> second
        )
            where TEvent : class
        {
            return new ChainedEventSubscriber<TEvent>(first, second);
        }

        /// <summary>
        /// Applies a filter to an event subscriber
        /// </summary>
        public static IEventSubscriber<TEvent> WithFilter<TEvent>(
            this IEventSubscriber<TEvent> subscriber,
            IEventFilter<TEvent> filter
        )
            where TEvent : class
        {
            return new FilteredEventSubscriber<TEvent>(subscriber, filter);
        }

        /// <summary>
        /// Transforms events before handling
        /// </summary>
        public static IEventSubscriber<TInputEvent> Transform<TInputEvent, TOutputEvent>(
            this IEventSubscriber<TOutputEvent> subscriber,
            IEventTransformer<TInputEvent, TOutputEvent> transformer
        )
            where TInputEvent : class
            where TOutputEvent : class
        {
            return new TransformingEventSubscriber<TInputEvent, TOutputEvent>(
                subscriber,
                transformer
            );
        }
    }

    /// <summary>
    /// Internal implementation of chained event subscriber
    /// </summary>
    internal sealed class ChainedEventSubscriber<TEvent> : IEventSubscriber<TEvent>
        where TEvent : class
    {
        private readonly IEventSubscriber<TEvent> _first;
        private readonly IEventSubscriber<TEvent> _second;

        public ChainedEventSubscriber(
            IEventSubscriber<TEvent> first,
            IEventSubscriber<TEvent> second
        )
        {
            _first = first;
            _second = second;
        }

        public async Task<UnitResult<EventHandlingError>> HandleAsync(
            TEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
        {
            var firstResult = await _first.HandleAsync(
                domainEvent,
                correlationId,
                cancellationToken
            );
            if (firstResult.IsFailure)
                return firstResult;

            return await _second.HandleAsync(domainEvent, correlationId, cancellationToken);
        }
    }

    /// <summary>
    /// Internal implementation of filtered event subscriber
    /// </summary>
    internal sealed class FilteredEventSubscriber<TEvent> : IEventSubscriber<TEvent>
        where TEvent : class
    {
        private readonly IEventSubscriber<TEvent> _subscriber;
        private readonly IEventFilter<TEvent> _filter;

        public FilteredEventSubscriber(
            IEventSubscriber<TEvent> subscriber,
            IEventFilter<TEvent> filter
        )
        {
            _subscriber = subscriber;
            _filter = filter;
        }

        public async Task<UnitResult<EventHandlingError>> HandleAsync(
            TEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
        {
            if (!_filter.ShouldProcess(domainEvent))
                return UnitResult.Success<EventHandlingError>();

            return await _subscriber.HandleAsync(domainEvent, correlationId, cancellationToken);
        }
    }

    /// <summary>
    /// Internal implementation of transforming event subscriber
    /// </summary>
    internal sealed class TransformingEventSubscriber<TInputEvent, TOutputEvent>
        : IEventSubscriber<TInputEvent>
        where TInputEvent : class
        where TOutputEvent : class
    {
        private readonly IEventSubscriber<TOutputEvent> _subscriber;
        private readonly IEventTransformer<TInputEvent, TOutputEvent> _transformer;

        public TransformingEventSubscriber(
            IEventSubscriber<TOutputEvent> subscriber,
            IEventTransformer<TInputEvent, TOutputEvent> transformer
        )
        {
            _subscriber = subscriber;
            _transformer = transformer;
        }

        public async Task<UnitResult<EventHandlingError>> HandleAsync(
            TInputEvent domainEvent,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
        {
            var transformResult = _transformer.Transform(domainEvent);
            if (transformResult.IsFailure)
            {
                return UnitResult.Failure<EventHandlingError>(
                    new EventHandlingError(
                        $"Event transformation failed: {transformResult.Error.Message}",
                        typeof(TInputEvent).Name,
                        typeof(TransformingEventSubscriber<TInputEvent, TOutputEvent>).Name,
                        correlationId,
                        transformResult.Error.Exception
                    )
                );
            }

            return await _subscriber.HandleAsync(
                transformResult.Value,
                correlationId,
                cancellationToken
            );
        }
    }
}
