using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing metatable related Lua functions (xxxmetatable and rawxxx).
    /// </summary>
    [SolarSharpModule]
    public class MetaTableModule
    {
        // setmetatable (table, metatable)
        // -------------------------------------------------------------------------------------------------------------------
        // Sets the metatable for the given table. (You cannot change the metatable of other
        // types from Lua, only from C.) If metatable is nil, removes the metatable of the given table.
        // If the original metatable has a "__metatable" field, raises an error ("cannot change a protected metatable").
        // This function returns table.
        [MoonSharpModuleMethod]
        public static DynValue setmetatable(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var table = args.AsType(0, "setmetatable", DataType.Table);
            var metatable = args.AsType(1, "setmetatable", DataType.Table, true);

            // Security check: Block metatables that contain potentially dangerous patterns
            // Only enforce this in isolated/restricted security contexts
            if (metatable?.Table != null && executionContext.GetScript().IsAuthorizedToRun())
            {
                var securityConfig = executionContext.GetScript().SecurityPolicy();

                // Only apply strict metatable security in isolated contexts
                if (securityConfig.EnvironmentEmulation.Mode == EnvironmentMode.Isolated)
                {
                    var mt = metatable.Table;
                    var indexMethod = mt.Get("__index");
                    var newindexMethod = mt.Get("__newindex");
                    var metatableField = mt.Get("__metatable");

                    // Block the specific attack pattern: functions for both __index and __newindex
                    // with __metatable field (indicating attempt to hide metatable)
                    if (
                        indexMethod != null
                        && !indexMethod.IsNil()
                        && indexMethod.Type == DataType.Function
                        && newindexMethod != null
                        && !newindexMethod.IsNil()
                        && newindexMethod.Type == DataType.Function
                        && metatableField != null
                        && !metatableField.IsNil()
                    )
                    {
                        // This specific pattern (both metamethods as functions + hidden metatable)
                        // is commonly used for security bypass attempts
                        throw new MetatableViolationException(
                            "metatable with both __index and __newindex functions and __metatable field is not allowed in isolated security contexts",
                            "setmetatable",
                            table,
                            metatable
                        );
                    }
                }
            }

            var curmeta = executionContext.GetMetamethod(table, "__metatable");

            if (curmeta != null)
            {
                throw new ScriptRuntimeException("cannot change a protected metatable");
            }

            table.Table.MetaTable = metatable.Table;
            return table;
        }

        // getmetatable (object)
        // -------------------------------------------------------------------------------------------------------------------
        // If object does not have a metatable, returns nil. Otherwise, if the object's metatable
        // has a "__metatable" field, returns the associated value. Otherwise, returns the metatable of the given object.
        [MoonSharpModuleMethod]
        public static DynValue getmetatable(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var obj = args[0];
            Table meta = null;

            if (obj.Type.CanHaveTypeMetatables())
            {
                meta = executionContext.GetScript().GetTypeMetatable(obj.Type);
            }

            if (obj.Type == DataType.Table)
            {
                meta = obj.Table.MetaTable;
            }

            if (meta == null)
                return DynValue.Nil;
            if (meta.Get("__metatable") is var metaTable && metaTable.IsNotNil())
                return metaTable;
            return DynValue.NewTable(meta);
        }

        // rawget (table, index)
        // -------------------------------------------------------------------------------------------------------------------
        // Gets the real value of table[index], without invoking any metamethod. table must be a table; index may be any value.
        [MoonSharpModuleMethod]
        public static DynValue rawget(ScriptExecutionContext _, CallbackArguments args)
        {
            var table = args.AsType(0, "rawget", DataType.Table);
            var index = args[1];

            return table.Table.Get(index);
        }

        // rawset (table, index, value)
        // -------------------------------------------------------------------------------------------------------------------
        // Sets the real value of table[index] to value, without invoking any metamethod. table must be a table,
        // index any value different from nil and NaN, and value any Lua value.
        // This function returns table.
        [MoonSharpModuleMethod]
        public static DynValue rawset(ScriptExecutionContext _, CallbackArguments args)
        {
            var table = args.AsType(0, "rawset", DataType.Table);
            var index = args[1];

            table.Table.Set(index, args[2]);

            return table;
        }

        // rawequal (v1, v2)
        // -------------------------------------------------------------------------------------------------------------------
        // Checks whether v1 is equal to v2, without invoking any metamethod. Returns a boolean.
        [MoonSharpModuleMethod]
        public static DynValue rawequal(ScriptExecutionContext _, CallbackArguments args)
        {
            var v1 = args[0];
            var v2 = args[1];

            return DynValue.NewBoolean(v1.Equals(v2));
        }

        //rawlen (v)
        // -------------------------------------------------------------------------------------------------------------------
        //Returns the length of the object v, which must be a table or a string, without invoking any metamethod. Returns an integer number.
        [MoonSharpModuleMethod]
        public static DynValue rawlen(ScriptExecutionContext _, CallbackArguments args)
        {
            if (args[0].Type != DataType.String && args[0].Type != DataType.Table)
            {
                throw ScriptRuntimeException.BadArgument(
                    0,
                    "rawlen",
                    "table or string",
                    args[0].Type.ToErrorTypeString(),
                    false
                );
            }

            return args[0].GetLength();
        }
    }
}
