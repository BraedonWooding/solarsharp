# SolarSharp Internals

We use internals that are inspired by a few lua runtimes (LuaCSharp/MoonSharp/LuaJIT).

The core ideas are as follows:
1. Our core data type is `LuaValue` which is a C-style discriminated union.
2. Our core binding layer uses generated code (analyzers)

## Lua Value

This allows us to have a single type that can represent any Lua value (number, string, table, function, etc.) without the overhead of boxing/unboxing.

It is a struct.  It is 16 bytes (2 registers) and consists of a value field (8 bytes for double) and an object field (8 bytes for reference types).

The type tag can either be done using NaN boxing or via a dynamic object type field.  (These have different tradeoffs).

### LuaValue NaN boxing

This stores the type in the double field using NaN boxing.  This includes the generic types for user data (which uses the object field).

### LuaValue Static Object Tags

These are done by definining a type like `static TypeTag[] = [TypeTag.Nil, ...]"` where each type tag is a class, this allows us to use the object field to store the type tag while still supporting jump tables.
> These jump tables can conceptually be thought as something like:
> ```csharp
> switch (int(value.Array) - int(value.TypeTag.Nil))
> { case 1: ... }
> >```

What is cool about this trick is that we can just use the `default` case to handle "object" types (user data references).

## Generated Functions (and well I guess "custom" lower level functions too)

Most lua runtimes have a conceptual lower level language that functions like below;

```csharp
public LuaValue[] Call(LuaContext ctx, LuaFunction func, LuaValue[] args)
{
    // ...
}
```

I dislike all the array creation (even though net10 might help a little with using stack arrays).

Instead, we have a definition like this;

```csharp
public void Call(LuaContext ctx, LuaFunction func, Span<LuaValue> args, Span<LuaValue> ret)
{
    // ...
}
```

The spans are stack allocated in the parent frame (and are sized based on the function signature + the caller space).

## Why put the return values in a span from parent?

This is an optimization that allows us to avoid array allocations for return values.  Most of the time we can force them to be stack allocated making them more efficient to call functions, doing a `stackalloc[1]` is pretty free.

This means if you do something like:
```lua
x = foo()
```
Even if foo returns multiple args it's `ret` will just contain 1 space.

This optimization means we hide the return type a little and instead do this;

```csharp
public void Call(LuaContext ctx, LuaFunction func, LuaArgs args, LuaReturnValues ret)
{
    // ...
}

readonly ref struct LuaArgs {
    private readonly Span<LuaValue> _span;

    public LuaValue GetValue(int index) {
        if (index >= _span.Length) {
            return LuaValue.Nil;
        } else {
            return _span[index];
        }
    }
}

readonly ref struct LuaReturnValues {
    private Span<LuaValue> _span;
    private byte _offset;
    
    public void SetValue(int index, LuaValue value) {
        if (index >= _span.Length || index < _offset) {
            // Silent ignore
        } else {
            _span[index] = value;
        }
    }
}
```
> Note: we don't use `Span<LuaValue>` because those use 32 bit lengths and our lengths can only ever be up to 255 (given lua functions can't have more than 255 args/return values).  This saves us some space since these are almost always passed using CPU registers.

We do also support `SetValues(int offset, LuaValue v1, LuaValue v2, ...)` for efficiency (they are auto generated and go up to 15).

> A similar thing is done to the args too given you might want to fetch an arg that is not passed in.

# Stack vs Register VMs

Lua is classically a stack based VM (like the original Lua 5.x).  However, register based VMs have some advantages in terms of performance and optimization.  LuaJIT is a register based VM for example.

We use a mostly register based VM for performance.  This allows us to reduce the number of instructions needed for common operations (like variable access, arithmetic, etc.) by using registers directly instead of pushing/popping from the stack.

The way our register VM works is through the concept of a "stack frame".  At the beginning of a function call we allocate a "stack frame" (by bumping a stack pointer).  Note that this stack pointer is classically just a pointer into a large array of `LuaValue` that represents the stack (rather than an actual stack in memory).  This is mainly due to coroutines needing to be able to yield and resume.

# ByteCode

> Note: This entire document has a few different forms of bytecode tables, I've not formalized a single format yet.  This is mainly because I'm still experimenting with different opcodes and formats.

Our bytecode is inspired by LuaJIT's / other runtimes.

We use a register based VM with instructions that operate on registers.

Each instruction is 32 bits (4 bytes).  This is roughly composed of the following structure, some components will use 16 bits though (and thus will be A-B or B-C);

