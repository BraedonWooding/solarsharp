using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Authorization;
using SolarSharp.Interpreter.Security.FunctionBinding;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing loading Lua functions like 'require', 'load', etc.
    /// </summary>
    [SolarSharpModule]
    public class LoadModule
    {
        public static void MoonSharpInit(Script script, Table globalTable, Table ioTable)
        {
            var package = globalTable.Get("package");

            if (package.IsNil())
            {
                package = DynValue.NewTable(new Table());
                globalTable["package"] = package;
            }
            else if (package.Type != DataType.Table)
            {
                throw new InternalErrorException(
                    "'package' global variable was found and it is not a table"
                );
            }

#if PCL || ENABLE_DOTNET || NETFX_CORE
            string cfg = "\\\n;\n?\n!\n-\n";
#else
            var cfg = Path.DirectorySeparatorChar + "\n;\n?\n!\n-\n";
#endif

            package.Table.Set("config", DynValue.NewString(cfg));
        }

        // load (ld [, source [, mode [, env]]])
        // ----------------------------------------------------------------
        // Loads a chunk.
        //
        // If ld is a string, the chunk is this string.
        //
        // If there are no syntactic errors, returns the compiled chunk as a function;
        // otherwise, returns nil plus the error message.
        //
        // source is used as the source of the chunk for error messages and debug
        // information (see §4.9). When absent, it defaults to ld, if ld is a string,
        // or to "=(load)" otherwise.
        //
        // The string mode is ignored, and assumed to be "t";
        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Basic,
            description: "Load and compile Lua code dynamically",
            returnNilOnDenied: true
        )]
        public static DynValue load(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return load_impl(executionContext, args, null);
        }

        // loadsafe (ld [, source [, mode [, env]]])
        // ----------------------------------------------------------------
        // Same as load, except that "env" defaults to the current environment of the function
        // calling load, instead of the actual global environment.
        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Basic,
            description: "Load and compile Lua code dynamically with safe environment",
            returnNilOnDenied: true
        )]
        public static DynValue loadsafe(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return load_impl(executionContext, args, GetSafeDefaultEnv(executionContext));
        }

        public static DynValue load_impl(
            ScriptExecutionContext executionContext,
            CallbackArguments args,
            Table defaultEnv
        )
        {
            // Execute string with eval context - security violations throw exceptions
            var scriptResult = GetScript(executionContext);
            if (scriptResult.IsFailure)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(scriptResult.Error));

            var codeAndSourceResult = ParseLoadArguments(executionContext, args);
            if (codeAndSourceResult.IsFailure)
                return DynValue.NewTuple(
                    DynValue.Nil,
                    DynValue.NewString(codeAndSourceResult.Error)
                );

            var envResult = ParseEnvironment(args, defaultEnv);
            if (envResult.IsFailure)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(envResult.Error));

            var evalContextResult = CreateEvalContext(executionContext);
            if (evalContextResult.IsFailure)
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(evalContextResult.Error));

            // Security violations will throw UnauthorizedProcessExecutionException
            return LoadStringWithEvalContext(
                scriptResult.Value,
                codeAndSourceResult.Value.code,
                codeAndSourceResult.Value.source,
                envResult.Value,
                evalContextResult.Value
            );
        }

        // loadfile ([filename [, mode [, env]]])
        // ----------------------------------------------------------------
        // Similar to load, but gets the chunk from file filename or from the standard input,
        // if no file name is given. INCOMPAT: stdin not supported, mode ignored
        [MoonSharpModuleMethod]
        public static DynValue loadfile(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return loadfile_impl(executionContext, args, null);
        }

        // loadfile ([filename [, mode [, env]]])
        // ----------------------------------------------------------------
        // Same as loadfile, except that "env" defaults to the current environment of the function
        // calling load, instead of the actual global environment.
        [MoonSharpModuleMethod]
        public static DynValue loadfilesafe(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return loadfile_impl(executionContext, args, GetSafeDefaultEnv(executionContext));
        }

        private static DynValue loadfile_impl(
            ScriptExecutionContext executionContext,
            CallbackArguments args,
            Table defaultEnv
        )
        {
            // Functional pipeline for file loading
            // Files are loaded with their own policies based on their paths
            var result =
                from script in GetScript(executionContext)
                from filename in ParseFilename(args)
                from env in ParseFileEnvironment(args, defaultEnv)
                from loadResult in LoadFile(script, filename, env)
                select loadResult;

            return result.Match(
                value => value,
                err => DynValue.NewTuple(DynValue.Nil, DynValue.NewString(err))
            );
        }

        private static Table GetSafeDefaultEnv(ScriptExecutionContext executionContext)
        {
            var env = executionContext.CurrentGlobalEnv;

            return env
                ?? throw new ScriptRuntimeException("current environment cannot be backtracked.");
        }

        //dofile ([filename])
        //--------------------------------------------------------------------------------------------------------------
        //Opens the named file and executes its contents as a Lua chunk. When called without arguments,
        //dofile executes the contents of the standard input (stdin). Returns all values returned by the chunk.
        //In case of errors, dofile propagates the error to its caller (that is, dofile does not run in protected mode).
        [MoonSharpModuleMethod]
        public static DynValue dofile(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            try
            {
                var S = executionContext.GetScript();
                var v = args.AsType(0, "dofile", DataType.String);

                var fn = S.LoadFile(v.String);

                return DynValue.NewTailCallReq(fn); // tail call to dofile
            }
            catch (SyntaxErrorException ex)
            {
                throw new ScriptRuntimeException(ex);
            }
        }

        //require (modname)
        //----------------------------------------------------------------------------------------------------------------
        //Loads the given module. The function starts by looking into the package.loaded table to determine whether
        //modname is already loaded. If it is, then require returns the value stored at package.loaded[modname].
        //Otherwise, it tries to find a loader for the module.
        //
        //To find a loader, require is guided by the package.loaders array. By changing this array, we can change
        //how require looks for a module. The following explanation is based on the default configuration for package.loaders.
        //
        //First require queries package.preload[modname]. If it has a value, this value (which should be a function)
        //is the loader. Otherwise require searches for a Lua loader using the path stored in package.path.
        //If that also fails, it searches for a C loader using the path stored in package.cpath. If that also fails,
        //it tries an all-in-one loader (see package.loaders).
        //
        //Once a loader is found, require calls the loader with a single argument, modname. If the loader returns any value,
        //require assigns the returned value to package.loaded[modname]. If the loader returns no value and has not assigned
        //any value to package.loaded[modname], then require assigns true to this entry. In any case, require returns the
        //final value of package.loaded[modname].
        //
        //If there is any error loading or running the module, or if it cannot find any loader for the module, then require
        //signals an error.
        [MoonSharpModuleMethod]
        public static DynValue __require_clr_impl(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var S = executionContext.GetScript();
            var v = args.AsType(0, "__require_clr_impl", DataType.String);

            var fn = S.RequireModule(v.String);

            return fn; // tail call to dofile
        }

        [MoonSharpModuleMethod]
        public const string require =
            @"
