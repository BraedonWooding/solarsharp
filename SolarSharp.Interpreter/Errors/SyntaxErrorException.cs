using System;
using SolarSharp.Interpreter.Debug;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Errors
{
    /// <summary>
    /// Exception for all parsing/lexing errors. 
    /// </summary>
    [Serializable]
    public class SyntaxErrorException : ErrorException
    {
        // TODO: Maybe add "to" line number rather than just "from" (will need to diverge from SourceRef).

        internal Token Token { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this exception was caused by premature stream termination (that is, unexpected EOF).
        /// This can be used in REPL interfaces to tell between unrecoverable errors and those which can be recovered by extra input.
        /// </summary>
        public bool IsPrematureStreamTermination { get; set; }

        internal SyntaxErrorException(Token t, string format, params object[] args)
            : base(format, args)
        {
            Token = t;
        }

        internal SyntaxErrorException(Token t, string message)
            : base(message)
        {
            Token = t;
        }

        internal SyntaxErrorException(LuaState script, SourceRef sref, string format, params object[] args)
            : base(format, args)
        {
            DecorateMessage(sref);
        }

        internal SyntaxErrorException(LuaState script, SourceRef sref, string message)
            : base(message)
        {
            DecorateMessage(sref);
        }

        private SyntaxErrorException(SyntaxErrorException syntaxErrorException)
            : base(syntaxErrorException, syntaxErrorException.DecoratedMessage)
        {
            Token = syntaxErrorException.Token;
            DecoratedMessage = Message;
        }

        internal void DecorateMessage(LuaState script)
        {
            if (Token != null)
            {
                DecorateMessage(Token.GetSourceRef());
            }
        }
    }
}
