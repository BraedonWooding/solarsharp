using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions;

internal class LiteralExpression : Expression
{
    public LiteralExpression(ScriptLoadingContext lcontext, LuaValue value)
        : base(lcontext)
    {
        Value = value;
    }


    public LiteralExpression(ScriptLoadingContext lcontext, Token t)
        : base(lcontext)
    {
        Value = t.Type switch
        {
            TokenType.Number or TokenType.Number_Hex or TokenType.Number_HexFloat => LuaValue
                .NewNumber(t.GetNumberValue()).AsReadOnly(),
            TokenType.String or TokenType.String_Long => LuaValue.NewString(t.Text).AsReadOnly(),
            TokenType.True => LuaValue.True,
            TokenType.False => LuaValue.False,
            TokenType.Nil => LuaValue.Nil,
            _ => throw new InternalErrorException("type mismatch")
        };
        if (Value == null)
            throw new SyntaxErrorException(t, "unknown literal format near '{0}'", t.Text);

        lcontext.Lexer.Next();
    }

    public LuaValue Value { get; }

    public override void Compile(ByteCode bc)
    {
        bc.Emit_Literal(Value);
    }
}