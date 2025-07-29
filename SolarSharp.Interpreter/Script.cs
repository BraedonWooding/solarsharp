using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SolarSharp.Interpreter.CoreLib;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.IO;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Tree.Fast_Interface;

namespace SolarSharp.Interpreter;

/// <summary>
///     This class implements a SolarSharp scripting session. Multiple Script objects can coexist in the same program but
///     cannot share
///     data among themselves unless some mechanism is put in place.
/// </summary>
public class Script : IScriptPrivateResource
{
    /// <summary>
    ///     The version of the SolarSharp engine
    /// </summary>
    public const string VERSION = "2.0.0.0";

    /// <summary>
    ///     The Lua version being supported
    /// </summary>
    public const string LUA_VERSION = "5.2";

    private readonly ByteCode m_ByteCode;
    private readonly Processor m_MainProcessor;
    private readonly List<SourceCode> m_Sources = [];
    private readonly Table[] m_TypeMetatables = new Table[(int)LuaTypeExtensions.MaxMetaTypes];

    /// <summary>
    ///     Initializes the <see cref="Script" /> class.
    /// </summary>
    static Script()
    {
        GlobalOptions = new ScriptGlobalOptions();

        DefaultOptions = new ScriptOptions
        {
            DebugPrint = GlobalOptions.Platform.DefaultPrint,
            DebugInput = GlobalOptions.Platform.DefaultInput,
            CheckThreadAccess = true,
            ScriptLoader = PlatformAutoDetector.GetDefaultScriptLoader(),
            TailCallOptimizationThreshold = 65536
        };
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Script" /> clas.s
    /// </summary>
    public Script()
        : this(CoreModules.Preset_Default)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Script" /> class.
    /// </summary>
    /// <param name="coreModules">The core modules to be pre-registered in the default global table.</param>
    public Script(CoreModules coreModules)
    {
        Options = new ScriptOptions(DefaultOptions);
        PerformanceStats = new PerformanceStatistics();
        Registry = new Table(this);

        m_ByteCode = new ByteCode(this);
        m_MainProcessor = new Processor(this, Globals, m_ByteCode);
        Globals = new Table(this).RegisterCoreModules(coreModules);
    }


    /// <summary>
    ///     Gets or sets the script loader which will be used as the value of the
    ///     ScriptLoader property for all newly created scripts.
    /// </summary>
    public static ScriptOptions DefaultOptions { get; }

    /// <summary>
    ///     Gets access to the script options.
    /// </summary>
    public ScriptOptions Options { get; }

    /// <summary>
    ///     Gets the global options, that is options which cannot be customized per-script.
    /// </summary>
    public static ScriptGlobalOptions GlobalOptions { get; }

    /// <summary>
    ///     Gets access to performance statistics.
    /// </summary>
    public PerformanceStatistics PerformanceStats { get; private set; }

    /// <summary>
    ///     Gets the default global table for this script. Unless a different table is intentionally passed (or setfenv has
    ///     been used)
    ///     execution uses this table.
    /// </summary>
    public Table Globals { get; }

    /// <summary>
    ///     Gets the source code count.
    /// </summary>
    /// <value>
    ///     The source code count.
    /// </value>
    public int SourceCodeCount => m_Sources.Count;

    /// <summary>
    ///     SolarSharp (like Lua itself) provides a registry, a predefined table that can be used by any CLR code to
    ///     store whatever Lua values it needs to store.
    ///     Any CLR code can store data into this table, but it should take care to choose keys
    ///     that are different from those used by other libraries, to avoid collisions.
    ///     Typically, you should use as key a string GUID, a string containing your library name, or a
    ///     userdata with the address of a CLR object in your code.
    /// </summary>
    public Table Registry { get; private set; }

    Script IScriptPrivateResource.OwnerScript => this;

    /// <summary>
    ///     Loads a string containing a Lua/SolarSharp function.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="globalTable">The global table to bind to this chunk.</param>
    /// <param name="funcFriendlyName">Name of the function used to report errors, etc.</param>
    /// <returns>
    ///     A LuaValue containing a function which will execute the loaded code.
    /// </returns>
    public LuaValue LoadFunction(string code, Table globalTable = null, string funcFriendlyName = null)
    {
        this.CheckScriptOwnership(globalTable);

        var chunkName = $"libfunc_{funcFriendlyName ?? m_Sources.Count.ToString()}";

        SourceCode source = new(chunkName, code, m_Sources.Count, this);

        m_Sources.Add(source);

        var address = Loader_Fast.LoadFunction(this, source, m_ByteCode, globalTable != null || Globals != null);

        return MakeClosure(address, globalTable ?? Globals);
    }

    /// <summary>
    ///     Loads a string containing a Lua/SolarSharp script.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="globalTable">The global table to bind to this chunk.</param>
    /// <param name="codeFriendlyName">
    ///     Name of the code - used to report errors, etc.
    /// </param>
    /// <returns>
    ///     A LuaValue containing a function which will execute the loaded code.
    /// </returns>
    public LuaValue LoadString(string code, Table globalTable = null, string codeFriendlyName = null)
    {
        this.CheckScriptOwnership(globalTable);

        if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
        {
            code = code[StringModule.BASE64_DUMP_HEADER.Length..];
            var data = Convert.FromBase64String(code);
            using MemoryStream ms = new(data);
            return LoadStream(ms, globalTable, codeFriendlyName);
        }

        var chunkName = $"{codeFriendlyName ?? "chunk_" + m_Sources.Count}";

        SourceCode source = new(codeFriendlyName ?? chunkName, code, m_Sources.Count, this);

        m_Sources.Add(source);

        var address = Loader_Fast.LoadChunk(this,
            source,
            m_ByteCode);

        return MakeClosure(address, globalTable ?? Globals);
    }

    /// <summary>
    ///     Loads a Lua/SolarSharp script from a System.IO.Stream. NOTE: This will *NOT* close the stream!
    /// </summary>
    /// <param name="stream">The stream containing code.</param>
    /// <param name="globalTable">The global table to bind to this chunk.</param>
    /// <param name="codeFriendlyName">Name of the code - used to report errors, etc.</param>
    /// <returns>
    ///     A LuaValue containing a function which will execute the loaded code.
    /// </returns>
    public LuaValue LoadStream(Stream stream, Table globalTable = null, string codeFriendlyName = null)
    {
        this.CheckScriptOwnership(globalTable);

        Stream codeStream = new UndisposableStream(stream);

        if (!Processor.IsDumpStream(codeStream))
        {
            using StreamReader sr = new(codeStream);
            var scriptCode = sr.ReadToEnd();
            return LoadString(scriptCode, globalTable, codeFriendlyName);
        }

        var chunkName = $"{codeFriendlyName ?? "dump_" + m_Sources.Count}";

        SourceCode source = new(codeFriendlyName ?? chunkName,
            $"-- This script was decoded from a binary dump - dump_{m_Sources.Count}",
            m_Sources.Count, this);

        m_Sources.Add(source);

        var address =
            m_MainProcessor.Undump(codeStream, m_Sources.Count - 1, globalTable ?? Globals, out var hasUpvalues);

        if (hasUpvalues)
            return MakeClosure(address, globalTable ?? Globals);
        return MakeClosure(address);
    }

    /// <summary>
    ///     Dumps on the specified stream.
    /// </summary>
    /// <param name="function">The function.</param>
    /// <param name="stream">The stream.</param>
    /// <exception cref="ArgumentException">
    ///     function arg is not a function!
    ///     or
    ///     stream is readonly!
    ///     or
    ///     function arg has upvalues other than _ENV
    /// </exception>
    public void Dump(LuaValue function, Stream stream)
    {
        this.CheckScriptOwnership(function);

        if (function.Type != DataType.Function)
            throw new ArgumentException("function arg is not a function!");

        if (!stream.CanWrite)
            throw new ArgumentException("stream is readonly!");

        var upvaluesType = function.Function.GetUpvaluesType();

        if (upvaluesType == Closure.UpvaluesType.Closure)
            throw new ArgumentException("function arg has upvalues other than _ENV");

        UndisposableStream outStream = new(stream);
        m_MainProcessor.Dump(outStream, function.Function.EntryPointByteCodeLocation,
            upvaluesType == Closure.UpvaluesType.Environment);
    }


    /// <summary>
    ///     Loads a string containing a Lua/SolarSharp script.
    /// </summary>
    /// <param name="filename">The code.</param>
    /// <param name="globalContext">The global table to bind to this chunk.</param>
    /// <param name="friendlyFilename">The filename to be used in error messages.</param>
    /// <returns>
    ///     A LuaValue containing a function which will execute the loaded code.
    /// </returns>
    public LuaValue LoadFile(string filename, Table globalContext = null, string friendlyFilename = null)
    {
        this.CheckScriptOwnership(globalContext);

#pragma warning disable 618
        filename = Options.ScriptLoader.ResolveFileName(filename);
#pragma warning restore 618

        var code = Options.ScriptLoader.LoadFile(filename);
        switch (code)
        {
            case string v: return LoadString(v, globalContext, friendlyFilename ?? filename);
            case byte[] bytes:
                using (MemoryStream ms = new(bytes))
                {
                    return LoadStream(ms, globalContext, friendlyFilename ?? filename);
                }
            case Stream stream:
                using (stream)
                {
                    return LoadStream(stream, globalContext, friendlyFilename ?? filename);
                }
            case null: throw new InvalidCastException("Unexpected null from IScriptLoader.LoadFile");
            default:
                throw new InvalidCastException(
                    $"Unsupported return type from IScriptLoader.LoadFile : {code.GetType()}");
        }
    }


    /// <summary>
    ///     Loads and executes a string containing a Lua/SolarSharp script.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="globalContext">The global context.</param>
    /// <param name="codeFriendlyName">
    ///     Name of the code - used to report errors, etc.
    /// </param>
    /// <returns>
    ///     A LuaValue containing the result of the processing of the loaded chunk.
    /// </returns>
    public LuaValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
    {
        var func = LoadString(code, globalContext, codeFriendlyName);
        return Call(func);
    }


    /// <summary>
    ///     Loads and executes a stream containing a Lua/SolarSharp script.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="globalContext">The global context.</param>
    /// <param name="codeFriendlyName">
    ///     Name of the code - used to report errors, etc.
    /// </param>
    /// <returns>
    ///     A LuaValue containing the result of the processing of the loaded chunk.
    /// </returns>
    public LuaValue DoStream(Stream stream, Table globalContext = null, string codeFriendlyName = null)
    {
        var func = LoadStream(stream, globalContext, codeFriendlyName);
        return Call(func);
    }


    /// <summary>
    ///     Loads and executes a file containing a Lua/SolarSharp script.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="globalContext">The global context.</param>
    /// <param name="codeFriendlyName">
    ///     Name of the code - used to report errors, etc.
    /// </param>
    /// <returns>
    ///     A LuaValue containing the result of the processing of the loaded chunk.
    /// </returns>
    public LuaValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
    {
        var func = LoadFile(filename, globalContext, codeFriendlyName);
        return Call(func);
    }


    /// <summary>
    ///     Runs the specified file with all possible defaults for quick experimenting.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// A LuaValue containing the result of the processing of the executed script.
    public static LuaValue RunFile(string filename)
    {
        Script S = new();
        return S.DoFile(filename);
    }

    /// <summary>
    ///     Runs the specified code with all possible defaults for quick experimenting.
    /// </summary>
    /// <param name="code">The Lua/SolarSharp code.</param>
    /// A LuaValue containing the result of the processing of the executed script.
    public static LuaValue RunString(string code)
    {
        Script S = new();
        return S.DoString(code);
    }

    /// <summary>
    ///     Creates a closure from a bytecode address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="envTable">The env table to create a 0-upvalue</param>
    /// <returns></returns>
    private LuaValue MakeClosure(int address, Table envTable = null)
    {
        this.CheckScriptOwnership(envTable);
        Closure c;

        if (envTable == null)
        {
            var meta = m_MainProcessor.FindMeta(ref address);

            // if we find the meta for a new chunk, we use the value in the meta for the _ENV upvalue
            c = meta != null && meta.NumVal2 == (int)OpCodeMetadataType.ChunkEntrypoint
                ? new Closure(this, address,
                    [SymbolRef.Upvalue(WellKnownSymbols.ENV, 0)],
                    [meta.Value])
                : new Closure(this, address, [], []);
        }
        else
        {
            SymbolRef[] syms =
            [
                new() { i_Env = null, i_Index = 0, i_Name = WellKnownSymbols.ENV, i_Type = SymbolRefType.DefaultEnv }
            ];

            LuaValue[] vals =
            [
                LuaValue.NewTable(envTable)
            ];

            c = new Closure(this, address, syms, vals);
        }

        return LuaValue.NewClosure(c);
    }

    /// <summary>
    ///     Calls the specified function.
    /// </summary>
    /// <param name="function">The Lua/SolarSharp function to be called</param>
    /// <returns>
    ///     The return value(s) of the function call.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
    public LuaValue Call(LuaValue function)
    {
        return Call(function, []);
    }

    /// <summary>
    ///     Calls the specified function.
    /// </summary>
    /// <param name="function">The Lua/SolarSharp function to be called</param>
    /// <param name="args">The arguments to pass to the function.</param>
    /// <returns>
    ///     The return value(s) of the function call.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
    public LuaValue Call(LuaValue function, params LuaValue[] args)
    {
        this.CheckScriptOwnership(function);
        this.CheckScriptOwnership(args);

        if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
        {
            var metafunction = m_MainProcessor.GetMetamethod(function, "__call");

            if (metafunction != null)
            {
                var metaargs = new LuaValue[args.Length + 1];
                metaargs[0] = function;
                for (var i = 0; i < args.Length; i++)
                    metaargs[i + 1] = args[i];

                function = metafunction;
                args = metaargs;
            }
            else
            {
                throw new ArgumentException("function is not a function and has no __call metamethod.");
            }
        }
        else if (function.Type == DataType.ClrFunction)
        {
            return function.Callback.ClrCallback(CreateDynamicExecutionContext(), new CallbackArguments(args, false));
        }

        return m_MainProcessor.Call(function, args);
    }

    /// <summary>
    ///     Calls the specified function.
    /// </summary>
    /// <param name="function">The Lua/SolarSharp function to be called</param>
    /// <param name="args">The arguments to pass to the function.</param>
    /// <returns>
    ///     The return value(s) of the function call.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
    public LuaValue Call(LuaValue function, params object[] args)
    {
        var dargs = new LuaValue[args.Length];

        for (var i = 0; i < dargs.Length; i++)
            dargs[i] = LuaValue.FromObject(this, args[i]);

        return Call(function, dargs);
    }

    /// <summary>
    ///     Calls the specified function.
    /// </summary>
    /// <param name="function">The Lua/SolarSharp function to be called</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
    public LuaValue Call(object function)
    {
        return Call(LuaValue.FromObject(this, function));
    }

    /// <summary>
    ///     Calls the specified function.
    /// </summary>
    /// <param name="function">The Lua/SolarSharp function to be called </param>
    /// <param name="args">The arguments to pass to the function.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
    public LuaValue Call(object function, params object[] args)
    {
        return Call(LuaValue.FromObject(this, function), args);
    }

    /// <summary>
    ///     Creates a coroutine pointing at the specified function.
    /// </summary>
    /// <param name="function">The function.</param>
    /// <returns>
    ///     The coroutine handle.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
    public LuaValue CreateCoroutine(LuaValue function)
    {
        this.CheckScriptOwnership(function);

        if (function.Type == DataType.Function)
            return m_MainProcessor.Coroutine_Create(function.Function);
        if (function.Type == DataType.ClrFunction)
            return LuaValue.NewCoroutine(new Coroutine(function.Callback));
        throw new ArgumentException("function is not of DataType.Function or DataType.ClrFunction");
    }

    /// <summary>
    ///     Creates a new coroutine, recycling buffers from a dead coroutine to skip slower buffer creation in Mono.
    /// </summary>
    /// <param name="coroutine">
    ///     The <see cref="Coroutine" /> to recycle. This coroutine's state must be
    ///     <see cref="CoroutineState.Dead" />
    /// </param>
    /// <param name="function">The function</param>
    /// <returns>
    ///     The new coroutine handle.
    /// </returns>
    public LuaValue RecycleCoroutine(Coroutine coroutine, LuaValue function)
    {
        this.CheckScriptOwnership(coroutine);
        this.CheckScriptOwnership(function);

        if (coroutine is not { Type: Coroutine.CoroutineType.Coroutine })
            throw new InvalidOperationException("coroutine is not CoroutineType.Coroutine");
        if (function is not { Type: DataType.Function })
            throw new InvalidOperationException("function is not DataType.Function");
        if (coroutine.State != CoroutineState.Dead)
            throw new InvalidOperationException("coroutine's state must be CoroutineState.Dead to recycle");

        return coroutine.Recycle(m_MainProcessor, function.Function);
    }

    /// <summary>
    ///     Creates a coroutine pointing at the specified function.
    /// </summary>
    /// <param name="function">The function.</param>
    /// <returns>
    ///     The coroutine handle.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
    public LuaValue CreateCoroutine(object function)
    {
        return CreateCoroutine(LuaValue.FromObject(this, function));
    }

    /// <summary>
    ///     Gets the source code.
    /// </summary>
    /// <param name="sourceCodeID">The source code identifier.</param>
    /// <returns></returns>
    public SourceCode GetSourceCode(int sourceCodeID)
    {
        return m_Sources[sourceCodeID];
    }

    /// <summary>
    ///     Loads a module as per the "require" Lua function. http://www.lua.org/pil/8.1.html
    /// </summary>
    /// <param name="modname">The module name</param>
    /// <param name="globalContext">The global context.</param>
    /// <returns></returns>
    /// <exception cref="ScriptRuntimeException">Raised if module is not found</exception>
    public LuaValue RequireModule(string modname, Table globalContext = null)
    {
        this.CheckScriptOwnership(globalContext);

        var globals = globalContext ?? Globals;
        var filename = Options.ScriptLoader.ResolveModuleName(modname, globals) ??
                       throw new ScriptRuntimeException("module '{0}' not found", modname);
        var func = LoadFile(filename, globalContext, filename);
        return func;
    }

    /// <summary>
    ///     Gets a type metatable.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns></returns>
    public Table GetTypeMetatable(DataType type)
    {
        var t = (int)type;

        if (t >= 0 && t < m_TypeMetatables.Length)
            return m_TypeMetatables[t];

        return null;
    }

    /// <summary>
    ///     Sets a type metatable.
    /// </summary>
    /// <param name="type">The type. Must be Nil, Boolean, Number, String or Function</param>
    /// <param name="metatable">The metatable.</param>
    /// <exception cref="ArgumentException">Specified type not supported :  + type.ToString()</exception>
    public void SetTypeMetatable(DataType type, Table metatable)
    {
        this.CheckScriptOwnership(metatable);

        var t = (int)type;

        m_TypeMetatables[t] = t >= 0 && t < m_TypeMetatables.Length
            ? metatable
            : throw new ArgumentException("Specified type not supported : " + type);
    }


    /// <summary>
    ///     Warms up the parser/lexer structures so that SolarSharp operations start faster.
    /// </summary>
    public static void WarmUp()
    {
        Script s = new(CoreModules.Basic);
        s.LoadString("return 1;");
    }


    /// <summary>
    ///     Creates a new dynamic expression.
    /// </summary>
    /// <param name="code">The code of the expression.</param>
    /// <returns></returns>
    public DynamicExpression CreateDynamicExpression(string code)
    {
        var dee = Loader_Fast.LoadDynamicExpr(this, new SourceCode("__dynamic", code, -1, this));
        return new DynamicExpression(this, code, dee);
    }

    /// <summary>
    ///     Creates a new dynamic expression which is actually quite static, returning always the same constant value.
    /// </summary>
    /// <param name="code">The code of the not-so-dynamic expression.</param>
    /// <param name="constant">The constant to return.</param>
    /// <returns></returns>
    public DynamicExpression CreateConstantDynamicExpression(string code, LuaValue constant)
    {
        this.CheckScriptOwnership(constant);

        return new DynamicExpression(this, code, constant);
    }

    /// <summary>
    ///     Gets an execution context exposing only partial functionality, which should be used for
    ///     those cases where the execution engine is not really running - for example for dynamic expression
    ///     or calls from CLR to CLR callbacks
    /// </summary>
    internal ScriptExecutionContext CreateDynamicExecutionContext()
    {
        return new ScriptExecutionContext(m_MainProcessor, null);
    }

    /// <summary>
    ///     Gets a banner string with copyright info, link to website, version, etc.
    /// </summary>
    public static string GetBanner(string subproduct = null)
    {
        subproduct = subproduct != null ? subproduct + " " : "";

        StringBuilder sb = new();
        sb.AppendLine($"SolarSharp {subproduct}{VERSION} [{GlobalOptions.Platform.GetPlatformName()}]");
        sb.AppendLine("Copyright (C) 2014-2016 Marco Mastropaolo");
        sb.AppendLine("http://www.SolarSharp.org");
        return sb.ToString();
    }
}