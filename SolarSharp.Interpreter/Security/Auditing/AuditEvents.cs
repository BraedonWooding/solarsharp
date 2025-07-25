// ReSharper disable once CheckNamespace

using SolarSharp.Interpreter.Security.Auditing;

namespace SolarSharp.Interpreter.Security.Auditing
{
    // This file provides a convenient way to import all audit events with a single using statement
    // Usage: using SolarSharp.Interpreter.Security.Auditing;

    // Re-export all audit event types for convenience

    // Re-export enums

    // Re-export factory classes

    // Re-export extension methods
}

/// <summary>
/// Provides summary information about the security audit events system
/// </summary>
public static class SecurityAuditEventsSummary
{
    /// <summary>
    /// Gets the version of the security audit events system
    /// </summary>
    public static string Version => "1.0.0";

    /// <summary>
    /// Gets the available audit event types
    /// </summary>
    public static readonly string[] AvailableEventTypes =
    {
        nameof(EvalExecutionAuthorizedEvent),
        nameof(EvalExecutionDeniedEvent),
        nameof(ManifestValidatedEvent),
        nameof(ManifestValidationFailedEvent),
        nameof(TrustStoreModifiedEvent),
    };

    /// <summary>
    /// Gets the factory classes available for creating events
    /// </summary>
    public static readonly string[] AvailableFactories =
    {
        nameof(EvalExecutionEventFactory),
        nameof(ManifestValidationEventFactory),
        nameof(TrustStoreEventFactory),
    };

    /// <summary>
    /// Gets usage examples for the audit events system
    /// </summary>
    public static readonly string[] UsageExamples =
    {
        "// Create an eval execution authorized event",
        "var authorizedEvent = EvalExecutionEventFactory.CreateAuthorized(scriptId, code, policy, trustLevel);",
        "",
        "// Create a manifest validation failed event",
        "var failedEvent = ManifestValidationEventFactory.CreateFailed(scriptId, path, reason, failureType, duration);",
        "",
        "// Create a trust store key added event",
        "var keyAddedEvent = TrustStoreEventFactory.CreateKeyAdded(scriptId, store, fingerprint, trustLevel, modifiedBy, reason);",
        "",
        "// Convert to legacy format if needed",
        "var legacyEvent = authorizedEvent.ToLegacyEvent();",
        "",
        "// Filter events by success status",
        "var successfulEvents = events.WhereSuccessful();",
        "var failedEvents = events.WhereFailures();",
    };
}
