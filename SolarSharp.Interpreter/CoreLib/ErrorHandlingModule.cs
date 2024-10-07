﻿using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing error handling Lua functions (pcall and xpcall)
    /// </summary>
    [MoonSharpModule]
    public class ErrorHandlingModule
    {
        [MoonSharpModuleMethod]
        public static DynValue pcall(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return SetErrorHandlerStrategy("pcall", executionContext, args, DynValue.Nil);
        }

        private static DynValue SetErrorHandlerStrategy(string funcName,
            ScriptExecutionContext executionContext,
            CallbackArguments args,
            DynValue handlerBeforeUnwind)
        {
            DynValue v = args[0];
            DynValue[] a = new DynValue[args.Count - 1];

            for (int i = 1; i < args.Count; i++)
                a[i - 1] = args[i];

            if (args[0].Type == DataType.ClrFunction)
            {
                try
                {
                    DynValue ret = args[0].Callback.Invoke(executionContext, a);
                    if (ret.Type == DataType.TailCallRequest)
                    {
                        if (ret.TailCallData.Continuation != null || ret.TailCallData.ErrorHandler != null)
                            throw new ErrorException("the function passed to {0} cannot be called directly by {0}. wrap in a script function instead.", funcName);

                        return DynValue.NewTailCallReq(new TailCallData()
                        {
                            Args = ret.TailCallData.Args,
                            Function = ret.TailCallData.Function,
                            Continuation = new CallbackFunction(pcall_continuation, funcName),
                            ErrorHandler = new CallbackFunction(pcall_onerror, funcName),
                            ErrorHandlerBeforeUnwind = handlerBeforeUnwind
                        });
                    }
                    else if (ret.Type == DataType.YieldRequest)
                    {
                        throw new ErrorException("the function passed to {0} cannot be called directly by {0}. wrap in a script function instead.", funcName);
                    }
                    else
                    {
                        return DynValue.NewTupleNested(DynValue.True, ret);
                    }
                }
                catch (ErrorException ex)
                {
                    executionContext.PerformMessageDecorationBeforeUnwind(handlerBeforeUnwind, ex);
                    return DynValue.NewTupleNested(DynValue.False, DynValue.NewString(ex.DecoratedMessage));
                }
            }
            else if (args[0].Type != DataType.Function)
            {
                return DynValue.NewTupleNested(DynValue.False, DynValue.NewString("attempt to " + funcName + " a non-function"));
            }
            else
            {
                return DynValue.NewTailCallReq(new TailCallData()
                {
                    Args = a,
                    Function = v,
                    Continuation = new CallbackFunction(pcall_continuation, funcName),
                    ErrorHandler = new CallbackFunction(pcall_onerror, funcName),
                    ErrorHandlerBeforeUnwind = handlerBeforeUnwind
                });
            }
        }

        private static DynValue MakeReturnTuple(bool retstatus, CallbackArguments args)
        {
            DynValue[] rets = new DynValue[args.Count + 1];

            for (int i = 0; i < args.Count; i++)
                rets[i + 1] = args[i];

            rets[0] = DynValue.NewBoolean(retstatus);

            return DynValue.NewTuple(rets);
        }


        public static DynValue pcall_continuation(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return MakeReturnTuple(true, args);
        }

        public static DynValue pcall_onerror(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return MakeReturnTuple(false, args);
        }


        [MoonSharpModuleMethod]
        public static DynValue xpcall(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            List<DynValue> a = new();

            for (int i = 0; i < args.Count; i++)
            {
                if (i != 1)
                    a.Add(args[i]);
            }

            DynValue handler = DynValue.Nil;
            if (args[1].Type == DataType.Function || args[1].Type == DataType.ClrFunction)
            {
                handler = args[1];
            }
            else if (args[1].Type != DataType.Nil)
            {
                args.AsType(1, "xpcall", DataType.Function, false);
            }

            return SetErrorHandlerStrategy("xpcall", executionContext, new CallbackArguments(a, false), handler);
        }

    }
}
