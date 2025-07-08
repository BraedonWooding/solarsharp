using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// JSON converter that handles enum deserialization from both single values and arrays
    /// Supports both string and numeric representations
    /// </summary>
    public class EnumArrayJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    // Single string value
                    var stringValue = reader.GetString();
                    if (string.IsNullOrEmpty(stringValue))
                        return default(TEnum);
                    return ParseEnumValue(stringValue);

                case JsonTokenType.Number:
                    // Single numeric value
                    if (typeToConvert.IsEnum)
                    {
                        var numericValue = reader.GetInt32();
                        return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);
                    }
                    throw new JsonException($"Cannot convert number to {typeof(TEnum).Name}");

                case JsonTokenType.StartArray:
                    // Array of values (for flags enums)
                    var values = new List<TEnum>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var itemString = reader.GetString();
                            if (!string.IsNullOrEmpty(itemString))
                            {
                                values.Add(ParseEnumValue(itemString));
                            }
                        }
                        else if (reader.TokenType == JsonTokenType.Number)
                        {
                            var numericValue = reader.GetInt32();
                            values.Add((TEnum)Enum.ToObject(typeof(TEnum), numericValue));
                        }
                    }

                    // Combine flags if this is a flags enum
                    if (typeof(TEnum).GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                    {
                        int combinedValue = 0;
                        foreach (var value in values)
                        {
                            combinedValue |= Convert.ToInt32(value);
                        }
                        return (TEnum)Enum.ToObject(typeof(TEnum), combinedValue);
                    }

                    // For non-flags enums, return the first value or default
                    return values.FirstOrDefault();

                case JsonTokenType.Null:
                    return default(TEnum);

                default:
                    throw new JsonException(
                        $"Unexpected token type {reader.TokenType} when parsing {typeof(TEnum).Name}"
                    );
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options
        )
        {
            // For flags enums, write as array
            if (typeof(TEnum).GetCustomAttributes(typeof(FlagsAttribute), false).Any())
            {
                var flags = Enum.GetValues(typeof(TEnum))
                    .Cast<TEnum>()
                    .Where(flag => value.HasFlag(flag) && Convert.ToInt32(flag) != 0)
                    .ToList();

                if (flags.Count > 1)
                {
                    writer.WriteStartArray();
                    foreach (var flag in flags)
                    {
                        writer.WriteStringValue(flag.ToString());
                    }
                    writer.WriteEndArray();
                    return;
                }
            }

            // Write single value as string
            writer.WriteStringValue(value.ToString());
        }

        private static TEnum ParseEnumValue(string value)
        {
            // Try exact match first
            if (Enum.TryParse<TEnum>(value, true, out var result))
            {
                return result;
            }

            // Handle legacy capability names for backward compatibility
            if (typeof(TEnum) == typeof(ScriptCapabilities))
            {
                var mappedValue = MapLegacyCapabilityName(value);
                if (mappedValue != null && Enum.TryParse(mappedValue, true, out result))
                {
                    return result;
                }
            }

            // Handle special cases for common enum values
            var normalizedValue = value.Replace(" ", "").Replace("-", "").Replace("_", "");
            if (Enum.TryParse(normalizedValue, true, out result))
            {
                return result;
            }

            // Handle "All" for flags enums
            if (
                value.Equals("All", StringComparison.OrdinalIgnoreCase)
                && typeof(TEnum).GetCustomAttributes(typeof(FlagsAttribute), false).Any()
            )
            {
                // Return all flags combined
                int allFlags = 0;
                foreach (var enumValue in Enum.GetValues(typeof(TEnum)))
                {
                    allFlags |= Convert.ToInt32(enumValue);
                }
                return (TEnum)Enum.ToObject(typeof(TEnum), allFlags);
            }

            throw new JsonException($"Unable to parse '{value}' as {typeof(TEnum).Name}");
        }

        /// <summary>
        /// Maps legacy capability names to current ScriptCapabilities enum values
        /// </summary>
        private static string MapLegacyCapabilityName(string legacyName)
        {
            return legacyName?.ToLowerInvariant() switch
            {
                "basic" => "SafeCompute",
                "string" => "SafeCompute", // String operations are part of safe compute
                "math" => "SafeCompute", // Math operations are part of safe compute
                "table" => "SafeCompute", // Table operations are part of safe compute
                "io" => "FileRead", // Legacy IO maps to FileRead for backward compatibility
                "os" => "EnvironmentAccess", // OS access maps to EnvironmentAccess
                "fileread" => "FileRead",
                "filewrite" => "FileWrite",
                "networkaccess" => "NetworkAccess",
                "environmentaccess" => "EnvironmentAccess",
                _ => null,
            };
        }
    }
}
