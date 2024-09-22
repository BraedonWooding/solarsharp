using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class BinaryOperatorExpression : Expression
    {
        [Flags]
        private enum Operator
        {
            NotAnOperator = 0,

            Or = 0x1,
            And = 0x2,
            Less = 0x4,
            Greater = 0x8,
            LessOrEqual = 0x10,

            GreaterOrEqual = 0x20,
            NotEqual = 0x40,
            Equal = 0x80,
            StrConcat = 0x100,
            Add = 0x200,
            Sub = 0x400,
            Mul = 0x1000,
            Div = 0x2000,
            Mod = 0x4000,
            Power = 0x8000,
        }

        private class Node
        {
            public Expression Expr;
            public Operator Op;
            public Node Prev;
            public Node Next;
        }

        private class LinkedList
        {
            public Node Nodes;
            public Node Last;
            public Operator OperatorMask;
        }

        private const Operator POWER = Operator.Power;
        private const Operator MUL_DIV_MOD = Operator.Mul | Operator.Div | Operator.Mod;
        private const Operator ADD_SUB = Operator.Add | Operator.Sub;
        private const Operator STRCAT = Operator.StrConcat;
        private const Operator COMPARES = Operator.Less | Operator.Greater | Operator.GreaterOrEqual | Operator.LessOrEqual | Operator.Equal | Operator.NotEqual;
        private const Operator LOGIC_AND = Operator.And;
        private const Operator LOGIC_OR = Operator.Or;

        public static object BeginOperatorChain()
        {
            return new LinkedList();
        }

        public static void AddExpressionToChain(object chain, Expression exp)
        {
            LinkedList list = (LinkedList)chain;
            Node node = new() { Expr = exp };
            AddNode(list, node);
        }

        public static void AddOperatorToChain(object chain, Token op)
        {
            LinkedList list = (LinkedList)chain;
            Node node = new() { Op = ParseBinaryOperator(op) };
            AddNode(list, node);
        }

        public static Expression CommitOperatorChain(object chain, ScriptLoadingContext lcontext)
        {
            return CreateSubTree((LinkedList)chain, lcontext);
        }

        public static Expression CreatePowerExpression(Expression op1, Expression op2, ScriptLoadingContext lcontext)
        {
            return new BinaryOperatorExpression(op1, op2, Operator.Power, lcontext);
        }

        private static void AddNode(LinkedList list, Node node)
        {
            list.OperatorMask |= node.Op;

            if (list.Nodes == null)
            {
                list.Nodes = list.Last = node;
            }
            else
            {
                list.Last.Next = node;
                node.Prev = list.Last;
                list.Last = node;
            }
        }

        /// <summary>
        /// Creates a sub tree of binary expressions
        /// </summary>
        private static Expression CreateSubTree(LinkedList list, ScriptLoadingContext lcontext)
        {
            Operator opfound = list.OperatorMask;

            Node nodes = list.Nodes;

            if ((opfound & POWER) != 0)
                nodes = PrioritizeRightAssociative(nodes, lcontext, POWER);

            if ((opfound & MUL_DIV_MOD) != 0)
                nodes = PrioritizeLeftAssociative(nodes, lcontext, MUL_DIV_MOD);

            if ((opfound & ADD_SUB) != 0)
                nodes = PrioritizeLeftAssociative(nodes, lcontext, ADD_SUB);

            if ((opfound & STRCAT) != 0)
                nodes = PrioritizeRightAssociative(nodes, lcontext, STRCAT);

            if ((opfound & COMPARES) != 0)
                nodes = PrioritizeLeftAssociative(nodes, lcontext, COMPARES);

            if ((opfound & LOGIC_AND) != 0)
                nodes = PrioritizeLeftAssociative(nodes, lcontext, LOGIC_AND);

            if ((opfound & LOGIC_OR) != 0)
                nodes = PrioritizeLeftAssociative(nodes, lcontext, LOGIC_OR);


            if (nodes.Next != null || nodes.Prev != null)
                throw new InternalErrorException("Expression reduction didn't work! - 1");
            if (nodes.Expr == null)
                throw new InternalErrorException("Expression reduction didn't work! - 2");

            return nodes.Expr;
        }

        private static Node PrioritizeLeftAssociative(Node nodes, ScriptLoadingContext lcontext, Operator operatorsToFind)
        {
            for (Node N = nodes; N != null; N = N.Next)
            {
                Operator o = N.Op;

                if ((o & operatorsToFind) != 0)
                {
                    N.Op = Operator.NotAnOperator;
                    N.Expr = new BinaryOperatorExpression(N.Prev.Expr, N.Next.Expr, o, lcontext);
                    N.Prev = N.Prev.Prev;
                    N.Next = N.Next.Next;

                    if (N.Next != null)
                        N.Next.Prev = N;

                    if (N.Prev != null)
                        N.Prev.Next = N;
                    else
                        nodes = N;
                }
            }

            return nodes;
        }

        private static Node PrioritizeRightAssociative(Node nodes, ScriptLoadingContext lcontext, Operator operatorsToFind)
        {
            Node last;
            for (last = nodes; last.Next != null; last = last.Next)
            {
            }

            for (Node N = last; N != null; N = N.Prev)
            {
                Operator o = N.Op;

                if ((o & operatorsToFind) != 0)
                {
                    N.Op = Operator.NotAnOperator;
                    N.Expr = new BinaryOperatorExpression(N.Prev.Expr, N.Next.Expr, o, lcontext);
                    N.Prev = N.Prev.Prev;
                    N.Next = N.Next.Next;

                    if (N.Next != null)
                        N.Next.Prev = N;

                    if (N.Prev != null)
                        N.Prev.Next = N;
                    else
                        nodes = N;
                }
            }

            return nodes;
        }

        private static Operator ParseBinaryOperator(Token token)
        {
            return token.Type switch
            {
                TokenType.Or => Operator.Or,
                TokenType.And => Operator.And,
                TokenType.Op_LessThan => Operator.Less,
                TokenType.Op_GreaterThan => Operator.Greater,
                TokenType.Op_LessThanEqual => Operator.LessOrEqual,
                TokenType.Op_GreaterThanEqual => Operator.GreaterOrEqual,
                TokenType.Op_NotEqual => Operator.NotEqual,
                TokenType.Op_Equal => Operator.Equal,
                TokenType.Op_Concat => Operator.StrConcat,
                TokenType.Op_Add => Operator.Add,
                TokenType.Op_MinusOrSub => Operator.Sub,
                TokenType.Op_Mul => Operator.Mul,
                TokenType.Op_Div => Operator.Div,
                TokenType.Op_Mod => Operator.Mod,
                TokenType.Op_Pwr => Operator.Power,
                _ => throw new InternalErrorException("Unexpected binary operator '{0}'", token.Text),
            };
        }

        private readonly Expression m_Exp1, m_Exp2;
        private readonly Operator m_Operator;

        private BinaryOperatorExpression(Expression exp1, Expression exp2, Operator op, ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            m_Exp1 = exp1;
            m_Exp2 = exp2;
            m_Operator = op;
        }

        private static bool ShouldInvertBoolean(Operator op)
        {
            return op == Operator.NotEqual
                || op == Operator.GreaterOrEqual
                || op == Operator.Greater;
        }

        private static OpCode OperatorToOpCode(Operator op)
        {
            return op switch
            {
                Operator.Less or Operator.GreaterOrEqual => OpCode.Less,
                Operator.LessOrEqual or Operator.Greater => OpCode.LessEq,
                Operator.Equal or Operator.NotEqual => OpCode.Eq,
                Operator.StrConcat => OpCode.Concat,
                Operator.Add => OpCode.Add,
                Operator.Sub => OpCode.Sub,
                Operator.Mul => OpCode.Mul,
                Operator.Div => OpCode.Div,
                Operator.Mod => OpCode.Mod,
                Operator.Power => OpCode.Power,
                _ => throw new InternalErrorException("Unsupported operator {0}", op),
            };
        }

        public override void Compile(ByteCode bc)
        {
            m_Exp1.Compile(bc);

            if (m_Operator == Operator.Or)
            {
                Instruction i = bc.Emit_Jump(OpCode.JtOrPop, -1);
                m_Exp2.Compile(bc);
                i.NumVal = bc.GetJumpPointForNextInstruction();
                return;
            }

            if (m_Operator == Operator.And)
            {
                Instruction i = bc.Emit_Jump(OpCode.JfOrPop, -1);
                m_Exp2.Compile(bc);
                i.NumVal = bc.GetJumpPointForNextInstruction();
                return;
            }


            m_Exp2?.Compile(bc);

            bc.Emit_Operator(OperatorToOpCode(m_Operator));

            if (ShouldInvertBoolean(m_Operator))
                bc.Emit_Operator(OpCode.Not);
        }
    }
}
