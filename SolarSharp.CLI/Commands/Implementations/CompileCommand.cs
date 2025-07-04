using System;
using System.IO;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

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

            var config = new SecurityConfiguration();
            config.AllowedModules = CoreModules.None;
            Script S = new(config);

            DynValue chunk = S.LoadFile(p);

            using Stream stream = new FileStream(targetFileName, FileMode.Create, System.IO.FileAccess.Write);
            S.Dump(chunk, stream);
        }
    }
}
