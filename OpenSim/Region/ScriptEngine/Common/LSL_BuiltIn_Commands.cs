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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using Axiom.Math;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.LandManagement;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;

//using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL;

namespace OpenSim.Region.ScriptEngine.Common
{
    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_BuiltIn_Commands : MarshalByRefObject, LSL_BuiltIn_Commands_Interface
    {
        // private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal ScriptEngineBase.ScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal LLUUID m_itemID;
        internal bool throwErrorOnNotImplemented = true;
        
        public LSL_BuiltIn_Commands(ScriptEngineBase.ScriptEngine ScriptEngine, SceneObjectPart host, uint localID, LLUUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            //m_log.Info(ScriptEngineName, "LSL_BaseClass.Start() called. Hosted by [" + m_host.Name + ":" + m_host.UUID + "@" + m_host.AbsolutePosition + "]");
        }

        private DateTime m_timer = DateTime.Now;
        private string m_state = "default";

        public string State
        {
            get { return m_state; }
            set {
                // Set it if it changed
                if (m_state != value)
                {
                    m_state = value;
                    try
                    {
                        m_ScriptEngine.m_EventManager.state_entry(m_localID);

                    }
                    catch (AppDomainUnloadedException)
                    {
                        Console.WriteLine("[SCRIPT]: state change called when script was unloaded.  Nothing to worry about, but noting the occurance");
                    }
                }
            }
        }

        // Object never expires
        public override Object InitializeLifetimeService()
        {
            //Console.WriteLine("LSL_BuiltIn_Commands: InitializeLifetimeService()");
            //            return null;
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero; // TimeSpan.FromMinutes(1);
                //                lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                //                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        // Extension commands use this:
        public ICommander GetCommander(string name)
        {
            return World.GetCommander(name);
        }

        //These are the implementations of the various ll-functions used by the LSL scripts.
        //starting out, we use the System.Math library for trig functions. - ckrinke 8-14-07
        public double llSin(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Sin(f);
        }

        public double llCos(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Cos(f);
        }

        public double llTan(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Tan(f);
        }

        public double llAtan2(double x, double y)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Atan2(y, x);
        }

        public double llSqrt(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Sqrt(f);
        }

        public double llPow(double fbase, double fexponent)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Pow(fbase, fexponent);
        }

