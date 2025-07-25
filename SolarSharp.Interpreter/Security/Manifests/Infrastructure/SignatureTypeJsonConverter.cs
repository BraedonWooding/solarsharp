using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// JSON converter for SignatureType enum that maps standard algorithm names to enum values
    /// </summary>
    public class SignatureTypeJsonConverter : JsonConverter<SignatureType>
    {
        public override SignatureType Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    var algorithmName = reader.GetString();
                    return algorithmName switch
                    {
                        "None" => SignatureType.None,
                        "X509" => SignatureType.X509,
                        "PGP" => SignatureType.PGP,
                        "RSA_SHA256" => SignatureType.RSA_SHA256,
                        "SHA256withRSA" => SignatureType.RSA_SHA256, // Map standard algorithm name to enum
                        "RSAwithSHA256" => SignatureType.RSA_SHA256, // Alternative format
                        "ECDSA_P256_SHA256" => SignatureType.ECDSA_P256_SHA256,
                        "SHA256withECDSA" => SignatureType.ECDSA_P256_SHA256, // Map standard algorithm name
                        "SHA256withECDSA-P256" => SignatureType.ECDSA_P256_SHA256, // Alternative format
                        "ECDSA_P384_SHA256" => SignatureType.ECDSA_P384_SHA256,
                        "SHA256withECDSA-P384" => SignatureType.ECDSA_P384_SHA256, // Alternative format
                        "ECDSA_P521_SHA256" => SignatureType.ECDSA_P521_SHA256,
                        "SHA256withECDSA-P521" => SignatureType.ECDSA_P521_SHA256, // Alternative format
                        // Reject weak/insecure algorithms
                        "MD5withRSA" => throw new ManifestFormatException(
                            "Insecure hash algorithm MD5 is not allowed",
                            algorithmName
                        ),
                        "SHA1withRSA" => throw new ManifestFormatException(
                            "Weak hash algorithm SHA1 is not allowed",
                            algorithmName
                        ),
                        "SHA3-256withRSA" => throw new ManifestFormatException(
                            "Unsupported hash algorithm SHA3-256",
                            algorithmName
                        ),
                        "SHA256withDSA" => throw new ManifestFormatException(
                            "Unsupported algorithm DSA",
                            algorithmName
                        ),
                        _ => throw new JsonException(
                            $"Unknown signature algorithm: {algorithmName}"
                        ),
                    };

                case JsonTokenType.Number:
                    // Handle numeric enum values
                    var numericValue = reader.GetInt32();
                    if (Enum.IsDefined(typeof(SignatureType), numericValue))
                    {
                        return (SignatureType)numericValue;
                    }
                    throw new JsonException($"Unknown SignatureType numeric value: {numericValue}");

                default:
                    throw new JsonException(
                        $"Unexpected token type {reader.TokenType} when parsing SignatureType"
                    );
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            SignatureType value,
            JsonSerializerOptions options
        )
        {
            var algorithmName = value switch
            {
                SignatureType.None => "None",
                SignatureType.X509 => "X509",
                SignatureType.PGP => "PGP",
                SignatureType.RSA_SHA256 => "SHA256withRSA", // Use standard algorithm name in output
                SignatureType.ECDSA_P256_SHA256 => "SHA256withECDSA",
                SignatureType.ECDSA_P384_SHA256 => "ECDSA_P384_SHA256",
                SignatureType.ECDSA_P521_SHA256 => "ECDSA_P521_SHA256",
                _ => throw new JsonException($"Unknown SignatureType: {value}"),
            };

            writer.WriteStringValue(algorithmName);
        }
    }
}
