using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Loaders;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Tests
{
#if !EMBEDTEST
    internal class TestsScriptLoader : ScriptLoaderBase
    {
        public override bool ScriptFileExists(string name)
        {
            return File.Exists(name);
        }

        public override object LoadFile(string file)
        {
            return new FileStream(file, FileMode.Open, FileAccess.Read);
        }
    }
#endif

    public class TapRunner
    {
        private readonly string m_File;

        public TapRunner(string filename)
        {
            m_File = filename;
        }

        /// <summary>
        ///     Prints the specified string.
        /// </summary>
        /// <param name="str">The string.</param>
        public void Print(string str)
        {
            TestContext.Out.WriteLine(str);
            Assert.That(str.Trim(), Does.Not.StartWith("not ok"), $"TAP fail ({m_File}) : {str}");
        }

        public void Run()
        {
            Script S = new(CoreModules.Preset_Complete)
            {
                Options =
                {
                    DebugPrint = Print,
                    UseLuaErrorLocations = true
                }
            };

            S.Globals.Set("arg", DynValue.NewTable(S));

            ((ScriptLoaderBase)S.Options.ScriptLoader).ModulePaths =
                new[] { "TestMore/Modules/?", "TestMore/Modules/?.lua" };

            S.DoFile(m_File);
        }

        public static void Run(string filename)
        {
            TapRunner t = new(filename);
            t.Run();
        }
    }
}