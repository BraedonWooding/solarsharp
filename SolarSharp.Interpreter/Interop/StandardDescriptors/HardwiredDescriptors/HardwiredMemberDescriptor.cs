using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.Converters;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors
{
    public abstract class HardwiredMemberDescriptor : IMemberDescriptor
    {
        public Type MemberType { get; private set; }

        protected HardwiredMemberDescriptor(Type memberType, string name, bool isStatic, MemberDescriptorAccess access)
        {
            IsStatic = isStatic;
            Name = name;
            MemberAccess = access;
            MemberType = memberType;
        }

        public bool IsStatic { get; private set; }

        public string Name { get; private set; }

        public MemberDescriptorAccess MemberAccess { get; private set; }


        public DynValue GetValue(LuaState script, object obj)
        {
            this.CheckAccess(MemberDescriptorAccess.CanRead, obj);
            object result = GetValueImpl(script, obj);
            return ClrToScriptConversions.ObjectToDynValue(script, result);
        }

        public void SetValue(LuaState script, object obj, DynValue value)
        {
            this.CheckAccess(MemberDescriptorAccess.CanWrite, obj);
            object v = ScriptToClrConversions.DynValueToObjectOfType(value, MemberType, null, false);
            SetValueImpl(script, obj, v);
        }


        protected virtual object GetValueImpl(LuaState script, object obj)
        {
            throw new InvalidOperationException("GetValue on write-only hardwired descriptor " + Name);
        }

        protected virtual void SetValueImpl(LuaState script, object obj, object value)
        {
            throw new InvalidOperationException("SetValue on read-only hardwired descriptor " + Name);
        }
    }
}
