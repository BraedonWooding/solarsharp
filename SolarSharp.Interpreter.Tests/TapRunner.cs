using System.IO;
using MoonSharp.Interpreter.Loaders;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests
{
#if !EMBEDTEST
    internal class TestsScriptLoader : ScriptLoaderBase
    {
        public override bool ScriptFileExists(string name)
        {
            return File.Exists(name);
        }

        public override object LoadFile(string file, Table globalContext)
        {
            return new FileStream(file, FileMode.Open, FileAccess.Read);
        }
    }
#endif

    public class TapRunner
    {
        private readonly string m_File;

        /// <summary>
        /// Prints the specified string.
        /// </summary>
        /// <param name="str">The string.</param>
        public void Print(string str)
        {
            // System.Diagnostics.Debug.WriteLine(str);

            Assert.That(str.Trim().StartsWith("not ok"), Is.False, string.Format("TAP fail ({0}) : {1}", m_File, str));
        }

        public TapRunner(string filename)
        {
            m_File = filename;
        }

        public void Run()
        {
            Script S = new();

            S.Options.DebugPrint = Print;

            S.Options.UseLuaErrorLocations = true;

#if PCL
#if EMBEDTEST
			S.Options.ScriptLoader = new EmbeddedResourcesScriptLoader(Assembly.GetExecutingAssembly());
#else
			S.Options.ScriptLoader = new TestsScriptLoader();
#endif
#endif

            S.Globals.Set("arg", DynValue.NewTable(S));

            ((ScriptLoaderBase)S.Options.ScriptLoader).ModulePaths = new string[] { "TestMore/Modules/?", "TestMore/Modules/?.lua" };

            S.DoFile(m_File);
        }

        public static void Run(string filename)
        {
            TapRunner t = new(filename);
            t.Run();
        }



    }
}
