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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using OpenSim.GridLaunch.GUI;
using OpenSim.GridLaunch.GUI.Network;

namespace OpenSim.GridLaunch
{
    class Program
    {
        public static readonly string ConfigFile = "OpenSim.GridLaunch.ini";
        internal static Dictionary<string, AppExecutor> AppList = new Dictionary<string, AppExecutor>();
        private static readonly int delayBetweenExecuteSeconds = 10;
        //private static readonly int consoleReadIntervalMilliseconds = 50;
        ////private static readonly Timer readTimer = new Timer(readConsole, null, Timeout.Infinite, Timeout.Infinite);
        //private static Thread timerThread;
        //private static object timerThreadLock = new object();
        private static IGUI GUIModule;
        private static string GUIModuleName = "";
        public static readonly CommandProcessor Command = new CommandProcessor();
        public static readonly Settings Settings = new Settings();

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void AppConsoleOutputDelegate(string App, string Text);
        public static event AppConsoleOutputDelegate AppConsoleOutput;
        public delegate void AppConsoleErrorDelegate(string App, string Text);
        public static event AppConsoleErrorDelegate AppConsoleError;
        public delegate void AppCreatedDelegate(string App);
        public static event AppCreatedDelegate AppCreated;
        public delegate void AppRemovedDelegate(string App);
        public static event AppRemovedDelegate AppRemoved;

        internal static void FireAppConsoleOutput(string App, string Text)
        {
            if (AppConsoleOutput != null)
                AppConsoleOutput(App, Text);
        }
        internal static void FireAppConsoleError(string App, string Text)
        {
            if (AppConsoleError != null)
                AppConsoleError(App, Text);
        }
        

        private static readonly object startStopLock = new object();

        public static string Name { get { return "OpenSim Grid executor"; } }

        #region Start/Shutdown
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            // Startup
            m_log.Info(Name);
            m_log.Info(new string('-', Name.Length));

            // Read settings
            Settings.LoadConfig(ConfigFile);
            // Command line arguments override settings
            Settings.ParseCommandArguments(args);

            // Start GUI module
            StartGUIModule();

            // Start the processes
            ThreadPool.QueueUserWorkItem(startProcesses);

            // Hand over thread control to whatever GUI module
            GUIModule.StartGUI();

            // GUI module returned, we are done
            Shutdown();

        }

        private static void StartGUIModule()
        {
            // Create GUI module
            GUIModuleName = Settings["GUI"];

            switch (GUIModuleName.ToLower())
            {
                case "winform":
                    GUIModuleName = "WinForm";
                    GUIModule = new GUI.WinForm.ProcessPanel();
                    break;
                case "service":
                    GUIModuleName = "Service";
                    GUIModule = new Service();
                    break;
                case "tcpd":
                    GUIModuleName = "TCPD";
                    GUIModule = new TCPD();
                    break;
                case "console":
                default:
                    GUIModuleName = "Console";
                    GUIModule = new GUI.Console.Console();
                    break;
            }
            m_log.Info("GUI type: " + GUIModuleName);

        }

        internal static void Shutdown()
        {
            // Stop the processes
            stopProcesses();

            lock (startStopLock)
            {
                // Stop GUI module
                if (GUIModule != null)
                {
                    GUIModule.StopGUI();
                    GUIModule = null;
                }
            }
        }

        internal static void SafeDisposeOf(object obj)
        {
            IDisposable o = obj as IDisposable;
            try
            {
                if (o != null)
                    o.Dispose();
            }
            catch { }
        }
        #endregion

        #region Start / Stop applications
        private static void startProcesses(Object stateInfo)
        {
            // Stop before starting
            stopProcesses();

            // Start console read timer
            //timer_Start();

                // Start the applications
                foreach (string file in new ArrayList(Settings.Components.Keys))
                {
                    // Is this file marked for startup?
                    if (Settings.Components[file])
                    {
                        AppExecutor app = new AppExecutor(file);
                        app.Start();
                        AppList.Add(file, app);
                        if (AppCreated != null)
                            AppCreated(app.File);
                        System.Threading.Thread.Sleep(1000*delayBetweenExecuteSeconds);
                    }
                }
        }

        private static void stopProcesses()
        {
            // Stop timer
            //timer_Stop();

            // Lock so we don't collide with any timer still executing on AppList
            lock (AppList)
            {
                // Start the applications
                foreach (AppExecutor app in AppList.Values)
                {
                    try
                    {
                        m_log.Info("Stopping: " + app.File);
                        app.Stop();
                    }
                    catch (Exception ex)
                    {
                        m_log.ErrorFormat("Exception while stopping \"{0}\": {1}", app.File, ex.ToString());
                    }
                    finally
                    {
                        if (AppRemoved != null)
                            AppRemoved(app.File);
                        app.Dispose();
                    }

                }
                AppList.Clear();
            }
        }
        #endregion

       public static void Write(string App, string Text)
        {
            // Check if it is a commands
            bool isCommand = Command.Process(App, Text);

            // Write to stdInput of app
            if (!isCommand && AppList.ContainsKey(App))
                AppList[App].Write(Text);
        }

        public static void WriteLine(string App, string Text)
        {
            // Check if it is a commands
            bool isCommand = Command.Process(App, Text);

            // Write to stdInput of app
            if (!isCommand && AppList.ContainsKey(App))
                AppList[App].WriteLine(Text);
        }
    }
}
