using System;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Exception thrown when manifest validation fails
    /// </summary>
    public class ManifestValidationException : Exception
    {
        /// <summary>
        /// Creates a new ManifestValidationException
        /// </summary>
        public ManifestValidationException(string message)
            : base(message) { }

        /// <summary>
        /// Creates a new ManifestValidationException with an inner exception
        /// </summary>
        public ManifestValidationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
