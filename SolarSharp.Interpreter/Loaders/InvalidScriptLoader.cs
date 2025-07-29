using System;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Loaders;

/// <summary>
///     A script loader used for platforms we cannot initialize in any better way..
/// </summary>
internal class InvalidScriptLoader : IScriptLoader
{
    private readonly string m_Error;

    internal InvalidScriptLoader(string frameworkname)
    {
        m_Error = $"""
                   Loading scripts from files is not automatically supported on {frameworkname}. 
                   Please implement your own IScriptLoader (possibly, extending ScriptLoaderBase for easier implementation),
                   use a preexisting loader like EmbeddedResourcesScriptLoader or UnityAssetsScriptLoader or load scripts from strings.
                   """;
    }

    public object LoadFile(string file)
    {
        throw new PlatformNotSupportedException(m_Error);
    }

    public string ResolveFileName(string filename)
    {
        return filename;
    }

    public string ResolveModuleName(string modname, Table globalContext)
    {
        throw new PlatformNotSupportedException(m_Error);
    }
}