# MoonSharp Migration Guide

This guide attempts to help migrate code from MoonSharp to SolarSharp.

## Step 1. Renames

These can be applied as a single find + replace (with case sensitive & word boundaries enabled).  Of course this relies on you not having variables/types named this too, so be careful.

- Rename `MoonSharp` -> `SolarSharp`
- Rename `DynValue` -> `LuaValue`, this is now a struct and not a class, importantly this means that it's a **copy by value**
- Rename `Script` -> `LuaState`, while this class has changed quite a bit (it's a mix of Processor & Script) the API is mostly compatible.
- Rename `DataType` -> `LuaValueType`, there are some changes to this enum which you can see in Step 3.

## Step 2. Broken/Changed APIs

### `json` module

The `json` module no longer uses a custom parser, it instead (by default) uses newtonsoft json (though this is customisable).

It also is no longer a "builtin" module you can enable this through the following package `SolarSharp.Modules.Json.Newtonsoft`.

```cs
// You can cache the module returned from NewtonsoftJsonModule if you want (it can be shared between all LuaStates).
var state = new LuaState(NewtonsoftJsonModule.Create(new JsonSerializerSettings {}));
```

> `SolarSharp.Modules.Json` uses C#'s System.Text.Json and it's pretty easy to hook up a custom json serializer.

### Library Presets

If you had a custom library preset 

### Dynamic Expressions

We no longer support dynamic expressions, these were mainly intended for use in debuggers.  The idea was that they couldn't evalute functions but could access variables/do math.

They weren't very useful (outside of debuggers which was already removed).

If you want to run a section of code under a heavy sandbox we already support that through the new sandboxing features.  Which you can checkout here [Sandbox Features](./Sandboxing.md).

### Dump Files

The opcode & instruction format has changed significantly so we no longer support old dump files.

You'll have to recompile all Lua code to produce new dump files.

### Script Ownership

Very unlikely you worked with or relied on ownership, but objects now don't have an "owning" environment.  This does meant that tables can be shared between environments.

> For security reasons you should ideally keep objects sandboxed to their environment.  Enforcing this at the VM level is too low level (because at that point all we can really do is throw an exception/crash).

## Step 3. Lua Syntax Changes



## Step 4. LuaValues / LuaValueTypes
