using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.Converters;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.ReflectionMemberDescriptors
{
    /// <summary>
    /// Class providing easier marshalling of CLR fields
    /// </summary>
    public class FieldMemberDescriptor : IMemberDescriptor, IOptimizableDescriptor, IWireableDescriptor
    {
        /// <summary>
        /// Gets the FieldInfo got by reflection
        /// </summary>
        public FieldInfo FieldInfo { get; private set; }
        /// <summary>
        /// Gets the <see cref="InteropAccessMode" />
        /// </summary>
        public InteropAccessMode AccessMode { get; private set; }
        /// <summary>
        /// Gets a value indicating whether the described property is static.
        /// </summary>
        public bool IsStatic { get; private set; }
        /// <summary>
        /// Gets the name of the property
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets a value indicating whether this instance is a constant 
        /// </summary>
        public bool IsConst { get; private set; }
        /// <summary>
        /// Gets a value indicating whether this instance is readonly 
        /// </summary>
        public bool IsReadonly { get; private set; }

        private readonly object m_ConstValue = null;
        private Func<object, object> m_OptimizedGetter = null;


        /// <summary>
        /// Tries to create a new StandardUserDataFieldDescriptor, returning <c>null</c> in case the field is not 
        /// visible to script code.
        /// </summary>
        /// <param name="fi">The FieldInfo.</param>
        /// <param name="accessMode">The <see cref="InteropAccessMode" /></param>
        /// <returns>A new StandardUserDataFieldDescriptor or null.</returns>
        public static FieldMemberDescriptor TryCreateIfVisible(FieldInfo fi, InteropAccessMode accessMode)
        {
            if (fi.GetVisibilityFromAttributes() ?? fi.IsPublic)
                return new FieldMemberDescriptor(fi, accessMode);

            return null;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMemberDescriptor"/> class.
        /// </summary>
        /// <param name="fi">The FieldInfo.</param>
        /// <param name="accessMode">The <see cref="InteropAccessMode" /> </param>
        public FieldMemberDescriptor(FieldInfo fi, InteropAccessMode accessMode)
        {
            if (LuaState.GlobalOptions.Platform.IsRunningOnAOT())
                accessMode = InteropAccessMode.Reflection;

            FieldInfo = fi;
            AccessMode = accessMode;
            Name = fi.Name;
            IsStatic = FieldInfo.IsStatic;

            if (FieldInfo.IsLiteral)
            {
                IsConst = true;
                m_ConstValue = FieldInfo.GetValue(null);
            }
            else
            {
                IsReadonly = FieldInfo.IsInitOnly;
            }

            if (AccessMode == InteropAccessMode.Preoptimized)
            {
                OptimizeGetter();
            }
        }


        /// <summary>
        /// Gets the value of the property
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        public DynValue GetValue(LuaState script, object obj)
        {
            this.CheckAccess(MemberDescriptorAccess.CanRead, obj);

            // optimization+workaround of Unity bug.. 
            if (IsConst)
                return ClrToScriptConversions.ObjectToDynValue(script, m_ConstValue);

            if (AccessMode == InteropAccessMode.LazyOptimized && m_OptimizedGetter == null)
                OptimizeGetter();

            object result = m_OptimizedGetter != null ? m_OptimizedGetter(obj) : FieldInfo.GetValue(obj);
            return ClrToScriptConversions.ObjectToDynValue(script, result);
        }

        internal void OptimizeGetter()
        {
            if (IsConst)
                return;

            using (PerformanceStatistics.StartGlobalStopwatch(PerformanceCounter.AdaptersCompilation))
            {
                if (IsStatic)
                {
                    var paramExp = Expression.Parameter(typeof(object), "dummy");
                    var propAccess = Expression.Field(null, FieldInfo);
                    var castPropAccess = Expression.Convert(propAccess, typeof(object));
                    var lambda = Expression.Lambda<Func<object, object>>(castPropAccess, paramExp);
                    Interlocked.Exchange(ref m_OptimizedGetter, lambda.Compile());
                }
                else
                {
                    var paramExp = Expression.Parameter(typeof(object), "obj");
                    var castParamExp = Expression.Convert(paramExp, FieldInfo.DeclaringType);
                    var propAccess = Expression.Field(castParamExp, FieldInfo);
                    var castPropAccess = Expression.Convert(propAccess, typeof(object));
                    var lambda = Expression.Lambda<Func<object, object>>(castPropAccess, paramExp);
                    Interlocked.Exchange(ref m_OptimizedGetter, lambda.Compile());
                }
            }
        }

        /// <summary>
        /// Sets the value of the property
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="obj">The object.</param>
        /// <param name="v">The value to set.</param>
        public void SetValue(LuaState script, object obj, DynValue v)
        {
            this.CheckAccess(MemberDescriptorAccess.CanWrite, obj);

            if (IsReadonly || IsConst)
                throw new ErrorException("userdata field '{0}.{1}' cannot be written to.", FieldInfo.DeclaringType.Name, Name);

            object value = ScriptToClrConversions.DynValueToObjectOfType(v, FieldInfo.FieldType, null, false);

            try
            {
                if (value is double)
                    value = NumericConversions.DoubleToType(FieldInfo.FieldType, (double)value);

                FieldInfo.SetValue(IsStatic ? null : obj, value);
            }
            catch (ArgumentException)
            {
                // non-optimized setters fall here
                throw ErrorException.UserDataArgumentTypeMismatch(v.Type, FieldInfo.FieldType);
            }
            catch (InvalidCastException)
            {
                // optimized setters fall here
                throw ErrorException.UserDataArgumentTypeMismatch(v.Type, FieldInfo.FieldType);
            }
#if !(PCL || ENABLE_DOTNET || NETFX_CORE)
            catch (FieldAccessException ex)
            {
                throw new ErrorException(ex);
            }
#endif
        }


        /// <summary>
        /// Gets the types of access supported by this member
        /// </summary>
        public MemberDescriptorAccess MemberAccess
        {
            get
            {
                if (IsReadonly || IsConst)
                    return MemberDescriptorAccess.CanRead;
                else
                    return MemberDescriptorAccess.CanRead | MemberDescriptorAccess.CanWrite;
            }
        }

        void IOptimizableDescriptor.Optimize()
        {
            if (m_OptimizedGetter == null)
                OptimizeGetter();
        }

        /// <summary>
        /// Prepares the descriptor for hard-wiring.
        /// The descriptor fills the passed table with all the needed data for hardwire generators to generate the appropriate code.
        /// </summary>
        /// <param name="t">The table to be filled</param>
        public void PrepareForWiring(Table t)
        {
            t.Set("class", DynValue.NewString(GetType().FullName));
            t.Set("visibility", DynValue.NewString(FieldInfo.GetClrVisibility()));

            t.Set("name", DynValue.NewString(Name));
            t.Set("static", DynValue.NewBoolean(IsStatic));
            t.Set("const", DynValue.NewBoolean(IsConst));
            t.Set("readonly", DynValue.NewBoolean(IsReadonly));
            t.Set("decltype", DynValue.NewString(FieldInfo.DeclaringType.FullName));
            t.Set("declvtype", DynValue.NewBoolean(Framework.Do.IsValueType(FieldInfo.DeclaringType)));
            t.Set("type", DynValue.NewString(FieldInfo.FieldType.FullName));
            t.Set("read", DynValue.NewBoolean(true));
            t.Set("write", DynValue.NewBoolean(!(IsConst || IsReadonly)));
        }
    }
}
