using System;
using System.IO;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using SolarSharp;
using SolarSharp.Commands;

namespace SolarSharp.Commands.Implementations
{
    internal class CompileCommand : ICommand
    {
        public string Name
        {
            get { return "compile"; }
        }

        public void DisplayShortHelp()
        {
            Console.WriteLine("compile <filename> - Compiles the file in a binary format");
        }

        public void DisplayLongHelp()
        {
            Console.WriteLine("compile <filename> - Compiles the file in a binary format.\nThe destination filename will be appended with '-compiled'.");
        }

        public void Execute(ShellContext context, string p)
        {
            string targetFileName = p + "-compiled";

            Script S = new(CoreModules.None);

            DynValue chunk = S.LoadFile(p);

            using Stream stream = new FileStream(targetFileName, FileMode.Create, FileAccess.Write);
            S.Dump(chunk, stream);
        }
    }
}
