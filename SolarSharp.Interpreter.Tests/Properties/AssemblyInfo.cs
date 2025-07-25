using System.Reflection;
using NUnit.Framework;

[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Enable parallel test execution
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(4)]