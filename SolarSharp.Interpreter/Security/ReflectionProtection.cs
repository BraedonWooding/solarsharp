using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Enhanced reflection protection system that prevents unauthorized access to internal implementation
    /// </summary>
    public class ReflectionProtection
    {
        private readonly HashSet<string> _blockedTypes;
        private readonly HashSet<string> _blockedNamespaces;
        private readonly HashSet<string> _blockedAssemblies;
        private readonly HashSet<string> _allowedTypes;
        private readonly ISecurityAuditor _auditor;
        private readonly ReflectionProtectionMode _mode;

        public ReflectionProtection(
            ReflectionProtectionMode mode = ReflectionProtectionMode.Strict,
            ISecurityAuditor auditor = null
        )
        {
            _mode = mode;
            _auditor = auditor;
            _blockedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _blockedNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _blockedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            InitializeDefaultBlocks();
        }

        /// <summary>
        /// Checks if access to a type is allowed
        /// </summary>
        public bool IsTypeAccessAllowed(Type type, string operation = "access")
        {
            if (type == null)
                return false;

            var typeName = type.FullName ?? type.Name;
            var assemblyName = type.Assembly.GetName().Name;
            var namespaceName = type.Namespace ?? string.Empty;

            // Check if explicitly allowed (takes precedence)
            if (_allowedTypes.Contains(typeName))
            {
                _auditor?.LogCapabilityUsage(
                    "reflection",
                    "type_access_allowed",
                    new object[] { typeName, operation },
                    true,
                    true
                );
                return true;
            }

            // Check blocks
            if (
                _blockedTypes.Contains(typeName)
                || _blockedAssemblies.Contains(assemblyName)
                || _blockedNamespaces.Any(ns =>
                    namespaceName.StartsWith(ns, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                _auditor?.LogSecurityViolation(
                    $"Reflection access blocked: {typeName} for operation '{operation}'",
                    SecurityEventType.ReflectionAccessDenied
                );
                return false;
            }

            // In strict mode, only explicitly allowed types are permitted
            if (_mode == ReflectionProtectionMode.Strict)
            {
                _auditor?.LogSecurityViolation(
                    $"Reflection access denied in strict mode: {typeName} for operation '{operation}'",
                    SecurityEventType.ReflectionAccessDenied
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if access to a member is allowed
        /// </summary>
        public bool IsMemberAccessAllowed(MemberInfo member, string operation = "access")
        {
            if (member == null)
                return false;

            // First check if the declaring type is allowed
            if (!IsTypeAccessAllowed(member.DeclaringType, $"member_{operation}"))
                return false;

            // Check for dangerous member names
            var memberName = member.Name;
            if (IsDangerousMemberName(memberName))
            {
                _auditor?.LogSecurityViolation(
                    $"Dangerous member access blocked: {member.DeclaringType.FullName}.{memberName}",
                    SecurityEventType.ReflectionAccessDenied
                );
                return false;
            }

            // Check for security-sensitive attributes
            if (HasSecuritySensitiveAttributes(member))
            {
                _auditor?.LogSecurityViolation(
                    $"Security-sensitive member blocked: {member.DeclaringType.FullName}.{memberName}",
                    SecurityEventType.ReflectionAccessDenied
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a protected metatable that blocks dangerous operations
        /// </summary>
        public Table CreateProtectedMetatable(Script script, object targetObject = null)
        {
            var metatable = new Table
            {
                // Override dangerous metamethods
                ["__index"] = DynValue.NewCallback(
                    (ctx, args) =>
                    {
                        if (args.Count < 2)
                            return DynValue.Nil;

                        var key = args[1];
                        var keyStr = key.CastToString();

                        // Block access to dangerous properties/methods
                        if (IsDangerousKey(keyStr))
                        {
                            _auditor?.LogSecurityViolation(
                                $"Metatable access blocked for key: {keyStr}",
                                SecurityEventType.ReflectionAccessDenied
                            );
                            return DynValue.Nil;
                        }

                        // If target object exists, try to get the value safely
                        if (targetObject != null)
                        {
                            return GetValueSafely(targetObject, keyStr, script);
                        }

                        return DynValue.Nil;
                    }
                ),
                ["__newindex"] = DynValue.NewCallback(
                    (ctx, args) =>
                    {
                        // Block all modifications in protected mode
                        _auditor?.LogSecurityViolation(
                            "Metatable modification blocked",
                            SecurityEventType.MetatableViolation
                        );
                        throw new MetatableViolationException(
                            "Modifications not allowed in protected metatable",
                            "newindex"
                        );
                    }
                ),
                ["__metatable"] = DynValue.NewString("protected"), // Hide metatable access
            };

            return metatable;
        }

        /// <summary>
        /// Sanitizes a global environment table by removing dangerous references
        /// </summary>
        public void SanitizeGlobalEnvironment(Table globals)
        {
            var dangerousKeys = new[]
            {
                "debug",
                "package",
                "require",
                "dofile",
                "loadfile",
                "load",
                "loadstring",
                "getmetatable",
                "setmetatable",
                "rawget",
                "rawset",
                "rawequal",
                "rawlen",
                "getfenv",
                "setfenv",
            };

            foreach (var key in dangerousKeys)
            {
                if (globals.Get(key) != DynValue.Nil)
                {
                    globals.Set(key, DynValue.Nil);
                    _auditor?.LogCapabilityUsage(
                        "reflection",
                        "sanitize_global",
                        new object[] { key },
                        null,
                        true
                    );
                }
            }

            // Replace dangerous functions with safe alternatives if needed
            if (_mode != ReflectionProtectionMode.Permissive)
            {
                ReplaceDangerousFunctions(globals);
            }
        }

        /// <summary>
        /// Blocks specific types from reflection access
        /// </summary>
        public ReflectionProtection BlockType<T>()
        {
            return BlockType(typeof(T));
        }

        /// <summary>
        /// Blocks specific types from reflection access
        /// </summary>
        public ReflectionProtection BlockType(Type type)
        {
            _blockedTypes.Add(type.FullName ?? type.Name);
            return this;
        }

        /// <summary>
        /// Blocks entire namespaces from reflection access
        /// </summary>
        public ReflectionProtection BlockNamespace(string namespaceName)
        {
            _blockedNamespaces.Add(namespaceName);
            return this;
        }

        /// <summary>
        /// Blocks entire assemblies from reflection access
        /// </summary>
        public ReflectionProtection BlockAssembly(string assemblyName)
        {
            _blockedAssemblies.Add(assemblyName);
            return this;
        }

        /// <summary>
        /// Explicitly allows specific types (takes precedence over blocks)
        /// </summary>
        public ReflectionProtection AllowType<T>()
        {
            return AllowType(typeof(T));
        }

        /// <summary>
        /// Explicitly allows specific types (takes precedence over blocks)
        /// </summary>
        public ReflectionProtection AllowType(Type type)
        {
            _allowedTypes.Add(type.FullName ?? type.Name);
            return this;
        }

        private void InitializeDefaultBlocks()
        {
            // Block dangerous system types
            var dangerousTypes = new[]
            {
                typeof(Type),
                typeof(Assembly),
                typeof(AppDomain),
                typeof(Environment),
                typeof(GC),
                typeof(Process),
                typeof(File),
                typeof(Directory),
                typeof(WebClient),
                typeof(HttpClient),
            };

            foreach (var type in dangerousTypes)
            {
                BlockType(type);
            }

            // Block dangerous namespaces
            var dangerousNamespaces = new[]
            {
                "System.Reflection",
                "System.Runtime.InteropServices",
                "System.Runtime.Remoting",
                "System.Security",
                "System.Diagnostics",
                "System.CodeDom",
                "System.Compiler",
                "Microsoft.CSharp",
            };

            foreach (var ns in dangerousNamespaces)
            {
                BlockNamespace(ns);
            }

            // Block SolarSharp internals
            BlockNamespace("SolarSharp.Interpreter.Execution");
            BlockNamespace("SolarSharp.Interpreter.Tree");
            BlockNamespace("SolarSharp.Interpreter.Debugging");
        }

        private bool IsDangerousMemberName(string memberName)
        {
            var dangerousNames = new[]
            {
                "GetType",
                "GetMethod",
                "GetField",
                "GetProperty",
                "GetConstructor",
                "GetMethods",
                "GetFields",
                "GetProperties",
                "GetConstructors",
                "InvokeMember",
                "CreateInstance",
                "Assembly",
                "Module",
                "GetHashCode",
                "GetObjectData",
                "MemberwiseClone",
            };

            return dangerousNames.Contains(memberName, StringComparer.OrdinalIgnoreCase);
        }

        private bool HasSecuritySensitiveAttributes(MemberInfo member)
        {
            var sensitiveAttributes = new[]
            {
                typeof(SecurityCriticalAttribute),
                typeof(SecuritySafeCriticalAttribute),
                typeof(DllImportAttribute),
                typeof(MethodImplAttribute),
            };

            return member
                .GetCustomAttributes()
                .Any(attr => sensitiveAttributes.Contains(attr.GetType()));
        }

        private bool IsDangerousKey(string key)
        {
            var dangerousKeys = new[]
            {
                "getmetatable",
                "setmetatable",
                "rawget",
                "rawset",
                "rawequal",
                "rawlen",
                "debug",
                "package",
                "require",
                "dofile",
                "loadfile",
                "load",
                "loadstring",
                "getfenv",
                "setfenv",
                "__index",
                "__newindex",
                "__metatable",
                "__call",
                "type",
                "pairs",
                "ipairs",
                "next",
                "select",
                "unpack",
            };

            return dangerousKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
        }

        private DynValue GetValueSafely(object obj, string key, Script script)
        {
            try
            {
                var type = obj.GetType();
                var property = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);

                if (property != null && property.CanRead)
                {
                    if (IsMemberAccessAllowed(property, "read"))
                    {
                        var value = property.GetValue(obj);
                        return DynValue.FromObject(script, value);
                    }
                }

                var field = type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    if (IsMemberAccessAllowed(field, "read"))
                    {
                        var value = field.GetValue(obj);
                        return DynValue.FromObject(script, value);
                    }
                }
            }
            catch (Exception ex)
            {
                _auditor?.LogSecurityViolation(
                    $"Safe value access failed for key '{key}': {ex.Message}",
                    SecurityEventType.ReflectionAccessDenied,
                    ex
                );
            }

            return DynValue.Nil;
        }

        private void ReplaceDangerousFunctions(Table globals)
        {
            // Replace getmetatable with safe version
            globals.Set(
                "getmetatable",
                DynValue.NewCallback(
                    (ctx, args) =>
                    {
                        _auditor?.LogSecurityViolation(
                            "getmetatable access blocked",
                            SecurityEventType.ReflectionAccessDenied
                        );
                        return DynValue.Nil;
                    }
                )
            );

            // Replace setmetatable with safe version
            globals.Set(
                "setmetatable",
                DynValue.NewCallback(
                    (ctx, args) =>
                    {
                        _auditor?.LogSecurityViolation(
                            "setmetatable access blocked",
                            SecurityEventType.MetatableViolation
                        );
                        throw new MetatableViolationException(
                            "setmetatable is not allowed in protected mode",
                            "setmetatable"
                        );
                    }
                )
            );

            // Block rawget/rawset
            globals.Set(
                "rawget",
                DynValue.NewCallback(
                    (ctx, args) =>
                    {
                        _auditor?.LogSecurityViolation(
                            "rawget access blocked",
                            SecurityEventType.ReflectionAccessDenied
                        );
                        return DynValue.Nil;
                    }
                )
            );

            globals.Set(
                "rawset",
                DynValue.NewCallback(
                    (ctx, args) =>
                    {
                        _auditor?.LogSecurityViolation(
                            "rawset access blocked",
                            SecurityEventType.ReflectionAccessDenied
                        );
                        throw new MetatableViolationException(
                            "rawset is not allowed in protected mode",
                            "rawset"
                        );
                    }
                )
            );
        }
    }

    /// <summary>
    /// Reflection protection modes
    /// </summary>
    public enum ReflectionProtectionMode
    {
        /// <summary>
        /// Allow most reflection operations (development mode)
        /// </summary>
        Permissive,

        /// <summary>
        /// Block dangerous operations but allow safe ones
        /// </summary>
        Moderate,

        /// <summary>
        /// Block all reflection operations except explicitly allowed types
        /// </summary>
        Strict,
    }

    /// <summary>
    /// Extensions for applying reflection protection
    /// </summary>
    public static class ReflectionProtectionExtensions
    {
        /// <summary>
        /// Applies reflection protection to a security configuration
        /// </summary>
        public static SecurityPolicy WithReflectionProtection(
            this SecurityPolicy policy,
            ReflectionProtectionMode mode = ReflectionProtectionMode.Moderate
        )
        {
            // This would be integrated with the existing security policy
            // For now, it returns the policy unchanged but this is where integration would happen
            return policy;
        }

        /// <summary>
        /// Creates a reflection protection instance with configuration
        /// </summary>
        public static ReflectionProtection CreateReflectionProtection(
            this SecurityPolicy policy,
            ISecurityAuditor auditor = null
        )
        {
            var mode = policy.AllowEnvironmentAccess switch
            {
                false => ReflectionProtectionMode.Strict,
                true => ReflectionProtectionMode.Permissive,
            };

            return new ReflectionProtection(mode, auditor);
        }
    }
}
