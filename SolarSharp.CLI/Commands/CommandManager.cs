using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SolarSharp.Commands.Implementations;

namespace SolarSharp.Commands;

internal static class CommandManager
{
    private static readonly Dictionary<string, ICommand> m_Registry = new();

    public static void Initialize()
    {
        foreach (var t in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(tt => typeof(ICommand).IsAssignableFrom(tt))
                     .Where(tt => tt.IsClass && !tt.IsAbstract)
                )
        {
            var o = Activator.CreateInstance(t);
            var cmd = (ICommand)o;
            m_Registry.Add(cmd.Name, cmd);
        }
    }

    public static void Execute(ShellContext context, string commandLine)
    {
    }

    public static IEnumerable<ICommand> GetCommands()
    {
        yield return m_Registry["help"];

        foreach (var cmd in m_Registry.Values.Where(c => !(c is HelpCommand)).OrderBy(c => c.Name)) yield return cmd;
    }


    public static ICommand Find(string cmd)
    {
        if (m_Registry.TryGetValue(cmd, out var find))
            return find;

        return null;
    }
}