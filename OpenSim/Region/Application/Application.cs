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
using log4net;
using log4net.Config;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim
{
    public class Application
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static string iniFilePath = "";

        public static bool m_saveCrashDumps = false;
        public static string m_crashDir = "crashes";

        protected static OpenSimBase m_sim = null;

        //could move our main function into OpenSimMain and kill this class
        public static void Main(string[] args)
        {
            // First line
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            ArgvConfigSource configSource = new ArgvConfigSource(args);
            configSource.AddSwitch("Startup", "logconfig");
            string logConfigFile = configSource.Configs["Startup"].GetString("logconfig", String.Empty);
            if (logConfigFile != String.Empty)
            {
                XmlConfigurator.Configure(new System.IO.FileInfo(logConfigFile));
                m_log.InfoFormat("[OPENSIM MAIN]: configured log4net using \"{0}\" as configuration file", 
                                 logConfigFile);
            } 
            else
            {
                XmlConfigurator.Configure();
                m_log.Info("[OPENSIM MAIN]: configured log4net using default OpenSim.exe.config");
            }

            m_log.Info("Performing compatibility checks... ");
            string supported = String.Empty;
            if (Util.IsEnvironmentSupported(ref supported))
            {
                m_log.Info("Environment is compatible.\n");
            }
            else
            {
                m_log.Warn("Environment is unsupported (" + supported + ")\n");
            }

            Culture.SetCurrentCulture();


            configSource.Alias.AddAlias("On", true);
            configSource.Alias.AddAlias("Off", false);
            configSource.Alias.AddAlias("True", true);
            configSource.Alias.AddAlias("False", false);

            configSource.AddSwitch("Startup", "background");
            configSource.AddSwitch("Startup", "inifile");
            configSource.AddSwitch("Startup", "inimaster");
            configSource.AddSwitch("Startup", "inidirectory");
            configSource.AddSwitch("Startup", "gridmode");
            configSource.AddSwitch("Startup", "physics");
            configSource.AddSwitch("Startup", "gui");

            configSource.AddConfig("StandAlone");
            configSource.AddConfig("Network");

            bool background = configSource.Configs["Startup"].GetBoolean("background", false);
            m_saveCrashDumps = configSource.Configs["Startup"].GetBoolean("save_crashes", false);
            m_crashDir = configSource.Configs["Startup"].GetString("crash_dir", m_crashDir);

            if (background)
            {
                m_sim = new OpenSimBackground(configSource);
                m_sim.Startup();
            }
            else
            {
                m_sim = new OpenSim(configSource);

                m_sim.Startup();

                while (true)
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
            }
        }

        private static bool _IsHandlingException = false; // Make sure we don't go recursive on ourself

        /// <summary>
        /// Global exception handler -- all unhandlet exceptions end up here :)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (_IsHandlingException)
                return;
            _IsHandlingException = true;
            // TODO: Add config option to allow users to turn off error reporting
            // TODO: Post error report (disabled for now)

            string msg = String.Empty;
            msg += "\r\n";
            msg += "APPLICATION EXCEPTION DETECTED: " + e.ToString() + "\r\n";
            msg += "\r\n";

            msg += "Exception: " + e.ExceptionObject.ToString() + "\r\n";
            Exception ex = (Exception) e.ExceptionObject;
            if (ex.InnerException != null)
            {
                msg += "InnerException: " + ex.InnerException.ToString() + "\r\n";
            }

            msg += "\r\n";
            msg += "Application is terminating: " + e.IsTerminating.ToString() + "\r\n";

            m_log.ErrorFormat("[APPLICATION]: {0}", msg);

            if (m_saveCrashDumps)
            {
                // Log exception to disk
                try
                {
                    if (!Directory.Exists(m_crashDir))
                    {
                        Directory.CreateDirectory(m_crashDir);
                    }
                    string log = Util.GetUniqueFilename(ex.GetType() + ".txt");
                    StreamWriter m_crashLog =
                        new StreamWriter(
                            Path.Combine(m_crashDir, log)
                            );

                    m_crashLog.WriteLine(msg);
                    m_crashLog.Close();

                    File.Copy("OpenSim.ini", Path.Combine(m_crashDir, log + "_OpenSim.ini"), true);
                }
                catch (Exception e2)
                {
                    m_log.ErrorFormat("[CRASH LOGGER CRASHED]: {0}", e2);
                }
            }

            _IsHandlingException = false;
        }
    }
}