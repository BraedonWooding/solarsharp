using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Implements RFC 8785 JSON Canonicalization Scheme (JCS)
    /// https://www.rfc-editor.org/rfc/rfc8785.html
    /// </summary>
    public static class JsonCanonicalizer
    {
        /// <summary>
        /// Canonicalizes a JSON string according to RFC 8785
        /// </summary>
        public static string Canonicalize(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return CanonicalizeElement(doc.RootElement);
        }

        /// <summary>
        /// Canonicalizes a JSON string, excluding a specific field
        /// Used for signature verification
        /// </summary>
        public static string CanonicalizeExcluding(string json, string pathToExclude)
        {
            using var doc = JsonDocument.Parse(json);
            return CanonicalizeElement(doc.RootElement, pathToExclude.Split('.'));
        }

        /// <summary>
        /// Extracts a field value before canonicalization
        /// </summary>
        public static string ExtractField(string json, string path)
        {
            using var doc = JsonDocument.Parse(json);
            var pathParts = path.Split('.');
            var element = doc.RootElement;

            foreach (var part in pathParts)
            {
                if (element.ValueKind != JsonValueKind.Object)
                    return null;

                if (!element.TryGetProperty(part, out element))
                    return null;
            }

            return element.GetRawText();
        }

        private static string CanonicalizeElement(JsonElement element, string[] excludePath = null, int pathDepth = 0)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                    return "null";

                case JsonValueKind.True:
                    return "true";

                case JsonValueKind.False:
                    return "false";

                case JsonValueKind.Number:
                    return CanonicalizeNumber(element);

                case JsonValueKind.String:
                    return CanonicalizeString(element.GetString());

                case JsonValueKind.Array:
                    return CanonicalizeArray(element, excludePath, pathDepth);

                case JsonValueKind.Object:
                    return CanonicalizeObject(element, excludePath, pathDepth);

                default:
                    throw new ArgumentException($"Unsupported JSON value kind: {element.ValueKind}");
            }
        }

        private static string CanonicalizeObject(JsonElement obj, string[] excludePath, int pathDepth)
        {
            var sb = new StringBuilder();
            sb.Append('{');

            // Get and sort properties by UTF-16 code unit order
            var properties = new List<(string name, JsonElement value)>();
            foreach (var prop in obj.EnumerateObject())
            {
                // Check if this property should be excluded
                if (excludePath != null && pathDepth < excludePath.Length && 
                    prop.Name == excludePath[pathDepth])
                {
                    // If this is the final part of the path, skip this property
                    if (pathDepth == excludePath.Length - 1)
                        continue;
                }

                properties.Add((prop.Name, prop.Value));
            }

            // Sort by UTF-16 code units (this is what RFC 8785 requires)
            properties.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            bool first = true;
            foreach (var (name, value) in properties)
            {
                if (!first)
                    sb.Append(',');
                first = false;

                sb.Append(CanonicalizeString(name));
                sb.Append(':');

                // Pass the exclude path down if we're on the path
                if (excludePath != null && pathDepth < excludePath.Length && 
                    name == excludePath[pathDepth])
                {
                    sb.Append(CanonicalizeElement(value, excludePath, pathDepth + 1));
                }
                else
                {
                    sb.Append(CanonicalizeElement(value, excludePath, int.MaxValue));
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string CanonicalizeArray(JsonElement array, string[] excludePath, int pathDepth)
        {
            var sb = new StringBuilder();
            sb.Append('[');

            bool first = true;
            foreach (var element in array.EnumerateArray())
            {
                if (!first)
                    sb.Append(',');
                first = false;

                sb.Append(CanonicalizeElement(element, excludePath, pathDepth));
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string CanonicalizeString(string value)
        {
            var sb = new StringBuilder();
            sb.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append($"\\u{(int)c:x4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        private static string CanonicalizeNumber(JsonElement number)
        {
            // RFC 8785 requires specific number formatting
            if (number.TryGetInt64(out var longValue))
            {
                return longValue.ToString(CultureInfo.InvariantCulture);
            }

            if (number.TryGetDouble(out var doubleValue))
            {
                // Handle special cases
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    throw new ArgumentException("NaN and Infinity are not allowed in canonical JSON");
                }

                // RFC 8785 specifies ECMAScript's ToString for numbers
                // This is complex, but for practical purposes we can use these rules:
                
                // Integer values without decimal point
                if (doubleValue == Math.Truncate(doubleValue) && 
                    doubleValue >= -9007199254740992 && // -(2^53)
                    doubleValue <= 9007199254740992)    // 2^53
                {
                    return ((long)doubleValue).ToString(CultureInfo.InvariantCulture);
                }

                // Use exponential notation for very large/small numbers
                var str = doubleValue.ToString("G17", CultureInfo.InvariantCulture);
                
                // Ensure proper exponential notation format
                if (str.Contains('E'))
                {
                    var parts = str.Split('E');
                    var expPart = parts[1];
                    
                    // Remove positive sign from exponent
                    if (expPart.StartsWith("+"))
                        expPart = expPart.Substring(1);
                    
                    str = parts[0] + "e" + expPart.ToLowerInvariant();
                }

                return str;
            }

            if (number.TryGetDecimal(out var decimalValue))
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }

            // Fallback
            return number.GetRawText();
        }
    }
}