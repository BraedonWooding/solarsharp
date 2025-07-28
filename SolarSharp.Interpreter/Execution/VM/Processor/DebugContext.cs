using System.Collections.Generic;
using SolarSharp.Interpreter.Debugging;

namespace SolarSharp.Interpreter.Execution.VM
{
    internal sealed partial class Processor
    {
        private class DebugContext
        {
            public bool DebuggerEnabled = true;
            public IDebugger DebuggerAttached = null;
            public DebuggerAction.ActionType DebuggerCurrentAction = DebuggerAction.ActionType.None;
            public int DebuggerCurrentActionTarget = -1;
            public SourceRef LastHlRef = null;
            public int ExStackDepthAtStep = -1;
            public List<SourceRef> BreakPoints = new();
            public bool LineBasedBreakPoints = false;
        }
    }
}
