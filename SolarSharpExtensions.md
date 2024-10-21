# Extensions

The following are a series of extensions to SolarSharp.  There are intended primarily for scripting.

## Sync Library

This contains locks, threading, and other tools for multi-threading.

## Collections Library

This contains sets, queues, and other utilities that are optimized for this use.

## Array Library

The array standard library has a series of nice extensions for arrays & slices.

### `array.create(n)`

`array.create(n)` has an optional first argument that can be either;
- An initial capacity (to save allocations)
- An existing table (to copy the array from)

An array is a more standard C# styled list.

### `array:push(n)`

### `array:reserve(n)`

### `#array`

Returns the current length of the array.  Importantly length is not calculated in a similar way to how length in a lua table is calculated.  Length is purely defined by how many elements (including nil) have been pushed.  If you do `array[2] = nil` it won't reduce the length.

### `array:slice(self, start, end)`

Returns a slice of the array from the given range (inclusive).

> The slice is clamped to be within bounds of the array.

### `table.extractarray`

This will extract the array component from a table and returns it.  This is optimal because it saves the copying of the array.

This grabs the array that is defined from `1..#table`.  Elements that sit outside that will remain (and may be moved into the table segment).

## Async/MultiThreading

The idea of multi-threading is implemented through a task pool + coroutines.  An example to demonstrate is probably the easiest.

```lua
local array = {}
for i = 1, 1000 do
    array[i] = i
end

function sum(start_idx, end_idx)
    local local_sum = 0
    for i = start_idx, end_idx do
        local_sum = array[i]
    end


end

```

> Note: please make sure you read up on the Multi-threading guide in particular [Thread Safety](./Multithreading.md#thread-safety) because tables aren't threadsafe by default if you are mutating them.
