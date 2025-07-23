using System.Collections.Generic;
using System.Linq;
using System.Text;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Serialization
{
    /// <summary>
    ///
    /// </summary>
    public static class SerializationExtensions
    {
        private static readonly HashSet<string> LUAKEYWORDS = new HashSet<string>
        {
            "and",
            "break",
            "do",
            "else",
            "elseif",
            "end",
            "false",
            "for",
            "function",
            "goto",
            "if",
            "in",
            "local",
            "nil",
            "not",
            "or",
            "repeat",
            "return",
            "then",
            "true",
            "until",
            "while",
        };

        public static string Serialize(this Table table, bool prefixReturn = false, int tabs = 0)
        {
            // Tables are now always "prime" tables since we removed ownership
            // This check is no longer needed

            var tabstr = new string('\t', tabs);
            var sb = new StringBuilder();

            //sb.Append(tabstr);

            if (prefixReturn)
                sb.Append("return ");

            if (!table.Values.Any())
            {
                sb.Append("${ }");
                return sb.ToString();
            }

            sb.AppendLine("${");

            foreach (var kvp in table)
            {
                sb.Append(tabstr);

                var key = IsStringIdentifierValid(kvp.Key)
                    ? kvp.Key.String
                    : "[" + kvp.Key.SerializeValue(tabs + 1) + "]";

                sb.AppendFormat("\t{0} = {1},\n", key, kvp.Value.SerializeValue(tabs + 1));
            }

            sb.Append(tabstr);
            sb.Append("}");

            if (tabs == 0)
                sb.AppendLine();

            return sb.ToString();
        }

        private static bool IsStringIdentifierValid(DynValue dynValue)
        {
            if (dynValue.Type != DataType.String)
                return false;

            if (dynValue.String.Length == 0)
                return false;

            if (LUAKEYWORDS.Contains(dynValue.String))
                return false;

            if (!char.IsLetter(dynValue.String[0]) && dynValue.String[0] != '_')
                return false;

            foreach (var c in dynValue.String)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        public static string SerializeValue(this DynValue dynValue, int tabs = 0)
        {
            if (dynValue.Type is DataType.Nil or DataType.Void)
                return "nil";
            if (dynValue.Type == DataType.Tuple)
                return dynValue.Tuple.Any() ? dynValue.Tuple[0].SerializeValue(tabs) : "nil";
            if (dynValue.Type == DataType.Number)
                return dynValue.Number.ToString("r");
            if (dynValue.Type == DataType.Boolean)
                return dynValue.Boolean ? "true" : "false";
            if (dynValue.Type == DataType.String)
                return EscapeString(dynValue.String ?? "");
            if (dynValue.Type == DataType.Table)
                return dynValue.Table.Serialize(false, tabs);
            throw new ScriptRuntimeException("Value is not a primitive value or a prime table.");
        }

        private static string EscapeString(string s)
        {
            s = s.Replace(@"\", @"\\");
            s = s.Replace("\n", @"\n");
            s = s.Replace("\r", @"\r");
            s = s.Replace("\t", @"\t");
            s = s.Replace("\a", @"\a");
            s = s.Replace("\f", @"\f");
            s = s.Replace("\b", @"\b");
            s = s.Replace("\v", @"\v");
            s = s.Replace("\"", "\\\"");
            s = s.Replace("\'", @"\'");
            return "\"" + s + "\"";
        }
    }
}
