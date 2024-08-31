using System.Collections.Generic;

namespace SolarSharp.Interpreter.DataTypes
{
    public class Iterator
    {
        public Iterator(IEnumerator<KeyValuePair<DynValue, DynValue>> it)
        {
            It = it;
        }

        public DynValue Current { get; set; }
        public IEnumerator<KeyValuePair<DynValue, DynValue>> It { get; }

        public DynValue Next()
        {
        repeat:
            if (It.MoveNext()) 
            {
                // skip over nils
                if (It.Current.Value == null) goto repeat;
                Current = It.Current.Key;
                return It.Current.Value;
            }
            else
            {
                Current = null;
                return null;
            }
        }
    }
}
