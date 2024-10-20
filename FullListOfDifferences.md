# Compared to MoonSharp

Note: we have a separate package called `SolarSharp.Compatibility.MoonSharp` that adds *most* of this compatibility back.  It does not intend to fully match the old behaviour but instead intends to allow previous MoonSharp code to compile.

It does the following:
- Add proxy classes for all the exposed classes (i.e. a class DynValue that wraps a LuaValue) that keep the *OLD* moonsharp namespaces.  This should make c# code that previously compiled still compile.
- Enables syntax support for some legacy syntax (`$` & `|x, y|`).  This should make lua code that previous compiled still compile.
- Add some features like Dynamic Expressions/dynamic package back into SolarSharp by re-implementing them ontop of new features (i.e. Dynamic Expressions is re-implemented by using our new sandbox).
- Adds `_MOONSHARP` which just points to `_SOLARSHARP`

This has some downsides:
- While Lua code will run faster and allocate less, any objects given to/from C# functions that use MoonSharp objects will result in allocations.
- It does **not** add back script ownership, and there is no errors in passing objects between scripts
- It does **not** guarantee bug for bug compatibility (we fixed many bugs in the transition)
- The features are re-implemented so there might be meaningful implementation differences that may result in bugs if you rely on features being implemented in specific ways.
- `Void` will implicitly just convert back/from `Nil` so if you have any code that checks/relies on `Void` you may run into issues.  This is pretty unlikely to effect your code (since `void` was mostly an internal compiler type)
- Dynamic Expressions were fully removed, **but** compatibility mode will proxy them to the new sandboxed execution mode and will **roughly** match it's compatibility/purpose.  Read into [Sandboxing](./Sandboxing.md) for more details.
- Debugger were fully removed and we support **no** compatibility with their types **outside** of SourceRef.
- `json` uses System.Text.Json or Newtonsoft now and we don't support the old custom json parser/serializer.  This shouldn't have a meaningful difference for your application but some things such as order of keys may change (which aren't guaranteed anyways for json).
- We **don't** (and won't) support running from MoonSharp dumps (which are pre-compiled lua code).  We **may** add a new dump feature in the future (we don't currently support it) but it'll require regenerating the dumps.
- We **don't** (and won't) support MoonSharp hotwiring.  We **might** (likely will) add a new source generator solution to match the similar behaviour but it'll require regenerating the files.
- `__iterator` isn't supported and the replacement `__pairs` (added in Lua 5.2) isn't a perfect replacement from a syntax point of view.

## Removal of Script Ownership

- Script ownership have been removed (it was preventing the sharing of tables/objects between states)
	- This wasn't a very useful security feature (since if you are sharing tables between states you have larger issues) and introduced quite a bit of complexity (and lots of checks/extra memory).
- Prime tables have been removed (since there is no need to have a "shareable" table anymore)

## Class Renaming

- Script class was mostly removed & it was merged with "Processor" to become "LuaState"
- DynValue -> LuaValue and is a completely different type structure now
	- It's a 16 byte struct now rather than a 32 byte class.  It's much cheaper to copy / carry around *but* you can't update a `LuaValue` and expect it's "slot" to update (since it's a value type not a reference type)
	- We use NaN boxing to store the type in the number portion
	- This also removes readonly since structs are readonly by default
- DataType -> LuaValueType and there have been some changes in what types are available
	- Void is no longer a type

And many more... most classes were changed in some way.  By installing the `SolarSharp.Compatibility.MoonSharp` package it will add proxy classes so you don't need to update references.

## MoonSharpModuleMethodAttribute no longer applies to static fields

SolarSharpModuleMethodAttribute/MoonSharpModuleMethodAttribute can not be applied to static fields anymore, it can only be applied to methods.

If you add the `SolarSharp.Compatibility.MoonSharp` package it will add support for MoonSharpModuleMethodAttribute on static fields, but it does this through a pattern like this;

> TODO: This is a rough draft
```cs
[MoonSharpModuleMethod]
static string LuaCode = @"...";

[SolarSharpInit]
void LuaCode__impl(Context ctx, ...) {
    ctx.DoString(LuaCode);
}
```

## Debugger removed

Debugger was fully removed and has no support (even in compatibility package).  We **want** to instead support Lua's debugger format (through the `debug` library).

## `dynamic` removed

Dynamic package has been removed.  `SolarSharp.Compatibility.MoonSharp` adds a new package `dynamic` that uses our new sandboxing to roughly emulate it's behaviour (it should work for all major cases).  But we recommend you use the new sandboxing features instead.

Others:
- Dynamic Expressions were removed
	- They are harder to keep in sync with standard Lua processor and are ripe for bugs
	- We instead support much stronger sandboxing and introduce dynamic "like" expressions (for debugging/other use cases) through that.

## `json` changed

We use System.Text.Json as the de/serializer now.  Our apis have changed.
- We support encoding/decoding options
- We encode/decode null differently (and support a null override through options)

If you install `SolarSharp.Compatibility.MoonSharp` it will change the `json` package to match the old API.

## `__iterator` removed

`__iterator` support has been removed, instead use `__pairs`.  The only major difference here is that `for k, v in t ...` was previously supported by using `__iterator` but now you need to use `for k, v in pairs(t)`.

This isn't a perfect solution and if this significantly impacts you then I am willing to look into supporting `__iterator` (likely by adding compatibility code directly to the core package).

## Syntax Changes (Lua)

- `$` is no longer a valid character since prime tables were removed
- Lambda functions `|x, y|` were removed

Both of these are supported if you enable **legacy** MoonSharp compatibility mode.  We recommend against it, Lua is pretty concise as a language anyways.

## Global Changes

- `loadsafe`

## Lua Version Changes

We support Lua 5.4 now but with some differences:
- We don't support weak tables at the moment.  I am open to them being added but they require a proper design discussion since they aren't trivial.
- We use C#'s GC

# Compared to Lua

## New Libraries!

You have to opt-in to the new library set but we support the following new libraries:
- `sync` which comes with tons of multi-threading utilities for synchronising (locks for example)
- `queue` / `set` / `array` which are common collections that are optimized for their usecase
- `task` which provide a feature set similar to coroutines but for futures
- `thread` similar to task but is for long living threads
- `process` which are OS level processes
- `shell` a few shell utilities
- `json` for json support
  - By default uses `System.Text.Json` but you can use Newtonsoft by installing `SolarSharp.Modules.Json.Newtonsoft` and initialising it.

## New Syntax

None!  Intentionally.

## Multi-valued indexing for table metamethods

This isn't a syntax change but we do support multiple indexes for tables, this means you can have `__index`/`__newindex` that take multiple keys.

Note: that **tables** don't support multiple indexes and it'll raise an error if it applies to a table.

## `__ipairs`

Note: we don't support `__ipairs` since it was removed in Lua 5.4.
