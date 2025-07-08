using SolarSharp.Interpreter.Compatibility.Frameworks;
using SolarSharp.Interpreter.Compatibility.Frameworks.Base;

namespace SolarSharp.Interpreter.Compatibility
{
    public static class Framework
    {
        private static readonly FrameworkCurrent s_FrameworkCurrent = new FrameworkCurrent();

        public static FrameworkBase Do
        {
            get { return s_FrameworkCurrent; }
        }
    }
}
