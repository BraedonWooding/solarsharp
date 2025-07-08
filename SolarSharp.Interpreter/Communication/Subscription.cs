using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Communication
{
    /// <summary>
    /// Immutable subscription entry
    /// </summary>
    public readonly struct Subscription
    {
        /// <summary>
        /// The subscriber's identity
        /// </summary>
        public ScriptIdentity Subscriber { get; }

        /// <summary>
        /// The handler function
        /// </summary>
        public Func<PubSubMessage, Task<Maybe<string>>> Handler { get; }

        /// <summary>
        /// Creates a new subscription
        /// </summary>
        public Subscription(
            ScriptIdentity subscriber,
            Func<PubSubMessage, Task<Maybe<string>>> handler
        )
        {
            Subscriber = subscriber;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }
    }
}
