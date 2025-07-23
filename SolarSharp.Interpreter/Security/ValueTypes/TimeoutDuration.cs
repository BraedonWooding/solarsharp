using System;
using System.Globalization;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents a timeout duration value with parsing support for various formats.
    /// Immutable value type following domain-driven design principles.
    /// </summary>
    public readonly struct TimeoutDuration : IEquatable<TimeoutDuration>
    {
        /// <summary>
        /// Gets the timeout in milliseconds.
        /// </summary>
        public int Milliseconds { get; }

        /// <summary>
        /// Gets the timeout as a TimeSpan.
        /// </summary>
        public TimeSpan TimeSpan => TimeSpan.FromMilliseconds(Milliseconds);

        /// <summary>
        /// Initializes a new instance of TimeoutDuration.
        /// </summary>
        /// <param name="milliseconds">The duration in milliseconds.</param>
        private TimeoutDuration(int milliseconds)
        {
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timeout cannot be negative");
            
            Milliseconds = milliseconds;
        }

        /// <summary>
        /// Creates a TimeoutDuration from milliseconds.
        /// </summary>
        public static TimeoutDuration FromMilliseconds(int milliseconds) => new(milliseconds);

        /// <summary>
        /// Creates a TimeoutDuration from seconds.
        /// </summary>
        public static TimeoutDuration FromSeconds(int seconds) => new(seconds * 1000);

        /// <summary>
        /// Creates a TimeoutDuration from minutes.
        /// </summary>
        public static TimeoutDuration FromMinutes(int minutes) => new(minutes * 60 * 1000);

        /// <summary>
        /// Creates a TimeoutDuration from a TimeSpan.
        /// </summary>
        public static TimeoutDuration FromTimeSpan(TimeSpan timeSpan) => new((int)timeSpan.TotalMilliseconds);

        /// <summary>
        /// Parses a timeout string like "30s", "100ms", "5m", or TimeSpan format "00:00:30".
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>A Result containing the parsed TimeoutDuration or an error message.</returns>
        public static Result<TimeoutDuration, string> Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Result.Failure<TimeoutDuration, string>("Timeout cannot be empty");

            var normalized = value.Trim().ToLowerInvariant();

            // Try TimeSpan format first (00:00:00 or 00:00:00.000)
            if (normalized.Contains(':'))
            {
                if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var timespan))
                {
                    var totalMs = (int)timespan.TotalMilliseconds;
                    if (totalMs < 0)
                        return Result.Failure<TimeoutDuration, string>("Timeout cannot be negative");
                    return Result.Success<TimeoutDuration, string>(FromMilliseconds(totalMs));
                }
            }

            // Try unit-based formats
            if (normalized.EndsWith("ms"))
            {
                var numStr = normalized[..^2].Trim();
                if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
                    return Result.Success<TimeoutDuration, string>(FromMilliseconds(ms));
            }
            else if (normalized.EndsWith("s"))
            {
                var numStr = normalized[..^1].Trim();
                if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                    return Result.Success<TimeoutDuration, string>(FromSeconds(seconds));
            }
            else if (normalized.EndsWith("m"))
            {
                var numStr = normalized[..^1].Trim();
                if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                    return Result.Success<TimeoutDuration, string>(FromMinutes(minutes));
            }

            // Try as plain number (assume milliseconds for backwards compatibility)
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plainNum))
                return Result.Success<TimeoutDuration, string>(FromMilliseconds(plainNum));

            return Result.Failure<TimeoutDuration, string>($"Invalid timeout format: '{value}'");
        }

        /// <summary>
        /// Returns a string representation of the timeout.
        /// </summary>
        public override string ToString()
        {
            if (Milliseconds >= 60000 && Milliseconds % 60000 == 0)
                return $"{Milliseconds / 60000}m";
            if (Milliseconds >= 1000 && Milliseconds % 1000 == 0)
                return $"{Milliseconds / 1000}s";
            return $"{Milliseconds}ms";
        }

        /// <summary>
        /// Implicit conversion to int for milliseconds (for backwards compatibility).
        /// </summary>
        public static implicit operator int(TimeoutDuration duration) => duration.Milliseconds;

        /// <summary>
        /// Implicit conversion from int milliseconds.
        /// </summary>
        public static implicit operator TimeoutDuration(int milliseconds) => FromMilliseconds(milliseconds);

        /// <summary>
        /// Implicit conversion to TimeSpan.
        /// </summary>
        public static implicit operator TimeSpan(TimeoutDuration duration) => duration.TimeSpan;

        /// <summary>
        /// Implements equality comparison.
        /// </summary>
        public bool Equals(TimeoutDuration other) => Milliseconds == other.Milliseconds;

        /// <summary>
        /// Implements equality comparison.
        /// </summary>
        public override bool Equals(object obj) => obj is TimeoutDuration other && Equals(other);

        /// <summary>
        /// Gets hash code based on milliseconds.
        /// </summary>
        public override int GetHashCode() => Milliseconds.GetHashCode();

        /// <summary>
        /// Implements equality operator.
        /// </summary>
        public static bool operator ==(TimeoutDuration left, TimeoutDuration right) => left.Equals(right);

        /// <summary>
        /// Implements inequality operator.
        /// </summary>
        public static bool operator !=(TimeoutDuration left, TimeoutDuration right) => !left.Equals(right);
    }
}