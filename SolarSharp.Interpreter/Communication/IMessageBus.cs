using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Interface for the publish/subscribe message bus
    /// </summary>
    public interface IMessageBus
    {
        /// <summary>
        /// Configuration with fixed limits
        /// </summary>
        MessageBusConfig Config { get; }

        /// <summary>
        /// Publishes a message to all eligible subscribers
        /// </summary>
        Task<UnitResult<PublishError>> PublishAsync(PubSubMessage message);

        /// <summary>
        /// Sends a request and waits for a reply
        /// </summary>
        /// <param name="message">The request message</param>
        /// <param name="timeout">Optional timeout override (cannot exceed bus default)</param>
        Task<Result<string, RequestError>> RequestAsync(
            PubSubMessage message,
            TimeSpan? timeout = null
        );

        /// <summary>
        /// Subscribes to messages matching the pattern, returns new bus instance
        /// </summary>
        IMessageBus Subscribe(
            string pattern,
            ScriptIdentity subscriber,
            Func<PubSubMessage, Task<Maybe<string>>> handler
        );

        /// <summary>
        /// Unsubscribes from a pattern, returns new bus instance
        /// </summary>
        IMessageBus Unsubscribe(string pattern, ScriptIdentity subscriber);

        /// <summary>
        /// Registers a script with its policy, returns new bus instance
        /// </summary>
        IMessageBus RegisterScript(ScriptIdentity identity, PubSubPolicySnapshot policy);

        /// <summary>
        /// Unregisters a script, returns new bus instance
        /// </summary>
        IMessageBus UnregisterScript(ScriptIdentity identity);
    }

    /// <summary>
    /// Subscription handle for unsubscribing
    /// </summary>
    public interface ISubscription : IDisposable
    {
        /// <summary>
        /// The pattern this subscription matches
        /// </summary>
        string Pattern { get; }

        /// <summary>
        /// The subscriber identity
        /// </summary>
        ScriptIdentity Subscriber { get; }

        /// <summary>
        /// Unsubscribes from the message bus
        /// </summary>
        void Unsubscribe();
    }
}
