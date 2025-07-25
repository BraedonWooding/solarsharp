using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents module restrictions in a manifest policy.
    /// Can express patterns like "deny IO" or "deny all except IO and OS".
    /// </summary>
    public sealed record ModuleRestriction
    {
        private readonly RestrictionSet<CoreModules> _restriction;

        /// <summary>
        /// Private constructor ensures creation through factory methods.
        /// </summary>
        private ModuleRestriction(RestrictionSet<CoreModules> restriction)
        {
            _restriction = restriction ?? RestrictionSet<CoreModules>.None;
        }

        /// <summary>
        /// Creates an empty restriction (no modules are restricted).
        /// </summary>
        public static ModuleRestriction None { get; } = new(RestrictionSet<CoreModules>.None);

        /// <summary>
        /// Creates a restriction that denies all modules.
        /// </summary>
        public static ModuleRestriction DenyAll { get; } = new(RestrictionSet<CoreModules>.All);

        /// <summary>
        /// Creates a restriction that denies specific modules.
        /// </summary>
        public static ModuleRestriction DenyModules(params CoreModules[] modules)
        {
            if (modules == null || modules.Length == 0)
                return None;

            // Expand flags to individual modules
            var expandedModules = modules.SelectMany(ExpandModuleFlags).Distinct();
            return new ModuleRestriction(RestrictionSet<CoreModules>.DenySpecific(expandedModules));
        }

        /// <summary>
        /// Creates a restriction that denies all modules except those specified.
        /// </summary>
        public static ModuleRestriction DenyAllExcept(params CoreModules[] allowedModules)
        {
            if (allowedModules == null || allowedModules.Length == 0)
                return DenyAll;

            // Expand flags to individual modules
            var expandedModules = allowedModules.SelectMany(ExpandModuleFlags).Distinct();
            return new ModuleRestriction(RestrictionSet<CoreModules>.DenyAllExcept(expandedModules));
        }

        /// <summary>
        /// Creates a ModuleRestriction from string representations.
        /// </summary>
        public static Result<ModuleRestriction, string> Create(bool denyAll, IEnumerable<string> moduleNames)
        {
            if (moduleNames == null)
                return Result.Success<ModuleRestriction, string>(None);

            var moduleList = moduleNames.ToList();
            if (!moduleList.Any())
            {
                return Result.Success<ModuleRestriction, string>(denyAll ? DenyAll : None);
            }

            // Parse module names
            var parsedModules = new List<CoreModules>();
            var errors = new List<string>();

            foreach (var moduleName in moduleList)
            {
                if (string.IsNullOrWhiteSpace(moduleName))
                    continue;

                if (Enum.TryParse<CoreModules>(moduleName, true, out var module))
                {
                    parsedModules.Add(module);
                }
                else
                {
                    errors.Add($"Unknown module: '{moduleName}'");
                }
            }

            if (errors.Any())
                return Result.Failure<ModuleRestriction, string>(string.Join("; ", errors));

            return Result.Success<ModuleRestriction, string>(
                denyAll ? DenyAllExcept(parsedModules.ToArray()) : DenyModules(parsedModules.ToArray()));
        }

        /// <summary>
        /// Checks if a module is restricted.
        /// </summary>
        public bool IsRestricted(CoreModules module)
        {
            // Check each individual flag in the module
            var individualModules = ExpandModuleFlags(module);
            return individualModules.Any(m => _restriction.IsDenied(m));
        }

        /// <summary>
        /// Checks if a module is allowed.
        /// </summary>
        public bool IsAllowed(CoreModules module) => !IsRestricted(module);

        /// <summary>
        /// Gets the effective allowed modules when combined with a base set.
        /// </summary>
        public CoreModules GetEffectiveAllowedModules(CoreModules baseModules)
        {
            var result = CoreModules.None;
            
            // Check each possible module
            foreach (CoreModules module in Enum.GetValues(typeof(CoreModules)))
            {
                // Skip combined flags and None
                if (module == CoreModules.None || HasMultipleBits(module))
                    continue;

                // Module is allowed if it's in base set AND not restricted
                if (baseModules.HasFlag(module) && !_restriction.IsDenied(module))
                {
                    result |= module;
                }
            }

            return result;
        }

        /// <summary>
        /// Combines this restriction with another, taking the most restrictive combination.
        /// </summary>
        public ModuleRestriction CombineWith(ModuleRestriction other)
        {
            if (other == null)
                return this;

            return new ModuleRestriction(_restriction.CombineWith(other._restriction));
        }

        /// <summary>
        /// Converts to a JSON-friendly representation.
        /// </summary>
        public (bool DenyAll, ImmutableArray<string> Modules) ToJsonPattern()
        {
            var (denyAll, modules) = _restriction.ToJsonPattern();
            var moduleNames = modules.Select(m => m.ToString()).ToImmutableArray();
            return (denyAll, moduleNames);
        }

        /// <summary>
        /// Gets whether this restriction denies all modules.
        /// </summary>
        public bool DeniesAll => _restriction.DeniesEverything;

        /// <summary>
        /// Gets whether this restriction denies no modules.
        /// </summary>
        public bool DeniesNone => _restriction.DeniesNothing;

        public override string ToString() => _restriction.ToString();

        /// <summary>
        /// Expands module flags to individual modules.
        /// </summary>
        private static IEnumerable<CoreModules> ExpandModuleFlags(CoreModules modules)
        {
            // Handle special cases
            if (modules == CoreModules.None)
                yield break;

            // Check if all bits are set (using Preset_Complete as "all")
            if (modules == CoreModules.Preset_Complete)
            {
                // Return all individual modules
                foreach (CoreModules module in Enum.GetValues(typeof(CoreModules)))
                {
                    if (module != CoreModules.None && !HasMultipleBits(module))
                        yield return module;
                }
                yield break;
            }

            // Check each individual flag
            foreach (CoreModules module in Enum.GetValues(typeof(CoreModules)))
            {
                if (module == CoreModules.None)
                    continue;

                if (!HasMultipleBits(module) && modules.HasFlag(module))
                    yield return module;
            }
        }

        /// <summary>
        /// Checks if an enum value has multiple bits set.
        /// </summary>
        private static bool HasMultipleBits(CoreModules module)
        {
            int value = (int)module;
            return value != 0 && (value & (value - 1)) != 0;
        }
    }

    /// <summary>
    /// Fluent builder extensions for ModuleRestriction.
    /// </summary>
    public static class ModuleRestrictionExtensions
    {
        /// <summary>
        /// Creates a module restriction that denies the IO module.
        /// </summary>
        public static ModuleRestriction DenyIO(this ModuleRestriction _) =>
            ModuleRestriction.DenyModules(CoreModules.IO);

        /// <summary>
        /// Creates a module restriction that denies all modules except IO.
        /// </summary>
        public static ModuleRestriction DenyAllExceptIO(this ModuleRestriction _) =>
            ModuleRestriction.DenyAllExcept(CoreModules.IO);

        /// <summary>
        /// Creates a module restriction that denies OS and IO modules.
        /// </summary>
        public static ModuleRestriction DenySystemModules(this ModuleRestriction _) =>
            ModuleRestriction.DenyModules(CoreModules.OS_System, CoreModules.IO);

        /// <summary>
        /// Creates a module restriction that allows only safe modules (String, Table, Math, Bit32).
        /// </summary>
        public static ModuleRestriction AllowOnlySafeModules(this ModuleRestriction _) =>
            ModuleRestriction.DenyAllExcept(
                CoreModules.String, 
                CoreModules.Table, 
                CoreModules.Math, 
                CoreModules.Bit32);
    }
}