// Disable warnings about XML documentation
#pragma warning disable 1591

using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Security;
using lua_Integer = System.Int32;

namespace SolarSharp.Interpreter.Interop.LuaStateInterop
{
    /// <summary>
    /// Classes using the classic interface should inherit from this class.
    /// This class defines only static methods and is really meant to be used only
    /// from C# and not other .NET languages. 
    /// 
    /// For easier operation they should also define:
    ///		using ptrdiff_t = System.Int32;
    ///		using lua_Integer = System.Int32;
    ///		using LUA_INTFRM_T = System.Int64;
    ///		using UNSIGNED_LUA_INTFRM_T = System.UInt64;
    /// </summary>
    public partial class LuaBase
    {
        protected const lua_Integer LUA_TNONE = -1;
        protected const lua_Integer LUA_TNIL = 0;
        protected const lua_Integer LUA_TBOOLEAN = 1;
        protected const lua_Integer LUA_TLIGHTUSERDATA = 2;
        protected const lua_Integer LUA_TNUMBER = 3;
        protected const lua_Integer LUA_TSTRING = 4;
        protected const lua_Integer LUA_TTABLE = 5;
        protected const lua_Integer LUA_TFUNCTION = 6;
        protected const lua_Integer LUA_TUSERDATA = 7;
        protected const lua_Integer LUA_TTHREAD = 8;

        protected const lua_Integer LUA_MULTRET = -1;

        protected const string LUA_INTFRMLEN = "l";

        protected static DynValue GetArgument(LuaState l, lua_Integer pos)
        {
            return l.At(pos);
        }

        protected static DynValue ArgAsType(LuaState l, lua_Integer pos, DataType type, bool allowNil = false)
        {
            return GetArgument(l, pos).CheckType(l.FunctionName, type, pos - 1, allowNil ? TypeValidationFlags.AllowNil | TypeValidationFlags.AutoConvert : TypeValidationFlags.AutoConvert);
        }

        protected static lua_Integer LuaType(LuaState l, lua_Integer p)
        {
            switch (GetArgument(l, p).Type)
            {
                case DataType.Void:
                    return LUA_TNONE;
                case DataType.Nil:
                    return LUA_TNIL;
                case DataType.Boolean:
                    return LUA_TNIL;
                case DataType.Number:
                    return LUA_TNUMBER;
                case DataType.String:
                    return LUA_TSTRING;
                case DataType.Function:
                    return LUA_TFUNCTION;
                case DataType.Table:
                    return LUA_TTABLE;
                case DataType.UserData:
                    return LUA_TUSERDATA;
                case DataType.Thread:
                    return LUA_TTHREAD;
                case DataType.ClrFunction:
                    return LUA_TFUNCTION;
                case DataType.TailCallRequest:
                case DataType.YieldRequest:
                case DataType.Tuple:
                default:
                    throw new ScriptRuntimeException("Can't call LuaType on any type");
            }
        }

        protected static string LuaLCheckLString(LuaState luaState, lua_Integer argNum, out uint l)
        {
            var str = ArgAsType(luaState, argNum, DataType.String, false).String;
            l = (uint)str.Length;
            return str;
        }

        protected static void LuaPushInteger(LuaState l, lua_Integer val)
        {
            l.Push(DynValue.NewNumber(val));
        }

        protected static lua_Integer LuaToBoolean(LuaState l, lua_Integer p)
        {
            return GetArgument(l, p).CastToBool() ? 1 : 0;
        }

        protected static string LuaToLString(LuaState luaState, lua_Integer p, out uint l)
        {
            return LuaLCheckLString(luaState, p, out l);
        }

        protected static string LuaToString(LuaState luaState, lua_Integer p)
        {
            return LuaLCheckLString(luaState, p, out var l);
        }

        protected static void LuaLAddValue(LuaLBuffer b)
        {
            b.StringBuilder.Append(b.LuaState.Pop().ToPrintString());
        }

        protected static void LuaLAddLString(LuaLBuffer b, CharPtr s, uint p)
        {
            b.StringBuilder.Append(s.ToString((lua_Integer)p));
        }

        protected static void LuaLAddString(LuaLBuffer b, string s)
        {
            b.StringBuilder.Append(s.ToString());
        }


        protected static lua_Integer LuaLOptInteger(LuaState l, lua_Integer pos, lua_Integer def)
        {
            var v = ArgAsType(l, pos, DataType.Number, true);

            if (v.IsNil())
                return def;
            else
                return (lua_Integer)v.Number;
        }

        protected static lua_Integer LuaLCheckInteger(LuaState l, lua_Integer pos)
        {
            var v = ArgAsType(l, pos, DataType.Number, false);
            return (lua_Integer)v.Number;
        }

        protected static void LuaLArgCheck(LuaState l, bool condition, lua_Integer argNum, string message)
        {
            if (!condition)
                LuaLArgError(l, argNum, message);
        }

        protected static lua_Integer LuaLCheckInt(LuaState l, lua_Integer argNum)
        {
            return LuaLCheckInteger(l, argNum);
        }

        protected static lua_Integer LuaGetTop(LuaState l)
        {
            return l.Count;
        }

        protected static lua_Integer LuaLError(LuaState luaState, string message, params object[] args)
        {
            throw new ScriptRuntimeException(message, args);
        }