| A | B | C | OP(6) | OP2(2) |
|---|---|---|-------|--------|

> OP2 is the secondary opcode primarily used in math operations.  We can still do a jump since we build the full 8 bit opcode by combining OP and OP2.

Each component is perfectly 8 bits to make it faster to decode.  And given each instruction the 8 bits is interpreted differently based on the OP code.

There are typically a few different possible operand types;
- `r` / `Reg` - A register index (0-255)
- `i` / `Imm` - An immediate value (0-255) (or a float immediate)
- `c` / `Const` - A constant index (0-65535) into the constant (int/float/bool) table or the constant string table
- `u` / `Upval` - An upvalue index (0-65535)
> Note: we have 2 separate constant tables for reference and value types.  Typically, it's pretty obvious which one to use based on context.

This does limit us to 256 registers per function.  This could be an issue but variables in global scope / upvalues are accessed through special opcodes that use constant indices that are larger.

We have 41 opcodes in total (not including op2 variants).  Since Opcode is 6 bits we have room for 64 opcodes total, so we have some room to grow.

## Metadata

Every bytecode dump has the following metadata at the start (12 bytes):
- Magic Number (4 bytes) - `0x53 0x6F 0x6C 0x23` (`Sol#`)
- Version (1 byte) - Current version (current version `1`)
- Flags (1 byte) - Reserved for future use
- Reserved (2 bytes) - Reserved for future use
- Function Count (4 bytes) - Number of functions in the bytecode
- Then we have the function list (which contains function metadata such as upvalue count, arg count, register count, constant tables, string tables, and instruction offsets).

Note: we maintain a global string intern pool that is shared across all functions.

## Memory Ops (1 op)

> Stack operations use the stack pointer internally to determine where to read/write from.

Memory ops based on Op2
- `REG = 0x00` - Both A and B are registers
- `IMM = 0x01` - A is a register, B is an immediate int (16 bits)
- `CST = 0x10` - A is a register, B is a constant lua int/float/bool index
- `STR = 0x11` - A is a register, B is a constant string index
> We have some special `CST` values, for example `0` = false, `1` = true.  And in the case of `STR` `0` = nil, `1` = empty string.

| OP  | OP2 | A | B (2 bytes) | What is it?     |
|-----|-----|---|-------------|-----------------|
| MOV | REG | r | r           | R(A) = R(B)     |
| MOV | IMM | r | i           | R(A) = B        |
| MOV | CST | r | c           | R(A) = Const(B) |
| MOV | STR | r | c           | R(A) = Str(B)   |

## Upvalue Ops (2 ops)

> Upvalue operations use the function's upvalue table to determine where to read/write from.

Each upvalue is a 16 bit index so we can have up to 65536 upvalues per function.

> Op2 has the format of memory ops.

| OP       | OP2 | A | B   | What is it?                         |
|----------|-----|---|-----|-------------------------------------|
| UP_GET   | Reg | r | u   | R(A) = UP(B)                        |
| UP_SET   | Reg | r | u   | UP(B) = R(A)                        |
| UP_SET   | Imm | i | u   | UP(B) = A                           |
| UP_SET   | Cst | c | u   | UP(B) = Const(B)                    |
| UP_SET   | Str | c | u   | UP(B) = Str(B)                      |
| UP_CLOSE | -   | u | jmp | Close upvalues >= A, then jump to B |

> The jump for UP_CLOSE is optional, and is relative to the next instruction, so a default value of 0 is just the next instruction.

TODO: create new functions?

> Note that the UP_SET (C) variant only has a 8 bit constant index, so we can only have 256 constants for upvalue sets.  This should be fine though.

> We have some special `CST` values, for example `0` = false, `1` = true.  And in the case of `STR` `0` = nil, `1` = empty string.

## Bitwise Ops (6 ops)

All bitwise arithmetic is 32-bit integer based.  The value of Op2 uses the memory ops standard that is:
- `REG = 0x00` - Both A and B are registers
- `IMM = 0x01` - A is a register, B is an immediate int (16 bits)
- `CST = 0x10` - A is a register, B is a constant lua int index
- `STR = 0x11` - Not used for bitwise ops

