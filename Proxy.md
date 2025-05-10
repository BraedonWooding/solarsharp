# Moonsharp Proxy

This is a compatibilty layer built to support **most** MoonSharp projects to move to SolarSharp without having to make significant changes.

This covers the following;
1. Namespaces & class names, we define a series of wrapper classes that use the moonsharp namespaces
2. Supporting DynValues rather than just LuaValues
    a. LuaValues are an important aspect of our design since they are structs rather than classes, this gives them a significant performance boost
    b. Note: that void == nil and tuple == table for sake of type matching.
3. MoonSharpModuleMethodAttribute will work on static fields though it does work *slightly* differently, but not functionally differently enough to change the result.
4. `json` will be reverted back to the old behaviour (note: this includes bugs / awkward behaviour too)

To support progressive migration, you can set the global variable `MoonSharpToLuaSharpMigrationOptions` with the following options;
1. Disable `json` replacement (i.e. use the new `json` package)
2. Remove `DynValue` conversion (throws exception)

You can also enable logging an error anytime you hit any code that uses the moonsharp compatibilty layer rather than solarsharp.y

This does not cover the following;
1. Prime tables, just use normal tables they can be freely shared between script instances
2. Script ownership was also removed
3. Debugger was removed, we have different tooling for debugging lua scripts (including supporting existing lua debuggers)
