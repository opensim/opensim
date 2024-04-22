using System.Runtime.CompilerServices;

namespace OSHttpServer
{
    /// <summary>
    /// Priority for log entries
    /// </summary>
    /// <seealso cref="ILogWriter"/>

    public enum LogPrio
    {
        None,
        /// <summary>
        /// Very detailed logs to be able to follow the flow of the program.
        /// </summary>
        Trace,

        /// <summary>
        /// Logs to help debug errors in the application
        /// </summary>
        Debug,

        /// <summary>
        /// Information to be able to keep track of state changes etc.
        /// </summary>
        Info,

        /// <summary>
        /// Something did not go as we expected, but it's no problem.
        /// </summary>
        Warning,

        /// <summary>
        /// Something that should not fail failed, but we can still keep
        /// on going.
        /// </summary>
        Error,

        /// <summary>
        /// Something failed, and we cannot handle it properly.
        /// </summary>
        Fatal
    }

    /// <summary>
    /// Interface used to write to log files.
    /// </summary>
    public interface ILogWriter
    {
        /// <summary>
        /// Write an entry to the log file.
        /// </summary>
        /// <param name="source">object that is writing to the log</param>
        /// <param name="priority">importance of the log message</param>
        /// <param name="message">the message</param>
        void Write(object source, LogPrio priority, string message);
    }

    /// <summary>
    /// Default log writer, writes everything to null (nowhere).
    /// </summary>
    /// <seealso cref="ILogWriter"/>

    public sealed class NullLogWriter : ILogWriter
    {
        /// <summary>
        /// The logging instance.
        /// </summary>
        public static readonly NullLogWriter Instance = new();

        /// <summary>
        /// Writes everything to null
        /// </summary>
        /// <param name="source">object that wrote the log entry.</param>
        /// <param name="prio">Importance of the log message</param>
        /// <param name="message">The message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(object source, LogPrio prio, string message) {}
    }
}
