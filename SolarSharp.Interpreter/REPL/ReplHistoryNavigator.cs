using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.REPL
{
    /// <summary>
    /// An implementation of <see cref="ReplInterpreter"/> which supports a very basic history of recent input lines.
    /// </summary>
    public class ReplHistoryInterpreter : ReplInterpreter
    {
        private readonly string[] m_History;
        private int m_Last = -1;
        private int m_Navi = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplHistoryInterpreter"/> class.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="historySize">Size of the history.</param>
        public ReplHistoryInterpreter(Script script, int historySize)
            : base(script)
        {
            m_History = new string[historySize];
        }


        /// <summary>
        /// Evaluate a REPL command.
        /// This method returns the result of the computation, or null if more input is needed for having valid code.
        /// In case of errors, exceptions are propagated to the caller.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>
        /// This method returns the result of the computation, or null if more input is needed for a computation.
        /// </returns>
        public override DynValue Evaluate(string input)
        {
            m_Navi = -1;
            m_Last = (m_Last + 1) % m_History.Length;
            m_History[m_Last] = input;
            return base.Evaluate(input);
        }

        /// <summary>
        /// Gets the previous item in history, or null
        /// </summary>
        public string HistoryPrev()
        {
            m_Navi = m_Navi == -1 ? m_Last : (m_Navi - 1 + m_History.Length) % m_History.Length;

            if (m_Navi >= 0) return m_History[m_Navi];
            return null;
        }

        /// <summary>
        /// Gets the next item in history, or null
        /// </summary>
        public string HistoryNext()
        {
            if (m_Navi == -1)
                return null;
            else
                m_Navi = (m_Navi + 1) % m_History.Length;

            if (m_Navi >= 0) return m_History[m_Navi];
            return null;
        }



    }
}
