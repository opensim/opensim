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
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;

namespace OpenSim.Framework.Servers
{
    public class ServerBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public IConfigSource Config { get; protected set; }

        /// <summary>
        /// Console to be used for any command line output.  Can be null, in which case there should be no output.
        /// </summary>
        protected ICommandConsole m_console;

        protected OpenSimAppender m_consoleAppender;
        protected FileAppender m_logFileAppender;
        protected FileAppender m_statsLogFileAppender;

        protected DateTime m_startuptime;
        protected string m_startupDirectory = Environment.CurrentDirectory;

        protected string m_pidFile = String.Empty;

        protected ServerStatsCollector m_serverStatsCollector;

        /// <summary>
        /// Server version information.  Usually VersionInfo + information about git commit, operating system, etc.
        /// </summary>
        protected string m_version;

        public ServerBase()
        {
            m_startuptime = DateTime.Now;
            m_version = VersionInfo.Version;
            EnhanceVersionInformation();
        }

        protected void CreatePIDFile(string path)
        {
            if (File.Exists(path))
                m_log.ErrorFormat(
                    "[SERVER BASE]: Previous pid file {0} still exists on startup.  Possibly previously unclean shutdown.",
                    path);

            try
            {
                string pidstring = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();

                using (FileStream fs = File.Create(path))
                {
                    Byte[] buf = Encoding.ASCII.GetBytes(pidstring);
                    fs.Write(buf, 0, buf.Length);
                }

                m_pidFile = path;

                m_log.InfoFormat("[SERVER BASE]: Created pid file {0}", m_pidFile);
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format("[SERVER BASE]: Could not create PID file at {0} ", path), e);
            }
        }

        protected void RemovePIDFile()
        {
            if (m_pidFile != String.Empty)
            {
                try
                {
                    File.Delete(m_pidFile);
                }
                catch (Exception e)
                {
                    m_log.Error(string.Format("[SERVER BASE]: Error whilst removing {0} ", m_pidFile), e);
                }

                m_pidFile = String.Empty;
            }
        }

        /// <summary>
        /// Log information about the circumstances in which we're running (OpenSimulator version number, CLR details,
        /// etc.).
        /// </summary>
        public void LogEnvironmentInformation()
        {
            // FIXME: This should be done down in ServerBase but we need to sort out and refactor the log4net
            // XmlConfigurator calls first accross servers.
            m_log.InfoFormat("[SERVER BASE]: Starting in {0}", m_startupDirectory);

            m_log.InfoFormat("[SERVER BASE]: OpenSimulator version: {0}", m_version);

            // clr version potentially is more confusing than helpful, since it doesn't tell us if we're running under Mono/MS .NET and
            // the clr version number doesn't match the project version number under Mono.
            //m_log.Info("[STARTUP]: Virtual machine runtime version: " + Environment.Version + Environment.NewLine);
            m_log.InfoFormat(
                "[SERVER BASE]: Operating system version: {0}, .NET platform {1}, {2}-bit",
                Environment.OSVersion, Environment.OSVersion.Platform, Util.Is64BitProcess() ? "64" : "32");
        }

        public void RegisterCommonAppenders(IConfig startupConfig)
        {
            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "Console")
                {
                    m_consoleAppender = (OpenSimAppender)appender;
                }
                else if (appender.Name == "LogFileAppender")
                {
                    m_logFileAppender = (FileAppender)appender;
                }
                else if (appender.Name == "StatsLogFileAppender")
                {
                    m_statsLogFileAppender = (FileAppender)appender;
                }
            }

            if (null == m_consoleAppender)
            {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
            }
            else
            {
                // FIXME: This should be done through an interface rather than casting.
                m_consoleAppender.Console = (ConsoleBase)m_console;

                // If there is no threshold set then the threshold is effectively everything.
                if (null == m_consoleAppender.Threshold)
                    m_consoleAppender.Threshold = Level.All;

                Notice(String.Format("Console log level is {0}", m_consoleAppender.Threshold));
            }

            if (m_logFileAppender != null && startupConfig != null)
            {
                string cfgFileName = startupConfig.GetString("LogFile", null);
                if (cfgFileName != null)
                {
                    m_logFileAppender.File = cfgFileName;
                    m_logFileAppender.ActivateOptions();
                }

                m_log.InfoFormat("[SERVER BASE]: Logging started to file {0}", m_logFileAppender.File);
            }

            if (m_statsLogFileAppender != null && startupConfig != null)
            {
                string cfgStatsFileName = startupConfig.GetString("StatsLogFile", null);
                if (cfgStatsFileName != null)
                {
                    m_statsLogFileAppender.File = cfgStatsFileName;
                    m_statsLogFileAppender.ActivateOptions();
                }

                m_log.InfoFormat("[SERVER BASE]: Stats Logging started to file {0}", m_statsLogFileAppender.File);
            }
        }

        /// <summary>
        /// Register common commands once m_console has been set if it is going to be set
        /// </summary>
        public void RegisterCommonCommands()
        {
            if (m_console == null)
                return;

            m_console.Commands.AddCommand(
                "General", false, "show info", "show info", "Show general information about the server", HandleShow);

            m_console.Commands.AddCommand(
                "General", false, "show version", "show version", "Show server version", HandleShow);

            m_console.Commands.AddCommand(
                "General", false, "show uptime", "show uptime", "Show server uptime", HandleShow);

            m_console.Commands.AddCommand(
                "General", false, "get log level", "get log level", "Get the current console logging level",
                (mod, cmd) => ShowLogLevel());

            m_console.Commands.AddCommand(
                "General", false, "set log level", "set log level <level>",
                "Set the console logging level for this session.", HandleSetLogLevel);

            m_console.Commands.AddCommand(
                "General", false, "config set",
                "config set <section> <key> <value>",
                "Set a config option.  In most cases this is not useful since changed parameters are not dynamically reloaded.  Neither do changed parameters persist - you will have to change a config file manually and restart.", HandleConfig);

            m_console.Commands.AddCommand(
                "General", false, "config get",
                "config get [<section>] [<key>]",
                "Synonym for config show",
                HandleConfig);

            m_console.Commands.AddCommand(
                "General", false, "config show",
                "config show [<section>] [<key>]",
                "Show config information",
                "If neither section nor field are specified, then the whole current configuration is printed." + Environment.NewLine
                + "If a section is given but not a field, then all fields in that section are printed.",
                HandleConfig);

            m_console.Commands.AddCommand(
                "General", false, "config save",
                "config save <path>",
                "Save current configuration to a file at the given path", HandleConfig);

            m_console.Commands.AddCommand(
                "General", false, "command-script",
                "command-script <script>",
                "Run a command script from file", HandleScript);

            m_console.Commands.AddCommand(
                "General", false, "show threads",
                "show threads",
                "Show thread status", HandleShow);

            m_console.Commands.AddCommand(
                "Debug", false, "threads abort",
                "threads abort <thread-id>",
                "Abort a managed thread.  Use \"show threads\" to find possible threads.", HandleThreadsAbort);

            m_console.Commands.AddCommand(
                "General", false, "threads show",
                "threads show",
                "Show thread status.  Synonym for \"show threads\"",
                (string module, string[] args) => Notice(GetThreadsReport()));

            m_console.Commands.AddCommand (
                "Debug", false, "debug threadpool set",
                "debug threadpool set worker|iocp min|max <n>",
                "Set threadpool parameters.  For debug purposes.",
                HandleDebugThreadpoolSet);

            m_console.Commands.AddCommand (
                "Debug", false, "debug threadpool status",
                "debug threadpool status",
                "Show current debug threadpool parameters.",
                HandleDebugThreadpoolStatus);

            m_console.Commands.AddCommand(
                "Debug", false, "debug threadpool level",
                "debug threadpool level 0.." + Util.MAX_THREADPOOL_LEVEL,
                "Turn on logging of activity in the main thread pool.",
                "Log levels:\n"
                    + "  0 = no logging\n"
                    + "  1 = only first line of stack trace; don't log common threads\n"
                    + "  2 = full stack trace; don't log common threads\n"
                    + "  3 = full stack trace, including common threads\n",
                HandleDebugThreadpoolLevel);

//            m_console.Commands.AddCommand(
//                "Debug", false, "show threadpool calls active",
//                "show threadpool calls active",
//                "Show details about threadpool calls that are still active (currently waiting or in progress)",
//                HandleShowThreadpoolCallsActive);

            m_console.Commands.AddCommand(
                "Debug", false, "show threadpool calls complete",
                "show threadpool calls complete",
                "Show details about threadpool calls that have been completed.",
                HandleShowThreadpoolCallsComplete);

            m_console.Commands.AddCommand(
                "Debug", false, "force gc",
                "force gc",
                "Manually invoke runtime garbage collection.  For debugging purposes",
                HandleForceGc);

            m_console.Commands.AddCommand(
                "General", false, "quit",
                "quit",
                "Quit the application", (mod, args) => Shutdown());

            m_console.Commands.AddCommand(
                "General", false, "shutdown",
                "shutdown",
                "Quit the application", (mod, args) => Shutdown());

            ChecksManager.RegisterConsoleCommands(m_console);
            StatsManager.RegisterConsoleCommands(m_console);
        }

        public void RegisterCommonComponents(IConfigSource configSource)
        {
//            IConfig networkConfig = configSource.Configs["Network"];

            m_serverStatsCollector = new ServerStatsCollector();
            m_serverStatsCollector.Initialise(configSource);
            m_serverStatsCollector.Start();
        }

        private void HandleShowThreadpoolCallsActive(string module, string[] args)
        {
            List<KeyValuePair<string, int>> calls = Util.GetFireAndForgetCallsInProgress().ToList();
            calls.Sort((kvp1, kvp2) => kvp2.Value.CompareTo(kvp1.Value));
            int namedCalls = 0;

            ConsoleDisplayList cdl = new ConsoleDisplayList();
            foreach (KeyValuePair<string, int> kvp in calls)
            {
                if (kvp.Value > 0)
                {
                    cdl.AddRow(kvp.Key, kvp.Value);
                    namedCalls += kvp.Value;
                }
            }

            cdl.AddRow("TOTAL NAMED", namedCalls);

            long allQueuedCalls = Util.TotalQueuedFireAndForgetCalls;
            long allRunningCalls = Util.TotalRunningFireAndForgetCalls;

            cdl.AddRow("TOTAL QUEUED", allQueuedCalls);
            cdl.AddRow("TOTAL RUNNING", allRunningCalls);
            cdl.AddRow("TOTAL ANONYMOUS", allQueuedCalls + allRunningCalls - namedCalls);
            cdl.AddRow("TOTAL ALL", allQueuedCalls + allRunningCalls);

            MainConsole.Instance.Output(cdl.ToString());
        }

        private void HandleShowThreadpoolCallsComplete(string module, string[] args)
        {
            List<KeyValuePair<string, int>> calls = Util.GetFireAndForgetCallsMade().ToList();
            calls.Sort((kvp1, kvp2) => kvp2.Value.CompareTo(kvp1.Value));
            int namedCallsMade = 0;

            ConsoleDisplayList cdl = new ConsoleDisplayList();
            foreach (KeyValuePair<string, int> kvp in calls)
            {
                cdl.AddRow(kvp.Key, kvp.Value);
                namedCallsMade += kvp.Value;
            }

            cdl.AddRow("TOTAL NAMED", namedCallsMade);

            long allCallsMade = Util.TotalFireAndForgetCallsMade;
            cdl.AddRow("TOTAL ANONYMOUS", allCallsMade - namedCallsMade);
            cdl.AddRow("TOTAL ALL", allCallsMade);

            MainConsole.Instance.Output(cdl.ToString());
        }

        private void HandleDebugThreadpoolStatus(string module, string[] args)
        {
            int workerThreads, iocpThreads;

            ThreadPool.GetMinThreads(out workerThreads, out iocpThreads);
            Notice("Min worker threads:       {0}", workerThreads);
            Notice("Min IOCP threads:         {0}", iocpThreads);

            ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);
            Notice("Max worker threads:       {0}", workerThreads);
            Notice("Max IOCP threads:         {0}", iocpThreads);

            ThreadPool.GetAvailableThreads(out workerThreads, out iocpThreads);
            Notice("Available worker threads: {0}", workerThreads);
            Notice("Available IOCP threads:   {0}", iocpThreads);
        }

        private void HandleDebugThreadpoolSet(string module, string[] args)
        {
            if (args.Length != 6)
            {
                Notice("Usage: debug threadpool set worker|iocp min|max <n>");
                return;
            }

            int newThreads;

            if (!ConsoleUtil.TryParseConsoleInt(m_console, args[5], out newThreads))
                return;

            string poolType = args[3];
            string bound = args[4];

            bool fail = false;
            int workerThreads, iocpThreads;

            if (poolType == "worker")
            {
                if (bound == "min")
                {
                    ThreadPool.GetMinThreads(out workerThreads, out iocpThreads);

                    if (!ThreadPool.SetMinThreads(newThreads, iocpThreads))
                        fail = true;
                }
                else
                {
                    ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);

                    if (!ThreadPool.SetMaxThreads(newThreads, iocpThreads))
                        fail = true;
                }
            }
            else
            {
                if (bound == "min")
                {
                    ThreadPool.GetMinThreads(out workerThreads, out iocpThreads);

                    if (!ThreadPool.SetMinThreads(workerThreads, newThreads))
                        fail = true;
                }
                else
                {
                    ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);

                    if (!ThreadPool.SetMaxThreads(workerThreads, newThreads))
                        fail = true;
                }
            }

            if (fail)
            {
                Notice("ERROR: Could not set {0} {1} threads to {2}", poolType, bound, newThreads);
            }
            else
            {
                int minWorkerThreads, maxWorkerThreads, minIocpThreads, maxIocpThreads;

                ThreadPool.GetMinThreads(out minWorkerThreads, out minIocpThreads);
                ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxIocpThreads);

                Notice("Min worker threads now {0}", minWorkerThreads);
                Notice("Min IOCP threads now {0}", minIocpThreads);
                Notice("Max worker threads now {0}", maxWorkerThreads);
                Notice("Max IOCP threads now {0}", maxIocpThreads);
            }
        }

        private static void HandleDebugThreadpoolLevel(string module, string[] cmdparams)
        {
            if (cmdparams.Length < 4)
            {
                MainConsole.Instance.Output("Usage: debug threadpool level 0.." + Util.MAX_THREADPOOL_LEVEL);
                return;
            }

            string rawLevel = cmdparams[3];
            int newLevel;

            if (!int.TryParse(rawLevel, out newLevel))
            {
                MainConsole.Instance.Output("{0} is not a valid debug level", rawLevel);
                return;
            }

            if (newLevel < 0 || newLevel > Util.MAX_THREADPOOL_LEVEL)
            {
                MainConsole.Instance.Output("{0} is outside the valid debug level range of 0.." + Util.MAX_THREADPOOL_LEVEL, null, newLevel);
                return;
            }

            Util.LogThreadPool = newLevel;
            MainConsole.Instance.Output("LogThreadPool set to {0}", newLevel);
        }

        private void HandleForceGc(string module, string[] args)
        {
            Notice("Manually invoking runtime garbage collection");
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.Default;
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

                case "version":
                    Notice(GetVersionText());
                    break;

                case "uptime":
                    Notice(GetUptimeReport());
                    break;

                case "threads":
                    Notice(GetThreadsReport());
                    break;
            }
        }

        /// <summary>
        /// Change and load configuration file data.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmd"></param>
        private void HandleConfig(string module, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] cmdparams = args.ToArray();

            if (cmdparams.Length > 0)
            {
                string firstParam = cmdparams[0].ToLower();

                switch (firstParam)
                {
                    case "set":
                        if (cmdparams.Length < 4)
                        {
                            Notice("Syntax: config set <section> <key> <value>");
                            Notice("Example: config set ScriptEngine.DotNetEngine NumberOfScriptThreads 5");
                        }
                        else
                        {
                            IConfig c;
                            IConfigSource source = new IniConfigSource();
                            c = source.AddConfig(cmdparams[1]);
                            if (c != null)
                            {
                                string _value = String.Join(" ", cmdparams, 3, cmdparams.Length - 3);
                                c.Set(cmdparams[2], _value);
                                Config.Merge(source);

                                Notice("In section [{0}], set {1} = {2}", c.Name, cmdparams[2], _value);
                            }
                        }
                        break;

                    case "get":
                    case "show":
                        if (cmdparams.Length == 1)
                        {
                            foreach (IConfig config in Config.Configs)
                            {
                                Notice("[{0}]", config.Name);
                                string[] keys = config.GetKeys();
                                foreach (string key in keys)
                                    Notice("  {0} = {1}", key, config.GetString(key));
                            }
                        }
                        else if (cmdparams.Length == 2 || cmdparams.Length == 3)
                        {
                            IConfig config = Config.Configs[cmdparams[1]];
                            if (config == null)
                            {
                                Notice("Section \"{0}\" does not exist.",cmdparams[1]);
                                break;
                            }
                            else
                            {
                                if (cmdparams.Length == 2)
                                {
                                    Notice("[{0}]", config.Name);
                                    foreach (string key in config.GetKeys())
                                        Notice("  {0} = {1}", key, config.GetString(key));
                                }
                                else
                                {
                                    Notice(
                                        "config get {0} {1} : {2}",
                                        cmdparams[1], cmdparams[2], config.GetString(cmdparams[2]));
                                }
                            }
                        }
                        else
                        {
                            Notice("Syntax: config {0} [<section>] [<key>]", firstParam);
                            Notice("Example: config {0} ScriptEngine.DotNetEngine NumberOfScriptThreads", firstParam);
                        }

                        break;

                    case "save":
                        if (cmdparams.Length < 2)
                        {
                            Notice("Syntax: config save <path>");
                            return;
                        }

                        string path = cmdparams[1];
                        Notice("Saving configuration file: {0}", path);

                        if (Config is IniConfigSource)
                        {
                            IniConfigSource iniCon = (IniConfigSource)Config;
                            iniCon.Save(path);
                        }
                        else if (Config is XmlConfigSource)
                        {
                            XmlConfigSource xmlCon = (XmlConfigSource)Config;
                            xmlCon.Save(path);
                        }

                        break;
                }
            }
        }

        private void HandleSetLogLevel(string module, string[] cmd)
        {
            if (cmd.Length != 4)
            {
                Notice("Usage: set log level <level>");
                return;
            }

            if (null == m_consoleAppender)
            {
                Notice("No appender named Console found (see the log4net config file for this executable)!");
                return;
            }

            string rawLevel = cmd[3];

            ILoggerRepository repository = LogManager.GetRepository();
            Level consoleLevel = repository.LevelMap[rawLevel];

            if (consoleLevel != null)
                m_consoleAppender.Threshold = consoleLevel;
            else
                Notice(
                    "{0} is not a valid logging level.  Valid logging levels are ALL, DEBUG, INFO, WARN, ERROR, FATAL, OFF",
                    rawLevel);

            ShowLogLevel();
        }

        private void ShowLogLevel()
        {
            Notice("Console log level is {0}", m_consoleAppender.Threshold);
        }

        protected virtual void HandleScript(string module, string[] parms)
        {
            if (parms.Length != 2)
            {
                Notice("Usage: command-script <path-to-script");
                return;
            }

            RunCommandScript(parms[1]);
        }

        /// <summary>
        /// Run an optional startup list of commands
        /// </summary>
        /// <param name="fileName"></param>
        protected void RunCommandScript(string fileName)
        {
            if (m_console == null)
                return;

            if (File.Exists(fileName))
            {
                m_log.Info("[SERVER BASE]: Running " + fileName);

                using (StreamReader readFile = File.OpenText(fileName))
                {
                    string currentCommand;
                    while ((currentCommand = readFile.ReadLine()) != null)
                    {
                        currentCommand = currentCommand.Trim();
                        if (!(currentCommand == ""
                            || currentCommand.StartsWith(";")
                            || currentCommand.StartsWith("//")
                            || currentCommand.StartsWith("#")))
                        {
                            m_log.Info("[SERVER BASE]: Running '" + currentCommand + "'");
                            m_console.RunCommand(currentCommand);
                        }
                    }
                }
            }
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

        protected void ShowInfo()
        {
            Notice(GetVersionText());
            Notice("Startup directory: " + m_startupDirectory);
            if (null != m_consoleAppender)
                Notice(String.Format("Console log level: {0}", m_consoleAppender.Threshold));
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
//                m_log.DebugFormat("[SERVER BASE]: Found {0}", gitRefPointerPath);

                string rawPointer = "";

                using (StreamReader pointerFile = File.OpenText(gitRefPointerPath))
                    rawPointer = pointerFile.ReadLine();

//                m_log.DebugFormat("[SERVER BASE]: rawPointer [{0}]", rawPointer);

                Match m = Regex.Match(rawPointer, "^ref: (.+)$");

                if (m.Success)
                {
//                    m_log.DebugFormat("[SERVER BASE]: Matched [{0}]", m.Groups[1].Value);

                    string gitRef = m.Groups[1].Value;
                    string gitRefPath = gitDir + gitRef;
                    if (File.Exists(gitRefPath))
                    {
//                        m_log.DebugFormat("[SERVER BASE]: Found gitRefPath [{0}]", gitRefPath);

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
                    buildVersion = buildVersion.Trim();
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

        public string GetVersionText()
        {
            return String.Format("Version: {0} (SIMULATION/{1} - SIMULATION/{2})",
                m_version, VersionInfo.SimulationServiceVersionSupportedMin, VersionInfo.SimulationServiceVersionSupportedMax);
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

            sb.Append(GetThreadPoolReport());

            sb.Append("\n");
            int totalThreads = Process.GetCurrentProcess().Threads.Count;
            if (totalThreads > 0)
                sb.AppendFormat("Total process threads active: {0}\n\n", totalThreads);

            return sb.ToString();
        }

        /// <summary>
        /// Get a thread pool report.
        /// </summary>
        /// <returns></returns>
        public static string GetThreadPoolReport()
        {

            StringBuilder sb = new StringBuilder();

            // framework pool is alwasy active
            int maxWorkers;
            int minWorkers;
            int curWorkers;
            int maxComp;
            int minComp;
            int curComp;

            try
            {
                ThreadPool.GetMaxThreads(out maxWorkers, out maxComp);
                ThreadPool.GetMinThreads(out minWorkers, out minComp);
                ThreadPool.GetAvailableThreads(out curWorkers, out curComp);
                curWorkers = maxWorkers - curWorkers;
                curComp = maxComp - curComp;

                sb.Append("\nFramework main threadpool \n");
                sb.AppendFormat("workers:    {0} ({1} / {2})\n", curWorkers, maxWorkers, minWorkers);
                sb.AppendFormat("Completion: {0} ({1} / {2})\n", curComp, maxComp, minComp);
            }
            catch { }

            if (Util.FireAndForgetMethod == FireAndForgetMethod.QueueUserWorkItem)
            {
                sb.AppendFormat("\nThread pool used: Framework main threadpool\n");
                return sb.ToString();
            }

            string threadPoolUsed = null;
            int maxThreads = 0;
            int minThreads = 0;
            int allocatedThreads = 0;
            int inUseThreads = 0;
            int waitingCallbacks = 0;

            if (Util.FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool)
            {
                STPInfo stpi = Util.GetSmartThreadPoolInfo();

                // ROBUST currently leaves this the FireAndForgetMethod but never actually initializes the threadpool.
                if (stpi != null)
                {
                    threadPoolUsed = "SmartThreadPool";
                    maxThreads = stpi.MaxThreads;
                    minThreads = stpi.MinThreads;
                    inUseThreads = stpi.InUseThreads;
                    allocatedThreads = stpi.ActiveThreads;
                    waitingCallbacks = stpi.WaitingCallbacks;
                }
            }
 
            if (threadPoolUsed != null)
            {
                sb.Append("\nThreadpool (excluding script engine pools)\n");
                sb.AppendFormat("Thread pool used           : {0}\n", threadPoolUsed);
                sb.AppendFormat("Max threads                : {0}\n", maxThreads);
                sb.AppendFormat("Min threads                : {0}\n", minThreads);
                sb.AppendFormat("Allocated threads          : {0}\n", allocatedThreads < 0 ? "not applicable" : allocatedThreads.ToString());
                sb.AppendFormat("In use threads             : {0}\n", inUseThreads);
                sb.AppendFormat("Work items waiting         : {0}\n", waitingCallbacks < 0 ? "not available" : waitingCallbacks.ToString());
            }
            else
            {
                sb.AppendFormat("Thread pool not used\n");
            }

            return sb.ToString();
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
                MainConsole.Instance.Output("Aborted thread with id {0}", threadId);
            else
                MainConsole.Instance.Output("ERROR - Thread with id {0} not found in managed threads", threadId);
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
        protected void Notice(string format, params object[] components)
        {
            if (m_console != null)
                m_console.Output(format, components);
        }

        public virtual void Shutdown()
        {
            m_serverStatsCollector.Close();
            ShutdownSpecific();
        }

        /// <summary>
        /// Should be overriden and referenced by descendents if they need to perform extra shutdown processing
        /// </summary>
        protected virtual void ShutdownSpecific() {}
    }
}
