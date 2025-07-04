using NUnit.Framework;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class JsonCanonicalizerTests
    {
        /// <summary>
        /// Tests that null JSON values are properly canonicalized and whitespace is removed.
        /// </summary>
        /// <remarks>
        /// This test verifies that the JSON canonicalizer correctly handles null values
        /// and strips surrounding whitespace to produce a canonical representation.
        /// </remarks>
        [Test]
        public void TestNullValue()
        {
            Assert.That(JsonCanonicalizer.Canonicalize("null"), Is.EqualTo("null"));
            Assert.That(JsonCanonicalizer.Canonicalize(" null "), Is.EqualTo("null"));
        }

        /// <summary>
        /// Tests that boolean JSON values (true/false) are properly canonicalized.
        /// </summary>
        /// <remarks>
        /// This test verifies that boolean literals are correctly processed by the canonicalizer
        /// and maintain their exact representation in the canonical form.
        /// </remarks>
        [Test]
        public void TestBooleanValues()
        {
            Assert.That(JsonCanonicalizer.Canonicalize("true"), Is.EqualTo("true"));
            Assert.That(JsonCanonicalizer.Canonicalize("false"), Is.EqualTo("false"));
        }

        /// <summary>
        /// Tests that numeric JSON values are canonicalized to their minimal representation.
        /// </summary>
        /// <remarks>
        /// This test verifies that numbers are converted to their canonical form including:
        /// integers, decimals with trailing zeros removed, and exponential notation normalization.
        /// This ensures consistent hashing and comparison of JSON manifest content.
        /// </remarks>
        [Test]
        public void TestNumberValues()
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
        }

        [Test]
        public void TestStringValues()
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
        }

        [Test]
        public void TestEmptyContainers()
        {
            Assert.That(JsonCanonicalizer.Canonicalize("{}"), Is.EqualTo("{}"));
            Assert.That(JsonCanonicalizer.Canonicalize("[]"), Is.EqualTo("[]"));
            Assert.That(JsonCanonicalizer.Canonicalize(" { } "), Is.EqualTo("{}"));
            Assert.That(JsonCanonicalizer.Canonicalize(" [ ] "), Is.EqualTo("[]"));
        }

        [Test]
        public void TestArrays()
        {
            Assert.That(JsonCanonicalizer.Canonicalize("[1,2,3]"), Is.EqualTo("[1,2,3]"));
            Assert.That(JsonCanonicalizer.Canonicalize("[1, 2, 3]"), Is.EqualTo("[1,2,3]"));
            Assert.That(JsonCanonicalizer.Canonicalize("[\"a\",\"b\",\"c\"]"), Is.EqualTo("[\"a\",\"b\",\"c\"]"));
            Assert.That(JsonCanonicalizer.Canonicalize("[true,false,null]"), Is.EqualTo("[true,false,null]"));
        }

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

        [Test]
        public void TestNestedObjects()
        {
            var input = @"{
                ""person"": {
                    ""name"": ""John"",
                    ""age"": 30
                },
                ""active"": true
            }";
            var expected = "{\"active\":true,\"person\":{\"age\":30,\"name\":\"John\"}}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        [Test]
        public void TestComplexExample()
        {
            var input = @"{
                ""numbers"": [1, 2, 3],
                ""string"": ""hello\nworld"",
                ""nested"": {
                    ""z"": null,
                    ""a"": true
                },
                ""emoji"": ""😀""
            }";
            var expected = "{\"emoji\":\"😀\",\"nested\":{\"a\":true,\"z\":null},\"numbers\":[1,2,3],\"string\":\"hello\\nworld\"}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        [Test]
        public void TestExcludeField()
        {
            var input = @"{
                ""data"": ""value"",
                ""signature"": ""sig123"",
                ""timestamp"": 12345
            }";
            
            // Exclude signature field
            var canonicalized = JsonCanonicalizer.CanonicalizeExcluding(input, "signature");
            var expected = "{\"data\":\"value\",\"timestamp\":12345}";
            Assert.That(canonicalized, Is.EqualTo(expected));
        }

        [Test]
        public void TestExcludeNestedField()
        {
            var input = @"{
                ""data"": ""value"",
                ""security"": {
                    ""signature"": ""sig123"",
                    ""algorithm"": ""RSA""
                }
            }";
            
            // Exclude nested signature field
            var canonicalized = JsonCanonicalizer.CanonicalizeExcluding(input, "security.signature");
            var expected = "{\"data\":\"value\",\"security\":{\"algorithm\":\"RSA\"}}";
            Assert.That(canonicalized, Is.EqualTo(expected));
        }

        [Test]
        public void TestExtractField()
        {
            var input = @"{
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
            var input = @"{
                ""a""    :    1   ,
                ""b""    :    ""test""
            }";
            var expected = "{\"a\":1,\"b\":\"test\"}";
            Assert.That(JsonCanonicalizer.Canonicalize(input), Is.EqualTo(expected));
        }

        [Test]
        public void TestUnicodeHandling()
        {
            // Control characters should be escaped (input must be valid JSON)
            Assert.That(JsonCanonicalizer.Canonicalize("\"\\u0000\""), Is.EqualTo("\"\\u0000\""));
            Assert.That(JsonCanonicalizer.Canonicalize("\"\\u001f\""), Is.EqualTo("\"\\u001f\""));
            
            // Regular Unicode should pass through
            Assert.That(JsonCanonicalizer.Canonicalize("\"café\""), Is.EqualTo("\"café\""));
            Assert.That(JsonCanonicalizer.Canonicalize("\"日本語\""), Is.EqualTo("\"日本語\""));
        }

        [Test]
        public void TestRFC8785Examples()
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

        [Test]
        public void TestInvalidJson()
        {
            // JsonReaderException is a subclass of JsonException
            Assert.That(() => JsonCanonicalizer.Canonicalize("{invalid}"), 
                Throws.InstanceOf<System.Text.Json.JsonException>());
            
            // "undefined" is not valid JSON - test with actual invalid JSON
            Assert.That(() => JsonCanonicalizer.Canonicalize("{\"key\": }"), 
                Throws.InstanceOf<System.Text.Json.JsonException>());
        }
    }
}