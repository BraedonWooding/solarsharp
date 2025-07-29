namespace SolarSharp.Interpreter.DataTypes;

/// <summary>
///     A base class for many SolarSharp objects.
/// </summary>
// TODO: Remove this class
public class RefIdObject
{
    /// <summary>
    ///     Formats a string with a type name and a ref-id
    /// </summary>
    /// <param name="typeString">The type name.</param>
    /// <returns></returns>
    public string FormatTypeString(string typeString)
    {
        return $"{typeString}: {GetHashCode():X8}";
    }
}