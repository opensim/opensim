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
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using OpenSim.Region.ScriptEngine.Common;
using integer = System.Int32;
using key = System.String;
using vector = OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3;
using rotation = OpenSim.Region.ScriptEngine.Common.LSL_Types.Quaternion;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSL
{
    //[Serializable]
    public class LSL_BaseClass : MarshalByRefObject, LSL_BuiltIn_Commands_Interface, IScript
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Object never expires
        public override Object InitializeLifetimeService()
        {
            //Console.WriteLine("LSL_BaseClass: InitializeLifetimeService()");
            //            return null;
            ILease lease = (ILease) base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero; // TimeSpan.FromMinutes(1);
                //lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                //lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }


        private Executor m_Exec;

        public Executor Exec
        {
            get
            {
                if (m_Exec == null)
                    m_Exec = new Executor(this);
                return m_Exec;
            }
        }

        public LSL_BuiltIn_Commands_Interface m_LSL_Functions;
        public string SourceCode = "";

        public LSL_BaseClass()
        {
        }

        public string State()
        {
            return m_LSL_Functions.State();
        }


        public void Start(LSL_BuiltIn_Commands_Interface LSL_Functions)
        {
            m_LSL_Functions = LSL_Functions;

            //m_log.Info("[ScriptEngine]: LSL_BaseClass.Start() called.");

            // Get this AppDomain's settings and display some of them.
            AppDomainSetup ads = AppDomain.CurrentDomain.SetupInformation;
            Console.WriteLine("AppName={0}, AppBase={1}, ConfigFile={2}",
                              ads.ApplicationName,
                              ads.ApplicationBase,
                              ads.ConfigurationFile
                );

            // Display the name of the calling AppDomain and the name
            // of the second domain.
            // NOTE: The application's thread has transitioned between
            // AppDomains.
            Console.WriteLine("Calling to '{0}'.",
                              Thread.GetDomain().FriendlyName
                );

            return;
        }


        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        // They are only forwarders to LSL_BuiltIn_Commands.cs
        //
        public double llSin(double f)
        {
            return m_LSL_Functions.llSin(f);
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

        public int llAbs(int i)
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

        public int llFloor(double f)
        {
            return m_LSL_Functions.llFloor(f);
        }

        public int llCeil(double f)
        {
            return m_LSL_Functions.llCeil(f);
        }

        public int llRound(double f)
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

        public int llListen(int channelID, string name, string ID, string msg)
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

        public int llDetectedType(int number)
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

        public int llDetectedGroup(int number)
        {
            return m_LSL_Functions.llDetectedGroup(number);
        }

        public int llDetectedLinkNumber(int number)
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

        public int llGetStatus(int status)
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

        public int llTarget(vector position, double range)
        {
            return m_LSL_Functions.llTarget(position, range);
        }

        public void llTargetRemove(int number)
        {
            m_LSL_Functions.llTargetRemove(number);
        }

        public int llRotTarget(rotation rot, double error)
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

        public int llGiveMoney(string destination, int amount)
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

        public void llRezObject(string inventory, vector pos, rotation rot, int param)
        {
            m_LSL_Functions.llRezObject(inventory, pos, rot, param);
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

        public void llTakeCamera()
        {
            m_LSL_Functions.llTakeCamera();
        }

        public void llReleaseCamera()
        {
            m_LSL_Functions.llReleaseCamera();
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
        public int llStringLength(string str)
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

        public int llGetStartParameter()
        {
            return m_LSL_Functions.llGetStartParameter();
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

        public int llGetPermissions()
        {
            return m_LSL_Functions.llGetPermissions();
        }

        public int llGetLinkNumber()
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

        public void llGetLinkName(int linknum)
        {
            m_LSL_Functions.llGetLinkName(linknum);
        }

        public int llGetInventoryNumber(int type)
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

        public int llGetNumberOfSides()
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

        public void llRot2Angle()
        {
            m_LSL_Functions.llRot2Angle();
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

        public int llSubStringIndex(string source, string pattern)
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

        public List<string> llListSort(List<string> src, int stride, int ascending)
        {
            return m_LSL_Functions.llListSort(src, stride, ascending);
        }

        public int llGetListLength(List<string> src)
        {
            return m_LSL_Functions.llGetListLength(src);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public int llList2Integer(List<string> src, int index)
        {
            return m_LSL_Functions.llList2Integer(src, index);
        }

        public double llList2double(List<string> src, int index)
        {
            return m_LSL_Functions.llList2double(src, index);
        }

        public string llList2String(List<string> src, int index)
        {
            return m_LSL_Functions.llList2String(src, index);
        }

        public string llList2Key(List<string> src, int index)
        {
            return m_LSL_Functions.llList2Key(src, index);
        }

        public vector llList2Vector(List<string> src, int index)
        {
            return m_LSL_Functions.llList2Vector(src, index);
        }

        public rotation llList2Rot(List<string> src, int index)
        {
            return m_LSL_Functions.llList2Rot(src, index);
        }

        public List<string> llList2List(List<string> src, int start, int end)
        {
            return m_LSL_Functions.llList2List(src, start, end);
        }

        public List<string> llDeleteSubList(List<string> src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubList(src, start, end);
        }

        public int llGetListEntryType(List<string> src, int index)
        {
            return m_LSL_Functions.llGetListEntryType(src, index);
        }

        public string llList2CSV(List<string> src)
        {
            return m_LSL_Functions.llList2CSV(src);
        }

        public List<string> llCSV2List(string src)
        {
            return m_LSL_Functions.llCSV2List(src);
        }

        public List<string> llListRandomize(List<string> src, int stride)
        {
            return m_LSL_Functions.llListRandomize(src, stride);
        }

        public List<string> llList2ListStrided(List<string> src, int start, int end, int stride)
        {
            return m_LSL_Functions.llList2ListStrided(src, start, end, stride);
        }

        public vector llGetRegionCorner()
        {
            return m_LSL_Functions.llGetRegionCorner();
        }

        public List<string> llListInsertList(List<string> dest, List<string> src, int start)
        {
            return m_LSL_Functions.llListInsertList(dest, src, start);
        }

        public int llListFindList(List<string> src, List<string> test)
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

        public int llEdgeOfWorld(vector pos, vector dir)
        {
            return m_LSL_Functions.llEdgeOfWorld(pos, dir);
        }

        public int llGetAgentInfo(string id)
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

        public void llParseString2List()
        {
            m_LSL_Functions.llParseString2List();
        }

        public int llOverMyLand(string id)
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

        public int llSameGroup(string agent)
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

        public int llGetAttached()
        {
            return m_LSL_Functions.llGetAttached();
        }

        public int llGetFreeMemory()
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
        public void llParticleSystem(List<Object> rules)
        {
            m_LSL_Functions.llParticleSystem(rules);
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            m_LSL_Functions.llGroundRepel(height, water, tau);
        }

        public void llGiveInventoryList()
        {
            m_LSL_Functions.llGiveInventoryList();
        }

        public void llSetVehicleType(int type)
        {
            m_LSL_Functions.llSetVehicleType(type);
        }

        public void llSetVehicledoubleParam(int param, double value)
        {
            m_LSL_Functions.llSetVehicledoubleParam(param, value);
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

        public void llDumpList2String()
        {
            m_LSL_Functions.llDumpList2String();
        }

        public void llScriptDanger(vector pos)
        {
            m_LSL_Functions.llScriptDanger(pos);
        }

        public void llDialog(string avatar, string message, List<string> buttons, int chat_channel)
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

        public int llGetScriptState(string name)
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

        public void llSetPrimitiveParams(List<string> rules)
        {
            m_LSL_Functions.llSetPrimitiveParams(rules);
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

        public List<string> llGetAnimationList(string id)
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

        public int llGetNumberOfPrims()
        {
            return m_LSL_Functions.llGetNumberOfPrims();
        }

        public string llGetNumberOfNotecardLines(string name)
        {
            return m_LSL_Functions.llGetNumberOfNotecardLines(name);
        }

        public List<string> llGetBoundingBox(string obj)
        {
            return m_LSL_Functions.llGetBoundingBox(obj);
        }

        public vector llGetGeometricCenter()
        {
            return m_LSL_Functions.llGetGeometricCenter();
        }

        public void llGetPrimitiveParams()
        {
            m_LSL_Functions.llGetPrimitiveParams();
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public string llIntegerToBase64(int number)
        {
            return m_LSL_Functions.llIntegerToBase64(number);
        }

        public int llBase64ToInteger(string str)
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

        public List<string> llParseStringKeepNulls(string src, List<string> seperators, List<string> spacers)
        {
            return m_LSL_Functions.llParseStringKeepNulls(src, seperators, spacers);
        }

        public void llRezAtRoot(string inventory, vector position, vector velocity, rotation rot, int param)
        {
            m_LSL_Functions.llRezAtRoot(inventory, position, velocity, rot, param);
        }

        public int llGetObjectPermMask(int mask)
        {
            return m_LSL_Functions.llGetObjectPermMask(mask);
        }

        public void llSetObjectPermMask(int mask, int value)
        {
            m_LSL_Functions.llSetObjectPermMask(mask, value);
        }

        public void llGetInventoryPermMask(string item, int mask)
        {
            m_LSL_Functions.llGetInventoryPermMask(item, mask);
        }

        public void llSetInventoryPermMask(string item, int mask, int value)
        {
            m_LSL_Functions.llSetInventoryPermMask(item, mask, value);
        }

        public string llGetInventoryCreator(string item)
        {
            return m_LSL_Functions.llGetInventoryCreator(item);
        }

        public void llRequestSimulatorData(string simulator, int data)
        {
            m_LSL_Functions.llRequestSimulatorData(simulator, data);
        }

        public void llForceMouselook(int mouselook)
        {
            m_LSL_Functions.llForceMouselook(mouselook);
        }

        public double llGetObjectMass(string id)
        {
            return m_LSL_Functions.llGetObjectMass(id);
        }

        public void llListReplaceList()
        {
            m_LSL_Functions.llListReplaceList();
        }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            m_LSL_Functions.llLoadURL(avatar_id, message, url);
        }

        public void llParcelMediaCommandList(List<string> commandList)
        {
            m_LSL_Functions.llParcelMediaCommandList(commandList);
        }

        public void llParcelMediaQuery()
        {
            m_LSL_Functions.llParcelMediaQuery();
        }

        public int llModPow(int a, int b, int c)
        {
            return m_LSL_Functions.llModPow(a, b, c);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public int llGetInventoryType(string name)
        {
            return m_LSL_Functions.llGetInventoryType(name);
        }

        public void llSetPayPrice(int price, List<string> quick_pay_buttons)
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

        public void llSetCameraParams(List<string> rules)
        {
            m_LSL_Functions.llSetCameraParams(rules);
        }

        public void llClearCameraParams()
        {
            m_LSL_Functions.llClearCameraParams();
        }

        public double llListStatistics(int operation, List<string> src)
        {
            return m_LSL_Functions.llListStatistics(operation, src);
        }

        public int llGetUnixTime()
        {
            return m_LSL_Functions.llGetUnixTime();
        }

        public int llGetParcelFlags(vector pos)
        {
            return m_LSL_Functions.llGetParcelFlags(pos);
        }

        public int llGetRegionFlags()
        {
            return m_LSL_Functions.llGetRegionFlags();
        }

        public string llXorBase64StringsCorrect(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64StringsCorrect(str1, str2);
        }

        public void llHTTPRequest(string url, List<string> parameters, string body)
        {
            m_LSL_Functions.llHTTPRequest(url, parameters, body);
        }

        public void llResetLandBanList()
        {
            m_LSL_Functions.llResetLandBanList();
        }

        public void llResetLandPassList()
        {
            m_LSL_Functions.llResetLandPassList();
        }

        public int llGetParcelPrimCount(vector pos, int category, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelPrimCount(pos, category, sim_wide);
        }

        public List<string> llGetParcelPrimOwners(vector pos)
        {
            return m_LSL_Functions.llGetParcelPrimOwners(pos);
        }

        public int llGetObjectPrimCount(string object_id)
        {
            return m_LSL_Functions.llGetObjectPrimCount(object_id);
        }

        //
        // DO NOT MODIFY HERE: MODIFY IN LSL_BuiltIn_Commands.cs
        //
        public int llGetParcelMaxPrims(vector pos, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelMaxPrims(pos, sim_wide);
        }

        public List<string> llGetParcelDetails(vector pos, List<string> param)
        {
            return m_LSL_Functions.llGetParcelDetails(pos, param);
        }

        //
        // OpenSim Functions
        //
        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            return m_LSL_Functions.osSetDynamicTextureURL(dynamicID, contentType, url, extraParams, timer);
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
        public const int DATA_ONLINE = 1;
        public const int DATA_NAME = 2;
        public const int DATA_BORN = 3;
        public const int DATA_RATING = 4;
        public const int DATA_SIM_POS = 5;
        public const int DATA_SIM_STATUS = 6;
        public const int DATA_SIM_RATING = 7;
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
        public const int TYPE_INVALID = 0;
        public const int TYPE_INTEGER = 1;
        public const int TYPE_double = 2;
        public const int TYPE_STRING = 3;
        public const int TYPE_KEY = 4;
        public const int TYPE_VECTOR = 5;
        public const int TYPE_ROTATION = 6;
        public const int REMOTE_DATA_CHANNEL = 1;
        public const int REMOTE_DATA_REQUEST = 2;
        public const int REMOTE_DATA_REPLY = 3;
        //public const int PRIM_TYPE = 1;
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
        public const int PRIM_TEXGEN_DEFAULT = 0;
        public const int PRIM_TEXGEN_PLANAR = 1;
        public const int PRIM_TYPE_BOX = 0;
        public const int PRIM_TYPE_CYLINDER = 1;
        public const int PRIM_TYPE_PRISM = 2;
        public const int PRIM_TYPE_SPHERE = 3;
        public const int PRIM_TYPE_TORUS = 4;
        public const int PRIM_TYPE_TUBE = 5;
        public const int PRIM_TYPE_RING = 6;
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
        public const int DEBUG_CHANNEL  0x7FFFFFFF;
        public const int PUBLIC_CHANNEL 0x00000000

        // Can not be public const?
        public static readonly vector ZERO_VECTOR = new vector(0.0, 0.0, 0.0);
        public static readonly rotation ZERO_ROTATION = new rotation(0.0, 0.0, 0.0, 1.0);
    }
}