function(modulename)
	if (package == nil) then package = { }; end
	if (package.loaded == nil) then package.loaded = { }; end

	local m = package.loaded[modulename];

	if (m ~= nil) then
		return m;
	end

	local func = __require_clr_impl(modulename);

	local res = func(modulename);

	if (res == nil) then
		res = true;
	end

	package.loaded[modulename] = res;

	return res;
end";

        private static Result<Script, string> GetScript(ScriptExecutionContext context)
        {
            try
            {
                var script = context.GetScript();
                return script != null
                    ? Result.Success<Script, string>(script)
                    : Result.Failure<Script, string>("Script context not available");
            }
            catch (Exception ex)
            {
                return Result.Failure<Script, string>($"Failed to get script: {ex.Message}");
            }
        }

        private static Result<LuaExecutionContext, string> CreateEvalContext(
            ScriptExecutionContext scriptContext
        )
        {
            var currentContext = ExecutionContextManager.Current;
            if (currentContext.HasNoValue)
            {
                // When called from C# (e.g., Script.DoString), create context with ":eval"
                // This represents dynamic code execution from the host environment
                var hostContext = LuaExecutionContext
                    .CreateFromPath(":eval", Maybe<LuaExecutionContext>.None)
                    .Match(
                        ctx => ctx,
                        err =>
                            throw new InvalidOperationException(
                                $"Failed to create host context: {err}"
                            )
                    );

                // Already has :eval, so just return it
                return Result.Success<LuaExecutionContext, string>(hostContext);
            }

            // When called from within Lua, append :eval to the current file context
            var evalContextResult = currentContext.Value.CreateEvalContext();
            return evalContextResult.Match(
                ctx => Result.Success<LuaExecutionContext, string>(ctx),
                err => Result.Failure<LuaExecutionContext, string>(err.ToString())
            );
        }

        private static Result<(string code, string source), string> ParseLoadArguments(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            if (args.Count < 1)
                return Result.Failure<(string, string), string>("load: missing argument");

            var ld = args[0];
            var source =
                args.Count > 1 && args[1].Type == DataType.String ? args[1].String : "=(load)";

            if (ld.Type == DataType.Function)
            {
                return ReadFromFunction(executionContext, ld).Map(code => (code, source));
            }
            if (ld.Type == DataType.String)
            {
                return Result.Success<(string, string), string>((ld.String, source));
            }
            return Result.Failure<(string, string), string>(
                "load: first argument must be string or function"
            );
        }

        private static Result<string, string> ReadFromFunction(
            ScriptExecutionContext executionContext,
            DynValue function
        )
        {
            try
            {
                var script = "";
                var S = executionContext.GetScript();

                while (true)
                {
                    var ret = S.Call(function);
                    if (ret.Type == DataType.String && ret.String.Length > 0)
                        script += ret.String;
                    else if (ret.IsNil())
                        break;
                    else
                        throw new InvalidOperationException("reader function must return a string");
                }

                return Result.Success<string, string>(script);
            }
            catch (Exception ex)
            {
                return Result.Failure<string, string>(ex.Message);
            }
        }

        private static Result<Table, string> ParseEnvironment(
            CallbackArguments args,
            Table defaultEnv
        )
        {
            if (args.Count > 3 && !args[3].IsNil())
            {
                if (args[3].Type != DataType.Table)
                    return Result.Failure<Table, string>("load: env must be a table");
                return Result.Success<Table, string>(args[3].Table);
            }

            return Result.Success<Table, string>(defaultEnv);
        }

        private static DynValue LoadStringWithEvalContext(
            Script script,
            string code,
            string source,
            Table env,
            LuaExecutionContext evalContext
        )
        {
            // Perform runtime authorization check for dynamic code execution
            // Security violations throw exceptions to make them immediately visible
            var authorizationResult = AuthorizeEvalExecution(script, evalContext);
            if (authorizationResult.IsFailure)
            {
                // Security violations must throw exceptions, not return nil values
                var error = authorizationResult.Error;
                throw new UnauthorizedProcessExecutionException(error.Message, "load");
            }

            // Apply eval-specific policy for proper resource limits
            var evalPolicyResult = GetEvalPolicy(script, evalContext);
            if (evalPolicyResult.IsFailure)
            {
                throw new UnauthorizedProcessExecutionException(evalPolicyResult.Error, "load");
            }

            var evalPolicy = evalPolicyResult.Value;

            // Execute with eval-specific policy using WithTemporaryPolicy
            var result = ExecutionContextManager.WithContext(
                evalContext,
                _ =>
                {
                    try
                    {
                        // Apply the eval policy temporarily during execution
                        return script.WithTemporaryPolicy(
                            evalPolicy,
                            () =>
                            {
                                try
                                {
                                    var dynValue = script.LoadStringInternal(code, env, source);
                                    return Result.Success<DynValue, ExecutionError>(dynValue);
                                }
                                catch (SyntaxErrorException syntaxEx)
                                {
                                    var errorMsg = syntaxEx.DecoratedMessage ?? syntaxEx.Message;
                                    return Result.Success<DynValue, ExecutionError>(
                                        DynValue.NewTuple(
                                            DynValue.Nil,
                                            DynValue.NewString(errorMsg)
                                        )
                                    );
                                }
                                catch (UnauthorizedProcessExecutionException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    return Result.Success<DynValue, ExecutionError>(
                                        DynValue.NewTuple(
                                            DynValue.Nil,
                                            DynValue.NewString(ex.Message)
                                        )
                                    );
                                }
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        return Result.Success<DynValue, ExecutionError>(
                            DynValue.NewTuple(DynValue.Nil, DynValue.NewString(ex.Message))
                        );
                    }
                }
            );

            return result.Match(
                success => success,
                failure =>
                    throw new InvalidOperationException(
                        $"Context execution failed: {failure.Message}"
                    )
            );
        }

        /// <summary>
        /// Gets the eval-specific policy for the given execution context
        /// </summary>
        private static Result<SecurityPolicy, string> GetEvalPolicy(
            Script script,
            LuaExecutionContext evalContext
        )
        {
            var contextPath = evalContext.ToString();

            // Try to get the manifest-merged policy set from the script
            // The Script constructor should have set up a PolicySet that includes manifest policies
            var compiledManifest = GetCompiledManifest(script);
            if (compiledManifest is { HasSignedContent: true })
            {
                // Create a temporary PolicySet with the manifest's policies
                var manifestPolicySet = CreatePolicySetFromManifest(compiledManifest);
                if (manifestPolicySet != null)
                {
                    var policyResult = manifestPolicySet.ResolvePolicy(contextPath);
                    if (policyResult.IsSuccess)
                    {
                        return Result.Success<SecurityPolicy, string>(policyResult.Value);
                    }
                }
            }

            // Fall back to BasePolicySet
            var basePolicySet = script.GetService<BasePolicySet>();
            if (basePolicySet != null)
            {
                var policyResult = basePolicySet.PolicySet.ResolvePolicy(contextPath);
                if (policyResult.IsSuccess)
                {
                    return Result.Success<SecurityPolicy, string>(policyResult.Value);
                }
            }

            // As a last resort, use isolated policy for eval contexts
            return Result.Success<SecurityPolicy, string>(Examples.IsolatedSecurityPolicy);
        }

        /// <summary>
        /// Gets the compiled manifest from the script using reflection
        /// </summary>
        private static Manifest GetCompiledManifest(Script script)
        {
            try
            {
                // Use reflection to access the private m_CompiledManifest field
                var field = typeof(Script).GetField(
                    "m_CompiledManifest",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                return field?.GetValue(script) as Manifest;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a PolicySet from a V2.0 manifest's signed content blocks
        /// </summary>
        private static PolicySet CreatePolicySetFromManifest(Manifest manifest)
        {
            try
            {
                var builder = new PolicySetBuilder();
                var policyCounter = 0;

                // Process all signed content blocks
                foreach (var signedBlock in manifest.SignedContent)
                {
                    // Convert ManifestPolicy to SecurityPolicy for each policy in the block
                    foreach (var manifestPolicy in signedBlock.Policies)
                    {
                        var policyName = $"manifest_policy_{policyCounter++}";
                        var securityPolicy = ConvertManifestPolicyToSecurityPolicy(manifestPolicy);

                        if (securityPolicy != null)
                        {
                            builder.DefinePolicy(policyName, securityPolicy);

                            // Map file patterns from packages this policy applies to
                            foreach (var packageId in manifestPolicy.Packages)
                            {
                                if (packageId == "*")
                                {
                                    // Apply to all files in packages from this block
                                    foreach (var (packageName, package) in signedBlock.Packages)
                                    {
                                        foreach (var filePath in package.Files.Keys)
                                        {
                                            var pattern = CreatePatternFromFilePath(
                                                filePath,
                                                manifestPolicy.Selector
                                            );
                                            builder.MapFilePattern(pattern, policyName);
                                        }
                                    }
                                }
                                else if (signedBlock.Packages.ContainsKey(packageId))
                                {
                                    // Apply to specific package files
                                    var package = signedBlock.Packages[packageId];
                                    foreach (var filePath in package.Files.Keys)
                                    {
                                        var pattern = CreatePatternFromFilePath(
                                            filePath,
                                            manifestPolicy.Selector
                                        );
                                        builder.MapFilePattern(pattern, policyName);
                                    }
                                }
                            }
                        }
                    }
                }

                // Add a fallback isolated policy if no policies were created
                if (policyCounter == 0)
                {
                    builder.DefinePolicy("isolated", Examples.IsolatedSecurityPolicy);
                    builder.WithDefaultPolicy("isolated");
                }

                return builder.Build();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a ManifestPolicy to a SecurityPolicy
        /// </summary>
        private static SecurityPolicy ConvertManifestPolicyToSecurityPolicy(
            ManifestPolicy manifestPolicy
        )
        {
            try
            {
                // Start with an isolated base policy
                var basePolicy = Examples.IsolatedSecurityPolicy;

                // Check for deny-all first
                if (manifestPolicy.DenyAll)
                {
                    return basePolicy with { AllowExecution = false };
                }

                // Build file permissions from grants
                var filePermissions = ImmutableDictionary<string, FilePermissions>.Empty;

                foreach (var readPath in manifestPolicy.Grant.FileRead)
                {
                    filePermissions = filePermissions.SetItem(readPath, FilePermissions.Read);
                }

                foreach (var writePath in manifestPolicy.Grant.FileWrite)
                {
                    var existingPermission = filePermissions.GetValueOrDefault(
                        writePath,
                        FilePermissions.None
                    );
                    var newPermission =
                        existingPermission | FilePermissions.Read | FilePermissions.ReadWrite;
                    filePermissions = filePermissions.SetItem(writePath, newPermission);
                }

                // Parse restrictions
                var timeoutMs = ParseTimeoutFromString(manifestPolicy.Restrict.Timeout);
                var maxMemoryMB = ParseMemoryFromString(manifestPolicy.Restrict.MaxMemory);

                // Check if eval is denied - if eval is denied, don't allow execution
                var allowEval =
                    !manifestPolicy.Restrict.Deny.Contains("eval")
                    && !manifestPolicy.Restrict.Deny.Contains("dynamic-code");

                return basePolicy with
                {
                    AllowExecution = allowEval, // Use allowEval to control execution
                    FilePermissions = filePermissions,
                    TimeoutMs = timeoutMs > 0 ? timeoutMs : basePolicy.TimeoutMs,
                    MaxMemoryMB = maxMemoryMB > 0 ? maxMemoryMB : basePolicy.MaxMemoryMB,
                };
            }
            catch
            {
                return Examples.IsolatedSecurityPolicy;
            }
        }

        /// <summary>
        /// Creates a file pattern from a file path and selector
        /// </summary>
        private static string CreatePatternFromFilePath(string filePath, string selector)
        {
            var pattern = filePath;

            // Add selector suffix if not the default :file
            if (!string.IsNullOrEmpty(selector) && selector != ":file")
            {
                pattern += selector;
            }

            return pattern;
        }

        /// <summary>
        /// Parses timeout string like "5s", "30000ms" to milliseconds
        /// </summary>
        private static int ParseTimeoutFromString(string timeoutStr)
        {
            if (string.IsNullOrEmpty(timeoutStr))
                return 0;

            try
            {
                if (timeoutStr.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
                {
                    var value = timeoutStr.Substring(0, timeoutStr.Length - 2);
                    return int.Parse(value);
                }
                else if (timeoutStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    var value = timeoutStr.Substring(0, timeoutStr.Length - 1);
                    return int.Parse(value) * 1000;
                }

                // Try parsing as raw number (assume milliseconds)
                return int.Parse(timeoutStr);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Parses memory string like "10MB", "1GB" to MB
        /// </summary>
        private static int ParseMemoryFromString(string memoryStr)
        {
            if (string.IsNullOrEmpty(memoryStr))
                return 0;

            try
            {
                if (memoryStr.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
                {
                    var value = memoryStr.Substring(0, memoryStr.Length - 2);
                    return int.Parse(value);
                }
                else if (memoryStr.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
                {
                    var value = memoryStr.Substring(0, memoryStr.Length - 2);
                    return int.Parse(value) * 1024;
                }
                else if (memoryStr.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
                {
                    var value = memoryStr.Substring(0, memoryStr.Length - 2);
                    return Math.Max(1, int.Parse(value) / 1024);
                }

                // Try parsing as raw number (assume MB)
                return int.Parse(memoryStr);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Authorizes eval execution using the script's security policy resolver
        /// </summary>
        private static UnitResult<AuthorizationError> AuthorizeEvalExecution(
            Script script,
            LuaExecutionContext evalContext
        )
        {
            // Get the security policy resolver from the script's services
            var resolver = script.GetService<SecurityPolicyResolver>();
            if (resolver == null)
            {
                return UnitResult.Failure(
                    new AuthorizationError(
                        OperationType.DynamicCodeExecution,
                        "Security policy resolver not available",
                        evalContext.ToString(),
                        "missing_resolver"
                    )
                );
            }

            // Get the security auditor from the script's services (optional)
            var auditor = script.GetService<ISecurityAuditor>();

            // Create authorization service and check permission
            var authService = new ExecutionAuthorizationService(resolver, auditor);
            return authService.AuthorizeEvalExecution(evalContext);
        }

        private static Result<string, string> ParseFilename(CallbackArguments args)
        {
            if (args.Count < 1 || args[0].Type != DataType.String)
                return Result.Failure<string, string>("loadfile: filename must be a string");

            var filename = args[0].String;
            if (string.IsNullOrWhiteSpace(filename))
                return Result.Failure<string, string>("loadfile: filename cannot be empty");

            return Result.Success<string, string>(filename);
        }

        private static Result<Table, string> ParseFileEnvironment(
            CallbackArguments args,
            Table defaultEnv
        )
        {
            if (args.Count > 2 && !args[2].IsNil())
            {
                if (args[2].Type != DataType.Table)
                    return Result.Failure<Table, string>("loadfile: env must be a table");
                return Result.Success<Table, string>(args[2].Table);
            }

            return Result.Success<Table, string>(defaultEnv);
        }

        private static Result<DynValue, string> LoadFile(Script script, string filename, Table env)
        {
            // Load file with its own manifest/policy based on its path
            // The script's SecurityPolicyResolver will determine the appropriate policy
            try
            {
                var result = script.LoadFile(filename, env);
                return Result.Success<DynValue, string>(result);
            }
            catch (Exception ex)
            {
                var message = ex switch
                {
                    SyntaxErrorException syntaxEx => syntaxEx.DecoratedMessage ?? syntaxEx.Message,
                    _ => ex.Message,
                };
                return Result.Failure<DynValue, string>(message);
            }
        }
    }
}
