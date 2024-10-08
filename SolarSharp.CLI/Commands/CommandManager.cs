﻿using SolarSharp;
using SolarSharp.Commands.Implementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SolarSharp.Commands
{
    internal static class CommandManager
    {
        private static readonly Dictionary<string, ICommand> m_Registry = new();

        public static void Initialize()
        {
            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes()
                .Where(tt => typeof(ICommand).IsAssignableFrom(tt))
                .Where(tt => tt.IsClass && !tt.IsAbstract)
            )
            {
                object o = Activator.CreateInstance(t);
                ICommand cmd = (ICommand)o;
                m_Registry.Add(cmd.Name, cmd);
            }
        }

        public static void Execute(ShellContext context, string commandLine)
        {

        }

        public static IEnumerable<ICommand> GetCommands()
        {
            yield return m_Registry["help"];

            foreach (ICommand cmd in m_Registry.Values.Where(c => !(c is HelpCommand)).OrderBy(c => c.Name))
            {
                yield return cmd;
            }
        }


        public static ICommand Find(string cmd)
        {
            if (m_Registry.ContainsKey(cmd))
                return m_Registry[cmd];

            return null;
        }
    }
}
