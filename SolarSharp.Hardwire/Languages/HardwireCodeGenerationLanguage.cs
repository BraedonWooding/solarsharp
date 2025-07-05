using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;

namespace SolarSharp.Hardwire.Languages
{
    /// <summary>
    /// Represents the base class for hardwiring code generation in different programming languages.
    /// Provides an abstraction for language-specific implementations targeting C#,
    /// VB.NET, or other supported CodeDom-supported languages.
    /// </summary>
    public abstract class HardwireCodeGenerationLanguage
    {
        /// <summary>
        /// Provides an implementation of the <see cref="HardwireCodeGenerationLanguage"/> class
        /// for generating code in the C# programming language.
        /// </summary>
        /// <remarks>
        /// This property returns an instance of <see cref="CSharpHardwireCodeGenerationLanguage"/>.
        /// It is the default language used in the absence of any other specified language for
        /// the <see cref="HardwireGenerator"/> class.
        /// </remarks>
        public static HardwireCodeGenerationLanguage CSharp => new CSharpHardwireCodeGenerationLanguage();

        /// <summary>
        /// Gets a VB.NET-specific implementation of the hardwire code generation language.
        /// This property provides access to functionalities for generating VB.NET code constructs and configurations.
        /// </summary>
        public static HardwireCodeGenerationLanguage VbNet => new VbNetHardwireCodeGenerationLanguage();

        /// <summary>
        /// Gets the name of the code generation language.
        /// This property is used to identify the specific programming language
        /// associated with the derived implementation of the code generation process.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets a CodeDomProvider instance that supports code generation and compilation
        /// using this language. The CodeDomProvider serves as a bridge between the
        /// Code Document Object Model (CodeDOM) and the compiler for a specific programming language.
        /// </summary>
        /// <remarks>
        /// The returned CodeDomProvider instance allows for generating source code from
        /// CodeDOM objects and can compile the generated code. This property is essential
        /// for enabling dynamic code generation and compilation workflows.
        /// </remarks>
        public abstract CodeDomProvider CodeDomProvider { get; }

        /// <summary>
        /// Generates a unary plus operation expression for the given argument.
        /// </summary>
        /// <param name="arg">The expression to which the unary plus operator will be applied.</param>
        /// <return>
        /// A <see cref="System.CodeDom.CodeExpression"/> representing the unary plus operation for the specified argument.
        /// </return>
        public abstract CodeExpression UnaryPlus(CodeExpression arg);

        /// <summary>
        /// Returns a CodeExpression that represents the unary increment operation of the specified code expression.
        /// </summary>
        /// <param name="arg">The CodeExpression to apply the unary increment operation to.</param>
        /// <returns>A CodeExpression representing the unary incremented value of the input expression.</returns>
        public abstract CodeExpression UnaryIncrement(CodeExpression arg);

        /// <summary>
        /// Generates a unary decrement operation expression for the specified argument.
        /// </summary>
        /// <param name="arg">The expression to be decremented.</param>
        /// <returns>A new <see cref="CodeExpression"/> representing the decrement operation on the specified expression.</returns>
        public abstract CodeExpression UnaryDecrement(CodeExpression arg);

        /// <summary>
        /// Generates a CodeExpression representing the unary negation of the provided argument.
        /// </summary>
        /// <param name="arg">The CodeExpression to be negated.</param>
        /// <returns>A CodeExpression representing the negated value of the provided argument.</returns>
        public abstract CodeExpression UnaryNegation(CodeExpression arg);

        /// <summary>
        /// Generates a logical NOT operation representation for the provided expression.
        /// </summary>
        /// <param name="arg">The input expression for which the logical NOT operation is to be applied.</param>
        /// <returns>A <see cref="CodeExpression"/> that represents the logical NOT operation.</returns>
        public abstract CodeExpression UnaryLogicalNot(CodeExpression arg);

        /// <summary>
        /// Generates a unary one's complement operation for the given code expression,
        /// which typically represents a bitwise NOT operation.
        /// </summary>
        /// <param name="arg">The input <see cref="CodeExpression"/> to which the operation is applied.</param>
        /// <returns>A <see cref="CodeExpression"/> representing the unary one's complement of the input expression.</returns>
        public abstract CodeExpression UnaryOneComplement(CodeExpression arg);

        /// <summary>
        /// Performs a bitwise XOR operation between two code expressions and returns the resulting expression.
        /// </summary>
        /// <param name="arg1">The first code expression to apply the XOR operation on.</param>
        /// <param name="arg2">The second code expression to apply the XOR operation on.</param>
        /// <returns>A <see cref="CodeExpression"/> representing the result of the XOR operation between the two input expressions.</returns>
        public abstract CodeExpression BinaryXor(CodeExpression arg1, CodeExpression arg2);

        /// <summary>
        /// Creates a multidimensional array expression using the specified element type and dimensions.
        /// </summary>
        /// <param name="type">The element type for the array.</param>
        /// <param name="args">An array of expressions representing the dimensions of the multidimensional array.</param>
        /// <returns>A <see cref="CodeExpression"/> that represents the constructed multidimensional array.</returns>
        public abstract CodeExpression CreateMultidimensionalArray(string type, CodeExpression[] args);

        /// <summary>
        /// Returns an array of strings containing the initial comment for the generated code files.
        /// </summary>
        /// <returns>
        /// An array of strings representing the initial comment lines to include in the generated file.
        /// </returns>
        public abstract string[] GetInitialComment();

        /// <summary>
        /// Converts a <see cref="CodeExpression"/> to its string representation.
        /// </summary>
        /// <param name="exp">The <see cref="CodeExpression"/> to be converted to a string.</param>
        /// <returns>A string representation of the provided <see cref="CodeExpression"/>.</returns>
        protected string ExpressionToString(CodeExpression exp)
        {
            using StringWriter sourceWriter = new();
            CodeDomProvider.GenerateCodeFromExpression(exp, sourceWriter, new CodeGeneratorOptions());
            return sourceWriter.ToString();
        }

        /// <summary>
        /// Creates a custom <see cref="CodeSnippetExpression"/> based on the provided format and arguments.
        /// </summary>
        /// <param name="format">The format string that defines the structure of the expression.</param>
        /// <param name="args">An array of <see cref="CodeExpression"/> objects to be formatted into the snippet.</param>
        /// <returns>A new <see cref="CodeSnippetExpression"/> containing the formatted code.</returns>
        protected CodeExpression SnippetExpression(string format, params CodeExpression[] args)
        {
            var fmt = "(" + format + ")";
            var res = string.Format(fmt, args.Select(ExpressionToString).OfType<object>().ToArray());
            return new CodeSnippetExpression(res);
        }
    }
}
