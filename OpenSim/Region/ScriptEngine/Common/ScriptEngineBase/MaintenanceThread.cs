using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// This class does maintenance on script engine.
    /// </summary>
    public class MaintenanceThread
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

        private void ReadConfig()
        {
            MaintenanceLoopms = m_ScriptEngine.ScriptConfigSource.GetInt("MaintenanceLoopms", 50);
        }


        #region " Maintenance thread "
        /// <summary>
        /// Maintenance thread. Enforcing max execution time for example.
        /// </summary>
        public static Thread MaintenanceThreadThread;

        /// <summary>
        /// Starts maintenance thread
        /// </summary>
        private void StartMaintenanceThread()
        {
            StopMaintenanceThread();

            MaintenanceThreadThread = new Thread(MaintenanceLoop);
            MaintenanceThreadThread.Name = "ScriptMaintenanceThread";
            MaintenanceThreadThread.IsBackground = true;
            MaintenanceThreadThread.Start();
        }

        /// <summary>
        /// Stops maintenance thread
        /// </summary>
        private void StopMaintenanceThread()
        {
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
                m_ScriptEngine.Log.Error("EventQueueManager", "Exception stopping maintenence thread: " + ex.ToString());
            }

        }

        /// <summary>
        /// A thread should run in this loop and check all running scripts
        /// </summary>
        public void MaintenanceLoop()
        {
            try
            {
                long Last_maxFunctionExecutionTimens = 0;// DateTime.Now.Ticks;
                long Last_ReReadConfigFilens = DateTime.Now.Ticks;
                while (true)
                {
                    System.Threading.Thread.Sleep(MaintenanceLoopms);                           // Sleep

                    // Re-reading config every x seconds?
                    if (m_ScriptEngine.ReReadConfigFileSeconds > 0)
                    {
                        // Check if its time to re-read config
                        if (DateTime.Now.Ticks - Last_ReReadConfigFilens > m_ScriptEngine.ReReadConfigFilens)
                        {
                            // Its time to re-read config file
                            m_ScriptEngine.ConfigSource.Reload();                                                   // Re-read config
                            Last_ReReadConfigFilens = DateTime.Now.Ticks;                                           // Reset time
                        }
                    }

                    // Adjust number of running script threads if not correct
                    if (m_ScriptEngine.m_EventQueueManager.eventQueueThreads.Count != m_ScriptEngine.m_EventQueueManager.numberOfThreads)
                    {
                        m_ScriptEngine.m_EventQueueManager.AdjustNumberOfScriptThreads();
                    }


                    // Check if any script has exceeded its max execution time
                    if (m_ScriptEngine.m_EventQueueManager.EnforceMaxExecutionTime)
                    {
                        if (DateTime.Now.Ticks - Last_maxFunctionExecutionTimens > m_ScriptEngine.m_EventQueueManager.maxFunctionExecutionTimens)
                        {
                            m_ScriptEngine.m_EventQueueManager.CheckScriptMaxExecTime();                           // Do check
                            Last_maxFunctionExecutionTimens = DateTime.Now.Ticks;                                  // Reset time
                        }
                    }
                }
            }
            catch (ThreadAbortException tae)
            {
            }
        }
        #endregion
    }
}
