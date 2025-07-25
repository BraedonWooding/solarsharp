using System;
using System.Linq;
using System.Reflection;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.CoreLib;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.FunctionBinding;

namespace SolarSharp.Interpreter.Modules
{
    /// <summary>
    /// Class managing modules (mostly as extension methods)
    /// </summary>
    public static class ModuleRegister
    {
        /// <summary>
        /// Register the core modules to a table without filtering
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="modules">The modules.</param>
        /// <param name="skipFiltering">If true, skip platform filtering</param>
        /// <returns></returns>
        public static Table RegisterCoreModules(
            this Table table,
            Script script,
            CoreModules modules,
            bool skipFiltering
        )
        {
            if (!skipFiltering)
            {
                // Use the script's platform accessor if available, otherwise fall back to global
                var platform = script?.Platform ?? Script.GlobalOptions.Platform;
                modules = platform.FilterSupportedCoreModules(modules);
            }

            return RegisterCoreModulesInternal(table, script, modules);
        }

        /// <summary>
        /// Register the core modules to a table
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="modules">The modules.</param>
        /// <returns></returns>
        public static Table RegisterCoreModules(
            this Table table,
            Script script,
            CoreModules modules
        )
        {
            // Use the script's platform accessor if available, otherwise fall back to global
            var platform = script?.Platform ?? Script.GlobalOptions.Platform;
            modules = platform.FilterSupportedCoreModules(modules);
            return RegisterCoreModulesInternal(table, script, modules);
        }

        private static Table RegisterCoreModulesInternal(
            Table table,
            Script script,
            CoreModules modules
        )
        {
            if (modules.Has(CoreModules.GlobalConsts))
                table.RegisterConstants(script);
            if (modules.Has(CoreModules.TableIterators))
                table.RegisterModuleType<TableIteratorsModule>(script);
            if (modules.Has(CoreModules.Basic))
                table.RegisterModuleType<BasicModule>(script);
            if (modules.Has(CoreModules.Metatables))
                table.RegisterModuleType<MetaTableModule>(script);
            if (modules.Has(CoreModules.String))
                table.RegisterModuleType<StringModule>(script);
            if (modules.Has(CoreModules.LoadMethods))
                table.RegisterModuleType<LoadModule>(script);
            if (modules.Has(CoreModules.Table))
                table.RegisterModuleType<TableModule>(script);
            if (modules.Has(CoreModules.Table))
                table.RegisterModuleType<TableModule_Globals>(script);
            if (modules.Has(CoreModules.ErrorHandling))
                table.RegisterModuleType<ErrorHandlingModule>(script);
            if (modules.Has(CoreModules.Math))
                table.RegisterModuleType<MathModule>(script);
            if (modules.Has(CoreModules.Coroutine))
                table.RegisterModuleType<CoroutineModule>(script);
            if (modules.Has(CoreModules.Bit32))
                table.RegisterModuleType<Bit32Module>(script);
            if (modules.Has(CoreModules.Dynamic))
                table.RegisterModuleType<DynamicModule>(script);
            if (modules.Has(CoreModules.OS_System))
                table.RegisterModuleType<OsSystemModule>(script);
            if (modules.Has(CoreModules.OS_Time))
                table.RegisterModuleType<OsTimeModule>(script);
            if (modules.Has(CoreModules.IO))
                table.RegisterModuleType<IoModule>(script);
            if (modules.Has(CoreModules.Debug))
                table.RegisterModuleType<DebugModule>(script);
            if (modules.Has(CoreModules.Json))
                table.RegisterModuleType<JsonModule>(script);
            if (modules.Has(CoreModules.PubSub))
                table.RegisterModuleType<PubSubModule>(script);

            return table;
        }

        /// <summary>
        /// Register ALL core modules with security-bound functions.
        /// This replaces policy-based module filtering with function-level security enforcement.
        /// </summary>
        /// <param name="table">The table to register modules to</param>
        /// <param name="script">The script context</param>
        /// <param name="functionRegistry">The contextual function registry for per-context isolation</param>
        /// <param name="messageBus">The security message bus for audit logging</param>
        /// <returns>The table with all modules registered</returns>
        public static Table RegisterAllCoreModulesWithSecurity(
            this Table table,
            Script script,
            ContextualFunctionRegistry functionRegistry,
            ISecurityMessageBus messageBus
        )
        {
            // Always load ALL modules - security is enforced at function call time
            var allModules = CoreModules.Preset_Complete;

            // Filter only for platform compatibility, not security
            if (script?.Platform != null)
            {
                allModules = script.Platform.FilterSupportedCoreModules(allModules);
            }

            // Register all supported modules with security wrappers
            return RegisterCoreModulesWithSecurity(
                table,
                script,
                allModules,
                functionRegistry,
                messageBus
            );
        }

