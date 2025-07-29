using System;
using System.Text;
using SolarSharp.Commands;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.REPL;

namespace SolarSharp;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        CommandManager.Initialize();

        Script.DefaultOptions.ScriptLoader = new ReplInterpreterScriptLoader();

        Script script = new(CoreModules.Preset_Complete)
        {
            Globals =
            {
                ["makestatic"] = (Func<string, LuaValue>)MakeStatic
            }
        };

        if (CheckArgs(args, new ShellContext(script)))
            return;

        Banner();

        ReplInterpreter interpreter = new(script)
        {
            HandleDynamicExprs = true,
            HandleClassicExprsSyntax = true
        };


        while (true) InterpreterLoop(interpreter, new ShellContext(script));
    }

    private static LuaValue MakeStatic(string type)
    {
        var tt = Type.GetType(type);
        if (tt == null)
            Console.WriteLine("Type '{0}' not found.", type);
        else
            return UserData.CreateStatic(tt);

        return LuaValue.Nil;
    }

    private static void InterpreterLoop(ReplInterpreter interpreter, ShellContext shellContext)
    {
        Console.Write(interpreter.ClassicPrompt + " ");

        var s = Console.ReadLine();

        if (!interpreter.HasPendingCommand && s.StartsWith('!'))
        {
            ExecuteCommand(shellContext, s[1..]);
            return;
        }

        try
        {
            var result = interpreter.Evaluate(s);

            if (result != null && result.Type != DataType.Void)
                Console.WriteLine("{0}", result);
        }
        catch (InterpreterException ex)
        {
            Console.WriteLine("{0}", ex.DecoratedMessage ?? ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("{0}", ex.Message);
        }
    }

    private static void Banner()
    {
        Console.WriteLine(Script.GetBanner("Console"));
        Console.WriteLine();
        Console.WriteLine("Type Lua code to execute it or type !help to see help on commands.\n");
        Console.WriteLine("Welcome.\n");
    }


    private static bool CheckArgs(string[] args, ShellContext shellContext)
    {
        if (args.Length == 0)
            return false;

        if (args.Length == 1 && args[0].Length > 0 && args[0][0] != '-')
        {
            Script script = new();
            script.DoFile(args[0]);
        }

        if (args[0] == "-H" || args[0] == "--help" || args[0] == "/?" || args[0] == "-?")
        {
            ShowCmdLineHelpBig();
        }
        else if (args[0] == "-X")
        {
            if (args.Length == 2)
            {
                ExecuteCommand(shellContext, args[1]);
            }
            else
            {
                Console.WriteLine("Wrong syntax.");
                ShowCmdLineHelp();
            }
        }

        return true;
    }

    private static void ShowCmdLineHelpBig()
    {
        Console.WriteLine(
            "usage: solarsharp [-H | --help | -X \"command\" | -W <dumpfile> <destfile> [--internals] [--vb] [--class:<name>] [--namespace:<name>] | <script>]");
        Console.WriteLine();
        Console.WriteLine("-H : shows this help");
        Console.WriteLine("-X : executes the specified command");
        Console.WriteLine("-W : creates hardwire descriptors");
        Console.WriteLine();
    }

    private static void ShowCmdLineHelp()
    {
        Console.WriteLine(
            "usage: solarsharp [-H | --help | -X \"command\" | -W <dumpfile> <destfile> [--internals] [--vb] | <script>]");
    }

    private static void ExecuteCommand(ShellContext shellContext, string cmdline)
    {
        StringBuilder cmd = new();
        StringBuilder args = new();
        var dest = cmd;

        for (var i = 0; i < cmdline.Length; i++)
        {
            if (dest == cmd && cmdline[i] == ' ')
            {
                dest = args;
                continue;
            }

            dest.Append(cmdline[i]);
        }

        var scmd = cmd.ToString().Trim();
        var sargs = args.ToString().Trim();

        var C = CommandManager.Find(scmd);

        if (C == null)
            Console.WriteLine("Invalid command '{0}'.", scmd);
        else
            C.Execute(shellContext, sargs);
    }
}