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
using System.Collections;
using OpenSim.Region.ScriptEngine.Interfaces;

using key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Interfaces
{
    /// <summary>
    /// To permit region owners to enable the extended scripting functionality
    /// of OSSL, without allowing malicious scripts to access potentially
    /// troublesome functions, each OSSL function is assigned a threat level,
    /// and access to the functions is granted or denied based on a default
    /// threshold set in OpenSim.ini (which can be overridden for individual
    /// functions on a case-by-case basis)
    /// </summary>
    public enum ThreatLevel
    {
        // Not documented, presumably means permanently disabled ?
        NoAccess = -1,

        /// <summary>
        /// Function is no threat at all. It doesn't constitute a threat to 
        /// either users or the system and has no known side effects.
        /// </summary>
        None = 0,

        /// <summary>
        /// Abuse of this command can cause a nuisance to the region operator,
        /// such as log message spew.
        /// </summary>
        Nuisance = 1,

        /// <summary>
        /// Extreme levels of abuse of this function can cause impaired 
        /// functioning of the region, or very gullible users can be tricked
        /// into experiencing harmless effects.
        /// </summary>
        VeryLow = 2,

        /// <summary>
        /// Intentional abuse can cause crashes or malfunction under certain
        /// circumstances, which can be easily rectified; or certain users can
        /// be tricked into certain situations in an avoidable manner.
        /// </summary>
        Low = 3,

        /// <summary>
        /// Intentional abuse can cause denial of service and crashes with
        /// potential of data or state loss; or trusting users can be tricked 
        /// into embarrassing or uncomfortable situations.
        /// </summary>
        Moderate = 4,

        /// <summary>
        /// Casual abuse can cause impaired functionality or temporary denial
        /// of service conditions. Intentional abuse can easily cause crashes
        /// with potential data loss, or can be used to trick experienced and
        /// cautious users into unwanted situations, or changes global data
        /// permanently and without undo ability.
        /// </summary>
        High = 5,

        /// <summary>
        /// Even normal use may, depending on the number of instances, or
        /// frequency of use, result in severe service impairment or crash
        /// with loss of data, or can be used to cause unwanted or harmful
        /// effects on users without giving the user a means to avoid it.
        /// </summary>
        VeryHigh = 6,

        /// <summary>
        /// Even casual use is a danger to region stability, or function allows
        /// console or OS command execution, or function allows taking money
        /// without consent, or allows deletion or modification of user data,
        /// or allows the compromise of sensitive data by design.
        /// </summary>
        Severe = 7
    };

    public interface IOSSL_Api
    {
        void CheckThreatLevel(ThreatLevel level, string function);

        //OpenSim functions
        string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams, int timer);
        string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                           int timer, int alpha);
        string osSetDynamicTextureURLBlendFace(string dynamicID, string contentType, string url, string extraParams,
                                           bool blend, int disp, int timer, int alpha, int face);
        string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams, int timer);
        string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                            int timer, int alpha);
        string osSetDynamicTextureDataBlendFace(string dynamicID, string contentType, string data, string extraParams,
                                            bool blend, int disp, int timer, int alpha, int face);

        LSL_Float osGetTerrainHeight(int x, int y);
        LSL_Float osTerrainGetHeight(int x, int y); // Deprecated
        LSL_Integer osSetTerrainHeight(int x, int y, double val);
        LSL_Integer osTerrainSetHeight(int x, int y, double val); //Deprecated
        void osTerrainFlush();

        int osRegionRestart(double seconds);
        void osRegionNotice(string msg);
        bool osConsoleCommand(string Command);
        void osSetParcelMediaURL(string url);
        void osSetPrimFloatOnWater(int floatYN);
        void osSetParcelSIPAddress(string SIPAddress);

        // Avatar Info Commands
        string osGetAgentIP(string agent);
        LSL_List osGetAgents();

        // Teleport commands
        void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        void osTeleportAgent(string agent, int regionX, int regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        void osTeleportOwner(string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        void osTeleportOwner(int regionX, int regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        void osTeleportOwner(LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);

        // Animation commands
        void osAvatarPlayAnimation(string avatar, string animation);
        void osAvatarStopAnimation(string avatar, string animation);

        #region Attachment commands

        /// <summary>
        /// Attach the object containing this script to the avatar that owns it without asking for PERMISSION_ATTACH
        /// </summary>
        /// <param name='attachment'>The attachment point.  For example, ATTACH_CHEST</param>
        void osForceAttachToAvatar(int attachment);

        /// <summary>
        /// Attach an inventory item in the object containing this script to the avatar that owns it without asking for PERMISSION_ATTACH
        /// </summary>
        /// <remarks>
        /// Nothing happens if the owner is not in the region.
        /// </remarks>
        /// <param name='itemName'>Tha name of the item.  If this is not found then a warning is said to the owner</param>
        /// <param name='attachment'>The attachment point.  For example, ATTACH_CHEST</param>
        void osForceAttachToAvatarFromInventory(string itemName, int attachment);

        /// <summary>
        /// Attach an inventory item in the object containing this script to any avatar in the region without asking for PERMISSION_ATTACH
        /// </summary>
        /// <remarks>
        /// Nothing happens if the avatar is not in the region.
        /// </remarks>
        /// <param name='rawAvatarId'>The UUID of the avatar to which to attach.  Nothing happens if this is not a UUID</para>
        /// <param name='itemName'>The name of the item.  If this is not found then a warning is said to the owner</param>
        /// <param name='attachment'>The attachment point.  For example, ATTACH_CHEST</param>
        void osForceAttachToOtherAvatarFromInventory(string rawAvatarId, string itemName, int attachmentPoint);

        /// <summary>
        /// Detach the object containing this script from the avatar it is attached to without checking for PERMISSION_ATTACH
        /// </summary>
        /// <remarks>Nothing happens if the object is not attached.</remarks>
        void osForceDetachFromAvatar();

        /// <summary>
        /// Returns a strided list of the specified attachment points and the number of attachments on those points.
        /// </summary>
        /// <param name="avatar">avatar UUID</param>
        /// <param name="attachmentPoints">list of ATTACH_* constants</param>
        /// <returns></returns>
        LSL_List osGetNumberOfAttachments(LSL_Key avatar, LSL_List attachmentPoints);

        /// <summary>
        /// Sends a specified message to the specified avatar's attachments on
        ///     the specified attachment points.
        /// </summary>
        /// <remarks>
        /// Behaves as osMessageObject(), without the sending script needing to know the attachment keys in advance.
        /// </remarks>
        /// <param name="avatar">avatar UUID</param>
        /// <param name="message">message string</param>
        /// <param name="attachmentPoints">list of ATTACH_* constants, or -1 for all attachments. If -1 is specified and OS_ATTACH_MSG_INVERT_POINTS is present in flags, no action is taken.</param>
        /// <param name="flags">flags further constraining the attachments to deliver the message to.</param>
        void osMessageAttachments(LSL_Key avatar, string message, LSL_List attachmentPoints, int flags);

        #endregion

        //texture draw functions
        string osMovePen(string drawList, int x, int y);
        string osDrawLine(string drawList, int startX, int startY, int endX, int endY);
        string osDrawLine(string drawList, int endX, int endY);
        string osDrawText(string drawList, string text);
        string osDrawEllipse(string drawList, int width, int height);
        string osDrawRectangle(string drawList, int width, int height);
        string osDrawFilledRectangle(string drawList, int width, int height);
        string osDrawPolygon(string drawList, LSL_List x, LSL_List y);
        string osDrawFilledPolygon(string drawList, LSL_List x, LSL_List y);
        string osSetFontName(string drawList, string fontName);
        string osSetFontSize(string drawList, int fontSize);
        string osSetPenSize(string drawList, int penSize);
        string osSetPenColor(string drawList, string color);
        string osSetPenColour(string drawList, string colour); // Deprecated
        string osSetPenCap(string drawList, string direction, string type);
        string osDrawImage(string drawList, int width, int height, string imageUrl);
        vector osGetDrawStringSize(string contentType, string text, string fontName, int fontSize);
        void osSetStateEvents(int events);

        double osList2Double(LSL_Types.list src, int index);

        void osSetRegionWaterHeight(double height);
        void osSetRegionSunSettings(bool useEstateSun, bool sunFixed, double sunHour);
        void osSetEstateSunSettings(bool sunFixed, double sunHour);
        double osGetCurrentSunHour();
        double osGetSunParam(string param);
        double osSunGetParam(string param); // Deprecated
        void osSetSunParam(string param, double value);
        void osSunSetParam(string param, double value); // Deprecated

        // Wind Module Functions
        string osWindActiveModelPluginName();
        void osSetWindParam(string plugin, string param, LSL_Float value);
        LSL_Float osGetWindParam(string plugin, string param);

        // Parcel commands
        void osParcelJoin(vector pos1, vector pos2);
        void osParcelSubdivide(vector pos1, vector pos2);
        void osSetParcelDetails(vector pos, LSL_List rules);
        void osParcelSetDetails(vector pos, LSL_List rules); // Deprecated

        string osGetScriptEngineName();
        string osGetSimulatorVersion();
        Object osParseJSONNew(string JSON);
        Hashtable osParseJSON(string JSON);

        void osMessageObject(key objectUUID,string message);

        void osMakeNotecard(string notecardName, LSL_Types.list contents);

        string osGetNotecardLine(string name, int line);
        string osGetNotecard(string name);
        int osGetNumberOfNotecardLines(string name);

        string osAvatarName2Key(string firstname, string lastname);
        string osKey2Name(string id);

        // Grid Info Functions
        string osGetGridNick();
        string osGetGridName();
        string osGetGridLoginURI();
        string osGetGridHomeURI();
        string osGetGridGatekeeperURI();
        string osGetGridCustom(string key);

        LSL_String osFormatString(string str, LSL_List strings);
        LSL_List osMatchString(string src, string pattern, int start);
        LSL_String osReplaceString(string src, string pattern, string replace, int count, int start);

        // Information about data loaded into the region
        string osLoadedCreationDate();
        string osLoadedCreationTime();
        string osLoadedCreationID();

        LSL_List osGetLinkPrimitiveParams(int linknumber, LSL_List rules);

        /// <summary>
        /// Check if the given key is an npc
        /// </summary>
        /// <param name="npc"></param>
        /// <returns>TRUE if the key belongs to an npc in the scene.  FALSE otherwise.</returns>
        LSL_Integer osIsNpc(LSL_Key npc);

        key         osNpcCreate(string user, string name, vector position, string notecard);
        key         osNpcCreate(string user, string name, vector position, string notecard, int options);
        LSL_Key     osNpcSaveAppearance(key npc, string notecard);
        void        osNpcLoadAppearance(key npc, string notecard);
        vector      osNpcGetPos(key npc);
        void        osNpcMoveTo(key npc, vector position);
        void        osNpcMoveToTarget(key npc, vector target, int options);

        /// <summary>
        /// Get the owner of the NPC
        /// </summary>
        /// <param name="npc"></param>
        /// <returns>
        /// The owner of the NPC for an owned NPC.  The NPC's agent id for an unowned NPC.  UUID.Zero if the key is not an npc.
        /// </returns>
        LSL_Key     osNpcGetOwner(key npc);

        rotation    osNpcGetRot(key npc);
        void        osNpcSetRot(LSL_Key npc, rotation rot);
        void        osNpcStopMoveToTarget(LSL_Key npc);
        void        osNpcSay(key npc, string message);
        void        osNpcSay(key npc, int channel, string message);
        void        osNpcShout(key npc, int channel, string message);
        void        osNpcSit(key npc, key target, int options);
        void        osNpcStand(LSL_Key npc);
        void        osNpcRemove(key npc);
        void        osNpcPlayAnimation(LSL_Key npc, string animation);
        void        osNpcStopAnimation(LSL_Key npc, string animation);
        void        osNpcTouch(LSL_Key npcLSL_Key, LSL_Key object_key, LSL_Integer link_num);
        void        osNpcWhisper(key npc, int channel, string message);

        LSL_Key     osOwnerSaveAppearance(string notecard);
        LSL_Key     osAgentSaveAppearance(key agentId, string notecard);

        key osGetMapTexture();
        key osGetRegionMapTexture(string regionName);
        LSL_List osGetRegionStats();

        int osGetSimulatorMemory();
        void osKickAvatar(string FirstName,string SurName,string alert);
        void osSetSpeed(string UUID, LSL_Float SpeedModifier);
        LSL_Float osGetHealth(string avatar);
        void osCauseHealing(string avatar, double healing);
        void osCauseDamage(string avatar, double damage);
        LSL_List osGetPrimitiveParams(LSL_Key prim, LSL_List rules);
        void osSetPrimitiveParams(LSL_Key prim, LSL_List rules);
        void osSetProjectionParams(bool projection, LSL_Key texture, double fov, double focus, double amb);
        void osSetProjectionParams(LSL_Key prim, bool projection, LSL_Key texture, double fov, double focus, double amb);

        LSL_List osGetAvatarList();

        LSL_String osUnixTimeToTimestamp(long time);

        LSL_String osGetInventoryDesc(string item);

        LSL_Integer osInviteToGroup(LSL_Key agentId);
        LSL_Integer osEjectFromGroup(LSL_Key agentId);

        void osSetTerrainTexture(int level, LSL_Key texture);
        void osSetTerrainTextureHeight(int corner, double low, double high);

        /// <summary>
        /// Checks if thing is a UUID.
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>1 if thing is a valid UUID, 0 otherwise</returns>
        LSL_Integer osIsUUID(string thing);

        /// <summary>
        /// Wraps to Math.Min()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        LSL_Float osMin(double a, double b);

        /// <summary>
        /// Wraps to Math.max()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        LSL_Float osMax(double a, double b);

        /// <summary>
        /// Get the key of the object that rezzed this object.
        /// </summary>
        /// <returns>Rezzing object key or NULL_KEY if rezzed by agent or otherwise unknown.</returns>
        LSL_Key osGetRezzingObject();

        /// <summary>
        /// Sets the response type for an HTTP request/response
        /// </summary>
        /// <returns></returns>
        void osSetContentType(LSL_Key id, string type);
    }
}
