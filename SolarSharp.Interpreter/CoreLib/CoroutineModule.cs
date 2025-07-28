using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing coroutine Lua functions
/// </summary>
[SolarSharpModule(Namespace = "coroutine")]
public class CoroutineModule
{
    [SolarSharpModuleMethod]
    public static DynValue create(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        if (args[0].Type != DataType.Function && args[0].Type != DataType.ClrFunction)
            args.AsType(0, "create", DataType.Function); // this throws

        return executionContext.GetScript().CreateCoroutine(args[0]);
    }

    [SolarSharpModuleMethod]
    public static DynValue wrap(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        if (args[0].Type != DataType.Function && args[0].Type != DataType.ClrFunction)
            args.AsType(0, "wrap", DataType.Function); // this throws

        var v = create(executionContext, args);
        var c = DynValue.NewCallback((_, args) => v.Coroutine.Resume(args.GetArray()));
        return c;
    }

    [SolarSharpModuleMethod]
    public static DynValue resume(ScriptExecutionContext _, CallbackArguments args)
    {
        var handle = args.AsType(0, "resume", DataType.Thread);

        try
        {
            var ret = handle.Coroutine.Resume(args.GetArray(1));

            List<DynValue> retval = [DynValue.True];

            if (ret.Type == DataType.Tuple)
                for (var i = 0; i < ret.Tuple.Length; i++)
                {
                    var v = ret.Tuple[i];

                    if (i == ret.Tuple.Length - 1 && v.Type == DataType.Tuple)
                        retval.AddRange(v.Tuple);
                    else
                        retval.Add(v);
                }
            else
                retval.Add(ret);

            return DynValue.NewTuple(retval.ToArray());
        }
        catch (ScriptRuntimeException ex)
        {
            return DynValue.NewTuple(
                DynValue.False,
                DynValue.NewString(ex.Message));
        }
    }

    [SolarSharpModuleMethod]
    public static DynValue yield(ScriptExecutionContext _, CallbackArguments args)
    {
        return DynValue.NewYieldReq(args.GetArray());
    }

    [SolarSharpModuleMethod]
    public static DynValue running(ScriptExecutionContext executionContext, CallbackArguments _)
    {
        var C = executionContext.GetCallingCoroutine();
        return DynValue.NewTuple(DynValue.NewCoroutine(C), DynValue.NewBoolean(C.State == CoroutineState.Main));
    }

    [SolarSharpModuleMethod]
    public static DynValue status(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var handle = args.AsType(0, "status", DataType.Thread);
        var running = executionContext.GetCallingCoroutine();
        var cs = handle.Coroutine.State;

        switch (cs)
        {
            case CoroutineState.Main:
            case CoroutineState.Running:
                return handle.Coroutine == running ? DynValue.NewString("running") : DynValue.NewString("normal");
            case CoroutineState.NotStarted:
            case CoroutineState.Suspended:
                return DynValue.NewString("suspended");
            case CoroutineState.Dead:
                return DynValue.NewString("dead");
            default:
                throw new InternalErrorException("Unexpected coroutine state {0}", cs);
        }
    }
}