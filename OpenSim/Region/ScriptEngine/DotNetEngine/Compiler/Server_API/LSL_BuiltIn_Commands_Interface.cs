/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler
{
    public interface LSL_BuiltIn_Commands_Interface
    {
        float llSin(float f);
        float llCos(float f);
        float llTan(float f);
        float llAtan2(float x, float y);
        float llSqrt(float f);
        float llPow(float fbase, float fexponent);
        UInt32 llAbs(Int32 i);
        float llFabs(float f);
        float llFrand(float mag);
        UInt32 llFloor(float f);
        UInt32 llCeil(float f);
        UInt32 llRound(float f);
        float llVecMag(Axiom.Math.Vector3 v);
        Axiom.Math.Vector3 llVecNorm(Axiom.Math.Vector3 v);
        float llVecDist(Axiom.Math.Vector3 a, Axiom.Math.Vector3 b);
        Axiom.Math.Vector3 llRot2Euler(Axiom.Math.Quaternion r);
        Axiom.Math.Quaternion llEuler2Rot(Axiom.Math.Vector3 v);
        Axiom.Math.Quaternion llAxes2Rot(Axiom.Math.Vector3 fwd, Axiom.Math.Vector3 left, Axiom.Math.Vector3 up);
        Axiom.Math.Vector3 llRot2Fwd(Axiom.Math.Quaternion r);
        Axiom.Math.Vector3 llRot2Left(Axiom.Math.Quaternion r);
        Axiom.Math.Vector3 llRot2Up(Axiom.Math.Quaternion r);
        Axiom.Math.Quaternion llRotBetween(Axiom.Math.Vector3 start, Axiom.Math.Vector3 end);
        void llWhisper(int channelID, string text);
        //void llSay(UInt32 channelID, string text);
        void llSay(int channelID, string text);
        void llShout(UInt16 channelID, string text);
        UInt32 llListen(UInt16 channelID, string name, string ID, string msg);
        void llListenControl(UInt32 number, UInt32 active);
        void llListenRemove(UInt32 number);
        void llSensor(string name, string id, UInt32 type, float range, float arc);
        void llSensorRepeat(string name, string id, UInt32 type, float range, float arc, float rate);
        void llSensorRemove();
        string llDetectedName(UInt32 number);
        string llDetectedKey(UInt32 number);
        string llDetectedOwner(UInt32 number);
        UInt32 llDetectedType(UInt32 number);
        Axiom.Math.Vector3 llDetectedPos(UInt32 number);
        Axiom.Math.Vector3 llDetectedVel(UInt32 number);
        Axiom.Math.Vector3 llDetectedGrab(UInt32 number);
        Axiom.Math.Quaternion llDetectedRot(UInt32 number);
        UInt32 llDetectedGroup(UInt32 number);
        UInt32 llDetectedLinkNumber(UInt32 number);
        void llDie();
        float llGround(Axiom.Math.Vector3 offset);
        float llCloud(Axiom.Math.Vector3 offset);
        Axiom.Math.Vector3 llWind(Axiom.Math.Vector3 offset);
        void llSetStatus(UInt32 status, UInt32 value);
        UInt32 llGetStatus(UInt32 status);
        void llSetScale(Axiom.Math.Vector3 scale);
        Axiom.Math.Vector3 llGetScale();
        void llSetColor(Axiom.Math.Vector3 color, UInt32 face);
        float llGetAlpha(UInt32 face);
        void llSetAlpha(float alpha, UInt32 face);
        Axiom.Math.Vector3 llGetColor(UInt32 face);
        void llSetTexture(string texture, UInt32 face);
        void llScaleTexture(float u, float v, UInt32 face);
        void llOffsetTexture(float u, float v, UInt32 face);
        void llRotateTexture(float rotation, UInt32 face);
        string llGetTexture(UInt32 face);
        void llSetPos(Axiom.Math.Vector3 pos);

        //wiki: vector llGetPos()
        Axiom.Math.Vector3 llGetPos();
        //wiki: vector llGetLocalPos()
        Axiom.Math.Vector3 llGetLocalPos();
        //wiki: llSetRot(rotation rot)
        void llSetRot(Axiom.Math.Quaternion rot);
        //wiki: rotation llGetRot()
        Axiom.Math.Quaternion llGetRot();
        //wiki: rotation llGetLocalRot()
        Axiom.Math.Quaternion llGetLocalRot();
        //wiki: llSetForce(vector force, integer local)
        void llSetForce(Axiom.Math.Vector3 force, Int32 local);
        //wiki: vector llGetForce()
        Axiom.Math.Vector3 llGetForce();
        //wiki: integer llTarget(vector position, float range)
        Int32 llTarget(Axiom.Math.Vector3 position, float range);
        //wiki: llTargetRemove(integer number)
        void llTargetRemove(Int32 number);
        //wiki: integer llRotTarget(rotation rot, float error)
        Int32 llRotTarget(Axiom.Math.Quaternion rot, float error);
        //wiki: integer llRotTargetRemove(integer number)
        void llRotTargetRemove(Int32 number);
        //wiki: llMoveToTarget(vector target, float tau)
        void llMoveToTarget(Axiom.Math.Vector3 target, float tau);
        //wiki: llStopMoveToTarget()
        void llStopMoveToTarget();
        //wiki: llApplyImpulse(vector force, integer local)
        void llApplyImpulse(Axiom.Math.Vector3 force, Int32 local);
        //wiki: llapplyRotationalImpulse(vector force, integer local)
        void llApplyRotationalImpulse(Axiom.Math.Vector3 force, Int32 local);
        //wiki: llSetTorque(vector torque, integer local)
        void llSetTorque(Axiom.Math.Vector3 torque, Int32 local);
        //wiki: vector llGetTorque()
        Axiom.Math.Vector3 llGetTorque();
        //wiki: llSeForceAndTorque(vector force, vector torque, integer local)
        void llSetForceAndTorque(Axiom.Math.Vector3 force, Axiom.Math.Vector3 torque, Int32 local);
        //wiki: vector llGetVel()
        Axiom.Math.Vector3 llGetVel();
        //wiki: vector llGetAccel()
        Axiom.Math.Vector3 llGetAccel();
        //wiki: vector llGetOmega()
        Axiom.Math.Vector3 llGetOmega();
        //wiki: float llGetTimeOfDay()
        float llGetTimeOfDay();
        //wiki: float llGetWallclock()
        float llGetWallclock();
        //wiki: float llGetTime()
        float llGetTime();
        //wiki: llResetTime()
        void llResetTime();
        //wiki: float llGetAndResetTime()
        float llGetAndResetTime();
        //wiki (deprecated) llSound(string sound, float volume, integer queue, integer loop)
        void llSound();
        //wiki: llPlaySound(string sound, float volume)
        void llPlaySound(string sound, float volume);
        //wiki: llLoopSound(string sound, float volume)
        void llLoopSound(string sound, float volume);
        //wiki: llLoopSoundMaster(string sound, float volume)
        void llLoopSoundMaster(string sound, float volume);
        //wiki: llLoopSoundSlave(string sound, float volume)
        void llLoopSoundSlave(string sound, float volume);
        //wiki llPlaySoundSlave(string sound, float volume)
        void llPlaySoundSlave(string sound, float volume);
        //wiki: llTriggerSound(string sound, float volume)
        void llTriggerSound(string sound, float volume);
        //wiki: llStopSound()
        void llStopSound();
        //wiki: llPreloadSound(string sound)
        void llPreloadSound(string sound);
        //wiki: string llGetSubString(string src, integer start, integer end)
        void llGetSubString(string src, Int32 start, Int32 end);
        //wiki: string llDeleteSubString(string src, integer start, integer end)
        string llDeleteSubString(string src, Int32 start, Int32 end);
        //wiki string llInsertString(string dst, integer position, string src)
        void llInsertString(string dst, Int32 position, string src);
        //wiki: string llToUpper(string source)
        string llToUpper(string source);
        //wiki: string llToLower(string source)
        string llToLower(string source);
        //wiki: integer llGiveMoney(key destination, integer amount)
        Int32 llGiveMoney(string destination, Int32 amount);
        //wiki: (deprecated)
        void llMakeExplosion();
        //wiki: (deprecated)
        void llMakeFountain();
        //wiki: (deprecated)
        void llMakeSmoke();
        //wiki: (deprecated)
        void llMakeFire();
        //wiki: llRezObject(string inventory, vector pos, vector rel, rotation rot, integer param)
        void llRezObject(string inventory, Axiom.Math.Vector3 pos, Axiom.Math.Quaternion rot, Int32 param);
        //wiki: llLookAt(vector target, float strength, float damping)
        void llLookAt(Axiom.Math.Vector3 target, float strength, float damping);
        //wiki: llStopLookAt()
        void llStopLookAt();
        //wiki: llSetTimerEvent(float sec)
        void llSetTimerEvent(float sec);
        //wiki: llSleep(float sec)
        void llSleep(float sec);
        //wiki: float llGetMass()
        float llGetMass();
        //wiki: llCollisionFilter(string name, key id, integer accept)
        void llCollisionFilter(string name, string id, Int32 accept);
        //wiki: llTakeControls(integer controls, integer accept, integer pass_on)
        void llTakeControls(Int32 controls, Int32 accept, Int32 pass_on);
        //wiki: llReleaseControls()
        void llReleaseControls();
        //wiki: llAttachToAvatar(integer attachment)
        void llAttachToAvatar(Int32 attachment);
        //wiki: llDetachFromAvatar()
        void llDetachFromAvatar();
        //wiki: (deprecated) llTakeCamera()
        void llTakeCamera();
        //wiki: (deprecated) llReleaseCamera()
        void llReleaseCamera();
        //wiki: key llGetOwner()
        string llGetOwner();
        //wiki: llInstantMessage(key user, string message)
        void llInstantMessage(string user, string message);
        //wiki: llEmail(string address, string subject, string message)
        void llEmail(string address, string subject, string message);
        //wiki: llGetNextEmail(string address, string subject)
        void llGetNextEmail(string address, string subject);
        //wiki:    key llGetKey()
        string llGetKey();
        //wiki: llSetBuoyancy(float buoyancy)
        void llSetBuoyancy(float buoyancy);
        //wiki: llSetHoverHeight(float height, integer water, float tau)
        void llSetHoverHeight(float height, Int32 water, float tau);
        //wiki: llStopHover
        void llStopHover();
        //wiki: llMinEventDelay(float delay)
        void llMinEventDelay(float delay);
        //wiki: (deprecated) llSoundPreload()
        void llSoundPreload();
        //wiki: llRotLookAt(rotation target, float strength, float damping)
        void llRotLookAt(Axiom.Math.Quaternion target, float strength, float damping);
        //wiki: integer llStringLength(string str)
        Int32 llStringLength(string str);
        //wiki: llStartAnimation(string anim)
        void llStartAnimation(string anim);
        //wiki: llStopAnimation(string anim)
        void llStopAnimation(string anim);
        //wiki: (deprecated) llPointAt
        void llPointAt();
        //wiki: (deprecated) llStopPointAt
        void llStopPointAt();
        //wiki: llTargetOmega(vector axis, float spinrate, float gain)
        void llTargetOmega(Axiom.Math.Vector3 axis, float spinrate, float gain);
        //wiki: integer llGetStartParameter()
        Int32 llGetStartParameter();
        //wiki: llGodLikeRezObject(key inventory, vector pos)
        void llGodLikeRezObject(string inventory, Axiom.Math.Vector3 pos);
        //wiki: llRequestPermissions(key agent, integer perm)
        void llRequestPermissions(string agent, Int32 perm);
        //wiki: key llGetPermissionsKey()
        string llGetPermissionsKey();
        //wiki: integer llGetPermissions()
        Int32 llGetPermissions();
        //wiki integer llGetLinkNumber()
        Int32 llGetLinkNumber();
        //wiki: llSetLinkColor(integer linknumber, vector color, integer face)
        void llSetLinkColor(Int32 linknumber, Axiom.Math.Vector3 color, Int32 face);
        //wiki: llCreateLink(key target, integer parent)
        void llCreateLink(string target, Int32 parent);
        //wiki: llBreakLink(integer linknum)
        void llBreakLink(Int32 linknum);
        //wiki: llBreakAllLinks()
        void llBreakAllLinks();
        //wiki: key llGetLinkKey(integer linknum)
        string llGetLinkKey(Int32 linknum);
        //wiki: llGetLinkName(integer linknum)
        void llGetLinkName(Int32 linknum);
        //wiki: integer llGetInventoryNumber(integer type)
        Int32 llGetInventoryNumber(Int32 type);
        //wiki: string llGetInventoryName(integer type, integer number)
        string llGetInventoryName(Int32 type, Int32 number);
        //wiki: llSetScriptState(string name, integer run)
        void llSetScriptState(string name, Int32 run);
        //wiki: float llGetEnergy()
        float llGetEnergy();
        //wiki: llGiveInventory(key destination, string inventory)
        void llGiveInventory(string destination, string inventory);
        //wiki: llRemoveInventory(string item)
        void llRemoveInventory(string item);
        //wiki: llSetText(string text, vector color, float alpha)
        void llSetText(string text, Axiom.Math.Vector3 color, float alpha);
        //wiki: float llWater(vector offset)
        float llWater(Axiom.Math.Vector3 offset);
        //wiki: llPassTouches(integer pass)
        void llPassTouches(Int32 pass);
        //wiki: key llRequestAgentData(key id, integer data)
        string llRequestAgentData(string id, Int32 data);
        //wiki: key llRequestInventoryData(string name)
        string llRequestInventoryData(string name);
        //wiki: llSetDamage(float damage)
        void llSetDamage(float damage);
        //wiki: llTeleportAgentHome(key agent)
        void llTeleportAgentHome(string agent);
        //wiki: llModifyLand(integer action, integer brush)
        void llModifyLand(Int32 action, Int32 brush);
        //wiki: llCollisionSound(string impact_sound, float impact_volume)
        void llCollisionSound(string impact_sound, float impact_volume);
        //wiki: llCollisionSprite(string impact_sprite)
        void llCollisionSprite(string impact_sprite);
        //wiki: string llGetAnimation(key id)
        string llGetAnimation(string id);
        //wiki: llResetScript()
        void llResetScript();
        //wiki: llMessageLinked(integer linknum, integer num, string str, key id)
        void llMessageLinked(Int32 linknum, Int32 num, string str, string id);
        //wiki: llPushObject(key target, vector impulse, vector ang_impulse, integer local)
        void llPushObject(string target, Axiom.Math.Vector3 impulse, Axiom.Math.Vector3 ang_impulse, Int32 local);
        //wiki: llPassCollisions(integer pass)
        void llPassCollisions(Int32 pass);
        //wiki: string llGetScriptName()
        string llGetScriptName();
        //wiki: integer llGetNumberOfSides()
        Int32 llGetNumberOfSides();
        //wiki: rotation llAxisAngle2Rot(vector axis, float angle)
        Axiom.Math.Quaternion llAxisAngle2Rot(Axiom.Math.Vector3 axis, float angle);
        //wiki: vector llRot2Axis(rotation rot)
        Axiom.Math.Vector3 llRot2Axis(Axiom.Math.Quaternion rot);
        void llRot2Angle();
        //wiki: float llAcos(float val)
        float llAcos(float val);
        //wiki: float llAsin(float val)
        float llAsin(float val);
        //wiki: float llAngleBetween(rotation a, rotation b)
        float llAngleBetween(Axiom.Math.Quaternion a, Axiom.Math.Quaternion b);
        //wiki: string llGetInventoryKey(string name)
        string llGetInventoryKey(string name);
        //wiki: llAllowInventoryDrop(integer add)
        void llAllowInventoryDrop(Int32 add);
        //wiki: vector llGetSunDirection()
        Axiom.Math.Vector3 llGetSunDirection();
        //wiki: vector llGetTextureOffset(integer face)
        Axiom.Math.Vector3 llGetTextureOffset(Int32 face);
        //wiki: vector llGetTextureScale(integer side)
        Axiom.Math.Vector3 llGetTextureScale(Int32 side);
        //wiki: float llGetTextureRot(integer side)
        float llGetTextureRot(Int32 side);
        //wiki: integer llSubStringIndex(string source, string pattern)
        Int32 llSubStringIndex(string source, string pattern);
        //wiki: key llGetOwnerKey(key id)
        string llGetOwnerKey(string id);
        //wiki: vector llGetCenterOfMass()
        Axiom.Math.Vector3 llGetCenterOfMass();
        //wiki: list llListSort(list src, integer stride, integer ascending)
        void llListSort();
        void llGetListLength();
        void llList2Integer();
        void llList2Float();
        void llList2String();
        void llList2Key();
        void llList2Vector();
        void llList2Rot();
        void llList2List();
        void llDeleteSubList();
        void llGetListEntryType();
        void llList2CSV();
        void llCSV2List();
        void llListRandomize();
        void llList2ListStrided();
        void llGetRegionCorner();
        void llListInsertList();
        void llListFindList();
        //wiki: string llGetObjectName()
        string llGetObjectName();
        //wiki: llSetObjectName(string name)
        void llSetObjectName(string name);
        //wiki: string llGetDate()
        string llGetDate();
        //wiki: integer llEdgeOfWorld(vector pos, vector dir)
        Int32 llEdgeOfWorld(Axiom.Math.Vector3 pos, Axiom.Math.Vector3 dir);
        //wiki: integer llGetAgentInfo(key id)
        Int32 llGetAgentInfo(string id);
        //wiki: llAdjustSoundVolume(float volume)
        void llAdjustSoundVolume(float volume);
        //wiki: llSetSoundQueueing(integer queue)
        void llSetSoundQueueing(Int32 queue);
        //wiki: llSetSoundRadius(float radius)
        void llSetSoundRadius(float radius);
        //wiki: string llKey2Name(key id)
        string llKey2Name(string id);
        //wiki: llSetTextureAnim(integer mode, integer face, integer sizex, integer sizey, float start, float length, float rate)
        void llSetTextureAnim(Int32 mode, Int32 face, Int32 sizex, Int32 sizey, float start, float length, float rate);
        //wiki: llTriggerSoundLimited(string sound, float volume, vector top_north_east, vector bottom_south_west)
        void llTriggerSoundLimited(string sound, float volume, Axiom.Math.Vector3 top_north_east, Axiom.Math.Vector3 bottom_south_west);
        //wiki: llEjectFromLand(key pest)
        void llEjectFromLand(string pest);
        void llParseString2List();
        //wiki: integer llOverMyLand(key id)
        Int32 llOverMyLand(string id);
        //wiki: key llGetLandOwnerAt(vector pos)
        string llGetLandOwnerAt(Axiom.Math.Vector3 pos);
        //wiki: key llGetNotecardLine(string name, integer line)
        string llGetNotecardLine(string name, Int32 line);
        //wiki: vector llGetAgentSize(key id)
        Axiom.Math.Vector3 llGetAgentSize(string id);
        //wiki: integer llSameGroup(key agent)
        Int32 llSameGroup(string agent);
        //wiki: llUnSit(key id)
        void llUnSit(string id);
        //wiki: vector llGroundSlope(vector offset)
        Axiom.Math.Vector3 llGroundSlope(Axiom.Math.Vector3 offset);
        //wiki: vector llGroundNormal(vector offset)
        Axiom.Math.Vector3 llGroundNormal(Axiom.Math.Vector3 offset);
        //wiki: vector llGroundContour(vector offset)
        Axiom.Math.Vector3 llGroundContour(Axiom.Math.Vector3 offset);
        //wiki: integer llGetAttached()
        Int32 llGetAttached();
        //wiki: integer llGetFreeMemory()
        Int32 llGetFreeMemory();
        //wiki: string llGetRegionName()
        string llGetRegionName();
        //wiki: float llGetRegionTimeDilation()
        float llGetRegionTimeDilation();
        //wiki: float llGetRegionFPS()
        float llGetRegionFPS();
        //wiki: llParticleSystem(List<Object> rules
        void llParticleSystem(List<Object> rules);
        //wiki: llGroundRepel(float height, integer water, float tau)
        void llGroundRepel(float height, Int32 water, float tau);
        void llGiveInventoryList();
        //wiki: llSetVehicleType(integer type)
        void llSetVehicleType(Int32 type);
        //wiki: llSetVehicleFloatParam(integer param, float value)
        void llSetVehicleFloatParam(Int32 param, float value);
        //wiki: llSetVehicleVectorParam(integer param, vector vec)
        void llSetVehicleVectorParam(Int32 param, Axiom.Math.Vector3 vec);
        //wiki: llSetVehicleRotationParam(integer param, rotation rot)
        void llSetVehicleRotationParam(Int32 param, Axiom.Math.Quaternion rot);
        //wiki: llSetVehicleFlags(integer flags)
        void llSetVehicleFlags(Int32 flags);
        //wiki: llRemoveVehicleFlags(integer flags)
        void llRemoveVehicleFlags(Int32 flags);
        //wiki: llSitTarget(vector offset, rotation rot)
        void llSitTarget(Axiom.Math.Vector3 offset, Axiom.Math.Quaternion rot);
        //wiki key llAvatarOnSitTarget()
        string llAvatarOnSitTarget();
        //wiki: llAddToLandPassList(key avatar, float hours)
        void llAddToLandPassList(string avatar, float hours);
        //wiki: llSetTouchText(string text)
        void llSetTouchText(string text);
        //wiki: llSetSitText(string text)
        void llSetSitText(string text);
        //wiki: llSetCameraEyeOffset(vector offset)
        void llSetCameraEyeOffset(Axiom.Math.Vector3 offset);
        //wiki: llSeteCameraAtOffset(vector offset)
        void llSetCameraAtOffset(Axiom.Math.Vector3 offset);
        void llDumpList2String();
        //wiki: integer llScriptDanger(vector pos)
        void llScriptDanger(Axiom.Math.Vector3 pos);
        void llDialog();
        //wiki: llVolumeDetect(integer detect)
        void llVolumeDetect(Int32 detect);
        //wiki: llResetOtherScript(string name)
        void llResetOtherScript(string name);
        //wiki: integer llGetScriptState(string name)
        Int32 llGetScriptState(string name);
        //wiki: (deprecated)
        void llRemoteLoadScript();
        //wiki: llSetRemoteScriptAccessPin(integer pin)
        void llSetRemoteScriptAccessPin(Int32 pin);
        //wiki: llRemoteLoadScriptPin(key target, string name, integer pin, integer running, integer start_param)
        void llRemoteLoadScriptPin(string target, string name, Int32 pin, Int32 running, Int32 start_param);
        //wiki: llOpenRemoteDataChannel()
        void llOpenRemoteDataChannel();
        //wiki: key llSendRemoteData(key channel, string dest, integer idata, string sdata)
        string llSendRemoteData(string channel, string dest, Int32 idata, string sdata);
        //wiki: llRemoteDataReply(key channel, key message_id, string sdata, integer idata)
        void llRemoteDataReply(string channel, string message_id, string sdata, Int32 idata);
        //wiki: llCloseRemoteDataChannel(key channel)
        void llCloseRemoteDataChannel(string channel);
        //wiki: string llMD5String(string src, integer nonce)
        void llMD5String(string src, Int32 nonce);
        void llSetPrimitiveParams();
        //wiki: string llStringToBase64(string str)
        string llStringToBase64(string str);
        //wiki: string llBase64ToString(string str)
        string llBase64ToString(string str);
        //wiki: (deprecated)
        void llXorBase64Strings();
        //wiki: llRemoteDataSetRegion()
        void llRemoteDataSetRegion();
        //wiki: float llLog10(float val)
        float llLog10(float val);
        //wiki: float llLog(float val)
        float llLog(float val);
        void llGetAnimationList();
        //wiki: llSetParcelMusicURL(string url)
        void llSetParcelMusicURL(string url);
        //wiki: vector llGetRootPosition()
        Axiom.Math.Vector3 llGetRootPosition();
        //wiki: rotation llGetRootRotation()
        Axiom.Math.Quaternion llGetRootRotation();
        //wiki: string llGetObjectDesc()
        string llGetObjectDesc();
        //wiki: llSetObjectDesc(string desc)
        void llSetObjectDesc(string desc);
        //wiki: key llGetCreator()
        string llGetCreator();
        //wiki: string llGetTimestamp()
        string llGetTimestamp();
        //wiki: llSetLinkAlpha(integer linknumber, float alpha, integer face)
        void llSetLinkAlpha(Int32 linknumber, float alpha, Int32 face);
        //wiki: integer llGetNumberOfPrims()
        Int32 llGetNumberOfPrims();
        //wiki: key llGetNumberOfNotecardLines(string name)
        string llGetNumberOfNotecardLines(string name);
        void llGetBoundingBox();
        //wiki: vector llGetGeometricCenter()
        Axiom.Math.Vector3 llGetGeometricCenter();
        void llGetPrimitiveParams();
        //wiki: string llIntegerToBase64(integer number)
        string llIntegerToBase64(Int32 number);
        //wiki integer llBase64ToInteger(string str)
        Int32 llBase64ToInteger(string str);
        //wiki: float llGetGMTclock()
        float llGetGMTclock();
        //wiki: string llGetSimulatorHostname()
        string llGetSimulatorHostname();
        //llSetLocalRot(rotation rot)
        void llSetLocalRot(Axiom.Math.Quaternion rot);
        void llParseStringKeepNulls();
        //wiki: llRezAtRoot(string inventory, vector position, vector velocity, rotation rot, integer param)
        void llRezAtRoot(string inventory, Axiom.Math.Vector3 position, Axiom.Math.Vector3 velocity, Axiom.Math.Quaternion rot, Int32 param);
        //wiki: integer llGetObjectPermMask(integer mask)
        Int32 llGetObjectPermMask(Int32 mask);
        //wiki: llSetObjectPermMask(integer mask, integer value)
        void llSetObjectPermMask(Int32 mask, Int32 value);
        //wiki integer llGetInventoryPermMask(string item, integer mask)
        void llGetInventoryPermMask(string item, Int32 mask);
        //wiki: llSetInventoryPermMask(string item, integer mask, integer value)
        void llSetInventoryPermMask(string item, Int32 mask, Int32 value);
        //wiki: key llGetInventoryCreator(string item)
        string llGetInventoryCreator(string item);
        //wiki: llOwnerSay(string msg)
        void llOwnerSay(string msg);
        //wiki: key llRequestSimulatorData(string simulator, integer data)
        void llRequestSimulatorData(string simulator, Int32 data);
        //wiki: llForceMouselook(integer mouselook)
        void llForceMouselook(Int32 mouselook);
        //wiki: float llGetObjectMass(key id)
        float llGetObjectMass(string id);
        void llListReplaceList();
        //wiki: llLoadURL(key avatar_id, string message, string url)
        void llLoadURL(string avatar_id, string message, string url);
        void llParcelMediaCommandList();
        void llParcelMediaQuery();
        //wiki integer llModPow(integer a, integer b, integer c)
        Int32 llModPow(Int32 a, Int32 b, Int32 c);
        //wiki: integer llGetInventoryType(string name)
        Int32 llGetInventoryType(string name);
        void llSetPayPrice();
        //wiki: vector llGetCameraPos()
        Axiom.Math.Vector3 llGetCameraPos();
        //wiki rotation llGetCameraRot()
        Axiom.Math.Quaternion llGetCameraRot();
        //wiki: (deprecated)
        void llSetPrimURL();
        //wiki: (deprecated)
        void llRefreshPrimURL();
        //wiki: string llEscapeURL(string url)
        string llEscapeURL(string url);
        //wiki: string llUnescapeURL(string url)
        string llUnescapeURL(string url);
        //wiki: llMapDestination(string simname, vector pos, vector look_at)
        void llMapDestination(string simname, Axiom.Math.Vector3 pos, Axiom.Math.Vector3 look_at);
        //wiki: llAddToLandBanList(key avatar, float hours)
        void llAddToLandBanList(string avatar, float hours);
        //wiki: llRemoveFromLandPassList(key avatar)
        void llRemoveFromLandPassList(string avatar);
        //wiki: llRemoveFromLandBanList(key avatar)
        void llRemoveFromLandBanList(string avatar);
        void llSetCameraParams();
        void llClearCameraParams();
        void llListStatistics();
        //wiki: integer llGetUnixTime()
        Int32 llGetUnixTime();
        //wiki: integer llGetParcelFlags(vector pos)
        Int32 llGetParcelFlags(Axiom.Math.Vector3 pos);
        //wiki: integer llGetRegionFlags()
        Int32 llGetRegionFlags();
        //wiki: string llXorBase64StringsCorrect(string str1, string str2)
        string llXorBase64StringsCorrect(string str1, string str2);
        void llHTTPRequest();
        //wiki: llResetLandBanList()
        void llResetLandBanList();
        //wiki: llResetLandPassList()
        void llResetLandPassList();
        //wiki integer llGetParcelPrimCount(vector pos, integer category, integer sim_wide)
        Int32 llGetParcelPrimCount(Axiom.Math.Vector3 pos, Int32 category, Int32 sim_wide);
        void llGetParcelPrimOwners();
        //wiki: integer llGetObjectPrimCount(key object_id)
        Int32 llGetObjectPrimCount(string object_id);
        //wiki: integer llGetParcelMaxPrims( vector pos, integer sim_wide )
        Int32 llGetParcelMaxPrims(Axiom.Math.Vector3 pos, Int32 sim_wide);
        //wiki list llGetParcelDetails(vector pos, list params)
        List<string> llGetParcelDetails(Axiom.Math.Vector3 pos, List<string> param);
    }
}
