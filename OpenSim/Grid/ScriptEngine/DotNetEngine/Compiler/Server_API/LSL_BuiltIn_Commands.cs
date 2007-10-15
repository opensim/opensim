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

using Axiom.Math;
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Framework.Console;
using OpenSim.Framework.Utilities;
using System.Runtime.Remoting.Lifetime;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler
{
    //
    // !!!IMPORTANT!!!
    //
    // REMEMBER TO UPDATE http://opensimulator.org/wiki/LlFunction_implementation_status
    //

    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_BuiltIn_Commands : MarshalByRefObject, LSL_BuiltIn_Commands_Interface
    {

        private System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
        private ScriptEngine m_ScriptEngine;
        private SceneObjectPart m_host;
        private uint m_localID;
        private LLUUID m_itemID;
        private bool throwErrorOnNotImplemented = true;


        public LSL_BuiltIn_Commands(ScriptEngine ScriptEngine, SceneObjectPart host, uint localID, LLUUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;


            //MainLog.Instance.Notice("ScriptEngine", "LSL_BaseClass.Start() called. Hosted by [" + m_host.Name + ":" + m_host.UUID + "@" + m_host.AbsolutePosition + "]");
        }


        private string m_state = "default";

        public string State()
        {
            return m_state;
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

        //These are the implementations of the various ll-functions used by the LSL scripts.
        //starting out, we use the System.Math library for trig functions. - ckrinke 8-14-07
        public double llSin(double f) { return (double)Math.Sin(f); }
        public double llCos(double f) { return (double)Math.Cos(f); }
        public double llTan(double f) { return (double)Math.Tan(f); }
        public double llAtan2(double x, double y) { return (double)Math.Atan2(y, x); }
        public double llSqrt(double f) { return (double)Math.Sqrt(f); }
        public double llPow(double fbase, double fexponent) { return (double)Math.Pow(fbase, fexponent); }
        public int llAbs(int i) { return (int)Math.Abs(i); }
        public double llFabs(double f) { return (double)Math.Abs(f); }

        public double llFrand(double mag)
        {
            lock (Util.RandomClass)
            {
                return Util.RandomClass.Next((int)mag);
            }
        }

        public int llFloor(double f) { return (int)Math.Floor(f); }
        public int llCeil(double f) { return (int)Math.Ceiling(f); }
        public int llRound(double f) { return (int)Math.Round(f, 3); }

        //This next group are vector operations involving squaring and square root. ckrinke
        public double llVecMag(LSL_Types.Vector3 v)
        {
            return (v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }

        public LSL_Types.Vector3 llVecNorm(LSL_Types.Vector3 v)
        {
            double mag = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            LSL_Types.Vector3 nor = new LSL_Types.Vector3();
            nor.X = v.X / mag; nor.Y = v.Y / mag; nor.Z = v.Z / mag;
            return nor;
        }

        public double llVecDist(LSL_Types.Vector3 a, LSL_Types.Vector3 b)
        {
            double dx = a.X - b.X; double dy = a.Y - b.Y; double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        //Now we start getting into quaternions which means sin/cos, matrices and vectors. ckrinke
        public LSL_Types.Vector3 llRot2Euler(LSL_Types.Quaternion r)
        {
            //This implementation is from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions. ckrinke
            LSL_Types.Quaternion t = new LSL_Types.Quaternion(r.X * r.X, r.Y * r.Y, r.Z * r.Z, r.R * r.R);
            double m = (t.X + t.Y + t.Z + t.R);
            if (m == 0) return new LSL_Types.Vector3();
            double n = 2 * (r.Y * r.R + r.X * r.Z);
            double p = m * m - n * n;
            if (p > 0)
                return new LSL_Types.Vector3(Math.Atan2(2.0 * (r.X * r.R - r.Y * r.Z), (-t.X - t.Y + t.Z + t.R)),
                  Math.Atan2(n, Math.Sqrt(p)), Math.Atan2(2.0 * (r.Z * r.R - r.X * r.Y), (t.X - t.Y - t.Z + t.R)));
            else if (n > 0)
                return new LSL_Types.Vector3(0.0, Math.PI / 2, Math.Atan2((r.Z * r.R + r.X * r.Y), 0.5 - t.X - t.Z));
            else
                return new LSL_Types.Vector3(0.0, -Math.PI / 2, Math.Atan2((r.Z * r.R + r.X * r.Y), 0.5 - t.X - t.Z));
        }

        public LSL_Types.Quaternion llEuler2Rot(LSL_Types.Vector3 v)
        {
            //this comes from from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions but is incomplete as of 8/19/07
            float err = 0.00001f;
            double ax = Math.Sin(v.X / 2); double aw = Math.Cos(v.X / 2);
            double by = Math.Sin(v.Y / 2); double bw = Math.Cos(v.Y / 2);
            double cz = Math.Sin(v.Z / 2); double cw = Math.Cos(v.Z / 2);
            LSL_Types.Quaternion a1 = new LSL_Types.Quaternion(0.0, 0.0, cz, cw);
            LSL_Types.Quaternion a2 = new LSL_Types.Quaternion(0.0, by, 0.0, bw);
            LSL_Types.Quaternion a3 = new LSL_Types.Quaternion(ax, 0.0, 0.0, aw);
            LSL_Types.Quaternion a = new LSL_Types.Quaternion();
            //This multiplication doesnt compile, yet.            a = a1 * a2 * a3;
            LSL_Types.Quaternion b = new LSL_Types.Quaternion(ax * bw * cw + aw * by * cz,
                  aw * by * cw - ax * bw * cz, aw * bw * cz + ax * by * cw, aw * bw * cw - ax * by * cz);
            LSL_Types.Quaternion c = new LSL_Types.Quaternion();
            //This addition doesnt compile yet c = a + b;
            LSL_Types.Quaternion d = new LSL_Types.Quaternion();
            //This addition doesnt compile yet d = a - b;
            if ((Math.Abs(c.X) > err && Math.Abs(d.X) > err) ||
                (Math.Abs(c.Y) > err && Math.Abs(d.Y) > err) ||
                (Math.Abs(c.Z) > err && Math.Abs(d.Z) > err) ||
                (Math.Abs(c.R) > err && Math.Abs(d.R) > err))
            {
                //return a new Quaternion that is null until I figure this out
                //                return b;
                //            return a;
            }
            return new LSL_Types.Quaternion();
        }

        public LSL_Types.Quaternion llAxes2Rot(LSL_Types.Vector3 fwd, LSL_Types.Vector3 left, LSL_Types.Vector3 up) { return new LSL_Types.Quaternion(); }
        public LSL_Types.Vector3 llRot2Fwd(LSL_Types.Quaternion r) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llRot2Left(LSL_Types.Quaternion r) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llRot2Up(LSL_Types.Quaternion r) { return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llRotBetween(LSL_Types.Vector3 start, LSL_Types.Vector3 end) { return new LSL_Types.Quaternion(); }

        public void llWhisper(int channelID, string text)
        {
            //type for whisper is 0
            World.SimChat(Helpers.StringToField(text),
                          0, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
        }

        public void llSay(int channelID, string text)
        {
            //type for say is 1

            World.SimChat(Helpers.StringToField(text),
                           1, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
        }

        public void llShout(int channelID, string text)
        {
            //type for shout is 2
            World.SimChat(Helpers.StringToField(text),
                          2, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
        }

        public int llListen(int channelID, string name, string ID, string msg) { NotImplemented("llListen"); return 0; }
        public void llListenControl(int number, int active) { NotImplemented("llListenControl"); return; }
        public void llListenRemove(int number) { NotImplemented("llListenRemove"); return; }
        public void llSensor(string name, string id, int type, double range, double arc) { NotImplemented("llSensor"); return; }
        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate) { NotImplemented("llSensorRepeat"); return; }
        public void llSensorRemove() { NotImplemented("llSensorRemove"); return; }
        public string llDetectedName(int number) { NotImplemented("llDetectedName"); return ""; }
        public string llDetectedKey(int number) { NotImplemented("llDetectedKey"); return ""; }
        public string llDetectedOwner(int number) { NotImplemented("llDetectedOwner"); return ""; }
        public int llDetectedType(int number) { NotImplemented("llDetectedType"); return 0; }
        public LSL_Types.Vector3 llDetectedPos(int number) { NotImplemented("llDetectedPos"); return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llDetectedVel(int number) { NotImplemented("llDetectedVel"); return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llDetectedGrab(int number) { NotImplemented("llDetectedGrab"); return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llDetectedRot(int number) { NotImplemented("llDetectedRot"); return new LSL_Types.Quaternion(); }
        public int llDetectedGroup(int number) { NotImplemented("llDetectedGroup"); return 0; }
        public int llDetectedLinkNumber(int number) { NotImplemented("llDetectedLinkNumber"); return 0; }
        public void llDie() { NotImplemented("llDie"); return; }
        public double llGround(LSL_Types.Vector3 offset) { NotImplemented("llGround"); return 0; }
        public double llCloud(LSL_Types.Vector3 offset) { NotImplemented("llCloud"); return 0; }
        public LSL_Types.Vector3 llWind(LSL_Types.Vector3 offset) { NotImplemented("llWind"); return new LSL_Types.Vector3(); }
        public void llSetStatus(int status, int value) { NotImplemented("llSetStatus"); return; }
        public int llGetStatus(int status) { NotImplemented("llGetStatus"); return 0; }

        public void llSetScale(LSL_Types.Vector3 scale)
        {
            // TODO: this needs to trigger a persistance save as well
            LLVector3 tmp = m_host.Scale;
            tmp.X = (float)scale.X;
            tmp.Y = (float)scale.Y;
            tmp.Z = (float)scale.Z;
            m_host.Scale = tmp;
            return;
        }
        public LSL_Types.Vector3 llGetScale()
        {
            return new LSL_Types.Vector3(m_host.Scale.X, m_host.Scale.Y, m_host.Scale.Z);
        }

        public void llSetColor(LSL_Types.Vector3 color, int face) { NotImplemented("llSetColor"); return; }
        public double llGetAlpha(int face) { NotImplemented("llGetAlpha"); return 0; }
        public void llSetAlpha(double alpha, int face) { NotImplemented("llSetAlpha"); return; }
        public LSL_Types.Vector3 llGetColor(int face) { NotImplemented("llGetColor"); return new LSL_Types.Vector3(); }
        public void llSetTexture(string texture, int face) { NotImplemented("llSetTexture"); return; }
        public void llScaleTexture(double u, double v, int face) { NotImplemented("llScaleTexture"); return; }
        public void llOffsetTexture(double u, double v, int face) { NotImplemented("llOffsetTexture"); return; }
        public void llRotateTexture(double rotation, int face) { NotImplemented("llRotateTexture"); return; }

        public string llGetTexture(int face) { NotImplemented("llGetTexture"); return ""; }

        public void llSetPos(LSL_Types.Vector3 pos)
        {
            if (m_host.ParentID != 0)
            {
                m_host.UpdateOffSet(new LLVector3((float)pos.X, (float)pos.Y, (float)pos.Z));
            }
            else
            {
                m_host.UpdateGroupPosition(new LLVector3((float)pos.X, (float)pos.Y, (float)pos.Z));
            }
        }

        public LSL_Types.Vector3 llGetPos()
        {
            return new LSL_Types.Vector3(m_host.AbsolutePosition.X,
                                         m_host.AbsolutePosition.Y,
                                         m_host.AbsolutePosition.Z);
        }

        public LSL_Types.Vector3 llGetLocalPos()
        {
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
            m_host.UpdateRotation(new LLQuaternion((float)rot.X, (float)rot.Y, (float)rot.Z, (float)rot.R));
        }
        public LSL_Types.Quaternion llGetRot()
        {
            LLQuaternion q = m_host.RotationOffset;
            return new LSL_Types.Quaternion(q.X, q.Y, q.Z, q.W);
        }
        public LSL_Types.Quaternion llGetLocalRot() { NotImplemented("llGetLocalRot"); return new LSL_Types.Quaternion(); }
        public void llSetForce(LSL_Types.Vector3 force, int local) { NotImplemented("llSetForce"); }
        public LSL_Types.Vector3 llGetForce() { NotImplemented("llGetForce"); return new LSL_Types.Vector3(); }
        public int llTarget(LSL_Types.Vector3 position, double range) { NotImplemented("llTarget"); return 0; }
        public void llTargetRemove(int number) { NotImplemented("llTargetRemove"); }
        public int llRotTarget(LSL_Types.Quaternion rot, double error) { NotImplemented("llRotTarget"); return 0; }
        public void llRotTargetRemove(int number) { NotImplemented("llRotTargetRemove"); }
        public void llMoveToTarget(LSL_Types.Vector3 target, double tau) { NotImplemented("llMoveToTarget"); }
        public void llStopMoveToTarget() { NotImplemented("llStopMoveToTarget"); }
        public void llApplyImpulse(LSL_Types.Vector3 force, int local) { NotImplemented("llApplyImpulse"); }
        public void llApplyRotationalImpulse(LSL_Types.Vector3 force, int local) { NotImplemented("llApplyRotationalImpulse"); }
        public void llSetTorque(LSL_Types.Vector3 torque, int local) { NotImplemented("llSetTorque"); }
        public LSL_Types.Vector3 llGetTorque() { NotImplemented("llGetTorque"); return new LSL_Types.Vector3(); }
        public void llSetForceAndTorque(LSL_Types.Vector3 force, LSL_Types.Vector3 torque, int local) { NotImplemented("llSetForceAndTorque"); }
        public LSL_Types.Vector3 llGetVel() { NotImplemented("llGetVel"); return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetAccel() { NotImplemented("llGetAccel"); return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetOmega() { NotImplemented("llGetOmega"); return new LSL_Types.Vector3(); }
        public double llGetTimeOfDay() { NotImplemented("llGetTimeOfDay"); return 0; }

        public double llGetWallclock()
        {
            return DateTime.Now.TimeOfDay.TotalSeconds;
        }

        public double llGetTime() { NotImplemented("llGetTime"); return 0; }
        public void llResetTime() { NotImplemented("llResetTime"); }
        public double llGetAndResetTime() { NotImplemented("llGetAndResetTime"); return 0; }
        public void llSound() { NotImplemented("llSound"); }
        public void llPlaySound(string sound, double volume) { NotImplemented("llPlaySound"); }
        public void llLoopSound(string sound, double volume) { NotImplemented("llLoopSound"); }
        public void llLoopSoundMaster(string sound, double volume) { NotImplemented("llLoopSoundMaster"); }
        public void llLoopSoundSlave(string sound, double volume) { NotImplemented("llLoopSoundSlave"); }
        public void llPlaySoundSlave(string sound, double volume) { NotImplemented("llPlaySoundSlave"); }
        public void llTriggerSound(string sound, double volume) { NotImplemented("llTriggerSound"); }
        public void llStopSound() { NotImplemented("llStopSound"); }
        public void llPreloadSound(string sound) { NotImplemented("llPreloadSound"); }

        public string llGetSubString(string src, int start, int end)
        {
            return src.Substring(start, end);
        }

        public string llDeleteSubString(string src, int start, int end)
        {
            return src.Remove(start, end - start);
        }
        public string llInsertString(string dst, int position, string src)
        {
            return dst.Insert(position, src);
        }
        public string llToUpper(string src)
        {
            return src.ToUpper();
        }

        public string llToLower(string src)
        {
            return src.ToLower();
        }

        public int llGiveMoney(string destination, int amount) { NotImplemented("llGiveMoney"); return 0; }
        public void llMakeExplosion() { NotImplemented("llMakeExplosion"); }
        public void llMakeFountain() { NotImplemented("llMakeFountain"); }
        public void llMakeSmoke() { NotImplemented("llMakeSmoke"); }
        public void llMakeFire() { NotImplemented("llMakeFire"); }
        public void llRezObject(string inventory, LSL_Types.Vector3 pos, LSL_Types.Quaternion rot, int param) { NotImplemented("llRezObject"); }
        public void llLookAt(LSL_Types.Vector3 target, double strength, double damping) { NotImplemented("llLookAt"); }
        public void llStopLookAt() { NotImplemented("llStopLookAt"); }

        public void llSetTimerEvent(double sec)
        {
            // Setting timer repeat
            m_ScriptEngine.m_LSLLongCmdHandler.SetTimerEvent(m_localID, m_itemID, sec);
        }

        public void llSleep(double sec)
        {
            System.Threading.Thread.Sleep((int)(sec * 1000));
        }

        public double llGetMass() { NotImplemented("llGetMass"); return 0; }
        public void llCollisionFilter(string name, string id, int accept) { NotImplemented("llCollisionFilter"); }
        public void llTakeControls(int controls, int accept, int pass_on) { NotImplemented("llTakeControls"); }
        public void llReleaseControls() { NotImplemented("llReleaseControls"); }
        public void llAttachToAvatar(int attachment) { NotImplemented("llAttachToAvatar"); }
        public void llDetachFromAvatar() { NotImplemented("llDetachFromAvatar"); }
        public void llTakeCamera() { NotImplemented("llTakeCamera"); }
        public void llReleaseCamera() { NotImplemented("llReleaseCamera"); }

        public string llGetOwner()
        {
            return m_host.ObjectOwner.ToStringHyphenated();
        }

        public void llInstantMessage(string user, string message) { NotImplemented("llInstantMessage"); }
        public void llEmail(string address, string subject, string message) { NotImplemented("llEmail"); }
        public void llGetNextEmail(string address, string subject) { NotImplemented("llGetNextEmail"); }

        public string llGetKey()
        {
            return m_host.UUID.ToStringHyphenated();
        }

        public void llSetBuoyancy(double buoyancy) { NotImplemented("llSetBuoyancy"); }
        public void llSetHoverHeight(double height, int water, double tau) { NotImplemented("llSetHoverHeight"); }
        public void llStopHover() { NotImplemented("llStopHover"); }
        public void llMinEventDelay(double delay) { NotImplemented("llMinEventDelay"); }
        public void llSoundPreload() { NotImplemented("llSoundPreload"); }
        public void llRotLookAt(LSL_Types.Quaternion target, double strength, double damping) { NotImplemented("llRotLookAt"); }

        public int llStringLength(string str)
        {
            if (str.Length > 0)
            {
                return str.Length;
            }
            else
            {
                return 0;
            }
        }

        public void llStartAnimation(string anim) { NotImplemented("llStartAnimation"); }
        public void llStopAnimation(string anim) { NotImplemented("llStopAnimation"); }
        public void llPointAt() { NotImplemented("llPointAt"); }
        public void llStopPointAt() { NotImplemented("llStopPointAt"); }
        public void llTargetOmega(LSL_Types.Vector3 axis, double spinrate, double gain) { NotImplemented("llTargetOmega"); }
        public int llGetStartParameter() { NotImplemented("llGetStartParameter"); return 0; }
        public void llGodLikeRezObject(string inventory, LSL_Types.Vector3 pos) { NotImplemented("llGodLikeRezObject"); }
        public void llRequestPermissions(string agent, int perm) { NotImplemented("llRequestPermissions"); }
        public string llGetPermissionsKey() { NotImplemented("llGetPermissionsKey"); return ""; }
        public int llGetPermissions() { NotImplemented("llGetPermissions"); return 0; }
        public int llGetLinkNumber() { NotImplemented("llGetLinkNumber"); return 0; }
        public void llSetLinkColor(int linknumber, LSL_Types.Vector3 color, int face) { NotImplemented("llSetLinkColor"); }
        public void llCreateLink(string target, int parent) { NotImplemented("llCreateLink"); }
        public void llBreakLink(int linknum) { NotImplemented("llBreakLink"); }
        public void llBreakAllLinks() { NotImplemented("llBreakAllLinks"); }
        public string llGetLinkKey(int linknum) { NotImplemented("llGetLinkKey"); return ""; }
        public void llGetLinkName(int linknum) { NotImplemented("llGetLinkName"); }
        public int llGetInventoryNumber(int type) { NotImplemented("llGetInventoryNumber"); return 0; }
        public string llGetInventoryName(int type, int number) { NotImplemented("llGetInventoryName"); return ""; }
        public void llSetScriptState(string name, int run) { NotImplemented("llSetScriptState"); }
        public double llGetEnergy() { return 1.0f; }
        public void llGiveInventory(string destination, string inventory) { NotImplemented("llGiveInventory"); }
        public void llRemoveInventory(string item) { NotImplemented("llRemoveInventory"); }

        public void llSetText(string text, LSL_Types.Vector3 color, double alpha)
        {
            Axiom.Math.Vector3 av3 = new Axiom.Math.Vector3((float)color.X, (float)color.Y, (float)color.Z);
            m_host.SetText(text, av3, alpha);
        }


        public double llWater(LSL_Types.Vector3 offset) { NotImplemented("llWater"); return 0; }
        public void llPassTouches(int pass) { NotImplemented("llPassTouches"); }
        public string llRequestAgentData(string id, int data) { NotImplemented("llRequestAgentData"); return ""; }
        public string llRequestInventoryData(string name) { NotImplemented("llRequestInventoryData"); return ""; }
        public void llSetDamage(double damage) { NotImplemented("llSetDamage"); }
        public void llTeleportAgentHome(string agent) { NotImplemented("llTeleportAgentHome"); }
        public void llModifyLand(int action, int brush) { }
        public void llCollisionSound(string impact_sound, double impact_volume) { NotImplemented("llCollisionSound"); }
        public void llCollisionSprite(string impact_sprite) { NotImplemented("llCollisionSprite"); }
        public string llGetAnimation(string id) { NotImplemented("llGetAnimation"); return ""; }
        public void llResetScript() 
        {
            m_ScriptEngine.m_ScriptManager.ResetScript(m_localID, m_itemID);
        }
        public void llMessageLinked(int linknum, int num, string str, string id) { }
        public void llPushObject(string target, LSL_Types.Vector3 impulse, LSL_Types.Vector3 ang_impulse, int local) { }
        public void llPassCollisions(int pass) { }
        public string llGetScriptName() { return ""; }

        public int llGetNumberOfSides() { return 0; }

        public LSL_Types.Quaternion llAxisAngle2Rot(LSL_Types.Vector3 axis, double angle) { return new LSL_Types.Quaternion(); }
        public LSL_Types.Vector3 llRot2Axis(LSL_Types.Quaternion rot) { return new LSL_Types.Vector3(); }
        public void llRot2Angle() { }

        public double llAcos(double val)
        {
            return (double)Math.Acos(val);
        }

        public double llAsin(double val)
        {
            return (double)Math.Asin(val);
        }

        public double llAngleBetween(LSL_Types.Quaternion a, LSL_Types.Quaternion b) { return 0; }
        public string llGetInventoryKey(string name) { return ""; }
        public void llAllowInventoryDrop(int add) { }
        public LSL_Types.Vector3 llGetSunDirection() { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetTextureOffset(int face) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetTextureScale(int side) { return new LSL_Types.Vector3(); }
        public double llGetTextureRot(int side) { return 0; }

        public int llSubStringIndex(string source, string pattern)
        {
            return source.IndexOf(pattern);
        }

        public string llGetOwnerKey(string id) { NotImplemented("llGetOwnerKey"); return ""; }

        public LSL_Types.Vector3 llGetCenterOfMass() { NotImplemented("llGetCenterOfMass"); return new LSL_Types.Vector3(); }

        public List<string> llListSort(List<string> src, int stride, int ascending)
        {
            SortedList<string, List<string>> sorted = new SortedList<string, List<string>>();
            // Add chunks to an array
            int s = stride;
            if (s < 1)
                s = 1;
            int c = 0;
            List<string> chunk = new List<string>();
            string chunkString = "";
            foreach (string element in src)
            {
                c++;
                if (c > s)
                {
                    sorted.Add(chunkString, chunk);
                    chunkString = "";
                    chunk = new List<string>();
                    c = 0;
                }
                chunk.Add(element);
                chunkString += element.ToString();
            }
            if (chunk.Count > 0)
                sorted.Add(chunkString, chunk);

            List<string> ret = new List<string>();
            foreach (List<string> ls in sorted.Values)
            {
                ret.AddRange(ls);
            }

            if (ascending == LSL.LSL_BaseClass.TRUE)
                return ret;
            ret.Reverse();
            return ret;
        }

        public int llGetListLength(List<string> src)
        {
            return src.Count;
        }

        public int llList2Integer(List<string> src, int index)
        {
            return Convert.ToInt32(src[index]);
        }

        public double llList2double(List<string> src, int index)
        {
            return Convert.ToDouble(src[index]);
        }

        public float llList2Float(List<string> src, int index)
        {
            return Convert.ToSingle(src[index]);
        }

        public string llList2String(List<string> src, int index)
        {
            return src[index];
        }

        public string llList2Key(List<string> src, int index)
        {
            //return OpenSim.Framework.Types.ToStringHyphenated(src[index]);
            return src[index].ToString();
        }

        public LSL_Types.Vector3 llList2Vector(List<string> src, int index)
        {
            return new LSL_Types.Vector3(double.Parse(src[index]), double.Parse(src[index + 1]), double.Parse(src[index + 2]));
        }
        public LSL_Types.Quaternion llList2Rot(List<string> src, int index)
        {
            return new LSL_Types.Quaternion(double.Parse(src[index]), double.Parse(src[index + 1]), double.Parse(src[index + 2]), double.Parse(src[index + 3]));
        }
        public List<string> llList2List(List<string> src, int start, int end)
        {
            if (end > start)
            {
                // Simple straight forward chunk
                return src.GetRange(start, end - start);
            }
            else
            {
                // Some of the end + some of the beginning
                // First chunk
                List<string> ret = new List<string>();
                ret.AddRange(src.GetRange(start, src.Count - start));
                ret.AddRange(src.GetRange(0, end));
                return ret;
            }




        }
        public List<string> llDeleteSubList(List<string> src, int start, int end)
        {
            List<string> ret = new List<string>(src);
            ret.RemoveRange(start, end - start);
            return ret;
        }
        public int llGetListEntryType(List<string> src, int index) { NotImplemented("llGetListEntryType"); return 0; }
        public string llList2CSV(List<string> src)
        {
            string ret = "";
            foreach (string s in src)
            {
                if (s.Length > 0)
                    ret += ",";
                ret += s;
            }
            return ret;
        }
        public List<string> llCSV2List(string src)
        {
            List<string> ret = new List<string>();
            foreach (string s in src.Split(",".ToCharArray()))
            {
                ret.Add(s);
            }
            return ret;
        }
        public List<string> llListRandomize(List<string> src, int stride)
        {
            int s = stride;
            if (s < 1)
                s = 1;

            // This is a cowardly way of doing it ;)
            // TODO: Instead, randomize and check if random is mod stride or if it can not be, then array.removerange
            List<List<string>> tmp = new List<List<string>>();

            // Add chunks to an array
            int c = 0;
            List<string> chunk = new List<string>();
            foreach (string element in src)
            {
                c++;
                if (c > s)
                {
                    tmp.Add(chunk);
                    chunk = new List<string>();
                    c = 0;
                }
                chunk.Add(element);
            }
            if (chunk.Count > 0)
                tmp.Add(chunk);

            // Decreate (<- what kind of word is that? :D ) array back into a list
            int rnd;
            List<string> ret = new List<string>();
            while (tmp.Count > 0)
            {
                rnd = Util.RandomClass.Next(tmp.Count);
                foreach (string str in tmp[rnd])
                {
                    ret.Add(str);
                }
                tmp.RemoveAt(rnd);
            }

            return ret;


        }
        public List<string> llList2ListStrided(List<string> src, int start, int end, int stride)
        {
            List<string> ret = new List<string>();
            int s = stride;
            if (s < 1)
                s = 1;

            int sc = s;
            for (int i = start; i < src.Count; i++)
            {
                sc--;
                if (sc == 0)
                {
                    sc = s;
                    // Addthis
                    ret.Add(src[i]);
                }
                if (i == end)
                    break;
            }
            return ret;
        }

        public LSL_Types.Vector3 llGetRegionCorner()
        {
            return new LSL_Types.Vector3(World.RegionInfo.RegionLocX * 256, World.RegionInfo.RegionLocY * 256, 0);
        }

        public List<string> llListInsertList(List<string> dest, List<string> src, int start)
        {

            List<string> ret = new List<string>(dest);
            //foreach (string s in src.Reverse())
            for (int ci = src.Count - 1; ci > -1; ci--)
            {
                ret.Insert(start, src[ci]);
            }
            return ret;
        }
        public int llListFindList(List<string> src, List<string> test)
        {
            foreach (string s in test)
            {
                for (int ci = 0; ci < src.Count; ci++)
                {

                    if (s == src[ci])
                        return ci;
                }
            }
            return -1;
        }

        public string llGetObjectName()
        {
            return m_host.Name;
        }

        public void llSetObjectName(string name)
        {
            m_host.Name = name;
        }

        public string llGetDate()
        {
            DateTime date = DateTime.Now.ToUniversalTime();
            string result = date.ToString("yyyy-MM-dd");
            return result;
        }

        public int llEdgeOfWorld(LSL_Types.Vector3 pos, LSL_Types.Vector3 dir) { NotImplemented("llEdgeOfWorld"); return 0; }
        public int llGetAgentInfo(string id) { NotImplemented("llGetAgentInfo"); return 0; }
        public void llAdjustSoundVolume(double volume) { NotImplemented("llAdjustSoundVolume"); }
        public void llSetSoundQueueing(int queue) { NotImplemented("llSetSoundQueueing"); }
        public void llSetSoundRadius(double radius) { NotImplemented("llSetSoundRadius"); }
        public string llKey2Name(string id) { NotImplemented("llKey2Name"); return ""; }
        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate) { NotImplemented("llSetTextureAnim"); }
        public void llTriggerSoundLimited(string sound, double volume, LSL_Types.Vector3 top_north_east, LSL_Types.Vector3 bottom_south_west) { NotImplemented("llTriggerSoundLimited"); }
        public void llEjectFromLand(string pest) { NotImplemented("llEjectFromLand"); }

        public void llParseString2List() { NotImplemented("llParseString2List"); }

        public int llOverMyLand(string id) { NotImplemented("llOverMyLand"); return 0; }
        public string llGetLandOwnerAt(LSL_Types.Vector3 pos) { NotImplemented("llGetLandOwnerAt"); return ""; }
        public string llGetNotecardLine(string name, int line) { NotImplemented("llGetNotecardLine"); return ""; }
        public LSL_Types.Vector3 llGetAgentSize(string id) { NotImplemented("llGetAgentSize"); return new LSL_Types.Vector3(); }
        public int llSameGroup(string agent) { NotImplemented("llSameGroup"); return 0; }
        public void llUnSit(string id) { NotImplemented("llUnSit"); }
        public LSL_Types.Vector3 llGroundSlope(LSL_Types.Vector3 offset) { NotImplemented("llGroundSlope"); return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGroundNormal(LSL_Types.Vector3 offset) { NotImplemented("llGroundNormal"); return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGroundContour(LSL_Types.Vector3 offset) { NotImplemented("llGroundContour"); return new LSL_Types.Vector3(); }
        public int llGetAttached() { NotImplemented("llGetAttached"); return 0; }
        public int llGetFreeMemory() { NotImplemented("llGetFreeMemory"); return 0; }

        public string llGetRegionName()
        {
            return World.RegionInfo.RegionName;
        }

        public double llGetRegionTimeDilation() { return 1.0f; }
        public double llGetRegionFPS() { return 10.0f; }

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

        public void llParticleSystem(List<Object> rules)
        {
            Primitive.ParticleSystem prules = new Primitive.ParticleSystem();
            for (int i = 0; i < rules.Count; i += 2)
            {
                switch ((int)rules[i])
                {
                    case (int)PrimitiveRule.PSYS_PART_FLAGS:
                        prules.PartFlags = (uint)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_START_COLOR:
                        prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_START_ALPHA:
                        //what is the cast?                    prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_END_COLOR:
                        prules.PartEndColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_END_ALPHA:
                        //what is the cast?                    prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_START_SCALE:
                        //what is the cast?                    prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_END_SCALE:
                        //what is the cast?                    prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_PART_MAX_AGE:
                        prules.MaxAge = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_ACCEL:
                        //what is the cast?                    prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_PATTERN:
                        //what is the cast?                    prules.PartStartColor = (LLColor)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_TEXTURE:
                        prules.Texture = (LLUUID)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_BURST_RATE:
                        prules.BurstRate = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_BURST_PART_COUNT:
                        prules.BurstPartCount = (byte)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_BURST_RADIUS:
                        prules.BurstRadius = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_BURST_SPEED_MIN:
                        prules.BurstSpeedMin = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_BURST_SPEED_MAX:
                        prules.BurstSpeedMax = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_MAX_AGE:
                        prules.MaxAge = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_TARGET_KEY:
                        prules.Target = (LLUUID)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_OMEGA:
                        //cast??                    prules.MaxAge = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_ANGLE_BEGIN:
                        prules.InnerAngle = (float)rules[i + 1];
                        break;

                    case (int)PrimitiveRule.PSYS_SRC_ANGLE_END:
                        prules.OuterAngle = (float)rules[i + 1];
                        break;

                }
            }

            m_host.AddNewParticleSystem(prules);
        }

        public void llGroundRepel(double height, int water, double tau) { NotImplemented("llGroundRepel"); }
        public void llGiveInventoryList() { NotImplemented("llGiveInventoryList"); }
        public void llSetVehicleType(int type) { NotImplemented("llSetVehicleType"); }
        public void llSetVehicledoubleParam(int param, double value) { NotImplemented("llSetVehicledoubleParam"); }
        public void llSetVehicleVectorParam(int param, LSL_Types.Vector3 vec) { NotImplemented("llSetVehicleVectorParam"); }
        public void llSetVehicleRotationParam(int param, LSL_Types.Quaternion rot) { NotImplemented("llSetVehicleRotationParam"); }
        public void llSetVehicleFlags(int flags) { NotImplemented("llSetVehicleFlags"); }
        public void llRemoveVehicleFlags(int flags) { NotImplemented("llRemoveVehicleFlags"); }
        public void llSitTarget(LSL_Types.Vector3 offset, LSL_Types.Quaternion rot) { NotImplemented("llSitTarget"); }
        public string llAvatarOnSitTarget() { NotImplemented("llAvatarOnSitTarget"); return ""; }
        public void llAddToLandPassList(string avatar, double hours) { NotImplemented("llAddToLandPassList"); }

        public void llSetTouchText(string text)
        {
            m_host.TouchName = text;
        }

        public void llSetSitText(string text)
        {
            m_host.SitName = text;
        }

        public void llSetCameraEyeOffset(LSL_Types.Vector3 offset) { NotImplemented("llSetCameraEyeOffset"); }
        public void llSetCameraAtOffset(LSL_Types.Vector3 offset) { NotImplemented("llSetCameraAtOffset"); }
        public void llDumpList2String() { NotImplemented("llDumpList2String"); }
        public void llScriptDanger(LSL_Types.Vector3 pos) { NotImplemented("llScriptDanger"); }
        public void llDialog(string avatar, string message, List<string> buttons, int chat_channel) { NotImplemented("llDialog"); }
        public void llVolumeDetect(int detect) { NotImplemented("llVolumeDetect"); }
        public void llResetOtherScript(string name) { NotImplemented("llResetOtherScript"); }

        public int llGetScriptState(string name) { NotImplemented("llGetScriptState"); return 0; }

        public void llRemoteLoadScript() { NotImplemented("llRemoteLoadScript"); }
        public void llSetRemoteScriptAccessPin(int pin) { NotImplemented("llSetRemoteScriptAccessPin"); }
        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param) { NotImplemented("llRemoteLoadScriptPin"); }
        public void llOpenRemoteDataChannel() { NotImplemented("llOpenRemoteDataChannel"); }
        public string llSendRemoteData(string channel, string dest, int idata, string sdata) { NotImplemented("llSendRemoteData"); return ""; }
        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata) { NotImplemented("llRemoteDataReply"); }
        public void llCloseRemoteDataChannel(string channel) { NotImplemented("llCloseRemoteDataChannel"); }

        public string llMD5String(string src, int nonce)
        {
            return Util.Md5Hash(src + ":" + nonce.ToString());
        }

        public void llSetPrimitiveParams(List<string> rules) { NotImplemented("llSetPrimitiveParams"); }
        public string llStringToBase64(string str)
        {

            try
            {
                byte[] encData_byte = new byte[str.Length];
                encData_byte = System.Text.Encoding.UTF8.GetBytes(str);
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
            System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
            System.Text.Decoder utf8Decode = encoder.GetDecoder();
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
        public void llXorBase64Strings() { throw new Exception("Command deprecated! Use llXorBase64StringsCorrect instead."); }
        public void llRemoteDataSetRegion() { NotImplemented("llRemoteDataSetRegion"); }
        public double llLog10(double val) { return (double)Math.Log10(val); }
        public double llLog(double val) { return (double)Math.Log(val); }
        public List<string> llGetAnimationList(string id) { NotImplemented("llGetAnimationList"); return new List<string>(); }
        public void llSetParcelMusicURL(string url) { NotImplemented("llSetParcelMusicURL"); }

        public LSL_Types.Vector3 llGetRootPosition() { NotImplemented("llGetRootPosition"); return new LSL_Types.Vector3(); }

        public LSL_Types.Quaternion llGetRootRotation() { NotImplemented("llGetRootRotation"); return new LSL_Types.Quaternion(); }

        public string llGetObjectDesc()
        {
            return m_host.Description;
        }

        public void llSetObjectDesc(string desc)
        {
            m_host.Description = desc;
        }

        public string llGetCreator()
        {
            return m_host.ObjectCreator.ToStringHyphenated();
        }

        public string llGetTimestamp() { return DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); }
        public void llSetLinkAlpha(int linknumber, double alpha, int face) { NotImplemented("llSetLinkAlpha"); }
        public int llGetNumberOfPrims() { NotImplemented("llGetNumberOfPrims"); return 0; }
        public string llGetNumberOfNotecardLines(string name) { NotImplemented("llGetNumberOfNotecardLines"); return ""; }
        public List<string> llGetBoundingBox(string obj) { NotImplemented("llGetBoundingBox"); return new List<string>(); }
        public LSL_Types.Vector3 llGetGeometricCenter() { NotImplemented("llGetGeometricCenter"); return new LSL_Types.Vector3(); }
        public void llGetPrimitiveParams() { NotImplemented("llGetPrimitiveParams"); }
        public string llIntegerToBase64(int number)
        {
            NotImplemented("llIntegerToBase64"); return "";
        }
        public int llBase64ToInteger(string str)
        {
            NotImplemented("llBase64ToInteger"); return 0;
        }

        public double llGetGMTclock()
        {
            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }

        public string llGetSimulatorHostname()
        {
            return System.Environment.MachineName;
        }

        public void llSetLocalRot(LSL_Types.Quaternion rot) { NotImplemented("llSetLocalRot"); }
        public List<string> llParseStringKeepNulls(string src, List<string> seperators, List<string> spacers) { NotImplemented("llParseStringKeepNulls"); return new List<string>(); }
        public void llRezAtRoot(string inventory, LSL_Types.Vector3 position, LSL_Types.Vector3 velocity, LSL_Types.Quaternion rot, int param) { NotImplemented("llRezAtRoot"); }

        public int llGetObjectPermMask(int mask) { NotImplemented("llGetObjectPermMask"); return 0; }

        public void llSetObjectPermMask(int mask, int value) { NotImplemented("llSetObjectPermMask"); }

        public void llGetInventoryPermMask(string item, int mask) { NotImplemented("llGetInventoryPermMask"); }
        public void llSetInventoryPermMask(string item, int mask, int value) { NotImplemented("llSetInventoryPermMask"); }
        public string llGetInventoryCreator(string item) { NotImplemented("llGetInventoryCreator"); return ""; }
        public void llOwnerSay(string msg) { NotImplemented("llOwnerSay"); }
        public void llRequestSimulatorData(string simulator, int data) { NotImplemented("llRequestSimulatorData"); }
        public void llForceMouselook(int mouselook) { NotImplemented("llForceMouselook"); }
        public double llGetObjectMass(string id) { NotImplemented("llGetObjectMass"); return 0; }
        public void llListReplaceList() { NotImplemented("llListReplaceList"); }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            LLUUID avatarId = new LLUUID(avatar_id);
            m_ScriptEngine.World.SendUrlToUser(avatarId, m_host.Name, m_host.UUID, m_host.ObjectOwner, false, message, url);
        }

        public void llParcelMediaCommandList(List<string> commandList) { NotImplemented("llParcelMediaCommandList"); }
        public void llParcelMediaQuery() { NotImplemented("llParcelMediaQuery"); }

        public int llModPow(int a, int b, int c)
        {
            Int64 tmp = 0;
            Int64 val = Math.DivRem(Convert.ToInt64(Math.Pow(a, b)), c, out tmp);
            return Convert.ToInt32(tmp);
        }

        public int llGetInventoryType(string name) { NotImplemented("llGetInventoryType"); return 0; }

        public void llSetPayPrice(int price, List<string> quick_pay_buttons) { NotImplemented("llSetPayPrice"); }
        public LSL_Types.Vector3 llGetCameraPos() { NotImplemented("llGetCameraPos"); return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llGetCameraRot() { NotImplemented("llGetCameraRot"); return new LSL_Types.Quaternion(); }
        public void llSetPrimURL() { NotImplemented("llSetPrimURL"); }
        public void llRefreshPrimURL() { NotImplemented("llRefreshPrimURL"); }

        public string llEscapeURL(string url)
        {
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
            try
            {
                return Uri.UnescapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llUnescapeURL: " + ex.ToString();
            }
        }
        public void llMapDestination(string simname, LSL_Types.Vector3 pos, LSL_Types.Vector3 look_at) { NotImplemented("llMapDestination"); }
        public void llAddToLandBanList(string avatar, double hours) { NotImplemented("llAddToLandBanList"); }
        public void llRemoveFromLandPassList(string avatar) { NotImplemented("llRemoveFromLandPassList"); }
        public void llRemoveFromLandBanList(string avatar) { NotImplemented("llRemoveFromLandBanList"); }
        public void llSetCameraParams(List<string> rules) { NotImplemented("llSetCameraParams"); }
        public void llClearCameraParams() { NotImplemented("llClearCameraParams"); }
        public double llListStatistics(int operation, List<string> src) { NotImplemented("llListStatistics"); return 0; }

        public int llGetUnixTime()
        {
            return Util.UnixTimeSinceEpoch();
        }

        public int llGetParcelFlags(LSL_Types.Vector3 pos) { NotImplemented("llGetParcelFlags"); return 0; }
        public int llGetRegionFlags() { NotImplemented("llGetRegionFlags"); return 0; }
        public string llXorBase64StringsCorrect(string str1, string str2)
        {
            string ret = "";
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
        public void llHTTPRequest(string url, List<string> parameters, string body)
        {
            m_ScriptEngine.m_LSLLongCmdHandler.StartHttpRequest(m_localID, m_itemID, url, parameters, body);
        }
        public void llResetLandBanList() { NotImplemented("llResetLandBanList"); }
        public void llResetLandPassList() { NotImplemented("llResetLandPassList"); }
        public int llGetParcelPrimCount(LSL_Types.Vector3 pos, int category, int sim_wide) { NotImplemented("llGetParcelPrimCount"); return 0; }
        public List<string> llGetParcelPrimOwners(LSL_Types.Vector3 pos) { NotImplemented("llGetParcelPrimOwners"); return new List<string>(); }
        public int llGetObjectPrimCount(string object_id) { NotImplemented("llGetObjectPrimCount"); return 0; }
        public int llGetParcelMaxPrims(LSL_Types.Vector3 pos, int sim_wide) { NotImplemented("llGetParcelMaxPrims"); return 0; }
        public List<string> llGetParcelDetails(LSL_Types.Vector3 pos, List<string> param) { NotImplemented("llGetParcelDetails"); return new List<string>(); }

        //
        // OpenSim functions
        //
        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams, int timer)
        {
            if (dynamicID == "")
            {
                IDynamicTextureManager textureManager = this.World.RequestModuleInterface<IDynamicTextureManager>();
                LLUUID createdTexture = textureManager.AddDynamicTextureURL(World.RegionInfo.SimUUID, this.m_host.UUID, contentType, url, extraParams, timer);
                return createdTexture.ToStringHyphenated();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return LLUUID.Zero.ToStringHyphenated();
        }

        private void NotImplemented(string Command)
        {
            if (throwErrorOnNotImplemented)
                throw new NotImplementedException("Command not implemented: " + Command);
        }

    }
}
