﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.CoreLib;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Tree.Fast_Interface;
using SolarSharp.Interpreter.DataStructs;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// This class implements a MoonSharp scripting session. Multiple Script objects can coexist in the same program but cannot share
    /// data among themselves unless some mechanism is put in place.
    /// </summary>
    public class LuaState
    {
        /// <summary>
        /// The version of the MoonSharp engine
        /// </summary>
        public const string VERSION = "2.0.0.0";

        /// <summary>
        /// The Lua version being supported
        /// </summary>
        public const string LUA_VERSION = "5.2";
        private readonly Processor m_MainProcessor = null;
        private readonly ByteCode m_ByteCode;
        private readonly Table m_GlobalTable;
        private readonly Table[] m_TypeMetatables = new Table[(int)LuaTypeExtensions.MaxMetaTypes];

        // TODO: Change stack size to something reasonable
        // this is 128kb which is actually pretty reasonable (but not per state...)
        private const int STACK_SIZE = 131072;
        private readonly FastStack<DynValue> _stack;
        private readonly FastStack<CallInfo> _callInfo;

        /// <summary>
        /// Initializes the <see cref="LuaState"/> class.
        /// </summary>
        static LuaState()
        {
            GlobalOptions = new ScriptGlobalOptions();

            DefaultOptions = new ScriptOptions()
            {
                DebugPrint = s => { GlobalOptions.Platform.DefaultPrint(s); },
                DebugInput = s => { return GlobalOptions.Platform.DefaultInput(s); },
                CheckThreadAccess = true,
                ScriptLoader = PlatformAutoDetector.GetDefaultScriptLoader(),
                TailCallOptimizationThreshold = 65536
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LuaState"/> class.
        /// </summary>
        public LuaState()
            : this(CoreModules.Preset_Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LuaState"/> class.
        /// </summary>
        /// <param name="coreModules">The core modules to be pre-registered in the default global table.</param>
        public LuaState(CoreModules coreModules)
        {
            Options = new ScriptOptions(DefaultOptions);
            PerformanceStats = new PerformanceStatistics();
            Registry = new Table(this);

            m_ByteCode = new ByteCode(this);
            m_MainProcessor = new Processor(this, m_GlobalTable, m_ByteCode);
            m_GlobalTable = new Table(this).RegisterCoreModules(coreModules);
        }

        /// <summary>
        /// Gets or sets the script loader which will be used as the value of the
        /// ScriptLoader property for all newly created scripts.
        /// </summary>
        public static ScriptOptions DefaultOptions { get; private set; }

        /// <summary>
        /// Gets access to the script options. 
        /// </summary>
        public ScriptOptions Options { get; private set; }

        /// <summary>
        /// Gets the global options, that is options which cannot be customized per-script.
        /// </summary>
        public static ScriptGlobalOptions GlobalOptions { get; private set; }

        /// <summary>
        /// Gets access to performance statistics.
        /// </summary>
        public PerformanceStatistics PerformanceStats { get; private set; }

        /// <summary>
        /// Gets the default global table for this script. Unless a different table is intentionally passed (or setfenv has been used)
        /// execution uses this table.
        /// </summary>
        public Table Globals
        {
            get { return m_GlobalTable; }
        }

        /// <summary>
        /// Loads a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadString(string code, Table globalTable = null, string codeFriendlyName = null)
        {
            if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
            {
                code = code.Substring(StringModule.BASE64_DUMP_HEADER.Length);
                byte[] data = Convert.FromBase64String(code);
                using MemoryStream ms = new(data);
                return LoadStream(ms, globalTable, codeFriendlyName);
            }

            string chunkName = string.Format("{0}", codeFriendlyName ?? "?");
            int address = Loader_Fast.LoadChunk(this,
                source,
                m_ByteCode);

            return MakeClosure(address, globalTable ?? m_GlobalTable);
        }

        /// <summary>
        /// Loads a Lua/MoonSharp script from a System.IO.Stream. NOTE: This will *NOT* close the stream!
        /// </summary>
        /// <param name="stream">The stream containing code.</param>
        /// <param name="globalTable">The global table to bind to this chunk.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadStream(Stream stream, Table globalTable = null, string codeFriendlyName = null)
        {
            // note: I don't think it's that harmful for us to close... just needs to be a parameter
            using StreamReader sr = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            string scriptCode = sr.ReadToEnd();
            return LoadString(scriptCode, globalTable, codeFriendlyName);
        }

        /// <summary>
        /// Loads a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="filename">The code.</param>
        /// <param name="globalContext">The global table to bind to this chunk.</param>
        /// <param name="friendlyFilename">The filename to be used in error messages.</param>
        /// <returns>
        /// A DynValue containing a function which will execute the loaded code.
        /// </returns>
        public DynValue LoadFile(string filename, Table globalContext = null, string friendlyFilename = null)
        {
#pragma warning disable 618
            filename = Options.ScriptLoader.ResolveFileName(filename, globalContext ?? m_GlobalTable);
#pragma warning restore 618

            object code = Options.ScriptLoader.LoadFile(filename, globalContext ?? m_GlobalTable);
            switch (code)
            {
                case string v: return LoadString(v, globalContext, friendlyFilename ?? filename);
                case byte[] bytes: using (MemoryStream ms = new(bytes)) return LoadStream(ms, globalContext, friendlyFilename ?? filename);
                case Stream stream: using (stream) return LoadStream(stream, globalContext, friendlyFilename ?? filename);
                case null: throw new InvalidCastException("Unexpected null from IScriptLoader.LoadFile");
                default: throw new InvalidCastException(string.Format("Unsupported return type from IScriptLoader.LoadFile : {0}", code.GetType()));
            }
        }

        /// <summary>
        /// Loads and executes a string containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
        {
            DynValue func = LoadString(code, globalContext, codeFriendlyName);
            return Call(func);
        }

        /// <summary>
        /// Loads and executes a stream containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoStream(Stream stream, Table globalContext = null, string codeFriendlyName = null)
        {
            DynValue func = LoadStream(stream, globalContext, codeFriendlyName);
            return Call(func);
        }

        /// <summary>
        /// Loads and executes a file containing a Lua/MoonSharp script.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
        /// <returns>
        /// A DynValue containing the result of the processing of the loaded chunk.
        /// </returns>
        public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
        {
            DynValue func = LoadFile(filename, globalContext, codeFriendlyName);
            return Call(func);
        }

        /// <summary>
        /// Runs the specified file with all possible defaults for quick experimenting.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// A DynValue containing the result of the processing of the executed script.
        public static DynValue RunFile(string filename)
        {
            LuaState S = new();
            return S.DoFile(filename);
        }

        /// <summary>
        /// Runs the specified code with all possible defaults for quick experimenting.
        /// </summary>
        /// <param name="code">The Lua/MoonSharp code.</param>
        /// A DynValue containing the result of the processing of the executed script.
        public static DynValue RunString(string code)
        {
            LuaState S = new();
            return S.DoString(code);
        }

        /// <summary>
        /// Creates a closure from a bytecode address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="envTable">The env table to create a 0-upvalue</param>
        /// <returns></returns>
        private DynValue MakeClosure(int address, Table envTable = null)
        {
            Closure c;

            if (envTable == null)
            {
                // TODO:
                Instruction meta = null;// m_MainProcessor.FindMeta(ref address);

                // if we find the meta for a new chunk, we use the value in the meta for the _ENV upvalue
                c = meta != null && meta.NumVal2 == (int)OpCodeMetadataType.ChunkEntrypoint
                    ? new Closure(address, new SymbolRef[] { SymbolRef.Upvalue(WellKnownSymbols.ENV, 0) },
                        new DynValue[] { meta.Value })
                    : new Closure(address, new SymbolRef[0], new DynValue[0]);
            }
            else
            {
                var syms = new SymbolRef[] {
                    new() { i_Env = null, i_Index= 0, i_Name = WellKnownSymbols.ENV, i_Type =  SymbolRefType.DefaultEnv },
                };

                var vals = new DynValue[] {
                    DynValue.NewTable(envTable)
                };

                c = new Closure(address, syms, vals);
            }

            return DynValue.NewClosure(c);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <returns>
        /// The return value(s) of the function call.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue function)
        {
            return Call(function, new DynValue[0]);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns>
        /// The return value(s) of the function call.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue function, params DynValue[] args)
        {
            if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
            {
                DynValue metafunction = m_MainProcessor.GetMetamethod(function, "__call");

                if (metafunction.IsNotNil())
                {
                    DynValue[] metaargs = new DynValue[args.Length + 1];
                    metaargs[0] = function;
                    for (int i = 0; i < args.Length; i++)
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
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns>
        /// The return value(s) of the function call.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue function, params object[] args)
        {
            DynValue[] dargs = new DynValue[args.Length];

            for (int i = 0; i < dargs.Length; i++)
                dargs[i] = DynValue.FromObject(this, args[i]);

            return Call(function, dargs);
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(object function)
        {
            return Call(DynValue.FromObject(this, function));
        }

        /// <summary>
        /// Calls the specified function.
        /// </summary>
        /// <param name="function">The Lua/MoonSharp function to be called </param>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(object function, params object[] args)
        {
            return Call(DynValue.FromObject(this, function), args);
        }

        /// <summary>
        /// Creates a coroutine pointing at the specified function.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns>
        /// The coroutine handle.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
        public DynValue CreateCoroutine(DynValue function)
        {
            if (function.Type == DataType.Function)
                return m_MainProcessor.Coroutine_Create(function.Function);
            else if (function.Type == DataType.ClrFunction)
                return DynValue.NewCoroutine(new Coroutine(function.Callback));
            else
                throw new ArgumentException("function is not of DataType.Function or DataType.ClrFunction");
        }

        /// <summary>
        /// Creates a new coroutine, recycling buffers from a dead coroutine to skip slower buffer creation in Mono.
        /// </summary>
        /// <param name="coroutine">The <see cref="Coroutine"/> to recycle. This coroutine's state must be <see cref="CoroutineState.Dead"/></param>
        /// <param name="function">The function</param>
        /// <returns>
        /// The new coroutine handle.
        /// </returns>
        public DynValue RecycleCoroutine(Coroutine coroutine, DynValue function)
        {
            if (coroutine == null || coroutine.Type != Coroutine.CoroutineType.Coroutine)
                throw new InvalidOperationException("coroutine is not CoroutineType.Coroutine");
            if (function.IsNil() || function.Type != DataType.Function)
                throw new InvalidOperationException("function is not DataType.Function");
            if (coroutine.State != CoroutineState.Dead)
                throw new InvalidOperationException("coroutine's state must be CoroutineState.Dead to recycle");

            return coroutine.Recycle(m_MainProcessor, function.Function);
        }

        /// <summary>
        /// Creates a coroutine pointing at the specified function.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns>
        /// The coroutine handle.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
        public DynValue CreateCoroutine(object function)
        {
            return CreateCoroutine(DynValue.FromObject(this, function));
        }

        /// <summary>
        /// Gets the source code count.
        /// </summary>
        /// <value>
        /// The source code count.
        /// </value>
        public int SourceCodeCount
        {
            get { return m_Sources.Count; }
        }

        /// <summary>
        /// Loads a module as per the "require" Lua function. http://www.lua.org/pil/8.1.html
        /// </summary>
        /// <param name="modname">The module name</param>
        /// <param name="globalContext">The global context.</param>
        /// <returns></returns>
        /// <exception cref="ErrorException">Raised if module is not found</exception>
        public DynValue RequireModule(string modname, Table globalContext = null)
        {
            Table globals = globalContext ?? m_GlobalTable;
            string filename = Options.ScriptLoader.ResolveModuleName(modname, globals) ?? throw new ErrorException("module '{0}' not found", modname);
            DynValue func = LoadFile(filename, globalContext, filename);
            return func;
        }

        /// <summary>
        /// Gets a type metatable.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public Table GetTypeMetatable(DataType type)
        {
            int t = (int)type;

            if (t >= 0 && t < m_TypeMetatables.Length)
                return m_TypeMetatables[t];

            return null;
        }

        /// <summary>
        /// Sets a type metatable.
        /// </summary>
        /// <param name="type">The type. Must be Nil, Boolean, Number, String or Function</param>
        /// <param name="metatable">The metatable.</param>
        /// <exception cref="ArgumentException">Specified type not supported :  + type.ToString()</exception>
        public void SetTypeMetatable(DataType type, Table metatable)
        {
            int t = (int)type;

            m_TypeMetatables[t] = t >= 0 && t < m_TypeMetatables.Length
                ? metatable
                : throw new ArgumentException("Specified type not supported : " + type.ToString());
        }


        /// <summary>
        /// Warms up the parser/lexer structures so that MoonSharp operations start faster.
        /// </summary>
        public static void WarmUp()
        {
            LuaState s = new(CoreModules.Basic);
            s.LoadString("return 1;");
        }

        /// <summary>
        /// MoonSharp (like Lua itself) provides a registry, a predefined table that can be used by any CLR code to 
        /// store whatever Lua values it needs to store. 
        /// Any CLR code can store data into this table, but it should take care to choose keys 
        /// that are different from those used by other libraries, to avoid collisions. 
        /// Typically, you should use as key a string GUID, a string containing your library name, or a 
        /// userdata with the address of a CLR object in your code.
        /// </summary>
        public Table Registry
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a banner string with copyright info, link to website, version, etc.
        /// </summary>
        public static string GetBanner(string subproduct = null)
        {
            subproduct = subproduct != null ? subproduct + " " : "";

            StringBuilder sb = new();
            sb.AppendLine(string.Format("MoonSharp {0}{1} [{2}]", subproduct, VERSION, GlobalOptions.Platform.GetPlatformName()));
            sb.AppendLine("Copyright (C) 2014-2016 Marco Mastropaolo");
            sb.AppendLine("http://www.SolarSharp.org");
            return sb.ToString();
        }
    }
}