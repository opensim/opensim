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
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using key = System.String;
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass : MarshalByRefObject
    {
        public ILSL_Api m_LSL_Functions;

        public void ApiTypeLSL(IScriptApi api)
        {
            if (!(api is ILSL_Api))
                return;

            m_LSL_Functions = (ILSL_Api)api;
        }

        public void state(string newState)
        {
            m_LSL_Functions.state(newState);
        }

        //
        // Script functions
        //
        public void llSay(int channelID, string text)
        {
            m_LSL_Functions.llSay(channelID, text);
        }

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

        public void llShout(int channelID, string text)
        {
            m_LSL_Functions.llShout(channelID, text);
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

        public LSL_Types.LSLInteger llList2Integer(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Integer(src, index);
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

        public void llOwnerSay(string msg)
        {
            m_LSL_Functions.llOwnerSay(msg);
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

        public double llList2Float(LSL_Types.list src, int index)
        {
            return m_LSL_Functions.llList2Float(src, index);
        }
    }
}
