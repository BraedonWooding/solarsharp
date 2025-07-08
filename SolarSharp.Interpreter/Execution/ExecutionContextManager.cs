using System;
using System.Collections.Generic;
using System.Threading;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Manages execution contexts in a functional way
    /// </summary>
    public static class ExecutionContextManager
    {
        // Thread-local storage for current context stack
        // This is the only mutable state, encapsulated here
        private static readonly ThreadLocal<ExecutionContextStack> _contextStack =
            new ThreadLocal<ExecutionContextStack>(() => new ExecutionContextStack());

        /// <summary>
        /// Gets the current execution context
        /// </summary>
        public static Maybe<LuaExecutionContext> Current
        {
            get { return _contextStack.Value.Current; }
        }

        /// <summary>
        /// Executes an operation within a given context
        /// </summary>
        public static Result<T, ExecutionError> WithContext<T>(
            LuaExecutionContext context,
            Func<LuaExecutionContext, Result<T, ExecutionError>> operation
        )
        {
            using (_contextStack.Value.Push(context))
            {
                return operation(context);
            }
        }

        /// <summary>
        /// Executes an operation within a child context
        /// </summary>
        public static Result<T, ExecutionError> WithChildContext<T>(
            LuaExecutionContext parent,
            string sourceFile,
            Func<LuaExecutionContext, Result<T, ExecutionError>> operation
        )
        {
            var childContext = parent.With(
                sourceFile: sourceFile,
                parent: Maybe<LuaExecutionContext>.From(parent)
            );

            return WithContext(childContext, operation);
        }

        /// <summary>
        /// Gets the current context as a Result
        /// </summary>
        public static Result<LuaExecutionContext, ExecutionError> GetCurrent() =>
            Current.Match(
                ctx => Result.Success<LuaExecutionContext, ExecutionError>(ctx),
                () => Result.Failure<LuaExecutionContext, ExecutionError>(new NoExecutionContext())
            );

        /// <summary>
        /// Private class to manage context stack
        /// </summary>
        private sealed class ExecutionContextStack
        {
            private readonly Stack<LuaExecutionContext> _stack = new Stack<LuaExecutionContext>();

            public Maybe<LuaExecutionContext> Current
            {
                get
                {
                    return _stack.Count > 0
                        ? Maybe<LuaExecutionContext>.From(_stack.Peek())
                        : Maybe<LuaExecutionContext>.None;
                }
            }

            public IDisposable Push(LuaExecutionContext context)
            {
                _stack.Push(context);
                return new StackPopper(_stack);
            }

            private sealed class StackPopper : IDisposable
            {
                private readonly Stack<LuaExecutionContext> _stack;
                private bool _disposed;

                public StackPopper(Stack<LuaExecutionContext> stack)
                {
                    _stack = stack;
                }

                public void Dispose()
                {
                    if (!_disposed)
                    {
                        _stack.Pop();
                        _disposed = true;
                    }
                }
            }
        }
    }
}
