using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Serialization.Json;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib
{
    [MoonSharpModule(Namespace = "json")]
    public class JsonModule
    {
        [MoonSharpModuleMethod]
        public static DynValue parse(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            try
            {
                DynValue vs = args.AsType(0, "parse", DataType.String, false);
                Table t = JsonTableConverter.JsonToTable(vs.String, executionContext.GetScript());
                return DynValue.NewTable(t);
            }
            catch (SyntaxErrorException ex)
            {
                throw new ScriptRuntimeException(ex);
            }
        }

        [MoonSharpModuleMethod]
        public static DynValue serialize(ScriptExecutionContext _, CallbackArguments args)
        {
            try
            {
                DynValue vt = args.AsType(0, "serialize", DataType.Table, false);
                string s = vt.Table.TableToJson();
                return DynValue.NewString(s);
            }
            catch (SyntaxErrorException ex)
            {
                throw new ScriptRuntimeException(ex);
            }
        }

        [MoonSharpModuleMethod]
        public static DynValue isnull(ScriptExecutionContext _, CallbackArguments args)
        {
            DynValue vs = args[0];
            return DynValue.NewBoolean(JsonNull.IsJsonNull(vs) || vs.IsNil());
        }

        [MoonSharpModuleMethod]
#pragma warning disable IDE0060 // Remove unused parameter
        public static DynValue @null(ScriptExecutionContext _, CallbackArguments _args)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return JsonNull.Create();
        }
    }
}
