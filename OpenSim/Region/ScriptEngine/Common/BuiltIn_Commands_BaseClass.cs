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
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;
using integer = System.Int32;
using key = System.String;
using vector = OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3;
using rotation = OpenSim.Region.ScriptEngine.Common.LSL_Types.Quaternion;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class BuiltIn_Commands_BaseClass : MarshalByRefObject, LSL_BuiltIn_Commands_Interface, OSSL_BuilIn_Commands_Interface, IScript
    {
        //
        // Included as base for any LSL-script that is compiled.
        // Any function added here will be accessible to the LSL script. But it must also be added to "LSL_BuiltIn_Commands_Interface" in "OpenSim.Region.ScriptEngine.Common" class.
        //
        // Security note: This script will be running inside an restricted AppDomain. Currently AppDomain is not very restricted.
        //

        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Object never expires
        public override Object InitializeLifetimeService()
        {
            //Console.WriteLine("LSL_BaseClass: InitializeLifetimeService()");
            //            return null;
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero; // TimeSpan.FromMinutes(1);
                //lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                //lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }

        public EventQueueManager.Queue_llDetectParams_Struct _llDetectParams;
        EventQueueManager.Queue_llDetectParams_Struct IScript.llDetectParams
        {
            get { return _llDetectParams; }
            set { _llDetectParams = value; }
        }

        private Executor m_Exec;

        ExecutorBase IScript.Exec
        {
            get
            {
                if (m_Exec == null)
                    m_Exec = new Executor(this);
                return m_Exec;
            }
        }


        public BuilIn_Commands m_LSL_Functions;
        private string _Source = String.Empty;
        public string Source
        {
            get
            {
                return _Source;
            }
            set { _Source = value; }
        }

        private int m_StartParam = 0;
        public int StartParam
        {
            get { return m_StartParam; }
            set { m_StartParam = value; }
        }

        public BuiltIn_Commands_BaseClass()
        {
        }

        public string State
        {
            get { return m_LSL_Functions.State; }
            set { m_LSL_Functions.State = value;  }
        }
        public void state(string state)
        {
            State = state;

        }

        public void Start(BuilIn_Commands LSL_Functions)
        {
            m_LSL_Functions = LSL_Functions;

            //m_log.Info(ScriptEngineName, "LSL_BaseClass.Start() called.");

            // Get this AppDomain's settings and display some of them.
            // AppDomainSetup ads = AppDomain.CurrentDomain.SetupInformation;
            // Console.WriteLine("AppName={0}, AppBase={1}, ConfigFile={2}",
            //                  ads.ApplicationName,
            //                  ads.ApplicationBase,
            //                  ads.ConfigurationFile
            //    );

            // Display the name of the calling AppDomain and the name
            // of the second domain.
            // NOTE: The application's thread has transitioned between
            // AppDomains.
            // Console.WriteLine("Calling to '{0}'.",
            //                  Thread.GetDomain().FriendlyName
            //    );

            return;
        }



        public OSSL_BuilIn_Commands.OSSLPrim Prim {
            get { return m_LSL_Functions.Prim; }
        }


        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        // They are only forwarders to LSL_BuiltIn_Commands.cs
        //

        public ICommander GetCommander(string name)
        {
            return m_LSL_Functions.GetCommander(name);
        }

        public double llSin(double f)
        {
            return m_LSL_Functions.llSin(f);
        }
        public void osSetRegionWaterHeight(double height)
        {
            m_LSL_Functions.osSetRegionWaterHeight(height);
        }
        public double llCos(double f)
        {
            return m_LSL_Functions.llCos(f);
        }

        public double llTan(double f)
        {
            return m_LSL_Functions.llTan(f);
        }

        public double llAtan2(double x, double y)
        {
            return m_LSL_Functions.llAtan2(x, y);
        }

        public double llSqrt(double f)
        {
            return m_LSL_Functions.llSqrt(f);
        }

        public double llPow(double fbase, double fexponent)
        {
            return m_LSL_Functions.llPow(fbase, fexponent);
        }

        public LSL_Types.LSLInteger llAbs(int i)
        {
            return m_LSL_Functions.llAbs(i);
        }

        public double llFabs(double f)
        {
            return m_LSL_Functions.llFabs(f);
        }

        public double llFrand(double mag)
        {
            return m_LSL_Functions.llFrand(mag);
        }

        public LSL_Types.LSLInteger llFloor(double f)
        {
            return m_LSL_Functions.llFloor(f);
        }

        public LSL_Types.LSLInteger llCeil(double f)
        {
            return m_LSL_Functions.llCeil(f);
        }

        public LSL_Types.LSLInteger llRound(double f)
        {
            return m_LSL_Functions.llRound(f);
        }

        public double llVecMag(vector v)
        {
            return m_LSL_Functions.llVecMag(v);
        }

        public vector llVecNorm(vector v)
        {
            return m_LSL_Functions.llVecNorm(v);
        }

        public double llVecDist(vector a, vector b)
        {
            return m_LSL_Functions.llVecDist(a, b);
        }

        public vector llRot2Euler(rotation r)
        {
            return m_LSL_Functions.llRot2Euler(r);
        }

        public rotation llEuler2Rot(vector v)
        {
            return m_LSL_Functions.llEuler2Rot(v);
        }

        public rotation llAxes2Rot(vector fwd, vector left, vector up)
        {
            return m_LSL_Functions.llAxes2Rot(fwd, left, up);
        }

        public vector llRot2Fwd(rotation r)
        {
            return m_LSL_Functions.llRot2Fwd(r);
        }

        public vector llRot2Left(rotation r)
        {
            return m_LSL_Functions.llRot2Left(r);
        }

        public vector llRot2Up(rotation r)
        {
            return m_LSL_Functions.llRot2Up(r);
        }

        public rotation llRotBetween(vector start, vector end)
        {
            return m_LSL_Functions.llRotBetween(start, end);
        }

        public void llWhisper(int channelID, string text)
        {
            m_LSL_Functions.llWhisper(channelID, text);
        }

        public void llSay(int channelID, string text)
        {
            m_LSL_Functions.llSay(channelID, text);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llShout(int channelID, string text)
        {
            m_LSL_Functions.llShout(channelID, text);
        }

        public void llOwnerSay(string msg)
        {
            m_LSL_Functions.llOwnerSay(msg);
        }

        public void llRegionSay(int channelID, string text)
        {
            m_LSL_Functions.llRegionSay(channelID, text);
        }

        public LSL_Types.LSLInteger llListen(int channelID, string name, string ID, string msg)
        {
            return m_LSL_Functions.llListen(channelID, name, ID, msg);
        }

        public void llListenControl(int number, int active)
        {
            m_LSL_Functions.llListenControl(number, active);
        }

        public void llListenRemove(int number)
        {
            m_LSL_Functions.llListenRemove(number);
        }

        public void llSensor(string name, string id, int type, double range, double arc)
        {
            m_LSL_Functions.llSensor(name, id, type, range, arc);
        }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            m_LSL_Functions.llSensorRepeat(name, id, type, range, arc, rate);
        }

        public void llSensorRemove()
        {
            m_LSL_Functions.llSensorRemove();
        }

        public string llDetectedName(int number)
        {
            return m_LSL_Functions.llDetectedName(number);
        }

        public string llDetectedKey(int number)
        {
            return m_LSL_Functions.llDetectedKey(number);
        }

        public string llDetectedOwner(int number)
        {
            return m_LSL_Functions.llDetectedOwner(number);
        }

        public LSL_Types.LSLInteger llDetectedType(int number)
        {
            return m_LSL_Functions.llDetectedType(number);
        }

        public vector llDetectedPos(int number)
        {
            return m_LSL_Functions.llDetectedPos(number);
        }

        public vector llDetectedVel(int number)
        {
            return m_LSL_Functions.llDetectedVel(number);
        }

        public vector llDetectedGrab(int number)
        {
            return m_LSL_Functions.llDetectedGrab(number);
        }

        public rotation llDetectedRot(int number)
        {
            return m_LSL_Functions.llDetectedRot(number);
        }

        public LSL_Types.LSLInteger llDetectedGroup(int number)
        {
            return m_LSL_Functions.llDetectedGroup(number);
        }

        public LSL_Types.LSLInteger llDetectedLinkNumber(int number)
        {
            return m_LSL_Functions.llDetectedLinkNumber(number);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llDie()
        {
            m_LSL_Functions.llDie();
        }

        public double llGround(vector offset)
        {
            return m_LSL_Functions.llGround(offset);
        }

        public double llCloud(vector offset)
        {
            return m_LSL_Functions.llCloud(offset);
        }

        public vector llWind(vector offset)
        {
            return m_LSL_Functions.llWind(offset);
        }

        public void llSetStatus(int status, int value)
        {
            m_LSL_Functions.llSetStatus(status, value);
        }

        public LSL_Types.LSLInteger llGetStatus(int status)
        {
            return m_LSL_Functions.llGetStatus(status);
        }

        public void llSetScale(vector scale)
        {
            m_LSL_Functions.llSetScale(scale);
        }

        public vector llGetScale()
        {
            return m_LSL_Functions.llGetScale();
        }

        public void llSetColor(vector color, int face)
        {
            m_LSL_Functions.llSetColor(color, face);
        }

        public double llGetAlpha(int face)
        {
            return m_LSL_Functions.llGetAlpha(face);
        }

        public void llSetAlpha(double alpha, int face)
        {
            m_LSL_Functions.llSetAlpha(alpha, face);
        }

        public vector llGetColor(int face)
        {
            return m_LSL_Functions.llGetColor(face);
        }

        public void llSetTexture(string texture, int face)
        {
            m_LSL_Functions.llSetTexture(texture, face);
        }

        public void llScaleTexture(double u, double v, int face)
        {
            m_LSL_Functions.llScaleTexture(u, v, face);
        }

        public void llOffsetTexture(double u, double v, int face)
        {
            m_LSL_Functions.llOffsetTexture(u, v, face);
        }

        public void llRotateTexture(double rotation, int face)
        {
            m_LSL_Functions.llRotateTexture(rotation, face);
        }

        public string llGetTexture(int face)
        {
            return m_LSL_Functions.llGetTexture(face);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llSetPos(vector pos)
        {
            m_LSL_Functions.llSetPos(pos);
        }

        public vector llGetPos()
        {
            return m_LSL_Functions.llGetPos();
        }

        public vector llGetLocalPos()
        {
            return m_LSL_Functions.llGetLocalPos();
        }

        public void llSetRot(rotation rot)
        {
            m_LSL_Functions.llSetRot(rot);
        }

        public rotation llGetRot()
        {
            return m_LSL_Functions.llGetRot();
        }

        public rotation llGetLocalRot()
        {
            return m_LSL_Functions.llGetLocalRot();
        }

        public void llSetForce(vector force, int local)
        {
            m_LSL_Functions.llSetForce(force, local);
        }

        public vector llGetForce()
        {
            return m_LSL_Functions.llGetForce();
        }

        public LSL_Types.LSLInteger llTarget(vector position, double range)
        {
            return m_LSL_Functions.llTarget(position, range);
        }

        public void llTargetRemove(int number)
        {
            m_LSL_Functions.llTargetRemove(number);
        }

        public LSL_Types.LSLInteger llRotTarget(rotation rot, double error)
        {
            return m_LSL_Functions.llRotTarget(rot, error);
        }

        public void llRotTargetRemove(int number)
        {
            m_LSL_Functions.llRotTargetRemove(number);
        }

        public void llMoveToTarget(vector target, double tau)
        {
            m_LSL_Functions.llMoveToTarget(target, tau);
        }

        public void llStopMoveToTarget()
        {
            m_LSL_Functions.llStopMoveToTarget();
        }

        public void llApplyImpulse(vector force, int local)
        {
            m_LSL_Functions.llApplyImpulse(force, local);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llApplyRotationalImpulse(vector force, int local)
        {
            m_LSL_Functions.llApplyRotationalImpulse(force, local);
        }

        public void llSetTorque(vector torque, int local)
        {
            m_LSL_Functions.llSetTorque(torque, local);
        }

        public vector llGetTorque()
        {
            return m_LSL_Functions.llGetTorque();
        }

        public void llSetForceAndTorque(vector force, vector torque, int local)
        {
            m_LSL_Functions.llSetForceAndTorque(force, torque, local);
        }

        public vector llGetVel()
        {
            return m_LSL_Functions.llGetVel();
        }

        public vector llGetAccel()
        {
            return m_LSL_Functions.llGetAccel();
        }

        public vector llGetOmega()
        {
            return m_LSL_Functions.llGetOmega();
        }

        public double llGetTimeOfDay()
        {
            return m_LSL_Functions.llGetTimeOfDay();
        }

        public double llGetWallclock()
        {
            return m_LSL_Functions.llGetWallclock();
        }

        public double llGetTime()
        {
            return m_LSL_Functions.llGetTime();
        }

        public void llResetTime()
        {
            m_LSL_Functions.llResetTime();
        }

        public double llGetAndResetTime()
        {
            return m_LSL_Functions.llGetAndResetTime();
        }

        public void llSound()
        {
            m_LSL_Functions.llSound();
        }

        public void llPlaySound(string sound, double volume)
        {
            m_LSL_Functions.llPlaySound(sound, volume);
        }

        public void llLoopSound(string sound, double volume)
        {
            m_LSL_Functions.llLoopSound(sound, volume);
        }

        public void llLoopSoundMaster(string sound, double volume)
        {
            m_LSL_Functions.llLoopSoundMaster(sound, volume);
        }

        public void llLoopSoundSlave(string sound, double volume)
        {
            m_LSL_Functions.llLoopSoundSlave(sound, volume);
        }

        public void llPlaySoundSlave(string sound, double volume)
        {
            m_LSL_Functions.llPlaySoundSlave(sound, volume);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llTriggerSound(string sound, double volume)
        {
            m_LSL_Functions.llTriggerSound(sound, volume);
        }

        public void llStopSound()
        {
            m_LSL_Functions.llStopSound();
        }

        public void llPreloadSound(string sound)
        {
            m_LSL_Functions.llPreloadSound(sound);
        }

        public string llGetSubString(string src, int start, int end)
        {
            return m_LSL_Functions.llGetSubString(src, start, end);
        }

        public string llDeleteSubString(string src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubString(src, start, end);
        }

        public string llInsertString(string dst, int position, string src)
        {
            return m_LSL_Functions.llInsertString(dst, position, src);
        }

        public string llToUpper(string source)
        {
            return m_LSL_Functions.llToUpper(source);
        }

        public string llToLower(string source)
        {
            return m_LSL_Functions.llToLower(source);
        }

        public LSL_Types.LSLInteger llGiveMoney(string destination, int amount)
        {
            return m_LSL_Functions.llGiveMoney(destination, amount);
        }

        public void llMakeExplosion()
        {
            m_LSL_Functions.llMakeExplosion();
        }

        public void llMakeFountain()
        {
            m_LSL_Functions.llMakeFountain();
        }

        public void llMakeSmoke()
        {
            m_LSL_Functions.llMakeSmoke();
        }

        public void llMakeFire()
        {
            m_LSL_Functions.llMakeFire();
        }

        public void llRezObject(string inventory, vector pos, vector vel, rotation rot, int param)
        {
            m_LSL_Functions.llRezObject(inventory, pos, vel, rot, param);
        }

        public void llLookAt(vector target, double strength, double damping)
        {
            m_LSL_Functions.llLookAt(target, strength, damping);
        }

        public void llStopLookAt()
        {
            m_LSL_Functions.llStopLookAt();
        }

        public void llSetTimerEvent(double sec)
        {
            m_LSL_Functions.llSetTimerEvent(sec);
        }

        public void llSleep(double sec)
        {
            m_LSL_Functions.llSleep(sec);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public double llGetMass()
        {
            return m_LSL_Functions.llGetMass();
        }

        public void llCollisionFilter(string name, string id, int accept)
        {
            m_LSL_Functions.llCollisionFilter(name, id, accept);
        }

        public void llTakeControls(int controls, int accept, int pass_on)
        {
            m_LSL_Functions.llTakeControls(controls, accept, pass_on);
        }

        public void llReleaseControls()
        {
            m_LSL_Functions.llReleaseControls();
        }

        public void llAttachToAvatar(int attachment)
        {
            m_LSL_Functions.llAttachToAvatar(attachment);
        }

        public void llDetachFromAvatar()
        {
            m_LSL_Functions.llDetachFromAvatar();
        }

        public void llTakeCamera(string avatar)
        {
            m_LSL_Functions.llTakeCamera(avatar);
        }

        public void llReleaseCamera(string avatar)
        {
            m_LSL_Functions.llReleaseCamera(avatar);
        }

        public string llGetOwner()
        {
            return m_LSL_Functions.llGetOwner();
        }

        public void llInstantMessage(string user, string message)
        {
            m_LSL_Functions.llInstantMessage(user, message);
        }

        public void llEmail(string address, string subject, string message)
        {
            m_LSL_Functions.llEmail(address, subject, message);
        }

        public void llGetNextEmail(string address, string subject)
        {
            m_LSL_Functions.llGetNextEmail(address, subject);
        }

        public string llGetKey()
        {
            return m_LSL_Functions.llGetKey();
        }

        public void llSetBuoyancy(double buoyancy)
        {
            m_LSL_Functions.llSetBuoyancy(buoyancy);
        }

        public void llSetHoverHeight(double height, int water, double tau)
        {
            m_LSL_Functions.llSetHoverHeight(height, water, tau);
        }

        public void llStopHover()
        {
            m_LSL_Functions.llStopHover();
        }

        public void llMinEventDelay(double delay)
        {
            m_LSL_Functions.llMinEventDelay(delay);
        }

        public void llSoundPreload()
        {
            m_LSL_Functions.llSoundPreload();
        }

        public void llRotLookAt(rotation target, double strength, double damping)
        {
            m_LSL_Functions.llRotLookAt(target, strength, damping);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public LSL_Types.LSLInteger llStringLength(string str)
        {
            return m_LSL_Functions.llStringLength(str);
        }

        public void llStartAnimation(string anim)
        {
            m_LSL_Functions.llStartAnimation(anim);
        }

        public void llStopAnimation(string anim)
        {
            m_LSL_Functions.llStopAnimation(anim);
        }

        public void llPointAt()
        {
            m_LSL_Functions.llPointAt();
        }

        public void llStopPointAt()
        {
            m_LSL_Functions.llStopPointAt();
        }

        public void llTargetOmega(vector axis, double spinrate, double gain)
        {
            m_LSL_Functions.llTargetOmega(axis, spinrate, gain);
        }

        public LSL_Types.LSLInteger llGetStartParameter()
        {
            return m_StartParam;
        }

        public void llGodLikeRezObject(string inventory, vector pos)
        {
            m_LSL_Functions.llGodLikeRezObject(inventory, pos);
        }

        public void llRequestPermissions(string agent, int perm)
        {
            m_LSL_Functions.llRequestPermissions(agent, perm);
        }

        public string llGetPermissionsKey()
        {
            return m_LSL_Functions.llGetPermissionsKey();
        }

        public LSL_Types.LSLInteger llGetPermissions()
        {
            return m_LSL_Functions.llGetPermissions();
        }

        public LSL_Types.LSLInteger llGetLinkNumber()
        {
            return m_LSL_Functions.llGetLinkNumber();
        }

        public void llSetLinkColor(int linknumber, vector color, int face)
        {
            m_LSL_Functions.llSetLinkColor(linknumber, color, face);
        }

        public void llCreateLink(string target, int parent)
        {
            m_LSL_Functions.llCreateLink(target, parent);
        }

        public void llBreakLink(int linknum)
        {
            m_LSL_Functions.llBreakLink(linknum);
        }

        public void llBreakAllLinks()
        {
            m_LSL_Functions.llBreakAllLinks();
        }

        public string llGetLinkKey(int linknum)
        {
            return m_LSL_Functions.llGetLinkKey(linknum);
        }

        public string llGetLinkName(int linknum)
        {
            return m_LSL_Functions.llGetLinkName(linknum);
        }

        public LSL_Types.LSLInteger llGetInventoryNumber(int type)
        {
            return m_LSL_Functions.llGetInventoryNumber(type);
        }

        public string llGetInventoryName(int type, int number)
        {
            return m_LSL_Functions.llGetInventoryName(type, number);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llSetScriptState(string name, int run)
        {
            m_LSL_Functions.llSetScriptState(name, run);
        }

        public double llGetEnergy()
        {
            return m_LSL_Functions.llGetEnergy();
        }

        public void llGiveInventory(string destination, string inventory)
        {
            m_LSL_Functions.llGiveInventory(destination, inventory);
        }

        public void llRemoveInventory(string item)
        {
            m_LSL_Functions.llRemoveInventory(item);
        }

        public void llSetText(string text, vector color, double alpha)
        {
            m_LSL_Functions.llSetText(text, color, alpha);
        }

        public double llWater(vector offset)
        {
            return m_LSL_Functions.llWater(offset);
        }

        public void llPassTouches(int pass)
        {
            m_LSL_Functions.llPassTouches(pass);
        }

        public string llRequestAgentData(string id, int data)
        {
            return m_LSL_Functions.llRequestAgentData(id, data);
        }

        public string llRequestInventoryData(string name)
        {
            return m_LSL_Functions.llRequestInventoryData(name);
        }

        public void llSetDamage(double damage)
        {
            m_LSL_Functions.llSetDamage(damage);
        }

        public void llTeleportAgentHome(string agent)
        {
            m_LSL_Functions.llTeleportAgentHome(agent);
        }

        public void llModifyLand(int action, int brush)
        {
            m_LSL_Functions.llModifyLand(action, brush);
        }

        public void llCollisionSound(string impact_sound, double impact_volume)
        {
            m_LSL_Functions.llCollisionSound(impact_sound, impact_volume);
        }

        public void llCollisionSprite(string impact_sprite)
        {
            m_LSL_Functions.llCollisionSprite(impact_sprite);
        }

        public string llGetAnimation(string id)
        {
            return m_LSL_Functions.llGetAnimation(id);
        }

        public void llResetScript()
        {
            m_LSL_Functions.llResetScript();
        }

        public void llMessageLinked(int linknum, int num, string str, string id)
        {
            m_LSL_Functions.llMessageLinked(linknum, num, str, id);
        }

        public void llPushObject(string target, vector impulse, vector ang_impulse, int local)
        {
            m_LSL_Functions.llPushObject(target, impulse, ang_impulse, local);
        }

        public void llPassCollisions(int pass)
        {
            m_LSL_Functions.llPassCollisions(pass);
        }

        public string llGetScriptName()
        {
            return m_LSL_Functions.llGetScriptName();
        }

        public LSL_Types.LSLInteger llGetNumberOfSides()
        {
            return m_LSL_Functions.llGetNumberOfSides();
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public rotation llAxisAngle2Rot(vector axis, double angle)
        {
            return m_LSL_Functions.llAxisAngle2Rot(axis, angle);
        }

        public vector llRot2Axis(rotation rot)
        {
            return m_LSL_Functions.llRot2Axis(rot);
        }

        public double llRot2Angle(rotation rot)
        {
            return m_LSL_Functions.llRot2Angle(rot);
        }

        public double llAcos(double val)
        {
            return m_LSL_Functions.llAcos(val);
        }

        public double llAsin(double val)
        {
            return m_LSL_Functions.llAsin(val);
        }

        public double llAngleBetween(rotation a, rotation b)
        {
            return m_LSL_Functions.llAngleBetween(a, b);
        }

        public string llGetInventoryKey(string name)
        {
            return m_LSL_Functions.llGetInventoryKey(name);
        }

        public void llAllowInventoryDrop(int add)
        {
            m_LSL_Functions.llAllowInventoryDrop(add);
        }

        public vector llGetSunDirection()
        {
            return m_LSL_Functions.llGetSunDirection();
        }

        public vector llGetTextureOffset(int face)
        {
            return m_LSL_Functions.llGetTextureOffset(face);
        }

        public vector llGetTextureScale(int side)
        {
            return m_LSL_Functions.llGetTextureScale(side);
        }

        public double llGetTextureRot(int side)
        {
            return m_LSL_Functions.llGetTextureRot(side);
        }

        public LSL_Types.LSLInteger llSubStringIndex(string source, string pattern)
        {
            return m_LSL_Functions.llSubStringIndex(source, pattern);
        }

        public string llGetOwnerKey(string id)
        {
            return m_LSL_Functions.llGetOwnerKey(id);
        }

        public vector llGetCenterOfMass()
        {
            return m_LSL_Functions.llGetCenterOfMass();
        }

        public LSL_Types.list llListSort(LSL_Types.list src, int stride, int ascending)
        {
            return m_LSL_Functions.llListSort(src, stride, ascending);
        }

        public LSL_Types.LSLInteger llGetListLength(LSL_Types.list src)
        {
            return m_LSL_Functions.llGetListLength(src);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public LSL_Types.LSLInteger llList2Integer(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Integer(src, index);
        }

        public double osList2Double(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.osList2Double(src, index);
        }

        public string llList2String(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2String(src, index);
        }

        public string llList2Key(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Key(src, index);
        }

        public vector llList2Vector(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Vector(src, index);
        }

        public rotation llList2Rot(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Rot(src, index);
        }

        public LSL_Types.list llList2List(LSL_Types.list src, int start, int end)
        {
            return m_LSL_Functions.llList2List(src, start, end);
        }

        public LSL_Types.list llDeleteSubList(LSL_Types.list src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubList(src, start, end);
        }

        public LSL_Types.LSLInteger llGetListEntryType(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llGetListEntryType(src, index);
        }

        public string llList2CSV(LSL_Types.list src)
        {
            return m_LSL_Functions.llList2CSV(src);
        }

        public LSL_Types.list llCSV2List(string src)
        {
            return m_LSL_Functions.llCSV2List(src);
        }

        public LSL_Types.list llListRandomize(LSL_Types.list src, int stride)
        {
            return m_LSL_Functions.llListRandomize(src, stride);
        }

        public LSL_Types.list llList2ListStrided(LSL_Types.list src, int start, int end, int stride)
        {
            return m_LSL_Functions.llList2ListStrided(src, start, end, stride);
        }

        public vector llGetRegionCorner()
        {
            return m_LSL_Functions.llGetRegionCorner();
        }

        public LSL_Types.list llListInsertList(LSL_Types.list dest, LSL_Types.list src, int start)
        {
            return m_LSL_Functions.llListInsertList(dest, src, start);
        }

        public LSL_Types.LSLInteger llListFindList(LSL_Types.list src, LSL_Types.list test)
        {
            return m_LSL_Functions.llListFindList(src, test);
        }

        public string llGetObjectName()
        {
            return m_LSL_Functions.llGetObjectName();
        }

        public void llSetObjectName(string name)
        {
            m_LSL_Functions.llSetObjectName(name);
        }

        public string llGetDate()
        {
            return m_LSL_Functions.llGetDate();
        }

        public LSL_Types.LSLInteger llEdgeOfWorld(vector pos, vector dir)
        {
            return m_LSL_Functions.llEdgeOfWorld(pos, dir);
        }

        public LSL_Types.LSLInteger llGetAgentInfo(string id)
        {
            return m_LSL_Functions.llGetAgentInfo(id);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llAdjustSoundVolume(double volume)
        {
            m_LSL_Functions.llAdjustSoundVolume(volume);
        }

        public void llSetSoundQueueing(int queue)
        {
            m_LSL_Functions.llSetSoundQueueing(queue);
        }

        public void llSetSoundRadius(double radius)
        {
            m_LSL_Functions.llSetSoundRadius(radius);
        }

        public string llKey2Name(string id)
        {
            return m_LSL_Functions.llKey2Name(id);
        }

        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_LSL_Functions.llSetTextureAnim(mode, face, sizex, sizey, start, length, rate);
        }

        public void llTriggerSoundLimited(string sound, double volume, vector top_north_east, vector bottom_south_west)
        {
            m_LSL_Functions.llTriggerSoundLimited(sound, volume, top_north_east, bottom_south_west);
        }

        public void llEjectFromLand(string pest)
        {
            m_LSL_Functions.llEjectFromLand(pest);
        }

        public LSL_Types.list llParseString2List(string str, LSL_Types.list separators, LSL_Types.list spacers)
        {
            return m_LSL_Functions.llParseString2List(str,separators,spacers);
        }

        public LSL_Types.LSLInteger llOverMyLand(string id)
        {
            return m_LSL_Functions.llOverMyLand(id);
        }

        public string llGetLandOwnerAt(vector pos)
        {
            return m_LSL_Functions.llGetLandOwnerAt(pos);
        }

        public string llGetNotecardLine(string name, int line)
        {
            return m_LSL_Functions.llGetNotecardLine(name, line);
        }

        public vector llGetAgentSize(string id)
        {
            return m_LSL_Functions.llGetAgentSize(id);
        }

        public LSL_Types.LSLInteger llSameGroup(string agent)
        {
            return m_LSL_Functions.llSameGroup(agent);
        }

        public void llUnSit(string id)
        {
            m_LSL_Functions.llUnSit(id);
        }

        public vector llGroundSlope(vector offset)
        {
            return m_LSL_Functions.llGroundSlope(offset);
        }

        public vector llGroundNormal(vector offset)
        {
            return m_LSL_Functions.llGroundNormal(offset);
        }

        public vector llGroundContour(vector offset)
        {
            return m_LSL_Functions.llGroundContour(offset);
        }

        public LSL_Types.LSLInteger llGetAttached()
        {
            return m_LSL_Functions.llGetAttached();
        }

        public LSL_Types.LSLInteger llGetFreeMemory()
        {
            return m_LSL_Functions.llGetFreeMemory();
        }

        public string llGetRegionName()
        {
            return m_LSL_Functions.llGetRegionName();
        }

        public double llGetRegionTimeDilation()
        {
            return m_LSL_Functions.llGetRegionTimeDilation();
        }

        public double llGetRegionFPS()
        {
            return m_LSL_Functions.llGetRegionFPS();
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llParticleSystem(LSL_Types.list rules)
        {
            m_LSL_Functions.llParticleSystem(rules);
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            m_LSL_Functions.llGroundRepel(height, water, tau);
        }

        public void llGiveInventoryList(string destination, string category, LSL_Types.list inventory)
        {
            m_LSL_Functions.llGiveInventoryList(destination, category, inventory);
        }

        public void llSetVehicleType(int type)
        {
            m_LSL_Functions.llSetVehicleType(type);
        }

        public void llSetVehicledoubleParam(int param, double value)
        {
            m_LSL_Functions.llSetVehicledoubleParam(param, value);
        }

        public void llSetVehicleFloatParam(int param, float value)
        {
            m_LSL_Functions.llSetVehicleFloatParam(param, value);
        }

        public void llSetVehicleVectorParam(int param, vector vec)
        {
            m_LSL_Functions.llSetVehicleVectorParam(param, vec);
        }

        public void llSetVehicleRotationParam(int param, rotation rot)
        {
            m_LSL_Functions.llSetVehicleRotationParam(param, rot);
        }

        public void llSetVehicleFlags(int flags)
        {
            m_LSL_Functions.llSetVehicleFlags(flags);
        }

        public void llRemoveVehicleFlags(int flags)
        {
            m_LSL_Functions.llRemoveVehicleFlags(flags);
        }

        public void llSitTarget(vector offset, rotation rot)
        {
            m_LSL_Functions.llSitTarget(offset, rot);
        }

        public string llAvatarOnSitTarget()
        {
            return m_LSL_Functions.llAvatarOnSitTarget();
        }

        public void llAddToLandPassList(string avatar, double hours)
        {
            m_LSL_Functions.llAddToLandPassList(avatar, hours);
        }

        public void llSetTouchText(string text)
        {
            m_LSL_Functions.llSetTouchText(text);
        }

        public void llSetSitText(string text)
        {
            m_LSL_Functions.llSetSitText(text);
        }

        public void llSetCameraEyeOffset(vector offset)
        {
            m_LSL_Functions.llSetCameraEyeOffset(offset);
        }

        public void llSetCameraAtOffset(vector offset)
        {
            m_LSL_Functions.llSetCameraAtOffset(offset);
        }

        public string llDumpList2String(LSL_Types.list src, string seperator)
        {
            return m_LSL_Functions.llDumpList2String(src, seperator);
        }

        public LSL_Types.LSLInteger llScriptDanger(vector pos)
        {
            return m_LSL_Functions.llScriptDanger(pos);
        }

        public void llDialog(string avatar, string message, LSL_Types.list buttons, int chat_channel)
        {
            m_LSL_Functions.llDialog(avatar, message, buttons, chat_channel);
        }

        public void llVolumeDetect(int detect)
        {
            m_LSL_Functions.llVolumeDetect(detect);
        }

        public void llResetOtherScript(string name)
        {
            m_LSL_Functions.llResetOtherScript(name);
        }

        public LSL_Types.LSLInteger llGetScriptState(string name)
        {
            return m_LSL_Functions.llGetScriptState(name);
        }

        public void llRemoteLoadScript()
        {
            m_LSL_Functions.llRemoteLoadScript();
        }

        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_LSL_Functions.llSetRemoteScriptAccessPin(pin);
        }

        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            m_LSL_Functions.llRemoteLoadScriptPin(target, name, pin, running, start_param);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public void llOpenRemoteDataChannel()
        {
            m_LSL_Functions.llOpenRemoteDataChannel();
        }

        public string llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            return m_LSL_Functions.llSendRemoteData(channel, dest, idata, sdata);
        }

        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            m_LSL_Functions.llRemoteDataReply(channel, message_id, sdata, idata);
        }

        public void llCloseRemoteDataChannel(string channel)
        {
            m_LSL_Functions.llCloseRemoteDataChannel(channel);
        }

        public string llMD5String(string src, int nonce)
        {
            return m_LSL_Functions.llMD5String(src, nonce);
        }

        public void llSetPrimitiveParams(LSL_Types.list rules)
        {
            m_LSL_Functions.llSetPrimitiveParams(rules);
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_Types.list rules)
        {
            m_LSL_Functions.llSetLinkPrimitiveParams(linknumber, rules);
        }
        public string llStringToBase64(string str)
        {
            return m_LSL_Functions.llStringToBase64(str);
        }

        public string llBase64ToString(string str)
        {
            return m_LSL_Functions.llBase64ToString(str);
        }

        public void llXorBase64Strings()
        {
            m_LSL_Functions.llXorBase64Strings();
        }

        public void llRemoteDataSetRegion()
        {
            m_LSL_Functions.llRemoteDataSetRegion();
        }

        public double llLog10(double val)
        {
            return m_LSL_Functions.llLog10(val);
        }

        public double llLog(double val)
        {
            return m_LSL_Functions.llLog(val);
        }

        public LSL_Types.list llGetAnimationList(string id)
        {
            return m_LSL_Functions.llGetAnimationList(id);
        }

        public void llSetParcelMusicURL(string url)
        {
            m_LSL_Functions.llSetParcelMusicURL(url);
        }

        public vector llGetRootPosition()
        {
            return m_LSL_Functions.llGetRootPosition();
        }

        public rotation llGetRootRotation()
        {
            return m_LSL_Functions.llGetRootRotation();
        }

        public string llGetObjectDesc()
        {
            return m_LSL_Functions.llGetObjectDesc();
        }

        public void llSetObjectDesc(string desc)
        {
            m_LSL_Functions.llSetObjectDesc(desc);
        }

        public string llGetCreator()
        {
            return m_LSL_Functions.llGetCreator();
        }

        public string llGetTimestamp()
        {
            return m_LSL_Functions.llGetTimestamp();
        }

        public void llSetLinkAlpha(int linknumber, double alpha, int face)
        {
            m_LSL_Functions.llSetLinkAlpha(linknumber, alpha, face);
        }

        public LSL_Types.LSLInteger llGetNumberOfPrims()
        {
            return m_LSL_Functions.llGetNumberOfPrims();
        }

        public string llGetNumberOfNotecardLines(string name)
        {
            return m_LSL_Functions.llGetNumberOfNotecardLines(name);
        }

        public LSL_Types.list llGetBoundingBox(string obj)
        {
            return m_LSL_Functions.llGetBoundingBox(obj);
        }

        public vector llGetGeometricCenter()
        {
            return m_LSL_Functions.llGetGeometricCenter();
        }

        public LSL_Types.list llGetPrimitiveParams(LSL_Types.list rules)
        {
            return m_LSL_Functions.llGetPrimitiveParams(rules);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public string llIntegerToBase64(int number)
        {
            return m_LSL_Functions.llIntegerToBase64(number);
        }

        public LSL_Types.LSLInteger llBase64ToInteger(string str)
        {
            return m_LSL_Functions.llBase64ToInteger(str);
        }

        public double llGetGMTclock()
        {
            return m_LSL_Functions.llGetGMTclock();
        }

        public string llGetSimulatorHostname()
        {
            return m_LSL_Functions.llGetSimulatorHostname();
        }

        public void llSetLocalRot(rotation rot)
        {
            m_LSL_Functions.llSetLocalRot(rot);
        }

        public LSL_Types.list llParseStringKeepNulls(string src, LSL_Types.list seperators, LSL_Types.list spacers)
        {
            return m_LSL_Functions.llParseStringKeepNulls(src, seperators, spacers);
        }

        public void llRezAtRoot(string inventory, vector position, vector velocity, rotation rot, int param)
        {
            m_LSL_Functions.llRezAtRoot(inventory, position, velocity, rot, param);
        }

        public LSL_Types.LSLInteger llGetObjectPermMask(int mask)
        {
            return m_LSL_Functions.llGetObjectPermMask(mask);
        }

        public void llSetObjectPermMask(int mask, int value)
        {
            m_LSL_Functions.llSetObjectPermMask(mask, value);
        }

        public LSL_Types.LSLInteger llGetInventoryPermMask(string item, int mask)
        {
            return m_LSL_Functions.llGetInventoryPermMask(item, mask);
        }

        public void llSetInventoryPermMask(string item, int mask, int value)
        {
            m_LSL_Functions.llSetInventoryPermMask(item, mask, value);
        }

        public string llGetInventoryCreator(string item)
        {
            return m_LSL_Functions.llGetInventoryCreator(item);
        }

        public string llRequestSimulatorData(string simulator, int data)
        {
            return m_LSL_Functions.llRequestSimulatorData(simulator, data);
        }

        public void llForceMouselook(int mouselook)
        {
            m_LSL_Functions.llForceMouselook(mouselook);
        }

        public double llGetObjectMass(string id)
        {
            return m_LSL_Functions.llGetObjectMass(id);
        }

        public LSL_Types.list llListReplaceList(LSL_Types.list dest, LSL_Types.list src, int start, int end)
        {
            return m_LSL_Functions.llListReplaceList(dest, src, start, end);
        }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            m_LSL_Functions.llLoadURL(avatar_id, message, url);
        }

        public void llParcelMediaCommandList(LSL_Types.list commandList)
        {
            m_LSL_Functions.llParcelMediaCommandList(commandList);
        }

        public LSL_Types.list llParcelMediaQuery(LSL_Types.list aList)
        {
            return m_LSL_Functions.llParcelMediaQuery(aList);
        }

        public LSL_Types.LSLInteger llModPow(int a, int b, int c)
        {
            return m_LSL_Functions.llModPow(a, b, c);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public LSL_Types.LSLInteger llGetInventoryType(string name)
        {
            return m_LSL_Functions.llGetInventoryType(name);
        }

        public void llSetPayPrice(int price, LSL_Types.list quick_pay_buttons)
        {
            m_LSL_Functions.llSetPayPrice(price, quick_pay_buttons);
        }

        public vector llGetCameraPos()
        {
            return m_LSL_Functions.llGetCameraPos();
        }

        public rotation llGetCameraRot()
        {
            return m_LSL_Functions.llGetCameraRot();
        }

        public void llSetPrimURL()
        {
            m_LSL_Functions.llSetPrimURL();
        }

        public void llRefreshPrimURL()
        {
            m_LSL_Functions.llRefreshPrimURL();
        }

        public string llEscapeURL(string url)
        {
            return m_LSL_Functions.llEscapeURL(url);
        }

        public string llUnescapeURL(string url)
        {
            return m_LSL_Functions.llUnescapeURL(url);
        }

        public void llMapDestination(string simname, vector pos, vector look_at)
        {
            m_LSL_Functions.llMapDestination(simname, pos, look_at);
        }

        public void llAddToLandBanList(string avatar, double hours)
        {
            m_LSL_Functions.llAddToLandBanList(avatar, hours);
        }

        public void llRemoveFromLandPassList(string avatar)
        {
            m_LSL_Functions.llRemoveFromLandPassList(avatar);
        }

        public void llRemoveFromLandBanList(string avatar)
        {
            m_LSL_Functions.llRemoveFromLandBanList(avatar);
        }

        public void llSetCameraParams(LSL_Types.list rules)
        {
            m_LSL_Functions.llSetCameraParams(rules);
        }

        public void llClearCameraParams()
        {
            m_LSL_Functions.llClearCameraParams();
        }

        public double llListStatistics(int operation, LSL_Types.list src)
        {
            return m_LSL_Functions.llListStatistics(operation, src);
        }

        public LSL_Types.LSLInteger llGetUnixTime()
        {
            return m_LSL_Functions.llGetUnixTime();
        }

        public LSL_Types.LSLInteger llGetParcelFlags(vector pos)
        {
            return m_LSL_Functions.llGetParcelFlags(pos);
        }

        public LSL_Types.LSLInteger llGetRegionFlags()
        {
            return m_LSL_Functions.llGetRegionFlags();
        }

        public string llXorBase64StringsCorrect(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64StringsCorrect(str1, str2);
        }

        public string llHTTPRequest(string url, LSL_Types.list parameters, string body)
        {
            return m_LSL_Functions.llHTTPRequest(url, parameters, body);
        }

        public void llResetLandBanList()
        {
            m_LSL_Functions.llResetLandBanList();
        }

        public void llResetLandPassList()
        {
            m_LSL_Functions.llResetLandPassList();
        }

        public LSL_Types.LSLInteger llGetParcelPrimCount(vector pos, int category, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelPrimCount(pos, category, sim_wide);
        }

        public LSL_Types.list llGetParcelPrimOwners(vector pos)
        {
            return m_LSL_Functions.llGetParcelPrimOwners(pos);
        }

        public LSL_Types.LSLInteger llGetObjectPrimCount(string object_id)
        {
            return m_LSL_Functions.llGetObjectPrimCount(object_id);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public LSL_Types.LSLInteger llGetParcelMaxPrims(vector pos, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelMaxPrims(pos, sim_wide);
        }

        public LSL_Types.list llGetParcelDetails(vector pos, LSL_Types.list param)
        {
            return m_LSL_Functions.llGetParcelDetails(pos, param);
        }

        public void llSetLinkTexture(int linknumber, string texture, int face)
        {
            m_LSL_Functions.llSetLinkTexture(linknumber, texture, face);
        }

        public string llStringTrim(string src, int type)
        {
            return m_LSL_Functions.llStringTrim(src, type);
        }

        public LSL_Types.list llGetObjectDetails(string id, LSL_Types.list args)
        {
            return m_LSL_Functions.llGetObjectDetails(id, args);
        }

        //
        // OpenSim Functions
        //
        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            return m_LSL_Functions.osSetDynamicTextureURL(dynamicID, contentType, url, extraParams, timer);
        }

        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                             int timer)
        {
            return m_LSL_Functions.osSetDynamicTextureData(dynamicID, contentType, data, extraParams, timer);
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                           int timer, int alpha)
        {
            return m_LSL_Functions.osSetDynamicTextureURLBlend(dynamicID, contentType, url, extraParams, timer, alpha);
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                             int timer, int alpha)
        {
            return m_LSL_Functions.osSetDynamicTextureDataBlend(dynamicID, contentType, data, extraParams, timer, alpha);
        }

        public double osTerrainGetHeight(int x, int y)
        {
            return m_LSL_Functions.osTerrainGetHeight(x, y);
        }

        public int osTerrainSetHeight(int x, int y, double val)
        {
            return m_LSL_Functions.osTerrainSetHeight(x, y, val);
        }

        public int osRegionRestart(double seconds)
        {
            return m_LSL_Functions.osRegionRestart(seconds);
        }

        public void osRegionNotice(string msg)
        {
            m_LSL_Functions.osRegionNotice(msg);
        }

        public bool osConsoleCommand(string Command)
        {
            return m_LSL_Functions.osConsoleCommand(Command);
        }

        public void osSetParcelMediaURL(string url)
        {
            m_LSL_Functions.osSetParcelMediaURL(url);
        }

        public void osSetPrimFloatOnWater(int floatYN)
        {
            m_LSL_Functions.osSetPrimFloatOnWater(floatYN);
        }

        // Teleport Functions

        public void osTeleportAgent(string agent, string regionName, vector position, vector lookat)
        {
            m_LSL_Functions.osTeleportAgent(agent, regionName, position, lookat);
        }

        public void osTeleportAgent(string agent, vector position, vector lookat)
        {
            m_LSL_Functions.osTeleportAgent(agent, position, lookat);
        }

        // Animation Functions

        public void osAvatarPlayAnimation(string avatar, string animation)
        {
            m_LSL_Functions.osAvatarPlayAnimation(avatar, animation);
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            m_LSL_Functions.osAvatarStopAnimation(avatar, animation);
        }


        //Texture Draw functions

        public string osMovePen(string drawList, int x, int y)
        {
            return m_LSL_Functions.osMovePen(drawList, x, y);
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            return m_LSL_Functions.osDrawLine(drawList, startX, startY, endX, endY);
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            return m_LSL_Functions.osDrawLine(drawList, endX, endY);
        }

        public string osDrawText(string drawList, string text)
        {
            return m_LSL_Functions.osDrawText(drawList, text);
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            return m_LSL_Functions.osDrawEllipse(drawList, width, height);
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            return m_LSL_Functions.osDrawRectangle(drawList, width, height);
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            return m_LSL_Functions.osDrawFilledRectangle(drawList, width, height);
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            return m_LSL_Functions.osSetFontSize(drawList, fontSize);
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            return m_LSL_Functions.osSetPenSize(drawList, penSize);
        }

        public string osSetPenColour(string drawList, string colour)
        {
            return m_LSL_Functions.osSetPenColour(drawList, colour);
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            return m_LSL_Functions.osDrawImage(drawList, width, height, imageUrl);
        }

        public void osSetStateEvents(int events)
        {
            m_LSL_Functions.osSetStateEvents(events);
        }

        public void osOpenRemoteDataChannel(string channel)
        {
            m_LSL_Functions.osOpenRemoteDataChannel(channel);
        }

        public string osGetScriptEngineName()
        {
            return m_LSL_Functions.osGetScriptEngineName();
        }

        //

        public double llList2Float(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Float(src, index);
        }


        public System.Collections.Hashtable osParseJSON(string JSON)
        { 
            return m_LSL_Functions.osParseJSON(JSON);
        }

        //for testing purposes only
        public void osSetParcelMediaTime(double time)
        {
            m_LSL_Functions.osSetParcelMediaTime(time);
        }

        // LSL CONSTANTS
        public const int TRUE = 1;
        public const int FALSE = 0;

        public const int STATUS_PHYSICS = 1;
        public const int STATUS_ROTATE_X = 2;
        public const int STATUS_ROTATE_Y = 4;
        public const int STATUS_ROTATE_Z = 8;
        public const int STATUS_PHANTOM = 16;
        public const int STATUS_SANDBOX = 32;
        public const int STATUS_BLOCK_GRAB = 64;
        public const int STATUS_DIE_AT_EDGE = 128;
        public const int STATUS_RETURN_AT_EDGE = 256;
        public const int STATUS_CAST_SHADOWS = 512;

        public const int AGENT = 1;
        public const int ACTIVE = 2;
        public const int PASSIVE = 4;
        public const int SCRIPTED = 8;

        public const int CONTROL_FWD = 1;
        public const int CONTROL_BACK = 2;
        public const int CONTROL_LEFT = 4;
        public const int CONTROL_RIGHT = 8;
        public const int CONTROL_UP = 16;
        public const int CONTROL_DOWN = 32;
        public const int CONTROL_ROT_LEFT = 256;
        public const int CONTROL_ROT_RIGHT = 512;
        public const int CONTROL_LBUTTON = 268435456;
        public const int CONTROL_ML_LBUTTON = 1073741824;

        //Permissions
        public const int PERMISSION_DEBIT = 2;
        public const int PERMISSION_TAKE_CONTROLS = 4;
        public const int PERMISSION_REMAP_CONTROLS = 8;
        public const int PERMISSION_TRIGGER_ANIMATION = 16;
        public const int PERMISSION_ATTACH = 32;
        public const int PERMISSION_RELEASE_OWNERSHIP = 64;
        public const int PERMISSION_CHANGE_LINKS = 128;
        public const int PERMISSION_CHANGE_JOINTS = 256;
        public const int PERMISSION_CHANGE_PERMISSIONS = 512;
        public const int PERMISSION_TRACK_CAMERA = 1024;
        public const int PERMISSION_CONTROL_CAMERA = 2048;

        public const int AGENT_FLYING = 1;
        public const int AGENT_ATTACHMENTS = 2;
        public const int AGENT_SCRIPTED = 4;
        public const int AGENT_MOUSELOOK = 8;
        public const int AGENT_SITTING = 16;
        public const int AGENT_ON_OBJECT = 32;
        public const int AGENT_AWAY = 64;
        public const int AGENT_WALKING = 128;
        public const int AGENT_IN_AIR = 256;
        public const int AGENT_TYPING = 512;
        public const int AGENT_CROUCHING = 1024;
        public const int AGENT_BUSY = 2048;
        public const int AGENT_ALWAYS_RUN = 4096;

        //Particle Systems
        public const int PSYS_PART_INTERP_COLOR_MASK = 1;
        public const int PSYS_PART_INTERP_SCALE_MASK = 2;
        public const int PSYS_PART_BOUNCE_MASK = 4;
        public const int PSYS_PART_WIND_MASK = 8;
        public const int PSYS_PART_FOLLOW_SRC_MASK = 16;
        public const int PSYS_PART_FOLLOW_VELOCITY_MASK = 32;
        public const int PSYS_PART_TARGET_POS_MASK = 64;
        public const int PSYS_PART_TARGET_LINEAR_MASK = 128;
        public const int PSYS_PART_EMISSIVE_MASK = 256;
        public const int PSYS_PART_FLAGS = 0;
        public const int PSYS_PART_START_COLOR = 1;
        public const int PSYS_PART_START_ALPHA = 2;
        public const int PSYS_PART_END_COLOR = 3;
        public const int PSYS_PART_END_ALPHA = 4;
        public const int PSYS_PART_START_SCALE = 5;
        public const int PSYS_PART_END_SCALE = 6;
        public const int PSYS_PART_MAX_AGE = 7;
        public const int PSYS_SRC_ACCEL = 8;
        public const int PSYS_SRC_PATTERN = 9;
        public const int PSYS_SRC_INNERANGLE = 10;
        public const int PSYS_SRC_OUTERANGLE = 11;
        public const int PSYS_SRC_TEXTURE = 12;
        public const int PSYS_SRC_BURST_RATE = 13;
        public const int PSYS_SRC_BURST_PART_COUNT = 15;
        public const int PSYS_SRC_BURST_RADIUS = 16;
        public const int PSYS_SRC_BURST_SPEED_MIN = 17;
        public const int PSYS_SRC_BURST_SPEED_MAX = 18;
        public const int PSYS_SRC_MAX_AGE = 19;
        public const int PSYS_SRC_TARGET_KEY = 20;
        public const int PSYS_SRC_OMEGA = 21;
        public const int PSYS_SRC_ANGLE_BEGIN = 22;
        public const int PSYS_SRC_ANGLE_END = 23;
        public const int PSYS_SRC_PATTERN_DROP = 1;
        public const int PSYS_SRC_PATTERN_EXPLODE = 2;
        public const int PSYS_SRC_PATTERN_ANGLE = 4;
        public const int PSYS_SRC_PATTERN_ANGLE_CONE = 8;
        public const int PSYS_SRC_PATTERN_ANGLE_CONE_EMPTY = 16;

        public const int VEHICLE_TYPE_NONE = 0;
        public const int VEHICLE_TYPE_SLED = 1;
        public const int VEHICLE_TYPE_CAR = 2;
        public const int VEHICLE_TYPE_BOAT = 3;
        public const int VEHICLE_TYPE_AIRPLANE = 4;
        public const int VEHICLE_TYPE_BALLOON = 5;
        public const int VEHICLE_LINEAR_FRICTION_TIMESCALE = 16;
        public const int VEHICLE_ANGULAR_FRICTION_TIMESCALE = 17;
        public const int VEHICLE_LINEAR_MOTOR_DIRECTION = 18;
        public const int VEHICLE_LINEAR_MOTOR_OFFSET = 20;
        public const int VEHICLE_ANGULAR_MOTOR_DIRECTION = 19;
        public const int VEHICLE_HOVER_HEIGHT = 24;
        public const int VEHICLE_HOVER_EFFICIENCY = 25;
        public const int VEHICLE_HOVER_TIMESCALE = 26;
        public const int VEHICLE_BUOYANCY = 27;
        public const int VEHICLE_LINEAR_DEFLECTION_EFFICIENCY = 28;
        public const int VEHICLE_LINEAR_DEFLECTION_TIMESCALE = 29;
        public const int VEHICLE_LINEAR_MOTOR_TIMESCALE = 30;
        public const int VEHICLE_LINEAR_MOTOR_DECAY_TIMESCALE = 31;
        public const int VEHICLE_ANGULAR_DEFLECTION_EFFICIENCY = 32;
        public const int VEHICLE_ANGULAR_DEFLECTION_TIMESCALE = 33;
        public const int VEHICLE_ANGULAR_MOTOR_TIMESCALE = 34;
        public const int VEHICLE_ANGULAR_MOTOR_DECAY_TIMESCALE = 35;
        public const int VEHICLE_VERTICAL_ATTRACTION_EFFICIENCY = 36;
        public const int VEHICLE_VERTICAL_ATTRACTION_TIMESCALE = 37;
        public const int VEHICLE_BANKING_EFFICIENCY = 38;
        public const int VEHICLE_BANKING_MIX = 39;
        public const int VEHICLE_BANKING_TIMESCALE = 40;
        public const int VEHICLE_REFERENCE_FRAME = 44;
        public const int VEHICLE_FLAG_NO_DEFLECTION_UP = 1;
        public const int VEHICLE_FLAG_LIMIT_ROLL_ONLY = 2;
        public const int VEHICLE_FLAG_HOVER_WATER_ONLY = 4;
        public const int VEHICLE_FLAG_HOVER_TERRAIN_ONLY = 8;
        public const int VEHICLE_FLAG_HOVER_GLOBAL_HEIGHT = 16;
        public const int VEHICLE_FLAG_HOVER_UP_ONLY = 32;
        public const int VEHICLE_FLAG_LIMIT_MOTOR_UP = 64;
        public const int VEHICLE_FLAG_MOUSELOOK_STEER = 128;
        public const int VEHICLE_FLAG_MOUSELOOK_BANK = 256;
        public const int VEHICLE_FLAG_CAMERA_DECOUPLED = 512;

        public const int INVENTORY_ALL = -1;
        public const int INVENTORY_NONE = -1;
        public const int INVENTORY_TEXTURE = 0;
        public const int INVENTORY_SOUND = 1;
        public const int INVENTORY_LANDMARK = 3;
        public const int INVENTORY_CLOTHING = 5;
        public const int INVENTORY_OBJECT = 6;
        public const int INVENTORY_NOTECARD = 7;
        public const int INVENTORY_SCRIPT = 10;
        public const int INVENTORY_BODYPART = 13;
        public const int INVENTORY_ANIMATION = 20;
        public const int INVENTORY_GESTURE = 21;

        public const int ATTACH_CHEST = 1;
        public const int ATTACH_HEAD = 2;
        public const int ATTACH_LSHOULDER = 3;
        public const int ATTACH_RSHOULDER = 4;
        public const int ATTACH_LHAND = 5;
        public const int ATTACH_RHAND = 6;
        public const int ATTACH_LFOOT = 7;
        public const int ATTACH_RFOOT = 8;
        public const int ATTACH_BACK = 9;
        public const int ATTACH_PELVIS = 10;
        public const int ATTACH_MOUTH = 11;
        public const int ATTACH_CHIN = 12;
        public const int ATTACH_LEAR = 13;
        public const int ATTACH_REAR = 14;
        public const int ATTACH_LEYE = 15;
        public const int ATTACH_REYE = 16;
        public const int ATTACH_NOSE = 17;
        public const int ATTACH_RUARM = 18;
        public const int ATTACH_RLARM = 19;
        public const int ATTACH_LUARM = 20;
        public const int ATTACH_LLARM = 21;
        public const int ATTACH_RHIP = 22;
        public const int ATTACH_RULEG = 23;
        public const int ATTACH_RLLEG = 24;
        public const int ATTACH_LHIP = 25;
        public const int ATTACH_LULEG = 26;
        public const int ATTACH_LLLEG = 27;
        public const int ATTACH_BELLY = 28;
        public const int ATTACH_RPEC = 29;
        public const int ATTACH_LPEC = 30;

        public const int LAND_LEVEL = 0;
        public const int LAND_RAISE = 1;
        public const int LAND_LOWER = 2;
        public const int LAND_SMOOTH = 3;
        public const int LAND_NOISE = 4;
        public const int LAND_REVERT = 5;
        public const int LAND_SMALL_BRUSH = 1;
        public const int LAND_MEDIUM_BRUSH = 2;
        public const int LAND_LARGE_BRUSH = 3;

        //Agent Dataserver
        public const int DATA_ONLINE = 1;
        public const int DATA_NAME = 2;
        public const int DATA_BORN = 3;
        public const int DATA_RATING = 4;
        public const int DATA_SIM_POS = 5;
        public const int DATA_SIM_STATUS = 6;
        public const int DATA_SIM_RATING = 7;
        public const int DATA_PAYINFO = 8;
        public const int DATA_SIM_RELEASE = 128;

        public const int ANIM_ON = 1;
        public const int LOOP = 2;
        public const int REVERSE = 4;
        public const int PING_PONG = 8;
        public const int SMOOTH = 16;
        public const int ROTATE = 32;
        public const int SCALE = 64;
        public const int ALL_SIDES = -1;
        public const int LINK_SET = -1;
        public const int LINK_ROOT = 1;
        public const int LINK_ALL_OTHERS = -2;
        public const int LINK_ALL_CHILDREN = -3;
        public const int LINK_THIS = -4;
        public const int CHANGED_INVENTORY = 1;
        public const int CHANGED_COLOR = 2;
        public const int CHANGED_SHAPE = 4;
        public const int CHANGED_SCALE = 8;
        public const int CHANGED_TEXTURE = 16;
        public const int CHANGED_LINK = 32;
        public const int CHANGED_ALLOWED_DROP = 64;
        public const int CHANGED_OWNER = 128;
        public const int CHANGED_REGION_RESTART = 256;
        public const int CHANGED_REGION = 512;
        public const int CHANGED_TELEPORT = 1024;
        public const int TYPE_INVALID = 0;
        public const int TYPE_INTEGER = 1;
        public const int TYPE_double = 2;
        public const int TYPE_STRING = 3;
        public const int TYPE_KEY = 4;
        public const int TYPE_VECTOR = 5;
        public const int TYPE_ROTATION = 6;

        //XML RPC Remote Data Channel
        public const int REMOTE_DATA_CHANNEL = 1;
        public const int REMOTE_DATA_REQUEST = 2;
        public const int REMOTE_DATA_REPLY = 3;

        //llHTTPRequest
        public const int HTTP_METHOD = 0;
        public const int HTTP_MIMETYPE = 1;
        public const int HTTP_BODY_MAXLENGTH = 2;
        public const int HTTP_VERIFY_CERT = 3;

        public const int PRIM_MATERIAL = 2;
        public const int PRIM_PHYSICS = 3;
        public const int PRIM_TEMP_ON_REZ = 4;
        public const int PRIM_PHANTOM = 5;
        public const int PRIM_POSITION = 6;
        public const int PRIM_SIZE = 7;
        public const int PRIM_ROTATION = 8;
        public const int PRIM_TYPE = 9;
        public const int PRIM_TEXTURE = 17;
        public const int PRIM_COLOR = 18;
        public const int PRIM_BUMP_SHINY = 19;
        public const int PRIM_FULLBRIGHT = 20;
        public const int PRIM_FLEXIBLE = 21;
        public const int PRIM_TEXGEN = 22;
        public const int PRIM_CAST_SHADOWS = 24; // Not implemented, here for completeness sake
        public const int PRIM_POINT_LIGHT = 23; // Huh?
        public const int PRIM_GLOW = 25;
        public const int PRIM_TEXGEN_DEFAULT = 0;
        public const int PRIM_TEXGEN_PLANAR = 1;

        public const int PRIM_TYPE_BOX = 0;
        public const int PRIM_TYPE_CYLINDER = 1;
        public const int PRIM_TYPE_PRISM = 2;
        public const int PRIM_TYPE_SPHERE = 3;
        public const int PRIM_TYPE_TORUS = 4;
        public const int PRIM_TYPE_TUBE = 5;
        public const int PRIM_TYPE_RING = 6;
        public const int PRIM_TYPE_SCULPT = 7;

        public const int PRIM_HOLE_DEFAULT = 0;
        public const int PRIM_HOLE_CIRCLE = 16;
        public const int PRIM_HOLE_SQUARE = 32;
        public const int PRIM_HOLE_TRIANGLE = 48;

        public const int PRIM_MATERIAL_STONE = 0;
        public const int PRIM_MATERIAL_METAL = 1;
        public const int PRIM_MATERIAL_GLASS = 2;
        public const int PRIM_MATERIAL_WOOD = 3;
        public const int PRIM_MATERIAL_FLESH = 4;
        public const int PRIM_MATERIAL_PLASTIC = 5;
        public const int PRIM_MATERIAL_RUBBER = 6;
        public const int PRIM_MATERIAL_LIGHT = 7;

        public const int PRIM_SHINY_NONE = 0;
        public const int PRIM_SHINY_LOW = 1;
        public const int PRIM_SHINY_MEDIUM = 2;
        public const int PRIM_SHINY_HIGH = 3;
        public const int PRIM_BUMP_NONE = 0;
        public const int PRIM_BUMP_BRIGHT = 1;
        public const int PRIM_BUMP_DARK = 2;
        public const int PRIM_BUMP_WOOD = 3;
        public const int PRIM_BUMP_BARK = 4;
        public const int PRIM_BUMP_BRICKS = 5;
        public const int PRIM_BUMP_CHECKER = 6;
        public const int PRIM_BUMP_CONCRETE = 7;
        public const int PRIM_BUMP_TILE = 8;
        public const int PRIM_BUMP_STONE = 9;
        public const int PRIM_BUMP_DISKS = 10;
        public const int PRIM_BUMP_GRAVEL = 11;
        public const int PRIM_BUMP_BLOBS = 12;
        public const int PRIM_BUMP_SIDING = 13;
        public const int PRIM_BUMP_LARGETILE = 14;
        public const int PRIM_BUMP_STUCCO = 15;
        public const int PRIM_BUMP_SUCTION = 16;
        public const int PRIM_BUMP_WEAVE = 17;

        public const int PRIM_SCULPT_TYPE_SPHERE = 1;
        public const int PRIM_SCULPT_TYPE_TORUS = 2;
        public const int PRIM_SCULPT_TYPE_PLANE = 3;
        public const int PRIM_SCULPT_TYPE_CYLINDER = 4;

        public const int MASK_BASE = 0;
        public const int MASK_OWNER = 1;
        public const int MASK_GROUP = 2;
        public const int MASK_EVERYONE = 3;
        public const int MASK_NEXT = 4;

        public const int PERM_TRANSFER = 8192;
        public const int PERM_MODIFY = 16384;
        public const int PERM_COPY = 32768;
        public const int PERM_MOVE = 524288;
        public const int PERM_ALL = 2147483647;

        public const int PARCEL_MEDIA_COMMAND_STOP = 0;
        public const int PARCEL_MEDIA_COMMAND_PAUSE = 1;
        public const int PARCEL_MEDIA_COMMAND_PLAY = 2;
        public const int PARCEL_MEDIA_COMMAND_LOOP = 3;
        public const int PARCEL_MEDIA_COMMAND_TEXTURE = 4;
        public const int PARCEL_MEDIA_COMMAND_URL = 5;
        public const int PARCEL_MEDIA_COMMAND_TIME = 6;
        public const int PARCEL_MEDIA_COMMAND_AGENT = 7;
        public const int PARCEL_MEDIA_COMMAND_UNLOAD = 8;
        public const int PARCEL_MEDIA_COMMAND_AUTO_ALIGN = 9;

        public const int PARCEL_FLAG_ALLOW_FLY = 0x1;                           // parcel allows flying
        public const int PARCEL_FLAG_ALLOW_SCRIPTS = 0x2;                       // parcel allows outside scripts
        public const int PARCEL_FLAG_ALLOW_LANDMARK = 0x8;                      // parcel allows landmarks to be created
        public const int PARCEL_FLAG_ALLOW_TERRAFORM = 0x10;                    // parcel allows anyone to terraform the land
        public const int PARCEL_FLAG_ALLOW_DAMAGE = 0x20;                       // parcel allows damage
        public const int PARCEL_FLAG_ALLOW_CREATE_OBJECTS = 0x40;               // parcel allows anyone to create objects
        public const int PARCEL_FLAG_USE_ACCESS_GROUP = 0x100;                  // parcel limits access to a group
        public const int PARCEL_FLAG_USE_ACCESS_LIST = 0x200;                   // parcel limits access to a list of residents
        public const int PARCEL_FLAG_USE_BAN_LIST = 0x400;                      // parcel uses a ban list, including restricting access based on payment info
        public const int PARCEL_FLAG_USE_LAND_PASS_LIST = 0x800;                // parcel allows passes to be purchased
        public const int PARCEL_FLAG_LOCAL_SOUND_ONLY = 0x8000;                 // parcel restricts spatialized sound to the parcel
        public const int PARCEL_FLAG_RESTRICT_PUSHOBJECT = 0x200000;            // parcel restricts llPushObject
        public const int PARCEL_FLAG_ALLOW_GROUP_SCRIPTS = 0x2000000;           // parcel allows scripts owned by group
        public const int PARCEL_FLAG_ALLOW_CREATE_GROUP_OBJECTS = 0x4000000;    // parcel allows group object creation
        public const int PARCEL_FLAG_ALLOW_ALL_OBJECT_ENTRY = 0x8000000;        // parcel allows objects owned by any user to enter
        public const int PARCEL_FLAG_ALLOW_GROUP_OBJECT_ENTRY = 0x10000000;     // parcel allows with the same group to enter

        public const int REGION_FLAG_ALLOW_DAMAGE = 0x1;                        // region is entirely damage enabled
        public const int REGION_FLAG_FIXED_SUN = 0x10;                          // region has a fixed sun position
        public const int REGION_FLAG_BLOCK_TERRAFORM = 0x40;                    // region terraforming disabled
        public const int REGION_FLAG_SANDBOX = 0x100;                           // region is a sandbox
        public const int REGION_FLAG_DISABLE_COLLISIONS = 0x1000;               // region has disabled collisions
        public const int REGION_FLAG_DISABLE_PHYSICS = 0x4000;                  // region has disabled physics
        public const int REGION_FLAG_BLOCK_FLY = 0x80000;                       // region blocks flying
        public const int REGION_FLAG_ALLOW_DIRECT_TELEPORT = 0x100000;          // region allows direct teleports
        public const int REGION_FLAG_RESTRICT_PUSHOBJECT = 0x400000;            // region restricts llPushObject

        public const int PAY_HIDE = -1;
        public const int PAY_DEFAULT = -2;

        public const string NULL_KEY = "00000000-0000-0000-0000-000000000000";
        public const string EOF = "\n\n\n";
        public const double PI = 3.14159274f;
        public const double TWO_PI = 6.28318548f;
        public const double PI_BY_TWO = 1.57079637f;
        public const double DEG_TO_RAD = 0.01745329238f;
        public const double RAD_TO_DEG = 57.29578f;
        public const double SQRT2 = 1.414213538f;
        public const int STRING_TRIM_HEAD = 1;
        public const int STRING_TRIM_TAIL = 2;
        public const int STRING_TRIM = 3;
        public const int LIST_STAT_RANGE = 0;
        public const int LIST_STAT_MIN = 1;
        public const int LIST_STAT_MAX = 2;
        public const int LIST_STAT_MEAN = 3;
        public const int LIST_STAT_MEDIAN = 4;
        public const int LIST_STAT_STD_DEV = 5;
        public const int LIST_STAT_SUM = 6;
        public const int LIST_STAT_SUM_SQUARES = 7;
        public const int LIST_STAT_NUM_COUNT = 8;
        public const int LIST_STAT_GEOMETRIC_MEAN = 9;
        public const int LIST_STAT_HARMONIC_MEAN = 100;

        //ParcelPrim Categories
        public const int PARCEL_COUNT_TOTAL = 0;
        public const int PARCEL_COUNT_OWNER = 1;
        public const int PARCEL_COUNT_GROUP = 2;
        public const int PARCEL_COUNT_OTHER = 3;
        public const int PARCEL_COUNT_SELECTED = 4;
        public const int PARCEL_COUNT_TEMP = 5;

        public const int DEBUG_CHANNEL  = 0x7FFFFFFF;
        public const int PUBLIC_CHANNEL = 0x00000000;

        public const int OBJECT_NAME = 1;
        public const int OBJECT_DESC = 2;
        public const int OBJECT_POS = 3;
        public const int OBJECT_ROT = 4;
        public const int OBJECT_VELOCITY = 5;
        public const int OBJECT_OWNER = 6;
        public const int OBJECT_GROUP = 7;
        public const int OBJECT_CREATOR = 8;

        // Can not be public const?
        public static readonly vector ZERO_VECTOR = new vector(0.0, 0.0, 0.0);
        public static readonly rotation ZERO_ROTATION = new rotation(0.0, 0, 0.0, 1.0);

        // constants for llSetCameraParams
        public const int CAMERA_PITCH = 0;
        public const int CAMERA_FOCUS_OFFSET = 1;
        public const int CAMERA_FOCUS_OFFSET_X = 2;
        public const int CAMERA_FOCUS_OFFSET_Y = 3;
        public const int CAMERA_FOCUS_OFFSET_Z = 4;
        public const int CAMERA_POSITION_LAG = 5;
        public const int CAMERA_FOCUS_LAG = 6;
        public const int CAMERA_DISTANCE = 7;
        public const int CAMERA_BEHINDNESS_ANGLE = 8;
        public const int CAMERA_BEHINDNESS_LAG = 9;
        public const int CAMERA_POSITION_THRESHOLD = 10;
        public const int CAMERA_FOCUS_THRESHOLD = 11;
        public const int CAMERA_ACTIVE = 12;
        public const int CAMERA_POSITION = 13;
        public const int CAMERA_POSITION_X = 14;
        public const int CAMERA_POSITION_Y = 15;
        public const int CAMERA_POSITION_Z = 16;
        public const int CAMERA_FOCUS = 17;
        public const int CAMERA_FOCUS_X = 18;
        public const int CAMERA_FOCUS_Y = 19;
        public const int CAMERA_FOCUS_Z = 20;
        public const int CAMERA_POSITION_LOCKED = 21;
        public const int CAMERA_FOCUS_LOCKED = 22;

        // constants for llGetParcelDetails
        public const int PARCEL_DETAILS_NAME = 0;
        public const int PARCEL_DETAILS_DESC = 1;
        public const int PARCEL_DETAILS_OWNER = 2;
        public const int PARCEL_DETAILS_GROUP = 3;
        public const int PARCEL_DETAILS_AREA = 4;
    }
}
