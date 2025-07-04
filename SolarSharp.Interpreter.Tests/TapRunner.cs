using System.IO;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Loaders;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests
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
            return new FileStream(file, FileMode.Open, System.IO.FileAccess.Read);
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

            Assert.That(str.Trim(), Does.Not.StartWith("not ok"), string.Format("TAP fail ({0}) : {1}", m_File, str));
        }

        public TapRunner(string filename)
        {
            m_File = filename;
        }

        public void Run()
        {
            var config = new SecurityConfiguration()
                .WithScriptingLimits();  // Use generous limits for test suite
            config.AllowedModules = CoreModules.Preset_Complete;
            config.Capabilities |= ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | ScriptCapabilities.FileDelete; // Enable file operations for IO tests
            config.FileSystem.DefaultFileAccess = SolarSharp.Interpreter.Security.FileAccess.SandboxedReadWrite;
            config.FileSystem.DefaultDirectoryAccess = DirectoryAccess.ListAndCreateFiles;
            Script S = new(config);

            S.Options.DebugPrint = Print;

            S.Options.UseLuaErrorLocations = true;

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
