using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    /// <summary>
    /// Tests for Lua closure functionality, verifying proper lexical scoping and variable capture.
    /// Closures in Lua capture variables from their enclosing scope and maintain access to them
    /// even after the enclosing function returns, which is critical for many Lua programming patterns.
    /// </summary>
    [TestFixture]
    [Category("VM.Integration")]
    public class ClosureTests
    {
        /// <summary>
        /// Tests closure capture of function parameters. Verifies that an inner function
        /// can access parameters from its enclosing function's scope.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void ClosureOnParam()
        {
            const string script =
                @"
				local function g (z)
				  local function f(a)
					return a + z;
				  end
				  return f;
				end

				return (g(3)(2));";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(5));
            });
        }

        /// <summary>
        /// Tests the behavior of Lua functions implemented using lambda expressions.
        /// Verifies that lambda functions capture variables correctly and can
        /// reference them within their scope. Additionally, ensures proper execution
        /// of nested lambda functions with parameter passing.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void LambdaFunctions()
        {
            const string script =
                @"
			g = |f, x|f(x, x+1)
			f = |x, y, z|x*(y+z)
			return g(|x,y|f(x,y,1), 2)";
            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(8));
            });
        }

        /// <summary>
        /// Tests the behavior of closures created using Lambda expressions in Lua.
        /// Confirms that the Lambda captures variables from its enclosing function and maintains
        /// the correct references to those variables.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void ClosureOnParamLambda()
        {
            const string script =
                @"
				local function g (z)
				  return |a| a + z
				end

				return (g(3)(2));";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(5));
            });
        }

        /// <summary>
        /// Validates functionality of closures in a scripting environment. Ensures that values from
        /// an outer scope are correctly captured and maintained over multiple function calls,
        /// even when those functions are stored in a dynamically created table.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void Closures()
        {
            // expected : 201 2001 20001 200001 2000001
            const string script =
                @"
						a = {}
						x = 0

						function container()
							local x = 20

							for i=1,5 do
								local y = 0
								a[i] = function () y=y+1; x = x * 10; return x+y end
							end
						end

						container();

						x = 4000

						return a[1](), a[2](), a[3](), a[4](), a[5]()";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(5));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[3].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[4].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(201));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(2001));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(20001));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(200001));
                Assert.That(res.Tuple[4].Number, Is.EqualTo(2000001));
            });
        }

        /// <summary>
        /// Tests the creation and behavior of closures within a non-anonymous, local context.
        /// Verifies that nested functions can capture and modify variables from their enclosing scopes,
        /// while preserving their local states across function calls.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void ClosuresNonAnonymousLocal()
        {
            // expected : 201 2001 20001 200001 2000001
            const string script =
                @"
						a = {}
						x = 0

						function container()
							local x = 20

							for i=1,5 do
								local y = 0
								local function zz() y=y+1; x = x * 10; return x+y end
								a[i] = zz;
							end
						end

						container();

						x = 4000

						return a[1](), a[2](), a[3](), a[4](), a[5]()";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(5));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[3].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[4].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(201));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(2001));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(20001));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(200001));
                Assert.That(res.Tuple[4].Number, Is.EqualTo(2000001));
            });
        }

        /// <summary>
        /// Validates the behavior of non-anonymous closures in Lua-like scripted loops.
        /// Ensures that inner functions maintain independent state for local variables captured
        /// within their respective scope during iteration.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void ClosuresNonAnonymous()
        {
            // expected : 201 2001 20001 200001 2000001
            const string script =
                @"
				a = {}
				x = 0

				function container()
					local x = 20

					for i=1,5 do
						local y = 0
						function zz() y=y+1; x = x * 10; return x+y end
						a[i] = zz;
					end
				end

				container();

				x = 4000

				return a[1](), a[2](), a[3](), a[4](), a[5]()";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(5));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[3].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[4].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(201));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(2001));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(20001));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(200001));
                Assert.That(res.Tuple[4].Number, Is.EqualTo(2000001));
            });
        }

        /// <summary>
        /// Tests closure behavior in the absence of tables by executing a nested function scenario.
        /// Verifies the correct closure capture, value transformations, and preservation of
        /// captured variables across multiple levels of nested functions and loops.
        /// Ensures the sequence and the resulting outputs match expected numeric transformations.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void ClosureNoTable()
        {
            const string script =
                @"
				x = 0

				function container()
					local x = 20

					for i=1,5 do
						local y = 0
		
						function zz() y=y+1; x = x * 10; return x+y end
		
						a1 = a2;
						a2 = a3;
						a3 = a4;
						a4 = a5;
						a5 = zz;
					end
				end

				container();

				x = 4000

				return a1(), a2(), a3(), a4(), a5()";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(5));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[3].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[4].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(201));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(2001));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(20001));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(200001));
                Assert.That(res.Tuple[4].Number, Is.EqualTo(2000001));
            });
        }

        /// <summary>
        /// Verifies the handling of Lua closures that capture and access upvalues
        /// (local variables from their enclosing scope), particularly in nested contexts.
        /// This test ensures that closures correctly bind to the expected variables
        /// even when they are defined within a nested function structure.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void NestedUpvalues()
        {
            const string script =
                @"
			local y = y;

			local x = 0;
			local m = { };

			function m:a()
				self.t = {
					dojob = function() 
						if (x == 0) then return 1; else return 0; end
					end,
				};
			end

			m:a();

			return 10 * m.t.dojob();";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        /// <summary>
        /// Verifies correct handling of nested closures in Lua where upvalues are accessed in
        /// scenarios where variables might go out of scope. Ensures closures maintain access
        /// to such variables, confirming the behavior expected in Lua.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void NestedOutOfScopeUpvalues()
        {
            const string script =
                @"

			function X()
				local y = y;

				local x = 0;
				local m = { };

				function m:a()
					self.t = {
						dojob = function() 
							if (x == 0) then return 1; else return 0; end
						end,
					};
				end

				return m;
			end

			Q = X();

			Q:a();

			return 10 * Q.t.dojob();";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        /// <summary>
        /// Tests local variable redefinition within a closure and its impact on captured variables.
        /// Verifies if closures correctly capture variables at the time of their definition
        /// and whether subsequent local redefinitions affect captured values. This ensures the
        /// interpreter adheres to proper lexical scoping and variable shadowing behavior.
        /// </summary>    [Category("VM.E2E")]
        [Test]
        public void LocalRedefinition()
        {
            const string script =
                @"

				result = ''

				local hi = 'hello'

				local function test()
					result = result .. hi;
				end

				test();

				hi = 'X'

				test();

				local hi = '!';

				test();

				return result;
								";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("helloXX"));
            });
        }
    }
}
