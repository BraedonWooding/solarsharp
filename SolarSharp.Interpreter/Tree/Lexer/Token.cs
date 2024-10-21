using SolarSharp.Interpreter.Debug;
using System;

namespace SolarSharp.Interpreter.Tree.Lexer
{
    internal class Token
    {
        public readonly Source Source;
        public readonly int FromCol, ToCol, FromLine, ToLine, PrevCol, PrevLine;
        public readonly TokenType Type;

        public string Text { get; set; }

        public Token(TokenType type, Source source, int fromLine, int fromCol, int toLine, int toCol, int prevLine, int prevCol)
        {
            Type = type;
            Source = source;
            FromLine = fromLine;
            FromCol = fromCol;
            ToCol = toCol;
            ToLine = toLine;
            PrevCol = prevCol;
            PrevLine = prevLine;
        }

        public override string ToString()
        {
            string tokenTypeString = (Type.ToString() + "                                                      ").Substring(0, 16);

            string location = string.Format("{0}:{1}-{2}:{3}", FromLine, FromCol, ToLine, ToCol);

            location = (location + "                                                      ").Substring(0, 10);

            return string.Format("{0}  - {1} - '{2}'", tokenTypeString, location, Text ?? "");
        }

        public static TokenType? GetReservedTokenType(string reservedWord)
        {
            return reservedWord switch
            {
                "and" => TokenType.And,
                "break" => TokenType.Break,
                "do" => TokenType.Do,
                "else" => TokenType.Else,
                "elseif" => TokenType.ElseIf,
                "end" => TokenType.End,
                "false" => TokenType.False,
                "for" => TokenType.For,
                "function" => TokenType.Function,
                "goto" => TokenType.Goto,
                "if" => TokenType.If,
                "in" => TokenType.In,
                "local" => TokenType.Local,
                "nil" => TokenType.Nil,
                "not" => TokenType.Not,
                "or" => TokenType.Or,
                "repeat" => TokenType.Repeat,
                "return" => TokenType.Return,
                "then" => TokenType.Then,
                "true" => TokenType.True,
                "until" => TokenType.Until,
                "while" => TokenType.While,
                _ => null,
            };
        }

        public double GetNumberValue()
        {
            if (Type == TokenType.Number)
                return LexerUtils.ParseNumber(this);
            else if (Type == TokenType.Number_Hex)
                return LexerUtils.ParseHexInteger(this);
            else if (Type == TokenType.Number_HexFloat)
                return LexerUtils.ParseHexFloat(this);
            else
                throw new NotSupportedException("GetNumberValue is supported only on numeric tokens");
        }

        public bool IsEndOfBlock()
        {
            switch (Type)
            {
                case TokenType.Else:
                case TokenType.ElseIf:
                case TokenType.End:
                case TokenType.Until:
                case TokenType.Eof:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsUnaryOperator()
        {
            return Type == TokenType.Op_MinusOrSub || Type == TokenType.Not || Type == TokenType.Op_Len;
        }

        public bool IsBinaryOperator()
        {
            switch (Type)
            {
                case TokenType.And:
                case TokenType.Or:
                case TokenType.Op_Equal:
                case TokenType.Op_LessThan:
                case TokenType.Op_LessThanEqual:
                case TokenType.Op_GreaterThanEqual:
                case TokenType.Op_GreaterThan:
                case TokenType.Op_NotEqual:
                case TokenType.Op_Concat:
                case TokenType.Op_Pwr:
                case TokenType.Op_Mod:
                case TokenType.Op_Div:
                case TokenType.Op_Mul:
                case TokenType.Op_MinusOrSub:
                case TokenType.Op_Add:
                    return true;
                default:
                    return false;
            }
        }

        internal SourceRef GetSourceRef()
        {
            return new SourceRef
            {
                Source = Source,
                LineNumber = FromLine,
                ColumnNumber = FromCol,
            };
        }
    }
}
