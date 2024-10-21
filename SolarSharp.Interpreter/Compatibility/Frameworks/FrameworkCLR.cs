using SolarSharp.Interpreter.Compatibility.Frameworks.Base;
using System;

namespace SolarSharp.Interpreter.Compatibility.Frameworks
{
    internal class FrameworkCurrent : FrameworkClrBase
    {
        public override Type GetTypeInfoFromType(Type t)
        {
            return t;
        }

        public override bool StringContainsChar(string str, char chr)
        {
            return str.Contains(chr);
        }

        public override Type GetInterface(Type type, string name)
        {
            return type.GetInterface(name);
        }
    }
}
