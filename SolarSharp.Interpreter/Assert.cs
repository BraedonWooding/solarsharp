using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SolarSharp.Interpreter
{
    public static class Assert
    {
        public static void ThatIsTrue(bool condition, [CallerArgumentExpression(nameof(condition))] string message = "")
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
