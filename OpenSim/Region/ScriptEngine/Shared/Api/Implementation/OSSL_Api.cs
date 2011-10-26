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
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Net;
using System.Threading;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;

using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
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

    class FunctionPerms
    {
        public List<UUID> AllowedCreators;
        public List<UUID> AllowedOwners;
        public List<string> AllowedOwnerClasses;

        public FunctionPerms()
        {
            AllowedCreators = new List<UUID>();
            AllowedOwners = new List<UUID>();
            AllowedOwnerClasses = new List<string>();
        }
    }

    [Serializable]
    public class OSSL_Api : MarshalByRefObject, IOSSL_Api, IScriptApi
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        internal IScriptEngine m_ScriptEngine;
        internal ILSL_Api m_LSL_Api = null; // get a reference to the LSL API so we can call methods housed there
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool m_OSFunctionsEnabled = false;
        internal ThreatLevel m_MaxThreatLevel = ThreatLevel.VeryLow;
        internal float m_ScriptDelayFactor = 1.0f;
        internal float m_ScriptDistanceFactor = 1.0f;
        internal bool m_debuggerSafe = false;
        internal Dictionary<string, FunctionPerms > m_FunctionPerms = new Dictionary<string, FunctionPerms >();

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;
            m_debuggerSafe = m_ScriptEngine.Config.GetBoolean("DebuggerSafe", false);

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
            if (m_debuggerSafe)
            {
                OSSLShoutError(msg);
            }
            else
            {
                throw new Exception("OSSL Runtime Error: " + msg);
            }
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
                FunctionPerms perms = new FunctionPerms();
                m_FunctionPerms[function] = perms;

                string ownerPerm = m_ScriptEngine.Config.GetString("Allow_" + function, "");
                string creatorPerm = m_ScriptEngine.Config.GetString("Creators_" + function, "");
                if (ownerPerm == "" && creatorPerm == "")
                {
                    // Default behavior
                    perms.AllowedOwners = null;
                    perms.AllowedCreators = null;
                    perms.AllowedOwnerClasses = null;
                }
                else
                {
                    bool allowed;

                    if (bool.TryParse(ownerPerm, out allowed))
                    {
                        // Boolean given
                        if (allowed)
                        {
                            // Allow globally
                            perms.AllowedOwners.Add(UUID.Zero);
                        }
                    }
                    else
                    {
                        string[] ids = ownerPerm.Split(new char[] {','});
                        foreach (string id in ids)
                        {
                            string current = id.Trim();
                            if (current.ToUpper() == "PARCEL_GROUP_MEMBER" || current.ToUpper() == "PARCEL_OWNER" || current.ToUpper() == "ESTATE_MANAGER" || current.ToUpper() == "ESTATE_OWNER")
                            {
                                if (!perms.AllowedOwnerClasses.Contains(current))
                                    perms.AllowedOwnerClasses.Add(current.ToUpper());
                            }
                            else
                            {
                                UUID uuid;

                                if (UUID.TryParse(current, out uuid))
                                {
                                    if (uuid != UUID.Zero)
                                        perms.AllowedOwners.Add(uuid);
                                }
                            }
                        }

                        ids = creatorPerm.Split(new char[] {','});
                        foreach (string id in ids)
                        {
                            string current = id.Trim();
                            UUID uuid;

                            if (UUID.TryParse(current, out uuid))
                            {
                                if (uuid != UUID.Zero)
                                    perms.AllowedCreators.Add(uuid);
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
            if (m_FunctionPerms[function].AllowedOwners == null)
            {
                // Allow / disallow by threat level
                if (level > m_MaxThreatLevel)
                    OSSLError(
                        String.Format(
                            "{0} permission denied.  Allowed threat level is {1} but function threat level is {2}.",
                            function, m_MaxThreatLevel, level));
            }
            else
            {
                if (!m_FunctionPerms[function].AllowedOwners.Contains(UUID.Zero))
                {
                    // Not anyone. Do detailed checks
                    if (m_FunctionPerms[function].AllowedOwners.Contains(m_host.OwnerID))
                    {
                        // prim owner is in the list of allowed owners
                        return;
                    }

                    TaskInventoryItem ti = m_host.Inventory.GetInventoryItem(m_itemID);
                    if (ti == null)
                    {
                        OSSLError(
                            String.Format("{0} permission error. Can't find script in prim inventory.",
                            function));
                    }

                    UUID ownerID = ti.OwnerID;

                    //OSSL only may be used if objet is in the same group as the parcel
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("PARCEL_GROUP_MEMBER"))
                    {
                        ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

                        if (land.LandData.GroupID == ti.GroupID && land.LandData.GroupID != UUID.Zero)
                        {
                            return;
                        }
                    }

                    //Only Parcelowners may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("PARCEL_OWNER"))
                    {
                        ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

                        if (land.LandData.OwnerID == ownerID)
                        {
                            return;
                        }
                    }

                    //Only Estate Managers may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("ESTATE_MANAGER"))
                    {
                        //Only Estate Managers may use the function
                        if (World.RegionInfo.EstateSettings.IsEstateManager(ownerID) && World.RegionInfo.EstateSettings.EstateOwner != ownerID)
                        {
                            return;
                        }
                    }

                    //Only regionowners may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("ESTATE_OWNER"))
                    {
                        if (World.RegionInfo.EstateSettings.EstateOwner == ownerID)
                        {
                            return;
                        }
                    }

                    if (!m_FunctionPerms[function].AllowedCreators.Contains(ti.CreatorID))
                        OSSLError(
                            String.Format("{0} permission denied. Script creator is not in the list of users allowed to execute this function and prim owner also has no permission.",
                            function));
                    if (ti.CreatorID != ownerID)
                    {
                        if ((ti.CurrentPermissions & (uint)PermissionMask.Modify) != 0)
                            OSSLError(
                                String.Format("{0} permission denied. Script permissions error.",
                                function));

                    }
                }
            }
        }

        internal void OSSLDeprecated(string function, string replacement)
        {
            OSSLShoutError(string.Format("Use of function {0} is deprecated. Use {1} instead.", function, replacement));
        }

        protected void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        public LSL_Integer osSetTerrainHeight(int x, int y, double val)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetTerrainHeight");
            return SetTerrainHeight(x, y, val);
        }

        public LSL_Integer osTerrainSetHeight(int x, int y, double val)
        {
            CheckThreatLevel(ThreatLevel.High, "osTerrainSetHeight");
            OSSLDeprecated("osTerrainSetHeight", "osSetTerrainHeight");
            return SetTerrainHeight(x, y, val);
        }

        private LSL_Integer SetTerrainHeight(int x, int y, double val)
        {
            m_host.AddScriptLPS(1);
            if (x > ((int)Constants.RegionSize - 1) || x < 0 || y > ((int)Constants.RegionSize - 1) || y < 0)
                OSSLError("osSetTerrainHeight: Coordinate out of bounds");

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

        public LSL_Float osGetTerrainHeight(int x, int y)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetTerrainHeight");
            return GetTerrainHeight(x, y);
        }

        public LSL_Float osTerrainGetHeight(int x, int y)
        {
            CheckThreatLevel(ThreatLevel.None, "osTerrainGetHeight");
            OSSLDeprecated("osTerrainGetHeight", "osGetTerrainHeight");
            return GetTerrainHeight(x, y);
        }

        private LSL_Float GetTerrainHeight(int x, int y)
        {
            m_host.AddScriptLPS(1);
            if (x > ((int)Constants.RegionSize - 1) || x < 0 || y > ((int)Constants.RegionSize - 1) || y < 0)
                OSSLError("osGetTerrainHeight: Coordinate out of bounds");

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

            IRestartModule restartModule = World.RequestModuleInterface<IRestartModule>();
            m_host.AddScriptLPS(1);
            if (World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false) && (restartModule != null))
            {
                if (seconds < 15)
                {
                    restartModule.AbortRestart("Restart aborted");
                    return 1;
                }

                List<int> times = new List<int>();
                while (seconds > 0)
                {
                    times.Add((int)seconds);
                    if (seconds > 300)
                        seconds -= 120;
                    else if (seconds > 30)
                        seconds -= 30;
                    else
                        seconds -= 15;
                }

                restartModule.ScheduleRestart(UUID.Zero, "Region will restart in {0}", times.ToArray(), true);
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
                EntityBase entity;
                if (World.Entities.TryGetValue(target, out entity))
                {
                    if (entity is SceneObjectGroup)
                        ((SceneObjectGroup)entity).Rotation = rotation;
                    else if (entity is ScenePresence)
                        ((ScenePresence)entity).Rotation = rotation;
                }
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

            m_host.ParentGroup.RootPart.SetFloatOnWater(floatYN);
        }

        // Teleport functions
        public void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.High, "osTeleportAgent");

            TeleportAgent(agent, regionName, position, lookat, false);
        }

        private void TeleportAgent(string agent, string regionName,
            LSL_Types.Vector3 position, LSL_Types.Vector3 lookat, bool relaxRestrictions)
        {
            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // For osTeleportAgent, agent must be over owners land to avoid abuse
                    // For osTeleportOwner, this restriction isn't necessary
                    if (relaxRestrictions ||
                        m_host.OwnerID
                        == World.LandChannel.GetLandObject(
                            presence.AbsolutePosition.X, presence.AbsolutePosition.Y).LandData.OwnerID)
                    {
                        // We will launch the teleport on a new thread so that when the script threads are terminated
                        // before teleport in ScriptInstance.GetXMLState(), we don't end up aborting the one doing the teleporting.                        
                        Util.FireAndForget(
                            o => World.RequestTeleportLocation(presence.ControllingClient, regionName,
                                new Vector3((float)position.x, (float)position.y, (float)position.z),
                                new Vector3((float)lookat.x, (float)lookat.y, (float)lookat.z), (uint)TPFlags.ViaLocation));

                        ScriptSleep(5000);
                    }
                }
            }
        }

        public void osTeleportAgent(string agent, int regionX, int regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.High, "osTeleportAgent");

            TeleportAgent(agent, regionX, regionY, position, lookat, false);
        }

        private void TeleportAgent(string agent, int regionX, int regionY,
            LSL_Types.Vector3 position, LSL_Types.Vector3 lookat, bool relaxRestrictions)
        {
            ulong regionHandle = Util.UIntsToLong(((uint)regionX * (uint)Constants.RegionSize), ((uint)regionY * (uint)Constants.RegionSize));

            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // For osTeleportAgent, agent must be over owners land to avoid abuse
                    // For osTeleportOwner, this restriction isn't necessary
                    if (relaxRestrictions ||
                        m_host.OwnerID
                        == World.LandChannel.GetLandObject(
                            presence.AbsolutePosition.X, presence.AbsolutePosition.Y).LandData.OwnerID)
                    {
                        // We will launch the teleport on a new thread so that when the script threads are terminated
                        // before teleport in ScriptInstance.GetXMLState(), we don't end up aborting the one doing the teleporting.
                        Util.FireAndForget(
                            o => World.RequestTeleportLocation(presence.ControllingClient, regionHandle,
                                new Vector3((float)position.x, (float)position.y, (float)position.z),
                                new Vector3((float)lookat.x, (float)lookat.y, (float)lookat.z), (uint)TPFlags.ViaLocation));

                        ScriptSleep(5000);
                    }
                }
            }
        }

        public void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            osTeleportAgent(agent, World.RegionInfo.RegionName, position, lookat);
        }

        public void osTeleportOwner(string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // Threat level None because this is what can already be done with the World Map in the viewer
            CheckThreatLevel(ThreatLevel.None, "osTeleportOwner");

            TeleportAgent(m_host.OwnerID.ToString(), regionName, position, lookat, true);
        }

        public void osTeleportOwner(LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            osTeleportOwner(World.RegionInfo.RegionName, position, lookat);
        }

        public void osTeleportOwner(int regionX, int regionY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            CheckThreatLevel(ThreatLevel.None, "osTeleportOwner");

            TeleportAgent(m_host.OwnerID.ToString(), regionX, regionY, position, lookat, true);
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
            World.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (!sp.IsChildAgent)
                    result.Add(new LSL_String(sp.Name));
            });
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
                    m_host.TaskInventory.LockItemsForRead(true);
                    foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
                    {
                        if (inv.Value.Name == animation)
                        {
                            if (inv.Value.Type == (int)AssetType.Animation)
                                animID = inv.Value.AssetID;
                            continue;
                        }
                    }
                    m_host.TaskInventory.LockItemsForRead(false);
                    if (animID == UUID.Zero)
                        target.Animator.AddAnimation(animation, m_host.UUID);
                    else
                        target.Animator.AddAnimation(animID, m_host.UUID);
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
                    UUID animID = UUID.Zero;
                    m_host.TaskInventory.LockItemsForRead(true);
                    foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
                    {
                        if (inv.Value.Name == animation)
                        {
                            if (inv.Value.Type == (int)AssetType.Animation)
                                animID = inv.Value.AssetID;
                            continue;
                        }
                    }
                    m_host.TaskInventory.LockItemsForRead(false);
                    
                    if (animID == UUID.Zero)
                        target.Animator.RemoveAnimation(animation);
                    else
                        target.Animator.RemoveAnimation(animID);
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
            CheckThreatLevel(ThreatLevel.None, "osDrawPolygon");

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

        public string osSetPenColor(string drawList, string color)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenColor");
            
            m_host.AddScriptLPS(1);
            drawList += "PenColor " + color + "; ";
            return drawList;
        }

        // Deprecated
        public string osSetPenColour(string drawList, string colour)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenColour");
            OSSLDeprecated("osSetPenColour", "osSetPenColor");

            m_host.AddScriptLPS(1);
            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osSetPenCap(string drawList, string direction, string type)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetPenCap");

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
            OSSLDeprecated("osSunGetParam", "osGetSunParam");
            return GetSunParam(param);
        }

        public double osGetSunParam(string param)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetSunParam");
            return GetSunParam(param);
        }

        private double GetSunParam(string param)
        {
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
            OSSLDeprecated("osSunSetParam", "osSetSunParam");
            SetSunParam(param, value);
        }

        public void osSetSunParam(string param, double value)
        {
            CheckThreatLevel(ThreatLevel.None, "osSetSunParam");
            SetSunParam(param, value);
        }

        private void SetSunParam(string param, double value)
        {
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

        public void osSetWindParam(string plugin, string param, LSL_Float value)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetWindParam");
            m_host.AddScriptLPS(1);

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module != null)
            {
                try
                {
                    module.WindParamSet(plugin, param, (float)value);
                }
                catch (Exception) { }
            }
        }

        public LSL_Float osGetWindParam(string plugin, string param)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osGetWindParam");
            m_host.AddScriptLPS(1);

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module != null)
            {
                return module.WindParamGet(plugin, param);
            }

            return 0.0f;
        }

        // Routines for creating and managing parcels programmatically
        public void osParcelJoin(LSL_Vector pos1, LSL_Vector pos2)
        {
            CheckThreatLevel(ThreatLevel.High, "osParcelJoin");
            m_host.AddScriptLPS(1);

            int startx = (int)(pos1.x < pos2.x ? pos1.x : pos2.x);
            int starty = (int)(pos1.y < pos2.y ? pos1.y : pos2.y);
            int endx = (int)(pos1.x > pos2.x ? pos1.x : pos2.x);
            int endy = (int)(pos1.y > pos2.y ? pos1.y : pos2.y);

            World.LandChannel.Join(startx,starty,endx,endy,m_host.OwnerID);
        }

        public void osParcelSubdivide(LSL_Vector pos1, LSL_Vector pos2)
        {
            CheckThreatLevel(ThreatLevel.High, "osParcelSubdivide");
            m_host.AddScriptLPS(1);

            int startx = (int)(pos1.x < pos2.x ? pos1.x : pos2.x);
            int starty = (int)(pos1.y < pos2.y ? pos1.y : pos2.y);
            int endx = (int)(pos1.x > pos2.x ? pos1.x : pos2.x);
            int endy = (int)(pos1.y > pos2.y ? pos1.y : pos2.y);

            World.LandChannel.Subdivide(startx,starty,endx,endy,m_host.OwnerID);
        }

        public void osParcelSetDetails(LSL_Vector pos, LSL_List rules)
        {
            const string functionName = "osParcelSetDetails";
            CheckThreatLevel(ThreatLevel.High, functionName);
            OSSLDeprecated(functionName, "osSetParcelDetails");
            SetParcelDetails(pos, rules, functionName);
        }

        public void osSetParcelDetails(LSL_Vector pos, LSL_List rules)
        {
            const string functionName = "osSetParcelDetails";
            CheckThreatLevel(ThreatLevel.High, functionName);
            SetParcelDetails(pos, rules, functionName);
        }

        private void SetParcelDetails(LSL_Vector pos, LSL_List rules, string functionName)
        {
            m_host.AddScriptLPS(1);

            // Get a reference to the land data and make sure the owner of the script
            // can modify it

            ILandObject startLandObject = World.LandChannel.GetLandObject((int)pos.x, (int)pos.y);
            if (startLandObject == null)
            {
                OSSLShoutError("There is no land at that location");
                return;
            }

            if (!World.Permissions.CanEditParcelProperties(m_host.OwnerID, startLandObject, GroupPowers.LandOptions))
            {
                OSSLShoutError("You do not have permission to modify the parcel");
                return;
            }

            // Create a new land data object we can modify
            LandData newLand = startLandObject.LandData.Copy();
            UUID uuid;

            // Process the rules, not sure what the impact would be of changing owner or group
            for (int idx = 0; idx < rules.Length;)
            {
                int code = rules.GetLSLIntegerItem(idx++);
                string arg = rules.GetLSLStringItem(idx++);
                switch (code)
                {
                    case ScriptBaseClass.PARCEL_DETAILS_NAME:
                        newLand.Name = arg;
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_DESC:
                        newLand.Description = arg;
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_OWNER:
                        CheckThreatLevel(ThreatLevel.VeryHigh, functionName);
                        if (UUID.TryParse(arg, out uuid))
                            newLand.OwnerID = uuid;
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_GROUP:
                        CheckThreatLevel(ThreatLevel.VeryHigh, functionName);
                        if (UUID.TryParse(arg, out uuid))
                            newLand.GroupID = uuid;
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_CLAIMDATE:
                        CheckThreatLevel(ThreatLevel.VeryHigh, functionName);
                        newLand.ClaimDate = Convert.ToInt32(arg);
                        if (newLand.ClaimDate == 0)
                            newLand.ClaimDate = Util.UnixTimeSinceEpoch();
                        break;
                 }
             }

            World.LandChannel.UpdateLandObject(newLand.LocalID,newLand);
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
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelSIPAddress");

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

        /// <summary>
        /// Send a message to to object identified by the given UUID
        /// </summary>
        /// <remarks>
        /// A script in the object must implement the dataserver function
        /// the dataserver function is passed the ID of the calling function and a string message
        /// </remarks>
        /// <param name="objectUUID"></param>
        /// <param name="message"></param>
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

        /// <summary>
        /// Write a notecard directly to the prim's inventory.
        /// </summary>
        /// <remarks>
        /// This needs ThreatLevel high. It is an excellent griefer tool,
        /// In a loop, it can cause asset bloat and DOS levels of asset
        /// writes.
        /// </remarks>
        /// <param name="notecardName">The name of the notecard to write.</param>
        /// <param name="contents">The contents of the notecard.</param>
        public void osMakeNotecard(string notecardName, LSL_Types.list contents)
        {
            CheckThreatLevel(ThreatLevel.High, "osMakeNotecard");
            m_host.AddScriptLPS(1);

            StringBuilder notecardData = new StringBuilder();

            for (int i = 0; i < contents.Length; i++)
                notecardData.Append((string)(contents.GetLSLStringItem(i) + "\n"));

            SaveNotecard(notecardName, "Script generated notecard", notecardData.ToString(), false);
        }

        /// <summary>
        /// Save a notecard to prim inventory.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description">Description of notecard</param>
        /// <param name="notecardData"></param>
        /// <param name="forceSameName">
        /// If true, then if an item exists with the same name, it is replaced.
        /// If false, then a new item is created witha slightly different name (e.g. name 1)
        /// </param>
        /// <returns>Prim inventory item created.</returns>
        protected TaskInventoryItem SaveNotecard(string name, string description, string data, bool forceSameName)
        {
            // Create new asset
            AssetBase asset = new AssetBase(UUID.Random(), name, (sbyte)AssetType.Notecard, m_host.OwnerID.ToString());
            asset.Description = description;

            int textLength = data.Length;
            data
                = "Linden text version 2\n{\nLLEmbeddedItems version 1\n{\ncount 0\n}\nText length "
                    + textLength.ToString() + "\n" + data + "}\n";

            asset.Data = Util.UTF8.GetBytes(data);
            World.AssetService.Store(asset);

            // Create Task Entry
            TaskInventoryItem taskItem = new TaskInventoryItem();

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

            if (forceSameName)
                m_host.Inventory.AddInventoryItemExclusive(taskItem, false);
            else
                m_host.Inventory.AddInventoryItem(taskItem, false);

            return taskItem;
        }

        /// <summary>
        /// Load the notecard data found at the given prim inventory item name or asset uuid.
        /// </summary>
        /// <param name="notecardNameOrUuid"></param>
        /// <returns>The text loaded.  Null if no notecard was found.</returns>
        protected string LoadNotecard(string notecardNameOrUuid)
        {
            UUID assetID = CacheNotecard(notecardNameOrUuid);
            StringBuilder notecardData = new StringBuilder();

            for (int count = 0; count < NotecardCache.GetLines(assetID); count++)
            {
                string line = NotecardCache.GetLine(assetID, count) + "\n";

//                m_log.DebugFormat("[OSSL]: From notecard {0} loading line {1}", notecardNameOrUuid, line);

                notecardData.Append(line);
            }

            return notecardData.ToString();
        }

        /// <summary>
        /// Cache a notecard's contents.
        /// </summary>
        /// <param name="notecardNameOrUuid"></param>
        /// <returns>
        /// The asset id of the notecard, which is used for retrieving the cached data.
        /// UUID.Zero if no asset could be found.
        /// </returns>
        protected UUID CacheNotecard(string notecardNameOrUuid)
        {
            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(notecardNameOrUuid, out assetID))
            {
                m_host.TaskInventory.LockItemsForRead(true);
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
                {
                    if (item.Type == 7 && item.Name == notecardNameOrUuid)
                    {
                        assetID = item.AssetID;
                    }
                }
                m_host.TaskInventory.LockItemsForRead(false);
            }

            if (assetID == UUID.Zero)
                return UUID.Zero;

            if (!NotecardCache.IsCached(assetID))
            {
                AssetBase a = World.AssetService.Get(assetID.ToString());

                if (a == null)
                    return UUID.Zero;

                System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                string data = enc.GetString(a.Data);
                NotecardCache.Cache(assetID, data);
            };

            return assetID;
        }

        /// <summary>
        /// Directly get an entire notecard at once.
        /// </summary>
        /// <remarks>
        /// Instead of using the LSL Dataserver event to pull notecard data
        /// this will simply read the entire notecard and return its data as a string.
        ///
        /// Warning - due to the synchronous method this function uses to fetch assets, its use
        ///            may be dangerous and unreliable while running in grid mode.
        /// </remarks>
        /// <param name="name">Name of the notecard or its asset id</param>
        /// <param name="line">The line number to read.  The first line is line 0</param>
        /// <returns>Notecard line</returns>
        public string osGetNotecardLine(string name, int line)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osGetNotecardLine");
            m_host.AddScriptLPS(1);

            UUID assetID = CacheNotecard(name);

            if (assetID == UUID.Zero)
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return "ERROR!";
            }

            return NotecardCache.GetLine(assetID, line);
        }

        /// <summary>
        /// Get an entire notecard at once.
        /// </summary>
        /// <remarks>
        /// Instead of using the LSL Dataserver event to pull notecard data line by line,
        /// this will simply read the entire notecard and return its data as a string.
        ///
        /// Warning - due to the synchronous method this function uses to fetch assets, its use
        ///            may be dangerous and unreliable while running in grid mode.
        /// </remarks>
        /// <param name="name">Name of the notecard or its asset id</param>
        /// <returns>Notecard text</returns>
        public string osGetNotecard(string name)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osGetNotecard");
            m_host.AddScriptLPS(1);

            string text = LoadNotecard(name);

            if (text == null)
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return "ERROR!";
            }
            else
            {
                return text;
            }
        }

        /// <summary>
        /// Get the number of lines in the given notecard.
        /// </summary>
        /// <remarks>
        /// Instead of using the LSL Dataserver event to pull notecard data,
        /// this will simply read the number of note card lines and return this data as an integer.
        ///
        /// Warning - due to the synchronous method this function uses to fetch assets, its use
        ///            may be dangerous and unreliable while running in grid mode.
        /// </remarks>
        /// <param name="name">Name of the notecard or its asset id</param>
        /// <returns></returns>
        public int osGetNumberOfNotecardLines(string name)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osGetNumberOfNotecardLines");
            m_host.AddScriptLPS(1);

            UUID assetID = CacheNotecard(name);

            if (assetID == UUID.Zero)
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return -1;
            }

            return NotecardCache.GetLines(assetID);
        }

        public string osAvatarName2Key(string firstname, string lastname)
        {
            CheckThreatLevel(ThreatLevel.Low, "osAvatarName2Key");

            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, firstname, lastname);
            if (null == account)
            {
                return UUID.Zero.ToString();
            }
            else
            {
                return account.PrincipalID.ToString();
            }
        }

        public string osKey2Name(string id)
        {
            CheckThreatLevel(ThreatLevel.Low, "osKey2Name");
            UUID key = new UUID();

            if (UUID.TryParse(id, out key))
            {
                UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
                if (null == account)
                {
                    return "";
                }
                else
                {
                    return account.Name;
                }
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Get the nickname of this grid, as set in the [GridInfo] config section.
        /// </summary>
        /// <remarks>
        /// Threat level is Moderate because intentional abuse, for instance
        /// scripts that are written to be malicious only on one grid,
        /// for instance in a HG scenario, are a distinct possibility.
        /// </remarks>
        /// <returns></returns>
        public string osGetGridNick()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridNick");
            m_host.AddScriptLPS(1);
            string nick = "hippogrid";
            IConfigSource config = m_ScriptEngine.ConfigSource;
            if (config.Configs["GridInfo"] != null)
                nick = config.Configs["GridInfo"].GetString("gridnick", nick);
            return nick;
        }

        public string osGetGridName()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridName");
            m_host.AddScriptLPS(1);
            string name = "the lost continent of hippo";
            IConfigSource config = m_ScriptEngine.ConfigSource;
            if (config.Configs["GridInfo"] != null)
                name = config.Configs["GridInfo"].GetString("gridname", name);
            return name;
        }

        public string osGetGridLoginURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridLoginURI");
            m_host.AddScriptLPS(1);
            string loginURI = "http://127.0.0.1:9000/";
            IConfigSource config = m_ScriptEngine.ConfigSource;
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
            while (match.Success)
            {
                foreach (System.Text.RegularExpressions.Group g in match.Groups)
                {
                    if (g.Success)
                    {
                        result.Add(new LSL_String(g.Value));
                        result.Add(new LSL_Integer(g.Index));
                    }
                }

                match = match.NextMatch();
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

        /// <summary>
        /// Get the primitive parameters of a linked prim.
        /// </summary>
        /// <remarks>
        /// Threat level is 'Low' because certain users could possibly be tricked into
        /// dropping an unverified script into one of their own objects, which could
        /// then gather the physical construction details of the object and transmit it
        /// to an unscrupulous third party, thus permitting unauthorized duplication of
        /// the object's form.
        /// </remarks>
        /// <param name="linknumber"></param>
        /// <param name="rules"></param>
        /// <returns></returns>
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

        public LSL_Key osNpcCreate(string firstname, string lastname, LSL_Vector position, string notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcCreate");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                AvatarAppearance appearance = null;

                UUID id;
                if (UUID.TryParse(notecard, out id))
                {
                    ScenePresence clonePresence = World.GetScenePresence(id);
                    if (clonePresence != null)
                        appearance = clonePresence.Appearance;
                }

                if (appearance == null)
                {
                    string appearanceSerialized = LoadNotecard(notecard);

                    if (appearanceSerialized != null)
                    {
                        OSDMap appearanceOsd = (OSDMap)OSDParser.DeserializeLLSDXml(appearanceSerialized);
                        appearance = new AvatarAppearance();
                        appearance.Unpack(appearanceOsd);
                    }
                }

                if (appearance == null)
                    return new LSL_Key(UUID.Zero.ToString());

                UUID x = module.CreateNPC(firstname,
                                          lastname,
                                          new Vector3((float) position.x, (float) position.y, (float) position.z),
                                          World,appearance);

                return new LSL_Key(x.ToString());
            }

            return new LSL_Key(UUID.Zero.ToString());
        }

        /// <summary>
        /// Save the current appearance of the NPC permanently to the named notecard.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="notecard">The name of the notecard to which to save the appearance.</param>
        /// <returns>The asset ID of the notecard saved.</returns>
        public LSL_Key osNpcSaveAppearance(LSL_Key npc, string notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSaveAppearance");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();

            if (npcModule != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return new LSL_Key(UUID.Zero.ToString());

                if (!npcModule.IsNPC(npcId, m_host.ParentGroup.Scene))
                    return new LSL_Key(UUID.Zero.ToString());

                return SaveAppearanceToNotecard(npcId, notecard);
            }

            return new LSL_Key(UUID.Zero.ToString());
        }

        public void osNpcLoadAppearance(LSL_Key npc, string notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcLoadAppearance");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();

            if (npcModule != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return;

                string appearanceSerialized = LoadNotecard(notecard);
                OSDMap appearanceOsd = (OSDMap)OSDParser.DeserializeLLSDXml(appearanceSerialized);
//                OSD a = OSDParser.DeserializeLLSDXml(appearanceSerialized);
//                Console.WriteLine("appearanceSerialized {0}", appearanceSerialized);
//                Console.WriteLine("a.Type {0}, a.ToString() {1}", a.Type, a);
                AvatarAppearance appearance = new AvatarAppearance();
                appearance.Unpack(appearanceOsd);

                npcModule.SetNPCAppearance(npcId, appearance, m_host.ParentGroup.Scene);
            }
        }

        public LSL_Vector osNpcGetPos(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcGetPos");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return new LSL_Vector(0, 0, 0);

                if (!npcModule.IsNPC(npcId, m_host.ParentGroup.Scene))
                    return new LSL_Vector(0, 0, 0);

                Vector3 pos = World.GetScenePresence(npcId).AbsolutePosition;
                return new LSL_Vector(pos.X, pos.Y, pos.Z);
            }

            return new LSL_Vector(0, 0, 0);
        }

        public void osNpcMoveTo(LSL_Key npc, LSL_Vector position)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcMoveTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return;
                
                Vector3 pos = new Vector3((float) position.x, (float) position.y, (float) position.z);
                module.MoveToTarget(npcId, World, pos, false, true);
            }
        }

        public void osNpcMoveToTarget(LSL_Key npc, LSL_Vector target, int options)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcMoveToTarget");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return;

                Vector3 pos = new Vector3((float)target.x, (float)target.y, (float)target.z);
                module.MoveToTarget(
                    new UUID(npc.m_string),
                    World,
                    pos,
                    (options & ScriptBaseClass.OS_NPC_NO_FLY) != 0,
                    (options & ScriptBaseClass.OS_NPC_LAND_AT_TARGET) != 0);
            }
        }

        public LSL_Rotation osNpcGetRot(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcGetRot");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return new LSL_Rotation(Quaternion.Identity.X, Quaternion.Identity.Y, Quaternion.Identity.Z, Quaternion.Identity.W);

                if (!npcModule.IsNPC(npcId, m_host.ParentGroup.Scene))
                    return new LSL_Rotation(Quaternion.Identity.X, Quaternion.Identity.Y, Quaternion.Identity.Z, Quaternion.Identity.W);

                ScenePresence sp = World.GetScenePresence(npcId);
                Quaternion rot = sp.Rotation;

                return new LSL_Rotation(rot.X, rot.Y, rot.Z, rot.W);
            }

            return new LSL_Rotation(Quaternion.Identity.X, Quaternion.Identity.Y, Quaternion.Identity.Z, Quaternion.Identity.W);
        }

        public void osNpcSetRot(LSL_Key npc, LSL_Rotation rotation)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSetRot");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return;

                if (!npcModule.IsNPC(npcId, m_host.ParentGroup.Scene))
                    return;

                ScenePresence sp = World.GetScenePresence(npcId);
                sp.Rotation = LSL_Api.Rot2Quaternion(rotation);
            }
        }

        public void osNpcStopMoveToTarget(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osNpcStopMoveTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
                module.StopMoveToTarget(new UUID(npc.m_string), World);
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

        public void osNpcSit(LSL_Key npc, LSL_Key target, int options)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSit");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                module.Sit(new UUID(npc.m_string), new UUID(target.m_string), World);
            }
        }

        public void osNpcStand(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStand");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                module.Stand(new UUID(npc.m_string), World);
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

        /// <summary>
        /// Save the current appearance of the script owner permanently to the named notecard.
        /// </summary>
        /// <param name="notecard">The name of the notecard to which to save the appearance.</param>
        /// <returns>The asset ID of the notecard saved.</returns>
        public LSL_Key osOwnerSaveAppearance(string notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osOwnerSaveAppearance");

            return SaveAppearanceToNotecard(m_host.OwnerID, notecard);
        }

        public LSL_Key osAgentSaveAppearance(LSL_Key avatarId, string notecard)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAgentSaveAppearance");

            return SaveAppearanceToNotecard(avatarId, notecard);
        }

        protected LSL_Key SaveAppearanceToNotecard(ScenePresence sp, string notecard)
        {
            IAvatarFactoryModule appearanceModule = World.RequestModuleInterface<IAvatarFactoryModule>();

            if (appearanceModule != null)
            {
                appearanceModule.SaveBakedTextures(sp.UUID);
                OSDMap appearancePacked = sp.Appearance.Pack();

                TaskInventoryItem item
                    = SaveNotecard(notecard, "Avatar Appearance", Util.GetFormattedXml(appearancePacked as OSD), true);

                return new LSL_Key(item.AssetID.ToString());
            }
            else
            {
                return new LSL_Key(UUID.Zero.ToString());
            }
        }

        protected LSL_Key SaveAppearanceToNotecard(UUID avatarId, string notecard)
        {
            ScenePresence sp = World.GetScenePresence(avatarId);

            if (sp == null || sp.IsChildAgent)
                return new LSL_Key(UUID.Zero.ToString());

            return SaveAppearanceToNotecard(sp, notecard);
        }

        protected LSL_Key SaveAppearanceToNotecard(LSL_Key rawAvatarId, string notecard)
        {
            UUID avatarId;
            if (!UUID.TryParse(rawAvatarId, out avatarId))
                return new LSL_Key(UUID.Zero.ToString());

            return SaveAppearanceToNotecard(avatarId, notecard);
        }
        
        /// <summary>
        /// Get current region's map texture UUID
        /// </summary>
        /// <returns></returns>
        public LSL_Key osGetMapTexture()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetMapTexture");
            return m_ScriptEngine.World.RegionInfo.RegionSettings.TerrainImageID.ToString();
        }

        /// <summary>
        /// Get a region's map texture UUID by region UUID or name.
        /// </summary>
        /// <param name="regionName"></param>
        /// <returns></returns>
        public LSL_Key osGetRegionMapTexture(string regionName)
        {
            CheckThreatLevel(ThreatLevel.High, "osGetRegionMapTexture");
            Scene scene = m_ScriptEngine.World;
            UUID key = UUID.Zero;
            GridRegion region;

            //If string is a key, use it. Otherwise, try to locate region by name.
            if (UUID.TryParse(regionName, out key))
                region = scene.GridService.GetRegionByUUID(UUID.Zero, key);
            else
                region = scene.GridService.GetRegionByName(UUID.Zero, regionName);

            // If region was found, return the regions map texture key.
            if (region != null)
                key = region.TerrainImage;

            ScriptSleep(1000);

            return key.ToString();
        }
        
       /// <summary>
        /// Return information regarding various simulator statistics (sim fps, physics fps, time
        /// dilation, total number of prims, total number of active scripts, script lps, various
        /// timing data, packets in/out, etc. Basically much the information that's shown in the
        /// client's Statistics Bar (Ctrl-Shift-1)
        /// </summary>
        /// <returns>List of floats</returns>
        public LSL_List osGetRegionStats()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetRegionStats");
            m_host.AddScriptLPS(1);
            LSL_List ret = new LSL_List();
            float[] stats = World.StatsReporter.LastReportedSimStats;
            
            for (int i = 0; i < 21; i++)
            {
                ret.Add(new LSL_Float(stats[i]));
            }
            return ret;
        }

        public int osGetSimulatorMemory()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetSimulatorMemory");
            m_host.AddScriptLPS(1);
            long pws = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

            if (pws > Int32.MaxValue)
                return Int32.MaxValue;
            if (pws < 0)
                return 0;

            return (int)pws;
        }
        
        public void osSetSpeed(string UUID, LSL_Float SpeedModifier)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osSetSpeed");
            m_host.AddScriptLPS(1);
            ScenePresence avatar = World.GetScenePresence(new UUID(UUID));
            avatar.SpeedModifier = (float)SpeedModifier;
        }
        
        public void osKickAvatar(string FirstName,string SurName,string alert)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osKickAvatar");
            if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID))
            {
                World.ForEachScenePresence(delegate(ScenePresence sp)
                {
                    if (!sp.IsChildAgent &&
                        sp.Firstname == FirstName &&
                        sp.Lastname == SurName)
                    {
                        // kick client...
                        if (alert != null)
                            sp.ControllingClient.Kick(alert);

                        // ...and close on our side
                        sp.Scene.IncomingCloseAgent(sp.UUID);
                    }
                });
            }
        }
        
        public void osCauseDamage(string avatar, double damage)
        {
            CheckThreatLevel(ThreatLevel.High, "osCauseDamage");
            m_host.AddScriptLPS(1);

            UUID avatarId = new UUID(avatar);
            Vector3 pos = m_host.GetWorldPosition();

            ScenePresence presence = World.GetScenePresence(avatarId); 
            if (presence != null)
            {
                LandData land = World.GetLandData((float)pos.X, (float)pos.Y);
                if ((land.Flags & (uint)ParcelFlags.AllowDamage) == (uint)ParcelFlags.AllowDamage)
                {
                    float health = presence.Health;
                    health -= (float)damage;
                    presence.setHealthWithUpdate(health);
                    if (health <= 0)
                    {
                        float healthliveagain = 100;
                        presence.ControllingClient.SendAgentAlertMessage("You died!", true);
                        presence.setHealthWithUpdate(healthliveagain);
                        presence.Scene.TeleportClientHome(presence.UUID, presence.ControllingClient);
                    }
                }
            }
        }
        
        public void osCauseHealing(string avatar, double healing)
        {
            CheckThreatLevel(ThreatLevel.High, "osCauseHealing");
            m_host.AddScriptLPS(1);

            UUID avatarId = new UUID(avatar);
            ScenePresence presence = World.GetScenePresence(avatarId);
            Vector3 pos = m_host.GetWorldPosition();
            bool result = World.ScriptDanger(m_host.LocalId, new Vector3((float)pos.X, (float)pos.Y, (float)pos.Z));
            if (result)
            {
                if (presence != null)
                {
                    float health = presence.Health;
                    health += (float)healing;
                    if (health >= 100)
                    {
                        health = 100;
                    }
                    presence.setHealthWithUpdate(health);
                }
            }
        }

        public LSL_List osGetPrimitiveParams(LSL_Key prim, LSL_List rules)
        {
            CheckThreatLevel(ThreatLevel.High, "osGetPrimitiveParams");
            m_host.AddScriptLPS(1);
            InitLSL();
            
            return m_LSL_Api.GetLinkPrimitiveParamsEx(prim, rules);
        }

        public void osSetPrimitiveParams(LSL_Key prim, LSL_List rules)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetPrimitiveParams");
            m_host.AddScriptLPS(1);
            InitLSL();
            
            m_LSL_Api.SetPrimitiveParamsEx(prim, rules);
        }
        
        /// <summary>
        /// Set parameters for light projection in host prim 
        /// </summary>
        public void osSetProjectionParams(bool projection, LSL_Key texture, double fov, double focus, double amb)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetProjectionParams");

            osSetProjectionParams(UUID.Zero.ToString(), projection, texture, fov, focus, amb);
        }

        /// <summary>
        /// Set parameters for light projection with uuid of target prim
        /// </summary>
        public void osSetProjectionParams(LSL_Key prim, bool projection, LSL_Key texture, double fov, double focus, double amb)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetProjectionParams");
            m_host.AddScriptLPS(1);

            SceneObjectPart obj = null;
            if (prim == UUID.Zero.ToString())
            {
                obj = m_host;
            }
            else
            {
                obj = World.GetSceneObjectPart(new UUID(prim));
                if (obj == null)
                    return;
            }

            obj.Shape.ProjectionEntry = projection;
            obj.Shape.ProjectionTextureUUID = new UUID(texture);
            obj.Shape.ProjectionFOV = (float)fov;
            obj.Shape.ProjectionFocus = (float)focus;
            obj.Shape.ProjectionAmbiance = (float)amb;

            obj.ParentGroup.HasGroupChanged = true;
            obj.ScheduleFullUpdate();
        }

        /// <summary>
        /// Like osGetAgents but returns enough info for a radar
        /// </summary>
        /// <returns>Strided list of the UUID, position and name of each avatar in the region</returns>
        public LSL_List osGetAvatarList()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetAvatarList");

            LSL_List result = new LSL_List();
            World.ForEachScenePresence(delegate (ScenePresence avatar)
            {
                if (avatar != null && avatar.UUID != m_host.OwnerID)
                {
                    if (avatar.IsChildAgent == false)
                    {
                        result.Add(new LSL_String(avatar.UUID.ToString()));
                        OpenMetaverse.Vector3 ap = avatar.AbsolutePosition;
                        result.Add(new LSL_Vector(ap.X, ap.Y, ap.Z));
                        result.Add(new LSL_String(avatar.Name));
                    }
                }
            });

            return result;
        }

        /// <summary>
        /// Convert a unix time to a llGetTimestamp() like string
        /// </summary>
        /// <param name="unixTime"></param>
        /// <returns></returns>
        public LSL_String osUnixTimeToTimestamp(long time)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osUnixTimeToTimestamp");
            long baseTicks = 621355968000000000;
            long tickResolution = 10000000;
            long epochTicks = (time * tickResolution) + baseTicks;
            DateTime date = new DateTime(epochTicks);

            return date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }
    }
}