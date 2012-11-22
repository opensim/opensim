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
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using Nini.Config;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Servers
{
    public class ServerBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Console to be used for any command line output.  Can be null, in which case there should be no output.
        /// </summary>
        protected ICommandConsole m_console;

        protected OpenSimAppender m_consoleAppender;
        protected FileAppender m_logFileAppender; 

        protected DateTime m_startuptime;
        protected string m_startupDirectory = Environment.CurrentDirectory;

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

                m_log.InfoFormat("[LOGGING]: Logging started to file {0}", m_logFileAppender.File);
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
                "General", false, "show uptime", "show uptime", "Show server uptime", HandleShow);
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

                case "uptime":
                    Notice(GetUptimeReport());
                    break;
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
    }
}