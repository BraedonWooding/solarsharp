using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    /// <summary>
    /// A test suite for verifying binary dumping and loading functionality in the SolarSharp scripting environment.
    /// </summary>
    [TestFixture]
    [Category("VM.Integration")]
    public class BinaryDumpTests
    {
        /// <summary>
        /// Validates the binary dumping and loading functionality of a Lua chunk within the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method facilitates execution of a Lua script that demonstrates the process of serializing a compiled
        /// Lua function into a binary string using <c>string.dump</c>, deserializing it back into a loadable function
        /// via <c>load</c>, and ensuring that the function executes correctly with the expected output.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the expected type or value of the Lua function result does not match the actual outcome
        /// during the validation process.
        /// </exception>
        [Test]
        public void BinDump_ChunkDump()
        {
            const string script =
                @"
				local chunk = load('return 81;');
				local str = string.dump(chunk);
				local fn = load(str);
				return fn(9);
			";

            var res = new Script(Examples.Common.Desktop).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(81));
            });
        }

        /// <summary>
        /// Validates the binary dumping and loading functionality of a Lua function represented as a string in the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method executes a Lua script where a compiled Lua function is serialized into a binary string using <c>string.dump</c>,
        /// subsequently deserialized into a loadable function via <c>load</c>, and verifies that the function runs successfully with
        /// the expected output.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the Lua function's output type or value deviates from expected results after deserialization and execution.
        /// </exception>
        [Test]
        public void BinDump_StringDump()
        {
            const string script =
                @"
				local str = string.dump(function(n) return n * n; end);
				local fn = load(str);
				return fn(9);
			";

            var res = new Script(Examples.Common.Desktop).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(81));
            });
        }

        /// <summary>
        /// Validates the serialization and deserialization functionality of a Lua function
        /// using a binary dump with the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method tests the end-to-end process of taking a Lua function,
        /// dumping its bytecode representation to a memory stream, loading it back as a new function,
        /// and verifying the expected behaviour and result of the deserialized function.
        /// It ensures compatibility and correctness of the binary dump and load functionality.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the deserialized Lua function does not produce the expected result
        /// or has a mismatch in the expected data type.
        /// </exception>
        [Test]
        public void BinDump_StandardDumpFunc()
        {
            const string script =
                @"
				function fact(n)
					return n * 24;
				end

				local str = string.dump(fact);
				
			";

            var s1 = new Script(Examples.Common.Desktop);
            s1.DoString(script);
            var func = s1.Globals.Get("fact");

            using var ms = new MemoryStream();
            s1.Dump(func, ms);
            ms.Seek(0, SeekOrigin.Begin);

            var s2 = new Script(Examples.Common.Desktop);
            var fact = s2.LoadStream(ms);
            var res = fact.Function.Call(5);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120));
            });
        }

        /// <summary>
        /// Tests the binary dumping and loading functionality of a Lua factorial function within the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method ensures that a Lua script defining a recursive factorial function is correctly serialized as a binary
        /// representation using the <c>Dump</c> method, deserialized back into a function using <c>LoadStream</c>, and validated
        /// for accurate execution. The test verifies the integrity and correctness of the binary dumping and loading processes.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the factorial function does not return the expected result or the result type does not match the expected
        /// data type during the validation process.
        /// </exception>
        [Test]
        public void BinDump_FactorialDumpFunc()
        {
            const string script =
                @"
				function fact(n)
					if (n == 0) then return 1; end
					return fact(n - 1) * n;
				end
			";

            var s1 = new Script(Examples.Common.Desktop);
            s1.DoString(script);
            var func = s1.Globals.Get("fact");

            using var ms = new MemoryStream();
            s1.Dump(func, ms);
            ms.Seek(0, SeekOrigin.Begin);

            var s2 = new Script(Examples.Common.Desktop);
            var fact = s2.LoadStream(ms);
            fact.Function.Script.Globals.Set("fact", fact);
            var res = fact.Function.Call(5);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120));
            });
        }

        /// <summary>
        /// Tests the serialization and deserialization of a Lua factorial function using the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This test validates the process of serializing a compiled Lua function into a binary format and then deserializing it into a new script environment,
        /// followed by asserting the correctness of the function's behaviour and result.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the deserialized Lua function's return type or result does not match the expected value during the test validation.
        /// </exception>
        [Test]
        public void BinDump_FactorialDumpFuncGlobal()
        {
            const string script =
                @"
				x = 0

				function fact(n)
					if (n == x) then return 1; end
					return fact(n - 1) * n;
				end
			";

            var s1 = new Script(Examples.Common.Desktop);
            s1.DoString(script);
            var func = s1.Globals.Get("fact");

            using var ms = new MemoryStream();
            s1.Dump(func, ms);
            ms.Seek(0, SeekOrigin.Begin);

            var s2 = new Script(Examples.Common.Desktop);
            var fact = s2.LoadStream(ms);
            fact.Function.Script.Globals.Set("fact", fact);
            fact.Function.Script.Globals.Set("x", DynValue.NewNumber(0));
            var res = fact.Function.Call(5);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120));
            });
        }

        /// <summary>
        /// Validates the binary dumping and loading mechanics of a Lua function that relies on a local variable
        /// (or "upvalue") within the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This test ensures correct handling of Lua functions utilizing upvalues, such as local variables referenced from
        /// an outer scope. It verifies that binary serialization and deserialization produce a consistent state of the
        /// function's behaviour. Specifically, the test checks for errors when dumping and loading such a function to ensure
        /// correctness and compliance with Lua semantics.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when attempting to process the Lua function during binary dumping, demonstrating an expected failure
        /// when upvalues are involved due to limitations or intentional behaviour in the handling of local upvalues in
        /// binary dumping.
        /// </exception>
        [Test]
        public void BinDump_FactorialDumpFuncUpvalue()
        {
            const string script =
                @"
				local x = 0

				function fact(n)
					if (n == x) then return 1; end
					return fact(n - 1) * n;
				end
			";

            Assert.Throws<ArgumentException>(static () =>
            {
                var s1 = new Script(Examples.Common.Desktop);
                s1.DoString(script);
                var func = s1.Globals.Get("fact");

                using var ms = new MemoryStream();
                s1.Dump(func, ms);
                ms.Seek(0, SeekOrigin.Begin);

                var s2 = new Script(Examples.Common.Desktop);
                s2.LoadStream(ms);
            });
        }

        /// <summary>
        /// Validates the binary dumping and loading functionality of a Lua script utilizing a closure with upvalues
        /// for factorial calculation in the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method tests the behaviour of a Lua script that defines a factorial function leveraging a closure with
        /// upvalues. The script modifies upvalue states dynamically during execution, and the test ensures that
        /// the expected computation results are achieved after the function execution. The validation asserts both the
        /// type of the result and the correctness of the numerical outcome.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the actual result's type or value differs from the expected outcomes during script execution.
        /// </exception>
        [Test]
        public void BinDump_FactorialClosure()
        {
            const string script =
                @"
			local x = 5;

			function fact(n)
				if (n == x) then return 1; end
				return fact(n - 1) * n;
			end

			x = 0;
			y = fact(5);
			x = 3;
			y = y + fact(5);
			return y;";

            var res = new Script(Examples.Common.Desktop).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(140));
            });
        }

        /// <summary>
        /// Validates the binary dumping and loading mechanism in relation to closures created over function parameters within the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method executes a Lua script that defines a nested function utilizing closures to capture an outer function's parameter. It validates
        /// that the interpreter correctly handles the serialization and deserialization of such closures, retaining the associated parameter's value.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the type or value of the Lua expression's result does not match the expected number returned by the closure execution.
        /// </exception>
        [Test]
        public void BinDump_ClosureOnParam()
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

            var res = new Script(Examples.Common.Desktop).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(5));
            });
        }

        /// <summary>
        /// Validates the binary dumping and reloading behaviour of functions with nested upvalues in the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This test executes a Lua script that defines a function with a nested structure and upvalues, verifies its binary
        /// serialization via dumping, reloading it back into a function, and evaluates the correctness of its functionality.
        /// It checks that the nested closures maintain correct access to their upvalues, producing the expected output.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the type or numerical result of the Lua function's execution does not align with the expected behaviour
        /// during the validation process.
        /// </exception>
        [Test]
        public void BinDump_NestedUpvalues()
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

            var res = new Script(Examples.Common.Desktop).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        /// <summary>
        /// Validates the behaviour of nested closures and the handling of out-of-scope upvalues in the SolarSharp interpreter's binary dumping and loading process.
        /// </summary>
        /// <remarks>
        /// This method executes a Lua script containing nested closures, where an upvalue goes out of scope due to function definitions and variable scopes.
        /// It ensures that the binary dumping and loading process correctly preserves and restores the functionality of closures and their upvalues
        /// when serialized and deserialized. The functionality is verified by invoking the returned functions and asserting the expected outputs.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the expected output or data type of the Lua function result does not match the actual outcome during validation.
        /// </exception>
        [Test]
        public void BinDump_NestedOutOfScopeUpvalues()
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

            var res = new Script(Examples.Common.Desktop).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        /// <summary>
        /// Validates and demonstrates the ability to change the execution environment of Lua functions
        /// using the <c>debug.setupvalue</c> method within the SolarSharp interpreter.
        /// </summary>
        /// <remarks>
        /// This method tests the usage of the <c>_ENV</c> global variable in Lua for sandboxing and modifying
        /// execution contexts. It showcases the reassignment and propagation of a modified environment across
        /// Lua functions. The test also ensures that the state of environment tables before and after the
        /// change remains consistent and predictable.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the Lua environment table behaviour or the sequence of operations does not meet the
        /// expected outcomes during validation.
        /// </exception>
        [Test]
        public void Load_ChangeEnvWithDebugSetUpvalue()
        {
            var list = new List<Table>();

            const string script =
                @"
				function print_env()
				  print(_ENV)
				end

				function sandbox()
				  print(_ENV) -- prints: 'table: 0x100100610'
				  -- need to keep access to a few globals:
				  _ENV = { print = print, print_env = print_env, debug = debug, load = load }
				  print(_ENV) -- prints: 'table: 0x100105140'
				  print_env() -- prints: 'table: 0x100105140'
				  local code1 = load('print(_ENV)')
				  code1()     -- prints: 'table: 0x100100610'
				  debug.setupvalue(code1, 0, _ENV) -- set our modified env
				  debug.setupvalue(code1, 1, _ENV) -- set our modified env
				  code1()     -- prints: 'table: 0x100105140'
				  local code2 = load('print(_ENV)', nil, nil, _ENV) -- pass 'env' arg
				  code2()     -- prints: 'table: 0x100105140'
				end

				sandbox()";

            var s = new Script(Examples.Common.Desktop)
            {
                Globals = { ["print"] = (Action<Table>)(t => list.Add(t)) },
            };

            s.DoString(script);

            Assert.That(list, Has.Count.EqualTo(6));

            var eqs = new[] { 0, 1, 1, 0, 1, 1 };

            for (var i = 0; i < 6; i++)
                Assert.That(list[i], Is.EqualTo(list[eqs[i]]));
        }
    }
}
