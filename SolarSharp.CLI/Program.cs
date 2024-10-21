﻿using System;
using System.Text;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.REPL;
using SolarSharp.Commands;
using SolarSharp.Commands.Implementations;

namespace SolarSharp
{
    internal class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            CommandManager.Initialize();

            LuaState.DefaultOptions.ScriptLoader = new ReplInterpreterScriptLoader();

            LuaState script = new(CoreModules.Preset_Complete);

            script.Globals["makestatic"] = (Func<string, DynValue>)MakeStatic;

            if (CheckArgs(args, new ShellContext(script)))
                return;

            Banner();

            ReplInterpreter interpreter = new(script)
            {
                HandleDynamicExprs = true,
                HandleClassicExprsSyntax = true
            };


            while (true)
            {
                InterpreterLoop(interpreter, new ShellContext(script));
            }
        }

        private static DynValue MakeStatic(string type)
        {
            Type tt = Type.GetType(type);
            if (tt == null)
                Console.WriteLine("Type '{0}' not found.", type);
            else
                return UserData.CreateStatic(tt);

            return DynValue.Nil;
        }

        private static void InterpreterLoop(ReplInterpreter interpreter, ShellContext shellContext)
        {
            Console.Write(interpreter.ClassicPrompt + " ");

            string s = Console.ReadLine();

            if (!interpreter.HasPendingCommand && s.StartsWith("!"))
            {
                ExecuteCommand(shellContext, s.Substring(1));
                return;
            }

            try
            {
                DynValue result = interpreter.Evaluate(s);

                if (result.IsNotNil())
                    Console.WriteLine("{0}", result);
            }
            catch (ErrorException ex)
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
            Console.WriteLine(LuaState.GetBanner("Console"));
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
                LuaState script = new();
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
            else if (args[0] == "-W")
            {
                bool internals = false;
                string dumpfile = null;
                string destfile = null;
                string classname = null;
                string namespacename = null;
                bool useVb = false;
                bool fail = true;

                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--internals")
                        internals = true;
                    else if (args[i] == "--vb")
                        useVb = true;
                    else if (args[i].StartsWith("--class:"))
                        classname = args[i].Substring("--class:".Length);
                    else if (args[i].StartsWith("--namespace:"))
                        namespacename = args[i].Substring("--namespace:".Length);
                    else if (dumpfile == null)
                        dumpfile = args[i];
                    else if (destfile == null)
                    {
                        destfile = args[i];
                        fail = false;
                    }
                    else fail = true;
                }

                if (fail)
                {
                    Console.WriteLine("Wrong syntax.");
                    ShowCmdLineHelp();
                }
                else
                {
                    HardWireCommand.Generate(useVb ? "vb" : "cs", dumpfile, destfile, internals, classname, namespacename);
                }
            }

            return true;
        }

        private static void ShowCmdLineHelpBig()
        {
            Console.WriteLine("usage: moonsharp [-H | --help | -X \"command\" | -W <dumpfile> <destfile> [--internals] [--vb] [--class:<name>] [--namespace:<name>] | <script>]");
            Console.WriteLine();
            Console.WriteLine("-H : shows this help");
            Console.WriteLine("-X : executes the specified command");
            Console.WriteLine("-W : creates hardwire descriptors");
            Console.WriteLine();
        }

        private static void ShowCmdLineHelp()
        {
            Console.WriteLine("usage: moonsharp [-H | --help | -X \"command\" | -W <dumpfile> <destfile> [--internals] [--vb] | <script>]");
        }

        private static void ExecuteCommand(ShellContext shellContext, string cmdline)
        {
            StringBuilder cmd = new();
            StringBuilder args = new();
            StringBuilder dest = cmd;

            for (int i = 0; i < cmdline.Length; i++)
            {
                if (dest == cmd && cmdline[i] == ' ')
                {
                    dest = args;
                    continue;
                }

                dest.Append(cmdline[i]);
            }

            string scmd = cmd.ToString().Trim();
            string sargs = args.ToString().Trim();

            ICommand C = CommandManager.Find(scmd);

            if (C == null)
                Console.WriteLine("Invalid command '{0}'.", scmd);
            else
                C.Execute(shellContext, sargs);
        }







    }
}
