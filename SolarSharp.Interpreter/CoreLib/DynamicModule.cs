using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing dynamic expression evaluations at runtime (a MoonSharp addition).
    /// </summary>
    [MoonSharpModule(Namespace = "dynamic")]
    public class DynamicModule
    {
        private class DynamicExprWrapper
        {
            public DynamicExpression Expr;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public static void MoonSharpInit(Table globalTable, Table stringTable)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            UserData.RegisterType<DynamicExprWrapper>(InteropAccessMode.HideMembers);
        }

        [MoonSharpModuleMethod]
        public static DynValue eval(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            try
            {
                if (args[0].Type == DataType.UserData)
                {
                    UserData ud = args[0].UserData;
                    if (ud.Object is DynamicExprWrapper wrapper)
                    {
                        return wrapper.Expr.Evaluate(executionContext);
                    }
                    else
                    {
                        throw ScriptRuntimeException.BadArgument(0, "dynamic.eval", "A userdata was passed, but was not a previously prepared expression.");
                    }
                }
                else
                {
                    DynValue vs = args.AsType(0, "dynamic.eval", DataType.String, false);
                    DynamicExpression expr = executionContext.GetScript().CreateDynamicExpression(vs.String);
                    return expr.Evaluate(executionContext);
                }
            }
            catch (SyntaxErrorException ex)
            {
                throw new ScriptRuntimeException(ex);
            }
        }

        [MoonSharpModuleMethod]
        public static DynValue prepare(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            try
            {
                DynValue vs = args.AsType(0, "dynamic.prepare", DataType.String, false);
                DynamicExpression expr = executionContext.GetScript().CreateDynamicExpression(vs.String);
                return UserData.Create(new DynamicExprWrapper() { Expr = expr });
            }
            catch (SyntaxErrorException ex)
            {
                throw new ScriptRuntimeException(ex);
            }
        }


    }

}
