using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.CoreLib.IO;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.FunctionBinding;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing io Lua functions. Proper support requires a compatible IPlatformAccessor
    /// </summary>
    [SolarSharpModule(Namespace = "io")]
    public class IoModule
    {
        public static void MoonSharpInit(Script script, Table globalTable, Table ioTable)
        {
            UserData.RegisterType<FileUserDataBase>(InteropAccessMode.Default, "file");

            var meta = new Table();
            var __index = DynValue.NewCallback(
                new CallbackFunction(__index_callback, "__index_callback")
            );
            meta.Set("__index", __index);
            ioTable.MetaTable = meta;

            SetStandardFile(script, StandardFileType.StdIn, script.Options.Stdin);
            SetStandardFile(script, StandardFileType.StdOut, script.Options.Stdout);
            SetStandardFile(script, StandardFileType.StdErr, script.Options.Stderr);
        }

        private static DynValue __index_callback(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var name = args[1].CastToString();

            if (name == "stdin")
                return GetStandardFile(executionContext.GetScript(), StandardFileType.StdIn);
            if (name == "stdout")
                return GetStandardFile(executionContext.GetScript(), StandardFileType.StdOut);
            if (name == "stderr")
                return GetStandardFile(executionContext.GetScript(), StandardFileType.StdErr);
            return DynValue.Nil;
        }

        private static DynValue GetStandardFile(Script S, StandardFileType file)
        {
            var R = S.Registry;

            var ff = R.Get("853BEAAF298648839E2C99D005E1DF94_STD_" + file);
            return ff;
        }

        private static void SetStandardFile(Script S, StandardFileType file, Stream optionsStream)
        {
            var R = S.Registry;

            optionsStream ??= S.Platform.IO_GetStandardStream(file);

            var udb =
                file == StandardFileType.StdIn
                    ? StandardIOFileUserDataBase.CreateInputStream(optionsStream)
                    : (FileUserDataBase)
                        StandardIOFileUserDataBase.CreateOutputStream(optionsStream);
            R.Set("853BEAAF298648839E2C99D005E1DF94_STD_" + file, UserData.Create(udb));
        }

        private static FileUserDataBase GetDefaultFile(
            ScriptExecutionContext executionContext,
            StandardFileType file
        )
        {
            var R = executionContext.GetScript().Registry;

            var ff = R.Get("853BEAAF298648839E2C99D005E1DF94_" + file);

            if (ff.IsNil())
            {
                ff = GetStandardFile(executionContext.GetScript(), file);
            }

            return ff.CheckUserDataType<FileUserDataBase>("getdefaultfile(" + file + ")");
        }

        private static void SetDefaultFile(
            ScriptExecutionContext executionContext,
            StandardFileType file,
            FileUserDataBase fileHandle
        )
        {
            SetDefaultFile(executionContext.GetScript(), file, fileHandle);
        }

        internal static void SetDefaultFile(
            Script script,
            StandardFileType file,
            FileUserDataBase fileHandle
        )
        {
            var R = script.Registry;
            R.Set("853BEAAF298648839E2C99D005E1DF94_" + file, UserData.Create(fileHandle));
        }

        public static void SetDefaultFile(Script script, StandardFileType file, Stream stream)
        {
            if (file == StandardFileType.StdIn)
                SetDefaultFile(script, file, StandardIOFileUserDataBase.CreateInputStream(stream));
            else
                SetDefaultFile(script, file, StandardIOFileUserDataBase.CreateOutputStream(stream));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Close a file handle and flush any pending writes",
            returnNilOnDenied: true
        )]
        public static DynValue close(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var outp =
                args.AsUserData<FileUserDataBase>(0, "close", true)
                ?? GetDefaultFile(executionContext, StandardFileType.StdOut);
            return outp.close(executionContext, args);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Flush any pending writes to output stream or file",
            returnNilOnDenied: true
        )]
        public static DynValue flush(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var outp =
                args.AsUserData<FileUserDataBase>(0, "close", true)
                ?? GetDefaultFile(executionContext, StandardFileType.StdOut);
            outp.flush();
            return DynValue.True;
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileRead,
            description: "Get or set the default input file stream",
            returnNilOnDenied: true
        )]
        public static DynValue input(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return HandleDefaultStreamSetter(executionContext, args, StandardFileType.StdIn);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Get or set the default output file stream",
            returnNilOnDenied: true
        )]
        public static DynValue output(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return HandleDefaultStreamSetter(executionContext, args, StandardFileType.StdOut);
        }

        private static DynValue HandleDefaultStreamSetter(
            ScriptExecutionContext executionContext,
            CallbackArguments args,
            StandardFileType defaultFiles
        )
        {
            if (args.Count == 0 || args[0].IsNil())
            {
                var file = GetDefaultFile(executionContext, defaultFiles);
                return UserData.Create(file);
            }

            FileUserDataBase inp;
            if (args[0].Type == DataType.String || args[0].Type == DataType.Number)
            {
                var fileName = args[0].CastToString();
                inp = Open(
                    executionContext,
                    fileName,
                    GetUTF8Encoding(),
                    defaultFiles == StandardFileType.StdIn ? "r" : "w"
                );
            }
            else
            {
                inp = args.AsUserData<FileUserDataBase>(
                    0,
                    defaultFiles == StandardFileType.StdIn ? "input" : "output"
                );
            }

            SetDefaultFile(executionContext, defaultFiles, inp);

            return UserData.Create(inp);
        }

        private static Encoding GetUTF8Encoding()
        {
            return new UTF8Encoding(false);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileRead,
            description: "Read all lines from a file and return as iterator",
            returnNilOnDenied: true
        )]
        public static DynValue lines(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var filename = args.AsType(0, "lines", DataType.String).String;

            try
            {
                var readLines = new List<DynValue>();

                using (
                    var stream = executionContext
                        .GetScript()
                        .Platform.IO_OpenFile(executionContext.GetScript(), filename, null, "r")
                )
                {
                    using var reader = new StreamReader(stream);
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        readLines.Add(DynValue.NewString(line));
                    }
                }

                readLines.Add(DynValue.Nil);

                return DynValue.FromObject(executionContext.GetScript(), readLines.Select(s => s));
            }
            catch (Exception ex)
            {
                throw new ScriptRuntimeException(IoExceptionToLuaMessage(ex, filename));
            }
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
            description: "Open file for reading or writing",
            returnNilOnDenied: true
        )]
        public static DynValue open(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var filename = args.AsType(0, "open", DataType.String).String;
            var vmode = args.AsType(1, "open", DataType.String, true);
            var vencoding = args.AsType(2, "open", DataType.String, true);

            var mode = vmode.IsNil() ? "r" : vmode.String;

            var invalidChars = mode.Replace("+", "")
                .Replace("r", "")
                .Replace("a", "")
                .Replace("w", "")
                .Replace("b", "")
                .Replace("t", "");

            if (invalidChars.Length > 0)
                throw ScriptRuntimeException.BadArgument(1, "open", "invalid mode");

            try
            {
                var encoding = vencoding.IsNil() ? null : vencoding.String;

                // list of codes: http://msdn.microsoft.com/en-us/library/vstudio/system.text.encoding%28v=vs.90%29.aspx.
                // In addition, "binary" is available.
                Encoding e = null;
                var isBinary = Framework.Do.StringContainsChar(mode, 'b');

                if (encoding == "binary")
                {
                    isBinary = true;
                    e = new BinaryEncoding();
                }
                else if (encoding == null)
                {
                    e = !isBinary ? GetUTF8Encoding() : new BinaryEncoding();
                }
                else
                {
                    if (isBinary)
                        throw new ScriptRuntimeException(
                            "Can't specify encodings other than nil or 'binary' for binary streams."
                        );

                    e = Encoding.GetEncoding(encoding);
                }

                return UserData.Create(Open(executionContext, filename, e, mode));
            }
            catch (CriticalSecurityException)
            {
                // Critical security exceptions always throw
                throw;
            }
            catch (NonCriticalSecurityException ex)
            {
                // Non-critical security exceptions may throw based on configuration
                var script = executionContext.GetScript();
                if (script.SecurityPolicy().ThrowOnNonCriticalViolations)
                {
                    throw;
                }

                // Return nil with error message for graceful handling
                return DynValue.NewTuple(DynValue.Nil, DynValue.NewString(ex.Message));
            }
            catch (Exception ex)
            {
                return DynValue.NewTuple(
                    DynValue.Nil,
                    DynValue.NewString(IoExceptionToLuaMessage(ex, filename))
                );
            }
        }

        public static string IoExceptionToLuaMessage(Exception ex, string filename)
        {
            if (ex is FileNotFoundException)
                return $"{filename}: No such file or directory";
            return ex.Message;
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Check if value is a file handle and return its type or status",
            returnNilOnDenied: true
        )]
        public static DynValue type(ScriptExecutionContext _, CallbackArguments args)
        {
            if (args[0].Type != DataType.UserData)
                return DynValue.Nil;

            if (!(args[0].UserData.Object is FileUserDataBase file))
                return DynValue.Nil;
            if (file.isopen())
                return DynValue.NewString("file");
            return DynValue.NewString("closed file");
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileRead,
            description: "Read data from the default input file stream",
            returnNilOnDenied: true
        )]
        public static DynValue read(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var file = GetDefaultFile(executionContext, StandardFileType.StdIn);
            return file.read(executionContext, args);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Write data to the default output file stream",
            returnNilOnDenied: true
        )]
        public static DynValue write(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var file = GetDefaultFile(executionContext, StandardFileType.StdOut);
            return file.write(executionContext, args);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.IO,
            requiredCapabilities: ScriptCapabilities.FileWrite,
            description: "Create a temporary file for reading and writing",
            returnNilOnDenied: true
        )]
        public static DynValue tmpfile(ScriptExecutionContext executionContext, CallbackArguments _)
        {
            var tmpfilename = executionContext.GetScript().Platform.IO_OS_GetTempFilename();
            var file = Open(executionContext, tmpfilename, GetUTF8Encoding(), "w");
            return UserData.Create(file);
        }

        private static FileUserDataBase Open(
            ScriptExecutionContext executionContext,
            string filename,
            Encoding encoding,
            string mode
        )
        {
            return new FileUserData(executionContext.GetScript(), filename, encoding, mode);
        }
    }
}
