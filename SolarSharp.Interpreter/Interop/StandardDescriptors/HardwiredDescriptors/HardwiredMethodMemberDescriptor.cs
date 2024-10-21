using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors
{
    public abstract class HardwiredMethodMemberDescriptor : FunctionMemberDescriptorBase
    {
        public override DynValue Execute(LuaState script, object obj, ScriptExecutionContext context, CallbackArguments args)
        {
            this.CheckAccess(MemberDescriptorAccess.CanExecute, obj);

            object[] pars = base.BuildArgumentList(script, obj, context, args, out List<int> outParams);
            object retv = Invoke(script, obj, pars, CalcArgsCount(pars));

            return DynValue.FromObject(script, retv);
        }

        private int CalcArgsCount(object[] pars)
        {
            int count = pars.Length;

            for (int i = 0; i < pars.Length; i++)
                if (Parameters[i].HasDefaultValue && pars[i] is DefaultValue)
                {
                    count -= 1;
                }

            return count;
        }

        protected abstract object Invoke(LuaState script, object obj, object[] pars, int argscount);
    }
}
