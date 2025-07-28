using System;
using System.IO;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Commands.Implementations;

internal class CompileCommand : ICommand
{
    public string Name => "compile";

    public void DisplayShortHelp()
    {
        Console.WriteLine("compile <filename> - Compiles the file in a binary format");
    }

    public void DisplayLongHelp()
    {
        Console.WriteLine(
            "compile <filename> - Compiles the file in a binary format.\nThe destination filename will be appended with '-compiled'.");
    }

    public void Execute(ShellContext context, string p)
    {
        var targetFileName = p + "-compiled";

        Script S = new(CoreModules.None);

        var chunk = S.LoadFile(p);

        using Stream stream = new FileStream(targetFileName, FileMode.Create, FileAccess.Write);
        S.Dump(chunk, stream);
    }
}