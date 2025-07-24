using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Converts manifest policies directly to SecurityPolicy.
    /// Handles the new domain-driven restriction model.
    /// </summary>
    public static class ManifestPolicyConverter
    {
        /// <summary>
        /// Converts a manifest to a SecurityPolicy by processing ALL its policies.
        /// </summary>
        public static Result<SecurityPolicy, string> ConvertToSecurityPolicy(Manifest manifest, string manifestDirectory)
        {
            try
            {
                // Get all policies from the manifest
                var policies = manifest
                    .GetAllPackages()
                    .SelectMany(p => manifest.GetPoliciesForPackage(p.PackageId))
                    .ToList();

                if (!policies.Any())
                {
                    // Manifest with no policies means no restrictions - return unrestricted policy
                    return Result.Success<SecurityPolicy, string>(new SecurityPolicy
                    {
                        AllowExecution = true,
                        AllowedModules = CoreModules.Preset_Complete,
                        Capabilities = ScriptCapabilities.All,
                        DefaultFileAccess = FilePermissions.ReadWrite,
                        AllowNetworkAccess = true,
                        TimeoutMs = -1,      // Unlimited
                        MaxMemoryMB = -1,    // Unlimited  
                        MaxInstructions = -1, // Unlimited
                        MaxCallDepth = -1,   // Unlimited
                        MaxTables = -1       // Unlimited
                    });
                }

                // Convert all policies to domain types first
                var domainPolicies = new List<ManifestPolicyDomain>();
                foreach (var dto in policies)
                {
                    var domainResult = ManifestPolicyMapper.ToDomain(dto);
                    if (domainResult.IsFailure)
                        return Result.Failure<SecurityPolicy, string>(domainResult.Error);
                    domainPolicies.Add(domainResult.Value);
                }

                // Start with reasonable defaults - manifests only restrict, never grant
                // When DenyAll = false, we should allow execution with unlimited resources
                var policy = new SecurityPolicy
                {
                    AllowExecution = true,
                    TimeoutMs = -1,      // Unlimited by default
                    MaxMemoryMB = -1,    // Unlimited by default  
                    MaxInstructions = -1, // Unlimited by default
                    MaxCallDepth = -1,   // Unlimited by default
                    MaxTables = -1       // Unlimited by default
                };

                // Apply all policies - manifests can only restrict, not grant
                foreach (var manifestPolicy in domainPolicies)
                {
                    // Apply deny-all first
                    if (manifestPolicy.DenyAll)
                    {
                        // Deny all access
                        policy = policy with 
                        { 
                            AllowedModules = CoreModules.None,
                            Capabilities = ScriptCapabilities.None,
                            DefaultFileAccess = FilePermissions.None,
                            AllowNetworkAccess = false
                        };
                    }

                    // Apply module restrictions
                    if (!manifestPolicy.ModuleRestrictions.DeniesNone)
                    {
                        var effectiveModules = manifestPolicy.ModuleRestrictions
                            .GetEffectiveAllowedModules(policy.AllowedModules);
                        policy = policy with { AllowedModules = effectiveModules };
                    }

                    // Apply capability restrictions
                    if (!manifestPolicy.CapabilityRestrictions.DeniesNone)
                    {
                        var effectiveCapabilities = manifestPolicy.CapabilityRestrictions
                            .GetEffectiveAllowedCapabilities(policy.Capabilities);
                        policy = policy with { Capabilities = effectiveCapabilities };
                        
                        // Update file access based on capability restrictions
                        if (manifestPolicy.CapabilityRestrictions.IsRestricted(ScriptCapabilities.FileWrite))
                        {
                            // If write is restricted, downgrade to read-only at most
                            if (policy.DefaultFileAccess is FilePermissions.ReadWrite or FilePermissions.SandboxedReadWrite)
                            {
                                policy = policy with { DefaultFileAccess = FilePermissions.Read };
                            }
                        }
                        if (manifestPolicy.CapabilityRestrictions.IsRestricted(ScriptCapabilities.FileRead))
                        {
                            // If read is also restricted, no file access
                            policy = policy with { DefaultFileAccess = FilePermissions.None };
                        }
                        if (manifestPolicy.CapabilityRestrictions.IsRestricted(ScriptCapabilities.NetworkAccess))
                        {
                            policy = policy with { AllowNetworkAccess = false };
                        }
                        
                        // If eval-related capabilities are restricted, disable execution
                        if (manifestPolicy.CapabilityRestrictions.IsRestricted(ScriptCapabilities.ProcessExecution))
                        {
                            policy = policy with { AllowExecution = false };
                        }
                    }

                    // Apply memory restriction (take minimum, -1 means unlimited)
                    manifestPolicy.MaxMemory.Execute(memSize =>
                    {
                        var newMemoryMB = memSize.Megabytes;
                        if (policy.MaxMemoryMB == -1)
                        {
                            // Currently unlimited, apply the restriction
                            policy = policy with { MaxMemoryMB = newMemoryMB };
                        }
                        else if (newMemoryMB < policy.MaxMemoryMB)
                        {
                            // Take the more restrictive value
                            policy = policy with { MaxMemoryMB = newMemoryMB };
                        }
                    });

                    // Apply timeout restriction (take minimum, -1 means unlimited)
                    manifestPolicy.Timeout.Execute(timeout =>
                    {
                        var newTimeoutMs = timeout.Milliseconds;
                        if (policy.TimeoutMs == -1)
                        {
                            // Currently unlimited, apply the restriction
                            policy = policy with { TimeoutMs = newTimeoutMs };
                        }
                        else if (newTimeoutMs < policy.TimeoutMs)
                        {
                            // Take the more restrictive value
                            policy = policy with { TimeoutMs = newTimeoutMs };
                        }
                    });

                    // Apply path restrictions
                    if (!manifestPolicy.PathRestrictions.DeniesNone)
                    {
                        // Scope all path restrictions to the manifest directory
                        var scopedPaths = manifestPolicy.PathRestrictions.ScopeToDirectory(manifestDirectory);
                        
                        // Apply path restrictions to the policy
                        policy = policy with { PathRestrictions = scopedPaths };
                    }

                    // Apply host restrictions
                    if (!manifestPolicy.HostRestrictions.DeniesNone)
                    {
                        // Apply host restrictions to the policy (for runtime checking)
                        policy = policy with { HostRestrictions = manifestPolicy.HostRestrictions };
                        
                        // Also convert to allowed hosts if possible for legacy compatibility
                        var allowedHosts = manifestPolicy.HostRestrictions.ToAllowedHosts();
                        allowedHosts.Execute(hosts =>
                        {
                            policy = policy with { AllowedHosts = hosts };
                        });
                    }
                }

                return Result.Success<SecurityPolicy, string>(policy);
            }
            catch (Exception ex)
            {
                return Result.Failure<SecurityPolicy, string>($"Failed to convert manifest to policy: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a ManifestPolicyContribution that represents the restrictions from a manifest.
        /// </summary>
        public static Result<ManifestPolicyContribution, string> CreateContribution(
            Manifest manifest, 
            string manifestDirectory)
        {
            try
            {
                // Get all policies from the manifest
                var policies = manifest
                    .GetAllPackages()
                    .SelectMany(p => manifest.GetPoliciesForPackage(p.PackageId))
                    .ToList();

                if (!policies.Any())
                {
                    // Manifest with no policies means no restrictions - return empty contribution
                    return Result.Success<ManifestPolicyContribution, string>(
                        new ManifestPolicyContribution 
                        { 
                            ManifestId = manifest.ManifestId,
                            Directory = manifestDirectory,
                            Policies = ImmutableArray<ManifestPolicyDomain>.Empty,
                            SigningKeys = manifest.GetAllSigningKeys().ToImmutableArray()
                        });
                }

                // Convert all policies to domain types
                var domainPolicies = new List<ManifestPolicyDomain>();
                foreach (var dto in policies)
                {
                    var domainResult = ManifestPolicyMapper.ToDomain(dto);
                    if (domainResult.IsFailure)
                        return Result.Failure<ManifestPolicyContribution, string>(domainResult.Error);
                    domainPolicies.Add(domainResult.Value);
                }

                // Create contribution
                return Result.Success<ManifestPolicyContribution, string>(new ManifestPolicyContribution
                {
                    ManifestId = manifest.ManifestId,
                    Directory = manifestDirectory,
                    Policies = domainPolicies.ToImmutableArray(),
                    SigningKeys = manifest.GetAllSigningKeys()
                });
            }
            catch (Exception ex)
            {
                return Result.Failure<ManifestPolicyContribution, string>(
                    $"Failed to create manifest contribution: {ex.Message}");
            }
        }

    }
}