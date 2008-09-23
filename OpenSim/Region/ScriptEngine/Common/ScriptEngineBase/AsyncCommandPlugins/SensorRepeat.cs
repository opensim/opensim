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
//#define SPAM
using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase.AsyncCommandPlugins
{
    public class SensorRepeat
    {
        public AsyncCommandManager m_CmdManager;

        public SensorRepeat(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;
        }

        public Dictionary<uint, Dictionary<UUID, LSL_Types.list>> SenseEvents =
            new Dictionary<uint, Dictionary<UUID, LSL_Types.list>>();
        private Object SenseLock = new Object();

        //
        // SenseRepeater and Sensors
        //
        private class SenseRepeatClass
        {
            public uint localID;
            public UUID itemID;
            public double interval;
            public DateTime next;

            public string name;
            public UUID keyID;
            public int type;
            public double range;
            public double arc;
            public SceneObjectPart host;
        }

        private List<SenseRepeatClass> SenseRepeaters = new List<SenseRepeatClass>();
        private object SenseRepeatListLock = new object();

        public void SetSenseRepeatEvent(uint m_localID, UUID m_itemID,
            string name, UUID keyID, int type, double range, double arc, double sec, SceneObjectPart host)
        {
            #if SPAM
            Console.WriteLine("SetSensorEvent");
            #endif
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

        public void UnSetSenseRepeaterEvents(uint m_localID, UUID m_itemID)
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

        public void SenseOnce(uint m_localID, UUID m_itemID,
                              string name, UUID keyID, int type,
                              double range, double arc, SceneObjectPart host)
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

        public LSL_Types.list GetSensorList(uint m_localID, UUID m_itemID)
        {
            lock (SenseLock)
            {
                Dictionary<UUID, LSL_Types.list> Obj = null;
                if (!SenseEvents.TryGetValue(m_localID, out Obj))
                {
                    #if SPAM
                    m_CmdManager.m_ScriptEngine.Log.Info("[AsyncLSL]: GetSensorList missing localID: " + m_localID);
                    #endif
                    return null;
                }
                lock (Obj)
                {
                    // Get script
                    LSL_Types.list SenseList = null;
                    if (!Obj.TryGetValue(m_itemID, out SenseList))
                    {
                        #if SPAM
                        m_CmdManager.m_ScriptEngine.Log.Info("[AsyncLSL]: GetSensorList missing itemID: " + m_itemID);
                        #endif
                        return null;
                    }
                    return SenseList;
                }
            }
        }

        private void SensorSweep(SenseRepeatClass ts)
        {
            //m_ScriptEngine.Log.Info("[AsyncLSL]:Enter SensorSweep");
            SceneObjectPart SensePoint = ts.host;

            if (SensePoint == null)
            {

                #if SPAM
                //m_ScriptEngine.Log.Info("[AsyncLSL]: Enter SensorSweep (SensePoint == null) for "+ts.itemID.ToString());
                #endif
                return;
            }
            //m_ScriptEngine.Log.Info("[AsyncLSL]: Enter SensorSweep Scan");

            Vector3 sensorPos = SensePoint.AbsolutePosition;
            Vector3 regionPos = new Vector3(m_CmdManager.m_ScriptEngine.World.RegionInfo.RegionLocX * Constants.RegionSize, m_CmdManager.m_ScriptEngine.World.RegionInfo.RegionLocY * Constants.RegionSize, 0);
            Vector3 fromRegionPos = sensorPos + regionPos;

            Quaternion q = SensePoint.RotationOffset;
            LSL_Types.Quaternion r = new LSL_Types.Quaternion(q.X, q.Y, q.Z, q.W);
            LSL_Types.Vector3 forward_dir = (new LSL_Types.Vector3(1, 0, 0) * r);
            double mag_fwd = LSL_Types.Vector3.Mag(forward_dir);

            // Here we should do some smart culling ...
            // math seems quicker than strings so try that first
            LSL_Types.list SensedObjects = new LSL_Types.list();
            LSL_Types.Vector3 ZeroVector = new LSL_Types.Vector3(0, 0, 0);

            foreach (EntityBase ent in m_CmdManager.m_ScriptEngine.World.Entities.Values)
            {
                Vector3 toRegionPos = ent.AbsolutePosition + regionPos;
                double dis = Math.Abs((double)Util.GetDistanceTo(toRegionPos, fromRegionPos));
                if (dis <= ts.range)
                {
                    // In Range, is it the right Type ?
                    int objtype = 0;

                    if (m_CmdManager.m_ScriptEngine.World.GetScenePresence(ent.UUID) != null) objtype |= 0x01; // actor
                    if (ent.Velocity.Equals(ZeroVector))
                        objtype |= 0x04; // passive non-moving
                    else
                        objtype |= 0x02; // active moving
                    if (ent is IScript) objtype |= 0x08; // Scripted. It COULD have one hidden ...

                    if (((ts.type & objtype) != 0) || ((ts.type & objtype) == ts.type))
                    {
                        // docs claim AGENT|ACTIVE should find agent objects OR active objects
                        // so the bitwise AND with object type should be non-zero

                        // Right type too, what about the other params , key and name ?
                        bool keep = true;
                        if (ts.arc < Math.PI)
                        {
                            // not omni-directional. Can you see it ?
                            // vec forward_dir = llRot2Fwd(llGetRot())
                            // vec obj_dir = toRegionPos-fromRegionPos
                            // dot=dot(forward_dir,obj_dir)
                            // mag_fwd = mag(forward_dir)
                            // mag_obj = mag(obj_dir)
                            // ang = acos(dot /(mag_fwd*mag_obj))
                            double ang_obj = 0;
                            try
                            {
                                Vector3 diff = toRegionPos - fromRegionPos;
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

                        if (keep && (ts.keyID != UUID.Zero) && (ts.keyID != ent.UUID))
                        {
                            keep = false;
                        }

                        if (keep && (ts.name.Length > 0))
                        {
                            string avatarname=null;
                            string objectname=null;
                            string entname =ent.Name;

                            // try avatar username surname
                            CachedUserInfo profile = m_CmdManager.m_ScriptEngine.World.CommsManager.UserProfileCacheService.GetUserDetails(ent.UUID);
                            if (profile != null && profile.UserProfile != null)
                            {
                               avatarname = profile.UserProfile.FirstName + " " + profile.UserProfile.SurName;
                            }
                            // try an scene object
                            SceneObjectPart SOP = m_CmdManager.m_ScriptEngine.World.GetSceneObjectPart(ent.UUID);
                            if (SOP != null)
                            {
                                objectname = SOP.Name;
                            }

                            if ((ts.name != entname) && (ts.name != avatarname) && (ts.name != objectname))
                            {
                               keep = false;
                            }
                        }

                        if (keep == true) SensedObjects.Add(ent.UUID);
                    }
                }
            }
            #if SPAM
            //m_ScriptEngine.Log.Info("[AsyncLSL]: Enter SensorSweep SenseLock");
            #endif
            lock (SenseLock)
            {
                // Create object if it doesn't exist
                if (SenseEvents.ContainsKey(ts.localID) == false)
                {
                    SenseEvents.Add(ts.localID, new Dictionary<UUID, LSL_Types.list>());
                }
                // clear if previous traces exist
                Dictionary<UUID, LSL_Types.list> Obj;
                SenseEvents.TryGetValue(ts.localID, out Obj);
                if (Obj.ContainsKey(ts.itemID) == true)
                    Obj.Remove(ts.itemID);

                // note list may be zero length
                Obj.Add(ts.itemID, SensedObjects);

                if (SensedObjects.Length == 0)
                {
                    // send a "no_sensor"
                    // Add it to queue
                    m_CmdManager.m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "no_sensor", EventQueueManager.llDetectNull,
                                                                        new object[] { });
                }
                else
                {
                    m_CmdManager.m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(ts.localID, ts.itemID, "sensor", EventQueueManager.llDetectNull,
                                                                        new object[] { new LSL_Types.LSLInteger(SensedObjects.Length) });
                }
                m_CmdManager.m_ScriptEngine.World.EventManager.TriggerTimerEvent(ts.localID, ts.interval);
            }
        }
    }
}
