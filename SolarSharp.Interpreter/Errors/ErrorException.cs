using SolarSharp.Interpreter.DataTypes;
using System;

namespace SolarSharp.Interpreter.Errors
{
    /// <summary>
    /// Core lua `error` exception.
    /// </summary>
    [Serializable]
    public class ErrorException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorException"/> class.
        /// </summary>
        /// <param name="ex">The ex.</param>
        protected ErrorException(Exception ex, string message)
            : base(message, ex)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorException"/> class.
        /// </summary>
        /// <param name="ex">The ex.</param>
        protected ErrorException(Exception ex)
            : base(ex.Message, ex)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        protected ErrorException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorException"/> class.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        protected ErrorException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Gets the instruction pointer of the execution (if it makes sense)
        /// </summary>
        public int InstructionPtr { get; internal set; }

        /// <summary>
        /// Gets the decorated message (error message plus error location in script) if possible.
        /// </summary>
        public string DecoratedMessage { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether the message should not be decorated
        /// </summary>
        public bool DoNotDecorateMessage { get; set; }

        internal void DecorateMessage(LuaState script, SourceRef sref, int ip = -1)
        {
            if (string.IsNullOrEmpty(this.DecoratedMessage))
            {
                if (DoNotDecorateMessage)
                {
                    this.DecoratedMessage = this.Message;
                    return;
                }
                else if (sref != null)
                {
                    this.DecoratedMessage = string.Format("{0}: {1}", sref.FormatLocation(script), this.Message);
                }
                else
                {
                    this.DecoratedMessage = string.Format("bytecode:{0}: {1}", ip, this.Message);
                }
            }
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an arithmetic operation was attempted on non-numbers
        /// </summary>
        /// <param name="l">The left operand.</param>
        /// <param name="r">The right operand (or null).</param>
        /// <returns>The exception to be raised.</returns>
        /// <exception cref="InternalErrorException">If both are numbers</exception>
        public static ErrorException ArithmeticOnNonNumber(DynValue l, DynValue r = default)
        {
            if (l.Type != DataType.Number && l.Type != DataType.String)
                return new ErrorException("attempt to perform arithmetic on a {0} value", l.Type.ToLuaTypeString());
            else if (r.IsNotNil() && r.Type != DataType.Number && r.Type != DataType.String)
                return new ErrorException("attempt to perform arithmetic on a {0} value", r.Type.ToLuaTypeString());
            else if (l.Type == DataType.String || r.IsNotNil() && r.Type == DataType.String)
                return new ErrorException("attempt to perform arithmetic on a string value");
            else
                throw new InternalErrorException("ArithmeticOnNonNumber - both are numbers");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a concat operation was attempted on non-strings
        /// </summary>
        /// <param name="l">The left operand.</param>
        /// <param name="r">The right operand.</param>
        /// <returns>The exception to be raised.</returns>
        /// <exception cref="InternalErrorException">If both are numbers or strings</exception>
        public static ErrorException ConcatOnNonString(DynValue l, DynValue r)
        {
            if (l.Type != DataType.Number && l.Type != DataType.String)
                return new ErrorException("attempt to concatenate a {0} value", l.Type.ToLuaTypeString());
            else if (r.IsNotNil() && r.Type != DataType.Number && r.Type != DataType.String)
                return new ErrorException("attempt to concatenate a {0} value", r.Type.ToLuaTypeString());
            else
                throw new InternalErrorException("ConcatOnNonString - both are numbers/strings");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a len operator was applied on an invalid operand
        /// </summary>
        /// <param name="r">The operand.</param>
        /// <returns>The exception to be raised.</returns>
        public static ErrorException LenOnInvalidType(DynValue r)
        {
            return new ErrorException("attempt to get length of a {0} value", r.Type.ToLuaTypeString());
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a comparison operator was applied on an invalid combination of operand types
        /// </summary>
        /// <param name="l">The left operand.</param>
        /// <param name="r">The right operand.</param>
        /// <returns>The exception to be raised.</returns>
        public static ErrorException CompareInvalidType(DynValue l, DynValue r)
        {
            if (l.Type.ToLuaTypeString() == r.Type.ToLuaTypeString())
                return new ErrorException("attempt to compare two {0} values", l.Type.ToLuaTypeString());
            else
                return new ErrorException("attempt to compare {0} with {1}", l.Type.ToLuaTypeString(), r.Type.ToLuaTypeString());
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with a bad argument
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <param name="message">The error message.</param>
        /// <returns>The exception to be raised.</returns>
        public static ErrorException BadArgument(int argNum, string funcName, string message)
        {
            return new ErrorException("bad argument #{0} to '{1}' ({2})", argNum + 1, funcName, message);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with a bad userdata argument
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <param name="expected">The expected System.Type.</param>
        /// <param name="got">The object which was used.</param>
        /// <param name="allowNil">True if nils were allowed in this call.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgumentUserData(int argNum, string funcName, Type expected, object got, bool allowNil)
        {
            return new ErrorException("bad argument #{0} to '{1}' (userdata<{2}>{3} expected, got {4})",
                argNum + 1,
                funcName,
                expected.Name,
                allowNil ? "nil or " : "",
                got != null ? "userdata<" + got.GetType().Name + ">" : "null"
                );
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with a bad argument
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <param name="expected">The expected data type.</param>
        /// <param name="got">The data type received.</param>
        /// <param name="allowNil">True if nils were allowed in this call.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgument(int argNum, string funcName, DataType expected, DataType got, bool allowNil)
        {
            return BadArgument(argNum, funcName, expected.ToErrorTypeString(), got.ToErrorTypeString(), allowNil);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with a bad argument
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <param name="expected">The expected type description.</param>
        /// <param name="got">The description of the type received.</param>
        /// <param name="allowNil">True if nils were allowed in this call.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgument(int argNum, string funcName, string expected, string got, bool allowNil)
        {
            return new ErrorException("bad argument #{0} to '{1}' ({2}{3} expected, got {4})",
                argNum + 1, funcName, allowNil ? "nil or " : "", expected, got);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with no value when a value was required.
        /// 
        /// This function creates a message like "bad argument #xxx to 'yyy' (zzz expected, got no value)"
        /// while <see cref="BadArgumentValueExpected" /> creates a message like "bad argument #xxx to 'yyy' (value expected)"
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <param name="expected">The expected data type.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgumentNoValue(int argNum, string funcName, DataType expected)
        {
            return new ErrorException("bad argument #{0} to '{1}' ({2} expected, got no value)",
                argNum + 1, funcName, expected.ToErrorTypeString());
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an out of range index was specified
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgumentIndexOutOfRange(string funcName, int argNum)
        {
            return new ErrorException("bad argument #{0} to '{1}' (index out of range)", argNum + 1, funcName);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with a negative number when a positive one was expected.
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgumentNoNegativeNumbers(int argNum, string funcName)
        {
            return new ErrorException("bad argument #{0} to '{1}' (not a non-negative number in proper range)",
                argNum + 1, funcName);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a function was called with no value when a value was required.
        /// This function creates a message like "bad argument #xxx to 'yyy' (value expected)"
        /// while <see cref="BadArgumentNoValue" /> creates a message like "bad argument #xxx to 'yyy' (zzz expected, got no value)"
        /// </summary>
        /// <param name="argNum">The argument number (0-based).</param>
        /// <param name="funcName">Name of the function generating this error.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException BadArgumentValueExpected(int argNum, string funcName)
        {
            return new ErrorException("bad argument #{0} to '{1}' (value expected)",
                argNum + 1, funcName);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an invalid attempt to index the specified object was made
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException IndexType(DynValue obj)
        {
            return new ErrorException("attempt to index a {0} value", obj.Type.ToLuaTypeString());
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a loop was detected when performing __index over metatables.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException LoopInIndex()
        {
            return new ErrorException("loop in gettable");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a loop was detected when performing __newindex over metatables.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException LoopInNewIndex()
        {
            return new ErrorException("loop in settable");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a loop was detected when performing __call over metatables.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException LoopInCall()
        {
            return new ErrorException("loop in call");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a table indexing operation used nil as the key.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException TableIndexIsNil()
        {
            return new ErrorException("table index is nil");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a table indexing operation used a NaN as the key.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException TableIndexIsNaN()
        {
            return new ErrorException("table index is NaN");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a conversion to number failed.
        /// </summary>
        /// <param name="stage">
        /// Selects the correct error message:
        /// 0 - "value must be a number"
        /// 1 - "'for' initial value must be a number"
        /// 2 - "'for' step must be a number"
        /// 3 - "'for' limit must be a number"
        /// </param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException ConvertToNumberFailed(int stage)
        {
            switch (stage)
            {
                case 1:
                    return new ErrorException("'for' initial value must be a number");
                case 2:
                    return new ErrorException("'for' step must be a number");
                case 3:
                    return new ErrorException("'for' limit must be a number");
                default:
                    return new ErrorException("value must be a number");
            }
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a conversion of a CLR type to a Lua type has failed.
        /// </summary>
        /// <param name="obj">The object which could not be converted.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException ConvertObjectFailed(object obj)
        {
            return new ErrorException("cannot convert clr type {0}", obj.GetType());
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a conversion of a Lua type to a CLR type has failed.
        /// </summary>
        /// <param name="t">The Lua type.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException ConvertObjectFailed(DataType t)
        {
            return new ErrorException("cannot convert a {0} to a clr type", t.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a constrained conversion of a Lua type to a CLR type has failed.
        /// </summary>
        /// <param name="t">The Lua type.</param>
        /// <param name="t2">The expected CLR type.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException ConvertObjectFailed(DataType t, Type t2)
        {
            return new ErrorException("cannot convert a {0} to a clr type {1}", t.ToString().ToLowerInvariant(), t2.FullName);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// a userdata of a specific CLR type was expected and a non-userdata type was passed.
        /// </summary>
        /// <param name="t">The Lua type.</param>
        /// <param name="clrType">The expected CLR type.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException UserDataArgumentTypeMismatch(DataType t, Type clrType)
        {
            return new ErrorException("cannot find a conversion from a MoonSharp {0} to a clr {1}", t.ToString().ToLowerInvariant(), clrType.FullName);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt to index an invalid member of a userdata was done.
        /// </summary>
        /// <param name="typename">The name of the userdata type.</param>
        /// <param name="fieldname">The field name.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException UserDataMissingField(string typename, string fieldname)
        {
            return new ErrorException("cannot access field {0} of userdata<{1}>", fieldname, typename);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt resume a coroutine in an invalid state was done.
        /// </summary>
        /// <param name="state">The state of the coroutine.</param>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException CannotResumeNotSuspended(CoroutineState state)
        {
            if (state == CoroutineState.Dead)
                return new ErrorException("cannot resume dead coroutine");
            else
                return new ErrorException("cannot resume non-suspended coroutine");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt to yield across a CLR boundary was made.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException CannotYield()
        {
            return new ErrorException("attempt to yield across a CLR-call boundary");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt to yield from the main coroutine was made.
        /// </summary>
        /// <returns>
        /// The exception to be raised.
        /// </returns>
        public static ErrorException CannotYieldMain()
        {
            return new ErrorException("attempt to yield from outside a coroutine");
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt to call a non-function was made
        /// </summary>
        /// <param name="type">The lua non-function data type.</param>
        /// <param name="debugText">The debug text to aid location (appears as "near 'xxx'").</param>
        /// <returns></returns>
        public static ErrorException AttemptToCallNonFunc(DataType type, string debugText = null)
        {
            string functype = type.ToErrorTypeString();

            if (debugText != null)
                return new ErrorException("attempt to call a {0} value near '{1}'", functype, debugText);
            else
                return new ErrorException("attempt to call a {0} value", functype);
        }


        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt to access a non-static member from a static userdata was made
        /// </summary>
        /// <param name="desc">The member descriptor.</param>
        public static ErrorException AccessInstanceMemberOnStatics(IMemberDescriptor desc)
        {
            return new ErrorException("attempt to access instance member {0} from a static userdata", desc.Name);
        }

        /// <summary>
        /// Creates a ScriptRuntimeException with a predefined error message specifying that
        /// an attempt to access a non-static member from a static userdata was made
        /// </summary>
        /// <param name="typeDescr">The type descriptor.</param>
        /// <param name="desc">The member descriptor.</param>
        /// <returns></returns>
        public static ErrorException AccessInstanceMemberOnStatics(IUserDataDescriptor typeDescr, IMemberDescriptor desc)
        {
            return new ErrorException("attempt to access instance member {0}.{1} from a static userdata", typeDescr.Name, desc.Name);
        }
    }
}
