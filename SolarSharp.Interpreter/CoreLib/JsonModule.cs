using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Serialization.Json;

namespace SolarSharp.Interpreter.CoreLib;

[SolarSharpModule(Namespace = "json")]
public class JsonModule
{
    [SolarSharpModuleMethod]
    public static LuaValue parse(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        try
        {
            var vs = args.AsType(0, "parse", DataType.String);
            var t = JsonTableConverter.JsonToTable(vs.String, executionContext.GetScript());
            return LuaValue.NewTable(t);
        }
        catch (SyntaxErrorException ex)
        {
            throw new ScriptRuntimeException(ex);
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue serialize(ScriptExecutionContext _, CallbackArguments args)
    {
        try
        {
            var vt = args.AsType(0, "serialize", DataType.Table);
            var s = vt.Table.TableToJson();
            return LuaValue.NewString(s);
        }
        catch (SyntaxErrorException ex)
        {
            throw new ScriptRuntimeException(ex);
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue isnull(ScriptExecutionContext _, CallbackArguments args)
    {
        var vs = args[0];
        return LuaValue.NewBoolean(JsonNull.IsJsonNull(vs) || vs.IsNil());
    }

    [SolarSharpModuleMethod]
#pragma warning disable IDE0060 // Remove unused parameter
    public static LuaValue @null(ScriptExecutionContext _, CallbackArguments _args)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        return JsonNull.Create();
    }
}