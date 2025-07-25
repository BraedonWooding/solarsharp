using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// JSON converter that handles null values for int properties by using default value
    /// </summary>
    public class NullableIntJsonConverter : JsonConverter<int>
    {
        public override int Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.GetInt32();
                case JsonTokenType.String:
                    if (int.TryParse(reader.GetString(), out var result))
                        return result;
                    return 0;
                case JsonTokenType.Null:
                    return 0; // Default value for null
                default:
                    throw new JsonException(
                        $"Unexpected token type {reader.TokenType} when parsing int"
                    );
            }
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}
