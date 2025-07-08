using System.Collections.Generic;
using System.Text.Json.Serialization;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Policy for a specific topic or pattern
    /// </summary>
    public class TopicPolicy
    {
        /// <summary>
        /// Constraints on who can receive when we publish to this topic (outbound constraints)
        /// </summary>
        [JsonPropertyName("allowedRecipients")]
        public List<IdentityConstraints> AllowedRecipients { get; set; } =
            new List<IdentityConstraints>();

        /// <summary>
        /// Constraints on who can send to this topic when we subscribe (inbound constraints)
        /// </summary>
        [JsonPropertyName("allowedSenders")]
        public List<IdentityConstraints> AllowedSenders { get; set; } =
            new List<IdentityConstraints>();

        /// <summary>
        /// Additional constraints like message size, rate limits, etc.
        /// </summary>
        [JsonPropertyName("constraints")]
        public Dictionary<string, object> Constraints { get; set; } =
            new Dictionary<string, object>();
    }
}
