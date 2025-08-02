#if NET8_0_OR_GREATER
using Lua;
using Lua.Standard;

namespace Benchmark.Implementations
{
    public class LuaCSharpImplementation : AImplementation
    {
        private readonly LuaState state;

        public LuaCSharpImplementation()
        {
            state = LuaState.Create();
            state.OpenStandardLibraries();
        }

        public override AImplementation CreateFresh()
        {
            return new LuaCSharpImplementation();
        }

        public override void RegisterFunction(string v, Func<double, double, double> add)
        {
            state.Environment["add"] = new LuaFunction("add", (context, buffer, ct) =>
            {
                buffer.Span[0] = context.GetArgument<double>(0) + context.GetArgument<double>(1);
                return new(1);
            });
        }

        public override async Task<object> Run(string file)
        {
            return await state.DoStringAsync(file);
        }
    }
}
#endif