using System;
using System.Collections.Generic;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// A class representing a script coroutine
    /// </summary>
    public class Coroutine : RefIdObject
    {
        /// <summary>
        /// Possible types of coroutine
        /// </summary>
        public enum CoroutineType
        {
            /// <summary>
            /// A valid coroutine
            /// </summary>
            Coroutine,
            /// <summary>
            /// A CLR callback assigned to a coroutine. 
            /// </summary>
            ClrCallback,
            /// <summary>
            /// A CLR callback assigned to a coroutine and already executed.
            /// </summary>
            ClrCallbackDead,
            /// <summary>
            /// A recycled coroutine
            /// </summary>
            Recycled
        }

        /// <summary>
        /// Gets the type of coroutine
        /// </summary>
        public CoroutineType Type { get; private set; }

        private readonly CallbackFunction m_ClrCallback;
        private readonly Processor m_Processor;


        internal Coroutine(CallbackFunction function)
        {
            Type = CoroutineType.ClrCallback;
            m_ClrCallback = function;
            OwnerScript = null;
        }

        internal Coroutine(Processor proc)
        {
            Type = CoroutineType.Coroutine;
            m_Processor = proc;
            m_Processor.AssociatedCoroutine = this;
            OwnerScript = proc.GetScript();
        }

        internal void MarkClrCallbackAsDead()
        {
            if (Type != CoroutineType.ClrCallback)
                throw new InvalidOperationException("State must be CoroutineType.ClrCallback");

            Type = CoroutineType.ClrCallbackDead;
        }

        internal DynValue Recycle(Processor mainProcessor, Closure closure)
        {
            Type = CoroutineType.Recycled;
            return m_Processor.Coroutine_Recycle(mainProcessor, closure);
        }

        /// <summary>
        /// Gets this coroutine as a typed enumerable which can be looped over for resuming.
        /// Returns its result as DynValue(s)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead</exception>
        public IEnumerable<DynValue> AsTypedEnumerable()
        {
            if (Type != CoroutineType.Coroutine)
                throw new InvalidOperationException("Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead");

            while (State == CoroutineState.NotStarted || State == CoroutineState.Suspended)
                yield return Resume();
        }


        /// <summary>
        /// Gets this coroutine as a typed enumerable which can be looped over for resuming.
        /// Returns its result as System.Object. Only the first element of tuples is returned.
        /// Only non-CLR coroutines can be resumed with this method. Use an overload of the Resume method accepting a ScriptExecutionContext instead.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead</exception>
        public IEnumerable<object> AsEnumerable()
        {
            foreach (DynValue v in AsTypedEnumerable())
            {
                yield return v.ToScalar().ToObject();
            }
        }

        /// <summary>
        /// Gets this coroutine as a typed enumerable which can be looped over for resuming.
        /// Returns its result as the specified type. Only the first element of tuples is returned.
        /// Only non-CLR coroutines can be resumed with this method. Use an overload of the Resume method accepting a ScriptExecutionContext instead.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead</exception>
        public IEnumerable<T> AsEnumerable<T>()
        {
            foreach (DynValue v in AsTypedEnumerable())
            {
                yield return v.ToScalar().ToObject<T>();
            }
        }

        /// <summary>
        /// The purpose of this method is to convert a MoonSharp/Lua coroutine to a Unity3D coroutine.
        /// This loops over the coroutine, discarding returned values, and returning null for each invocation.
        /// This means however that the coroutine will be invoked each frame.
        /// Only non-CLR coroutines can be resumed with this method. Use an overload of the Resume method accepting a ScriptExecutionContext instead.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead</exception>
        public System.Collections.IEnumerator AsUnityCoroutine()
        {
#pragma warning disable 0219
            foreach (DynValue v in AsTypedEnumerable())
            {
                yield return null;
            }
#pragma warning restore 0219
        }

        /// <summary>
        /// Resumes the coroutine.
        /// Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead</exception>
        public DynValue Resume(params DynValue[] args)
        {
            if (Type == CoroutineType.Coroutine)
                return m_Processor.Coroutine_Resume(args);
            else
                throw new InvalidOperationException("Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead");
        }


        /// <summary>
        /// Resumes the coroutine.
        /// </summary>
        /// <param name="context">The ScriptExecutionContext.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public DynValue Resume(ScriptExecutionContext context, params DynValue[] args)
        {
            if (Type == CoroutineType.Coroutine)
                return m_Processor.Coroutine_Resume(args);
            else if (Type == CoroutineType.ClrCallback)
            {
                DynValue ret = m_ClrCallback.Invoke(context, args);
                MarkClrCallbackAsDead();
                return ret;
            }
            else
                throw ScriptRuntimeException.CannotResumeNotSuspended(CoroutineState.Dead);
        }

        /// <summary>
        /// Resumes the coroutine.
        /// Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead</exception>
        public DynValue Resume()
        {
            return Resume(new DynValue[0]);
        }


        /// <summary>
        /// Resumes the coroutine.
        /// </summary>
        /// <param name="context">The ScriptExecutionContext.</param>
        /// <returns></returns>
        public DynValue Resume(ScriptExecutionContext context)
        {
            return Resume(context, new DynValue[0]);
        }

        /// <summary>
        /// Resumes the coroutine.
        /// Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead.</exception>
        public DynValue Resume(params object[] args)
        {
            if (Type != CoroutineType.Coroutine)
                throw new InvalidOperationException("Only non-CLR coroutines can be resumed with this overload of the Resume method. Use the overload accepting a ScriptExecutionContext instead");

            DynValue[] dargs = new DynValue[args.Length];

            for (int i = 0; i < dargs.Length; i++)
                dargs[i] = DynValue.FromObject(OwnerScript, args[i]);

            return Resume(dargs);
        }


        /// <summary>
        /// Resumes the coroutine
        /// </summary>
        /// <param name="context">The ScriptExecutionContext.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public DynValue Resume(ScriptExecutionContext context, params object[] args)
        {
            DynValue[] dargs = new DynValue[args.Length];

            for (int i = 0; i < dargs.Length; i++)
                dargs[i] = DynValue.FromObject(context.GetScript(), args[i]);

            return Resume(context, dargs);
        }




        /// <summary>
        /// Gets the coroutine state.
        /// </summary>
        public CoroutineState State
        {
            get
            {
                if (Type == CoroutineType.ClrCallback)
                    return CoroutineState.NotStarted;
                else if (Type == CoroutineType.ClrCallbackDead)
                    return CoroutineState.Dead;
                else
                    return m_Processor.State;
            }
        }

        /// <summary>
        /// Gets the script owning this resource.
        /// </summary>
        /// <value>
        /// The script owning this resource.
        /// </value>
        /// <exception cref="NotImplementedException"></exception>
        public LuaState OwnerScript
        {
            get;
            private set;
        }
    }
}