        protected static void LuaLAddChar(LuaLBuffer b, char p)
        {
            b.StringBuilder.Append(p);
        }

        protected static void LuaLBuffInit(LuaState l, LuaLBuffer b)
        {
        }

        protected static void LuaPushLiteral(LuaState l, string literalString)
        {
            l.Push(DynValue.NewString(literalString));
        }

        protected static void LuaLPushResult(LuaLBuffer b)
        {
            // SolarSharp modification: Check string length before creating
            var result = b.StringBuilder.ToString();
            var luaState = b.LuaState;
            var script = luaState.ExecutionContext.GetScript();
            if (script.IsAuthorizedToRun())
            {
                var resourceController = script.ResourceController();
                resourceController?.CheckStringLength(result.Length);
            }
            
            LuaPushLiteral(luaState, result);
        }

        protected static void LuaPushLString(LuaState l, CharPtr s, uint len)
        {
            // SolarSharp modification: Check string length before creating
            var script = l.ExecutionContext.GetScript();
            if (script.IsAuthorizedToRun())
            {
                var resourceController = script.ResourceController();
                resourceController?.CheckStringLength((lua_Integer)len);
            }
            
            var ss = s.ToString((lua_Integer)len);
            l.Push(DynValue.NewString(ss));
        }

        protected static void LuaLCheckStack(LuaState l, lua_Integer n, string message)
        {
            // nop ?
        }

        protected static string LUA_QL(string p)
        {
            return "'" + p + "'";
        }


        protected static void LuaPushNil(LuaState l)
        {
            l.Push(DynValue.Nil);
        }

        protected static void LuaAssert(bool p)
        {
            // ??! 
            // A lot of KopiLua methods fall here in valid state!

            //if (!p)
            //	throw new InternalErrorException("LuaAssert failed!");
        }

        protected static string LuaLTypeName(LuaState l, lua_Integer p)
        {
            return l.At(p).Type.ToErrorTypeString();
        }

        protected static lua_Integer LuaIsString(LuaState l, lua_Integer p)
        {
            var v = l.At(p);
            return (v.Type == DataType.String || v.Type == DataType.Number) ? 1 : 0;
        }

        protected static void LuaPop(LuaState l, lua_Integer p)
        {
            for (var i = 0; i < p; i++)
                l.Pop();
        }

        protected static void LuaGetTable(LuaState l, lua_Integer p)
        {
            // DEBT: this should call metamethods, now it performs raw access
            var key = l.Pop();
            var table = l.At(p);

            if (table.Type != DataType.Table)
                throw new NotImplementedException();

            var v = table.Table.Get(key);
            l.Push(v);
        }

        protected static lua_Integer LuaLOptInt(LuaState l, lua_Integer pos, lua_Integer def)
        {
            return LuaLOptInteger(l, pos, def);
        }

        protected static CharPtr LuaLCheckString(LuaState l, lua_Integer p)
        {
            return LuaLCheckLString(l, p, out var dummy);
        }

        protected static string LuaLCheckStringStr(LuaState l, lua_Integer p)
        {
            return LuaLCheckLString(l, p, out var dummy);
        }

        protected static void LuaLArgError(LuaState l, lua_Integer arg, string p)
        {
            throw ScriptRuntimeException.BadArgument(arg - 1, l.FunctionName, p);
        }

        protected static double LuaLCheckNumber(LuaState l, lua_Integer pos)
        {
            var v = ArgAsType(l, pos, DataType.Number, false);
            return v.Number;
        }

        protected static void LuaPushValue(LuaState l, lua_Integer arg)
        {
            var v = l.At(arg);
            l.Push(v);
        }


        /// <summary>
        /// Calls a function.
        /// To call a function you must use the following protocol: first, the function to be called is pushed onto the stack; then,
        /// the arguments to the function are pushed in direct order; that is, the first argument is pushed first. Finally you call
        /// lua_call; nargs is the number of arguments that you pushed onto the stack. All arguments and the function value are
        /// popped from the stack when the function is called. The function results are pushed onto the stack when the function
        /// returns. The number of results is adjusted to nresults, unless nresults is LUA_MULTRET. In this case, all results from
        /// the function are pushed. Lua takes care that the returned values fit into the stack space. The function results are
        /// pushed onto the stack in direct order (the first result is pushed first), so that after the call the last result is on
        /// the top of the stack.
        /// </summary>
        /// <param name="l">The LuaState</param>
        /// <param name="nargs">The number of arguments.</param>
        /// <param name="nresults">The number of expected results.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected static void LuaCall(LuaState l, lua_Integer nargs, lua_Integer nresults = LUA_MULTRET)
        {
            var args = l.GetTopArray(nargs);

            l.Discard(nargs);

            var func = l.Pop();

            var ret = l.ExecutionContext.Call(func, args);

            if (nresults != 0)
            {
                if (nresults == -1)
                {
                    nresults = (ret.Type == DataType.Tuple) ? ret.Tuple.Length : 1;
                }

                var vals = (ret.Type == DataType.Tuple) ? ret.Tuple : new DynValue[1] { ret };

                var copied = 0;

                for (var i = 0; i < vals.Length && copied < nresults; i++, copied++)
                {
                    l.Push(vals[i]);
                }

                while (copied < nresults)
                {
                    l.Push(DynValue.Nil);
                }
            }
        }
    }
}
