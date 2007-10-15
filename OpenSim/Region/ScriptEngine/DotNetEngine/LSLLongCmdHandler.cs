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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Interfaces;

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
                Thread.Sleep(25);
                // Check HttpRequests
                CheckHttpRequests();
                Thread.Sleep(25);
                // Check XMLRPCRequests
                CheckXMLRPCRequests();
                Thread.Sleep(25);
                // Check Listeners
                CheckListeners();
                Thread.Sleep(25);

                // Sleep before next cycle
                //Thread.Sleep(cmdHandlerThreadCycleSleepms);
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

        public void CheckXMLRPCRequests()
        {

            IXMLRPC xmlrpc = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();

            while (xmlrpc.hasRequests())
            {
                RPCRequestInfo rInfo = xmlrpc.GetNextRequest();
                System.Console.WriteLine("PICKED REQUEST");

                //Deliver data to prim's remote_data handler
                object[] resobj = new object[] { 
                    2, rInfo.GetChannelKey().ToString(), rInfo.GetMessageID().ToString(), "", rInfo.GetIntValue(), rInfo.GetStrVal() 
                };
                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                    rInfo.GetLocalID(), rInfo.GetItemID(), "remote_data", resobj
                );

            }

        }

        public void CheckListeners()
        {

            IWorldComm comms = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();

            while (comms.HasMessages())
            {
                ListenerInfo lInfo = comms.GetNextMessage();
                System.Console.WriteLine("PICKED LISTENER");

                //Deliver data to prim's listen handler
                object[] resobj = new object[] {
                    lInfo.GetChannel(), lInfo.GetName(), lInfo.GetID().ToString(), lInfo.GetMessage() 
                };

                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                    lInfo.GetLocalID(), lInfo.GetItemID(), "listen", resobj
                );

            }

        }




    }
}
