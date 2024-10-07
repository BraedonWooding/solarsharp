using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using NUnit.Framework;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class TableTests
    {
        [Test]
        public void TableAccessAndEmptyCtor()
        {
            string script = @"
						a = { }
						
						a[1] = 1;

						return a[1]";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1));
            });
        }

        [Test]
        public void TableAccessAndCtor()
        {
            string script = @"
						a = { 55, 2, 3, aurevoir=6, [false] = 7 }
						
						a[1] = 1;
						a.ciao = 4;
						a['hello'] = 5;

						return a[1], a[2], a[3], a['ciao'], a.hello, a.aurevoir, a[false]";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(7));
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[3].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[4].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[5].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[6].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(1));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(2));
                Assert.That(res.Tuple[2].Number, Is.EqualTo(3));
                Assert.That(res.Tuple[3].Number, Is.EqualTo(4));
                Assert.That(res.Tuple[4].Number, Is.EqualTo(5));
                Assert.That(res.Tuple[5].Number, Is.EqualTo(6));
                Assert.That(res.Tuple[6].Number, Is.EqualTo(7));
            });
        }

        [Test]
        public void TableMethod1()
        {
            string script = @"
						x = 0
	
						a = 
						{ 
							value = 1912,

							val = function(self, num)
								x = self.value + num
							end
						}
						
						a.val(a, 82);

						return x";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1994));
            });
        }

        [Test]
        public void TableMethod2()
        {
            string script = @"
						x = 0
	
						a = 
						{ 
							value = 1912,

							val = function(self, num)
								x = self.value + num
							end
						}
						
						a:val(82);

						return x";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1994));
            });
        }

        [Test]
        public void TableMethod3()
        {
            string script = @"
						x = 0
	
						a = 
						{ 
							value = 1912,
						}

						function a.val(self, num)
							x = self.value + num
						end
						
						a:val(82);

						return x";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1994));
            });
        }


        [Test]
        public void TableMethod4()
        {
            string script = @"
						x = 0
	
						local a = 
						{ 
							value = 1912,
						}

						function a:val(num)
							x = self.value + num
						end
						
						a:val(82);

						return x";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1994));
            });
        }

        [Test]
        public void TableMethod5()
        {
            string script = @"
						x = 0

						a = 
						{ 
							value = 1912,
						}

						b = { tb = a };
						c = { tb = b };

						function c.tb.tb:val(num)
							x = self.value + num
						end
						
						a:val(82);

						return x";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(1994));
            });
        }


        [Test]
        public void TableMethod6()
        {
            string script = @"
						do
						  local a = {x=0}
						  function a:add (x) self.x, a.y = self.x+x, 20; return self end
						  return (a:add(10):add(20):add(30).x == 60 and a.y == 20)
						end";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Boolean));
                Assert.That(res.CastToBool(), Is.EqualTo(true));
            });
        }

        [Test]
        public void TableNextWithChangeInCollection()
        {
            string script = @"
				x = { }

				function copy(k, v)
					x[k] = v;
				end

				t = 
				{
					a = 1,
					b = 2,
					c = 3,
					d = 4,
					e = 5
				}

				k,v = next(t, nil);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);
				v = nil;

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

				s = x.a .. '|' .. x.b .. '|' .. x.c .. '|' .. x.d .. '|' .. x.e

				return s;";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("1|2|3|4|5"));
            });
        }

        [Test]
        public void TableWithArraysNextWithChangeInCollection()
        {
            string script = @"
				x = { }

				function copy(k, v)
					x[k] = v;
				end

				t = 
				{
                    ""A"", ""B"", ""C"",
					a = 1,
					b = 2,
					c = 3,
					d = 4,
					e = 5
				}

				k,v = next(t, nil);
				copy(k, v);
				k,v = next(t, k);
				copy(k, v);
				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);
				v = nil;

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

                function dump(o)
                   if type(o) == 'table' then
                      local s = '{ '
                      for k,v in pairs(o) do
                         if type(k) ~= 'number' then k = '""'..k..'""' end
                         s = s .. '['..k..'] = ' .. dump(v) .. ','
                      end
                      return s .. '} '
                   else
                      return tostring(o)
                   end
                end

				return dump(x)";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("{ [1] = A,[2] = B,[3] = C,[\"a\"] = 1,[\"b\"] = 2,[\"c\"] = 3,[\"d\"] = 4,[\"e\"] = 5,} "));
            });
        }

        [Test]
        public void TablePairsWithoutMetatable()
        {
            string script = @"
				V = 0
				K = ''

				t = 
				{
					a = 1,
					b = 2,
					c = 3,
					d = 4,
					e = 5
				}

				for k, v in pairs(t) do
					K = K .. k;
					V = V + v;
				end

				return K, V;";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[0].String, Has.Length.EqualTo(5));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(15));
            });
        }

        [Test]
        public void TableIPairsWithoutMetatable()
        {
            string script = @"    
				x = 0
				y = 0

				t = { 2, 4, 6, 8, 10, 12 };

				for i,j in ipairs(t) do
					x = x + i
					y = y + j

					if (i >= 3) then
						break
					end
				end
    
				return x, y";

            DynValue res = LuaState.RunString(script);

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
        public void TestLoadSyntaxError()
        {
            string script = @"    
			function reader ()
				i = i + 1
				return t[i]
			end


			t = { [[?syntax error?]] }
			i = 0
			f, msg = load(reader, 'errorchunk')

			return f, msg;
		";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Nil));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.String));
            });
        }


        [Test]
        public void TableSimplifiedAccess1()
        {
            string script = @"    
			t = {
				ciao = 'hello'
			}

			return t;
		";

            LuaState s = new();
            DynValue t = s.DoString(script);

            Assert.That(t.Table["ciao"], Is.EqualTo("hello"));
        }

        [Test]
        public void TableSimplifiedAccess2()
        {
            string script = @"    
			t = {
				ciao = x
			}

			return t;
		";

            LuaState s = new();
            s.Globals["x"] = "hello";
            DynValue t = s.DoString(script);

            Assert.That(t.Table["ciao"], Is.EqualTo("hello"));
        }

        [Test]
        public void TableSimplifiedAccess3()
        {
            string script = @"    
			t = {
			}

			return t;
		";

            LuaState s = new();
            DynValue t = s.DoString(script);

            s.Globals["t", "ciao"] = "hello";

            Assert.That(t.Table["ciao"], Is.EqualTo("hello"));
        }

        [Test]
        public void TableSimplifiedAccess4()
        {
            string script = @"    
			t = {
			}
		";

            LuaState s = new();
            s.DoString(script);

            s.Globals["t", "ciao"] = "hello";

            Assert.That(s.Globals["t", "ciao"], Is.EqualTo("hello"));
        }


        [Test]
        public void TableSimplifiedAccess5()
        {
            string script = @"    
			t = {
				ciao = 'hello'
			}
		";

            LuaState s = new();
            s.DoString(script);

            Assert.That(s.Globals["t", "ciao"], Is.EqualTo("hello"));
        }

        [Test]
        public void TableSimplifiedAccess6()
        {
            string script = @"    
			t = {
				ciao = 
				{	'hello' }
			}
		";

            LuaState s = new(CoreModules.None);
            s.DoString(script);

            Assert.That(s.Globals["t", "ciao", 1], Is.EqualTo("hello"));
        }


        [Test]
        public void TestNilRemovesEntryForPairs()
        {
            string script = @"
				str = ''

				function showTable(t)
					for i, j in pairs(t) do
						str = str .. i;
					end
					str = str .. '$'
				end

				tb = {}
				tb['id'] = 3

				showTable(tb)

				tb['id'] = nil

				showTable(tb)

				return str
			";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("id$$"));
            });
        }

        [Test]
        public void TestUnpack()
        {
            string script = @"
				return unpack({3,4})
			";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Number, Is.EqualTo(3));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(4));
            });
        }

        [Test]
        public void Table_Length_Calculations()
        {
            Table T = new(null);

            Assert.That(T, Is.Empty, "A");

            T.Set(1, DynValue.True);

            Assert.That(T, Has.Length.EqualTo(1), "B");

            T.Set(2, DynValue.True);
            T.Set(3, DynValue.True);
            T.Set(4, DynValue.True);

            Assert.That(T, Has.Length.EqualTo(4), "C");

            T.Set(3, DynValue.Nil);

            Assert.That(T, Has.Length.EqualTo(2), "D");

            T.Set(3, DynValue.True);

            Assert.That(T, Has.Length.EqualTo(4), "E");

            T.Set(3, DynValue.Nil);

            Assert.That(T, Has.Length.EqualTo(2), "F");

            T.Append(DynValue.True);

            Assert.That(T, Has.Length.EqualTo(4), "G");

            T.Append(DynValue.True);

            Assert.That(T, Has.Length.EqualTo(5), "H");
        }
    }
}
