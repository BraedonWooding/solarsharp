using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents capability restrictions in a manifest policy.
    /// Can express patterns like "deny Eval" or "deny all except Eval and FileRead".
    /// </summary>
    public sealed record CapabilityRestriction
    {
        private readonly RestrictionSet<ScriptCapabilities> _restriction;

        /// <summary>
        /// Private constructor ensures creation through factory methods.
        /// </summary>
        private CapabilityRestriction(RestrictionSet<ScriptCapabilities> restriction)
        {
            _restriction = restriction ?? RestrictionSet<ScriptCapabilities>.None;
        }

        /// <summary>
        /// Creates an empty restriction (no capabilities are restricted).
        /// </summary>
        public static CapabilityRestriction None { get; } = new(RestrictionSet<ScriptCapabilities>.None);

        /// <summary>
        /// Creates a restriction that denies all capabilities.
        /// </summary>
        public static CapabilityRestriction DenyAll { get; } = new(RestrictionSet<ScriptCapabilities>.All);

        /// <summary>
        /// Creates a restriction that denies specific capabilities.
        /// </summary>
        public static CapabilityRestriction DenyCapabilities(params ScriptCapabilities[] capabilities)
        {
            if (capabilities == null || capabilities.Length == 0)
                return None;

            // Expand flags to individual capabilities
            var expandedCapabilities = capabilities.SelectMany(ExpandCapabilityFlags).Distinct();
            return new CapabilityRestriction(RestrictionSet<ScriptCapabilities>.DenySpecific(expandedCapabilities));
        }

        /// <summary>
        /// Creates a restriction that denies all capabilities except those specified.
        /// </summary>
        public static CapabilityRestriction DenyAllExcept(params ScriptCapabilities[] allowedCapabilities)
        {
            if (allowedCapabilities == null || allowedCapabilities.Length == 0)
                return DenyAll;

            // Expand flags to individual capabilities
            var expandedCapabilities = allowedCapabilities.SelectMany(ExpandCapabilityFlags).Distinct();
            return new CapabilityRestriction(RestrictionSet<ScriptCapabilities>.DenyAllExcept(expandedCapabilities));
        }

        /// <summary>
        /// Creates a CapabilityRestriction from string representations.
        /// </summary>
        public static Result<CapabilityRestriction, string> Create(bool denyAll, IEnumerable<string> capabilityNames)
        {
            if (capabilityNames == null)
                return Result.Success<CapabilityRestriction, string>(None);

            var capabilityList = capabilityNames.ToList();
            if (!capabilityList.Any())
            {
                return Result.Success<CapabilityRestriction, string>(denyAll ? DenyAll : None);
            }

            // Parse capability names
            var parsedCapabilities = new List<ScriptCapabilities>();
            var errors = new List<string>();

            foreach (var capabilityName in capabilityList)
            {
                if (string.IsNullOrWhiteSpace(capabilityName))
                    continue;

                var mappedCapability = MapCapabilityName(capabilityName);
                if (mappedCapability.HasValue)
                {
                    parsedCapabilities.Add(mappedCapability.Value);
                }
                else
                {
                    errors.Add($"Unknown capability: '{capabilityName}'");
                }
            }

            if (errors.Any())
                return Result.Failure<CapabilityRestriction, string>(string.Join("; ", errors));

            return Result.Success<CapabilityRestriction, string>(
                denyAll ? DenyAllExcept(parsedCapabilities.ToArray()) : DenyCapabilities(parsedCapabilities.ToArray()));
        }

        /// <summary>
        /// Maps capability names to ScriptCapabilities enum values.
        /// Supports both direct enum names and Lua-specific aliases.
        /// </summary>
        private static ScriptCapabilities? MapCapabilityName(string capabilityName)
        {
            if (string.IsNullOrWhiteSpace(capabilityName))
                return null;

            var normalizedName = capabilityName.ToLowerInvariant();

            // Direct enum name mapping (case-insensitive)
            if (Enum.TryParse<ScriptCapabilities>(capabilityName, true, out var capability))
                return capability;

            // Lua-specific capability aliases
            return normalizedName switch
            {
                "eval" => ScriptCapabilities.ProcessExecution,
                "load" => ScriptCapabilities.ProcessExecution, 
                "loadstring" => ScriptCapabilities.ProcessExecution,
                "dofile" => ScriptCapabilities.ProcessExecution,
                "io" => ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
                "fileread" => ScriptCapabilities.FileRead,
                "filewrite" => ScriptCapabilities.FileWrite,
                "filedelete" => ScriptCapabilities.FileDelete,
                "network" => ScriptCapabilities.NetworkAccess,
                "networkaccess" => ScriptCapabilities.NetworkAccess,
                "system" => ScriptCapabilities.SystemInformation,
                "systeminformation" => ScriptCapabilities.SystemInformation,
                "processexecution" => ScriptCapabilities.ProcessExecution,
                "reflection" => ScriptCapabilities.ReflectionAccess,
                "reflectionaccess" => ScriptCapabilities.ReflectionAccess,
                "nativeinterop" => ScriptCapabilities.NativeInterop,
                "directoryoperations" => ScriptCapabilities.DirectoryOperations,
                "environment" => ScriptCapabilities.EnvironmentAccess,
                "environmentaccess" => ScriptCapabilities.EnvironmentAccess,
                _ => null
            };
        }

        /// <summary>
        /// Checks if a capability is restricted.
        /// </summary>
        public bool IsRestricted(ScriptCapabilities capability)
        {
            // Check each individual flag in the capability
            var individualCapabilities = ExpandCapabilityFlags(capability);
            return individualCapabilities.Any(c => _restriction.IsDenied(c));
        }

        /// <summary>
        /// Checks if a capability is allowed.
        /// </summary>
        public bool IsAllowed(ScriptCapabilities capability) => !IsRestricted(capability);

        /// <summary>
        /// Gets the effective allowed capabilities when combined with a base set.
        /// </summary>
        public ScriptCapabilities GetEffectiveAllowedCapabilities(ScriptCapabilities baseCapabilities)
        {
            var result = ScriptCapabilities.None;
            
            // Check each possible capability
            foreach (ScriptCapabilities capability in Enum.GetValues(typeof(ScriptCapabilities)))
            {
                // Skip combined flags and None
                if (capability == ScriptCapabilities.None || HasMultipleBits(capability))
                    continue;

                // Capability is allowed if it's in base set AND not restricted
                if (baseCapabilities.HasFlag(capability) && !_restriction.IsDenied(capability))
                {
                    result |= capability;
                }
            }

            return result;
        }

        /// <summary>
        /// Combines this restriction with another, taking the most restrictive combination.
        /// </summary>
        public CapabilityRestriction CombineWith(CapabilityRestriction other)
        {
            if (other == null)
                return this;

            return new CapabilityRestriction(_restriction.CombineWith(other._restriction));
        }

        /// <summary>
        /// Converts to a JSON-friendly representation.
        /// </summary>
        public (bool DenyAll, ImmutableArray<string> Capabilities) ToJsonPattern()
        {
            var (denyAll, capabilities) = _restriction.ToJsonPattern();
            var capabilityNames = capabilities.Select(c => c.ToString()).ToImmutableArray();
            return (denyAll, capabilityNames);
        }

        /// <summary>
        /// Gets whether this restriction denies all capabilities.
        /// </summary>
        public bool DeniesAll => _restriction.DeniesEverything;

        /// <summary>
        /// Gets whether this restriction denies no capabilities.
        /// </summary>
        public bool DeniesNone => _restriction.DeniesNothing;

        public override string ToString() => _restriction.ToString();

        /// <summary>
        /// Expands capability flags to individual capabilities.
        /// </summary>
        private static IEnumerable<ScriptCapabilities> ExpandCapabilityFlags(ScriptCapabilities capabilities)
        {
            // Handle special cases
            if (capabilities == ScriptCapabilities.None)
                yield break;

            if (capabilities == ScriptCapabilities.All)
            {
                // Return all individual capabilities
                foreach (ScriptCapabilities capability in Enum.GetValues(typeof(ScriptCapabilities)))
                {
                    if (capability != ScriptCapabilities.None && capability != ScriptCapabilities.All && !HasMultipleBits(capability))
                        yield return capability;
                }
                yield break;
            }

            // Check each individual flag
            foreach (ScriptCapabilities capability in Enum.GetValues(typeof(ScriptCapabilities)))
            {
                if (capability is ScriptCapabilities.None or ScriptCapabilities.All)
                    continue;

                if (!HasMultipleBits(capability) && capabilities.HasFlag(capability))
                    yield return capability;
            }
        }

        /// <summary>
        /// Checks if an enum value has multiple bits set.
        /// </summary>
        private static bool HasMultipleBits(ScriptCapabilities capability)
        {
            int value = (int)capability;
            return value != 0 && (value & (value - 1)) != 0;
        }
    }

    /// <summary>
    /// Fluent builder extensions for CapabilityRestriction.
    /// </summary>
    public static class CapabilityRestrictionExtensions
    {
        /// <summary>
        /// Creates a capability restriction that denies process execution.
        /// </summary>
        public static CapabilityRestriction DenyProcessExecution(this CapabilityRestriction _) =>
            CapabilityRestriction.DenyCapabilities(ScriptCapabilities.ProcessExecution);

        /// <summary>
        /// Creates a capability restriction that denies all capabilities except file operations.
        /// </summary>
        public static CapabilityRestriction DenyAllExceptFileOperations(this CapabilityRestriction _) =>
            CapabilityRestriction.DenyAllExcept(ScriptCapabilities.FileRead, ScriptCapabilities.FileWrite);

        /// <summary>
        /// Creates a capability restriction that denies dangerous capabilities (ProcessExecution, NativeInterop, ReflectionAccess).
        /// </summary>
        public static CapabilityRestriction DenyDangerousCapabilities(this CapabilityRestriction _) =>
            CapabilityRestriction.DenyCapabilities(ScriptCapabilities.ProcessExecution, ScriptCapabilities.NativeInterop, ScriptCapabilities.ReflectionAccess);

        /// <summary>
        /// Creates a capability restriction that allows only file operations.
        /// </summary>
        public static CapabilityRestriction AllowOnlyFileOperations(this CapabilityRestriction _) =>
            CapabilityRestriction.DenyAllExcept(
                ScriptCapabilities.FileRead, 
                ScriptCapabilities.FileWrite);
    }
}