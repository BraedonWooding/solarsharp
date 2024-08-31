// This is just a simple playground for profiling
using Benchmark;
using Benchmark.Implementations;
using SolarSharp.Interpreter.DataTypes;

var file = new LuaFile("./Tests/binarytrees.lua-2.lua");
var itCount = 2000;

var impl = new SolarSharpImplementation();
impl.script.Globals.Set("A", (DynValue.NewCallback((ctx, arg) =>
{
    return DynValue.NewNumber(10);
    //return DynValue.NewYieldReq(new DynValue[1] { DynValue.NewNumber(10) });
    //return DynValue.NewTailCallReq(new TailCallData
    //{
    //    Args = new DynValue[0],
    //    Function = DynValue.NewCallback((ctx, arg) =>
    //    {
    //        return DynValue.NewYieldReq(new DynValue[1] { DynValue.NewNumber(10) });
    //    }),
    //    Continuation = new CallbackFunction((ctx, arg) =>
    //    {
    //        Console.WriteLine("CONT");
    //        return DynValue.NewNumber(20);
    //    })
    //});
})));

impl.Run(@"
    local x = coroutine.create(function()
        coroutine.yield(1)
        coroutine.yield(""A"")
        coroutine.yield(""B"")
    end)

    getmetatable('').__add = function(str,i)
        print(coroutine.resume(x))
        local _, y = coroutine.resume(x)
        str = str .. i
        str = str .. y
        return str
    end

    local str = ""test""
    print(str + ""er"")
");

//Console.WriteLine("Starting, type any key to continue");
//Console.ReadKey();

////// recommended you put your debugger here
////for (int i = 0; i < itCount; i++)
////{
////    impl.Run(file.Contents);
////}

//Console.WriteLine("Done");
