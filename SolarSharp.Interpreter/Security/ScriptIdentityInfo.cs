using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Identity information for a script
    /// </summary>
    public sealed record ScriptIdentityInfo
    {
        /// <summary>
        /// Script name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Script version
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = "1.0.0";

        /// <summary>
        /// Public key token for identity verification
        /// </summary>
        [JsonPropertyName("publicKeyToken")]
        public string PublicKeyToken { get; init; } = string.Empty;

        /// <summary>
        /// Full public key
        /// </summary>
        [JsonPropertyName("publicKey")]
        public string PublicKey { get; init; } = string.Empty;
    }
}
