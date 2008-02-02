using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// This class does maintenance on script engine.
    /// </summary>
    public class MaintenanceThread : iScriptEngineFunctionModule
    {
        public ScriptEngine m_ScriptEngine;
        private int MaintenanceLoopms;

        public MaintenanceThread(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;

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
            MaintenanceLoopms = m_ScriptEngine.ScriptConfigSource.GetInt("MaintenanceLoopms", 50);
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
            m_ScriptEngine.Log.Debug(m_ScriptEngine.ScriptEngineName, "StopMaintenanceThread() called");
#endif
            PleaseShutdown = true;
            Thread.Sleep(100);
            try
            {
                if (MaintenanceThreadThread != null)
                {
                    if (MaintenanceThreadThread.IsAlive)
                    {
                        MaintenanceThreadThread.Abort();
                    }
                }
            }
            catch (Exception ex)
            {
                m_ScriptEngine.Log.Error(m_ScriptEngine.ScriptEngineName, "Exception stopping maintenence thread: " + ex.ToString());
            }

        }

        /// <summary>
        /// A thread should run in this loop and check all running scripts
        /// </summary>
        public void MaintenanceLoop()
        {
            if (m_ScriptEngine.m_EventQueueManager.maxFunctionExecutionTimens < MaintenanceLoopms)
                m_ScriptEngine.Log.Warn(m_ScriptEngine.ScriptEngineName,
                    "Configuration error: MaxEventExecutionTimeMs is less than MaintenanceLoopms. The Maintenance Loop will only check scripts once per run.");

            long Last_maxFunctionExecutionTimens = 0; // DateTime.Now.Ticks;
            long Last_ReReadConfigFilens = DateTime.Now.Ticks;
            while (true)
            {
                try
                {
                    while (true)
                    {
                        System.Threading.Thread.Sleep(MaintenanceLoopms); // Sleep before next pass
                        if (PleaseShutdown)
                            return;

                        if (m_ScriptEngine != null)
                        {
                            //
                            // Re-reading config every x seconds
                            //
                            if (m_ScriptEngine.RefreshConfigFilens > 0)
                            {
                                // Check if its time to re-read config
                                if (DateTime.Now.Ticks - Last_ReReadConfigFilens > m_ScriptEngine.RefreshConfigFilens)
                                {
                                    //Console.WriteLine("Time passed: " + (DateTime.Now.Ticks - Last_ReReadConfigFilens) + ">" + m_ScriptEngine.RefreshConfigFilens );
                                    // Its time to re-read config file
                                    m_ScriptEngine.ReadConfig();
                                    Last_ReReadConfigFilens = DateTime.Now.Ticks; // Reset time
                                }
                            }

                            //
                            // Adjust number of running script threads if not correct
                            //
                            if (m_ScriptEngine.m_EventQueueManager != null)
                                m_ScriptEngine.m_EventQueueManager.AdjustNumberOfScriptThreads();

                            //
                            // Check if any script has exceeded its max execution time
                            //
                            if (m_ScriptEngine.m_EventQueueManager != null && m_ScriptEngine.m_EventQueueManager.EnforceMaxExecutionTime)
                            {
                                // We are enforcing execution time
                                if (DateTime.Now.Ticks - Last_maxFunctionExecutionTimens >
                                    m_ScriptEngine.m_EventQueueManager.maxFunctionExecutionTimens)
                                {
                                    // Its time to check again
                                    m_ScriptEngine.m_EventQueueManager.CheckScriptMaxExecTime(); // Do check
                                    Last_maxFunctionExecutionTimens = DateTime.Now.Ticks; // Reset time
                                }
                            }
                        } // m_ScriptEngine != null
                    }
                }
                catch (Exception ex)
                {
                    m_ScriptEngine.Log.Error(m_ScriptEngine.ScriptEngineName, "Exception in MaintenanceLoopThread. Thread will recover after 5 sec throttle. Exception: " + ex.ToString());
                    Thread.Sleep(5000);
                }
            }
        }
        #endregion
        /// <summary>
        /// If set to true then threads and stuff should try to make a graceful exit
        /// </summary>
        public bool PleaseShutdown
        {
            get { return _PleaseShutdown; }
            set { _PleaseShutdown = value; }
        }
        private bool _PleaseShutdown = false;

    }
}
