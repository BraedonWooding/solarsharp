using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Operations
{
    /// <summary>
    /// Composes multiple security policies into a single policy with the most restrictive values
    /// </summary>
    public static class SecurityPolicyComposer
    {
        /// <summary>
        /// Composes multiple security policies into a single policy taking the most restrictive values from each
        /// </summary>
        /// <param name="policies">The policies to compose</param>
        /// <returns>A new SecurityPolicy with the most restrictive values from all input policies</returns>
        public static SecurityPolicy ComposeSecurityPolicies(params SecurityPolicy[] policies)
        {
            if (policies == null || policies.Length == 0)
            {
                // Return deny-all policy for empty input
                return new SecurityPolicy();
            }

            // Filter out null policies
            var validPolicies = policies.Where(p => p != null).ToArray();

            if (validPolicies.Length == 0)
            {
                // Return deny-all policy if all policies were null
                return new SecurityPolicy();
            }

            if (validPolicies.Length == 1)
            {
                // Single policy, return as-is
                return validPolicies[0];
            }

            // Start with the first policy and compose with the rest
            var result = validPolicies[0];

            for (var i = 1; i < validPolicies.Length; i++)
            {
                result = ComposeTwoPolicies(result, validPolicies[i]);
            }

            return result;
        }

        private static SecurityPolicy ComposeTwoPolicies(
            SecurityPolicy policy1,
            SecurityPolicy policy2
        )
        {
            return new SecurityPolicy
            {
                // Name is composed from both policies
                Name = Maybe<string>.From(
                    $"Composed({policy1.Name.GetValueOrDefault("Policy1")},{policy2.Name.GetValueOrDefault("Policy2")})"
                ),

                // For numeric limits, take the minimum non-zero value
                TimeoutMs = TakeMinimumNonZero(policy1.TimeoutMs, policy2.TimeoutMs),
                MaxMemoryMB = TakeMinimumNonZero(policy1.MaxMemoryMB, policy2.MaxMemoryMB),
                MaxInstructions = TakeMinimumNonZero(
                    policy1.MaxInstructions,
                    policy2.MaxInstructions
                ),
                MaxCallDepth = TakeMinimumNonZero(policy1.MaxCallDepth, policy2.MaxCallDepth),

                // For boolean permissions, false wins over true (most restrictive)
                AllowExecution = policy1.AllowExecution && policy2.AllowExecution,
                AllowNetworkAccess = policy1.AllowNetworkAccess && policy2.AllowNetworkAccess,
                AllowEnvironmentAccess =
                    policy1.AllowEnvironmentAccess && policy2.AllowEnvironmentAccess,
                EnableChroot = policy1.EnableChroot || policy2.EnableChroot, // More restrictive
                ThrowOnNonCriticalViolations =
                    policy1.ThrowOnNonCriticalViolations || policy2.ThrowOnNonCriticalViolations,
                PreventSignedModification =
                    policy1.PreventSignedModification || policy2.PreventSignedModification,

                // For module flags and capabilities, take the intersection (bitwise AND)
                AllowedModules = policy1.AllowedModules & policy2.AllowedModules,
                Capabilities = policy1.Capabilities & policy2.Capabilities,

                // For lists, take the intersection
                AllowedHosts = policy1
                    .AllowedHosts.Intersect(policy2.AllowedHosts)
                    .ToImmutableArray(),
                AllowedEnvironmentVariables = policy1
                    .AllowedEnvironmentVariables.Intersect(policy2.AllowedEnvironmentVariables)
                    .ToImmutableArray(),

                // For file/directory permissions, include all paths but with most restrictive access
                FilePermissions = ComposeFilePermissions(
                    policy1.FilePermissions,
                    policy2.FilePermissions
                ),
                DirectoryPermissions = ComposeDirectoryPermissions(
                    policy1.DirectoryPermissions,
                    policy2.DirectoryPermissions
                ),

                // For default permissions, take the most restrictive
                DefaultFileAccess = (FilePermissions)
                    Math.Min((int)policy1.DefaultFileAccess, (int)policy2.DefaultFileAccess),
                DefaultDirectoryAccess = (DirectoryPermissions)
                    Math.Min(
                        (int)policy1.DefaultDirectoryAccess,
                        (int)policy2.DefaultDirectoryAccess
                    ),

                // For pub/sub permissions, use the intersection
                PubSubPermissions = policy1.PubSubPermissions.IntersectWith(
                    policy2.PubSubPermissions
                ),

                // For environment emulation, compose them
                EnvironmentEmulation = ComposeEnvironmentEmulation(
                    policy1.EnvironmentEmulation,
                    policy2.EnvironmentEmulation
                ),

                // For token access, take the intersection
                AllowReadByToken = policy1.AllowReadByToken.Intersect(policy2.AllowReadByToken),
                AllowWriteByToken = policy1.AllowWriteByToken.Intersect(policy2.AllowWriteByToken),
            };
        }

        private static int TakeMinimumNonZero(int value1, int value2)
        {
            if (value1 == 0)
                return value2;
            if (value2 == 0)
                return value1;
            return Math.Min(value1, value2);
        }

        private static long TakeMinimumNonZero(long value1, long value2)
        {
            if (value1 == 0)
                return value2;
            if (value2 == 0)
                return value1;
            return Math.Min(value1, value2);
        }

        private static ImmutableDictionary<string, FilePermissions> ComposeFilePermissions(
            ImmutableDictionary<string, FilePermissions> permissions1,
            ImmutableDictionary<string, FilePermissions> permissions2
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, FilePermissions>();

            // Add all paths from both sets with the most restrictive permission
            foreach (var kvp in permissions1)
            {
                if (permissions2.TryGetValue(kvp.Key, out var otherPermission))
                {
                    // Path exists in both, take most restrictive
                    builder[kvp.Key] = (FilePermissions)
                        Math.Min((int)kvp.Value, (int)otherPermission);
                }
                else
                {
                    // Path only in first set - use None (most restrictive for intersection)
                    builder[kvp.Key] = FilePermissions.None;
                }
            }

            // Add paths that only exist in the second set
            foreach (var kvp in permissions2)
            {
                if (!permissions1.ContainsKey(kvp.Key))
                {
                    // Path only in second set - use None (most restrictive for intersection)
                    builder[kvp.Key] = FilePermissions.None;
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableDictionary<
            string,
            DirectoryPermissions
        > ComposeDirectoryPermissions(
            ImmutableDictionary<string, DirectoryPermissions> permissions1,
            ImmutableDictionary<string, DirectoryPermissions> permissions2
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<string, DirectoryPermissions>();

            // Add all paths from both sets with the most restrictive permission
            foreach (var kvp in permissions1)
            {
                if (permissions2.TryGetValue(kvp.Key, out var otherPermission))
                {
                    // Path exists in both, take most restrictive
                    builder[kvp.Key] = (DirectoryPermissions)
                        Math.Min((int)kvp.Value, (int)otherPermission);
                }
                else
                {
                    // Path only in first set - use None (most restrictive for intersection)
                    builder[kvp.Key] = DirectoryPermissions.None;
                }
            }

            // Add paths that only exist in the second set
            foreach (var kvp in permissions2)
            {
                if (!permissions1.ContainsKey(kvp.Key))
                {
                    // Path only in second set - use None (most restrictive for intersection)
                    builder[kvp.Key] = DirectoryPermissions.None;
                }
            }

            return builder.ToImmutable();
        }

        private static EnvironmentEmulationPolicy ComposeEnvironmentEmulation(
            EnvironmentEmulationPolicy policy1,
            EnvironmentEmulationPolicy policy2
        )
        {
            // Take the more restrictive mode
            var mode =
                policy1.Mode == EnvironmentMode.Isolated || policy2.Mode == EnvironmentMode.Isolated
                    ? EnvironmentMode.Isolated
                : policy1.Mode == EnvironmentMode.Sandboxed
                || policy2.Mode == EnvironmentMode.Sandboxed
                    ? EnvironmentMode.Sandboxed
                : EnvironmentMode.Passthrough;

            // Merge emulated variables, preferring values from policy1 in case of conflicts
            var mergedEmulatedVariables = new Dictionary<string, string>(
                policy2.EmulatedVariables,
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var kvp in policy1.EmulatedVariables)
            {
                mergedEmulatedVariables[kvp.Key] = kvp.Value;
            }

            return new EnvironmentEmulationPolicy
            {
                Mode = mode,
                BlockDangerousVariables =
                    policy1.BlockDangerousVariables || policy2.BlockDangerousVariables,

                // Use the more restrictive sandbox paths
                SandboxHome = policy1.SandboxHome ?? policy2.SandboxHome,
                SandboxUser = policy1.SandboxUser ?? policy2.SandboxUser,
                SandboxTemp = policy1.SandboxTemp ?? policy2.SandboxTemp,
                SandboxPath = policy1.SandboxPath ?? policy2.SandboxPath,
                WorkingDirectory = policy1.WorkingDirectory ?? policy2.WorkingDirectory,

                // Merge blocked variables (union)
                BlockedVariables = policy1
                    .BlockedVariables.Union(policy2.BlockedVariables)
                    .ToList(),

                // Merge emulated variables
                EmulatedVariables = mergedEmulatedVariables,

                // Intersect passthrough variables (more restrictive)
                PassthroughVariables = policy1
                    .PassthroughVariables.Intersect(policy2.PassthroughVariables)
                    .ToList(),

                // Intersect passthrough patterns (more restrictive)
                PassthroughPatterns = policy1
                    .PassthroughPatterns.Intersect(policy2.PassthroughPatterns)
                    .ToList(),
            };
        }
    }
}
