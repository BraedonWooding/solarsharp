using System.IO;
using System.Text;
using System.Text.Json;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Serialization.Json
{
    /// <summary>
    /// Class performing conversions between Tables and Json.
    /// NOTE : the conversions are done respecting json syntax but using Lua constructs. This means mostly that:
    /// 1) Lua string escapes can be accepted while they are not technically valid JSON, and viceversa
    /// 2) Null values are represented using a static userdata of type JsonNull
    /// 3) Do not use it when input cannot be entirely trusted
    /// </summary>
    public static class JsonTableConverter
    {
        /// <summary>
        /// Converts a table to a json string
        /// </summary>
        /// <param name="table">The table.</param>
        /// <returns></returns>
        public static string TableToJson(this Table table)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            TableToJson(writer, table);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Tables to json.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="table">The table.</param>
        private static void TableToJson(Utf8JsonWriter writer, Table table)
        {
            if (table.Length == 0)
            {
                writer.WriteStartObject();
                foreach (var pair in table)
                {
                    if (pair.Key.Type == DataType.String && IsValueJsonCompatible(pair.Value))
                    {
                        writer.WritePropertyName(pair.Key.String);
                        ValueToJson(writer, pair.Value);
                    }
                }
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStartArray();
                for (var i = 1; i <= table.Length; i++)
                {
                    var value = table.Get(i);
                    if (IsValueJsonCompatible(value))
                    {
                        ValueToJson(writer, value);
                    }
                }
                writer.WriteEndArray();
            }
        }

        /// <summary>
        /// Converts a generic object to JSON
        /// </summary>
        public static string ObjectToJson(object obj)
        {
            var v = ObjectValueConverter.SerializeObjectToDynValue(null, obj, JsonNull.Create());
            return v.Table.TableToJson();
        }

        private static void ValueToJson(Utf8JsonWriter writer, DynValue value)
        {
            switch (value.Type)
            {
                case DataType.Boolean:
                    writer.WriteBooleanValue(value.Boolean);
                    break;
                case DataType.Number:
                    writer.WriteNumberValue(value.Number);
                    break;
                case DataType.String:
                    writer.WriteStringValue(value.String ?? "");
                    break;
                case DataType.Table:
                    TableToJson(writer, value.Table);
                    break;
                case DataType.Nil:
                case DataType.Void:
                case DataType.UserData:
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static bool IsValueJsonCompatible(DynValue value)
        {
            return value.Type == DataType.Boolean
                || value.IsNil()
                || value.Type == DataType.Number
                || value.Type == DataType.String
                || value.Type == DataType.Table
                || JsonNull.IsJsonNull(value);
        }

        /// <summary>
        /// Converts a json string to a table
        /// </summary>
        /// <param name="json">The json.</param>
        /// <param name="script">The script to which the table is assigned (null for prime tables).</param>
        /// <returns>A table containing the representation of the given json.</returns>
        public static Table JsonToTable(string json, Script script = null)
        {
            var L = new Lexer(0, json, false);

            if (L.Current.Type == TokenType.Brk_Open_Curly)
                return ParseJsonObject(L, script);
            if (L.Current.Type == TokenType.Brk_Open_Square)
                return ParseJsonArray(L, script);
            throw new SyntaxErrorException(L.Current, "Unexpected token : '{0}'", L.Current.Text);
        }

        private static void AssertToken(Lexer L, TokenType type)
        {
            if (L.Current.Type != type)
                throw new SyntaxErrorException(
                    L.Current,
                    "Unexpected token : '{0}'",
                    L.Current.Text
                );
        }

        private static Table ParseJsonArray(Lexer L, Script script)
        {
            var t = new Table();

            L.Next();

            while (L.Current.Type != TokenType.Brk_Close_Square)
            {
                var v = ParseJsonValue(L, script);
                t.Append(v);
                L.Next();

                if (L.Current.Type == TokenType.Comma)
                    L.Next();
            }

            return t;
        }

        private static Table ParseJsonObject(Lexer L, Script script)
        {
            var t = new Table();

            L.Next();

            while (L.Current.Type != TokenType.Brk_Close_Curly)
            {
                AssertToken(L, TokenType.String);
                var key = L.Current.Text;
                L.Next();
                AssertToken(L, TokenType.Colon);
                L.Next();
                var v = ParseJsonValue(L, script);
                t.Set(key, v);
                L.Next();

                if (L.Current.Type == TokenType.Comma)
                    L.Next();
            }

            return t;
        }

        private static DynValue ParseJsonValue(Lexer L, Script script)
        {
            if (L.Current.Type == TokenType.Brk_Open_Curly)
            {
                var t = ParseJsonObject(L, script);
                return DynValue.NewTable(t);
            }
            if (L.Current.Type == TokenType.Brk_Open_Square)
            {
                var t = ParseJsonArray(L, script);
                return DynValue.NewTable(t);
            }
            if (L.Current.Type == TokenType.String)
            {
                return DynValue.NewString(L.Current.Text);
            }
            if (L.Current.Type is TokenType.Number or TokenType.Op_MinusOrSub)
            {
                return ParseJsonNumberValue(L, script);
            }
            if (L.Current.Type == TokenType.True)
            {
                return DynValue.True;
            }
            if (L.Current.Type == TokenType.False)
            {
                return DynValue.False;
            }
            if (L.Current.Type == TokenType.Name && L.Current.Text == "null")
            {
                return JsonNull.Create();
            }
            throw new SyntaxErrorException(L.Current, "Unexpected token : '{0}'", L.Current.Text);
        }

        private static DynValue ParseJsonNumberValue(Lexer L, Script _)
        {
            bool negative;
            if (L.Current.Type == TokenType.Op_MinusOrSub)
            {
                // Negative number consists of 2 tokens.
                L.Next();
                negative = true;
            }
            else
            {
                negative = false;
            }
            if (L.Current.Type != TokenType.Number)
            {
                throw new SyntaxErrorException(
                    L.Current,
                    "Unexpected token : '{0}'",
                    L.Current.Text
                );
            }
            var numberValue = L.Current.GetNumberValue();
            if (negative)
            {
                numberValue = -numberValue;
            }
            return DynValue.NewNumber(numberValue).AsReadOnly();
        }
    }
}
