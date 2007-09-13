using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Handles LSL commands that takes long time and returns an event, for example timers, HTTP requests, etc.
    /// </summary>
    class LSLLongCmdHandler
    {
        private Thread cmdHandlerThread;
        private int cmdHandlerThreadCycleSleepms = 100;

        private ScriptEngine m_ScriptEngine;
        public LSLLongCmdHandler(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;

            // Start the thread that will be doing the work
            cmdHandlerThread = new Thread(CmdHandlerThreadLoop);
            cmdHandlerThread.Name = "CmdHandlerThread";
            cmdHandlerThread.Priority = ThreadPriority.BelowNormal;
            cmdHandlerThread.IsBackground = true;
            cmdHandlerThread.Start();
        }
        ~LSLLongCmdHandler()
        {
            // Shut down thread
            try
            {
                if (cmdHandlerThread != null)
                {
                    if (cmdHandlerThread.IsAlive == true)
                    {
                        cmdHandlerThread.Abort();
                        cmdHandlerThread.Join();
                    }
                }
            }
            catch { }
        }

        private void CmdHandlerThreadLoop()
        {
            while (true)
            {
                // Check timers
                CheckTimerEvents();                    

                // Sleep before next cycle
                Thread.Sleep(cmdHandlerThreadCycleSleepms);
            }
        }

        /// <summary>
        /// Remove a specific script (and all its pending commands)
        /// </summary>
        /// <param name="m_localID"></param>
        /// <param name="m_itemID"></param>
        public void RemoveScript(uint m_localID, LLUUID m_itemID)
        {
            // Remove a specific script

            // Remove from: Timers
            UnSetTimerEvents(m_localID, m_itemID);
        }


        //
        // TIMER
        //
        private class TimerClass
        {
            public uint localID;
            public LLUUID itemID;
            public double interval;
            public DateTime next;
        }
        private List<TimerClass> Timers = new List<TimerClass>();
        private object ListLock = new object();
        public void SetTimerEvent(uint m_localID, LLUUID m_itemID, double sec)
        {
            Console.WriteLine("SetTimerEvent");

            // Always remove first, in case this is a re-set
            UnSetTimerEvents(m_localID, m_itemID);
            if (sec == 0) // Disabling timer
                return;

            // Add to timer
            TimerClass ts = new TimerClass();
            ts.localID = m_localID;
            ts.itemID = m_itemID;
            ts.interval = sec;
            ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
            lock (ListLock)
            {
                Timers.Add(ts);
            }
        }
        public void UnSetTimerEvents(uint m_localID, LLUUID m_itemID)
        {
            // Remove from timer
            lock (ListLock)
            {
                List<TimerClass> NewTimers = new List<TimerClass>();
                foreach (TimerClass ts in Timers)
                {
                    if (ts.localID != m_localID && ts.itemID != m_itemID)
                    {
                        NewTimers.Add(ts);
                    }
                }
                Timers.Clear();
                Timers = NewTimers;
            }
        }
        public void CheckTimerEvents()
        {
            // Nothing to do here?
            if (Timers.Count == 0)
                return;

            lock (ListLock)
            {

                // Go through all timers
                foreach (TimerClass ts in Timers)
                {
                    // Time has passed?
                    if (ts.next.ToUniversalTime() < DateTime.Now.ToUniversalTime())
                    {
                        // Add it to queue
                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "timer", new object[] { });
                        // set next interval


                        ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
                    }
                }
            } // lock
        }

    }
}
