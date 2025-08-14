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
using System.Diagnostics; //for [DebuggerNonUserCode]
using System.Runtime.CompilerServices;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

#pragma warning disable IDE1006

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass
    {
        public ILSL_Api m_LSL_Functions;

        public void ApiTypeLSL(IScriptApi api)
        {
            if (api is ILSL_Api p)
                m_LSL_Functions = p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void state(string newState)
        {
            m_LSL_Functions.state(newState);
        }

        //
        // Script functions
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llAbs(LSL_Integer i)
        {
            return m_LSL_Functions.llAbs(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llAcos(LSL_Float val)
        {
            return m_LSL_Functions.llAcos(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llAddToLandBanList(LSL_Key avatar, LSL_Float hours)
        {
            m_LSL_Functions.llAddToLandBanList(avatar, hours);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llAddToLandPassList(LSL_Key avatar, LSL_Float hours)
        {
            m_LSL_Functions.llAddToLandPassList(avatar, hours);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llAdjustSoundVolume(LSL_Float volume)
        {
            m_LSL_Functions.llAdjustSoundVolume(volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llAllowInventoryDrop(LSL_Integer add)
        {
            m_LSL_Functions.llAllowInventoryDrop(add);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llAngleBetween(LSL_Rotation a, LSL_Rotation b)
        {
            return m_LSL_Functions.llAngleBetween(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llApplyImpulse(LSL_Vector force, LSL_Integer local)
        {
            m_LSL_Functions.llApplyImpulse(force, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llApplyRotationalImpulse(LSL_Vector force, int local)
        {
            m_LSL_Functions.llApplyRotationalImpulse(force, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llAsin(LSL_Float val)
        {
            return m_LSL_Functions.llAsin(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llAtan2(LSL_Float y, LSL_Float x)
        {
            return m_LSL_Functions.llAtan2(y, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llAttachToAvatar(LSL_Integer attachment)
        {
            m_LSL_Functions.llAttachToAvatar(attachment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llAttachToAvatarTemp(LSL_Integer attachment)
        {
            m_LSL_Functions.llAttachToAvatarTemp(attachment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llAvatarOnSitTarget()
        {
            return m_LSL_Functions.llAvatarOnSitTarget();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llAvatarOnLinkSitTarget(LSL_Integer linknum)
        {
            return m_LSL_Functions.llAvatarOnLinkSitTarget(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llAxes2Rot(LSL_Vector fwd, LSL_Vector left, LSL_Vector up)
        {
            return m_LSL_Functions.llAxes2Rot(fwd, left, up);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llAxisAngle2Rot(LSL_Vector axis, double angle)
        {
            return m_LSL_Functions.llAxisAngle2Rot(axis, angle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llBase64ToInteger(string str)
        {
            return m_LSL_Functions.llBase64ToInteger(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llBase64ToString(string str)
        {
            return m_LSL_Functions.llBase64ToString(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llBreakAllLinks()
        {
            m_LSL_Functions.llBreakAllLinks();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llBreakLink(int linknum)
        {
            m_LSL_Functions.llBreakLink(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llCeil(double f)
        {
            return m_LSL_Functions.llCeil(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llClearCameraParams()
        {
            m_LSL_Functions.llClearCameraParams();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llCloseRemoteDataChannel(string channel)
        {
            m_LSL_Functions.llCloseRemoteDataChannel(channel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llCloud(LSL_Vector offset)
        {
            return m_LSL_Functions.llCloud(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llCollisionFilter(LSL_String name, LSL_Key id, LSL_Integer accept)
        {
            m_LSL_Functions.llCollisionFilter(name, id, accept);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llCollisionSound(LSL_String impact_sound, LSL_Float impact_volume)
        {
            m_LSL_Functions.llCollisionSound(impact_sound, impact_volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llCollisionSprite(LSL_String impact_sprite)
        {
            m_LSL_Functions.llCollisionSprite(impact_sprite);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llCos(double f)
        {
            return m_LSL_Functions.llCos(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llCreateLink(LSL_Key target, LSL_Integer parent)
        {
            m_LSL_Functions.llCreateLink(target, parent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llCSV2List(string src)
        {
            return m_LSL_Functions.llCSV2List(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llDeleteSubList(LSL_List src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubList(src, start, end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llDeleteSubString(string src, int start, int end)
        {
            return m_LSL_Functions.llDeleteSubString(src, start, end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llDetachFromAvatar()
        {
            m_LSL_Functions.llDetachFromAvatar();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedGrab(int number)
        {
            return m_LSL_Functions.llDetectedGrab(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llDetectedGroup(int number)
        {
            return m_LSL_Functions.llDetectedGroup(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llDetectedKey(int number)
        {
            return m_LSL_Functions.llDetectedKey(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llDetectedLinkNumber(int number)
        {
            return m_LSL_Functions.llDetectedLinkNumber(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llDetectedName(int number)
        {
            return m_LSL_Functions.llDetectedName(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llDetectedOwner(int number)
        {
            return m_LSL_Functions.llDetectedOwner(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedPos(int number)
        {
            return m_LSL_Functions.llDetectedPos(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llDetectedRot(int number)
        {
            return m_LSL_Functions.llDetectedRot(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llDetectedType(int number)
        {
            return m_LSL_Functions.llDetectedType(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedTouchBinormal(int index)
        {
            return m_LSL_Functions.llDetectedTouchBinormal(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llDetectedTouchFace(int index)
        {
            return m_LSL_Functions.llDetectedTouchFace(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedTouchNormal(int index)
        {
            return m_LSL_Functions.llDetectedTouchNormal(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedTouchPos(int index)
        {
            return m_LSL_Functions.llDetectedTouchPos(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedTouchST(int index)
        {
            return m_LSL_Functions.llDetectedTouchST(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedTouchUV(int index)
        {
            return m_LSL_Functions.llDetectedTouchUV(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llDetectedVel(int number)
        {
            return m_LSL_Functions.llDetectedVel(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llDialog(LSL_Key avatar, LSL_String message, LSL_List buttons, int chat_channel)
        {
            m_LSL_Functions.llDialog(avatar, message, buttons, chat_channel);
        }

        [DebuggerNonUserCode]
        public void llDie()
        {
            m_LSL_Functions.llDie();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llDumpList2String(LSL_List src, string seperator)
        {
            return m_LSL_Functions.llDumpList2String(src, seperator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llEdgeOfWorld(LSL_Vector pos, LSL_Vector dir)
        {
            return m_LSL_Functions.llEdgeOfWorld(pos, dir);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llEjectFromLand(LSL_Key pest)
        {
            m_LSL_Functions.llEjectFromLand(pest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llEmail(string address, string subject, string message)
        {
            m_LSL_Functions.llEmail(address, subject, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llEscapeURL(string url)
        {
            return m_LSL_Functions.llEscapeURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llEuler2Rot(LSL_Vector v)
        {
            return m_LSL_Functions.llEuler2Rot(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llFabs(double f)
        {
            return m_LSL_Functions.llFabs(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llFloor(double f)
        {
            return m_LSL_Functions.llFloor(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llForceMouselook(int mouselook)
        {
            m_LSL_Functions.llForceMouselook(mouselook);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llFrand(double mag)
        {
            return m_LSL_Functions.llFrand(mag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGenerateKey()
        {
            return m_LSL_Functions.llGenerateKey();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetAccel()
        {
            return m_LSL_Functions.llGetAccel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetAgentInfo(LSL_Key id)
        {
            return m_LSL_Functions.llGetAgentInfo(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetAgentLanguage(LSL_Key id)
        {
            return m_LSL_Functions.llGetAgentLanguage(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetAgentList(LSL_Integer scope, LSL_List options)
        {
            return m_LSL_Functions.llGetAgentList(scope, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetAgentSize(LSL_Key id)
        {
            return m_LSL_Functions.llGetAgentSize(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetAlpha(int face)
        {
            return m_LSL_Functions.llGetAlpha(face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetAndResetTime()
        {
            return m_LSL_Functions.llGetAndResetTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetAnimation(LSL_Key id)
        {
            return m_LSL_Functions.llGetAnimation(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetAnimationList(LSL_Key id)
        {
            return m_LSL_Functions.llGetAnimationList(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetAttached()
        {
            return m_LSL_Functions.llGetAttached();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetAttachedList(LSL_Key id)
        {
            return m_LSL_Functions.llGetAttachedList(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetBoundingBox(string obj)
        {
            return m_LSL_Functions.llGetBoundingBox(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetCameraAspect()
        {
            return m_LSL_Functions.llGetCameraAspect();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetCameraFOV()
        {
            return m_LSL_Functions.llGetCameraFOV();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetCameraPos()
        {
            return m_LSL_Functions.llGetCameraPos();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetCameraRot()
        {
            return m_LSL_Functions.llGetCameraRot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetCenterOfMass()
        {
            return m_LSL_Functions.llGetCenterOfMass();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetColor(int face)
        {
            return m_LSL_Functions.llGetColor(face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetCreator()
        {
            return m_LSL_Functions.llGetCreator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetDate()
        {
            return m_LSL_Functions.llGetDate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetEnergy()
        {
            return m_LSL_Functions.llGetEnergy();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetEnv(LSL_String name)
        {
            return m_LSL_Functions.llGetEnv(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetForce()
        {
            return m_LSL_Functions.llGetForce();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetFreeMemory()
        {
            return m_LSL_Functions.llGetFreeMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetUsedMemory()
        {
            return m_LSL_Functions.llGetUsedMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetFreeURLs()
        {
            return m_LSL_Functions.llGetFreeURLs();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetGeometricCenter()
        {
            return m_LSL_Functions.llGetGeometricCenter();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetGMTclock()
        {
            return m_LSL_Functions.llGetGMTclock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetHTTPHeader(LSL_Key request_id, string header)
        {
            return m_LSL_Functions.llGetHTTPHeader(request_id, header);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetInventoryAcquireTime(string item)
        {
            return m_LSL_Functions.llGetInventoryAcquireTime(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetInventoryCreator(string item)
        {
            return m_LSL_Functions.llGetInventoryCreator(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetInventoryKey(string name)
        {
            return m_LSL_Functions.llGetInventoryKey(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetInventoryName(int type, int number)
        {
            return m_LSL_Functions.llGetInventoryName(type, number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetInventoryNumber(int type)
        {
            return m_LSL_Functions.llGetInventoryNumber(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetInventoryPermMask(string item, int mask)
        {
            return m_LSL_Functions.llGetInventoryPermMask(item, mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetInventoryDesc(string name)
        {
            return m_LSL_Functions.llGetInventoryDesc(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetInventoryType(string name)
        {
            return m_LSL_Functions.llGetInventoryType(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetKey()
        {
            return m_LSL_Functions.llGetKey();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetLandOwnerAt(LSL_Vector pos)
        {
            return m_LSL_Functions.llGetLandOwnerAt(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetLinkKey(int linknum)
        {
            return m_LSL_Functions.llGetLinkKey(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetObjectLinkKey(LSL_Key objectid, int linknum)
        {
            return m_LSL_Functions.llGetObjectLinkKey(objectid, linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetLinkName(int linknum)
        {
            return m_LSL_Functions.llGetLinkName(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetLinkNumber()
        {
            return m_LSL_Functions.llGetLinkNumber();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetLinkNumberOfSides(int link)
        {
            return m_LSL_Functions.llGetLinkNumberOfSides(link);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetListEntryType(LSL_List src, int index)
        {
            return m_LSL_Functions.llGetListEntryType(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetListLength(LSL_List src)
        {
            return m_LSL_Functions.llGetListLength(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetLocalPos()
        {
            return m_LSL_Functions.llGetLocalPos();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetLocalRot()
        {
            return m_LSL_Functions.llGetLocalRot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetMass()
        {
            return m_LSL_Functions.llGetMass();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetMassMKS()
        {
            return m_LSL_Functions.llGetMassMKS();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetMemoryLimit()
        {
            return m_LSL_Functions.llGetMemoryLimit();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llGetNextEmail(string address, string subject)
        {
            m_LSL_Functions.llGetNextEmail(address, subject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetNotecardLine(string name, int line)
        {
            return m_LSL_Functions.llGetNotecardLine(name, line);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetNumberOfNotecardLines(string name)
        {
            return m_LSL_Functions.llGetNumberOfNotecardLines(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetNotecardLineSync(string name, int line)
        {
            return m_LSL_Functions.llGetNotecardLineSync(name, line);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetNumberOfPrims()
        {
            return m_LSL_Functions.llGetNumberOfPrims();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetNumberOfSides()
        {
            return m_LSL_Functions.llGetNumberOfSides();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetObjectDesc()
        {
            return m_LSL_Functions.llGetObjectDesc();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetObjectDetails(LSL_Key id, LSL_List args)
        {
            return m_LSL_Functions.llGetObjectDetails(id, args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetObjectMass(string id)
        {
            return m_LSL_Functions.llGetObjectMass(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetObjectName()
        {
            return m_LSL_Functions.llGetObjectName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetObjectPermMask(int mask)
        {
            return m_LSL_Functions.llGetObjectPermMask(mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetObjectPrimCount(LSL_Key object_id)
        {
            return m_LSL_Functions.llGetObjectPrimCount(object_id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetOmega()
        {
            return m_LSL_Functions.llGetOmega();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetOwner()
        {
            return m_LSL_Functions.llGetOwner();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetOwnerKey(string id)
        {
            return m_LSL_Functions.llGetOwnerKey(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetParcelDetails(LSL_Vector pos, LSL_List param)
        {
            return m_LSL_Functions.llGetParcelDetails(pos, param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetParcelFlags(LSL_Vector pos)
        {
            return m_LSL_Functions.llGetParcelFlags(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetParcelMaxPrims(LSL_Vector pos, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelMaxPrims(pos, sim_wide);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetParcelMusicURL()
        {
            return m_LSL_Functions.llGetParcelMusicURL();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetParcelPrimCount(LSL_Vector pos, int category, int sim_wide)
        {
            return m_LSL_Functions.llGetParcelPrimCount(pos, category, sim_wide);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetParcelPrimOwners(LSL_Vector pos)
        {
            return m_LSL_Functions.llGetParcelPrimOwners(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetPermissions()
        {
            return m_LSL_Functions.llGetPermissions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llGetPermissionsKey()
        {
            return m_LSL_Functions.llGetPermissionsKey();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetPos()
        {
            return m_LSL_Functions.llGetPos();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetPrimitiveParams(LSL_List rules)
        {
            return m_LSL_Functions.llGetPrimitiveParams(rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetLinkPrimitiveParams(int linknum, LSL_List rules)
        {
            return m_LSL_Functions.llGetLinkPrimitiveParams(linknum, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetRegionAgentCount()
        {
            return m_LSL_Functions.llGetRegionAgentCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetRegionCorner()
        {
            return m_LSL_Functions.llGetRegionCorner();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetRegionFlags()
        {
            return m_LSL_Functions.llGetRegionFlags();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetRegionFPS()
        {
            return m_LSL_Functions.llGetRegionFPS();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetRegionName()
        {
            return m_LSL_Functions.llGetRegionName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetRegionTimeDilation()
        {
            return m_LSL_Functions.llGetRegionTimeDilation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetRootPosition()
        {
            return m_LSL_Functions.llGetRootPosition();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetRootRotation()
        {
            return m_LSL_Functions.llGetRootRotation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetRot()
        {
            return m_LSL_Functions.llGetRot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetScale()
        {
            return m_LSL_Functions.llGetScale();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetScriptName()
        {
            return m_LSL_Functions.llGetScriptName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetScriptState(string name)
        {
            return m_LSL_Functions.llGetScriptState(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetSimulatorHostname()
        {
            return m_LSL_Functions.llGetSimulatorHostname();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetSPMaxMemory()
        {
            return m_LSL_Functions.llGetSPMaxMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetStartParameter()
        {
            return m_LSL_Functions.llGetStartParameter();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetStatus(int status)
        {
            return m_LSL_Functions.llGetStatus(status);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetSubString(string src, int start, int end)
        {
            return m_LSL_Functions.llGetSubString(src, start, end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetTexture(int face)
        {
            return m_LSL_Functions.llGetTexture(face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetTextureOffset(int face)
        {
            return m_LSL_Functions.llGetTextureOffset(face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetTextureRot(int side)
        {
            return m_LSL_Functions.llGetTextureRot(side);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetTextureScale(int side)
        {
            return m_LSL_Functions.llGetTextureScale(side);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetTime()
        {
            return m_LSL_Functions.llGetTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetTimeOfDay()
        {
            return m_LSL_Functions.llGetTimeOfDay();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetTimestamp()
        {
            return m_LSL_Functions.llGetTimestamp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetTorque()
        {
            return m_LSL_Functions.llGetTorque();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetUnixTime()
        {
            return m_LSL_Functions.llGetUnixTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetVel()
        {
            return m_LSL_Functions.llGetVel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetWallclock()
        {
            return m_LSL_Functions.llGetWallclock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llGiveInventory(LSL_Key destination, LSL_String inventory)
        {
            m_LSL_Functions.llGiveInventory(destination, inventory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llGiveInventoryList(LSL_Key destination, LSL_String category, LSL_List inventory)
        {
            m_LSL_Functions.llGiveInventoryList(destination, category, inventory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGiveMoney(LSL_Key destination, LSL_Integer amount)
        {
            return m_LSL_Functions.llGiveMoney(destination, amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llTransferLindenDollars(LSL_Key destination, LSL_Integer amount)
        {
            return m_LSL_Functions.llTransferLindenDollars(destination, amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llGodLikeRezObject(LSL_String inventory, LSL_Vector pos)
        {
            m_LSL_Functions.llGodLikeRezObject(inventory, pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGround(LSL_Vector offset)
        {
            return m_LSL_Functions.llGround(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGroundContour(LSL_Vector offset)
        {
            return m_LSL_Functions.llGroundContour(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGroundNormal(LSL_Vector offset)
        {
            return m_LSL_Functions.llGroundNormal(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llGroundRepel(double height, int water, double tau)
        {
            m_LSL_Functions.llGroundRepel(height, water, tau);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGroundSlope(LSL_Vector offset)
        {
            return m_LSL_Functions.llGroundSlope(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llHTTPRequest(LSL_String url, LSL_List parameters, LSL_String body)
        {
            return m_LSL_Functions.llHTTPRequest(url, parameters, body);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llHTTPResponse(LSL_Key id, int status, LSL_String body)
        {
            m_LSL_Functions.llHTTPResponse(id, status, body);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llInsertString(LSL_String dst, int position, LSL_String src)
        {
            return m_LSL_Functions.llInsertString(dst, position, src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llInstantMessage(LSL_String user, LSL_String message)
        {
            m_LSL_Functions.llInstantMessage(user, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llIntegerToBase64(int number)
        {
            return m_LSL_Functions.llIntegerToBase64(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llKey2Name(LSL_Key id)
        {
            return m_LSL_Functions.llKey2Name(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetUsername(LSL_Key id)
        {
            return m_LSL_Functions.llGetUsername(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestUsername(LSL_Key id)
        {
            return m_LSL_Functions.llRequestUsername(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetDisplayName(LSL_Key id)
        {
            return m_LSL_Functions.llGetDisplayName(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestDisplayName(LSL_Key id)
        {
            return m_LSL_Functions.llRequestDisplayName(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llCastRay(LSL_Vector start, LSL_Vector end, LSL_List options)
        {
            return m_LSL_Functions.llCastRay(start, end, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkParticleSystem(int linknum, LSL_List rules)
        {
            m_LSL_Functions.llLinkParticleSystem(linknum, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llList2CSV(LSL_List src)
        {
            return m_LSL_Functions.llList2CSV(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llList2Float(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Float(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llList2Integer(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Integer(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llList2Key(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Key(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llList2List(LSL_List src, int start, int end)
        {
            return m_LSL_Functions.llList2List(src, start, end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llList2ListStrided(LSL_List src, int start, int end, int stride)
        {
            return m_LSL_Functions.llList2ListStrided(src, start, end, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llList2ListSlice(LSL_List src, int start, int end, int stride, int stride_index)
        {
            return m_LSL_Functions.llList2ListSlice(src, start, end, stride, stride_index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llList2Rot(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Rot(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llList2String(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2String(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llList2Vector(LSL_List src, int index)
        {
            return m_LSL_Functions.llList2Vector(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llListen(int channelID, string name, string ID, string msg)
        {
            return m_LSL_Functions.llListen(channelID, name, ID, msg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llListenControl(int number, int active)
        {
            m_LSL_Functions.llListenControl(number, active);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llListenRemove(int number)
        {
            m_LSL_Functions.llListenRemove(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llListFindList(LSL_List src, LSL_List test)
        {
            return m_LSL_Functions.llListFindList(src, test);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llListFindListNext(LSL_List src, LSL_List test, LSL_Integer instance)
        {
            return m_LSL_Functions.llListFindListNext(src, test, instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llListFindStrided(LSL_List src, LSL_List test, LSL_Integer lstart, LSL_Integer lend, LSL_Integer lstride)
        {
            return m_LSL_Functions.llListFindStrided(src, test, lstart, lend, lstride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llListInsertList(LSL_List dest, LSL_List src, int start)
        {
            return m_LSL_Functions.llListInsertList(dest, src, start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llListRandomize(LSL_List src, int stride)
        {
            return m_LSL_Functions.llListRandomize(src, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llListReplaceList(LSL_List dest, LSL_List src, int start, int end)
        {
            return m_LSL_Functions.llListReplaceList(dest, src, start, end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llListSort(LSL_List src, int stride, int ascending)
        {
            return m_LSL_Functions.llListSort(src, stride, ascending);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llListSortStrided(LSL_List src, int stride, int stride_index, int ascending)
        {
            return m_LSL_Functions.llListSortStrided(src, stride, stride_index, ascending);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llListStatistics(int operation, LSL_List src)
        {
            return m_LSL_Functions.llListStatistics(operation, src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLoadURL(string avatar_id, string message, string url)
        {
            m_LSL_Functions.llLoadURL(avatar_id, message, url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llLog(double val)
        {
            return m_LSL_Functions.llLog(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llLog10(double val)
        {
            return m_LSL_Functions.llLog10(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLookAt(LSL_Vector target, double strength, double damping)
        {
            m_LSL_Functions.llLookAt(target, strength, damping);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLoopSound(string sound, double volume)
        {
            m_LSL_Functions.llLoopSound(sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLoopSoundMaster(string sound, double volume)
        {
            m_LSL_Functions.llLoopSoundMaster(sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLoopSoundSlave(string sound, double volume)
        {
            m_LSL_Functions.llLoopSoundSlave(sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llManageEstateAccess(int action, string avatar)
        {
            return m_LSL_Functions.llManageEstateAccess(action, avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMakeExplosion(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_LSL_Functions.llMakeExplosion(particles, scale, vel, lifetime, arc, texture, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMakeFire(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_LSL_Functions.llMakeFire(particles, scale, vel, lifetime, arc, texture, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMakeFountain(int particles, double scale, double vel, double lifetime, double arc, int bounce, string texture, LSL_Vector offset, double bounce_offset)
        {
            m_LSL_Functions.llMakeFountain(particles, scale, vel, lifetime, arc, bounce, texture, offset, bounce_offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMakeSmoke(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_LSL_Functions.llMakeSmoke(particles, scale, vel, lifetime, arc, texture, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMapBeacon(string simname, LSL_Vector pos, LSL_List loptions)
        {
            m_LSL_Functions.llMapBeacon(simname, pos, loptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMapDestination(string simname, LSL_Vector pos, LSL_Vector look_at)
        {
            m_LSL_Functions.llMapDestination(simname, pos, look_at);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llMD5String(string src, int nonce)
        {
            return m_LSL_Functions.llMD5String(src, nonce);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llSHA1String(string src)
        {
            return m_LSL_Functions.llSHA1String(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llSHA256String(LSL_String src)
        {
            return m_LSL_Functions.llSHA256String(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMessageLinked(int linknum, int num, string str, string id)
        {
            m_LSL_Functions.llMessageLinked(linknum, num, str, id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMinEventDelay(double delay)
        {
            m_LSL_Functions.llMinEventDelay(delay);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llModifyLand(int action, int brush)
        {
            m_LSL_Functions.llModifyLand(action, brush);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llModPow(int a, int b, int c)
        {
            return m_LSL_Functions.llModPow(a, b, c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llMoveToTarget(LSL_Vector target, double tau)
        {
            m_LSL_Functions.llMoveToTarget(target, tau);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llName2Key(LSL_String name)
        {
            return m_LSL_Functions.llName2Key(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llOffsetTexture(double u, double v, int face)
        {
            m_LSL_Functions.llOffsetTexture(u, v, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llOpenRemoteDataChannel()
        {
            m_LSL_Functions.llOpenRemoteDataChannel();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llOverMyLand(string id)
        {
            return m_LSL_Functions.llOverMyLand(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llOwnerSay(string msg)
        {
            m_LSL_Functions.llOwnerSay(msg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llParcelMediaCommandList(LSL_List commandList)
        {
            m_LSL_Functions.llParcelMediaCommandList(commandList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llParcelMediaQuery(LSL_List aList)
        {
            return m_LSL_Functions.llParcelMediaQuery(aList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llParseString2List(string str, LSL_List separators, LSL_List spacers)
        {
            return m_LSL_Functions.llParseString2List(str, separators, spacers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llParseStringKeepNulls(string src, LSL_List seperators, LSL_List spacers)
        {
            return m_LSL_Functions.llParseStringKeepNulls(src, seperators, spacers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llParticleSystem(LSL_List rules)
        {
            m_LSL_Functions.llParticleSystem(rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPassCollisions(int pass)
        {
            m_LSL_Functions.llPassCollisions(pass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPassTouches(int pass)
        {
            m_LSL_Functions.llPassTouches(pass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPlaySound(string sound, double volume)
        {
            m_LSL_Functions.llPlaySound(sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPlaySoundSlave(string sound, double volume)
        {
            m_LSL_Functions.llPlaySoundSlave(sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPointAt(LSL_Vector pos)
        {
            m_LSL_Functions.llPointAt(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llPow(double fbase, double fexponent)
        {
            return m_LSL_Functions.llPow(fbase, fexponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPreloadSound(string sound)
        {
            m_LSL_Functions.llPreloadSound(sound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llPushObject(string target, LSL_Vector impulse, LSL_Vector ang_impulse, int local)
        {
            m_LSL_Functions.llPushObject(target, impulse, ang_impulse, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRefreshPrimURL()
        {
            m_LSL_Functions.llRefreshPrimURL();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRegionSay(int channelID, string text)
        {
            m_LSL_Functions.llRegionSay(channelID, text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRegionSayTo(string key, int channelID, string text)
        {
            m_LSL_Functions.llRegionSayTo(key, channelID, text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llReleaseCamera(string avatar)
        {
            m_LSL_Functions.llReleaseCamera(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llReleaseURL(string url)
        {
            m_LSL_Functions.llReleaseURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llReleaseControls()
        {
            m_LSL_Functions.llReleaseControls();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            m_LSL_Functions.llRemoteDataReply(channel, message_id, sdata, idata);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoteDataSetRegion()
        {
            m_LSL_Functions.llRemoteDataSetRegion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoteLoadScript(string target, string name, int running, int start_param)
        {
            m_LSL_Functions.llRemoteLoadScript(target, name, running, start_param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            m_LSL_Functions.llRemoteLoadScriptPin(target, name, pin, running, start_param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoveFromLandBanList(string avatar)
        {
            m_LSL_Functions.llRemoveFromLandBanList(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoveFromLandPassList(string avatar)
        {
            m_LSL_Functions.llRemoveFromLandPassList(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoveInventory(string item)
        {
            m_LSL_Functions.llRemoveInventory(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRemoveVehicleFlags(int flags)
        {
            m_LSL_Functions.llRemoveVehicleFlags(flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestUserKey(LSL_String username)
        {
            return m_LSL_Functions.llRequestUserKey(username);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestAgentData(string id, int data)
        {
            return m_LSL_Functions.llRequestAgentData(id, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetVisualParams(string id, LSL_List visualparams)
        {
            return m_LSL_Functions.llGetVisualParams(id, visualparams);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestInventoryData(LSL_String name)
        {
            return m_LSL_Functions.llRequestInventoryData(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRequestPermissions(string agent, int perm)
        {
            m_LSL_Functions.llRequestPermissions(agent, perm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestSecureURL()
        {
            return m_LSL_Functions.llRequestSecureURL();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestSimulatorData(string simulator, int data)
        {
            return m_LSL_Functions.llRequestSimulatorData(simulator, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetSimStats(LSL_Integer stat_type)
        {
            return m_LSL_Functions.llGetSimStats(stat_type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRequestURL()
        {
            return m_LSL_Functions.llRequestURL();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llResetLandBanList()
        {
            m_LSL_Functions.llResetLandBanList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llResetLandPassList()
        {
            m_LSL_Functions.llResetLandPassList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llResetOtherScript(string name)
        {
            m_LSL_Functions.llResetOtherScript(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llResetScript()
        {
            m_LSL_Functions.llResetScript();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llResetTime()
        {
            m_LSL_Functions.llResetTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRezAtRoot(string inventory, LSL_Vector position, LSL_Vector velocity, LSL_Rotation rot, int param)
        {
            m_LSL_Functions.llRezAtRoot(inventory, position, velocity, rot, param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRezObject(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param)
        {
            m_LSL_Functions.llRezObject(inventory, pos, vel, rot, param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llRot2Angle(LSL_Rotation rot)
        {
            return m_LSL_Functions.llRot2Angle(rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llRot2Axis(LSL_Rotation rot)
        {
            return m_LSL_Functions.llRot2Axis(rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llRot2Euler(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Euler(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llRot2Fwd(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Fwd(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llRot2Left(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Left(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llRot2Up(LSL_Rotation r)
        {
            return m_LSL_Functions.llRot2Up(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRotateTexture(double rotation, int face)
        {
            m_LSL_Functions.llRotateTexture(rotation, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llRotBetween(LSL_Vector start, LSL_Vector end)
        {
            return m_LSL_Functions.llRotBetween(start, end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRotLookAt(LSL_Rotation target, double strength, double damping)
        {
            m_LSL_Functions.llRotLookAt(target, strength, damping);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llRotTarget(LSL_Rotation rot, double error)
        {
            return m_LSL_Functions.llRotTarget(rot, error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llRotTargetRemove(int number)
        {
            m_LSL_Functions.llRotTargetRemove(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llRound(double f)
        {
            return m_LSL_Functions.llRound(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llSameGroup(string agent)
        {
            return m_LSL_Functions.llSameGroup(agent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSay(int channelID, string text)
        {
            m_LSL_Functions.llSay(channelID, text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llScaleByFactor(double scaling_factor)
        {
            return m_LSL_Functions.llScaleByFactor(scaling_factor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetMaxScaleFactor()
        {
            return m_LSL_Functions.llGetMaxScaleFactor();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetMinScaleFactor()
        {
            return m_LSL_Functions.llGetMinScaleFactor();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llScaleTexture(double u, double v, int face)
        {
            m_LSL_Functions.llScaleTexture(u, v, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llScriptDanger(LSL_Vector pos)
        {
            return m_LSL_Functions.llScriptDanger(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llScriptProfiler(LSL_Integer flags)
        {
            m_LSL_Functions.llScriptProfiler(flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            return m_LSL_Functions.llSendRemoteData(channel, dest, idata, sdata);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSensor(string name, string id, int type, double range, double arc)
        {
            m_LSL_Functions.llSensor(name, id, type, range, arc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSensorRemove()
        {
            m_LSL_Functions.llSensorRemove();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            m_LSL_Functions.llSensorRepeat(name, id, type, range, arc, rate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetAlpha(double alpha, int face)
        {
            m_LSL_Functions.llSetAlpha(alpha, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetBuoyancy(double buoyancy)
        {
            m_LSL_Functions.llSetBuoyancy(buoyancy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetCameraAtOffset(LSL_Vector offset)
        {
            m_LSL_Functions.llSetCameraAtOffset(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetCameraEyeOffset(LSL_Vector offset)
        {
            m_LSL_Functions.llSetCameraEyeOffset(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkCamera(LSL_Integer link, LSL_Vector eye, LSL_Vector at)
        {
            m_LSL_Functions.llSetLinkCamera(link, eye, at);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetCameraParams(LSL_List rules)
        {
            m_LSL_Functions.llSetCameraParams(rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetClickAction(int action)
        {
            m_LSL_Functions.llSetClickAction(action);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetColor(LSL_Vector color, int face)
        {
            m_LSL_Functions.llSetColor(color, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetContentType(LSL_Key id, LSL_Integer type)
        {
            m_LSL_Functions.llSetContentType(id, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetDamage(double damage)
        {
            m_LSL_Functions.llSetDamage(damage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llGetHealth(LSL_String key)
        {
            return m_LSL_Functions.llGetHealth(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetForce(LSL_Vector force, int local)
        {
            m_LSL_Functions.llSetForce(force, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetForceAndTorque(LSL_Vector force, LSL_Vector torque, int local)
        {
            m_LSL_Functions.llSetForceAndTorque(force, torque, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetVelocity(LSL_Vector force, int local)
        {
            m_LSL_Functions.llSetVelocity(force, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetAngularVelocity(LSL_Vector force, int local)
        {
            m_LSL_Functions.llSetAngularVelocity(force, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetHoverHeight(double height, int water, double tau)
        {
            m_LSL_Functions.llSetHoverHeight(height, water, tau);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetInventoryPermMask(string item, int mask, int value)
        {
            m_LSL_Functions.llSetInventoryPermMask(item, mask, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkAlpha(int linknumber, double alpha, int face)
        {
            m_LSL_Functions.llSetLinkAlpha(linknumber, alpha, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkColor(int linknumber, LSL_Vector color, int face)
        {
            m_LSL_Functions.llSetLinkColor(linknumber, color, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            m_LSL_Functions.llSetLinkPrimitiveParams(linknumber, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkTexture(int linknumber, string texture, int face)
        {
            m_LSL_Functions.llSetLinkTexture(linknumber, texture, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkTextureAnim(int linknum, int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_LSL_Functions.llSetLinkTextureAnim(linknum, mode, face, sizex, sizey, start, length, rate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLocalRot(LSL_Rotation rot)
        {
            m_LSL_Functions.llSetLocalRot(rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llSetMemoryLimit(LSL_Integer limit)
        {
            return m_LSL_Functions.llSetMemoryLimit(limit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetObjectDesc(string desc)
        {
            m_LSL_Functions.llSetObjectDesc(desc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetObjectName(string name)
        {
            m_LSL_Functions.llSetObjectName(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetObjectPermMask(int mask, int value)
        {
            m_LSL_Functions.llSetObjectPermMask(mask, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetParcelMusicURL(string url)
        {
            m_LSL_Functions.llSetParcelMusicURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetPayPrice(int price, LSL_List quick_pay_buttons)
        {
            m_LSL_Functions.llSetPayPrice(price, quick_pay_buttons);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetPos(LSL_Vector pos)
        {
            m_LSL_Functions.llSetPos(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llSetRegionPos(LSL_Vector pos)
        {
            return m_LSL_Functions.llSetRegionPos(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetPrimitiveParams(LSL_List rules)
        {
            m_LSL_Functions.llSetPrimitiveParams(rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkPrimitiveParamsFast(int linknum, LSL_List rules)
        {
            m_LSL_Functions.llSetLinkPrimitiveParamsFast(linknum, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetPrimURL(string url)
        {
            m_LSL_Functions.llSetPrimURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_LSL_Functions.llSetRemoteScriptAccessPin(pin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetRot(LSL_Rotation rot)
        {
            m_LSL_Functions.llSetRot(rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetScale(LSL_Vector scale)
        {
            m_LSL_Functions.llSetScale(scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetScriptState(string name, int run)
        {
            m_LSL_Functions.llSetScriptState(name, run);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetSitText(string text)
        {
            m_LSL_Functions.llSetSitText(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetSoundQueueing(int queue)
        {
            m_LSL_Functions.llSetSoundQueueing(queue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetSoundRadius(double radius)
        {
            m_LSL_Functions.llSetSoundRadius(radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetStatus(int status, int value)
        {
            m_LSL_Functions.llSetStatus(status, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetText(string text, LSL_Vector color, double alpha)
        {
            m_LSL_Functions.llSetText(text, color, alpha);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetTexture(string texture, int face)
        {
            m_LSL_Functions.llSetTexture(texture, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_LSL_Functions.llSetTextureAnim(mode, face, sizex, sizey, start, length, rate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetTimerEvent(double sec)
        {
            m_LSL_Functions.llSetTimerEvent(sec);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetTorque(LSL_Vector torque, int local)
        {
            m_LSL_Functions.llSetTorque(torque, local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetTouchText(string text)
        {
            m_LSL_Functions.llSetTouchText(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetVehicleFlags(int flags)
        {
            m_LSL_Functions.llSetVehicleFlags(flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetVehicleFloatParam(int param, LSL_Float value)
        {
            m_LSL_Functions.llSetVehicleFloatParam(param, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetVehicleRotationParam(int param, LSL_Rotation rot)
        {
            m_LSL_Functions.llSetVehicleRotationParam(param, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetVehicleType(int type)
        {
            m_LSL_Functions.llSetVehicleType(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetVehicleVectorParam(int param, LSL_Vector vec)
        {
            m_LSL_Functions.llSetVehicleVectorParam(param, vec);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llShout(int channelID, string text)
        {
            m_LSL_Functions.llShout(channelID, text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llSin(double f)
        {
            return m_LSL_Functions.llSin(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSitTarget(LSL_Vector offset, LSL_Rotation rot)
        {
            m_LSL_Functions.llSitTarget(offset, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkSitTarget(LSL_Integer link, LSL_Vector offset, LSL_Rotation rot)
        {
            m_LSL_Functions.llLinkSitTarget(link, offset, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSleep(double sec)
        {
            m_LSL_Functions.llSleep(sec);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSound(string sound, double volume, int queue, int loop)
        {
            m_LSL_Functions.llSound(sound, volume, queue, loop);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSoundPreload(string sound)
        {
            m_LSL_Functions.llSoundPreload(sound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llSqrt(double f)
        {
            return m_LSL_Functions.llSqrt(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStartAnimation(string anim)
        {
            m_LSL_Functions.llStartAnimation(anim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopAnimation(string anim)
        {
            m_LSL_Functions.llStopAnimation(anim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStartObjectAnimation(string anim)
        {
            m_LSL_Functions.llStartObjectAnimation(anim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopObjectAnimation(string anim)
        {
            m_LSL_Functions.llStopObjectAnimation(anim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetObjectAnimationNames()
        {
            return m_LSL_Functions.llGetObjectAnimationNames();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopHover()
        {
            m_LSL_Functions.llStopHover();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopLookAt()
        {
            m_LSL_Functions.llStopLookAt();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopMoveToTarget()
        {
            m_LSL_Functions.llStopMoveToTarget();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopPointAt()
        {
            m_LSL_Functions.llStopPointAt();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llStopSound()
        {
            m_LSL_Functions.llStopSound();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llStringLength(string str)
        {
            return m_LSL_Functions.llStringLength(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llStringToBase64(string str)
        {
            return m_LSL_Functions.llStringToBase64(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llStringTrim(LSL_String src, LSL_Integer type)
        {
            return m_LSL_Functions.llStringTrim(src, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llSubStringIndex(string source, string pattern)
        {
            return m_LSL_Functions.llSubStringIndex(source, pattern);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTakeCamera(string avatar)
        {
            m_LSL_Functions.llTakeCamera(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTakeControls(int controls, int accept, int pass_on)
        {
            m_LSL_Functions.llTakeControls(controls, accept, pass_on);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llTan(double f)
        {
            return m_LSL_Functions.llTan(f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llTarget(LSL_Vector position, double range)
        {
            return m_LSL_Functions.llTarget(position, range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTargetOmega(LSL_Vector axis, double spinrate, double gain)
        {
            m_LSL_Functions.llTargetOmega(axis, spinrate, gain);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTargetRemove(int number)
        {
            m_LSL_Functions.llTargetRemove(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTargetedEmail(LSL_Integer target, LSL_String subject, LSL_String message)
        {
            m_LSL_Functions.llTargetedEmail(target, subject, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTeleportAgent(string agent, string simname, LSL_Vector pos, LSL_Vector lookAt)
        {
            m_LSL_Functions.llTeleportAgent(agent, simname, pos, lookAt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTeleportAgentGlobalCoords(string agent, LSL_Vector global, LSL_Vector pos, LSL_Vector lookAt)
        {
            m_LSL_Functions.llTeleportAgentGlobalCoords(agent, global, pos, lookAt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTeleportAgentHome(string agent)
        {
            m_LSL_Functions.llTeleportAgentHome(agent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTextBox(string avatar, string message, int chat_channel)
        {
            m_LSL_Functions.llTextBox(avatar, message, chat_channel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llToLower(string source)
        {
            return m_LSL_Functions.llToLower(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llToUpper(string source)
        {
            return m_LSL_Functions.llToUpper(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTriggerSound(string sound, double volume)
        {
            m_LSL_Functions.llTriggerSound(sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llTriggerSoundLimited(string sound, double volume, LSL_Vector top_north_east, LSL_Vector bottom_south_west)
        {
            m_LSL_Functions.llTriggerSoundLimited(sound, volume, top_north_east, bottom_south_west);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llUnescapeURL(string url)
        {
            return m_LSL_Functions.llUnescapeURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llUnSit(string id)
        {
            m_LSL_Functions.llUnSit(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llVecDist(LSL_Vector a, LSL_Vector b)
        {
            return m_LSL_Functions.llVecDist(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llVecMag(LSL_Vector v)
        {
            return m_LSL_Functions.llVecMag(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llVecNorm(LSL_Vector v)
        {
            return m_LSL_Functions.llVecNorm(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llVolumeDetect(int detect)
        {
            m_LSL_Functions.llVolumeDetect(detect);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float llWater(LSL_Vector offset)
        {
            return m_LSL_Functions.llWater(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llWhisper(int channelID, string text)
        {
            m_LSL_Functions.llWhisper(channelID, text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llWind(LSL_Vector offset)
        {
            return m_LSL_Functions.llWind(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llXorBase64(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64(str1, str2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llXorBase64Strings(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64Strings(str1, str2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llXorBase64StringsCorrect(string str1, string str2)
        {
            return m_LSL_Functions.llXorBase64StringsCorrect(str1, str2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetPrimMediaParams(int face, LSL_List rules)
        {
            return m_LSL_Functions.llGetPrimMediaParams(face, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            return m_LSL_Functions.llGetLinkMedia(link, face, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llSetPrimMediaParams(int face, LSL_List rules)
        {
            return m_LSL_Functions.llSetPrimMediaParams(face, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llSetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            return m_LSL_Functions.llSetLinkMedia(link, face, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llClearPrimMedia(LSL_Integer face)
        {
            return m_LSL_Functions.llClearPrimMedia(face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llClearLinkMedia(LSL_Integer link, LSL_Integer face)
        {
            return m_LSL_Functions.llClearLinkMedia(link, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetLinkNumberOfSides(LSL_Integer link)
        {
            return m_LSL_Functions.llGetLinkNumberOfSides(link);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetKeyframedMotion(LSL_List frames, LSL_List options)
        {
            m_LSL_Functions.llSetKeyframedMotion(frames, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetPhysicsMaterial(int material_bits, LSL_Float material_gravity_modifier, LSL_Float material_restitution, LSL_Float material_friction, LSL_Float material_density)
        {
            m_LSL_Functions.llSetPhysicsMaterial(material_bits, material_gravity_modifier, material_restitution, material_friction, material_density);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llGetPhysicsMaterial()
        {
            return m_LSL_Functions.llGetPhysicsMaterial();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetAnimationOverride(LSL_String animState, LSL_String anim)
        {
            m_LSL_Functions.llSetAnimationOverride(animState, anim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llResetAnimationOverride(LSL_String anim_state)
        {
            m_LSL_Functions.llResetAnimationOverride(anim_state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llGetAnimationOverride(LSL_String anim_state)
        {
            return m_LSL_Functions.llGetAnimationOverride(anim_state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llJsonGetValue(LSL_String json, LSL_List specifiers)
        {
            return m_LSL_Functions.llJsonGetValue(json, specifiers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llJson2List(LSL_String json)
        {
            return m_LSL_Functions.llJson2List(json);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llList2Json(LSL_String type, LSL_List values)
        {
            return m_LSL_Functions.llList2Json(type, values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llJsonSetValue(LSL_String json, LSL_List specifiers, LSL_String value)
        {
            return m_LSL_Functions.llJsonSetValue(json, specifiers, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llJsonValueType(LSL_String json, LSL_List specifiers)
        {
            return m_LSL_Functions.llJsonValueType(json, specifiers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetDayLength()
        {
            return m_LSL_Functions.llGetDayLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetRegionDayLength()
        {
            return m_LSL_Functions.llGetRegionDayLength();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetDayOffset()
        {
            return m_LSL_Functions.llGetDayOffset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetRegionDayOffset()
        {
            return m_LSL_Functions.llGetRegionDayOffset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetSunDirection()
        {
            return m_LSL_Functions.llGetSunDirection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetRegionSunDirection()
        {
            return m_LSL_Functions.llGetRegionSunDirection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetMoonDirection()
        {
            return m_LSL_Functions.llGetMoonDirection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llGetRegionMoonDirection()
        {
            return m_LSL_Functions.llGetRegionMoonDirection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetSunRotation()
        {
            return m_LSL_Functions.llGetSunRotation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetRegionSunRotation()
        {
            return m_LSL_Functions.llGetRegionSunRotation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetMoonRotation()
        {
            return m_LSL_Functions.llGetMoonRotation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Rotation llGetRegionMoonRotation()
        {
            return m_LSL_Functions.llGetRegionMoonRotation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llChar(LSL_Integer unicode)
        {
            return m_LSL_Functions.llChar(unicode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llOrd(LSL_String s, LSL_Integer index)
        {
            return m_LSL_Functions.llOrd(s, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llHash(LSL_String s)
        {
            return m_LSL_Functions.llHash(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llReplaceSubString(LSL_String src, LSL_String pattern, LSL_String replacement, int count)
        {
            return m_LSL_Functions.llReplaceSubString(src, pattern, replacement, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkAdjustSoundVolume(LSL_Integer linknumber, LSL_Float volume)
        {
            m_LSL_Functions.llLinkAdjustSoundVolume(linknumber, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkStopSound(LSL_Integer linknumber)
        {
            m_LSL_Functions.llLinkStopSound(linknumber);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkPlaySound(LSL_Integer linknumber, string sound, double volume)
        {
            m_LSL_Functions.llLinkPlaySound(linknumber, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkPlaySound(LSL_Integer linknumber, string sound, double volume, LSL_Integer flags)
        {
            m_LSL_Functions.llLinkPlaySound(linknumber, sound, volume, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkSetSoundQueueing(int linknumber, int queue)
        {
            m_LSL_Functions.llLinkSetSoundQueueing(linknumber, queue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkSetSoundRadius(int linknumber, double radius)
        {
            m_LSL_Functions.llLinkSetSoundRadius(linknumber, radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llLinear2sRGB(LSL_Vector src)
        {
            return m_LSL_Functions.llLinear2sRGB(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Vector llsRGB2Linear(LSL_Vector src)
        {
            return m_LSL_Functions.llsRGB2Linear(src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataAvailable()
        {
            return m_LSL_Functions.llLinksetDataAvailable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataCountKeys()
        {
            return m_LSL_Functions.llLinksetDataCountKeys();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llLinksetDataRead(LSL_String name)
        {
            return m_LSL_Functions.llLinksetDataRead(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llLinksetDataReadProtected(LSL_String name, LSL_String pass)
        {
            return m_LSL_Functions.llLinksetDataReadProtected(name, pass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataDelete(LSL_String name)
        {
            return m_LSL_Functions.llLinksetDataDelete(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataDeleteProtected(LSL_String name, LSL_String pass)
        {
            return m_LSL_Functions.llLinksetDataDeleteProtected(name, pass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinksetDataReset()
        {
            m_LSL_Functions.llLinksetDataReset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataWrite(LSL_String name, LSL_String value)
        {
            return m_LSL_Functions.llLinksetDataWrite(name, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataWriteProtected(LSL_String name, LSL_String value, LSL_String pass)
        {
            return m_LSL_Functions.llLinksetDataWriteProtected(name, value, pass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llLinksetDataDeleteFound(LSL_String pattern, LSL_String pass)
        {
            return m_LSL_Functions.llLinksetDataDeleteFound(pattern, pass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llLinksetDataCountFound(LSL_String pattern)
        {
            return m_LSL_Functions.llLinksetDataCountFound(pattern);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llLinksetDataListKeys(LSL_Integer start, LSL_Integer count)
        {
            return m_LSL_Functions.llLinksetDataListKeys(start, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List llLinksetDataFindKeys(LSL_String pattern, LSL_Integer start, LSL_Integer count)
        {
            return m_LSL_Functions.llLinksetDataFindKeys(pattern, start, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llIsFriend(LSL_Key agent_id)
        {
            return m_LSL_Functions.llIsFriend(agent_id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llDerezObject(LSL_Key objectUUID, LSL_Integer flag)
        {
            return m_LSL_Functions.llDerezObject(objectUUID, flag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key llRezObjectWithParams(string inventory, LSL_List lparam)
        {
            return m_LSL_Functions.llRezObjectWithParams(inventory, lparam);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer llGetLinkSitFlags(LSL_Integer linknum)
        {
            return m_LSL_Functions.llGetLinkSitFlags(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llSetLinkSitFlags(LSL_Integer linknum, LSL_Integer flags)
        {
            m_LSL_Functions.llSetLinkSitFlags(linknum, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llHMAC(LSL_String private_key, LSL_String message, LSL_String algo)
        {
            return m_LSL_Functions.llHMAC(private_key, message, algo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String llComputeHash(LSL_String message, LSL_String algo)
        {
            return m_LSL_Functions.llComputeHash(message, algo);
        }

    }
}
