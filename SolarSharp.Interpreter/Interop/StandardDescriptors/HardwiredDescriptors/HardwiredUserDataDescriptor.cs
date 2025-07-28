using System;
using SolarSharp.Interpreter.Interop.BasicDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors
{
    public abstract class HardwiredUserDataDescriptor : DispatchingUserDataDescriptor
    {
        protected HardwiredUserDataDescriptor(Type T) :
            base(T, "::hardwired::" + T.Name)
        {

        }

    }
}
