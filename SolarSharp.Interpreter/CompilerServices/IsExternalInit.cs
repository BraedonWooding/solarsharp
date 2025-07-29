// This file provides the IsExternalInit type required for C# 9 init-only properties
// when targeting .NET Standard 2.1 or earlier frameworks that don't include it.

#if NETSTANDARD2_1 || NETSTANDARD2_0 || NETSTANDARD1_0 || NETFRAMEWORK
using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit;
}

#endif

#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue) {
            ReturnValue = returnValue;
        }

        public bool ReturnValue { get; }
    }
}
#endif