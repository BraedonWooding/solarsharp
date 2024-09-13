using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing table Lua functions 
    /// </summary>
    [MoonSharpModule(Namespace = "table")]
    public class TableModule
    {
        [MoonSharpModuleMethod]
        public static DynValue unpack(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue s = args.AsType(0, "unpack", DataType.Table, false);
            DynValue vi = args.AsType(1, "unpack", DataType.Number, true);
            DynValue vj = args.AsType(2, "unpack", DataType.Number, true);

            int ii = vi.IsNil() ? 1 : (int)vi.Number;
            int ij = vj.IsNil() ? GetTableLength(executionContext, s) : (int)vj.Number;

            Table t = s.Table;

            DynValue[] v = new DynValue[ij - ii + 1];

            int tidx = 0;
            for (int i = ii; i <= ij; i++)
                v[tidx++] = t.Get(i);

            return DynValue.NewTuple(v);
        }

        [MoonSharpModuleMethod]
        public static DynValue pack(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            Table t = new(executionContext.GetScript());
            DynValue v = DynValue.NewTable(t);

            for (int i = 0; i < args.Count; i++)
                t.Set(i + 1, args[i]);

            t.Set("n", DynValue.NewNumber(args.Count));

            return v;
        }

        [MoonSharpModuleMethod]
        public static DynValue sort(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue vlist = args.AsType(0, "sort", DataType.Table, false);
            DynValue lt = args[1];

            if (lt.Type != DataType.Function && lt.Type != DataType.ClrFunction && lt.IsNotNil())
                args.AsType(1, "sort", DataType.Function, true); // this throws

            vlist.Table.Sort(new Comparer(executionContext, lt));
            return vlist;
        }

        private class Comparer : IComparer<DynValue>
        {
            private ScriptExecutionContext _executionContext;
            private DynValue _comparer;

            public Comparer(ScriptExecutionContext executionContext, DynValue lt)
            {
                _executionContext = executionContext;
                _comparer = lt;
            }

            public int Compare(DynValue a, DynValue b)
            {
                if (_comparer.IsNil())
                {
                    var comparer = _executionContext.GetBinaryMetamethod(a, b, "__lt");
                    if (comparer.IsNil())
                    {
                        if (a.Type == DataType.Number && b.Type == DataType.Number)
                            return a.Number.CompareTo(b.Number);
                        if (a.Type == DataType.String && b.Type == DataType.String)
                            return a.String.CompareTo(b.String);

                        throw ScriptRuntimeException.CompareInvalidType(a, b);
                    }
                    else
                    {
                        return LuaComparerToClrComparer(comparer, a, b);
                    }
                }
                else
                {
                    return LuaComparerToClrComparer(_comparer, a, b);
                }
            }

            private int LuaComparerToClrComparer(DynValue comparer, DynValue a, DynValue b)
            {
                // sadly we have to make 2 calls for each one.
                // since we can do a non-stable sort, maybe it's worth looking at implementing that?
                if (_executionContext.GetScript().Call(comparer, a, b).CastToBool())
                {
                    return -1;
                }
                else if (_executionContext.GetScript().Call(comparer, b, a).CastToBool())
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        [MoonSharpModuleMethod]
        public static DynValue insert(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue vlist = args.AsType(0, "table.insert", DataType.Table, false);
            Table table = vlist.Table;
            DynValue vvalue;

            if (args.Count > 3)
                throw new ScriptRuntimeException("wrong number of arguments to 'insert'");

            int len = GetTableLength(executionContext, vlist);
            int pos;
            if (args.Count == 2)
            {
                // we insert at end of the array
                vvalue = args[1];
                pos = len + 1;
            }
            else
            {
                DynValue vpos = args[1];
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

        [MoonSharpModuleMethod]
        public static DynValue remove(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue vlist = args.AsType(0, "table.remove", DataType.Table, false);
            DynValue vpos = args.AsType(1, "table.remove", DataType.Number, true);
            DynValue ret = DynValue.Nil;

            if (args.Count > 2)
                throw new ScriptRuntimeException("wrong number of arguments to 'remove'");

            int len = GetTableLength(executionContext, vlist);
            Table list = vlist.Table;

            int pos = vpos.IsNil() ? len : (int)vpos.Number;

            if (pos >= len + 1 || pos < 1 && len > 0)
                throw new ScriptRuntimeException("bad argument #1 to 'remove' (position out of bounds)");

            for (int i = pos; i <= len; i++)
            {
                if (i == pos)
                    ret = list.Get(i);

                list.Set(i, list.Get(i + 1));
            }

            return ret;
        }

        //table.concat (list [, sep [, i [, j]]])
        //Given a list where all elements are strings or numbers, returns the string list[i]..sep..list[i+1] (...) sep..list[j]. 
        //The default value for sep is the empty string, the default for i is 1, and the default for j is #list. If i is greater 
        //than j, returns the empty string. 
        [MoonSharpModuleMethod]
        public static DynValue concat(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue vlist = args.AsType(0, "concat", DataType.Table, false);
            DynValue vsep = args.AsType(1, "concat", DataType.String, true);
            DynValue vstart = args.AsType(2, "concat", DataType.Number, true);
            DynValue vend = args.AsType(3, "concat", DataType.Number, true);

            Table list = vlist.Table;
            string sep = vsep.IsNil() ? "" : vsep.String;
            int start = vstart.IsNilOrNan() ? 1 : (int)vstart.Number;
            int end = vend.IsNilOrNan() ? GetTableLength(executionContext, vlist) : (int)vend.Number;
            if (end < start)
                return DynValue.NewString(string.Empty);

            StringBuilder sb = new();

            for (int i = start; i <= end; i++)
            {
                DynValue v = list.Get(i);

                if (v.Type != DataType.Number && v.Type != DataType.String)
                    throw new ScriptRuntimeException("invalid value ({1}) at index {0} in table for 'concat'", i, v.Type.ToLuaTypeString());

                string s = v.ToPrintString();

                if (i != start)
                    sb.Append(sep);

                sb.Append(s);

            }

            return DynValue.NewString(sb.ToString());
        }

        private static int GetTableLength(ScriptExecutionContext executionContext, DynValue vlist)
        {
            DynValue __len = executionContext.GetMetamethod(vlist, "__len");

            if (__len.IsNotNil())
            {
                DynValue lenv = executionContext.GetScript().Call(__len, vlist);
                double? len = lenv.CastToNumber();
                return len == null ? throw new ScriptRuntimeException("object length is not a number") : (int)len;
            }
            else
            {
                return vlist.Table.Length;
            }
        }
    }


    /// <summary>
    /// Class exposing table.unpack and table.pack in the global namespace (to work around the most common Lua 5.1 compatibility issue).
    /// </summary>
    [MoonSharpModule]
    public class TableModule_Globals
    {
        [MoonSharpModuleMethod]
        public static DynValue unpack(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return TableModule.unpack(executionContext, args);
        }

        [MoonSharpModuleMethod]
        public static DynValue pack(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return TableModule.pack(executionContext, args);
        }
    }


}
