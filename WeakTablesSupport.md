# Weak Tables in SolarSharp

Weak tables are complex primarily because they need to maintain an exact 1:1 interface with normal tables.

A few things to note is that we only GC "references" in solarsharp, so any keys/values that are non-references such as ints/doubles aren't eligible for auto collection.

> A difference to Lua is that we will GC light user data / strings since they are GC'd.

This likely would require us to create 3 separate **new** dictionaries;
1. Weak values, these can use a `WeakReference` in the value type of the dictionary and can probably just wrap a standard lua dictionary.
2. Weak keys only, this would use a `ConditionalWeakTable` (likely), though this would probably only support netstandard2.1/netcore+ (and potentially just netcore+).  The issue here however is more that ConditionalWeakTables are not dictionaries, and there are tons of behaviour that might not be possible.
3. Weak keys & values

## Recommended Milestones

This should be done over multiple PRs, and the recommended route of tackling this is;

### Weak Values (only)

Weak Values only.  This isn't actually that useful (most pooling systems use weak keys) but it's the simplest for us to build.

It very likely can be accomplished by having LuaDictionary be `LuaValue, WeakReference<LuaValue>`.  Currently, it's already type generic, so it probably just needs a wrapper class.

You will have to wrap almost every method though so I recommend you create an interface first called `ILuaDictionary`.  When a `LuaTable` goes from strong -> weak values, it would just change it's dictionary type to this weak value dictionary.

The challenge will be however that `WeakReference` won't support LuaValue because LuaValue will be a struct.  Still thinking of solutions here but current thought is to add a new LuaValue type called `WeakReference`, then we just just store a `LuaValue, LuaValue` but the LuaValue holds a weak reference to the actual object.

> This weak reference wouldn't be a normal "type" but likely just a flag or something set on the NaN boxed type?  The idea being that we want to preserve the original type it's just that resolving it might be nil or the type.

> Conversion shouldn't be too tricky, just have to move every item across.  Do **NOT** try to over-optimize converting between modes, this is not a common use-case, and the performance here is unlikely to matter.  However, an empty dictionary conversion should be cheap!

### Weak Keys (only)

This is more challenging because you can't just use a `WeakReference<LuaValue>, LuaValue`, else the table will grow forever and you will end up with issues if the value -> keys.

So instead, you should use the ephemeron table that C# supports which is a `ConditionalWeakTable`, the challenging part here will be adding enough of an interface to it to support `ILuaDictionary`.

> There is a good chance that we'll only be able to support netstandard2.1+ with this (or potentially even newer versions).  Due to netstandard2.0 not having the full featured class.

This is tricky, because you can't store structs in ConditionalWeakTables (for either value or key), but this can be solved via;
1. Storing `object` in the conditional table only.  We can use the type info stored on it to figure out it's type (and in most cases we know what type it is).
2. We could also use a `LuaValueCls` or something that wraps object + type (in a new allocation though which sucks).
3. Another option is storing the types separately, since we use NaN boxing this isn't necessary for normal tables, but we can do it for this.
   1. This might require a custom weak table, since ideally we would store the types in a separate array with the indexes being synced.
4. If we are going down the custom weak table we might just write a completely custom one, that just stores key + value + type in the entry info for each node (might be better than storing separately).

However, how do we handle this when the keys/values are doubles or booleans or some other non-GC'd type?
1. We could box them but that'll break with Weak Keys + Values that'll likely build on this (since each box will be a unique object which will be instantly GC'd).
   1. Though this might be acceptable?  I would definitely prefer for them to just never get GC'd though
2. Store them in a separate lua table.

The complexity for 2. 1. is that if we have 3 cases now;
1. ReferenceType Key + ReferenceType Value => stored in conditional weak table as expected
2. ValueType Key + ReferenceType Value => stored in `LuaTable<LuaValue, WeakReference<LuaValue>>` Note: that this runs into the same issues as Weak Values only but since we would have solved it there this should be fine.
3. ReferenceType Key + ValueType Value => since the value type can **NOT** reference the key because our only value types are ints/nil/bool/doubles (struct types have to be boxed) we can just use a normal `LuaTable<WeakReference<LuaValue>, LuaValue>` (remembering the weak reference issue from 2) since we don't need to make it so that the value has a weak reference to the key.

As you can see this becomes quite complex, and any lookup will result in having to figure out what table to look it up since we would now have 2 tables (you can fold 3 + 2 into the same table if you use the weak lua value type).

### Weak Keys + Values (both)

Conceptually, this is like above but the values can also be collected independently, so you would need a real key + real value reference elsewhere.

I'm not entirely sure how this would look but the idea being;

`ConditionalWeakTable<object, WeakReference<object>>` this is basically above but importantly the key **will not** keep the value alive if there are no other references to the key.

All the issues here have to be solved by the above milestones, so ironically this becomes the "easiest" one.

> We have to use ConditionalWeakTable because if you have a normal `LuaTable<WeakReference<LuaValue>, WeakReference<LuaValue>>` then it won't work if the value references the key (because it'll keep it alive forever).
