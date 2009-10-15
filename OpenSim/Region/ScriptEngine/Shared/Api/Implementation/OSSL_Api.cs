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
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Net;
using System.Threading;
using OpenMetaverse;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Region.CoreModules.Avatar.NPC;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Hypergrid;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using System.Text.RegularExpressions;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    //////////////////////////////////////////////////////////////
    //
    // Level description
    //
    // None     - Function is no threat at all. It doesn't constitute
    //            an threat to either users or the system and has no
    //            known side effects
    //
    // Nuisance - Abuse of this command can cause a nuisance to the
    //            region operator, such as log message spew
    //
    // VeryLow  - Extreme levels ob abuse of this function can cause
    //            impaired functioning of the region, or very gullible
    //            users can be tricked into experiencing harmless effects
    //
    // Low      - Intentional abuse can cause crashes or malfunction
    //            under certain circumstances, which can easily be rectified,
    //            or certain users can be tricked into certain situations
    //            in an avoidable manner.
    //
    // Moderate - Intentional abuse can cause denial of service and crashes
    //            with potential of data or state loss, or trusting users
    //            can be tricked into embarrassing or uncomfortable
    //            situationsa.
    //
    // High     - Casual abuse can cause impaired functionality or temporary
    //            denial of service conditions. Intentional abuse can easily
    //            cause crashes with potential data loss, or can be used to
    //            trick experienced and cautious users into unwanted situations,
    //            or changes global data permanently and without undo ability
    //            Malicious scripting can allow theft of content
    //
    // VeryHigh - Even normal use may, depending on the number of instances,
    //            or frequency of use, result in severe service impairment
    //            or crash with loss of data, or can be used to cause
    //            unwanted or harmful effects on users without giving the
    //            user a means to avoid it.
    //
    // Severe   - Even casual use is a danger to region stability, or function
    //            allows console or OS command execution, or function allows
    //            taking money without consent, or allows deletion or
    //            modification of user data, or allows the compromise of
    //            sensitive data by design.

    [Serializable]
    public class OSSL_Api : MarshalByRefObject, IOSSL_Api, IScriptApi
    {
        internal IScriptEngine m_ScriptEngine;
        internal ILSL_Api m_LSL_Api = null; // get a reference to the LSL API so we can call methods housed there
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool m_OSFunctionsEnabled = false;
        internal ThreatLevel m_MaxThreatLevel = ThreatLevel.VeryLow;
        internal float m_ScriptDelayFactor = 1.0f;
        internal float m_ScriptDistanceFactor = 1.0f;
        internal Dictionary<string, List<UUID> > m_FunctionPerms = new Dictionary<string, List<UUID> >();

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            if (m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
                m_OSFunctionsEnabled = true;

            m_ScriptDelayFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDistanceLimitFactor", 1.0f);

            string risk = m_ScriptEngine.Config.GetString("OSFunctionThreatLevel", "VeryLow");
            switch (risk)
            {
            case "None":
                m_MaxThreatLevel = ThreatLevel.None;
                break;
            case "VeryLow":
                m_MaxThreatLevel = ThreatLevel.VeryLow;
                break;
            case "Low":
                m_MaxThreatLevel = ThreatLevel.Low;
                break;
            case "Moderate":
                m_MaxThreatLevel = ThreatLevel.Moderate;
                break;
            case "High":
                m_MaxThreatLevel = ThreatLevel.High;
                break;
            case "VeryHigh":
                m_MaxThreatLevel = ThreatLevel.VeryHigh;
                break;
            case "Severe":
                m_MaxThreatLevel = ThreatLevel.Severe;
                break;
            default:
                break;
            }
        }

        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
//                lease.RenewOnCallTime = TimeSpan.FromSeconds(10.0);
//                lease.SponsorshipTimeout = TimeSpan.FromMinutes(1.0);
            }
            return lease;
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        internal void OSSLError(string msg)
        {
            throw new Exception("OSSL Runtime Error: " + msg);
        }

        private void InitLSL()
        {
            if (m_LSL_Api != null)
                return;

            m_LSL_Api = (ILSL_Api)m_ScriptEngine.GetApi(m_itemID, "LSL");
        }

        //
        //Dumps an error message on the debug console.
        //

        internal void OSSLShoutError(string message) 
        {
            if (message.Length > 1023)
                message = message.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(message),
                          ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, true);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, message);
        }

        public void CheckThreatLevel(ThreatLevel level, string function)
        {
            if (!m_OSFunctionsEnabled)
                OSSLError(String.Format("{0} permission denied.  All OS functions are disabled.", function)); // throws

            if (!m_FunctionPerms.ContainsKey(function))
            {
                string perm = m_ScriptEngine.Config.GetString("Allow_" + function, "");
                if (perm == "")
                {
                    m_FunctionPerms[function] = null; // a null value is default
                }
                else
                {
                    bool allowed;

                    if (bool.TryParse(perm, out allowed))
                    {
                        // Boolean given
                        if (allowed)
                        {
                            m_FunctionPerms[function] = new List<UUID>();
                            m_FunctionPerms[function].Add(UUID.Zero);
                        }
                        else
                            m_FunctionPerms[function] = new List<UUID>(); // Empty list = none
                    }
                    else
                    {
                        m_FunctionPerms[function] = new List<UUID>();

                        string[] ids = perm.Split(new char[] {','});
                        foreach (string id in ids)
                        {
                            string current = id.Trim();
                            UUID uuid;

                            if (UUID.TryParse(current, out uuid))
                            {
                                if (uuid != UUID.Zero)
                                    m_FunctionPerms[function].Add(uuid);
                            }
                        }
                    }
                }
            }

            // If the list is null, then the value was true / undefined
            // Threat level governs permissions in this case
            //
            // If the list is non-null, then it is a list of UUIDs allowed
            // to use that particular function. False causes an empty
            // list and therefore means "no one"
            //
            // To allow use by anyone, the list contains UUID.Zero
            //
            if (m_FunctionPerms[function] == null) // No list = true
            {
                if (level > m_MaxThreatLevel)
                    OSSLError(
                        String.Format(
                            "{0} permission denied.  Allowed threat level is {1} but function threat level is {2}.",
                            function, m_MaxThreatLevel, level));
            }
            else
            {
                if (!m_FunctionPerms[function].Contains(UUID.Zero))
                {
                    if (!m_FunctionPerms[function].Contains(m_host.OwnerID))
                        OSSLError(
                            String.Format("{0} permission denied.  Prim owner is not in the list of users allowed to execute this function.",
                            function));
                }
            }
        }

        protected void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        //
        // OpenSim functions
        //
        public LSL_Integer osTerrainSetHeight(int x, int y, double val)
        {
            CheckThreatLevel(ThreatLevel.High, "osTerrainSetHeight");

            m_host.AddScriptLPS(1);
            if (x > ((int)Constants.RegionSize - 1) || x < 0 || y > ((int)Constants.RegionSize - 1) || y < 0)
                OSSLError("osTerrainSetHeight: Coordinate out of bounds");

            if (World.Permissions.CanTerraformLand(m_host.OwnerID, new Vector3(x, y, 0)))
            {
                World.Heightmap[x, y] = val;
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public LSL_Float osTerrainGetHeight(int x, int y)
        {
            CheckThreatLevel(ThreatLevel.None, "osTerrainGetHeight");

            m_host.AddScriptLPS(1);
            if (x > ((int)Constants.RegionSize - 1) || x < 0 || y > ((int)Constants.RegionSize - 1) || y < 0)
                OSSLError("osTerrainGetHeight: Coordinate out of bounds");

            return World.Heightmap[x, y];
        }

        public void osTerrainFlush()
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osTerrainFlush");

            ITerrainModule terrainModule = World.RequestModuleInterface<ITerrainModule>();
            if (terrainModule != null) terrainModule.TaintTerrain();
        }

        public int osRegionRestart(double seconds)
        {
            // This is High here because region restart is not reliable
            // it may result in the region staying down or becoming
            // unstable. This should be changed to Low or VeryLow once
            // The underlying functionality is fixed, since the security
            // as such is sound
            //
            CheckThreatLevel(ThreatLevel.High, "osRegionRestart");

            m_host.AddScriptLPS(1);
            if (World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false))
            {
                World.Restart((float)seconds);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public void osRegionNotice(string msg)
        {
            // This implementation provides absolutely no security
            // It's high griefing potential makes this classification
            // necessary
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osRegionNotice");

            m_host.AddScriptLPS(1);

            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();

            if (dm != null)
                dm.SendGeneralAlert(msg);
        }

        public void osSetRot(UUID target, Quaternion rotation)
        {
            // This function has no security. It can be used to destroy
            // arbitrary builds the user would normally have no rights to
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetRot");

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(target))
            {
                World.Entities[target].Rotation = rotation;
            }
            else
            {
                OSSLError("osSetRot: Invalid target");
            }
        }

        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            // This may be upgraded depending on the griefing or DOS
            // potential, or guarded with a delay
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURL");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                             int timer, int alpha)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURLBlend");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer, true, (byte) alpha);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureURLBlendFace(string dynamicID, string contentType, string url, string extraParams,
                                             bool blend, int disp, int timer, int alpha, int face)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURLBlendFace");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer, blend, disp, (byte) alpha, face);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                           int timer)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureData");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                          int timer, int alpha)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureDataBlend");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer, true, (byte) alpha);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureDataBlendFace(string dynamicID, string contentType, string data, string extraParams,
                                          bool blend, int disp, int timer, int alpha, int face)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureDataBlendFace");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer, blend, disp, (byte) alpha, face);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public bool osConsoleCommand(string command)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osConsoleCommand");

            m_host.AddScriptLPS(1);

            if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID))
            {
                MainConsole.Instance.RunCommand(command);
                return true;
            }
            return false;
        }

        public void osSetPrimFloatOnWater(int floatYN)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetPrimFloatOnWater");

            m_host.AddScriptLPS(1);
            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    m_host.ParentGroup.RootPart.SetFloatOnWater(floatYN);
                }
            }
        }

        // Teleport functions
        public void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.High, "osTeleportAgent");

            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // agent must be over owners land to avoid abuse
                    if (m_host.OwnerID
                        == World.LandChannel.GetLandObject(
                            presence.AbsolutePosition.X, presence.AbsolutePosition.Y).LandData.OwnerID)
                    {

                        // Check for hostname , attempt to make a hglink
                        // and convert the regionName to the target region
                        if (regionName.Contains(".") && regionName.Contains(":"))
                        {
                            // Try to link the region
                            IHyperlinkService hyperService = World.RequestModuleInterface<IHyperlinkService>();
                            if (hyperService != null)
                            {
                                GridRegion regInfo = hyperService.TryLinkRegion(presence.ControllingClient,
                                                                                regionName);
                                // Get the region name
                                if (regInfo != null)
                                {
                                    regionName = regInfo.RegionName;
                                }
                                else
                                {
                                    // Might need to ping the client here in case of failure??
                                }
                            }
                        }
                        presence.ControllingClient.SendTeleportLocationStart();
                        World.RequestTeleportLocation(presence.ControllingClient, regionName,
                            new Vector3((float)position.x, (float)position.y, (float)position.z),
                            new Vector3((float)lookat.x, (float)lookat.y, (float)lookat.z), (uint)TPFlags.ViaLocation);

                        ScriptSleep(5000);
                    }
                }
            }
        }

        // Teleport functions
        public void osTeleportAgent(string agent, uint regionX, uint regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.High, "osTeleportAgent");

            ulong regionHandle = Util.UIntsToLong((regionX * (uint)Constants.RegionSize), (regionY * (uint)Constants.RegionSize));

            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // agent must be over owners land to avoid abuse
                    if (m_host.OwnerID
                        == World.LandChannel.GetLandObject(
                            presence.AbsolutePosition.X, presence.AbsolutePosition.Y).LandData.OwnerID)
                    {
                        presence.ControllingClient.SendTeleportLocationStart();
                        World.RequestTeleportLocation(presence.ControllingClient, regionHandle,
                            new Vector3((float)position.x, (float)position.y, (float)position.z),
                            new Vector3((float)lookat.x, (float)lookat.y, (float)lookat.z), (uint)TPFlags.ViaLocation);
                        ScriptSleep(5000);
                    }
                }
            }
        }

        public void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            osTeleportAgent(agent, World.RegionInfo.RegionName, position, lookat);
        }

        // Functions that get information from the agent itself.
        //
        // osGetAgentIP - this is used to determine the IP address of
        //the client.  This is needed to help configure other in world
        //resources based on the IP address of the clients connected.
        //I think High is a good risk level for this, as it is an
        //information leak.
        public string osGetAgentIP(string agent)
        {
            CheckThreatLevel(ThreatLevel.High, "osGetAgentIP");

            UUID avatarID = (UUID)agent;

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey((UUID)agent) && World.Entities[avatarID] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatarID];
                EndPoint ep = target.ControllingClient.GetClientEP();
                if (ep is IPEndPoint)
                {
                    IPEndPoint ip = (IPEndPoint)ep;
                    return ip.Address.ToString();
                }
            }
            // fall through case, just return nothing
            return "";
        }

        // Get a list of all the avatars/agents in the region
        public LSL_List osGetAgents()
        {
            // threat level is None as we could get this information with an
            // in-world script as well, just not as efficient
            CheckThreatLevel(ThreatLevel.None, "osGetAgents");

            LSL_List result = new LSL_List();
            foreach (ScenePresence avatar in World.GetAvatars())
            {
                result.Add(avatar.Name);
            }
            return result;
        }

        // Adam's super super custom animation functions
        public void osAvatarPlayAnimation(string avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarPlayAnimation");

            UUID avatarID = (UUID)avatar;

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey((UUID)avatar) && World.Entities[avatarID] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatarID];
                if (target != null)
                {
                    UUID animID=UUID.Zero;
                    lock (m_host.TaskInventory)
                    {
                        foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
                        {
                            if (inv.Value.Name == animation)
                            {
                                if (inv.Value.Type == (int)AssetType.Animation)
                                    animID = inv.Value.AssetID;
                                continue;
                            }
                        }
                    }
                    if (animID == UUID.Zero)
                        target.AddAnimation(animation, m_host.UUID);
                    else
                        target.AddAnimation(animID, m_host.UUID);
                }
            }
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarStopAnimation");

            UUID avatarID = (UUID)avatar;

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatarID) && World.Entities[avatarID] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatarID];
                if (target != null)
                {
                    UUID animID=UUID.Zero;
                    lock (m_host.TaskInventory)
                    {
                        foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
                        {
                            if (inv.Value.Name == animation)
                            {
                                if (inv.Value.Type == (int)AssetType.Animation)
                                    animID = inv.Value.AssetID;
                                continue;
                            }
                        }
                    }
                    if (animID == UUID.Zero)
                        target.RemoveAnimation(animation);
                    else
                        target.RemoveAnimation(animID);
                }
            }
        }

        //Texture draw functions
        public string osMovePen(string drawList, int x, int y)
        {
            CheckThreatLevel(ThreatLevel.None, "osMovePen");

            m_host.AddScriptLPS(1);
            drawList += "MoveTo " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawLine");

            m_host.AddScriptLPS(1);
            drawList += "MoveTo "+ startX+","+ startY +"; LineTo "+endX +","+endY +"; ";
            return drawList;
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawLine");

            m_host.AddScriptLPS(1);
            drawList += "LineTo " + endX + "," + endY + "; ";
            return drawList;
        }

        public string osDrawText(string drawList, string text)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawText");

            m_host.AddScriptLPS(1);
            drawList += "Text " + text + "; ";
            return drawList;
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawEllipse");

            m_host.AddScriptLPS(1);
            drawList += "Ellipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawRectangle");

            m_host.AddScriptLPS(1);
            drawList += "Rectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawFilledRectangle");

            m_host.AddScriptLPS(1);
            drawList += "FillRectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledPolygon(string drawList, LSL_List x, LSL_List y)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawFilledPolygon");

            m_host.AddScriptLPS(1);

            if (x.Length != y.Length || x.Length < 3)
            {
                return "";
            }
            drawList += "FillPolygon " + x.GetLSLStringItem(0) + "," + y.GetLSLStringItem(0);
            for (int i = 1; i < x.Length; i++)
            {
                drawList += "," + x.GetLSLStringItem(i) + "," + y.GetLSLStringItem(i);
            }
            drawList += "; ";
            return drawList;
        }

        public string osDrawPolygon(string drawList, LSL_List x, LSL_List y)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawFilledPolygon");

            m_host.AddScriptLPS(1);

            if (x.Length != y.Length || x.Length < 3)
            {
                return "";
            }
            drawList += "Polygon " + x.GetLSLStringItem(0) + "," + y.GetLSLStringItem(0);
            for (int i = 1; i < x.Length; i++)
            {
                drawList += "," + x.GetLSLStringItem(i) + "," + y.GetLSLStringItem(i);
            }
            drawList += "; ";
            return drawList;
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetFontSize");

            m_host.AddScriptLPS(1);
            drawList += "FontSize "+ fontSize +"; ";
            return drawList;
        }

        public string osSetFontName(string drawList, string fontName)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetFontName");

            m_host.AddScriptLPS(1);
            drawList += "FontName "+ fontName +"; ";
            return drawList;
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenSize");

            m_host.AddScriptLPS(1);
            drawList += "PenSize " + penSize + "; ";
            return drawList;
        }

        public string osSetPenColour(string drawList, string colour)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenColour");

            m_host.AddScriptLPS(1);
            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osSetPenCap(string drawList, string direction, string type)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenColour");

            m_host.AddScriptLPS(1);
            drawList += "PenCap " + direction + "," + type + "; ";
            return drawList;
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawImage");

            m_host.AddScriptLPS(1);
            drawList +="Image " +width + "," + height+ ","+ imageUrl +"; " ;
            return drawList;
        }

        public LSL_Vector osGetDrawStringSize(string contentType, string text, string fontName, int fontSize)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osGetDrawStringSize");
            m_host.AddScriptLPS(1);

            LSL_Vector vec = new LSL_Vector(0,0,0);
            IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
            if (textureManager != null)
            {
                double xSize, ySize;
                textureManager.GetDrawStringSize(contentType, text, fontName, fontSize,
                                                 out xSize, out ySize);
                vec.x = xSize;
                vec.y = ySize;
            }
            return vec;
        }

        public void osSetStateEvents(int events)
        {
            // This function is a hack. There is no reason for it's existence
            // anymore, since state events now work properly.
            // It was probably added as a crutch or debugging aid, and
            // should be removed
            //
            CheckThreatLevel(ThreatLevel.High, "osSetStateEvents");

            m_host.SetScriptEvents(m_itemID, events);
        }

        public void osSetRegionWaterHeight(double height)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetRegionWaterHeight");

            m_host.AddScriptLPS(1);
            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.Permissions.IsGod(m_host.OwnerID))
            {
                World.EventManager.TriggerRequestChangeWaterHeight((float)height);
            }
        }

        /// <summary>
        /// Changes the Region Sun Settings, then Triggers a Sun Update
        /// </summary>
        /// <param name="useEstateSun">True to use Estate Sun instead of Region Sun</param>
        /// <param name="sunFixed">True to keep the sun stationary</param>
        /// <param name="sunHour">The "Sun Hour" that is desired, 0...24, with 0 just after SunRise</param>
        public void osSetRegionSunSettings(bool useEstateSun, bool sunFixed, double sunHour)
        {
            CheckThreatLevel(ThreatLevel.Nuisance, "osSetRegionSunSettings");

            m_host.AddScriptLPS(1);
            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.Permissions.IsGod(m_host.OwnerID))
            {
                while (sunHour > 24.0)
                    sunHour -= 24.0;

                while (sunHour < 0)
                    sunHour += 24.0;


                World.RegionInfo.RegionSettings.UseEstateSun = useEstateSun;
                World.RegionInfo.RegionSettings.SunPosition  = sunHour + 6; // LL Region Sun Hour is 6 to 30
                World.RegionInfo.RegionSettings.FixedSun     = sunFixed;
                World.RegionInfo.RegionSettings.Save();

                World.EventManager.TriggerEstateToolsSunUpdate(World.RegionInfo.RegionHandle, sunFixed, useEstateSun, (float)sunHour);
            }
        }

        /// <summary>
        /// Changes the Estate Sun Settings, then Triggers a Sun Update
        /// </summary>
        /// <param name="sunFixed">True to keep the sun stationary, false to use global time</param>
        /// <param name="sunHour">The "Sun Hour" that is desired, 0...24, with 0 just after SunRise</param>
        public void osSetEstateSunSettings(bool sunFixed, double sunHour)
        {
            CheckThreatLevel(ThreatLevel.Nuisance, "osSetEstateSunSettings");

            m_host.AddScriptLPS(1);
            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.Permissions.IsGod(m_host.OwnerID))
            {
                while (sunHour > 24.0)
                    sunHour -= 24.0;

                while (sunHour < 0)
                    sunHour += 24.0;

                World.RegionInfo.EstateSettings.UseGlobalTime = !sunFixed;
                World.RegionInfo.EstateSettings.SunPosition = sunHour;
                World.RegionInfo.EstateSettings.FixedSun = sunFixed;
                World.RegionInfo.EstateSettings.Save();

                World.EventManager.TriggerEstateToolsSunUpdate(World.RegionInfo.RegionHandle, sunFixed, World.RegionInfo.RegionSettings.UseEstateSun, (float)sunHour);
            }
        }

        /// <summary>
        /// Return the current Sun Hour 0...24, with 0 being roughly sun-rise
        /// </summary>
        /// <returns></returns>
        public double osGetCurrentSunHour()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetCurrentSunHour");

            m_host.AddScriptLPS(1);

            // Must adjust for the fact that Region Sun Settings are still LL offset
            double sunHour = World.RegionInfo.RegionSettings.SunPosition - 6;

            // See if the sun module has registered itself, if so it's authoritative
            ISunModule module = World.RequestModuleInterface<ISunModule>();
            if (module != null)
            {
                sunHour = module.GetCurrentSunHour();
            }

            return sunHour;
        }

        public double osSunGetParam(string param)
        {
            CheckThreatLevel(ThreatLevel.None, "osSunGetParam");
            m_host.AddScriptLPS(1);

            double value = 0.0;

            ISunModule module = World.RequestModuleInterface<ISunModule>();
            if (module != null)
            {
                value = module.GetSunParameter(param);
            }

            return value;
        }

        public void osSunSetParam(string param, double value)
        {
            CheckThreatLevel(ThreatLevel.None, "osSunSetParam");
            m_host.AddScriptLPS(1);

            ISunModule module = World.RequestModuleInterface<ISunModule>();
            if (module != null)
            {
                module.SetSunParameter(param, value);
            }

        }


        public string osWindActiveModelPluginName()
        {
            CheckThreatLevel(ThreatLevel.None, "osWindActiveModelPluginName");
            m_host.AddScriptLPS(1);

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module != null)
            {
                return module.WindActiveModelPluginName;
            }

            return String.Empty;
        }

        public void osWindParamSet(string plugin, string param, float value)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osWindParamSet");
            m_host.AddScriptLPS(1);

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module != null)
            {
                try
                {
                    module.WindParamSet(plugin, param, value);
                }
                catch (Exception) { }
            }
        }

        public float osWindParamGet(string plugin, string param)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osWindParamGet");
            m_host.AddScriptLPS(1);

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module != null)
            {
                return module.WindParamGet(plugin, param);
            }

            return 0.0f;
        }



        public double osList2Double(LSL_Types.list src, int index)
        {
            // There is really no double type in OSSL. C# and other
            // have one, but the current implementation of LSL_Types.list
            // is not allowed to contain any.
            // This really should be removed.
            //
            CheckThreatLevel(ThreatLevel.None, "osList2Double");

            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0.0;
            }
            return Convert.ToDouble(src.Data[index]);
        }

        public void osSetParcelMediaURL(string url)
        {
            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMediaURL");

            m_host.AddScriptLPS(1);

            ILandObject land
                = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if (land.LandData.OwnerID != m_host.OwnerID)
                return;

            land.SetMediaUrl(url);
        }
        
        public void osSetParcelSIPAddress(string SIPAddress)
        {
            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMediaURL");

            m_host.AddScriptLPS(1);
            

            ILandObject land
                = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if (land.LandData.OwnerID != m_host.OwnerID)
            {
                OSSLError("osSetParcelSIPAddress: Sorry, you need to own the land to use this function");
                return;
            }
            
            // get the voice module
            IVoiceModule voiceModule = World.RequestModuleInterface<IVoiceModule>();
            
            if (voiceModule != null) 
                voiceModule.setLandSIPAddress(SIPAddress,land.LandData.GlobalID);
            else
                OSSLError("osSetParcelSIPAddress: No voice module enabled for this land");
            
            
        }

        public string osGetScriptEngineName()
        {
            // This gets a "high" because knowing the engine may be used
            // to exploit engine-specific bugs or induce usage patterns
            // that trigger engine-specific failures.
            // Besides, public grid users aren't supposed to know.
            //
            CheckThreatLevel(ThreatLevel.High, "osGetScriptEngineName");

            m_host.AddScriptLPS(1);

            int scriptEngineNameIndex = 0;

            if (!String.IsNullOrEmpty(m_ScriptEngine.ScriptEngineName))
            {
                // parse off the "ScriptEngine."
                scriptEngineNameIndex = m_ScriptEngine.ScriptEngineName.IndexOf(".", scriptEngineNameIndex);
                scriptEngineNameIndex++; // get past delimiter

                int scriptEngineNameLength = m_ScriptEngine.ScriptEngineName.Length - scriptEngineNameIndex;

                // create char array then a string that is only the script engine name
                Char[] scriptEngineNameCharArray = m_ScriptEngine.ScriptEngineName.ToCharArray(scriptEngineNameIndex, scriptEngineNameLength);
                String scriptEngineName = new String(scriptEngineNameCharArray);

                return scriptEngineName;
            }
            else
            {
                return String.Empty;
            }
        }

        public string osGetSimulatorVersion()
        {
            // High because it can be used to target attacks to known weaknesses
            // This would allow a new class of griefer scripts that don't even
            // require their user to know what they are doing (see script
            // kiddie)
            //
            CheckThreatLevel(ThreatLevel.High,"osGetSimulatorVersion");
            m_host.AddScriptLPS(1);
            return m_ScriptEngine.World.GetSimulatorVersion();
        }

        public Hashtable osParseJSON(string JSON)
        {
            CheckThreatLevel(ThreatLevel.None, "osParseJSON");

            m_host.AddScriptLPS(1);

            // see http://www.json.org/ for more details on JSON

            string currentKey = null;
            Stack objectStack = new Stack(); // objects in JSON can be nested so we need to keep a track of this
            Hashtable jsondata = new Hashtable(); // the hashtable to be returned
            int i = 0;
            try
            {

                // iterate through the serialised stream of tokens and store at the right depth in the hashtable
                // the top level hashtable may contain more nested hashtables within it each containing an objects representation
                for (i = 0; i < JSON.Length; i++)
                {

                    // m_log.Debug(""+JSON[i]);
                    switch (JSON[i])
                    {
                        case '{':
                            // create hashtable and add it to the stack or array if we are populating one, we can have a lot of nested objects in JSON

                            Hashtable currentObject = new Hashtable();
                            if (objectStack.Count == 0) // the stack should only be empty for the first outer object
                            {

                                objectStack.Push(jsondata);
                            }
                            else if (objectStack.Peek().ToString() == "System.Collections.ArrayList")
                            {
                                // add it to the parent array
                                ((ArrayList)objectStack.Peek()).Add(currentObject);
                                objectStack.Push(currentObject);
                            }
                            else
                            {
                                // add it to the parent hashtable
                                ((Hashtable)objectStack.Peek()).Add(currentKey,currentObject);
                                objectStack.Push(currentObject);
                            }

                            // clear the key
                            currentKey = null;
                            break;

                        case '}':
                            // pop the hashtable off the stack
                            objectStack.Pop();
                            break;

                        case '"':// string boundary

                            string tokenValue = "";
                            i++; // move to next char

                            // just loop through until the next quote mark storing the string, ignore quotes with pre-ceding \
                            while (JSON[i] != '"')
                            {
                                tokenValue += JSON[i];

                                // handle escaped double quotes \"
                                if (JSON[i] == '\\' && JSON[i+1] == '"')
                                {
                                    tokenValue += JSON[i+1];
                                    i++;
                                }
                                i++;

                            }

                            // ok we've got a string, if we've got an array on the top of the stack then we store it
                            if (objectStack.Peek().ToString() == "System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(tokenValue);
                            }
                            else if (currentKey == null)   // no key stored and its not an array this must be a key so store it
                            {
                                currentKey = tokenValue;
                            }
                            else
                            {
                                // we have a key so lets store this value
                                ((Hashtable)objectStack.Peek()).Add(currentKey,tokenValue);
                                // now lets clear the key, we're done with it and moving on
                                currentKey = null;
                            }

                            break;

                        case ':':// key : value separator
                            // just ignore
                            break;

                        case ' ':// spaces
                            // just ignore
                            break;

                        case '[': // array start
                            ArrayList currentArray = new ArrayList();

                            if (objectStack.Peek().ToString() == "System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(currentArray);
                            }
                            else
                            {
                                ((Hashtable)objectStack.Peek()).Add(currentKey,currentArray);
                                // clear the key
                                currentKey = null;
                            }
                            objectStack.Push(currentArray);

                            break;

                        case ',':// seperator
                            // just ignore
                            break;

                        case ']'://Array end
                            // pop the array off the stack
                            objectStack.Pop();
                            break;

                        case 't': // we've found a character start not in quotes, it must be a boolean true

                            if (objectStack.Peek().ToString() == "System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(true);
                            }
                            else
                            {
                                ((Hashtable)objectStack.Peek()).Add(currentKey,true);
                                currentKey = null;
                            }

                            //advance the counter to the letter 'e'
                            i = i + 3;
                            break;

                        case 'f': // we've found a character start not in quotes, it must be a boolean false

                            if (objectStack.Peek().ToString() == "System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(false);
                            }
                            else
                            {
                                ((Hashtable)objectStack.Peek()).Add(currentKey,false);
                                currentKey = null;
                            }
                            //advance the counter to the letter 'e'
                            i = i + 4;
                            break;

                        case '\n':// carriage return
                            // just ignore
                            break;

                        case '\r':// carriage return
                            // just ignore
                            break;

                        default:
                            // ok here we're catching all numeric types int,double,long we might want to spit these up mr accurately
                            // but for now we'll just do them as strings

                            string numberValue = "";

                            // just loop through until the next known marker quote mark storing the string
                            while (JSON[i] != '"' && JSON[i] != ',' && JSON[i] != ']' && JSON[i] != '}' && JSON[i] != ' ')
                            {
                                numberValue += "" + JSON[i++];
                            }

                            i--; // we want to process this caracter that marked the end of this string in the main loop

                            // ok we've got a string, if we've got an array on the top of the stack then we store it
                            if (objectStack.Peek().ToString() == "System.Collections.ArrayList")
                            {
                                ((ArrayList)objectStack.Peek()).Add(numberValue);
                            }
                            else
                            {
                                // we have a key so lets store this value
                                ((Hashtable)objectStack.Peek()).Add(currentKey,numberValue);
                                // now lets clear the key, we're done with it and moving on
                                currentKey = null;
                            }

                            break;
                    }
                }
            }
            catch(Exception)
            {
                OSSLError("osParseJSON: The JSON string is not valid " + JSON) ;
            }

            return jsondata;
        }

        // send a message to to object identified by the given UUID, a script in the object must implement the dataserver function
        // the dataserver function is passed the ID of the calling function and a string message
        public void osMessageObject(LSL_Key objectUUID, string message)
        {
            CheckThreatLevel(ThreatLevel.Low, "osMessageObject");
            m_host.AddScriptLPS(1);

            object[] resobj = new object[] { new LSL_Types.LSLString(m_host.UUID.ToString()), new LSL_Types.LSLString(message) };

            SceneObjectPart sceneOP = World.GetSceneObjectPart(new UUID(objectUUID));

            m_ScriptEngine.PostObjectEvent(
                sceneOP.LocalId, new EventParams(
                    "dataserver", resobj, new DetectParams[0]));
        }


        // This needs ThreatLevel high. It is an excellent griefer tool,
        // In a loop, it can cause asset bloat and DOS levels of asset
        // writes.
        //
        public void osMakeNotecard(string notecardName, LSL_Types.list contents)
        {
            CheckThreatLevel(ThreatLevel.High, "osMakeNotecard");
            m_host.AddScriptLPS(1);

            // Create new asset
            AssetBase asset = new AssetBase();
            asset.Name = notecardName;
            asset.Description = "Script Generated Notecard";
            asset.Type = 7;
            asset.FullID = UUID.Random();
            string notecardData = "";

            for (int i = 0; i < contents.Length; i++) {
                notecardData += contents.GetLSLStringItem(i) + "\n";
            }

            int textLength = notecardData.Length;
            notecardData = "Linden text version 2\n{\nLLEmbeddedItems version 1\n{\ncount 0\n}\nText length "
            + textLength.ToString() + "\n" + notecardData + "}\n";

            asset.Data = Util.UTF8.GetBytes(notecardData);
            World.AssetService.Store(asset);

            // Create Task Entry
            TaskInventoryItem taskItem=new TaskInventoryItem();

            taskItem.ResetIDs(m_host.UUID);
            taskItem.ParentID = m_host.UUID;
            taskItem.CreationDate = (uint)Util.UnixTimeSinceEpoch();
            taskItem.Name = asset.Name;
            taskItem.Description = asset.Description;
            taskItem.Type = (int)AssetType.Notecard;
            taskItem.InvType = (int)InventoryType.Notecard;
            taskItem.OwnerID = m_host.OwnerID;
            taskItem.CreatorID = m_host.OwnerID;
            taskItem.BasePermissions = (uint)PermissionMask.All;
            taskItem.CurrentPermissions = (uint)PermissionMask.All;
            taskItem.EveryonePermissions = 0;
            taskItem.NextPermissions = (uint)PermissionMask.All;
            taskItem.GroupID = m_host.GroupID;
            taskItem.GroupPermissions = 0;
            taskItem.Flags = 0;
            taskItem.PermsGranter = UUID.Zero;
            taskItem.PermsMask = 0;
            taskItem.AssetID = asset.FullID;

            m_host.Inventory.AddInventoryItem(taskItem, false);
        }


        /*Instead of using the LSL Dataserver event to pull notecard data, 
                 this will simply read the requested line and return its data as a string.
         
                 Warning - due to the synchronous method this function uses to fetch assets, its use 
                           may be dangerous and unreliable while running in grid mode.
                */
        public string osGetNotecardLine(string name, int line)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osGetNotecardLine");
            m_host.AddScriptLPS(1);

            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(name, out assetID))
            {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
                {
                    if (item.Type == 7 && item.Name == name)
                    {
                        assetID = item.AssetID;
                    }
                }
            }

            if (assetID == UUID.Zero)
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return "ERROR!";
            }

            if (!NotecardCache.IsCached(assetID))
            {
                AssetBase a = World.AssetService.Get(assetID.ToString());
                if (a != null)
                {
                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                    string data = enc.GetString(a.Data);
                    NotecardCache.Cache(assetID, data);
                }
                else
                {
                    OSSLShoutError("Notecard '" + name + "' could not be found.");
                    return "ERROR!";
                }
            };

            return NotecardCache.GetLine(assetID, line, 255);


        }

        /*Instead of using the LSL Dataserver event to pull notecard data line by line, 
          this will simply read the entire notecard and return its data as a string.
         
          Warning - due to the synchronous method this function uses to fetch assets, its use 
                    may be dangerous and unreliable while running in grid mode.
         */

        public string osGetNotecard(string name)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osGetNotecard");
            m_host.AddScriptLPS(1);

            UUID assetID = UUID.Zero;
            string NotecardData = "";

            if (!UUID.TryParse(name, out assetID))
            {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
                {
                    if (item.Type == 7 && item.Name == name)
                    {
                        assetID = item.AssetID;
                    }
                }
            }

            if (assetID == UUID.Zero)
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return "ERROR!";
            }

            if (!NotecardCache.IsCached(assetID))
            {
                AssetBase a = World.AssetService.Get(assetID.ToString());
                if (a != null)
                {
                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                    string data = enc.GetString(a.Data);
                    NotecardCache.Cache(assetID, data);
                }
                else
                {
                    OSSLShoutError("Notecard '" + name + "' could not be found.");
                    return "ERROR!";
                }
            };

            for (int count = 0; count < NotecardCache.GetLines(assetID); count++)
            {
                NotecardData += NotecardCache.GetLine(assetID, count, 255) + "\n";
            }

            return NotecardData;


        }

        /*Instead of using the LSL Dataserver event to pull notecard data, 
          this will simply read the number of note card lines and return this data as an integer.
          
          Warning - due to the synchronous method this function uses to fetch assets, its use 
                    may be dangerous and unreliable while running in grid mode.
         */

        public int osGetNumberOfNotecardLines(string name)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osGetNumberOfNotecardLines");
            m_host.AddScriptLPS(1);

            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(name, out assetID))
            {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
                {
                    if (item.Type == 7 && item.Name == name)
                    {
                        assetID = item.AssetID;
                    }
                }
            }

            if (assetID == UUID.Zero)
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return -1;
            }

            if (!NotecardCache.IsCached(assetID))
            {
                AssetBase a = World.AssetService.Get(assetID.ToString());
                if (a != null)
                {
                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                    string data = enc.GetString(a.Data);
                    NotecardCache.Cache(assetID, data);
                }
                else
                {
                    OSSLShoutError("Notecard '" + name + "' could not be found.");
                    return -1;
                }
            };

            return NotecardCache.GetLines(assetID);


        }

        public string osAvatarName2Key(string firstname, string lastname)
        {
            CheckThreatLevel(ThreatLevel.Low, "osAvatarName2Key");

            CachedUserInfo userInfo = World.CommsManager.UserProfileCacheService.GetUserDetails(firstname, lastname);

            if (null == userInfo)
            {
                return UUID.Zero.ToString();
            }
            else
            {
                return userInfo.UserProfile.ID.ToString();
            }
        }

        public string osKey2Name(string id)
        {
            CheckThreatLevel(ThreatLevel.Low, "osKey2Name");
            UUID key = new UUID();

            if (UUID.TryParse(id, out key))
            {
                CachedUserInfo userInfo = World.CommsManager.UserProfileCacheService.GetUserDetails(key);

                if (null == userInfo)
                {
                    return "";
                }
                else
                {
                    return userInfo.UserProfile.Name;
                }
            }
            else
            {
                return "";
            }

        }

        /// Threat level is Moderate because intentional abuse, for instance
        /// scripts that are written to be malicious only on one grid,
        /// for instance in a HG scenario, are a distinct possibility.
        ///
        /// Use value from the config file and return it.
        ///
        public string osGetGridNick()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridNick");
            m_host.AddScriptLPS(1);
            string nick = "hippogrid";
            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["GridInfo"] != null)
                nick = config.Configs["GridInfo"].GetString("gridnick", nick);
            return nick;
        }

        public string osGetGridName()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridName");
            m_host.AddScriptLPS(1);
            string name = "the lost continent of hippo";
            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["GridInfo"] != null)
                name = config.Configs["GridInfo"].GetString("gridname", name);
            return name;
        }

        public string osGetGridLoginURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridLoginURI");
            m_host.AddScriptLPS(1);
            string loginURI = "http://127.0.0.1:9000/";
            IConfigSource config = new IniConfigSource(Application.iniFilePath);
            if (config.Configs["GridInfo"] != null)
                loginURI = config.Configs["GridInfo"].GetString("login", loginURI);
            return loginURI;
        }

        public LSL_String osFormatString(string str, LSL_List strings)
        {
            CheckThreatLevel(ThreatLevel.Low, "osFormatString");
            m_host.AddScriptLPS(1);

            return String.Format(str, strings.Data);
        }

        public LSL_List osMatchString(string src, string pattern, int start)
        {
            CheckThreatLevel(ThreatLevel.High, "osMatchString");
            m_host.AddScriptLPS(1);

            LSL_List result = new LSL_List();

            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            if (start < 0)
            {
                start = src.Length + start;
            }

            if (start < 0 || start >= src.Length)
            {
                return result;  // empty list
            }

            // Find matches beginning at start position
            Regex matcher = new Regex(pattern);
            Match match = matcher.Match(src, start);
            if (match.Success)
            {
                foreach (System.Text.RegularExpressions.Group g in match.Groups)
                {
                    if (g.Success)
                    {
                        result.Add(g.Value);
                        result.Add(g.Index);
                    }
                }
            }

            return result;
        }

        public string osLoadedCreationDate()
        {
            CheckThreatLevel(ThreatLevel.Low, "osLoadedCreationDate");
            m_host.AddScriptLPS(1);

            return World.RegionInfo.RegionSettings.LoadedCreationDate;
        }

        public string osLoadedCreationTime()
        {
            CheckThreatLevel(ThreatLevel.Low, "osLoadedCreationTime");
            m_host.AddScriptLPS(1);

            return World.RegionInfo.RegionSettings.LoadedCreationTime;
        }

        public string osLoadedCreationID()
        {
            CheckThreatLevel(ThreatLevel.Low, "osLoadedCreationID");
            m_host.AddScriptLPS(1);

            return World.RegionInfo.RegionSettings.LoadedCreationID;
        }
        
        // Threat level is 'Low' because certain users could possibly be tricked into
        // dropping an unverified script into one of their own objects, which could
        // then gather the physical construction details of the object and transmit it
        // to an unscrupulous third party, thus permitting unauthorized duplication of
        // the object's form.
        //
        public LSL_List osGetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            CheckThreatLevel(ThreatLevel.High, "osGetLinkPrimitiveParams");
            m_host.AddScriptLPS(1);
            InitLSL();
            LSL_List retVal = new LSL_List();
            List<SceneObjectPart> parts = ((LSL_Api)m_LSL_Api).GetLinkParts(linknumber);
            foreach (SceneObjectPart part in parts)
            {
                retVal += ((LSL_Api)m_LSL_Api).GetLinkPrimitiveParams(part, rules);
            }
            return retVal;
        }

        public LSL_Key osNpcCreate(string firstname, string lastname, LSL_Vector position, LSL_Key cloneFrom)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcCreate");
            //QueueUserWorkItem 

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID x = module.CreateNPC(firstname,
                                          lastname,
                                          new Vector3((float) position.x, (float) position.y, (float) position.z),
                                          World,
                                          new UUID(cloneFrom));

                return new LSL_Key(x.ToString());
            }
            return new LSL_Key(UUID.Zero.ToString());
        }

        public void osNpcMoveTo(LSL_Key npc, LSL_Vector position)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcMoveTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                Vector3 pos = new Vector3((float) position.x, (float) position.y, (float) position.z);
                module.Autopilot(new UUID(npc.m_string), World, pos);
            }
        }

        public void osNpcSay(LSL_Key npc, string message)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSay");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                module.Say(new UUID(npc.m_string), World, message);
            }
        }

        public void osNpcRemove(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcRemove");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                module.DeleteNPC(new UUID(npc.m_string), World);
            }
        }
    }
}
