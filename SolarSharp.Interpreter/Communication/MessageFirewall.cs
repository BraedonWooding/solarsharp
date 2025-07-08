using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Enforces message routing rules based on manifest policies using modern IdentityConstraints
    /// </summary>
    public sealed class MessageFirewall
    {
        private readonly ImmutableDictionary<ScriptIdentity, CompiledRules> _rules;

        private MessageFirewall(ImmutableDictionary<ScriptIdentity, CompiledRules> rules)
        {
            _rules = rules;
        }

        /// <summary>
        /// Creates a firewall from registered policies
        /// </summary>
        public static MessageFirewall Create(
            ImmutableDictionary<ScriptIdentity, PubSubPolicySnapshot> policies
        )
        {
            var rules = policies.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => CompileRules(kvp.Key, kvp.Value)
            );

            return new MessageFirewall(rules);
        }

        /// <summary>
        /// Gets eligible recipients for a message
        /// </summary>
        public IEnumerable<(
            ScriptIdentity Identity,
            Func<PubSubMessage, Task<Maybe<string>>> Handler
        )> GetEligibleRecipients(
            PubSubMessage message,
            ImmutableDictionary<string, ImmutableList<Subscription>> subscriptions
        )
        {
            var matchingSubscriptions = subscriptions
                .Where(kvp => TopicMatcher.Matches(kvp.Key, message.Topic))
                .SelectMany(kvp => kvp.Value);

            foreach (var subscription in matchingSubscriptions)
            {
                if (CanReceive(subscription.Subscriber, message))
                {
                    yield return (subscription.Subscriber, subscription.Handler);
                }
            }
        }

        /// <summary>
        /// Checks if a subscriber can receive a message
        /// </summary>
        public bool CanReceive(ScriptIdentity subscriber, PubSubMessage message)
        {
            if (!_rules.TryGetValue(subscriber, out var rules))
                return false;

            return rules.CanReceiveFrom(message.Topic, message.Source);
        }

        /// <summary>
        /// Checks if a script can publish to a topic
        /// </summary>
        public bool CanPublish(ScriptIdentity publisher, string topic)
        {
            if (!_rules.TryGetValue(publisher, out var rules))
                return false;

            return rules.CanPublishTo(topic);
        }

        private static CompiledRules CompileRules(
            ScriptIdentity identity,
            PubSubPolicySnapshot policy
        )
        {
            var publishPatterns = policy.PublishPatterns;
            var subscribePatterns = policy.SubscribePatterns;

            var topicRules = new Dictionary<string, CompiledTopicRule>();
            foreach (var kvp in policy.Topics)
            {
                topicRules[kvp.Key] = CompileTopicRule(kvp.Value);
            }

            return new CompiledRules(publishPatterns, subscribePatterns, topicRules);
        }

        private static CompiledTopicRule CompileTopicRule(TopicPolicy policy)
        {
            var receiveConstraints = CompileIdentityConstraintsList(policy.AllowedSenders);
            var publishConstraints = CompileIdentityConstraintsList(policy.AllowedRecipients);

            return new CompiledTopicRule(receiveConstraints, publishConstraints);
        }

        private static Func<ScriptIdentity, bool> CompileIdentityConstraintsList(
            List<IdentityConstraints> constraintsList
        )
        {
            if (constraintsList == null || constraintsList.Count == 0)
                return _ => false; // No constraints means no access

            // Compile all constraints and return true if ANY constraint matches (OR operation)
            var compiledConstraints = constraintsList.Select(CompileIdentityConstraints).ToArray();
            return identity => compiledConstraints.Any(constraint => constraint(identity));
        }

        private static Func<ScriptIdentity, bool> CompileIdentityConstraints(
            IdentityConstraints constraints
        )
        {
            if (constraints == null)
                return _ => true;

            var predicates = new List<Func<ScriptIdentity, bool>>();

            if (constraints.PublicKeyTokens.Count > 0)
            {
                var tokens = constraints.PublicKeyTokens.Select(ParseHexToken).ToList();

                predicates.Add(identity =>
                    tokens.Any(token => identity.PublicKeyToken.SequenceEqual(token))
                );
            }

            if (constraints.Names.Count > 0)
            {
                var names = constraints.Names.ToHashSet();
                predicates.Add(identity => names.Contains(identity.Name));
            }

            if (!string.IsNullOrEmpty(constraints.Version))
            {
                var version = NuGetVersion.Parse(constraints.Version);
                predicates.Add(identity => identity.Version == version);
            }

            if (!string.IsNullOrEmpty(constraints.MinVersion))
            {
                var minVersion = NuGetVersion.Parse(constraints.MinVersion);
                predicates.Add(identity => identity.Version >= minVersion);
            }

            if (!string.IsNullOrEmpty(constraints.MaxVersion))
            {
                var maxVersion = NuGetVersion.Parse(constraints.MaxVersion);
                predicates.Add(identity => identity.Version <= maxVersion);
            }

            return identity => predicates.All(p => p(identity));
        }

        private static byte[] ParseHexToken(string hex)
        {
            hex = hex.Replace("-", "").Replace(" ", "");
            if (hex.Length != 32)
                throw new ArgumentException(
                    $"Public key token must be 16 bytes (32 hex chars), got {hex.Length}"
                );

            var bytes = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// Compiled rules for efficient runtime checking
        /// </summary>
        private sealed class CompiledRules
        {
            private readonly ImmutableHashSet<string> _publishPatterns;
            private readonly ImmutableHashSet<string> _subscribePatterns;
            private readonly Dictionary<string, CompiledTopicRule> _topicRules;

            public CompiledRules(
                ImmutableHashSet<string> publishPatterns,
                ImmutableHashSet<string> subscribePatterns,
                Dictionary<string, CompiledTopicRule> topicRules
            )
            {
                _publishPatterns = publishPatterns;
                _subscribePatterns = subscribePatterns;
                _topicRules = topicRules;
            }

            public bool CanPublishTo(string topic)
            {
                return _publishPatterns.Any(pattern => TopicMatcher.Matches(pattern, topic));
            }

            public bool CanReceiveFrom(string topic, ScriptIdentity sender)
            {
                if (!_subscribePatterns.Any(pattern => TopicMatcher.Matches(pattern, topic)))
                    return false;

                var matchingRule = _topicRules
                    .Where(kvp => TopicMatcher.Matches(kvp.Key, topic))
                    .OrderByDescending(kvp => kvp.Key.Length)
                    .Select(kvp => kvp.Value)
                    .FirstOrDefault();

                if (matchingRule == null)
                    return true;

                return matchingRule.CanReceiveFrom(sender);
            }
        }

        /// <summary>
        /// Compiled rule for a specific topic
        /// </summary>
        private sealed class CompiledTopicRule
        {
            private readonly Func<ScriptIdentity, bool> _receiveConstraints;
            private readonly Func<ScriptIdentity, bool> _publishConstraints;

            public CompiledTopicRule(
                Func<ScriptIdentity, bool> receiveConstraints,
                Func<ScriptIdentity, bool> publishConstraints
            )
            {
                _receiveConstraints = receiveConstraints;
                _publishConstraints = publishConstraints;
            }

            public bool CanReceiveFrom(ScriptIdentity sender) => _receiveConstraints(sender);

            public bool CanSendTo(ScriptIdentity recipient) => _publishConstraints(recipient);
        }
    }
}
