using System.Threading.Tasks;

namespace SolarSharp.Interpreter.Security.FunctionBinding
{
    /// <summary>
    /// Default implementation of ISecurityMessageBus that integrates with the existing
    /// SecurityAuditor system. Can be extended to use actual message bus systems.
    /// </summary>
    public sealed class DefaultSecurityMessageBus : ISecurityMessageBus
    {
        private readonly ISecurityAuditor _auditor;

        public DefaultSecurityMessageBus(ISecurityAuditor auditor = null)
        {
            _auditor = auditor ?? new SecurityAuditor();
        }

        public Task LogFunctionAccessAsync(SecurityFunctionAccessEvent accessEvent)
        {
            if (accessEvent.AccessGranted)
            {
                _auditor.LogCapabilityUsage(
                    accessEvent.FunctionName,
                    "FunctionAccess",
                    new object[] { accessEvent.SourceFile, accessEvent.ContextId },
                    accessEvent,
                    true
                );
            }
            else
            {
                _auditor.LogSecurityViolation(
                    $"Function access denied: {accessEvent.FunctionName} in {accessEvent.SourceFile} - {accessEvent.DenialReason}",
                    SecurityEventType.AccessDenied,
                    new UnauthorizedProcessExecutionException(
                        accessEvent.DenialReason,
                        accessEvent.FunctionName
                    )
                );
            }

            return Task.CompletedTask;
        }

        public Task LogPolicyViolationAsync(SecurityPolicyViolationEvent violationEvent)
        {
            var eventType = violationEvent.Severity switch
            {
                SecurityViolationSeverity.Critical => SecurityEventType.PolicyViolation,
                SecurityViolationSeverity.High => SecurityEventType.PolicyViolation,
                SecurityViolationSeverity.Medium => SecurityEventType.PolicyViolation,
                SecurityViolationSeverity.Low => SecurityEventType.SuspiciousActivity,
                _ => SecurityEventType.PolicyViolation,
            };

            _auditor.LogSecurityViolation(
                $"Policy violation: {violationEvent.ViolationType} in {violationEvent.SourceFile} - {violationEvent.ViolationDetails}",
                eventType,
                new MissingCapabilityException(
                    $"{violationEvent.ViolationType}: {violationEvent.ViolationDetails}",
                    violationEvent.ViolationType
                )
            );

            return Task.CompletedTask;
        }

        public Task LogPrivilegeTransitionAsync(SecurityPrivilegeTransitionEvent transitionEvent)
        {
            var message =
                $"Privilege transition: {transitionEvent.FromContext} -> {transitionEvent.ToContext} "
                + $"({transitionEvent.FromPolicy} -> {transitionEvent.ToPolicy}) - {transitionEvent.Reason}";

            if (transitionEvent.IsEscalation)
            {
                _auditor.LogSecurityViolation(
                    $"Privilege escalation detected: {message}",
                    SecurityEventType.SuspiciousActivity,
                    new UnauthorizedProcessExecutionException(
                        $"Privilege escalation: {transitionEvent.Reason}",
                        "PrivilegeEscalation"
                    )
                );
            }
            else
            {
                _auditor.LogCapabilityUsage(
                    "PrivilegeTransition",
                    transitionEvent.TransitionType.ToString(),
                    new object[] { transitionEvent.FromContext, transitionEvent.ToContext },
                    transitionEvent,
                    true
                );
            }

            return Task.CompletedTask;
        }

        public Task LogFunctionModificationAsync(
            SecurityFunctionModificationEvent modificationEvent
        )
        {
            var message =
                $"Function modification: {modificationEvent.FunctionName} in {modificationEvent.SourceFile} "
                + $"({modificationEvent.ModificationType}) - Context: {modificationEvent.ContextId}";

            if (modificationEvent.IsGlobalModification)
            {
                // Global modifications are more sensitive
                _auditor.LogSecurityViolation(
                    $"Global function modification detected: {message}",
                    SecurityEventType.SuspiciousActivity,
                    new MetatableViolationException(
                        $"Global function modification: {modificationEvent.ModificationType}",
                        "FunctionModification"
                    )
                );
            }
            else
            {
                // Local modifications are normal but worth logging
                _auditor.LogCapabilityUsage(
                    "FunctionModification",
                    modificationEvent.ModificationType.ToString(),
                    new object[] { modificationEvent.FunctionName, modificationEvent.ContextId },
                    modificationEvent,
                    true
                );
            }

            return Task.CompletedTask;
        }
    }
}
