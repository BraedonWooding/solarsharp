using System;
using System.Globalization;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents a memory size value with parsing support for various units.
    /// Immutable value type following domain-driven design principles.
    /// </summary>
    public readonly struct MemorySize : IEquatable<MemorySize>
    {
        /// <summary>
        /// Gets the memory size in bytes.
        /// </summary>
        public long Bytes { get; }

        /// <summary>
        /// Gets the memory size in megabytes.
        /// </summary>
        public int Megabytes => (int)(Bytes / (1024 * 1024));

        /// <summary>
        /// Initializes a new instance of MemorySize.
        /// </summary>
        /// <param name="bytes">The size in bytes.</param>
        private MemorySize(long bytes)
        {
            if (bytes < 0)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Memory size cannot be negative");
            
            Bytes = bytes;
        }

        /// <summary>
        /// Creates a MemorySize from bytes.
        /// </summary>
        public static MemorySize FromBytes(long bytes) => new(bytes);

        /// <summary>
        /// Creates a MemorySize from kilobytes.
        /// </summary>
        public static MemorySize FromKilobytes(int kilobytes) => new(kilobytes * 1024L);

        /// <summary>
        /// Creates a MemorySize from megabytes.
        /// </summary>
        public static MemorySize FromMegabytes(int megabytes) => new(megabytes * 1024L * 1024L);

        /// <summary>
        /// Creates a MemorySize from gigabytes.
        /// </summary>
        public static MemorySize FromGigabytes(int gigabytes) => new(gigabytes * 1024L * 1024L * 1024L);

        /// <summary>
        /// Parses a memory size string like "100MB", "2GB", "512KB".
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>A Result containing the parsed MemorySize or an error message.</returns>
        public static Result<MemorySize, string> Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Result.Failure<MemorySize, string>("Memory size cannot be empty");

            var normalized = value.Trim().ToUpperInvariant();

            // Try to parse with units
            if (normalized.EndsWith("B"))
            {
                if (normalized.EndsWith("KB"))
                {
                    var numStr = normalized[..^2].Trim();
                    if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
                        return Result.Success<MemorySize, string>(FromKilobytes(kb));
                }
                else if (normalized.EndsWith("MB"))
                {
                    var numStr = normalized[..^2].Trim();
                    if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb))
                        return Result.Success<MemorySize, string>(FromMegabytes(mb));
                }
                else if (normalized.EndsWith("GB"))
                {
                    var numStr = normalized[..^2].Trim();
                    if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gb))
                        return Result.Success<MemorySize, string>(FromGigabytes(gb));
                }
                else
                {
                    // Just "B" for bytes
                    var numStr = normalized[..^1].Trim();
                    if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
                        return Result.Success<MemorySize, string>(FromBytes(bytes));
                }
            }

            // Try to parse as plain number (assume MB for backwards compatibility)
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plainNum))
                return Result.Success<MemorySize, string>(FromMegabytes(plainNum));

            return Result.Failure<MemorySize, string>($"Invalid memory size format: '{value}'");
        }

        /// <summary>
        /// Returns a string representation of the memory size.
        /// </summary>
        public override string ToString()
        {
            if (Bytes >= 1024L * 1024L * 1024L && Bytes % (1024L * 1024L * 1024L) == 0)
                return $"{Bytes / (1024L * 1024L * 1024L)}GB";
            if (Bytes >= 1024L * 1024L && Bytes % (1024L * 1024L) == 0)
                return $"{Bytes / (1024L * 1024L)}MB";
            if (Bytes >= 1024L && Bytes % 1024L == 0)
                return $"{Bytes / 1024L}KB";
            return $"{Bytes}B";
        }

        /// <summary>
        /// Implicit conversion to int for megabytes (for backwards compatibility).
        /// </summary>
        public static implicit operator int(MemorySize size) => size.Megabytes;

        /// <summary>
        /// Implicit conversion from int megabytes.
        /// </summary>
        public static implicit operator MemorySize(int megabytes) => FromMegabytes(megabytes);

        /// <summary>
        /// Implements equality comparison.
        /// </summary>
        public bool Equals(MemorySize other) => Bytes == other.Bytes;

        /// <summary>
        /// Implements equality comparison.
        /// </summary>
        public override bool Equals(object obj) => obj is MemorySize other && Equals(other);

        /// <summary>
        /// Gets hash code based on bytes.
        /// </summary>
        public override int GetHashCode() => Bytes.GetHashCode();

        /// <summary>
        /// Implements equality operator.
        /// </summary>
        public static bool operator ==(MemorySize left, MemorySize right) => left.Equals(right);

        /// <summary>
        /// Implements inequality operator.
        /// </summary>
        public static bool operator !=(MemorySize left, MemorySize right) => !left.Equals(right);
    }
}