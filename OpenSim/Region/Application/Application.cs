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
using System.Net;
using System.Reflection;
using log4net;
using log4net.Config;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim
{
    /// <summary>
    /// Starting class for the OpenSimulator Region
    /// </summary>
    public class Application
    {
        /// <summary>
        /// Text Console Logger
        /// </summary>
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Path to the main ini Configuration file
        /// </summary>
        public static string iniFilePath = "";

        /// <summary>
        /// Save Crashes in the bin/crashes folder.  Configurable with m_crashDir
        /// </summary>
        public static bool m_saveCrashDumps = false;

        /// <summary>
        /// Directory to save crash reports to.  Relative to bin/
        /// </summary>
        public static string m_crashDir = "crashes";

        /// <summary>
        /// Instance of the OpenSim class.  This could be OpenSim or OpenSimBackground depending on the configuration
        /// </summary>
        protected static OpenSimBase m_sim = null;

        //could move our main function into OpenSimMain and kill this class
        public static void Main(string[] args)
        {
            // First line, hook the appdomain to the crash reporter
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            ServicePointManager.DefaultConnectionLimit = 12;

            // Add the arguments supplied when running the application to the configuration
            ArgvConfigSource configSource = new ArgvConfigSource(args);

            // Configure Log4Net
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

            m_log.InfoFormat(
                "[OPENSIM MAIN]: System Locale is {0}", System.Threading.Thread.CurrentThread.CurrentCulture);

            string monoThreadsPerCpu = System.Environment.GetEnvironmentVariable("MONO_THREADS_PER_CPU");

            m_log.InfoFormat(
                "[OPENSIM MAIN]: Environment variable MONO_THREADS_PER_CPU is {0}", monoThreadsPerCpu ?? "unset");

            // Increase the number of IOCP threads available. Mono defaults to a tragically low number
            int workerThreads, iocpThreads;
            System.Threading.ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);
            m_log.InfoFormat("[OPENSIM MAIN]: Runtime gave us {0} worker threads and {1} IOCP threads", workerThreads, iocpThreads);
            if (workerThreads < 500 || iocpThreads < 1000)
            {
                workerThreads = 500;
                iocpThreads = 1000;
                m_log.Info("[OPENSIM MAIN]: Bumping up to 500 worker threads and 1000 IOCP threads");
                System.Threading.ThreadPool.SetMaxThreads(workerThreads, iocpThreads);
            }

            // Check if the system is compatible with OpenSimulator.
            // Ensures that the minimum system requirements are met
            string supported = String.Empty;
            if (Util.IsEnvironmentSupported(ref supported))
            {
                m_log.Info("Environment is compatible.\n");
            }
            else
            {
                m_log.Warn("Environment is unsupported (" + supported + ")\n");
            }

            // Configure nIni aliases and localles
            Culture.SetCurrentCulture();


            // Validate that the user has the most basic configuration done
            // If not, offer to do the most basic configuration for them warning them along the way of the importance of 
            // reading these files.
            /*
            m_log.Info("Checking for reguired configuration...\n");

            bool OpenSim_Ini = (File.Exists(Path.Combine(Util.configDir(), "OpenSim.ini")))
                               || (File.Exists(Path.Combine(Util.configDir(), "opensim.ini")))
                               || (File.Exists(Path.Combine(Util.configDir(), "openSim.ini")))
                               || (File.Exists(Path.Combine(Util.configDir(), "Opensim.ini")));
            
            bool StanaloneCommon_ProperCased = File.Exists(Path.Combine(Path.Combine(Util.configDir(), "config-include"), "StandaloneCommon.ini"));
            bool StanaloneCommon_lowercased = File.Exists(Path.Combine(Path.Combine(Util.configDir(), "config-include"), "standalonecommon.ini"));
            bool GridCommon_ProperCased = File.Exists(Path.Combine(Path.Combine(Util.configDir(), "config-include"), "GridCommon.ini"));
            bool GridCommon_lowerCased = File.Exists(Path.Combine(Path.Combine(Util.configDir(), "config-include"), "gridcommon.ini"));

            if ((OpenSim_Ini) 
                && (
                (StanaloneCommon_ProperCased
                || StanaloneCommon_lowercased
                || GridCommon_ProperCased
                || GridCommon_lowerCased
                )))
            {
                m_log.Info("Required Configuration Files Found\n");
            }
            else
            {
                MainConsole.Instance = new LocalConsole("Region");
                string resp = MainConsole.Instance.CmdPrompt(
                                        "\n\n*************Required Configuration files not found.*************\n\n   OpenSimulator will not run without these files.\n\nRemember, these file names are Case Sensitive in Linux and Proper Cased.\n1. ./OpenSim.ini\nand\n2. ./config-include/StandaloneCommon.ini \nor\n3. ./config-include/GridCommon.ini\n\nAlso, you will want to examine these files in great detail because only the basic system will load by default. OpenSimulator can do a LOT more if you spend a little time going through these files.\n\n" + ": " + "Do you want to copy the most basic Defaults from standalone?",
                                        "yes");
                if (resp == "yes")
                {
                    
                        if (!(OpenSim_Ini))
                        {
                            try
                            {
                                File.Copy(Path.Combine(Util.configDir(), "OpenSim.ini.example"),
                                          Path.Combine(Util.configDir(), "OpenSim.ini"));
                            } catch (UnauthorizedAccessException)
                            {
                                MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, Make sure OpenSim has have the required permissions\n");
                            } catch (ArgumentException)
                            {
                                MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, The current directory is invalid.\n");
                            } catch (System.IO.PathTooLongException)
                            {
                                MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, the Path to these files is too long.\n");
                            } catch (System.IO.DirectoryNotFoundException)
                            {
                                MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, the current directory is reporting as not found.\n");
                            } catch (System.IO.FileNotFoundException)
                            {
                                MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, the example is not found, please make sure that the example files exist.\n");
                            } catch (System.IO.IOException)
                            {
                                // Destination file exists already or a hard drive failure...   ..    so we can just drop this one
                                //MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, the example is not found, please make sure that the example files exist.\n");
                            } catch (System.NotSupportedException)
                            {
                                MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, The current directory is invalid.\n");
                            }

                        }
                        if (!(StanaloneCommon_ProperCased || StanaloneCommon_lowercased))
                        {
                            try
                            {
                                File.Copy(Path.Combine(Path.Combine(Util.configDir(), "config-include"), "StandaloneCommon.ini.example"),
                                          Path.Combine(Path.Combine(Util.configDir(), "config-include"), "StandaloneCommon.ini"));
                            }
                            catch (UnauthorizedAccessException)
                            {
                                MainConsole.Instance.Output("Unable to Copy StandaloneCommon.ini.example to StandaloneCommon.ini, Make sure OpenSim has the required permissions\n");
                            }
                            catch (ArgumentException)
                            {
                                MainConsole.Instance.Output("Unable to Copy StandaloneCommon.ini.example to StandaloneCommon.ini, The current directory is invalid.\n");
                            }
                            catch (System.IO.PathTooLongException)
                            {
                                MainConsole.Instance.Output("Unable to Copy StandaloneCommon.ini.example to StandaloneCommon.ini, the Path to these files is too long.\n");
                            }
                            catch (System.IO.DirectoryNotFoundException)
                            {
                                MainConsole.Instance.Output("Unable to Copy StandaloneCommon.ini.example to StandaloneCommon.ini, the current directory is reporting as not found.\n");
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                MainConsole.Instance.Output("Unable to Copy StandaloneCommon.ini.example to StandaloneCommon.ini, the example is not found, please make sure that the example files exist.\n");
                            }
                            catch (System.IO.IOException)
                            {
                                // Destination file exists already or a hard drive failure...   ..    so we can just drop this one
                                //MainConsole.Instance.Output("Unable to Copy OpenSim.ini.example to OpenSim.ini, the example is not found, please make sure that the example files exist.\n");
                            }
                            catch (System.NotSupportedException)
                            {
                                MainConsole.Instance.Output("Unable to Copy StandaloneCommon.ini.example to StandaloneCommon.ini, The current directory is invalid.\n");
                            }
                        }
                }
                MainConsole.Instance = null;
            }
            */
            configSource.Alias.AddAlias("On", true);
            configSource.Alias.AddAlias("Off", false);
            configSource.Alias.AddAlias("True", true);
            configSource.Alias.AddAlias("False", false);
            configSource.Alias.AddAlias("Yes", true);
            configSource.Alias.AddAlias("No", false);

            configSource.AddSwitch("Startup", "background");
            configSource.AddSwitch("Startup", "inifile");
            configSource.AddSwitch("Startup", "inimaster");
            configSource.AddSwitch("Startup", "inidirectory");
            configSource.AddSwitch("Startup", "physics");
            configSource.AddSwitch("Startup", "gui");
            configSource.AddSwitch("Startup", "console");
            configSource.AddSwitch("Startup", "save_crashes");
            configSource.AddSwitch("Startup", "crash_dir");

            configSource.AddConfig("StandAlone");
            configSource.AddConfig("Network");

            // Check if we're running in the background or not
            bool background = configSource.Configs["Startup"].GetBoolean("background", false);

            // Check if we're saving crashes
            m_saveCrashDumps = configSource.Configs["Startup"].GetBoolean("save_crashes", false);

            // load Crash directory config
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
                        // Block thread here for input
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
            {
                return;
            }

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
                    using (StreamWriter m_crashLog = new StreamWriter(Path.Combine(m_crashDir, log)))
                    {
                        m_crashLog.WriteLine(msg);
                    }

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
