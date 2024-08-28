using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
#pragma warning disable 169 // unused private field

    [TestFixture]
    public class UserDataEventsTests
    {
        public class SomeClass
        {
            public event EventHandler MyEvent;
            public static event EventHandler MySEvent;

            public bool Trigger_MyEvent()
            {
                if (MyEvent != null)
                {
                    MyEvent(this, EventArgs.Empty);
                    return true;
                }
                return false;
            }

            public static bool Trigger_MySEvent()
            {
                if (MySEvent != null)
                {
                    MySEvent(null, EventArgs.Empty);
                    return true;
                }
                return false;
            }
        }


        [Test]
        public void Interop_Event_Simple()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            var obj = new SomeClass();
            s.Globals["myobj"] = obj;
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MyEvent.add(handler);
				");

            obj.Trigger_MyEvent();

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        [Test]
        public void Interop_Event_TwoObjects()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            var obj = new SomeClass();
            var obj2 = new SomeClass();
            s.Globals["myobj"] = obj;
            s.Globals["myobj2"] = obj2;
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MyEvent.add(handler);
				");

            obj.Trigger_MyEvent();
            obj2.Trigger_MyEvent();

            Assert.That(invocationCount, Is.EqualTo(1));
        }


        [Test]
        public void Interop_Event_Multi()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            var obj = new SomeClass();
            s.Globals["myobj"] = obj;
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MyEvent.add(handler);
				myobj.MyEvent.add(handler);
				");

            obj.Trigger_MyEvent();

            Assert.That(invocationCount, Is.EqualTo(2));
        }

        [Test]
        public void Interop_Event_MultiAndDetach()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            var obj = new SomeClass();
            s.Globals["myobj"] = obj;
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MyEvent.add(handler);
				myobj.MyEvent.add(handler);
				myobj.Trigger_MyEvent();
				myobj.MyEvent.remove(handler);
				myobj.Trigger_MyEvent();
				");

            Assert.That(invocationCount, Is.EqualTo(3));
        }

        [Test]
        public void Interop_Event_DetachAndDeregister()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            var obj = new SomeClass();
            s.Globals["myobj"] = obj;
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MyEvent.add(handler);
				myobj.MyEvent.add(handler);
				myobj.Trigger_MyEvent();
				myobj.MyEvent.remove(handler);
				myobj.Trigger_MyEvent();
				myobj.MyEvent.remove(handler);
				");

            Assert.That(obj.Trigger_MyEvent(), Is.False, "deregistration");
            Assert.That(invocationCount, Is.EqualTo(3));
        }


        [Test]
        public void Interop_SEvent_DetachAndDeregister()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            s.Globals["myobj"] = typeof(SomeClass);
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MySEvent.add(handler);
				myobj.MySEvent.add(handler);
				myobj.Trigger_MySEvent();
				myobj.MySEvent.remove(handler);
				myobj.Trigger_MySEvent();
				myobj.MySEvent.remove(handler);
				");

            Assert.That(SomeClass.Trigger_MySEvent(), Is.False, "deregistration");
            Assert.That(invocationCount, Is.EqualTo(3));
        }

        [Test]
        public void Interop_SEvent_DetachAndReregister()
        {
            int invocationCount = 0;
            UserData.RegisterType<SomeClass>();
            UserData.RegisterType<EventArgs>();

            Script s = new(CoreModules.None);

            s.Globals["myobj"] = typeof(SomeClass);
            s.Globals["ext"] = DynValue.NewCallback((c, a) => { invocationCount += 1; return DynValue.Void; });

            s.DoString(@"
				function handler(o, a)
					ext();
				end

				myobj.MySEvent.add(handler);
				myobj.Trigger_MySEvent();
				myobj.MySEvent.remove(handler);
				myobj.Trigger_MySEvent();
				myobj.MySEvent.add(handler);
				myobj.Trigger_MySEvent();
			");

            Assert.Multiple(() =>
            {
                Assert.That(invocationCount, Is.EqualTo(2));
                Assert.That(SomeClass.Trigger_MySEvent(), Is.True, "deregistration");
            });
        }








    }
}
