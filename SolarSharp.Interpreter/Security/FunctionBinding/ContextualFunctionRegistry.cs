using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;

namespace SolarSharp.Interpreter.Security.FunctionBinding
{
    /// <summary>
    /// Provides copy-on-write, context-isolated views of global functions.
    /// Each execution context gets its own lazy-loaded view that shares data
    /// with parent contexts until modifications occur.
    /// </summary>
    public sealed class ContextualFunctionRegistry
    {
        // Script-specific function definitions - not shared between Script instances
        private readonly ConcurrentDictionary<string, DynValue> _scriptFunctions = new();

        // Per-context function overrides - only allocated when context modifies functions
        private readonly ConcurrentDictionary<string, ContextFunctionView> _contextViews = new();

        // Fast path cache for contexts with no overrides
        private readonly ConditionalWeakTable<LuaExecutionContext, object> _pureContextCache =
            new();

        /// <summary>
        /// Registers a function for this script instance. This is the baseline function available to all contexts
        /// within this script unless they override it locally.
        /// </summary>
        public void RegisterGlobalFunction(string name, DynValue function)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Function name cannot be null or empty", nameof(name));

            _scriptFunctions[name] = function ?? throw new ArgumentNullException(nameof(function));
        }

        /// <summary>
        /// Gets a function for the given execution context. Uses copy-on-write semantics:
        /// - If context has no local overrides, returns global function (fast path)
        /// - If context has overrides, returns context-specific function
        /// - If parent context exists, inherits its view until local modifications occur
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynValue GetFunction(LuaExecutionContext context, string name)
        {
            if (string.IsNullOrEmpty(name))
                return DynValue.Nil;

            // Fast path: Check if context has no overrides
            if (_pureContextCache.TryGetValue(context, out _))
            {
                return _scriptFunctions.TryGetValue(name, out var globalFunc)
                    ? globalFunc
                    : DynValue.Nil;
            }

            // Check for context-specific overrides
            var contextKey = GetContextKey(context);
            if (_contextViews.TryGetValue(contextKey, out var view))
            {
                return view.GetFunction(name);
            }

            // No context view exists yet - check if parent has one
            if (context.Parent.HasValue)
            {
                var parentFunc = GetFunction(context.Parent.Value, name);
                if (parentFunc != DynValue.Nil)
                    return parentFunc;
            }

            // Fall back to script function
            return _scriptFunctions.TryGetValue(name, out var func) ? func : DynValue.Nil;
        }

        /// <summary>
        /// Sets a function for a specific execution context. This creates a copy-on-write
        /// context view if one doesn't exist yet.
        /// </summary>
        public void SetFunction(LuaExecutionContext context, string name, DynValue function)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Function name cannot be null or empty", nameof(name));

            var contextKey = GetContextKey(context);

            // Remove from pure context cache since we're making modifications
            _pureContextCache.Remove(context);

