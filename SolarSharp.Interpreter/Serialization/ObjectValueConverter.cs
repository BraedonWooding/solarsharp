using System;
using System.Collections;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.Converters;

namespace SolarSharp.Interpreter.Serialization;

public static class ObjectValueConverter
{
    public static LuaValue SerializeObjectToLuaValue(Script script, object o, LuaValue valueForNulls = null)
    {
        if (o == null)
            return valueForNulls ?? LuaValue.Nil;

        var v = ClrToScriptConversions.TryObjectToTrivialLuaValue(script, o);

        if (v != null)
            return v;

        if (o is Enum)
            return LuaValue.NewNumber(NumericConversions.TypeToDouble(Enum.GetUnderlyingType(o.GetType()), o));

        Table t = new(script);


        if (o is IEnumerable ienum)
        {
            foreach (var obj in ienum) t.Append(SerializeObjectToLuaValue(script, obj, valueForNulls));
        }
        else
        {
            var type = o.GetType();

            foreach (var pi in Framework.Do.GetProperties(type))
            {
                var getter = Framework.Do.GetGetMethod(pi);
                var isStatic = getter.IsStatic;
                var obj = getter.Invoke(isStatic ? null : o,
                    null); // convoluted workaround for --full-aot Mono execution

                t.Set(pi.Name, SerializeObjectToLuaValue(script, obj, valueForNulls));
            }
        }

        return LuaValue.NewTable(t);
    }
}