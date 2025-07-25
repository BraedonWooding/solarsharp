using System;

namespace SolarSharp.Interpreter.Security.FunctionBinding
{
    /// <summary>
    /// Represents security-related errors that occur during function access validation.
    /// </summary>
    public sealed class SecurityFunctionError : IEquatable<SecurityFunctionError>
    {
        public string Message { get; }
        public string FunctionName { get; }
        public string SourceFile { get; }
        public SecurityFunctionErrorType ErrorType { get; }

        private SecurityFunctionError(
            SecurityFunctionErrorType errorType,
            string message,
            string functionName,
            string sourceFile
        )
        {
            ErrorType = errorType;
            Message = message;
            FunctionName = functionName;
            SourceFile = sourceFile;
        }

        public static SecurityFunctionError ModuleAccessDenied(
            string functionName,
            string requiredModule,
            string sourceFile,
            string policyName
        ) =>
            new(
                SecurityFunctionErrorType.ModuleAccessDenied,
                $"Function '{functionName}' requires module '{requiredModule}' but policy '{policyName}' denies access",
                functionName,
                sourceFile
            );

        public static SecurityFunctionError CapabilityAccessDenied(
            string functionName,
            string requiredCapability,
            string sourceFile,
            string policyName
        ) =>
            new(
                SecurityFunctionErrorType.CapabilityAccessDenied,
                $"Function '{functionName}' requires capability '{requiredCapability}' but policy '{policyName}' denies access",
                functionName,
                sourceFile
            );

        public static SecurityFunctionError PolicyResolutionFailed(
            string functionName,
            string sourceFile,
            string reason
        ) =>
            new(
                SecurityFunctionErrorType.PolicyResolutionFailed,
                $"Failed to resolve security policy for function '{functionName}': {reason}",
                functionName,
                sourceFile
            );

        public static SecurityFunctionError CustomCheckFailed(
            string functionName,
            string sourceFile,
            string customCheckName,
            string reason
        ) =>
            new(
                SecurityFunctionErrorType.CustomCheckFailed,
                $"Custom security check '{customCheckName}' failed for function '{functionName}': {reason}",
                functionName,
                sourceFile
            );

        public bool Equals(SecurityFunctionError other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Message == other.Message
                && FunctionName == other.FunctionName
                && SourceFile == other.SourceFile
                && ErrorType == other.ErrorType;
        }

        public override bool Equals(object obj) =>
            obj is SecurityFunctionError other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Message, FunctionName, SourceFile, (int)ErrorType);

        public override string ToString() => Message;
    }

    public enum SecurityFunctionErrorType
    {
        ModuleAccessDenied,
        CapabilityAccessDenied,
        PolicyResolutionFailed,
        CustomCheckFailed,
    }
}
