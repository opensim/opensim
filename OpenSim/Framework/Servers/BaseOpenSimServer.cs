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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Timer=System.Timers.Timer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;


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
        private Timer m_periodicDiagnosticsTimer = new Timer(60 * 60 * 1000);

        protected CommandConsole m_console;
        protected OpenSimAppender m_consoleAppender;
        protected IAppender m_logFileAppender = null; 

        /// <summary>
        /// Time at which this server was started
        /// </summary>
        protected DateTime m_startuptime;

        /// <summary>
        /// Record the initial startup directory for info purposes
        /// </summary>
        protected string m_startupDirectory = Environment.CurrentDirectory;

        /// <summary>
        /// Server version information.  Usually VersionInfo + information about git commit, operating system, etc.
        /// </summary>
        protected string m_version;

        protected string m_pidFile = String.Empty;
        
        /// <summary>
        /// Random uuid for private data 
        /// </summary>
        protected string m_osSecret = String.Empty;

        protected BaseHttpServer m_httpServer;
        public BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }

        public BaseOpenSimServer()
        {
            m_startuptime = DateTime.Now;
            m_version = VersionInfo.Version;
            
            // Random uuid for private data
            m_osSecret = UUID.Random().ToString();

            m_periodicDiagnosticsTimer.Elapsed += new ElapsedEventHandler(LogDiagnostics);
            m_periodicDiagnosticsTimer.Enabled = true;

            // This thread will go on to become the console listening thread
            Thread.CurrentThread.Name = "ConsoleThread";

            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "LogFileAppender")
                {
                    m_logFileAppender = appender;
                }
            }
        }
        
        /// <summary>
        /// Must be overriden by child classes for their own server specific startup behaviour.
        /// </summary>
        protected virtual void StartupSpecific()
        {
            if (m_console != null)
            {
                ILoggerRepository repository = LogManager.GetRepository();
                IAppender[] appenders = repository.GetAppenders();

                foreach (IAppender appender in appenders)
                {
                    if (appender.Name == "Console")
                    {
                        m_consoleAppender = (OpenSimAppender)appender;
                        break;
                    }
                }

                if (null == m_consoleAppender)
                {
                    Notice("No appender named Console found (see the log4net config file for this executable)!");
                }
                else
                {
                    m_consoleAppender.Console = m_console;
                    
                    // If there is no threshold set then the threshold is effectively everything.
                    if (null == m_consoleAppender.Threshold)
                        m_consoleAppender.Threshold = Level.All;
                    
                    Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
                }
                
                m_console.Commands.AddCommand("General", false, "quit",
                        "quit",
                        "Quit the application", HandleQuit);

                m_console.Commands.AddCommand("General", false, "shutdown",
                        "shutdown",
                        "Quit the application", HandleQuit);

                m_console.Commands.AddCommand("General", false, "set log level",
                        "set log level <level>",
                        "Set the console logging level", HandleLogLevel);

                m_console.Commands.AddCommand("General", false, "show info",
                        "show info",
                        "Show general information about the server", HandleShow);

                m_console.Commands.AddCommand("General", false, "show threads",
                        "show threads",
                        "Show thread status", HandleShow);

                m_console.Commands.AddCommand("General", false, "show uptime",
                        "show uptime",
                        "Show server uptime", HandleShow);

                m_console.Commands.AddCommand("General", false, "show version",
                        "show version",
                        "Show server version", HandleShow);

                m_console.Commands.AddCommand("General", false, "threads abort",
                        "threads abort <thread-id>",
                        "Abort a managed thread.  Use \"show threads\" to find possible threads.", HandleThreadsAbort);

                m_console.Commands.AddCommand("General", false, "threads show",
                        "threads show",
                        "Show thread status.  Synonym for \"show threads\"",
                        (string module, string[] args) => Notice(GetThreadsReport()));
            }
        }
        
        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>
        public virtual void ShutdownSpecific() {}
        
        /// <summary>
        /// Provides a list of help topics that are available.  Overriding classes should append their topics to the
        /// information returned when the base method is called.
        /// </summary>
        /// 
        /// <returns>
        /// A list of strings that represent different help topics on which more information is available
        /// </returns>
        protected virtual List<string> GetHelpTopics() { return new List<string>(); }

        /// <summary>
        /// Print statistics to the logfile, if they are active
        /// </summary>
        protected void LogDiagnostics(object source, ElapsedEventArgs e)
        {
            StringBuilder sb = new StringBuilder("DIAGNOSTICS\n\n");
            sb.Append(GetUptimeReport());
            sb.Append(StatsManager.SimExtraStats.Report());
            sb.Append(Environment.NewLine);
            sb.Append(GetThreadsReport());

            m_log.Debug(sb);
        }

        /// <summary>
        /// Get a report about the registered threads in this server.
        /// </summary>
        protected string GetThreadsReport()
        {
            // This should be a constant field.
            string reportFormat = "{0,6}   {1,35}   {2,16}   {3,13}   {4,10}   {5,30}";

            StringBuilder sb = new StringBuilder();
            Watchdog.ThreadWatchdogInfo[] threads = Watchdog.GetThreadsInfo();

            sb.Append(threads.Length + " threads are being tracked:" + Environment.NewLine);

            int timeNow = Environment.TickCount & Int32.MaxValue;

            sb.AppendFormat(reportFormat, "ID", "NAME", "LAST UPDATE (MS)", "LIFETIME (MS)", "PRIORITY", "STATE");
            sb.Append(Environment.NewLine);

            foreach (Watchdog.ThreadWatchdogInfo twi in threads)
            {
                Thread t = twi.Thread;
                
                sb.AppendFormat(
                    reportFormat,
                    t.ManagedThreadId,
                    t.Name,
                    timeNow - twi.LastTick,
                    timeNow - twi.FirstTick,
                    t.Priority,
                    t.ThreadState);

                sb.Append("\n");
            }

            sb.Append("\n");

            // For some reason mono 2.6.7 returns an empty threads set!  Not going to confuse people by reporting
            // zero active threads.
            int totalThreads = Process.GetCurrentProcess().Threads.Count;
            if (totalThreads > 0)
                sb.AppendFormat("Total threads active: {0}\n\n", totalThreads);

            sb.Append("Main threadpool (excluding script engine pools)\n");
            sb.Append(Util.GetThreadPoolReport());

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
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public virtual void Startup()
        {
            m_log.Info("[STARTUP]: Beginning startup processing");

            EnhanceVersionInformation();
            
            m_log.Info("[STARTUP]: OpenSimulator version: " + m_version + Environment.NewLine);
            // clr version potentially is more confusing than helpful, since it doesn't tell us if we're running under Mono/MS .NET and
            // the clr version number doesn't match the project version number under Mono.
            //m_log.Info("[STARTUP]: Virtual machine runtime version: " + Environment.Version + Environment.NewLine);
            m_log.InfoFormat(
                "[STARTUP]: Operating system version: {0}, .NET platform {1}, {2}-bit\n",
                Environment.OSVersion, Environment.OSVersion.Platform, Util.Is64BitProcess() ? "64" : "32");
            
            StartupSpecific();
            
            TimeSpan timeTaken = DateTime.Now - m_startuptime;
            
            m_log.InfoFormat(
                "[STARTUP]: Non-script portion of startup took {0}m {1}s.  PLEASE WAIT FOR LOGINS TO BE ENABLED ON REGIONS ONCE SCRIPTS HAVE STARTED.",
                timeTaken.Minutes, timeTaken.Seconds);
        }

        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>
        public virtual void Shutdown()
        {
            ShutdownSpecific();
            
            m_log.Info("[SHUTDOWN]: Shutdown processing on main thread complete.  Exiting...");
            RemovePIDFile();
            
            Environment.Exit(0);
        }

        private void HandleQuit(string module, string[] args)
        {
            Shutdown();
        }

        private void HandleLogLevel(string module, string[] cmd)
        {
            if (null == m_consoleAppender)
            {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
                return;
            }
      
            if (cmd.Length > 3)
            {
                string rawLevel = cmd[3];
                
                ILoggerRepository repository = LogManager.GetRepository();
                Level consoleLevel = repository.LevelMap[rawLevel];
                
                if (consoleLevel != null)
                    m_consoleAppender.Threshold = consoleLevel;
                else
                    Notice(
                        String.Format(
                            "{0} is not a valid logging level.  Valid logging levels are ALL, DEBUG, INFO, WARN, ERROR, FATAL, OFF",
                            rawLevel));
            }

            Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
        }

        /// <summary>
        /// Show help information
        /// </summary>
        /// <param name="helpArgs"></param>
        protected virtual void ShowHelp(string[] helpArgs)
        {
            Notice("");
            
            if (helpArgs.Length == 0)
            {
                Notice("set log level [level] - change the console logging level only.  For example, off or debug.");
                Notice("show info - show server information (e.g. startup path).");
                Notice("show threads - list tracked threads");
                Notice("show uptime - show server startup time and uptime.");
                Notice("show version - show server version.");
                Notice("");

                return;
            }
        }

        public virtual void HandleShow(string module, string[] cmd)
        {
            List<string> args = new List<string>(cmd);

            args.RemoveAt(0);

            string[] showParams = args.ToArray();

            switch (showParams[0])
            {
                case "info":
                    ShowInfo();
                    break;

                case "threads":
                    Notice(GetThreadsReport());
                    break;

                case "uptime":
                    Notice(GetUptimeReport());
                    break;

                case "version":
                    Notice(GetVersionText());
                    break;
            }
        }

        public virtual void HandleThreadsAbort(string module, string[] cmd)
        {
            if (cmd.Length != 3)
            {
                MainConsole.Instance.Output("Usage: threads abort <thread-id>");
                return;
            }

            int threadId;
            if (!int.TryParse(cmd[2], out threadId))
            {
                MainConsole.Instance.Output("ERROR: Thread id must be an integer");
                return;
            }

            if (Watchdog.AbortThread(threadId))
                MainConsole.Instance.OutputFormat("Aborted thread with id {0}", threadId);
            else
                MainConsole.Instance.OutputFormat("ERROR - Thread with id {0} not found in managed threads", threadId);
        }
        
        protected void ShowInfo()
        {
            Notice(GetVersionText());
            Notice("Startup directory: " + m_startupDirectory);                
            if (null != m_consoleAppender)
                Notice(String.Format("Console log level: {0}", m_consoleAppender.Threshold));              
        }
        
        protected string GetVersionText()
        {
            return String.Format("Version: {0} (interface version {1})", m_version, VersionInfo.MajorInterfaceVersion);
        }

        /// <summary>
        /// Console output is only possible if a console has been established.
        /// That is something that cannot be determined within this class. So
        /// all attempts to use the console MUST be verified.
        /// </summary>
        /// <param name="msg"></param>
        protected void Notice(string msg)
        {
            if (m_console != null)
            {
                m_console.Output(msg);
            }
        }
        
        /// <summary>
        /// Console output is only possible if a console has been established.
        /// That is something that cannot be determined within this class. So
        /// all attempts to use the console MUST be verified.
        /// </summary>
        /// <param name="format"></param>        
        /// <param name="components"></param>
        protected void Notice(string format, params string[] components)
        {
            if (m_console != null)
                m_console.OutputFormat(format, components);
        }

        /// <summary>
        /// Enhance the version string with extra information if it's available.
        /// </summary>
        protected void EnhanceVersionInformation()
        {
            string buildVersion = string.Empty;

            // The subversion information is deprecated and will be removed at a later date
            // Add subversion revision information if available
            // Try file "svn_revision" in the current directory first, then the .svn info.
            // This allows to make the revision available in simulators not running from the source tree.
            // FIXME: Making an assumption about the directory we're currently in - we do this all over the place
            // elsewhere as well
            string gitDir = "../.git/";
            string gitRefPointerPath = gitDir + "HEAD";

            string svnRevisionFileName = "svn_revision";
            string svnFileName = ".svn/entries";
            string manualVersionFileName = ".version";
            string inputLine;
            int strcmp;

            if (File.Exists(manualVersionFileName))
            {
                using (StreamReader CommitFile = File.OpenText(manualVersionFileName))
                    buildVersion = CommitFile.ReadLine();

                m_version += buildVersion ?? "";
            }
            else if (File.Exists(gitRefPointerPath))
            {
//                m_log.DebugFormat("[OPENSIM]: Found {0}", gitRefPointerPath);

                string rawPointer = "";

                using (StreamReader pointerFile = File.OpenText(gitRefPointerPath))
                    rawPointer = pointerFile.ReadLine();

//                m_log.DebugFormat("[OPENSIM]: rawPointer [{0}]", rawPointer);

                Match m = Regex.Match(rawPointer, "^ref: (.+)$");

                if (m.Success)
                {
//                    m_log.DebugFormat("[OPENSIM]: Matched [{0}]", m.Groups[1].Value);

                    string gitRef = m.Groups[1].Value;
                    string gitRefPath = gitDir + gitRef;
                    if (File.Exists(gitRefPath))
                    {
//                        m_log.DebugFormat("[OPENSIM]: Found gitRefPath [{0}]", gitRefPath);

                        using (StreamReader refFile = File.OpenText(gitRefPath))
                        {
                            string gitHash = refFile.ReadLine();
                            m_version += gitHash.Substring(0, 7);
                        }
                    }
                }
            }
            else
            {
                // Remove the else logic when subversion mirror is no longer used
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

                m_version += string.IsNullOrEmpty(buildVersion) ? "      " : ("." + buildVersion + "     ").Substring(0, 6);
            }
        }
        
        protected void CreatePIDFile(string path)
        {
            try
            {
                string pidstring = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                FileStream fs = File.Create(path);

                Byte[] buf = Encoding.ASCII.GetBytes(pidstring);
                fs.Write(buf, 0, buf.Length);
                fs.Close();
                m_pidFile = path;
            }
            catch (Exception)
            {
            }
        }
        
        public string osSecret {
            // Secret uuid for the simulator
            get { return m_osSecret; }            
        }

        public string StatReport(IOSHttpRequest httpRequest)
        {
            // If we catch a request for "callback", wrap the response in the value for jsonp
            if (httpRequest.Query.ContainsKey("callback"))
            {
                return httpRequest.Query["callback"].ToString() + "(" + StatsManager.SimExtraStats.XReport((DateTime.Now - m_startuptime).ToString() , m_version) + ");";
            } 
            else 
            {
                return StatsManager.SimExtraStats.XReport((DateTime.Now - m_startuptime).ToString() , m_version);
            }
        }
           
        protected void RemovePIDFile()
        {
            if (m_pidFile != String.Empty)
            {
                try
                {
                    File.Delete(m_pidFile);
                    m_pidFile = String.Empty;
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
