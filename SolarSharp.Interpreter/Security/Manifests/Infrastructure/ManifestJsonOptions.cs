using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Provides standardized JSON serialization options for manifest processing
    /// </summary>
    public static class ManifestJsonOptions
    {
        /// <summary>
        /// Gets the default JsonSerializerOptions for manifest deserialization
        /// </summary>
        public static JsonSerializerOptions Default { get; } = CreateDefaultOptions();

        private static JsonSerializerOptions CreateDefaultOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true,
            };

            // Add converter for ImmutableArray<string>
            options.Converters.Add(new ImmutableArrayJsonConverter<string>());

            // Add converters for enums that support both single values and arrays
            options.Converters.Add(new EnumArrayJsonConverter<ScriptCapabilities>());
            options.Converters.Add(new EnumArrayJsonConverter<CoreModules>());
            options.Converters.Add(new EnumArrayJsonConverter<FilePermissions>());
            options.Converters.Add(new EnumArrayJsonConverter<DirectoryPermissions>());

            // Add converter for handling null int values
            options.Converters.Add(new NullableIntJsonConverter());

            // Add custom converter for SignatureType to handle standard algorithm names
            options.Converters.Add(new SignatureTypeJsonConverter());

            // Add standard string enum converter for other enums
            options.Converters.Add(new JsonStringEnumConverter());

            return options;
        }
    }
}
