﻿using System;
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
    /// Class providing easier marshalling of CLR properties
    /// </summary>
    public class PropertyMemberDescriptor : IMemberDescriptor, IOptimizableDescriptor,
        IWireableDescriptor
    {
        /// <summary>
        /// Gets the PropertyInfo got by reflection
        /// </summary>
        public PropertyInfo PropertyInfo { get; private set; }
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
        /// Gets a value indicating whether this instance can be read from
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can be read from; otherwise, <c>false</c>.
        /// </value>
        public bool CanRead { get { return m_Getter != null; } }
        /// <summary>
        /// Gets a value indicating whether this instance can be written to.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can be written to; otherwise, <c>false</c>.
        /// </value>
        public bool CanWrite { get { return m_Setter != null; } }


        private readonly MethodInfo m_Getter, m_Setter;
        private Func<object, object> m_OptimizedGetter = null;
        private Action<object, object> m_OptimizedSetter = null;


        /// <summary>
        /// Tries to create a new StandardUserDataPropertyDescriptor, returning <c>null</c> in case the property is not 
        /// visible to script code.
        /// </summary>
        /// <param name="pi">The PropertyInfo.</param>
        /// <param name="accessMode">The <see cref="InteropAccessMode" /></param>
        /// <returns>A new StandardUserDataPropertyDescriptor or null.</returns>
        public static PropertyMemberDescriptor TryCreateIfVisible(PropertyInfo pi, InteropAccessMode accessMode)
        {
            MethodInfo getter = Framework.Do.GetGetMethod(pi);
            MethodInfo setter = Framework.Do.GetSetMethod(pi);

            bool? pvisible = pi.GetVisibilityFromAttributes();
            bool? gvisible = getter.GetVisibilityFromAttributes();
            bool? svisible = setter.GetVisibilityFromAttributes();

            if (pvisible.HasValue)
            {
                return TryCreate(pi, accessMode,
                    gvisible ?? pvisible.Value ? getter : null,
                    svisible ?? pvisible.Value ? setter : null);
            }
            else
            {
                return TryCreate(pi, accessMode,
                    gvisible ?? getter.IsPublic ? getter : null,
                    svisible ?? setter.IsPublic ? setter : null);
            }
        }

        private static PropertyMemberDescriptor TryCreate(PropertyInfo pi, InteropAccessMode accessMode, MethodInfo getter, MethodInfo setter)
        {
            if (getter == null && setter == null)
                return null;
            else
                return new PropertyMemberDescriptor(pi, accessMode, getter, setter);
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMemberDescriptor"/> class.
        /// NOTE: This constructor gives get/set visibility based exclusively on the CLR visibility of the 
        /// getter and setter methods.
        /// </summary>
        /// <param name="pi">The pi.</param>
        /// <param name="accessMode">The access mode.</param>
        public PropertyMemberDescriptor(PropertyInfo pi, InteropAccessMode accessMode)
            : this(pi, accessMode, Framework.Do.GetGetMethod(pi), Framework.Do.GetSetMethod(pi))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMemberDescriptor" /> class.
        /// </summary>
        /// <param name="pi">The PropertyInfo.</param>
        /// <param name="accessMode">The <see cref="InteropAccessMode" /></param>
        /// <param name="getter">The getter method. Use null to make the property writeonly.</param>
        /// <param name="setter">The setter method. Use null to make the property readonly.</param>
        public PropertyMemberDescriptor(PropertyInfo pi, InteropAccessMode accessMode, MethodInfo getter, MethodInfo setter)
        {
            if (getter == null && setter == null)
                throw new ArgumentNullException("getter and setter cannot both be null");

            if (LuaState.GlobalOptions.Platform.IsRunningOnAOT())
                accessMode = InteropAccessMode.Reflection;

            PropertyInfo = pi;
            AccessMode = accessMode;
            Name = pi.Name;

            m_Getter = getter;
            m_Setter = setter;

            IsStatic = (m_Getter ?? m_Setter).IsStatic;

            if (AccessMode == InteropAccessMode.Preoptimized)
            {
                OptimizeGetter();
                OptimizeSetter();
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

            if (m_Getter == null)
                throw new ScriptRuntimeException("userdata property '{0}.{1}' cannot be read from.", PropertyInfo.DeclaringType.Name, Name);

            if (AccessMode == InteropAccessMode.LazyOptimized && m_OptimizedGetter == null)
                OptimizeGetter();

            object result = null;

            if (m_OptimizedGetter != null)
                result = m_OptimizedGetter(obj);
            else
                result = m_Getter.Invoke(IsStatic ? null : obj, null); // convoluted workaround for --full-aot Mono execution

            return ClrToScriptConversions.ObjectToDynValue(script, result);
        }

        internal void OptimizeGetter()
        {
            using (PerformanceStatistics.StartGlobalStopwatch(PerformanceCounter.AdaptersCompilation))
            {
                if (m_Getter != null)
                {
                    if (IsStatic)
                    {
                        var paramExp = Expression.Parameter(typeof(object), "dummy");
                        var propAccess = Expression.Property(null, PropertyInfo);
                        var castPropAccess = Expression.Convert(propAccess, typeof(object));
                        var lambda = Expression.Lambda<Func<object, object>>(castPropAccess, paramExp);
                        Interlocked.Exchange(ref m_OptimizedGetter, lambda.Compile());
                    }
                    else
                    {
                        var paramExp = Expression.Parameter(typeof(object), "obj");
                        var castParamExp = Expression.Convert(paramExp, PropertyInfo.DeclaringType);
                        var propAccess = Expression.Property(castParamExp, PropertyInfo);
                        var castPropAccess = Expression.Convert(propAccess, typeof(object));
                        var lambda = Expression.Lambda<Func<object, object>>(castPropAccess, paramExp);
                        Interlocked.Exchange(ref m_OptimizedGetter, lambda.Compile());
                    }
                }
            }
        }

        internal void OptimizeSetter()
        {
            using (PerformanceStatistics.StartGlobalStopwatch(PerformanceCounter.AdaptersCompilation))
            {
                if (m_Setter != null && !Framework.Do.IsValueType(PropertyInfo.DeclaringType))
                {
                    MethodInfo setterMethod = Framework.Do.GetSetMethod(PropertyInfo);

                    if (IsStatic)
                    {
                        var paramExp = Expression.Parameter(typeof(object), "dummy");
                        var paramValExp = Expression.Parameter(typeof(object), "val");
                        var castParamValExp = Expression.Convert(paramValExp, PropertyInfo.PropertyType);
                        var callExpression = Expression.Call(setterMethod, castParamValExp);
                        var lambda = Expression.Lambda<Action<object, object>>(callExpression, paramExp, paramValExp);
                        Interlocked.Exchange(ref m_OptimizedSetter, lambda.Compile());
                    }
                    else
                    {
                        var paramExp = Expression.Parameter(typeof(object), "obj");
                        var paramValExp = Expression.Parameter(typeof(object), "val");
                        var castParamExp = Expression.Convert(paramExp, PropertyInfo.DeclaringType);
                        var castParamValExp = Expression.Convert(paramValExp, PropertyInfo.PropertyType);
                        var callExpression = Expression.Call(castParamExp, setterMethod, castParamValExp);
                        var lambda = Expression.Lambda<Action<object, object>>(callExpression, paramExp, paramValExp);
                        Interlocked.Exchange(ref m_OptimizedSetter, lambda.Compile());
                    }
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

            if (m_Setter == null)
                throw new ScriptRuntimeException("userdata property '{0}.{1}' cannot be written to.", PropertyInfo.DeclaringType.Name, Name);

            object value = ScriptToClrConversions.DynValueToObjectOfType(v, PropertyInfo.PropertyType, null, false);

            try
            {
                if (value is double)
                    value = NumericConversions.DoubleToType(PropertyInfo.PropertyType, (double)value);

                if (AccessMode == InteropAccessMode.LazyOptimized && m_OptimizedSetter == null)
                    OptimizeSetter();

                if (m_OptimizedSetter != null)
                {
                    m_OptimizedSetter(obj, value);
                }
                else
                {
                    m_Setter.Invoke(IsStatic ? null : obj, new object[] { value }); // convoluted workaround for --full-aot Mono execution
                }
            }
            catch (ArgumentException)
            {
                // non-optimized setters fall here
                throw ScriptRuntimeException.UserDataArgumentTypeMismatch(v.Type, PropertyInfo.PropertyType);
            }
            catch (InvalidCastException)
            {
                // optimized setters fall here
                throw ScriptRuntimeException.UserDataArgumentTypeMismatch(v.Type, PropertyInfo.PropertyType);
            }
        }


        /// <summary>
        /// Gets the types of access supported by this member
        /// </summary>
        public MemberDescriptorAccess MemberAccess
        {
            get
            {
                MemberDescriptorAccess access = 0;

                if (m_Setter != null) access |= MemberDescriptorAccess.CanWrite;
                if (m_Getter != null) access |= MemberDescriptorAccess.CanRead;

                return access;
            }
        }

        /// <summary>
        /// Called by standard descriptors when background optimization or preoptimization needs to be performed.
        /// </summary>
        void IOptimizableDescriptor.Optimize()
        {
            OptimizeGetter();
            OptimizeSetter();
        }

        /// <summary>
        /// Prepares the descriptor for hard-wiring.
        /// The descriptor fills the passed table with all the needed data for hardwire generators to generate the appropriate code.
        /// </summary>
        /// <param name="t">The table to be filled</param>
        public void PrepareForWiring(Table t)
        {
            t.Set("class", DynValue.NewString(GetType().FullName));
            t.Set("visibility", DynValue.NewString(PropertyInfo.GetClrVisibility()));
            t.Set("name", DynValue.NewString(Name));
            t.Set("static", DynValue.NewBoolean(IsStatic));
            t.Set("read", DynValue.NewBoolean(CanRead));
            t.Set("write", DynValue.NewBoolean(CanWrite));
            t.Set("decltype", DynValue.NewString(PropertyInfo.DeclaringType.FullName));
            t.Set("declvtype", DynValue.NewBoolean(Framework.Do.IsValueType(PropertyInfo.DeclaringType)));
            t.Set("type", DynValue.NewString(PropertyInfo.PropertyType.FullName));
        }
    }
}
