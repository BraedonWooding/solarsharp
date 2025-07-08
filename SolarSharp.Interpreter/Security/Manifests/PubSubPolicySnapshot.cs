using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Snapshot of a script's publish/subscribe policy
    /// </summary>
    public sealed class PubSubPolicySnapshot
    {
        /// <summary>
        /// Topics with their constraints
        /// </summary>
        public ImmutableDictionary<string, TopicPolicy> Topics { get; }

        /// <summary>
        /// Patterns this script can publish to
        /// </summary>
        public ImmutableHashSet<string> PublishPatterns { get; }

        /// <summary>
        /// Patterns this script subscribes to
        /// </summary>
        public ImmutableHashSet<string> SubscribePatterns { get; }

        private PubSubPolicySnapshot(
            ImmutableDictionary<string, TopicPolicy> topics,
            ImmutableHashSet<string> publishPatterns,
            ImmutableHashSet<string> subscribePatterns
        )
        {
            Topics = topics;
            PublishPatterns = publishPatterns;
            SubscribePatterns = subscribePatterns;
        }

        /// <summary>
        /// Creates a policy snapshot from a configuration
        /// </summary>
        public static PubSubPolicySnapshot Create(PubSubPermissions mutablePolicy)
        {
            if (mutablePolicy == null)
            {
                return new PubSubPolicySnapshot(
                    ImmutableDictionary<string, TopicPolicy>.Empty,
                    ImmutableHashSet<string>.Empty,
                    ImmutableHashSet<string>.Empty
                );
            }

            var topics = mutablePolicy.Topics ?? ImmutableDictionary<string, TopicPolicy>.Empty;

            var publishPatterns = mutablePolicy.Publish.IsDefaultOrEmpty
                ? ImmutableHashSet<string>.Empty
                : mutablePolicy.Publish.ToImmutableHashSet();

            var subscribePatterns = mutablePolicy.Subscribe.IsDefaultOrEmpty
                ? ImmutableHashSet<string>.Empty
                : mutablePolicy.Subscribe.ToImmutableHashSet();

            // Validate that all publish/subscribe patterns have corresponding topic definitions
            ValidatePatterns(publishPatterns, subscribePatterns, topics.Keys);

            return new PubSubPolicySnapshot(topics, publishPatterns, subscribePatterns);
        }

        /// <summary>
        /// Empty policy with no permissions
        /// </summary>
        public static PubSubPolicySnapshot Empty { get; } =
            new PubSubPolicySnapshot(
                ImmutableDictionary<string, TopicPolicy>.Empty,
                ImmutableHashSet<string>.Empty,
                ImmutableHashSet<string>.Empty
            );

        /// <summary>
        /// Checks if this policy allows publishing to a topic
        /// </summary>
        public bool CanPublish(string topic)
        {
            return PublishPatterns.Any(pattern => TopicMatcher.Matches(pattern, topic));
        }

        /// <summary>
        /// Checks if this policy allows subscribing to a topic
        /// </summary>
        public bool CanSubscribe(string topic)
        {
            return SubscribePatterns.Any(pattern => TopicMatcher.Matches(pattern, topic));
        }

        /// <summary>
        /// Gets the policy for a specific topic
        /// </summary>
        public TopicPolicy GetTopicPolicy(string topic)
        {
            // Find the most specific matching pattern
            var matchingKey = Topics
                .Keys.Where(pattern => TopicMatcher.Matches(pattern, topic))
                .OrderByDescending(pattern => pattern.Length)
                .FirstOrDefault();

            return matchingKey != null ? Topics[matchingKey] : null;
        }

        private static void ValidatePatterns(
            ImmutableHashSet<string> publishPatterns,
            ImmutableHashSet<string> subscribePatterns,
            IEnumerable<string> definedTopics
        )
        {
            var allPatterns = publishPatterns.Union(subscribePatterns);
            var topicList = definedTopics.ToList();

            foreach (var pattern in allPatterns)
            {
                if (
                    !topicList.Any(topic =>
                        TopicMatcher.Matches(topic, pattern) || TopicMatcher.Matches(pattern, topic)
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Pattern '{pattern}' has no corresponding topic definition in manifest"
                    );
                }
            }
        }
    }

    /// <summary>
    /// Helper for matching topic patterns
    /// </summary>
    public static class TopicMatcher
    {
        /// <summary>
        /// Checks if a pattern matches a topic
        /// </summary>
        public static bool Matches(string pattern, string topic)
        {
            if (pattern == topic)
                return true;

            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return topic.StartsWith(prefix);
            }

            return false;
        }
    }
}
