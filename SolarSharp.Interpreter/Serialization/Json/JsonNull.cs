using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.Attributes;

namespace SolarSharp.Interpreter.Serialization.Json;

/// <summary>
///     UserData representing a null value in a table converted from Json
/// </summary>
public sealed class JsonNull
{
    public static bool isNull()
    {
        return true;
    }

    [SolarSharpHidden]
    public static bool IsJsonNull(LuaValue v)
    {
        return v.Type == DataType.UserData &&
               v.UserData.Descriptor != null &&
               v.UserData.Descriptor.Type == typeof(JsonNull);
    }

    [SolarSharpHidden]
    public static LuaValue Create()
    {
        return UserData.CreateStatic<JsonNull>();
    }
}