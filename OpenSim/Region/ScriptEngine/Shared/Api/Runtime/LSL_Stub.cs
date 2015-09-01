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
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

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
        public LSL_Integer llAbs(int i)
        {
            return m_LSL_Functions.llAbs(i);
        }

        public LSL_Float llAcos(double val)
        {
            return m_LSL_Functions.llAcos(val);
        }

        public void llAddToLandBanList(string avatar, double hours)
        {
            m_LSL_Functions.llAddToLandBanList(avatar, hours);
        }

        public void llAddToLandPassList(string avatar, double hours)
        {
            m_LSL_Functions.llAddToLandPassList(avatar, hours);
        }

        public void llAdjustSoundVolume(double volume)
        {
            m_LSL_Functions.llAdjustSoundVolume(volume);
        }

        public void llAllowInventoryDrop(int add)
        {
            m_LSL_Functions.llAllowInventoryDrop(add);
        }

        public LSL_Float llAngleBetween(LSL_Rotation a, LSL_Rotation b)
        {
            return m_LSL_Functions.llAngleBetween(a, b);
        }

        public void llApplyImpulse(LSL_Vector force, int local)
        {
            m_LSL_Functions.llApplyImpulse(force, local);
        }

        public void llApplyRotationalImpulse(LSL_Vector force, int local)
        {
            m_LSL_Functions.llApplyRotationalImpulse(force, local);
        }

        public LSL_Float llAsin(double val)
        {
            return m_LSL_Functions.llAsin(val);
        }

        public LSL_Float llAtan2(double x, double y)
        {
            return m_LSL_Functions.llAtan2(x, y);
        }

        public void llAttachToAvatar(int attachment)
        {
            m_LSL_Functions.llAttachToAvatar(attachment);
        }

        public LSL_Key llAvatarOnSitTarget()
        {
            return m_LSL_Functions.llAvatarOnSitTarget();
        }

        public LSL_Key llAvatarOnLinkSitTarget(int linknum)
        {
            return m_LSL_Functions.llAvatarOnLinkSitTarget(linknum);
        }

        public LSL_Rotation llAxes2Rot(LSL_Vector fwd, LSL_Vector left, LSL_Vector up)
        {
            return m_LSL_Functions.llAxes2Rot(fwd, left, up);
        }

        public LSL_Rotation llAxisAngle2Rot(LSL_Vector axis, double angle)
        {
            return m_LSL_Functions.llAxisAngle2Rot(axis, angle);
        }

        public LSL_Integer llBase64ToInteger(string str)
        {
            return m_LSL_Functions.llBase64ToInteger(str);
        }

        public LSL_String llBase64ToString(string str)
        {
            return m_LSL_Functions.llBase64ToString(str);
        }

        public void llBreakAllLinks()
        {
            m_LSL_Functions.llBreakAllLinks();
        }

        public void llBreakLink(int linknum)
        {
            m_LSL_Functions.llBreakLink(linknum);
        }

        public LSL_Integer llCeil(double f)
        {
            return m_LSL_Functions.llCeil(f);
        }

        public void llClearCameraParams()
        {
            m_LSL_Functions.llClearCameraParams();
        }

        public void llCloseRemoteDataChannel(string channel)
        {
            m_LSL_Functions.llCloseRemoteDataChannel(channel);
        }

        public LSL_Float llCloud(LSL_Vector offset)
        {
            return m_LSL_Functions.llCloud(offset);
        }

        public void llCollisionFilter(string name, string id, int accept)
        {
            m_LSL_Functions.llCollisionFilter(name, id, accept);
        }

        public void llCollisionSound(string impact_sound, double impact_volume)
        {
            m_LSL_Functions.llCollisionSound(impact_sound, impact_volume);
        }

        public void llCollisionSprite(string impact_sprite)
        {
            m_LSL_Functions.llCollisionSprite(impact_sprite);
        }

        public LSL_Float llCos(double f)
        {
            return m_LSL_Functions.llCos(f);
        }

        public void llCreateLink(string target, int parent)
        {
            m_LSL_Functions.llCreateLink(target, parent);
        }

        public LSL_List llCSV2List(string src)
        {
            return m_LSL_Functions.llCSV2List(src);
        }

        public LSL_List llDeleteSubList(LSL_List src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubList(src, start, end);
        }

        public LSL_String llDeleteSubString(string src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubString(src, start, end);
        }

        public void llDetachFromAvatar()
        {
            m_LSL_Functions.llDetachFromAvatar();
        }

        public LSL_Vector llDetectedGrab(int number)
        {
            return m_LSL_Functions.llDetectedGrab(number);
        }

        public LSL_Integer llDetectedGroup(int number)
        {
            return m_LSL_Functions.llDetectedGroup(number);
        }

        public LSL_Key llDetectedKey(int number)
        {
            return m_LSL_Functions.llDetectedKey(number);
        }

        public LSL_Integer llDetectedLinkNumber(int number)
        {
            return m_LSL_Functions.llDetectedLinkNumber(number);
        }

        public LSL_String llDetectedName(int number)
        {
            return m_LSL_Functions.llDetectedName(number);
        }

        public LSL_Key llDetectedOwner(int number)
        {
            return m_LSL_Functions.llDetectedOwner(number);
        }

        public LSL_Vector llDetectedPos(int number)
        {
            return m_LSL_Functions.llDetectedPos(number);
        }

        public LSL_Rotation llDetectedRot(int number)
        {
            return m_LSL_Functions.llDetectedRot(number);
        }

        public LSL_Integer llDetectedType(int number)
        {
            return m_LSL_Functions.llDetectedType(number);
        }

        public LSL_Vector llDetectedTouchBinormal(int index)
        {
            return m_LSL_Functions.llDetectedTouchBinormal(index);
        }

        public LSL_Integer llDetectedTouchFace(int index)
        {
            return m_LSL_Functions.llDetectedTouchFace(index);
        }

        public LSL_Vector llDetectedTouchNormal(int index)
        {
            return m_LSL_Functions.llDetectedTouchNormal(index);
        }

        public LSL_Vector llDetectedTouchPos(int index)
        {
            return m_LSL_Functions.llDetectedTouchPos(index);
        }

        public LSL_Vector llDetectedTouchST(int index)
        {
            return m_LSL_Functions.llDetectedTouchST(index);
        }

        public LSL_Vector llDetectedTouchUV(int index)
        {
            return m_LSL_Functions.llDetectedTouchUV(index);
        }

        public LSL_Vector llDetectedVel(int number)
        {
            return m_LSL_Functions.llDetectedVel(number);
        }

        public void llDialog(string avatar, string message, LSL_List buttons, int chat_channel)
        {
            m_LSL_Functions.llDialog(avatar, message, buttons, chat_channel);
        }

        public void llDie()
        {
            m_LSL_Functions.llDie();
        }

        public LSL_String llDumpList2String(LSL_List src, string seperator)
        {
            return m_LSL_Functions.llDumpList2String(src, seperator);
        }

        public LSL_Integer llEdgeOfWorld(LSL_Vector pos, LSL_Vector dir)
        {
            return m_LSL_Functions.llEdgeOfWorld(pos, dir);
        }

        public void llEjectFromLand(string pest)
        {
            m_LSL_Functions.llEjectFromLand(pest);
        }

        public void llEmail(string address, string subject, string message)
        {
            m_LSL_Functions.llEmail(address, subject, message);
        }

        public LSL_String llEscapeURL(string url)
        {
            return m_LSL_Functions.llEscapeURL(url);
        }

        public LSL_Rotation llEuler2Rot(LSL_Vector v)
        {
            return m_LSL_Functions.llEuler2Rot(v);
        }

        public LSL_Float llFabs(double f)
        {
            return m_LSL_Functions.llFabs(f);
        }

        public LSL_Integer llFloor(double f)
        {
            return m_LSL_Functions.llFloor(f);
        }

        public void llForceMouselook(int mouselook)
        {
            m_LSL_Functions.llForceMouselook(mouselook);
        }

        public LSL_Float llFrand(double mag)
        {
            return m_LSL_Functions.llFrand(mag);
        }

        public LSL_Key llGenerateKey()
        {
            return m_LSL_Functions.llGenerateKey();
        }

        public LSL_Vector llGetAccel()
        {
            return m_LSL_Functions.llGetAccel();
        }

        public LSL_Integer llGetAgentInfo(string id)
        {
            return m_LSL_Functions.llGetAgentInfo(id);
        }

        public LSL_String llGetAgentLanguage(string id)
        {
            return m_LSL_Functions.llGetAgentLanguage(id);
        }

        public LSL_List llGetAgentList(LSL_Integer scope, LSL_List options)
        {
            return m_LSL_Functions.llGetAgentList(scope, options);
        }

        public LSL_Vector llGetAgentSize(string id)
        {
            return m_LSL_Functions.llGetAgentSize(id);
        }

        public LSL_Float llGetAlpha(int face)
        {
            return m_LSL_Functions.llGetAlpha(face);
        }

        public LSL_Float llGetAndResetTime()
        {
            return m_LSL_Functions.llGetAndResetTime();
        }

        public LSL_String llGetAnimation(string id)
        {
            return m_LSL_Functions.llGetAnimation(id);
        }

        public LSL_List llGetAnimationList(string id)
        {
            return m_LSL_Functions.llGetAnimationList(id);
        }

        public LSL_Integer llGetAttached()
        {
            return m_LSL_Functions.llGetAttached();
        }

        public LSL_List llGetBoundingBox(string obj)
        {
            return m_LSL_Functions.llGetBoundingBox(obj);
        }

        public LSL_Vector llGetCameraPos()
        {
            return m_LSL_Functions.llGetCameraPos();
        }

        public LSL_Rotation llGetCameraRot()
        {
            return m_LSL_Functions.llGetCameraRot();
        }

        public LSL_Vector llGetCenterOfMass()
        {
            return m_LSL_Functions.llGetCenterOfMass();
        }

        public LSL_Vector llGetColor(int face)
        {
            return m_LSL_Functions.llGetColor(face);
        }

        public LSL_String llGetCreator()
        {
            return m_LSL_Functions.llGetCreator();
        }

        public LSL_String llGetDate()
        {
            return m_LSL_Functions.llGetDate();
        }

        public LSL_Float llGetEnergy()
        {
            return m_LSL_Functions.llGetEnergy();
        }

        public LSL_String llGetEnv(LSL_String name)
        {
            return m_LSL_Functions.llGetEnv(name);
        }

        public LSL_Vector llGetForce()
        {
            return m_LSL_Functions.llGetForce();
        }

        public LSL_Integer llGetFreeMemory()
        {
            return m_LSL_Functions.llGetFreeMemory();
        }

        public LSL_Integer llGetFreeURLs()
        {
            return m_LSL_Functions.llGetFreeURLs();
        }

        public LSL_Vector llGetGeometricCenter()
        {
            return m_LSL_Functions.llGetGeometricCenter();
        }

        public LSL_Float llGetGMTclock()
        {
            return m_LSL_Functions.llGetGMTclock();
        }

        public LSL_String llGetHTTPHeader(LSL_Key request_id, string header)
        {
            return m_LSL_Functions.llGetHTTPHeader(request_id, header);
        }

        public LSL_Key llGetInventoryCreator(string item)
        {
            return m_LSL_Functions.llGetInventoryCreator(item);
        }

        public LSL_Key llGetInventoryKey(string name)
        {
            return m_LSL_Functions.llGetInventoryKey(name);
        }

        public LSL_String llGetInventoryName(int type, int number)
        {
            return m_LSL_Functions.llGetInventoryName(type, number);
        }

        public LSL_Integer llGetInventoryNumber(int type)
        {
            return m_LSL_Functions.llGetInventoryNumber(type);
        }

        public LSL_Integer llGetInventoryPermMask(string item, int mask)
        {
            return m_LSL_Functions.llGetInventoryPermMask(item, mask);
        }

        public LSL_Integer llGetInventoryType(string name)
        {
            return m_LSL_Functions.llGetInventoryType(name);
        }

        public LSL_Key llGetKey()
        {
            return m_LSL_Functions.llGetKey();
        }

        public LSL_Key llGetLandOwnerAt(LSL_Vector pos)
        {
            return m_LSL_Functions.llGetLandOwnerAt(pos);
        }

        public LSL_Key llGetLinkKey(int linknum)
        {
            return m_LSL_Functions.llGetLinkKey(linknum);
        }

        public LSL_String llGetLinkName(int linknum)
        {
            return m_LSL_Functions.llGetLinkName(linknum);
        }

        public LSL_Integer llGetLinkNumber()
        {
            return m_LSL_Functions.llGetLinkNumber();
        }

        public LSL_Integer llGetLinkNumberOfSides(int link)
        {
            return m_LSL_Functions.llGetLinkNumberOfSides(link);
        }

        public void llSetKeyframedMotion(LSL_List frames, LSL_List options)
        {
            m_LSL_Functions.llSetKeyframedMotion(frames, options);
        }

        public LSL_Integer llGetListEntryType(LSL_List src, int index)
        {
            return m_LSL_Functions.llGetListEntryType(src, index);
        }

        public LSL_Integer llGetListLength(LSL_List src)
        {
            return m_LSL_Functions.llGetListLength(src);
        }

        public LSL_Vector llGetLocalPos()
        {
            return m_LSL_Functions.llGetLocalPos();
        }

        public LSL_Rotation llGetLocalRot()
        {
            return m_LSL_Functions.llGetLocalRot();
        }

        public LSL_Float llGetMass()
        {
            return m_LSL_Functions.llGetMass();
        }

        public LSL_Float llGetMassMKS()
        {
            return m_LSL_Functions.llGetMassMKS();
        }

        public LSL_Integer llGetMemoryLimit()
        {
            return m_LSL_Functions.llGetMemoryLimit();
        }

        public void llGetNextEmail(string address, string subject)
        {
            m_LSL_Functions.llGetNextEmail(address, subject);
        }

        public LSL_String llGetNotecardLine(string name, int line)
        {
            return m_LSL_Functions.llGetNotecardLine(name, line);
        }

        public LSL_Key llGetNumberOfNotecardLines(string name)
        {
            return m_LSL_Functions.llGetNumberOfNotecardLines(name);
        }

        public LSL_Integer llGetNumberOfPrims()
        {
            return m_LSL_Functions.llGetNumberOfPrims();
        }

        public LSL_Integer llGetNumberOfSides()
        {
            return m_LSL_Functions.llGetNumberOfSides();
        }

        public LSL_String llGetObjectDesc()
        {
            return m_LSL_Functions.llGetObjectDesc();
        }

        public LSL_List llGetObjectDetails(string id, LSL_List args)
        {
            return m_LSL_Functions.llGetObjectDetails(id, args);
        }

        public LSL_Float llGetObjectMass(string id)
        {
            return m_LSL_Functions.llGetObjectMass(id);
        }

        public LSL_String llGetObjectName()
        {
            return m_LSL_Functions.llGetObjectName();
        }

        public LSL_Integer llGetObjectPermMask(int mask)
        {
            return m_LSL_Functions.llGetObjectPermMask(mask);
        }

        public LSL_Integer llGetObjectPrimCount(string object_id)
        {
            return m_LSL_Functions.llGetObjectPrimCount(object_id);
        }

        public LSL_Vector llGetOmega()
        {
            return m_LSL_Functions.llGetOmega();
        }

        public LSL_Key llGetOwner()
        {
            return m_LSL_Functions.llGetOwner();
        }

        public LSL_Key llGetOwnerKey(string id)
        {
            return m_LSL_Functions.llGetOwnerKey(id);
        }

        public LSL_List llGetParcelDetails(LSL_Vector pos, LSL_List param)
        {
            return m_LSL_Functions.llGetParcelDetails(pos, param);
        }

        public LSL_Integer llGetParcelFlags(LSL_Vector pos)
        {
            return m_LSL_Functions.llGetParcelFlags(pos);
        }

        public LSL_Integer llGetParcelMaxPrims(LSL_Vector pos, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelMaxPrims(pos, sim_wide);
        }

        public LSL_String llGetParcelMusicURL()
        {
            return m_LSL_Functions.llGetParcelMusicURL();
        }

        public LSL_Integer llGetParcelPrimCount(LSL_Vector pos, int category, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelPrimCount(pos, category, sim_wide);
        }

        public LSL_List llGetParcelPrimOwners(LSL_Vector pos)
        {
            return m_LSL_Functions.llGetParcelPrimOwners(pos);
        }

        public LSL_Integer llGetPermissions()
        {
            return m_LSL_Functions.llGetPermissions();
        }

        public LSL_Key llGetPermissionsKey()
        {
            return m_LSL_Functions.llGetPermissionsKey();
        }

        public LSL_Vector llGetPos()
        {
            return m_LSL_Functions.llGetPos();
        }

        public LSL_List llGetPrimitiveParams(LSL_List rules)
        {
            return m_LSL_Functions.llGetPrimitiveParams(rules);
        }

        public LSL_List llGetLinkPrimitiveParams(int linknum, LSL_List rules)
        {
            return m_LSL_Functions.llGetLinkPrimitiveParams(linknum, rules);
        }

        public LSL_Integer llGetRegionAgentCount()
        {
            return m_LSL_Functions.llGetRegionAgentCount();
        }

        public LSL_Vector llGetRegionCorner()
        {
            return m_LSL_Functions.llGetRegionCorner();
        }

        public LSL_Integer llGetRegionFlags()
        {
            return m_LSL_Functions.llGetRegionFlags();
        }

        public LSL_Float llGetRegionFPS()
        {
            return m_LSL_Functions.llGetRegionFPS();
        }

        public LSL_String llGetRegionName()
        {
            return m_LSL_Functions.llGetRegionName();
        }

        public LSL_Float llGetRegionTimeDilation()
        {
            return m_LSL_Functions.llGetRegionTimeDilation();
        }

        public LSL_Vector llGetRootPosition()
        {
            return m_LSL_Functions.llGetRootPosition();
        }

        public LSL_Rotation llGetRootRotation()
        {
            return m_LSL_Functions.llGetRootRotation();
        }

        public LSL_Rotation llGetRot()
        {
            return m_LSL_Functions.llGetRot();
        }

        public LSL_Vector llGetScale()
        {
            return m_LSL_Functions.llGetScale();
        }

        public LSL_String llGetScriptName()
        {
            return m_LSL_Functions.llGetScriptName();
        }

        public LSL_Integer llGetScriptState(string name)
        {
            return m_LSL_Functions.llGetScriptState(name);
        }

        public LSL_String llGetSimulatorHostname()
        {
            return m_LSL_Functions.llGetSimulatorHostname();
        }

        public LSL_Integer llGetSPMaxMemory()
        {
            return m_LSL_Functions.llGetSPMaxMemory();
        }

        public LSL_Integer llGetStartParameter()
        {
            return m_LSL_Functions.llGetStartParameter();
        }

        public LSL_Integer llGetStatus(int status)
        {
            return m_LSL_Functions.llGetStatus(status);
        }

        public LSL_String llGetSubString(string src, int start, int end)
        {
            return m_LSL_Functions.llGetSubString(src, start, end);
        }

        public LSL_Vector llGetSunDirection()
        {
            return m_LSL_Functions.llGetSunDirection();
        }

        public LSL_String llGetTexture(int face)
        {
            return m_LSL_Functions.llGetTexture(face);
        }

        public LSL_Vector llGetTextureOffset(int face)
        {
            return m_LSL_Functions.llGetTextureOffset(face);
        }

        public LSL_Float llGetTextureRot(int side)
        {
            return m_LSL_Functions.llGetTextureRot(side);
        }

        public LSL_Vector llGetTextureScale(int side)
        {
            return m_LSL_Functions.llGetTextureScale(side);
        }

        public LSL_Float llGetTime()
        {
            return m_LSL_Functions.llGetTime();
        }

        public LSL_Float llGetTimeOfDay()
        {
            return m_LSL_Functions.llGetTimeOfDay();
        }

        public LSL_String llGetTimestamp()
        {
            return m_LSL_Functions.llGetTimestamp();
        }

        public LSL_Vector llGetTorque()
        {
            return m_LSL_Functions.llGetTorque();
        }

        public LSL_Integer llGetUnixTime()
        {
            return m_LSL_Functions.llGetUnixTime();
        }

        public LSL_Integer llGetUsedMemory()
        {
            return m_LSL_Functions.llGetUsedMemory();
        }

        public LSL_Vector llGetVel()
        {
            return m_LSL_Functions.llGetVel();
        }

        public LSL_Float llGetWallclock()
        {
            return m_LSL_Functions.llGetWallclock();
        }

        public void llGiveInventory(string destination, string inventory)
        {
            m_LSL_Functions.llGiveInventory(destination, inventory);
        }

        public void llGiveInventoryList(string destination, string category, LSL_List inventory)
        {
            m_LSL_Functions.llGiveInventoryList(destination, category, inventory);
        }

        public void llGiveMoney(string destination, int amount)
        {
            m_LSL_Functions.llGiveMoney(destination, amount);
        }

        public LSL_String llTransferLindenDollars(string destination, int amount)
        {
            return m_LSL_Functions.llTransferLindenDollars(destination, amount);
        }

        public void llGodLikeRezObject(string inventory, LSL_Vector pos)
        {
            m_LSL_Functions.llGodLikeRezObject(inventory, pos);
        }

        public LSL_Float llGround(LSL_Vector offset)
        {
            return m_LSL_Functions.llGround(offset);
        }

        public LSL_Vector llGroundContour(LSL_Vector offset)
        {
            return m_LSL_Functions.llGroundContour(offset);
        }

        public LSL_Vector llGroundNormal(LSL_Vector offset)
        {
            return m_LSL_Functions.llGroundNormal(offset);
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            m_LSL_Functions.llGroundRepel(height, water, tau);
        }

        public LSL_Vector llGroundSlope(LSL_Vector offset)
        {
            return m_LSL_Functions.llGroundSlope(offset);
        }

        public LSL_String llHTTPRequest(string url, LSL_List parameters, string body)
        {
            return m_LSL_Functions.llHTTPRequest(url, parameters, body);
        }

        public void llHTTPResponse(LSL_Key id, int status, string body)
        {
            m_LSL_Functions.llHTTPResponse(id, status, body);
        }

        public LSL_String llInsertString(string dst, int position, string src)
        {
            return m_LSL_Functions.llInsertString(dst, position, src);
        }

        public void llInstantMessage(string user, string message)
        {
            m_LSL_Functions.llInstantMessage(user, message);
        }

        public LSL_String llIntegerToBase64(int number)
        {
            return m_LSL_Functions.llIntegerToBase64(number);
        }

        public LSL_String llKey2Name(string id)
        {
            return m_LSL_Functions.llKey2Name(id);
        }

        public LSL_String llGetUsername(string id)
        {
            return m_LSL_Functions.llGetUsername(id);
        }

        public LSL_String llRequestUsername(string id)
        {
            return m_LSL_Functions.llRequestUsername(id);
        }

        public LSL_String llGetDisplayName(string id)
        {
            return m_LSL_Functions.llGetDisplayName(id);
        }

        public LSL_String llRequestDisplayName(string id)
        {
            return m_LSL_Functions.llRequestDisplayName(id);
        }

        public LSL_List llCastRay(LSL_Vector start, LSL_Vector end, LSL_List options)
        {
            return m_LSL_Functions.llCastRay(start, end, options);
        }

        public void llLinkParticleSystem(int linknum, LSL_List rules)
        {
            m_LSL_Functions.llLinkParticleSystem(linknum, rules);
        }

        public LSL_String llList2CSV(LSL_List src)
        {
            return m_LSL_Functions.llList2CSV(src);
        }

        public LSL_Float llList2Float(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Float(src, index);
        }

        public LSL_Integer llList2Integer(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Integer(src, index);
        }

        public LSL_Key llList2Key(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Key(src, index);
        }

        public LSL_List llList2List(LSL_List src, int start, int end)
        {
            return m_LSL_Functions.llList2List(src, start, end);
        }

        public LSL_List llList2ListStrided(LSL_List src, int start, int end, int stride)
        {
            return m_LSL_Functions.llList2ListStrided(src, start, end, stride);
        }

        public LSL_Rotation llList2Rot(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Rot(src, index);
        }

        public LSL_String llList2String(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2String(src, index);
        }

        public LSL_Vector llList2Vector(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Vector(src, index);
        }

        public LSL_Integer llListen(int channelID, string name, string ID, string msg)
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

        public LSL_Integer llListFindList(LSL_List src, LSL_List test)
        {
            return m_LSL_Functions.llListFindList(src, test);
        }

        public LSL_List llListInsertList(LSL_List dest, LSL_List src, int start)
        {
            return m_LSL_Functions.llListInsertList(dest, src, start);
        }

        public LSL_List llListRandomize(LSL_List src, int stride)
        {
            return m_LSL_Functions.llListRandomize(src, stride);
        }

        public LSL_List llListReplaceList(LSL_List dest, LSL_List src, int start, int end)
        {
            return m_LSL_Functions.llListReplaceList(dest, src, start, end);
        }

        public LSL_List llListSort(LSL_List src, int stride, int ascending)
        {
            return m_LSL_Functions.llListSort(src, stride, ascending);
        }

        public LSL_Float llListStatistics(int operation, LSL_List src)
        {
            return m_LSL_Functions.llListStatistics(operation, src);
        }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            m_LSL_Functions.llLoadURL(avatar_id, message, url);
        }

        public LSL_Float llLog(double val)
        {
            return m_LSL_Functions.llLog(val);
        }

        public LSL_Float llLog10(double val)
        {
            return m_LSL_Functions.llLog10(val);
        }

        public void llLookAt(LSL_Vector target, double strength, double damping)
        {
            m_LSL_Functions.llLookAt(target, strength, damping);
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

        public LSL_Integer llManageEstateAccess(int action, string avatar)
        {
            return m_LSL_Functions.llManageEstateAccess(action, avatar);
        }

        public void llMakeExplosion(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_LSL_Functions.llMakeExplosion(particles, scale, vel, lifetime, arc, texture, offset);
        }

        public void llMakeFire(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_LSL_Functions.llMakeFire(particles, scale, vel, lifetime, arc, texture, offset);
        }

        public void llMakeFountain(int particles, double scale, double vel, double lifetime, double arc, int bounce, string texture, LSL_Vector offset, double bounce_offset)
        {
            m_LSL_Functions.llMakeFountain(particles, scale, vel, lifetime, arc, bounce, texture, offset, bounce_offset);
        }

        public void llMakeSmoke(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_LSL_Functions.llMakeSmoke(particles, scale, vel, lifetime, arc, texture, offset);
        }

        public void llMapDestination(string simname, LSL_Vector pos, LSL_Vector look_at)
        {
            m_LSL_Functions.llMapDestination(simname, pos, look_at);
        }

        public LSL_String llMD5String(string src, int nonce)
        {
            return m_LSL_Functions.llMD5String(src, nonce);
        }

        public LSL_String llSHA1String(string src)
        {
            return m_LSL_Functions.llSHA1String(src);
        }

        public void llMessageLinked(int linknum, int num, string str, string id)
        {
            m_LSL_Functions.llMessageLinked(linknum, num, str, id);
        }

        public void llMinEventDelay(double delay)
        {
            m_LSL_Functions.llMinEventDelay(delay);
        }

        public void llModifyLand(int action, int brush)
        {
            m_LSL_Functions.llModifyLand(action, brush);
        }

        public LSL_Integer llModPow(int a, int b, int c)
        {
            return m_LSL_Functions.llModPow(a, b, c);
        }

        public void llMoveToTarget(LSL_Vector target, double tau)
        {
            m_LSL_Functions.llMoveToTarget(target, tau);
        }

        public void llOffsetTexture(double u, double v, int face)
        {
            m_LSL_Functions.llOffsetTexture(u, v, face);
        }

        public void llOpenRemoteDataChannel()
        {
            m_LSL_Functions.llOpenRemoteDataChannel();
        }

        public LSL_Integer llOverMyLand(string id)
        {
            return m_LSL_Functions.llOverMyLand(id);
        }

        public void llOwnerSay(string msg)
        {
            m_LSL_Functions.llOwnerSay(msg);
        }

        public void llParcelMediaCommandList(LSL_List commandList)
        {
            m_LSL_Functions.llParcelMediaCommandList(commandList);
        }

        public LSL_List llParcelMediaQuery(LSL_List aList)
        {
            return m_LSL_Functions.llParcelMediaQuery(aList);
        }

        public LSL_List llParseString2List(string str, LSL_List separators, LSL_List spacers)
        {
            return m_LSL_Functions.llParseString2List(str, separators, spacers);
        }

        public LSL_List llParseStringKeepNulls(string src, LSL_List seperators, LSL_List spacers)
        {
            return m_LSL_Functions.llParseStringKeepNulls(src, seperators, spacers);
        }

        public void llParticleSystem(LSL_List rules)
        {
            m_LSL_Functions.llParticleSystem(rules);
        }

        public void llPassCollisions(int pass)
        {
            m_LSL_Functions.llPassCollisions(pass);
        }

        public void llPassTouches(int pass)
        {
            m_LSL_Functions.llPassTouches(pass);
        }

        public void llPlaySound(string sound, double volume)
        {
            m_LSL_Functions.llPlaySound(sound, volume);
        }

        public void llPlaySoundSlave(string sound, double volume)
        {
            m_LSL_Functions.llPlaySoundSlave(sound, volume);
        }

        public void llPointAt(LSL_Vector pos)
        {
            m_LSL_Functions.llPointAt(pos);
        }

        public LSL_Float llPow(double fbase, double fexponent)
        {
            return m_LSL_Functions.llPow(fbase, fexponent);
        }

        public void llPreloadSound(string sound)
        {
            m_LSL_Functions.llPreloadSound(sound);
        }

        public void llPushObject(string target, LSL_Vector impulse, LSL_Vector ang_impulse, int local)
        {
            m_LSL_Functions.llPushObject(target, impulse, ang_impulse, local);
        }

        public void llRefreshPrimURL()
        {
            m_LSL_Functions.llRefreshPrimURL();
        }

        public void llRegionSay(int channelID, string text)
        {
            m_LSL_Functions.llRegionSay(channelID, text);
        }

        public void llRegionSayTo(string key, int channelID, string text)
        {
            m_LSL_Functions.llRegionSayTo(key, channelID, text);
        }

        public void llReleaseCamera(string avatar)
        {
            m_LSL_Functions.llReleaseCamera(avatar);
        }

        public void llReleaseURL(string url)
        {
            m_LSL_Functions.llReleaseURL(url);
        }

        public void llReleaseControls()
        {
            m_LSL_Functions.llReleaseControls();
        }

        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            m_LSL_Functions.llRemoteDataReply(channel, message_id, sdata, idata);
        }

        public void llRemoteDataSetRegion()
        {
            m_LSL_Functions.llRemoteDataSetRegion();
        }

        public void llRemoteLoadScript(string target, string name, int running, int start_param)
        {
            m_LSL_Functions.llRemoteLoadScript(target, name, running, start_param);
        }

        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            m_LSL_Functions.llRemoteLoadScriptPin(target, name, pin, running, start_param);
        }

        public void llRemoveFromLandBanList(string avatar)
        {
            m_LSL_Functions.llRemoveFromLandBanList(avatar);
        }

        public void llRemoveFromLandPassList(string avatar)
        {
            m_LSL_Functions.llRemoveFromLandPassList(avatar);
        }

        public void llRemoveInventory(string item)
        {
            m_LSL_Functions.llRemoveInventory(item);
        }

        public void llRemoveVehicleFlags(int flags)
        {
            m_LSL_Functions.llRemoveVehicleFlags(flags);
        }

        public LSL_Key llRequestAgentData(string id, int data)
        {
            return m_LSL_Functions.llRequestAgentData(id, data);
        }

        public LSL_Key llRequestInventoryData(string name)
        {
            return m_LSL_Functions.llRequestInventoryData(name);
        }

        public void llRequestPermissions(string agent, int perm)
        {
            m_LSL_Functions.llRequestPermissions(agent, perm);
        }

        public LSL_String llRequestSecureURL()
        {
            return m_LSL_Functions.llRequestSecureURL();
        }

        public LSL_Key llRequestSimulatorData(string simulator, int data)
        {
            return m_LSL_Functions.llRequestSimulatorData(simulator, data);
        }
        public LSL_Key llRequestURL()
        {
            return m_LSL_Functions.llRequestURL();
        }

        public void llResetLandBanList()
        {
            m_LSL_Functions.llResetLandBanList();
        }

        public void llResetLandPassList()
        {
            m_LSL_Functions.llResetLandPassList();
        }

        public void llResetOtherScript(string name)
        {
            m_LSL_Functions.llResetOtherScript(name);
        }

        public void llResetScript()
        {
            m_LSL_Functions.llResetScript();
        }

        public void llResetTime()
        {
            m_LSL_Functions.llResetTime();
        }

        public void llRezAtRoot(string inventory, LSL_Vector position, LSL_Vector velocity, LSL_Rotation rot, int param)
        {
            m_LSL_Functions.llRezAtRoot(inventory, position, velocity, rot, param);
        }

        public void llRezObject(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param)
        {
            m_LSL_Functions.llRezObject(inventory, pos, vel, rot, param);
        }

        public LSL_Float llRot2Angle(LSL_Rotation rot)
        {
            return m_LSL_Functions.llRot2Angle(rot);
        }

        public LSL_Vector llRot2Axis(LSL_Rotation rot)
        {
            return m_LSL_Functions.llRot2Axis(rot);
        }

        public LSL_Vector llRot2Euler(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Euler(r);
        }

        public LSL_Vector llRot2Fwd(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Fwd(r);
        }

        public LSL_Vector llRot2Left(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Left(r);
        }

        public LSL_Vector llRot2Up(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Up(r);
        }

        public void llRotateTexture(double rotation, int face)
        {
            m_LSL_Functions.llRotateTexture(rotation, face);
        }

        public LSL_Rotation llRotBetween(LSL_Vector start, LSL_Vector end)
        {
            return m_LSL_Functions.llRotBetween(start, end);
        }

        public void llRotLookAt(LSL_Rotation target, double strength, double damping)
        {
            m_LSL_Functions.llRotLookAt(target, strength, damping);
        }

        public LSL_Integer llRotTarget(LSL_Rotation rot, double error)
        {
            return m_LSL_Functions.llRotTarget(rot, error);
        }

        public void llRotTargetRemove(int number)
        {
            m_LSL_Functions.llRotTargetRemove(number);
        }

        public LSL_Integer llRound(double f)
        {
            return m_LSL_Functions.llRound(f);
        }

        public LSL_Integer llSameGroup(string agent)
        {
            return m_LSL_Functions.llSameGroup(agent);
        }

        public void llSay(int channelID, string text)
        {
            m_LSL_Functions.llSay(channelID, text);
        }

        public void llScaleTexture(double u, double v, int face)
        {
            m_LSL_Functions.llScaleTexture(u, v, face);
        }

        public LSL_Integer llScriptDanger(LSL_Vector pos)
        {
            return m_LSL_Functions.llScriptDanger(pos);
        }

        public void llScriptProfiler(LSL_Integer flags)
        {
            m_LSL_Functions.llScriptProfiler(flags);
        }

        public LSL_Key llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            return m_LSL_Functions.llSendRemoteData(channel, dest, idata, sdata);
        }

        public void llSensor(string name, string id, int type, double range, double arc)
        {
            m_LSL_Functions.llSensor(name, id, type, range, arc);
        }

        public void llSensorRemove()
        {
            m_LSL_Functions.llSensorRemove();
        }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            m_LSL_Functions.llSensorRepeat(name, id, type, range, arc, rate);
        }

        public void llSetAlpha(double alpha, int face)
        {
            m_LSL_Functions.llSetAlpha(alpha, face);
        }

        public void llSetBuoyancy(double buoyancy)
        {
            m_LSL_Functions.llSetBuoyancy(buoyancy);
        }

        public void llSetCameraAtOffset(LSL_Vector offset)
        {
            m_LSL_Functions.llSetCameraAtOffset(offset);
        }

        public void llSetCameraEyeOffset(LSL_Vector offset)
        {
            m_LSL_Functions.llSetCameraEyeOffset(offset);
        }

        public void llSetLinkCamera(LSL_Integer link, LSL_Vector eye, LSL_Vector at)
        {
            m_LSL_Functions.llSetLinkCamera(link, eye, at);
        }

        public void llSetCameraParams(LSL_List rules)
        {
            m_LSL_Functions.llSetCameraParams(rules);
        }

        public void llSetClickAction(int action)
        {
            m_LSL_Functions.llSetClickAction(action);
        }

        public void llSetColor(LSL_Vector color, int face)
        {
            m_LSL_Functions.llSetColor(color, face);
        }

        public void llSetContentType(LSL_Key id, LSL_Integer type)
        {
            m_LSL_Functions.llSetContentType(id, type);
        }

        public void llSetDamage(double damage)
        {
            m_LSL_Functions.llSetDamage(damage);
        }

        public void llSetForce(LSL_Vector force, int local)
        {
            m_LSL_Functions.llSetForce(force, local);
        }

        public void llSetForceAndTorque(LSL_Vector force, LSL_Vector torque, int local)
        {
            m_LSL_Functions.llSetForceAndTorque(force, torque, local);
        }

        public void llSetVelocity(LSL_Vector force, int local)
        {
            m_LSL_Functions.llSetVelocity(force, local);
        }

        public void llSetAngularVelocity(LSL_Vector force, int local)
        {
            m_LSL_Functions.llSetAngularVelocity(force, local);
        }

        public void llSetHoverHeight(double height, int water, double tau)
        {
            m_LSL_Functions.llSetHoverHeight(height, water, tau);
        }

        public void llSetInventoryPermMask(string item, int mask, int value)
        {
            m_LSL_Functions.llSetInventoryPermMask(item, mask, value);
        }

        public void llSetLinkAlpha(int linknumber, double alpha, int face)
        {
            m_LSL_Functions.llSetLinkAlpha(linknumber, alpha, face);
        }

        public void llSetLinkColor(int linknumber, LSL_Vector color, int face)
        {
            m_LSL_Functions.llSetLinkColor(linknumber, color, face);
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            m_LSL_Functions.llSetLinkPrimitiveParams(linknumber, rules);
        }

        public void llSetLinkTexture(int linknumber, string texture, int face)
        {
            m_LSL_Functions.llSetLinkTexture(linknumber, texture, face);
        }

        public void llSetLinkTextureAnim(int linknum, int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_LSL_Functions.llSetLinkTextureAnim(linknum, mode, face, sizex, sizey, start, length, rate);
        }

        public void llSetLocalRot(LSL_Rotation rot)
        {
            m_LSL_Functions.llSetLocalRot(rot);
        }

        public LSL_Integer llSetMemoryLimit(LSL_Integer limit)
        {
            return m_LSL_Functions.llSetMemoryLimit(limit);
        }

        public void llSetObjectDesc(string desc)
        {
            m_LSL_Functions.llSetObjectDesc(desc);
        }

        public void llSetObjectName(string name)
        {
            m_LSL_Functions.llSetObjectName(name);
        }

        public void llSetObjectPermMask(int mask, int value)
        {
            m_LSL_Functions.llSetObjectPermMask(mask, value);
        }

        public void llSetParcelMusicURL(string url)
        {
            m_LSL_Functions.llSetParcelMusicURL(url);
        }

        public void llSetPayPrice(int price, LSL_List quick_pay_buttons)
        {
            m_LSL_Functions.llSetPayPrice(price, quick_pay_buttons);
        }

        public void llSetPos(LSL_Vector pos)
        {
            m_LSL_Functions.llSetPos(pos);
        }

        public void llSetPrimitiveParams(LSL_List rules)
        {
            m_LSL_Functions.llSetPrimitiveParams(rules);
        }

        public void llSetLinkPrimitiveParamsFast(int linknum, LSL_List rules)
        {
            m_LSL_Functions.llSetLinkPrimitiveParamsFast(linknum, rules);
        }

        public void llSetPrimURL(string url)
        {
            m_LSL_Functions.llSetPrimURL(url);
        }

        public LSL_Integer llSetRegionPos(LSL_Vector pos)
        {
            return m_LSL_Functions.llSetRegionPos(pos);
        }

        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_LSL_Functions.llSetRemoteScriptAccessPin(pin);
        }

        public void llSetRot(LSL_Rotation rot)
        {
            m_LSL_Functions.llSetRot(rot);
        }

        public void llSetScale(LSL_Vector scale)
        {
            m_LSL_Functions.llSetScale(scale);
        }

        public void llSetScriptState(string name, int run)
        {
            m_LSL_Functions.llSetScriptState(name, run);
        }

        public void llSetSitText(string text)
        {
            m_LSL_Functions.llSetSitText(text);
        }

        public void llSetSoundQueueing(int queue)
        {
            m_LSL_Functions.llSetSoundQueueing(queue);
        }

        public void llSetSoundRadius(double radius)
        {
            m_LSL_Functions.llSetSoundRadius(radius);
        }

        public void llSetStatus(int status, int value)
        {
            m_LSL_Functions.llSetStatus(status, value);
        }

        public void llSetText(string text, LSL_Vector color, double alpha)
        {
            m_LSL_Functions.llSetText(text, color, alpha);
        }

        public void llSetTexture(string texture, int face)
        {
            m_LSL_Functions.llSetTexture(texture, face);
        }

        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_LSL_Functions.llSetTextureAnim(mode, face, sizex, sizey, start, length, rate);
        }

        public void llSetTimerEvent(double sec)
        {
            m_LSL_Functions.llSetTimerEvent(sec);
        }

        public void llSetTorque(LSL_Vector torque, int local)
        {
            m_LSL_Functions.llSetTorque(torque, local);
        }

        public void llSetTouchText(string text)
        {
            m_LSL_Functions.llSetTouchText(text);
        }

        public void llSetVehicleFlags(int flags)
        {
            m_LSL_Functions.llSetVehicleFlags(flags);
        }

        public void llSetVehicleFloatParam(int param, LSL_Float value)
        {
            m_LSL_Functions.llSetVehicleFloatParam(param, value);
        }

        public void llSetVehicleRotationParam(int param, LSL_Rotation rot)
        {
            m_LSL_Functions.llSetVehicleRotationParam(param, rot);
        }

        public void llSetVehicleType(int type)
        {
            m_LSL_Functions.llSetVehicleType(type);
        }

        public void llSetVehicleVectorParam(int param, LSL_Vector vec)
        {
            m_LSL_Functions.llSetVehicleVectorParam(param, vec);
        }

        public void llShout(int channelID, string text)
        {
            m_LSL_Functions.llShout(channelID, text);
        }

        public LSL_Float llSin(double f)
        {
            return m_LSL_Functions.llSin(f);
        }

        public void llSitTarget(LSL_Vector offset, LSL_Rotation rot)
        {
            m_LSL_Functions.llSitTarget(offset, rot);
        }

        public void llLinkSitTarget(LSL_Integer link, LSL_Vector offset, LSL_Rotation rot)
        {
            m_LSL_Functions.llLinkSitTarget(link, offset, rot);
        }

        public void llSleep(double sec)
        {
            m_LSL_Functions.llSleep(sec);
        }

        public void llSound(string sound, double volume, int queue, int loop)
        {
            m_LSL_Functions.llSound(sound, volume, queue, loop);
        }

        public void llSoundPreload(string sound)
        {
            m_LSL_Functions.llSoundPreload(sound);
        }

        public LSL_Float llSqrt(double f)
        {
            return m_LSL_Functions.llSqrt(f);
        }

        public void llStartAnimation(string anim)
        {
            m_LSL_Functions.llStartAnimation(anim);
        }

        public void llStopAnimation(string anim)
        {
            m_LSL_Functions.llStopAnimation(anim);
        }

        public void llStopHover()
        {
            m_LSL_Functions.llStopHover();
        }

        public void llStopLookAt()
        {
            m_LSL_Functions.llStopLookAt();
        }

        public void llStopMoveToTarget()
        {
            m_LSL_Functions.llStopMoveToTarget();
        }

        public void llStopPointAt()
        {
            m_LSL_Functions.llStopPointAt();
        }

        public void llStopSound()
        {
            m_LSL_Functions.llStopSound();
        }

        public LSL_Integer llStringLength(string str)
        {
            return m_LSL_Functions.llStringLength(str);
        }

        public LSL_String llStringToBase64(string str)
        {
            return m_LSL_Functions.llStringToBase64(str);
        }

        public LSL_String llStringTrim(string src, int type)
        {
            return m_LSL_Functions.llStringTrim(src, type);
        }

        public LSL_Integer llSubStringIndex(string source, string pattern)
        {
            return m_LSL_Functions.llSubStringIndex(source, pattern);
        }

        public void llTakeCamera(string avatar)
        {
            m_LSL_Functions.llTakeCamera(avatar);
        }

        public void llTakeControls(int controls, int accept, int pass_on)
        {
            m_LSL_Functions.llTakeControls(controls, accept, pass_on);
        }

        public LSL_Float llTan(double f)
        {
            return m_LSL_Functions.llTan(f);
        }

        public LSL_Integer llTarget(LSL_Vector position, double range)
        {
            return m_LSL_Functions.llTarget(position, range);
        }

        public void llTargetOmega(LSL_Vector axis, double spinrate, double gain)
        {
            m_LSL_Functions.llTargetOmega(axis, spinrate, gain);
        }

        public void llTargetRemove(int number)
        {
            m_LSL_Functions.llTargetRemove(number);
        }

        public void llTeleportAgent(string agent, string simname, LSL_Vector pos, LSL_Vector lookAt)
        {
            m_LSL_Functions.llTeleportAgent(agent, simname, pos, lookAt);
        }

        public void llTeleportAgentGlobalCoords(string agent, LSL_Vector global, LSL_Vector pos, LSL_Vector lookAt)
        {
            m_LSL_Functions.llTeleportAgentGlobalCoords(agent, global, pos, lookAt);
        }

        public void llTeleportAgentHome(string agent)
        {
            m_LSL_Functions.llTeleportAgentHome(agent);
        }

        public void llTextBox(string avatar, string message, int chat_channel)
        {
            m_LSL_Functions.llTextBox(avatar, message, chat_channel);
        }

        public LSL_String llToLower(string source)
        {
            return m_LSL_Functions.llToLower(source);
        }

        public LSL_String llToUpper(string source)
        {
            return m_LSL_Functions.llToUpper(source);
        }

        public void llTriggerSound(string sound, double volume)
        {
            m_LSL_Functions.llTriggerSound(sound, volume);
        }

        public void llTriggerSoundLimited(string sound, double volume, LSL_Vector top_north_east, LSL_Vector bottom_south_west)
        {
            m_LSL_Functions.llTriggerSoundLimited(sound, volume, top_north_east, bottom_south_west);
        }

        public LSL_String llUnescapeURL(string url)
        {
            return m_LSL_Functions.llUnescapeURL(url);
        }

        public void llUnSit(string id)
        {
            m_LSL_Functions.llUnSit(id);
        }

        public LSL_Float llVecDist(LSL_Vector a, LSL_Vector b)
        {
            return m_LSL_Functions.llVecDist(a, b);
        }

        public LSL_Float llVecMag(LSL_Vector v)
        {
            return m_LSL_Functions.llVecMag(v);
        }

        public LSL_Vector llVecNorm(LSL_Vector v)
        {
            return m_LSL_Functions.llVecNorm(v);
        }

        public void llVolumeDetect(int detect)
        {
            m_LSL_Functions.llVolumeDetect(detect);
        }

        public LSL_Float llWater(LSL_Vector offset)
        {
            return m_LSL_Functions.llWater(offset);
        }

        public void llWhisper(int channelID, string text)
        {
            m_LSL_Functions.llWhisper(channelID, text);
        }

        public LSL_Vector llWind(LSL_Vector offset)
        {
            return m_LSL_Functions.llWind(offset);
        }

        public LSL_String llXorBase64Strings(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64Strings(str1, str2);
        }

        public LSL_String llXorBase64StringsCorrect(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64StringsCorrect(str1, str2);
        }
        
        public LSL_List llGetPrimMediaParams(int face, LSL_List rules)
        {
            return m_LSL_Functions.llGetPrimMediaParams(face, rules);
        }

        public LSL_List llGetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            return m_LSL_Functions.llGetLinkMedia(link, face, rules);
        }

        public LSL_Integer llSetPrimMediaParams(int face, LSL_List rules)
        {
            return m_LSL_Functions.llSetPrimMediaParams(face, rules);
        }

        public LSL_Integer llSetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            return m_LSL_Functions.llSetLinkMedia(link, face, rules);
        }

        public LSL_Integer llClearPrimMedia(LSL_Integer face)
        {
            return m_LSL_Functions.llClearPrimMedia(face);
        }

        public LSL_Integer llClearLinkMedia(LSL_Integer link, LSL_Integer face)
        {
            return m_LSL_Functions.llClearLinkMedia(link, face);
        }

        public void print(string str)
        {
            m_LSL_Functions.print(str);
        }
    }
}
