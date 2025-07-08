using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Represents a file policy entry with pattern, selector, and policy name
    /// </summary>
    public class FilePolicyEntry
    {
        /// <summary>
        /// File pattern (e.g., "*.lua", "lib/**/*.lua")
        /// Uses Microsoft.Extensions.FileSystemGlobbing patterns
        /// </summary>
        [JsonPropertyName("pattern")]
        public string Pattern { get; set; }

        /// <summary>
        /// Optional selector for execution context
        /// - null or empty: applies to both file and eval contexts
        /// - "file": only applies to file loads
        /// - "eval": only applies to eval contexts
        /// </summary>
        [JsonPropertyName("selector")]
        public string Selector { get; set; }

        /// <summary>
        /// Name of the policy to apply (must exist in policyDefinitions)
        /// </summary>
        [JsonPropertyName("policy")]
        public string Policy { get; set; }

        /// <summary>
        /// Checks if this entry applies to file contexts
        /// </summary>
        public bool AppliesToFile
        {
            get { return string.IsNullOrEmpty(Selector) || Selector == "file"; }
        }

        /// <summary>
        /// Checks if this entry applies to eval contexts
        /// </summary>
        public bool AppliesToEval
        {
            get { return string.IsNullOrEmpty(Selector) || Selector == "eval"; }
        }
    }
}
