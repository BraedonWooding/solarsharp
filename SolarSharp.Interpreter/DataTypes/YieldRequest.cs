namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// Class wrapping a request to yield a coroutine
    /// </summary>
    public class YieldRequest
    {
        /// <summary>
        /// The return values of the coroutine
        /// </summary>
        public DynValue[] ReturnValues;
    }
}
