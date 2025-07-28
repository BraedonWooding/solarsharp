namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// State of coroutines
    /// </summary>
    public enum CoroutineState
    {
        /// <summary>
        /// This is the main coroutine
        /// </summary>
        Main,
        /// <summary>
        /// Coroutine has not started yet
        /// </summary>
        NotStarted,
        /// <summary>
        /// Coroutine is suspended
        /// </summary>
        Suspended,
        /// <summary>
        /// Coroutine is running
        /// </summary>
        Running,
        /// <summary>
        /// Coroutine has terminated
        /// </summary>
        Dead
    }
}
