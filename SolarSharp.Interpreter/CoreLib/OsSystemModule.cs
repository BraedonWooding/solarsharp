using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

#pragma warning disable IDE0060 // Remove unused parameter

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing system related Lua functions from the 'os' module.
///     Proper support requires a compatible IPlatformAccessor
/// </summary>
[SolarSharpModule(Namespace = "os")]
public class OsSystemModule
{
    [SolarSharpModuleMethod]
    public static DynValue execute(ScriptExecutionContext _, CallbackArguments args)
    {
        var v = args.AsType(0, "execute", DataType.String, true);

        if (v.IsNil()) return DynValue.NewBoolean(true);

        try
        {
            var exitCode = Script.GlobalOptions.Platform.OS_Execute(v.String);

            return DynValue.NewTuple(
                DynValue.Nil,
                DynValue.NewString("exit"),
                DynValue.NewNumber(exitCode));
        }
        catch (Exception)
        {
            // +++ bad to swallow.. 
            return DynValue.Nil;
        }
    }

    [SolarSharpModuleMethod]
    public static DynValue exit(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_exitCode = args.AsType(0, "exit", DataType.Number, true);
        var exitCode = 0;

        if (v_exitCode.IsNotNil())
            exitCode = (int)v_exitCode.Number;

        Script.GlobalOptions.Platform.OS_ExitFast(exitCode);

        throw new InvalidOperationException("Unreachable code.. reached.");
    }

    [SolarSharpModuleMethod]
    public static DynValue getenv(ScriptExecutionContext _, CallbackArguments args)
    {
        var varName = args.AsType(0, "getenv", DataType.String);

        var val = Script.GlobalOptions.Platform.GetEnvironmentVariable(varName.String);

        if (val == null)
            return DynValue.Nil;
        return DynValue.NewString(val);
    }

    [SolarSharpModuleMethod]
    public static DynValue remove(ScriptExecutionContext _, CallbackArguments args)
    {
        var fileName = args.AsType(0, "remove", DataType.String).String;

        try
        {
            if (Script.GlobalOptions.Platform.OS_FileExists(fileName))
            {
                Script.GlobalOptions.Platform.OS_FileDelete(fileName);
                return DynValue.True;
            }

            return DynValue.NewTuple(
                DynValue.Nil,
                DynValue.NewString("{0}: No such file or directory.", fileName),
                DynValue.NewNumber(-1));
        }
        catch (Exception ex)
        {
            return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(ex.Message), DynValue.NewNumber(-1));
        }
    }

    [SolarSharpModuleMethod]
    public static DynValue rename(ScriptExecutionContext _, CallbackArguments args)
    {
        var fileNameOld = args.AsType(0, "rename", DataType.String).String;
        var fileNameNew = args.AsType(1, "rename", DataType.String).String;

        try
        {
            if (!Script.GlobalOptions.Platform.OS_FileExists(fileNameOld))
                return DynValue.NewTuple(DynValue.Nil,
                    DynValue.NewString("{0}: No such file or directory.", fileNameOld),
                    DynValue.NewNumber(-1));

            Script.GlobalOptions.Platform.OS_FileMove(fileNameOld, fileNameNew);
            return DynValue.True;
        }
        catch (Exception ex)
        {
            return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(ex.Message), DynValue.NewNumber(-1));
        }
    }

    [SolarSharpModuleMethod]
    public static DynValue setlocale(ScriptExecutionContext _, CallbackArguments _args)
    {
        // TODO:
        return DynValue.NewString("n/a");
    }

    [SolarSharpModuleMethod]
    public static DynValue tmpname(ScriptExecutionContext _, CallbackArguments _args)
    {
        return DynValue.NewString(Script.GlobalOptions.Platform.IO_OS_GetTempFilename());
    }
}

#pragma warning restore IDE0060 // Remove unused parameter