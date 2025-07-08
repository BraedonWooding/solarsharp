using System;
using System.Reflection;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.FunctionBinding
{
    /// <summary>
    /// Validates function access based on SecurityBoundFunctionAttribute and current execution context.
    /// This is the core infrastructure for function-level security enforcement.
    /// </summary>
    public sealed class SecurityFunctionChecker
    {
        private readonly SecurityPolicyResolver _policyResolver;
        private readonly ISecurityAuditor _auditor;

        public SecurityFunctionChecker(
            SecurityPolicyResolver policyResolver,
            ISecurityAuditor auditor = null
        )
        {
            _policyResolver =
                policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
            _auditor = auditor ?? new SecurityAuditor();
        }

        /// <summary>
        /// Checks if the given function can be executed in the current execution context.
        /// Returns Success if allowed, Failure with security error if denied.
        /// </summary>
        public UnitResult<SecurityFunctionError> CheckFunctionAccess(
            MethodInfo method,
            LuaExecutionContext context,
            string functionName = null
        )
        {
            var attribute = method.GetCustomAttribute<SecurityBoundFunctionAttribute>();
            if (attribute == null)
            {
                // Function has no security requirements - allow execution
                return UnitResult.Success<SecurityFunctionError>();
            }

            functionName ??= method.Name;

            // Resolve the current security policy for this context
            var policyResult = _policyResolver.ResolvePolicy(context);
            if (policyResult.IsFailure)
            {
                var error = SecurityFunctionError.PolicyResolutionFailed(
                    functionName,
                    context.SourceFile,
                    policyResult.Error.Message
                );

                LogSecurityViolation(functionName, context, error.Message);
                return UnitResult.Failure(error);
            }

            var policy = policyResult.Value;

            // Check module permissions
            if (attribute.RequiredModule != CoreModules.None)
            {
                if (!policy.AllowedModules.HasFlag(attribute.RequiredModule))
                {
                    var error = SecurityFunctionError.ModuleAccessDenied(
                        functionName,
                        attribute.RequiredModule.ToString(),
                        context.SourceFile,
                        policy.Name.GetValueOrDefault("unknown")
                    );

                    LogSecurityViolation(functionName, context, error.Message);
                    return UnitResult.Failure(error);
                }
            }

            // Check capability permissions
            if (attribute.RequiredCapabilities != ScriptCapabilities.None)
            {
                if (!policy.Capabilities.HasFlag(attribute.RequiredCapabilities))
                {
                    var error = SecurityFunctionError.CapabilityAccessDenied(
                        functionName,
                        attribute.RequiredCapabilities.ToString(),
                        context.SourceFile,
                        policy.Name.GetValueOrDefault("unknown")
                    );

                    LogSecurityViolation(functionName, context, error.Message);
                    return UnitResult.Failure(error);
                }
            }

            // Custom policy check if specified
            if (!string.IsNullOrEmpty(attribute.CustomPolicyCheck))
            {
                var customCheckResult = ExecuteCustomPolicyCheck(
                    attribute.CustomPolicyCheck,
                    policy,
                    context,
                    functionName
                );

                if (customCheckResult.IsFailure)
                {
                    LogSecurityViolation(functionName, context, customCheckResult.Error.Message);
                    return customCheckResult;
                }
            }

            // Log successful function access
            LogFunctionAccess(functionName, context, attribute.Description, true);

            return UnitResult.Success<SecurityFunctionError>();
        }

        /// <summary>
        /// Creates a secure wrapper function that checks permissions before executing the original function.
        /// </summary>
        public Func<ScriptExecutionContext, CallbackArguments, DynValue> CreateSecureWrapper(
            MethodInfo method,
            Func<ScriptExecutionContext, CallbackArguments, DynValue> originalFunction,
            string functionName = null
        )
        {
            var attribute = method.GetCustomAttribute<SecurityBoundFunctionAttribute>();
            if (attribute == null)
            {
                // No security requirements - return original function
                return originalFunction;
            }

            functionName ??= method.Name;

            return (executionContext, args) =>
            {
                // Get current execution context
                var currentContext = ExecutionContextManager.Current;
                if (currentContext.HasNoValue)
                {
                    // No execution context available - this shouldn't happen in normal operation
                    if (attribute.ReturnNilOnDenied)
                        return DynValue.Nil;

                    throw new UnauthorizedProcessExecutionException(
                        $"Function '{functionName}' requires execution context for security validation",
                        functionName
                    );
                }

                // Check function access permissions
                var accessResult = CheckFunctionAccess(method, currentContext.Value, functionName);
                if (accessResult.IsFailure)
                {
                    if (attribute.ReturnNilOnDenied)
                        return DynValue.Nil;

                    throw new UnauthorizedProcessExecutionException(
                        accessResult.Error.Message,
                        functionName
                    );
                }

                // Execute the original function
                return originalFunction(executionContext, args);
            };
        }

        private UnitResult<SecurityFunctionError> ExecuteCustomPolicyCheck(
            string customCheckName,
            SecurityPolicy policy,
            LuaExecutionContext context,
            string functionName
        )
        {
            // For now, just return success - custom checks can be implemented later
            // This would invoke a named custom policy validation function
            return UnitResult.Success<SecurityFunctionError>();
        }

        private void LogFunctionAccess(
            string functionName,
            LuaExecutionContext context,
            string description,
            bool allowed
        )
        {
            _auditor.LogCapabilityUsage(
                functionName,
                "FunctionAccess",
                new object[] { context.SourceFile, description },
                new
                {
                    FunctionName = functionName,
                    Context = context.SourceFile,
                    Allowed = allowed,
                },
                allowed
            );
        }

        private void LogSecurityViolation(
            string functionName,
            LuaExecutionContext context,
            string reason
        )
        {
            _auditor.LogSecurityViolation(
                $"Function access denied: {functionName} in {context.SourceFile} - {reason}",
                SecurityEventType.AccessDenied,
                new UnauthorizedProcessExecutionException(reason, functionName)
            );
        }
    }
}