        /// <summary>
        /// Register core modules with security-bound function wrappers.
        /// This provides function-level security enforcement through SecurityBoundFunction attributes.
        /// </summary>
        private static Table RegisterCoreModulesWithSecurity(
            Table table,
            Script script,
            CoreModules modules,
            ContextualFunctionRegistry functionRegistry,
            ISecurityMessageBus messageBus
        )
        {
            // Check if security infrastructure is available
            var policyResolver = script.GetService<SecurityPolicyResolver>();
            SecurityFunctionChecker securityChecker = null;

            if (policyResolver != null)
            {
                // Initialize security checker for this script
                securityChecker = new SecurityFunctionChecker(
                    policyResolver,
                    script.GetService<ISecurityAuditor>()
                );
            }

            // Register all modules - they will be secured at the function level if security infrastructure is available
            if (modules.Has(CoreModules.GlobalConsts))
                table.RegisterConstants(script);
            if (modules.Has(CoreModules.TableIterators))
                table.RegisterModuleTypeWithSecurity<TableIteratorsModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Basic))
                table.RegisterModuleTypeWithSecurity<BasicModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Metatables))
                table.RegisterModuleTypeWithSecurity<MetaTableModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.String))
                table.RegisterModuleTypeWithSecurity<StringModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.LoadMethods))
                table.RegisterModuleTypeWithSecurity<LoadModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Table))
            {
                table.RegisterModuleTypeWithSecurity<TableModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
                table.RegisterModuleTypeWithSecurity<TableModule_Globals>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            }
            if (modules.Has(CoreModules.ErrorHandling))
                table.RegisterModuleTypeWithSecurity<ErrorHandlingModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Math))
                table.RegisterModuleTypeWithSecurity<MathModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Coroutine))
                table.RegisterModuleTypeWithSecurity<CoroutineModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Bit32))
                table.RegisterModuleTypeWithSecurity<Bit32Module>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Dynamic))
                table.RegisterModuleTypeWithSecurity<DynamicModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.OS_System))
                table.RegisterModuleTypeWithSecurity<OsSystemModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.OS_Time))
                table.RegisterModuleTypeWithSecurity<OsTimeModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.IO))
                table.RegisterModuleTypeWithSecurity<IoModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Debug))
                table.RegisterModuleTypeWithSecurity<DebugModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.Json))
                table.RegisterModuleTypeWithSecurity<JsonModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );
            if (modules.Has(CoreModules.PubSub))
                table.RegisterModuleTypeWithSecurity<PubSubModule>(
                    script,
                    securityChecker,
                    functionRegistry
                );

            return table;
        }

        /// <summary>
        /// Registers a module type with security-bound function wrappers.
        /// All functions with SecurityBoundFunction attributes will be wrapped with security checks.
        /// </summary>
        private static Table RegisterModuleTypeWithSecurity<T>(
            this Table gtable,
            Script script,
            SecurityFunctionChecker securityChecker,
            ContextualFunctionRegistry functionRegistry
        )
        {
            return RegisterModuleTypeWithSecurity(
                gtable,
                typeof(T),
                script,
                securityChecker,
                functionRegistry
            );
        }

        /// <summary>
        /// Registers a module type with security-bound function wrappers.
        /// </summary>
        private static Table RegisterModuleTypeWithSecurity(
            this Table gtable,
            Type moduleType,
            Script script,
            SecurityFunctionChecker securityChecker,
            ContextualFunctionRegistry functionRegistry
        )
        {
            // First register the module normally to get all the functions
            var originalTable = new Table();
            originalTable.RegisterModuleType(moduleType, script);

            // Now wrap all functions that have SecurityBoundFunction attributes
            foreach (var method in moduleType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var moduleMethodAttr = method.GetCustomAttribute<MoonSharpModuleMethodAttribute>();
                var securityAttr = method.GetCustomAttribute<SecurityBoundFunctionAttribute>();

                if (moduleMethodAttr != null && securityAttr != null && securityChecker != null)
                {
                    // Create security wrapper for this function
                    var originalFunction = CreateOriginalFunctionCallback(method);
                    var secureFunction = securityChecker.CreateSecureWrapper(
                        method,
                        originalFunction,
                        method.Name
                    );

                    // Register the secure function in the contextual registry
                    var dynValue = DynValue.NewCallback(
                        new CallbackFunction(secureFunction, method.Name)
                    );
                    functionRegistry.RegisterGlobalFunction(method.Name, dynValue);

                    // Also set it in the global table for immediate use
                    gtable.Set(method.Name, dynValue);
                }
                else if (moduleMethodAttr != null)
                {
                    // Fallback to normal registration if security infrastructure is not available
                    var originalFunction = CreateOriginalFunctionCallback(method);
                    var dynValue = DynValue.NewCallback(
                        new CallbackFunction(originalFunction, method.Name)
                    );
                    gtable.Set(method.Name, dynValue);
                }
            }

            // Copy any non-function values from the original module registration
            CopyNonFunctionValues(originalTable, gtable);

            return gtable;
        }

        /// <summary>
        /// Creates a callback function from a module method using reflection.
        /// </summary>
        private static Func<
            ScriptExecutionContext,
            CallbackArguments,
            DynValue
        > CreateOriginalFunctionCallback(MethodInfo method)
        {
            return (context, args) =>
            {
                try
                {
                    // Invoke the original static module method
                    var parameters = new object[] { context, args };
                    var result = method.Invoke(null, parameters);
                    return (DynValue)result;
                }
                catch (TargetInvocationException ex)
                {
                    // Unwrap the inner exception from reflection
                    throw ex.InnerException ?? ex;
                }
            };
        }

        /// <summary>
        /// Copies non-function values from source table to destination table.
        /// This preserves module constants, metatables, and other non-function values.
        /// </summary>
        private static void CopyNonFunctionValues(Table source, Table destination)
        {
            foreach (var kvp in source)
            {
                if (kvp.Value.Type != DataType.Function && kvp.Value.Type != DataType.ClrFunction)
                {
                    destination.Set(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Registers the standard constants (_G, _VERSION, _MOONSHARP) to a table
        /// </summary>
        /// <param name="table">The table.</param>
        /// <returns></returns>
        public static Table RegisterConstants(this Table table, Script script)
        {
            var moonsharp_table = DynValue.NewTable(new Table());
            var m = moonsharp_table.Table;

            table.Set("_G", DynValue.NewTable(table));
            table.Set("_VERSION", DynValue.NewString($"MoonSharp {Script.VERSION}"));
            table.Set("_MOONSHARP", moonsharp_table);

            m.Set("version", DynValue.NewString(Script.VERSION));
            m.Set("luacompat", DynValue.NewString(Script.LUA_VERSION));
            // Use the provided script's platform accessor if available, otherwise fall back to global
            var platform = script?.Platform ?? Script.GlobalOptions.Platform;
            m.Set("platform", DynValue.NewString(platform.GetPlatformName()));
            m.Set("is_aot", DynValue.NewBoolean(platform.IsRunningOnAOT()));
            m.Set("is_unity", DynValue.NewBoolean(PlatformAutoDetector.IsRunningOnUnity));
            m.Set("is_mono", DynValue.NewBoolean(PlatformAutoDetector.IsRunningOnMono));
            m.Set("is_clr4", DynValue.NewBoolean(PlatformAutoDetector.IsRunningOnClr4));
            m.Set("is_pcl", DynValue.NewBoolean(PlatformAutoDetector.IsPortableFramework));
            m.Set("banner", DynValue.NewString(Script.GetBanner()));

            return table;
        }

        /// <summary>
        /// Registers a module type to the specified table
        /// </summary>
        /// <param name="gtable">The table.</param>
        /// <param name="t">The type</param>
        /// <param name="script">The script context</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">If the module contains some incompatibility</exception>
        public static Table RegisterModuleType(this Table gtable, Type t, Script script)
        {
            var table = CreateModuleNamespace(gtable, t, script);

            foreach (var mi in Framework.Do.GetMethods(t).Where(__mi => __mi.IsStatic))
            {
                if (
                    mi.GetCustomAttributes(typeof(MoonSharpModuleMethodAttribute), false).Length > 0
                )
                {
                    var attr = (MoonSharpModuleMethodAttribute)
                        mi.GetCustomAttributes(typeof(MoonSharpModuleMethodAttribute), false)
                            .First();

                    if (!CallbackFunction.CheckCallbackSignature(mi, true))
                        throw new ArgumentException(
                            $"Method {mi.Name} does not have the right signature."
                        );

#if NETFX_CORE
                    Delegate deleg = mi.CreateDelegate(
                        typeof(Func<ScriptExecutionContext, CallbackArguments, DynValue>)
                    );
#else
                    var deleg = Delegate.CreateDelegate(
                        typeof(Func<ScriptExecutionContext, CallbackArguments, DynValue>),
                        mi
                    );
#endif

                    var func = (Func<ScriptExecutionContext, CallbackArguments, DynValue>)deleg;

                    var name = !string.IsNullOrEmpty(attr.Name) ? attr.Name : mi.Name;

                    table.Set(name, DynValue.NewCallback(func, name));
                }
                else if (mi.Name == "MoonSharpInit")
                {
                    var parameters = mi.GetParameters();
                    if (parameters.Length == 3)
                    {
                        var args = new object[3] { script, gtable, table };
                        mi.Invoke(null, args);
                    }
                    else if (parameters.Length == 2)
                    {
                        var args = new object[2] { gtable, table };
                        mi.Invoke(null, args);
                    }
                }
            }

            foreach (
                var fi in Framework
                    .Do.GetFields(t)
                    .Where(_mi =>
                        _mi.IsStatic
                        && _mi.GetCustomAttributes(
                            typeof(MoonSharpModuleMethodAttribute),
                            false
                        ).Length > 0
                    )
            )
            {
                var attr = (MoonSharpModuleMethodAttribute)
                    fi.GetCustomAttributes(typeof(MoonSharpModuleMethodAttribute), false).First();
                var name = !string.IsNullOrEmpty(attr.Name) ? attr.Name : fi.Name;

                RegisterScriptField(fi, null, table, t, name, script);
            }

            foreach (
                var fi in Framework
                    .Do.GetFields(t)
                    .Where(_mi =>
                        _mi.IsStatic
                        && _mi.GetCustomAttributes(
                            typeof(MoonSharpModuleConstantAttribute),
                            false
                        ).Length > 0
                    )
            )
            {
                var attr = (MoonSharpModuleConstantAttribute)
                    fi.GetCustomAttributes(typeof(MoonSharpModuleConstantAttribute), false).First();
                var name = !string.IsNullOrEmpty(attr.Name) ? attr.Name : fi.Name;

                RegisterScriptFieldAsConst(fi, null, table, t, name);
            }

            return gtable;
        }

        private static void RegisterScriptFieldAsConst(
            FieldInfo fi,
            object o,
            Table table,
            Type t,
            string name
        )
        {
            if (fi.FieldType == typeof(string))
            {
                var val = fi.GetValue(o) as string;
                table.Set(name, DynValue.NewString(val));
            }
            else if (fi.FieldType == typeof(double))
            {
                var val = (double)fi.GetValue(o);
                table.Set(name, DynValue.NewNumber(val));
            }
            else
            {
                throw new ArgumentException(
                    $"Field {name} does not have the right type - it must be string or double."
                );
            }
        }

        private static void RegisterScriptField(
            FieldInfo fi,
            object o,
            Table table,
            Type t,
            string name,
            Script script
        )
        {
            if (fi.FieldType != typeof(string))
            {
                throw new ArgumentException(
                    $"Field {name} does not have the right type - it must be string."
                );
            }

            var val = fi.GetValue(o) as string;

            var fn = script.LoadFunction(val, table, name);

            table.Set(name, fn);
        }

        private static Table CreateModuleNamespace(Table gtable, Type t, Script script)
        {
            var attr = (SolarSharpModuleAttribute)
                Framework
                    .Do.GetCustomAttributes(t, typeof(SolarSharpModuleAttribute), false)
                    .First();

            if (string.IsNullOrEmpty(attr.Namespace))
            {
                return gtable;
            }
            var found = gtable.Get(attr.Namespace);

            Table table;
            if (found.Type == DataType.Table)
            {
                table = found.Table;
            }
            else
            {
                table = new Table();
                gtable.Set(attr.Namespace, DynValue.NewTable(table));
            }

            var package = gtable.Get("package");

            if (package.IsNil() || package.Type != DataType.Table)
            {
                gtable.Set("package", package = DynValue.NewTable(new Table()));
            }

            var loaded = package.Table.Get("loaded");

            if (loaded.IsNil() || loaded.Type != DataType.Table)
            {
                package.Table.Set("loaded", loaded = DynValue.NewTable(new Table()));
            }

            loaded.Table.Set(attr.Namespace, DynValue.NewTable(table));

            return table;
        }

        /// <summary>
        /// Registers a module type to the specified table
        /// </summary>
        /// <typeparam name="T">The module type</typeparam>
        /// <param name="table">The table.</param>
        /// <param name="script">The script context.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">If the module contains some incompatibility</exception>
        public static Table RegisterModuleType<T>(this Table table, Script script)
        {
            return table.RegisterModuleType(typeof(T), script);
        }
    }
}