| OP  | OP2 | A | B | What is it?             |
|-----|-----|---|---|-------------------------|
| NOT | REG | r | r | R(A) = ~R(B)            |
| AND | REG | r | r | R(A) = R(B) & R(C)      |
| AND | IMM | r | i | R(A) = R(B) & C         |
| AND | CST | r | c | R(A) = R(B) & Const(C)  |
| OR  | REG | r | r | R(A) = R(B) \| R(C)     |
| OR  | IMM | r | i | R(A) = R(B) \| C        |
| OR  | CST | r | c | R(A) = R(B) \| Const(C) |
| XOR | REG | r | r | R(A) = R(B) ^ R(C)      |
| XOR | IMM | r | i | R(A) = R(B) ^ C         |
| XOR | CST | r | c | R(A) = R(B) ^ Const(C)  |
| SHL | REG | r | r | R(A) = R(B) << R(C)     |
| SHL | IMM | r | i | R(A) = R(B) << C        |
| SHL | CST | r | c | R(A) = R(B) << Const(C) |
| SHR | REG | r | r | R(A) = R(B) >> R(C)     |
| SHR | IMM | r | i | R(A) = R(B) >> C        |
| SHR | CST | r | c | R(A) = R(B) >> Const(C) |

## Math Ops (12 ops)

> Op2 has 2 bits and each bit is used to control the type of B / C.

| OP     | OP2 | A | B | C | What is it?            |
|--------|-----|---|---|---|------------------------|
| ADD_rr | ?   | r | r | r | R(A) = R(B) + R(C)     |
| ADD_ri | ?   | r | r | i | R(A) = R(B) + C        |
| ADD_rc | ?   | r | r | c | R(A) = R(B) + Const(C) |
| SUB_rr | ?   | r | r | r | R(A) = R(B) - R(C)     |
| SUB_ri | ?   | r | r | i | R(A) = R(B) - C        |
| SUB_rc | ?   | r | r | c | R(A) = R(B) - Const(C) |
| MUL_rr | ?   | r | r | r | R(A) = R(B) * R(C)     |
| MUL_ri | ?   | r | r | i | R(A) = R(B) * C        |
| MUL_rc | ?   | r | r | c | R(A) = R(B) * Const(C) |
| DIV_rr | ?   | r | r | r | R(A) = R(B) / R(C)     |
| DIV_ri | ?   | r | r | i | R(A) = R(B) / C        |
| DIV_rc | ?   | r | r | c | R(A) = R(B) / Const(C) |

