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
* 
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using Axiom.Math;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Handles LSL commands that takes long time and returns an event, for example timers, HTTP requests, etc.
    /// </summary>
    public class AsyncLSLCommandManager : iScriptEngineFunctionModule
    {
        private static Thread cmdHandlerThread;
        private static int cmdHandlerThreadCycleSleepms;

        private ScriptEngine m_ScriptEngine;

        public Dictionary<uint, Dictionary<LLUUID, LSL_Types.list>> SenseEvents =
            new Dictionary<uint, Dictionary<LLUUID, LSL_Types.list>>();
        private Object SenseLock = new Object();

        public AsyncLSLCommandManager(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;
            ReadConfig();

            StartThread();
        }

        private void StartThread()
        {
            if (cmdHandlerThread == null)
            {
                // Start the thread that will be doing the work
                cmdHandlerThread = new Thread(CmdHandlerThreadLoop);
                cmdHandlerThread.Name = "AsyncLSLCmdHandlerThread";
                cmdHandlerThread.Priority = ThreadPriority.BelowNormal;
                cmdHandlerThread.IsBackground = true;
                cmdHandlerThread.Start();
                OpenSim.Framework.ThreadTracker.Add(cmdHandlerThread);
            }
        }

        public void ReadConfig()
        {
            cmdHandlerThreadCycleSleepms = m_ScriptEngine.ScriptConfigSource.GetInt("AsyncLLCommandLoopms", 100);
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
                        //cmdHandlerThread.Join();
                    }
                }
            }
            catch
            {
            }
        }

        private static void CmdHandlerThreadLoop()
        {
            while (true)
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(cmdHandlerThreadCycleSleepms);
                        //lock (ScriptEngine.ScriptEngines)
                        //{
                            foreach (ScriptEngine se in new ArrayList(ScriptEngine.ScriptEngines)) 
                            {
                                se.m_ASYNCLSLCommandManager.DoOneCmdHandlerPass();
                            }
                        //}
                        // Sleep before next cycle
                        //Thread.Sleep(cmdHandlerThreadCycleSleepms);
                    }
                }
                catch
                {
                }
            }
        }

        internal void DoOneCmdHandlerPass()
        {
            // Check timers
            CheckTimerEvents();
            // Check HttpRequests
            CheckHttpRequests();
            // Check XMLRPCRequests
            CheckXMLRPCRequests();
            // Check Listeners
            CheckListeners();
            // Check Sensors
            CheckSenseRepeaterEvents();
        }

        /// <summary>
        /// Remove a specific script (and all its pending commands)
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
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

            // Remove Sensors
            UnSetSenseRepeaterEvents(localID, itemID);

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
//                        Console.WriteLine("Time has passed: Now: " + DateTime.Now.Ticks + ", Passed: " + ts.next);
                        // Add it to queue
                        m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "timer", EventQueueManager.llDetectNull,
                                                                            null);
                        // set next interval

                        //ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
                        ts.next = DateTime.Now.Ticks + ts.interval;
                    }
                }
            }
        }

        #endregion
        #region SENSOR

        //
        // SenseRepeater and Sensors
        //
        private class SenseRepeatClass
        {
            public uint localID;
            public LLUUID itemID;
            public double interval;
            public DateTime next;

            public string name;
            public LLUUID keyID;
            public int type;
            public double range;
            public double arc;
            public SceneObjectPart host;
        }

        private List<SenseRepeatClass> SenseRepeaters = new List<SenseRepeatClass>();
        private object SenseRepeatListLock = new object();

        public void SetSenseRepeatEvent(uint m_localID, LLUUID m_itemID,
            string name, LLUUID keyID, int type, double range, double arc, double sec,SceneObjectPart host)
        {
            Console.WriteLine("SetSensorEvent");

            // Always remove first, in case this is a re-set
            UnSetSenseRepeaterEvents(m_localID, m_itemID);
            if (sec == 0) // Disabling timer
                return;

            // Add to timer
            SenseRepeatClass ts = new SenseRepeatClass();
            ts.localID = m_localID;
            ts.itemID = m_itemID;
            ts.interval = sec;
            ts.name = name;
            ts.keyID = keyID;
            ts.type = type;
            ts.range = range;
            ts.arc = arc;
            ts.host = host;

            ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
            lock (SenseRepeatListLock)
            {
                SenseRepeaters.Add(ts);
            }
        }

        public void UnSetSenseRepeaterEvents(uint m_localID, LLUUID m_itemID)
        {
            // Remove from timer
            lock (SenseRepeatListLock)
            {
                List<SenseRepeatClass> NewSensors = new List<SenseRepeatClass>();
                foreach (SenseRepeatClass ts in SenseRepeaters)
                {
                    if (ts.localID != m_localID && ts.itemID != m_itemID)
                    {
                        NewSensors.Add(ts);
                    }
                }
                SenseRepeaters.Clear();
                SenseRepeaters = NewSensors;
            }
        }

        public void CheckSenseRepeaterEvents()
        {
            // Nothing to do here?
            if (SenseRepeaters.Count == 0)
                return;

            lock (SenseRepeatListLock)
            {
                // Go through all timers
                foreach (SenseRepeatClass ts in SenseRepeaters)
                {
                    // Time has passed?
                    if (ts.next.ToUniversalTime() < DateTime.Now.ToUniversalTime())
                    {
                        SensorSweep(ts);
                        // set next interval
                        ts.next = DateTime.Now.ToUniversalTime().AddSeconds(ts.interval);
                    }
                }
            } // lock
        }

        public void SenseOnce(uint m_localID, LLUUID m_itemID,
            string name, LLUUID keyID, int type, double range, double arc, SceneObjectPart host)
        {
            // Add to timer
            SenseRepeatClass ts = new SenseRepeatClass();
            ts.localID = m_localID;
            ts.itemID = m_itemID;
            ts.interval = 0;
            ts.name = name;
            ts.keyID = keyID;
            ts.type = type;
            ts.range = range;
            ts.arc = arc;
            ts.host = host;
            SensorSweep(ts);
        }

        public LSL_Types.list GetSensorList(uint m_localID, LLUUID m_itemID)
        {
            lock (SenseLock)
            {
                Dictionary<LLUUID, LSL_Types.list> Obj = null;
                if (!SenseEvents.TryGetValue(m_localID, out Obj))
                {
                    m_ScriptEngine.Log.Info("[AsyncLSL]: GetSensorList missing localID: " + m_localID);
                    return null;
                }
                lock (Obj)
                {
                    // Get script
                    LSL_Types.list SenseList = null;
                    if (!Obj.TryGetValue(m_itemID, out SenseList))
                    {
                        m_ScriptEngine.Log.Info("[AsyncLSL]: GetSensorList missing itemID: " + m_itemID);
                        return null;
                    }
                    return SenseList;
                }
            }

        }

        private void SensorSweep(SenseRepeatClass ts)
        {
            //m_ScriptEngine.Log.Info("[AsyncLSL]:Enter SensorSweep");
             SceneObjectPart SensePoint =ts.host;
            
           if (SensePoint == null)
           {
               //m_ScriptEngine.Log.Info("[AsyncLSL]: Enter SensorSweep (SensePoint == null) for "+ts.itemID.ToString());
               return;
           }
           //m_ScriptEngine.Log.Info("[AsyncLSL]: Enter SensorSweep Scan");
            
           LLVector3 sensorPos = SensePoint.AbsolutePosition;
           LLVector3 regionPos = new LLVector3(m_ScriptEngine.World.RegionInfo.RegionLocX * Constants.RegionSize, m_ScriptEngine.World.RegionInfo.RegionLocY * Constants.RegionSize, 0);
           LLVector3 fromRegionPos = sensorPos + regionPos;

           LLQuaternion q = SensePoint.RotationOffset;
           LSL_Types.Quaternion r = new LSL_Types.Quaternion(q.X, q.Y, q.Z, q.W);
           LSL_Types.Vector3 forward_dir = (new LSL_Types.Vector3(1, 0, 0) * r);
           double mag_fwd = LSL_Types.Vector3.Mag(forward_dir);

            // Here we should do some smart culling ...
            // math seems quicker than strings so try that first
            LSL_Types.list SensedObjects = new LSL_Types.list();
            LSL_Types.Vector3 ZeroVector = new LSL_Types.Vector3(0, 0, 0);

            foreach (EntityBase ent in m_ScriptEngine.World.Entities.Values)
            {
                
                LLVector3 toRegionPos = ent.AbsolutePosition + regionPos;
                double dis = Math.Abs((double) Util.GetDistanceTo(toRegionPos, fromRegionPos));
                if (dis <= ts.range)
                {
                    // In Range, is it the right Type ?
                    int objtype = 0;

                    if (m_ScriptEngine.World.GetScenePresence(ent.UUID) != null) objtype |= 0x01; // actor
                    if (ent.Velocity.Equals(ZeroVector))
                        objtype |= 0x04; // passive non-moving
                    else
                        objtype |= 0x02; // active moving
                    if (ent is IScript) objtype |= 0x08; // Scripted. It COULD have one hidden ... 

                    if ( ((ts.type & objtype) != 0 ) ||((ts.type & objtype) == ts.type ))
                    {
                        // docs claim AGENT|ACTIVE should find agent objects OR active objects
                        // so the bitwise AND with object type should be non-zero

                        // Right type too, what about the other params , key and name ?
                        bool keep = true;
                        if (ts.arc != Math.PI)
                        {
                            // not omni-directional. Can you see it ?
                            // vec forward_dir = llRot2Fwd(llGetRot())
                            // vec obj_dir = toRegionPos-fromRegionPos
                            // dot=dot(forward_dir,obj_dir)
                            // mag_fwd = mag(forward_dir)
                            // mag_obj = mag(obj_dir)
                            // ang = acos( dot /(mag_fwd*mag_obj))
                            double ang_obj = 0;
                            try
                            {
                                LLVector3 diff =toRegionPos - fromRegionPos;
                                LSL_Types.Vector3 obj_dir = new LSL_Types.Vector3(diff.X, diff.Y, diff.Z);
                                double dot = LSL_Types.Vector3.Dot(forward_dir, obj_dir);
                                double mag_obj = LSL_Types.Vector3.Mag(obj_dir);
                                ang_obj = Math.Acos(dot / (mag_fwd * mag_obj));
                            }
                            catch 
                            { 
                            }

                            if (ang_obj > ts.arc) keep = false;
                        }

                        if (keep && (ts.name.Length > 0) && (ts.name != ent.Name))
                        {
                            keep = false;
                        }

                        if (keep && (ts.keyID != null) && (ts.keyID != LLUUID.Zero) && (ts.keyID != ent.UUID))
                        {
                            keep = false;
                        }
                        if (keep==true) SensedObjects.Add(ent.UUID);
                    }
                }
            }
            //m_ScriptEngine.Log.Info("[AsyncLSL]: Enter SensorSweep SenseLock");

            lock (SenseLock)
            {
                // Create object if it doesn't exist
                if (SenseEvents.ContainsKey(ts.localID) == false)
                {
                    SenseEvents.Add(ts.localID, new Dictionary<LLUUID, LSL_Types.list>());
                }
                // clear if previous traces exist
                Dictionary<LLUUID, LSL_Types.list> Obj;
                SenseEvents.TryGetValue(ts.localID, out Obj);
                if (Obj.ContainsKey(ts.itemID) == true)
                    Obj.Remove(ts.itemID);

                // note list may be zero length
                Obj.Add(ts.itemID, SensedObjects); 

            if (SensedObjects.Length == 0)
            {
                // send a "no_sensor"
                // Add it to queue
                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "no_sensor", EventQueueManager.llDetectNull,
                                                                    new object[] {});
            }
            else
            {
                        
                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "sensor", EventQueueManager.llDetectNull,
                                                                    new object[] { SensedObjects.Length });
            }
          }
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
                //m_ScriptEngine.Log.Info("[AsyncLSL]:" + httpInfo.response_body + httpInfo.status);

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
                //Thread.Sleep(2500);
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
                        //lInfo.GetChannel(), lInfo.GetName(), lInfo.GetID().ToString(), lInfo.GetMessage()
                            lInfo.GetChannel(), lInfo.GetName(), lInfo.GetSourceItemID().ToString(), lInfo.GetMessage()
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
