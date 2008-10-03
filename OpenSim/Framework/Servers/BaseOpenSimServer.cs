/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Servers
{
    /// <summary>
    /// Common base for the main OpenSimServers (user, grid, inventory, region, etc)
    /// </summary>
    public abstract class BaseOpenSimServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This will control a periodic log printout of the current 'show stats' (if they are active) for this
        /// server.
        /// </summary>
        private System.Timers.Timer m_periodicDiagnosticsTimer = new System.Timers.Timer(60 * 60 * 1000);

        protected ConsoleBase m_console;

        /// <summary>
        /// Time at which this server was started
        /// </summary>
        protected DateTime m_startuptime;

        /// <summary>
        /// Record the initial startup directory for info purposes
        /// </summary>
        protected string m_startupDirectory = Environment.CurrentDirectory;

        /// <summary>
        /// Server version information.  Usually VersionInfo + information about svn revision, operating system, etc.
        /// </summary>
        protected string m_version;

        protected BaseHttpServer m_httpServer;
        public BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }

        /// <summary>
        /// Holds the non-viewer statistics collection object for this service/server
        /// </summary>
        protected IStatsCollector m_stats;

        public BaseOpenSimServer()
        {
            m_startuptime = DateTime.Now;
            m_version = VersionInfo.Version;

            m_periodicDiagnosticsTimer.Elapsed += new ElapsedEventHandler(LogDiagnostics);
            m_periodicDiagnosticsTimer.Enabled = true;

            // Add ourselves to thread monitoring.  This thread will go on to become the console listening thread
            Thread.CurrentThread.Name = "ConsoleThread";
            ThreadTracker.Add(Thread.CurrentThread);
        }
        
        /// <summary>
        /// Must be overriden by child classes for their own server specific startup behaviour.
        /// </summary>
        protected abstract void StartupSpecific();

        /// <summary>
        /// Print statistics to the logfile, if they are active
        /// </summary>
        protected void LogDiagnostics(object source, ElapsedEventArgs e)
        {
            StringBuilder sb = new StringBuilder("DIAGNOSTICS\n\n");
            sb.Append(GetUptimeReport());

            if (m_stats != null)
            {
                sb.Append(m_stats.Report());
            }

            sb.Append(Environment.NewLine);
            sb.Append(GetThreadsReport());

            m_log.Debug(sb);
        }

        /// <summary>
        /// Get a report about the registered threads in this server.
        /// </summary>
        protected string GetThreadsReport()
        {
            StringBuilder sb = new StringBuilder();

            List<Thread> threads = ThreadTracker.GetThreads();
            if (threads == null)
            {
                sb.Append("Thread tracking is only enabled in DEBUG mode.");
            }
            else
            {
                sb.Append(threads.Count + " threads are being tracked:" + Environment.NewLine);
                foreach (Thread t in threads)
                {
                    sb.Append(
                        "ID: " + t.ManagedThreadId + ", Name: " + t.Name + ", Alive: " + t.IsAlive
                        + ", Pri: " + t.Priority + ", State: " + t.ThreadState + Environment.NewLine);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return a report about the uptime of this server
        /// </summary>
        /// <returns></returns>
        protected string GetUptimeReport()
        {
            StringBuilder sb = new StringBuilder(String.Format("Time now is {0}\n", DateTime.Now));
            sb.Append(String.Format("Server has been running since {0}, {1}\n", m_startuptime.DayOfWeek, m_startuptime));
            sb.Append(String.Format("That is an elapsed time of {0}\n", DateTime.Now - m_startuptime));

            return sb.ToString();
        }

        /// <summary>
        /// Set the level of log notices being echoed to the console
        /// </summary>
        /// <param name="setParams"></param>
        private void SetConsoleLogLevel(string[] setParams)
        {
            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();
            OpenSimAppender consoleAppender = null;

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "Console")
                {
                    consoleAppender = (OpenSimAppender)appender;
                    break;
                }
            }

            if (null == consoleAppender)
            {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
                return;
            }

            if (setParams.Length > 0)
            {
                Level consoleLevel = repository.LevelMap[setParams[0]];
                if (consoleLevel != null)
                    consoleAppender.Threshold = consoleLevel;
                else
                    Notice(
                        String.Format(
                            "{0} is not a valid logging level.  Valid logging levels are ALL, DEBUG, INFO, WARN, ERROR, FATAL, OFF",
                            setParams[0]));
            }

            // If there is no threshold set then the threshold is effectively everything.
            Level thresholdLevel
                = (null != consoleAppender.Threshold ? consoleAppender.Threshold : log4net.Core.Level.All);

            Notice(String.Format("Console log level is {0}", thresholdLevel));
        }

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public virtual void Startup()
        {
            m_log.Info("[STARTUP]: Beginning startup processing");                        

            EnhanceVersionInformation();
            
            m_log.Info("[STARTUP]: Version: " + m_version + "\n");
            
            StartupSpecific();
        }

        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>
        public virtual void Shutdown()
        {
            m_log.Info("[SHUTDOWN]: Shutdown processing on main thread complete.  Exiting...");

            Environment.Exit(0);
        }

        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public virtual void RunCmd(string command, string[] cmdparams)
        {
            switch (command)
            {
                case "help":
                    ShowHelp(cmdparams);
                    Notice("");
                    break;

                case "set":
                    Set(cmdparams);
                    break;

                case "show":
                    if (cmdparams.Length > 0)
                    {
                        Show(cmdparams);
                    }
                    break;

                case "quit":
                case "shutdown":
                    Shutdown();
                    break;
            }
        }

        /// <summary>
        /// Set an OpenSim parameter
        /// </summary>
        /// <param name="setArgs">
        /// The arguments given to the set command.
        /// </param>
        public virtual void Set(string[] setArgs)
        {
            // Temporary while we only have one command which takes at least two parameters
            if (setArgs.Length < 2)
                return;

            if (setArgs[0] == "log" && setArgs[1] == "level")
            {
                string[] setParams = new string[setArgs.Length - 2];
                Array.Copy(setArgs, 2, setParams, 0, setArgs.Length - 2);

                SetConsoleLogLevel(setParams);
            }
        }

        /// <summary>
        /// Show help information
        /// </summary>
        /// <param name="helpArgs"></param>
        protected virtual void ShowHelp(string[] helpArgs)
        {
            if (helpArgs.Length == 0)
            {
                Notice("");
                // TODO: not yet implemented
                //Notice("help [command] - display general help or specific command help.  Try help help for more info.");
                Notice("quit - equivalent to shutdown.");

                Notice("set log level [level] - change the console logging level only.  For example, off or debug.");
                Notice("show info - show server information (e.g. startup path).");

                if (m_stats != null)
                    Notice("show stats - show statistical information for this server");

                Notice("show threads - list tracked threads");
                Notice("show uptime - show server startup time and uptime.");
                Notice("show version - show server version.");
                Notice("shutdown - shutdown the server.\n");

                return;
            }
        }

        /// <summary>
        /// Outputs to the console information about the region
        /// </summary>
        /// <param name="showParams">
        /// What information to display (valid arguments are "uptime", "users", ...)
        /// </param>
        public virtual void Show(string[] showParams)
        {
            switch (showParams[0])
            {
                case "info":
                    Notice("Version: " + m_version);
                    Notice("Startup directory: " + m_startupDirectory);
                    break;

                case "stats":
                    if (m_stats != null)
                    {
                        Notice(m_stats.Report());
                    }
                    break;

                case "threads":
                    Notice(GetThreadsReport());
                    break;

                case "uptime":
                    Notice(GetUptimeReport());
                    break;

                case "version":
                    Notice("Version: " + m_version);
                    break;
            }
        }

        /// <summary>
        /// Console output is only possible if a console has been established.
        /// That is something that cannot be determined within this class. So
        /// all attempts to use the console MUST be verified.
        /// </summary>
        private void Notice(string msg)
        {
            if (m_console != null)
            {
                m_console.Notice(msg);
            }
        }

        /// <summary>
        /// Enhance the version string with extra information if it's available.
        /// </summary>
        protected void EnhanceVersionInformation()
        {
            string buildVersion = string.Empty;

            // Add subversion revision information if available
            // Try file "svn_revision" in the current directory first, then the .svn info.
            // This allows to make the revision available in simulators not running from the source tree.
            // FIXME: Making an assumption about the directory we're currently in - we do this all over the place
            // elsewhere as well
            string svnRevisionFileName = "svn_revision";
            string svnFileName = ".svn/entries";
            string inputLine;
            int strcmp;

            if (File.Exists(svnRevisionFileName))
            {
                StreamReader RevisionFile = File.OpenText(svnRevisionFileName);
                buildVersion = RevisionFile.ReadLine();
                buildVersion.Trim();
                RevisionFile.Close();
            }

            if (string.IsNullOrEmpty(buildVersion) && File.Exists(svnFileName))
            {
                StreamReader EntriesFile = File.OpenText(svnFileName);
                inputLine = EntriesFile.ReadLine();
                while (inputLine != null)
                {
                    // using the dir svn revision at the top of entries file
                    strcmp = String.Compare(inputLine, "dir");
                    if (strcmp == 0)
                    {
                        buildVersion = EntriesFile.ReadLine();
                        break;
                    }
                    else
                    {
                        inputLine = EntriesFile.ReadLine();
                    }
                }
                EntriesFile.Close();
            }

            m_version += string.IsNullOrEmpty(buildVersion)? ".00000" : ("." + buildVersion + "     ").Substring(0, 6);

            // Add operating system information if available
            string OSString = "";

            if (System.Environment.OSVersion.Platform != PlatformID.Unix)
            {
                OSString = System.Environment.OSVersion.ToString();
            }
            else
            {
                OSString = Util.ReadEtcIssue();
            }

            if (OSString.Length > 45)
            {
                OSString = OSString.Substring(0, 45);
            }

            m_version += " (OS " + OSString + ")";
        }
    }
}
