using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Data
{
    public enum LogSeverity : int
    {
        CRITICAL = 1,
        MAJOR = 2,
        MEDIUM = 3,
        LOW = 4,
        INFO = 5,
        VERBOSE = 6
    }
    public interface ILogData
    {
        void saveLog(string serverDaemon, string target, string methodCall, string arguments, int priority,string logMessage);
    }
}
