SolarSharp [![.NET](https://github.com/BraedonWooding/solarsharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/BraedonWooding/solarsharp/actions/workflows/dotnet.yml)
=========

A complete Lua solution written entirely in C# for the .NET, Mono, and Unity3D platforms.

> Is a modernized version of [MoonSharp](https://github.com/moonsharp-devs/moonsharp)
> Performance stats are here: https://braedonwooding.github.io/solarsharp/dev/bench/

Key changes from MoonSharp:
* Built on .net standard 2.1 to take advantage of more modern C# features
* Large performance improvements across the interpreter & parser
* Some less useful features removed to simplify codebase
* 
* Bug fixes
Look [here for differences from moonsharp](#differences-from-moonsharp).

Features:
* High compatibility with Lua 5.4 (with the only unsupported feature being weak tables support)
* A few key syntax extensions to make Lua code easier to write (lambdas)
* Easy to use API
* Runs on a lot of various platforms; AOT like iOS, Unity3D (including IL2CPP), Mono, ...
* Very easy interop with CLR objects & methods, supporting both just in time & ahead of time code generation
* Supports dumping/loading bytecode for obfuscation and quicker parsing at runtime
* Very strong sandboxing support
* Support for coroutines, including invocation of coroutines as C# iterators 
Look [here for differences from Lua](#differences-from-lua)

# License

The program and libraries are released under a 3-clause BSD license - see the license section.

# Usage

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

	// Note: Ideally don't create a unique state for each run, cache them!
	var state = new LuaState();
	var res = state.DoString(script);
	return res.Number;
}
```

# Differences from Lua

# Differences from MoonSharp

Most of these changes were done because:
- Smaller codebases are easier to iterate on, faster, and have less bugs
- Performance!!  There were a few critical changes made for performance that resulted in heavy refactoring.
  - DynValue, OpCodes, Instructions, Coroutines, and Processors were all *significantly* changed which made up a pretty big chunk of the codebase.

- Script class was mostly removed & it was merged with "Processor" to become "LuaState"
- Script ownership have been removed (it was preventing the sharing of tables/objects between states)
	- This wasn't a very useful security feature (since if you are sharing tables between states you have larger issues) and introduced quite a bit of complexity (and lots of checks/extra memory).
- Prime tables have been removed (since there is no need to have a "shareable" table anymore)
- DynValue -> LuaValue and is a completely different type structure now
	- It's a 16 byte struct now rather than a 32 byte class.  It's much cheaper to copy / carry around *but* you can't update a `LuaValue` and expect it's "slot" to update (since it's a value type not a reference type)
	- We use NaN boxing to store the type in the number portion
	- This also removes readonly since structs are readonly by default
- DataType -> LuaValueType and there have been some changes in what types are available
	- Void is no longer a type
- Dynamic Expressions were removed
	- They are harder to keep in sync with standard Lua processor and are ripe for bugs
	- We instead support much stronger sandboxing and introduce dynamic "like" expressions (for debugging/other use cases) through that.
- Debugger was removed, instead we (ideally) will support native Lua debuggers!  (though we will have to see how good this support will be).
- SolarSharpModule/MoonSharpModuleMethodAttribute can not be applied to static fields anymore, it can only be applied to methods.

You can refer to [Full List of Differences](./FullListOfDifferences.md) for more details.

## Why use MoonSharp if so much of it was modified?

- To be honest I didn't expect this much to have to be modified!
  - DynValues being a class and code being reliant on it being a reference type (i.e. closures using the dynvalues to write to upvalues rather than just writing to parent slots) was a significantly larger refactor than originally thought.
  - Tables are another really good example, the old implementation used linked lists and had major performance problems.  However, simply using a C# Dictionary faces performance issues of it's own due to specific implementation requirements of tables (requiring a custom dictionary type).
- Tests!  This is a really important aspect, having a full set of tests already in the project saves hours upon hours.
- Theseus Ship.  Building a large project like this from scratch requires a lot of time investment before you get a result, this way I can slowly transition the codebase across to my desired result while (ideally) at each point having a complete ship.

## API compatibility with MoonSharp?

Originally the intention was to maintain a very high API compatibility (outside of namespace/some type renames) this however turned out to not be very practical due to multiple architectural changes.  In the end I decided to instead have an API that more accurately represents Lua's API (though it is different to that as well).

In saying that I have a guide here [MoonSharp Migration Guide](./MoonSharpMigrationGuide.md) that you can follow which should (ideally) only take a few hours (often less) to adjust *most* MoonSharp projects across.