> Exponential is not implemented as a low level operation, it instead is just implemented through `math.pow` calls (it's just not common enough to warrant a low level op, but I might add it at some point in the future).

For cases like `add_rr` the OP2 bits act as a hint for faster math operations and is represented as:
- `0x00` - Either B or C are non-floats/ints and require a full call evaluation potentially.  (It will still check if both are floats/ints first before doing that).
- `0x01` - B & C are both integers
- `0x10` - B & C are both floats
- `0x11` - B & C are both objects

These are just hints, and if the hint is wrong it will fall back to the correct evaluation.
> Hints are generated through type flow analysis.  We might implement a "JIT" like mechanism that updates hints at runtime in the future.

In cases for `ri` or similar, it instead is defined as:
- `0x?0` - C is an integer immediate
- `0x?1` - C is a float immediate
- `0x1?` - B matches C (hint only)
- `0x0?` - B is an object (hint only)

## Table Ops (5 ops)

| OP      | OP2 | A | B  | C  | What is it?                    |
|---------|-----|---|----|----|--------------------------------|
| TBL_NEW | -   | r | i? | i? | R(A) = Table() (with size B/C) |
| TBL_GET | ?   | r | r  | r  | R(A) = R(B)[?]                 |
| TBL_SET | ?   | c | r  | c  | R(B)[?] = R(A)                 |
| GBL_GET | ?   | r | c  | -  | R(A) = Gbl[?]                  |
| GBL_SET | ?   | c | r  | -  | Gbl[?] = R(A)                  |

The table ops use the following variants for Op2:
- `Reg = 0x00` - `R(B)[R(C)]`
- `Imm = 0x01` - `R(B)[C]`
- `Cst = 0x10` - `R(B)[Const(C)]`
- `Str = 0x11` - `R(B)[Str(C)]`

> For TBL_NEW, B / C are combined and the lower 11 bits are the array size pre-hint, and the upper 5 bits (raised to the power of 2) are the hash size.  They are both optional (if not used they are 0).

## Call Ops (1 op)

Call Ops function quite differently to other ops.  This is because of multiple return results.

When you call a function you allocate space on the stack for the args + return values.  This is done through
registers.  For example the function call `a, b = foo(c, d, e)` would:
- Allocate registers for `c`, `d`, `e` (args)
- Allocate registers for `a`, `b` (return values)
Importantly, in that order.

When we call a function we have to inform them of the space allocations this is done by passing 2 `Span<LuaValue>`'s to the call function.  These are relatively cheap to create as they are just a pointer + length (16 bytes each) so we can pass them in CPU registers.

For example, the `CALL` opcode will have `A` as the function register, `B` as the number of args, and `C` as the number of return values allocated.

Variadic functions do complicate this process.  There are 2 concepts that we need to support:
1. `select(n, ...)` - This allows us to get the nth+ values from the vararg list.
2. `{...}` - This allows us to pack all vararg values into a table.
3. Passing varargs to other functions.  For example, `foo(...)` or even `foo(returns_multiple())` or even `{foo()}`.
> 3, kinda relates to 1 but is a little different because select is implemented as a library function so we could just have a custom opcode for it.

> Note: this doesn't apply to cases where a function returns a fixed number of return values (these are just transformed to normal calls effectively when they are made).  The only cases we need to handle are when a function returns multiple values (like `return ...` or `return returns_multiple()`).

I've taken a different route to multres/luajit here.  Since the caller is responsible for allocating space for return values in cases where we just consume **some** of the variadic return values we don't need to do anything special, for example:
```lua
a, b = returns_multiple()
```
Here we just allocate space for `a` and `b` and ignore the rest.  This is no different to fixed return functions.

When passing in variadic arguments we don't have to do **anything** different, since the caller knows how many they just allocate that many (as discussed above), this covers cases like `select(1, 4, 3, 2)`.
> Note: as discussed in next section there is an extra parameter on the `LuaReturnValues` struct that indicates the offset to start writing from.  I'm not going to discuss that here, because it's pretty minor and is just an optimization for `select`.

In cases where we don't have explicit return value allocations (like `foo(bar())`, `foo(1, bar())` or `{foo()}`).  We instead specify that through the `MULT` flag on the op2 parameter.  This doesn't necessarily mean it'll be called as a multiple return function for example the function definition `function foo() return 1, 2 end` doesn't need multires.  If the function however does not have a fixed return value count then when the call executes it'll execute it in multres mode.

Functions **know** when they need to be called in multres mode because they will have a non-fixed return value count, this means they'll already accept a different **extra** argument `LuaMultiResults` that will store all the un-fixed return values (they still store the fixed return values in the normal `LuaReturnValues` span stored in the struct).  This structure consists of this:
```csharp
readonly readonly ref struct LuaMultiResults {
    Action<int, LuaValue> setMultiResult;
    LuaReturnValues ret;
    
    public void SetResult(int index, LuaValue value)
    {
        if (!ret.TrySetValue(index, value))
        {
            setMultiResult(index, value);
        }
    }
}
```

Conceptually, this function pointer will get passed all the way down to the lowest level function that actually produces the multiple return values.  For example if we had:
```lua
function a() return 1, 2, 3 end
-- Indirection because otherwise the call would have a trivial fixed count
-- (it's still trivial, but ideally you could see how obfuscated this could get).
function b() local c = a; c() end
function c() return b() end
x, y = c()
```
Then the `SetMultiResult` function pointer will get passed from `c` to `b` to `a` where it will be used to set the multiple return values.

This means that we can hyper-optimize common cases like `{...}` by having it write **directly** to the table's array part (pretty cool!).  We can also handle cases like `foo(n, bar())` easily as well by just having it write to the argument frame of the `foo` call.

Cases like `select(n, foo())` can be implemented in a very similar way by just have it write to the parent's stack frame starting from offset `n-1` (this offset is stored always inside the `LuaReturnValues` struct as mentioned before).

| Op   | OP2       | A | B | C | What is it?                         |
|------|-----------|---|---|---|-------------------------------------|
| CALL | TAIL/MULT | r | i | i | A+B+1,...A+B+C = (R(A))(A+1,...A+B) |

The opcode2 for call can have a few variants:
- `NONE = 0x00` - Normal call
- `TAIL = 0x?1` - Tail call
- `MULT = 0x1?` - Multiple return values

Tail calls effectively just do `return func(args...)` internally.  This means they don't need to allocate a new stack frame, instead they just reuse the current one.  This is important for performance and preventing stack overflows in recursive functions.

## Return Ops (1 op)

| Op   | OP2  | A | B | C | What is it?           |
|------|------|---|---|---|-----------------------|
| RET  | MULT | r | i | - | return R(A)..R(A+B-1) |

The opcode2 for return can have a few variants:
- `NONE = 0x00` - Normal return
- `MULT = 0x01` - Multiple return values
> The first bit is reserved but for now is always 0.

There are also the return formats for RET_ONE/RET_ZERO

| Op       | OP2 | A | What is it?     |
|----------|-----|---|-----------------|
| RET_ONE  | REG | r | return R(A)     |
| RET_ONE  | CST | c | return Const(A) |
| RET_ONE  | STR | c | return Str(A)   |
| RET_ONE  | IMM | i | return A        |
| RET_ZERO | -   | - | return          |

Op2 in this case uses the memory op format.  This allows us to have optimized return paths for common cases such as returning a constant or immediate value.

## Unary Ops (1 op)

| Op    | OP2 | A | B | What is it?     |
|-------|-----|---|---|-----------------|
| UNARY | ?   | r | r | R(A) = OP R(B)  |

The unary ops are as follows (using Op2):
- `NEG = 0x00` - `R(A) = -R(B)`
- `LEN = 0x01` - `R(A) = #R(B)`
- `BNOT = 0x10` - `R(A) = ~R(B)` (bitwise)
- `LNOT = 0x11` - `R(A) = not R(B)` (logical)

> Note: this is still effectively 1 opcode per one since we build the jump table from OP + OP2.

## Conditional Jump Ops (6 ops)

Jmp targets are relative to the next instruction.  So a jump of 0 would just continue to the next instruction.

| Op     | OP2 | A | B (2 bytes) | What is it?     |
|--------|-----|---|-------------|-----------------|
| JMP_EQ | ?   | r | r           | if R(A) == R(B) |
| JMP_NE | ?   | r | r           | if R(A) != R(B) |
| JMP_GE | ?   | r | r           | if R(A) >= R(B) |
| JMP_GT | ?   | r | r           | if R(A) > R(B)  |
| JMP_LE | ?   | r | r           | if R(A) <= R(B) |
| JMP_LT | ?   | r | r           | if R(A) < R(B)  |

Note: due to floating points and NaN we do need all these comparisons.

Each jump op uses the following variants for Op2:
- `REG = 0x00` - Both A and B are registers
- `IMM = 0x01` - A is a register, B is an immediate int (16 bits)
- `CST = 0x10` - A is a register, B is a constant lua int index
- `STR = 0x11` - A is a register, B is a constant string index

## Jump Ops (1 ops)

| Op  | OP2 | A | B (2 bytes) | What is it?    |
|-----|-----|---|-------------|----------------|
| JMP | ?   | r | i           | jump to A      |

Note: Op2 is unused for most ops right now.  But it's specified for JMP and that's as follows:

Op2 uses the following variants:
- `ABSOLUTE = 0x00` - Absolute jump
- `RELATIVE = 0x01` - Relative jump
- `LARGE = 0x10` - Large jump (32 bit offset, next instruction word)
- `RESERVED = 0x11` - Reserved for future use

JMP will close all `upvalues >= R(A - 1)`, if the value is 0 then no upvalues are closed.

There is a special JMP opcode that is specified after any comparison jump.  This is literally just a 32 bit jump position.
This can be specified by having OP2 = `LARGE` as well (in which case the next instruction word is used as the jump offset).
> In the future, we might extend LARGE to be 32 + 16 bits (that is use jump offset + next word) to allow for larger jumps.
> For now the jump offset is set to 0 **always** when using LARGE and the next word is used as the full 32 bit offset.

## Loops (2 ops)

We have 3 loop types that are used for iterating over tables and numeric ranges.

| Op        | OP2 | A | B (2 bytes) | What is it?                     |
|-----------|-----|---|-------------|---------------------------------|
| LOOP_NUM  | ?   | r | jmp         | for `i=R(A),R(A+1),R(A+2)`      |
| LOOP_ITER | ?   | r | jmp         | for `... in R(A),R(A+1),R(A+2)` |

> TODO: all loop control variables are actually `<const>` registers, this prevents modification during the loop.  This is configurable by disabling `ConstantLoopControlVariables` in the compiler options.  If you disable this then at the start of each loop iteration the "internal" loop control variables are copied to normal registers (which means that any modifications are lost).

### Numeric Loops

Numeric loops have the start/end/step values stored in slots from the register `R(A)`.  That is:
- `R(A)` - Current value
- `R(A+1)` - End value
- `R(A+2)` - Step value

Numeric loops have the following values for Op2:
- `NO_STEP = 0x?0` - Step value is `1`, no slot at `R(A+2)`
- `HAS_STEP = 0x?1` - Step value is `R(A+2)`
- Reserved bits `0x1?` - Reserved for future use

The loop num will do the following operation:
- `R(A) = R(A) + step`
- If `step > 0` and `R(A) > R(A+1)` then jump to B
- If `step < 0` and `R(A) < R(A+1)` then jump to B

The jump is a relative jump from the current instruction.

Note: that this loop instruction is placed at the end of the loop body (just before the jump back to the top of the loop).  The initial loop jump is done through a conditional jump (typically `JMP_LE` or `JMP_GE`) after initializing the loop control variables or through the `LOOP_NUM_INIT` opcode.

### Table For Loops

Similar, to numeric loops the table for loop uses registers starting from `R(A)` to store the iterator state.  This is as follows:
- `R(A)` - The iterator function
- `R(A+1)` - The state value (typically a table)
- `R(A+2)` - The control variable (typically the last key)

The loop iter op will do the following operation:
- Call `R(A)(R(A+1), R(A+2))`
- If the first return value is `nil` then jump to B
- Otherwise, store the return values starting from `R(A+2)`

We however, have some optimizations for common cases here.  This is done through the init opcodes for loops.  Which are shown below.

## Loop Initialization Ops (2 ops)

| Op             | OP2 | A | B (2 bytes) | What is it?                     |
|----------------|-----|---|-------------|---------------------------------|
| LOOP_NUM_INIT  | ?   | r | jmp         | Initialize numeric for loop     |
| LOOP_ITER_INIT | ?   | r | jmp         | Initialize table for loop       |

Initialization ops are placed at the start of the loop body.  They are built to optimize common cases.

Both are very similar, and the value of `B` is the offset of the `LOOP_NUM` or `LOOP_ITER` opcode that ends the loop body (though this is currently unused).  This then executes our core execution loop **but** in a new context, this is explained a bit better in the Bytecode Execution section.

## Bytecode Execution

Bytecode execution can be thought of as a big while loop that fetches and executes instructions until a return or error occurs.  A common problem with doing it this way is that we end up with lots of branching which can lead to poor CPU branch prediction performance.  A more advanced technique is to use direct threading (used in LuaJIT) which uses computed gotos to jump directly to the instruction handlers.  However, this is not natively supported in C#.

What is supported is using **tail calls** and a jump table to achieve a similar effect.  This involves creating a set of methods for each instruction handler and using a jump table to call the appropriate handler based on the current instruction.  By using tail calls, we can avoid adding additional stack frames, which helps to keep the call stack shallow and improves performance (since it'll effectively just be jump instructions).  This results in ideally **very** similar performance to direct threading.

TailCalls aren't available in C#, but they are in the CLR.  So we output IL that uses tail calls for our instruction handlers.  This is done through the use of the `tail.` IL prefix before the `call` instruction.

You can think of the bytecode execution loop as follows:

```csharp
// Note: we have a separate method for each op2, since op1 + op2 is used to build the jump table.
public void Op_ADD_rr_Int(ref ExecutionContext ctx, OpcodeInstruction instr)
{
    // Since it's just a hint we still need to do type checking.
    var b = ctx.Slots[instr.Math.B];
    var c = ctx.Slots[instr.Math.C];
    if (b.IsInteger() && c.IsInteger())
    {
        ctx.Slots[instr.Math.A] = LuaValue.FromInteger(b.Unsafe_Int() + c.Unsafe_Int());
        // inlined switch to next instruction
        TailCallNextInstruction(ctx);
        return;
    }

    // Fallback to general addition, again using tail call.
    // (Pseudo code)
    TailCall(Op_ADD_rr_Object, ctx, instr);
    return;
}
```

We use the library InlineIL.Fody to help us with IL generation for tail calls.  This allows us to write normal C# code and have the IL generated for us.

Note: the full 256 bit opcode table is fully generated at compile-time, some op2 values aren't used and so use a generic `InvalidOpCode` handler.

## UpValues (TODO)

I'm not convinced on the best way here.  Personally, I'm against open -> close upvalues.  I'm thinking of just having all mutable upvalues be references (i.e. always closed).  And having immutable upvalues be inlined copies (think of them as extra arguments).

In saying this it might be simpler to start with open/close upvalues first and then optimize later.
