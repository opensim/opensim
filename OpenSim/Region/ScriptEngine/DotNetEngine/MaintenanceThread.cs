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
using System.Collections;
using System.Reflection;
using System.Threading;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// This class does maintenance on script engine.
    /// </summary>
    public class MaintenanceThread
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //public ScriptEngine m_ScriptEngine;
        private int MaintenanceLoopms;
        private int MaintenanceLoopTicks_ScriptLoadUnload;
        private int MaintenanceLoopTicks_Other;


        public MaintenanceThread()
        {
            //m_ScriptEngine = _ScriptEngine;

            ReadConfig();

            // Start maintenance thread
            StartMaintenanceThread();
        }

        ~MaintenanceThread()
        {
            StopMaintenanceThread();
        }

        public void ReadConfig()
        {
            // Bad hack, but we need a m_ScriptEngine :)
            lock (ScriptEngine.ScriptEngines)
            {
                foreach (ScriptEngine m_ScriptEngine in ScriptEngine.ScriptEngines)
                {
                    MaintenanceLoopms = m_ScriptEngine.ScriptConfigSource.GetInt("MaintenanceLoopms", 50);
                    MaintenanceLoopTicks_ScriptLoadUnload =
                        m_ScriptEngine.ScriptConfigSource.GetInt("MaintenanceLoopTicks_ScriptLoadUnload", 1);
                    MaintenanceLoopTicks_Other =
                        m_ScriptEngine.ScriptConfigSource.GetInt("MaintenanceLoopTicks_Other", 10);

                    return;
                }
            }
        }

        #region " Maintenance thread "
        /// <summary>
        /// Maintenance thread. Enforcing max execution time for example.
        /// </summary>
        public Thread MaintenanceThreadThread;

        /// <summary>
        /// Starts maintenance thread
        /// </summary>
        private void StartMaintenanceThread()
        {
            if (MaintenanceThreadThread == null)
            {
                MaintenanceThreadThread = new Thread(MaintenanceLoop);
                MaintenanceThreadThread.Name = "ScriptMaintenanceThread";
                MaintenanceThreadThread.IsBackground = true;
                MaintenanceThreadThread.Start();
            }
        }

        /// <summary>
        /// Stops maintenance thread
        /// </summary>
        private void StopMaintenanceThread()
        {
#if DEBUG
            //m_log.Debug("[" + m_ScriptEngine.ScriptEngineName + "]: StopMaintenanceThread() called");
#endif
            //PleaseShutdown = true;
            Thread.Sleep(100);
            try
            {
                if (MaintenanceThreadThread != null && MaintenanceThreadThread.IsAlive)
                {
                    MaintenanceThreadThread.Abort();
                }
            }
            catch (Exception)
            {
                //m_log.Error("[" + m_ScriptEngine.ScriptEngineName + "]: Exception stopping maintenence thread: " + ex.ToString());
            }
        }

        // private ScriptEngine lastScriptEngine; // Keep track of what ScriptEngine instance we are at so we can give exception
        /// <summary>
        /// A thread should run in this loop and check all running scripts
        /// </summary>
        public void MaintenanceLoop()
        {
            //if (m_ScriptEngine.m_EventQueueManager.maxFunctionExecutionTimens < MaintenanceLoopms)
            //    m_log.Warn("[" + m_ScriptEngine.ScriptEngineName + "]: " +
            //               "Configuration error: MaxEventExecutionTimeMs is less than MaintenanceLoopms. The Maintenance Loop will only check scripts once per run.");

            long Last_maxFunctionExecutionTimens = 0; // DateTime.Now.Ticks;
            long Last_ReReadConfigFilens = DateTime.Now.Ticks;
            int MaintenanceLoopTicks_ScriptLoadUnload_Count = 0;
            int MaintenanceLoopTicks_Other_Count = 0;
            bool MaintenanceLoopTicks_ScriptLoadUnload_ResetCount = false;
            bool MaintenanceLoopTicks_Other_ResetCount = false;

            while (true)
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(MaintenanceLoopms); // Sleep before next pass

                        // Reset counters?
                        if (MaintenanceLoopTicks_ScriptLoadUnload_ResetCount)
                        {
                            MaintenanceLoopTicks_ScriptLoadUnload_ResetCount = false;
                            MaintenanceLoopTicks_ScriptLoadUnload_Count = 0;
                        }
                        if (MaintenanceLoopTicks_Other_ResetCount)
                        {
                            MaintenanceLoopTicks_Other_ResetCount = false;
                            MaintenanceLoopTicks_Other_Count = 0;
                        }

                        // Increase our counters
                        MaintenanceLoopTicks_ScriptLoadUnload_Count++;
                        MaintenanceLoopTicks_Other_Count++;


                        //lock (ScriptEngine.ScriptEngines)
                        //{
                            foreach (ScriptEngine m_ScriptEngine in new ArrayList(ScriptEngine.ScriptEngines))
                            {
                                // lastScriptEngine = m_ScriptEngine;
                                // Re-reading config every x seconds
                                if (MaintenanceLoopTicks_Other_Count >= MaintenanceLoopTicks_Other)
                                {
                                    MaintenanceLoopTicks_Other_ResetCount = true;
                                    if (m_ScriptEngine.RefreshConfigFilens > 0)
                                    {
                                        // Check if its time to re-read config
                                        if (DateTime.Now.Ticks - Last_ReReadConfigFilens >
                                            m_ScriptEngine.RefreshConfigFilens)
                                        {
                                            //m_log.Debug("Time passed: " + (DateTime.Now.Ticks - Last_ReReadConfigFilens) + ">" + m_ScriptEngine.RefreshConfigFilens);
                                            // Its time to re-read config file
                                            m_ScriptEngine.ReadConfig();
                                            Last_ReReadConfigFilens = DateTime.Now.Ticks; // Reset time
                                        }


                                        // Adjust number of running script threads if not correct
                                        if (m_ScriptEngine.m_EventQueueManager != null)
                                            m_ScriptEngine.m_EventQueueManager.AdjustNumberOfScriptThreads();

                                        // Check if any script has exceeded its max execution time
                                        if (EventQueueManager.EnforceMaxExecutionTime)
                                        {
                                            // We are enforcing execution time
                                            if (DateTime.Now.Ticks - Last_maxFunctionExecutionTimens >
                                                EventQueueManager.maxFunctionExecutionTimens)
                                            {
                                                // Its time to check again
                                                m_ScriptEngine.m_EventQueueManager.CheckScriptMaxExecTime(); // Do check
                                                Last_maxFunctionExecutionTimens = DateTime.Now.Ticks; // Reset time
                                            }
                                        }
                                    }
                                }
                                if (MaintenanceLoopTicks_ScriptLoadUnload_Count >= MaintenanceLoopTicks_ScriptLoadUnload)
                                {
                                    MaintenanceLoopTicks_ScriptLoadUnload_ResetCount = true;
                                    // LOAD / UNLOAD SCRIPTS
                                    if (m_ScriptEngine.m_ScriptManager != null)
                                        m_ScriptEngine.m_ScriptManager.DoScriptLoadUnload();
                                }
                            }
                        //}
                    }
                }
                catch(ThreadAbortException)
                {
                    m_log.Error("Thread aborted in MaintenanceLoopThread.  If this is during shutdown, please ignore");
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Exception in MaintenanceLoopThread. Thread will recover after 5 sec throttle. Exception: {0}", ex.ToString());
                }
            }
        }
        #endregion

        ///// <summary>
        ///// If set to true then threads and stuff should try to make a graceful exit
        ///// </summary>
        //public bool PleaseShutdown
        //{
        //    get { return _PleaseShutdown; }
        //    set { _PleaseShutdown = value; }
        //}
        //private bool _PleaseShutdown = false;
    }
}
