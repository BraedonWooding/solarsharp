﻿using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.StandardDescriptors.ReflectionMemberDescriptors;
using System;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors
{
    internal class EventFacade : IUserDataType
    {
        private readonly Func<object, ScriptExecutionContext, CallbackArguments, DynValue> m_AddCallback;
        private readonly Func<object, ScriptExecutionContext, CallbackArguments, DynValue> m_RemoveCallback;
        private readonly object m_Object;

        public EventFacade(EventMemberDescriptor parent, object obj)
        {
            m_Object = obj;
            m_AddCallback = parent.AddCallback;
            m_RemoveCallback = parent.RemoveCallback;
        }

        public EventFacade(Func<object, ScriptExecutionContext, CallbackArguments, DynValue> addCallback, Func<object, ScriptExecutionContext, CallbackArguments, DynValue> removeCallback, object obj)
        {
            m_Object = obj;
            m_AddCallback = addCallback;
            m_RemoveCallback = removeCallback;
        }

        public DynValue Index(LuaState script, DynValue index, bool isDirectIndexing)
        {
            if (index.Type == DataType.String)
            {
                if (index.String == "add")
                    return DynValue.NewCallback((c, a) => m_AddCallback(m_Object, c, a));
                else if (index.String == "remove")
                    return DynValue.NewCallback((c, a) => m_RemoveCallback(m_Object, c, a));
            }

            throw new ScriptRuntimeException("Events only support add and remove methods");
        }

        public bool SetIndex(LuaState script, DynValue index, DynValue value, bool isDirectIndexing)
        {
            throw new ScriptRuntimeException("Events do not have settable fields");
        }

        public DynValue MetaIndex(LuaState script, string metaname)
        {
            return DynValue.Nil;
        }
    }
}
