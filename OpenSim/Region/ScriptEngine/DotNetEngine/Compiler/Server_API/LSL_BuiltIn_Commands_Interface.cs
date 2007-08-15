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
        double llSin(double f);
        double llCos(double f);
        double llTan(double f);
        double llAtan2(double x, double y);
        double llSqrt(double f);
        double llPow(double fbase, double fexponent);
        int llAbs(int i);
        double llFabs(double f);
        double llFrand(double mag);
        int llFloor(double f);
        int llCeil(double f);
        int llRound(double f);
        double llVecMag(Axiom.Math.Vector3 v);
        Axiom.Math.Vector3 llVecNorm(Axiom.Math.Vector3 v);
        double llVecDist(Axiom.Math.Vector3 a, Axiom.Math.Vector3 b);
        Axiom.Math.Vector3 llRot2Euler(Axiom.Math.Quaternion r);
        Axiom.Math.Quaternion llEuler2Rot(Axiom.Math.Vector3 v);
        Axiom.Math.Quaternion llAxes2Rot(Axiom.Math.Vector3 fwd, Axiom.Math.Vector3 left, Axiom.Math.Vector3 up);
        Axiom.Math.Vector3 llRot2Fwd(Axiom.Math.Quaternion r);
        Axiom.Math.Vector3 llRot2Left(Axiom.Math.Quaternion r);
        Axiom.Math.Vector3 llRot2Up(Axiom.Math.Quaternion r);
        Axiom.Math.Quaternion llRotBetween(Axiom.Math.Vector3 start, Axiom.Math.Vector3 end);
        void llWhisper(int channelID, string text);
        //void llSay(int channelID, string text);
        void llSay(int channelID, string text);
        void llShout(int channelID, string text);
        int llListen(int channelID, string name, string ID, string msg);
        void llListenControl(int number, int active);
        void llListenRemove(int number);
        void llSensor(string name, string id, int type, double range, double arc);
        void llSensorRepeat(string name, string id, int type, double range, double arc, double rate);
        void llSensorRemove();
        string llDetectedName(int number);
        string llDetectedKey(int number);
        string llDetectedOwner(int number);
        int llDetectedType(int number);
        Axiom.Math.Vector3 llDetectedPos(int number);
        Axiom.Math.Vector3 llDetectedVel(int number);
        Axiom.Math.Vector3 llDetectedGrab(int number);
        Axiom.Math.Quaternion llDetectedRot(int number);
        int llDetectedGroup(int number);
        int llDetectedLinkNumber(int number);
        void llDie();
        double llGround(Axiom.Math.Vector3 offset);
        double llCloud(Axiom.Math.Vector3 offset);
        Axiom.Math.Vector3 llWind(Axiom.Math.Vector3 offset);
        void llSetStatus(int status, int value);
        int llGetStatus(int status);
        void llSetScale(Axiom.Math.Vector3 scale);
        Axiom.Math.Vector3 llGetScale();
        void llSetColor(Axiom.Math.Vector3 color, int face);
        double llGetAlpha(int face);
        void llSetAlpha(double alpha, int face);
        Axiom.Math.Vector3 llGetColor(int face);
        void llSetTexture(string texture, int face);
        void llScaleTexture(double u, double v, int face);
        void llOffsetTexture(double u, double v, int face);
        void llRotateTexture(double rotation, int face);
        string llGetTexture(int face);
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
        void llSetForce(Axiom.Math.Vector3 force, int local);
        //wiki: vector llGetForce()
        Axiom.Math.Vector3 llGetForce();
        //wiki: integer llTarget(vector position, double range)
        int llTarget(Axiom.Math.Vector3 position, double range);
        //wiki: llTargetRemove(integer number)
        void llTargetRemove(int number);
        //wiki: integer llRotTarget(rotation rot, double error)
        int llRotTarget(Axiom.Math.Quaternion rot, double error);
        //wiki: integer llRotTargetRemove(integer number)
        void llRotTargetRemove(int number);
        //wiki: llMoveToTarget(vector target, double tau)
        void llMoveToTarget(Axiom.Math.Vector3 target, double tau);
        //wiki: llStopMoveToTarget()
        void llStopMoveToTarget();
        //wiki: llApplyImpulse(vector force, integer local)
        void llApplyImpulse(Axiom.Math.Vector3 force, int local);
        //wiki: llapplyRotationalImpulse(vector force, integer local)
        void llApplyRotationalImpulse(Axiom.Math.Vector3 force, int local);
        //wiki: llSetTorque(vector torque, integer local)
        void llSetTorque(Axiom.Math.Vector3 torque, int local);
        //wiki: vector llGetTorque()
        Axiom.Math.Vector3 llGetTorque();
        //wiki: llSeForceAndTorque(vector force, vector torque, integer local)
        void llSetForceAndTorque(Axiom.Math.Vector3 force, Axiom.Math.Vector3 torque, int local);
        //wiki: vector llGetVel()
        Axiom.Math.Vector3 llGetVel();
        //wiki: vector llGetAccel()
        Axiom.Math.Vector3 llGetAccel();
        //wiki: vector llGetOmega()
        Axiom.Math.Vector3 llGetOmega();
        //wiki: double llGetTimeOfDay()
        double llGetTimeOfDay();
        //wiki: double llGetWallclock()
        double llGetWallclock();
        //wiki: double llGetTime()
        double llGetTime();
        //wiki: llResetTime()
        void llResetTime();
        //wiki: double llGetAndResetTime()
        double llGetAndResetTime();
        //wiki (deprecated) llSound(string sound, double volume, integer queue, integer loop)
        void llSound();
        //wiki: llPlaySound(string sound, double volume)
        void llPlaySound(string sound, double volume);
        //wiki: llLoopSound(string sound, double volume)
        void llLoopSound(string sound, double volume);
        //wiki: llLoopSoundMaster(string sound, double volume)
        void llLoopSoundMaster(string sound, double volume);
        //wiki: llLoopSoundSlave(string sound, double volume)
        void llLoopSoundSlave(string sound, double volume);
        //wiki llPlaySoundSlave(string sound, double volume)
        void llPlaySoundSlave(string sound, double volume);
        //wiki: llTriggerSound(string sound, double volume)
        void llTriggerSound(string sound, double volume);
        //wiki: llStopSound()
        void llStopSound();
        //wiki: llPreloadSound(string sound)
        void llPreloadSound(string sound);
        //wiki: string llGetSubString(string src, integer start, integer end)
        void llGetSubString(string src, int start, int end);
        //wiki: string llDeleteSubString(string src, integer start, integer end)
        string llDeleteSubString(string src, int start, int end);
        //wiki string llInsertString(string dst, integer position, string src)
        void llInsertString(string dst, int position, string src);
        //wiki: string llToUpper(string source)
        string llToUpper(string source);
        //wiki: string llToLower(string source)
        string llToLower(string source);
        //wiki: integer llGiveMoney(key destination, integer amount)
        int llGiveMoney(string destination, int amount);
        //wiki: (deprecated)
        void llMakeExplosion();
        //wiki: (deprecated)
        void llMakeFountain();
        //wiki: (deprecated)
        void llMakeSmoke();
        //wiki: (deprecated)
        void llMakeFire();
        //wiki: llRezObject(string inventory, vector pos, vector rel, rotation rot, integer param)
        void llRezObject(string inventory, Axiom.Math.Vector3 pos, Axiom.Math.Quaternion rot, int param);
        //wiki: llLookAt(vector target, double strength, double damping)
        void llLookAt(Axiom.Math.Vector3 target, double strength, double damping);
        //wiki: llStopLookAt()
        void llStopLookAt();
        //wiki: llSetTimerEvent(double sec)
        void llSetTimerEvent(double sec);
        //wiki: llSleep(double sec)
        void llSleep(double sec);
        //wiki: double llGetMass()
        double llGetMass();
        //wiki: llCollisionFilter(string name, key id, integer accept)
        void llCollisionFilter(string name, string id, int accept);
        //wiki: llTakeControls(integer controls, integer accept, integer pass_on)
        void llTakeControls(int controls, int accept, int pass_on);
        //wiki: llReleaseControls()
        void llReleaseControls();
        //wiki: llAttachToAvatar(integer attachment)
        void llAttachToAvatar(int attachment);
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
        //wiki: llSetBuoyancy(double buoyancy)
        void llSetBuoyancy(double buoyancy);
        //wiki: llSetHoverHeight(double height, integer water, double tau)
        void llSetHoverHeight(double height, int water, double tau);
        //wiki: llStopHover
        void llStopHover();
        //wiki: llMinEventDelay(double delay)
        void llMinEventDelay(double delay);
        //wiki: (deprecated) llSoundPreload()
        void llSoundPreload();
        //wiki: llRotLookAt(rotation target, double strength, double damping)
        void llRotLookAt(Axiom.Math.Quaternion target, double strength, double damping);
        //wiki: integer llStringLength(string str)
        int llStringLength(string str);
        //wiki: llStartAnimation(string anim)
        void llStartAnimation(string anim);
        //wiki: llStopAnimation(string anim)
        void llStopAnimation(string anim);
        //wiki: (deprecated) llPointAt
        void llPointAt();
        //wiki: (deprecated) llStopPointAt
        void llStopPointAt();
        //wiki: llTargetOmega(vector axis, double spinrate, double gain)
        void llTargetOmega(Axiom.Math.Vector3 axis, double spinrate, double gain);
        //wiki: integer llGetStartParameter()
        int llGetStartParameter();
        //wiki: llGodLikeRezObject(key inventory, vector pos)
        void llGodLikeRezObject(string inventory, Axiom.Math.Vector3 pos);
        //wiki: llRequestPermissions(key agent, integer perm)
        void llRequestPermissions(string agent, int perm);
        //wiki: key llGetPermissionsKey()
        string llGetPermissionsKey();
        //wiki: integer llGetPermissions()
        int llGetPermissions();
        //wiki integer llGetLinkNumber()
        int llGetLinkNumber();
        //wiki: llSetLinkColor(integer linknumber, vector color, integer face)
        void llSetLinkColor(int linknumber, Axiom.Math.Vector3 color, int face);
        //wiki: llCreateLink(key target, integer parent)
        void llCreateLink(string target, int parent);
        //wiki: llBreakLink(integer linknum)
        void llBreakLink(int linknum);
        //wiki: llBreakAllLinks()
        void llBreakAllLinks();
        //wiki: key llGetLinkKey(integer linknum)
        string llGetLinkKey(int linknum);
        //wiki: llGetLinkName(integer linknum)
        void llGetLinkName(int linknum);
        //wiki: integer llGetInventoryNumber(integer type)
        int llGetInventoryNumber(int type);
        //wiki: string llGetInventoryName(integer type, integer number)
        string llGetInventoryName(int type, int number);
        //wiki: llSetScriptState(string name, integer run)
        void llSetScriptState(string name, int run);
        //wiki: double llGetEnergy()
        double llGetEnergy();
        //wiki: llGiveInventory(key destination, string inventory)
        void llGiveInventory(string destination, string inventory);
        //wiki: llRemoveInventory(string item)
        void llRemoveInventory(string item);
        //wiki: llSetText(string text, vector color, double alpha)
        void llSetText(string text, Axiom.Math.Vector3 color, double alpha);
        //wiki: double llWater(vector offset)
        double llWater(Axiom.Math.Vector3 offset);
        //wiki: llPassTouches(integer pass)
        void llPassTouches(int pass);
        //wiki: key llRequestAgentData(key id, integer data)
        string llRequestAgentData(string id, int data);
        //wiki: key llRequestInventoryData(string name)
        string llRequestInventoryData(string name);
        //wiki: llSetDamage(double damage)
        void llSetDamage(double damage);
        //wiki: llTeleportAgentHome(key agent)
        void llTeleportAgentHome(string agent);
        //wiki: llModifyLand(integer action, integer brush)
        void llModifyLand(int action, int brush);
        //wiki: llCollisionSound(string impact_sound, double impact_volume)
        void llCollisionSound(string impact_sound, double impact_volume);
        //wiki: llCollisionSprite(string impact_sprite)
        void llCollisionSprite(string impact_sprite);
        //wiki: string llGetAnimation(key id)
        string llGetAnimation(string id);
        //wiki: llResetScript()
        void llResetScript();
        //wiki: llMessageLinked(integer linknum, integer num, string str, key id)
        void llMessageLinked(int linknum, int num, string str, string id);
        //wiki: llPushObject(key target, vector impulse, vector ang_impulse, integer local)
        void llPushObject(string target, Axiom.Math.Vector3 impulse, Axiom.Math.Vector3 ang_impulse, int local);
        //wiki: llPassCollisions(integer pass)
        void llPassCollisions(int pass);
        //wiki: string llGetScriptName()
        string llGetScriptName();
        //wiki: integer llGetNumberOfSides()
        int llGetNumberOfSides();
        //wiki: rotation llAxisAngle2Rot(vector axis, double angle)
        Axiom.Math.Quaternion llAxisAngle2Rot(Axiom.Math.Vector3 axis, double angle);
        //wiki: vector llRot2Axis(rotation rot)
        Axiom.Math.Vector3 llRot2Axis(Axiom.Math.Quaternion rot);
        void llRot2Angle();
        //wiki: double llAcos(double val)
        double llAcos(double val);
        //wiki: double llAsin(double val)
        double llAsin(double val);
        //wiki: double llAngleBetween(rotation a, rotation b)
        double llAngleBetween(Axiom.Math.Quaternion a, Axiom.Math.Quaternion b);
        //wiki: string llGetInventoryKey(string name)
        string llGetInventoryKey(string name);
        //wiki: llAllowInventoryDrop(integer add)
        void llAllowInventoryDrop(int add);
        //wiki: vector llGetSunDirection()
        Axiom.Math.Vector3 llGetSunDirection();
        //wiki: vector llGetTextureOffset(integer face)
        Axiom.Math.Vector3 llGetTextureOffset(int face);
        //wiki: vector llGetTextureScale(integer side)
        Axiom.Math.Vector3 llGetTextureScale(int side);
        //wiki: double llGetTextureRot(integer side)
        double llGetTextureRot(int side);
        //wiki: integer llSubStringIndex(string source, string pattern)
        int llSubStringIndex(string source, string pattern);
        //wiki: key llGetOwnerKey(key id)
        string llGetOwnerKey(string id);
        //wiki: vector llGetCenterOfMass()
        Axiom.Math.Vector3 llGetCenterOfMass();
        //wiki: list llListSort(list src, integer stride, integer ascending)
        List<string> llListSort(List<string> src, int stride, int ascending);
        //integer llGetListLength(list src)
        int llGetListLength(List<string> src);
        //wiki: integer llList2Integer(list src, integer index)
        int llList2Integer(List<string> src, int index);
        //wiki: double llList2double(list src, integer index)
        double llList2double(List<string> src, int index);
        //wiki: string llList2String(list src, integer index)
        string llList2String(List<string> src, int index);
        //wiki: key llList2Key(list src, integer index)
        string llList2Key(List<string> src, int index);
        //wiki: vector llList2Vector(list src, integer index)
        Axiom.Math.Vector3 llList2Vector(List<string> src, int index);
        //wiki rotation llList2Rot(list src, integer index)
        Axiom.Math.Quaternion llList2Rot(List<string> src, int index);
        //wiki: list llList2List(list src, integer start, integer end)
        List<string> llList2List(List<string> src, int start, int end);
        //wiki: llDeleteSubList(list src, integer start, integer end)
        List<string> llDeleteSubList(List<string> src, int start, int end);
        //wiki: integer llGetListEntryType( list src, integer index )
        int llGetListEntryType(List<string> src, int index);
        //wiki: string llList2CSV( list src )
        string llList2CSV(List<string> src);
        //wiki: list llCSV2List( string src )
        List<string> llCSV2List(string src);
        //wiki: list llListRandomize( list src, integer stride )
        List<string> llListRandomize(List<string> src, int stride);
        //wiki: list llList2ListStrided( list src, integer start, integer end, integer stride )
        List<string> llList2ListStrided(List<string> src, int start, int end, int stride);
        //wiki: vector llGetRegionCorner( )
        Axiom.Math.Vector3 llGetRegionCorner();
        //wiki: list llListInsertList( list dest, list src, integer start )
        List<string> llListInsertList(List<string> dest, List<string> src, int start);
        //wiki: integer llListFindList( list src, list test )
        int llListFindList(List<string> src, List<string> test);
        //wiki: string llGetObjectName()
        string llGetObjectName();
        //wiki: llSetObjectName(string name)
        void llSetObjectName(string name);
        //wiki: string llGetDate()
        string llGetDate();
        //wiki: integer llEdgeOfWorld(vector pos, vector dir)
        int llEdgeOfWorld(Axiom.Math.Vector3 pos, Axiom.Math.Vector3 dir);
        //wiki: integer llGetAgentInfo(key id)
        int llGetAgentInfo(string id);
        //wiki: llAdjustSoundVolume(double volume)
        void llAdjustSoundVolume(double volume);
        //wiki: llSetSoundQueueing(integer queue)
        void llSetSoundQueueing(int queue);
        //wiki: llSetSoundRadius(double radius)
        void llSetSoundRadius(double radius);
        //wiki: string llKey2Name(key id)
        string llKey2Name(string id);
        //wiki: llSetTextureAnim(integer mode, integer face, integer sizex, integer sizey, double start, double length, double rate)
        void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate);
        //wiki: llTriggerSoundLimited(string sound, double volume, vector top_north_east, vector bottom_south_west)
        void llTriggerSoundLimited(string sound, double volume, Axiom.Math.Vector3 top_north_east, Axiom.Math.Vector3 bottom_south_west);
        //wiki: llEjectFromLand(key pest)
        void llEjectFromLand(string pest);
        void llParseString2List();
        //wiki: integer llOverMyLand(key id)
        int llOverMyLand(string id);
        //wiki: key llGetLandOwnerAt(vector pos)
        string llGetLandOwnerAt(Axiom.Math.Vector3 pos);
        //wiki: key llGetNotecardLine(string name, integer line)
        string llGetNotecardLine(string name, int line);
        //wiki: vector llGetAgentSize(key id)
        Axiom.Math.Vector3 llGetAgentSize(string id);
        //wiki: integer llSameGroup(key agent)
        int llSameGroup(string agent);
        //wiki: llUnSit(key id)
        void llUnSit(string id);
        //wiki: vector llGroundSlope(vector offset)
        Axiom.Math.Vector3 llGroundSlope(Axiom.Math.Vector3 offset);
        //wiki: vector llGroundNormal(vector offset)
        Axiom.Math.Vector3 llGroundNormal(Axiom.Math.Vector3 offset);
        //wiki: vector llGroundContour(vector offset)
        Axiom.Math.Vector3 llGroundContour(Axiom.Math.Vector3 offset);
        //wiki: integer llGetAttached()
        int llGetAttached();
        //wiki: integer llGetFreeMemory()
        int llGetFreeMemory();
        //wiki: string llGetRegionName()
        string llGetRegionName();
        //wiki: double llGetRegionTimeDilation()
        double llGetRegionTimeDilation();
        //wiki: double llGetRegionFPS()
        double llGetRegionFPS();
        //wiki: llParticleSystem(List<Object> rules
        void llParticleSystem(List<Object> rules);
        //wiki: llGroundRepel(double height, integer water, double tau)
        void llGroundRepel(double height, int water, double tau);
        void llGiveInventoryList();
        //wiki: llSetVehicleType(integer type)
        void llSetVehicleType(int type);
        //wiki: llSetVehicledoubleParam(integer param, double value)
        void llSetVehicledoubleParam(int param, double value);
        //wiki: llSetVehicleVectorParam(integer param, vector vec)
        void llSetVehicleVectorParam(int param, Axiom.Math.Vector3 vec);
        //wiki: llSetVehicleRotationParam(integer param, rotation rot)
        void llSetVehicleRotationParam(int param, Axiom.Math.Quaternion rot);
        //wiki: llSetVehicleFlags(integer flags)
        void llSetVehicleFlags(int flags);
        //wiki: llRemoveVehicleFlags(integer flags)
        void llRemoveVehicleFlags(int flags);
        //wiki: llSitTarget(vector offset, rotation rot)
        void llSitTarget(Axiom.Math.Vector3 offset, Axiom.Math.Quaternion rot);
        //wiki key llAvatarOnSitTarget()
        string llAvatarOnSitTarget();
        //wiki: llAddToLandPassList(key avatar, double hours)
        void llAddToLandPassList(string avatar, double hours);
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
        //wiki: llDialog( key avatar, string message, list buttons, integer chat_channel )
        void llDialog(string avatar, string message, List<string> buttons, int chat_channel);
        //wiki: llVolumeDetect(integer detect)
        void llVolumeDetect(int detect);
        //wiki: llResetOtherScript(string name)
        void llResetOtherScript(string name);
        //wiki: integer llGetScriptState(string name)
        int llGetScriptState(string name);
        //wiki: (deprecated)
        void llRemoteLoadScript();
        //wiki: llSetRemoteScriptAccessPin(integer pin)
        void llSetRemoteScriptAccessPin(int pin);
        //wiki: llRemoteLoadScriptPin(key target, string name, integer pin, integer running, integer start_param)
        void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param);
        //wiki: llOpenRemoteDataChannel()
        void llOpenRemoteDataChannel();
        //wiki: key llSendRemoteData(key channel, string dest, integer idata, string sdata)
        string llSendRemoteData(string channel, string dest, int idata, string sdata);
        //wiki: llRemoteDataReply(key channel, key message_id, string sdata, integer idata)
        void llRemoteDataReply(string channel, string message_id, string sdata, int idata);
        //wiki: llCloseRemoteDataChannel(key channel)
        void llCloseRemoteDataChannel(string channel);
        //wiki: string llMD5String(string src, integer nonce)
        string llMD5String(string src, int nonce);
        //wiki: llSetPrimitiveParams( list rules )
        void llSetPrimitiveParams(List<string> rules);
        //wiki: string llStringToBase64(string str)
        string llStringToBase64(string str);
        //wiki: string llBase64ToString(string str)
        string llBase64ToString(string str);
        //wiki: (deprecated)
        void llXorBase64Strings();
        //wiki: llRemoteDataSetRegion()
        void llRemoteDataSetRegion();
        //wiki: double llLog10(double val)
        double llLog10(double val);
        //wiki: double llLog(double val)
        double llLog(double val);
        //wiki: list llGetAnimationList( key id )
        List<string> llGetAnimationList(string id);
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
        //wiki: llSetLinkAlpha(integer linknumber, double alpha, integer face)
        void llSetLinkAlpha(int linknumber, double alpha, int face);
        //wiki: integer llGetNumberOfPrims()
        int llGetNumberOfPrims();
        //wiki: key llGetNumberOfNotecardLines(string name)
        string llGetNumberOfNotecardLines(string name);
        //wiki: list llGetBoundingBox( key object )
        List<string> llGetBoundingBox(string obj);
        //wiki: vector llGetGeometricCenter()
        Axiom.Math.Vector3 llGetGeometricCenter();
        void llGetPrimitiveParams();
        //wiki: string llIntegerToBase64(integer number)
        string llIntegerToBase64(int number);
        //wiki integer llBase64ToInteger(string str)
        int llBase64ToInteger(string str);
        //wiki: double llGetGMTclock()
        double llGetGMTclock();
        //wiki: string llGetSimulatorHostname()
        string llGetSimulatorHostname();
        //llSetLocalRot(rotation rot)
        void llSetLocalRot(Axiom.Math.Quaternion rot);
        //wiki: list llParseStringKeepNulls( string src, list separators, list spacers )
        List<string> llParseStringKeepNulls(string src, List<string> seperators, List<string> spacers);
        //wiki: llRezAtRoot(string inventory, vector position, vector velocity, rotation rot, integer param)
        void llRezAtRoot(string inventory, Axiom.Math.Vector3 position, Axiom.Math.Vector3 velocity, Axiom.Math.Quaternion rot, int param);
        //wiki: integer llGetObjectPermMask(integer mask)
        int llGetObjectPermMask(int mask);
        //wiki: llSetObjectPermMask(integer mask, integer value)
        void llSetObjectPermMask(int mask, int value);
        //wiki integer llGetInventoryPermMask(string item, integer mask)
        void llGetInventoryPermMask(string item, int mask);
        //wiki: llSetInventoryPermMask(string item, integer mask, integer value)
        void llSetInventoryPermMask(string item, int mask, int value);
        //wiki: key llGetInventoryCreator(string item)
        string llGetInventoryCreator(string item);
        //wiki: llOwnerSay(string msg)
        void llOwnerSay(string msg);
        //wiki: key llRequestSimulatorData(string simulator, integer data)
        void llRequestSimulatorData(string simulator, int data);
        //wiki: llForceMouselook(integer mouselook)
        void llForceMouselook(int mouselook);
        //wiki: double llGetObjectMass(key id)
        double llGetObjectMass(string id);
        void llListReplaceList();
        //wiki: llLoadURL(key avatar_id, string message, string url)
        void llLoadURL(string avatar_id, string message, string url);
        //wiki: llParcelMediaCommandList( list commandList )
        void llParcelMediaCommandList(List<string> commandList);
        void llParcelMediaQuery();
        //wiki integer llModPow(integer a, integer b, integer c)
        int llModPow(int a, int b, int c);
        //wiki: integer llGetInventoryType(string name)
        int llGetInventoryType(string name);
        //wiki: llSetPayPrice( integer price, list quick_pay_buttons )
        void llSetPayPrice(int price, List<string> quick_pay_buttons);
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
        //wiki: llAddToLandBanList(key avatar, double hours)
        void llAddToLandBanList(string avatar, double hours);
        //wiki: llRemoveFromLandPassList(key avatar)
        void llRemoveFromLandPassList(string avatar);
        //wiki: llRemoveFromLandBanList(key avatar)
        void llRemoveFromLandBanList(string avatar);
        //wiki: llSetCameraParams( list rules )
        void llSetCameraParams(List<string> rules);
        //wiki: llClearCameraParams()
        void llClearCameraParams();
        //wiki: double llListStatistics( integer operation, list src )
        double llListStatistics(int operation, List<string> src);
        //wiki: integer llGetUnixTime()
        int llGetUnixTime();
        //wiki: integer llGetParcelFlags(vector pos)
        int llGetParcelFlags(Axiom.Math.Vector3 pos);
        //wiki: integer llGetRegionFlags()
        int llGetRegionFlags();
        //wiki: string llXorBase64StringsCorrect(string str1, string str2)
        string llXorBase64StringsCorrect(string str1, string str2);
        void llHTTPRequest();
        //wiki: llResetLandBanList()
        void llResetLandBanList();
        //wiki: llResetLandPassList()
        void llResetLandPassList();
        //wiki integer llGetParcelPrimCount(vector pos, integer category, integer sim_wide)
        int llGetParcelPrimCount(Axiom.Math.Vector3 pos, int category, int sim_wide);
        //wiki: list llGetParcelPrimOwners( vector pos )
        List<string> llGetParcelPrimOwners(Axiom.Math.Vector3 pos);
        //wiki: integer llGetObjectPrimCount(key object_id)
        int llGetObjectPrimCount(string object_id);
        //wiki: integer llGetParcelMaxPrims( vector pos, integer sim_wide )
        int llGetParcelMaxPrims(Axiom.Math.Vector3 pos, int sim_wide);
        //wiki list llGetParcelDetails(vector pos, list params)
        List<string> llGetParcelDetails(Axiom.Math.Vector3 pos, List<string> param);
    }
}
