using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SolarSharp.Hardwire.Generators;

namespace SolarSharp.Hardwire
{
    public static class HardwireGeneratorRegistry
    {
        private static readonly Dictionary<string, IHardwireGenerator> m_Generators =
            new Dictionary<string, IHardwireGenerator>();

        public static void Register(IHardwireGenerator g)
        {
            m_Generators[g.ManagedType] = g;
        }

        public static IHardwireGenerator GetGenerator(string type)
        {
            if (m_Generators.ContainsKey(type))
                return m_Generators[type];
            return new NullGenerator(type);
        }

        public static void RegisterPredefined()
        {
            DiscoverFromAssembly(Assembly.GetExecutingAssembly());
        }

        public static void DiscoverFromAssembly(Assembly asm = null)
        {
            if (asm == null)
                asm = Assembly.GetCallingAssembly();

            foreach (
                var type in asm.GetTypes()
                    .Where(t => !(t.IsAbstract || t.IsGenericTypeDefinition || t.IsGenericType))
                    .Where(t => typeof(IHardwireGenerator).IsAssignableFrom(t))
            )
            {
                var g = (IHardwireGenerator)Activator.CreateInstance(type);
                Register(g);
            }
        }
    }
}
