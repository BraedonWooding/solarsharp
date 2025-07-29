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
    public static LuaValue execute(ScriptExecutionContext _, CallbackArguments args)
    {
        var v = args.AsType(0, "execute", DataType.String, true);

        if (v.IsNil()) return LuaValue.NewBoolean(true);

        try
        {
            var exitCode = Script.GlobalOptions.Platform.OS_Execute(v.String);

            return LuaValue.NewTuple(
                LuaValue.Nil,
                LuaValue.NewString("exit"),
                LuaValue.NewNumber(exitCode));
        }
        catch (Exception)
        {
            // +++ bad to swallow.. 
            return LuaValue.Nil;
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue exit(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_exitCode = args.AsType(0, "exit", DataType.Number, true);
        var exitCode = 0;

        if (v_exitCode.IsNotNil())
            exitCode = (int)v_exitCode.Number;

        Script.GlobalOptions.Platform.OS_ExitFast(exitCode);

        throw new InvalidOperationException("Unreachable code.. reached.");
    }

    [SolarSharpModuleMethod]
    public static LuaValue getenv(ScriptExecutionContext _, CallbackArguments args)
    {
        var varName = args.AsType(0, "getenv", DataType.String);

        var val = Script.GlobalOptions.Platform.GetEnvironmentVariable(varName.String);

        if (val == null)
            return LuaValue.Nil;
        return LuaValue.NewString(val);
    }

    [SolarSharpModuleMethod]
    public static LuaValue remove(ScriptExecutionContext _, CallbackArguments args)
    {
        var fileName = args.AsType(0, "remove", DataType.String).String;

        try
        {
            if (Script.GlobalOptions.Platform.OS_FileExists(fileName))
            {
                Script.GlobalOptions.Platform.OS_FileDelete(fileName);
                return LuaValue.True;
            }

            return LuaValue.NewTuple(
                LuaValue.Nil,
                LuaValue.NewString("{0}: No such file or directory.", fileName),
                LuaValue.NewNumber(-1));
        }
        catch (Exception ex)
        {
            return LuaValue.NewTuple(LuaValue.Nil, LuaValue.NewString(ex.Message), LuaValue.NewNumber(-1));
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue rename(ScriptExecutionContext _, CallbackArguments args)
    {
        var fileNameOld = args.AsType(0, "rename", DataType.String).String;
        var fileNameNew = args.AsType(1, "rename", DataType.String).String;

        try
        {
            if (!Script.GlobalOptions.Platform.OS_FileExists(fileNameOld))
                return LuaValue.NewTuple(LuaValue.Nil,
                    LuaValue.NewString("{0}: No such file or directory.", fileNameOld),
                    LuaValue.NewNumber(-1));

            Script.GlobalOptions.Platform.OS_FileMove(fileNameOld, fileNameNew);
            return LuaValue.True;
        }
        catch (Exception ex)
        {
            return LuaValue.NewTuple(LuaValue.Nil, LuaValue.NewString(ex.Message), LuaValue.NewNumber(-1));
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue setlocale(ScriptExecutionContext _, CallbackArguments _args)
    {
        // TODO:
        return LuaValue.NewString("n/a");
    }

    [SolarSharpModuleMethod]
    public static LuaValue tmpname(ScriptExecutionContext _, CallbackArguments _args)
    {
        return LuaValue.NewString(Script.GlobalOptions.Platform.IO_OS_GetTempFilename());
    }
}

#pragma warning restore IDE0060 // Remove unused parameter