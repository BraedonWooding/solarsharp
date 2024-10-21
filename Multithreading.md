# Multithreading

## `sync` Library

Check out the [sync library](./SolarSharpExtensions.md#sync-library) for the library reference of what is below.

## Thread Safety

### Tables

Tables are thread-safe **only** for concurrent reads, they aren't thread-safe for updates of any kind.

You can create a thread-safe table in a few ways.

#### `sync.protect`

The easiest is through `sync.protect(table)`, this will protect all reads/writes/metamethod calls with a lock.

Importantly, it won't protect calls like this

```lua
local t = sync.protect({
    a = function()
    end
})

-- The lock is taken for the access of "a" but not for the call
t.a()
```

Howerver it does persist for the entire duration of metamethod operations.  While it does support recursion/re-entrancy (i.e. if the table calls itself it won't dead-lock) this has to be on the *same* thread.  For example this will dead-lock.

```lua
-- Create a fresh task scheduler so that we get fresh threads
-- this just makes the deadlock happen every time
local task_scheduler = scheduler.create()

local t = sync.protect({
    a = "2",
    __mt = {
        __index = function (t, k)
            local handle = task.create(function()
                return rawget(t, k)
            end, task_scheduler)
            return handle:join()
        end
    }
})

-- Will never print
print(t[a])
```

> If you create a proxy type that you put the metamethod on, then as long as you just `sync.protect` the proxy type; this will prevent these kinds of deadlocks.

#### Custom `mutex`

You can also just create a custom mutex.

```lua
local task_scheduler = scheduler.create()
local mutex = sync.mutex()

local t = sync.protect({
    a = "2",
    __mt = {
        __index = function (t, k)
            local handle = task.create(function()
                return rawget(t, k)
            end, task_scheduler)
            return handle:join()
        end
    }
})

-- we can use <close> to auto-release the lock at the `end`
do
    local handle <close> = mutex.lock()
    -- Will print
    print(t[a])
end
```