        public int llAbs(int i)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Abs(i);
        }

        public double llFabs(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Abs(f);
        }

        public double llFrand(double mag)
        {
            m_host.AddScriptLPS(1);
            lock (Util.RandomClass)
            {
                return Util.RandomClass.NextDouble() * mag;
            }
        }

        public int llFloor(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Floor(f);
        }

        public int llCeil(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Ceiling(f);
        }

        public int llRound(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Round(f, 0);
        }

        //This next group are vector operations involving squaring and square root. ckrinke
        public double llVecMag(LSL_Types.Vector3 v)
        {
            m_host.AddScriptLPS(1);
            return LSL_Types.Vector3.Mag(v);
        }

        public LSL_Types.Vector3 llVecNorm(LSL_Types.Vector3 v)
        {
            m_host.AddScriptLPS(1);
            double mag = LSL_Types.Vector3.Mag(v);
            LSL_Types.Vector3 nor = new LSL_Types.Vector3();
            nor.x = v.x / mag;
            nor.y = v.y / mag;
            nor.z = v.z / mag;
            return nor;
        }

        public double llVecDist(LSL_Types.Vector3 a, LSL_Types.Vector3 b)
        {
            m_host.AddScriptLPS(1);
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        //Now we start getting into quaternions which means sin/cos, matrices and vectors. ckrinke
        public LSL_Types.Vector3 llRot2Euler(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);
            //This implementation is from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions. ckrinke
            LSL_Types.Quaternion t = new LSL_Types.Quaternion(r.x * r.x, r.y * r.y, r.z * r.z, r.s * r.s);
            double m = (t.x + t.y + t.z + t.s);
            if (m == 0) return new LSL_Types.Vector3();
            double n = 2 * (r.y * r.s + r.x * r.z);
            double p = m * m - n * n;
            if (p > 0)
                return new LSL_Types.Vector3(Math.Atan2(2.0 * (r.x * r.s - r.y * r.z), (-t.x - t.y + t.z + t.s)),
                                             Math.Atan2(n, Math.Sqrt(p)),
                                             Math.Atan2(2.0 * (r.z * r.s - r.x * r.y), (t.x - t.y - t.z + t.s)));
            else if (n > 0)
                return new LSL_Types.Vector3(0.0, Math.PI / 2, Math.Atan2((r.z * r.s + r.x * r.y), 0.5 - t.x - t.z));
            else
                return new LSL_Types.Vector3(0.0, -Math.PI / 2, Math.Atan2((r.z * r.s + r.x * r.y), 0.5 - t.x - t.z));
        }

        public LSL_Types.Quaternion llEuler2Rot(LSL_Types.Vector3 v)
        {
            m_host.AddScriptLPS(1);
            //this comes from from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions but is incomplete as of 8/19/07
            float err = 0.00001f;
            double ax = Math.Sin(v.x / 2);
            double aw = Math.Cos(v.x / 2);
            double by = Math.Sin(v.y / 2);
            double bw = Math.Cos(v.y / 2);
            double cz = Math.Sin(v.z / 2);
            double cw = Math.Cos(v.z / 2);
            LSL_Types.Quaternion a1 = new LSL_Types.Quaternion(0.0, 0.0, cz, cw);
            LSL_Types.Quaternion a2 = new LSL_Types.Quaternion(0.0, by, 0.0, bw);
            LSL_Types.Quaternion a3 = new LSL_Types.Quaternion(ax, 0.0, 0.0, aw);
            LSL_Types.Quaternion a = (a1 * a2) * a3;
            //This multiplication doesnt compile, yet.            a = a1 * a2 * a3;
            LSL_Types.Quaternion b = new LSL_Types.Quaternion(ax * bw * cw + aw * by * cz,
                                                              aw * by * cw - ax * bw * cz, aw * bw * cz + ax * by * cw,
                                                              aw * bw * cw - ax * by * cz);
            LSL_Types.Quaternion c = new LSL_Types.Quaternion();
            //This addition doesnt compile yet c = a + b;
            LSL_Types.Quaternion d = new LSL_Types.Quaternion();
            //This addition doesnt compile yet d = a - b;
            if ((Math.Abs(c.x) > err && Math.Abs(d.x) > err) ||
                (Math.Abs(c.y) > err && Math.Abs(d.y) > err) ||
                (Math.Abs(c.z) > err && Math.Abs(d.z) > err) ||
                (Math.Abs(c.s) > err && Math.Abs(d.s) > err))
            {
                return b;
                //return a new Quaternion that is null until I figure this out
                //                return b;
                //            return a;
            }
            return a;
        }

        public LSL_Types.Quaternion llAxes2Rot(LSL_Types.Vector3 fwd, LSL_Types.Vector3 left, LSL_Types.Vector3 up)
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Quaternion();
        }

        public LSL_Types.Vector3 llRot2Fwd(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);
            return (new LSL_Types.Vector3(1,0,0) * r);
        }

        public LSL_Types.Vector3 llRot2Left(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);
            return (new LSL_Types.Vector3(0, 1, 0) * r);
        }

        public LSL_Types.Vector3 llRot2Up(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);
            return (new LSL_Types.Vector3(0, 0, 1) * r);
        }
        public LSL_Types.Quaternion llRotBetween(LSL_Types.Vector3 a, LSL_Types.Vector3 b)
        {
            //A and B should both be normalized
            m_host.AddScriptLPS(1);
            double dotProduct = LSL_Types.Vector3.Dot(a, b);
            LSL_Types.Vector3 crossProduct = LSL_Types.Vector3.Cross(a, b);
            double magProduct = LSL_Types.Vector3.Mag(a) * LSL_Types.Vector3.Mag(b);
            double angle = Math.Acos(dotProduct / magProduct);
            LSL_Types.Vector3 axis = LSL_Types.Vector3.Norm(crossProduct);
            double s = Math.Sin(angle / 2);

            return new LSL_Types.Quaternion(axis.x * s, axis.y * s, axis.z * s, (float)Math.Cos(angle / 2));
        }
        public void llWhisper(int channelID, string text)
        {
            m_host.AddScriptLPS(1);
            World.SimChat(Helpers.StringToField(text),
                          ChatTypeEnum.Whisper, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(m_host.UUID.ToString(), ChatTypeEnum.Whisper, channelID, m_host.Name, text);
        }

        public void llSay(int channelID, string text)
        {
            m_host.AddScriptLPS(1);
            World.SimChat(Helpers.StringToField(text),
                          ChatTypeEnum.Say, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(m_host.UUID.ToString(), ChatTypeEnum.Say, channelID, m_host.Name, text);
        }

        public void llShout(int channelID, string text)
        {
            m_host.AddScriptLPS(1);
            World.SimChat(Helpers.StringToField(text),
                          ChatTypeEnum.Shout, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(m_host.UUID.ToString(), ChatTypeEnum.Shout, channelID, m_host.Name, text);
        }

        public int llListen(int channelID, string name, string ID, string msg)
        {
            m_host.AddScriptLPS(1);
            if (ID == String.Empty)
            {
                ID = LLUUID.Zero.ToString();
            }
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            return wComm.Listen(m_localID, m_itemID, m_host.UUID, channelID, name, ID, msg);
        }

        public void llListenControl(int number, int active)
        {
            m_host.AddScriptLPS(1);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.ListenControl(number, active);
        }

        public void llListenRemove(int number)
        {
            m_host.AddScriptLPS(1);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.ListenRemove(number);
        }

        public void llSensor(string name, string id, int type, double range, double arc)
        {
            m_host.AddScriptLPS(1);
            LLUUID keyID = LLUUID.Zero;
            try
            {
                if (id.Length > 0) keyID = new LLUUID(id);
            }
            catch 
            { 
            }
            m_ScriptEngine.m_ASYNCLSLCommandManager.m_SensorRepeat.SenseOnce(m_localID, m_itemID, name, keyID, type, range, arc, m_host);

            return;
       }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            m_host.AddScriptLPS(1);
            LLUUID keyID = LLUUID.Zero;
            try
            {
                if (id.Length > 0) keyID = new LLUUID(id);
            }
            catch
            {
            }

            m_ScriptEngine.m_ASYNCLSLCommandManager.m_SensorRepeat.SetSenseRepeatEvent(m_localID, m_itemID, name, keyID, type, range, arc, rate, m_host);
            return;
       }

        public void llSensorRemove()
        {
            m_host.AddScriptLPS(1);
            m_ScriptEngine.m_ASYNCLSLCommandManager.m_SensorRepeat.UnSetSenseRepeaterEvents(m_localID, m_itemID);
            return;
        }

        public string resolveName(LLUUID objecUUID)
        {
            // try avatar username surname
            UserProfileData profile = World.CommsManager.UserService.GetUserProfile(objecUUID);
            if (profile != null)
            {
                string avatarname = profile.FirstName + " " + profile.SurName;
                return avatarname;
            }
            // try an scene object
            SceneObjectPart SOP = World.GetSceneObjectPart(objecUUID);
            if (SOP != null)
            {
                string objectname = SOP.Name;
                return objectname;
            }

            EntityBase SensedObject = null;
            lock (World.Entities)
            {
                World.Entities.TryGetValue(objecUUID, out SensedObject);
            }

            if (SensedObject == null)
                return String.Empty;
            return SensedObject.Name;

        }

        public string llDetectedName(int number)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list SenseList = m_ScriptEngine.m_ASYNCLSLCommandManager.m_SensorRepeat.GetSensorList(m_localID, m_itemID);
            if (SenseList != null)
            {
                if ((number >= 0) && (number <= SenseList.Length))
                {
                    LLUUID SensedUUID = (LLUUID)SenseList.Data[number];
                    return resolveName(SensedUUID);
                }
            }
            return String.Empty;
       }

        public LLUUID uuidDetectedKey(int number)
        {
            LSL_Types.list SenseList = m_ScriptEngine.m_ASYNCLSLCommandManager.m_SensorRepeat.GetSensorList(m_localID, m_itemID);
            if (SenseList != null)
            {
                if ((number >= 0) && (number < SenseList.Length))
                {
                    LLUUID SensedUUID = (LLUUID)SenseList.Data[number];
                    return SensedUUID;
                }
            }
            else
            {
                ScriptManager sm;
                IScript script = null;

                if ((sm = m_ScriptEngine.m_ScriptManager) != null)
                {
                    if (sm.Scripts.ContainsKey(m_localID))
                    {
                        if ((script = sm.GetScript(m_localID, m_itemID)) != null)
                        {
                            if (script.llDetectParams._key[0] != null)
                            {
                                return new LLUUID(script.llDetectParams._key[0]);
                            }
                        }
                    }
                }
            }
            return LLUUID.Zero;
        }

        public EntityBase entityDetectedKey(int number)
        {
            LSL_Types.list SenseList = m_ScriptEngine.m_ASYNCLSLCommandManager.m_SensorRepeat.GetSensorList(m_localID, m_itemID);
            if (SenseList != null)
            {
                if ((number >= 0) && (number < SenseList.Length))
                {
                    LLUUID SensedUUID = (LLUUID)SenseList.Data[number];
                    EntityBase SensedObject = null;
                    lock (World.Entities)
                    {
                        World.Entities.TryGetValue(SensedUUID, out SensedObject);
                    }
                    return SensedObject;
                }
            }
            return null;
        }

        public string llDetectedKey(int number)
        {
            m_host.AddScriptLPS(1);
            LLUUID SensedUUID = uuidDetectedKey(number);
            if (SensedUUID == LLUUID.Zero)
                return String.Empty;

            return SensedUUID.ToString();
        }

        public string llDetectedOwner(int number)
        {
            // returns UUID of owner of object detected
            m_host.AddScriptLPS(1);
            EntityBase SensedObject = entityDetectedKey(number);
            if (SensedObject ==null)
                return String.Empty;
            LLUUID SensedUUID = uuidDetectedKey(number);
            if (World.GetScenePresence(SensedUUID) == null)
            {
                // sensed object is not an avatar
                // so get the owner of the sensed object
                SceneObjectPart SOP = World.GetSceneObjectPart(SensedUUID);
                if (SOP != null) { return SOP.ObjectOwner.ToString(); }
            }
            else
            {
                // sensed object is an avatar, and so must be its own owner
                return SensedUUID.ToString();
            }


            return String.Empty;
               
       }

        public int llDetectedType(int number)
        {
            m_host.AddScriptLPS(1);
            EntityBase SensedObject = entityDetectedKey(number);
            if (SensedObject == null)
                return 0;
            int mask = 0;

            LLUUID SensedUUID = uuidDetectedKey(number);
            LSL_Types.Vector3 ZeroVector = new LSL_Types.Vector3(0, 0, 0);

            if (World.GetScenePresence(SensedUUID) != null) mask |= 0x01; // actor
            if (SensedObject.Velocity.Equals(ZeroVector))
                mask |= 0x04; // passive non-moving
            else
                mask |= 0x02; // active moving
            if (SensedObject is IScript) mask |= 0x08; // Scripted. It COULD have one hidden ... 
            return mask;

        }

        public LSL_Types.Vector3 llDetectedPos(int number)
        {
            m_host.AddScriptLPS(1);
            EntityBase SensedObject = entityDetectedKey(number);
            if (SensedObject == null)
                return new LSL_Types.Vector3(0, 0, 0);
               
            return new LSL_Types.Vector3(SensedObject.AbsolutePosition.X,SensedObject.AbsolutePosition.Y,SensedObject.AbsolutePosition.Z);
        }

        public LSL_Types.Vector3 llDetectedVel(int number)
        {
            m_host.AddScriptLPS(1);
            EntityBase SensedObject = entityDetectedKey(number);
            if (SensedObject == null)
                return new LSL_Types.Vector3(0, 0, 0);

            return new LSL_Types.Vector3(SensedObject.Velocity.X, SensedObject.Velocity.Y, SensedObject.Velocity.Z);
           // return new LSL_Types.Vector3();
        }

        public LSL_Types.Vector3 llDetectedGrab(int number)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llDetectedGrab");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.Quaternion llDetectedRot(int number)
        {
            m_host.AddScriptLPS(1);
            EntityBase SensedObject = entityDetectedKey(number);
            if (SensedObject == null)
                return new LSL_Types.Quaternion();

            return new LSL_Types.Quaternion(SensedObject.Rotation.x, SensedObject.Rotation.y, SensedObject.Rotation.z, SensedObject.Rotation.w);
        }

        public int llDetectedGroup(int number)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llDetectedGroup");
            return 0;
        }

        public int llDetectedLinkNumber(int number)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llDetectedLinkNumber");
            return 0;
        }

        public void llDie()
        {
            m_host.AddScriptLPS(1);
            World.DeleteSceneObjectGroup(m_host.ParentGroup);
            return;
        }

        public double llGround(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            int x = (int)(m_host.AbsolutePosition.X + offset.x);
            int y = (int)(m_host.AbsolutePosition.Y + offset.y);
            return World.GetLandHeight(x, y);
        }

        public double llCloud(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llCloud");
            return 0;
        }

        public LSL_Types.Vector3 llWind(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llWind");
            return new LSL_Types.Vector3();
        }

        public void llSetStatus(int status, int value)
        {
            m_host.AddScriptLPS(1);
            if ((status & BuiltIn_Commands_BaseClass.STATUS_PHYSICS) == BuiltIn_Commands_BaseClass.STATUS_PHYSICS)
            {
                if (value == 1)
                    m_host.ScriptSetPhysicsStatus(true);
                else
                    m_host.ScriptSetPhysicsStatus(false);

            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_PHANTOM) == BuiltIn_Commands_BaseClass.STATUS_PHANTOM)
            {
                if (value == 1)
                    m_host.ScriptSetPhantomStatus(true);
                else
                    m_host.ScriptSetPhantomStatus(false);
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_CAST_SHADOWS) == BuiltIn_Commands_BaseClass.STATUS_CAST_SHADOWS)
            {
                m_host.AddFlag(LLObject.ObjectFlags.CastShadows);
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_ROTATE_X) == BuiltIn_Commands_BaseClass.STATUS_ROTATE_X)
            {
                NotImplemented("llSetStatus - STATUS_ROTATE_X");
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_ROTATE_Y) == BuiltIn_Commands_BaseClass.STATUS_ROTATE_Y)
            {
                NotImplemented("llSetStatus - STATUS_ROTATE_Y");
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_ROTATE_Z) == BuiltIn_Commands_BaseClass.STATUS_ROTATE_Z)
            {
                NotImplemented("llSetStatus - STATUS_ROTATE_Z");
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_BLOCK_GRAB) == BuiltIn_Commands_BaseClass.STATUS_BLOCK_GRAB)
            {
                NotImplemented("llSetStatus - STATUS_BLOCK_GRAB");
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_DIE_AT_EDGE) == BuiltIn_Commands_BaseClass.STATUS_DIE_AT_EDGE)
            {
                NotImplemented("llSetStatus - STATUS_DIE_AT_EDGE");
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_RETURN_AT_EDGE) == BuiltIn_Commands_BaseClass.STATUS_RETURN_AT_EDGE)
            {
                NotImplemented("llSetStatus - STATUS_RETURN_AT_EDGE");
            }
            if ((status & BuiltIn_Commands_BaseClass.STATUS_SANDBOX) == BuiltIn_Commands_BaseClass.STATUS_SANDBOX)
            {
                NotImplemented("llSetStatus - STATUS_SANDBOX");
            }
            
            return;
        }

        public int llGetStatus(int status)
        {
            m_host.AddScriptLPS(1);
            Console.WriteLine(m_host.UUID.ToString() + " status is " + m_host.ObjectFlags.ToString());
            switch (status)
            {
                case BuiltIn_Commands_BaseClass.STATUS_PHYSICS:
                    if ((m_host.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) == (uint)LLObject.ObjectFlags.Physics)
                    {
                        return 1;
                    }
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_PHANTOM:
                    if ((m_host.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == (uint)LLObject.ObjectFlags.Phantom)
                    {
                        return 1;
                    }
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_CAST_SHADOWS:
                    if ((m_host.ObjectFlags & (uint)LLObject.ObjectFlags.CastShadows) == (uint)LLObject.ObjectFlags.CastShadows)
                    {
                        return 1;
                    }
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_BLOCK_GRAB:
                    NotImplemented("llGetStatus - STATUS_BLOCK_GRAB");
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_DIE_AT_EDGE:
                    NotImplemented("llGetStatus - STATUS_DIE_AT_EDGE");
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_RETURN_AT_EDGE:
                    NotImplemented("llGetStatus - STATUS_RETURN_AT_EDGE");
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_ROTATE_X:
                    NotImplemented("llGetStatus - STATUS_ROTATE_X");
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_ROTATE_Y:
                    NotImplemented("llGetStatus - STATUS_ROTATE_Y");
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_ROTATE_Z:
                    NotImplemented("llGetStatus - STATUS_ROTATE_Z");
                    return 0;
                case BuiltIn_Commands_BaseClass.STATUS_SANDBOX:
                    NotImplemented("llGetStatus - STATUS_SANDBOX");
                    return 0;
            }
            NotImplemented("llGetStatus - Unknown Status parameter");
            return 0;
        }

        public void llSetScale(LSL_Types.Vector3 scale)
        {
            m_host.AddScriptLPS(1);
            // TODO: this needs to trigger a persistance save as well
            LLVector3 tmp = m_host.Scale;
            tmp.X = (float)scale.x;
            tmp.Y = (float)scale.y;
            tmp.Z = (float)scale.z;
            m_host.Scale = tmp;
            m_host.SendFullUpdateToAllClients();
            return;
        }

        public LSL_Types.Vector3 llGetScale()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.Scale.X, m_host.Scale.Y, m_host.Scale.Z);
        }

        public void llSetColor(LSL_Types.Vector3 color, int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            LLColor texcolor;
            if (face > -1)
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.R = (float)Math.Abs(color.x - 1);
                texcolor.G = (float)Math.Abs(color.y - 1);
                texcolor.B = (float)Math.Abs(color.z - 1);
                tex.FaceTextures[face].RGBA = texcolor;
                m_host.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (uint i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.R = (float)Math.Abs(color.x - 1);
                        texcolor.G = (float)Math.Abs(color.y - 1);
                        texcolor.B = (float)Math.Abs(color.z - 1);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = (float)Math.Abs(color.x - 1);
                    texcolor.G = (float)Math.Abs(color.y - 1);
                    texcolor.B = (float)Math.Abs(color.z - 1);
                    tex.DefaultTexture.RGBA = texcolor;
                }
                m_host.UpdateTexture(tex);
                return;
            }
            else
            {
                NotImplemented("llSetColor");
            }
        }

        public double llGetAlpha(int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            if (face == -1) // TMP: Until we can determine number of sides, ALL_SIDES (-1) will return default color
            {
                return (double)((tex.DefaultTexture.RGBA.A * 255) / 255);
            }
            if (face > -1)
            {
                return (double)((tex.GetFace((uint)face).RGBA.A * 255) / 255);
            }
            return 0;
        }

        public void llSetAlpha(double alpha, int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            LLColor texcolor;
            if (face > -1)
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.A = (float)Math.Abs(alpha - 1);
                tex.FaceTextures[face].RGBA = texcolor;
                m_host.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.A = (float)Math.Abs(alpha - 1);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                }
                texcolor = tex.DefaultTexture.RGBA;
                texcolor.A = (float)Math.Abs(alpha - 1);
                tex.DefaultTexture.RGBA = texcolor;
                m_host.UpdateTexture(tex);
                return;
            }
            else
            {
                NotImplemented("llSetAlpha");
            }
        }

        public LSL_Types.Vector3 llGetColor(int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            LLColor texcolor;
            LSL_Types.Vector3 rgb;
            if (face == -1) // TMP: Until we can determine number of sides, ALL_SIDES (-1) will return default color
            {
                texcolor = tex.DefaultTexture.RGBA;
                rgb.x = (255 - (texcolor.R * 255)) / 255;
                rgb.y = (255 - (texcolor.G * 255)) / 255;
                rgb.z = (255 - (texcolor.B * 255)) / 255;
                return rgb;
            }
            if (face > -1)
            {
                texcolor = tex.GetFace((uint)face).RGBA;
                rgb.x = (255 - (texcolor.R * 255)) / 255;
                rgb.y = (255 - (texcolor.G * 255)) / 255;
                rgb.z = (255 - (texcolor.B * 255)) / 255;
                return rgb;
            }
            else
            {
                NotImplemented("llGetColor");
                return new LSL_Types.Vector3();
            }
        }

        public void llSetTexture(string texture, int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;

            if (face > -1)
            {
                LLObject.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.TextureID = new LLUUID(texture);
                tex.FaceTextures[face] = texface;
                m_host.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (uint i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].TextureID = new LLUUID(texture);
                    }
                }
                tex.DefaultTexture.TextureID = new LLUUID(texture);
                m_host.UpdateTexture(tex);
                return;
            }
            else
            {
                NotImplemented("llSetTexture");
            }
        }

        public void llScaleTexture(double u, double v, int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            if (face > -1)
            {
                LLObject.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.RepeatU = (float)u;
                texface.RepeatV = (float)v;
                tex.FaceTextures[face] = texface;
                m_host.UpdateTexture(tex);
                return;
            }
            if (face == -1)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].RepeatU = (float)u;
                        tex.FaceTextures[i].RepeatV = (float)v;
                    }
                }
                tex.DefaultTexture.RepeatU = (float)u;
                tex.DefaultTexture.RepeatV = (float)v;
                m_host.UpdateTexture(tex);
                return;
            }
            else
            {
                NotImplemented("llScaleTexture");
            }
        }

        public void llOffsetTexture(double u, double v, int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            if (face > -1)
            {
                LLObject.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.OffsetU = (float)u;
                texface.OffsetV = (float)v;
                tex.FaceTextures[face] = texface;
                m_host.UpdateTexture(tex);
                return;
            }
            if (face == -1)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].OffsetU = (float)u;
                        tex.FaceTextures[i].OffsetV = (float)v;
                    }
                }
                tex.DefaultTexture.OffsetU = (float)u;
                tex.DefaultTexture.OffsetV = (float)v;
                m_host.UpdateTexture(tex);
                return;
            }
            else
            {
                NotImplemented("llOffsetTexture");
            }
        }

        public void llRotateTexture(double rotation, int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            if (face > -1)
            {
                LLObject.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.Rotation = (float)rotation;
                tex.FaceTextures[face] = texface;
                m_host.UpdateTexture(tex);
                return;
            }
            if (face == -1)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].Rotation = (float)rotation;
                    }
                }
                tex.DefaultTexture.Rotation = (float)rotation;
                m_host.UpdateTexture(tex);
                return;
            }
            else
            {
                NotImplemented("llRotateTexture");
            }
        }

        public string llGetTexture(int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            if (face == -1)
            {
                face = 0;
            }
            if (face > -1)
            {
                LLObject.TextureEntryFace texface;
                texface = tex.GetFace((uint)face);
                return texface.TextureID.ToString();
            }
            else
            {
                NotImplemented("llGetTexture");
                return String.Empty;
            }
        }

        public void llSetPos(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            if (m_host.ParentID != 0)
            {
                m_host.UpdateOffSet(new LLVector3((float)pos.x, (float)pos.y, (float)pos.z));
            }
            else
            {
                m_host.UpdateGroupPosition(new LLVector3((float)pos.x, (float)pos.y, (float)pos.z));
            }
        }

        public LSL_Types.Vector3 llGetPos()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.AbsolutePosition.X,
                                         m_host.AbsolutePosition.Y,
                                         m_host.AbsolutePosition.Z);
        }

        public LSL_Types.Vector3 llGetLocalPos()
        {
            m_host.AddScriptLPS(1);
            if (m_host.ParentID != 0)
            {
                return new LSL_Types.Vector3(m_host.OffsetPosition.X,
                                             m_host.OffsetPosition.Y,
                                             m_host.OffsetPosition.Z);
            }
            else
            {
                return new LSL_Types.Vector3(m_host.AbsolutePosition.X,
                                             m_host.AbsolutePosition.Y,
                                             m_host.AbsolutePosition.Z);
            }
        }

        public void llSetRot(LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);
            m_host.UpdateRotation(new LLQuaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s));
            // Update rotation does not move the object in the physics scene if it's a linkset.
            m_host.ParentGroup.AbsolutePosition = m_host.ParentGroup.AbsolutePosition;
        }

        public LSL_Types.Quaternion llGetRot()
        {
            m_host.AddScriptLPS(1);
            LLQuaternion q = m_host.RotationOffset;
            return new LSL_Types.Quaternion(q.X, q.Y, q.Z, q.W);
        }

        public LSL_Types.Quaternion llGetLocalRot()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Quaternion(m_host.RotationOffset.X, m_host.RotationOffset.Y, m_host.RotationOffset.Z, m_host.RotationOffset.W);
        }

        public void llSetForce(LSL_Types.Vector3 force, int local)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetForce");
        }

        public LSL_Types.Vector3 llGetForce()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetForce");
            return new LSL_Types.Vector3();
        }

        public int llTarget(LSL_Types.Vector3 position, double range)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llTarget");
            return 0;
        }

        public void llTargetRemove(int number)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llTargetRemove");
        }

        public int llRotTarget(LSL_Types.Quaternion rot, double error)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRotTarget");
            return 0;
        }

        public void llRotTargetRemove(int number)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRotTargetRemove");
        }

        public void llMoveToTarget(LSL_Types.Vector3 target, double tau)
        {
            m_host.AddScriptLPS(1);
            m_host.MoveToTarget(new LLVector3((float)target.x, (float)target.y, (float)target.z), (float)tau);
        }

        public void llStopMoveToTarget()
        {
            m_host.AddScriptLPS(1);
            m_host.StopMoveToTarget();
        }

        public void llApplyImpulse(LSL_Types.Vector3 force, int local)
        {
            m_host.AddScriptLPS(1);
            //No energy force yet
            
            if (force.x > 20000)
                    force.x = 20000;
            if (force.y > 20000)
                    force.y = 20000;
            if (force.z > 20000)
                    force.z = 20000;
            
            if (local == 1)
            {
                m_host.ApplyImpulse(new LLVector3((float)force.x, (float)force.y, (float)force.z), true);
            }
            else
            {
               
                m_host.ApplyImpulse(new LLVector3((float)force.x,(float)force.y,(float)force.z), false);
            }
        }

        public void llApplyRotationalImpulse(LSL_Types.Vector3 force, int local)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llApplyRotationalImpulse");
        }

        public void llSetTorque(LSL_Types.Vector3 torque, int local)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetTorque");
        }

        public LSL_Types.Vector3 llGetTorque()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetTorque");
            return new LSL_Types.Vector3();
        }

        public void llSetForceAndTorque(LSL_Types.Vector3 force, LSL_Types.Vector3 torque, int local)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetForceAndTorque");
        }

        public LSL_Types.Vector3 llGetVel()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.Velocity.X, m_host.Velocity.Y, m_host.Velocity.Z);
        }

        public LSL_Types.Vector3 llGetAccel()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.Acceleration.X, m_host.Acceleration.Y, m_host.Acceleration.Z);
        }

        public LSL_Types.Vector3 llGetOmega()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.RotationalVelocity.X, m_host.RotationalVelocity.Y, m_host.RotationalVelocity.Z);
        }

        public double llGetTimeOfDay()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetTimeOfDay");
            return 0;
        }

        public double llGetWallclock()
        {
            m_host.AddScriptLPS(1);
            return DateTime.Now.TimeOfDay.TotalSeconds;
        }

        public double llGetTime()
        {
            m_host.AddScriptLPS(1);
            TimeSpan ScriptTime = DateTime.Now - m_timer;
            return (double)(ScriptTime.TotalMilliseconds / 1000);
        }

        public void llResetTime()
        {
            m_host.AddScriptLPS(1);
            m_timer = DateTime.Now;
        }

        public double llGetAndResetTime()
        {
            m_host.AddScriptLPS(1);
            TimeSpan ScriptTime = DateTime.Now - m_timer;
            m_timer = DateTime.Now;
            return (double)(ScriptTime.TotalMilliseconds / 1000);
        }

        public void llSound()
        {
            m_host.AddScriptLPS(1);
            // This function has been deprecated
            // see http://www.lslwiki.net/lslwiki/wakka.php?wakka=llSound
            Deprecated("llSound");
        }

        public void llPlaySound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            m_host.SendSound(sound, volume, false, 0);
        }

        public void llLoopSound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            m_host.SendSound(sound, volume, false, 1);
        }

        public void llLoopSoundMaster(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llLoopSoundMaster");
        }

        public void llLoopSoundSlave(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llLoopSoundSlave");
        }

        public void llPlaySoundSlave(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llPlaySoundSlave");
        }

        public void llTriggerSound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            m_host.SendSound(sound, volume, true, 0);
        }

        public void llStopSound()
        {
            m_host.AddScriptLPS(1);
            m_host.SendSound(LLUUID.Zero.ToString(), 1.0, false, 2);
        }

        public void llPreloadSound(string sound)
        {
            m_host.AddScriptLPS(1);
            m_host.PreloadSound(sound);
        }

        /// <summary>
        /// Return a portion of the designated string bounded by
        /// inclusive indices (start and end). As usual, the negative
        /// indices, and the tolerance for out-of-bound values, makes
        /// this more complicated than it might otherwise seem.
        /// </summary>

        public string llGetSubString(string src, int start, int end)
        {

            m_host.AddScriptLPS(1);

            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.

            if (start < 0)
            {
                start = src.Length+start;
            }
            if (end < 0)
            {
                end = src.Length+end;
            }

            // Conventional substring
            if (start <= end)
            {
                // Implies both bounds are out-of-range.
                if(end < 0 || start >= src.Length)
                {
                    return String.Empty;
                }
                // If end is positive, then it directly
                // corresponds to the lengt of the substring
                // needed (plus one of course). BUT, it 
                // must be within bounds.
                if(end >= src.Length)
                {
                    end = src.Length-1;
                }

                if(start < 0)
                {
                    return src.Substring(0,end+1);
                }
                // Both indices are positive
                return src.Substring(start, (end+1) - start);
            }

            // Inverted substring (end < start)
            else
            {
                // Implies both indices are below the 
                // lower bound. In the inverted case, that 
                // means the entire string will be returned
                // unchanged.
                if(start < 0)
                {
                    return src;
                }
                // If both indices are greater than the upper 
                // bound the result may seem initially counter
                // intuitive.
                if(end >= src.Length)
                {
                    return src;
                }

                if(end < 0)
                {
                    if(start < src.Length)
                    {
                        return src.Substring(start);
                    }
                    else
                    {
                        return String.Empty;
                    }
                }
                else
                {
                    if(start < src.Length)
                    {
                        return src.Substring(0,end+1) + src.Substring(start);
                    }
                    else
                    {
                        return src.Substring(0,end+1);
                    }
                }
            }
         }

        /// <summary>
        /// Delete substring removes the specified substring bounded
        /// by the inclusive indices start and end. Indices may be 
        /// negative (indicating end-relative) and may be inverted,
        /// i.e. end < start.
        /// </summary>

        public string llDeleteSubString(string src, int start, int end)
        {

            m_host.AddScriptLPS(1);

            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            if (start < 0)
            {
                start = src.Length+start;
            }
            if (end < 0)
            {
                end = src.Length+end;
            }
            // Conventionally delimited substring
            if (start <= end)
            {
                // If both bounds are outside of the existing
                // string, then return unchanges.
                if(end < 0 || start >= src.Length)
                {
                    return src;
                }
                // At least one bound is in-range, so we
                // need to clip the out-of-bound argument.
                if(start < 0)
                {
                    start = 0;
                }

                if(end >= src.Length)
                {
                    end = src.Length-1;
                }

                return src.Remove(start,end-start+1);
            }
            // Inverted substring
            else
            {
                // In this case, out of bounds means that
                // the existing string is part of the cut.
                if(start < 0 || end >= src.Length)
                {
                    return String.Empty;
                }
                
                if(end > 0)
                {
                    if(start < src.Length)
                    {
                        return src.Remove(start).Remove(0,end+1);
                    }
                    else
                    {
                        return src.Remove(0,end+1);
                    }
                }
                else
                {
                    if(start < src.Length)
                    {
                        return src.Remove(start);
                    }
                    else
                    {
                        return src;
                    }
                }
            }
        }
   
        /// <summary>
        /// Insert string inserts the specified string identified by src
        /// at the index indicated by index. Index may be negative, in
        /// which case it is end-relative. The index may exceed either
        /// string bound, with the result being a concatenation.
        /// </summary>

        public string llInsertString(string dest, int index, string src)
        {

            m_host.AddScriptLPS(1);

            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            if (index < 0)
            {
                index = dest.Length+index;

                // Negative now means it is less than the lower
                // bound of the string.

                if(index < 0)
                {
                    return src+dest;
                }

            }

            if(index >= dest.Length)
            {
                return dest+src;
            }

            // The index is in bounds.
            // In this case the index refers to the index that will
            // be assigned to the first character of the inserted string. 
            // So unlike the other string operations, we do not add one
            // to get the correct string length.
            return dest.Substring(0,index)+src+dest.Substring(index);

        }
   
        public string llToUpper(string src)
        {
            m_host.AddScriptLPS(1);
            return src.ToUpper();
        }

        public string llToLower(string src)
        {
            m_host.AddScriptLPS(1);
            return src.ToLower();
        }

        public int llGiveMoney(string destination, int amount)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGiveMoney");
            return 0;
        }

        public void llMakeExplosion()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeExplosion");
        }

        public void llMakeFountain()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeFountain");
        }

        public void llMakeSmoke()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeSmoke");
        }

        public void llMakeFire()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeFire");
        }

        public void llRezObject(string inventory, LSL_Types.Vector3 pos, LSL_Types.Quaternion rot, int param)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRezObject");
        }

        public void llLookAt(LSL_Types.Vector3 target, double strength, double damping)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llLookAt");
        }

        public void llStopLookAt()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llStopLookAt");
        }

        public void llSetTimerEvent(double sec)
        {
            m_host.AddScriptLPS(1);
            // Setting timer repeat
            m_ScriptEngine.m_ASYNCLSLCommandManager.m_Timer.SetTimerEvent(m_localID, m_itemID, sec);
        }

        public void llSleep(double sec)
        {
            m_host.AddScriptLPS(1);
            Thread.Sleep((int)(sec * 1000));
        }

        public double llGetMass()
        {
            m_host.AddScriptLPS(1);
            return m_host.GetMass();
        }

        public void llCollisionFilter(string name, string id, int accept)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llCollisionFilter");
        }

        public void llTakeControls(int controls, int accept, int pass_on)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llTakeControls");
        }

        public void llReleaseControls()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llReleaseControls");
        }

        public void llAttachToAvatar(int attachment)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llAttachToAvatar");
        }

        public void llDetachFromAvatar()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llDetachFromAvatar");
        }

        public void llTakeCamera()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llTakeCamera");
        }

        public void llReleaseCamera()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llReleaseCamera");
        }

        public string llGetOwner()
        {
            m_host.AddScriptLPS(1);

            return m_host.ObjectOwner.ToString();
        }

        public void llInstantMessage(string user, string message)
        {
            m_host.AddScriptLPS(1);

            // We may be able to use ClientView.SendInstantMessage here, but we need a client instance.
            // InstantMessageModule.OnInstantMessage searches through a list of scenes for a client matching the toAgent,
            // but I don't think we have a list of scenes available from here.
            // (We also don't want to duplicate the code in OnInstantMessage if we can avoid it.)
            
            // user is a UUID

            // TODO: figure out values for client, fromSession, and imSessionID
            // client.SendInstantMessage(m_host.UUID, fromSession, message, user, imSessionID, m_host.Name, AgentManager.InstantMessageDialog.MessageFromAgent, (uint)Util.UnixTimeSinceEpoch());
            LLUUID friendTransactionID = LLUUID.Random();

            //m_pendingFriendRequests.Add(friendTransactionID, fromAgentID);

            GridInstantMessage msg = new GridInstantMessage();
            msg.fromAgentID = new Guid(m_host.UUID.ToString()); // fromAgentID.UUID;
            msg.fromAgentSession = new Guid(friendTransactionID.ToString());// fromAgentSession.UUID;
            msg.toAgentID = new Guid(user); // toAgentID.UUID;
            msg.imSessionID = new Guid(friendTransactionID.ToString()); // This is the item we're mucking with here
            Console.WriteLine("[Scripting IM]: From:" + msg.fromAgentID.ToString() + " To: " + msg.toAgentID.ToString() + " Session:" + msg.imSessionID.ToString() + " Message:" + message);
            Console.WriteLine("[Scripting IM]: Filling Session: " + msg.imSessionID.ToString());
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch();// timestamp;
            //if (client != null)
            //{
                msg.fromAgentName = m_host.Name;//client.FirstName + " " + client.LastName;// fromAgentName;
            //}
            //else
            //{
            //    msg.fromAgentName = "(hippos)";// Added for posterity.  This means that we can't figure out who sent it
            //}
            msg.message = message;
            msg.dialog = (byte)19; // messgage from script ??? // dialog;
            msg.fromGroup = false;// fromGroup;
            msg.offline = (byte)0; //offline;
            msg.ParentEstateID = 0; //ParentEstateID;
            msg.Position = new sLLVector3();// new sLLVector3(m_host.AbsolutePosition);
            msg.RegionID = World.RegionInfo.RegionID.UUID;//RegionID.UUID;
            msg.binaryBucket = new byte[0];// binaryBucket;
            World.TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
            //  NotImplemented("llInstantMessage");
      }

        public void llEmail(string address, string subject, string message)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llEmail");
        }

        public void llGetNextEmail(string address, string subject)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetNextEmail");
        }

        public string llGetKey()
        {
            m_host.AddScriptLPS(1);
            return m_host.UUID.ToString();
        }

        public void llSetBuoyancy(double buoyancy)
        {
            m_host.AddScriptLPS(1);
            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    m_host.ParentGroup.RootPart.SetBuoyancy((float)buoyancy);
                }
            }
        }

         

        public void llSetHoverHeight(double height, int water, double tau)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetHoverHeight");
        }

        public void llStopHover()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llStopHover");
        }

        public void llMinEventDelay(double delay)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMinEventDelay");
        }

        public void llSoundPreload()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSoundPreload");
        }

        public void llRotLookAt(LSL_Types.Quaternion target, double strength, double damping)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRotLookAt");
        }

        public int llStringLength(string str)
        {
            m_host.AddScriptLPS(1);
            if (str.Length > 0)
            {
                return str.Length;
            }
            else
            {
                return 0;
            }
        }

        public void llStartAnimation(string anim)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llStartAnimation");
        }

        public void llStopAnimation(string anim)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llStopAnimation");
        }

        public void llPointAt()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llPointAt");
        }

        public void llStopPointAt()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llStopPointAt");
        }

        public void llTargetOmega(LSL_Types.Vector3 axis, double spinrate, double gain)
        {
            m_host.AddScriptLPS(1);
            m_host.RotationalVelocity = new LLVector3((float)(axis.x * spinrate), (float)(axis.y * spinrate), (float)(axis.z * spinrate));
            m_host.AngularVelocity = new LLVector3((float)(axis.x * spinrate), (float)(axis.y * spinrate), (float)(axis.z * spinrate));
            m_host.ScheduleTerseUpdate();
            m_host.SendTerseUpdateToAllClients();
        }

        public int llGetStartParameter()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetStartParameter");
            return 0;
        }

        public void llGodLikeRezObject(string inventory, LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGodLikeRezObject");
        }

        public void llRequestPermissions(string agent, int perm)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRequestPermissions");
        }

        public string llGetPermissionsKey()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetPermissionsKey");
            return String.Empty;
        }

        public int llGetPermissions()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetPermissions");
            return 0;
        }

        public int llGetLinkNumber()
        {
            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup.Children.Count > 0)
            {
                return m_host.LinkNum + 1;
            }
            else
            {
                return 0;
            }
        }

        public void llSetLinkColor(int linknumber, LSL_Types.Vector3 color, int face)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknumber);
            if (linknumber > -1)
            {
                LLObject.TextureEntry tex = part.Shape.Textures;
                LLColor texcolor;
                if (face > -1)
                {
                    texcolor = tex.CreateFace((uint)face).RGBA;
                    texcolor.R = (float)Math.Abs(color.x - 1);
                    texcolor.G = (float)Math.Abs(color.y - 1);
                    texcolor.B = (float)Math.Abs(color.z - 1);
                    tex.FaceTextures[face].RGBA = texcolor;
                    part.UpdateTexture(tex);
                    return;
                }
                else if (face == -1)
                {
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = (float)Math.Abs(color.x - 1);
                    texcolor.G = (float)Math.Abs(color.y - 1);
                    texcolor.B = (float)Math.Abs(color.z - 1);
                    tex.DefaultTexture.RGBA = texcolor;
                    for (uint i = 0; i < 32; i++)
                    {
                        if (tex.FaceTextures[i] != null)
                        {
                            texcolor = tex.FaceTextures[i].RGBA;
                            texcolor.R = (float)Math.Abs(color.x - 1);
                            texcolor.G = (float)Math.Abs(color.y - 1);
                            texcolor.B = (float)Math.Abs(color.z - 1);
                            tex.FaceTextures[i].RGBA = texcolor;
                        }
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = (float)Math.Abs(color.x - 1);
                    texcolor.G = (float)Math.Abs(color.y - 1);
                    texcolor.B = (float)Math.Abs(color.z - 1);
                    tex.DefaultTexture.RGBA = texcolor;
                    part.UpdateTexture(tex);
                    return;
                }
                return;
            }
            else if (linknumber == -1)
            {
                int num = m_host.ParentGroup.PrimCount;
                for (int w = 0; w < num; w++)
                {
                    linknumber = w;
                    part = m_host.ParentGroup.GetLinkNumPart(linknumber);
                    LLObject.TextureEntry tex = part.Shape.Textures;
                    LLColor texcolor;
                    if (face > -1)
                    {
                        texcolor = tex.CreateFace((uint)face).RGBA;
                        texcolor.R = (float)Math.Abs(color.x - 1);
                        texcolor.G = (float)Math.Abs(color.y - 1);
                        texcolor.B = (float)Math.Abs(color.z - 1);
                        tex.FaceTextures[face].RGBA = texcolor;
                        part.UpdateTexture(tex);
                    }
                    else if (face == -1)
                    {
                        texcolor = tex.DefaultTexture.RGBA;
                        texcolor.R = (float)Math.Abs(color.x - 1);
                        texcolor.G = (float)Math.Abs(color.y - 1);
                        texcolor.B = (float)Math.Abs(color.z - 1);
                        tex.DefaultTexture.RGBA = texcolor;
                        for (uint i = 0; i < 32; i++)
                        {
                            if (tex.FaceTextures[i] != null)
                            {
                                texcolor = tex.FaceTextures[i].RGBA;
                                texcolor.R = (float)Math.Abs(color.x - 1);
                                texcolor.G = (float)Math.Abs(color.y - 1);
                                texcolor.B = (float)Math.Abs(color.z - 1);
                                tex.FaceTextures[i].RGBA = texcolor;
                            }
                        }
                        texcolor = tex.DefaultTexture.RGBA;
                        texcolor.R = (float)Math.Abs(color.x - 1);
                        texcolor.G = (float)Math.Abs(color.y - 1);
                        texcolor.B = (float)Math.Abs(color.z - 1);
                        tex.DefaultTexture.RGBA = texcolor;
                        part.UpdateTexture(tex);
                    }
                }
                return;
            }
            else
            {
                NotImplemented("llSetLinkColor");
            }
        }

        public void llCreateLink(string target, int parent)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llCreateLink");
        }

        public void llBreakLink(int linknum)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llBreakLink");
        }

        public void llBreakAllLinks()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llBreakAllLinks");
        }

        public string llGetLinkKey(int linknum)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknum);
            if (part != null)
            {
                return part.UUID.ToString();
            }
            else
            {
                return "00000000-0000-0000-0000-000000000000";
            }
        }

        public string llGetLinkName(int linknum)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknum);
            if (part != null)
            {
                return part.Name;
            }
            else
            {
                return "00000000-0000-0000-0000-000000000000";
            }
        }

        public int llGetInventoryNumber(int type)
        {
            m_host.AddScriptLPS(1);
            int count = 0;
            foreach (KeyValuePair<LLUUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.InvType == type)
                {
                    count = count + 1;
                }
            }
            return count;
        }

        public string llGetInventoryName(int type, int number)
        {
            m_host.AddScriptLPS(1);
            ArrayList keys = new ArrayList();
            foreach (KeyValuePair<LLUUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.InvType == type)
                {
                    keys.Add(inv.Value.Name);
                }
            }
            if (keys.Count == 0)
            {
                return String.Empty;
            }
            keys.Sort();
            if (keys.Count > number)
            {
                return (string)keys[number];
            }
            return String.Empty;
        }

        public void llSetScriptState(string name, int run)
        {

            LLUUID item;
            ScriptManager sm;
            IScript script = null;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.
 
            if((item = ScriptByName(name)) != LLUUID.Zero)
                if((sm = m_ScriptEngine.m_ScriptManager) != null)
                    if(sm.Scripts.ContainsKey(m_localID))
                        if((script = sm.GetScript(m_localID, item)) != null)
                                script.Exec.Running = (run==0) ? false : true;
                

            // Required by SL

            if(script == null)
                ShoutError("llSetScriptState: script "+name+" not found");

            // If we didn;t find it, then it's safe to 
            // assume it is not running.

            return;

        }

        public double llGetEnergy()
        {
            m_host.AddScriptLPS(1);
            return 1.0f;
        }

        public void llGiveInventory(string destination, string inventory)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGiveInventory");
        }

        public void llRemoveInventory(string item)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRemoveInventory");
        }

        public void llSetText(string text, LSL_Types.Vector3 color, double alpha)
        {
            m_host.AddScriptLPS(1);
            Vector3 av3 = new Vector3((float)color.x, (float)color.y, (float)color.z);
            m_host.SetText(text, av3, alpha);
        }

        public double llWater(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            return World.RegionInfo.EstateSettings.waterHeight;
        }

        public void llPassTouches(int pass)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llPassTouches");
        }

        public string llRequestAgentData(string id, int data)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRequestAgentData");
            return String.Empty;
        }

        public string llRequestInventoryData(string name)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRequestInventoryData");
            return String.Empty;
        }

        public void llSetDamage(double damage)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetDamage");
        }

        public void llTeleportAgentHome(string agent)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llTeleportAgentHome");
        }

        public void llModifyLand(int action, int brush)
        {
            m_host.AddScriptLPS(1);
            if (World.PermissionsMngr.CanTerraform(m_host.OwnerID, new LLVector3(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, 0)))
            {
                NotImplemented("llModifyLand");
            }
        }

        public void llCollisionSound(string impact_sound, double impact_volume)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llCollisionSound");
        }

        public void llCollisionSprite(string impact_sprite)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llCollisionSprite");
        }

        public string llGetAnimation(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAnimation");
            return String.Empty;
        }

        public void llResetScript()
        {
            m_host.AddScriptLPS(1);
            m_ScriptEngine.m_ScriptManager.ResetScript(m_localID, m_itemID);
        }

        public void llMessageLinked(int linknum, int num, string msg, string id)
        {

            m_host.AddScriptLPS(1);

            uint partLocalID;
            LLUUID partItemID;

            switch ((int)linknum)
            {

                case (int)BuiltIn_Commands_BaseClass.LINK_ROOT:

                    SceneObjectPart part = m_host.ParentGroup.RootPart;

                    foreach (TaskInventoryItem item in part.TaskInventory.Values)
                    {
                        if (item.Type == 10)
                        {
                            partLocalID = part.LocalId;
                            partItemID = item.ItemID;

                            object[] resobj = new object[]
                            {
                                m_host.LinkNum + 1, num, msg, id
                            };

                            m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                                partLocalID, partItemID, "link_message", EventQueueManager.llDetectNull, resobj
                            );

                        }
                    }

                    break;

                case (int)BuiltIn_Commands_BaseClass.LINK_SET:

                    Console.WriteLine("LINK_SET");

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                        {
                            if (item.Type == 10)
                            {
                                partLocalID = partInst.LocalId;
                                partItemID = item.ItemID;
                                Object[] resobj = new object[]
                                {
                                    m_host.LinkNum + 1, num, msg, id
                                };

                                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                                    partLocalID, partItemID, "link_message", EventQueueManager.llDetectNull, resobj
                                );
                            }
                        }
                    }

                    break;

                case (int)BuiltIn_Commands_BaseClass.LINK_ALL_OTHERS:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        if (partInst.LocalId != m_host.LocalId)
                        {

                            foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                            {
                                if (item.Type == 10)
                                {
                                    partLocalID = partInst.LocalId;
                                    partItemID = item.ItemID;
                                    Object[] resobj = new object[]
                                    {
                                        m_host.LinkNum + 1, num, msg, id
                                    };

                                    m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                                        partLocalID, partItemID, "link_message", EventQueueManager.llDetectNull, resobj
                                    );
                                }
                            }

                        }
                    }

                    break;

                case (int)BuiltIn_Commands_BaseClass.LINK_ALL_CHILDREN:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        if (partInst.LocalId != m_host.ParentGroup.RootPart.LocalId)
                        {

                            foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                            {
                                if (item.Type == 10)
                                {
                                    partLocalID = partInst.LocalId;
                                    partItemID = item.ItemID;
                                    Object[] resobj = new object[]
                                    {
                                        m_host.LinkNum + 1, num, msg, id
                                    };

                                    m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                                        partLocalID, partItemID, "link_message", EventQueueManager.llDetectNull, resobj
                                    );
                                }
                            }

                        }
                    }

                    break;

                case (int)BuiltIn_Commands_BaseClass.LINK_THIS:

                    Object[] respObjThis = new object[]
                                {
                                    m_host.LinkNum + 1, num, msg, id
                                };

                    m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                        m_localID, m_itemID, "link_message", EventQueueManager.llDetectNull, respObjThis
                    );

                    break;

                default:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        if ((partInst.LinkNum + 1) == linknum)
                        {

                            foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                            {
                                if (item.Type == 10)
                                {
                                    partLocalID = partInst.LocalId;
                                    partItemID = item.ItemID;
                                    Object[] resObjDef = new object[]
                                    {
                                        m_host.LinkNum + 1, num, msg, id
                                    };

                                    m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                                        partLocalID, partItemID, "link_message", EventQueueManager.llDetectNull, resObjDef
                                    );
                                }
                            }

                        }
                    }

                    break;

            }

        }

        public void llPushObject(string target, LSL_Types.Vector3 impulse, LSL_Types.Vector3 ang_impulse, int local)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llPushObject");
        }

        public void llPassCollisions(int pass)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llPassCollisions");
        }

        public string llGetScriptName()
        {

            string result = String.Empty;

            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if(item.Type == 10 && item.ItemID == m_itemID)
                {
                    result =  item.Name!=null?item.Name:String.Empty;
                    break;
                }
            }

            return result;

        }

        public int llGetNumberOfSides()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetNumberOfSides");
            return 0;
        }

        public LSL_Types.Quaternion llAxisAngle2Rot(LSL_Types.Vector3 axis, double angle)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llAxisAngle2Rot");
            return new LSL_Types.Quaternion();
        }

        public LSL_Types.Vector3 llRot2Axis(LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRot2Axis");
            return new LSL_Types.Vector3();
        }

        public void llRot2Angle()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRot2Angle");
        }

        public double llAcos(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Acos(val);
        }

        public double llAsin(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Asin(val);
        }

        public double llAngleBetween(LSL_Types.Quaternion a, LSL_Types.Quaternion b)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llAngleBetween");
            return 0;
        }

        public string llGetInventoryKey(string name)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<LLUUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if(inv.Value.Name == name)
                {
                    if((inv.Value.OwnerMask & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify)) == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    {
                        return inv.Value.AssetID.ToString();
                    }
                    else
                    {
                        return LLUUID.Zero.ToString();
                    }
                }
            }
            return LLUUID.Zero.ToString();
        }

        public void llAllowInventoryDrop(int add)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llAllowInventoryDrop");
        }

        public LSL_Types.Vector3 llGetSunDirection()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetSunDirection");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.Vector3 llGetTextureOffset(int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            LSL_Types.Vector3 offset;
            if (face == -1)
            {
                face = 0;
            }
            offset.x = tex.GetFace((uint)face).OffsetU;
            offset.y = tex.GetFace((uint)face).OffsetV;
            offset.z = 0.0;
            return offset;
        }

        public LSL_Types.Vector3 llGetTextureScale(int side)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            LSL_Types.Vector3 scale;
            if (side == -1)
            {
                side = 0;
            }
            scale.x = tex.GetFace((uint)side).RepeatU;
            scale.y = tex.GetFace((uint)side).RepeatV;
            scale.z = 0.0;
            return scale;
        }

        public double llGetTextureRot(int face)
        {
            m_host.AddScriptLPS(1);
            LLObject.TextureEntry tex = m_host.Shape.Textures;
            if (face == -1)
            {
                face = 0;
            }
            return tex.GetFace((uint)face).Rotation;
        }

        public int llSubStringIndex(string source, string pattern)
        {
            m_host.AddScriptLPS(1);
            return source.IndexOf(pattern);
        }

        public string llGetOwnerKey(string id)
        {
            m_host.AddScriptLPS(1);
            LLUUID key = new LLUUID();
            if (LLUUID.TryParse(id, out key))
            {
                return World.GetSceneObjectPart(World.Entities[key].LocalId).OwnerID.ToString();
            }
            else
            {
                return LLUUID.Zero.ToString();
            }
        }

        public LSL_Types.Vector3 llGetCenterOfMass()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetCenterOfMass");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.list llListSort(LSL_Types.list src, int stride, int ascending)
        {
            m_host.AddScriptLPS(1);
            // SortedList<string, LSL_Types.list> sorted = new SortedList<string, LSL_Types.list>();
            // Add chunks to an array
            //int s = stride;
            //if (s < 1)
            //    s = 1;
            //int c = 0;
            //LSL_Types.list chunk = new LSL_Types.list();
            //string chunkString = String.Empty;
            //foreach (string element in src)
            //{
            //    c++;
            //    if (c > s)
            //    {
            //        sorted.Add(chunkString, chunk);
            //        chunkString = String.Empty;
            //        chunk = new LSL_Types.list();
            //        c = 0;
            //    }
            //    chunk.Add(element);
            //    chunkString += element.ToString();
            //}
            //if (chunk.Count > 0)
            //    sorted.Add(chunkString, chunk);

            //LSL_Types.list ret = new LSL_Types.list();
            //foreach (LSL_Types.list ls in sorted.Values)
            //{
            //    ret.AddRange(ls);
            //}

            //if (ascending == LSL_BaseClass.TRUE)
            //    return ret;
            //ret.Reverse();
            //return ret;
            NotImplemented("llListSort");
            return new LSL_Types.list();
        }

        public int llGetListLength(LSL_Types.list src)
        {
            m_host.AddScriptLPS(1);
            return src.Length;
        }

        public int llList2Integer(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0;
            }
            return Convert.ToInt32(src.Data[index]);
        }

        public double osList2Double(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0.0;
            }
            return Convert.ToDouble(src.Data[index]);
        }

        public double llList2Float(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0.0;
            }
            return Convert.ToDouble(src.Data[index]);
        }

        public string llList2String(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return String.Empty;
            }
            return src.Data[index].ToString();
        }

        public string llList2Key(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return "00000000-0000-0000-0000-000000000000";
            }
            //return OpenSim.Framework.ToString(src[index]);
            LLUUID tmpkey;
            if (LLUUID.TryParse(src.Data[index].ToString(), out tmpkey))
            {
                return tmpkey.ToString();
            }
            else
            {
                return "00000000-0000-0000-0000-000000000000";
            }
        }

        public LSL_Types.Vector3 llList2Vector(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return new LSL_Types.Vector3(0, 0, 0);
            }
            if (src.Data[index].GetType() == typeof(LSL_Types.Vector3))
            {
                return (LSL_Types.Vector3)src.Data[index];
            }
            else
            {
                return new LSL_Types.Vector3(0, 0, 0);
            }
        }

        public LSL_Types.Quaternion llList2Rot(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return new LSL_Types.Quaternion(0, 0, 0, 1);
            }
            if (src.Data[index].GetType() == typeof(LSL_Types.Quaternion))
            {
                return (LSL_Types.Quaternion)src.Data[index];
            }
            else
            {
                return new LSL_Types.Quaternion(0, 0, 0, 1);
            }
        }

        public LSL_Types.list llList2List(LSL_Types.list src, int start, int end)
        {
            m_host.AddScriptLPS(1);
            return src.GetSublist(start, end);
        }

        public LSL_Types.list llDeleteSubList(LSL_Types.list src, int start, int end)
        {
            //LSL_Types.list ret = new LSL_Types.list(src);
            //ret.RemoveRange(start, end - start);
            //return ret;

            // Just a hunch - needs testing
            return src.GetSublist(end, start);
        }

        public int llGetListEntryType(LSL_Types.list src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0;
            }

            if (src.Data[index] is Int32)
                return 1;
            if (src.Data[index] is Double)
                return 2;
            if (src.Data[index] is String)
            {
                LLUUID tuuid;
                if (LLUUID.TryParse(src.Data[index].ToString(), out tuuid))
                {
                    return 3;
                }
                else
                {
                    return 4;
                }
            }
            if (src.Data[index] is LSL_Types.Vector3)
                return 5;
            if (src.Data[index] is LSL_Types.Quaternion)
                return 6;
            if (src.Data[index] is LSL_Types.list)
                return 7;
            return 0;

        }

        /// <summary>
        /// Process the supplied list and return the
        /// content of the list formatted as a comma
        /// separated list. There is a space after
        /// each comma.
        /// </summary>

        public string llList2CSV(LSL_Types.list src)
        {

            string ret = String.Empty;
            int    x   = 0;

            m_host.AddScriptLPS(1);

            if(src.Data.Length > 0)
            {
                ret = src.Data[x++].ToString();
                for(;x<src.Data.Length;x++)
                {
                    ret += ", "+src.Data[x].ToString();
                }
            }

            return ret;

        }

        /// <summary>
        /// The supplied string is scanned for commas
        /// and converted into a list. Commas are only
        /// effective if they are encountered outside 
        /// of '<' '>' delimiters. Any whitespace
        /// before or after an element is trimmed.
        /// </summary>

        public LSL_Types.list llCSV2List(string src)
        {

            LSL_Types.list result = new LSL_Types.list();
            int parens = 0;
            int start  = 0;
            int length = 0;

            m_host.AddScriptLPS(1);

            for(int i=0; i<src.Length; i++)
            {
                switch(src[i])
                {
                    case '<' :
                        parens++;
                        length++;
                        break;
                    case '>' :
                        if(parens > 0)
                            parens--;
                        length++;
                        break;
                    case ',' :
                        if(parens == 0)
                        {
                            result.Add(src.Substring(start,length).Trim());
                            start += length+1;
                            length = 0;
                        } else
                            length++;
                        break;
                    default  :
                        length++;
                        break;
                }
            }

            result.Add(src.Substring(start,length).Trim());

            return result;

        }

        ///  <summary>
        ///  Randomizes the list, be arbitrarily reordering 
        ///  sublists of stride elements. As the stride approaches
        ///  the size of the list, the options become very
        ///  limited.
        ///  </summary>
        ///  <remarks>
        ///  This could take a while for very large list
        ///  sizes.
        ///  </remarks>
 
        public LSL_Types.list llListRandomize(LSL_Types.list src, int stride)
        {

            LSL_Types.list result;
            Random rand           = new Random();

            int   chunkk;
            int[] chunks;
            int   index1;
            int   index2;
            int   tmp;

            m_host.AddScriptLPS(1);

            if(stride == 0)
                stride = 1;

            // Stride MUST be a factor of the list length
            // If not, then return the src list. This also
            // traps those cases where stride > length.
 
            if(src.Length != stride && src.Length%stride == 0)
            {

                chunkk = src.Length/stride;

                chunks = new int[chunkk];

                for(int i=0;i<chunkk;i++)
                    chunks[i] = i;

                for(int i=0; i<chunkk-1; i++)
                {
                    //  randomly select 2 chunks
                    index1 = rand.Next(rand.Next(65536));
                    index1 = index1%chunkk;
                    index2 = rand.Next(rand.Next(65536));
                    index2 = index2%chunkk;

                    //  and swap their relative positions
                    tmp = chunks[index1];
                    chunks[index1] = chunks[index2];
                    chunks[index2] = tmp;
                }

                // Construct the randomized list

                result = new LSL_Types.list();

                for(int i=0; i<chunkk; i++)
                    for(int j=0;j<stride;j++)
                        result.Add(src.Data[chunks[i]*stride+j]);

            }
            else {
                object[] array = new object[src.Length];
                Array.Copy(src.Data, 0, array, 0, src.Length);
                result = new LSL_Types.list(array);
            }    

            return result;

        }

        /// <summary>
        /// Elements in the source list starting with 0 and then
        /// every i+stride. If the stride is negative then the scan
        /// is backwards producing an inverted result.
        /// Only those elements that are also in the specified 
        /// range are included in the result.
        /// </summary>
 
        public LSL_Types.list llList2ListStrided(LSL_Types.list src, int start, int end, int stride)
        {

            LSL_Types.list result = new LSL_Types.list();
            int[] si = new int[2];
            int[] ei = new int[2];
            bool twopass = false;

            m_host.AddScriptLPS(1);

            //  First step is always to deal with negative indices

            if(start < 0)
                start = src.Length+start;
            if(end   < 0)
                end   = src.Length+end;

            //  Out of bounds indices are OK, just trim them 
            //  accordingly

            if(start > src.Length)
                start = src.Length;

            if(end > src.Length)
                end = src.Length;

            //  There may be one or two ranges to be considered

            if(start != end)
            {

                if(start <= end) 
                {
                   si[0] = start;
                   ei[0] = end;
                }
                else
                {
                   si[1] = start;
                   ei[1] = src.Length;
                   si[0] = 0;
                   ei[0] = end;
                   twopass = true;
                }

                //  The scan always starts from the beginning of the
                //  source list, but members are only selected if they
                //  fall within the specified sub-range. The specified
                //  range values are inclusive.
                //  A negative stride reverses the direction of the
                //  scan producing an inverted list as a result. 
                
                if(stride == 0)
                    stride = 1;

                if(stride > 0)
                    for(int i=0;i<src.Length;i+=stride)
                    {
                        if(i<=ei[0] && i>=si[0])
                            result.Add(src.Data[i]);
                        if(twopass && i>=si[1] && i<=ei[1])
                            result.Add(src.Data[i]);
                    }
                else if(stride < 0)
                    for(int i=src.Length-1;i>=0;i+=stride)
                    {
                        if(i<=ei[0] && i>=si[0])
                            result.Add(src.Data[i]);
                        if(twopass && i>=si[1] && i<=ei[1])
                            result.Add(src.Data[i]);
                    }
            }

            return result;

        }

        public LSL_Types.Vector3 llGetRegionCorner()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(World.RegionInfo.RegionLocX * Constants.RegionSize, World.RegionInfo.RegionLocY * Constants.RegionSize, 0);
        }

        /// <summary>
        /// Insert the list identified by <src> into the
        /// list designated by <dest> such that the first
        /// new element has the index specified by <index>
        /// </summary>

        public LSL_Types.list llListInsertList(LSL_Types.list dest, LSL_Types.list src, int index)
        {
      
            LSL_Types.list pref = null;
            LSL_Types.list suff = null;

            m_host.AddScriptLPS(1);

            if(index < 0)
            {
                index = index+src.Length;
                if(index < 0)
                {
                    index = 0;
                }
            }

            if(index != 0)
            {
                pref = dest.GetSublist(0,index-1);
                if(index < dest.Length)
                {
                    suff = dest.GetSublist(index,-1);
                    return pref + src + suff;
                }
                else
                {
                    return pref + src;
                }
            }
            else
            {
                if(index < dest.Length)
                {
                    suff = dest.GetSublist(index,-1);
                    return src + suff;
                }
                else
                {
                    return src;
                }
            }

        }

        /// <summary>
        /// Returns the index of the first occurrence of test
        /// in src.
        /// </summary>
 
        public int llListFindList(LSL_Types.list src, LSL_Types.list test)
        {

            int index  = -1;
            int length = src.Length - test.Length + 1;

            m_host.AddScriptLPS(1);

            // If either list is empty, do not match

            if(src.Length != 0 && test.Length != 0)
            {
                for(int i=0; i< length; i++)
                {
                   if(src.Data[i].Equals(test.Data[0]))
                   {
                       int j;
                       for(j=1;j<test.Length;j++)
                           if(!src.Data[i+j].Equals(test.Data[j]))
                               break;
                       if(j == test.Length)
                       {
                           index = i;
                           break;
                       }
                   }
                }
            }
 
            return index;

        }

        public string llGetObjectName()
        {
            m_host.AddScriptLPS(1);
            return m_host.Name!=null?m_host.Name:String.Empty;
        }

        public void llSetObjectName(string name)
        {
            m_host.AddScriptLPS(1);
            m_host.Name = name!=null?name:String.Empty;
        }

        public string llGetDate()
        {
            m_host.AddScriptLPS(1);
            DateTime date = DateTime.Now.ToUniversalTime();
            string result = date.ToString("yyyy-MM-dd");
            return result;
        }

        public int llEdgeOfWorld(LSL_Types.Vector3 pos, LSL_Types.Vector3 dir)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llEdgeOfWorld");
            return 0;
        }

        public int llGetAgentInfo(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAgentInfo");
            return 0;
        }

        public void llAdjustSoundVolume(double volume)
        {
            m_host.AddScriptLPS(1);
            m_host.AdjustSoundGain(volume);
        }

        public void llSetSoundQueueing(int queue)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetSoundQueueing");
        }

        public void llSetSoundRadius(double radius)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetSoundRadius");
        }

        public string llKey2Name(string id)
        {
            m_host.AddScriptLPS(1);
            LLUUID key = new LLUUID();
            if (LLUUID.TryParse(id,out key))
            {
                if (World.m_innerScene.ScenePresences.ContainsKey(key))
                {
                    return World.m_innerScene.ScenePresences[key].Firstname + " " + World.m_innerScene.ScenePresences[key].Lastname;
                }
                if (World.GetSceneObjectPart(key) != null)
                {
                    return World.GetSceneObjectPart(key).Name;
                }
            }
            return String.Empty;
        }

        

        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_host.AddScriptLPS(1);
            Primitive.TextureAnimation pTexAnim = new Primitive.TextureAnimation();
            pTexAnim.Flags =(uint) mode;
            
            //ALL_SIDES
            if (face == -1)
                    face = 255;
            
            pTexAnim.Face = (uint)face;
            pTexAnim.Length = (float)length;
            pTexAnim.Rate = (float)rate;
            pTexAnim.SizeX = (uint)sizex;
            pTexAnim.SizeY = (uint)sizey;
            pTexAnim.Start = (float)start;

            m_host.AddTextureAnimation(pTexAnim);
            m_host.SendFullUpdateToAllClients();
        }

        public void llTriggerSoundLimited(string sound, double volume, LSL_Types.Vector3 top_north_east,
                                          LSL_Types.Vector3 bottom_south_west)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llTriggerSoundLimited");
        }

        public void llEjectFromLand(string pest)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llEjectFromLand");
        }

        public LSL_Types.list llParseString2List(string str, LSL_Types.list separators, LSL_Types.list spacers)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list ret = new LSL_Types.list();
            object[] delimiters = new object[separators.Length + spacers.Length];
            separators.Data.CopyTo(delimiters, 0);
            spacers.Data.CopyTo(delimiters, separators.Length); 
            bool dfound = false;
            do
            {
                dfound = false;
                int cindex = -1;
                string cdeli = "";
                for (int i = 0; i < delimiters.Length; i++)
                {
                    int index = str.IndexOf(delimiters[i].ToString());
                    bool found = index != -1;
                    if (found)
                    {
                        if ((cindex > index) || (cindex == -1))
                        {
                            cindex = index;
                            cdeli = (string)delimiters[i];
                        }
                        dfound = dfound || found;
                    }
                }
                if (cindex != -1)
                {
                    if (cindex > 0)
                    {
                        ret.Add(str.Substring(0, cindex));
                        if (spacers.Contains(cdeli))
                        {
                            ret.Add(cdeli);
                        }
                    }
                    if (cindex == 0 && spacers.Contains(cdeli))
                    {
                        ret.Add(cdeli);
                    }
                    str = str.Substring(cindex + cdeli.Length);
                }
            } while (dfound);
            if (str != "")
            {
                ret.Add(str);
            }
            return ret;
        }

        public int llOverMyLand(string id)
        {
            
            m_host.AddScriptLPS(1);
            LLUUID key = new LLUUID();
            if (LLUUID.TryParse(id,out key))
            {
                SceneObjectPart obj = new SceneObjectPart();
                obj = World.GetSceneObjectPart(World.Entities[key].LocalId);
                if (obj.OwnerID == World.GetLandOwner(obj.AbsolutePosition.X, obj.AbsolutePosition.Y))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        public string llGetLandOwnerAt(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            return World.GetLandOwner((float)pos.x, (float)pos.y).ToString();
        }

        public string llGetNotecardLine(string name, int line)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetNotecardLine");
            return String.Empty;
        }

        public LSL_Types.Vector3 llGetAgentSize(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAgentSize");
            return new LSL_Types.Vector3();
        }

        public int llSameGroup(string agent)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSameGroup");
            return 0;
        }

        public void llUnSit(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llUnSit");
        }

        public LSL_Types.Vector3 llGroundSlope(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGroundSlope");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.Vector3 llGroundNormal(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGroundNormal");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.Vector3 llGroundContour(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGroundContour");
            return new LSL_Types.Vector3();
        }

        public int llGetAttached()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAttached");
            return 0;
        }

        public int llGetFreeMemory()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetFreeMemory");
            return 0;
        }

        public string llGetRegionName()
        {
            m_host.AddScriptLPS(1);
            return World.RegionInfo.RegionName;
        }

        public double llGetRegionTimeDilation()
        {
            m_host.AddScriptLPS(1);
            return (double)World.TimeDilation;
        }

        public double llGetRegionFPS()
        {
            m_host.AddScriptLPS(1);
            return 10.0f;
        }

        /* particle system rules should be coming into this routine as doubles, that is
        rule[0] should be an integer from this list and rule[1] should be the arg
        for the same integer. wiki.secondlife.com has most of this mapping, but some
        came from http://www.caligari-designs.com/p4u2

        We iterate through the list for 'Count' elements, incrementing by two for each
        iteration and set the members of Primitive.ParticleSystem, one at a time.
        */

        public enum PrimitiveRule : int
        {
            PSYS_PART_FLAGS = 0,
            PSYS_PART_START_COLOR = 1,
            PSYS_PART_START_ALPHA = 2,
            PSYS_PART_END_COLOR = 3,
            PSYS_PART_END_ALPHA = 4,
            PSYS_PART_START_SCALE = 5,
            PSYS_PART_END_SCALE = 6,
            PSYS_PART_MAX_AGE = 7,
            PSYS_SRC_ACCEL = 8,
            PSYS_SRC_PATTERN = 9,
            PSYS_SRC_TEXTURE = 12,
            PSYS_SRC_BURST_RATE = 13,
            PSYS_SRC_BURST_PART_COUNT = 15,
            PSYS_SRC_BURST_RADIUS = 16,
            PSYS_SRC_BURST_SPEED_MIN = 17,
            PSYS_SRC_BURST_SPEED_MAX = 18,
            PSYS_SRC_MAX_AGE = 19,
            PSYS_SRC_TARGET_KEY = 20,
            PSYS_SRC_OMEGA = 21,
            PSYS_SRC_ANGLE_BEGIN = 22,
            PSYS_SRC_ANGLE_END = 23
        }

        internal Primitive.ParticleSystem.ParticleDataFlags ConvertUINTtoFlags(uint flags)
        {
            Primitive.ParticleSystem.ParticleDataFlags returnval = Primitive.ParticleSystem.ParticleDataFlags.None;

            return returnval;
        }

        // AL: This does not actually do anything yet. There are issues within Libsecondlife revolving around PSYS_PART_FLAGS
        // (need to OR the values, but currently stores this within an enum) as well as discovery of how the CRC works and the
        // actual packet. 

        public void llParticleSystem(LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);
            Primitive.ParticleSystem prules = new Primitive.ParticleSystem();
            LSL_Types.Vector3 tempv = new LSL_Types.Vector3();
            
            float tempf = 0;

            for (int i = 0; i < rules.Length; i += 2)
            {
                switch ((int)rules.Data[i])
                {
                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_FLAGS:
                        prules.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)((uint)Convert.ToInt32(rules.Data[i + 1].ToString()));
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_START_COLOR:
                        tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                        prules.PartStartColor.R = (float)tempv.x;
                        prules.PartStartColor.G = (float)tempv.y;
                        prules.PartStartColor.B = (float)tempv.z;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_START_ALPHA:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.PartStartColor.A = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_END_COLOR:
                        tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                        //prules.PartEndColor = new LLColor(tempv.x,tempv.y,tempv.z,1);
                        
                        prules.PartEndColor.R = (float)tempv.x;
                        prules.PartEndColor.G = (float)tempv.y;
                        prules.PartEndColor.B = (float)tempv.z;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_END_ALPHA:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.PartEndColor.A = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_START_SCALE:
                        tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                        prules.PartStartScaleX = (float)tempv.x;
                        prules.PartStartScaleY = (float)tempv.y;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_END_SCALE:
                        tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                        prules.PartEndScaleX = (float)tempv.x;
                        prules.PartEndScaleY = (float)tempv.y;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_PART_MAX_AGE:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.PartMaxAge = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_ACCEL:
                        tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                        prules.PartAcceleration.X = (float)tempv.x;
                        prules.PartAcceleration.Y = (float)tempv.y;
                        prules.PartAcceleration.Z = (float)tempv.z;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_PATTERN:
                        int tmpi = (int)rules.Data[i + 1];
                        prules.Pattern = (Primitive.ParticleSystem.SourcePattern)tmpi;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_TEXTURE:
                        prules.Texture = new LLUUID(rules.Data[i + 1].ToString());
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_BURST_RATE:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.BurstRate = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_BURST_PART_COUNT:
                        prules.BurstPartCount = (byte)Convert.ToByte(rules.Data[i + 1].ToString());
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_BURST_RADIUS:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.BurstRadius = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_BURST_SPEED_MIN:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.BurstSpeedMin = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_BURST_SPEED_MAX:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.BurstSpeedMax = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_MAX_AGE:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.MaxAge = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_TARGET_KEY:
                        LLUUID key = LLUUID.Zero;
                        if (LLUUID.TryParse(rules.Data[i + 1].ToString(), out key))
                        {
                            prules.Target = key;
                        }
                        else
                        {
                            prules.Target = m_host.UUID;
                        }
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_OMEGA:
                        // AL: This is an assumption, since it is the only thing that would match.
                        tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                        prules.AngularVelocity.X = (float)tempv.x;
                        prules.AngularVelocity.Y = (float)tempv.y;
                        prules.AngularVelocity.Z = (float)tempv.z;
                        //cast??                    prules.MaxAge = (float)rules[i + 1];
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_ANGLE_BEGIN:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.InnerAngle = (float)tempf;
                        break;

                    case (int)BuiltIn_Commands_BaseClass.PSYS_SRC_ANGLE_END:
                        tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                        prules.OuterAngle = (float)tempf;
                        break;
                }

            }
            prules.CRC = 1;

            m_host.AddNewParticleSystem(prules);
            m_host.SendFullUpdateToAllClients();
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGroundRepel");
        }

        public void llGiveInventoryList()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGiveInventoryList");
        }

        public void llSetVehicleType(int type)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetVehicleType");
        }

        public void llSetVehicledoubleParam(int param, double value)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetVehicledoubleParam");
        }

        public void llSetVehicleVectorParam(int param, LSL_Types.Vector3 vec)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetVehicleVectorParam");
        }

        public void llSetVehicleRotationParam(int param, LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetVehicleRotationParam");
        }

        public void llSetVehicleFlags(int flags)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetVehicleFlags");
        }

        public void llRemoveVehicleFlags(int flags)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRemoveVehicleFlags");
        }

        public void llSitTarget(LSL_Types.Vector3 offset, LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);
            // LSL quaternions can normalize to 0, normal Quaternions can't.
            if (rot.s == 0 && rot.x == 0 && rot.y == 0 && rot.z == 0)
                rot.z = 1; // ZERO_ROTATION = 0,0,0,1

            m_host.SetSitTarget(new Vector3((float)offset.x, (float)offset.y, (float)offset.z), new Quaternion((float)rot.s, (float)rot.x, (float)rot.y, (float)rot.z));
        }

        public string llAvatarOnSitTarget()
        {
            m_host.AddScriptLPS(1);
            return m_host.GetAvatarOnSitTarget().ToString();
            //LLUUID AVID = m_host.GetAvatarOnSitTarget();
     
            //if (AVID != LLUUID.Zero)
            //    return AVID.ToString();
            //else
            //    return String.Empty;
        }

        public void llAddToLandPassList(string avatar, double hours)
        {
            m_host.AddScriptLPS(1);
            LLUUID key;
            LandData land = World.LandChannel.getLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.ownerID == m_host.OwnerID)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                if (LLUUID.TryParse(avatar, out key))
                {
                    entry.AgentID = key;
                    entry.Flags = ParcelManager.AccessList.Access;
                    entry.Time = DateTime.Now.AddHours(hours);
                    land.parcelAccessList.Add(entry);
                }
            }
        }

        public void llSetTouchText(string text)
        {
            m_host.AddScriptLPS(1);
            m_host.TouchName = text;
        }

        public void llSetSitText(string text)
        {
            m_host.AddScriptLPS(1);
            m_host.SitName = text;
        }

        public void llSetCameraEyeOffset(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetCameraEyeOffset");
        }

        public void llSetCameraAtOffset(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetCameraAtOffset");
        }

        public string llDumpList2String(LSL_Types.list src, string seperator)
        {
            m_host.AddScriptLPS(1);
            if (src.Length == 0)
            {
                return String.Empty;
            }
            string ret = String.Empty;
            foreach (object o in src.Data)
            {
                ret = ret + o.ToString() + seperator;
            }
            ret = ret.Substring(0, ret.Length - seperator.Length);
            return ret;
        }

        public void llScriptDanger(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llScriptDanger");
        }

        public void llDialog(string avatar, string message, LSL_Types.list buttons, int chat_channel)
        {
            m_host.AddScriptLPS(1);
            LLUUID av = new LLUUID();
            if (!LLUUID.TryParse(avatar,out av))
            {
                LSLError("First parameter to llDialog needs to be a key");
                return;
            }
            string[] buts = new string[buttons.Length];
            for(int i = 0; i < buttons.Length; i++)
            {
                buts[i] = buttons.Data[i].ToString();
            }
            World.SendDialogToUser(av, m_host.Name, m_host.UUID, m_host.OwnerID, message, new LLUUID("00000000-0000-2222-3333-100000001000"), chat_channel, buts);
        }

        public void llVolumeDetect(int detect)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llVolumeDetect");
        }

        /// <summary>
        /// Reset the named script. The script must be present
        /// in the same prim.
        /// </summary>

        public void llResetOtherScript(string name)
        {

            LLUUID item;
            ScriptManager sm;
            IScript script = null;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.
 
            if((item = ScriptByName(name)) != LLUUID.Zero)
                if((sm = m_ScriptEngine.m_ScriptManager) != null)
                    sm.ResetScript(m_localID, item);

            // Required by SL

            if(script == null)
                ShoutError("llResetOtherScript: script "+name+" not found");

            // If we didn;t find it, then it's safe to 
            // assume it is not running.

            return;

        }

        public int llGetScriptState(string name)
        {

            LLUUID item;
            ScriptManager sm;
            IScript script = null;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.
 
            if((item = ScriptByName(name)) != LLUUID.Zero)
                if((sm = m_ScriptEngine.m_ScriptManager) != null)
                    if((script = sm.GetScript(m_localID, item)) != null)
                        return script.Exec.Running?1:0;

            // Required by SL

            if(script == null)
                ShoutError("llGetScriptState: script "+name+" not found");

            // If we didn;t find it, then it's safe to 
            // assume it is not running.

            return 0;

        }

        public void llRemoteLoadScript()
        {
            m_host.AddScriptLPS(1);
            Deprecated("llRemoteLoadScript");
        }

        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetRemoteScriptAccessPin");
        }

        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRemoteLoadScriptPin");
        }

        //  remote_data(integer type, key channel, key message_id, string sender, integer ival, string sval)
        // Not sure where these constants should live:
        // REMOTE_DATA_CHANNEL = 1
        // REMOTE_DATA_REQUEST = 2
        // REMOTE_DATA_REPLY = 3
        public void llOpenRemoteDataChannel()
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod.IsEnabled())
            {
                LLUUID channelID = xmlrpcMod.OpenXMLRPCChannel(m_localID, m_itemID);
                object[] resobj = new object[] { 1, channelID.ToString(), LLUUID.Zero.ToString(), String.Empty, 0, String.Empty };
                m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(m_localID, m_itemID, "remote_data", EventQueueManager.llDetectNull, resobj);
            }
        }

        public string llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            return (xmlrpcMod.SendRemoteData(m_localID, m_itemID, channel, dest, idata, sdata)).ToString();
        }

        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod.RemoteDataReply(channel, message_id, sdata, idata);
        }

        public void llCloseRemoteDataChannel(string channel)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod.CloseXMLRPCChannel(channel);
        }

        public string llMD5String(string src, int nonce)
        {
            m_host.AddScriptLPS(1);
            return Util.Md5Hash(src + ":" + nonce.ToString());
        }

        public void llSetPrimitiveParams(LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetPrimitiveParams");
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetLinkPrimitiveParams");
        }

        public string llStringToBase64(string str)
        {
            m_host.AddScriptLPS(1);
            try
            {
                byte[] encData_byte = new byte[str.Length];
                encData_byte = Encoding.UTF8.GetBytes(str);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Encode" + e.Message);
            }
        }

        public string llBase64ToString(string str)
        {
            m_host.AddScriptLPS(1);
            UTF8Encoding encoder = new UTF8Encoding();
            Decoder utf8Decode = encoder.GetDecoder();
            try
            {
                byte[] todecode_byte = Convert.FromBase64String(str);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                string result = new String(decoded_char);
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Decode" + e.Message);
            }
        }

        public void llXorBase64Strings()
        {
            m_host.AddScriptLPS(1);
            Deprecated("llXorBase64Strings");
        }

        public void llRemoteDataSetRegion()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRemoteDataSetRegion");
        }

        public double llLog10(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Log10(val);
        }

        public double llLog(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Log(val);
        }

        public LSL_Types.list llGetAnimationList(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAnimationList");
            return new LSL_Types.list();
        }

        public void llSetParcelMusicURL(string url)
        {
            m_host.AddScriptLPS(1);
            LLUUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);
            if (landowner == LLUUID.Zero)
            {
                return;
            }
            if (landowner != m_host.ObjectOwner)
            {
                return;
            }
            World.SetLandMusicURL(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, url);
        }

        public void osSetParcelMediaURL(string url)
        {
            m_host.AddScriptLPS(1);
            LLUUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if(landowner == LLUUID.Zero)
            {
                return;
            }
        
            if(landowner != m_host.ObjectOwner)
            {
                return;
            }

            World.SetLandMediaURL(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, url);
        }

        public LSL_Types.Vector3 llGetRootPosition()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.ParentGroup.AbsolutePosition.X, m_host.ParentGroup.AbsolutePosition.Y, m_host.ParentGroup.AbsolutePosition.Z);
        }

        public LSL_Types.Quaternion llGetRootRotation()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Quaternion(m_host.ParentGroup.GroupRotation.X, m_host.ParentGroup.GroupRotation.Y, m_host.ParentGroup.GroupRotation.Z, m_host.ParentGroup.GroupRotation.W);
        }

        public string llGetObjectDesc()
        {
            return m_host.Description!=null?m_host.Description:String.Empty;
        }

        public void llSetObjectDesc(string desc)
        {
            m_host.AddScriptLPS(1);
            m_host.Description = desc!=null?desc:String.Empty;
        }

        public string llGetCreator()
        {
            m_host.AddScriptLPS(1);
            return m_host.ObjectCreator.ToString();
        }

        public string llGetTimestamp()
        {
            m_host.AddScriptLPS(1);
            return DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }

        public void llSetLinkAlpha(int linknumber, double alpha, int face)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknumber);
            if (linknumber > -1)
            {
                LLObject.TextureEntry tex = part.Shape.Textures;
                LLColor texcolor;
                if (face > -1)
                {
                    texcolor = tex.CreateFace((uint)face).RGBA;
                    texcolor.A = (float)Math.Abs(alpha - 1);
                    tex.FaceTextures[face].RGBA = texcolor;
                    part.UpdateTexture(tex);
                    return;
                }
                else if (face == -1)
                {
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.A = (float)Math.Abs(alpha - 1);
                    tex.DefaultTexture.RGBA = texcolor;
                    for (uint i = 0; i < 32; i++)
                    {
                        if (tex.FaceTextures[i] != null)
                        {
                            texcolor = tex.FaceTextures[i].RGBA;
                            texcolor.A = (float)Math.Abs(alpha - 1);
                            tex.FaceTextures[i].RGBA = texcolor;
                        }
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.A = (float)Math.Abs(alpha - 1);
                    tex.DefaultTexture.RGBA = texcolor;
                    part.UpdateTexture(tex);
                    return;
                }
                return;
            }
            else if (linknumber == -1)
            {
                int num = m_host.ParentGroup.PrimCount;
                for (int w = 0; w < num; w++)
                {
                    linknumber = w;
                    part = m_host.ParentGroup.GetLinkNumPart(linknumber);
                    LLObject.TextureEntry tex = part.Shape.Textures;
                    LLColor texcolor;
                    if (face > -1)
                    {
                        texcolor = tex.CreateFace((uint)face).RGBA;
                        texcolor.A = (float)Math.Abs(alpha - 1);
                        tex.FaceTextures[face].RGBA = texcolor;
                        part.UpdateTexture(tex);
                    }
                    else if (face == -1)
                    {
                        texcolor = tex.DefaultTexture.RGBA;
                        texcolor.A = (float)Math.Abs(alpha - 1);
                        tex.DefaultTexture.RGBA = texcolor;
                        for (uint i = 0; i < 32; i++)
                        {
                            if (tex.FaceTextures[i] != null)
                            {
                                texcolor = tex.FaceTextures[i].RGBA;
                                texcolor.A = (float)Math.Abs(alpha - 1);
                                tex.FaceTextures[i].RGBA = texcolor;
                            }
                        }
                        texcolor = tex.DefaultTexture.RGBA;
                        texcolor.A = (float)Math.Abs(alpha - 1);
                        tex.DefaultTexture.RGBA = texcolor;
                        part.UpdateTexture(tex);
                    }
                }
                return;
            }
            else
            {
                NotImplemented("llSetLinkAlpha");
            }
        }

        public int llGetNumberOfPrims()
        {
            m_host.AddScriptLPS(1);
            return m_host.ParentGroup.PrimCount;
        }

        public string llGetNumberOfNotecardLines(string name)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetNumberOfNotecardLines");
            return String.Empty;
        }

        public LSL_Types.list llGetBoundingBox(string obj)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetBoundingBox");
            return new LSL_Types.list();
        }

        public LSL_Types.Vector3 llGetGeometricCenter()
        {
            return new LSL_Types.Vector3(m_host.GetGeometricCenter().X, m_host.GetGeometricCenter().Y, m_host.GetGeometricCenter().Z);
        }

        public void llGetPrimitiveParams()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetPrimitiveParams");
        }

        //  <remarks>
        //  <para>
        //  The .NET definition of base 64 is:
        //  <list>
        //  <item>
        //  Significant: A-Z a-z 0-9 + -
        //  </item>
        //  <item>
        //  Whitespace: \t \n \r ' '
        //  </item>
        //  <item>
        //  Valueless: =
        //  </item>
        //  <item>
        //  End-of-string: \0 or '=='
        //  </item>
        //  </list>
        //  </para>
        //  <para>
        //  Each point in a base-64 string represents
        //  a 6 bit value. A 32-bit integer can be 
        //  represented using 6 characters (with some
        //  redundancy). 
        //  </para>
        //  <para>
        //  LSL requires a base64 string to be 8 
        //  characters in length. LSL also uses '/'
        //  rather than '-' (MIME compliant).
        //  </para>
        //  <para>
        //  RFC 1341 used as a reference (as specified
        //  by the SecondLife Wiki).
        //  </para>
        //  <para>
        //  SL do not record any kind of exception for
        //  these functions, so the string to integer
        //  conversion returns '0' if an invalid 
        //  character is encountered during conversion.
        //  </para>
        //  <para>
        //  References
        //  <list>
        //  <item>
        //  http://lslwiki.net/lslwiki/wakka.php?wakka=Base64
        //  </item>
        //  <item>
        //  </item>
        //  </list>
        //  </para>
        //  </remarks>
      
        //  <summary>
        //  Table for converting 6-bit integers into
        //  base-64 characters
        //  </summary>

        private static readonly char[] i2ctable = 
        {
            'A','B','C','D','E','F','G','H',
            'I','J','K','L','M','N','O','P',
            'Q','R','S','T','U','V','W','X',
            'Y','Z',
            'a','b','c','d','e','f','g','h',
            'i','j','k','l','m','n','o','p',
            'q','r','s','t','u','v','w','x',
            'y','z',
            '0','1','2','3','4','5','6','7',
            '8','9',
            '+','/'
        };

        //  <summary>
        //  Table for converting base-64 characters
        //  into 6-bit integers.
        //  </summary>

        private static readonly int[] c2itable =
        {
            -1,-1,-1,-1,-1,-1,-1,-1,    // 0x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 1x    
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 2x
            -1,-1,-1,63,-1,-1,-1,64,
            53,54,55,56,57,58,59,60,    // 3x
            61,62,-1,-1,-1,0,-1,-1,
            -1,1,2,3,4,5,6,7,           // 4x
            8,9,10,11,12,13,14,15,
            16,17,18,19,20,21,22,23,    // 5x
            24,25,26,-1,-1,-1,-1,-1,
            -1,27,28,29,30,31,32,33,    // 6x
            34,35,36,37,38,39,40,41,
            42,43,44,45,46,47,48,49,    // 7x
            50,51,52,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 8x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 9x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Ax
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Bx
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Cx
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Dx
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Ex
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Fx
            -1,-1,-1,-1,-1,-1,-1,-1
        };

        //  <summary>
        //  Converts a 32-bit integer into a Base64
        //  character string. Base64 character strings
        //  are always 8 characters long. All iinteger
        //  values are acceptable.
        //  </summary>
        //  <param name="number">
        //  32-bit integer to be converted.
        //  </param>
        //  <returns>
        //  8 character string. The 1st six characters
        //  contain the encoded number, the last two
        //  characters are padded with "=".
        //  </returns>

        public string llIntegerToBase64(int number)
        {

            // uninitialized string

            char[] imdt = new char[8];

            m_host.AddScriptLPS(1);

            // Manually unroll the loop

            imdt[7] = '=';
            imdt[6] = '=';
            imdt[5] = i2ctable[number<<4  & 0x3F];
            imdt[4] = i2ctable[number>>2  & 0x3F];
            imdt[3] = i2ctable[number>>8  & 0x3F];
            imdt[2] = i2ctable[number>>14 & 0x3F];
            imdt[1] = i2ctable[number>>20 & 0x3F];
            imdt[0] = i2ctable[number>>26 & 0x3F];

            return new string(imdt);

        }

        //  <summary>
        //  Converts an eight character base-64 string
        //  into a 32-bit integer.
        //  </summary>
        //  <param name="str">
        //  8 characters string to be converted. Other
        //  length strings return zero.
        //  </param>
        //  <returns>
        //  Returns an integer representing the 
        //  encoded value providedint he 1st 6 
        //  characters of the string.
        //  </returns>
        //  <remarks>
        //  This is coded to behave like LSL's
        //  implementation (I think), based upon the
        //  information available at the Wiki.
        //  If more than 8 characters are supplied, 
        //  zero is returned.
        //  If a NULL string is supplied, zero will
        //  be returned.
        //  If fewer than 6 characters are supplied, then
        //  the answer will reflect a partial 
        //  accumulation.
        //  <para>
        //  The 6-bit segments are 
        //  extracted left-to-right in big-endian mode, 
        //  which means that segment 6 only contains the 
        //  two low-order bits of the 32 bit integer as
        //  its high order 2 bits. A short string therefore
        //  means loss of low-order information. E.g.
        //
        //  |<---------------------- 32-bit integer ----------------------->|<-Pad->|
        //  |<--Byte 0----->|<--Byte 1----->|<--Byte 2----->|<--Byte 3----->|<-Pad->|
        //  |3|3|2|2|2|2|2|2|2|2|2|2|1|1|1|1|1|1|1|1|1|1| | | | | | | | | | |P|P|P|P|
        //  |1|0|9|8|7|6|5|4|3|2|1|0|9|8|7|6|5|4|3|2|1|0|9|8|7|6|5|4|3|2|1|0|P|P|P|P|
        //  |  str[0]   |  str[1]   |  str[2]   |  str[3]   |  str[4]   |  str[6]   |
        //
        //  </para>
        //  </remarks>

        public int llBase64ToInteger(string str)
        {

            int number = 0;
            int digit;

            m_host.AddScriptLPS(1);

            //    Require a well-fromed base64 string

            if(str.Length > 8)
                return 0;

            //    The loop is unrolled in the interests
            //    of performance and simple necessity.
            //
            //    MUST find 6 digits to be well formed
            //      -1 == invalid
            //       0 == padding

            if((digit=c2itable[str[0]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<26;
 
            if((digit=c2itable[str[1]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<20;
 
            if((digit=c2itable[str[2]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<14;
 
            if((digit=c2itable[str[3]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<8;
 
            if((digit=c2itable[str[4]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<2;
 
            if((digit=c2itable[str[5]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit>>4;
 
            // ignore trailing padding
 
            return number;

        }

        public double llGetGMTclock()
        {
            m_host.AddScriptLPS(1);
            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }

        public string llGetSimulatorHostname()
        {
            m_host.AddScriptLPS(1);
            return System.Environment.MachineName;
        }

        public void llSetLocalRot(LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);
            m_host.RotationOffset = new LLQuaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s);
        }

        //  <summary>
        //  Scan the string supplied in 'src' and
        //  tokenize it based upon two sets of 
        //  tokenizers provided in two lists, 
        //  separators and spacers.
        //  </summary>
        //
        //  <remarks>
        //  Separators demarcate tokens and are
        //  elided as they are encountered. Spacers
        //  also demarcate tokens, but are themselves
        //  retained as tokens.
        //
        //  Both separators and spacers may be arbitrarily
        //  long strings. i.e. ":::".
        //
        //  The function returns an ordered list 
        //  representing the tokens found in the supplied
        //  sources string. If two successive tokenizers
        //  are encountered, then a NULL entry is added
        //  to the list.
        //
        //  It is a precondition that the source and 
        //  toekizer lisst are non-null. If they are null,
        //  then a null pointer exception will be thrown 
        //  while their lengths are being determined.
        //
        //  A small amount of working memoryis required
        //  of approximately 8*#tokenizers.
        //
        //  There are many ways in which this function 
        //  can be implemented, this implementation is 
        //  fairly naive and assumes that when the
        //  function is invooked with a short source
        //  string and/or short lists of tokenizers, then
        //  performance will not be an issue.
        //
        //  In order to minimize the perofrmance 
        //  effects of long strings, or large numbers
        //  of tokeizers, the function skips as far as
        //  possible whenever a toekenizer is found, 
        //  and eliminates redundant tokenizers as soon
        //  as is possible.
        //
        //  The implementation tries to avoid any copying
        //  of arrays or other objects.
        //  </remarks>
    
        public LSL_Types.list llParseStringKeepNulls(string src, LSL_Types.list separators, LSL_Types.list spacers)
        {

            int         beginning = 0;
            int         srclen    = src.Length;
            int         seplen    = separators.Length;
            object[]    separray  = separators.Data;
            int         spclen    = spacers.Length;
            object[]    spcarray  = spacers.Data;
            int         mlen      = seplen+spclen;

            int[]       offset    = new int[mlen+1];
            bool[]      active    = new bool[mlen];

            int         best;
            int         j;

            //    Initial capacity reduces resize cost

            LSL_Types.list tokens = new LSL_Types.list();

            m_host.AddScriptLPS(1);

            //    All entries are initially valid

            for(int i=0; i<mlen; i++) active[i] = true;

            offset[mlen] = srclen;
            
            while(beginning<srclen)
            {

                best = mlen;    // as bad as it gets

                //    Scan for separators

                for(j=0; j<seplen; j++)
                {    
                    if(active[j])
                    {
                        // scan all of the markers
                        if((offset[j] = src.IndexOf((string)separray[j],beginning)) == -1)
                        { 
                            // not present at all
                            active[j] = false;
                        } else
                        {
                            // present and correct
                            if(offset[j] < offset[best])
                            {    
                                // closest so far
                                best = j;
                                if(offset[best] == beginning)
                                    break;
                            }
                        }
                    }
                }

                //    Scan for spacers

                if(offset[best] != beginning)
                {
                    for(j=seplen; (j<mlen) && (offset[best] > beginning); j++)
                    {    
                        if(active[j])
                        {
                            // scan all of the markers
                            if((offset[j] = src.IndexOf((string)spcarray[j-seplen],beginning)) == -1)
                            { 
                                // not present at all
                                active[j] = false;
                            } else
                            {
                                // present and correct
                                if(offset[j] < offset[best])
                                {    
                                    // closest so far
                                    best = j;
                                }
                            }
                        }
                    }
                }

                //    This is the normal exit from the scanning loop

                if(best == mlen)
                {    
                    // no markers were found on this pass
                    // so we're pretty much done
                    tokens.Add(src.Substring(beginning, srclen-beginning));
                    break;
                }

                //    Otherwise we just add the newly delimited token
                //    and recalculate where the search should continue.

                tokens.Add(src.Substring(beginning,offset[best]-beginning));

                if(best<seplen)
                {
                    beginning = offset[best]+((string)separray[best]).Length;
                } else
                {
                    beginning = offset[best]+((string)spcarray[best-seplen]).Length;
                    tokens.Add(spcarray[best-seplen]);
                }

            }

            //    This an awkward an not very intuitive boundary case. If the
            //    last substring is a tokenizer, then there is an implied trailing
            //    null list entry. Hopefully the single comparison will not be too
            //    arduous. Alternatively the 'break' could be replced with a return
            //    but that's shabby programming.

            if(beginning == srclen)
            {
                if(srclen != 0)
                    tokens.Add("");
            }

            return tokens;

        }

        public void llRezAtRoot(string inventory, LSL_Types.Vector3 position, LSL_Types.Vector3 velocity,
                                LSL_Types.Quaternion rot, int param)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRezAtRoot");
        }

        public int llGetObjectPermMask(int mask)
        {
            m_host.AddScriptLPS(1);

            int permmask = 0;

            if (mask == BuiltIn_Commands_BaseClass.MASK_BASE)//0
            {
                permmask = (int)m_host.BaseMask;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_OWNER)//1
            {
                permmask = (int)m_host.OwnerMask;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_GROUP)//2
            {
                permmask = (int)m_host.GroupMask;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_EVERYONE)//3
            {
                permmask = (int)m_host.EveryoneMask;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_NEXT)//4
            {
                permmask = (int)m_host.NextOwnerMask;
            }

            return permmask;
        }

        public void llSetObjectPermMask(int mask, int value)
        {
            m_host.AddScriptLPS(1);

            if (mask == BuiltIn_Commands_BaseClass.MASK_BASE)//0
            {
                m_host.BaseMask = (uint)value;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_OWNER)//1
            {
                m_host.OwnerMask = (uint)value;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_GROUP)//2
            {
                m_host.GroupMask = (uint)value;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_EVERYONE)//3
            {
                m_host.EveryoneMask = (uint)value;
            }

            else if (mask == BuiltIn_Commands_BaseClass.MASK_NEXT)//4
            {
                m_host.NextOwnerMask = (uint)value;
            }
        }

        public int llGetInventoryPermMask(string item, int mask)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<LLUUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == item)
                {
                    switch (mask)
                    {
                        case 0:
                            return (int)inv.Value.BaseMask;
                        case 1:
                            return (int)inv.Value.OwnerMask;
                        case 2:
                            return (int)inv.Value.GroupMask;
                        case 3:
                            return (int)inv.Value.EveryoneMask;
                        case 4:
                            return (int)inv.Value.NextOwnerMask;
                    }
                }
            }
            return -1;
        }

        public void llSetInventoryPermMask(string item, int mask, int value)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetInventoryPermMask");
        }

        public string llGetInventoryCreator(string item)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<LLUUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == item)
                {
                    return inv.Value.CreatorID.ToString();
                }
            }
            llSay(0, "No item name '" + item + "'");
            return String.Empty;
        }

        public void llOwnerSay(string msg)
        {
            //m_host.AddScriptLPS(1); // since we reuse llInstantMessage
            //temp fix so that lsl wiki examples aren't annoying to use to test other functions
            //should be similar to : llInstantMessage(llGetOwner(),msg)
            // llGetOwner ==> m_host.ObjectOwner.ToString()
            llInstantMessage(m_host.ObjectOwner.ToString(),msg);
            
            //World.SimChat(Helpers.StringToField(msg), ChatTypeEnum.Say, 0, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
            //IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            //wComm.DeliverMessage(m_host.UUID.ToString(), ChatTypeEnum.Say, 0, m_host.Name, msg);
        }

        public void llRequestSimulatorData(string simulator, int data)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRequestSimulatorData");
        }

        public void llForceMouselook(int mouselook)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llForceMouselook");
        }

        public double llGetObjectMass(string id)
        {
            m_host.AddScriptLPS(1);
            LLUUID key = new LLUUID();
            if (LLUUID.TryParse(id,out key))
            {
                return (double)World.GetSceneObjectPart(World.Entities[key].LocalId).GetMass();
            }
            return 0;
        }

        /// <summary>
        /// illListReplaceList removes the sub-list defined by the inclusive indices
        /// start and end and inserts the src list in its place. The inclusive 
        /// nature of the indices means that at least one element must be deleted
        /// if the indices are within the bounds of the existing list. I.e. 2,2
        /// will remove the element at index 2 and replace it with the source
        /// list. Both indices may be negative, with the usual interpretation. An
        /// interesting case is where end is lower than start. As these indices 
        /// bound the list to be removed, then 0->end, and start->lim are removed
        /// and the source list is added as a suffix.
        /// </summary>

        public LSL_Types.list llListReplaceList(LSL_Types.list dest, LSL_Types.list src, int start, int end)
        {
      
            LSL_Types.list pref = null;

            m_host.AddScriptLPS(1);

            // Note that although we have normalized, both
            // indices could still be negative.
            if(start < 0)
            {
                start = start+dest.Length;
            }

            if(end < 0)
            {
                end = end+dest.Length;
            }
            // The comventional case, remove a sequence starting with
            // start and ending with end. And then insert the source
            // list.
            if(start <= end)
            {
                // If greater than zero, then there is going to be a 
                // surviving prefix. Otherwise the inclusive nature 
                // of the indices mean that we're going to add the 
                // source list as a prefix.
                if(start > 0)
                {
                    pref = dest.GetSublist(0,start-1);
                    // Only add a suffix if there is something
                    // beyond the end index (it's inclusive too).
                    if(end+1 < dest.Length)
                    {
                        return pref + src + dest.GetSublist(end+1,-1);
                    }
                    else
                    {
                        return pref + src;
                    }
                }
                // If start is less than or equal to zero, then
                // the new list is simply a prefix. We still need to
                // figure out any necessary surgery to the destination
                // based upon end. Note that if end exceeds the upper
                // bound in this case, the entire destination list
                // is removed.
                else
                {
                    if(end+1 < dest.Length)
                    {
                        return src + dest.GetSublist(end+1,-1);
                    }
                    else
                    {
                        return src;
                    }
                }
            }
            // Finally, if start > end, we strip away a prefix and
            // a suffix, to leave the list that sits <between> ens
            // and start, and then tag on the src list. AT least 
            // that's my interpretation. We can get sublist to do
            // this for us. Note that one, or both of the indices
            // might have been negative.
            else
            {
                return dest.GetSublist(end+1,start-1)+src;
            }
        }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            m_host.AddScriptLPS(1);
            LLUUID avatarId = new LLUUID(avatar_id);
            m_ScriptEngine.World.SendUrlToUser(avatarId, m_host.Name, m_host.UUID, m_host.ObjectOwner, false, message,
                                               url);
        }

        public void llParcelMediaCommandList(LSL_Types.list commandList)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llParcelMediaCommandList");
        }

        public void llParcelMediaQuery()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llParcelMediaQuery");
        }

        public int llModPow(int a, int b, int c)
        {
            m_host.AddScriptLPS(1);
            Int64 tmp = 0;
            Math.DivRem(Convert.ToInt64(Math.Pow(a, b)), c, out tmp);
            return Convert.ToInt32(tmp);
        }

        public int llGetInventoryType(string name)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<LLUUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == name)
                {
                    return inv.Value.InvType;
                }
            }
            return -1;
        }

        public void llSetPayPrice(int price, LSL_Types.list quick_pay_buttons)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetPayPrice");
        }

        public LSL_Types.Vector3 llGetCameraPos()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetCameraPos");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.Quaternion llGetCameraRot()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetCameraRot");
            return new LSL_Types.Quaternion();
        }

        public void llSetPrimURL()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetPrimURL");
        }

        public void llRefreshPrimURL()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRefreshPrimURL");
        }

        public string llEscapeURL(string url)
        {
            m_host.AddScriptLPS(1);
            try
            {
                return Uri.EscapeUriString(url);
            }
            catch (Exception ex)
            {
                return "llEscapeURL: " + ex.ToString();
            }
        }

        public string llUnescapeURL(string url)
        {
            m_host.AddScriptLPS(1);
            try
            {
                return Uri.UnescapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llUnescapeURL: " + ex.ToString();
            }
        }

        public void llMapDestination(string simname, LSL_Types.Vector3 pos, LSL_Types.Vector3 look_at)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMapDestination");
        }

        public void llAddToLandBanList(string avatar, double hours)
        {
            m_host.AddScriptLPS(1);
            LLUUID key;
            LandData land = World.LandChannel.getLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.ownerID == m_host.OwnerID)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                if (LLUUID.TryParse(avatar, out key))
                {
                    entry.AgentID = key;
                    entry.Flags = ParcelManager.AccessList.Ban;
                    entry.Time = DateTime.Now.AddHours(hours);
                    land.parcelAccessList.Add(entry);
                }
            }
        }

        public void llRemoveFromLandPassList(string avatar)
        {
            m_host.AddScriptLPS(1);
            LLUUID key;
            LandData land = World.LandChannel.getLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.ownerID == m_host.OwnerID)
            {
                if (LLUUID.TryParse(avatar, out key))
                {
                    foreach (ParcelManager.ParcelAccessEntry entry in land.parcelAccessList)
                    {
                        if (entry.AgentID == key && entry.Flags == ParcelManager.AccessList.Access)
                        {
                            land.parcelAccessList.Remove(entry);
                            break;
                        }
                    }
                }
            }
        }

        public void llRemoveFromLandBanList(string avatar)
        {
            m_host.AddScriptLPS(1);
            LLUUID key;
            LandData land = World.LandChannel.getLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.ownerID == m_host.OwnerID)
            {
                if (LLUUID.TryParse(avatar, out key))
                {
                    foreach (ParcelManager.ParcelAccessEntry entry in land.parcelAccessList)
                    {
                        if (entry.AgentID == key && entry.Flags == ParcelManager.AccessList.Ban)
                        {
                            land.parcelAccessList.Remove(entry);
                            break;
                        }
                    }
                }
            }
        }

        public void llSetCameraParams(LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetCameraParams");
        }

        public void llClearCameraParams()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llClearCameraParams");
        }

        public double llListStatistics(int operation, LSL_Types.list src)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list nums = LSL_Types.list.ToDoubleList(src);
            switch (operation)
            {
                case BuiltIn_Commands_BaseClass.LIST_STAT_RANGE:
                    return nums.Range();
                case BuiltIn_Commands_BaseClass.LIST_STAT_MIN:
                    return nums.Min();
                case BuiltIn_Commands_BaseClass.LIST_STAT_MAX:
                    return nums.Max();
                case BuiltIn_Commands_BaseClass.LIST_STAT_MEAN:
                    return nums.Mean();
                case BuiltIn_Commands_BaseClass.LIST_STAT_MEDIAN:
                    return nums.Median();
                case BuiltIn_Commands_BaseClass.LIST_STAT_NUM_COUNT:
                    return nums.NumericLength();
                case BuiltIn_Commands_BaseClass.LIST_STAT_STD_DEV:
                    return nums.StdDev();
                case BuiltIn_Commands_BaseClass.LIST_STAT_SUM:
                    return nums.Sum();
                case BuiltIn_Commands_BaseClass.LIST_STAT_SUM_SQUARES:
                    return nums.SumSqrs();
                case BuiltIn_Commands_BaseClass.LIST_STAT_GEOMETRIC_MEAN:
                    return nums.GeometricMean();
                case BuiltIn_Commands_BaseClass.LIST_STAT_HARMONIC_MEAN:
                    return nums.HarmonicMean();
                default:
                    return 0.0;
            }
        }

        public int llGetUnixTime()
        {
            m_host.AddScriptLPS(1);
            return Util.UnixTimeSinceEpoch();
        }

        public int llGetParcelFlags(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            return (int)World.LandChannel.getLandObject((float)pos.x, (float)pos.y).landData.landFlags;
        }

        public int llGetRegionFlags()
        {
            m_host.AddScriptLPS(1);
            return (int)World.RegionInfo.EstateSettings.regionFlags;
        }

        public string llXorBase64StringsCorrect(string str1, string str2)
        {
            m_host.AddScriptLPS(1);
            string ret = String.Empty;
            string src1 = llBase64ToString(str1);
            string src2 = llBase64ToString(str2);
            int c = 0;
            for (int i = 0; i < src1.Length; i++)
            {
                ret += src1[i] ^ src2[c];

                c++;
                if (c > src2.Length)
                    c = 0;
            }
            return llStringToBase64(ret);
        }

        public string llHTTPRequest(string url, LSL_Types.list parameters, string body)
        {
            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            // parameter flags support are implemented in ScriptsHttpRequests.cs
            //   in StartHttpRequest

            m_host.AddScriptLPS(1);
            IHttpRequests httpScriptMod =
                m_ScriptEngine.World.RequestModuleInterface<IHttpRequests>();
            List<string> param = new List<string>();
            foreach (object o in parameters.Data)
            {
                param.Add(o.ToString());
            }
            LLUUID reqID = httpScriptMod.
                StartHttpRequest(m_localID, m_itemID, url, param, body);

            if (reqID != LLUUID.Zero)
                return reqID.ToString();
            else
                return null;
        }

        public void llResetLandBanList()
        {
            m_host.AddScriptLPS(1);
            LandData land = World.LandChannel.getLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.ownerID == m_host.OwnerID)
            {
                foreach (ParcelManager.ParcelAccessEntry entry in land.parcelAccessList)
                {
                    if (entry.Flags == ParcelManager.AccessList.Ban)
                    {
                        land.parcelAccessList.Remove(entry);
                    }
                }
            }
        }

        public void llResetLandPassList()
        {
            m_host.AddScriptLPS(1);
            LandData land = World.LandChannel.getLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.ownerID == m_host.OwnerID)
            {
                foreach (ParcelManager.ParcelAccessEntry entry in land.parcelAccessList)
                {
                    if (entry.Flags == ParcelManager.AccessList.Access)
                    {
                        land.parcelAccessList.Remove(entry);
                    }
                }
            }
        }

        public int llGetParcelPrimCount(LSL_Types.Vector3 pos, int category, int sim_wide)
        {
            m_host.AddScriptLPS(1);

            LandData land = World.GetLandData((float)pos.x, (float)pos.y);

            if(land == null)
            {
                return 0;
            }

            else
            {
                if(sim_wide == 1)
                {
                    if (category == 0)
                    {
                        return land.simwidePrims;
                    }

                    else
                    {
                        //public int simwideArea = 0;
                        return 0;
                    }
                }

                else
                {
                    if(category == 0)//Total Prims
                    {
                        return 0;//land.
                    }

                    else if(category == 1)//Owner Prims
                    {
                        return land.ownerPrims;
                    }

                    else if(category == 2)//Group Prims
                    {
                        return land.groupPrims;
                    }

                    else if(category == 3)//Other Prims
                    {
                        return land.otherPrims;
                    }

                    else if(category == 4)//Selected
                    {
                        return land.selectedPrims;
                    }

                    else if(category == 5)//Temp
                    {
                        return 0;//land.
                    }
                }
            }
            return 0;
        }

        public LSL_Types.list llGetParcelPrimOwners(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            LandObject land = (LandObject)World.LandChannel.getLandObject((float)pos.x, (float)pos.y);
            LSL_Types.list ret = new LSL_Types.list();
            if (land != null)
            {
                foreach (KeyValuePair<LLUUID, int> d in land.getLandObjectOwners())
                {
                    ret.Add(d.Key.ToString());
                    ret.Add(d.Value);
                }
            }
            return ret;
        }

        public int llGetObjectPrimCount(string object_id)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = World.GetSceneObjectPart(new LLUUID(object_id));
            if (part == null)
            {
                return 0;
            }
            else
            {
                return part.ParentGroup.Children.Count;
            }
        }

        public int llGetParcelMaxPrims(LSL_Types.Vector3 pos, int sim_wide)
        {
            m_host.AddScriptLPS(1);
            // Alondria: This currently just is utilizing the normal grid's 0.22 prims/m2 calculation
            // Which probably will be irrelevent in OpenSim....
            LandData land = World.GetLandData((float)pos.x, (float)pos.y);

            float bonusfactor = World.RegionInfo.EstateSettings.objectBonusFactor;

            if (land == null)
            {
                return 0;
            }

            if (sim_wide == 1)
            {
                decimal v = land.simwideArea * (decimal)(0.22) * (decimal)bonusfactor;

                return (int)v;
            }

            else
            {
                decimal v = land.area * (decimal)(0.22) * (decimal)bonusfactor;

                return (int)v;
            }

        }

        public LSL_Types.list llGetParcelDetails(LSL_Types.Vector3 pos, LSL_Types.list param)
        {
            m_host.AddScriptLPS(1);
            LandData land = World.GetLandData((float)pos.x, (float)pos.y);
            if (land == null)
            {
                return new LSL_Types.list(0);
            }
            LSL_Types.list ret = new LSL_Types.list();
            foreach (object o in param.Data)
            {
                switch (o.ToString())
                {
                    case "0":
                        ret = ret + new LSL_Types.list(land.landName);
                        break;
                    case "1":
                        ret = ret + new LSL_Types.list(land.landDesc);
                        break;
                    case "2":
                        ret = ret + new LSL_Types.list(land.ownerID.ToString());
                        break;
                    case "3":
                        ret = ret + new LSL_Types.list(land.groupID.ToString());
                        break;
                    case "4":
                        ret = ret + new LSL_Types.list(land.area);
                        break;
                    default:
                        ret = ret + new LSL_Types.list(0);
                        break;
                }
            }
            return ret;
        }

        public string llStringTrim(string src, int type)
        {
            m_host.AddScriptLPS(1);
            if (type == (int)BuiltIn_Commands_BaseClass.STRING_TRIM_HEAD) { return src.TrimStart(); }
            if (type == (int)BuiltIn_Commands_BaseClass.STRING_TRIM_TAIL) { return src.TrimEnd(); }
            if (type == (int)BuiltIn_Commands_BaseClass.STRING_TRIM) { return src.Trim(); }
            return src;
        }

        public LSL_Types.list llGetObjectDetails(string id, LSL_Types.list args)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list ret = new LSL_Types.list();
            LLUUID key = new LLUUID();
            if (LLUUID.TryParse(id, out key))
            {
                if (World.m_innerScene.ScenePresences.ContainsKey(key))
                {
                    ScenePresence av = World.m_innerScene.ScenePresences[key];
                    foreach(object o in args.Data)
                    {
                        switch(o.ToString())
                        {
                            case "1":
                                ret.Add(av.Firstname + " " + av.Lastname);
                                break;
                            case "2":
                                ret.Add("");
                                break;
                            case "3":
                                ret.Add(new LSL_Types.Vector3((double)av.AbsolutePosition.X, (double)av.AbsolutePosition.Y, (double)av.AbsolutePosition.Z));
                                break;
                            case "4":
                                ret.Add(new LSL_Types.Quaternion((double)av.Rotation.x, (double)av.Rotation.y, (double)av.Rotation.z, (double)av.Rotation.w));
                                break;
                            case "5": 
                                ret.Add(new LSL_Types.Vector3(av.Velocity.X,av.Velocity.Y,av.Velocity.Z));
                                break;
                            case "6":
                                ret.Add(id);
                                break;
                            case "7":
                                ret.Add(LLUUID.Zero.ToString());
                                break;
                            case "8":
                                ret.Add(LLUUID.Zero.ToString());
                                break;
                        }
                    }
                    return ret;
                }
                SceneObjectPart obj = World.GetSceneObjectPart(key);
                if (obj != null)
                {
                    foreach(object o in args.Data)
                    {
                        switch(o.ToString())
                        {
                            case "1":
                                ret.Add(obj.Name);
                                break;
                            case "2":
                                ret.Add(obj.Description);
                                break;
                            case "3":
                                ret.Add(new LSL_Types.Vector3(obj.AbsolutePosition.X,obj.AbsolutePosition.Y,obj.AbsolutePosition.Z));
                                break;
                            case "4":
                                ret.Add(new LSL_Types.Quaternion(obj.RotationOffset.X, obj.RotationOffset.Y, obj.RotationOffset.Z, obj.RotationOffset.W));
                                break;
                            case "5":
                                ret.Add(new LSL_Types.Vector3(obj.Velocity.X, obj.Velocity.Y, obj.Velocity.Z));
                                break;
                            case "6":
                                ret.Add(obj.OwnerID.ToString());
                                break;
                            case "7":
                                ret.Add(obj.GroupID.ToString());
                                break;
                            case "8":
                                ret.Add(obj.CreatorID.ToString());
                                break;
                        }
                    }
                    return ret;
                }
            }
            return new LSL_Types.list();
        }


        internal LLUUID ScriptByName(string name)
        {
            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 10 && item.Name == name)
                    return item.ItemID;
            }
            return LLUUID.Zero;
        }

        internal void ShoutError(string msg)
        {
            llShout(BuiltIn_Commands_BaseClass.DEBUG_CHANNEL, msg);
        }



        internal void NotImplemented(string command)
        {
            if (throwErrorOnNotImplemented)
                throw new NotImplementedException("Command not implemented: " + command);
        }

        internal void Deprecated(string command)
        {
            throw new Exception("Command deprecated: " + command);
        }

        internal void LSLError(string msg)
        {
            throw new Exception("LSL Runtime Error: " + msg);
        }
    }
}
