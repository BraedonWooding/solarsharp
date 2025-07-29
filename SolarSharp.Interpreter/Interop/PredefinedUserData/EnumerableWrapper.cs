using System.Collections;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.Converters;

namespace SolarSharp.Interpreter.Interop.PredefinedUserData;

/// <summary>
///     Wrappers for enumerables as return types
/// </summary>
internal class EnumerableWrapper : IUserDataType
{
    private readonly IEnumerator m_Enumerator;
    private readonly Script m_Script;
    private bool m_HasTurnOnce;
    private LuaValue m_Prev = LuaValue.Nil;

    private EnumerableWrapper(Script script, IEnumerator enumerator)
    {
        m_Script = script;
        m_Enumerator = enumerator;
    }

    public LuaValue Index(Script script, LuaValue index, bool isDirectIndexing)
    {
        if (index.Type == DataType.String)
        {
            var idx = index.String;

            if (idx == "Current" || idx == "current") return LuaValue.FromObject(script, m_Enumerator.Current);

            if (idx == "MoveNext" || idx == "moveNext" || idx == "move_next")
                return LuaValue.NewCallback((_, _) => LuaValue.NewBoolean(m_Enumerator.MoveNext()));

            if (idx == "Reset" || idx == "reset")
                return LuaValue.NewCallback((_, _) =>
                {
                    Reset();
                    return LuaValue.Nil;
                });
        }

        return null;
    }

    public bool SetIndex(Script script, LuaValue index, LuaValue value, bool isDirectIndexing)
    {
        return false;
    }

    public LuaValue MetaIndex(Script script, string metaname)
    {
        if (metaname == "__call")
            return LuaValue.NewCallback(LuaIteratorCallback);
        return null;
    }

    public void Reset()
    {
        if (m_HasTurnOnce)
            m_Enumerator.Reset();

        m_HasTurnOnce = true;
    }

    private LuaValue GetNext(LuaValue prev)
    {
        if (prev.IsNil())
            Reset();

        while (m_Enumerator.MoveNext())
        {
            var v = ClrToScriptConversions.ObjectToLuaValue(m_Script, m_Enumerator.Current);

            if (!v.IsNil())
                return v;
        }

        return LuaValue.Nil;
    }

    private LuaValue LuaIteratorCallback(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        m_Prev = GetNext(m_Prev);
        return m_Prev;
    }

    internal static LuaValue ConvertIterator(Script script, IEnumerator enumerator)
    {
        EnumerableWrapper ei = new(script, enumerator);
        return LuaValue.NewTuple(UserData.Create(ei), LuaValue.Nil, LuaValue.Nil);
    }

    internal static LuaValue ConvertTable(Table table)
    {
        return ConvertIterator(table.OwnerScript, table.Values.GetEnumerator());
    }
}