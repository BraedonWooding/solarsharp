using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Maps between JSON-serializable ManifestPolicy DTOs and domain types.
    /// This keeps our JSON format stable while allowing rich domain modeling internally.
    /// </summary>
    public static class ManifestPolicyMapper
    {
        /// <summary>
        /// Converts a ManifestPolicy DTO to domain restriction types.
        /// </summary>
        public static Result<ManifestPolicyDomain, string> ToDomain(ManifestPolicy dto)
        {
            if (dto == null)
                return Result.Failure<ManifestPolicyDomain, string>("ManifestPolicy cannot be null");

            var errors = new List<string>();

            // Parse memory size
            var memorySize = string.IsNullOrWhiteSpace(dto.MaxMemory)
                ? Maybe<MemorySize>.None
                : MemorySize.Parse(dto.MaxMemory)
                    .Match(
                        success => Maybe<MemorySize>.From(success),
                        error => { errors.Add($"Invalid max-memory: {error}"); return Maybe<MemorySize>.None; }
                    );

            // Parse timeout
            var timeout = string.IsNullOrWhiteSpace(dto.Timeout)
                ? Maybe<TimeoutDuration>.None
                : TimeoutDuration.Parse(dto.Timeout)
                    .Match(
                        success => Maybe<TimeoutDuration>.From(success),
                        error => { errors.Add($"Invalid timeout: {error}"); return Maybe<TimeoutDuration>.None; }
                    );

            // Parse module restrictions
            var moduleRestriction = ModuleRestriction.Create(
                dto.Modules.DenyAll,
                dto.Modules.Modules)
                .Match(
                    success => success,
                    error => { errors.Add($"Invalid modules: {error}"); return ModuleRestriction.None; }
                );

            // Parse capability restrictions
            var capabilityRestriction = CapabilityRestriction.Create(
                dto.Capabilities.DenyAll,
                dto.Capabilities.Capabilities)
                .Match(
                    success => success,
                    error => { errors.Add($"Invalid capabilities: {error}"); return CapabilityRestriction.None; }
                );

            // Parse path restrictions
            var pathRestriction = PathRestriction.Create(
                dto.Paths.DenyAll,
                dto.Paths.Patterns)
                .Match(
                    success => success,
                    error => { errors.Add($"Invalid paths: {error}"); return PathRestriction.None; }
                );

            // Parse host restrictions
            var hostRestriction = HostRestriction.Create(
                dto.Hosts.DenyAll,
                dto.Hosts.Patterns)
                .Match(
                    success => success,
                    error => { errors.Add($"Invalid hosts: {error}"); return HostRestriction.None; }
                );

            if (errors.Any())
                return Result.Failure<ManifestPolicyDomain, string>(string.Join("; ", errors));

            return Result.Success<ManifestPolicyDomain, string>(new ManifestPolicyDomain
            {
                Packages = dto.Packages,
                Selector = dto.Selector,
                MaxMemory = memorySize,
                Timeout = timeout,
                ModuleRestrictions = moduleRestriction,
                CapabilityRestrictions = capabilityRestriction,
                PathRestrictions = pathRestriction,
                HostRestrictions = hostRestriction,
                DenyAll = dto.DenyAll,
                InheritFromFile = dto.InheritFromFile
            });
        }

        /// <summary>
        /// Converts domain types back to JSON-serializable DTO.
        /// </summary>
        public static ManifestPolicy ToDto(ManifestPolicyDomain domain)
        {
            if (domain == null)
                throw new ArgumentNullException(nameof(domain));

            var (modulesDenyAll, modules) = domain.ModuleRestrictions.ToJsonPattern();
            var (capabilitiesDenyAll, capabilities) = domain.CapabilityRestrictions.ToJsonPattern();
            var (pathsDenyAll, paths) = domain.PathRestrictions.ToJsonPattern();
            var (hostsDenyAll, hosts) = domain.HostRestrictions.ToJsonPattern();

            return new ManifestPolicy
            {
                Packages = domain.Packages,
                Selector = domain.Selector,
                MaxMemory = domain.MaxMemory.Match(m => m.ToString(), () => ""),
                Timeout = domain.Timeout.Match(t => t.ToString(), () => ""),
                Modules = new ManifestModuleRestriction
                {
                    DenyAll = modulesDenyAll,
                    Modules = modules
                },
                Capabilities = new ManifestCapabilityRestriction
                {
                    DenyAll = capabilitiesDenyAll,
                    Capabilities = capabilities
                },
                Paths = new ManifestPathRestriction
                {
                    DenyAll = pathsDenyAll,
                    Patterns = paths
                },
                Hosts = new ManifestHostRestriction
                {
                    DenyAll = hostsDenyAll,
                    Patterns = hosts
                },
                DenyAll = domain.DenyAll,
                InheritFromFile = domain.InheritFromFile
            };
        }
    }

    /// <summary>
    /// Domain representation of ManifestPolicy with rich types.
    /// This is what we use internally for processing.
    /// </summary>
    public sealed record ManifestPolicyDomain
    {
        /// <summary>
        /// Package IDs this policy applies to
        /// </summary>
        public ImmutableArray<string> Packages { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Selector for when this policy applies
        /// </summary>
        public string Selector { get; init; } = ":file";

        /// <summary>
        /// Maximum memory restriction (if any)
        /// </summary>
        public Maybe<MemorySize> MaxMemory { get; init; } = Maybe<MemorySize>.None;

        /// <summary>
        /// Timeout restriction (if any)
        /// </summary>
        public Maybe<TimeoutDuration> Timeout { get; init; } = Maybe<TimeoutDuration>.None;

        /// <summary>
        /// Module restrictions
        /// </summary>
        public ModuleRestriction ModuleRestrictions { get; init; } = ModuleRestriction.None;

        /// <summary>
        /// Capability restrictions
        /// </summary>
        public CapabilityRestriction CapabilityRestrictions { get; init; } = CapabilityRestriction.None;

        /// <summary>
        /// Path restrictions
        /// </summary>
        public PathRestriction PathRestrictions { get; init; } = PathRestriction.None;

        /// <summary>
        /// Host restrictions
        /// </summary>
        public HostRestriction HostRestrictions { get; init; } = HostRestriction.None;

        /// <summary>
        /// Whether this policy denies all access
        /// </summary>
        public bool DenyAll { get; init; } = false;

        /// <summary>
        /// Whether to inherit restrictions from file context
        /// </summary>
        public bool InheritFromFile { get; init; } = true;
    }
}