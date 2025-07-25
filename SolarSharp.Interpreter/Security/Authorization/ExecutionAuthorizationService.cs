using System;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security.Auditing;

namespace SolarSharp.Interpreter.Security.Authorization
{
    /// <summary>
    /// Default implementation of IExecutionAuthorizationService that uses the security policy resolver
    /// to perform context-aware authorization checks.
    /// </summary>
    internal sealed class ExecutionAuthorizationService : IExecutionAuthorizationService
    {
        private readonly SecurityPolicyResolver _policyResolver;
        private readonly ISecurityAuditor _auditor;

        public ExecutionAuthorizationService(
            SecurityPolicyResolver policyResolver,
            ISecurityAuditor auditor = null
        )
        {
            _policyResolver =
                policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
            _auditor = auditor ?? new SecurityAuditor();
        }

        /// <summary>
        /// Authorizes an operation in the given execution context using the appropriate security policy.
        /// </summary>
        public UnitResult<AuthorizationError> AuthorizeOperation(
            LuaExecutionContext context,
            OperationType operationType
        )
        {
            return operationType switch
            {
                OperationType.DynamicCodeExecution => AuthorizeEvalExecution(context),
                _ => UnitResult.Success<AuthorizationError>(),
            };
        }

        /// <summary>
        /// Authorizes dynamic code execution (eval operations) in the given context.
        /// This creates an eval context and checks if execution is allowed.
        /// </summary>
        public UnitResult<AuthorizationError> AuthorizeEvalExecution(LuaExecutionContext context)
        {
            // Create an eval context by appending :eval to the current context
            var evalContext = context.CreateEvalContext();

            return evalContext.Match(
                evalCtx =>
                {
                    // Resolve the policy for the eval context
                    var policyResult = _policyResolver.ResolvePolicy(evalCtx);

                    return policyResult.Match(
                        policy =>
                        {
                            // Check if execution is allowed
                            if (!policy.AllowExecution)
                            {
                                var policyName = policy.Name.GetValueOrDefault("unknown");
                                var error = AuthorizationError.DynamicCodeExecutionDenied(
                                    evalCtx.ToString(),
                                    policyName
                                );

                                // Emit security audit event
                                var deniedEvent = EvalExecutionEventFactory.CreateDenied(
                                    scriptId: Guid.NewGuid().ToString(),
                                    code: "", // Code not available at authorization time
                                    denialReason: $"Policy '{policyName}' denies dynamic code execution",
                                    denyingPolicy: policyName,
                                    trustLevel: "Untrusted",
                                    violationType: SecurityViolationType.PolicyViolation
                                );

                                _auditor.LogSecurityViolation(
                                    deniedEvent.ErrorMessage ?? deniedEvent.ToString(),
                                    SecurityEventType.EvalExecutionDenied,
                                    new UnauthorizedProcessExecutionException(error.Message, "eval")
                                );

                                return UnitResult.Failure(error);
                            }

                            // Emit successful authorization event
                            var authorizedEvent = EvalExecutionEventFactory.CreateAuthorized(
                                scriptId: Guid.NewGuid().ToString(),
                                code: "", // Code not available at authorization time
                                authorizingPolicy: policy.Name.GetValueOrDefault("unknown"),
                                trustLevel: "Untrusted" // Default trust level since SecurityPolicy doesn't have this property
                            );

                            // Log as capability usage since it's a successful authorization
                            _auditor.LogCapabilityUsage(
                                "EvalExecution",
                                "Authorize",
                                new object[] { evalCtx.ToString() },
                                authorizedEvent,
                                true
                            );

                            return UnitResult.Success<AuthorizationError>();
                        },
                        policyError =>
                            UnitResult.Failure(
                                new AuthorizationError(
                                    OperationType.DynamicCodeExecution,
                                    $"Policy resolution failed: {policyError.Message}",
                                    evalCtx.ToString(),
                                    "policy_resolution_failed"
                                )
                            )
                    );
                },
                error =>
                    UnitResult.Failure(
                        new AuthorizationError(
                            OperationType.DynamicCodeExecution,
                            $"Failed to create eval context: {error}",
                            context.ToString(),
                            "context_creation_failed"
                        )
                    )
            );
        }
    }
}
