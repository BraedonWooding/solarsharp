﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MoonSharp.Commands.Implementations
{
	class DebugCommand : ICommand
	{
		//private RemoteDebuggerService m_Debugger;

		public string Name
		{
			get { return "debug"; }
		}

		public void DisplayShortHelp()
		{
			Console.WriteLine("debug - Starts the interactive debugger");
		}

		public void DisplayLongHelp()
		{
			Console.WriteLine("debug - Starts the interactive debugger. Requires a web browser with Flash installed.");
		}

		public void Execute(ShellContext context, string arguments)
		{
			throw new NotImplementedException("Debugger is not yet implemented.");
		}
	}
}