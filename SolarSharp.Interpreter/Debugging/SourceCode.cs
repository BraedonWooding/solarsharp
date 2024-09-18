using System.Collections.Generic;

namespace SolarSharp.Interpreter.Debugging
{
    /// <summary>
    /// Class representing the source code of a given script
    /// </summary>
    public class SourceCode
    {
        /// <summary>
        /// Gets the name of the source code
        /// </summary>
        public string Name { get; private set; }
        
        /// <summary>
        /// Gets the source code as a string
        /// </summary>
        public string Code { get; private set; }

        // TODO: Remove
        /// <summary>
        /// Gets the source identifier inside a script
        /// </summary>
        public int SourceID { get; private set; }

        internal List<SourceRef> Refs { get; private set; }

        internal SourceCode(string name, string code, int sourceID)
        {
            Refs = new List<SourceRef>();
            Name = name;
            Code = code;
            SourceID = sourceID;
        }
    }
}
