using System;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.FunctionBinding
{
    /// <summary>
    /// Marks a function as requiring specific security permissions for execution.
    /// Functions with this attribute will have their permissions checked against
    /// the current execution context's security policy before execution.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class SecurityBoundFunctionAttribute : Attribute
    {
        /// <summary>
        /// The core module that must be allowed by the security policy.
        /// If None, no module permission is required.
        /// </summary>
        public CoreModules RequiredModule { get; }

        /// <summary>
        /// The script capabilities that must be allowed by the security policy.
        /// If None, no capability permission is required.
        /// </summary>
        public ScriptCapabilities RequiredCapabilities { get; }

        /// <summary>
        /// Optional custom policy check function name.
        /// If specified, this function will be called for additional validation.
        /// </summary>
        public string CustomPolicyCheck { get; }

        /// <summary>
        /// Whether this function should return nil when access is denied (true)
        /// or throw a security exception (false). Default is false (throw exception).
        /// </summary>
        public bool ReturnNilOnDenied { get; }

        /// <summary>
        /// Human-readable description of what this function does for audit logs.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Creates a security-bound function attribute.
        /// </summary>
        /// <param name="requiredModule">The core module that must be allowed</param>
        /// <param name="requiredCapabilities">The capabilities that must be allowed</param>
        /// <param name="customPolicyCheck">Optional custom policy validation function</param>
        /// <param name="returnNilOnDenied">Whether to return nil instead of throwing on denial</param>
        /// <param name="description">Human-readable description for audit logs</param>
        public SecurityBoundFunctionAttribute(
            CoreModules requiredModule = CoreModules.None,
            ScriptCapabilities requiredCapabilities = ScriptCapabilities.None,
            string customPolicyCheck = null,
            bool returnNilOnDenied = false,
            string description = null
        )
        {
            RequiredModule = requiredModule;
            RequiredCapabilities = requiredCapabilities;
            CustomPolicyCheck = customPolicyCheck;
            ReturnNilOnDenied = returnNilOnDenied;
            Description = description ?? "Unknown function";
        }
    }
}
