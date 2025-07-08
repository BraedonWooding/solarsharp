using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Loaders;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

// Type alias for backward compatibility

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

            Assert.That(str.Trim(), Does.Not.StartWith("not ok"), $"TAP fail ({m_File}) : {str}");
        }

        private TapRunner(string filename)
        {
            m_File = filename;
        }

        private void Run()
        {
            // Special handling for deep recursion test
            var callDepthLimit = m_File.Contains("305-table.t") ? 100000 : 20000;

            var customPolicy = Examples.DesktopSecurityPolicy with
            {
                MaxCallDepth = callDepthLimit, // Higher limit for table permutation test
                AllowedModules = CoreModules.Preset_Complete,
                Capabilities =
                    Examples.DesktopSecurityPolicy.Capabilities
                    | ScriptCapabilities.FileRead
                    | ScriptCapabilities.FileWrite
                    | ScriptCapabilities.FileDelete, // Enable file operations for IO tests
                DefaultFileAccess = FilePermissions.SandboxedReadWrite,
                DefaultDirectoryAccess = DirectoryPermissions.ListAndCreateFiles,
            };

            var policySet = new PolicySetBuilder()
                .DefinePolicy("taprunner", customPolicy)
                .MapFilePattern("*", "taprunner")
                .WithDefaultPolicy("taprunner")
                .Build();

            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation failed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var S = new Script(basePolicySet)
            {
                Options = { DebugPrint = Print, UseLuaErrorLocations = true },
            };

            S.Globals.Set("arg", DynValue.NewTable(S));

            ((ScriptLoaderBase)S.Options.ScriptLoader).ModulePaths = new[]
            {
                "TestMore/Modules/?",
                "TestMore/Modules/?.lua",
            };

            S.DoFile(m_File);
        }

        public static void Run(string filename)
        {
            var t = new TapRunner(filename);
            t.Run();
        }
    }
}
