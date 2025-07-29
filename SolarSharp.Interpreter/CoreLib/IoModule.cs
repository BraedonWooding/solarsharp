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

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing io Lua functions. Proper support requires a compatible IPlatformAccessor
/// </summary>
[SolarSharpModule(Namespace = "io")]
public class IoModule
{
    public static void SolarSharpInit(Table globalTable, Table ioTable)
    {
        UserData.RegisterType<FileUserDataBase>(InteropAccessMode.Default, "file");

        Table meta = new(ioTable.OwnerScript);
        var __index = LuaValue.NewCallback(new CallbackFunction(__index_callback, "__index_callback"));
        meta.Set("__index", __index);
        ioTable.MetaTable = meta;

        SetStandardFile(globalTable.OwnerScript, StandardFileType.StdIn, globalTable.OwnerScript.Options.Stdin);
        SetStandardFile(globalTable.OwnerScript, StandardFileType.StdOut, globalTable.OwnerScript.Options.Stdout);
        SetStandardFile(globalTable.OwnerScript, StandardFileType.StdErr, globalTable.OwnerScript.Options.Stderr);
    }

    private static LuaValue __index_callback(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var name = args[1].CastToString();

        if (name == "stdin")
            return GetStandardFile(executionContext.GetScript(), StandardFileType.StdIn);
        if (name == "stdout")
            return GetStandardFile(executionContext.GetScript(), StandardFileType.StdOut);
        if (name == "stderr")
            return GetStandardFile(executionContext.GetScript(), StandardFileType.StdErr);
        return LuaValue.Nil;
    }

    private static LuaValue GetStandardFile(Script S, StandardFileType file)
    {
        var R = S.Registry;

        var ff = R.Get("853BEAAF298648839E2C99D005E1DF94_STD_" + file);
        return ff;
    }

    private static void SetStandardFile(Script S, StandardFileType file, Stream optionsStream)
    {
        var R = S.Registry;

        optionsStream ??= Script.GlobalOptions.Platform.IO_GetStandardStream(file);

        FileUserDataBase udb = file == StandardFileType.StdIn
            ? StandardIOFileUserDataBase.CreateInputStream(optionsStream)
            : StandardIOFileUserDataBase.CreateOutputStream(optionsStream);
        R.Set("853BEAAF298648839E2C99D005E1DF94_STD_" + file, UserData.Create(udb));
    }

    private static FileUserDataBase GetDefaultFile(ScriptExecutionContext executionContext, StandardFileType file)
    {
        var R = executionContext.GetScript().Registry;

        var ff = R.Get("853BEAAF298648839E2C99D005E1DF94_" + file);

        if (ff.IsNil()) ff = GetStandardFile(executionContext.GetScript(), file);

        return ff.CheckUserDataType<FileUserDataBase>("getdefaultfile(" + file + ")");
    }

    private static void SetDefaultFile(ScriptExecutionContext executionContext, StandardFileType file,
        FileUserDataBase fileHandle)
    {
        SetDefaultFile(executionContext.GetScript(), file, fileHandle);
    }

    internal static void SetDefaultFile(Script script, StandardFileType file, FileUserDataBase fileHandle)
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


    [SolarSharpModuleMethod]
    public static LuaValue close(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var outp = args.AsUserData<FileUserDataBase>(0, "close", true) ??
                   GetDefaultFile(executionContext, StandardFileType.StdOut);
        return outp.close(executionContext, args);
    }

    [SolarSharpModuleMethod]
    public static LuaValue flush(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var outp = args.AsUserData<FileUserDataBase>(0, "close", true) ??
                   GetDefaultFile(executionContext, StandardFileType.StdOut);
        outp.flush();
        return LuaValue.True;
    }


    [SolarSharpModuleMethod]
    public static LuaValue input(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return HandleDefaultStreamSetter(executionContext, args, StandardFileType.StdIn);
    }

    [SolarSharpModuleMethod]
    public static LuaValue output(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return HandleDefaultStreamSetter(executionContext, args, StandardFileType.StdOut);
    }

    private static LuaValue HandleDefaultStreamSetter(ScriptExecutionContext executionContext, CallbackArguments args,
        StandardFileType defaultFiles)
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
            inp = Open(executionContext, fileName, GetUTF8Encoding(),
                defaultFiles == StandardFileType.StdIn ? "r" : "w");
        }
        else
        {
            inp = args.AsUserData<FileUserDataBase>(0, defaultFiles == StandardFileType.StdIn ? "input" : "output");
        }

        SetDefaultFile(executionContext, defaultFiles, inp);

        return UserData.Create(inp);
    }

    private static Encoding GetUTF8Encoding()
    {
        return new UTF8Encoding(false);
    }

    [SolarSharpModuleMethod]
    public static LuaValue lines(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var filename = args.AsType(0, "lines", DataType.String).String;

        try
        {
            List<LuaValue> readLines = new();

            using (var stream =
                   Script.GlobalOptions.Platform.IO_OpenFile(filename, "r"))
            {
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    readLines.Add(LuaValue.NewString(line));
                }
            }

            readLines.Add(LuaValue.Nil);

            return LuaValue.FromObject(executionContext.GetScript(), readLines.Select(s => s));
        }
        catch (Exception ex)
        {
            throw new ScriptRuntimeException(IoExceptionToLuaMessage(ex, filename));
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue open(ScriptExecutionContext executionContext, CallbackArguments args)
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
                        "Can't specify encodings other than nil or 'binary' for binary streams.");

                e = Encoding.GetEncoding(encoding);
            }

            return UserData.Create(Open(executionContext, filename, e, mode));
        }
        catch (Exception ex)
        {
            return LuaValue.NewTuple(LuaValue.Nil,
                LuaValue.NewString(IoExceptionToLuaMessage(ex, filename)));
        }
    }

    public static string IoExceptionToLuaMessage(Exception ex, string filename)
    {
        if (ex is FileNotFoundException)
            return $"{filename}: No such file or directory";
        return ex.Message;
    }

    [SolarSharpModuleMethod]
    public static LuaValue type(ScriptExecutionContext _, CallbackArguments args)
    {
        if (args[0].Type != DataType.UserData)
            return LuaValue.Nil;

        if (!(args[0].UserData.Object is FileUserDataBase file))
            return LuaValue.Nil;
        if (file.isopen())
            return LuaValue.NewString("file");
        return LuaValue.NewString("closed file");
    }

    [SolarSharpModuleMethod]
    public static LuaValue read(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var file = GetDefaultFile(executionContext, StandardFileType.StdIn);
        return file.read(executionContext, args);
    }

    [SolarSharpModuleMethod]
    public static LuaValue write(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var file = GetDefaultFile(executionContext, StandardFileType.StdOut);
        return file.write(executionContext, args);
    }

    [SolarSharpModuleMethod]
    public static LuaValue tmpfile(ScriptExecutionContext executionContext, CallbackArguments _)
    {
        var tmpfilename = Script.GlobalOptions.Platform.IO_OS_GetTempFilename();
        var file = Open(executionContext, tmpfilename, GetUTF8Encoding(), "w");
        return UserData.Create(file);
    }

    private static FileUserDataBase Open(ScriptExecutionContext executionContext, string filename, Encoding encoding,
        string mode)
    {
        return new FileUserData(executionContext.GetScript(), filename, encoding, mode);
    }
}