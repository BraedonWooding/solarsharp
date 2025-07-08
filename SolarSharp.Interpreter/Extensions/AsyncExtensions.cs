#if HASDYNAMIC
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SolarSharp.Interpreter.REPL;

namespace SolarSharp.Interpreter.Extensions
{
    /// <summary>
    /// This class contains extension methods providing async wrappers of many methods.
    /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
    ///
    /// This type is supported only on .NET 4.x and .NET 4.x PCL targets.
    /// </summary>
    public static class AsyncExtensions
    {
        /// <summary>
        /// Calls a Lua function, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="function">The function.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public static Task<DynValue> CallAsync(
            this Script script,
            DynValue function,
            params object[] args
        )
        {
            return Task.Factory.StartNew(
                () => script.Call(function, args),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Calls a Lua function, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="function">The function.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public static Task<DynValue> CallAsync(
            this Script script,
            object function,
            params object[] args
        )
        {
            return Task.Factory.StartNew(
                () => script.Call(function, args),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Calls a function, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public static Task<DynValue> CallAsync(this DynValue function, params object[] args)
        {
            return CallAsync(function.Function.OwnerScript, function, args);
        }

        /// <summary>
        /// Loads and executes a string, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="code">The code.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code friendly.</param>
        /// <returns></returns>
        public static Task<DynValue> DoStringAsync(
            this Script script,
            string code,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            return Task.Factory.StartNew(
                () => script.DoString(code, globalContext, codeFriendlyName),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Loads and executes a file, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="filename">The filename.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code friendly.</param>
        /// <returns></returns>
        public static Task<DynValue> DoFileAsync(
            this Script script,
            string filename,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            return Task.Factory.StartNew(
                () => script.DoFile(filename, globalContext, codeFriendlyName),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Loads and executes a stream, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="globalContext">The global context.</param>
        /// <param name="codeFriendlyName">Name of the code friendly.</param>
        /// <returns></returns>
        public static Task<DynValue> DoStreamAsync(
            this Script script,
            Stream stream,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            return Task.Factory.StartNew(
                () => script.DoStream(stream, globalContext, codeFriendlyName),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Asynchronously loads a string, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="code">The code.</param>
        /// <param name="globalTable">The global table.</param>
        /// <param name="codeFriendlyName">Name of the code friendly.</param>
        /// <returns></returns>
        public static Task<DynValue> LoadStringAsync(
            this Script script,
            string code,
            Table globalTable = null,
            string codeFriendlyName = null
        )
        {
            return Task.Factory.StartNew(
                () => script.LoadString(code, globalTable, codeFriendlyName),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Asynchronously loads a file, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="filename">The filename.</param>
        /// <param name="globalTable">The global table.</param>
        /// <param name="codeFriendlyName">Name of the code friendly.</param>
        /// <returns></returns>
        public static Task<DynValue> LoadFileAsync(
            this Script script,
            string filename,
            Table globalTable = null,
            string codeFriendlyName = null
        )
        {
            return Task.Factory.StartNew(
                () => script.LoadFile(filename, globalTable, codeFriendlyName),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Asynchronously loads a stream, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="globalTable">The global table.</param>
        /// <param name="codeFriendlyName">Name of the code friendly.</param>
        /// <returns></returns>
        public static Task<DynValue> LoadStreamAsync(
            this Script script,
            Stream stream,
            Table globalTable = null,
            string codeFriendlyName = null
        )
        {
            return Task.Factory.StartNew(
                () => script.LoadStream(stream, globalTable, codeFriendlyName),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Asynchronously creates a coroutine pointing to this script function, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns></returns>
        public static Task<DynValue> CreateCoroutineAsync(this DynValue function)
        {
            return Task.Factory.StartNew(
                () => function.Function.OwnerScript.CreateCoroutine(function),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// Asynchronously creates a coroutine pointing to this script function, awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="function">The function.</param>
        /// <returns></returns>
        public static Task<DynValue> CreateCoroutineAsync(this Script script, DynValue function)
        {
            return Task.Factory.StartNew(
                () => script.CreateCoroutine(function),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// REPL interpreter awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="input">The input.</param>
        /// <param name="interpreter">The interpreter.</param>
        /// <returns></returns>
        public static Task<DynValue> CreateDynamicExpressionAsync(
            this Script script,
            string input,
            ReplInterpreter interpreter = null
        )
        {
            return Task.Factory.StartNew(
                () => script.CreateDynamicExpression(input, interpreter),
                TaskCreationOptions.LongRunning
            );
        }

        /// <summary>
        /// REPL interpreter awaitable.
        /// Asynchronous execution is performed by scheduling the method on the thread pool (through a Task.Factory.StartNew).
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="input">The input.</param>
        /// <param name="interpreter">The interpreter.</param>
        /// <returns></returns>
        public static Task<DynValue> CreateDynamicExpressionAsync(
            this Script script,
            string input,
            out ReplInterpreter interpreter
        )
        {
            ReplInterpreter intepreter1 = new ReplInterpreter();
            interpreter = intepreter1;

            return Task.Factory.StartNew(
                () => script.CreateDynamicExpression(input, intepreter1),
                TaskCreationOptions.LongRunning
            );
        }
    }
}

#endif
