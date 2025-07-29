// This is just a simple playground for profiling

using Benchmark;
using Benchmark.Implementations;

var file = new LuaFile("./Tests/empty_test.lua");

var impl = new SolarSharpImplementation();
//impl.Run(file.Contents);

//impl.script.Globals.Set("A", (LuaValue.NewCallback((ctx, arg) =>
//{
//    return LuaValue.NewNumber(10);
//    //return LuaValue.NewYieldReq(new LuaValue[1] { LuaValue.NewNumber(10) });
//    //return LuaValue.NewTailCallReq(new TailCallData
//    //{
//    //    Args = new LuaValue[0],
//    //    Function = LuaValue.NewCallback((ctx, arg) =>
//    //    {
//    //        return LuaValue.NewYieldReq(new LuaValue[1] { LuaValue.NewNumber(10) });
//    //    }),
//    //    Continuation = new CallbackFunction((ctx, arg) =>
//    //    {
//    //        Console.WriteLine("CONT");
//    //        return LuaValue.NewNumber(20);
//    //    })
//    //});
//})));

//impl.Run(@"
//    local x = coroutine.create(function()
//        coroutine.yield(1)
//        coroutine.yield(""A"")
//        coroutine.yield(""B"")
//    end)

//    getmetatable('').__add = function(str,i)
//        print(coroutine.resume(x))
//        local _, y = coroutine.resume(x)
//        str = str .. i
//        str = str .. y
//        return str
//    end

//    local str = ""test""
//    print(str + ""er"")
//");

//Console.WriteLine("Starting, type any key to continue");
//Console.ReadKey();

// recommended you put your debugger here
impl.Run(file.Contents);

Console.WriteLine("Done");