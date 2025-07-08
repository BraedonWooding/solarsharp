using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors
{
    public abstract class HardwiredMethodMemberDescriptor : FunctionMemberDescriptorBase
    {
        public override DynValue Execute(
            Script script,
            object obj,
            ScriptExecutionContext context,
            CallbackArguments args
        )
        {
            this.CheckAccess(MemberDescriptorAccess.CanExecute, obj);

            var pars = base.BuildArgumentList(script, obj, context, args, out var outParams);
            var retv = Invoke(script, obj, pars, CalcArgsCount(pars));

            return DynValue.FromObject(script, retv);
        }

        private int CalcArgsCount(object[] pars)
        {
            var count = pars.Length;

            for (var i = 0; i < pars.Length; i++)
                if (Parameters[i].HasDefaultValue && pars[i] is DefaultValue)
                {
                    count -= 1;
                }

            return count;
        }

        protected abstract object Invoke(Script script, object obj, object[] pars, int argscount);
    }
}
