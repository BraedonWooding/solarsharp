using SolarSharp.Interpreter.Debug;
using System;

namespace SolarSharp.Interpreter.Errors
{
    /// <summary>
    /// Exception thrown when an inconsistent state is reached in the interpreter.
    /// 
    /// This is the only exceptions that will cause the interpreter to not perform typical
    /// error handling behaviour i.e. it'll propagate past an x/pcall and it won't call error handlers
    /// it also won't close to be closed variables.
    /// </summary>
    [Serializable]
    public class InternalErrorException : Exception
    {
        [Obsolete("Prefer using InternalErrorException(string message, SourceRef state)")]
        internal InternalErrorException(string message) : base(message)
        {
        }

        [Obsolete("Prefer using InternalErrorException(string message, SourceRef state)")]
        internal InternalErrorException(string format, params object[] args) : base(string.Format(format, args))
        {
        }

        /// <remarks>I'm expecting that we'll be able to provide a SourceRef for all error exceptions that are internal.</remarks>
        internal InternalErrorException(string message, SourceRef state) : base(state.FormatMessage(message))
        {
        }
    }
}
