using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.StandardDescriptors.ReflectionMemberDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors;

internal class EventFacade : IUserDataType
{
    private readonly Func<object, ScriptExecutionContext, CallbackArguments, LuaValue> m_AddCallback;
    private readonly object m_Object;
    private readonly Func<object, ScriptExecutionContext, CallbackArguments, LuaValue> m_RemoveCallback;

    public EventFacade(EventMemberDescriptor parent, object obj)
    {
        m_Object = obj;
        m_AddCallback = parent.AddCallback;
        m_RemoveCallback = parent.RemoveCallback;
    }

    public EventFacade(Func<object, ScriptExecutionContext, CallbackArguments, LuaValue> addCallback,
        Func<object, ScriptExecutionContext, CallbackArguments, LuaValue> removeCallback, object obj)
    {
        m_Object = obj;
        m_AddCallback = addCallback;
        m_RemoveCallback = removeCallback;
    }

    public LuaValue Index(Script script, LuaValue index, bool isDirectIndexing)
    {
        if (index.Type == DataType.String)
        {
            if (index.String == "add")
                return LuaValue.NewCallback((c, a) => m_AddCallback(m_Object, c, a));
            if (index.String == "remove")
                return LuaValue.NewCallback((c, a) => m_RemoveCallback(m_Object, c, a));
        }

        throw new ScriptRuntimeException("Events only support add and remove methods");
    }

    public bool SetIndex(Script script, LuaValue index, LuaValue value, bool isDirectIndexing)
    {
        throw new ScriptRuntimeException("Events do not have settable fields");
    }

    public LuaValue MetaIndex(Script script, string metaname)
    {
        return null;
    }
}