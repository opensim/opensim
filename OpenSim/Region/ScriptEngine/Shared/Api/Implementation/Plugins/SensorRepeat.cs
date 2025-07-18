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
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using OpenSim.Framework;
//using log4net;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Plugins
{
    public class SensorRepeat
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Used by one-off and repeated sensors
        /// </summary>
        public class SensorInfo
        {
            public uint localID;
            public UUID itemID;
            public double interval;
            public DateTime next;

            public string name;
            public UUID keyID;
            public int type;
            public float range;
            public float arc;
            public SceneObjectPart host;

            public SensorInfo Clone()
            {
                return (SensorInfo)MemberwiseClone();
            }
        }

        public AsyncCommandManager m_CmdManager;

        /// <summary>
        /// Number of sensors active.
        /// </summary>
        public int SensorsCount
        {
            get
            {
                return SenseRepeaters.Count;
            }
        }

        public SensorRepeat(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;
            maximumRange = CmdManager.m_ScriptEngine.Config.GetFloat("SensorMaxRange", 96.0f);
            maximumToReturn = CmdManager.m_ScriptEngine.Config.GetInt("SensorMaxResults", 16);
            m_npcModule = m_CmdManager.m_ScriptEngine.World.RequestModuleInterface<INPCModule>();
        }

        private readonly INPCModule m_npcModule;

        private readonly object SenseLock = new();

        private const int AGENT = 1;
        private const int AGENT_BY_USERNAME = 0x10;
        private const int NPC = 0x20;
        private const int ACTIVE = 2;
        private const int PASSIVE = 4;
        private const int SCRIPTED = 8;

        private readonly float maximumRange = 96.0f;
        private readonly int maximumToReturn = 16;

        //
        // Sensed entity
        //
        private class SensedEntity : IComparable
        {
            public SensedEntity(float detectedDistance, UUID detectedID)
            {
                distance = detectedDistance;
                itemID = detectedID;
            }
            public int CompareTo(object obj)
            {
                if (obj is not SensedEntity ent)
                    throw new InvalidOperationException();
                if (ent.distance < distance) return 1;
                if (ent.distance > distance) return -1;
                return 0;
            }
            public UUID itemID;
            public float distance;
        }

        /// <summary>
        /// Sensors to process.
        /// </summary>
        /// <remarks>
        /// Always lock SenseRepeatListLock when updating this list.
        /// </remarks>
        private List<SensorInfo> SenseRepeaters = new();
        private readonly object SenseRepeatListLock = new();

        public void SetSenseRepeatEvent(uint m_localID, UUID m_itemID,
                                        string name, UUID keyID, int type, double range,
                                        double arc, double sec, SceneObjectPart host)
        {
            // Always remove first, in case this is a re-set
            UnSetSenseRepeaterEvents(m_localID, m_itemID);

            if (sec == 0) // Disabling timer
                return;
            float frange = (float)range;
            // Add to timer
            SensorInfo ts = new()
            {
                localID = m_localID,
                itemID = m_itemID,
                interval = sec,
                next = DateTime.UtcNow.AddSeconds(sec),
                name = name,
                keyID = keyID,
                type = type,
                range = frange >= maximumRange ? maximumRange : frange,
                arc = (float)arc,
                host = host
            };
            AddSenseRepeater(ts);
        }

        private void AddSenseRepeater(SensorInfo senseRepeater)
        {
            lock (SenseRepeatListLock)
                SenseRepeaters.Add(senseRepeater);
        }

        public void UnSetSenseRepeaterEvents(uint m_localID, UUID m_itemID)
        {
            // Remove from timer
            lock (SenseRepeatListLock)
            {
                List<SensorInfo> newSenseRepeaters = new(SenseRepeaters.Count);
                foreach (SensorInfo ts in SenseRepeaters)
                {
                    if (ts.localID != m_localID || ts.itemID.NotEqual(m_itemID))
                    {
                        newSenseRepeaters.Add(ts);
                    }
                }
                SenseRepeaters = newSenseRepeaters;
            }
        }

        public void CheckSenseRepeaterEvents()
        {
            // Go through all timers

            List<SensorInfo> curSensors;
            lock(SenseRepeatListLock)
                curSensors = SenseRepeaters;

            DateTime now = DateTime.UtcNow;
            foreach (SensorInfo ts in curSensors)
            {
                if(ts.interval == 0)
                {
                    SensorSweep(ts);
                    lock (SenseRepeatListLock)
                        SenseRepeaters.Remove(ts);
                }
                // Time has passed?
                else if (ts.next < now)
                {
                    SensorSweep(ts);
                    // set next interval
                    ts.next = now.AddSeconds(ts.interval);
                }
            }
        }

        public void SenseOnce(uint m_localID, UUID m_itemID,
                              string name, UUID keyID, int type,
                              double range, double arc, SceneObjectPart host)
        {
            float frange = (float)range;
            // Add to timer
            SensorInfo ts = new()
            {
                localID = m_localID,
                itemID = m_itemID,
                interval = 0,
                name = name,
                keyID = keyID,
                type = type,
                range = frange >= maximumRange ? maximumRange : frange,
                arc = (float)arc,
                host = host
            };
            AddSenseRepeater(ts);
        }

        private void SensorSweep(SensorInfo ts)
        {
            if (ts.host is null)
                return;

            List<SensedEntity> sensedEntities = new();

            // Is the sensor type is AGENT and not SCRIPTED then include agents
            if ((ts.type & (AGENT | AGENT_BY_USERNAME | NPC)) != 0 && (ts.type & SCRIPTED) == 0)
            {
                sensedEntities.AddRange(doAgentSensor(ts));
            }

            // If SCRIPTED or PASSIVE or ACTIVE check objects
            if ((ts.type & SCRIPTED) != 0 || (ts.type & PASSIVE) != 0 || (ts.type & ACTIVE) != 0)
            {
                sensedEntities.AddRange(doObjectSensor(ts));
            }

            lock (SenseLock)
            {
                if (sensedEntities.Count == 0)
                {
                    // send a "no_sensor"
                    // Add it to queue
                    m_CmdManager.m_ScriptEngine.PostScriptEvent(ts.itemID,
                            new EventParams("no_sensor", Array.Empty<object>(),
                            Array.Empty<DetectParams>()));
                }
                else
                {
                    // Sort the list to get everything ordered by distance
                    sensedEntities.Sort();
                    int count = sensedEntities.Count;
                    int idx;
                    List<DetectParams> detected = new();
                    for (idx = 0; idx < count; idx++)
                    {
                        try
                        {
                            DetectParams detect = new()
                            {
                                Key = sensedEntities[idx].itemID
                            };
                            detect.Populate(m_CmdManager.m_ScriptEngine.World);
                            detected.Add(detect);
                        }
                        catch (Exception)
                        {
                            // Ignore errors, the object has been deleted or the avatar has gone and
                            // there was a problem in detect.Populate so nothing added to the list
                        }
                        if (detected.Count == maximumToReturn)
                            break;
                    }

                    if (detected.Count == 0)
                    {
                        // To get here with zero in the list there must have been some sort of problem
                        // like the object being deleted or the avatar leaving to have caused some
                        // difficulty during the Populate above so fire a no_sensor event
                        m_CmdManager.m_ScriptEngine.PostScriptEvent(ts.itemID,
                                new EventParams("no_sensor", Array.Empty<object>(),
                                Array.Empty<DetectParams>()));
                    }
                    else
                    {
                        m_CmdManager.m_ScriptEngine.PostScriptEvent(ts.itemID,
                                new EventParams("sensor",
                                new Object[] {new LSL_Types.LSLInteger(detected.Count) },
                                detected.ToArray()));
                    }
                }
            }
        }

        private List<SensedEntity> doObjectSensor(SensorInfo ts)
        {
            List<EntityBase> Entities;
            List<SensedEntity> sensedEntities = new();

            // If this is an object sense by key try to get it directly
            // rather than getting a list to scan through
            if (ts.keyID.IsNotZero())
            {
                m_CmdManager.m_ScriptEngine.World.Entities.TryGetValue(ts.keyID, out EntityBase e);
                if (e is null)
                    return sensedEntities;
                Entities = new List<EntityBase> { e };
            }
            else
            {
                Entities = new List<EntityBase>(m_CmdManager.m_ScriptEngine.World.GetEntities());
            }

            SceneObjectPart sensorPart = ts.host;
            bool doarc = ts.arc < MathF.PI;

            // pre define some things to avoid repeated definitions in the loop body
            float dis;
            int objtype;
            SceneObjectPart part;

            Vector3 fromRegionPos;
            Vector3 forward_dir;
            float mag_fwd; // to compensate in case rotation is not normalized
            if (sensorPart.ParentGroup.IsAttachment)
            {
                // In attachments, rotate the sensor cone with the
                // avatar rotation. This may include a nonzero elevation if
                // in mouselook.
                // This will not include the rotation and position of the
                // attachment point (e.g. your head when a sensor is in your
                // hair attached to your scull. Your hair  will turn with
                // your head but the sensor will stay with your (global)
                // avatar rotation and position.
                // Position of a sensor in a child prim attached to an avatar
                // will be still wrong.
                ScenePresence avatar = m_CmdManager.m_ScriptEngine.World.GetScenePresence(sensorPart.ParentGroup.AttachedAvatar);
                if (avatar is null)
                    return sensedEntities;

                fromRegionPos = avatar.AbsolutePosition;
                if (doarc)
                {
                    forward_dir = Vector3.UnitXRotated(avatar.Rotation);
                    mag_fwd = forward_dir.LengthSquared();
                }
                else
                {
                    forward_dir = Vector3.Zero;
                    mag_fwd = 1;
                }
            }
            else
            {
                fromRegionPos = sensorPart.GetWorldPosition();
                if (doarc)
                {
                    forward_dir = Vector3.UnitXRotated(sensorPart.GetWorldRotation());
                    mag_fwd = forward_dir.LengthSquared();
                }
                else
                {
                    forward_dir = Vector3.Zero;
                    mag_fwd = 1;
                }
            }

            float rangeSQ = ts.range * ts.range;

            bool nameSearch = !string.IsNullOrEmpty(ts.name);

            foreach (EntityBase ent in Entities)
            {
                if (ent.IsDeleted) // taken so long to do this it has gone from the scene
                    continue;

                if (ent is not SceneObjectGroup sog) // dont bother if it is a pesky avatar
                    continue;

                if (sog.IsAttachment) // Attached so ignore
                    continue;

                if (sensorPart.UUID.Equals(ent.UUID))
                    continue;

                if (nameSearch && !ent.Name.Equals(ts.name))
                    continue;

                Vector3 diff = ent.AbsolutePosition - fromRegionPos;
                dis = diff.LengthSquared();
                if(dis > rangeSQ)
                    continue;

                part = sog.RootPart;
                objtype = 0;
                if (part.Inventory.ContainsScripts())
                {
                    objtype |= ACTIVE | SCRIPTED; // Scripted and active. It COULD have one hidden ...
                }
                else
                {
                    if (ent.Velocity.IsZero())
                    {
                        objtype |= PASSIVE; // Passive non-moving
                    }
                    else
                    {
                        objtype |= ACTIVE; // moving so active
                    }
                }

                if ((ts.type & objtype) == 0)
                    continue;

                // Right type too, what about the other params , key and name ?
                if (doarc)
                {
                    float mag = mag_fwd * dis;
                    if(mag > 1e-6f)
                    {
                        float dot = Vector3.Dot(forward_dir, diff) / MathF.Sqrt(mag);
                        if(dot < -1.0f)
                            continue;
                        if(dot < 1.0f)
                        {
                            if (ts.arc < MathF.Acos(dot))
                                continue;
                        }
                        else if(dot > 1.00001f)
                            continue;
                    }
                }

                // add distance for sorting purposes later
                sensedEntities.Add(new SensedEntity(dis, ent.UUID));
            }
            return sensedEntities;
        }

        private List<SensedEntity> doAgentSensor(SensorInfo ts)
        {
            List<SensedEntity> sensedEntities = new();

            // If nobody about quit fast
            if (m_CmdManager.m_ScriptEngine.World.GetRootAgentCount() == 0)
                return sensedEntities;

            SceneObjectPart SensePoint = ts.host;
            Vector3 fromRegionPos = SensePoint.GetWorldPosition();

            Quaternion q;
            if (SensePoint.ParentGroup.IsAttachment)
            {
                // In attachments, rotate the sensor cone with the
                // avatar rotation. This may include a nonzero elevation if
                // in mouselook.
                // This will not include the rotation and position of the
                // attachment point (e.g. your head when a sensor is in your
                // hair attached to your scull. Your hair  will turn with
                // your head but the sensor will stay with your (global)
                // avatar rotation and position.
                // Position of a sensor in a child prim attached to an avatar
                // will be still wrong.
                ScenePresence avatar = m_CmdManager.m_ScriptEngine.World.GetScenePresence(SensePoint.ParentGroup.AttachedAvatar);

                // Don't proceed if the avatar for this attachment has since been removed from the scene.
                if (avatar is null)
                    return sensedEntities;
                fromRegionPos = avatar.AbsolutePosition;
                q = avatar.Rotation;
            }
            else
                q = SensePoint.GetWorldRotation();

            Vector3 forward_dir = Vector3.UnitXRotated(q);
            float mag_fwd = forward_dir.LengthSquared();

            bool attached = (SensePoint.ParentGroup.AttachmentPoint != 0);
            Vector3 toRegionPos;

            Action<ScenePresence> senseEntity = new(presence =>
            {
                //m_log.DebugFormat(
                //    "[SENSOR REPEAT]: Inspecting scene presence {0}, type {1} on sensor sweep for {2}, type {3}",
                //    presence.Name, presence.PresenceType, ts.name, ts.type);

                if ((ts.type & NPC) == 0 && presence.PresenceType == PresenceType.Npc)
                {
                    INPC npcData = m_npcModule.GetNPC(presence.UUID, presence.Scene);
                    if (npcData is null || !npcData.SenseAsAgent)
                    {
                        //m_log.DebugFormat(
                        //    "[SENSOR REPEAT]: Discarding NPC {0} from agent sense sweep for script item id {1}",
                        //    presence.Name, ts.itemID);
                        return;
                    }
                }

                if ((ts.type & AGENT) == 0)
                {
                    if (presence.PresenceType == PresenceType.User)
                    {
                        return;
                    }
                    else
                    {
                        INPC npcData = m_npcModule.GetNPC(presence.UUID, presence.Scene);
                        if (npcData is not null && npcData.SenseAsAgent)
                        {
                            //m_log.DebugFormat(
                            //    "[SENSOR REPEAT]: Discarding NPC {0} from non-agent sense sweep for script item id {1}",
                            //    presence.Name, ts.itemID);
                            return;
                        }
                    }
                }

                if (presence.IsDeleted || presence.IsChildAgent || presence.IsViewerUIGod)
                    return;

                // if the object the script is in is attached and the avatar is the owner
                // then this one is not wanted
                if (attached && presence.UUID.Equals(SensePoint.OwnerID))
                    return;

                toRegionPos = presence.AbsolutePosition;
                float dis = Vector3.Distance(toRegionPos, fromRegionPos);
                if (presence.IsSatOnObject && presence.ParentPart is not null &&
                    presence.ParentPart.ParentGroup is not null &&
                    presence.ParentPart.ParentGroup.RootPart is not null)
                {
                    Vector3 rpos = presence.ParentPart.ParentGroup.RootPart.AbsolutePosition;
                    float dis2 = Vector3.Distance(rpos, fromRegionPos);
                    if (dis > dis2)
                        dis = dis2;
                }

                // Disabled for now since all osNpc* methods check for appropriate ownership permission.
                // Perhaps could be re-enabled as an NPC setting at some point since being able to make NPCs not
                // sensed might be useful.
                //if (presence.PresenceType == PresenceType.Npc && npcModule != null)
                //{
                //    UUID npcOwner = npcModule.GetOwner(presence.UUID);
                //    if (npcOwner != UUID.Zero && npcOwner != SensePoint.OwnerID)
                //        return;
                //}

                // are they in range
                if (dis <= ts.range)
                {
                    // Are they in the required angle of view
                    if (ts.arc < MathF.PI)
                    {
                        // not omni-directional. Can you see it ?
                        // vec forward_dir = llRot2Fwd(llGetRot())
                        // vec obj_dir = toRegionPos-fromRegionPos
                        // dot=dot(forward_dir,obj_dir)
                        // mag_fwd = mag(forward_dir)
                        // mag_obj = mag(obj_dir)
                        // ang = acos(dot /(mag_fwd*mag_obj))
                        float ang_obj = 0;
                        try
                        {
                            Vector3 obj_dir = toRegionPos - fromRegionPos;
                            float dot = Vector3.Dot(forward_dir, obj_dir);
                            float mag_corr = MathF.Sqrt( mag_fwd * obj_dir.LengthSquared());
                            ang_obj = MathF.Acos(dot / mag_corr);
                        }
                        catch
                        {
                        }
                        if (ang_obj <= ts.arc)
                        {
                            sensedEntities.Add(new SensedEntity(dis, presence.UUID));
                        }
                    }
                    else
                    {
                        sensedEntities.Add(new SensedEntity(dis, presence.UUID));
                    }
                }
            });

            // If this is an avatar sense by key try to get them directly
            // rather than getting a list to scan through
            if (ts.keyID.IsNotZero())
            {
                // Try direct lookup by UUID
                if (!m_CmdManager.m_ScriptEngine.World.TryGetScenePresence(ts.keyID, out ScenePresence sp))
                    return sensedEntities;
                senseEntity(sp);
            }
            else if (!string.IsNullOrEmpty(ts.name))
            {
                // Try lookup by name will return if/when found
                if (((ts.type & AGENT) != 0) && m_CmdManager.m_ScriptEngine.World.TryGetAvatarByName(ts.name, out ScenePresence sp))
                    senseEntity(sp);
                if ((ts.type & AGENT_BY_USERNAME) != 0)
                {
                    m_CmdManager.m_ScriptEngine.World.ForEachRootScenePresence(
                        delegate (ScenePresence ssp)
                        {
                            if (ssp.Lastname == "Resident")
                            {
                                if (ssp.Firstname.ToLower() == ts.name)
                                    senseEntity(ssp);
                                return;
                            }
                            if (ssp.Name.Replace(" ", ".").ToLower() == ts.name)
                                senseEntity(ssp);
                        }
                    );
                }

                return sensedEntities;
            }
            else
            {
                m_CmdManager.m_ScriptEngine.World.ForEachRootScenePresence(senseEntity);
            }
            return sensedEntities;
        }

        public Object[] GetSerializationData(UUID itemID)
        {
            List<Object> data = new();

            foreach (SensorInfo ts in SenseRepeaters)
            {
                if (ts.itemID.Equals(itemID))
                {
                    data.Add(ts.interval);
                    data.Add(ts.name);
                    data.Add(ts.keyID);
                    data.Add(ts.type);
                    data.Add(ts.range);
                    data.Add(ts.arc);
                }
            }

            return data.ToArray();
        }

        public void CreateFromData(uint localID, UUID itemID, UUID objectID, object[] data)
        {
            SceneObjectPart part = m_CmdManager.m_ScriptEngine.World.GetSceneObjectPart(objectID);

            if (part is null)
                return;

            int idx = 0;

            while (idx < data.Length)
            {
                SensorInfo ts = new()
                {
                    localID = localID,
                    itemID = itemID,

                    interval = (double)data[idx],
                    name = (string)data[idx + 1],
                    keyID = (UUID)data[idx + 2],
                    type = (int)data[idx + 3],
                    range = (float)data[idx + 4],
                    arc = (float)data[idx + 5],
                    host = part
                };

                ts.next = DateTime.UtcNow.AddSeconds(ts.interval);

                AddSenseRepeater(ts);

                idx += 6;
            }
        }

        public List<SensorInfo> GetSensorInfo()
        {
            List<SensorInfo> retList = new();

            lock (SenseRepeatListLock)
            {
                foreach (SensorInfo i in SenseRepeaters)
                    retList.Add(i.Clone());
            }

            return retList;
        }
    }
}
