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
using System.IO;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Xml;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using log4net;
using log4net.Config;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using Nini.Config;

namespace OpenSim.Server.Base
{
    public class ServicesServerBase
    {
        // Logger
        //
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        // Command line args
        //
        protected string[] m_Arguments;

        // Configuration
        //
        protected IConfigSource m_Config = null;

        public IConfigSource Config
        {
            get { return m_Config; }
        }

        // Run flag
        //
        private bool m_Running = true;

        // PID file
        //
        private string m_pidFile = String.Empty;

        // Handle all the automagical stuff
        //
        public ServicesServerBase(string prompt, string[] args)
        {
            // Save raw arguments
            //
            m_Arguments = args;

            // Read command line
            //
            ArgvConfigSource argvConfig = new ArgvConfigSource(args);

            argvConfig.AddSwitch("Startup", "console", "c");
            argvConfig.AddSwitch("Startup", "logfile", "l");
            argvConfig.AddSwitch("Startup", "inifile", "i");
            argvConfig.AddSwitch("Startup", "prompt",  "p");
            argvConfig.AddSwitch("Startup", "logconfig", "g");

            // Automagically create the ini file name
            //
            string fileName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
            string iniFile = fileName + ".ini";
            string logConfig = null;

            IConfig startupConfig = argvConfig.Configs["Startup"];
            if (startupConfig != null)
            {
                // Check if a file name was given on the command line
                //
                iniFile = startupConfig.GetString("inifile", iniFile);
                //
                // Check if a prompt was given on the command line
                prompt = startupConfig.GetString("prompt", prompt);
                //
                // Check for a Log4Net config file on the command line
                logConfig =startupConfig.GetString("logconfig",logConfig);
            }

            // Find out of the file name is a URI and remote load it
            // if it's possible. Load it as a local file otherwise.
            //
            Uri configUri;

            try
            {
                if (Uri.TryCreate(iniFile, UriKind.Absolute, out configUri) &&
                    configUri.Scheme == Uri.UriSchemeHttp)
                {
                    XmlReader r = XmlReader.Create(iniFile);
                    m_Config = new XmlConfigSource(r);
                }
                else
                {
                    m_Config = new IniConfigSource(iniFile);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Error reading from config source.  {0}", e.Message);
                Environment.Exit(1);
            }

            // Merge the configuration from the command line into the
            // loaded file
            //
            m_Config.Merge(argvConfig);

            // Refresh the startupConfig post merge
            //
            if (m_Config.Configs["Startup"] != null)
            {
                startupConfig = m_Config.Configs["Startup"];
            }

            prompt = startupConfig.GetString("Prompt", prompt);

            // Allow derived classes to load config before the console is
            // opened.
            //
            ReadConfig();

            // Create main console
            //
            string consoleType = "local";
            if (startupConfig != null)
                consoleType = startupConfig.GetString("console", consoleType);

            if (consoleType == "basic")
            {
                MainConsole.Instance = new CommandConsole(prompt);
            }
            else if (consoleType == "rest")
            {
                MainConsole.Instance = new RemoteConsole(prompt);
                ((RemoteConsole)MainConsole.Instance).ReadConfig(Config);
            }
            else
            {
                MainConsole.Instance = new LocalConsole(prompt);
            }

            // Configure the appenders for log4net
            //
            OpenSimAppender consoleAppender = null;
            FileAppender fileAppender = null;

            if (logConfig != null)
            {
                FileInfo cfg = new FileInfo(logConfig);
                XmlConfigurator.Configure(cfg);
            }
            else
            {
                XmlConfigurator.Configure();
            }

            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "Console")
                {
                    consoleAppender = (OpenSimAppender)appender;
                }
                if (appender.Name == "LogFileAppender")
                {
                    fileAppender = (FileAppender)appender;
                }
            }

            if (consoleAppender == null)
            {
                System.Console.WriteLine("No console appender found. Server can't start");
                Thread.CurrentThread.Abort();
            }
            else
            {
                consoleAppender.Console = (ConsoleBase)MainConsole.Instance;

                if (null == consoleAppender.Threshold)
                    consoleAppender.Threshold = Level.All;
            }

            // Set log file
            //
            if (fileAppender != null)
            {
                if (startupConfig != null)
                {
                    string cfgFileName = startupConfig.GetString("logfile", null);
                    if (cfgFileName != null)
                    {
                        fileAppender.File = cfgFileName;
                        fileAppender.ActivateOptions();
                    }
                }
            }

            if (startupConfig.GetString("PIDFile", String.Empty) != String.Empty)
            {
                CreatePIDFile(startupConfig.GetString("PIDFile"));
            }

            // Register the quit command
            //
            MainConsole.Instance.Commands.AddCommand("General", false, "quit",
                    "quit",
                    "Quit the application", HandleQuit);

            MainConsole.Instance.Commands.AddCommand("General", false, "shutdown",
                    "shutdown",
                    "Quit the application", HandleQuit);
            
            // Register a command to read other commands from a file
            MainConsole.Instance.Commands.AddCommand("General", false, "command-script",
                                          "command-script <script>",
                                          "Run a command script from file", HandleScript);


            // Allow derived classes to perform initialization that
            // needs to be done after the console has opened
            //
            Initialise();
        }

        public bool Running
        {
            get { return m_Running; }
        }

        public virtual int Run()
        {
            while (m_Running)
            {
                try
                {
                    MainConsole.Instance.Prompt();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Command error: {0}", e);
                }
            }

            if (m_pidFile != String.Empty)
                File.Delete(m_pidFile);
            return 0;
        }

        protected virtual void HandleQuit(string module, string[] args)
        {
            m_Running = false;
            m_log.Info("[CONSOLE] Quitting");
        }

        protected virtual void HandleScript(string module, string[] parms)
        {
            if (parms.Length != 2)
            {
                return;
            }
            RunCommandScript(parms[1]);
        }

        /// <summary>
        /// Run an optional startup list of commands
        /// </summary>
        /// <param name="fileName"></param>
        private void RunCommandScript(string fileName)
        {
            if (File.Exists(fileName))
            {
                m_log.Info("[COMMANDFILE]: Running " + fileName);

                using (StreamReader readFile = File.OpenText(fileName))
                {
                    string currentCommand;
                    while ((currentCommand = readFile.ReadLine()) != null)
                    {
                        if (currentCommand != String.Empty)
                        {
                            m_log.Info("[COMMANDFILE]: Running '" + currentCommand + "'");
                            MainConsole.Instance.RunCommand(currentCommand);
                        }
                    }
                }
            }
        }


        protected virtual void ReadConfig()
        {
        }

        protected virtual void Initialise()
        {
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
    }
}
