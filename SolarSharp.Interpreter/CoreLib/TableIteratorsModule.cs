using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing table Lua iterators (pairs, ipairs, next)
    /// </summary>
    [MoonSharpModule]
    public class TableIteratorsModule
    {
        // ipairs (t)
        // -------------------------------------------------------------------------------------------------------------------
        // If t has a metamethod __ipairs, calls it with t as argument and returns the first three results from the call.
        // Otherwise, returns three values: an iterator function, the table t, and 0, so that the construction
        //	  for i,v in ipairs(t) do body end
        // will iterate over the pairs (1,t[1]), (2,t[2]), ..., up to the first integer key absent from the table. 
        [MoonSharpModuleMethod]
        public static DynValue ipairs(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue table = args[0];
            var tableVal = table.Table;

            DynValue meta = executionContext.GetMetamethodTailCall(table, "__ipairs", args.GetArray());
            if (meta.IsNotNil()) return meta;

            var current = DynValue.NewNumber(0);
            return DynValue.NewTuple(DynValue.NewCallback((ex, args) =>
            {
                if (args[1].Number == current.Number)
                {
                    int next = (int)current.Number + 1;
                    current.AssignNumber(next);
                    var value = tableVal.Get(next);
                    if (value.IsNil()) return value;
                    return DynValue.NewTuple(current, value);
                }
                else
                {
                    return __next_i(executionContext, args);
                }
            }), table, current);
        }

        // pairs (t)
        // -------------------------------------------------------------------------------------------------------------------
        // If t has a metamethod __pairs, calls it with t as argument and returns the first three results from the call.
        // Otherwise, returns three values: the next function, the table t, and nil, so that the construction
        //     for k,v in pairs(t) do body end
        // will iterate over all key–value pairs of table t.
        // See function next for the caveats of modifying the table during its traversal. 
        [MoonSharpModuleMethod]
        public static DynValue pairs(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue table = args[0];
            DynValue meta = executionContext.GetMetamethodTailCall(table, "__pairs", args.GetArray());
            if (meta.IsNotNil()) return meta;

            // TODO: Should we check if someone is calling this wrong?  i.e. if they do something like callback = pairs(); callback("BOO")
            //       we could compare the dynvalues to check that the keys are the same (can use ref checks even) and in case they aren't fallback to next
            // we use an efficient iterator when using pairs()
            // over the slower next(), this should save quite a few cycles
            var it = table.Table.GetEnumerator();
            return DynValue.NewTuple(DynValue.NewCallback((ex, args) =>
            {
                if (args[1].Equals(it.Current.Key))
                {
                    return it.MoveNext() ? DynValue.NewTuple(it.Current.Key, it.Current.Value) : DynValue.Nil;
                }
                else
                {
                    // fallback to next
                    return next(executionContext, args);
                }
            }), table);
        }

        // next (table [, index])
        // -------------------------------------------------------------------------------------------------------------------
        // Allows a program to traverse all fields of a table. Its first argument is a table and its second argument is an 
        // index in this table. next returns the next index of the table and its associated value. 
        // When called with nil as its second argument, next returns an initial index and its associated value. 
        // When called with the last index, or with nil in an empty table, next returns nil. If the second argument is absent, 
        // then it is interpreted as nil. In particular, you can use next(t) to check whether a table is empty.
        // The order in which the indices are enumerated is not specified, even for numeric indices. 
        // (To traverse a table in numeric order, use a numerical for.)
        // The behavior of next is undefined if, during the traversal, you assign any value to a non-existent field in the table. 
        // You may however modify existing fields. In particular, you may clear existing fields. 
        [MoonSharpModuleMethod]
        public static DynValue next(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue table = args.AsType(0, "next", DataType.Table);
            DynValue index = args[1];

            return table.Table.GetNextFromIt(index);
        }

        // __next_i (table [, index])
        // -------------------------------------------------------------------------------------------------------------------
        // Allows a program to traverse all fields of an array. index is an integer number
        public static DynValue __next_i(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue table = args.AsType(0, "!!next_i!!", DataType.Table);
            DynValue index = args.AsType(1, "!!next_i!!", DataType.Number);

            int idx = (int)index.Number + 1;
            DynValue val = table.Table.Get(idx);

            if (val.Type != DataType.Nil)
            {
                return DynValue.NewTuple(DynValue.NewNumber(idx), val);
            }
            else
            {
                return DynValue.Nil;
            }
        }
    }
}
