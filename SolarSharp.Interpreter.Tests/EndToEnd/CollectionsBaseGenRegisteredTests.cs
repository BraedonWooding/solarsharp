﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class CollectionsBaseGenRegisteredTests
    {
        public class RegCollItem
        {
            public int Value;

            public RegCollItem(int v)
            {
                Value = v;
            }
        }

        public class RegCollMethods
        {
            private readonly List<RegCollItem> m_Items = new() { new RegCollItem(7), new RegCollItem(8), new RegCollItem(9) };
            private readonly List<int> m_List = new() { 1, 2, 3 };
            private readonly int[] m_Array = new int[3] { 2, 4, 6 };
            private readonly int[,] m_MultiArray = new int[2, 3] { { 2, 4, 6 }, { 7, 8, 9 } };

            public int[,] GetMultiArray()
            {
                return m_MultiArray;
            }

            public int[] GetArray()
            {
                return m_Array;
            }

            public List<RegCollItem> GetItems()
            {
                return m_Items;
            }

            public List<int> GetList()
            {
                return m_List;
            }

            public IEnumerator<int> GetEnumerator()
            {
                return GetList().GetEnumerator();
            }
        }

        private static void Do(string code, Action<DynValue> asserts)
        {
            // useless ops, to trick AOT to include the extension methods..
            List<int> lst = new() { 1, 1, 1, 1 };
            lst.Add(lst.Sum());
            lst.Add(lst.Last());

            Do(code, (d, o) => asserts(d));
        }

        private static void Do(string code, Action<DynValue, RegCollMethods> asserts)
        {
            try
            {
                UserData.RegisterType<RegCollMethods>();
                UserData.RegisterType<RegCollItem>();
                UserData.RegisterType<Array>();
                UserData.RegisterType(typeof(List<>));
                UserData.RegisterExtensionType(typeof(Enumerable));

                Script s = new();

                var obj = new RegCollMethods();
                s.Globals["o"] = obj;
                s.Globals["ctor"] = UserData.CreateStatic<RegCollItem>();

                DynValue res = s.DoString(code);

                asserts(res, obj);
            }
            catch (ScriptRuntimeException ex)
            {
                Debug.WriteLine(ex.DecoratedMessage);
                ex.Rethrow();
                throw;
            }
            finally
            {
                UserData.UnregisterType<RegCollMethods>();
                UserData.UnregisterType<RegCollItem>();
                UserData.UnregisterType<Array>();
                UserData.UnregisterType(typeof(List<>));
                UserData.UnregisterType(typeof(List<RegCollItem>));
                UserData.UnregisterType(typeof(List<int>));
                //UserData.UnregisterType<IEnumerable>();
            }
        }

        [Test]
        public void RegCollGen_List_ExtMeth_Last()
        {
            Do(@"
				local list = o:GetList()
				return list:Last();			
			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(3));
                 });
             });
        }

        [Test]
        public void RegCollGen_List_ExtMeth_Sum()
        {
            Do(@"
				local list = o:GetList()
				return list:Sum();			
			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(6));
                 });
             });
        }



        [Test]
        public void RegCollGen_IteratorOnList_Auto()
        {
            Do(@"
				local list = o:GetList()

				local x = 0;
				for i in list do 
					x = x + i;
				end
				return x;
			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(6));
                 });
             });
        }


        [Test]
        public void RegCollGen_IteratorOnList_Manual()
        {
            Do(@"
				function each(obj)
					local e = obj:GetEnumerator()
					return function()
						if e:MoveNext() then
							return e.Current
						end
					end
				end

				local list = o; 

				local x = 0;
				for i in each(list) do 
					x = x + i;
				end
				return x;

			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(6));
                 });
             });
        }

        [Test]
        public void RegCollGen_IteratorOnList_ChangeElem()
        {
            Do(@"
				local list = o:GetList()

				list[1] = list[2] + list[1];

				local x = 0;
				for i in list do 
					x = x + i;
				end
				return x;
			",
             (r, o) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(9));
                     Assert.That(o.GetList()[0], Is.EqualTo(1));
                     Assert.That(o.GetList()[1], Is.EqualTo(5));
                     Assert.That(o.GetList()[2], Is.EqualTo(3));
                 });
             });
        }


        [Test]
        public void RegCollGen_IteratorOnArray_Auto()
        {
            Do(@"
				local array = o:GetArray()

				local x = 0;
				for i in array do 
					x = x + i;
				end
				return x;			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(12));
                 });
             });
        }


        [Test]
        public void RegCollGen_IteratorOnArray_ChangeElem()
        {
            Do(@"
				local array = o:get_array()

				array[1] = array[2] - 1;

				local x = 0;
				for i in array do 
					x = x + i;
				end
				return x;
			",
             (r, o) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(13));
                     Assert.That(o.GetArray()[0], Is.EqualTo(2));
                     Assert.That(o.GetArray()[1], Is.EqualTo(5));
                     Assert.That(o.GetArray()[2], Is.EqualTo(6));
                 });
             });
        }

        [Test]
        public void RegCollGen_IteratorOnMultiDimArray_ChangeElem()
        {
            Do(@"
				local array = o:GetMultiArray()

				array[0, 1] = array[1, 2];

				local x = 0;
				for i in array do 
					x = x + i;
				end
				return x;
			",
             (r, o) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(41));
                     Assert.That(o.GetMultiArray()[0, 0], Is.EqualTo(2));
                     Assert.That(o.GetMultiArray()[0, 1], Is.EqualTo(9));
                     Assert.That(o.GetMultiArray()[0, 2], Is.EqualTo(6));
                     Assert.That(o.GetMultiArray()[1, 0], Is.EqualTo(7));
                     Assert.That(o.GetMultiArray()[1, 1], Is.EqualTo(8));
                     Assert.That(o.GetMultiArray()[1, 2], Is.EqualTo(9));
                 });
             });
        }





        [Test]
        public void RegCollGen_IteratorOnObjList_Auto()
        {
            Do(@"
				local list = o:GetItems()

				local x = 0;
				for i in list do 
					x = x + i.Value;
				end
				return x;
			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(24));
                 });
             });
        }


        [Test]
        public void RegCollGen_IteratorOnObjList_Manual()
        {
            Do(@"
				function each(obj)
					local e = obj:GetEnumerator()
					return function()
						if e:MoveNext() then
							return e.Current
						end
					end
				end

				local list = o.get_items(); 

				local x = 0;
				for i in each(list) do 
					x = x + i.Value;
				end
				return x;

			",
             (r) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(24));
                 });
             });
        }

        [Test]
        public void RegCollGen_IteratorOnObjList_ChangeElem()
        {
            Do(@"
				local list = o:GetItems()

				list[1] = ctor.__new(list[2].Value + list[1].Value);

				local x = 0;
				for i in list do 
					x = x + i.Value;
				end
				return x;
			",
             (r, o) =>
             {
                 Assert.Multiple(() =>
                 {
                     Assert.That(r.Type, Is.EqualTo(DataType.Number));
                     Assert.That(r.Number, Is.EqualTo(7 + 17 + 9));
                     Assert.That(o.GetItems()[0].Value, Is.EqualTo(7));
                     Assert.That(o.GetItems()[1].Value, Is.EqualTo(17));
                     Assert.That(o.GetItems()[2].Value, Is.EqualTo(9));
                 });
             });
        }



    }
}
