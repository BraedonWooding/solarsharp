using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.FunctionBinding;

#pragma warning disable IDE0060 // Remove unused parameter

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing system related Lua functions from the 'os' module.
    /// Proper support requires a compatible IPlatformAccessor
    /// </summary>
    [SolarSharpModule(Namespace = "os")]
    public class OsSystemModule
    {
        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.ProcessExecution,
            description: "Execute system commands and processes"
        )]
        public static DynValue execute(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var v = args.AsType(0, "execute", DataType.String, true);

            if (v.IsNil())
            {
                return DynValue.NewBoolean(true);
            }
            try
            {
                var exitCode = executionContext.GetScript().Platform.OS_Execute(v.String);

                return DynValue.NewTuple(
                    DynValue.Nil,
                    DynValue.NewString("exit"),
                    DynValue.NewNumber(exitCode)
                );
            }
            catch (Exception)
            {
                // +++ bad to swallow..
                return DynValue.Nil;
            }
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.ProcessExecution,
            description: "Terminate the script or application with exit code"
        )]
        public static DynValue exit(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var v_exitCode = args.AsType(0, "exit", DataType.Number, true);
            var exitCode = 0;

            if (v_exitCode.IsNotNil())
                exitCode = (int)v_exitCode.Number;

            executionContext.GetScript().Platform.OS_ExitFast(exitCode);

            throw new InvalidOperationException("Unreachable code.. reached.");
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.EnvironmentAccess,
            returnNilOnDenied: true,
            description: "Read environment variable values"
        )]
        public static DynValue getenv(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var varName = args.AsType(0, "getenv", DataType.String);

            var val = executionContext.GetScript().Platform.GetEnvironmentVariable(varName.String);

            if (val == null)
                return DynValue.Nil;
            return DynValue.NewString(val);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.FileDelete,
            description: "Delete files from the filesystem"
        )]
        public static DynValue remove(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var fileName = args.AsType(0, "remove", DataType.String).String;

            try
            {
                if (executionContext.GetScript().Platform.OS_FileExists(fileName))
                {
                    executionContext.GetScript().Platform.OS_FileDelete(fileName);
                    return DynValue.True;
                }
                return DynValue.NewTuple(
                    DynValue.Nil,
                    DynValue.NewString("{0}: No such file or directory.", fileName),
                    DynValue.NewNumber(-1)
                );
            }
            catch (Exception ex)
            {
                return DynValue.NewTuple(
                    DynValue.Nil,
                    DynValue.NewString(ex.Message),
                    DynValue.NewNumber(-1)
                );
            }
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Move or rename files in the filesystem"
        )]
        public static DynValue rename(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var fileNameOld = args.AsType(0, "rename", DataType.String).String;
            var fileNameNew = args.AsType(1, "rename", DataType.String).String;

            try
            {
                if (!executionContext.GetScript().Platform.OS_FileExists(fileNameOld))
                {
                    return DynValue.NewTuple(
                        DynValue.Nil,
                        DynValue.NewString("{0}: No such file or directory.", fileNameOld),
                        DynValue.NewNumber(-1)
                    );
                }

                executionContext.GetScript().Platform.OS_FileMove(fileNameOld, fileNameNew);
                return DynValue.True;
            }
            catch (Exception ex)
            {
                return DynValue.NewTuple(
                    DynValue.Nil,
                    DynValue.NewString(ex.Message),
                    DynValue.NewNumber(-1)
                );
            }
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.SystemInformation,
            returnNilOnDenied: true,
            description: "Set or query locale information (currently returns placeholder)"
        )]
        public static DynValue setlocale(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            // TODO:
            return DynValue.NewString("n/a");
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.OS_System,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Generate temporary filename for file operations"
        )]
        public static DynValue tmpname(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return DynValue.NewString(
                executionContext.GetScript().Platform.IO_OS_GetTempFilename()
            );
        }
    }
}

#pragma warning restore IDE0060 // Remove unused parameter
