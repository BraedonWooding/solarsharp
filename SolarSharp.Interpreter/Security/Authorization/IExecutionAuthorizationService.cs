using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Execution;

namespace SolarSharp.Interpreter.Security.Authorization
{
    /// <summary>
    /// Service for runtime authorization of operations based on execution context and security policies.
    /// This provides context-aware authorization that separates module capability from execution permission.
    /// </summary>
    public interface IExecutionAuthorizationService
    {
        /// <summary>
        /// Authorizes an operation in the given execution context using the appropriate security policy.
        /// This performs runtime policy resolution and authorization checks.
        /// </summary>
        /// <param name="context">The current execution context</param>
        /// <param name="operationType">The type of operation requiring authorization</param>
        /// <returns>Success if authorized, failure with detailed error if denied</returns>
        UnitResult<AuthorizationError> AuthorizeOperation(
            LuaExecutionContext context,
            OperationType operationType
        );

        /// <summary>
        /// Authorizes dynamic code execution (eval operations) in the given context.
        /// This specifically handles :eval suffix patterns and eval policies.
        /// </summary>
        /// <param name="context">The current execution context</param>
        /// <returns>Success if authorized, failure with detailed error if denied</returns>
        UnitResult<AuthorizationError> AuthorizeEvalExecution(LuaExecutionContext context);
    }
}
