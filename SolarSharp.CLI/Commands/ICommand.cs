namespace SolarSharp.Commands
{
    internal interface ICommand
    {
        string Name { get; }
        void DisplayShortHelp();
        void DisplayLongHelp();
        void Execute(ShellContext context, string argument);
    }
}
