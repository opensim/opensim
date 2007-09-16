using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;
using OpenSim.Region.ScriptEngine.Common;

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
                // Check HttpRequests
                CheckHttpRequests();

                // Sleep before next cycle
                Thread.Sleep(cmdHandlerThreadCycleSleepms);
            }
        }

        /// <summary>
        /// Remove a specific script (and all its pending commands)
        /// </summary>
        /// <param name="m_localID"></param>
        /// <param name="m_itemID"></param>
        public void RemoveScript(uint localID, LLUUID itemID)
        {
            // Remove a specific script

            // Remove from: Timers
            UnSetTimerEvents(localID, itemID);
            // Remove from: HttpRequest
            StopHttpRequest(localID, itemID);
        }

        #region TIMER

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
        private object TimerListLock = new object();
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
            lock (TimerListLock)
            {
                Timers.Add(ts);
            }
        }
        public void UnSetTimerEvents(uint m_localID, LLUUID m_itemID)
        {
            // Remove from timer
            lock (TimerListLock)
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

            lock (TimerListLock)
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
        #endregion

        #region HTTP REQUEST

        //
        // HTTP REAQUEST
        //
        private class HttpClass
        {
            public uint localID;
            public LLUUID itemID;
            public string url;
            public List<string> parameters;
            public string body;
            public DateTime next;

            public string response_request_id;
            public int response_status;
            public List<string> response_metadata;
            public string response_body;

            public void SendRequest()
            {
                // TODO: SEND REQUEST!!!
            }
            public void Stop()
            {
                // TODO: Cancel any ongoing request
            }
            public bool CheckResponse()
            {
                // TODO: Check if we got a response yet, return true if so -- false if not
                return true;

                // TODO: If we got a response, set the following then return true
                //response_request_id
                //response_status
                //response_metadata
                //response_body

            }
        }
        private List<HttpClass> HttpRequests = new List<HttpClass>();
        private object HttpListLock = new object();
        public void StartHttpRequest(uint localID, LLUUID itemID, string url, List<string> parameters, string body)
        {
            Console.WriteLine("StartHttpRequest");

            HttpClass htc = new HttpClass();
            htc.localID = localID;
            htc.itemID = itemID;
            htc.url = url;
            htc.parameters = parameters;
            htc.body = body;
            lock (HttpListLock)
            {

                //ADD REQUEST
                HttpRequests.Add(htc);
            }
        }
        public void StopHttpRequest(uint m_localID, LLUUID m_itemID)
        {
            // Remove from list
            lock (HttpListLock)
            {
                List<HttpClass> NewHttpList = new List<HttpClass>();
                foreach (HttpClass ts in HttpRequests)
                {
                    if (ts.localID != m_localID && ts.itemID != m_itemID)
                    {
                        // Keeping this one
                        NewHttpList.Add(ts);
                    }
                    else
                    {
                        // Shutting this one down
                        ts.Stop();
                    }
                }
                HttpRequests.Clear();
                HttpRequests = NewHttpList;
            }
        }
        public void CheckHttpRequests()
        {
            // Nothing to do here?
            if (HttpRequests.Count == 0)
                return;

            lock (HttpListLock)
            {
                foreach (HttpClass ts in HttpRequests)
                {

                    if (ts.CheckResponse() == true)
                    {
                        // Add it to event queue
                        //key request_id, integer status, list metadata, string body
                        object[] resobj = new object[] { ts.response_request_id, ts.response_status, ts.response_metadata, ts.response_body };
                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "http_response", resobj);
                        // Now stop it
                        StopHttpRequest(ts.localID, ts.itemID);
                    }
                }
            } // lock
        }
        #endregion

    }
}
