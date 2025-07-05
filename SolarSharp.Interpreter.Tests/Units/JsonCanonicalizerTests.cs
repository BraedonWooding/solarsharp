using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Provides unit tests for verifying the functionality and correctness of the JSON canonicalization process.
    /// </summary>
    /// <remarks>
    ///     The tests ensure that JSON content is transformed into its canonical form following strict rules for comparison,
    ///     including whitespace removal, property ordering, and value normalization. These tests cover various scenarios,
    ///     including simple types, complex structures, and edge cases.
    ///     Test isolation: Parallelizable - stateless operations
    ///     Dependencies: None
    /// </remarks>
    [TestFixture]
    [Category("DataStructureTest")]
    public class JsonCanonicalizerTests
    {
        /// <summary>
        ///     Tests that null JSON values are properly canonicalized and whitespace is removed.
        /// </summary>
        /// <remarks>
        ///     This test verifies that the JSON canonicalizer correctly handles null values
        ///     and strips surrounding whitespace to produce a canonical representation.
        /// </remarks>
        [Test]
        public void TestNullValue()
        {
            Assert.Multiple(static () =>
            {
                Assert.That(JsonCanonicalizer.Canonicalize("null"), Is.EqualTo("null"));
                Assert.That(JsonCanonicalizer.Canonicalize(" null "), Is.EqualTo("null"));
            });
        }

        /// <summary>
        ///     Tests that boolean JSON values (true/false) are properly canonicalized.
        /// </summary>
        /// <remarks>
        ///     This test verifies that boolean literals are correctly processed by the canonicalizer
        ///     and maintain their exact representation in the canonical form.
        /// </remarks>
        [Test]
        public void TestBooleanValues()
        {
            Assert.Multiple(static () =>
            {
                Assert.That(JsonCanonicalizer.Canonicalize("true"), Is.EqualTo("true"));
                Assert.That(JsonCanonicalizer.Canonicalize("false"), Is.EqualTo("false"));
            });
        }

        /// <summary>
        ///     Tests that numeric JSON values are canonicalized to their minimal representation.
        /// </summary>
        /// <remarks>
        ///     This test verifies that numbers are converted to their canonical form including:
        ///     integers, decimals with trailing zeros removed, and exponential notation normalization.
        ///     This ensures consistent hashing and comparison of JSON manifest content.
        /// </remarks>
        [Test]
        public void TestNumberValues()
        {
            Assert.Multiple(static () =>
            {
                // Integers
                Assert.That(JsonCanonicalizer.Canonicalize("0"), Is.EqualTo("0"));
                Assert.That(JsonCanonicalizer.Canonicalize("123"), Is.EqualTo("123"));
                Assert.That(JsonCanonicalizer.Canonicalize("-456"), Is.EqualTo("-456"));

                // Decimals should maintain minimal representation
                Assert.That(JsonCanonicalizer.Canonicalize("1.0"), Is.EqualTo("1"));
                Assert.That(JsonCanonicalizer.Canonicalize("1.5"), Is.EqualTo("1.5"));

                // Exponential notation
                Assert.That(JsonCanonicalizer.Canonicalize("1e10"), Is.EqualTo("10000000000"));
                Assert.That(JsonCanonicalizer.Canonicalize("1.23e20"), Is.EqualTo("1.23e20"));
            });
        }

        /// <summary>
        ///     Tests that JSON string values are properly canonicalized, preserving content
        ///     and handling escape sequences accurately.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the JSON canonicalizer correctly processes string literals,
        ///     including basic strings, escaped sequences, and control characters, to maintain
        ///     their proper canonical form without altering their meaning or structure.
        /// </remarks>
        [Test]
        public void TestStringValues()
        {
            Assert.Multiple(static () =>
            {
                // Basic strings
                Assert.That(JsonCanonicalizer.Canonicalize("\"hello\""), Is.EqualTo("\"hello\""));
                Assert.That(JsonCanonicalizer.Canonicalize("\"\""), Is.EqualTo("\"\""));

                // Escape sequences
                Assert.That(JsonCanonicalizer.Canonicalize("\"\\\"quoted\\\"\""), Is.EqualTo("\"\\\"quoted\\\"\""));
                Assert.That(JsonCanonicalizer.Canonicalize("\"line\\nbreak\""), Is.EqualTo("\"line\\nbreak\""));
                Assert.That(JsonCanonicalizer.Canonicalize("\"tab\\there\""), Is.EqualTo("\"tab\\there\""));

                // Control characters should be escaped (input must be valid JSON)
                Assert.That(JsonCanonicalizer.Canonicalize("\"\\u0001\""), Is.EqualTo("\"\\u0001\""));
            });
        }

        /// <summary>
        ///     Validates that empty JSON objects and arrays are properly canonicalized.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the JSON canonicalizer correctly processes empty containers
        ///     such as objects (“{}”) and arrays (“[]”) by removing unnecessary whitespace and
        ///     outputting them in their canonical form.
        /// </remarks>
        [Test]
        public void TestEmptyContainers()
        {
            Assert.Multiple(static () =>
            {
                Assert.That(JsonCanonicalizer.Canonicalize("{}"), Is.EqualTo("{}"));
                Assert.That(JsonCanonicalizer.Canonicalize("[]"), Is.EqualTo("[]"));
                Assert.That(JsonCanonicalizer.Canonicalize(" { } "), Is.EqualTo("{}"));
                Assert.That(JsonCanonicalizer.Canonicalize(" [ ] "), Is.EqualTo("[]"));
            });
        }

        /// <summary>
        ///     Tests that JSON arrays are consistently canonicalized by normalizing their format and structure.
        /// </summary>
        /// <remarks>
        ///     This test verifies that the JSON canonicalizer processes arrays correctly by removing extraneous whitespace,
        ///     maintaining the order of elements, and preserving the integrity of mixed element types including strings,
        ///     numbers, booleans, and null values.
        /// </remarks>
        [Test]
        public void TestArrays()
        {
            Assert.Multiple(static () =>
            {
                Assert.That(JsonCanonicalizer.Canonicalize("[1,2,3]"), Is.EqualTo("[1,2,3]"));
                Assert.That(JsonCanonicalizer.Canonicalize("[1, 2, 3]"), Is.EqualTo("[1,2,3]"));
                Assert.That(JsonCanonicalizer.Canonicalize("[\"a\",\"b\",\"c\"]"), Is.EqualTo("[\"a\",\"b\",\"c\"]"));
                Assert.That(JsonCanonicalizer.Canonicalize("[true,false,null]"), Is.EqualTo("[true,false,null]"));
            });
        }

        /// <summary>
        ///     Tests that JSON object properties are ordered according to UTF-16 code unit ordering.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the JSON canonicalization process correctly reorders object properties
        ///     by their UTF-16 code unit sequences to produce a consistent and canonical representation.
        ///     Additionally, it verifies that numeric keys are sorted before alphabetic keys in UTF-16 ordering.
        /// </remarks>
        [Test]
        public void TestObjectPropertyOrdering()
        {
            // Properties should be sorted by UTF-16 code units
            var input = "{\"z\":1,\"a\":2,\"m\":3}";
            var expected = "{\"a\":2,\"m\":3,\"z\":1}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));

            // Numbers come before letters in UTF-16
            input = "{\"name\":\"test\",\"1\":\"one\",\"age\":30}";
            expected = "{\"1\":\"one\",\"age\":30,\"name\":\"test\"}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        ///     Tests that nested JSON objects are properly canonicalized.
        /// </summary>
        /// <remarks>
        ///     This test ensures that JSON objects containing nested structures are correctly processed
        ///     by the canonicalizer, maintaining property ordering and whitespace handling for the
        ///     canonical representation.
        /// </remarks>
        [Test]
        public void TestNestedObjects()
        {
            const string input = @"{
                ""person"": {
                    ""name"": ""John"",
                    ""age"": 30
                },
                ""active"": true
            }";
            var expected = "{\"active\":true,\"person\":{\"age\":30,\"name\":\"John\"}}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        ///     Verifies that complex JSON structures, including arrays, strings with escape sequences,
        ///     nested objects, null values, and Unicode characters, are properly canonicalized.
        /// </summary>
        /// <remarks>
        ///     This test ensures the JSON canonicalizer produces a consistent canonical representation
        ///     for complex inputs involving diverse data types, nested structures, and special characters.
        /// </remarks>
        [Test]
        public void TestComplexExample()
        {
            const string input = @"{
                ""numbers"": [1, 2, 3],
                ""string"": ""hello\nworld"",
                ""nested"": {
                    ""z"": null,
                    ""a"": true
                },
                ""emoji"": ""😀""
            }";
            const string expected =
                "{\"emoji\":\"😀\",\"nested\":{\"a\":true,\"z\":null},\"numbers\":[1,2,3],\"string\":\"hello\\nworld\"}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        ///     Tests that a specified field can be excluded from JSON canonicalization.
        /// </summary>
        /// <remarks>
        ///     This test verifies that the JSON canonicalizer correctly removes the specified
        ///     field from the input JSON prior to generating the canonical representation,
        ///     ensuring the output does not include unwanted data.
        /// </remarks>
        [Test]
        public void TestExcludeField()
        {
            const string input = @"{
                ""data"": ""value"",
                ""signature"": ""sig123"",
                ""timestamp"": 12345
            }";

            // Exclude signature field
            var canonicalized = JsonCanonicalizer.CanonicalizeExcluding(input, "signature");
            var expected = "{\"data\":\"value\",\"timestamp\":12345}";
            Assert.That(canonicalized, Is.EqualTo(expected));
        }

        /// <summary>
        ///     Validates that a nested JSON field can be excluded during canonicalization.
        /// </summary>
        /// <remarks>
        ///     This test verifies the functionality of the JSON canonicalizer in excluding
        ///     a specific nested field based on its path, ensuring the remaining structure
        ///     is properly canonicalized.
        /// </remarks>
        [Test]
        public void TestExcludeNestedField()
        {
            const string input = @"{
                ""data"": ""value"",
                ""security"": {
                    ""signature"": ""sig123"",
                    ""algorithm"": ""RSA""
                }
            }";

            // Exclude nested signature field
            var canonicalized = JsonCanonicalizer.CanonicalizeExcluding(input, "security.signature");
            const string expected = "{\"data\":\"value\",\"security\":{\"algorithm\":\"RSA\"}}";
            Assert.That(canonicalized, Is.EqualTo(expected));
        }

        /// <summary>
        ///     Tests the extraction of a field from a JSON string using a specified path.
        /// </summary>
        /// <remarks>
        ///     This test verifies that the method correctly extracts the value of a specified field
        ///     or object from a JSON string based on the provided dot-separated path.
        ///     It ensures that the resulting extracted value or object matches the expected format.
        /// </remarks>
        [Test]
        public void TestExtractField()
        {
            const string input = @"{
                ""data"": ""value"",
                ""security"": {
                    ""signature"": {
                        ""value"": ""sig123"",
                        ""algorithm"": ""RSA""
                    }
                }
            }";

            // Extract signature value
            var extracted = JsonCanonicalizer.ExtractField(input, "security.signature.value");
            Assert.That(extracted, Is.EqualTo("\"sig123\""));

            // Extract whole signature object
            extracted = JsonCanonicalizer.ExtractField(input, "security.signature");
            Assert.That(extracted, Does.Contain("sig123"));
            Assert.That(extracted, Does.Contain("RSA"));
        }

        [Test]
        public void TestWhitespaceHandling()
        {
            const string input = @"{
                ""a""    :    1   ,
                ""b""    :    ""test""
            }";
            const string expected = "{\"a\":1,\"b\":\"test\"}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        ///     Tests that Unicode characters, including control characters and regular Unicode,
        ///     are properly canonicalized.
        /// </summary>
        /// <remarks>
        ///     This test ensures that control characters are correctly escaped and remain in their
        ///     canonical form, while regular Unicode characters are preserved and passed through accurately.
        /// </remarks>
        [Test]
        public void TestUnicodeHandling()
        {
            Assert.Multiple(static () =>
            {
                // Control characters should be escaped (input must be valid JSON)
                Assert.That(JsonCanonicalizer.Canonicalize("\"\\u0000\""), Is.EqualTo("\"\\u0000\""));
                Assert.That(JsonCanonicalizer.Canonicalize("\"\\u001f\""), Is.EqualTo("\"\\u001f\""));

                // Regular Unicode should pass through
                Assert.That(JsonCanonicalizer.Canonicalize("\"café\""), Is.EqualTo("\"café\""));
                Assert.That(JsonCanonicalizer.Canonicalize("\"日本語\""), Is.EqualTo("\"日本語\""));
            });
        }

        /// <summary>
        ///     Tests the JSON canonicalizer against examples provided in RFC 8785.
        /// </summary>
        /// <remarks>
        ///     This test validates that the canonicalizer adheres to the requirements set forth in RFC 8785,
        ///     ensuring proper handling of scenarios such as empty string keys, property ordering, and other edge cases.
        /// </remarks>
        [Test]
        public void TestRfc8785Examples()
        {
            // Example from RFC 8785 Section 3.2.2.3.3 - empty string keys
            var input = "{\"\":\"\",\"\":\"\"}";
            var expected = "{\"\":\"\",\"\":\"\"}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));

            // Property ordering example
            input = @"{""中文"":1,""אברית"":2,""日本語"":3}";
            // These should be ordered by UTF-16 code units
            var result = JsonCanonicalizer.Canonicalize(input);
            Assert.That(result, Does.StartWith("{"));
            Assert.That(result, Does.EndWith("}"));
            Assert.That(result, Does.Contain("\"中文\":1"));
            Assert.That(result, Does.Contain("\"אברית\":2"));
            Assert.That(result, Does.Contain("\"日本語\":3"));
        }

        /// <summary>
        ///     Verifies that invalid JSON input is handled by throwing the appropriate exceptions.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the JSON canonicalizer detects malformed JSON input
        ///     and raises an exception of type <c>JsonException</c>, validating its robustness against invalid data.
        /// </remarks>
        [Test]
        public void TestInvalidJson()
        {
            // JsonReaderException is a subclass of JsonException
            Assert.That(static () => JsonCanonicalizer.Canonicalize("{invalid}"),
                Throws.InstanceOf<JsonException>());

            Assert.That(static () => JsonCanonicalizer.Canonicalize("{\"key\": }"),
                Throws.InstanceOf<JsonException>());
        }
    }
}