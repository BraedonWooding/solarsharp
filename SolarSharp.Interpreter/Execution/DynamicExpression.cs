using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Tree.Expressions;

namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Represents a dynamic expression in the script
    /// </summary>
    public class DynamicExpression : IScriptPrivateResource
    {
        private readonly DynamicExprExpression m_Exp;
        private readonly DynValue m_Constant;

        /// <summary>
        /// The code which generated this expression
        /// </summary>
        public readonly string ExpressionCode;

        internal DynamicExpression(Script S, string strExpr, DynamicExprExpression expr)
        {
            ExpressionCode = strExpr;
            OwnerScript = S;
            m_Exp = expr;
        }

        internal DynamicExpression(Script S, string strExpr, DynValue constant)
        {
            ExpressionCode = strExpr;
            OwnerScript = S;
            m_Constant = constant;
        }

        /// <summary>
        /// Evaluates the expression
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public DynValue Evaluate(ScriptExecutionContext? context = null)
        {
            context ??= OwnerScript.CreateDynamicExecutionContext();

            this.CheckScriptOwnership(context.Value.GetScript());

            if (m_Constant.IsNotNil())
                return m_Constant;

            return m_Exp.Eval(context.Value);
        }

        /// <summary>
        /// Finds a symbol in the expression
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public SymbolRef FindSymbol(ScriptExecutionContext context)
        {
            this.CheckScriptOwnership(context.GetScript());

            if (m_Exp != null)
                return m_Exp.FindDynamic(context);
            else
                return null;
        }

        /// <summary>
        /// Gets the script owning this resource.
        /// </summary>
        /// <value>
        /// The script owning this resource.
        /// </value>
        public Script OwnerScript
        {
            get;
            private set;
        }

        /// <summary>
        /// Determines whether this instance is a constant expression
        /// </summary>
        /// <returns></returns>
        public bool IsConstant()
        {
            return m_Constant.IsNotNil();
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return ExpressionCode.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified <see cref="object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (!(obj is DynamicExpression o))
                return false;

            return o.ExpressionCode == ExpressionCode;
        }

    }
}
