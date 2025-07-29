using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class ErrorHandlingTests
    {
        [Test]
        public void PCallMultipleReturns()
        {
            var script = "return pcall(function() return 1,2,3 end)";

            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(4));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Boolean, Is.EqualTo(true));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(1));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(2));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(3));
            });
        }

        [Test]
        public void Errors_PCall_ClrFunction()
        {
            var script = @"
				r, msg = pcall(assert, false, 'catched')
				return r, msg;
								";

            var res = Script.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Boolean));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[0].Boolean, Is.EqualTo(false));
            });
        }

        [Test]
        public void Errors_PCall_Multiples()
        {
            var script = @"
function try(fn)
	local x, y = pcall(fn)
	
	if (x) then
		return y
	else
		return '!'
	end
end

function a()
	return try(b) .. 'a';
end

function b()
	return try(c) .. 'b';
end

function c()
	return try(d) .. 'c';
end

function d()
	local t = { } .. 'x'
end


return a()
";

            var res = Script.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("!cba"));
            });
        }

        [Test]
        public void Errors_TryCatch_Multiples()
        {
            var script = @"
function a()
	return try(b) .. 'a';
end

function b()
	return try(c) .. 'b';
end

function c()
	return try(d) .. 'c';
end

function d()
	local t = { } .. 'x'
end


return a()
";
            Script S = new(CoreModules.None)
            {
                Globals =
                {
                    ["try"] = LuaValue.NewCallback((_, a) =>
                    {
                        try
                        {
                            var v = a[0].Function.Call();
                            return v;
                        }
                        catch (ScriptRuntimeException)
                        {
                            return LuaValue.NewString("!");
                        }
                    })
                }
            };


            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("!cba"));
            });
        }
    }
}