using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Pub/sub permissions configuration
    /// </summary>
    public record PubSubPermissions
    {
        /// <summary>
        /// Topics this script is allowed to publish to
        /// </summary>
        [JsonPropertyName("publish")]
        public ImmutableArray<string> Publish { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Topics this script is allowed to subscribe to
        /// </summary>
        [JsonPropertyName("subscribe")]
        public ImmutableArray<string> Subscribe { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Topic-specific policies
        /// </summary>
        [JsonPropertyName("topics")]
        public ImmutableDictionary<string, TopicPolicy> Topics { get; init; } =
            ImmutableDictionary<string, TopicPolicy>.Empty;

        /// <summary>
        /// Creates intersection (most restrictive) of two permission sets
        /// </summary>
        public PubSubPermissions IntersectWith(PubSubPermissions other)
        {
            if (other == null)
                return this;

            // Intersection of publish topics
            var publishIntersection = Publish.Intersect(other.Publish).ToImmutableArray();

            // Intersection of subscribe topics
            var subscribeIntersection = Subscribe.Intersect(other.Subscribe).ToImmutableArray();

            // For topics, we need to merge and take most restrictive
            var topicsBuilder = ImmutableDictionary.CreateBuilder<string, TopicPolicy>();

            // Only include topics that exist in both
            foreach (var kvp in Topics)
            {
                if (other.Topics.TryGetValue(kvp.Key, out var otherPolicy))
                {
                    // TODO: Implement TopicPolicy intersection
                    topicsBuilder[kvp.Key] = kvp.Value;
                }
            }

            return this with
            {
                Publish = publishIntersection,
                Subscribe = subscribeIntersection,
                Topics = topicsBuilder.ToImmutable(),
            };
        }
    }
}
