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
using System.Runtime.CompilerServices;

using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;

#pragma warning disable IDE1006

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass
    {
        public IOSSL_Api m_OSSL_Functions;

        public void ApiTypeOSSL(IScriptApi api)
        {
            if (api is not IOSSL_Api p)
                return;

            m_OSSL_Functions = p;
            //Prim = new OSSLPrim(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetRegionWaterHeight(double height)
        {
            m_OSSL_Functions.osSetRegionWaterHeight(height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetRegionSunSettings(bool useEstateSun, bool sunFixed, double sunHour)
        {
            m_OSSL_Functions.osSetRegionSunSettings(useEstateSun, sunFixed, sunHour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetEstateSunSettings(bool sunFixed, double sunHour)
        {
            m_OSSL_Functions.osSetEstateSunSettings(sunFixed, sunHour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetCurrentSunHour()
        {
            return m_OSSL_Functions.osGetCurrentSunHour();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetSunParam(LSL_String param)
        {
            return m_OSSL_Functions.osGetSunParam(param);
        }
        // Deprecated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double osSunGetParam(string param)
        {
            return m_OSSL_Functions.osSunGetParam(param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetSunParam(string param, double value)
        {
            m_OSSL_Functions.osSetSunParam(param, value);
        }

        // Deprecated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSunSetParam(string param, double value)
        {
            m_OSSL_Functions.osSunSetParam(param, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osWindActiveModelPluginName()
        {
            return m_OSSL_Functions.osWindActiveModelPluginName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetWindParam(string plugin, string param, LSL_Float value)
        {
            m_OSSL_Functions.osSetWindParam(plugin, param, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetWindParam(string plugin, string param)
        {
            return m_OSSL_Functions.osGetWindParam(plugin, param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetParcelDwell(vector pos)
        {
            return m_OSSL_Functions.osGetParcelDwell(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osParcelJoin(vector pos1, vector pos2)
        {
            m_OSSL_Functions.osParcelJoin(pos1,pos2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osParcelSubdivide(vector pos1, vector pos2)
        {
            m_OSSL_Functions.osParcelSubdivide(pos1, pos2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetParcelDetails(vector pos, LSL_List rules)
        {
            m_OSSL_Functions.osSetParcelDetails(pos, rules);
        }
        // Deprecated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osParcelSetDetails(vector pos, LSL_List rules)
        {
            m_OSSL_Functions.osParcelSetDetails(pos,rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            return m_OSSL_Functions.osSetDynamicTextureURL(dynamicID, contentType, url, extraParams, timer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                             int timer)
        {
            return m_OSSL_Functions.osSetDynamicTextureData(dynamicID, contentType, data, extraParams, timer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureDataFace(string dynamicID, string contentType, string data, string extraParams,
                                             int timer, int face)
        {
            return m_OSSL_Functions.osSetDynamicTextureDataFace(dynamicID, contentType, data, extraParams, timer, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                           int timer, int alpha)
        {
            return m_OSSL_Functions.osSetDynamicTextureURLBlend(dynamicID, contentType, url, extraParams, timer, alpha);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                             int timer, int alpha)
        {
            return m_OSSL_Functions.osSetDynamicTextureDataBlend(dynamicID, contentType, data, extraParams, timer, alpha);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureURLBlendFace(string dynamicID, string contentType, string url, string extraParams,
                                           bool blend, int disp, int timer, int alpha, int face)
        {
            return m_OSSL_Functions.osSetDynamicTextureURLBlendFace(dynamicID, contentType, url, extraParams,
                                             blend, disp, timer, alpha, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetDynamicTextureDataBlendFace(string dynamicID, string contentType, string data, string extraParams,
                                             bool blend, int disp, int timer, int alpha, int face)
        {
            return m_OSSL_Functions.osSetDynamicTextureDataBlendFace(dynamicID, contentType, data, extraParams,
                                             blend, disp, timer, alpha, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetTerrainHeight(int x, int y)
        {
            return m_OSSL_Functions.osGetTerrainHeight(x, y);
        }
        // Deprecated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osTerrainGetHeight(int x, int y)
        {
            return m_OSSL_Functions.osTerrainGetHeight(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osSetTerrainHeight(int x, int y, double val)
        {
            return m_OSSL_Functions.osSetTerrainHeight(x, y, val);
        }
        // Deprecated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osTerrainSetHeight(int x, int y, double val)
        {
            return m_OSSL_Functions.osTerrainSetHeight(x, y, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTerrainFlush()
        {
            m_OSSL_Functions.osTerrainFlush();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int osRegionRestart(double seconds)
        {
            return m_OSSL_Functions.osRegionRestart(seconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int osRegionRestart(double seconds, string msg)
        {
            return m_OSSL_Functions.osRegionRestart(seconds, msg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osRegionNotice(string msg)
        {
            m_OSSL_Functions.osRegionNotice(msg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osRegionNotice(LSL_Key agentID, string msg)
        {
            m_OSSL_Functions.osRegionNotice(agentID, msg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool osConsoleCommand(string Command)
        {
            return m_OSSL_Functions.osConsoleCommand(Command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetParcelMusicURL(LSL_String url)
        {
            m_OSSL_Functions.osSetParcelMusicURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetParcelMediaURL(LSL_String url)
        {
            m_OSSL_Functions.osSetParcelMediaURL(url);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetParcelSIPAddress(string SIPAddress)
        {
            m_OSSL_Functions.osSetParcelSIPAddress(SIPAddress);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetPrimFloatOnWater(int floatYN)
        {
            m_OSSL_Functions.osSetPrimFloatOnWater(floatYN);
        }

        // Teleport Functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osLocalTeleportAgent(LSL_Key agent, vector position, vector velocity, vector lookat, LSL_Integer flags)
        {
            m_OSSL_Functions.osLocalTeleportAgent(agent, position, velocity, lookat, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTeleportAgent(string agent, string regionName, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportAgent(agent, regionName, position, lookat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTeleportAgent(string agent, int regionX, int regionY, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportAgent(agent, regionX, regionY, position, lookat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTeleportAgent(string agent, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportAgent(agent, position, lookat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTeleportOwner(string regionName, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportOwner(regionName, position, lookat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTeleportOwner(int regionX, int regionY, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportOwner(regionX, regionY, position, lookat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTeleportOwner(vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportOwner(position, lookat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetAgents()
        {
            return m_OSSL_Functions.osGetAgents();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetAgentIP(string agent)
        {
            return m_OSSL_Functions.osGetAgentIP(agent);
        }

        // Animation Functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osAvatarPlayAnimation(LSL_Key avatar, string animation)
        {
            m_OSSL_Functions.osAvatarPlayAnimation(avatar, animation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osAvatarStopAnimation(LSL_Key avatar, string animation)
        {
            m_OSSL_Functions.osAvatarStopAnimation(avatar, animation);
        }

        #region Attachment commands

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceAttachToAvatar(int attachmentPoint)
        {
            m_OSSL_Functions.osForceAttachToAvatar(attachmentPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceAttachToAvatarFromInventory(string itemName, int attachmentPoint)
        {
            m_OSSL_Functions.osForceAttachToAvatarFromInventory(itemName, attachmentPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceAttachToOtherAvatarFromInventory(string rawAvatarId, string itemName, int attachmentPoint)
        {
            m_OSSL_Functions.osForceAttachToOtherAvatarFromInventory(rawAvatarId, itemName, attachmentPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceDetachFromAvatar()
        {
            m_OSSL_Functions.osForceDetachFromAvatar();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetNumberOfAttachments(LSL_Key avatar, LSL_List attachmentPoints)
        {
            return m_OSSL_Functions.osGetNumberOfAttachments(avatar, attachmentPoints);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osMessageAttachments(LSL_Key avatar, string message, LSL_List attachmentPoints, int flags)
        {
            m_OSSL_Functions.osMessageAttachments(avatar, message, attachmentPoints, flags);
        }

        #endregion

        // Texture Draw functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osMovePen(string drawList, int x, int y)
        {
            return m_OSSL_Functions.osMovePen(drawList, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            return m_OSSL_Functions.osDrawLine(drawList, startX, startY, endX, endY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawLine(string drawList, int endX, int endY)
        {
            return m_OSSL_Functions.osDrawLine(drawList, endX, endY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawText(string drawList, string text)
        {
            return m_OSSL_Functions.osDrawText(drawList, text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawEllipse(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawEllipse(drawList, width, height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawFilledEllipse(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawFilledEllipse(drawList, width, height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawRectangle(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawRectangle(drawList, width, height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawFilledRectangle(drawList, width, height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawPolygon(string drawList, LSL_List x, LSL_List y)
        {
            return m_OSSL_Functions.osDrawPolygon(drawList, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawFilledPolygon(string drawList, LSL_List x, LSL_List y)
        {
            return m_OSSL_Functions.osDrawFilledPolygon(drawList, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawResetTransform(string drawList)
        {
            return m_OSSL_Functions.osDrawResetTransform(drawList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawRotationTransform(string drawList, LSL_Float x)
        {
            return m_OSSL_Functions.osDrawRotationTransform(drawList, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawScaleTransform(string drawList, LSL_Float x, LSL_Float y)
        {
            return m_OSSL_Functions.osDrawScaleTransform(drawList, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawTranslationTransform(string drawList, LSL_Float x, LSL_Float y)
        {
            return m_OSSL_Functions.osDrawTranslationTransform(drawList, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetFontSize(string drawList, int fontSize)
        {
            return m_OSSL_Functions.osSetFontSize(drawList, fontSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetFontName(string drawList, string fontName)
        {
            return m_OSSL_Functions.osSetFontName(drawList, fontName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetPenSize(string drawList, int penSize)
        {
            return m_OSSL_Functions.osSetPenSize(drawList, penSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetPenCap(string drawList, string direction, string type)
        {
            return m_OSSL_Functions.osSetPenCap(drawList, direction, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetPenColor(string drawList, string color)
        {
            return m_OSSL_Functions.osSetPenColor(drawList, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetPenColor(string drawList, vector color)
        {
            return m_OSSL_Functions.osSetPenColor(drawList, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetPenColor(string drawList, vector color, float alpha)
        {
            return m_OSSL_Functions.osSetPenColor(drawList, color, alpha);
        }

        // Deprecated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSetPenColour(string drawList, string colour)
        {
            return m_OSSL_Functions.osSetPenColour(drawList, colour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            return m_OSSL_Functions.osDrawImage(drawList, width, height, imageUrl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osGetDrawStringSize(string contentType, string text, string fontName, int fontSize)
        {
            return m_OSSL_Functions.osGetDrawStringSize(contentType, text, fontName, fontSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetScriptEngineName()
        {
            return m_OSSL_Functions.osGetScriptEngineName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osCheckODE()
        {
            return m_OSSL_Functions.osCheckODE();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetPhysicsEngineType()
        {
            return m_OSSL_Functions.osGetPhysicsEngineType();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetPhysicsEngineName()
        {
            return m_OSSL_Functions.osGetPhysicsEngineName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetSimulatorVersion()
        {
           return m_OSSL_Functions.osGetSimulatorVersion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osMessageObject(key objectUUID,string message)
        {
            m_OSSL_Functions.osMessageObject(objectUUID,message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osMakeNotecard(string notecardName, LSL_String contents)
        {
            m_OSSL_Functions.osMakeNotecard(notecardName, contents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osMakeNotecard(string notecardName, LSL_List contents)
        {
            m_OSSL_Functions.osMakeNotecard(notecardName, contents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetNotecardLine(string name, int line)
        {
            return m_OSSL_Functions.osGetNotecardLine(name, line);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetNotecard(string name)
        {
            return m_OSSL_Functions.osGetNotecard(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int osGetNumberOfNotecardLines(string name)
        {
            return m_OSSL_Functions.osGetNumberOfNotecardLines(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osAvatarName2Key(string firstname, string lastname)
        {
            return m_OSSL_Functions.osAvatarName2Key(firstname, lastname);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osKey2Name(string id)
        {
            return m_OSSL_Functions.osKey2Name(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osSHA256(string input)
        {
            return m_OSSL_Functions.osSHA256(input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGridNick()
        {
            return m_OSSL_Functions.osGetGridNick();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGridName()
        {
            return m_OSSL_Functions.osGetGridName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGridLoginURI()
        {
            return m_OSSL_Functions.osGetGridLoginURI();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGridHomeURI()
        {
            return m_OSSL_Functions.osGetGridHomeURI();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGridGatekeeperURI()
        {
            return m_OSSL_Functions.osGetGridGatekeeperURI();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGridCustom(string key)
        {
            return m_OSSL_Functions.osGetGridCustom(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetAvatarHomeURI(string uuid)
        {
            return m_OSSL_Functions.osGetAvatarHomeURI(uuid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osFormatString(string str, LSL_List strings)
        {
            return m_OSSL_Functions.osFormatString(str, strings);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osMatchString(string src, string pattern, int start)
        {
            return m_OSSL_Functions.osMatchString(src, pattern, start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osReplaceString(string src, string pattern, string replace, int count, int start)
        {
            return m_OSSL_Functions.osReplaceString(src,pattern,replace,count,start);
        }

        // Information about data loaded into the region
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osLoadedCreationDate()
        {
            return m_OSSL_Functions.osLoadedCreationDate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osLoadedCreationTime()
        {
            return m_OSSL_Functions.osLoadedCreationTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osLoadedCreationID()
        {
            return m_OSSL_Functions.osLoadedCreationID();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            return m_OSSL_Functions.osGetLinkPrimitiveParams(linknumber, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceCreateLink(string target, int parent)
        {
            m_OSSL_Functions.osForceCreateLink(target, parent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceBreakLink(int linknum)
        {
            m_OSSL_Functions.osForceBreakLink(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceBreakAllLinks()
        {
            m_OSSL_Functions.osForceBreakAllLinks();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osDie(LSL_Key objectUUID)
        {
            m_OSSL_Functions.osDie(objectUUID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osIsNpc(LSL_Key npc)
        {
            return m_OSSL_Functions.osIsNpc(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public key osNpcCreate(string user, string name, vector position, key cloneFrom)
        {
            return m_OSSL_Functions.osNpcCreate(user, name, position, cloneFrom);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public key osNpcCreate(string user, string name, vector position, key cloneFrom, int options)
        {
            return m_OSSL_Functions.osNpcCreate(user, name, position, cloneFrom, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public key osNpcSaveAppearance(key npc, LSL_String notecard)
        {
            return m_OSSL_Functions.osNpcSaveAppearance(npc, notecard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public key osNpcSaveAppearance(key npc, LSL_String notecard, LSL_Integer includeHuds)
        {
            return m_OSSL_Functions.osNpcSaveAppearance(npc, notecard, includeHuds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcLoadAppearance(key npc, string notecard)
        {
            m_OSSL_Functions.osNpcLoadAppearance(npc, notecard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osNpcGetOwner(LSL_Key npc)
        {
            return m_OSSL_Functions.osNpcGetOwner(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osNpcGetPos(LSL_Key npc)
        {
            return m_OSSL_Functions.osNpcGetPos(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcMoveTo(key npc, vector position)
        {
            m_OSSL_Functions.osNpcMoveTo(npc, position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcMoveToTarget(key npc, vector target, int options)
        {
            m_OSSL_Functions.osNpcMoveToTarget(npc, target, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public rotation osNpcGetRot(key npc)
        {
            return m_OSSL_Functions.osNpcGetRot(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSetRot(key npc, rotation rot)
        {
            m_OSSL_Functions.osNpcSetRot(npc, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcStopMoveToTarget(LSL_Key npc)
        {
            m_OSSL_Functions.osNpcStopMoveToTarget(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSetProfileAbout(LSL_Key npc, string about)
        {
            m_OSSL_Functions.osNpcSetProfileAbout(npc, about);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSetProfileImage(LSL_Key npc, string image)
        {
            m_OSSL_Functions.osNpcSetProfileImage(npc, image);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSay(key npc, string message)
        {
            m_OSSL_Functions.osNpcSay(npc, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSay(key npc, int channel, string message)
        {
            m_OSSL_Functions.osNpcSay(npc, channel, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSayTo(LSL_Key npc, LSL_Key target, int channel, string msg)
        {
            m_OSSL_Functions.osNpcSayTo(npc, target, channel, msg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcShout(key npc, int channel, string message)
        {
            m_OSSL_Functions.osNpcShout(npc, channel, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcSit(LSL_Key npc, LSL_Key target, int options)
        {
            m_OSSL_Functions.osNpcSit(npc, target, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcStand(LSL_Key npc)
        {
            m_OSSL_Functions.osNpcStand(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcRemove(key npc)
        {
            m_OSSL_Functions.osNpcRemove(npc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcPlayAnimation(LSL_Key npc, string animation)
        {
            m_OSSL_Functions.osNpcPlayAnimation(npc, animation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcStopAnimation(LSL_Key npc, string animation)
        {
            m_OSSL_Functions.osNpcStopAnimation(npc, animation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcWhisper(key npc, int channel, string message)
        {
            m_OSSL_Functions.osNpcWhisper(npc, channel, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osNpcTouch(LSL_Key npcLSL_Key, LSL_Key object_key, LSL_Integer link_num)
        {
            m_OSSL_Functions.osNpcTouch(npcLSL_Key, object_key, link_num);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osOwnerSaveAppearance(LSL_String notecard)
        {
            return m_OSSL_Functions.osOwnerSaveAppearance(notecard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osOwnerSaveAppearance(LSL_String notecard, LSL_Integer includeHuds)
        {
            return m_OSSL_Functions.osOwnerSaveAppearance(notecard, includeHuds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osAgentSaveAppearance(LSL_Key agentId, LSL_String notecard)
        {
            return m_OSSL_Functions.osAgentSaveAppearance(agentId, notecard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osAgentSaveAppearance(LSL_Key agentId, LSL_String notecard, LSL_Integer includeHuds)
        {
            return m_OSSL_Functions.osAgentSaveAppearance(agentId, notecard, includeHuds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string osGetGender(LSL_Key rawAvatarId)
        {
            return m_OSSL_Functions.osGetGender(rawAvatarId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public key osGetMapTexture()
        {
            return m_OSSL_Functions.osGetMapTexture();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public key osGetRegionMapTexture(string regionNameOrID)
        {
            return m_OSSL_Functions.osGetRegionMapTexture(regionNameOrID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetRegionStats()
        {
            return m_OSSL_Functions.osGetRegionStats();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osGetRegionSize()
        {
            return m_OSSL_Functions.osGetRegionSize();
        }

        /// <summary>
        /// Returns the amount of memory in use by the Simulator Daemon.
        /// Amount in bytes - if >= 2GB, returns 2GB. (LSL is not 64-bit aware)
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetSimulatorMemory()
        {
            return m_OSSL_Functions.osGetSimulatorMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetSimulatorMemoryKB()
        {
            return m_OSSL_Functions.osGetSimulatorMemoryKB();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osKickAvatar(string FirstName, string SurName, string alert)
        {
            m_OSSL_Functions.osKickAvatar(FirstName, SurName, alert);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osKickAvatar(LSL_Key agentId, string alert)
        {
            m_OSSL_Functions.osKickAvatar(agentId, alert);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetSpeed(string UUID, LSL_Float SpeedModifier)
        {
            m_OSSL_Functions.osSetSpeed(UUID, SpeedModifier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetOwnerSpeed(LSL_Float SpeedModifier)
        {
            m_OSSL_Functions.osSetOwnerSpeed(SpeedModifier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetHealth(key avatar)
        {
            return m_OSSL_Functions.osGetHealth(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osCauseDamage(key avatar, LSL_Float damage)
        {
            m_OSSL_Functions.osCauseDamage(avatar, damage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osCauseHealing(key avatar, LSL_Float healing)
        {
            m_OSSL_Functions.osCauseHealing(avatar, healing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetHealth(key avatar, LSL_Float health)
        {
            m_OSSL_Functions.osSetHealth(avatar, health);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetHealRate(key avatar, LSL_Float health)
        {
            m_OSSL_Functions.osSetHealRate(avatar, health);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetHealRate(key avatar)
        {
            return m_OSSL_Functions.osGetHealRate(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceOtherSit(string avatar)
        {
            m_OSSL_Functions.osForceOtherSit(avatar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceOtherSit(string avatar, string target)
        {
            m_OSSL_Functions.osForceOtherSit(avatar, target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetPrimitiveParams(LSL_Key prim, LSL_List rules)
        {
            return m_OSSL_Functions.osGetPrimitiveParams(prim, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetPrimitiveParams(LSL_Key prim, LSL_List rules)
        {
            m_OSSL_Functions.osSetPrimitiveParams(prim, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetProjectionParams(LSL_Integer projection, LSL_Key texture, double fov, double focus, double amb)
        {
            m_OSSL_Functions.osSetProjectionParams(projection, texture, fov, focus, amb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetProjectionParams(LSL_Key prim, LSL_Integer projection, LSL_Key texture, double fov, double focus, double amb)
        {
            m_OSSL_Functions.osSetProjectionParams(prim, projection, texture, fov, focus, amb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetProjectionParams(LSL_Integer linknumber, LSL_Integer projection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb)
        {
            m_OSSL_Functions.osSetProjectionParams(linknumber, projection, texture, fov, focus, amb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetAvatarList()
        {
            return m_OSSL_Functions.osGetAvatarList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetNPCList()
        {
            return m_OSSL_Functions.osGetNPCList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osUnixTimeToTimestamp(LSL_Integer time)
        {
            return m_OSSL_Functions.osUnixTimeToTimestamp(time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osInviteToGroup(LSL_Key agentId)
        {
            return m_OSSL_Functions.osInviteToGroup(agentId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osEjectFromGroup(LSL_Key agentId)
        {
            return m_OSSL_Functions.osEjectFromGroup(agentId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetTerrainTexture(int level, LSL_Key texture)
        {
            m_OSSL_Functions.osSetTerrainTexture(level, texture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetTerrainTextures(LSL_List textures, LSL_Integer types)
        {
            m_OSSL_Functions.osSetTerrainTextures(textures, types);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetTerrainTextureHeight(int corner, double low, double high)
        {
            m_OSSL_Functions.osSetTerrainTextureHeight(corner, low, high);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osIsUUID(string thing)
        {
            return m_OSSL_Functions.osIsUUID(thing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osMin(double a, double b)
        {
            return m_OSSL_Functions.osMin(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osMax(double a, double b)
        {
            return m_OSSL_Functions.osMax(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetRezzingObject()
        {
            return m_OSSL_Functions.osGetRezzingObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetContentType(LSL_Key id, string type)
        {
            m_OSSL_Functions.osSetContentType(id,type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osDropAttachment()
        {
            m_OSSL_Functions.osDropAttachment();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceDropAttachment()
        {
            m_OSSL_Functions.osForceDropAttachment();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osDropAttachmentAt(vector pos, rotation rot)
        {
            m_OSSL_Functions.osDropAttachmentAt(pos, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osForceDropAttachmentAt(vector pos, rotation rot)
        {
            m_OSSL_Functions.osForceDropAttachmentAt(pos, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osListenRegex(int channelID, string name, string ID, string msg, int regexBitfield)
        {
            return m_OSSL_Functions.osListenRegex(channelID, name, ID, msg, regexBitfield);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osRegexIsMatch(string input, string pattern)
        {
            return m_OSSL_Functions.osRegexIsMatch(input, pattern);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osRequestURL(LSL_List options)
        {
            return m_OSSL_Functions.osRequestURL(options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osRequestSecureURL(LSL_List options)
        {
            return m_OSSL_Functions.osRequestSecureURL(options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osCollisionSound(string impact_sound, double impact_volume)
        {
            m_OSSL_Functions.osCollisionSound(impact_sound, impact_volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osVolumeDetect(int detect)
        {
            m_OSSL_Functions.osVolumeDetect(detect);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetInertiaData()
        {
            return m_OSSL_Functions.osGetInertiaData();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetInertia(LSL_Float mass, vector centerOfMass, vector principalInertiaScaled,  rotation rot)
        {
            m_OSSL_Functions.osSetInertia(mass, centerOfMass, principalInertiaScaled, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetInertiaAsBox(LSL_Float mass, vector boxSize, vector centerOfMass, rotation rot)
        {
            m_OSSL_Functions.osSetInertiaAsBox(mass, boxSize, centerOfMass, rot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetInertiaAsSphere(LSL_Float mass,  LSL_Float radius, vector centerOfMass)
        {
            m_OSSL_Functions.osSetInertiaAsSphere(mass, radius, centerOfMass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetInertiaAsCylinder(LSL_Float mass,  LSL_Float radius, LSL_Float length, vector centerOfMass,rotation lslrot)
        {
            m_OSSL_Functions.osSetInertiaAsCylinder( mass, radius, length, centerOfMass, lslrot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osClearInertia()
        {
            m_OSSL_Functions.osClearInertia();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osTeleportObject(LSL_Key objectUUID, vector targetPos, rotation targetrotation, LSL_Integer flags)
        {
            return m_OSSL_Functions.osTeleportObject(objectUUID, targetPos, targetrotation, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetLinkNumber(LSL_String name)
        {
            return m_OSSL_Functions.osGetLinkNumber(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osRound(LSL_Float value, LSL_Integer digits)
        {
            return m_OSSL_Functions.osRound(value, digits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osVecMagSquare(vector a)
        {
            return m_OSSL_Functions.osVecMagSquare(a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osVecDistSquare(vector a, vector b)
        {
            return m_OSSL_Functions.osVecDistSquare(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osAngleBetween(vector a, vector b)
        {
            return m_OSSL_Functions.osAngleBetween(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osAdjustSoundVolume(LSL_Integer linknum, LSL_Float volume)
        {
            m_OSSL_Functions.osAdjustSoundVolume(linknum, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetSoundRadius(LSL_Integer linknum, LSL_Float radius)
        {
            m_OSSL_Functions.osSetSoundRadius(linknum, radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osPlaySound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_OSSL_Functions.osPlaySound(linknum, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osLoopSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_OSSL_Functions.osLoopSound(linknum, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osLoopSoundMaster(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_OSSL_Functions.osLoopSoundMaster(linknum, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osLoopSoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_OSSL_Functions.osLoopSoundSlave(linknum, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osPlaySoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_OSSL_Functions.osPlaySoundSlave(linknum, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTriggerSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_OSSL_Functions.osTriggerSound(linknum, sound, volume);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osTriggerSoundLimited(LSL_Integer linknum, LSL_String sound, LSL_Float volume,
                 vector top_north_east, vector bottom_south_west)
        {
            m_OSSL_Functions.osTriggerSoundLimited(linknum, sound, volume,
                                            top_north_east, bottom_south_west);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osStopSound(LSL_Integer linknum)
        {
            m_OSSL_Functions.osStopSound(linknum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osPreloadSound(LSL_Integer linknum, LSL_String sound)
        {
            m_OSSL_Functions.osPreloadSound(linknum, sound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osDetectedCountry(LSL_Integer number)
        {
            return m_OSSL_Functions.osDetectedCountry(number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetAgentCountry(LSL_Key agentId)
        {
            return m_OSSL_Functions.osGetAgentCountry(agentId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osStringSubString(LSL_String src, LSL_Integer offset)
        {
            return m_OSSL_Functions.osStringSubString(src, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osStringSubString(LSL_String src, LSL_Integer offset, LSL_Integer length)
        {
            return m_OSSL_Functions.osStringSubString(src, offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osStringStartsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            return m_OSSL_Functions.osStringStartsWith(src, value, ignorecase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osStringEndsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            return m_OSSL_Functions.osStringEndsWith(src, value, ignorecase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osStringIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            return m_OSSL_Functions.osStringIndexOf(src, value, ignorecase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osStringIndexOf(LSL_String src, LSL_String value, LSL_Integer offset, LSL_Integer count, LSL_Integer ignorecase)
        {
            return m_OSSL_Functions.osStringIndexOf(src, value, offset, count, ignorecase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            return m_OSSL_Functions.osStringLastIndexOf(src, value, ignorecase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value, LSL_Integer offset, LSL_Integer count, LSL_Integer ignorecase)
        {
            return m_OSSL_Functions.osStringLastIndexOf(src, value, offset, count, ignorecase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osStringRemove(LSL_String src, LSL_Integer offset, LSL_Integer count)
        {
            return m_OSSL_Functions.osStringRemove(src, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osStringReplace(LSL_String src, LSL_String oldvalue, LSL_String newvalue)
        {
            return m_OSSL_Functions.osStringReplace(src, oldvalue, newvalue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osApproxEquals(LSL_Float a, LSL_Float b)
        {
            return m_OSSL_Functions.osApproxEquals(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osApproxEquals(LSL_Float a, LSL_Float b, LSL_Float margin)
        {
            return m_OSSL_Functions.osApproxEquals(a, b, margin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osApproxEquals(vector va, vector vb)
        {
            return m_OSSL_Functions.osApproxEquals(va, vb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osApproxEquals(vector va, vector vb, LSL_Float margin)
        {
            return m_OSSL_Functions.osApproxEquals(va, vb, margin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osApproxEquals(rotation ra, rotation rb)
        {
            return m_OSSL_Functions.osApproxEquals(ra, rb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osApproxEquals(rotation ra, rotation rb, LSL_Float margin)
        {
            return m_OSSL_Functions.osApproxEquals(ra, rb, margin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetInventoryLastOwner(LSL_String itemNameOrId)
        {
            return m_OSSL_Functions.osGetInventoryLastOwner(itemNameOrId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetInventoryItemKey(LSL_String name)
        {
            return m_OSSL_Functions.osGetInventoryItemKey(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetLinkInventoryKey(LSL_Integer linkNumber, LSL_String name, LSL_Integer type)
        {
            return m_OSSL_Functions.osGetLinkInventoryKey(linkNumber, name, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetLinkInventoryKeys(LSL_Integer linkNumber, LSL_Integer type)
        {
            return m_OSSL_Functions.osGetLinkInventoryKeys(linkNumber, type);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetLinkInventoryItemKey(LSL_Integer linkNumber, LSL_String name)
        {
            return m_OSSL_Functions.osGetLinkInventoryItemKey(linkNumber, name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetInventoryItemKeys(LSL_Integer type)
        {
            return m_OSSL_Functions.osGetInventoryItemKeys(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetLinkInventoryItemKeys(LSL_Integer linkNumber, LSL_Integer type)
        {
           return m_OSSL_Functions.osGetLinkInventoryItemKeys(linkNumber, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetInventoryName(LSL_Key itemId)
        {
            return m_OSSL_Functions.osGetInventoryName(itemId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetLinkInventoryName(LSL_Integer linkNumber, LSL_Key itemId)
        {
            return m_OSSL_Functions.osGetLinkInventoryName(linkNumber, itemId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetInventoryNames(LSL_Integer type)
        {
           return m_OSSL_Functions.osGetInventoryNames(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetLinkInventoryNames(LSL_Integer linkNumber, LSL_Integer type)
        {
           return m_OSSL_Functions.osGetLinkInventoryNames(linkNumber, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetInventoryDesc(LSL_String itemNameOrId)
        {
            return m_OSSL_Functions.osGetInventoryDesc(itemNameOrId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetLinkInventoryDesc(LSL_Integer linkNumber, LSL_String itemNameOrId)
        {
            return m_OSSL_Functions.osGetLinkInventoryDesc(linkNumber, itemNameOrId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osRemoveLinkInventory(LSL_Integer linkNumber, LSL_String name)
        {
            m_OSSL_Functions.osRemoveLinkInventory(linkNumber, name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osGiveLinkInventory(LSL_Integer linkNumber, LSL_Key destination, LSL_String inventory)
        {
            m_OSSL_Functions.osGiveLinkInventory(linkNumber, destination, inventory);
        }

        public void osGiveLinkInventoryList(LSL_Integer linkNumber, LSL_Key destination, LSL_String category, LSL_List inventory)
        {
            m_OSSL_Functions.osGiveLinkInventoryList(linkNumber, destination, category, inventory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetLastChangedEventKey()
        {
            return m_OSSL_Functions.osGetLastChangedEventKey();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetPSTWallclock()
        {
            return m_OSSL_Functions.osGetPSTWallclock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public rotation osSlerp(rotation a, rotation b, LSL_Float amount)
        {
            return m_OSSL_Functions.osSlerp(a, b, amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osSlerp(vector a, vector b, LSL_Float amount)
        {
            return m_OSSL_Functions.osSlerp(a, b, amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osResetAllScripts(LSL_Integer allLinkSet)
        {
            m_OSSL_Functions.osResetAllScripts(allLinkSet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osIsNotValidNumber(LSL_Float v)
        {
            return m_OSSL_Functions.osIsNotValidNumber(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetSitActiveRange(LSL_Float v)
        {
            m_OSSL_Functions.osSetSitActiveRange(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetLinkSitActiveRange(LSL_Integer linkNumber, LSL_Float v)
        {
            m_OSSL_Functions.osSetLinkSitActiveRange(linkNumber, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetSitActiveRange()
        {
            return m_OSSL_Functions.osGetSitActiveRange();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetLinkSitActiveRange(LSL_Integer linkNumber)
        {
            return m_OSSL_Functions.osGetLinkSitActiveRange(linkNumber);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetStandTarget(vector v)
        {
            m_OSSL_Functions.osSetStandTarget(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osSetLinkStandTarget(LSL_Integer linkNumber, vector v)
        {
            m_OSSL_Functions.osSetLinkStandTarget(linkNumber, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osGetSitTargetPos()
        {
            return m_OSSL_Functions.osGetSitTargetPos();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public rotation osGetSitTargetRot()
        {
            return m_OSSL_Functions.osGetSitTargetRot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osGetStandTarget()
        {
            return m_OSSL_Functions.osGetStandTarget();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osGetLinkStandTarget(LSL_Integer linkNumber)
        {
            return m_OSSL_Functions.osGetLinkStandTarget(linkNumber);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osClearObjectAnimations()
        {
            return m_OSSL_Functions.osClearObjectAnimations();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetApparentTime()
        {
            return m_OSSL_Functions.osGetApparentTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetApparentTimeString(LSL_Integer format24)
        {
            return m_OSSL_Functions.osGetApparentTimeString(format24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osGetApparentRegionTime()
        {
            return m_OSSL_Functions.osGetApparentRegionTime();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osGetApparentRegionTimeString(LSL_Integer format24)
        {
            return m_OSSL_Functions.osGetApparentRegionTimeString(format24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osReplaceAgentEnvironment(LSL_Key agentkey, LSL_Integer transition, LSL_String daycycle)
        {
            return m_OSSL_Functions.osReplaceAgentEnvironment(agentkey, transition, daycycle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osReplaceParcelEnvironment(LSL_Integer transition, LSL_String daycycle)
        {
            return m_OSSL_Functions.osReplaceParcelEnvironment(transition, daycycle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osReplaceRegionEnvironment(LSL_Integer transition, LSL_String daycycle,
           LSL_Float daylen, LSL_Float dayoffset, LSL_Float altitude1, LSL_Float altitude2, LSL_Float altitude3)
        {
            return m_OSSL_Functions.osReplaceRegionEnvironment(transition, daycycle, daylen,
                        dayoffset, altitude1, altitude2, altitude3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osResetEnvironment(LSL_Integer parcelOrRegion, LSL_Integer transition)
        {
            return m_OSSL_Functions.osResetEnvironment(parcelOrRegion, transition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osParticleSystem(LSL_List rules)
        {
            m_OSSL_Functions.osParticleSystem(rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osLinkParticleSystem(LSL_Integer linknumber, LSL_List rules)
        {
            m_OSSL_Functions.osLinkParticleSystem(linknumber, rules);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osNpcLookAt(LSL_Key npckey, LSL_Integer type, LSL_Key objkey, vector offset)
        {
            return m_OSSL_Functions.osNpcLookAt(npckey, type, objkey, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osAvatarType(LSL_Key avkey)
        {
            return m_OSSL_Functions.osAvatarType(avkey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osAvatarType(LSL_String sFirstName, LSL_String sLastName)
        {
            return m_OSSL_Functions.osAvatarType(sFirstName, sLastName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osListSortInPlace(LSL_List src, LSL_Integer stride, LSL_Integer ascending)
        {
            m_OSSL_Functions.osListSortInPlace(src, stride, ascending);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void osListSortInPlaceStrided(LSL_List src, LSL_Integer stride, LSL_Integer stride_index, LSL_Integer ascending)
        {
            m_OSSL_Functions.osListSortInPlaceStrided(src, stride, stride_index, ascending);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetParcelDetails(LSL_Key id, LSL_List param)
        {
            return m_OSSL_Functions.osGetParcelDetails(id, param);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osGetParcelIDs()
        {
            return m_OSSL_Functions.osGetParcelIDs();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Key osGetParcelID()
        {
            return m_OSSL_Functions.osGetParcelID();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_List osOldList2ListStrided(LSL_List src, int start, int end, int stride)
        {
            return m_OSSL_Functions.osOldList2ListStrided(src, start, end, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetPrimCount()
        {
            return m_OSSL_Functions.osGetPrimCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetPrimCount(LSL_Key object_id)
        {
            return m_OSSL_Functions.osGetPrimCount(object_id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetSittingAvatarsCount()
        {
            return m_OSSL_Functions.osGetSittingAvatarsCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osGetSittingAvatarsCount(LSL_Key object_id)
        {
            return m_OSSL_Functions.osGetSittingAvatarsCount(object_id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osAESEncrypt(string secret, string plainText)
        {
            return m_OSSL_Functions.osAESEncrypt(secret, plainText);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osAESEncryptTo(string secret, string plainText, string ivString)
        {
            return m_OSSL_Functions.osAESEncryptTo(secret, plainText, ivString);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osAESDecrypt(string secret, string encryptedText)
        {
            return m_OSSL_Functions.osAESDecrypt(secret, encryptedText);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osAESDecryptFrom(string secret, string encryptedText, string ivString)
        {
            return m_OSSL_Functions.osAESDecryptFrom(secret, encryptedText, ivString);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osGetLinkColor(LSL_Integer link, LSL_Integer face)
        {
            return m_OSSL_Functions.osGetLinkColor(link, face);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osTemperature2sRBG(LSL_Float dtemp)
        {
            return m_OSSL_Functions.osTemperature2sRGB(dtemp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osListFindListNext(LSL_List src, LSL_List test, LSL_Integer start, LSL_Integer end, LSL_Integer instance)
        {
            return m_OSSL_Functions.osListFindListNext(src, test, start, end, instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_String osListAsString(LSL_List src, int index)
        {
            return m_OSSL_Functions.osListAsString(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Integer osListAsInteger(LSL_List src, int index)
        {
            return m_OSSL_Functions.osListAsInteger(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LSL_Float osListAsFloat(LSL_List src, int index)
        {
            return m_OSSL_Functions.osListAsFloat(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public vector osListAsVector(LSL_List src, int index)
        {
            return m_OSSL_Functions.osListAsVector(src, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public rotation osListAsRotation(LSL_List src, int index)
        {
            return m_OSSL_Functions.osListAsRotation(src, index);
        }
    }
}
