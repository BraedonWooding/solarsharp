# Differences to Lua (5.2)

> We are working on enabling 5.4 compatibility.

While we aim to keep the compatible, performance / other impacts means there are a few changes.

## Character Encoding in strings

Lua uses utf-8, but we use utf-16 (because C# uses it).  This is extremely unlikely to effect you but if you for some reason do need to use proper utf-8 behaviour then you can use the `unicode` library that supports converting strings to a utf-8 like byte array to operate on.

> TODO: Unicode Library

## Weak Table Support

We don't support weak tables at all.

This is mainly because while there are solid use cases, there haven't been any yet that warrant the significant implementation effort.

If you are interested in contributing towards this; more is written here [Weak Tables](./WeakTablesSupport.md) that goes through what implementation thoughts.

## Standard Library differences to Lua's Standard Library

### String

As stated before the fact we use utf-16 changes quite a bit of how strings are used primarily some methods change;
- `byte` returns utf-16 codepoints rather than utf-8.
