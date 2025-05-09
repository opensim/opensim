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

#pragma warning disable IDE1006

using key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
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
        //ApiDesc Generate a dynamic texture from a given URL, returns the texture UUID, applies to all faces.
            string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams, int timer);
        //ApiDesc Generate a dynamic texture alpha blended from a given URL, returns the texture UUID, applies to all faces.
            string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                                int timer, int alpha);
        //ApiDesc Generate a dynamic texture alpha blended from a given URL, returns the texture UUID, applies to a given face.
            string osSetDynamicTextureURLBlendFace(string dynamicID, string contentType, string url, string extraParams,
                                                bool blend, int disp, int timer, int alpha, int face);
        //ApiDesc Generate a dynamic texture from a given draw string, returns the texture UUID, applies to all faces.
            string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams, int timer);
        //ApiDesc Generate a dynamic texture from a given draw string, returns the texture UUID, applies to a given face.
            string osSetDynamicTextureDataFace(string dynamicID, string contentType, string data, string extraParams, int timer, int face);
        //ApiDesc Generate a dynamic texture alpha blended from a given draw string, returns the texture UUID, applies to all faces.
            string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                                 int timer, int alpha);
        //ApiDesc Generate a dynamic texture alpha blended from a given draw string, returns the texture UUID, applies to a given face.
            string osSetDynamicTextureDataBlendFace(string dynamicID, string contentType, string data, string extraParams,
                                                 bool blend, int disp, int timer, int alpha, int face);

        //ApiDesc Returns the height of terrain at a given x and y coordinate (meters).
         LSL_Float osGetTerrainHeight(int x, int y);
        //ApiDesc DEPRECATED. Use osGetTerrainHeight instead.
         LSL_Float osTerrainGetHeight(int x, int y); // Deprecated
        //ApiDesc Set the terrain height at a given x and y coordinate (meters).
       LSL_Integer osSetTerrainHeight(int x, int y, double val);
        //ApiDesc DEPRECATED. Use osSetTerrainHeight instead.
       LSL_Integer osTerrainSetHeight(int x, int y, double val); //Deprecated
        //ApiDesc Send terrain to all agents
              void osTerrainFlush();

        //ApiDesc Schedule a region restart seconds in the future.
               int osRegionRestart(double seconds);
        //ApiDesc Schedule a region restart with broadcast message seconds in the future.
               int osRegionRestart(double seconds, string msg);
        //ApiDesc Send a notice message to all avatars in the region.
              void osRegionNotice(string msg);
        //ApiDesc Send a notice message to a given avatar in the region.
              void osRegionNotice(LSL_Key agentID, string msg);
        //ApiDesc Execute a console command.
              bool osConsoleCommand(string Command);
        //ApiDesc Sets the current parcel music URL.
              void osSetParcelMusicURL(LSL_String url);
        //ApiDesc Sets the current parcel media URL.
              void osSetParcelMediaURL(LSL_String url);
        //ApiDesc Sets whether the object should float on water.
              void osSetPrimFloatOnWater(int floatYN);
        //ApiDesc Sets the voice module SIP address to a given address.
              void osSetParcelSIPAddress(string SIPAddress);

        // Avatar Info Commands
        //ApiDesc Returns a list of avatars in the current region.
          LSL_List osGetAgents();
        //ApiDesc Returns the IP address of a given avatar.
            string osGetAgentIP(string agent);

        // Teleport commands
        //ApiDesc Teleport a given avatar (key) to a given position and rotation as look at vector with a velocity vector and teleport flags.
              void osLocalTeleportAgent(LSL_Key agent, LSL_Types.Vector3 position, LSL_Types.Vector3 velocity, LSL_Types.Vector3 lookat, LSL_Integer flags);
        //ApiDesc Teleport a given avatar (key) to a given region by name and local position and rotatation as look at vector.
              void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        //ApiDesc Teleport a given avatar (key) to a given region by grid position and local position and rotation as look at vector.
              void osTeleportAgent(string agent, int regionX, int regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        //ApiDesc Teleport a given avatar (key) to a given position and rotation as look at vector.
              void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        //ApiDesc Teleport the object owner to a given region by name and local position and rotation as look at vector.
              void osTeleportOwner(string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        //ApiDesc Teleport the object owner to a given region by grid position and local position and rotation as look at vector.
              void osTeleportOwner(int regionX, int regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        //ApiDesc Teleport the object owner to a given position and rotation as look at vector.
              void osTeleportOwner(LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);

        // Animation commands
        //ApiDesc Forcefully plays a given animation for a given avatar bypassing animation permissions.
              void osAvatarPlayAnimation(LSL_Key avatarId, string animation);
        //ApiDesc Forcefully stops a given animation for a given avatar bypassing animation permissions.
              void osAvatarStopAnimation(LSL_Key avatarId, string animation);

        #region Attachment commands

        /// <summary>
        /// Attach the object containing this script to the avatar that owns it without asking for PERMISSION_ATTACH
        /// </summary>
        /// <param name='attachment'>The attachment point.  For example, ATTACH_CHEST</param>
        //ApiDesc Forcefully attaches the object containing this script to the object owner bypassing attach permissions.
              void osForceAttachToAvatar(int attachment);

        /// <summary>
        /// Attach an inventory item in the object containing this script to the avatar that owns it without asking for PERMISSION_ATTACH
        /// </summary>
        /// <remarks>
        /// Nothing happens if the owner is not in the region.
        /// </remarks>
        /// <param name='itemName'>Tha name of the item.  If this is not found then a warning is said to the owner</param>
        /// <param name='attachment'>The attachment point.  For example, ATTACH_CHEST</param>
        //ApiDesc Forcefully attaches an object from the inventory of the object containing this script to the object owner bypassing attach permissions.
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
        //ApiDesc Forcefully attaches an object from the inventory of the object containing this script to a given attachment point of a given avatar bypassing attach permissions.
              void osForceAttachToOtherAvatarFromInventory(string rawAvatarId, string itemName, int attachmentPoint);

        /// <summary>
        /// Detach the object containing this script from the avatar it is attached to without checking for PERMISSION_ATTACH
        /// </summary>
        /// <remarks>Nothing happens if the object is not attached.</remarks>
        //ApiDesc Forcefully detach the object containing this script from the avatar it is attached to bypassing attach permissions.
              void osForceDetachFromAvatar();

        /// <summary>
        /// Returns a strided list of the specified attachment points and the number of attachments on those points.
        /// </summary>
        /// <param name="avatar">avatar UUID</param>
        /// <param name="attachmentPoints">list of ATTACH_* constants</param>
        /// <returns></returns>
        //ApiDesc Returns the number of attachments attached to a list of attachment points of a given avatar.
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
        //ApiDesc Sends a message as dataserver event to a given attachment based on the avatar key and attachment point. Can be constrained further with options.
              void osMessageAttachments(LSL_Key avatar, string message, LSL_List attachmentPoints, int flags);

        #endregion

        //texture draw functions
        //ApiDesc Move the drawing position to a given coordinate (pixels x y).
            string osMovePen(string drawList, int x, int y);
        //ApiDesc Draws a line from a starting coordinate (pixels x y) to a target position.
            string osDrawLine(string drawList, int startX, int startY, int endX, int endY);
        //ApiDesc Draws a line from the current drawing position to a target position (pixels x y).
            string osDrawLine(string drawList, int endX, int endY);
        //ApiDesc Draws a given string as text at the current drawing position. Text extends right and downwards.
            string osDrawText(string drawList, string text);
        //ApiDesc Appends an ellipse drawing command to the string provided in drawList and returns the result.\nThe ellipse is drawn with the current pen size and color on the x,y point pairs that comes from LSL list.
            string osDrawEllipse(string drawList, int width, int height);
        //ApiDesc Appends a filled ellipse drawing command to the string provided in drawList and returns the result.\nThe ellipse is drawn with the current pen size and color on the x,y point pairs that comes from LSL list.
            string osDrawFilledEllipse(string drawList, int width, int height);
        //ApiDesc Appends a rectangle drawing command to the string provided in drawList and returns the result.\nThe rectangle is drawn with the current pen size and color on the x,y point pairs that comes from LSL list.
            string osDrawRectangle(string drawList, int width, int height);
        //ApiDesc Appends a filled rectangle drawing command to the string provided in drawList and returns the result.\nThe rectangle is drawn with the current pen size and color on the x,y point pairs that comes from LSL list.
            string osDrawFilledRectangle(string drawList, int width, int height);
        //ApiDesc Appends a polygon drawing command to the string provided in drawList and returns the result.\nThe polygon is drawn with the current pen size and color on the x,y point pairs that comes from LSL list.
            string osDrawPolygon(string drawList, LSL_List x, LSL_List y);
        //ApiDesc Appends a filled polygon drawing command to the string provided in drawList and returns the result.\nThe polygon is drawn with the current pen size and color on the x,y point pairs that comes from LSL list.
            string osDrawFilledPolygon(string drawList, LSL_List x, LSL_List y);
        //ApiDesc Reset all transforms.
            string osDrawResetTransform(string drawList);
        //ApiDesc Appends a rotation transform drawing command to the string provided in drawList and returns the result.
            string osDrawRotationTransform(string drawList, LSL_Float x);
        //ApiDesc Appends a scale transform drawing command to the string provided in drawList and returns the result.
            string osDrawScaleTransform(string drawList, LSL_Float x, LSL_Float y);
        //ApiDesc Appends a translation transform drawing command to the string provided in drawList and returns the result.
            string osDrawTranslationTransform(string drawList, LSL_Float x, LSL_Float y);
        //ApiDesc Set the name of the font that will be used by osDrawText.\n- Threat Level: Not Checked.
            string osSetFontName(string drawList, string fontName);
        //ApiDesc Sets the size of the font used by subsequent osDrawTextText() calls. The fontSize parameter represents the font height in points.
            string osSetFontSize(string drawList, int fontSize);
        //ApiDesc Sets the pen size to a square of penSize pixels by penSize pixels. If penSize is an odd number, the pen will be exactly centered on the coordinates provided in the various drawing commands.
            string osSetPenSize(string drawList, int penSize);
        //ApiDesc This sets the drawing color to either a named .NET color, a 32-bit color value (formatted as eight hexadecimal digits in the format aarrggbb, representing the eight-bit alpha, red, green and blue channels) or a LSL vector color and alpha float value.
            string osSetPenColor(string drawList, string color);
        //ApiDesc This sets the drawing color to either a named .NET color, a 32-bit color value (formatted as eight hexadecimal digits in the format aarrggbb, representing the eight-bit alpha, red, green and blue channels) or a LSL vector color and alpha float value.
            string osSetPenColor(string drawList, LSL_Types.Vector3 color);
        //ApiDesc This sets the drawing color to either a named .NET color, a 32-bit color value (formatted as eight hexadecimal digits in the format aarrggbb, representing the eight-bit alpha, red, green and blue channels) or a LSL vector color and alpha float value.
            string osSetPenColor(string drawList, LSL_Types.Vector3 color, LSL_Float alpha);
        //ApiDesc DEPRECATED. Use osSetPenColor instead.
            string osSetPenColour(string drawList, string colour); // Deprecated
        //ApiDesc Sets the start, end or both caps to either "diamond", "arrow", "round", or default "flat" shape.
            string osSetPenCap(string drawList, string direction, string type);
        //ApiDesc Draw a given image from URL or asset uuid with given width and height at the current draw position.
            string osDrawImage(string drawList, int width, int height, string image);
        //ApiDesc Returns a vector containing the horizontal and vertical dimensions in pixels of the specified text, if drawn in the specified font and at the specified point size. The horizontal extent is returned in the .x component of the vector, and the vertical extent is returned in .y. The .z component is not used. 
            vector osGetDrawStringSize(string contentType, string text, string fontName, int fontSize);

        //ApiDesc Set the water height of the region.
              void osSetRegionWaterHeight(double height);
        //ApiDesc Set whether to use estate sun, sun type (fixed, daycycle) and time offset for the region.
              void osSetRegionSunSettings(bool useEstateSun, bool sunFixed, double sunHour);
        //ApiDesc Set sun type (fixed, daycycle) and time offset for the estate sun.
              void osSetEstateSunSettings(bool sunFixed, double sunHour);
        //ApiDesc Returns the current region sun hour as float (24 hour clock).
         LSL_Float osGetCurrentSunHour();
        //ApiDesc Returns parameters about the skybox.
         LSL_Float osGetSunParam(LSL_String param);
        //ApiDesc DEPRECATED. Use osGetSunParam instead.
            double osSunGetParam(string param); // Deprecated
        //ApiDesc Not implemented.
              void osSetSunParam(string param, double value);
        //ApiDesc DEPRECATED. Use osSetSunParam instead.
              void osSunSetParam(string param, double value); // Deprecated

        // Wind Module Functions
        //ApiDesc Returns the name of the currently enabled wind plugin.
            string osWindActiveModelPluginName();
        //ApiDesc Set a parameter in a given wind plugin.
              void osSetWindParam(string plugin, string param, LSL_Float value);
        //ApiDesc Get a parameter from a given wind plugin.
         LSL_Float osGetWindParam(string plugin, string param);

        // Parcel commands
        //ApiDesc Returns the number of visitors to the region since start.
       LSL_Integer osGetParcelDwell(vector pos);
        //ApiDesc Joins a parcel with another based on positions within both parcels as vectors.
              void osParcelJoin(vector pos1, vector pos2);
        //ApiDesc Subdivides a parcel as rectangle given a start and end position as vector.
              void osParcelSubdivide(vector pos1, vector pos2);
        //ApiDesc Sets PARCEl_FLAGS for a parcel given a vector inside the parcel.
              void osSetParcelDetails(vector pos, LSL_List rules);
        //ApiDesc DEPRECATED. Use osSetParcelDetails instead.
              void osParcelSetDetails(vector pos, LSL_List rules); // Deprecated

        //ApiDesc Returns the name of the active script engine.
            string osGetScriptEngineName();
        //ApiDesc Returns the version information of the current simulator.
            string osGetSimulatorVersion();
       LSL_Integer osCheckODE();
        //ApiDesc Returns the type of the currently enabled physics engine. 
            string osGetPhysicsEngineType();
        //ApiDesc Returns the name of the currently enabled physics engine.
            string osGetPhysicsEngineName();

        //ApiDesc Directly send a message as dataserver event to a given object by its key.
              void osMessageObject(key objectUUID, string message);

        //ApiDesc Creates a new notecard in the primitive inventory with given contents.
              void osMakeNotecard(string notecardName, LSL_String contents);
        //ApiDesc Creates a new notecard in the primitive inventory with given contents.
              void osMakeNotecard(string notecardName, LSL_List contents);

        //ApiDesc Directly returns a given line of a notecard in the primitive inventory.
            string osGetNotecardLine(string name, int line);
        //ApiDesc Directly returns the entire contents of a given notecard as a string.
            string osGetNotecard(string name);
        //ApiDesc Returns the number of lines of a given notecard.
               int osGetNumberOfNotecardLines(string name);

        //ApiDesc Returns the avatar key, based on their first and last name.
            string osAvatarName2Key(string firstname, string lastname);
        //ApiDesc Returns the avatar name given their key.
            string osKey2Name(string id);
        //ApiDesc Returns the SHA256 representation of the input string.
            string osSHA256(string input);

        // Grid Info Functions
        //ApiDesc Returns the grid nick of the current grid.
            string osGetGridNick();
        //ApiDesc Returns the grid name of the current grid.
            string osGetGridName();
        //ApiDesc Returns the login URI of the current grid.
            string osGetGridLoginURI();
        //ApiDesc Returns the home URI of the current grid.
            string osGetGridHomeURI();
        //ApiDesc Returns the gatekeeper URI of the current grid.
            string osGetGridGatekeeperURI();
        //ApiDesc Returns custom grid information based on input key.
            string osGetGridCustom(string key);
        //ApiDesc Tries to determine the home grid URI of an avatar.
            string osGetAvatarHomeURI(string uuid);

        //ApiDesc Fills a given strings placeholders {%d} with the entries of the given list. 
        LSL_String osFormatString(string str, LSL_List strings);
        //ApiDesc Returns a list of matches from a given string from a given start.
          LSL_List osMatchString(string src, string pattern, int start);
        //ApiDesc Returns a string with replaced substrings given a match pattern from a given start for a given number of matches.
        LSL_String osReplaceString(string src, string pattern, string replace, int count, int start);

        // Information about data loaded into the region
        //ApiDesc Returns the creation date of the loaded region scene data.
            string osLoadedCreationDate();
        //ApiDesc Returns the creation time of the loaded region scene data.
            string osLoadedCreationTime();
        //ApiDesc Returns the original region UUID of the loaded region scene data.
            string osLoadedCreationID();

        //ApiDesc Returns a list of the primitive parameters given its link number.
          LSL_List osGetLinkPrimitiveParams(int linknumber, LSL_List rules);

        /// <summary>
        /// Identical to llCreateLink() but does not require permission from the owner.
        /// </summary>
        /// <param name='target'></param>
        /// <param name='parent'></param>
         //ApiDesc Add a link to a linkset bypassing object owner permissions.
              void osForceCreateLink(string target, int parent);

        /// <summary>
        /// Identical to llBreakLink() but does not require permission from the owner.
        /// </summary>
        /// <param name='linknum'></param>
         //ApiDesc Break a link of a linkset bypassing object owner permissions.
              void osForceBreakLink(int linknum);

        /// <summary>
        /// Identical to llBreakAllLinks() but does not require permission from the owner.
        /// </summary>
         //ApiDesc Break all links of a linkset bypassing object owner permissions.
              void osForceBreakAllLinks();

        /// <summary>
        /// Similar to llDie but given an object UUID
        /// </summary>
        /// <param name="objectUUID"></param>
         //ApiDesc Similar to llDie, but works on a given object UUID.
              void osDie(LSL_Key objectUUID);

        /// <summary>
        /// Check if the given key is an npc
        /// </summary>
        /// <param name="npc"></param>
        /// <returns>TRUE if the key belongs to an npc in the scene.  FALSE otherwise.</returns>
         //ApiDesc Returns an integer whether a given key is an NPC or avatar.
       LSL_Integer osIsNpc(LSL_Key npc);

        //ApiDesc Creates a new NPC with a given name at a given position using a supplied notecard for appearance.
               key osNpcCreate(string user, string name, vector position, string notecard);
        //ApiDesc Creates a new NPC with a given name at a given position using a supplied notecard for appearance and additional options.
               key osNpcCreate(string user, string name, vector position, string notecard, int options);
        //ApiDesc Creates a new notecard with a given name from a given NPC (key).
           LSL_Key osNpcSaveAppearance(key npc, LSL_String notecard);
        //ApiDesc Creates a new notecard with a given name from a given NPC (key) with the option to include HUDs.
           LSL_Key osNpcSaveAppearance(key npc, LSL_String notecard, LSL_Integer includeHuds);
        //ApiDesc Loads a given appearance notecard to a given NPC (key).
              void osNpcLoadAppearance(key npc, string notecard);
        //ApiDesc Returns the position of a given NPC (key).
            vector osNpcGetPos(key npc);
        //ApiDesc Moves a given NPC (key) to a position.
              void osNpcMoveTo(key npc, vector position);
        //ApiDesc Sets a target for a given NPC (key) to move towards.
              void osNpcMoveToTarget(key npc, vector target, int options);

        /// <summary>
        /// Get the owner of the NPC
        /// </summary>
        /// <param name="npc"></param>
        /// <returns>
        /// The owner of the NPC for an owned NPC.  The NPC's agent id for an unowned NPC.  UUID.Zero if the key is not an npc.
        /// </returns>
        //ApiDesc Returns the owner key of a given NPC. NULL_KEY if the NPC is unowned.
           LSL_Key osNpcGetOwner(key npc);
     
        //ApiDesc Returns the current rotation of a given NPC (key).
          rotation osNpcGetRot(key npc);

        //ApiDesc Sets the rotation of a given NPC (key).
              void osNpcSetRot(LSL_Key npc, rotation rot);
        //ApiDesc Removes the target a given NPC (key) is moving towards.
              void osNpcStopMoveToTarget(LSL_Key npc);
        //ApiDesc Sets a given NPC (key) profile about text to a given string.
              void osNpcSetProfileAbout(LSL_Key npc, string about);
        //ApiDesc Sets a given NPC (key) profile image to a given image (asset UUID).
              void osNpcSetProfileImage(LSL_Key npc, string image);
        //ApiDesc Instructs a given NPC (key) to say a given message.
              void osNpcSay(key npc, string message);
        //ApiDesc Instructs a given NPC (key) to say a given message on a given channel.
              void osNpcSay(key npc, int channel, string message);
        //ApiDesc Instructs a given NPC (key) to say to a given avatar (key) a given message on a given channel.
              void osNpcSayTo(LSL_Key npc, LSL_Key target, int channel, string msg);
        //ApiDesc Instructs a given NPC (key) to shout a given message on a given channel.
              void osNpcShout(key npc, int channel, string message);
        //ApiDesc Instructs a given NPC (key) to sit on a given object (UUID).
              void osNpcSit(key npc, key target, int options);
        //ApiDesc Instructs a given NPC (key) to stand up.
              void osNpcStand(LSL_Key npc);
        //ApiDesc Removes a given NPC (key).
              void osNpcRemove(key npc);
        //ApiDesc Instructs a given NPC (key) to play a given animation (name) from the inventory of the object containing the script.
              void osNpcPlayAnimation(LSL_Key npc, string animation);
        //ApiDesc Instructs a given NPC (key) to stop playing a given animation (name).
              void osNpcStopAnimation(LSL_Key npc, string animation);
        //ApiDesc Instructs a given NPC (key) to touch a given object (UUID) and link.
              void osNpcTouch(LSL_Key npcLSL_Key, LSL_Key object_key, LSL_Integer link_num);
        //ApiDesc Instructs a given NPC (key) to whisper a given message on a given channel.
              void osNpcWhisper(key npc, int channel, string message);

        //ApiDesc Save appearance of object owner to a notecard in the primitive inventory.
           LSL_Key osOwnerSaveAppearance(LSL_String notecard);
        //ApiDesc Save appearance of object owner (with the choice to include Huds or no) to a notecard in the primitive inventory.
           LSL_Key osOwnerSaveAppearance(LSL_String notecard, LSL_Integer includeHuds);
        //ApiDesc Save appearance of an avatar to a notecard in the primitive inventory.
           LSL_Key osAgentSaveAppearance(key agentId, LSL_String notecard);
        //ApiDesc Save appearance of an avatar (with the choice to include Huds or no) to a notecard in the primitive inventory.
           LSL_Key osAgentSaveAppearance(key agentId, LSL_String notecard, LSL_Integer includeHuds);

        //ApiDesc Returns the gender of a given avatar as string. 
        LSL_String osGetGender(LSL_Key rawAvatarId);
        //ApiDesc Returns the asset UUID of the texture representing the region map tile of the current region.
           LSL_Key osGetMapTexture();
        //ApiDesc Returns the asset UUID of the texture representing the region map tile of a given region name or UUID.
           LSL_Key osGetRegionMapTexture(string regionNameOrID);
        //ApiDesc Returns a list of statistics regarding the region and simulator from the stats reporter module.
          LSL_List osGetRegionStats();
        //ApiDesc Returns the x and y size of the region as vector. z is unused.
            vector osGetRegionSize();

        //ApiDesc Returns the current memory usage of the simulator in bytes.
               int osGetSimulatorMemory();
        //ApiDesc Returns the current memory usage of the simulator in kilobytes.
               int osGetSimulatorMemoryKB();
        //ApiDesc Disconnects an avatar from the simulator by first and last name or avatar key.
              void osKickAvatar(string FirstName, string SurName, string alert);
        //ApiDesc Disconnects an avatar from the simulator by first and last name or avatar key.
              void osKickAvatar(LSL_Key agentId, string alert);
        //ApiDesc Sets a modifier for the movement speed of a given avatar.
              void osSetSpeed(string UUID, LSL_Float SpeedModifier);
        //ApiDesc Sets a modifier for the movement speed of the object owner.
              void osSetOwnerSpeed(LSL_Float SpeedModifier);
        //ApiDesc Returns the current health of a given avatar.
         LSL_Float osGetHealth(key agentId);
        //ApiDesc Heals a given avatar by a given amount.
              void osCauseHealing(key agentId, LSL_Float healing);
        //ApiDesc Sets the health of a given avatar to a given amount.
              void osSetHealth(key agentId, LSL_Float health);
        //ApiDesc Sets the rate of healing for a given avatar to a given amount.
              void osSetHealRate(key agentId, LSL_Float health);
        //ApiDesc Returns the rate of healing for a given avatar.
         LSL_Float osGetHealRate(key agentId);
        //ApiDesc Subtracts health from a given avatar by a given amount.
              void osCauseDamage(key avatar, LSL_Float damage);
        //ApiDesc Forces a given avatar to sit bypassing permissions.
              void osForceOtherSit(string avatar);
        //ApiDesc Forces a given avatar to sit on a given target (object UUID) bypassing permissions.
              void osForceOtherSit(string avatar, string target);
        //ApiDesc Returns a list of primitive params of a given primitive (object UUID).
          LSL_List osGetPrimitiveParams(LSL_Key prim, LSL_List rules);
        //ApiDesc Sets primitive params of a given primitive (object UUID).
              void osSetPrimitiveParams(LSL_Key prim, LSL_List rules);
        //ApiDesc Sets the light projection parameters of the object containing the script.
              void osSetProjectionParams(LSL_Integer projection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb);
        //ApiDesc Sets the light projection parameters of a given primitive (object UUID).
              void osSetProjectionParams(LSL_Key prim, LSL_Integer projection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb);
        //ApiDesc Sets the light projection parameters of a given link.
              void osSetProjectionParams(LSL_Integer linknumber, LSL_Integer projection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb);

        //ApiDesc Returns a strided list (key, position, name) of all the avatars in the region.
          LSL_List osGetAvatarList();
        //ApiDesc Returns a strided list (key, position, name) of all NPCs in the region.
          LSL_List osGetNPCList();

        //ApiDesc Returns the timestamp string for a given unix epoch (seconds).
        LSL_String osUnixTimeToTimestamp(LSL_Integer time);

        //ApiDesc Sends a group invite to a given avatar for the group the object belongs to.
       LSL_Integer osInviteToGroup(LSL_Key agentId);
        //ApiDesc Removes a given avatar from the group the object belongs to.
       LSL_Integer osEjectFromGroup(LSL_Key agentId);

        //ApiDesc Sets the terrain texture for a given level.
              void osSetTerrainTexture(int level, LSL_Key texture);
        //ApiDesc Sets terrain textures for legacy viewers it types == 0 or 2, textures for new viewers it types == 1 or 2 or PBR materials if types == 1
             void osSetTerrainTextures(LSL_List textures, LSL_Integer types);
        //ApiDesc Sets the texture low and high values for a given region corner.
              void osSetTerrainTextureHeight(int corner, double low, double high);

        /// <summary>
        /// Checks if thing is a UUID.
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>1 if thing is a valid UUID, 0 otherwise</returns>
        //ApiDesc Returns an integer whether a given string is a UUID or not.
       LSL_Integer osIsUUID(string thing);

        /// <summary>
        /// Wraps to Math.Min()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        //ApiDesc Returns the smaller of two given numbers.
         LSL_Float osMin(double a, double b);

        /// <summary>
        /// Wraps to Math.max()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        //ApiDesc Returns the larger of two given numbers.
         LSL_Float osMax(double a, double b);

        /// <summary>
        /// Get the key of the object that rezzed this object.
        /// </summary>
        /// <returns>Rezzing object key or NULL_KEY if rezzed by agent or otherwise unknown.</returns>
        //ApiDesc Returns the key of the object that rezzed the object the script is in.
           LSL_Key osGetRezzingObject();

        /// <summary>
        /// Sets the response type for an HTTP request/response
        /// </summary>
        /// <returns></returns>
        //ApiDesc Sets the response type of a HTTP request or response
              void osSetContentType(LSL_Key id, string type);

        /// <summary>
        /// Attempts to drop an attachment to the ground
        /// </summary>
        //ApiDesc Attempts to drop the attachment the script is in to the ground.
              void osDropAttachment();

        /// <summary>
        /// Attempts to drop an attachment to the ground while bypassing the script permissions
        /// </summary>
        //ApiDesc Attempts to drop the attachment the script is in to the ground bypassing script permissions.
              void osForceDropAttachment();

        /// <summary>
        /// Attempts to drop an attachment at the specified coordinates.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <returns></returns>
        //ApiDesc Attempts to drop the attachment the script is in to a given position on the ground.
              void osDropAttachmentAt(vector pos, rotation rot);

        /// <summary>
        /// Attempts to drop an attachment at the specified coordinates.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <returns></returns>
        //ApiDesc Attempts to drop the attachment the script is in to a given position on the ground bypassing script permissions.
              void osForceDropAttachmentAt(vector pos, rotation rot);

        /// <summary>
        /// Identical to llListen except for a bitfield which indicates which
        /// string parameters should be parsed as regex patterns.
        /// </summary>
        /// <param name="channelID"></param>
        /// <param name="name"></param>
        /// <param name="ID"></param>
        /// <param name="msg"></param>
        /// <param name="regexBitfield">
        /// OS_LISTEN_REGEX_NAME
        /// OS_LISTEN_REGEX_MESSAGE
        /// </param>
        /// <returns></returns>
        //ApiDesc Creates a listener that parses matches for name and message through regex.
       LSL_Integer osListenRegex(int channelID, string name, string ID, string msg, int regexBitfield);

        /// <summary>
        /// Wraps to bool Regex.IsMatch(string input, string pattern)
        /// </summary>
        /// <param name="input">string to test for match</param>
        /// <param name="regex">string to use as pattern</param>
        /// <returns>boolean</returns>
        //ApiDesc Returns an integer whether the input string matches a regex pattern.
       LSL_Integer osRegexIsMatch(string input, string pattern);

        //ApiDesc Request a new URL and directly return the assigned key.
           LSL_Key osRequestURL(LSL_List options);
        //ApiDesc Request a new secure URL and directly return the assigned key.
           LSL_Key osRequestSecureURL(LSL_List options);
        //ApiDesc Sets a given collision sound and volume to the object containing the script.
              void osCollisionSound(string impact_sound, double impact_volume);
     
        //ApiDesc Sets or unsets volume detection for the object containing the script.
              void osVolumeDetect(int detect);
     
        //ApiDesc Returns a list of the inertia data of the object containing the script.
          LSL_List osGetInertiaData();
        //ApiDesc Unsets the inertia data of the object containing the script.
              void osClearInertia();
        //ApiDesc Sets the inertia data for the object containing the script.
              void osSetInertia(LSL_Float mass, vector centerOfMass, vector principalInertiaScaled,  rotation rot);
        //ApiDesc Sets the inertia data for the object containing the script based on a box shape bounding box calculation.
              void osSetInertiaAsBox(LSL_Float mass, vector boxSize, vector centerOfMass, rotation rot);
        //ApiDesc Sets the inertia data for the object containing the script based on a spherical bounding box calculation.
              void osSetInertiaAsSphere(LSL_Float mass,  LSL_Float radius, vector centerOfMass);
        //ApiDesc Sets the inertia data for the object containing the script based on a cylindrical bounding box calculation.
              void osSetInertiaAsCylinder(LSL_Float mass,  LSL_Float radius, LSL_Float length, vector centerOfMass,rotation lslrot);
     
        //ApiDesc Teleport a given primitive (object UUID) to a given position and rotation.
       LSL_Integer osTeleportObject(LSL_Key objectUUID, vector targetPos, rotation targetrotation, LSL_Integer flags);
        //ApiDesc Return the link number of a given primitive in the linkset by name.
       LSL_Integer osGetLinkNumber(LSL_String name);

        //ApiDesc Rounds a given value to a specified amount of digits.
         LSL_Float osRound(LSL_Float value, LSL_Integer digits);

        //ApiDesc Returns the squared values of a vector multiplied with each other.
         LSL_Float osVecMagSquare(vector a);
        //ApiDesc Returns the difference of two squared vectors.
         LSL_Float osVecDistSquare(vector a, vector b);
        //ApiDesc Returns the angle between two vectors.
         LSL_Float osAngleBetween(vector a, vector b);

        //ApiDesc Sets the sound volume of a given link.
              void osAdjustSoundVolume(LSL_Integer linknum, LSL_Float volume);
        //ApiDesc Sets a looping sound and volume of a given link.
              void osLoopSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume);
        //ApiDesc Sets a looping sound and volume of a given link as master.
              void osLoopSoundMaster(LSL_Integer linknum, LSL_String sound, LSL_Float volume);
        //ApiDesc Sets a looping sound and volume of a given link as slave.
              void osLoopSoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume);
        //ApiDesc Sets a sound and volume of a given link.
              void osPlaySound(LSL_Integer linknum, LSL_String sound, LSL_Float volume);
        //ApiDesc Sets a sound and volume of a given link as slave.
              void osPlaySoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume);
        //ApiDesc Sets a sound to preload for a given link.
              void osPreloadSound(LSL_Integer linknum, LSL_String sound);
        //ApiDesc Sets the sound radius for a given link.
              void osSetSoundRadius(LSL_Integer linknum, LSL_Float radius);
        //ApiDesc Stops the sound for a given link.
              void osStopSound(LSL_Integer linknum);
        //ApiDesc Trigger a given preloaded sound with volume for a given link.
              void osTriggerSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume);
        //ApiDesc Trigger a given preloaded sound with volume and axis-aligned bounding box for a given link.
              void osTriggerSoundLimited(LSL_Integer linknum, LSL_String sound, LSL_Float volume, vector top_north_east, vector bottom_south_west);

        //ApiDesc Detected params return of triggered user event of their set country.
        LSL_String osDetectedCountry(LSL_Integer number);
        //ApiDesc Returns the country of a user.
        LSL_String osGetAgentCountry(LSL_Key agentId);

        //ApiDesc Returns the remainder of a string from start index.
        LSL_String osStringSubString(LSL_String src, LSL_Integer start);
        //ApiDesc Returns a substring from start index and length.
        LSL_String osStringSubString(LSL_String src, LSL_Integer start, LSL_Integer length);
        //ApiDesc Returns an integer whether a string starts with another string.
       LSL_Integer osStringStartsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase);
        //ApiDesc Returns an integer whether a string ends with another string.
       LSL_Integer osStringEndsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase);
        //ApiDesc Returns the index of a substring of a given string.
       LSL_Integer osStringIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase);
        //ApiDesc Returns the index of a substring of given string starting from an index and match count.
       LSL_Integer osStringIndexOf(LSL_String src, LSL_String value, LSL_Integer start, LSL_Integer count, LSL_Integer ignorecase);
        //ApiDesc Returns the index of the last substring of a given string.
       LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase);
        //ApiDesc Returns the index of the last substring of a given string starting from an index and match count.
       LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value, LSL_Integer start, LSL_Integer count, LSL_Integer ignorecase);
        //ApiDesc Returns the remainder of a given string with characters from start index and count removed.
        LSL_String osStringRemove(LSL_String src, LSL_Integer start, LSL_Integer count);
        //ApiDesc Returns a string with a substring replaced with another string.
        LSL_String osStringReplace(LSL_String src, LSL_String oldvalue, LSL_String newvalue);

        //ApiDesc Returns an integer whether two float values are within floating point precision equal.
       LSL_Integer osApproxEquals(LSL_Float a, LSL_Float b);
        //ApiDesc Returns an integer whether two float values are within a given margin equal.
       LSL_Integer osApproxEquals(LSL_Float a, LSL_Float b, LSL_Float margin);
        //ApiDesc Returns an integer whether two vectors are within floating point precision equal.
       LSL_Integer osApproxEquals(vector va, vector vb);
        //ApiDesc Returns an integer whether two vectors are within a given margin equal.
       LSL_Integer osApproxEquals(vector va, vector vb, LSL_Float margin);
        //ApiDesc Returns an integer whether two rotations are within floating point precision equal.
       LSL_Integer osApproxEquals(rotation ra, rotation rb);
        //ApiDesc Returns an integer whether two rotations are within a given margin equal.
       LSL_Integer osApproxEquals(rotation ra, rotation rb, LSL_Float margin);
        //ApiDesc Returns the last owner (key) of a given inventory item (name or key) of the object containing the script.
           LSL_Key osGetInventoryLastOwner(LSL_String itemNameOrId);
        //ApiDesc Returns the key of a given inventory item of the object containing the script. Not the asset UUID.
           LSL_Key osGetInventoryItemKey(LSL_String name);
        //ApiDesc Returns the asset UUID of a given inventory item filtered by type in a given link.
           LSL_Key osGetLinkInventoryKey(LSL_Integer linkNumber, LSL_String name, LSL_Integer type);
        //ApiDesc Returns a list of asset UUIDs of inventory items filtered by type in a given link.
          LSL_List osGetLinkInventoryKeys(LSL_Integer linkNumber, LSL_Integer type);
        //ApiDesc Returns the asset UUID of a given inventory item of a given link.
           LSL_Key osGetLinkInventoryItemKey(LSL_Integer linkNumber, LSL_String name);
        //ApiDesc Returns the name of a given inventory item key in the object containing the script.
        LSL_String osGetInventoryName(LSL_Key itemId);
        //ApiDesc Returns the name of a given inventory item key in a given link.
        LSL_String osGetLinkInventoryName(LSL_Integer linkNumber, LSL_Key itemId);
        //ApiDesc Returns the description of a given inventory item key or name in the object containing the script.
        LSL_String osGetInventoryDesc(LSL_String itemNameOrId);
        //ApiDesc Returns the description of a given inventory item key or name in a given link.
        LSL_String osGetLinkInventoryDesc(LSL_Integer linkNumber, LSL_String itemNameorid);
        //ApiDesc Returns a list of inventory item keys filtered by type in the object containing the script.
          LSL_List osGetInventoryItemKeys(LSL_Integer type);
        //ApiDesc Returns a list of inventory item keys filtered by type in a given link.
          LSL_List osGetLinkInventoryItemKeys(LSL_Integer linkNumber, LSL_Integer type);
        //ApiDesc Returns a list of inventory names filtered by type in the object containing the script.
          LSL_List osGetInventoryNames(LSL_Integer type);
        //ApiDesc Returns a list of inventory names filtered by type in a given link.
          LSL_List osGetLinkInventoryNames(LSL_Integer linkNumber, LSL_Integer type);
        //ApiDesc Removes a inventory item by name in a given link.
              void osRemoveLinkInventory(LSL_Integer linkNumber, LSL_String name);

        //ApiDesc Send a given inventory item (name) from a given link to a destination object or avatar (key).
              void osGiveLinkInventory(LSL_Integer linkNumber, LSL_Key destination, LSL_String inventory);
        //ApiDesc Send a list of inventory items (key) in a given link to a given destination avatar (key) creating a new named folder.
              void osGiveLinkInventoryList(LSL_Integer linkNumber, LSL_Key destination, LSL_String folderName, LSL_List inventory);
        //ApiDesc Returns the key of the last event setting detected params.
           LSL_Key osGetLastChangedEventKey();
        //ApiDesc Returns the seconds since midnight of the PST time zone.
         LSL_Float osGetPSTWallclock();
        //ApiDesc Returns a spherical interpolation of two rotations shifted by amount.
      LSL_Rotation osSlerp(LSL_Rotation a, LSL_Rotation b, LSL_Float amount);
        //ApiDesc Returns a spherical interpolation of two vectors shifted by amount.
            vector osSlerp(vector a, vector b, LSL_Float amount);

        //ApiDesc Resets all scripts in the inventory of a link, the entire linkset or itself.
              void osResetAllScripts(LSL_Integer AllLinkset);
        //ApiDesc Returns an integer whether a given number is out of bounds or NaN.
       LSL_Integer osIsNotValidNumber(LSL_Float v);
     
        //ApiDesc Sets the max distance for allowing avatars to sit on the object containing the script.
              void osSetSitActiveRange(LSL_Float v);
        //ApiDesc Sets the max distance for allowing avatars to sit on a given link..
              void osSetLinkSitActiveRange(LSL_Integer linkNumber, LSL_Float v);
        //ApiDesc Returns the max distance for allowing avatars to sit on the object containing the script.
         LSL_Float osGetSitActiveRange();
        //ApiDesc Returns the max distance for allowing avatars to sit on a given link.
         LSL_Float osGetLinkSitActiveRange(LSL_Integer linkNumber);
        //ApiDesc Returns the position of the sit target of the object containing the script.
            vector osGetSitTargetPos();
        //ApiDesc Returns the rotation of the sit target of the object containing the script.
          rotation osGetSitTargetRot();
        //ApiDesc Sets the stand offset from the position of the object containing the script.
              void osSetStandTarget(vector v);
        //ApiDesc Sets the stand offset from the position of a given link.
              void osSetLinkStandTarget(LSL_Integer linkNumber, vector v);
        //ApiDesc Returns the stand offset of the object containing the script.
            vector osGetStandTarget();
        //ApiDesc Returns the stand offset of a given link.
            vector osGetLinkStandTarget(LSL_Integer linkNumber);
        //ApiDesc Removes the object animations and returns the count of removed animations.
       LSL_Integer osClearObjectAnimations();

        //ApiDesc Returns the virtual seconds since environment midnight.
         LSL_Float osGetApparentTime();
        //ApiDesc Returns the virtual second since environment midnight as timestamp.
        LSL_String osGetApparentTimeString(LSL_Integer format24);
        //ApiDesc Returns the virtual seconds since environment midnight.
         LSL_Float osGetApparentRegionTime();
        //ApiDesc Returns the virtual second since environment midnight as timestamp.
        LSL_String osGetApparentRegionTimeString(LSL_Integer format24);

        //ApiDesc Sets the environment of a given avatar (key) to a given settings item (asset UUID) with transition time.
       LSL_Integer osReplaceAgentEnvironment(LSL_Key agentkey, LSL_Integer transition, LSL_String daycycle);
        //ApiDesc Sets the environment of the current parcel to a given settings item (asset UUID) with transition time.
       LSL_Integer osReplaceParcelEnvironment(LSL_Integer transition, LSL_String daycycle);
        //ApiDesc Alters the region environment base parameters of a given settings item (asset UUID) with transition time.
       LSL_Integer osReplaceRegionEnvironment(LSL_Integer transition, LSL_String daycycle, LSL_Float daylen, LSL_Float dayoffset, LSL_Float altitude1, LSL_Float altitude2, LSL_Float altitude3);
        //ApiDesc Resets either the parcel or region environment to their default values.
       LSL_Integer osResetEnvironment(LSL_Integer parcelOrRegion, LSL_Integer transition);

        //ApiDesc Sets the particle system rules of the object containing the script.
              void osParticleSystem(LSL_List rules);
        //ApiDesc Sets the particle system rules of a given link.
              void osLinkParticleSystem(LSL_Integer linknumber, LSL_List rules);

        //ApiDesc Sets the look at direction of a NPC to a given object (key) and offset.
       LSL_Integer osNpcLookAt(LSL_Key npckey, LSL_Integer type, LSL_Key objkey, vector offset);

        //ApiDesc Returns the type of a given avatar (key).
       LSL_Integer osAvatarType(LSL_Key avkey);
        //ApiDesc Returns the type of a given avatar (name).
       LSL_Integer osAvatarType(LSL_String sFirstName, LSL_String sLastName);
        //ApiDesc Sorts a given list in place.
              void osListSortInPlace(LSL_List src, LSL_Integer stride, LSL_Integer ascending);
        //ApiDesc Sorts a given strided list in place.
              void osListSortInPlaceStrided(LSL_List src, LSL_Integer stride, LSL_Integer stride_index, LSL_Integer ascending);
        //ApiDesc Returns a list with the requested parcel details.
          LSL_List osGetParcelDetails(LSL_Key id, LSL_List details);
        //ApiDesc Returns a list of parcel ids of the current region.
          LSL_List osGetParcelIDs();
        //ApiDesc Returns the parcel id of the current parcel.
           LSL_Key osGetParcelID();
        //ApiDesc Returns a strided list of a given list.
          LSL_List osOldList2ListStrided(LSL_List src, int start, int end, int stride);
        //ApiDesc Returns the number of links in the object containing the script.
       LSL_Integer osGetPrimCount();
        //ApiDesc Returns the number of links in a given object (key).
       LSL_Integer osGetPrimCount(LSL_Key object_id);
        //ApiDesc Returns the number of avatars seated on the object containing the script.
       LSL_Integer osGetSittingAvatarsCount();
        //ApiDesc Returns the number of avatars seated on a given object (key).
       LSL_Integer osGetSittingAvatarsCount(LSL_Key object_id);
        //ApiDesc Encrypt a plain text using AES-256-CBC Symmetric Algorithm Key (secret) and a random Initialization Vector (IV). Returns the Hex string of the IV bytes and the Hex string of the encrypted text bytes separated with (:).
        LSL_String osAESEncrypt(string secret, string plainText);
        //ApiDesc Encrypt a plain text using AES-256-CBC Symmetric Algorithm Key (secret) and a custom Initialization Vector (ivString). Returns the Hex string of the IV bytes and the Hex string of the encrypted text bytes separated with (:).
        LSL_String osAESEncryptTo(string secret, string plainText, string ivString);
        //ApiDesc Decrypt an encrypted text using osAESEncrypt() and the same Key (secret) used in the encryption. Returns the decrypted text.
        LSL_String osAESDecrypt(string secret, string encryptedText);
        //ApiDesc Decrypt an encrypted text using osAESEncryptTo() and the same Key (secret) and Initialization Vector (ivString) used in the encryption. Returns the decrypted text.
        LSL_String osAESDecryptFrom(string secret, string encryptedText, string ivString);
        //ApiDesc Returns the color vector of a given link and face.
            vector osGetLinkColor(LSL_Integer linknum, LSL_Integer face);
        //ApiDesc Returns the color vector of a given color temperature.
            vector osTemperature2sRGB(LSL_Float dtemp);

             /// <summary>
             /// osListFindListNext identical to llListFindListNext but with search limited to sublist from start to end (excluded) 
             /// </summary>
             /// <param name="src">string to test for match</param>
             /// <param name="test">string to use as pattern</param>
             /// <param name="start">index of where to start search</param>
             /// <param name="end">index of where to stop search.</param>
             /// <param name="instance">number of match to return</param>
             /// <returns>a integer with index of match point or -1</returns>
        //ApiDesc Returns the nth index of the sublist constrained with start and end count.
       LSL_Integer osListFindListNext(LSL_List src, LSL_List test, LSL_Integer start, LSL_Integer end, LSL_Integer instance);
        //ApiDesc Returns a string that is at index(>=0) in src or empty string if that is not a string
        LSL_String osListAsString(LSL_List src, int index);
        //ApiDesc Returns a integer that is at index(>=0) in src or 0 if that is not a integer
       LSL_Integer osListAsInteger(LSL_List src, int index);
        //ApiDesc Returns a float that is at index(>=0) in src or 0 if that is not a float
         LSL_Float osListAsFloat(LSL_List src, int index);
        //ApiDesc Returns a vector that is at index(>=0) in src or Zero vector if that is not a vector
            vector osListAsVector(LSL_List src, int index);
        //ApiDesc Returns a rotation that is at index(>=0) in src or zero rotation if that is not a vector
      LSL_Rotation osListAsRotation(LSL_List src, int index);
    }
}
