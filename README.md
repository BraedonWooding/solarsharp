SolarSharp [![.NET](https://github.com/BraedonWooding/solarsharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/BraedonWooding/solarsharp/actions/workflows/dotnet.yml)
=========

A complete Lua solution written entirely in C# for the .NET, Mono, and Unity3D platforms.

> Is a modernized version of [MoonSharp](https://github.com/moonsharp-devs/moonsharp)
> Performance stats are here: https://braedonwooding.github.io/solarsharp/dev/bench/

Changes from MoonSharp:
* Built on .net standard 2.1 to take advantage of more modern C# features
* Performance improvements across the interpreter & parser
* Bug fixes

Features:
* High compatibility with Lua 5.2 (with the only unsupported feature being weak tables support) 
* Support for metalua style anonymous functions (lambda-style)
* Easy to use API
* Runs on .netcore, Unity3D, and many other platforms
* Runs on Ahead-of-time platforms like iOS
* Runs on IL2CPP converted code
* No external dependencies, implemented in as few targets as possible
* Easy and performant interop with CLR objects, with runtime code generation where supported
* Interop with methods, extension methods, overloads, fields, properties and indexers supported
* Support for the complete Lua standard library with very few exceptions (mostly located on the 'debug' module) and a few extensions (in the string library, mostly)
* Async methods for .NET 4.x targets
* Supports dumping/loading bytecode for obfuscation and quicker parsing at runtime
* An embedded JSON parser (with no dependencies) to convert between JSON and Lua tables
* Easy opt-out of Lua standard library modules to sandbox what scripts can access
* Easy to use error handling (script errors are exceptions)
* Support for coroutines, including invocation of coroutines as C# iterators 
* REPL interpreter, plus facilities to easily implement your own REPL in few lines of code

**License**

The program and libraries are released under a 3-clause BSD license - see the license section.

**Usage**

Use of the library is easy as:

```C#
double Factorial()
{
	string script = @"    
		-- defines a factorial function
		function fact (n)
			if (n == 0) then
				return 1
			else
				return n*fact(n - 1)
			end
		end

	return fact(5)";

	DynValue res = Script.RunString(script);
	return res.Number;
}
```
