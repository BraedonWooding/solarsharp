using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.Attributes;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.ReflectionMemberDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors;

/// <summary>
///     Standard descriptor for userdata types.
/// </summary>
public class StandardUserDataDescriptor : DispatchingUserDataDescriptor
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StandardUserDataDescriptor" /> class.
    /// </summary>
    /// <param name="type">The type this descriptor refers to.</param>
    /// <param name="accessMode">The interop access mode this descriptor uses for members access</param>
    /// <param name="friendlyName">A human readable friendly name of the descriptor.</param>
    public StandardUserDataDescriptor(Type type, InteropAccessMode accessMode, string friendlyName = null)
        : base(type, friendlyName)
    {
        if (accessMode == InteropAccessMode.NoReflectionAllowed)
            throw new ArgumentException(
                "Can't create a StandardUserDataDescriptor under a NoReflectionAllowed access mode");

        if (Script.GlobalOptions.Platform.IsRunningOnAOT())
            accessMode = InteropAccessMode.Reflection;

        if (accessMode == InteropAccessMode.Default)
            accessMode = UserData.DefaultAccessMode;

        AccessMode = accessMode;

        FillMemberList();
    }

    /// <summary>
    ///     Gets the interop access mode this descriptor uses for members access
    /// </summary>
    public InteropAccessMode AccessMode { get; }

    /// <summary>
    ///     Fills the member list.
    /// </summary>
    private void FillMemberList()
    {
        HashSet<string> membersToIgnore = new(
            Framework.Do.GetCustomAttributes(Type, typeof(SolarSharpHideMemberAttribute), true)
                .OfType<SolarSharpHideMemberAttribute>()
                .Select(a => a.MemberName)
        );

        var type = Type;

        if (AccessMode == InteropAccessMode.HideMembers)
            return;

        if (!type.IsDelegateType())
        {
            // add declared constructors
            foreach (var ci in Framework.Do.GetConstructors(type))
            {
                if (membersToIgnore.Contains("__new"))
                    continue;

                AddMember("__new", MethodMemberDescriptor.TryCreateIfVisible(ci, AccessMode));
            }

            // valuetypes don't reflect their empty ctor.. actually empty ctors are a perversion, we don't care and implement ours
            if (Framework.Do.IsValueType(type) && !membersToIgnore.Contains("__new"))
                AddMember("__new", new ValueTypeDefaultCtorMemberDescriptor(type));
        }


        // add methods to method list and metamethods
        foreach (var mi in Framework.Do.GetMethods(type))
        {
            if (membersToIgnore.Contains(mi.Name)) continue;

            var md = MethodMemberDescriptor.TryCreateIfVisible(mi, AccessMode);

            if (md != null)
            {
                if (!MethodMemberDescriptor.CheckMethodIsCompatible(mi, false))
                    continue;

                // transform explicit/implicit conversions to a friendlier name.
                var name = mi.Name;
                if (mi.IsSpecialName && (mi.Name == SPECIALNAME_CAST_EXPLICIT || mi.Name == SPECIALNAME_CAST_IMPLICIT))
                    name = mi.ReturnType.GetConversionMethodName();

                AddMember(name, md);

                foreach (var metaname in mi.GetMetaNamesFromAttributes()) AddMetaMember(metaname, md);
            }
        }

        // get properties
        foreach (var pi in Framework.Do.GetProperties(type))
        {
            if (pi.IsSpecialName || pi.GetIndexParameters().Any() || membersToIgnore.Contains(pi.Name))
                continue;

            AddMember(pi.Name, PropertyMemberDescriptor.TryCreateIfVisible(pi, AccessMode));
        }

        // get fields
        foreach (var fi in Framework.Do.GetFields(type))
        {
            if (fi.IsSpecialName || membersToIgnore.Contains(fi.Name))
                continue;

            AddMember(fi.Name, FieldMemberDescriptor.TryCreateIfVisible(fi, AccessMode));
        }

        // get events
        foreach (var ei in Framework.Do.GetEvents(type))
        {
            if (ei.IsSpecialName || membersToIgnore.Contains(ei.Name))
                continue;

            AddMember(ei.Name, EventMemberDescriptor.TryCreateIfVisible(ei, AccessMode));
        }

        // get nested types and create statics
        foreach (var nestedType in Framework.Do.GetNestedTypes(type))
        {
            if (membersToIgnore.Contains(nestedType.Name))
                continue;

            if (!Framework.Do.IsGenericTypeDefinition(nestedType))
                if (Framework.Do.IsNestedPublic(nestedType) || Framework.Do
                        .GetCustomAttributes(nestedType, typeof(SolarSharpUserDataAttribute), true).Length > 0)
                {
                    var descr = UserData.RegisterType(nestedType, AccessMode);

                    if (descr != null)
                        AddLuaValue(nestedType.Name, UserData.CreateStatic(nestedType));
                }
        }

        if (!membersToIgnore.Contains("[this]"))
        {
            if (Type.IsArray)
            {
                var rank = Type.GetArrayRank();

                var get_pars = new ParameterDescriptor[rank];
                var set_pars = new ParameterDescriptor[rank + 1];

                for (var i = 0; i < rank; i++)
                    get_pars[i] = set_pars[i] = new ParameterDescriptor("idx" + i, typeof(int));

                set_pars[rank] = new ParameterDescriptor("value", Type.GetElementType());

                AddMember(SPECIALNAME_INDEXER_SET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_SET, true, set_pars));
                AddMember(SPECIALNAME_INDEXER_GET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_GET, false, get_pars));
            }
            else if (Type == typeof(Array))
            {
                AddMember(SPECIALNAME_INDEXER_SET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_SET, true));
                AddMember(SPECIALNAME_INDEXER_GET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_GET, false));
            }
        }
    }
}