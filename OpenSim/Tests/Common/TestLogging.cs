using System;
using System.Collections.Generic;
using System.Text;
using log4net.Appender;
using log4net.Layout;

namespace OpenSim.Tests.Common
{
    public static class TestLogging
    {
        public static void LogToConsole()
        {
            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout =
                new PatternLayout("%date [%thread] %-5level %logger [%property{NDC}] - %message%newline");
            log4net.Config.BasicConfigurator.Configure(consoleAppender);
        }
    }
}
