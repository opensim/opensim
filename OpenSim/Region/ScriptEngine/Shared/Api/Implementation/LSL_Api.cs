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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Avatar.Currency.SampleMoney;
using OpenSim.Region.Environment.Modules.World.Land;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_Api : MarshalByRefObject, ILSL_Api, IScriptApi
    {
        // private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal IScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool throwErrorOnNotImplemented = true;
        internal AsyncCommandManager AsyncCommands = null;
        internal float m_ScriptDelayFactor = 1.0f;
        internal float m_ScriptDistanceFactor = 1.0f;

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["XEngine"] == null)
                config.AddConfig("XEngine");

            m_ScriptDelayFactor = config.Configs["XEngine"].
                    GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor = config.Configs["XEngine"].
                    GetFloat("ScriptDistanceLimitFactor", 1.0f);

            AsyncCommands = (AsyncCommandManager)ScriptEngine.AsyncCommands;
        }

        private void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        private DateTime m_timer = DateTime.Now;
        private bool m_waitingForScriptAnswer=false;


        // Object never expires
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero;
            }
            return lease;
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        public void state(string newState)
        {
            m_ScriptEngine.SetState(m_itemID, newState);
            throw new EventAbortException();
        }

        // Extension commands use this:
        public ICommander GetCommander(string name)
        {
            return World.GetCommander(name);
        }

        private UUID InventorySelf()
        {
            UUID invItemID = new UUID();

            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == 10 && inv.Value.ItemID == m_itemID)
                {
                    invItemID = inv.Key;
                    break;
                }
            }

            return invItemID;
        }

        private UUID InventoryKey(string name, int type)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == name)
                {
                    if (inv.Value.Type != type)
                        return UUID.Zero;

                    return inv.Value.AssetID.ToString();
                }
            }
            return UUID.Zero;
        }

        private UUID InventoryKey(string name)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == name)
                {
                    return inv.Value.AssetID.ToString();
                }
            }
            return UUID.Zero;
        }


        /// <summary>
        /// accepts a valid UUID, -or- a name of an inventory item.
        /// Returns a valid UUID or UUID.Zero if key invalid and item not found
        /// in prim inventory.
        /// </summary>
        /// <param name="k"></param>
        /// <returns></returns>
        private UUID KeyOrName(string k)
        {
            UUID key = UUID.Zero;

            // if we can parse the string as a key, use it.
            if (UUID.TryParse(k, out key))
            {
                return key;
            }
            // else try to locate the name in inventory of object. found returns key,
            // not found returns UUID.Zero which will translate to the default particle texture
            else
            {
                return InventoryKey(k);
            }
        }

        public void osSetRegionWaterHeight(double height)
        {
            m_host.AddScriptLPS(1);
            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.ExternalChecks.ExternalChecksCanBeGodLike(m_host.OwnerID))
            {
                World.EventManager.TriggerRequestChangeWaterHeight((float)height);
            }
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

        public LSL_Types.LSLInteger llAbs(int i)
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

        public LSL_Types.LSLInteger llFloor(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Floor(f);
        }

        public LSL_Types.LSLInteger llCeil(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Ceiling(f);
        }

        // Xantor 01/May/2008 fixed midpointrounding (2.5 becomes 3.0 instead of 2.0, default = ToEven)
        public LSL_Types.LSLInteger llRound(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Round(f, MidpointRounding.AwayFromZero);
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

        // Utility function for llRot2Euler

        // normalize an angle between 0 - 2*PI (0 and 360 degrees)
        private double NormalizeAngle(double angle)
        {
            angle = angle % (Math.PI * 2);
            if (angle < 0) angle = angle + Math.PI * 2;
            return angle;
        }

        // Old implementation of llRot2Euler, now normalized

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
                return new LSL_Types.Vector3(NormalizeAngle(Math.Atan2(2.0 * (r.x * r.s - r.y * r.z), (-t.x - t.y + t.z + t.s))),
                                             NormalizeAngle(Math.Atan2(n, Math.Sqrt(p))),
                                             NormalizeAngle(Math.Atan2(2.0 * (r.z * r.s - r.x * r.y), (t.x - t.y - t.z + t.s))));
            else if (n > 0)
                return new LSL_Types.Vector3(0.0, Math.PI / 2, NormalizeAngle(Math.Atan2((r.z * r.s + r.x * r.y), 0.5 - t.x - t.z)));
            else
                return new LSL_Types.Vector3(0.0, -Math.PI / 2, NormalizeAngle(Math.Atan2((r.z * r.s + r.x * r.y), 0.5 - t.x - t.z)));
        }

        /* From wiki:
        The Euler angle vector (in radians) is converted to a rotation by doing the rotations around the 3 axes
        in Z, Y, X order. So llEuler2Rot(<1.0, 2.0, 3.0> * DEG_TO_RAD) generates a rotation by taking the zero rotation,
        a vector pointing along the X axis, first rotating it 3 degrees around the global Z axis, then rotating the resulting
        vector 2 degrees around the global Y axis, and finally rotating that 1 degree around the global X axis.
        */

        /* How we arrived at this llEuler2Rot
         *
         * Experiment in SL to determine conventions:
         *   llEuler2Rot(<PI,0,0>)=<1,0,0,0>
         *   llEuler2Rot(<0,PI,0>)=<0,1,0,0>
         *   llEuler2Rot(<0,0,PI>)=<0,0,1,0>
         *
         * Important facts about Quaternions
         *  - multiplication is non-commutative (a*b != b*a)
         *  - http://en.wikipedia.org/wiki/Quaternion#Basis_multiplication
         *
         * Above SL experiment gives (c1,c2,c3,s1,s2,s3 as defined in our llEuler2Rot):
         *   Qx = c1+i*s1
         *   Qy = c2+j*s2;
         *   Qz = c3+k*s3;
         *
         * Rotations applied in order (from above) Z, Y, X
         * Q = (Qz * Qy) * Qx
         * ((c1+i*s1)*(c2+j*s2))*(c3+k*s3)
         * (c1*c2+i*s1*c2+j*c1*s2+ij*s1*s2)*(c3+k*s3)
         * (c1*c2+i*s1*c2+j*c1*s2+k*s1*s2)*(c3+k*s3)
         * c1*c2*c3+i*s1*c2*c3+j*c1*s2*c3+k*s1*s2*c3+k*c1*c2*s3+ik*s1*c2*s3+jk*c1*s2*s3+kk*s1*s2*s3
         * c1*c2*c3+i*s1*c2*c3+j*c1*s2*c3+k*s1*s2*c3+k*c1*c2*s3 -j*s1*c2*s3 +i*c1*s2*s3   -s1*s2*s3
         * regroup: x=i*(s1*c2*c3+c1*s2*s3)
         *          y=j*(c1*s2*c3-s1*c2*s3)
         *          z=k*(s1*s2*c3+c1*c2*s3)
         *          s=   c1*c2*c3-s1*s2*s3
         *
         * This implementation agrees with the functions found here:
         * http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions
         * And with the results in SL.
         *
         * It's also possible to calculate llEuler2Rot by direct multiplication of
         * the Qz, Qy, and Qx vectors (as above - and done in the "accurate" function
         * from the wiki).
         * Apparently in some cases this is better from a numerical precision perspective?
         */

        public LSL_Types.Quaternion llEuler2Rot(LSL_Types.Vector3 v)
        {
            m_host.AddScriptLPS(1);

            double x,y,z,s;

            double c1 = Math.Cos(v.x/2.0);
            double c2 = Math.Cos(v.y/2.0);
            double c3 = Math.Cos(v.z/2.0);
            double s1 = Math.Sin(v.x/2.0);
            double s2 = Math.Sin(v.y/2.0);
            double s3 = Math.Sin(v.z/2.0);

            x = s1*c2*c3+c1*s2*s3;
            y = c1*s2*c3-s1*c2*s3;
            z = s1*s2*c3+c1*c2*s3;
            s = c1*c2*c3-s1*s2*s3;

            return new LSL_Types.Quaternion(x, y, z, s);
        }

        public LSL_Types.Quaternion llAxes2Rot(LSL_Types.Vector3 fwd, LSL_Types.Vector3 left, LSL_Types.Vector3 up)
        {
            m_host.AddScriptLPS(1);
            double x, y, z, s;
            int f = 0;
            // Important Note: q1=<x,y,z,s> is equal to q2=<-x,-y,-z,-s>
            // Computing quaternion x,y,z,s values
            x = ((fwd.x - left.y - up.z + 1) / 4);
            x *= x;
            x = Math.Sqrt(Math.Sqrt(x));
            y = ((1 - up.z) / 2 - x * x);
            y *= y;
            y = Math.Sqrt(Math.Sqrt(y));
            z = ((1 - left.y) / 2 - x * x);
            z *= z;
            z = Math.Sqrt(Math.Sqrt(z));
            s = (1 - x * x - y * y - z * z);
            s *= s;
            s = Math.Sqrt(Math.Sqrt(s));

            // Set f for signs detection
            if (fwd.y + left.x >= 0) { f += 1; }
            if (fwd.z + up.x >= 0) { f += 2; }
            if (left.z - up.y >= 0) { f += 4; }
            // Set correct quaternion signs based on f value
            if (f == 0) { x = -x; }
            if (f == 1) { x = -x; y = -y; }
            if (f == 2) { x = -x; z = -z; }
            if (f == 3) { s = -s; }
            if (f == 4) { x = -x; s = -s; }
            if (f == 5) { z = -z; }
            if (f == 6) { y = -y; }

            LSL_Types.Quaternion result = new LSL_Types.Quaternion(x, y, z, s);

            // a hack to correct a few questionable angles :(
            if (llVecDist(llRot2Fwd(result), fwd) > 0.001 || llVecDist(llRot2Left(result), left) > 0.001)
                result.s = -s;

            return result;
        }

        public LSL_Types.Vector3 llRot2Fwd(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);

            double x, y, z, m;

            m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            x = r.x * r.x - r.y * r.y - r.z * r.z + r.s * r.s;
            y = 2 * (r.x * r.y + r.z * r.s);
            z = 2 * (r.x * r.z - r.y * r.s);
            return (new LSL_Types.Vector3(x, y, z));
        }

        public LSL_Types.Vector3 llRot2Left(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);

            double x, y, z, m;

            m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            x = 2 * (r.x * r.y - r.z * r.s);
            y = -r.x * r.x + r.y * r.y - r.z * r.z + r.s * r.s;
            z = 2 * (r.x * r.s + r.y * r.z);
            return (new LSL_Types.Vector3(x, y, z));
        }

        public LSL_Types.Vector3 llRot2Up(LSL_Types.Quaternion r)
        {
            m_host.AddScriptLPS(1);
            double x, y, z, m;

            m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            x = 2 * (r.x * r.z + r.y * r.s);
            y = 2 * (-r.x * r.s + r.y * r.z);
            z = -r.x * r.x - r.y * r.y + r.z * r.z + r.s * r.s;
            return (new LSL_Types.Vector3(x, y, z));
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

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(text),
                          ChatTypeEnum.Whisper, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Whisper, channelID, m_host.Name, m_host.UUID, text);
        }

        public void llSay(int channelID, string text)
        {
            m_host.AddScriptLPS(1);

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(text),
                          ChatTypeEnum.Say, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Say, channelID, m_host.Name, m_host.UUID, text);
        }

        public void llShout(int channelID, string text)
        {
            m_host.AddScriptLPS(1);

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(text),
                          ChatTypeEnum.Shout, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID, true);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Shout, channelID, m_host.Name, m_host.UUID, text);
        }

        public void llRegionSay(int channelID, string text)
        {
            if (channelID == 0)
            {
                LSLError("Cannot use llRegionSay() on channel 0");
                return;
            }

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            m_host.AddScriptLPS(1);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Region, channelID, m_host.Name, m_host.UUID, text);
        }

        public LSL_Types.LSLInteger llListen(int channelID, string name, string ID, string msg)
        {
            m_host.AddScriptLPS(1);
            UUID keyID;
            UUID.TryParse(ID, out keyID);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            return wComm.Listen(m_localID, m_itemID, m_host.UUID, channelID, name, keyID, msg);
        }

        public void llListenControl(int number, int active)
        {
            m_host.AddScriptLPS(1);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.ListenControl(m_itemID, number, active);
        }

        public void llListenRemove(int number)
        {
            m_host.AddScriptLPS(1);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.ListenRemove(m_itemID, number);
        }

        public void llSensor(string name, string id, int type, double range, double arc)
        {
            m_host.AddScriptLPS(1);
            UUID keyID = UUID.Zero;
            UUID.TryParse(id, out keyID);

            AsyncCommands.SensorRepeatPlugin.SenseOnce(m_localID, m_itemID, name, keyID, type, range, arc, m_host);
       }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            m_host.AddScriptLPS(1);
            UUID keyID = UUID.Zero;
            UUID.TryParse(id, out keyID);

            AsyncCommands.SensorRepeatPlugin.SetSenseRepeatEvent(m_localID, m_itemID, name, keyID, type, range, arc, rate, m_host);
        }

        public void llSensorRemove()
        {
            m_host.AddScriptLPS(1);
            AsyncCommands.SensorRepeatPlugin.UnSetSenseRepeaterEvents(m_localID, m_itemID);
        }

        public string resolveName(UUID objecUUID)
        {
            // try avatar username surname
            CachedUserInfo profile = World.CommsManager.UserProfileCacheService.GetUserDetails(objecUUID);
            if (profile != null && profile.UserProfile != null)
            {
                string avatarname = profile.UserProfile.FirstName + " " + profile.UserProfile.SurName;
                return avatarname;
            }
            // try an scene object
            SceneObjectPart SOP = World.GetSceneObjectPart(objecUUID);
            if (SOP != null)
            {
                string objectname = SOP.Name;
                return objectname;
            }

            EntityBase SensedObject;
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
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return String.Empty;
            return d.Name;
        }

        public string llDetectedKey(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return String.Empty;
            return d.Key.ToString();
        }

        public string llDetectedOwner(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return String.Empty;
            return d.Owner.ToString();
        }

        public LSL_Types.LSLInteger llDetectedType(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return 0;
            return new LSL_Types.LSLInteger(d.Type);
        }

        public LSL_Types.Vector3 llDetectedPos(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return new LSL_Types.Vector3();
            return d.Position;
        }

        public LSL_Types.Vector3 llDetectedVel(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return new LSL_Types.Vector3();
            return d.Velocity;
        }

        public LSL_Types.Vector3 llDetectedGrab(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams parms = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (parms == null)
                return new LSL_Types.Vector3(0, 0, 0);

            return parms.OffsetPos;
        }

        public LSL_Types.Quaternion llDetectedRot(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return new LSL_Types.Quaternion();
            return d.Rotation;
        }

        public LSL_Types.LSLInteger llDetectedGroup(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams d = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (d == null)
                return new LSL_Types.LSLInteger(0);
            if (m_host.GroupID == d.Group)
                return new LSL_Types.LSLInteger(1);
            return new LSL_Types.LSLInteger(0);
        }

        public LSL_Types.LSLInteger llDetectedLinkNumber(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams parms = m_ScriptEngine.GetDetectParams(m_itemID, number);
            if (parms == null)
                return new LSL_Types.LSLInteger(0);

            return new LSL_Types.LSLInteger(parms.LinkNum);
        }

        public void llDie()
        {
            m_host.AddScriptLPS(1);
            throw new SelfDeleteException();
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
            return 0;
        }

        public LSL_Types.Vector3 llWind(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3();
        }

        public void llSetStatus(int status, int value)
        {
            m_host.AddScriptLPS(1);

            int statusrotationaxis = 0;

            if ((status & ScriptBaseClass.STATUS_PHYSICS) == ScriptBaseClass.STATUS_PHYSICS)
            {
                if (value == 1)
                {
                    SceneObjectGroup group = m_host.ParentGroup;
                    if (group == null)
                        return;
                    bool allow = true;
                    foreach (SceneObjectPart part in group.Children.Values)
                    {
                        if (part.Scale.X > World.m_maxPhys || part.Scale.Y > World.m_maxPhys || part.Scale.Z > World.m_maxPhys)
                        {
                            allow = false;
                            break;
                        }
                    }

                    if (!allow)
                        return;
                    m_host.ScriptSetPhysicsStatus(true);
                }
                else
                    m_host.ScriptSetPhysicsStatus(false);
            }

            if ((status & ScriptBaseClass.STATUS_PHANTOM) == ScriptBaseClass.STATUS_PHANTOM)
            {
                if (value == 1)
                    m_host.ScriptSetPhantomStatus(true);
                else
                    m_host.ScriptSetPhantomStatus(false);
            }

            if ((status & ScriptBaseClass.STATUS_CAST_SHADOWS) == ScriptBaseClass.STATUS_CAST_SHADOWS)
            {
                m_host.AddFlag(PrimFlags.CastShadows);
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_X) == ScriptBaseClass.STATUS_ROTATE_X)
            {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_X;
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_Y) == ScriptBaseClass.STATUS_ROTATE_Y)
            {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_Y;
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_Z) == ScriptBaseClass.STATUS_ROTATE_Z)
            {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_Z;
            }

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB) == ScriptBaseClass.STATUS_BLOCK_GRAB)
            {
                NotImplemented("llSetStatus - STATUS_BLOCK_GRAB");
            }

            if ((status & ScriptBaseClass.STATUS_DIE_AT_EDGE) == ScriptBaseClass.STATUS_DIE_AT_EDGE)
            {
                if (value == 1)
                    m_host.SetDieAtEdge(true);
                else
                    m_host.SetDieAtEdge(false);
            }

            if ((status & ScriptBaseClass.STATUS_RETURN_AT_EDGE) == ScriptBaseClass.STATUS_RETURN_AT_EDGE)
            {
                NotImplemented("llSetStatus - STATUS_RETURN_AT_EDGE");
            }

            if ((status & ScriptBaseClass.STATUS_SANDBOX) == ScriptBaseClass.STATUS_SANDBOX)
            {
                NotImplemented("llSetStatus - STATUS_SANDBOX");
            }

            if (statusrotationaxis != 0)
            {
                m_host.SetAxisRotation(statusrotationaxis, value);
            }
        }

        public LSL_Types.LSLInteger llGetStatus(int status)
        {
            m_host.AddScriptLPS(1);
            // Console.WriteLine(m_host.ToString() + " status is " + m_host.GetEffectiveObjectFlags().ToString());
            switch (status)
            {
                case ScriptBaseClass.STATUS_PHYSICS:
                    if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) == (uint)PrimFlags.Physics)
                    {
                        return 1;
                    }
                    return 0;

                case ScriptBaseClass.STATUS_PHANTOM:
                    if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) == (uint)PrimFlags.Phantom)
                    {
                        return 1;
                    }
                    return 0;

                case ScriptBaseClass.STATUS_CAST_SHADOWS:
                    if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.CastShadows) == (uint)PrimFlags.CastShadows)
                    {
                        return 1;
                    }
                    return 0;

                case ScriptBaseClass.STATUS_BLOCK_GRAB:
                    NotImplemented("llGetStatus - STATUS_BLOCK_GRAB");
                    return 0;

                case ScriptBaseClass.STATUS_DIE_AT_EDGE:
                    if (m_host.GetDieAtEdge())
                        return 1;
                    else
                        return 0;

                case ScriptBaseClass.STATUS_RETURN_AT_EDGE:
                    NotImplemented("llGetStatus - STATUS_RETURN_AT_EDGE");
                    return 0;

                case ScriptBaseClass.STATUS_ROTATE_X:
                    NotImplemented("llGetStatus - STATUS_ROTATE_X");
                    return 0;

                case ScriptBaseClass.STATUS_ROTATE_Y:
                    NotImplemented("llGetStatus - STATUS_ROTATE_Y");
                    return 0;

                case ScriptBaseClass.STATUS_ROTATE_Z:
                    NotImplemented("llGetStatus - STATUS_ROTATE_Z");
                    return 0;

                case ScriptBaseClass.STATUS_SANDBOX:
                    NotImplemented("llGetStatus - STATUS_SANDBOX");
                    return 0;
            }
            return 0;
        }

        public void llSetScale(LSL_Types.Vector3 scale)
        {
            m_host.AddScriptLPS(1);
            SetScale(m_host, scale);
        }

        private void SetScale(SceneObjectPart part, LSL_Types.Vector3 scale)
        {
            // TODO: this needs to trigger a persistance save as well

            if (part == null || part.ParentGroup == null || part.ParentGroup.RootPart == null)
                return;

            if (part.ParentGroup.RootPart.PhysActor != null && part.ParentGroup.RootPart.PhysActor.IsPhysical)
            {
                if (scale.x > World.m_maxPhys)
                    scale.x = World.m_maxPhys;
                if (scale.y > World.m_maxPhys)
                    scale.y = World.m_maxPhys;
                if (scale.z > World.m_maxPhys)
                    scale.z = World.m_maxPhys;
            }
            if (scale.x > World.m_maxNonphys)
                scale.x = World.m_maxNonphys;
            if (scale.y > World.m_maxNonphys)
                scale.y = World.m_maxNonphys;
            if (scale.z > World.m_maxNonphys)
                scale.z = World.m_maxNonphys;
            Vector3 tmp = part.Scale;
            tmp.X = (float)scale.x;
            tmp.Y = (float)scale.y;
            tmp.Z = (float)scale.z;
            part.Scale = tmp;
            part.SendFullUpdateToAllClients();
        }

        public LSL_Types.Vector3 llGetScale()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Types.Vector3(m_host.Scale.X, m_host.Scale.Y, m_host.Scale.Z);
        }

        public void llSetColor(LSL_Types.Vector3 color, int face)
        {
            m_host.AddScriptLPS(1);

            SetColor(m_host, color, face);
        }

        private void SetColor(SceneObjectPart part, LSL_Types.Vector3 color, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            if (face > -1)
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.R = (float)color.x;
                texcolor.G = (float)color.y;
                texcolor.B = (float)color.z;
                tex.FaceTextures[face].RGBA = texcolor;
                part.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (uint i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.R = (float)color.x;
                        texcolor.G = (float)color.y;
                        texcolor.B = (float)color.z;
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = (float)color.x;
                    texcolor.G = (float)color.y;
                    texcolor.B = (float)color.z;
                    tex.DefaultTexture.RGBA = texcolor;
                }
                part.UpdateTexture(tex);
                return;
            }
        }

        public void SetGlow(SceneObjectPart part, int face, float glow)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face > -1)
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].Glow = glow;
                part.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (uint i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].Glow = glow;
                    }
                    tex.DefaultTexture.Glow = glow;
                }
                part.UpdateTexture(tex);
                return;
            }
        }

        public void SetShiny(SceneObjectPart part, int face, int shiny, Bumpiness bump)
        {

            Shininess sval = new Shininess();

            switch (shiny)
            {
            case 0:
                sval = Shininess.None;
                break;
            case 1:
                sval = Shininess.Low;
                break;
            case 2:
                sval = Shininess.Medium;
                break;
            case 3:
                sval = Shininess.High;
                break;
            default:
                sval = Shininess.None;
                break;
            }

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face > -1)
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].Shiny = sval;
                tex.FaceTextures[face].Bump = bump;
                part.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (uint i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].Shiny = sval;
                        tex.FaceTextures[i].Bump = bump;;
                    }
                    tex.DefaultTexture.Shiny = sval;
                    tex.DefaultTexture.Bump = bump;
                }
                part.UpdateTexture(tex);
                return;
            }
        }

        public void SetFullBright(SceneObjectPart part, int face, bool bright)
        {
             Primitive.TextureEntry tex = part.Shape.Textures;
             if (face > -1)
             {
                 tex.CreateFace((uint) face);
                 tex.FaceTextures[face].Fullbright = bright;
                 part.UpdateTexture(tex);
                 return;
             }
             else if (face == -1)
             {
                 for (uint i = 0; i < 32; i++)
                 {
                     if (tex.FaceTextures[i] != null)
                     {
                         tex.FaceTextures[i].Fullbright = bright;
                     }
                 }
                 tex.DefaultTexture.Fullbright = bright;
                 part.UpdateTexture(tex);
                 return;
             }
         }

        public double llGetAlpha(int face)
        {
            m_host.AddScriptLPS(1);
            Primitive.TextureEntry tex = m_host.Shape.Textures;
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

            SetAlpha(m_host, alpha, face);
        }

        private void SetAlpha(SceneObjectPart part, double alpha, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            if (face > -1)
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.A = (float)Math.Abs(alpha);
                tex.FaceTextures[face].RGBA = texcolor;
                part.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.A = (float)Math.Abs(alpha);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                }
                texcolor = tex.DefaultTexture.RGBA;
                texcolor.A = (float)Math.Abs(alpha);
                tex.DefaultTexture.RGBA = texcolor;
                part.UpdateTexture(tex);
                return;
            }
        }

        /// <summary>
        /// Set flexi parameters of a part.
        ///
        /// FIXME: Much of this code should probably be within the part itself.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="flexi"></param>
        /// <param name="softness"></param>
        /// <param name="gravity"></param>
        /// <param name="friction"></param>
        /// <param name="wind"></param>
        /// <param name="tension"></param>
        /// <param name="Force"></param>
        private void SetFlexi(SceneObjectPart part, bool flexi, int softness, float gravity, float friction,
            float wind, float tension, LSL_Types.Vector3 Force)
        {
            if (part == null)
                return;

            bool needs_fakedelete = false;
            if (flexi)
            {
                if (!part.Shape.FlexiEntry)
                {
                    needs_fakedelete = true;
                }
                part.Shape.FlexiEntry = true;   // this setting flexi true isn't working, but the below parameters do
                                                // work once the prim is already flexi
                part.Shape.FlexiSoftness = softness;
                part.Shape.FlexiGravity = gravity;
                part.Shape.FlexiDrag = friction;
                part.Shape.FlexiWind = wind;
                part.Shape.FlexiTension = tension;
                part.Shape.FlexiForceX = (float)Force.x;
                part.Shape.FlexiForceY = (float)Force.y;
                part.Shape.FlexiForceZ = (float)Force.z;
                part.Shape.PathCurve = 0x80;

            }
            else
            {
                if (part.Shape.FlexiEntry)
                {
                    needs_fakedelete = true;
                }
                part.Shape.FlexiEntry = false;
            }

            needs_fakedelete = false;
            if (needs_fakedelete)
            {
                if (part.ParentGroup != null)
                {
                    part.ParentGroup.FakeDeleteGroup();
                }
            }

            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();
        }

        /// <summary>
        /// Set a light point on a part
        ///
        /// FIXME: Much of this code should probably be in SceneObjectGroup
        /// </summary>
        /// <param name="part"></param>
        /// <param name="light"></param>
        /// <param name="color"></param>
        /// <param name="intensity"></param>
        /// <param name="radius"></param>
        /// <param name="falloff"></param>
        private void SetPointLight(SceneObjectPart part, bool light, LSL_Types.Vector3 color, float intensity, float radius, float falloff)
        {
            if (part == null)
                return;

            if (light)
            {
                part.Shape.LightEntry = true;
                part.Shape.LightColorR = (float)color.x;
                part.Shape.LightColorG = (float)color.y;
                part.Shape.LightColorB = (float)color.z;
                part.Shape.LightIntensity = intensity;
                part.Shape.LightRadius = radius;
                part.Shape.LightFalloff = falloff;
            }
            else
            {
                part.Shape.LightEntry = false;
            }

            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();
        }

        public LSL_Types.Vector3 llGetColor(int face)
        {
            m_host.AddScriptLPS(1);
            Primitive.TextureEntry tex = m_host.Shape.Textures;
            Color4 texcolor;
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
                return new LSL_Types.Vector3();
            }
        }

        public void llSetTexture(string texture, int face)
        {
            m_host.AddScriptLPS(1);
            SetTexture(m_host, texture, face);
            // ScriptSleep(200);
        }

        private void SetTexture(SceneObjectPart part, string texture, int face)
        {
            UUID textureID=new UUID();

            if (!UUID.TryParse(texture, out textureID))
            {
                textureID=InventoryKey(texture, (int)AssetType.Texture);
            }

            if (textureID == UUID.Zero)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;

            if (face > -1)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.TextureID = textureID;
                tex.FaceTextures[face] = texface;
                part.UpdateTexture(tex);
                return;
            }
            else if (face == -1)
            {
                for (uint i = 0; i < 32; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].TextureID = textureID;
                    }
                }
                tex.DefaultTexture.TextureID = textureID;
                part.UpdateTexture(tex);
                return;
            }
        }

        public void llScaleTexture(double u, double v, int face)
        {
            m_host.AddScriptLPS(1);

            ScaleTexture(m_host, u, v, face);
            // ScriptSleep(200);
        }

        private void ScaleTexture(SceneObjectPart part, double u, double v, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face > -1)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.RepeatU = (float)u;
                texface.RepeatV = (float)v;
                tex.FaceTextures[face] = texface;
                part.UpdateTexture(tex);
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
                part.UpdateTexture(tex);
                return;
            }
        }

        public void llOffsetTexture(double u, double v, int face)
        {
            m_host.AddScriptLPS(1);
            OffsetTexture(m_host, u, v, face);
            // ScriptSleep(200);
        }

        private void OffsetTexture(SceneObjectPart part, double u, double v, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face > -1)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.OffsetU = (float)u;
                texface.OffsetV = (float)v;
                tex.FaceTextures[face] = texface;
                part.UpdateTexture(tex);
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
                part.UpdateTexture(tex);
                return;
            }
        }

        public void llRotateTexture(double rotation, int face)
        {
            m_host.AddScriptLPS(1);
            RotateTexture(m_host, rotation, face);
            // ScriptSleep(200);
        }

        private void RotateTexture(SceneObjectPart part, double rotation, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face > -1)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.Rotation = (float)rotation;
                tex.FaceTextures[face] = texface;
                part.UpdateTexture(tex);
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
                part.UpdateTexture(tex);
                return;
            }
        }

        public string llGetTexture(int face)
        {
            m_host.AddScriptLPS(1);
            Primitive.TextureEntry tex = m_host.Shape.Textures;
            if (face == -1)
            {
                face = 0;
            }
            if (face > -1)
            {
                Primitive.TextureEntryFace texface;
                texface = tex.GetFace((uint)face);
                return texface.TextureID.ToString();
            }
            else
            {
                return String.Empty;
            }
        }

        public void llSetPos(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);

            SetPos(m_host, pos);

            ScriptSleep(200);
        }

        private void SetPos(SceneObjectPart part, LSL_Types.Vector3 targetPos)
        {
            // Capped movemment if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)
            LSL_Types.Vector3 currentPos = llGetLocalPos();
            if (llVecDist(currentPos, targetPos) > 10.0f * m_ScriptDistanceFactor)
            {
                targetPos = currentPos + m_ScriptDistanceFactor * 10 * llVecNorm(targetPos - currentPos);
            }

            if (part.ParentID != 0)
            {
                part.UpdateOffSet(new Vector3((float)targetPos.x, (float)targetPos.y, (float)targetPos.z));
            }
            else
            {
                part.UpdateGroupPosition(new Vector3((float)targetPos.x, (float)targetPos.y, (float)targetPos.z));
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

            SetRot(m_host, rot);

            ScriptSleep(200);
        }

        private void SetRot(SceneObjectPart part, LSL_Types.Quaternion rot)
        {
            part.UpdateRotation(new Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s));
            // Update rotation does not move the object in the physics scene if it's a linkset.
            part.ParentGroup.AbsolutePosition = part.ParentGroup.AbsolutePosition;
        }

        public LSL_Types.Quaternion llGetRot()
        {
            m_host.AddScriptLPS(1);
            Quaternion q = m_host.RotationOffset;
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
            //NotImplemented("llSetForce");

            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    if (local != 0)
                        force *= llGetRot();

                    m_host.ParentGroup.RootPart.SetForce(new PhysicsVector((float)force.x, (float)force.y, (float)force.z));
                }
            }
        }

        public LSL_Types.Vector3 llGetForce()
        {
            LSL_Types.Vector3 force = new LSL_Types.Vector3(0.0, 0.0, 0.0);

            m_host.AddScriptLPS(1);
            //NotImplemented("llGetForce");
            //return new LSL_Types.Vector3();

            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    PhysicsVector tmpForce = m_host.ParentGroup.RootPart.GetForce();
                    force.x = tmpForce.X;
                    force.y = tmpForce.Y;
                    force.z = tmpForce.Z;
                }
            }

            return force;
        }

        public LSL_Types.LSLInteger llTarget(LSL_Types.Vector3 position, double range)
        {
            m_host.AddScriptLPS(1);
            return m_host.registerTargetWaypoint(new Vector3((float)position.x, (float)position.y, (float)position.z), (float)range);

        }

        public void llTargetRemove(int number)
        {
            m_host.AddScriptLPS(1);
            m_host.unregisterTargetWaypoint(number);
        }

        public LSL_Types.LSLInteger llRotTarget(LSL_Types.Quaternion rot, double error)
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
            m_host.MoveToTarget(new Vector3((float)target.x, (float)target.y, (float)target.z), (float)tau);
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

            m_host.ApplyImpulse(new Vector3((float)force.x, (float)force.y, (float)force.z), local != 0);
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
            return (double)(((DateTime.Now.TimeOfDay.TotalMilliseconds / 1000) % (3600 * 4)) * World.TimeDilation);
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
            return (double)((ScriptTime.TotalMilliseconds / 1000)*World.TimeDilation);
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
            return (double)((ScriptTime.TotalMilliseconds / 1000)*World.TimeDilation);
        }

        public void llSound()
        {
            m_host.AddScriptLPS(1);
            // This function has been deprecated
            // see http://www.lslwiki.net/lslwiki/wakka.php?wakka=llSound
            Deprecated("llSound");
        }

        // Xantor 20080528 PlaySound updated so it accepts an objectinventory name -or- a key to a sound
        // 20080530 Updated to remove code duplication
        public void llPlaySound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);

            // send the sound, once, to all clients in range
            m_host.SendSound(KeyOrName(sound).ToString(), volume, false, 0);
        }

        // Xantor 20080528 we should do this differently.
        // 1) apply the sound to the object
        // 2) schedule full update
        // just sending the sound out once doesn't work so well when other avatars come in view later on
        // or when the prim gets moved, changed, sat on, whatever
        // see large number of mantises (mantes?)
        // 20080530 Updated to remove code duplication
        // 20080530 Stop sound if there is one, otherwise volume only changes don't work
        public void llLoopSound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);

            if (m_host.Sound != UUID.Zero)
                llStopSound();

            m_host.Sound = KeyOrName(sound);
            m_host.SoundGain = volume;
            m_host.SoundFlags = 1;      // looping
            m_host.SoundRadius = 20;    // Magic number, 20 seems reasonable. Make configurable?

            m_host.ScheduleFullUpdate();
            m_host.SendFullUpdateToAllClients();
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
            // send the sound, once, to all clients in range
            m_host.SendSound(KeyOrName(sound).ToString(), volume, false, 0);
        }

        // Xantor 20080528: Clear prim data of sound instead
        public void llStopSound()
        {
            m_host.AddScriptLPS(1);

            m_host.Sound = UUID.Zero;
            m_host.SoundGain = 0;
            m_host.SoundFlags = 0;
            m_host.SoundRadius = 0;

            m_host.ScheduleFullUpdate();
            m_host.SendFullUpdateToAllClients();

            // m_host.SendSound(UUID.Zero.ToString(), 1.0, false, 2);
        }

        public void llPreloadSound(string sound)
        {
            m_host.AddScriptLPS(1);
            m_host.PreloadSound(sound);
            // ScriptSleep(1000);
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
                if (end < 0 || start >= src.Length)
                {
                    return String.Empty;
                }
                // If end is positive, then it directly
                // corresponds to the lengt of the substring
                // needed (plus one of course). BUT, it
                // must be within bounds.
                if (end >= src.Length)
                {
                    end = src.Length-1;
                }

                if (start < 0)
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
                if (start < 0)
                {
                    return src;
                }
                // If both indices are greater than the upper
                // bound the result may seem initially counter
                // intuitive.
                if (end >= src.Length)
                {
                    return src;
                }

                if (end < 0)
                {
                    if (start < src.Length)
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
                    if (start < src.Length)
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
                if (end < 0 || start >= src.Length)
                {
                    return src;
                }
                // At least one bound is in-range, so we
                // need to clip the out-of-bound argument.
                if (start < 0)
                {
                    start = 0;
                }

                if (end >= src.Length)
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
                if (start < 0 || end >= src.Length)
                {
                    return String.Empty;
                }

                if (end > 0)
                {
                    if (start < src.Length)
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
                    if (start < src.Length)
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

                if (index < 0)
                {
                    return src+dest;
                }

            }

            if (index >= dest.Length)
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

        public LSL_Types.LSLInteger llGiveMoney(string destination, int amount)
        {
            UUID invItemID=InventorySelf();
            if (invItemID == UUID.Zero)
                return 0;

            m_host.AddScriptLPS(1);

            if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero)
                return 0;

            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0)
            {
                LSLError("No permissions to give money");
                return 0;
            }

            UUID toID=new UUID();

            if (!UUID.TryParse(destination, out toID))
            {
                LSLError("Bad key in llGiveMoney");
                return 0;
            }

            IMoneyModule money=World.RequestModuleInterface<IMoneyModule>();

            if (money == null)
            {
                NotImplemented("llGiveMoney");
                return 0;
            }

            bool result=money.ObjectGiveMoney(m_host.ParentGroup.RootPart.UUID, m_host.ParentGroup.RootPart.OwnerID, toID, amount);

            if (result)
                return 1;

            return 0;
        }

        public void llMakeExplosion()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeExplosion");
            // ScriptSleep(100);
        }

        public void llMakeFountain()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeFountain");
            // ScriptSleep(100);
        }

        public void llMakeSmoke()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeSmoke");
            // ScriptSleep(100);
        }

        public void llMakeFire()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llMakeFire");
            // ScriptSleep(100);
        }

        public void llRezAtRoot(string inventory, LSL_Types.Vector3 pos, LSL_Types.Vector3 vel, LSL_Types.Quaternion rot, int param)
        {
            float dist = (float)llVecMag(llGetPos() - pos);

            if (dist > m_ScriptDistanceFactor * 10.0f)
                return;

            m_host.AddScriptLPS(1);
            bool found = false;

            // Instead of using return;, I'm using continue; because in our TaskInventory implementation
            // it's possible to have two items with the same task inventory name.
            // this is an easter egg of sorts.

            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == inventory)
                {
                    // make sure we're an object.
                    if (inv.Value.InvType != (int)InventoryType.Object)
                    {
                        llSay(0, "Unable to create requested object. Object is missing from database.");
                        continue;
                    }

                    Vector3 llpos = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);

                    // test if we're further away then 10m
                    if (Util.GetDistanceTo(llpos, m_host.AbsolutePosition) > 10)
                        return; // wiki says, if it's further away then 10m, silently fail.

                    Vector3 llvel = new Vector3((float)vel.x, (float)vel.y, (float)vel.z);

                    // need the magnitude later
                    float velmag = (float)Util.GetMagnitude(llvel);

                    SceneObjectGroup new_group = World.RezObject(m_host, inv.Value, llpos, new Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s), llvel, param);

                    // If either of these are null, then there was an unknown error.
                    if (new_group == null)
                        continue;
                    if (new_group.RootPart == null)
                        continue;

                    // objects rezzed with this method are die_at_edge by default.
                    new_group.RootPart.SetDieAtEdge(true);

                    m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                            "object_rez", new Object[] {
                            new LSL_Types.LSLString(
                            new_group.RootPart.UUID.ToString()) },
                            new DetectParams[0]));

                    float groupmass = new_group.GetMass();

                    //Recoil.
                    llApplyImpulse(new LSL_Types.Vector3(llvel.X * groupmass, llvel.Y * groupmass, llvel.Z * groupmass), 0);
                    found = true;
                    // Variable script delay? (see (http://wiki.secondlife.com/wiki/LSL_Delay)
                    ScriptSleep((int)((groupmass * velmag) / 10));
                    // ScriptSleep(100);
                    break;
                }
            }
            if (!found)
                llSay(0, "Could not find object " + inventory);
        }

        public void llRezObject(string inventory, LSL_Types.Vector3 pos, LSL_Types.Vector3 vel, LSL_Types.Quaternion rot, int param)
        {
            llRezAtRoot(inventory, pos, vel, rot, param);
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
            AsyncCommands.TimerPlugin.SetTimerEvent(m_localID, m_itemID, sec);
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
            if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
            {
                return;
            }

            if (m_host.TaskInventory[InventorySelf()].PermsGranter != UUID.Zero)
            {
                ScenePresence presence = World.GetScenePresence(m_host.TaskInventory[InventorySelf()].PermsGranter);

                if (presence != null)
                {
                    if ((m_host.TaskInventory[InventorySelf()].PermsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                    {
                        presence.RegisterControlEventsToScript(controls, accept, pass_on, m_localID, m_itemID);

                    }
                }
            }

            m_host.AddScriptLPS(1);
            //NotImplemented("llTakeControls");
        }

        public void llReleaseControls()
        {
            m_host.AddScriptLPS(1);

            if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
            {
                return;
            }

            if (m_host.TaskInventory[InventorySelf()].PermsGranter != UUID.Zero)
            {
                ScenePresence presence = World.GetScenePresence(m_host.TaskInventory[InventorySelf()].PermsGranter);

                if (presence != null)
                {
                    if ((m_host.TaskInventory[InventorySelf()].PermsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                    {
                        // Unregister controls from Presence
                        presence.UnRegisterControlEventsToScript(m_localID, m_itemID);
                        // Remove Take Control permission.
                        m_host.TaskInventory[InventorySelf()].PermsMask &= ~ScriptBaseClass.PERMISSION_TAKE_CONTROLS;
                    }
                }
            }
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

        public void llTakeCamera(string avatar)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llTakeCamera");
        }

        public void llReleaseCamera(string avatar)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llReleaseCamera");
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
            UUID friendTransactionID = UUID.Random();

            //m_pendingFriendRequests.Add(friendTransactionID, fromAgentID);

            GridInstantMessage msg = new GridInstantMessage();
            msg.fromAgentID = new Guid(m_host.ToString()); // fromAgentID.Guid;
            msg.fromAgentSession = new Guid(friendTransactionID.ToString());// fromAgentSession.UUID;
            msg.toAgentID = new Guid(user); // toAgentID.Guid;
            msg.imSessionID = new Guid(friendTransactionID.ToString()); // This is the item we're mucking with here
//            Console.WriteLine("[Scripting IM]: From:" + msg.fromAgentID.ToString() + " To: " + msg.toAgentID.ToString() + " Session:" + msg.imSessionID.ToString() + " Message:" + message);
//            Console.WriteLine("[Scripting IM]: Filling Session: " + msg.imSessionID.ToString());
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
            msg.Position = Vector3.Zero;// new Vector3(m_host.AbsolutePosition);
            msg.RegionID = World.RegionInfo.RegionID.Guid;//RegionID.Guid;
            msg.binaryBucket = new byte[0];// binaryBucket;
            World.TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
            // ScriptSleep(2000);

            //  NotImplemented("llInstantMessage");
      }

        public void llEmail(string address, string subject, string message)
        {
            m_host.AddScriptLPS(1);
            IEmailModule emailModule = m_ScriptEngine.World.RequestModuleInterface<IEmailModule>();
            if (emailModule == null)
                return;

            emailModule.SendEmail(m_host.UUID, address, subject, message);
            // ScriptSleep(20000);
        }

        public void llGetNextEmail(string address, string subject)
        {
            m_host.AddScriptLPS(1);
            IEmailModule emailModule = m_ScriptEngine.World.RequestModuleInterface<IEmailModule>();
            if (emailModule == null)
                return;
            Email email;

            email = emailModule.GetNextEmail(m_host.UUID, address, subject);

            if (email == null)
                return;

            m_ScriptEngine.PostObjectEvent(m_host.LocalId,
                    new EventParams("email",
                    new Object[] {
                        new LSL_Types.LSLString(email.time),
                        new LSL_Types.LSLString(email.sender),
                        new LSL_Types.LSLString(email.subject),
                        new LSL_Types.LSLString(email.message),
                        new LSL_Types.LSLInteger(email.numLeft)},
                    new DetectParams[0]));

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

        public LSL_Types.LSLInteger llStringLength(string str)
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

            UUID invItemID=InventorySelf();
            if (invItemID == UUID.Zero)
                return;

            if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero)
                return;

            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0)
            {
                ScenePresence presence = World.GetScenePresence(m_host.TaskInventory[invItemID].PermsGranter);

                if (presence != null)
                {
                    // Do NOT try to parse UUID, animations cannot be triggered by ID
                    UUID animID=InventoryKey(anim, (int)AssetType.Animation);
                    if (animID == UUID.Zero)
                        presence.AddAnimation(anim);
                    else
                        presence.AddAnimation(animID);
                }
            }
        }

        public void llStopAnimation(string anim)
        {
            m_host.AddScriptLPS(1);

            UUID invItemID=InventorySelf();
            if (invItemID == UUID.Zero)
                return;

            if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero)
                return;

            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0)
            {
                UUID animID = new UUID();

                if (!UUID.TryParse(anim, out animID))
                {
                    animID=InventoryKey(anim);
                }

                if (animID == UUID.Zero)
                    return;

                ScenePresence presence = World.GetScenePresence(m_host.TaskInventory[invItemID].PermsGranter);

                if (presence != null)
                {
                    if (animID == UUID.Zero)
                        presence.RemoveAnimation(anim);
                    else
                        presence.RemoveAnimation(animID);
                }
            }
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
            m_host.RotationalVelocity = new Vector3((float)(axis.x * spinrate), (float)(axis.y * spinrate), (float)(axis.z * spinrate));
            m_host.AngularVelocity = new Vector3((float)(axis.x * spinrate), (float)(axis.y * spinrate), (float)(axis.z * spinrate));
            m_host.ScheduleTerseUpdate();
            m_host.SendTerseUpdateToAllClients();
            m_host.ParentGroup.HasGroupChanged = true;
        }

        public LSL_Types.LSLInteger llGetStartParameter()
        {
            m_host.AddScriptLPS(1);
            return m_ScriptEngine.GetStartParameter(m_itemID);
        }

        public void llGodLikeRezObject(string inventory, LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGodLikeRezObject");
        }

        public void llRequestPermissions(string agent, int perm)
        {
            UUID agentID=new UUID();

            if (!UUID.TryParse(agent, out agentID))
                return;

            UUID invItemID=InventorySelf();

            if (invItemID == UUID.Zero)
                return; // Not in a prim? How??

            if (agentID == UUID.Zero || perm == 0) // Releasing permissions
            {
                m_host.TaskInventory[invItemID].PermsGranter=UUID.Zero;
                m_host.TaskInventory[invItemID].PermsMask=0;

                m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                        "run_time_permissions", new Object[] {
                        new LSL_Types.LSLInteger(0) },
                        new DetectParams[0]));

                return;
            }

            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup.RootPart.IsAttachment && agent == m_host.ParentGroup.RootPart.AttachedAvatar)
            {
                // When attached, certain permissions are implicit if requested from owner
                int implicitPerms = ScriptBaseClass.PERMISSION_TAKE_CONTROLS |
                        ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION |
                        ScriptBaseClass.PERMISSION_CONTROL_CAMERA |
                        ScriptBaseClass.PERMISSION_ATTACH;

                if ((perm & (~implicitPerms)) == 0) // Requested only implicit perms
                {
                    m_host.TaskInventory[invItemID].PermsGranter=agentID;
                    m_host.TaskInventory[invItemID].PermsMask=perm;

                    m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                            "run_time_permissions", new Object[] {
                            new LSL_Types.LSLInteger(perm) },
                            new DetectParams[0]));

                    return;
                }
            }
            else if (m_host.SitTargetAvatar == agentID) // Sitting avatar
            {
                // When agent is sitting, certain permissions are implicit if requested from sitting agent
                int implicitPerms = ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION |
                        ScriptBaseClass.PERMISSION_CONTROL_CAMERA |
                        ScriptBaseClass.PERMISSION_TRACK_CAMERA;

                if ((perm & (~implicitPerms)) == 0) // Requested only implicit perms
                {
                    m_host.TaskInventory[invItemID].PermsGranter=agentID;
                    m_host.TaskInventory[invItemID].PermsMask=perm;

                    m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                            "run_time_permissions", new Object[] {
                            new LSL_Types.LSLInteger(perm) },
                            new DetectParams[0]));

                    return;
                }
            }

            ScenePresence presence = World.GetScenePresence(agentID);

            if (presence != null)
            {
                string ownerName=resolveName(m_host.ParentGroup.RootPart.OwnerID);
                if (ownerName == String.Empty)
                    ownerName="(hippos)";

                if (!m_waitingForScriptAnswer)
                {
                    m_host.TaskInventory[invItemID].PermsGranter=agentID;
                    m_host.TaskInventory[invItemID].PermsMask=0;
                    presence.ControllingClient.OnScriptAnswer+=handleScriptAnswer;
                    m_waitingForScriptAnswer=true;
                }

                presence.ControllingClient.SendScriptQuestion(m_host.UUID, m_host.ParentGroup.RootPart.Name, ownerName, invItemID, perm);
                return;
            }

            // Requested agent is not in range, refuse perms
            m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                    "run_time_permissions", new Object[] {
                    new LSL_Types.LSLInteger(0) },
                    new DetectParams[0]));
        }

        void handleScriptAnswer(IClientAPI client, UUID taskID, UUID itemID, int answer)
        {
            if (taskID != m_host.UUID)
                return;

            UUID invItemID=InventorySelf();

            if (invItemID == UUID.Zero)
                return;

            client.OnScriptAnswer-=handleScriptAnswer;
            m_waitingForScriptAnswer=false;

            m_host.TaskInventory[invItemID].PermsMask=answer;
            m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                    "run_time_permissions", new Object[] {
                    new LSL_Types.LSLInteger(answer) },
                    new DetectParams[0]));
        }

        public string llGetPermissionsKey()
        {
            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 10 && item.ItemID == m_itemID)
                {
                    return item.PermsGranter.ToString();
                }
            }

            return UUID.Zero.ToString();
        }

        public LSL_Types.LSLInteger llGetPermissions()
        {
            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 10 && item.ItemID == m_itemID)
                {
                    return item.PermsMask;
                }
            }

            return 0;
        }

        public LSL_Types.LSLInteger llGetLinkNumber()
        {
            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup.Children.Count > 1)
            {
                return m_host.LinkNum;
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
                Primitive.TextureEntry tex = part.Shape.Textures;
                Color4 texcolor;
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
                    Primitive.TextureEntry tex = part.Shape.Textures;
                    Color4 texcolor;
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
        }

        public void llCreateLink(string target, int parent)
        {
            m_host.AddScriptLPS(1);
            UUID invItemID = InventorySelf();
            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0) {
              ShoutError("Script trying to link but PERMISSION_CHANGE_LINKS permission not set!");
              return;
            }
            IClientAPI client = World.GetScenePresence(m_host.TaskInventory[invItemID].PermsGranter).ControllingClient;
            SceneObjectPart targetPart = World.GetSceneObjectPart(target);
            SceneObjectGroup parentPrim = null, childPrim = null;
            if (targetPart != null)
            {
                if (parent != 0) {
                    parentPrim = m_host.ParentGroup;
                    childPrim = targetPart.ParentGroup;
                }
                else
                {
                    parentPrim = targetPart.ParentGroup;
                    childPrim = m_host.ParentGroup;
                }
                byte uf = childPrim.RootPart.UpdateFlag;
                childPrim.RootPart.UpdateFlag = 0;
                parentPrim.LinkToGroup(childPrim);
                childPrim.RootPart.UpdateFlag = uf;
            }
            parentPrim.TriggerScriptChangedEvent(Changed.LINK);
            parentPrim.RootPart.AddFlag(PrimFlags.CreateSelected);
            parentPrim.GetProperties(client);

            ScriptSleep(1000);
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
                return UUID.Zero.ToString();
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
                return "";
            }
        }

        public LSL_Types.LSLInteger llGetInventoryNumber(int type)
        {
            m_host.AddScriptLPS(1);
            int count = 0;
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
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
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
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
            UUID item;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if ((item = ScriptByName(name)) != UUID.Zero)
            {
                m_ScriptEngine.SetScriptState(item, run == 0 ? false : true);
            }
            else
            {
                ShoutError("llSetScriptState: script "+name+" not found");
            }
        }

        public double llGetEnergy()
        {
            m_host.AddScriptLPS(1);
            // TODO: figure out real energy value
            return 1.0f;
        }

        public void llGiveInventory(string destination, string inventory)
        {
            m_host.AddScriptLPS(1);
            bool found = false;
            UUID destId = UUID.Zero;
            UUID objId = UUID.Zero;

            if (!UUID.TryParse(destination, out destId))
            {
                llSay(0, "Could not parse key " + destination);
                return;
            }

            // move the first object found with this inventory name
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == inventory)
                {
                    found = true;
                    objId = inv.Key;
                    break;
                }
            }

            if (!found)
            {
                llSay(0, String.Format("Could not find object '{0}'", inventory));
                throw new Exception(String.Format("The inventory object '{0}' could not be found", inventory));
            }

            // check if destination is an avatar
            if (World.GetScenePresence(destId) != null)
            {
                // destination is an avatar
                World.MoveTaskInventoryItem(destId, null, m_host, objId);
            }
            else
            {
                // destination is an object
                World.MoveTaskInventoryItem(destId, m_host, objId);
            }
            // ScriptSleep(3000);
        }

        public void llRemoveInventory(string name)
        {
            m_host.AddScriptLPS(1);
            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Name == name)
                {
                    m_host.RemoveInventoryItem(item.ItemID);
                    return;
                }
            }
        }

        public void llSetText(string text, LSL_Types.Vector3 color, double alpha)
        {
            m_host.AddScriptLPS(1);
            Vector3 av3 = new Vector3((float)color.x, (float)color.y, (float)color.z);
            m_host.SetText(text, av3, alpha);
            m_host.ParentGroup.HasGroupChanged = true;
        }

        public double llWater(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            return World.RegionInfo.RegionSettings.WaterHeight;
        }

        public void llPassTouches(int pass)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llPassTouches");
        }

        public string llRequestAgentData(string id, int data)
        {
            m_host.AddScriptLPS(1);

            UserProfileData userProfile =
                    World.CommsManager.UserService.GetUserProfile(id);

            UserAgentData userAgent =
                    World.CommsManager.UserService.GetAgentByUUID(id);

            if (userProfile == null || userAgent == null)
                return UUID.Zero.ToString();

            string reply = String.Empty;

            switch (data)
            {
            case 1: // DATA_ONLINE (0|1)
                // TODO: implement fetching of this information
                if (userProfile.CurrentAgent.AgentOnline)
                    reply = "1";
                else
                    reply = "0";
                break;
            case 2: // DATA_NAME (First Last)
                reply = userProfile.FirstName + " " + userProfile.SurName;
                break;
            case 3: // DATA_BORN (YYYY-MM-DD)
                DateTime born = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                born = born.AddSeconds(userProfile.Created);
                reply = born.ToString("yyyy-MM-dd");
                break;
            case 4: // DATA_RATING (0,0,0,0,0,0)
                reply = "0,0,0,0,0,0";
                break;
            case 8: // DATA_PAYINFO (0|1|2|3)
                reply = "0";
                break;
            default:
                return UUID.Zero.ToString(); // Raise no event
            }

            UUID rq = UUID.Random();

            UUID tid = AsyncCommands.
                DataserverPlugin.RegisterRequest(m_localID,
                                             m_itemID, rq.ToString());

            AsyncCommands.
            DataserverPlugin.DataserverReply(rq.ToString(), reply);

            // ScriptSleep(100);
            return tid.ToString();
        }

        public string llRequestInventoryData(string name)
        {
            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 3 && item.Name == name)
                {
                    UUID tid = AsyncCommands.
                        DataserverPlugin.RegisterRequest(m_localID,
                                                     m_itemID, item.AssetID.ToString());

                    Vector3 region = new Vector3(
                        World.RegionInfo.RegionLocX * Constants.RegionSize,
                        World.RegionInfo.RegionLocY * Constants.RegionSize,
                        0);

                    World.AssetCache.GetAsset(item.AssetID,
                        delegate(UUID i, AssetBase a)
                        {
                            AssetLandmark lm = new AssetLandmark(a);

                            float rx = (uint)(lm.RegionHandle >> 32);
                            float ry = (uint)lm.RegionHandle;
                            region = lm.Position + new Vector3(rx, ry, 0) - region;

                            string reply = region.ToString();
                            AsyncCommands.
                                DataserverPlugin.DataserverReply(i.ToString(),
                                                             reply);
                        }, false);

                    // ScriptSleep(1000);
                    return tid.ToString();
                }
            }
            // ScriptSleep(1000);
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
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // agent must be over the owners land
                    if (m_host.OwnerID == World.GetLandOwner(presence.AbsolutePosition.X, presence.AbsolutePosition.Y))
                        World.TeleportClientHome(agentId, presence.ControllingClient);
                }
            }
            // ScriptSleep(5000);
        }

        public void llModifyLand(int action, int brush)
        {
            m_host.AddScriptLPS(1);
            World.ExternalChecks.ExternalChecksCanTerraformLand(m_host.OwnerID, new Vector3(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, 0));
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
            m_ScriptEngine.ApiResetScript(m_itemID);
            throw new EventAbortException();
        }

        public void llMessageLinked(int linknum, int num, string msg, string id)
        {

            m_host.AddScriptLPS(1);

            // uint partLocalID;
            UUID partItemID;

            switch ((int)linknum)
            {

                case (int)ScriptBaseClass.LINK_ROOT:

                    SceneObjectPart part = m_host.ParentGroup.RootPart;

                    foreach (TaskInventoryItem item in part.TaskInventory.Values)
                    {
                        if (item.Type == 10)
                        {
                            // partLocalID = part.LocalId;
                            partItemID = item.ItemID;

                            object[] resobj = new object[]
                            {
                                new LSL_Types.LSLInteger(m_host.LinkNum), new LSL_Types.LSLInteger(num), new LSL_Types.LSLString(msg), new LSL_Types.LSLString(id)
                            };

                            m_ScriptEngine.PostScriptEvent(partItemID,
                                    new EventParams("link_message",
                                    resobj, new DetectParams[0]));
                        }
                    }

                    break;

                case (int)ScriptBaseClass.LINK_SET:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                        {
                            if (item.Type == 10)
                            {
                                // partLocalID = partInst.LocalId;
                                partItemID = item.ItemID;
                                Object[] resobj = new object[]
                                {
                                    new LSL_Types.LSLInteger(m_host.LinkNum), new LSL_Types.LSLInteger(num), new LSL_Types.LSLString(msg), new LSL_Types.LSLString(id)
                                };

                                m_ScriptEngine.PostScriptEvent(partItemID,
                                        new EventParams("link_message",
                                        resobj, new DetectParams[0]));
                            }
                        }
                    }

                    break;

                case (int)ScriptBaseClass.LINK_ALL_OTHERS:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        if (partInst.LocalId != m_host.LocalId)
                        {

                            foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                            {
                                if (item.Type == 10)
                                {
                                    // partLocalID = partInst.LocalId;
                                    partItemID = item.ItemID;
                                    Object[] resobj = new object[]
                                    {
                                        new LSL_Types.LSLInteger(m_host.LinkNum), new LSL_Types.LSLInteger(num), new LSL_Types.LSLString(msg), new LSL_Types.LSLString(id)
                                    };

                                    m_ScriptEngine.PostScriptEvent(partItemID,
                                            new EventParams("link_message",
                                            resobj, new DetectParams[0]));
                                }
                            }

                        }
                    }

                    break;

                case (int)ScriptBaseClass.LINK_ALL_CHILDREN:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        if (partInst.LocalId != m_host.ParentGroup.RootPart.LocalId)
                        {

                            foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                            {
                                if (item.Type == 10)
                                {
                                    // partLocalID = partInst.LocalId;
                                    partItemID = item.ItemID;
                                    Object[] resobj = new object[]
                                    {
                                        new LSL_Types.LSLInteger(m_host.LinkNum), new LSL_Types.LSLInteger(num), new LSL_Types.LSLString(msg), new LSL_Types.LSLString(id)
                                    };

                                    m_ScriptEngine.PostScriptEvent(partItemID,
                                            new EventParams("link_message",
                                            resobj, new DetectParams[0]));
                                }
                            }

                        }
                    }

                    break;

                case (int)ScriptBaseClass.LINK_THIS:

                    foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
                    {
                        if (item.Type == 10)
                        {
                            partItemID = item.ItemID;

                            object[] resobj = new object[]
                            {
                                new LSL_Types.LSLInteger(m_host.LinkNum), new LSL_Types.LSLInteger(num), new LSL_Types.LSLString(msg), new LSL_Types.LSLString(id)
                            };

                            m_ScriptEngine.PostScriptEvent(partItemID,
                                    new EventParams("link_message",
                                    resobj, new DetectParams[0]));
                        }
                    }

                    break;

                default:

                    foreach (SceneObjectPart partInst in m_host.ParentGroup.GetParts())
                    {

                        if ((partInst.LinkNum) == linknum)
                        {

                            foreach (TaskInventoryItem item in partInst.TaskInventory.Values)
                            {
                                if (item.Type == 10)
                                {
                                    // partLocalID = partInst.LocalId;
                                    partItemID = item.ItemID;
                                    Object[] resObjDef = new object[]
                                    {
                                        new LSL_Types.LSLInteger(m_host.LinkNum), new LSL_Types.LSLInteger(num), new LSL_Types.LSLString(msg), new LSL_Types.LSLString(id)
                                    };

                                    m_ScriptEngine.PostScriptEvent(partItemID,
                                            new EventParams("link_message",
                                            resObjDef, new DetectParams[0]));
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
            SceneObjectPart targ = World.GetSceneObjectPart(target);
            if (targ == null)
                return;
            targ.ApplyImpulse(new Vector3((float)impulse.x, (float)impulse.y, (float)impulse.z), local != 0);
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
                if (item.Type == 10 && item.ItemID == m_itemID)
                {
                    result =  item.Name!=null?item.Name:String.Empty;
                    break;
                }
            }

            return result;

        }

        // this function to understand which shape it is (taken from meshmerizer)
        // quite useful can be used by meshmerizer to have a centralized point of understanding the shape
        // except that it refers to scripting constants
        private int getScriptPrimType(PrimitiveBaseShape primShape)
        {
            if (primShape.SculptEntry)
                return ScriptBaseClass.PRIM_TYPE_SCULPT;
            if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Square)
            {
                if (primShape.PathCurve == (byte)Extrusion.Straight)
                    return ScriptBaseClass.PRIM_TYPE_BOX;
                else if (primShape.PathCurve == (byte)Extrusion.Curve1)
                    return ScriptBaseClass.PRIM_TYPE_TUBE;
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
                if (primShape.PathCurve == (byte)Extrusion.Straight)
                    return ScriptBaseClass.PRIM_TYPE_CYLINDER;
                // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
                else if (primShape.PathCurve == (byte)Extrusion.Curve1)
                    return ScriptBaseClass.PRIM_TYPE_TORUS;
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            {
                if (primShape.PathCurve == (byte)Extrusion.Curve1 || primShape.PathCurve == (byte)Extrusion.Curve2)
                    return ScriptBaseClass.PRIM_TYPE_SPHERE;
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
            {
                if (primShape.PathCurve == (byte)Extrusion.Straight)
                    return ScriptBaseClass.PRIM_TYPE_PRISM;
                else if (primShape.PathCurve == (byte)Extrusion.Curve1)
                    return ScriptBaseClass.PRIM_TYPE_RING;
            }
            return ScriptBaseClass.PRIM_TYPE_BOX;
        }

        // Helper functions to understand if object has cut, hollow, dimple, and other affecting number of faces
        private void hasCutHollowDimpleProfileCut(int primType, PrimitiveBaseShape shape, out bool hasCut, out bool hasHollow,
            out bool hasDimple, out bool hasProfileCut)
        {
            if (primType == ScriptBaseClass.PRIM_TYPE_BOX
                ||
                primType == ScriptBaseClass.PRIM_TYPE_CYLINDER
                ||
                primType == ScriptBaseClass.PRIM_TYPE_PRISM)

                hasCut = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0);
            else
                hasCut = (shape.PathBegin > 0) || (shape.PathEnd > 0);

            hasHollow = shape.ProfileHollow > 0;
            hasDimple = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0); // taken from llSetPrimitiveParms
            hasProfileCut = hasDimple; // is it the same thing?

        }

        public LSL_Types.LSLInteger llGetNumberOfSides()
        {
            m_host.AddScriptLPS(1);
            int ret = 0;
            bool hasCut;
            bool hasHollow;
            bool hasDimple;
            bool hasProfileCut;

            int primType = getScriptPrimType(m_host.Shape);
            hasCutHollowDimpleProfileCut(primType, m_host.Shape, out hasCut, out hasHollow, out hasDimple, out hasProfileCut);

            switch (primType)
            {
                case ScriptBaseClass.PRIM_TYPE_BOX:
                    ret = 6;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case ScriptBaseClass.PRIM_TYPE_CYLINDER:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case ScriptBaseClass.PRIM_TYPE_PRISM:
                    ret = 5;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case ScriptBaseClass.PRIM_TYPE_SPHERE:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasDimple) ret += 2;
                    if (hasHollow) ret += 3; // Emulate lsl on secondlife (according to documentation it should have added only +1)
                    break;
                case ScriptBaseClass.PRIM_TYPE_TORUS:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case ScriptBaseClass.PRIM_TYPE_TUBE:
                    ret = 4;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case ScriptBaseClass.PRIM_TYPE_RING:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case ScriptBaseClass.PRIM_TYPE_SCULPT:
                    ret = 1;
                    break;
            }
            return ret;
        }


        /* The new / changed functions were tested with the following LSL script:

        default
        {
            state_entry()
            {
                rotation rot = llEuler2Rot(<0,70,0> * DEG_TO_RAD);

                llOwnerSay("to get here, we rotate over: "+ (string) llRot2Axis(rot));
                llOwnerSay("and we rotate for: "+ (llRot2Angle(rot) * RAD_TO_DEG));

                // convert back and forth between quaternion <-> vector and angle

                rotation newrot = llAxisAngle2Rot(llRot2Axis(rot),llRot2Angle(rot));

                llOwnerSay("Old rotation was: "+(string) rot);
                llOwnerSay("re-converted rotation is: "+(string) newrot);

                llSetRot(rot);  // to check the parameters in the prim
            }
        }
        */



        // Xantor 29/apr/2008
        // Returns rotation described by rotating angle radians about axis.
        // q = cos(a/2) + i (x * sin(a/2)) + j (y * sin(a/2)) + k (z * sin(a/2))
        public LSL_Types.Quaternion llAxisAngle2Rot(LSL_Types.Vector3 axis, double angle)
        {
            m_host.AddScriptLPS(1);

            double x, y, z, s, t;

            s = Math.Cos(angle / 2);
            t = Math.Sin(angle / 2); // temp value to avoid 2 more sin() calcs
            x = axis.x * t;
            y = axis.y * t;
            z = axis.z * t;

            return new LSL_Types.Quaternion(x,y,z,s);
        }


        // Xantor 29/apr/2008
        // converts a Quaternion to X,Y,Z axis rotations
        public LSL_Types.Vector3 llRot2Axis(LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);
            double x,y,z;

            if (rot.s > 1) // normalization needed
            {
                double length = Math.Sqrt(rot.x * rot.x + rot.y * rot.y +
                        rot.z * rot.z + rot.s * rot.s);

                rot.x /= length;
                rot.y /= length;
                rot.z /= length;
                rot.s /= length;

            }

            // double angle = 2 * Math.Acos(rot.s);
            double s = Math.Sqrt(1 - rot.s * rot.s);
            if (s < 0.001)
            {
                x = 1;
                y = z = 0;
            }
            else
            {
                x = rot.x / s; // normalise axis
                y = rot.y / s;
                z = rot.z / s;
            }


            return new LSL_Types.Vector3(x,y,z);