            // Get or create context view
            var view = _contextViews.GetOrAdd(contextKey, _ => CreateContextView(context));
            view.SetFunction(name, function);
        }

        /// <summary>
        /// Removes a function from a specific execution context.
        /// Sets it to nil in the context's view.
        /// </summary>
        public void RemoveFunction(LuaExecutionContext context, string name)
        {
            SetFunction(context, name, DynValue.Nil);
        }

        /// <summary>
        /// Marks a context as having no local overrides for fast-path optimization.
        /// This should be called for contexts that will never modify global functions.
        /// </summary>
        public void MarkContextAsPure(LuaExecutionContext context)
        {
            var contextKey = GetContextKey(context);
            if (!_contextViews.ContainsKey(contextKey))
            {
                _pureContextCache.GetOrCreateValue(context);
            }
        }

        /// <summary>
        /// Gets all function names available in the given context.
        /// Efficiently combines global and context-specific functions.
        /// </summary>
        public IEnumerable<string> GetAvailableFunctions(LuaExecutionContext context)
        {
            var functions = new HashSet<string>(_scriptFunctions.Keys);

            // Add functions from parent contexts
            AddParentFunctions(context, functions);

            // Add/override with context-specific functions
            var contextKey = GetContextKey(context);
            if (_contextViews.TryGetValue(contextKey, out var view))
            {
                foreach (var func in view.GetAllFunctions())
                {
                    if (func.Value != DynValue.Nil)
                        functions.Add(func.Key);
                    else
                        functions.Remove(func.Key); // Context explicitly removed this function
                }
            }

            return functions;
        }

        private void AddParentFunctions(LuaExecutionContext context, HashSet<string> functions)
        {
            if (!context.Parent.HasValue)
                return;

            var parentKey = GetContextKey(context.Parent.Value);
            if (_contextViews.TryGetValue(parentKey, out var parentView))
            {
                foreach (var func in parentView.GetAllFunctions())
                {
                    if (func.Value != DynValue.Nil)
                        functions.Add(func.Key);
                    else
                        functions.Remove(func.Key);
                }
            }

            // Recurse to grandparent
            AddParentFunctions(context.Parent.Value, functions);
        }

        private ContextFunctionView CreateContextView(LuaExecutionContext context)
        {
            // Inherit parent's view if available
            if (context.Parent.HasValue)
            {
                var parentKey = GetContextKey(context.Parent.Value);
                if (_contextViews.TryGetValue(parentKey, out var parentView))
                {
                    return new ContextFunctionView(parentView);
                }
            }

            // Create new view based on script functions
            return new ContextFunctionView(_scriptFunctions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetContextKey(LuaExecutionContext context)
        {
            // Use source file + hash of identity as key for efficient lookup
            // This ensures each unique context gets its own view
            return $"{context.SourceFile}#{context.GetHashCode():X8}";
        }
    }

    /// <summary>
    /// Copy-on-write view of functions for a specific execution context.
    /// Efficiently stores only the differences from the parent view.
    /// </summary>
    internal sealed class ContextFunctionView
    {
        private readonly ImmutableDictionary<string, DynValue> _baselineFunctions;
        private ConcurrentDictionary<string, DynValue> _overrides;

        public ContextFunctionView(IReadOnlyDictionary<string, DynValue> baselineFunctions)
        {
            _baselineFunctions = baselineFunctions.ToImmutableDictionary();
        }

        public ContextFunctionView(ContextFunctionView parent)
        {
            _baselineFunctions = parent.GetEffectiveBaseline();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynValue GetFunction(string name)
        {
            // Check overrides first (most recent changes)
            if (_overrides?.TryGetValue(name, out var overrideFunc) == true)
                return overrideFunc;

            // Fall back to baseline
            return _baselineFunctions.TryGetValue(name, out var baselineFunc)
                ? baselineFunc
                : DynValue.Nil;
        }

        public void SetFunction(string name, DynValue function)
        {
            // Lazy-initialize overrides dictionary only when needed
            _overrides ??= new ConcurrentDictionary<string, DynValue>();
            _overrides[name] = function ?? DynValue.Nil;
        }

        public IEnumerable<KeyValuePair<string, DynValue>> GetAllFunctions()
        {
            // Return baseline functions first
            foreach (var kvp in _baselineFunctions)
            {
                if (_overrides?.ContainsKey(kvp.Key) != true)
                    yield return kvp;
            }

            // Then return overrides (which may shadow baseline functions)
            if (_overrides != null)
            {
                foreach (var kvp in _overrides)
                {
                    yield return kvp;
                }
            }
        }

        private ImmutableDictionary<string, DynValue> GetEffectiveBaseline()
        {
            if (_overrides == null || _overrides.IsEmpty)
                return _baselineFunctions;

            // Merge baseline with overrides to create new baseline for child
            var builder = _baselineFunctions.ToBuilder();
            foreach (var kvp in _overrides)
            {
                if (kvp.Value == DynValue.Nil)
                    builder.Remove(kvp.Key);
                else
                    builder[kvp.Key] = kvp.Value;
            }
            return builder.ToImmutable();
        }
    }
}
