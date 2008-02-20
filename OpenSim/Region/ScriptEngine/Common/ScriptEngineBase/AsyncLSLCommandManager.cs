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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Handles LSL commands that takes long time and returns an event, for example timers, HTTP requests, etc.
    /// </summary>
    public class AsyncLSLCommandManager : iScriptEngineFunctionModule
    {
        private Thread cmdHandlerThread;
        private int cmdHandlerThreadCycleSleepms;

        private ScriptEngine m_ScriptEngine;

        public AsyncLSLCommandManager(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;
            ReadConfig();

            // Start the thread that will be doing the work
            cmdHandlerThread = new Thread(CmdHandlerThreadLoop);
            cmdHandlerThread.Name = "CmdHandlerThread";
            cmdHandlerThread.Priority = ThreadPriority.BelowNormal;
            cmdHandlerThread.IsBackground = true;
            cmdHandlerThread.Start();
        }

        public void ReadConfig()
        {
            cmdHandlerThreadCycleSleepms = m_ScriptEngine.ScriptConfigSource.GetInt("AsyncLLCommandLoopms", 50);
        }


        ~AsyncLSLCommandManager()
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
            catch
            {
            }
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
            IHttpRequests iHttpReq =
                m_ScriptEngine.World.RequestModuleInterface<IHttpRequests>();
            iHttpReq.StopHttpRequest(localID, itemID);

            IWorldComm comms = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            comms.DeleteListener(itemID);

            IXMLRPC xmlrpc = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpc.DeleteChannels(itemID);

            xmlrpc.CancelSRDRequests(itemID);

        }

        #region TIMER

        //
        // TIMER
        //
        private class TimerClass
        {
            public uint localID;
            public LLUUID itemID;
            //public double interval;
            public long interval;
            //public DateTime next;
            public long next;
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
            ts.interval = Convert.ToInt64(sec * 10000000); // How many 100 nanoseconds (ticks) should we wait
            //       2193386136332921 ticks
            //       219338613 seconds

            //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
            ts.next = DateTime.Now.Ticks + ts.interval;
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
                foreach (TimerClass ts in new ArrayList(Timers))
                {
                    if (ts.localID == m_localID && ts.itemID == m_itemID)
                        Timers.Remove(ts);
                }
            }

            // Old method: Create new list       
            //List<TimerClass> NewTimers = new List<TimerClass>();
            //foreach (TimerClass ts in Timers)
            //{
            //    if (ts.localID != m_localID && ts.itemID != m_itemID)
            //    {
            //        NewTimers.Add(ts);
            //    }
            //}
            //Timers.Clear();
            //Timers = NewTimers;
            //}
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
                    if (ts.next < DateTime.Now.Ticks)
                    {
                        
                        Console.WriteLine("Time has passed: Now: " + DateTime.Now.Ticks + ", Passed: " + ts.next);
                        // Add it to queue
                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "timer", EventQueueManager.llDetectNull,
                                                                            null);
                        // set next interval

                        //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
                        ts.next = DateTime.Now.Ticks + ts.interval;
                    }
                }
            } // lock
        }

        #endregion

        #region HTTP REQUEST

        public void CheckHttpRequests()
        {
            if (m_ScriptEngine.World == null)
                return;

            IHttpRequests iHttpReq =
                m_ScriptEngine.World.RequestModuleInterface<IHttpRequests>();

            HttpRequestClass httpInfo = null;

            if (iHttpReq != null)
                httpInfo = iHttpReq.GetNextCompletedRequest();

            while (httpInfo != null)
            {
                //Console.WriteLine("PICKED HTTP REQ:" + httpInfo.response_body + httpInfo.status);

                // Deliver data to prim's remote_data handler
                //
                // TODO: Returning null for metadata, since the lsl function
                // only returns the byte for HTTP_BODY_TRUNCATED, which is not
                // implemented here yet anyway.  Should be fixed if/when maxsize
                // is supported

                if (m_ScriptEngine.m_ScriptManager.GetScript(httpInfo.localID, httpInfo.itemID) != null)
                {
                    iHttpReq.RemoveCompletedRequest(httpInfo.reqID);
                    object[] resobj = new object[]
                    {
                        httpInfo.reqID.ToString(), httpInfo.status, null, httpInfo.response_body
                    };

                    m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                        httpInfo.localID, httpInfo.itemID, "http_response", EventQueueManager.llDetectNull, resobj
                    );

                }

                httpInfo = iHttpReq.GetNextCompletedRequest();

            }
        }

        #endregion

        #region Check llRemoteData channels

        public void CheckXMLRPCRequests()
        {
            if (m_ScriptEngine.World == null)
                return;

            IXMLRPC xmlrpc = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();

            if (xmlrpc != null)
            {
                RPCRequestInfo rInfo = xmlrpc.GetNextCompletedRequest();

                while (rInfo != null)
                {
                    if (m_ScriptEngine.m_ScriptManager.GetScript(rInfo.GetLocalID(), rInfo.GetItemID()) != null)
                    {
                        xmlrpc.RemoveCompletedRequest(rInfo.GetMessageID());

                        //Deliver data to prim's remote_data handler
                        object[] resobj = new object[]
                        {
                            2, rInfo.GetChannelKey().ToString(), rInfo.GetMessageID().ToString(), String.Empty,
                            rInfo.GetIntValue(),
                            rInfo.GetStrVal()
                        };
                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                            rInfo.GetLocalID(), rInfo.GetItemID(), "remote_data", EventQueueManager.llDetectNull, resobj
                        );

                    }

                    rInfo = xmlrpc.GetNextCompletedRequest();

                }

                SendRemoteDataRequest srdInfo = xmlrpc.GetNextCompletedSRDRequest();

                while (srdInfo != null)
                {
                    if (m_ScriptEngine.m_ScriptManager.GetScript(srdInfo.m_localID, srdInfo.m_itemID) != null)
                    {
                        xmlrpc.RemoveCompletedSRDRequest(srdInfo.GetReqID());

                        //Deliver data to prim's remote_data handler
                        object[] resobj = new object[]
                        {
                            3, srdInfo.channel.ToString(), srdInfo.GetReqID().ToString(), String.Empty,
                            srdInfo.idata,
                            srdInfo.sdata
                        };
                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                            srdInfo.m_localID, srdInfo.m_itemID, "remote_data", EventQueueManager.llDetectNull, resobj
                        );

                    }

                    srdInfo = xmlrpc.GetNextCompletedSRDRequest();

                }


            }
        }

        #endregion

        #region Check llListeners

        public void CheckListeners()
        {
            if (m_ScriptEngine.World == null)
                return;
            IWorldComm comms = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();

            if (comms != null)
            {
                while (comms.HasMessages())
                {
                    if (m_ScriptEngine.m_ScriptManager.GetScript(
                        comms.PeekNextMessageLocalID(), comms.PeekNextMessageItemID()) != null)
                    {
                        ListenerInfo lInfo = comms.GetNextMessage();

                        //Deliver data to prim's listen handler
                        object[] resobj = new object[]
                        {
                            lInfo.GetChannel(), lInfo.GetName(), lInfo.GetID().ToString(), lInfo.GetMessage()
                        };

                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                            lInfo.GetLocalID(), lInfo.GetItemID(), "listen", EventQueueManager.llDetectNull, resobj
                        );
                    }

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
