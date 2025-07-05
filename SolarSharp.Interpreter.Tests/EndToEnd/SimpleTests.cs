using System;
using System.Collections.Generic;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [Category("IntegrationTest")]
    public class SimpleTests
    {
        [Test]
        public void EmptyLongComment()
        {
            Script S = new();
            var res = S.DoString("--[[]]");
        }


        [Test]
        public void EmptyChunk()
        {
            Script S = new();
            var res = S.DoString("");
        }

        [Test]
        public void CSharpStaticFunctionCallStatement()
        {
            IList<DynValue> args = null;

            var script = "print(\"hello\", \"world\");";

            Script S = new();

            S.Globals.Set("print", DynValue.NewCallback(new CallbackFunction((x, a) =>
            {
                args = a.GetArray();
                return DynValue.NewNumber(1234.0);
            })));

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Void));
                Assert.That(args.Count, Is.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(args[0].Type, Is.EqualTo(DataType.String));
                Assert.That(args[0].String, Is.EqualTo("hello"));
                Assert.That(args[1].Type, Is.EqualTo(DataType.String));
                Assert.That(args[1].String, Is.EqualTo("world"));
            });
        }

        [Test]
        public void CSharpStaticFunctionCallRedef()
        {
            IList<DynValue> args = null;

            var script = "local print = print; print(\"hello\", \"world\");";

            var S = new Script();
            S.Globals.Set("print", DynValue.NewCallback(new CallbackFunction((_x, a) => { args = a.GetArray(); return DynValue.NewNumber(1234.0); })));

            var res = S.DoString(script);

            Assert.That(args.Count, Is.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(args[0].Type, Is.EqualTo(DataType.String));
                Assert.That(args[0].String, Is.EqualTo("hello"));
                Assert.That(args[1].Type, Is.EqualTo(DataType.String));
                Assert.That(args[1].String, Is.EqualTo("world"));
                Assert.That(res.Type, Is.EqualTo(DataType.Void));
            });
        }

        [Test]
        public void CSharpStaticFunctionCall4()
        {
            var script = "return callback()();";

            var callback2 = DynValue.NewCallback(new CallbackFunction((_x, a) => { return DynValue.NewNumber(1234.0); }));
            var callback = DynValue.NewCallback(new CallbackFunction((_x, a) => { return callback2; }));

            var S = new Script();
            S.Globals.Set("callback", callback);

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1234.0));
            });
        }

        [Test]
        public void CSharpStaticFunctionCall3()
        {
            var script = "return callback();";

            var callback = DynValue.NewCallback(new CallbackFunction((_x, a) => { return DynValue.NewNumber(1234.0); }));

            var S = new Script();
            S.Globals.Set("callback", callback);

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1234.0));
            });
        }

        [Test]
        public void CSharpStaticFunctionCall2()
        {
            IList<DynValue> args = null;

            var script = "return callback 'hello';";

            var S = new Script();
            S.Globals.Set("callback", DynValue.NewCallback(new CallbackFunction((_x, a) => { args = a.GetArray(); return DynValue.NewNumber(1234.0); })));

            var res = S.DoString(script);

            Assert.That(args.Count, Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(args[0].Type, Is.EqualTo(DataType.String));
                Assert.That(args[0].String, Is.EqualTo("hello"));
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1234.0));
            });
        }

        [Test]
        public void CSharpStaticFunctionCall()
        {
            IList<DynValue> args = null;

            var script = "return print(\"hello\", \"world\");";

            var S = new Script();
            S.Globals.Set("print", DynValue.NewCallback(new CallbackFunction((_x, a) => { args = a.GetArray(); return DynValue.NewNumber(1234.0); })));

            var res = S.DoString(script);

            Assert.That(args.Count, Is.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(args[0].Type, Is.EqualTo(DataType.String));
                Assert.That(args[0].String, Is.EqualTo("hello"));
                Assert.That(args[1].Type, Is.EqualTo(DataType.String));
                Assert.That(args[1].String, Is.EqualTo("world"));
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1234.0));
            });
        }

        [Test]
        //!!! DO NOT REFORMAT THIS METHOD !!!
        public void LongStrings()
        {
            var script = @"    
				x = [[
					ciao
				]];

				y = [=[ [[uh]] ]=];

				z = [===[[==[[=[[[eheh]]=]=]]===]

				return x,y,z";

            var res = new Script().DoString(script);

            Assert.That(res.Tuple, Has.Length.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[0].String, Is.EqualTo("\t\t\t\t\tciao\n\t\t\t\t"));
                Assert.That(res.Tuple[1].String, Is.EqualTo(" [[uh]] "));
                Assert.That(res.Tuple[2].String, Is.EqualTo("[==[[=[[[eheh]]=]=]"));
            });
        }

        [Test]
        public void UnicodeEscapeLua53Style()
        {
            var script = @"    
				x = 'ciao\u{41}';
				return x;";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("ciaoA"));
            });
        }

        [Test]
        public void InvalidEscape()
        {
            var script = @"    
				x = 'ciao\k{41}';
				return x;";

            Assert.Throws<SyntaxErrorException>(() => new Script().DoString(script));
        }

        [Test]
        public void KeywordsInStrings()
        {
            var keywrd = "and break do else elseif end false end for function end goto if ::in:: in local nil not [or][[][==][[]] repeat return { then 0 end return; }; then true (x != 5 or == * 3 - 5) x";

            var script = string.Format(@"    
				x = '{0}';
				return x;", keywrd);

            var res = new Script().DoString(script);
            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo(keywrd));
            });
        }

        [Test]
        public void ParserErrorMessage()
        {
            var caught = false;
            var script = @"    
				return 'It's a wet floor warning saying wheat flour instead. \
				Probably, the cook thought it was funny. \
				He was wrong.'";

            try
            {
                var res = new Script().DoString(script);
            }
            catch (SyntaxErrorException ex)
            {
                caught = true;
                Assert.That(ex.Message, Is.Not.Null.And.Not.Empty);
            }

            Assert.That(caught, Is.True);
        }

        [Test]
        public void StringsWithBackslashLineEndings2()
        {
            var script = @"    
				return 'a\
				b\
				c'";

            var res = new Script().DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.String));
        }

        [Test]
        public void StringsWithBackslashLineEndings()
        {
            var script = @"    
				return 'It is a wet floor warning saying wheat flour instead. \
				Probably, the cook thought it was funny. \
				He was wrong.'";

            var res = new Script().DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.String));
        }

        [Test]
        public void FunctionCallWrappers()
        {
            var script = @"    
				function boh(x) 
					return 1912 + x;
				end
			";

            Script s = new();
            s.DoString(script);

            var res = s.Globals.Get("boh").Function.Call(82);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1994));
            });
        }


        [Test]
        public void ReturnSimpleUnop()
        {
            var script = @"return -42";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(-42));
            });
        }

        [Test]
        public void ReturnSimple()
        {
            var script = @"return 42";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(42));
            });
        }


        [Test]
        public void OperatorSimple()
        {
            var script = @"return 6*7";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(42));
            });
        }


        [Test]
        public void SimpleBoolShortCircuit()
        {
            var script = @"    
				x = true or crash();
				y = false and crash();
			";

            Script S = new();
            S.Globals.Set("crash", DynValue.NewCallback(new CallbackFunction((_x, a) =>
            {
                throw new Exception("FAIL!");
            })));

            S.DoString(script);
        }

        [Test]
        public void FunctionOrOperator()
        {
            var script = @"    
				loadstring = loadstring or load;

				return loadstring;
			";

            Script S = new();
            var res = S.DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.ClrFunction));

        }


        [Test]
        public void SelectNegativeIndex()
        {
            var script = @"    
				return select(-1,'a','b','c');
			";

            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("c"));
            });
        }





        [Test]
        public void BoolConversionAndShortCircuit()
        {
            var script = @"    
				i = 0;

				function f()
					i = i + 1;
					return '!';
				end					
				
				x = false;
				y = true;

				return false or f(), true or f(), false and f(), true and f(), i";

            var res = new Script().DoString(script);

            Assert.That(res.Tuple, Has.Length.EqualTo(5));
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Boolean));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Boolean));
                Assert.That(res.Tuple[3].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[4].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].String, Is.EqualTo("!"));
                Assert.That(res.Tuple[1].Boolean, Is.EqualTo(true));
                Assert.That(res.Tuple[2].Boolean, Is.EqualTo(false));
                Assert.That(res.Tuple[3].String, Is.EqualTo("!"));
                Assert.That(res.Tuple[4].Number, Is.EqualTo(2));
            });
        }
        [Test]
        public void HanoiTowersDontCrash()
        {
            var script = @"
			function move(n, src, dst, via)
				if n > 0 then
					move(n - 1, src, via, dst)
					move(n - 1, via, dst, src)
				end
			end
 
			move(4, 1, 2, 3)
			";

            var res = new Script().DoString(script);
        }

        [Test]
        public void Factorial()
        {
            var script = @"    
				-- defines a factorial function
				function fact (n)
					if (n == 0) then
						return 1
					else
						return n*fact(n - 1)
					end
				end
    
				return fact(5)";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120.0));
            });
        }

        [Test]
        public void IfStatmWithScopeCheck()
        {
            var script = @"    
				x = 0

				if (x == 0) then
					local i = 3;
					x = i * 2;
				end
    
				return i, x";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Nil));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void ScopeBlockCheck()
        {
            var script = @"    
				local x = 6;
				
				do
					local i = 33;
				end
		
				return i, x";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Nil));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void ForLoopWithBreak()
        {
            var script = @"    
				x = 0

				for i = 1, 10 do
					x = i
					break;
				end
    
				return x";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1));
            });
        }


        [Test]
        public void ForEachLoopWithBreak()
        {
            var script = @"    
				x = 0
				y = 0

				t = { 2, 4, 6, 8, 10, 12 };

				function iter (a, ii)
				  ii = ii + 1
				  local v = a[ii]
				  if v then
					return ii, v
				  end
				end
    
				function ipairslua (a)
				  return iter, a, 0
				end

				for i,j in ipairslua(t) do
					x = x + i
					y = y + j

					if (i >= 3) then
						break
					end
				end
    
				return x, y";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(6));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(12));
            });
        }


        [Test]
        public void ForEachLoop()
        {
            var script = @"    
				x = 0
				y = 0

				t = { 2, 4, 6, 8, 10, 12 };

				function iter (a, ii)
				  ii = ii + 1
				  local v = a[ii]
				  if v then
					return ii, v
				  end
				end
    
				function ipairslua (a)
				  return iter, a, 0
				end

				for i,j in ipairslua(t) do
					x = x + i
					y = y + j
				end
    
				return x, y";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(21));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(42));
            });
        }

        [Test]
        public void LengthOperator()
        {
            var script = @"    
				x = 'ciao'
				y = { 1, 2, 3 }
   
				return #x, #y";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(4));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(3));
            });
        }


        [Test]
        public void ForLoopWithBreakAndScopeCheck()
        {
            var script = @"    
				x = 0

				for i = 1, 10 do
					x = x + i

					if (i == 3) then
						break
					end
				end
    
				return i, x";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Nil));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void FactorialWithOneReturn()
        {
            var script = @"    
				-- defines a factorial function
				function fact (n)
					if (n == 0) then
						return 1
					end
					return n*fact(n - 1)
				end
    
				return fact(5)";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120.0));
            });
        }

        [Test]
        public void VeryBasic()
        {
            var script = @"return 7";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(7));
            });
        }

        [Test]
        public void OperatorPrecedence1()
        {
            var script = @"return 1+2*3";

            Script s = new();
            var res = s.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(7));
            });
        }
        [Test]
        public void OperatorPrecedence2()
        {
            var script = @"return 2*3+1";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(7));
            });
        }

        [Test]
        public void OperatorAssociativity()
        {
            var script = @"return 2^3^2";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(512));
            });
        }

        [Test]
        public void OperatorPrecedence3()
        {
            var script = @"return 5-3-2";
            Script S = new();

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(0));
            });
        }

        [Test]
        public void OperatorPrecedence4()
        {
            var script = @"return 3 + -1";
            Script S = new();

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(2));
            });
        }

        [Test]
        public void OperatorPrecedence5()
        {
            var script = @"return 3 * -1 + 5 * 3";
            Script S = new();

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(12));
            });
        }

        [Test]
        public void OperatorPrecedence6()
        {
            var script = @"return -2^2";
            Script S = new();

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(-4));
            });
        }

        [Test]
        public void OperatorPrecedence7()
        {
            var script = @"return -7 / 0.5";
            Script S = new();

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(-14));
            });
        }

        [Test]
        public void OperatorPrecedenceAndAssociativity()
        {
            var script = @"return 5+3*7-2*5+2^3^2";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(528));
            });
        }

        [Test]
        public void OperatorParenthesis()
        {
            var script = @"return (5+3)*7-2*5+(2^3)^2";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(110));
            });
        }

        [Test]
        public void GlobalVarAssignment()
        {
            var script = @"x = 1; return x;";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1));
            });
        }
        [Test]
        public void TupleAssignment1()
        {
            var script = @"    
				function y()
					return 2, 3
				end

				function x()
					return 1, y()
				end

				w, x, y, z = 0, x()
    
				return w+x+y+z";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void IterativeFactorialWithWhile()
        {
            var script = @"    
				function fact (n)
					local result = 1;
					while(n > 0) do
						result = result * n;
						n = n - 1;
					end
					return result;
				end
    
				return fact(5)";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120.0));
            });
        }



        [Test]
        public void IterativeFactorialWithRepeatUntilAndScopeCheck()
        {
            var script = @"    
				function fact (n)
					local result = 1;
					repeat
						local checkscope = 1;
						result = result * n;
						n = n - 1;
					until (n == 0 and checkscope == 1)
					return result;
				end
    
				return fact(5)";

            Script s = new();
            var res = s.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120.0));
            });
        }

        [Test]

        public void SimpleForLoop()
        {
            var script = @"    
					x = 0
					for i = 1, 3 do
						x = x + i;
					end

					return x;
			";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6.0));
            });
        }

        [Test]
        public void SimpleFunc()
        {
            var script = @"    
				function fact (n)
					return 3;
				end
    
				return fact(3)";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(3));
            });
        }

        [Test]
        public void IterativeFactorialWithFor()
        {
            var script = @"    
				-- defines a factorial function
				function fact (n)
					x = 1
					for i = n, 1, -1 do
						x = x * i;
					end

					return x;
				end
    
				return fact(5)";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120.0));
            });
        }


        [Test]
        public void LocalFunctionsObscureScopeRule()
        {
            var script = @"    
				local function fact()
					return fact;
				end

				return fact();
				";

            var res = new Script().DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.Function));
        }

        [Test]
        public void FunctionWithStringArg2()
        {
            var script = @"    
				x = 0;

				fact = function(y)
					x = y
				end

				fact 'ciao';

				return x;
				";


            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("ciao"));
            });
        }

        [Test]
        public void FunctionWithStringArg()
        {
            var script = @"    
				x = 0;

				function fact(y)
					x = y
				end

				fact 'ciao';

				return x;
				";


            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("ciao"));
            });

        }

        [Test]
        public void FunctionWithTableArg()
        {
            var script = @"    
				x = 0;

				function fact(y)
					x = y
				end

				fact { 1,2,3 };

				return x;
				";


            var res = new Script().DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.Table));

        }


        [Test]
        public void TupleAssignment2()
        {
            var script = @"    
				function boh()
					return 1, 2;
				end

				x,y,z = boh(), boh()

				return x,y,z;
				";


            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(1));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(1));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(2));
            });
        }
        [Test]
        public void LoopWithReturn()
        {
            var script = @"function Allowed( )
									for i = 1, 20 do
  										return false 
									end
									return true
								end
						Allowed();
								";

            var res = new Script().DoString(script);

        }
        [Test]
        public void IfWithLongExpr()
        {
            var script = @"function Allowed( )
									for i = 1, 20 do
									if ( false ) or ( true and true ) or ( 7+i <= 9 and false ) then 
  										return false 
									end
									end		
									return true
								end
						Allowed();
								";

            var res = new Script().DoString(script);

        }

        [Test]
        public void IfWithLongExprTbl()
        {
            var script = @"
						t = { {}, {} }
						
						function Allowed( )
									for i = 1, 20 do
									if ( t[1][3] ) or ( i <= 17 and t[1][1] ) or ( 7+i <= 9 and t[1][1] ) then 
  										return false 
									end
									end		
									return true
								end
						Allowed();
								";

            var res = new Script().DoString(script);

        }

        [Test]
        public void ExpressionReducesTuples()
        {
            var script = @"
					function x()
						return 1,2
					end

					do return (x()); end
					do return x(); end
								";

            var config = new SecurityConfiguration
            {
	            AllowedModules = CoreModules.None
            };
            var res = new Script(config).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1));
            });
        }


        [Test]
        public void ExpressionReducesTuples2()
        {
            var script = @"
					function x()
						return 3,4
					end

					return 1,x(),x()
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(4));
            });
        }


        [Test]
        public void ArgsDoNotChange()
        {
            var script = @"
					local a = 1;
					local b = 2;

					function x(c, d)
						c = c + 3;
						d = d + 4;
						return c + d;
					end

					return x(a, b+1), a, b;
								";

            var res = new Script().DoString(script);

            Assert.That(res.Tuple, Has.Length.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(11));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(1));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(2));
            });
        }


        [Test]
        public void VarArgsNoError()
        {
            var script = @"
					function x(...)

					end

					function y(a, ...)

					end

					return 1;
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1));
            });
        }

        [Test]
        public void VarArgsSum()
        {
            var script = @"
					function x(...)
						local t = pack(...);
						local sum = 0;

						for i = 1, #t do
							sum = sum + t[i];
						end
	
						return sum;
					end

					return x(1,2,3,4);
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        [Test]
        public void VarArgsSum2()
        {
            var script = @"
					function x(m, ...)
						local t = pack(...);
						local sum = 0;

						for i = 1, #t do
							sum = sum + t[i];
						end
	
						return sum * m;
					end

					return x(5,1,2,3,4);
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(50));
            });
        }

        [Test]
        public void VarArgsSumTb()
        {
            var script = @"
					function x(...)
						local t = {...};
						local sum = 0;

						for i = 1, #t do
							sum = sum + t[i];
						end
	
						return sum;
					end

					return x(1,2,3,4);
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        [Test]
        public void SwapPattern()
        {
            var script = @"
					local n1 = 1
					local n2 = 2
					local n3 = 3
					local n4 = 4
					n1,n2,n3,n4 = n4,n3,n2,n1

					return n1,n2,n3,n4;
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(4));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(4));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(3));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(2));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(1));
            });
        }

        [Test]
        public void SwapPatternGlobal()
        {
            var script = @"
					n1 = 1
					n2 = 2
					n3 = 3
					n4 = 4
					n1,n2,n3,n4 = n4,n3,n2,n1

					return n1,n2,n3,n4;
								";

            var res = new Script().DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(4));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Number, Is.EqualTo(4));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(3));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(2));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(1));
            });
        }

        [Test]
        public void EnvTestSuite()
        {
            var script = @"
				local RES = { }

				RES.T1 = (_ENV == _G) 

				a = 1

				local function f(t)
				  local _ENV = t 

				  RES.T2 = (getmetatable == nil) 
  
				  a = 2 -- create a new entry in t, doesn't touch the original 'a' global
				  b = 3 -- create a new entry in t
				end

				local t = {}
				f(t)

				RES.T3 = a;
				RES.T4 = b;
				RES.T5 = t.a;
				RES.T6 = t.b;

				return RES;
								";

            var res = new Script().DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.Table));

            var T = res.Table;

            Assert.Multiple(() =>
            {
                Assert.That(T.Get("T1").Type, Is.EqualTo(DataType.Boolean), "T1-Type");
                Assert.That(T.Get("T1").Boolean, Is.EqualTo(true), "T1-Val");

                Assert.That(T.Get("T2").Type, Is.EqualTo(DataType.Boolean), "T2-Type");
                Assert.That(T.Get("T2").Boolean, Is.EqualTo(true), "T2-Val");

                Assert.That(T.Get("T3").Type, Is.EqualTo(DataType.Number), "T3-Type");
                Assert.That(T.Get("T3").Number, Is.EqualTo(1), "T3-Val");

                Assert.That(T.Get("T4").Type, Is.EqualTo(DataType.Nil), "T4");

                Assert.That(T.Get("T5").Type, Is.EqualTo(DataType.Number), "T5-Type");
                Assert.That(T.Get("T5").Number, Is.EqualTo(2), "T5-Val");

                Assert.That(T.Get("T6").Type, Is.EqualTo(DataType.Number), "T6-Type");
                Assert.That(T.Get("T6").Number, Is.EqualTo(3), "T6-Val");
            });
        }

        [Test]
        public void TupleToOperator()
        {
            var script = @"    
				function x()
					return 3, 'xx';
				end

				return x() == 3;	
			";

            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Boolean));
                Assert.That(res.Boolean, Is.EqualTo(true));
            });
        }


        [Test]
        public void LiteralExpands()
        {
            var script = @"    
				x = 'a\65\66\67z';
				return x;	
			";

            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("aABCz"));
            });
        }

        [Test]
        public void HomonymArguments()
        {
            var script = @"    
				function test(_,value,_) return _; end

				return test(1, 2, 3);	
			";

            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(3));
            });
        }

        [Test]
        public void VarArgsSumMainChunk()
        {
            var script = @"
					local t = pack(...);
					local sum = 0;

					for i = 1, #t do
						sum = sum + t[i];
					end
	
					return sum;
								";

            var fn = new Script().LoadString(script);

            var res = fn.Function.Call(1, 2, 3, 4);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        [Test]
        public void VarArgsInNoVarArgsReturnsError()
        {
            var script = @"
					function x()
						local t = {...};
						local sum = 0;

						for i = 1, #t do
							sum = sum + t[i];
						end
	
						return sum;
					end

					return x(1,2,3,4);
								";

            Assert.Throws<SyntaxErrorException>(() => new Script().DoString(script));
        }

        [Test]
        public void HexFloats_1()
        {
            var script = "return 0x0.1E";
            var result = new Script().DoString(script);
            Assert.That(result.Number, Is.EqualTo(0x1E / (double)0x100));
        }

        [Test]
        public void HexFloats_2()
        {
            var script = "return 0xA23p-4";
            var result = new Script().DoString(script);
            Assert.That(result.Number, Is.EqualTo(0xA23 / 16.0));
        }

        [Test]
        public void HexFloats_3()
        {
            var script = "return 0X1.921FB54442D18P+1";
            var result = new Script().DoString(script);
            Assert.That(result.Number, Is.EqualTo((1 + 0x921FB54442D18 / (double)0x10000000000000) * 2));
        }

        [Test]
        public void Simple_Delegate_Interop_1()
        {
            var a = 3;
            var script = new Script
            {
	            Globals =
	            {
		            ["action"] = new Action(() => a = 5)
	            }
            };
            script.DoString("action()");
            Assert.That(a, Is.EqualTo(5));
        }

        [Test]
        public void Simple_Delegate_Interop_2()
        {
            var oldPolicy = UserData.RegistrationPolicy;

            try
            {
                UserData.RegistrationPolicy = Interop.InteropRegistrationPolicy.Automatic;

                var a = 3;
                var script = new Script
                {
	                Globals =
	                {
		                ["action"] = new Action(() => a = 5)
	                }
                };
                script.DoString("action()");
                Assert.That(a, Is.EqualTo(5));
            }
            finally
            {
                UserData.RegistrationPolicy = oldPolicy;
            }
        }

        [Test]
        public void MissingArgsDefaultToNil()
        {
            Script S = new();
            var res = S.DoString(@"
				function test(a)
					return a;
				end

				test();
				");
        }

        [Test]
        public void ParsingTest()
        {
            Script S = new();
            var res = S.LoadString(@"
				t = {'a', 'b', 'c', ['d'] = 'f', ['e'] = 5, [65] = true, [true] = false}
				function myFunc()
				  return 'one', 'two'
				end

				print('Table Test 1:')
				for k,v in pairs(t) do
				  print(tostring(k) .. ' / ' .. tostring(v))
				end
				print('Table Test 2:')
				for X,X in pairs(t) do
				  print(tostring(X) .. ' / ' .. tostring(X))
				end
				print('Function Test 1:')
				v1,v2 = myFunc()
				print(v1)
				print(v2)
				print('Function Test 2:')
				v,v = myFunc()
				print(v)
				print(v)
				");
        }

        //		[Test]
        //		public void TestModulesLoadingWithoutCrash()
        //		{
        //#if !PCL
        //			var basePath = AppDomain.CurrentDomain.BaseDirectory;
        //			var scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts\\test");
        //			Script script = new Script();

        //			((ScriptLoaderBase)script.Options.ScriptLoader).ModulePaths = new[]
        //			{
        //				System.IO.Path.Combine(basePath, "scripts\\test\\test.lua"),
        //			};
        //			var obj = script.LoadFile(System.IO.Path.Combine(scriptPath, "test.lua"));
        //			obj.Function.Call();
        //#endif
        //		}

        [Test]
        public void NumericConversionFailsIfOutOfBounds()
        {
            Script S = new()
            {
	            Globals =
	            {
		            ["my_function_takes_byte"] = (Action<byte>)(p => { })
	            }
            };

            try
            {
                S.DoString("my_function_takes_byte(2010191) -- a huge number that is definitely not a byte");

                Assert.Fail(); // ScriptRuntimeException should have been thrown, if it doesn't Assert.Fail should execute
            }
            catch (ScriptRuntimeException e)
            {
                Assert.Pass(e.DecoratedMessage);
            }
        }
    }
}
