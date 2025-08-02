using System;
using System.Collections.Generic;
using System.Text;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Debugging;

/// <summary>
///     Class representing the source code of a given script
/// </summary>
public class SourceCode
{
    internal SourceCode(string name, string code, int sourceID)
    {
        Refs = new List<SourceRef>();

        List<string> lines = new();

        Name = name;
        Code = code;

        lines.Add($"-- Begin of chunk : {name} ");

        lines.AddRange(Code.Split('\n'));

        Lines = lines.ToArray();

        SourceID = sourceID;
    }

    /// <summary>
    ///     Gets the name of the source code
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Gets the source code as a string
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Gets the source code lines.
    /// </summary>
    public string[] Lines { get; }

    /// <summary>
    ///     Gets the source identifier inside a script
    /// </summary>
    public int SourceID { get; private set; }

    internal List<SourceRef> Refs { get; private set; }

    /// <summary>
    ///     Gets the code snippet represented by a source ref
    /// </summary>
    /// <param name="sourceCodeRef">The source code reference.</param>
    /// <returns></returns>
    public string GetCodeSnippet(SourceRef sourceCodeRef)
    {
        if (sourceCodeRef.FromLine == sourceCodeRef.ToLine)
        {
            var from = AdjustStrIndex(Lines[sourceCodeRef.FromLine], sourceCodeRef.FromChar);
            var to = AdjustStrIndex(Lines[sourceCodeRef.FromLine], sourceCodeRef.ToChar);
            return Lines[sourceCodeRef.FromLine][from..to];
        }

        StringBuilder sb = new();

        for (var i = sourceCodeRef.FromLine; i <= sourceCodeRef.ToLine; i++)
            if (i == sourceCodeRef.FromLine)
            {
                var from = AdjustStrIndex(Lines[i], sourceCodeRef.FromChar);
                sb.Append(Lines[i][from..]);
            }
            else if (i == sourceCodeRef.ToLine)
            {
                var to = AdjustStrIndex(Lines[i], sourceCodeRef.ToChar);
                sb.Append(Lines[i][..(to + 1)]);
            }
            else
            {
                sb.Append(Lines[i]);
            }

        return sb.ToString();
    }

    private int AdjustStrIndex(string str, int loc)
    {
        return Math.Max(Math.Min(str.Length, loc), 0);
    }
}