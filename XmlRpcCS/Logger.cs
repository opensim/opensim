namespace Nwc.XmlRpc
{
    using System;

    /// <summary>Define levels of logging.</summary><remarks> This duplicates
    /// similar enumerations in System.Diagnostics.EventLogEntryType. The 
    /// duplication was merited because .NET Compact Framework lacked the EventLogEntryType enum.</remarks>
    public enum LogLevel
    {
        /// <summary>Information level, log entry for informational reasons only.</summary>
        Information,
        /// <summary>Warning level, indicates a possible problem.</summary>
        Warning,
        /// <summary>Error level, implies a significant problem.</summary>
        Error
    }

    ///<summary>
    ///Logging singleton with swappable output delegate.
    ///</summary>
    ///<remarks>
    ///This singleton provides a centralized log. The actual WriteEntry calls are passed
    ///off to a delegate however. Having a delegate do the actual logginh allows you to
    ///implement different logging mechanism and have them take effect throughout the system.
    ///</remarks>
    public class Logger
    {
        ///<summary>Delegate definition for logging.</summary>
        ///<param name="message">The message <c>String</c> to log.</param>
        ///<param name="level">The <c>LogLevel</c> of your message.</param>
        public delegate void LoggerDelegate(String message, LogLevel level);
        ///<summary>The LoggerDelegate that will recieve WriteEntry requests.</summary>
        static public LoggerDelegate Delegate = null;

        ///<summary>
        ///Method logging events are sent to.
        ///</summary>
        ///<param name="message">The message <c>String</c> to log.</param>
        ///<param name="level">The <c>LogLevel</c> of your message.</param>
        static public void WriteEntry(String message, LogLevel level)
        {
            if (Delegate != null)
                Delegate(message, level);
        }
    }
}
