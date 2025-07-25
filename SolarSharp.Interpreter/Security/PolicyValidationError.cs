using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Represents a policy validation error with an optional property path
    /// </summary>
    public sealed record PolicyValidationError(string Message, Maybe<string> PropertyPath = default)
    {
        /// <summary>
        /// Creates a validation error without a property path
        /// </summary>
        public static PolicyValidationError Create(string message) => new(message);

        /// <summary>
        /// Creates a validation error with a property path
        /// </summary>
        public static PolicyValidationError Create(string message, string propertyPath) =>
            new(message, Maybe<string>.From(propertyPath));
    }
}