//            NotImplemented("llRot2Axis");
        }


        // Returns the angle of a quaternion (see llRot2Axis for the axis)
        public double llRot2Angle(LSL_Types.Quaternion rot)
        {
            m_host.AddScriptLPS(1);

            if (rot.s > 1) // normalization needed
            {
                double length = Math.Sqrt(rot.x * rot.x + rot.y * rot.y +
                        rot.z * rot.z + rot.s * rot.s);

                rot.x /= length;
                rot.y /= length;
                rot.z /= length;
                rot.s /= length;

            }

            double angle = 2 * Math.Acos(rot.s);

            return angle;

//            NotImplemented("llRot2Angle");
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

        // Xantor 30/apr/2008
        public double llAngleBetween(LSL_Types.Quaternion a, LSL_Types.Quaternion b)
        {
            m_host.AddScriptLPS(1);

           return (double) Math.Acos(a.x * b.x + a.y * b.y + a.z * b.z + a.s * b.s) * 2;
        }

        public string llGetInventoryKey(string name)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == name)
                {
                    if ((inv.Value.CurrentPermissions & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify)) == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    {
                        return inv.Value.AssetID.ToString();
                    }
                    else
                    {
                        return UUID.Zero.ToString();
                    }
                }
            }
            return UUID.Zero.ToString();
        }

        public void llAllowInventoryDrop(int add)
        {
            m_host.AddScriptLPS(1);

            if (add != 0)
                m_host.ParentGroup.RootPart.AllowedDrop = true;
            else
                m_host.ParentGroup.RootPart.AllowedDrop = false;
        }

        public LSL_Types.Vector3 llGetSunDirection()
        {
            m_host.AddScriptLPS(1);

            LSL_Types.Vector3 SunDoubleVector3;
            Vector3 SunFloatVector3;

            // sunPosition estate setting is set in OpenSim.Region.Environment.Modules.SunModule
            // have to convert from Vector3 (float) to LSL_Types.Vector3 (double)
            SunFloatVector3 = World.RegionInfo.RegionSettings.SunVector;
            SunDoubleVector3.x = (double)SunFloatVector3.X;
            SunDoubleVector3.y = (double)SunFloatVector3.Y;
            SunDoubleVector3.z = (double)SunFloatVector3.Z;

            return SunDoubleVector3;
        }

        public LSL_Types.Vector3 llGetTextureOffset(int face)
        {
            m_host.AddScriptLPS(1);
            Primitive.TextureEntry tex = m_host.Shape.Textures;
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
            Primitive.TextureEntry tex = m_host.Shape.Textures;
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
            Primitive.TextureEntry tex = m_host.Shape.Textures;
            if (face == -1)
            {
                face = 0;
            }
            return tex.GetFace((uint)face).Rotation;
        }

        public LSL_Types.LSLInteger llSubStringIndex(string source, string pattern)
        {
            m_host.AddScriptLPS(1);
            return source.IndexOf(pattern);
        }

        public string llGetOwnerKey(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
                return World.GetSceneObjectPart(World.Entities[key].LocalId).OwnerID.ToString();
            }
            else
            {
                return UUID.Zero.ToString();
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

            if (stride <= 0)
            {
                stride = 1;
            }
            return src.Sort(stride, ascending);
        }

        public LSL_Types.LSLInteger llGetListLength(LSL_Types.list src)
        {
            m_host.AddScriptLPS(1);

            if (src == null)
            {
                return 0;
            }
            else
            {
                return src.Length;
            }
        }

        public LSL_Types.LSLInteger llList2Integer(LSL_Types.list src, int index)
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
            try
            {
                if (src.Data[index] is LSL_Types.LSLInteger)
                    return Convert.ToInt32(((LSL_Types.LSLInteger) src.Data[index]).value);
                else if (src.Data[index] is LSL_Types.LSLFloat)
                    return Convert.ToInt32(((LSL_Types.LSLFloat) src.Data[index]).value);
                else if (src.Data[index] is LSL_Types.LSLString)
                    return Convert.ToInt32(((LSL_Types.LSLString) src.Data[index]).m_string);
                return Convert.ToInt32(src.Data[index]);
            }
            catch (FormatException)
            {
                return 0;
            }
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
            if (src.Data[index] is LSL_Types.LSLInteger)
                return Convert.ToDouble(((LSL_Types.LSLInteger) src.Data[index]).value);
            else if (src.Data[index] is LSL_Types.LSLFloat)
                return Convert.ToDouble(((LSL_Types.LSLFloat) src.Data[index]).value);
            else if (src.Data[index] is LSL_Types.LSLString)
                return Convert.ToDouble(((LSL_Types.LSLString) src.Data[index]).m_string);
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
            try
            {
                if (src.Data[index] is LSL_Types.LSLInteger)
                    return Convert.ToDouble(((LSL_Types.LSLInteger) src.Data[index]).value);
                else if (src.Data[index] is LSL_Types.LSLFloat)
                    return Convert.ToDouble(((LSL_Types.LSLFloat) src.Data[index]).value);
                else if (src.Data[index] is LSL_Types.LSLString)
                    return Convert.ToDouble(((LSL_Types.LSLString) src.Data[index]).m_string);
                return Convert.ToDouble(src.Data[index]);
            }
            catch (FormatException)
            {
                return 0.0;
            }
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
                return "";
            }
            return src.Data[index].ToString();
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
                return new LSL_Types.Vector3(src.Data[index].ToString());
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
                return new LSL_Types.Quaternion(src.Data[index].ToString());
            }
        }

        public LSL_Types.list llList2List(LSL_Types.list src, int start, int end)
        {
            m_host.AddScriptLPS(1);
            return src.GetSublist(start, end);
        }

        public LSL_Types.list llDeleteSubList(LSL_Types.list src, int start, int end)
        {
            return src.DeleteSublist(end, start);
        }

        public LSL_Types.LSLInteger llGetListEntryType(LSL_Types.list src, int index)
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
                UUID tuuid;
                if (UUID.TryParse(src.Data[index].ToString(), out tuuid))
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

            if (src.Data.Length > 0)
            {
                ret = src.Data[x++].ToString();
                for (; x < src.Data.Length; x++)
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

            for (int i = 0; i < src.Length; i++)
            {
                switch (src[i])
                {
                    case '<':
                        parens++;
                        length++;
                        break;
                    case '>':
                        if (parens > 0)
                            parens--;
                        length++;
                        break;
                    case ',':
                        if (parens == 0)
                        {
                            result.Add(src.Substring(start,length).Trim());
                            start += length+1;
                            length = 0;
                        }
                        else
                        {
                            length++;
                        }
                        break;
                    default:
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

            m_host.AddScriptLPS(1);

            if (stride <= 0)
            {
                stride = 1;
            }

            // Stride MUST be a factor of the list length
            // If not, then return the src list. This also
            // traps those cases where stride > length.

            if (src.Length != stride && src.Length%stride == 0)
            {
                chunkk = src.Length/stride;

                chunks = new int[chunkk];

                for (int i = 0; i < chunkk; i++)
                    chunks[i] = i;

                // Knuth shuffle the chunkk index
                for (int i = chunkk - 1; i >= 1; i--)
                {
                    // Elect an unrandomized chunk to swap
                    int index = rand.Next(i + 1);
                    int tmp;

                    // and swap position with first unrandomized chunk
                    tmp = chunks[i];
                    chunks[i] = chunks[index];
                    chunks[index] = tmp;
                }

                // Construct the randomized list

                result = new LSL_Types.list();

                for (int i = 0; i < chunkk; i++)
                {
                    for (int j = 0; j < stride; j++)
                    {
                        result.Add(src.Data[chunks[i]*stride+j]);
                    }
                }
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

            if (start < 0)
                start = src.Length+start;
            if (end   < 0)
                end   = src.Length+end;

            //  Out of bounds indices are OK, just trim them
            //  accordingly

            if (start > src.Length)
                start = src.Length;

            if (end > src.Length)
                end = src.Length;

            //  There may be one or two ranges to be considered

            if (start != end)
            {

                if (start <= end)
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

                if (stride == 0)
                    stride = 1;

                if (stride > 0)
                {
                    for (int i = 0; i < src.Length; i += stride)
                    {
                        if (i<=ei[0] && i>=si[0])
                            result.Add(src.Data[i]);
                        if (twopass && i>=si[1] && i<=ei[1])
                            result.Add(src.Data[i]);
                    }
                }
                else if (stride < 0)
                {
                    for (int i = src.Length - 1; i >= 0; i += stride)
                    {
                        if (i <= ei[0] && i >= si[0])
                            result.Add(src.Data[i]);
                        if (twopass && i >= si[1] && i <= ei[1])
                            result.Add(src.Data[i]);
                    }
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

            if (index < 0)
            {
                index = index+dest.Length;
                if (index < 0)
                {
                    index = 0;
                }
            }

            if (index != 0)
            {
                pref = dest.GetSublist(0,index-1);
                if (index < dest.Length)
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
                if (index < dest.Length)
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

        public LSL_Types.LSLInteger llListFindList(LSL_Types.list src, LSL_Types.list test)
        {

            int index  = -1;
            int length = src.Length - test.Length + 1;

            m_host.AddScriptLPS(1);

            // If either list is empty, do not match

            if (src.Length != 0 && test.Length != 0)
            {
                for (int i = 0; i < length; i++)
                {
                   if (src.Data[i].Equals(test.Data[0]))
                   {
                       int j;
                       for (j = 1; j < test.Length; j++)
                           if (!src.Data[i+j].Equals(test.Data[j]))
                               break;
                       if (j == test.Length)
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

        public LSL_Types.LSLInteger llEdgeOfWorld(LSL_Types.Vector3 pos, LSL_Types.Vector3 dir)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llEdgeOfWorld");
            return 0;
        }

        public LSL_Types.LSLInteger llGetAgentInfo(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAgentInfo");
            return 0;
        }

        public void llAdjustSoundVolume(double volume)
        {
            m_host.AddScriptLPS(1);
            m_host.AdjustSoundGain(volume);
            // ScriptSleep(100);
        }

        public void llSetSoundQueueing(int queue)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetSoundQueueing");
        }

        public void llSetSoundRadius(double radius)
        {
            m_host.AddScriptLPS(1);
            m_host.SoundRadius = radius;
        }

        public string llKey2Name(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id,out key))
            {
                ScenePresence presence = World.GetScenePresence(key);

                if (presence != null)
                {
                    return presence.ControllingClient.Name;
                    //return presence.Name;
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
            pTexAnim.Flags = (Primitive.TextureAnimMode)mode;

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
            m_host.ParentGroup.HasGroupChanged = true;
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
            UUID agentId = new UUID();
            if (UUID.TryParse(pest, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // agent must be over the owners land
                    if (m_host.OwnerID == World.GetLandOwner(presence.AbsolutePosition.X, presence.AbsolutePosition.Y))
                        World.TeleportClientHome(agentId, presence.ControllingClient);
                }
            }
            // ScriptSleep(5000);
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
                    if (found && String.Empty != delimiters[i].ToString())
                    {
                        if ((cindex > index) || (cindex == -1))
                        {
                            cindex = index;
                            cdeli = delimiters[i].ToString();
                        }
                        dfound = dfound || found;
                    }
                }
                if (cindex != -1)
                {
                    if (cindex > 0)
                    {
                        ret.Add(str.Substring(0, cindex));
                        // Cannot use spacers.Contains() because spacers may be either type String or LSLString
                        for (int j = 0; j < spacers.Length; j++)
                        {
                            if (spacers.Data[j].ToString() == cdeli)
                            {
                                ret.Add(cdeli);
                                break;
                            }
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

        public LSL_Types.LSLInteger llOverMyLand(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id,out key))
            {
                ScenePresence presence = World.GetScenePresence(key);
                if (presence != null) // object is an avatar
                {
                    if (m_host.OwnerID == World.GetLandOwner(presence.AbsolutePosition.X, presence.AbsolutePosition.Y))
                        return 1;
                }
                else // object is not an avatar
                {
                    SceneObjectPart obj = World.GetSceneObjectPart(key);
                    if (obj != null)
                        if (m_host.OwnerID == World.GetLandOwner(obj.AbsolutePosition.X, obj.AbsolutePosition.Y))
                            return 1;
                }
            }
            return 0;
        }

        public string llGetLandOwnerAt(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            return World.GetLandOwner((float)pos.x, (float)pos.y).ToString();
        }

        public LSL_Types.Vector3 llGetAgentSize(string id)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAgentSize");
            return new LSL_Types.Vector3();
        }

        public LSL_Types.LSLInteger llSameGroup(string agent)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSameGroup");
            return 0;
        }

        public void llUnSit(string id)
        {
            m_host.AddScriptLPS(1);

            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
                ScenePresence av = World.GetScenePresence(key);

                if (av != null)
                {
                    if (llAvatarOnSitTarget() == id)
                    {
                        // if the avatar is sitting on this object, then
                        // we can unsit them.  We don't want random scripts unsitting random people
                        // Lets avoid the popcorn avatar scenario.
                        av.StandUp();
                    }
                    else
                    {
                        // If the object owner also owns the parcel
                        // or
                        // if the land is group owned and the object is group owned by the same group
                        // or
                        // if the object is owned by a person with estate access.

                        ILandObject parcel = World.LandChannel.GetLandObject(av.AbsolutePosition.X, av.AbsolutePosition.Y);
                        if (parcel != null)
                        {
                            if (m_host.ObjectOwner == parcel.landData.OwnerID ||
                                (m_host.OwnerID == m_host.GroupID && m_host.GroupID == parcel.landData.GroupID
                                && parcel.landData.IsGroupOwned) || World.ExternalChecks.ExternalChecksCanBeGodLike(m_host.OwnerID))
                            {
                                av.StandUp();
                            }
                        }
                    }
                }

            }

        }

        public LSL_Types.Vector3 llGroundSlope(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);

            Vector3 pos = m_host.AbsolutePosition + new Vector3((float)offset.x,
                                                                (float)offset.y,
                                                                (float)offset.z);

            Vector3 p0 = new Vector3(pos.X, pos.Y,
                                     (float)llGround(
                                                 new LSL_Types.Vector3(pos.X, pos.Y, pos.Z)
                                                 ));
            Vector3 p1 = new Vector3(pos.X + 1, pos.Y,
                                     (float)llGround(
                                                 new LSL_Types.Vector3(pos.X + 1, pos.Y, pos.Z)
                                                 ));
            Vector3 p2 = new Vector3(pos.X, pos.Y + 1,
                                     (float)llGround(
                                                 new LSL_Types.Vector3(pos.X, pos.Y + 1, pos.Z)
                                                 ));

            Vector3 v0 = new Vector3(
                p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(
                p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);

            v0.Normalize();
            v1.Normalize();

            Vector3 tv = new Vector3();
            tv.X = (v0.Y * v1.Z) - (v0.Z * v1.Y);
            tv.Y = (v0.Z * v1.X) - (v0.X * v1.Z);
            tv.Z = (v0.X * v1.Y) - (v0.Y * v1.X);

            return new LSL_Types.Vector3(tv.X, tv.Y, tv.Z);
        }

        public LSL_Types.Vector3 llGroundNormal(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.Vector3 x = llGroundSlope(offset);
            return new LSL_Types.Vector3(x.x, x.y, 1.0);
        }

        public LSL_Types.Vector3 llGroundContour(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.Vector3 x = llGroundSlope(offset);
            return new LSL_Types.Vector3(-x.y, x.x, 0.0);
        }

        public LSL_Types.LSLInteger llGetAttached()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGetAttached");
            return 0;
        }

        public LSL_Types.LSLInteger llGetFreeMemory()
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
            //TODO: return actual FPS
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

        private Primitive.ParticleSystem getNewParticleSystemWithSLDefaultValues()
        {
            Primitive.ParticleSystem ps = new Primitive.ParticleSystem();

            // TODO find out about the other defaults and add them here
            ps.PartStartColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
            ps.PartEndColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
            ps.PartStartScaleX = 1.0f;
            ps.PartStartScaleY = 1.0f;
            ps.PartEndScaleX = 1.0f;
            ps.PartEndScaleY = 1.0f;
            ps.BurstSpeedMin = 1.0f;
            ps.BurstSpeedMax = 1.0f;
            ps.BurstRate = 0.1f;
            ps.PartMaxAge = 10.0f;
            return ps;
        }

        public void llParticleSystem(LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);
            if (rules.Length == 0)
            {
                m_host.RemoveParticleSystem();
                m_host.ParentGroup.HasGroupChanged = true;
            }
            else
            {
                Primitive.ParticleSystem prules = getNewParticleSystemWithSLDefaultValues();
                LSL_Types.Vector3 tempv = new LSL_Types.Vector3();

                float tempf = 0;

                for (int i = 0; i < rules.Length; i += 2)
                {
                    switch ((int)rules.Data[i])
                    {
                        case (int)ScriptBaseClass.PSYS_PART_FLAGS:
                            prules.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)((uint)Convert.ToInt32(rules.Data[i + 1].ToString()));
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_START_COLOR:
                            tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                            prules.PartStartColor.R = (float)tempv.x;
                            prules.PartStartColor.G = (float)tempv.y;
                            prules.PartStartColor.B = (float)tempv.z;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_START_ALPHA:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.PartStartColor.A = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_END_COLOR:
                            tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                            //prules.PartEndColor = new Color4(tempv.x,tempv.y,tempv.z,1);

                            prules.PartEndColor.R = (float)tempv.x;
                            prules.PartEndColor.G = (float)tempv.y;
                            prules.PartEndColor.B = (float)tempv.z;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_END_ALPHA:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.PartEndColor.A = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_START_SCALE:
                            tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                            prules.PartStartScaleX = (float)tempv.x;
                            prules.PartStartScaleY = (float)tempv.y;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_END_SCALE:
                            tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                            prules.PartEndScaleX = (float)tempv.x;
                            prules.PartEndScaleY = (float)tempv.y;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_MAX_AGE:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.PartMaxAge = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_ACCEL:
                            tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                            prules.PartAcceleration.X = (float)tempv.x;
                            prules.PartAcceleration.Y = (float)tempv.y;
                            prules.PartAcceleration.Z = (float)tempv.z;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_PATTERN:
                            int tmpi = int.Parse(rules.Data[i + 1].ToString());
                            prules.Pattern = (Primitive.ParticleSystem.SourcePattern)tmpi;
                            break;

                        // Xantor 20080503
                        // Wiki:    PSYS_SRC_TEXTURE      string      inventory item name or key of the particle texture
                        //          "" = default texture.
                        // 20080530 Updated to remove code duplication
                        case (int)ScriptBaseClass.PSYS_SRC_TEXTURE:
                            prules.Texture = KeyOrName(rules.Data[i + 1].ToString());
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_RATE:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.BurstRate = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_PART_COUNT:
                            prules.BurstPartCount = (byte)Convert.ToByte(rules.Data[i + 1].ToString());
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_RADIUS:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.BurstRadius = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_SPEED_MIN:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.BurstSpeedMin = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_SPEED_MAX:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.BurstSpeedMax = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_MAX_AGE:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.MaxAge = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_TARGET_KEY:
                            UUID key = UUID.Zero;
                            if (UUID.TryParse(rules.Data[i + 1].ToString(), out key))
                            {
                                prules.Target = key;
                            }
                            else
                            {
                                prules.Target = m_host.UUID;
                            }
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_OMEGA:
                            // AL: This is an assumption, since it is the only thing that would match.
                            tempv = (LSL_Types.Vector3)rules.Data[i + 1];
                            prules.AngularVelocity.X = (float)tempv.x;
                            prules.AngularVelocity.Y = (float)tempv.y;
                            prules.AngularVelocity.Z = (float)tempv.z;
                            //cast??                    prules.MaxAge = (float)rules[i + 1];
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_ANGLE_BEGIN:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.InnerAngle = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_ANGLE_END:
                            tempf = Convert.ToSingle(rules.Data[i + 1].ToString());
                            prules.OuterAngle = (float)tempf;
                            break;
                    }

                }
                prules.CRC = 1;

                m_host.AddNewParticleSystem(prules);
                m_host.ParentGroup.HasGroupChanged = true;
            }
            m_host.SendFullUpdateToAllClients();
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llGroundRepel");
        }

        private UUID GetTaskInventoryItem(string name)
        {
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == name)
                    return inv.Key;
            }
            return UUID.Zero;
        }

        public void llGiveInventoryList(string destination, string category, LSL_Types.list inventory)
        {
            m_host.AddScriptLPS(1);

            UUID destID;
            if (!UUID.TryParse(destination, out destID))
                return;

            List<UUID> itemList = new List<UUID>();

            foreach (Object item in inventory.Data)
            {
                UUID itemID;
                if (UUID.TryParse(item.ToString(), out itemID))
                {
                    itemList.Add(itemID);
                }
                else
                {
                    itemID = GetTaskInventoryItem(item.ToString());
                    if (itemID != UUID.Zero)
                        itemList.Add(itemID);
                }
            }

            if (itemList.Count == 0)
                return;

            m_ScriptEngine.World.MoveTaskInventoryItems(destID, category, m_host, itemList);
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

        public void llSetVehicleFloatParam(int param, float value)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetVehicleFloatParam");
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

            m_host.SitTargetPosition = new Vector3((float)offset.x, (float)offset.y, (float)offset.z);
            m_host.SitTargetOrientation = new Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s);
        }

        public string llAvatarOnSitTarget()
        {
            m_host.AddScriptLPS(1);
            return m_host.GetAvatarOnSitTarget().ToString();
        }

        public void llAddToLandPassList(string avatar, double hours)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.OwnerID == m_host.OwnerID)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                if (UUID.TryParse(avatar, out key))
                {
                    entry.AgentID = key;
                    entry.Flags = ParcelManager.AccessList.Access;
                    entry.Time = DateTime.Now.AddHours(hours);
                    land.ParcelAccessList.Add(entry);
                }
            }
            // ScriptSleep(100);
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
            m_host.SetCameraEyeOffset(new Vector3((float)offset.x, (float)offset.y, (float)offset.z));
        }

        public void llSetCameraAtOffset(LSL_Types.Vector3 offset)
        {
            m_host.AddScriptLPS(1);
            m_host.SetCameraAtOffset(new Vector3((float)offset.x, (float)offset.y, (float)offset.z));
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

        public LSL_Types.LSLInteger llScriptDanger(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            bool result = World.scriptDanger(m_host.LocalId, new Vector3((float)pos.x, (float)pos.y, (float)pos.z));
            if (result)
            {
                return 1;
            }
            else
            {
                return 0;
            }

        }

        public void llDialog(string avatar, string message, LSL_Types.list buttons, int chat_channel)
        {
            m_host.AddScriptLPS(1);
            UUID av = new UUID();
            if (!UUID.TryParse(avatar,out av))
            {
                LSLError("First parameter to llDialog needs to be a key");
                return;
            }
            if (buttons.Length > 12)
            {
                LSLError("No more than 12 buttons can be shown");
                return;
            }
            string[] buts = new string[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons.Data[i].ToString() == String.Empty)
                {
                    LSLError("button label cannot be blank");
                    return;
                }
                if (buttons.Data[i].ToString().Length > 24)
                {
                    LSLError("button label cannot be longer than 24 characters");
                    return;
                }
                buts[i] = buttons.Data[i].ToString();
            }
            World.SendDialogToUser(av, m_host.Name, m_host.UUID, m_host.OwnerID, message, new UUID("00000000-0000-2222-3333-100000001000"), chat_channel, buts);
            // ScriptSleep(1000);
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
            UUID item;

            m_host.AddScriptLPS(1);

            if ((item = ScriptByName(name)) != UUID.Zero)
                m_ScriptEngine.ResetScript(item);
            else
                ShoutError("llResetOtherScript: script "+name+" not found");
        }

        public LSL_Types.LSLInteger llGetScriptState(string name)
        {
            UUID item;

            m_host.AddScriptLPS(1);

            if ((item = ScriptByName(name)) != UUID.Zero)
            {
                return m_ScriptEngine.GetScriptState(item) ?1:0;
            }

            ShoutError("llGetScriptState: script "+name+" not found");

            // If we didn't find it, then it's safe to
            // assume it is not running.

            return 0;
        }

        public void llRemoteLoadScript()
        {
            m_host.AddScriptLPS(1);
            Deprecated("llRemoteLoadScript");
            // ScriptSleep(3000);
        }

        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llSetRemoteScriptAccessPin");
        }

        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            m_host.AddScriptLPS(1);
            bool found = false;
            UUID destId = UUID.Zero;
            UUID srcId = UUID.Zero;

            if (!UUID.TryParse(target, out destId))
            {
                llSay(0, "Could not parse key " + target);
                return;
            }

            // target must be a different prim than the one containing the script
            if (m_host.UUID == destId)
            {
                return;
            }

            // copy the first script found with this inventory name
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == name)
                {
                    // make sure the object is a script
                    if (10 == inv.Value.Type)
                    {
                        found = true;
                        srcId = inv.Key;
                        break;
                    }
                }
            }

            if (!found)
            {
                llSay(0, "Could not find script " + name);
                return;
            }

            // the rest of the permission checks are done in RezScript, so check the pin there as well
            World.RezScript(srcId, m_host, destId, pin, running, start_param);
            // this will cause the delay even if the script pin or permissions were wrong - seems ok
            ScriptSleep(3000);
        }

        public void llOpenRemoteDataChannel()
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod.IsEnabled())
            {
                UUID channelID = xmlrpcMod.OpenXMLRPCChannel(m_localID, m_itemID, UUID.Zero);
                object[] resobj = new object[] { new LSL_Types.LSLInteger(1), new LSL_Types.LSLString(channelID.ToString()), new LSL_Types.LSLString(UUID.Zero.ToString()), new LSL_Types.LSLString(String.Empty), new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(String.Empty) };
                m_ScriptEngine.PostScriptEvent(m_itemID, new EventParams(
                        "remote_data", resobj,
                        new DetectParams[0]));
            }
            // ScriptSleep(1000);
        }

        public string llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            // ScriptSleep(3000);
            return (xmlrpcMod.SendRemoteData(m_localID, m_itemID, channel, dest, idata, sdata)).ToString();
        }

        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod.RemoteDataReply(channel, message_id, sdata, idata);
            // ScriptSleep(3000);
        }

        public void llCloseRemoteDataChannel(string channel)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod.CloseXMLRPCChannel(channel);
            // ScriptSleep(1000);
        }

        public string llMD5String(string src, int nonce)
        {
            m_host.AddScriptLPS(1);
            return Util.Md5Hash(src + ":" + nonce.ToString());
        }

        private ObjectShapePacket.ObjectDataBlock SetPrimitiveBlockShapeParams(SceneObjectPart part, int holeshape, LSL_Types.Vector3 cut, float hollow, LSL_Types.Vector3 twist)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock();

            if (holeshape != (int)ScriptBaseClass.PRIM_HOLE_DEFAULT &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_CIRCLE &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_SQUARE &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_TRIANGLE)
            {
                holeshape = (int)ScriptBaseClass.PRIM_HOLE_DEFAULT;
            }
            shapeBlock.ProfileCurve = (byte)holeshape;
            if (cut.x < 0f)
            {
                cut.x = 0f;
            }
            if (cut.x > 1f)
            {
                cut.x = 1f;
            }
            if (cut.y < 0f)
            {
                cut.y = 0f;
            }
            if (cut.y > 1f)
            {
                cut.y = 1f;
            }
            if (cut.y - cut.x < 0.05f)
            {
                cut.x = cut.y - 0.05f;
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * cut.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - cut.y));
            if (hollow < 0f)
            {
                hollow = 0f;
            }
            if (hollow > 0.95)
            {
                hollow = 0.95f;
            }
            shapeBlock.ProfileHollow = (ushort)(50000 * hollow);
            if (twist.x < -1.0f)
            {
                twist.x = -1.0f;
            }
            if (twist.x > 1.0f)
            {
                twist.x = 1.0f;
            }
            if (twist.y < -1.0f)
            {
                twist.y = -1.0f;
            }
            if (twist.y > 1.0f)
            {
                twist.y = 1.0f;
            }
            shapeBlock.PathTwistBegin = (sbyte)(100 * twist.x);
            shapeBlock.PathTwist = (sbyte)(100 * twist.y);

            shapeBlock.ObjectLocalID = part.LocalId;

            // retain pathcurve
            shapeBlock.PathCurve = part.Shape.PathCurve;

            return shapeBlock;
        }

        private void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Types.Vector3 cut, float hollow, LSL_Types.Vector3 twist, LSL_Types.Vector3 taper_b, LSL_Types.Vector3 topshear, byte fudge)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist);

            shapeBlock.ProfileCurve += fudge;

            if (taper_b.x < 0f)
            {
                taper_b.x = 0f;
            }
            if (taper_b.x > 2f)
            {
                taper_b.x = 2f;
            }
            if (taper_b.y < 0f)
            {
                taper_b.y = 0f;
            }
            if (taper_b.y > 2f)
            {
                taper_b.y = 2f;
            }
            shapeBlock.PathScaleX = (byte)(100 * (2.0 - taper_b.x));
            shapeBlock.PathScaleY = (byte)(100 * (2.0 - taper_b.y));
            if (topshear.x < -0.5f)
            {
                topshear.x = -0.5f;
            }
            if (topshear.x > 0.5f)
            {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f)
            {
                topshear.y = -0.5f;
            }
            if (topshear.y > 0.5f)
            {
                topshear.y = 0.5f;
            }
            shapeBlock.PathShearX = (byte)(100 * topshear.x);
            shapeBlock.PathShearY = (byte)(100 * topshear.y);

            part.UpdateShape(shapeBlock);
        }

        private void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Types.Vector3 cut, float hollow, LSL_Types.Vector3 twist, LSL_Types.Vector3 dimple, byte fudge)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist);

            // profile/path swapped for a sphere
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            shapeBlock.ProfileCurve += fudge;

            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 100;

            if (dimple.x < 0f)
            {
                dimple.x = 0f;
            }
            if (dimple.x > 1f)
            {
                dimple.x = 1f;
            }
            if (dimple.y < 0f)
            {
                dimple.y = 0f;
            }
            if (dimple.y > 1f)
            {
                dimple.y = 1f;
            }
            if (dimple.y - cut.x < 0.05f)
            {
                dimple.x = cut.y - 0.05f;
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * dimple.x);
            shapeBlock.ProfileEnd   = (ushort)(50000 * (1 - dimple.y));

            part.UpdateShape(shapeBlock);
        }

        private void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Types.Vector3 cut, float hollow, LSL_Types.Vector3 twist, LSL_Types.Vector3 holesize, LSL_Types.Vector3 topshear, LSL_Types.Vector3 profilecut, LSL_Types.Vector3 taper_a, float revolutions, float radiusoffset, float skew, byte fudge)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist);

            shapeBlock.ProfileCurve += fudge;

            // profile/path swapped for a torrus, tube, ring
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            if (holesize.x < 0.05f)
            {
                holesize.x = 0.05f;
            }
            if (holesize.x > 1f)
            {
                holesize.x = 1f;
            }
            if (holesize.y < 0.05f)
            {
                holesize.y = 0.05f;
            }
            if (holesize.y > 0.5f)
            {
                holesize.y = 0.5f;
            }
            shapeBlock.PathScaleX = (byte)(100 * (2 - holesize.x));
            shapeBlock.PathScaleY = (byte)(100 * (2 - holesize.y));
            if (topshear.x < -0.5f)
            {
                topshear.x = -0.5f;
            }
            if (topshear.x > 0.5f)
            {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f)
            {
                topshear.y = -0.5f;
            }
            if (topshear.y > 0.5f)
            {
                topshear.y = 0.5f;
            }
            shapeBlock.PathShearX = (byte)(100 * topshear.x);
            shapeBlock.PathShearY = (byte)(100 * topshear.y);
            if (profilecut.x < 0f)
            {
                profilecut.x = 0f;
            }
            if (profilecut.x > 1f)
            {
                profilecut.x = 1f;
            }
            if (profilecut.y < 0f)
            {
                profilecut.y = 0f;
            }
            if (profilecut.y > 1f)
            {
                profilecut.y = 1f;
            }
            if (profilecut.y - cut.x < 0.05f)
            {
                profilecut.x = cut.y - 0.05f;
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * profilecut.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - profilecut.y));
            if (taper_a.x < -1f)
            {
                taper_a.x = -1f;
            }
            if (taper_a.x > 1f)
            {
                taper_a.x = 1f;
            }
            if (taper_a.y < -1f)
            {
                taper_a.y = -1f;
            }
            if (taper_a.y > 1f)
            {
                taper_a.y = 1f;
            }
            shapeBlock.PathTaperX = (sbyte)(100 * taper_a.x);
            shapeBlock.PathTaperY = (sbyte)(100 * taper_a.y);
            if (revolutions < 1f)
            {
                revolutions = 1f;
            }
            if (revolutions > 4f)
            {
                revolutions = 4f;
            }
            shapeBlock.PathRevolutions = (byte)(66.666667 * (revolutions - 1.0));
            // limits on radiusoffset depend on revolutions and hole size (how?) seems like the maximum range is 0 to 1
            if (radiusoffset < 0f)
            {
                radiusoffset = 0f;
            }
            if (radiusoffset > 1f)
            {
                radiusoffset = 1f;
            }
            shapeBlock.PathRadiusOffset = (sbyte)(100 * radiusoffset);
            if (skew < -0.95f)
            {
                skew = -0.95f;
            }
            if (skew > 0.95f)
            {
                skew = 0.95f;
            }
            shapeBlock.PathSkew = (sbyte)(100 * skew);

            part.UpdateShape(shapeBlock);
        }

        private void SetPrimitiveShapeParams(SceneObjectPart part, string map, int type)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock();
            UUID sculptId;

            if (!UUID.TryParse(map, out sculptId))
            {
                llSay(0, "Could not parse key " + map);
                return;
            }

            shapeBlock.ObjectLocalID = part.LocalId;
            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 150;

            if (type != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_CYLINDER &&
                type != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_PLANE &&
                type != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE &&
                type != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_TORUS)
            {
                // default
                type = (int)ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE;
            }

            // retain pathcurve
            shapeBlock.PathCurve = part.Shape.PathCurve;

            part.Shape.SetSculptData((byte)type, sculptId);
            part.Shape.SculptEntry = true;
            part.UpdateShape(shapeBlock);
        }

        public void llSetPrimitiveParams(LSL_Types.list rules)
        {
            llSetLinkPrimitiveParams(m_host.LinkNum, rules);
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup == null)
                return;

            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknumber);

            if (part == null)
                return;

            int idx = 0;

            while (idx < rules.Length)
            {
                int code = Convert.ToInt32(rules.Data[idx++].ToString());

                int remain = rules.Length - idx;

                int face;
                LSL_Types.Vector3 v;

                switch (code)
                {
                    case (int)ScriptBaseClass.PRIM_POSITION:
                        if (remain < 1)
                            return;

                        v=new LSL_Types.Vector3(rules.Data[idx++].ToString());
                        SetPos(part, v);

                        break;
                    case (int)ScriptBaseClass.PRIM_SIZE:
                        if (remain < 1)
                            return;

                        v=new LSL_Types.Vector3(rules.Data[idx++].ToString());
                        SetScale(part, v);

                        break;
                    case (int)ScriptBaseClass.PRIM_ROTATION:
                        if (remain < 1)
                            return;

                        LSL_Types.Quaternion q = new LSL_Types.Quaternion(rules.Data[idx++].ToString());
                        SetRot(part, q);

                        break;

                    case (int)ScriptBaseClass.PRIM_TYPE:
                        if (remain < 3)
                            return;

                        code = Convert.ToInt32(rules.Data[idx++].ToString());

                        remain = rules.Length - idx;
                        float hollow;
                        LSL_Types.Vector3 twist;
                        LSL_Types.Vector3 taper_b;
                        LSL_Types.Vector3 topshear;
                        float revolutions;
                        float radiusoffset;
                        float skew;
                        LSL_Types.Vector3 holesize;
                        LSL_Types.Vector3 profilecut;

                        switch (code)
                        {
                            case (int)ScriptBaseClass.PRIM_TYPE_BOX:
                                if (remain < 6)
                                    return;

                                face = (int)rules.GetLSLIntegerItem(idx++);
                                v = rules.GetVector3Item(idx++); // cut
                                hollow = (float)rules.GetLSLFloatItem(idx++);
                                twist = rules.GetVector3Item(idx++);
                                taper_b = rules.GetVector3Item(idx++);
                                topshear = rules.GetVector3Item(idx++);

                                part.Shape.PathCurve = (byte)Extrusion.Straight;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, topshear, 1);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_CYLINDER:
                                if (remain < 6)
                                    return;

                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // holeshape
                                v = new LSL_Types.Vector3(rules.Data[idx++].ToString()); // cut
                                hollow = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                twist = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                taper_b = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                topshear = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                part.Shape.ProfileShape = ProfileShape.Circle;
                                part.Shape.PathCurve = (byte)Extrusion.Straight;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, topshear, 0);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_PRISM:
                                if (remain < 6)
                                    return;

                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // holeshape
                                v = new LSL_Types.Vector3(rules.Data[idx++].ToString()); //cut
                                hollow = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                twist = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                taper_b = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                topshear = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                part.Shape.PathCurve = (byte)Extrusion.Straight;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, topshear, 3);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_SPHERE:
                                if (remain < 5)
                                    return;

                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // holeshape
                                v = new LSL_Types.Vector3(rules.Data[idx++].ToString()); // cut
                                hollow = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                twist = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                taper_b = new LSL_Types.Vector3(rules.Data[idx++].ToString()); // dimple
                                part.Shape.PathCurve = (byte)Extrusion.Curve1;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, 5);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_TORUS:
                                if (remain < 11)
                                    return;

                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // holeshape
                                v = new LSL_Types.Vector3(rules.Data[idx++].ToString()); //cut
                                hollow = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                twist = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                holesize = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                topshear = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                profilecut = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                taper_b = new LSL_Types.Vector3(rules.Data[idx++].ToString()); // taper_a
                                revolutions = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                radiusoffset = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                skew = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                part.Shape.PathCurve = (byte)Extrusion.Curve1;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, holesize, topshear, profilecut, taper_b, revolutions, radiusoffset, skew, 0);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_TUBE:
                                if (remain < 11)
                                    return;

                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // holeshape
                                v = new LSL_Types.Vector3(rules.Data[idx++].ToString()); //cut
                                hollow = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                twist = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                holesize = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                topshear = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                profilecut = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                taper_b = new LSL_Types.Vector3(rules.Data[idx++].ToString()); // taper_a
                                revolutions = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                radiusoffset = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                skew = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                part.Shape.PathCurve = (byte)Extrusion.Curve1;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, holesize, topshear, profilecut, taper_b, revolutions, radiusoffset, skew, 1);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_RING:
                                if (remain < 11)
                                    return;

                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // holeshape
                                v = new LSL_Types.Vector3(rules.Data[idx++].ToString()); //cut
                                hollow = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                twist = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                holesize = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                topshear = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                profilecut = new LSL_Types.Vector3(rules.Data[idx++].ToString());
                                taper_b = new LSL_Types.Vector3(rules.Data[idx++].ToString()); // taper_a
                                revolutions = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                radiusoffset = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                skew = (float)Convert.ToDouble(rules.Data[idx++].ToString());
                                part.Shape.PathCurve = (byte)Extrusion.Curve1;
                                SetPrimitiveShapeParams(part, face, v, hollow, twist, holesize, topshear, profilecut, taper_b, revolutions, radiusoffset, skew, 3);
                                break;

                            case (int)ScriptBaseClass.PRIM_TYPE_SCULPT:
                                if (remain < 2)
                                    return;

                                string map = rules.Data[idx++].ToString();
                                face = Convert.ToInt32(rules.Data[idx++].ToString()); // type
                                part.Shape.PathCurve = (byte)Extrusion.Curve1;
                                SetPrimitiveShapeParams(part, map, face);
                                break;
                        }

                        break;

                    case (int)ScriptBaseClass.PRIM_TEXTURE:
                        if (remain < 5)
                            return;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());
                        string tex=rules.Data[idx++].ToString();
                        LSL_Types.Vector3 repeats=new LSL_Types.Vector3(rules.Data[idx++].ToString());
                        LSL_Types.Vector3 offsets=new LSL_Types.Vector3(rules.Data[idx++].ToString());
                        double rotation=Convert.ToDouble(rules.Data[idx++].ToString());

                        SetTexture(part, tex, face);
                        ScaleTexture(part, repeats.x, repeats.y, face);
                        OffsetTexture(part, offsets.x, offsets.y, face);
                        RotateTexture(part, rotation, face);

                        break;

                    case (int)ScriptBaseClass.PRIM_COLOR:
                        if (remain < 3)
                            return;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());
                        LSL_Types.Vector3 color=new LSL_Types.Vector3(rules.Data[idx++].ToString());
                        double alpha=Convert.ToDouble(rules.Data[idx++].ToString());

                        SetColor(part, color, face);
                        SetAlpha(part, alpha, face);

                        break;
                    case (int)ScriptBaseClass.PRIM_FLEXIBLE:
                        if (remain < 7)
                            return;

                        bool flexi = rules.GetLSLIntegerItem(idx++);
                        int softness = rules.GetLSLIntegerItem(idx++);
                        float gravity = (float)rules.GetLSLFloatItem(idx++);
                        float friction = (float)rules.GetLSLFloatItem(idx++);
                        float wind = (float)rules.GetLSLFloatItem(idx++);
                        float tension = (float)rules.GetLSLFloatItem(idx++);
                        LSL_Types.Vector3 force = rules.GetVector3Item(idx++);

                        SetFlexi(part, flexi, softness, gravity, friction, wind, tension, force);

                        break;
                    case (int)ScriptBaseClass.PRIM_POINT_LIGHT:
                        if (remain < 5)
                            return;
                        bool light = rules.GetLSLIntegerItem(idx++);
                        LSL_Types.Vector3 lightcolor = rules.GetVector3Item(idx++);
                        float intensity = (float)rules.GetLSLFloatItem(idx++);
                        float radius = (float)rules.GetLSLFloatItem(idx++);
                        float falloff = (float)rules.GetLSLFloatItem(idx++);

                        SetPointLight(part, light, lightcolor, intensity, radius, falloff);

                        break;
                    case (int)ScriptBaseClass.PRIM_GLOW:
                        if (remain < 2)
                            return;
                    // TODO: LSL constant ALL_SIDES (value -1) is inserted into LSL_Types.list
                    //       as a raw CLI Int32. When / if this is inserted as an
                    //       LSL_Types.LSLInteger extract value using LSL_Types.list.GetLSLIntegerItem
                    //    face = rules.GetLSLIntegerItem(idx++);
                        face = Convert.ToInt32(rules.Data[idx++].ToString());
                        float glow = (float)rules.GetLSLFloatItem(idx++);

                        SetGlow(part, face, glow);

                        break;
                    case (int)ScriptBaseClass.PRIM_BUMP_SHINY:
                        if (remain < 3)
                            return;
                        face = Convert.ToInt32(rules.Data[idx++].ToString());
                        int shiny = Convert.ToInt32(rules.Data[idx++].ToString());
                        Bumpiness bump = (Bumpiness)Convert.ToByte(rules.Data[idx++].ToString());

                        SetShiny(part, face, shiny, bump);

                        break;
                     case (int)ScriptBaseClass.PRIM_FULLBRIGHT:
                         if (remain < 2)
                             return;
                   // TODO: LSL constant ALL_SIDES (value -1) is inserted into LSL_Types.list
                   //       as a raw CLI Int32. When / if this is inserted as an
                   //       LSL_Types.LSLInteger extract value using LSL_Types.list.GetLSLIntegerItem
                   //      face = rules.GetLSLIntegerItem(idx++);
                         face = Convert.ToInt32(rules.Data[idx++].ToString());
                         bool st = rules.GetLSLIntegerItem(idx++);
                         SetFullBright(part, face , st);
                         break;
                     case (int)ScriptBaseClass.PRIM_MATERIAL:
                         if (remain < 1)
                             return;
                        if (part != null)
                        {
                            /* Unhandled at this time - sends "Unhandled" message
                               will enable when available
                            byte material = (byte)Convert.ToByte( rules.Data[idx++].ToString());
                            part.Material =  material;
                            */
                            return;
                        }
                        break;
                     case (int)ScriptBaseClass.PRIM_PHANTOM:
                        if (remain < 1)
                             return;

                         string ph = rules.Data[idx++].ToString();
                         bool phantom;

                         if (ph.Equals("1"))
                             phantom = true;
                         else
                             phantom = false;

                         part.ScriptSetPhantomStatus(phantom);
                        part.ScheduleFullUpdate();
                        break;
                     case (int)ScriptBaseClass.PRIM_PHYSICS:
                        if (remain < 1)
                             return;
                         string phy = rules.Data[idx++].ToString();
                         bool physics;

                         if (phy.Equals("1"))
                             physics = true;
                         else
                             physics = false;

                         m_host.ScriptSetPhysicsStatus(physics);
                        part.ScheduleFullUpdate();
                        break;
                }
            }
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
            // ScriptSleep(300);
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

        public LSL_Types.list llGetAnimationList( string id )
        {
            m_host.AddScriptLPS(1);

            LSL_Types.list l = new LSL_Types.list();
            ScenePresence av = World.GetScenePresence(id);
            if (av == null)
                return l;
            UUID[] anims;
            anims = av.GetAnimationArray();
            foreach (UUID foo in anims)
                l.Add(foo.ToString());
            return l;
        }

        public void llSetParcelMusicURL(string url)
        {
            m_host.AddScriptLPS(1);
            UUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);
            if (landowner == UUID.Zero)
            {
                return;
            }
            if (landowner != m_host.ObjectOwner)
            {
                return;
            }
            World.SetLandMusicURL(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, url);
            // ScriptSleep(2000);
        }

        public void osSetParcelMediaURL(string url)
        {
            m_host.AddScriptLPS(1);
            UUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if (landowner == UUID.Zero)
            {
                return;
            }

            if (landowner != m_host.ObjectOwner)
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
                Primitive.TextureEntry tex = part.Shape.Textures;
                Color4 texcolor;
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
                    Primitive.TextureEntry tex = part.Shape.Textures;
                    Color4 texcolor;
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
        }

        public LSL_Types.LSLInteger llGetNumberOfPrims()
        {
            m_host.AddScriptLPS(1);
            return m_host.ParentGroup.PrimCount;
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

        public LSL_Types.list llGetPrimitiveParams(LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);

            LSL_Types.list res = new LSL_Types.list();
            int idx=0;
            while (idx < rules.Length)
            {
                int code=Convert.ToInt32(rules.Data[idx++].ToString());
                int remain=rules.Length-idx;

                switch (code)
                {
                    case (int)ScriptBaseClass.PRIM_MATERIAL:
                        res.Add(new LSL_Types.LSLInteger(m_host.Material));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHYSICS:
                        if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) != 0)
                            res.Add(new LSL_Types.LSLInteger(1));
                        else
                            res.Add(new LSL_Types.LSLInteger(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_TEMP_ON_REZ:
                        if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.TemporaryOnRez) != 0)
                            res.Add(new LSL_Types.LSLInteger(1));
                        else
                            res.Add(new LSL_Types.LSLInteger(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHANTOM:
                        if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) != 0)
                            res.Add(new LSL_Types.LSLInteger(1));
                        else
                            res.Add(new LSL_Types.LSLInteger(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_POSITION:
                        res.Add(new LSL_Types.Vector3(m_host.AbsolutePosition.X,
                                                      m_host.AbsolutePosition.Y,
                                                      m_host.AbsolutePosition.Z));
                        break;

                    case (int)ScriptBaseClass.PRIM_SIZE:
                        res.Add(new LSL_Types.Vector3(m_host.Scale.X,
                                                      m_host.Scale.Y,
                                                      m_host.Scale.Z));
                        break;

                    case (int)ScriptBaseClass.PRIM_ROTATION:
                        res.Add(new LSL_Types.Quaternion(m_host.RotationOffset.X,
                                                         m_host.RotationOffset.Y,
                                                         m_host.RotationOffset.Z,
                                                         m_host.RotationOffset.W));
                        break;

                    case (int)ScriptBaseClass.PRIM_TYPE:
                        // implementing box
                        PrimitiveBaseShape Shape = m_host.Shape;
                        int primType = getScriptPrimType(m_host.Shape);
                        res.Add(new LSL_Types.LSLInteger(primType));
                        switch (primType)
                        {
                            case ScriptBaseClass.PRIM_TYPE_BOX:
                            case ScriptBaseClass.PRIM_TYPE_CYLINDER:
                            case ScriptBaseClass.PRIM_TYPE_PRISM:
                                res.Add(new LSL_Types.LSLInteger(Shape.ProfileCurve));
                                res.Add(new LSL_Types.Vector3(Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));
                                res.Add(new LSL_Types.LSLFloat(Shape.ProfileHollow / 50000.0));
                                res.Add(new LSL_Types.Vector3(Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));
                                res.Add(new LSL_Types.Vector3(1 - (Shape.PathScaleX / 100.0 - 1), 1 - (Shape.PathScaleY / 100.0 - 1), 0));
                                res.Add(new LSL_Types.Vector3(Shape.PathShearX / 100.0, Shape.PathShearY / 100.0, 0));
                                break;

                            case ScriptBaseClass.PRIM_TYPE_SPHERE:
                                res.Add(new LSL_Types.LSLInteger(Shape.ProfileCurve));
                                res.Add(new LSL_Types.Vector3(Shape.PathBegin / 50000.0, 1 - Shape.PathEnd / 50000.0, 0));
                                res.Add(new LSL_Types.LSLFloat(Shape.ProfileHollow / 50000.0));
                                res.Add(new LSL_Types.Vector3(Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));
                                res.Add(new LSL_Types.Vector3(Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));
                                break;

                            case ScriptBaseClass.PRIM_TYPE_SCULPT:
                                res.Add(Shape.SculptTexture.ToString());
                                res.Add(new LSL_Types.LSLInteger(Shape.SculptType));
                                break;

                            case ScriptBaseClass.PRIM_TYPE_RING:
                            case ScriptBaseClass.PRIM_TYPE_TUBE:
                            case ScriptBaseClass.PRIM_TYPE_TORUS:
                                // holeshape
                                res.Add(new LSL_Types.LSLInteger(Shape.ProfileCurve));

                                // cut
                                res.Add(new LSL_Types.Vector3(Shape.PathBegin / 50000.0, 1 - Shape.PathEnd / 50000.0, 0));

                                // hollow
                                res.Add(new LSL_Types.LSLFloat(Shape.ProfileHollow / 50000.0));

                                // twist
                                res.Add(new LSL_Types.Vector3(Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));

                                // vector holesize
                                res.Add(new LSL_Types.Vector3(1 - (Shape.PathScaleX / 100.0 - 1), 1 - (Shape.PathScaleY / 100.0 - 1), 0));

                                // vector topshear
                                res.Add(new LSL_Types.Vector3(Shape.PathShearX / 100.0, Shape.PathShearY / 100.0, 0));

                                // vector profilecut
                                res.Add(new LSL_Types.Vector3(Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));


                                // vector tapera
                                res.Add(new LSL_Types.Vector3(Shape.PathTaperX / 100.0, Shape.PathTaperY / 100.0, 0));

                                // float revolutions, 
                                res.Add(new LSL_Types.LSLFloat(Shape.PathRevolutions / 50.0)); // needs fixing :(

                                // float radiusoffset, 
                                res.Add(new LSL_Types.LSLFloat(Shape.PathRadiusOffset / 100.0));

                                // float skew
                                res.Add(new LSL_Types.LSLFloat(Shape.PathSkew / 100.0));
                                break;

                        }
                        break;

                    case (int)ScriptBaseClass.PRIM_TEXTURE:
                        if (remain < 1)
                            return res;

                        int face = Convert.ToInt32(rules.Data[idx++].ToString());
                        if (face == -1)
                            face = 0;

                        Primitive.TextureEntry tex = m_host.Shape.Textures;
                        Primitive.TextureEntryFace texface = tex.GetFace((uint)face);

                        res.Add(new LSL_Types.LSLString(texface.TextureID.ToString()));
                        res.Add(new LSL_Types.Vector3(texface.RepeatU,
                                                      texface.RepeatV,
                                                      0));
                        res.Add(new LSL_Types.Vector3(texface.OffsetU,
                                                      texface.OffsetV,
                                                      0));
                        res.Add(new LSL_Types.LSLFloat(texface.Rotation));
                        break;

                    case (int)ScriptBaseClass.PRIM_COLOR:
                        if (remain < 1)
                            return res;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());

                        tex = m_host.Shape.Textures;
                        Color4 texcolor;
                        if (face == -1) // TMP: Until we can determine number of sides, ALL_SIDES (-1) will return default color
                            texcolor = tex.DefaultTexture.RGBA;
                        else
                            texcolor = tex.GetFace((uint)face).RGBA;
                        res.Add(new LSL_Types.Vector3((255 - (texcolor.R * 255)) / 255,
                                                      (255 - (texcolor.G * 255)) / 255,
                                                      (255 - (texcolor.B * 255)) / 255));
                        res.Add(new LSL_Types.LSLFloat((texcolor.A * 255) / 255));
                        break;

                    case (int)ScriptBaseClass.PRIM_BUMP_SHINY:
                        // TODO--------------
                        if (remain < 1)
                            return res;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());

                        res.Add(new LSL_Types.LSLInteger(0));
                        res.Add(new LSL_Types.LSLInteger(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_FULLBRIGHT:
                        // TODO--------------
                        if (remain < 1)
                            return res;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());

                        res.Add(new LSL_Types.LSLInteger(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_FLEXIBLE:
                        PrimitiveBaseShape shape = m_host.Shape;

                        if (shape.FlexiEntry)
                            res.Add(new LSL_Types.LSLInteger(1));              // active
                        else
                            res.Add(new LSL_Types.LSLInteger(0));
                        res.Add(new LSL_Types.LSLInteger(shape.FlexiSoftness));// softness
                        res.Add(new LSL_Types.LSLFloat(shape.FlexiGravity));   // gravity
                        res.Add(new LSL_Types.LSLFloat(shape.FlexiDrag));      // friction
                        res.Add(new LSL_Types.LSLFloat(shape.FlexiWind));      // wind
                        res.Add(new LSL_Types.LSLFloat(shape.FlexiTension));   // tension
                        res.Add(new LSL_Types.Vector3(shape.FlexiForceX,       // force
                                                      shape.FlexiForceY,
                                                      shape.FlexiForceZ));
                        break;

                    case (int)ScriptBaseClass.PRIM_TEXGEN:
                        // TODO--------------
                        // (PRIM_TEXGEN_DEFAULT, PRIM_TEXGEN_PLANAR)
                        if (remain < 1)
                            return res;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());

                        res.Add(new LSL_Types.LSLInteger(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_POINT_LIGHT:
                        shape = m_host.Shape;

                        if (shape.LightEntry)
                            res.Add(new LSL_Types.LSLInteger(1));              // active
                        else
                            res.Add(new LSL_Types.LSLInteger(0));
                        res.Add(new LSL_Types.Vector3(shape.LightColorR,       // color
                                                      shape.LightColorG,
                                                      shape.LightColorB));
                        res.Add(new LSL_Types.LSLFloat(shape.LightIntensity)); // intensity
                        res.Add(new LSL_Types.LSLFloat(shape.LightRadius));    // radius
                        res.Add(new LSL_Types.LSLFloat(shape.LightFalloff));   // falloff
                        break;

                    case (int)ScriptBaseClass.PRIM_GLOW:
                        // TODO--------------
                        if (remain < 1)
                            return res;

                        face=Convert.ToInt32(rules.Data[idx++].ToString());

                        res.Add(new LSL_Types.LSLFloat(0));
                        break;
                }
            }
            return res;
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

        public LSL_Types.LSLInteger llBase64ToInteger(string str)
        {
            int number = 0;
            int digit;

            m_host.AddScriptLPS(1);

            //    Require a well-fromed base64 string

            if (str.Length > 8)
                return 0;

            //    The loop is unrolled in the interests
            //    of performance and simple necessity.
            //
            //    MUST find 6 digits to be well formed
            //      -1 == invalid
            //       0 == padding

            if ((digit=c2itable[str[0]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<26;

            if ((digit=c2itable[str[1]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<20;

            if ((digit=c2itable[str[2]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<14;

            if ((digit=c2itable[str[3]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<8;

            if ((digit=c2itable[str[4]])<=0)
            {
                return digit<0?(int)0:number;
            }
            number += --digit<<2;

            if ((digit=c2itable[str[5]])<=0)
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
            m_host.RotationOffset = new Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s);
            // ScriptSleep(200);
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

            for (int i = 0; i < mlen; i++)
                active[i] = true;

            offset[mlen] = srclen;

            while (beginning < srclen)
            {

                best = mlen;    // as bad as it gets

                //    Scan for separators

                for (j = 0; j < seplen; j++)
                {
                    if (active[j])
                    {
                        // scan all of the markers
                        if ((offset[j] = src.IndexOf((string)separray[j],beginning)) == -1)
                        {
                            // not present at all
                            active[j] = false;
                        }
                        else
                        {
                            // present and correct
                            if (offset[j] < offset[best])
                            {
                                // closest so far
                                best = j;
                                if (offset[best] == beginning)
                                    break;
                            }
                        }
                    }
                }

                //    Scan for spacers

                if (offset[best] != beginning)
                {
                    for (j = seplen; (j < mlen) && (offset[best] > beginning); j++)
                    {
                        if (active[j])
                        {
                            // scan all of the markers
                            if ((offset[j] = src.IndexOf((string)spcarray[j-seplen], beginning)) == -1)
                            {
                                // not present at all
                                active[j] = false;
                            }
                            else
                            {
                                // present and correct
                                if (offset[j] < offset[best])
                                {
                                    // closest so far
                                    best = j;
                                }
                            }
                        }
                    }
                }

                //    This is the normal exit from the scanning loop

                if (best == mlen)
                {
                    // no markers were found on this pass
                    // so we're pretty much done
                    tokens.Add(src.Substring(beginning, srclen - beginning));
                    break;
                }

                //    Otherwise we just add the newly delimited token
                //    and recalculate where the search should continue.

                tokens.Add(src.Substring(beginning,offset[best]-beginning));

                if (best < seplen)
                {
                    beginning = offset[best] + ((string)separray[best]).Length;
                }
                else
                {
                    beginning = offset[best] + ((string)spcarray[best - seplen]).Length;
                    tokens.Add(spcarray[best - seplen]);
                }
            }

            //    This an awkward an not very intuitive boundary case. If the
            //    last substring is a tokenizer, then there is an implied trailing
            //    null list entry. Hopefully the single comparison will not be too
            //    arduous. Alternatively the 'break' could be replced with a return
            //    but that's shabby programming.

            if (beginning == srclen)
            {
                if (srclen != 0)
                    tokens.Add("");
            }

            return tokens;
        }

        public LSL_Types.LSLInteger llGetObjectPermMask(int mask)
        {
            m_host.AddScriptLPS(1);

            int permmask = 0;

            if (mask == ScriptBaseClass.MASK_BASE)//0
            {
                permmask = (int)m_host.BaseMask;
            }

            else if (mask == ScriptBaseClass.MASK_OWNER)//1
            {
                permmask = (int)m_host.OwnerMask;
            }

            else if (mask == ScriptBaseClass.MASK_GROUP)//2
            {
                permmask = (int)m_host.GroupMask;
            }

            else if (mask == ScriptBaseClass.MASK_EVERYONE)//3
            {
                permmask = (int)m_host.EveryoneMask;
            }

            else if (mask == ScriptBaseClass.MASK_NEXT)//4
            {
                permmask = (int)m_host.NextOwnerMask;
            }

            return permmask;
        }

        public void llSetObjectPermMask(int mask, int value)
        {
            m_host.AddScriptLPS(1);
            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["XEngine"] == null)
                config.AddConfig("XEngine");

            if (config.Configs["XEngine"].GetBoolean("AllowGodFunctions", false))
            {
                if (World.ExternalChecks.ExternalChecksCanRunConsoleCommand(m_host.OwnerID))
                {
                    if (mask == ScriptBaseClass.MASK_BASE)//0
                    {
                        m_host.BaseMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_OWNER)//1
                    {
                        m_host.OwnerMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_GROUP)//2
                    {
                        m_host.GroupMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_EVERYONE)//3
                    {
                        m_host.EveryoneMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_NEXT)//4
                    {
                        m_host.NextOwnerMask = (uint)value;
                    }
                }
            }
        }

        public LSL_Types.LSLInteger llGetInventoryPermMask(string item, int mask)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Name == item)
                {
                    switch (mask)
                    {
                        case 0:
                            return (int)inv.Value.BasePermissions;
                        case 1:
                            return (int)inv.Value.CurrentPermissions;
                        case 2:
                            return (int)inv.Value.GroupPermissions;
                        case 3:
                            return (int)inv.Value.EveryonePermissions;
                        case 4:
                            return (int)inv.Value.NextPermissions;
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
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
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
            m_host.AddScriptLPS(1);

            World.SimChatBroadcast(Utils.StringToBytes(msg), ChatTypeEnum.Owner, 0, m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);
//            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
//            wComm.DeliverMessage(ChatTypeEnum.Owner, 0, m_host.Name, m_host.UUID, msg);
        }

        public string llRequestSimulatorData(string simulator, int data)
        {
            try
            {
                m_host.AddScriptLPS(1);

                string reply = String.Empty;

                RegionInfo info = m_ScriptEngine.World.RequestClosestRegion(simulator);

                switch (data)
                {
                    case 5: // DATA_SIM_POS
                        if (info == null)
                        {
                            // ScriptSleep(1000);
                            return UUID.Zero.ToString();
                        }
                        reply = new LSL_Types.Vector3(
                            info.RegionLocX * Constants.RegionSize,
                            info.RegionLocY * Constants.RegionSize,
                            0).ToString();
                        break;
                    case 6: // DATA_SIM_STATUS
                        if (info != null)
                            reply = "up"; // Duh!
                        else
                            reply = "unknown";
                        break;
                    case 7: // DATA_SIM_RATING
                        if (info == null)
                        {
                            // ScriptSleep(1000);
                            return UUID.Zero.ToString();
                        }
                        int access = info.RegionSettings.Maturity;
                        if (access == 0)
                            reply = "PG";
                        else if (access == 1)
                            reply = "MATURE";
                        else
                            reply = "UNKNOWN";
                        break;
                    case 128: // SIM_RELEASE
                        reply = m_ScriptEngine.World.GetSimulatorVersion();
                        break;
                    default:
                        // ScriptSleep(1000);
                        return UUID.Zero.ToString(); // Raise no event
                }
                UUID rq = UUID.Random();

                UUID tid = AsyncCommands.
                    DataserverPlugin.RegisterRequest(m_localID, m_itemID, rq.ToString());

                AsyncCommands.
                    DataserverPlugin.DataserverReply(rq.ToString(), reply);

                // ScriptSleep(1000);
                return tid.ToString();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                return UUID.Zero.ToString();
            }
        }

        public void llForceMouselook(int mouselook)
        {
            m_host.AddScriptLPS(1);
            m_host.SetForceMouselook(mouselook != 0);
        }

        public double llGetObjectMass(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id,out key))
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
            if (start < 0)
            {
                start = start+dest.Length;
            }

            if (end < 0)
            {
                end = end+dest.Length;
            }
            // The comventional case, remove a sequence starting with
            // start and ending with end. And then insert the source
            // list.
            if (start <= end)
            {
                // If greater than zero, then there is going to be a
                // surviving prefix. Otherwise the inclusive nature
                // of the indices mean that we're going to add the
                // source list as a prefix.
                if (start > 0)
                {
                    pref = dest.GetSublist(0,start-1);
                    // Only add a suffix if there is something
                    // beyond the end index (it's inclusive too).
                    if (end + 1 < dest.Length)
                    {
                        return pref + src + dest.GetSublist(end + 1, -1);
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
                    if (end + 1 < dest.Length)
                    {
                        return src + dest.GetSublist(end + 1, -1);
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
                return dest.GetSublist(end + 1, start - 1) + src;
            }
        }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            m_host.AddScriptLPS(1);
            UUID avatarId = new UUID(avatar_id);
            m_ScriptEngine.World.SendUrlToUser(avatarId, m_host.Name, m_host.UUID, m_host.ObjectOwner, false, message,
                                               url);
            // ScriptSleep(10000);
        }

        public void llParcelMediaCommandList(LSL_Types.list commandList)
        {
            //TO DO: Implement the missing commands
            //PARCEL_MEDIA_COMMAND_STOP        Stop the media stream and go back to the first frame.
            //PARCEL_MEDIA_COMMAND_PAUSE       Pause the media stream (stop playing but stay on current frame).
            //PARCEL_MEDIA_COMMAND_PLAY        Start the media stream playing from the current frame and stop when the end is reached.
            //PARCEL_MEDIA_COMMAND_LOOP        Start the media stream playing from the current frame. When the end is reached, loop to the beginning and continue.
            //PARCEL_MEDIA_COMMAND_TEXTURE     key uuid        Use this to get or set the parcel's media texture.
            //PARCEL_MEDIA_COMMAND_URL         string url      Used to get or set the parcel's media url.
            //PARCEL_MEDIA_COMMAND_TIME        float time      Move a media stream to a specific time.
            //PARCEL_MEDIA_COMMAND_AGENT       key uuid        Applies the media command to the specified agent only.
            //PARCEL_MEDIA_COMMAND_UNLOAD      Completely unloads the movie and restores the original texture.
            //PARCEL_MEDIA_COMMAND_AUTO_ALIGN  integer boolean         Sets the parcel option 'Auto scale content'.
            //PARCEL_MEDIA_COMMAND_TYPE        string mime_type        Use this to get or set the parcel media MIME type (e.g. "text/html"). (1.19.1 RC0 or later)
            //PARCEL_MEDIA_COMMAND_SIZE        integer x, integer y    Use this to get or set the parcel media pixel resolution. (1.19.1 RC0 or later)
            //PARCEL_MEDIA_COMMAND_DESC        string desc     Use this to get or set the parcel media description. (1.19.1 RC0 or later)
            //PARCEL_MEDIA_COMMAND_LOOP_SET    float loop      Use this to get or set the parcel's media loop duration. (1.19.1 RC0 or later)
            m_host.AddScriptLPS(1);
            for (int i = 0; i < commandList.Data.Length; i++)
            {
                switch ((ParcelMediaCommandEnum)commandList.Data[i])
                {
                    case ParcelMediaCommandEnum.Play:
                        List<ScenePresence> scenePresencePlayList = World.GetScenePresences();
                        foreach (ScenePresence agent in scenePresencePlayList)
                        {
                            if (!agent.IsChildAgent)
                            {
                                agent.ControllingClient.SendParcelMediaCommand((uint)(4), ParcelMediaCommandEnum.Play, 0);
                            }
                        }
                        break;
                    case ParcelMediaCommandEnum.Stop:
                        List<ScenePresence> scenePresenceStopList = World.GetScenePresences();
                        foreach (ScenePresence agent in scenePresenceStopList)
                        {
                            if (!agent.IsChildAgent)
                            {
                                agent.ControllingClient.SendParcelMediaCommand((uint)(4), ParcelMediaCommandEnum.Stop, 0);
                            }
                        }
                        break;
                    case ParcelMediaCommandEnum.Pause:
                        List<ScenePresence> scenePresencePauseList = World.GetScenePresences();
                        foreach (ScenePresence agent in scenePresencePauseList)
                        {
                            if (!agent.IsChildAgent)
                            {
                                agent.ControllingClient.SendParcelMediaCommand((uint)(4), ParcelMediaCommandEnum.Pause, 0);
                            }
                        }
                        break;

                    case ParcelMediaCommandEnum.Url:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is string)
                            {
                                //Set the new media URL only if the user is the owner of the land
                                osSetParcelMediaURL(commandList.Data[i + 1].ToString());

                                List<ScenePresence> scenePresenceList = World.GetScenePresences();
                                LandData landData = World.GetLandData(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);
                                //Send an update of the mediaURL to all the clients that are in the parcel
                                foreach (ScenePresence agent in scenePresenceList)
                                {
                                    if (!agent.IsChildAgent)
                                    {
                                        //Send parcel media update to the client
                                        agent.ControllingClient.SendParcelMediaUpdate(landData.MediaURL, landData.MediaID, landData.MediaAutoScale, "", landData.Description, 0, 0, 1);
                                    }
                                }

                            }
                            i++;
                        }
                        break;
                    default:
                        ParcelMediaCommandEnum mediaCommandEnum = ParcelMediaCommandEnum.Url;
                        NotImplemented("llParcelMediaCommandList parameter do not supported yet: " + Enum.Parse(mediaCommandEnum.GetType(), commandList.Data[i].ToString()).ToString());
                        break;
                }//end switch

            }
            // ScriptSleep(2000);

            //NotImplemented("llParcelMediaCommandList");
        }

        public LSL_Types.list llParcelMediaQuery(LSL_Types.list aList)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list list = new LSL_Types.list();
            //TO DO: make the implementation for the missing commands
            //PARCEL_MEDIA_COMMAND_TEXTURE     key uuid        Use this to get or set the parcel's media texture.
            //PARCEL_MEDIA_COMMAND_URL         string url      Used to get or set the parcel's media url.
            //PARCEL_MEDIA_COMMAND_TYPE        string mime_type        Use this to get or set the parcel media MIME type (e.g. "text/html"). (1.19.1 RC0 or later)
            //PARCEL_MEDIA_COMMAND_SIZE        integer x, integer y    Use this to get or set the parcel media pixel resolution. (1.19.1 RC0 or later)
            //PARCEL_MEDIA_COMMAND_DESC        string desc     Use this to get or set the parcel media description. (1.19.1 RC0 or later)
            //PARCEL_MEDIA_COMMAND_LOOP_SET    float loop      Use this to get or set the parcel's media loop duration. (1.19.1 RC0 or later)
            for (int i = 0; i < aList.Data.Length; i++)
            {

                if (aList.Data[i] != null)
                {
                    switch ((ParcelMediaCommandEnum) aList.Data[i])
                    {
                        case ParcelMediaCommandEnum.Url:
                            list.Add(World.GetLandData(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).MediaURL);
                            break;
                        case ParcelMediaCommandEnum.Desc:
                            list.Add(World.GetLandData(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).Description);
                            break;
                        case ParcelMediaCommandEnum.Texture:
                            list.Add(World.GetLandData(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).MediaID);
                            break;
                        default:
                            ParcelMediaCommandEnum mediaCommandEnum = ParcelMediaCommandEnum.Url;
                            NotImplemented("llParcelMediaQuery parameter do not supported yet: " + Enum.Parse(mediaCommandEnum.GetType() , aList.Data[i].ToString()).ToString());
                            break;
                    }

                }
            }
            // ScriptSleep(2000);
            return list;
        }

        public LSL_Types.LSLInteger llModPow(int a, int b, int c)
        {
            m_host.AddScriptLPS(1);
            Int64 tmp = 0;
            Math.DivRem(Convert.ToInt64(Math.Pow(a, b)), c, out tmp);
            // ScriptSleep(1000);
            return Convert.ToInt32(tmp);
        }

        public LSL_Types.LSLInteger llGetInventoryType(string name)
        {
            m_host.AddScriptLPS(1);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
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

            if (quick_pay_buttons.Data.Length != 4)
            {
                LSLError("List must have 4 elements");
                return;
            }
            m_host.ParentGroup.RootPart.PayPrice[0]=price;

            m_host.ParentGroup.RootPart.PayPrice[1]=(LSL_Types.LSLInteger)quick_pay_buttons.Data[0];
            m_host.ParentGroup.RootPart.PayPrice[2]=(LSL_Types.LSLInteger)quick_pay_buttons.Data[1];
            m_host.ParentGroup.RootPart.PayPrice[3]=(LSL_Types.LSLInteger)quick_pay_buttons.Data[2];
            m_host.ParentGroup.RootPart.PayPrice[4]=(LSL_Types.LSLInteger)quick_pay_buttons.Data[3];
            m_host.ParentGroup.HasGroupChanged = true;
        }

        public LSL_Types.Vector3 llGetCameraPos()
        {
            m_host.AddScriptLPS(1);
            UUID invItemID=InventorySelf();
            if (invItemID == UUID.Zero)
                return new LSL_Types.Vector3();
            if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero)
               return new LSL_Types.Vector3();
            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                ShoutError("No permissions to track the camera");
                return new LSL_Types.Vector3();
            }
            ScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            if (presence != null)
            {
                LSL_Types.Vector3 pos = new LSL_Types.Vector3(presence.CameraPosition.X, presence.CameraPosition.Y, presence.CameraPosition.Z);
                return pos;
            }
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
            // ScriptSleep(2000);
        }

        public void llRefreshPrimURL()
        {
            m_host.AddScriptLPS(1);
            NotImplemented("llRefreshPrimURL");
            // ScriptSleep(20000);
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
            // ScriptSleep(1000);
        }

        public void llAddToLandBanList(string avatar, double hours)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.OwnerID == m_host.OwnerID)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                if (UUID.TryParse(avatar, out key))
                {
                    entry.AgentID = key;
                    entry.Flags = ParcelManager.AccessList.Ban;
                    entry.Time = DateTime.Now.AddHours(hours);
                    land.ParcelAccessList.Add(entry);
                }
            }
            // ScriptSleep(100);
        }

        public void llRemoveFromLandPassList(string avatar)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.OwnerID == m_host.OwnerID)
            {
                if (UUID.TryParse(avatar, out key))
                {
                    foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList)
                    {
                        if (entry.AgentID == key && entry.Flags == ParcelManager.AccessList.Access)
                        {
                            land.ParcelAccessList.Remove(entry);
                            break;
                        }
                    }
                }
            }
            // ScriptSleep(100);
        }

        public void llRemoveFromLandBanList(string avatar)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.OwnerID == m_host.OwnerID)
            {
                if (UUID.TryParse(avatar, out key))
                {
                    foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList)
                    {
                        if (entry.AgentID == key && entry.Flags == ParcelManager.AccessList.Ban)
                        {
                            land.ParcelAccessList.Remove(entry);
                            break;
                        }
                    }
                }
            }
            // ScriptSleep(100);
        }

        public void llSetCameraParams(LSL_Types.list rules)
        {
            m_host.AddScriptLPS(1);

            // our key in the object we are in
            UUID invItemID=InventorySelf();
            if (invItemID == UUID.Zero) return;

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if(objectID == UUID.Zero) return;

            // we need the permission first, to know which avatar we want to set the camera for
            UUID agentID = m_host.TaskInventory[invItemID].PermsGranter;
            if (agentID == UUID.Zero) return;
            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0) return;

            ScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent) return;

            SortedDictionary<int, float> parameters = new SortedDictionary<int, float>();
            object[] data = rules.Data;
            for (int i = 0; i < data.Length; ++i) {
                int type = Convert.ToInt32(data[i++].ToString());
                if (i >= data.Length) break; // odd number of entries => ignore the last

                // some special cases: Vector parameters are split into 3 float parameters (with type+1, type+2, type+3)
                switch (type) {
                case ScriptBaseClass.CAMERA_FOCUS:
                case ScriptBaseClass.CAMERA_FOCUS_OFFSET:
                case ScriptBaseClass.CAMERA_POSITION:
                    LSL_Types.Vector3 v = (LSL_Types.Vector3)data[i];
                    parameters.Add(type + 1, (float)v.x);
                    parameters.Add(type + 2, (float)v.y);
                    parameters.Add(type + 3, (float)v.z);
                    break;
                default:
                    // TODO: clean that up as soon as the implicit casts are in
                    if (data[i] is LSL_Types.LSLFloat)
                        parameters.Add(type, (float)((LSL_Types.LSLFloat)data[i]).value);
                    else if (data[i] is LSL_Types.LSLInteger)
                        parameters.Add(type, (float)((LSL_Types.LSLInteger)data[i]).value);
                    else parameters.Add(type, Convert.ToSingle(data[i]));
                    break;
                }
            }
            if (parameters.Count > 0) presence.ControllingClient.SendSetFollowCamProperties(objectID, parameters);
        }

        public void llClearCameraParams()
        {
            m_host.AddScriptLPS(1);

            // our key in the object we are in
            UUID invItemID=InventorySelf();
            if (invItemID == UUID.Zero) return;

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if(objectID == UUID.Zero) return;

            // we need the permission first, to know which avatar we want to clear the camera for
            UUID agentID = m_host.TaskInventory[invItemID].PermsGranter;
            if (agentID == UUID.Zero) return;
            if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0) return;

            ScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent) return;

            presence.ControllingClient.SendClearFollowCamProperties(objectID);
        }

        public double llListStatistics(int operation, LSL_Types.list src)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list nums = LSL_Types.list.ToDoubleList(src);
            switch (operation)
            {
                case ScriptBaseClass.LIST_STAT_RANGE:
                    return nums.Range();
                case ScriptBaseClass.LIST_STAT_MIN:
                    return nums.Min();
                case ScriptBaseClass.LIST_STAT_MAX:
                    return nums.Max();
                case ScriptBaseClass.LIST_STAT_MEAN:
                    return nums.Mean();
                case ScriptBaseClass.LIST_STAT_MEDIAN:
                    return nums.Median();
                case ScriptBaseClass.LIST_STAT_NUM_COUNT:
                    return nums.NumericLength();
                case ScriptBaseClass.LIST_STAT_STD_DEV:
                    return nums.StdDev();
                case ScriptBaseClass.LIST_STAT_SUM:
                    return nums.Sum();
                case ScriptBaseClass.LIST_STAT_SUM_SQUARES:
                    return nums.SumSqrs();
                case ScriptBaseClass.LIST_STAT_GEOMETRIC_MEAN:
                    return nums.GeometricMean();
                case ScriptBaseClass.LIST_STAT_HARMONIC_MEAN:
                    return nums.HarmonicMean();
                default:
                    return 0.0;
            }
        }

        public LSL_Types.LSLInteger llGetUnixTime()
        {
            m_host.AddScriptLPS(1);
            return Util.UnixTimeSinceEpoch();
        }

        public LSL_Types.LSLInteger llGetParcelFlags(LSL_Types.Vector3 pos)
        {
            m_host.AddScriptLPS(1);
            return (int)World.LandChannel.GetLandObject((float)pos.x, (float)pos.y).landData.Flags;
        }

        public LSL_Types.LSLInteger llGetRegionFlags()
        {
            m_host.AddScriptLPS(1);
            IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
            if (estate == null)
                return 67108864;
            return (int)estate.GetRegionFlags();
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
                if (c >= src2.Length)
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

            Vector3 position = m_host.AbsolutePosition;
            Vector3 velocity = m_host.Velocity;
            Quaternion rotation = m_host.RotationOffset;
            ScenePresence scenePresence = World.GetScenePresence(m_host.ObjectOwner);
            RegionInfo regionInfo = World.RegionInfo;

            Dictionary<string, string> httpHeaders = new Dictionary<string, string>();

            httpHeaders["X-SecondLife-Shard"] = "OpenSim";
            httpHeaders["X-SecondLife-Object-Name"] = m_host.Name;
            httpHeaders["X-SecondLife-Object-Key"] = m_itemID.ToString();
            httpHeaders["X-SecondLife-Region"] = string.Format("{0} ({1}, {2})", regionInfo.RegionName, regionInfo.RegionLocX, regionInfo.RegionLocY);
            httpHeaders["X-SecondLife-Local-Position"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})", position.X, position.Y, position.Z);
            httpHeaders["X-SecondLife-Local-Velocity"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})", velocity.X, velocity.Y, velocity.Z);
            httpHeaders["X-SecondLife-Local-Rotation"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000})", rotation.X, rotation.Y, rotation.Z, rotation.W);
            httpHeaders["X-SecondLife-Owner-Name"] = scenePresence == null ? string.Empty : scenePresence.ControllingClient.Name;
            httpHeaders["X-SecondLife-Owner-Key"] = m_host.ObjectOwner.ToString();

            UUID reqID = httpScriptMod.
                StartHttpRequest(m_localID, m_itemID, url, param, httpHeaders, body);

            if (reqID != UUID.Zero)
                return reqID.ToString();
            else
                return null;
        }

        public void llResetLandBanList()
        {
            m_host.AddScriptLPS(1);
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.OwnerID == m_host.OwnerID)
            {
                foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList)
                {
                    if (entry.Flags == ParcelManager.AccessList.Ban)
                    {
                        land.ParcelAccessList.Remove(entry);
                    }
                }
            }
            // ScriptSleep(100);
        }

        public void llResetLandPassList()
        {
            m_host.AddScriptLPS(1);
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).landData;
            if (land.OwnerID == m_host.OwnerID)
            {
                foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList)
                {
                    if (entry.Flags == ParcelManager.AccessList.Access)
                    {
                        land.ParcelAccessList.Remove(entry);
                    }
                }
            }
            // ScriptSleep(100);
        }

        public LSL_Types.LSLInteger llGetParcelPrimCount(LSL_Types.Vector3 pos, int category, int sim_wide)
        {
            m_host.AddScriptLPS(1);

            LandData land = World.GetLandData((float)pos.x, (float)pos.y);

            if (land == null)
            {
                return 0;
            }

            else
            {
                if (sim_wide == 1)
                {
                    if (category == 0)
                    {
                        return land.SimwidePrims;
                    }

                    else
                    {
                        //public int simwideArea = 0;
                        return 0;
                    }
                }

                else
                {
                    if (category == 0)//Total Prims
                    {
                        return 0;//land.
                    }

                    else if (category == 1)//Owner Prims
                    {
                        return land.OwnerPrims;
                    }

                    else if (category == 2)//Group Prims
                    {
                        return land.GroupPrims;
                    }

                    else if (category == 3)//Other Prims
                    {
                        return land.OtherPrims;
                    }

                    else if (category == 4)//Selected
                    {
                        return land.SelectedPrims;
                    }

                    else if (category == 5)//Temp
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
            LandObject land = (LandObject)World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);
            LSL_Types.list ret = new LSL_Types.list();
            if (land != null)
            {
                foreach (KeyValuePair<UUID, int> d in land.getLandObjectOwners())
                {
                    ret.Add(d.Key.ToString());
                    ret.Add(d.Value);
                }
            }
            // ScriptSleep(2000);
            return ret;
        }

        public LSL_Types.LSLInteger llGetObjectPrimCount(string object_id)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = World.GetSceneObjectPart(new UUID(object_id));
            if (part == null)
            {
                return 0;
            }
            else
            {
                return part.ParentGroup.Children.Count;
            }
        }

        public LSL_Types.LSLInteger llGetParcelMaxPrims(LSL_Types.Vector3 pos, int sim_wide)
        {
            m_host.AddScriptLPS(1);
            // Alondria: This currently just is utilizing the normal grid's 0.22 prims/m2 calculation
            // Which probably will be irrelevent in OpenSim....
            LandData land = World.GetLandData((float)pos.x, (float)pos.y);

            float bonusfactor = (float)World.RegionInfo.RegionSettings.ObjectBonus;

            if (land == null)
            {
                return 0;
            }

            if (sim_wide == 1)
            {
                decimal v = land.SimwideArea * (decimal)(0.22) * (decimal)bonusfactor;

                return (int)v;
            }

            else
            {
                decimal v = land.Area * (decimal)(0.22) * (decimal)bonusfactor;

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
                        ret = ret + new LSL_Types.list(land.Name);
                        break;
                    case "1":
                        ret = ret + new LSL_Types.list(land.Description);
                        break;
                    case "2":
                        ret = ret + new LSL_Types.list(land.OwnerID.ToString());
                        break;
                    case "3":
                        ret = ret + new LSL_Types.list(land.GroupID.ToString());
                        break;
                    case "4":
                        ret = ret + new LSL_Types.list(land.Area);
                        break;
                    default:
                        ret = ret + new LSL_Types.list(0);
                        break;
                }
            }
            return ret;
        }

        public void llSetLinkTexture(int linknumber, string texture, int face)
        {
            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup == null)
                return;

            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknumber);

            if (part == null)
                return;

            SetTexture(part, texture, face);
            // ScriptSleep(200);
        }

        public string llStringTrim(string src, int type)
        {
            m_host.AddScriptLPS(1);
            if (type == (int)ScriptBaseClass.STRING_TRIM_HEAD) { return src.TrimStart(); }
            if (type == (int)ScriptBaseClass.STRING_TRIM_TAIL) { return src.TrimEnd(); }
            if (type == (int)ScriptBaseClass.STRING_TRIM) { return src.Trim(); }
            return src;
        }

        public LSL_Types.list llGetObjectDetails(string id, LSL_Types.list args)
        {
            m_host.AddScriptLPS(1);
            LSL_Types.list ret = new LSL_Types.list();
            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
                ScenePresence av = World.GetScenePresence(key);

                if (av != null)
                {
                    foreach (object o in args.Data)
                    {
                        switch (o.ToString())
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
                                ret.Add(new LSL_Types.Quaternion((double)av.Rotation.X, (double)av.Rotation.Y, (double)av.Rotation.Z, (double)av.Rotation.W));
                                break;
                            case "5":
                                ret.Add(new LSL_Types.Vector3(av.Velocity.X, av.Velocity.Y, av.Velocity.Z));
                                break;
                            case "6":
                                ret.Add(id);
                                break;
                            case "7":
                                ret.Add(UUID.Zero.ToString());
                                break;
                            case "8":
                                ret.Add(UUID.Zero.ToString());
                                break;
                        }
                    }
                    return ret;
                }
                SceneObjectPart obj = World.GetSceneObjectPart(key);
                if (obj != null)
                {
                    foreach (object o in args.Data)
                    {
                        switch (o.ToString())
                        {
                            case "1":
                                ret.Add(obj.Name);
                                break;
                            case "2":
                                ret.Add(obj.Description);
                                break;
                            case "3":
                                ret.Add(new LSL_Types.Vector3(obj.AbsolutePosition.X, obj.AbsolutePosition.Y, obj.AbsolutePosition.Z));
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


        internal UUID ScriptByName(string name)
        {
            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 10 && item.Name == name)
                    return item.ItemID;
            }
            return UUID.Zero;
        }

        internal void ShoutError(string msg)
        {
            llShout(ScriptBaseClass.DEBUG_CHANNEL, msg);
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

        public delegate void AssetRequestCallback(UUID assetID, AssetBase asset);
        private void WithNotecard(UUID assetID, AssetRequestCallback cb)
        {
            World.AssetCache.GetAsset(assetID, delegate(UUID i, AssetBase a) { cb(i, a); }, false);
        }

        public string llGetNumberOfNotecardLines(string name)
        {
            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 7 && item.Name == name)
                {
                    UUID tid = AsyncCommands.
                            DataserverPlugin.RegisterRequest(m_localID,
                            m_itemID, item.AssetID.ToString());
                    if (NotecardCache.IsCached(item.AssetID))
                    {
                        AsyncCommands.
                        DataserverPlugin.DataserverReply(item.AssetID.ToString(),
                                NotecardCache.GetLines(item.AssetID).ToString());
                        // ScriptSleep(100);
                        return tid.ToString();
                    }
                    WithNotecard(item.AssetID, delegate (UUID id, AssetBase a)
                    {
                        System.Text.ASCIIEncoding enc =
                            new System.Text.ASCIIEncoding();
                        string data = enc.GetString(a.Data);
                        //Console.WriteLine(data);
                        NotecardCache.Cache(id, data);
                        AsyncCommands.
                                DataserverPlugin.DataserverReply(id.ToString(),
                                NotecardCache.GetLines(id).ToString());
                    });
                    // ScriptSleep(100);
                    return tid.ToString();
                }
            }
            // ScriptSleep(100);
            return UUID.Zero.ToString();
        }

        public string llGetNotecardLine(string name, int line)
        {
            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 7 && item.Name == name)
                {
                    UUID tid = AsyncCommands.
                            DataserverPlugin.RegisterRequest(m_localID,
                            m_itemID, item.AssetID.ToString());
                    if (NotecardCache.IsCached(item.AssetID))
                    {
                        AsyncCommands.
                        DataserverPlugin.DataserverReply(item.AssetID.ToString(),
                                NotecardCache.GetLine(item.AssetID, line));
                        // ScriptSleep(100);
                        return tid.ToString();
                    }
                    WithNotecard(item.AssetID, delegate (UUID id, AssetBase a)
                    {
                        System.Text.ASCIIEncoding enc =
                            new System.Text.ASCIIEncoding();
                        string data = enc.GetString(a.Data);
                        //Console.WriteLine(data);
                        NotecardCache.Cache(id, data);
                        AsyncCommands.
                                DataserverPlugin.DataserverReply(id.ToString(),
                                NotecardCache.GetLine(id, line));
                    });

                    // ScriptSleep(100);
                    return tid.ToString();
                }
            }

            // ScriptSleep(100);
            return String.Empty;
        }

    }

    public class NotecardCache
    {
        private class Notecard
        {
            public string[] text;
            public DateTime lastRef;
        }

        private static Dictionary<UUID, Notecard> m_Notecards =
                new Dictionary<UUID, Notecard>();

        public static void Cache(UUID assetID, string text)
        {
            CacheCheck();

            lock (m_Notecards)
            {
                if (m_Notecards.ContainsKey(assetID))
                    return;

                Notecard nc = new Notecard();
                nc.lastRef = DateTime.Now;
                nc.text = ParseText(text.Replace("\r", "").Split('\n'));
                m_Notecards[assetID] = nc;
            }
        }

        private static string[] ParseText(string[] input)
        {
            int idx = 0;
            int level = 0;
            List<string> output = new List<string>();
            string[] words;

            while (idx < input.Length)
            {
                if (input[idx] == "{")
                {
                    level++;
                    idx++;
                    continue;
                }

                if (input[idx]== "}")
                {
                    level--;
                    idx++;
                    continue;
                }

                switch (level)
                {
                case 0:
                    words = input[idx].Split(' '); // Linden text ver
                    int version = int.Parse(words[3]);
                    if (version != 2)
                        return new String[0];
                    break;
                case 1:
                    words = input[idx].Split(' ');
                    if (words[0] == "LLEmbeddedItems")
                        break;
                    if (words[0] == "Text")
                    {
                        int len = int.Parse(words[2]);
                        idx++;

                        int count = -1;

                        while (count < len)
                        {
                            // int l = input[idx].Length;
                            string ln = input[idx];

                            int need = len-count-1;
                            if (ln.Length > need)
                                ln = ln.Substring(0, need);

                            output.Add(ln);
                            count += ln.Length + 1;
                            idx++;
                        }

                        return output.ToArray();
                    }
                    break;
                case 2:
                    words = input[idx].Split(' '); // count
                    if (words[0] == "count")
                    {
                        int c = int.Parse(words[1]);
                        if (c > 0)
                            return new String[0];
                        break;
                    }
                    break;
                }
                idx++;
            }
            return output.ToArray();
        }

        public static bool IsCached(UUID assetID)
        {
            lock (m_Notecards)
            {
                return m_Notecards.ContainsKey(assetID);
            }
        }

        public static int GetLines(UUID assetID)
        {
            if (!IsCached(assetID))
                return -1;

            lock (m_Notecards)
            {
                m_Notecards[assetID].lastRef = DateTime.Now;
                return m_Notecards[assetID].text.Length;
            }
        }

        public static string GetLine(UUID assetID, int line)
        {
            if (line < 0)
                return "";

            string data;

            if (!IsCached(assetID))
                return "";

            lock (m_Notecards)
            {
                m_Notecards[assetID].lastRef = DateTime.Now;

                if (line >= m_Notecards[assetID].text.Length)
                    return "\n\n\n";

                data = m_Notecards[assetID].text[line];
                if (data.Length > 255)
                    data = data.Substring(0, 255);

                return data;
            }
        }

        public static void CacheCheck()
        {
            foreach (UUID key in new List<UUID>(m_Notecards.Keys))
            {
                Notecard nc = m_Notecards[key];
                if (nc.lastRef.AddSeconds(30) < DateTime.Now)
                    m_Notecards.Remove(key);
            }
        }
    }
}
