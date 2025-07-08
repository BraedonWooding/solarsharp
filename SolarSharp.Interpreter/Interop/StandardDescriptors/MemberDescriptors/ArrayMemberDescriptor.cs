using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.Converters;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors
{
    /// <summary>
    /// Member descriptor for indexer of array types
    /// </summary>
    public class ArrayMemberDescriptor : ObjectCallbackMemberDescriptor, IWireableDescriptor
    {
        private readonly bool m_IsSetter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayMemberDescriptor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="isSetter">if set to <c>true</c> is a setter indexer.</param>
        /// <param name="indexerParams">The indexer parameters.</param>
        public ArrayMemberDescriptor(
            string name,
            bool isSetter,
            ParameterDescriptor[] indexerParams
        )
            : base(name, isSetter ? ArrayIndexerSet : ArrayIndexerGet, indexerParams)
        {
            m_IsSetter = isSetter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayMemberDescriptor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="isSetter">if set to <c>true</c> [is setter].</param>
        public ArrayMemberDescriptor(string name, bool isSetter)
            : base(name, isSetter ? ArrayIndexerSet : ArrayIndexerGet)
        {
            m_IsSetter = isSetter;
        }

        /// <summary>
        /// Prepares the descriptor for hard-wiring.
        /// The descriptor fills the passed table with all the needed data for hardwire generators to generate the appropriate code.
        /// </summary>
        /// <param name="t">The table to be filled</param>
        public void PrepareForWiring(Table t)
        {
            t.Set("class", DynValue.NewString(GetType().FullName));
            t.Set("name", DynValue.NewString(Name));
            t.Set("setter", DynValue.NewBoolean(m_IsSetter));

            if (Parameters != null)
            {
                var pars = DynValue.NewPrimeTable();

                t.Set("params", pars);

                var i = 0;

                foreach (var p in Parameters)
                {
                    var pt = DynValue.NewPrimeTable();
                    pars.Table.Set(++i, pt);
                    p.PrepareForWiring(pt.Table);
                }
            }
        }

        private static int[] BuildArrayIndices(CallbackArguments args, int count)
        {
            var indices = new int[count];

            for (var i = 0; i < count; i++)
                indices[i] = args.AsInt(i, "userdata_array_indexer");

            return indices;
        }

        private static object ArrayIndexerSet(
            object arrayObj,
            ScriptExecutionContext ctx,
            CallbackArguments args
        )
        {
            var array = (Array)arrayObj;
            var indices = BuildArrayIndices(args, args.Count - 1);
            var value = args[^1];

            var elemType = array.GetType().GetElementType();

            var objValue = ScriptToClrConversions.DynValueToObjectOfType(
                value,
                elemType,
                null,
                false
            );

            array.SetValue(objValue, indices);

            return DynValue.Void;
        }

        private static object ArrayIndexerGet(
            object arrayObj,
            ScriptExecutionContext ctx,
            CallbackArguments args
        )
        {
            var array = (Array)arrayObj;
            var indices = BuildArrayIndices(args, args.Count);

            return array.GetValue(indices);
        }
    }
}
