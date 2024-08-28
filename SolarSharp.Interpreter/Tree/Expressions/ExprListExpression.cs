using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class ExprListExpression : Expression
    {
        private readonly List<Expression> expressions;

        public ExprListExpression(List<Expression> exps, ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            expressions = exps;
        }


        public Expression[] GetExpressions()
        {
            return expressions.ToArray();
        }

        public override void Compile(ByteCode bc)
        {
            foreach (var exp in expressions)
                exp.Compile(bc);

            if (expressions.Count > 1)
                bc.Emit_MkTuple(expressions.Count);
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            if (expressions.Count >= 1)
                return expressions[0].Eval(context);

            return DynValue.Void;
        }
    }
}
