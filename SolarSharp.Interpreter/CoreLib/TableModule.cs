using System.Collections.Generic;
using System.Text;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing table Lua functions
/// </summary>
[SolarSharpModule(Namespace = "table")]
public class TableModule
{
    [SolarSharpModuleMethod]
    public static LuaValue unpack(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var s = args.AsType(0, "unpack", DataType.Table);
        var vi = args.AsType(1, "unpack", DataType.Number, true);
        var vj = args.AsType(2, "unpack", DataType.Number, true);

        var ii = vi.IsNil() ? 1 : (int)vi.Number;
        var ij = vj.IsNil() ? GetTableLength(executionContext, s) : (int)vj.Number;

        var t = s.Table;

        var v = new LuaValue[ij - ii + 1];

        var tidx = 0;
        for (var i = ii; i <= ij; i++)
            v[tidx++] = t.Get(i);

        return LuaValue.NewTuple(v);
    }

    [SolarSharpModuleMethod]
    public static LuaValue pack(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        Table t = new(executionContext.GetScript());
        var v = LuaValue.NewTable(t);

        for (var i = 0; i < args.Count; i++)
            t.Set(i + 1, args[i]);

        t.Set("n", LuaValue.NewNumber(args.Count));

        return v;
    }

    [SolarSharpModuleMethod]
    public static LuaValue sort(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var vlist = args.AsType(0, "sort", DataType.Table);
        var lt = args[1];

        if (lt.Type != DataType.Function && lt.Type != DataType.ClrFunction && lt.IsNotNil())
            args.AsType(1, "sort", DataType.Function, true); // this throws

        vlist.Table.Sort(new Comparer(executionContext, lt));
        return vlist;
    }

    [SolarSharpModuleMethod]
    public static LuaValue insert(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var vlist = args.AsType(0, "table.insert", DataType.Table);
        var table = vlist.Table;
        LuaValue vvalue;

        if (args.Count > 3)
            throw new ScriptRuntimeException("wrong number of arguments to 'insert'");

        var len = GetTableLength(executionContext, vlist);
        int pos;
        if (args.Count == 2)
        {
            // we insert at end of the array
            vvalue = args[1];
            pos = len + 1;
        }
        else
        {
            var vpos = args[1];
            vvalue = args[2];
            if (vpos.Type != DataType.Number)
                throw ScriptRuntimeException.BadArgument(1, "table.insert", DataType.Number, vpos.Type, false);
            pos = (int)vpos.Number;
        }

        if (pos > len + 1 || pos < 1)
            throw new ScriptRuntimeException("bad argument #2 to 'insert' (position out of bounds)");

        table.Insert(pos, vvalue);

        return vlist;
    }

    [SolarSharpModuleMethod]
    public static LuaValue remove(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var vlist = args.AsType(0, "table.remove", DataType.Table);
        var vpos = args.AsType(1, "table.remove", DataType.Number, true);

        if (args.Count > 2)
            throw new ScriptRuntimeException("wrong number of arguments to 'remove'");

        var len = GetTableLength(executionContext, vlist);
        var list = vlist.Table;

        var pos = vpos.IsNil() ? len : (int)vpos.Number;

        if (pos >= len + 1 || (pos < 1 && len > 0))
            throw new ScriptRuntimeException("bad argument #1 to 'remove' (position out of bounds)");

        return list.ArrayRemoveAt(pos);
    }

    //table.concat (list [, sep [, i [, j]]])
    //Given a list where all elements are strings or numbers, returns the string list[i]..sep..list[i+1] (...) sep..list[j]. 
    //The default value for sep is the empty string, the default for i is 1, and the default for j is #list. If i is greater 
    //than j, returns the empty string. 
    [SolarSharpModuleMethod]
    public static LuaValue concat(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var vlist = args.AsType(0, "concat", DataType.Table);
        var vsep = args.AsType(1, "concat", DataType.String, true);
        var vstart = args.AsType(2, "concat", DataType.Number, true);
        var vend = args.AsType(3, "concat", DataType.Number, true);

        var list = vlist.Table;
        var sep = vsep.IsNil() ? "" : vsep.String;
        var start = vstart.IsNilOrNan() ? 1 : (int)vstart.Number;
        var end = vend.IsNilOrNan() ? GetTableLength(executionContext, vlist) : (int)vend.Number;
        if (end < start)
            return LuaValue.NewString(string.Empty);

        StringBuilder sb = new();

        for (var i = start; i <= end; i++)
        {
            var v = list.Get(i);

            if (v.Type != DataType.Number && v.Type != DataType.String)
                throw new ScriptRuntimeException("invalid value ({1}) at index {0} in table for 'concat'", i,
                    v.Type.ToLuaTypeString());

            var s = v.ToPrintString();

            if (i != start)
                sb.Append(sep);

            sb.Append(s);
        }

        return LuaValue.NewString(sb.ToString());
    }

    private static int GetTableLength(ScriptExecutionContext executionContext, LuaValue vlist)
    {
        var lenMethod = executionContext.GetMetamethod(vlist, "__len");

        if (lenMethod != null)
        {
            var lenv = executionContext.GetScript().Call(lenMethod, vlist);
            var len = lenv.CastToNumber();
            return len == null ? throw new ScriptRuntimeException("object length is not a number") : (int)len;
        }

        return vlist.Table.Length;
    }

    private class Comparer : IComparer<LuaValue>
    {
        private readonly LuaValue _comparer;
        private ScriptExecutionContext _executionContext;

        public Comparer(ScriptExecutionContext executionContext, LuaValue lt)
        {
            _executionContext = executionContext;
            _comparer = lt;
        }

        public int Compare(LuaValue a, LuaValue b)
        {
            if (_comparer == null || _comparer.IsNil())
            {
                var comparer = _executionContext.GetBinaryMetamethod(a, b, "__lt");
                if (comparer == null || comparer.IsNil())
                {
                    if (a.Type == DataType.Number && b.Type == DataType.Number)
                        return a.Number.CompareTo(b.Number);
                    if (a.Type == DataType.String && b.Type == DataType.String)
                        return a.String.CompareTo(b.String);

                    throw ScriptRuntimeException.CompareInvalidType(a, b);
                }

                return LuaComparerToClrComparer(comparer, a, b);
            }

            return LuaComparerToClrComparer(_comparer, a, b);
        }

        private int LuaComparerToClrComparer(LuaValue comparer, LuaValue a, LuaValue b)
        {
            // sadly we have to make 2 calls for each one.
            // since we can do a non-stable sort, maybe it's worth looking at implementing that?
            if (_executionContext.GetScript().Call(comparer, a, b).CastToBool()) return -1;

            if (_executionContext.GetScript().Call(comparer, b, a).CastToBool()) return 1;

            return 0;
        }
    }
}

/// <summary>
///     Class exposing table.unpack and table.pack in the global namespace (to work around the most common Lua 5.1
///     compatibility issue).
/// </summary>
[SolarSharpModule]
public class TableModule_Globals
{
    [SolarSharpModuleMethod]
    public static LuaValue unpack(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return TableModule.unpack(executionContext, args);
    }

    [SolarSharpModuleMethod]
    public static LuaValue pack(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return TableModule.pack(executionContext, args);
    }
}