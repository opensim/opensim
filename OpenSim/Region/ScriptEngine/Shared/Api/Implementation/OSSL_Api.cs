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
using OpenSim.Region.Framework.Scenes.Scripting;
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
using PermissionMask = OpenSim.Framework.PermissionMask;
using OpenSim.Services.Connectors.Hypergrid;

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
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const string GridInfoServiceConfigSectionName = "GridInfoService";

        internal IScriptEngine m_ScriptEngine;
        internal LSL_Api m_LSL_Api = null; // get a reference to the LSL API so we can call methods housed there
        internal SceneObjectPart m_host;
        internal TaskInventoryItem m_item;
        internal bool m_OSFunctionsEnabled = false;
        internal ThreatLevel m_MaxThreatLevel = ThreatLevel.VeryLow;
        internal float m_ScriptDelayFactor = 1.0f;
        internal float m_ScriptDistanceFactor = 1.0f;
        internal Dictionary<string, FunctionPerms > m_FunctionPerms = new Dictionary<string, FunctionPerms >();
        protected IUrlModule m_UrlModule = null;
        protected ISoundModule m_SoundModule = null;
        internal IConfig m_osslconfig;

        public void Initialize(
            IScriptEngine scriptEngine, SceneObjectPart host, TaskInventoryItem item)
        {
            m_ScriptEngine = scriptEngine;
            m_host = host;
            m_item = item;

            m_osslconfig = m_ScriptEngine.ConfigSource.Configs["OSSL"];
            if(m_osslconfig == null)
                m_osslconfig = m_ScriptEngine.Config;

            m_UrlModule = m_ScriptEngine.World.RequestModuleInterface<IUrlModule>();
            m_SoundModule = m_ScriptEngine.World.RequestModuleInterface<ISoundModule>();

            if (m_osslconfig.GetBoolean("AllowOSFunctions", false))
            {
                m_OSFunctionsEnabled = true;
                // m_log.Warn("[OSSL] OSSL FUNCTIONS ENABLED");
            }

            m_ScriptDelayFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDistanceLimitFactor", 1.0f);

            string risk = m_osslconfig.GetString("OSFunctionThreatLevel", "VeryLow");
            switch (risk)
            {
            case "NoAccess":
                m_MaxThreatLevel = ThreatLevel.NoAccess;
                break;
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
            throw new ScriptException("OSSL Runtime Error: " + msg);
        }

        /// <summary>
        /// Initialize the LSL interface.
        /// </summary>
        /// <remarks>
        /// FIXME: This is an abomination.  We should be able to set this up earlier but currently we have no
        /// guarantee the interface is present on Initialize().  There needs to be another post initialize call from
        /// ScriptInstance.
        /// </remarks>
        private void InitLSL()
        {
            if (m_LSL_Api != null)
                return;

            m_LSL_Api = (LSL_Api)m_ScriptEngine.GetApi(m_item.ItemID, "LSL");
        }

        //
        //Dumps an error message on the debug console.
        //

        internal void OSSLShoutError(string message)
        {
            if (message.Length > 1023)
                message = message.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(message),
                ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if(wComm != null)
                wComm.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, message);
        }

        // Returns if OSSL is enabled. Throws a script exception if OSSL is not allowed..
        // for safe funtions always active
        public void CheckThreatLevel()
        {
            m_host.AddScriptLPS(1);
            if (!m_OSFunctionsEnabled)
                OSSLError("permission denied.  All OS functions are disabled."); // throws
        }

        // Returns if the function is allowed. Throws a script exception if not allowed.
        public void CheckThreatLevel(ThreatLevel level, string function)
        {
            m_host.AddScriptLPS(1);
            if (!m_OSFunctionsEnabled)
                OSSLError(String.Format("{0} permission denied.  All OS functions are disabled.", function)); // throws

            string reasonWhyNot = CheckThreatLevelTest(level, function);
            if (!String.IsNullOrEmpty(reasonWhyNot))
            {
                OSSLError(reasonWhyNot);
            }
        }

        // Check to see if function is allowed. Returns an empty string if function permitted
        //     or a string explaining why this function can't be used.
        private string CheckThreatLevelTest(ThreatLevel level, string function)
        {
            if (!m_FunctionPerms.ContainsKey(function))
            {
                FunctionPerms perms = new FunctionPerms();
                m_FunctionPerms[function] = perms;

                string ownerPerm = m_osslconfig.GetString("Allow_" + function, "");
                string creatorPerm = m_osslconfig.GetString("Creators_" + function, "");
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
                            if (current.ToUpper() == "PARCEL_GROUP_MEMBER" || current.ToUpper() == "PARCEL_OWNER" || current.ToUpper() == "ESTATE_MANAGER" || current.ToUpper() == "ESTATE_OWNER" || current.ToUpper() == "ACTIVE_GOD" || current.ToUpper() == "GRID_GOD" || current.ToUpper() == "GOD")
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
                    return
                        String.Format(
                            "{0} permission denied.  Allowed threat level is {1} but function threat level is {2}.",
                            function, m_MaxThreatLevel, level);
            }
            else
            {
                if (!m_FunctionPerms[function].AllowedOwners.Contains(UUID.Zero))
                {
                    // Not anyone. Do detailed checks
                    if (m_FunctionPerms[function].AllowedOwners.Contains(m_host.OwnerID))
                    {
                        // prim owner is in the list of allowed owners
                        return String.Empty;
                    }

                    UUID ownerID = m_item.OwnerID;

                    //OSSL only may be used if object is in the same group as the parcel
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("PARCEL_GROUP_MEMBER"))
                    {
                        ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

                        if (land.LandData.GroupID == m_item.GroupID && land.LandData.GroupID != UUID.Zero)
                        {
                            return String.Empty;
                        }
                    }

                    //Only Parcelowners may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("PARCEL_OWNER"))
                    {
                        ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

                        if (land.LandData.OwnerID == ownerID)
                        {
                            return String.Empty;
                        }
                    }

                    //Only Estate Managers may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("ESTATE_MANAGER"))
                    {
                        //Only Estate Managers may use the function
                        if (World.RegionInfo.EstateSettings.IsEstateManagerOrOwner(ownerID) && World.RegionInfo.EstateSettings.EstateOwner != ownerID)
                        {
                            return String.Empty;
                        }
                    }

                    //Only regionowners may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("ESTATE_OWNER"))
                    {
                        if (World.RegionInfo.EstateSettings.EstateOwner == ownerID)
                        {
                            return String.Empty;
                        }
                    }


                    //Only grid gods may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("GRID_GOD"))
                    {
                        if (World.Permissions.IsGridGod(ownerID))
                        {
                            return String.Empty;
                        }
                    }

                    //Any god may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("GOD"))
                    {
                        if (World.Permissions.IsAdministrator(ownerID))
                        {
                            return String.Empty;
                        }
                    }

                    //Only active gods may use the function
                    if (m_FunctionPerms[function].AllowedOwnerClasses.Contains("ACTIVE_GOD"))
                    {
                        ScenePresence sp = World.GetScenePresence(ownerID);
                        if (sp != null && !sp.IsDeleted && sp.IsGod)
                        {
                            return String.Empty;
                        }
                    }

                    if (!m_FunctionPerms[function].AllowedCreators.Contains(m_item.CreatorID))
                        return(
                            String.Format("{0} permission denied. Script creator is not in the list of users allowed to execute this function and prim owner also has no permission.",
                            function));

                    if (m_item.CreatorID != ownerID)
                    {
                        if ((m_item.CurrentPermissions & (uint)PermissionMask.Modify) != 0)
                            return String.Format("{0} permission denied. Script permissions error.", function);

                    }
                }
            }
            return String.Empty;
        }

        internal void OSSLDeprecated(string function, string replacement)
        {
            OSSLShoutError(string.Format("Use of function {0} is deprecated. Use {1} instead.", function, replacement));
        }

        protected void ScriptSleep(int delay)
        {
            delay = (int)(delay * m_ScriptDelayFactor);
            if (delay < 10)
                return;
            if(m_item != null)
                m_ScriptEngine.SleepScript(m_item.ItemID, delay);
            else
                Thread.Sleep(delay);
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
            if (x > (World.RegionInfo.RegionSizeX - 1) || x < 0 || y > (World.RegionInfo.RegionSizeY - 1) || y < 0)
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
            CheckThreatLevel();
            return GetTerrainHeight(x, y);
        }

        public LSL_Float osTerrainGetHeight(int x, int y)
        {
            CheckThreatLevel();
            OSSLDeprecated("osTerrainGetHeight", "osGetTerrainHeight");
            return GetTerrainHeight(x, y);
        }

        private LSL_Float GetTerrainHeight(int x, int y)
        {
            if (x > (World.RegionInfo.RegionSizeX - 1) || x < 0 || y > (World.RegionInfo.RegionSizeY - 1) || y < 0)
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
            if (World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false) && (restartModule != null))
            {
                if (seconds < 15)
                {
                    restartModule.AbortRestart("Region restart has been aborted\n");
                    return 1;
                }

                RegionRestart(seconds, String.Empty);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public int osRegionRestart(double seconds, string msg)
        {
            CheckThreatLevel(ThreatLevel.High, "osRegionRestart");

            IRestartModule restartModule = World.RequestModuleInterface<IRestartModule>();
            if (World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false) && (restartModule != null))
            {
                if (seconds < 15)
                {
                    restartModule.AbortRestart("Region restart has been aborted\n");
                    return 1;
                }

                RegionRestart(seconds, msg);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        private void RegionRestart(double seconds, string msg)
        {
            IRestartModule restartModule = World.RequestModuleInterface<IRestartModule>();

            List<int> times = new List<int>();
            while (seconds > 0)
            {
                times.Add((int)seconds);
                if (seconds > 300)
                    seconds -= 120;
                else if (seconds > 120)
                    seconds -= 60;
                else if (seconds > 60)
                    seconds -= 30;
                else
                    seconds -= 15;
            }

            if (msg == String.Empty)
                restartModule.ScheduleRestart(UUID.Zero, "Region: " + World.RegionInfo.RegionName + " is about to restart.\n\nIf you stay here you will be logged out.\n\n\nTime remaining: {0}.\n", times.ToArray(), true);

            else
                restartModule.ScheduleRestart(UUID.Zero, msg + "\n\nTime remaining: {0}.\n", times.ToArray(), true);
        }

        public void osRegionNotice(string msg)
        {
            CheckThreatLevel(ThreatLevel.High, "osRegionNotice");

            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();
            if (dm == null)
                return;

            if (!World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false))
                return;

            dm.SendGeneralAlert(msg + "\n");
        }

        public void osRegionNotice(LSL_Key agentID, string msg)
        {
            CheckThreatLevel(ThreatLevel.High, "osRegionNotice");

            if (!World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false))
                return;

            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();
            if (dm == null)
                return;

            UUID avatarID;
            if (!UUID.TryParse(agentID, out avatarID))
                return;

            ScenePresence sp = null;
            if (!World.TryGetScenePresence(avatarID, out sp))
                return;

            if (sp == null || sp.IsChildAgent || sp.IsDeleted || sp.IsInTransit || sp.IsNPC)
                return;

            dm.SendAlertToUser(sp.ControllingClient, msg + "\n", false);
        }

        public void osSetRot(UUID target, Quaternion rotation)
        {
            // This function has no security. It can be used to destroy
            // arbitrary builds the user would normally have no rights to
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetRot");

            if (World.Entities.ContainsKey(target))
            {
                EntityBase entity;
                if (World.Entities.TryGetValue(target, out entity))
                {
                    if (entity is SceneObjectGroup)
                        ((SceneObjectGroup)entity).UpdateGroupRotationR(rotation);
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

            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams);
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

            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, true, (byte) alpha);
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

            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, blend, disp, (byte) alpha, face);
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
            return osSetDynamicTextureDataFace(dynamicID, contentType, data, extraParams, timer, -1);
        }

        public string osSetDynamicTextureDataFace(string dynamicID, string contentType, string data, string extraParams,
                                           int timer, int face)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureData");

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
                                                            extraParams, false, 3, 255, face);

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
                                                            extraParams, true, (byte) alpha);
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
            CheckThreatLevel(ThreatLevel.VeryLow , "osSetDynamicTextureDataBlendFace");

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
                                                            extraParams, blend, disp, (byte) alpha, face);
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

            // For safety, we add another permission check here, and don't rely only on the standard OSSL permissions
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

            m_host.ParentGroup.RootPart.SetFloatOnWater(floatYN);
        }

        private bool checkAllowAgentTPbyLandOwner(UUID agentId, Vector3 pos)
        {
            UUID hostOwner = m_host.OwnerID;

            if(hostOwner == agentId)
                return true;

            if (m_item.PermsGranter == agentId)
            {
                if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) != 0)
                    return true;
            }

            ILandObject land = World.LandChannel.GetLandObject(pos);
            if(land == null)
                return true;

            LandData landdata = land.LandData;
            if(landdata == null)
                return true;

            if(landdata.OwnerID == hostOwner)
                return true;

            EstateSettings es = World.RegionInfo.EstateSettings;
            if(es != null && es.IsEstateManagerOrOwner(hostOwner))
                return true;

            if(!landdata.IsGroupOwned)
                return false;

            UUID landGroup = landdata.GroupID;
            if(landGroup == UUID.Zero)
                return false;

            if(landGroup == m_host.GroupID)
                return true;

            return false;
        }

        // Teleport functions
        public void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.Severe, "osTeleportAgent");

            TeleportAgent(agent, regionName, position, lookat);
        }

        private void TeleportAgent(string agent, string regionName,
            LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            if(String.IsNullOrEmpty(regionName))
                regionName = World.RegionInfo.RegionName;

            UUID agentId;
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsInTransit)
                    return;

                Vector3 pos = presence.AbsolutePosition;
                if(!checkAllowAgentTPbyLandOwner(agentId, pos))
                {
                    ScriptSleep(500);
                    return;
                }

                if(regionName == World.RegionInfo.RegionName)
                {
                    // should be faster than going to threadpool
                    World.RequestTeleportLocation(presence.ControllingClient, regionName, position,
                        lookat, (uint)TPFlags.ViaLocation);
                    ScriptSleep(500);
                }
                else
                {
                // We will launch the teleport on a new thread so that when the script threads are terminated
                // before teleport in ScriptInstance.GetXMLState(), we don't end up aborting the one doing the teleporting.
                Util.FireAndForget(
                    o => World.RequestTeleportLocation(
                        presence.ControllingClient, regionName, position,
                        lookat, (uint)TPFlags.ViaLocation),
                    null, "OSSL_Api.TeleportAgentByRegionCoords");
                ScriptSleep(5000);
                }
            }
        }

        public void osTeleportAgent(string agent, int regionGridX, int regionGridY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.Severe, "osTeleportAgent");

            TeleportAgent(agent, regionGridX, regionGridY, position, lookat);
        }

        private void TeleportAgent(string agent, int regionGridX, int regionGridY,
            LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            ulong regionHandle = Util.RegionGridLocToHandle((uint)regionGridX, (uint)regionGridY);

            UUID agentId;
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsInTransit)
                    return;

                Vector3 pos = presence.AbsolutePosition;
                if(!checkAllowAgentTPbyLandOwner(agentId, pos))
                {
                    ScriptSleep(500);
                    return;
                }

                Util.FireAndForget(
                    o => World.RequestTeleportLocation(
                        presence.ControllingClient, regionHandle,
                        position, lookat, (uint)TPFlags.ViaLocation),
                    null, "OSSL_Api.TeleportAgentByRegionName");

                ScriptSleep(5000);
            }
        }

        public void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            UUID agentId;
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsInTransit)
                    return;

                Vector3 pos = presence.AbsolutePosition;
                if(!checkAllowAgentTPbyLandOwner(agentId, pos))
                {
                    ScriptSleep(500);
                    return;
                }

                World.RequestTeleportLocation(presence.ControllingClient, World.RegionInfo.RegionName, position,
                    lookat, (uint)TPFlags.ViaLocation);
                ScriptSleep(500);
            }
        }

        public void osTeleportOwner(string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // Threat level None because this is what can already be done with the World Map in the viewer
            CheckThreatLevel(ThreatLevel.None, "osTeleportOwner");

            TeleportAgent(m_host.OwnerID.ToString(), regionName, position, lookat);
        }

        public void osTeleportOwner(int regionGridX, int regionGridY, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            CheckThreatLevel(ThreatLevel.None, "osTeleportOwner");

            TeleportAgent(m_host.OwnerID.ToString(), regionGridX, regionGridY, position, lookat);
        }

        public void osTeleportOwner(LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            CheckThreatLevel(ThreatLevel.None, "osTeleportOwner");

            osTeleportAgent(m_host.OwnerID.ToString(), position, lookat);
        }

        ///<summary>
        /// Allows a script IN the target prim to force an avatar to sit on it using normal methods
        /// as if called by the client.
        /// Silent fail if agent (or target if overloaded) not found.
        /// Does work if passed key (or keys if overloaded).
        /// </summary>
        /// <param name="avatar"></param>
        public void osForceOtherSit(string avatar)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osForceOtherSit");

            ForceSit(avatar, m_host.UUID);
        }

        /// <summary>
        /// Overload method of osForceOtherSit(string avatar) to allow a script NOT in the target prim to force
        /// an avatar to sit on the target prim using normal methods as if called by the client.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="target"></param>
        public void osForceOtherSit(string avatar, string target)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osForceOtherSit");

            UUID targetID = new UUID(target);

            ForceSit(avatar, targetID);
        }

        public void ForceSit(string avatar, UUID targetID)
        {
            UUID agentID;

            if (!UUID.TryParse(avatar, out agentID))
                return;

            ScenePresence presence = World.GetScenePresence(agentID);

            SceneObjectPart part = World.GetSceneObjectPart(targetID);

            if (presence != null &&
                part != null &&
                part.SitTargetAvatar == UUID.Zero)
                presence.HandleAgentRequestSit(presence.ControllingClient,
                    agentID,
                    targetID,
                    part.SitTargetPosition);
        }

        // Get a list of all the avatars/agents in the region
        public LSL_List osGetAgents()
        {
            // threat level is None as we could get this information with an
            // in-world script as well, just not as efficient
            CheckThreatLevel(ThreatLevel.None, "osGetAgents");

            LSL_List result = new LSL_List();
            World.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                result.Add(new LSL_String(sp.Name));
            });
            return result;
        }

        public string osGetAgentIP(string agent)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osGetAgentIP");
            if(!(World.Permissions.IsGod(m_host.OwnerID))) // user god always needed
                return "";

            UUID avatarID = (UUID)agent;

            if (World.Entities.ContainsKey((UUID)agent) && World.Entities[avatarID] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatarID];
                return target.ControllingClient.RemoteEndPoint.Address.ToString();
            }

            // fall through case, just return nothing
            return "";
        }

        // Adam's super super custom animation functions
        public void osAvatarPlayAnimation(LSL_Key avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarPlayAnimation");

            UUID avatarID;
            if(!UUID.TryParse(avatar, out avatarID))
                return;

            ScenePresence target = World.GetScenePresence(avatarID);
            if (target == null)
                return;

            UUID animID = UUID.Zero;
            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == (int)AssetType.Animation)
                {
                   if (inv.Value.Name == animation)
                   {
                       animID = inv.Value.AssetID;
                       break;
                   }
                }
            }
            m_host.TaskInventory.LockItemsForRead(false);

            if (animID == UUID.Zero)
                target.Animator.AddAnimation(animation, m_host.UUID);
            else
                target.Animator.AddAnimation(animID, m_host.UUID);
        }

        public void osAvatarStopAnimation(LSL_Key avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarStopAnimation");

            UUID avatarID;
            if(!UUID.TryParse(avatar, out avatarID))
                return;

            ScenePresence target = World.GetScenePresence(avatarID);
            if (target == null)
                return;

            UUID animID;
            if (!UUID.TryParse(animation, out animID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(animation);
                if (item != null && item.Type == (int)AssetType.Animation)
                    animID = item.AssetID;
                else
                    animID = UUID.Zero;
            }

            if (animID == UUID.Zero)
                target.Animator.RemoveAnimation(animation);
            else
                target.Animator.RemoveAnimation(animID, true);
        }

        //Texture draw functions

        public string osDrawResetTransform(string drawList)
        {
            CheckThreatLevel();

            drawList += "ResetTransf;";
            return drawList;
        }

        public string osDrawRotationTransform(string drawList, LSL_Float x)
        {
            CheckThreatLevel();

            drawList += "RotTransf " + x + ";";
            return drawList;
        }

        public string osDrawScaleTransform(string drawList, LSL_Float x, LSL_Float y)
        {
            CheckThreatLevel();

            drawList += "ScaleTransf " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawTranslationTransform(string drawList, LSL_Float x, LSL_Float y)
        {
            CheckThreatLevel();

            drawList += "TransTransf " + x + "," + y + ";";
            return drawList;
        }

        public string osMovePen(string drawList, int x, int y)
        {
            CheckThreatLevel();

            drawList += "MoveTo " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            CheckThreatLevel();

            drawList += "MoveTo "+ startX+","+ startY +"; LineTo "+endX +","+endY +"; ";
            return drawList;
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            CheckThreatLevel();

            drawList += "LineTo " + endX + "," + endY + "; ";
            return drawList;
        }

        public string osDrawText(string drawList, string text)
        {
            CheckThreatLevel(ThreatLevel.None, "osDrawText");

            drawList += "Text " + text + "; ";
            return drawList;
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            CheckThreatLevel();

            drawList += "Ellipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledEllipse(string drawList, int width, int height)
        {
            CheckThreatLevel();

            drawList += "FillEllipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            CheckThreatLevel();

            drawList += "Rectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            CheckThreatLevel();

            drawList += "FillRectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledPolygon(string drawList, LSL_List x, LSL_List y)
        {
            CheckThreatLevel();

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
            CheckThreatLevel();

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
            CheckThreatLevel();

            drawList += "FontSize "+ fontSize +"; ";
            return drawList;
        }

        public string osSetFontName(string drawList, string fontName)
        {
            CheckThreatLevel();

            drawList += "FontName "+ fontName +"; ";
            return drawList;
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            CheckThreatLevel();

            drawList += "PenSize " + penSize + "; ";
            return drawList;
        }

        public string osSetPenColor(string drawList, string color)
        {
            CheckThreatLevel();

            drawList += "PenColor " + color + "; ";
            return drawList;
        }

        // Deprecated
        public string osSetPenColour(string drawList, string colour)
        {
            CheckThreatLevel();
            OSSLDeprecated("osSetPenColour", "osSetPenColor");

            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osSetPenCap(string drawList, string direction, string type)
        {
            CheckThreatLevel();

            drawList += "PenCap " + direction + "," + type + "; ";
            return drawList;
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            CheckThreatLevel();

            drawList +="Image " +width + "," + height+ ","+ imageUrl +"; " ;
            return drawList;
        }

        public LSL_Vector osGetDrawStringSize(string contentType, string text, string fontName, int fontSize)
        {
            CheckThreatLevel();

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

            m_host.SetScriptEvents(m_item.ItemID, events);
        }

        public void osSetRegionWaterHeight(double height)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetRegionWaterHeight");

            World.EventManager.TriggerRequestChangeWaterHeight((float)height);
        }

        /// <summary>
        /// Changes the Region Sun Settings, then Triggers a Sun Update
        /// </summary>
        /// <param name="useEstateSun">True to use Estate Sun instead of Region Sun</param>
        /// <param name="sunFixed">True to keep the sun stationary</param>
        /// <param name="sunHour">The "Sun Hour" that is desired, 0...24, with 0 just after SunRise</param>
        public void osSetRegionSunSettings(bool useEstateSun, bool sunFixed, double sunHour)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetRegionSunSettings");

            while (sunHour > 24.0)
                sunHour -= 24.0;

            while (sunHour < 0)
                sunHour += 24.0;

            World.RegionInfo.RegionSettings.UseEstateSun = useEstateSun;
            World.RegionInfo.RegionSettings.SunPosition = sunHour + 6; // LL Region Sun Hour is 6 to 30
            World.RegionInfo.RegionSettings.FixedSun = sunFixed;
            World.RegionInfo.RegionSettings.Save();

            World.EventManager.TriggerEstateToolsSunUpdate(World.RegionInfo.RegionHandle);
        }

        /// <summary>
        /// Changes the Estate Sun Settings, then Triggers a Sun Update
        /// </summary>
        /// <param name="sunFixed">True to keep the sun stationary, false to use global time</param>
        /// <param name="sunHour">The "Sun Hour" that is desired, 0...24, with 0 just after SunRise</param>
        public void osSetEstateSunSettings(bool sunFixed, double sunHour)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetEstateSunSettings");

            while (sunHour > 24.0)
                sunHour -= 24.0;

            while (sunHour < 0)
                sunHour += 24.0;

            World.RegionInfo.EstateSettings.UseGlobalTime = !sunFixed;
            World.RegionInfo.EstateSettings.SunPosition = sunHour;
            World.RegionInfo.EstateSettings.FixedSun = sunFixed;
            World.EstateDataService.StoreEstateSettings(World.RegionInfo.EstateSettings);

            World.EventManager.TriggerEstateToolsSunUpdate(World.RegionInfo.RegionHandle);
        }

        /// <summary>
        /// Return the current Sun Hour 0...24, with 0 being roughly sun-rise
        /// </summary>
        /// <returns></returns>
        public double osGetCurrentSunHour()
        {
            CheckThreatLevel();

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
            CheckThreatLevel();
            return GetSunParam(param);
        }

        private double GetSunParam(string param)
        {
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
            ISunModule module = World.RequestModuleInterface<ISunModule>();
            if (module != null)
            {
                module.SetSunParameter(param, value);
            }
        }

        public string osWindActiveModelPluginName()
        {
            CheckThreatLevel(ThreatLevel.None, "osWindActiveModelPluginName");

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

            int startx = (int)(pos1.x < pos2.x ? pos1.x : pos2.x);
            int starty = (int)(pos1.y < pos2.y ? pos1.y : pos2.y);
            int endx = (int)(pos1.x > pos2.x ? pos1.x : pos2.x);
            int endy = (int)(pos1.y > pos2.y ? pos1.y : pos2.y);

            World.LandChannel.Join(startx,starty,endx,endy,m_host.OwnerID);
        }

        public void osParcelSubdivide(LSL_Vector pos1, LSL_Vector pos2)
        {
            CheckThreatLevel(ThreatLevel.High, "osParcelSubdivide");

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
            // Get a reference to the land data and make sure the owner of the script
            // can modify it

            ILandObject startLandObject = World.LandChannel.GetLandObject((int)pos.x, (int)pos.y);
            if (startLandObject == null)
            {
                OSSLShoutError("There is no land at that location");
                return;
            }

            if (!World.Permissions.CanEditParcelProperties(m_host.OwnerID, startLandObject, GroupPowers.LandOptions, false))
            {
                OSSLShoutError("script owner does not have permission to modify the parcel");
                return;
            }

            // Create a new land data object we can modify
            LandData newLand = startLandObject.LandData.Copy();
            UUID uuid;
            EstateSettings es = World.RegionInfo.EstateSettings;

            bool changed = false;
            bool changedSeeAvs = false;
            bool changedoverlay = false;
            bool changedneedupdate = false;

            // Process the rules, not sure what the impact would be of changing owner or group
            for (int idx = 0; idx < rules.Length;)
            {
                int code = rules.GetLSLIntegerItem(idx++);
                string arg = rules.GetLSLStringItem(idx++);
                switch (code)
                {
                    case ScriptBaseClass.PARCEL_DETAILS_NAME:
                        if(newLand.Name != arg)
                        {
                            newLand.Name = arg;
                            changed = true;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_DESC:
                        if(newLand.Description != arg)
                        {
                            newLand.Description = arg;
                            changed = true;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_OWNER:
                        if(es != null && !es.IsEstateManagerOrOwner(m_host.OwnerID))
                        {
                            OSSLShoutError("script owner does not have permission to modify the parcel owner");
                        }
                        else
                        {
                            if (UUID.TryParse(arg, out uuid))
                            {
                                if(newLand.OwnerID != uuid)
                                {
                                    changed = true;
                                    newLand.OwnerID = uuid;
                                    newLand.GroupID = UUID.Zero;
                                }
                            }
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_GROUP:
                        if(m_host.OwnerID == newLand.OwnerID || es == null || es.IsEstateManagerOrOwner(m_host.OwnerID))
                        {
                            if (UUID.TryParse(arg, out uuid))
                            {
                                if(newLand.GroupID != uuid)
                                {
                                    if(uuid == UUID.Zero)
                                    {
                                        changed = true;
                                        newLand.GroupID = uuid;
                                    }
                                    else
                                    {
                                        IGroupsModule groupsModule = m_ScriptEngine.World.RequestModuleInterface<IGroupsModule>();
                                        GroupMembershipData member = null;
                                        if (groupsModule != null)
                                            member = groupsModule.GetMembershipData(uuid, newLand.OwnerID);
                                        if (member == null)
                                            OSSLShoutError(string.Format("land owner is not member of the new group for parcel"));
                                        else
                                        {
                                            changed = true;
                                            newLand.GroupID = uuid;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            OSSLShoutError("script owner does not have permission to modify the parcel group");
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_CLAIMDATE:
                        if(es != null && !es.IsEstateManagerOrOwner(m_host.OwnerID))
                        {
                            OSSLShoutError("script owner does not have permission to modify the parcel CLAIM DATE");
                        }
                        else
                        {
                            int date =  Convert.ToInt32(arg);
                            if (date == 0)
                                date = Util.UnixTimeSinceEpoch();
                            if(newLand.ClaimDate != date)
                            {
                                changed = true;
                                newLand.ClaimDate = date;
                            }
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_SEE_AVATARS:
                        bool newavs = (Convert.ToInt32(arg) != 0);
                        if(newLand.SeeAVs != newavs)
                        {
                            changed = true;
                            changedSeeAvs = true;
                            changedoverlay = true;
                            changedneedupdate = true;
                            newLand.SeeAVs = newavs;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_ANY_AVATAR_SOUNDS:
                        bool newavsounds = (Convert.ToInt32(arg) != 0);
                        if(newLand.AnyAVSounds != newavsounds)
                        {
                            changed = true;
                            newLand.AnyAVSounds = newavsounds;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_GROUP_SOUNDS:
                        bool newgrpsounds = (Convert.ToInt32(arg) != 0);
                        if(newLand.GroupAVSounds != newgrpsounds)
                        {
                            changed = true;
                            newLand.GroupAVSounds = newgrpsounds;
                        }
                        break;
                    }
            }
            if(changed)
            {
                World.LandChannel.UpdateLandObject(newLand.LocalID, newLand);

                if(changedneedupdate)
                {
                    UUID parcelID= newLand.GlobalID;
                    World.ForEachRootScenePresence(delegate (ScenePresence avatar)
                    {
                        if (avatar == null || avatar.IsDeleted || avatar.IsInTransit)
                            return;

                        if(changedSeeAvs && avatar.currentParcelUUID == parcelID )
                            avatar.currentParcelUUID = parcelID; // force parcel flags review

                        if(avatar.ControllingClient == null)
                            return;

                        // this will be needed for some things like damage etc
//                        if(avatar.currentParcelUUID == parcelID)
//                            startLandObject.SendLandUpdateToClient(avatar.ControllingClient);

                        if(changedoverlay && !avatar.IsNPC)
                            World.LandChannel.SendParcelsOverlay(avatar.ControllingClient);
                    });
                }
            }
        }

        public void osSetParcelMediaURL(string url)
        {
            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMediaURL");

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (land.LandData.OwnerID != m_host.OwnerID)
                return;

            land.SetMediaUrl(url);
        }

        public void osSetParcelSIPAddress(string SIPAddress)
        {
            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelSIPAddress");

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

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

        public LSL_Integer osCheckODE()
        {
            CheckThreatLevel();

            LSL_Integer ret = 0;    // false
            if (m_ScriptEngine.World.PhysicsScene != null)
            {
                string physEngine = m_ScriptEngine.World.PhysicsScene.EngineType;
                if (physEngine == "OpenDynamicsEngine")
                {
                    ret = 1;    // true
                }
            }
            return ret;
        }

        public string osGetPhysicsEngineType()
        {
            // High because it can be used to target attacks to known weaknesses
            // This would allow a new class of griefer scripts that don't even
            // require their user to know what they are doing (see script
            // kiddie)
            // Because it would be nice if scripts didn't blow up if the information
            //    about the physics engine, this function returns an empty string if
            //    the user does not have permission to see it. This as opposed to
            //    throwing an exception.

            m_host.AddScriptLPS(1);
            string ret = String.Empty;
            if (String.IsNullOrEmpty(CheckThreatLevelTest(ThreatLevel.High, "osGetPhysicsEngineType")))
            {
                if (m_ScriptEngine.World.PhysicsScene != null)
                {
                    ret = m_ScriptEngine.World.PhysicsScene.EngineType;
                    // An old physics engine might have an uninitialized engine type
                    if (ret == null)
                        ret = "unknown";
                }
            }

            return ret;
        }

        public string osGetPhysicsEngineName()
        {
            CheckThreatLevel();

            string ret = "NoEngine";
            if (m_ScriptEngine.World.PhysicsScene != null)
            {
                ret = m_ScriptEngine.World.PhysicsScene.EngineName;
                // An old physics engine might have an uninitialized engine type
                if (ret == null)
                    ret = "UnknownEngine";
                }
            return ret;
        }

        public string osGetSimulatorVersion()
        {
            // High because it can be used to target attacks to known weaknesses
            // This would allow a new class of griefer scripts that don't even
            // require their user to know what they are doing (see script
            // kiddie)
            //
            CheckThreatLevel(ThreatLevel.High,"osGetSimulatorVersion");

            return m_ScriptEngine.World.GetSimulatorVersion();
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

            UUID objUUID;
            if (!UUID.TryParse(objectUUID, out objUUID)) // prior to patching, a thrown exception regarding invalid GUID format would be shouted instead.
            {
                OSSLShoutError("osMessageObject() cannot send messages to objects with invalid UUIDs");
                return;
            }

            MessageObject(objUUID, message);
        }

        private void MessageObject(UUID objUUID, string message)
        {
            object[] resobj = new object[] { new LSL_Types.LSLString(m_host.UUID.ToString()), new LSL_Types.LSLString(message) };

            SceneObjectPart sceneOP = World.GetSceneObjectPart(objUUID);

            if (sceneOP == null) // prior to patching, PostObjectEvent() would cause a throw exception to be shouted instead.
            {
                OSSLShoutError("osMessageObject() cannot send message to " + objUUID.ToString() + ", object was not found in scene.");
                return;
            }

            m_ScriptEngine.PostObjectEvent(
                sceneOP.LocalId, new EventParams(
                    "dataserver", resobj, new DetectParams[0]));
        }


        /// <summary>
        /// Similar to llDie but given an object UUID
        /// </summary>
        /// <param name="objectUUID"></param>

        public void osDie(LSL_Key objectUUID)
        {
//            CheckThreatLevel(ThreatLevel.VeryHigh, "osDie");
            // if this is restricted to objects rezzed by this host level can be reduced

            CheckThreatLevel(ThreatLevel.Low, "osDie");

            UUID objUUID;
            if (!UUID.TryParse(objectUUID, out objUUID))
            {
                OSSLShoutError("osDie() cannot delete objects with invalid UUIDs");
                return;
            }

            InitLSL();
            // harakiri check
            if(objUUID == UUID.Zero)
            {
                if (!m_host.ParentGroup.IsAttachment)
                    m_LSL_Api.llDie();
                return;
            }

            SceneObjectGroup sceneOG = World.GetSceneObjectGroup(objUUID);

            if (sceneOG == null || sceneOG.IsDeleted)
                return;

            if(sceneOG.IsAttachment)
                return;

            if (sceneOG.OwnerID != m_host.OwnerID)
                return;

            // harakiri check
            if(sceneOG.UUID == m_host.ParentGroup.UUID)
            {
                m_LSL_Api.llDie();
                return;
            }

            // restrict to objects rezzed by host
            if(sceneOG.RezzerID == m_host.ParentGroup.UUID)
                World.DeleteSceneObject(sceneOG, false);
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
            byte[] a;
            byte[] b;
            byte[] c;

            b = Util.UTF8.GetBytes(data);

            a = Util.UTF8.GetBytes(
                "Linden text version 2\n{\nLLEmbeddedItems version 1\n{\ncount 0\n}\nText length " + b.Length.ToString() + "\n");

            c = Util.UTF8.GetBytes("}");

            byte[] d = new byte[a.Length + b.Length + c.Length];
            Buffer.BlockCopy(a, 0, d, 0, a.Length);
            Buffer.BlockCopy(b, 0, d, a.Length, b.Length);
            Buffer.BlockCopy(c, 0, d, a.Length + b.Length, c.Length);

            asset.Data = d;
            World.AssetService.Store(asset);

            // Create Task Entry
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ResetIDs(m_host.UUID);
            taskItem.ParentID = m_host.UUID;
            taskItem.CreationDate = (uint)Util.UnixTimeSinceEpoch();
            taskItem.Name = name;
            taskItem.Description = description;
            taskItem.Type = (int)AssetType.Notecard;
            taskItem.InvType = (int)InventoryType.Notecard;
            taskItem.OwnerID = m_host.OwnerID;
            taskItem.CreatorID = m_host.OwnerID;
            taskItem.BasePermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
            taskItem.CurrentPermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
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
            m_host.ParentGroup.InvalidateDeepEffectivePerms();

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

            if (assetID != UUID.Zero)
            {
                StringBuilder notecardData = new StringBuilder();

                for (int count = 0; count < NotecardCache.GetLines(assetID); count++)
                {
                    string line = NotecardCache.GetLine(assetID, count) + "\n";

    //                m_log.DebugFormat("[OSSL]: From notecard {0} loading line {1}", notecardNameOrUuid, line);

                    notecardData.Append(line);
                }

                return notecardData.ToString();
            }

            return null;
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

            bool notecardNameIsUUID = UUID.TryParse(notecardNameOrUuid, out assetID);

            if (!notecardNameIsUUID)
            {
                assetID = SearchTaskInventoryForAssetId(notecardNameOrUuid);
            }

            if (assetID == UUID.Zero)
                return UUID.Zero;

            if (!NotecardCache.IsCached(assetID))
            {
                AssetBase a = World.AssetService.Get(assetID.ToString());

                if (a == null)
                {
                    // Whoops, it's still possible here that the notecard name was properly
                    // formatted like a UUID but isn't an asset UUID so lets look it up by name after all
                    assetID = SearchTaskInventoryForAssetId(notecardNameOrUuid);
                    if (assetID == UUID.Zero)
                        return UUID.Zero;

                    if (!NotecardCache.IsCached(assetID))
                    {
                        a = World.AssetService.Get(assetID.ToString());

                        if (a == null)
                        {
                            return UUID.Zero;
                        }
                    }
                }

                NotecardCache.Cache(assetID, a.Data);
            };

            return assetID;
        }
        protected UUID SearchTaskInventoryForAssetId(string name)
        {
            UUID assetId = UUID.Zero;
            m_host.TaskInventory.LockItemsForRead(true);
            foreach (TaskInventoryItem item in m_host.TaskInventory.Values)
            {
                if (item.Type == 7 && item.Name == name)
                {
                    assetId = item.AssetID;
                }
            }
            m_host.TaskInventory.LockItemsForRead(false);
            return assetId;
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

            IUserManagement userManager = World.RequestModuleInterface<IUserManagement>();
            if (userManager == null)
            {
                OSSLShoutError("osAvatarName2Key: UserManagement module not available");
                return string.Empty;
            }

            // Check local grid
            UUID userID = userManager.GetUserIdByName(firstname, lastname);
            if (userID != UUID.Zero)
                return userID.ToString();

            // HG ?
            string realFirstName;
            string realLastName;
            string serverURI;
            if (Util.ParseForeignAvatarName(firstname, lastname, out realFirstName, out realLastName, out serverURI))
            {
                try
                {
                    UserAgentServiceConnector userConnection = new UserAgentServiceConnector(serverURI, true);

                    if (userConnection != null)
                    {
                        userID = userConnection.GetUUID(realFirstName, realLastName);
                        if (userID != UUID.Zero)
                        {
                            userManager.AddUser(userID, realFirstName, realLastName, serverURI);
                            return userID.ToString();
                        }
                    }
                }
                catch (Exception /*e*/)
                {
                    // m_log.Warn("[osAvatarName2Key] UserAgentServiceConnector - Unable to connect to destination grid ", e);
                }
            }

            return UUID.Zero.ToString();
        }

        public string osKey2Name(string id)
        {
            CheckThreatLevel(ThreatLevel.Low, "osKey2Name");

            UUID key = new UUID();

            if (UUID.TryParse(id, out key))
            {
                UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
                if (account != null)
                    return account.Name;

                if (m_ScriptEngine.World.GridUserService != null)
                {
                    GridUserInfo uInfo = m_ScriptEngine.World.GridUserService.GetGridUserInfo(key.ToString());

                    if (uInfo != null)
                    {
                        UUID userUUID; String gridURL; String firstName; String lastName; String tmp;

                        if (Util.ParseUniversalUserIdentifier(uInfo.UserID, out userUUID, out gridURL, out firstName, out lastName, out tmp))
                        {
                            string grid = new Uri(gridURL).Authority;
                            return firstName + "." + lastName + " @" + grid;
                        }
                    }
                }
            }

            return "";
        }

        private enum InfoType
        {
            Nick,
            Name,
            Login,
            Home,
            Custom
        };

        private string GridUserInfo(InfoType type)
        {
            return GridUserInfo(type, "");
        }

        private string GridUserInfo(InfoType type, string key)
        {
            string retval = String.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;
            string url = null;

            IConfig gridInfoConfig = config.Configs["GridInfo"];

            if (gridInfoConfig != null)
                url = gridInfoConfig.GetString("GridInfoURI", String.Empty);

            if (String.IsNullOrEmpty(url))
                return "Configuration Error!";

            string verb ="/json_grid_info";
            OSDMap json = new OSDMap();

            OSDMap info =  WebUtil.GetFromService(String.Format("{0}{1}",url,verb), 3000);

            if (info["Success"] != true)
                return "Get GridInfo Failed!";

            json = (OSDMap)OSDParser.DeserializeJson(info["_RawResult"].AsString());

            switch (type)
            {
                case InfoType.Nick:
                    retval = json["gridnick"];
                    break;

                case InfoType.Name:
                    retval = json["gridname"];
                    break;

                case InfoType.Login:
                    retval = json["login"];
                    break;

                case InfoType.Home:
                    retval = json["home"];
                    break;

                case InfoType.Custom:
                    retval = json[key];
                    break;

                default:
                    retval = "error";
                    break;
            }

            return retval;
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

            string nick = String.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;

            if (config.Configs[GridInfoServiceConfigSectionName] != null)
                nick = config.Configs[GridInfoServiceConfigSectionName].GetString("gridnick", nick);

            if (String.IsNullOrEmpty(nick))
                nick = GridUserInfo(InfoType.Nick);

            return nick;
        }

        public string osGetGridName()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridName");

            string name = String.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;

            if (config.Configs[GridInfoServiceConfigSectionName] != null)
                name = config.Configs[GridInfoServiceConfigSectionName].GetString("gridname", name);

            if (String.IsNullOrEmpty(name))
                name = GridUserInfo(InfoType.Name);

            return name;
        }

        public string osGetGridLoginURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridLoginURI");

            string loginURI = String.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;

            if (config.Configs[GridInfoServiceConfigSectionName] != null)
                loginURI = config.Configs[GridInfoServiceConfigSectionName].GetString("login", loginURI);

            if (String.IsNullOrEmpty(loginURI))
                loginURI = GridUserInfo(InfoType.Login);

            return loginURI;
        }

        public string osGetGridHomeURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridHomeURI");

            IConfigSource config = m_ScriptEngine.ConfigSource;
            string HomeURI = Util.GetConfigVarFromSections<string>(config, "HomeURI",
                new string[] { "Startup", "Hypergrid" }, String.Empty);

            if (!string.IsNullOrEmpty(HomeURI))
                return HomeURI;

            // Legacy. Remove soon!
            if (config.Configs["LoginService"] != null)
                HomeURI = config.Configs["LoginService"].GetString("SRV_HomeURI", HomeURI);

            if (String.IsNullOrEmpty(HomeURI))
                HomeURI = GridUserInfo(InfoType.Home);

            return HomeURI;
        }

        public string osGetGridGatekeeperURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridGatekeeperURI");

            IConfigSource config = m_ScriptEngine.ConfigSource;
            string gatekeeperURI = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI",
                new string[] { "Startup", "Hypergrid" }, String.Empty);

            if (!string.IsNullOrEmpty(gatekeeperURI))
                return gatekeeperURI;

            // Legacy. Remove soon!
            if (config.Configs["GridService"] != null)
                gatekeeperURI = config.Configs["GridService"].GetString("Gatekeeper", gatekeeperURI);

            return gatekeeperURI;
        }

        public string osGetGridCustom(string key)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridCustom");

            string retval = String.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;

            if (config.Configs[GridInfoServiceConfigSectionName] != null)
                retval = config.Configs[GridInfoServiceConfigSectionName].GetString(key, retval);

            if (String.IsNullOrEmpty(retval))
                retval = GridUserInfo(InfoType.Custom, key);

            return retval;
        }

        public string osGetAvatarHomeURI(string uuid)
        {
            CheckThreatLevel(ThreatLevel.Low, "osGetAvatarHomeURI");

            IUserManagement userManager = m_ScriptEngine.World.RequestModuleInterface<IUserManagement>();
            string returnValue = "";

            if (userManager != null)
            {
                returnValue = userManager.GetUserServerURL(new UUID(uuid), "HomeURI");
            }

            if (returnValue == "")
            {
                IConfigSource config = m_ScriptEngine.ConfigSource;
                returnValue = Util.GetConfigVarFromSections<string>(config, "HomeURI",
                    new string[] { "Startup", "Hypergrid" }, String.Empty);

                if (!string.IsNullOrEmpty(returnValue))
                    return returnValue;

                // Legacy. Remove soon!
                if (config.Configs["LoginService"] != null)
                    returnValue = config.Configs["LoginService"].GetString("SRV_HomeURI", returnValue);

                if (String.IsNullOrEmpty(returnValue))
                    returnValue = GridUserInfo(InfoType.Home);
            }

            return returnValue;
        }

        public LSL_String osFormatString(string str, LSL_List strings)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osFormatString");

            return String.Format(str, strings.Data);
        }

        public LSL_List osMatchString(string src, string pattern, int start)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osMatchString");

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

        public LSL_String osReplaceString(string src, string pattern, string replace, int count, int start)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osReplaceString");

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
                return src;
            }

            // Find matches beginning at start position
            Regex matcher = new Regex(pattern);
            return matcher.Replace(src,replace,count,start);
        }

        public string osLoadedCreationDate()
        {
            CheckThreatLevel(ThreatLevel.Low, "osLoadedCreationDate");

            return World.RegionInfo.RegionSettings.LoadedCreationDate;
        }

        public string osLoadedCreationTime()
        {
            CheckThreatLevel(ThreatLevel.Low, "osLoadedCreationTime");

            return World.RegionInfo.RegionSettings.LoadedCreationTime;
        }

        public string osLoadedCreationID()
        {
            CheckThreatLevel(ThreatLevel.Low, "osLoadedCreationID");

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

            InitLSL();
            // One needs to cast m_LSL_Api because we're using functions not
            // on the ILSL_Api interface.
            LSL_List retVal = new LSL_List();
            LSL_List remaining = new LSL_List();
            List<SceneObjectPart> parts = m_LSL_Api.GetLinkParts(linknumber);
            foreach (SceneObjectPart part in parts)
            {
                remaining = m_LSL_Api.GetPrimParams(part, rules, ref retVal);
            }

            while (remaining.Length > 1)
            {
                linknumber = remaining.GetLSLIntegerItem(0);
                parts = m_LSL_Api.GetLinkParts(linknumber);
                if(parts.Count == 0)
                    break;

                rules = remaining.GetSublist(1, -1);
                foreach (SceneObjectPart part in parts)
                    remaining = m_LSL_Api.GetPrimParams(part, rules, ref retVal);
            }
            return retVal;
        }

        public void osForceCreateLink(string target, int parent)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osForceCreateLink");

            InitLSL();
            m_LSL_Api.CreateLink(target, parent);
        }

        public void osForceBreakLink(int linknum)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osForceBreakLink");

            InitLSL();
            m_LSL_Api.BreakLink(linknum);
        }

        public void osForceBreakAllLinks()
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osForceBreakAllLinks");

            InitLSL();
            m_LSL_Api.BreakAllLinks();
        }

        public LSL_Integer osIsNpc(LSL_Key npc)
        {
            CheckThreatLevel();

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId;
                if (UUID.TryParse(npc.m_string, out npcId))
                    if (module.IsNPC(npcId, World))
                        return ScriptBaseClass.TRUE;
            }

            return ScriptBaseClass.FALSE;
        }

        public LSL_Key osNpcCreate(string firstname, string lastname, LSL_Vector position, string notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcCreate");

            // have to get the npc module also here to set the default Not Owned
            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if(module == null)
                return new LSL_Key(UUID.Zero.ToString());

            bool owned = (module.NPCOptionFlags & NPCOptionsFlags.AllowNotOwned) == 0;

            return NpcCreate(firstname, lastname, position, notecard, owned, false, false);
        }

        public LSL_Key osNpcCreate(string firstname, string lastname, LSL_Vector position, string notecard, int options)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcCreate");

            return NpcCreate(
                firstname, lastname, position, notecard,
                (options & ScriptBaseClass.OS_NPC_NOT_OWNED) == 0,
                (options & ScriptBaseClass.OS_NPC_SENSE_AS_AGENT) != 0,
                (options & ScriptBaseClass.OS_NPC_OBJECT_GROUP) != 0);
        }

        private LSL_Key NpcCreate(
            string firstname, string lastname, LSL_Vector position, string notecard, bool owned, bool senseAsAgent, bool hostGroupID)
        {
            if (!World.Permissions.CanRezObject(1, m_host.OwnerID, new Vector3((float)position.x, (float)position.y, (float)position.z)))
            {
                OSSLError("no permission to rez NPC at requested location");
                return new LSL_Key(UUID.Zero.ToString());
            }

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if(module == null)
            {
                OSSLError("NPC module not enabled");
                return new LSL_Key(UUID.Zero.ToString());
            }

            string groupTitle = String.Empty;
            UUID groupID = UUID.Zero;

            AvatarAppearance appearance = null;

            // check creation options
            NPCOptionsFlags createFlags = module.NPCOptionFlags;

            if((createFlags & NPCOptionsFlags.AllowNotOwned) == 0 && !owned)
            {
                OSSLError("Not owned NPCs disabled");
                owned = true; // we should get here...
            }

            if((createFlags & NPCOptionsFlags.AllowSenseAsAvatar) == 0 && senseAsAgent)
            {
                OSSLError("NPC allow sense as Avatar disabled");
                senseAsAgent = false;
            }

            if(hostGroupID && m_host.GroupID != UUID.Zero)
            {
                IGroupsModule groupsModule = m_ScriptEngine.World.RequestModuleInterface<IGroupsModule>();
                if (groupsModule != null)
                {
                    GroupMembershipData member = groupsModule.GetMembershipData(m_host.GroupID, m_host.OwnerID);
                    if (member == null)
                    {
                        OSSLError(string.Format("osNpcCreate: the object owner is not member of the object group"));
                        return new LSL_Key(UUID.Zero.ToString());
                    }

                    groupID = m_host.GroupID;

                    if((createFlags & NPCOptionsFlags.NoNPCGroup) != 0)
                    {
                        GroupRecord grprec = groupsModule.GetGroupRecord(m_host.GroupID);
                        if(grprec != null && grprec.GroupName != "")
                            groupTitle = grprec.GroupName;
                    }
                }
            }

            if((createFlags & NPCOptionsFlags.NoNPCGroup) == 0)
            {
                if (firstname != String.Empty || lastname != String.Empty)
                {
                    if (firstname != "Shown outfit:")
                        groupTitle = "- NPC -";
                }
            }

            if((createFlags & NPCOptionsFlags.AllowCloneOtherAvatars) != 0)
            {
                UUID id;
                if (UUID.TryParse(notecard, out id))
                {
                    ScenePresence clonePresence = World.GetScenePresence(id);
                    if (clonePresence != null)
                        appearance = clonePresence.Appearance;
                }
            }

            if (appearance == null)
            {
                if(!string.IsNullOrWhiteSpace(notecard))
                {
                    string appearanceSerialized = LoadNotecard(notecard);
                    if (appearanceSerialized == null)
                    {
                        OSSLError(string.Format("osNpcCreate: Notecard '{0}' not found.", notecard));
                        return new LSL_Key(UUID.Zero.ToString());
                    }

                    try
                    {
                        OSDMap appearanceOsd = (OSDMap)OSDParser.DeserializeLLSDXml(appearanceSerialized);
                        appearance = new AvatarAppearance();
                        appearance.Unpack(appearanceOsd);
                    }
                    catch
                    {
                        OSSLError(string.Format("osNpcCreate: Error processing notecard '{0}'", notecard));
                        return new LSL_Key(UUID.Zero.ToString());
                    }
                }
            }

            UUID ownerID = UUID.Zero;
            if (owned)
                ownerID = m_host.OwnerID;
            UUID x = module.CreateNPC(firstname,
                                      lastname,
                                      position,
                                      UUID.Random(),
                                      ownerID,
                                      groupTitle,
                                      groupID,
                                      senseAsAgent,
                                      World,
                                      appearance);

            ScenePresence sp;
            if (World.TryGetScenePresence(x, out sp))
            {
                sp.SendAvatarDataToAllAgents();
            }
            return new LSL_Key(x.ToString());
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

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
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

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                string appearanceSerialized = LoadNotecard(notecard);

                if (appearanceSerialized == null)
                    OSSLError(string.Format("osNpcCreate: Notecard reference '{0}' not found.", notecard));

                OSDMap appearanceOsd = (OSDMap)OSDParser.DeserializeLLSDXml(appearanceSerialized);
//                OSD a = OSDParser.DeserializeLLSDXml(appearanceSerialized);
//                Console.WriteLine("appearanceSerialized {0}", appearanceSerialized);
//                Console.WriteLine("a.Type {0}, a.ToString() {1}", a.Type, a);
                AvatarAppearance appearance = new AvatarAppearance();
                appearance.Unpack(appearanceOsd);

                npcModule.SetNPCAppearance(npcId, appearance, m_host.ParentGroup.Scene);
            }
        }

        public LSL_Key osNpcGetOwner(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.None, "osNpcGetOwner");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule != null)
            {
                UUID npcId;
                if (UUID.TryParse(npc.m_string, out npcId))
                {
                    UUID owner = npcModule.GetOwner(npcId);
                    if (owner != UUID.Zero)
                        return new LSL_Key(owner.ToString());
                    else
                        return npc;
                }
            }

            return new LSL_Key(UUID.Zero.ToString());
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

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return new LSL_Vector(0, 0, 0);

                ScenePresence sp = World.GetScenePresence(npcId);

                if (sp != null)
                    return new LSL_Vector(sp.AbsolutePosition);
            }

            return Vector3.Zero;
        }

        public void osNpcMoveTo(LSL_Key npc, LSL_Vector pos)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcMoveTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId;
                if (!UUID.TryParse(npc.m_string, out npcId))
                    return;

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.MoveToTarget(npcId, World, pos, false, true, false);
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

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.MoveToTarget(
                    new UUID(npc.m_string),
                    World,
                    target,
                    (options & ScriptBaseClass.OS_NPC_NO_FLY) != 0,
                    (options & ScriptBaseClass.OS_NPC_LAND_AT_TARGET) != 0,
                    (options & ScriptBaseClass.OS_NPC_RUNNING) != 0);
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
                    return new LSL_Rotation(Quaternion.Identity);

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return new LSL_Rotation(Quaternion.Identity);

                ScenePresence sp = World.GetScenePresence(npcId);

                if (sp != null)
                    return new LSL_Rotation(sp.GetWorldRotation());
            }

            return Quaternion.Identity;
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

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                ScenePresence sp = World.GetScenePresence(npcId);

                if (sp != null)
                    sp.Rotation = rotation;
            }
        }

        public void osNpcStopMoveToTarget(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStopMoveToTarget");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.StopMoveToTarget(npcId, World);
            }
        }

        public void osNpcSetProfileAbout(LSL_Key npc, string about)
        {
            CheckThreatLevel(ThreatLevel.Low, "osNpcSetProfileAbout");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                ScenePresence sp = World.GetScenePresence(npcId);
                if (sp != null)
                    ((INPC)(sp.ControllingClient)).profileAbout = about;
            }
        }

        public void osNpcSetProfileImage(LSL_Key npc, string image)
        {
            CheckThreatLevel(ThreatLevel.Low, "osNpcSetProfileImage");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                UUID ImageID = new UUID();

                ImageID = ScriptUtils.GetAssetIdFromItemName(m_host, image, (int)AssetType.Texture);

                if (ImageID == null || ImageID == UUID.Zero)
                {
                    if (!UUID.TryParse(image, out ImageID))
                        return;
                }

                ScenePresence sp = World.GetScenePresence(npcId);
                if (sp != null)
                    ((INPC)(sp.ControllingClient)).profileImage = ImageID;
            }
        }

        public void osNpcSay(LSL_Key npc, string message)
        {
            osNpcSay(npc, 0, message);
        }

        public void osNpcSay(LSL_Key npc, int channel, string message)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSay");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.Say(npcId, World, message, channel);
            }
        }

        public void osNpcSayTo(LSL_Key npc, LSL_Key target, int channel, string msg)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSayTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module == null)
                return;

            UUID npcId;
            if (!UUID.TryParse(npc.m_string, out npcId))
                return;

            UUID TargetID;
            if (!UUID.TryParse(target.m_string, out TargetID))
                return;

            if (!module.CheckPermissions(npcId, m_host.OwnerID))
                return;

            ScenePresence NPCpresence = World.GetScenePresence(npcId);
            if (NPCpresence == null || NPCpresence.IsDeleted || !NPCpresence.IsNPC)
                return;

            Vector3 npcPOS = NPCpresence.AbsolutePosition;
            string npcNAME = NPCpresence.Name;

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.DeliverMessageTo(TargetID, channel, npcPOS, npcNAME, npcId, msg);
        }

        public void osNpcShout(LSL_Key npc, int channel, string message)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcShout");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.Shout(npcId, World, message, channel);
            }
        }

        public void osNpcSit(LSL_Key npc, LSL_Key target, int options)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSit");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.Sit(npcId, new UUID(target.m_string), World);
            }
        }

        public void osNpcStand(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStand");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.Stand(npcId, World);
            }
        }

        public void osNpcRemove(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcRemove");

            try
            {
                INPCModule module = World.RequestModuleInterface<INPCModule>();
                if (module != null)
                {
                    UUID npcId = new UUID(npc.m_string);

                    if (!module.CheckPermissions(npcId, m_host.OwnerID))
                        return;

                    module.DeleteNPC(npcId, World);
                }
            }
            catch { }
        }

        public void osNpcPlayAnimation(LSL_Key npc, string animation)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcPlayAnimation");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module == null)
                return;

            UUID npcID;
            if(!UUID.TryParse(npc.m_string, out npcID))
                return;

            ScenePresence target = World.GetScenePresence(npcID);
            if (target == null || !target.IsNPC)
                return;

            if (!module.CheckPermissions(npcID, m_host.OwnerID))
                return;

            UUID animID = UUID.Zero;
            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
               if (inv.Value.Type == (int)AssetType.Animation)
               {
                   if (inv.Value.Name == animation)
                   {
                       animID = inv.Value.AssetID;
                       break;
                   }
               }
            }
            m_host.TaskInventory.LockItemsForRead(false);

            if (animID == UUID.Zero)
                target.Animator.AddAnimation(animation, m_host.UUID);
            else
                target.Animator.AddAnimation(animID, m_host.UUID);
        }

        public void osNpcStopAnimation(LSL_Key npc, string animation)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStopAnimation");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module == null)
                return;

            UUID npcID;
            if (!UUID.TryParse(npc.m_string, out npcID))
                return;

            ScenePresence target = World.GetScenePresence(npcID);
            if (target == null || !target.IsNPC)
                return;

            if (!module.CheckPermissions(npcID, m_host.OwnerID))
                return;

            UUID animID;
            if (!UUID.TryParse(animation, out animID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(animation);
                if (item != null && item.Type == (int)AssetType.Animation)
                    animID = item.AssetID;
                else
                    animID = UUID.Zero;
            }

            if (animID == UUID.Zero)
                target.Animator.RemoveAnimation(animation);
            else
                target.Animator.RemoveAnimation(animID, true);
        }

        public void osNpcWhisper(LSL_Key npc, int channel, string message)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcWhisper");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                UUID npcId = new UUID(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.Whisper(npcId, World, message, channel);
            }
        }

        public void osNpcTouch(LSL_Key npcLSL_Key, LSL_Key object_key, LSL_Integer link_num)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcTouch");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            int linkNum = link_num.value;
            if (module != null || (linkNum < 0 && linkNum != ScriptBaseClass.LINK_THIS))
            {
                UUID npcId;
                if (!UUID.TryParse(npcLSL_Key, out npcId) || !module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                SceneObjectPart part = null;
                UUID objectId;
                if (UUID.TryParse(LSL_String.ToString(object_key), out objectId))
                    part = World.GetSceneObjectPart(objectId);

                if (part == null)
                    return;

                if (linkNum != ScriptBaseClass.LINK_THIS)
                {
                    if (linkNum == 0 || linkNum == ScriptBaseClass.LINK_ROOT)
                    { // 0 and 1 are treated as root, find the root if the current part isnt it
                        if (!part.IsRoot)
                            part = part.ParentGroup.RootPart;
                    }
                    else
                    { // Find the prim with the given link number if not found then fail silently
                        part = part.ParentGroup.GetLinkNumPart(linkNum);
                        if (part == null)
                            return;
                    }
                }

                module.Touch(npcId, part.UUID);
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
                EntityTransferContext ctx = new EntityTransferContext();
                OSDMap appearancePacked = sp.Appearance.Pack(ctx);

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
        /// Get the gender as specified in avatar appearance for a given avatar key
        /// </summary>
        /// <param name="rawAvatarId"></param>
        /// <returns>"male" or "female" or "unknown"</returns>
        public LSL_String osGetGender(LSL_Key rawAvatarId)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetGender");

            UUID avatarId;
            if (!UUID.TryParse(rawAvatarId, out avatarId))
                return new LSL_String("unknown");

            ScenePresence sp = World.GetScenePresence(avatarId);

            if (sp == null || sp.IsChildAgent || sp.Appearance == null || sp.Appearance.VisualParams == null)
                return new LSL_String("unknown");

            // find the index of "shape" parameter "male"
            int vpShapeMaleIndex = 0;
            bool indexFound = false;
            VisualParam param = new VisualParam();
            foreach(var vpEntry in VisualParams.Params)
            {
                param = vpEntry.Value;
                if (param.Name == "male" && param.Wearable == "shape")
                {
                    indexFound = true;
                    break;
                }

                if (param.Group == 0)
                    vpShapeMaleIndex++;
            }

            if (!indexFound)
                return new LSL_String("unknown");

            float vpShapeMale = Utils.ByteToFloat(sp.Appearance.VisualParams[vpShapeMaleIndex], param.MinValue, param.MaxValue);

            bool isMale = vpShapeMale > 0.5f;
            return new LSL_String(isMale ? "male" : "female");
        }

        /// <summary>
        /// Get current region's map texture UUID
        /// </summary>
        /// <returns></returns>
        public LSL_Key osGetMapTexture()
        {
            CheckThreatLevel();

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

            LSL_List ret = new LSL_List();
            float[] stats = World.StatsReporter.LastReportedSimStats;

            for (int i = 0; i < 21; i++)
            {
                ret.Add(new LSL_Float(stats[i]));
            }
            return ret;
        }

        public LSL_Vector osGetRegionSize()
        {
            CheckThreatLevel();

            Scene scene = m_ScriptEngine.World;
            RegionInfo reg =  World.RegionInfo;
//            GridRegion region = scene.GridService.GetRegionByUUID(UUID.Zero, World.RegionInfo.RegionID);
//            return new LSL_Vector((float)region.RegionSizeX, (float)region.RegionSizeY, (float)Constants.RegionHeight);
            return new LSL_Vector((float)reg.RegionSizeX, (float)reg.RegionSizeY, 0.0f);
        }

        public int osGetSimulatorMemory()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetSimulatorMemory");

            long pws = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

            if (pws > Int32.MaxValue)
                return Int32.MaxValue;
            if (pws < 0)
                return 0;

            return (int)pws;
        }

        public int osGetSimulatorMemoryKB()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetSimulatorMemoryKB");

            long pws = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

            if((pws & 0x3FFL) != 0)
                pws += 0x400L;
            pws >>= 10;

            if (pws > Int32.MaxValue)
                return Int32.MaxValue;

            return (int)pws;
        }

        public void osSetSpeed(string ID, LSL_Float SpeedModifier)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osSetSpeed");

            UUID avid;
            if(!UUID.TryParse(ID, out avid))
                return;

            ScenePresence avatar = World.GetScenePresence(avid);
            if (avatar != null)
                avatar.SpeedModifier = (float)SpeedModifier;
        }

        public void osSetOwnerSpeed(LSL_Float SpeedModifier)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osSetOwnerSpeed");

            if(SpeedModifier > 4)
                SpeedModifier = 4;

            ScenePresence avatar = World.GetScenePresence(m_host.OwnerID);
            if (avatar != null)
                avatar.SpeedModifier = (float)SpeedModifier;
        }

        public void osKickAvatar(string FirstName, string SurName, string alert)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osKickAvatar");

            World.ForEachRootScenePresence(delegate (ScenePresence sp)
            {
                if (sp.Firstname == FirstName && sp.Lastname == SurName)
                {
                    // kick client...
                    if (alert != null)
                        sp.ControllingClient.Kick(alert);

                    // ...and close on our side
                    sp.Scene.CloseAgent(sp.UUID, false);
                }
            });
        }

        public void osKickAvatar(LSL_Key agentKey, string alert)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osKickAvatar");

            UUID id;
            if (!UUID.TryParse(agentKey, out id) || id == UUID.Zero)
                return;

            ScenePresence sp = World.GetScenePresence(id);
            if(sp == null)
                return;

            // kick client...
            if (alert != null)
                sp.ControllingClient.Kick(alert);

            // ...and close on our side
            sp.Scene.CloseAgent(id, false);
        }

        public LSL_Float osGetHealth(LSL_Key agentKey)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetHealth");

            LSL_Float health = new LSL_Float(-1);

            UUID id;
            if (!UUID.TryParse(agentKey, out id) || id == UUID.Zero)
                return health;

            ScenePresence presence = World.GetScenePresence(id);
            if (presence != null)
                health = presence.Health;
            return health;
        }

        public void osCauseDamage(LSL_Key avatar, LSL_Float damage)
        {
            CheckThreatLevel(ThreatLevel.High, "osCauseDamage");

            UUID avatarId;
            if (!UUID.TryParse(avatar, out avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence == null)
                return;

            Vector3 pos = m_host.GetWorldPosition();
            LandData land = World.GetLandData(pos);
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

        public void osCauseHealing(LSL_Key avatar, LSL_Float healing)
        {
            CheckThreatLevel(ThreatLevel.High, "osCauseHealing");

            UUID avatarId;
            if (!UUID.TryParse(avatar, out avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence == null)
                return;

            float health = presence.Health;
            health += (float)healing;

            if (health >= 100)
                health = 100;

            presence.setHealthWithUpdate(health);
        }

        public void osSetHealth(LSL_Key avatar, LSL_Float health)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetHealth");

            UUID avatarId = new UUID(avatar);
            ScenePresence presence = World.GetScenePresence(avatarId);

            if (presence != null)
            {
                if (health > 100.0)
                    health = 100.0;
                else if (health < 1.0)
                    health = 1.0;

                presence.setHealthWithUpdate((float)health);
            }
        }

        public void osSetHealRate(LSL_Key avatar, LSL_Float healrate)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetHealRate");

            UUID avatarId;
            if (!UUID.TryParse(avatar, out avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence == null)
                return;

            presence.HealRate = (float)healrate;
        }

        public LSL_Float osGetHealRate(LSL_Key avatar)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetHealRate");

            LSL_Float rate = new LSL_Float(0);

            UUID avatarId;
            if (!UUID.TryParse(avatar, out avatarId))
                return rate;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence != null)
                rate = presence.HealRate;
            return rate;
        }

        public LSL_List osGetPrimitiveParams(LSL_Key prim, LSL_List rules)
        {
            CheckThreatLevel();

            InitLSL();
            return m_LSL_Api.GetPrimitiveParamsEx(prim, rules);
        }

        public void osSetPrimitiveParams(LSL_Key prim, LSL_List rules)
        {
            CheckThreatLevel();

            InitLSL();
            m_LSL_Api.SetPrimitiveParamsEx(prim, rules, "osSetPrimitiveParams");
        }

        /// <summary>
        /// Set parameters for light projection in host prim
        /// </summary>
        public void osSetProjectionParams(bool projection, LSL_Key texture, double fov, double focus, double amb)
        {
            osSetProjectionParams(UUID.Zero.ToString(), projection, texture, fov, focus, amb);
        }

        /// <summary>
        /// Set parameters for light projection with uuid of target prim
        /// </summary>
        public void osSetProjectionParams(LSL_Key prim, bool projection, LSL_Key texture, double fov, double focus, double amb)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetProjectionParams");

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
            World.ForEachRootScenePresence(delegate (ScenePresence avatar)
            {
                if (avatar != null && !avatar.IsDeleted && avatar.UUID != m_host.OwnerID )
                {
                    result.Add(new LSL_String(avatar.UUID.ToString()));
                    result.Add(new LSL_Vector(avatar.AbsolutePosition));
                    result.Add(new LSL_String(avatar.Name));
                }
            });

            return result;
        }

        public LSL_List osGetNPCList()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetNPCList");

            LSL_List result = new LSL_List();
            World.ForEachRootScenePresence(delegate (ScenePresence avatar)
            {
                // npcs are not childagents but that is now.
                if (avatar != null && avatar.IsNPC && !avatar.IsDeleted && !avatar.IsChildAgent && !avatar.IsInTransit)
                {
                    result.Add(new LSL_String(avatar.UUID.ToString()));
                    result.Add(new LSL_Vector(avatar.AbsolutePosition));
                    result.Add(new LSL_String(avatar.Name));
                }
            });

            return result;
        }

        /// <summary>
        /// Convert a unix time to a llGetTimestamp() like string
        /// </summary>
        /// <param name="unixTime"></param>
        /// <returns></returns>
        public LSL_String osUnixTimeToTimestamp(LSL_Integer time)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osUnixTimeToTimestamp");

            long baseTicks = 621355968000000000;
            long tickResolution = 10000000;
            long epochTicks = (time * tickResolution) + baseTicks;
            DateTime date = new DateTime(epochTicks);

            return date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }

        /// <summary>
        /// Invite user to the group this object is set to
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public LSL_Integer osInviteToGroup(LSL_Key agentId)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osInviteToGroup");

            UUID agent = new UUID(agentId);

            // groups module is required
            IGroupsModule groupsModule = m_ScriptEngine.World.RequestModuleInterface<IGroupsModule>();
            if (groupsModule == null) return ScriptBaseClass.FALSE;

            // object has to be set to a group, but not group owned
            if (m_host.GroupID == UUID.Zero || m_host.GroupID == m_host.OwnerID) return ScriptBaseClass.FALSE;

            // object owner has to be in that group and required permissions
            GroupMembershipData member = groupsModule.GetMembershipData(m_host.GroupID, m_host.OwnerID);
            if (member == null || (member.GroupPowers & (ulong)GroupPowers.Invite) == 0) return ScriptBaseClass.FALSE;

            // check if agent is in that group already
            //member = groupsModule.GetMembershipData(agent, m_host.GroupID, agent);
            //if (member != null) return ScriptBaseClass.FALSE;

            // invited agent has to be present in this scene
            if (World.GetScenePresence(agent) == null) return ScriptBaseClass.FALSE;

            groupsModule.InviteGroup(null, m_host.OwnerID, m_host.GroupID, agent, UUID.Zero);

            return ScriptBaseClass.TRUE;
        }

        /// <summary>
        /// Eject user from the group this object is set to
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public LSL_Integer osEjectFromGroup(LSL_Key agentId)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osEjectFromGroup");

            UUID agent = new UUID(agentId);

            // groups module is required
            IGroupsModule groupsModule = m_ScriptEngine.World.RequestModuleInterface<IGroupsModule>();
            if (groupsModule == null) return ScriptBaseClass.FALSE;

            // object has to be set to a group, but not group owned
            if (m_host.GroupID == UUID.Zero || m_host.GroupID == m_host.OwnerID) return ScriptBaseClass.FALSE;

            // object owner has to be in that group and required permissions
            GroupMembershipData member = groupsModule.GetMembershipData(m_host.GroupID, m_host.OwnerID);
            if (member == null || (member.GroupPowers & (ulong)GroupPowers.Eject) == 0) return ScriptBaseClass.FALSE;

            // agent has to be in that group
            //member = groupsModule.GetMembershipData(agent, m_host.GroupID, agent);
            //if (member == null) return ScriptBaseClass.FALSE;

            // ejectee can be offline

            groupsModule.EjectGroupMember(null, m_host.OwnerID, m_host.GroupID, agent);

            return ScriptBaseClass.TRUE;
        }

        /// <summary>
        /// Sets terrain estate texture
        /// </summary>
        /// <param name="level"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        public void osSetTerrainTexture(int level, LSL_Key texture)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetTerrainTexture");

            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.Permissions.IsGod(m_host.OwnerID))
            {
                if (level < 0 || level > 3)
                    return;

                UUID textureID = new UUID();
                if (!UUID.TryParse(texture, out textureID))
                    return;

                // estate module is required
                IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
                if (estate != null)
                    estate.setEstateTerrainBaseTexture(level, textureID);
            }
        }

        /// <summary>
        /// Sets terrain heights of estate
        /// </summary>
        /// <param name="corner"></param>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <returns></returns>
        public void osSetTerrainTextureHeight(int corner, double low, double high)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetTerrainTextureHeight");

            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.Permissions.IsGod(m_host.OwnerID))
            {
                if (corner < 0 || corner > 3)
                    return;

                // estate module is required
                IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
                if (estate != null)
                    estate.setEstateTerrainTextureHeights(corner, (float)low, (float)high);
            }
        }

        #region Attachment commands

        public void osForceAttachToAvatar(int attachmentPoint)
        {
            CheckThreatLevel(ThreatLevel.High, "osForceAttachToAvatar");

            InitLSL();
            m_LSL_Api.AttachToAvatar(attachmentPoint);
        }

        public void osForceAttachToAvatarFromInventory(string itemName, int attachmentPoint)
        {
            CheckThreatLevel(ThreatLevel.High, "osForceAttachToAvatarFromInventory");

            ForceAttachToAvatarFromInventory(m_host.OwnerID, itemName, attachmentPoint);
        }

        public void osForceAttachToOtherAvatarFromInventory(string rawAvatarId, string itemName, int attachmentPoint)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osForceAttachToOtherAvatarFromInventory");

            UUID avatarId;

            if (!UUID.TryParse(rawAvatarId, out avatarId))
                return;

            ForceAttachToAvatarFromInventory(avatarId, itemName, attachmentPoint);
        }

        public void ForceAttachToAvatarFromInventory(UUID avatarId, string itemName, int attachmentPoint)
        {
            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;

            if (attachmentsModule == null)
                return;

            InitLSL();

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);

            if (item == null)
            {
                m_LSL_Api.llSay(0, string.Format("Could not find object '{0}'", itemName));
                throw new Exception(String.Format("The inventory item '{0}' could not be found", itemName));
            }

            if (item.InvType != (int)InventoryType.Object)
            {
                // FIXME: Temporary null check for regression tests since they dont' have the infrastructure to set
                // up the api reference.
                if (m_LSL_Api != null)
                   m_LSL_Api.llSay(0, string.Format("Unable to attach, item '{0}' is not an object.", itemName));

                throw new Exception(String.Format("The inventory item '{0}' is not an object", itemName));
            }

            ScenePresence sp = World.GetScenePresence(avatarId);

            if (sp == null)
                return;

            string message;
            InventoryItemBase newItem = World.MoveTaskInventoryItem(sp.UUID, UUID.Zero, m_host, item.ItemID, out message);

            if (newItem == null)
            {
                m_log.ErrorFormat(
                    "[OSSL API]: Could not create user inventory item {0} for {1}, attach point {2} in {3}: {4}",
                    itemName, m_host.Name, attachmentPoint, World.Name, message);
                m_LSL_Api.llSay(0, message);
                return;
            }

            attachmentsModule.RezSingleAttachmentFromInventory(sp, newItem.ID, (uint)attachmentPoint);
        }

        public void osForceDetachFromAvatar()
        {
            CheckThreatLevel(ThreatLevel.High, "osForceDetachFromAvatar");

            InitLSL();
            m_LSL_Api.DetachFromAvatar();
        }

        public LSL_List osGetNumberOfAttachments(LSL_Key avatar, LSL_List attachmentPoints)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetNumberOfAttachments");

            UUID targetUUID;
            ScenePresence target;
            LSL_List resp = new LSL_List();

            if (attachmentPoints.Length >= 1 && UUID.TryParse(avatar.ToString(), out targetUUID) && World.TryGetScenePresence(targetUUID, out target))
            {
                foreach (object point in attachmentPoints.Data)
                {
                    LSL_Integer ipoint = new LSL_Integer(
                        (point is LSL_Integer || point is int || point is uint) ?
                            (int)point :
                            0
                    );
                    resp.Add(ipoint);
                    if (ipoint == 0)
                    {
                        // indicates zero attachments
                        resp.Add(new LSL_Integer(0));
                    }
                    else
                    {
                        // gets the number of attachments on the attachment point
                        resp.Add(new LSL_Integer(target.GetAttachments((uint)ipoint).Count));
                    }
                }
            }

            return resp;
        }

        public void osMessageAttachments(LSL_Key avatar, string message, LSL_List attachmentPoints, int options)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osMessageAttachments");

            UUID targetUUID;
            if(!UUID.TryParse(avatar.ToString(), out targetUUID))
                return;

            if(targetUUID == UUID.Zero)
                return;

            ScenePresence target;
            if(!World.TryGetScenePresence(targetUUID, out target))
               return;

            if(target.IsDeleted || target.IsInTransit)
               return;

            List<int> aps = new List<int>();
            if(attachmentPoints.Length != 0)
            {
                foreach (object point in attachmentPoints.Data)
                {
                    int ipoint;
                    if (int.TryParse(point.ToString(), out ipoint))
                    {
                        aps.Add(ipoint);
                    }
                }
                // parsing failed
                if(aps.Count != attachmentPoints.Length)
                    return;
            }

            List<SceneObjectGroup> attachments = new List<SceneObjectGroup>();

            bool msgAll;
            bool invertPoints = (options & ScriptBaseClass.OS_ATTACH_MSG_INVERT_POINTS) != 0;

            if(aps.Count == 0)
            {
                if(!invertPoints)
                    return;
                msgAll = true;
                invertPoints = false;
            }
            else
                msgAll = aps.Contains(ScriptBaseClass.OS_ATTACH_MSG_ALL);

            if (msgAll && invertPoints)
                return;

            if (msgAll || invertPoints)
            {
                attachments = target.GetAttachments();
            }
            else
            {
                foreach (int point in aps)
                {
                    if (point > 0)
                    {
                        attachments.AddRange(target.GetAttachments((uint)point));
                    }
                }
            }

            // if we have no attachments at this point, exit now
            if (attachments.Count == 0)
            {
                return;
            }

            bool optionObjCreator = (options &
                        ScriptBaseClass.OS_ATTACH_MSG_OBJECT_CREATOR) != 0;
            bool optionScriptCreator = (options &
                        ScriptBaseClass.OS_ATTACH_MSG_SCRIPT_CREATOR) != 0;

            UUID hostCreatorID = m_host.CreatorID;
            UUID itemCreatorID = m_item.CreatorID;

            foreach (SceneObjectGroup sog in attachments)
            {
                if(sog.IsDeleted || sog.inTransit)
                    continue;

                if (invertPoints && aps.Contains((int)sog.AttachmentPoint))
                    continue;

                UUID CreatorID = sog.RootPart.CreatorID;
                if (optionObjCreator && CreatorID != hostCreatorID)
                    continue;

                if (optionScriptCreator && CreatorID != itemCreatorID)
                    continue;

                SceneObjectPart[] parts = sog.Parts;
                foreach(SceneObjectPart p in parts)
                    MessageObject(p.UUID, message);
            }
        }

        #endregion

        /// <summary>
        /// Checks if thing is a UUID.
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>1 if thing is a valid UUID, 0 otherwise</returns>
        public LSL_Integer osIsUUID(string thing)
        {
            CheckThreatLevel();

            UUID test;
            return UUID.TryParse(thing, out test) ? 1 : 0;
        }

        /// <summary>
        /// Wraps to Math.Min()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public LSL_Float osMin(double a, double b)
        {
            CheckThreatLevel();

            return Math.Min(a, b);
        }

        /// <summary>
        /// Wraps to Math.max()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public LSL_Float osMax(double a, double b)
        {
            CheckThreatLevel();

            return Math.Max(a, b);
        }

        public LSL_Key osGetRezzingObject()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetRezzingObject");

            UUID rezID = m_host.ParentGroup.RezzerID;
            if(rezID == UUID.Zero || m_host.ParentGroup.Scene.GetScenePresence(rezID) != null)
                return new LSL_Key(UUID.Zero.ToString());
            return new LSL_Key(rezID.ToString());
        }

        /// <summary>
        /// Sets the response type for an HTTP request/response
        /// </summary>
        /// <returns></returns>
        public void osSetContentType(LSL_Key id, string type)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osSetContentType");

            if (m_UrlModule != null)
                m_UrlModule.HttpContentType(new UUID(id),type);
        }

        /// Shout an error if the object owner did not grant the script the specified permissions.
        /// </summary>
        /// <param name="perms"></param>
        /// <returns>boolean indicating whether an error was shouted.</returns>
        protected bool ShoutErrorOnLackingOwnerPerms(int perms, string errorPrefix)
        {
            bool fail = false;
            if (m_item.PermsGranter != m_host.OwnerID)
            {
                fail = true;
                OSSLShoutError(string.Format("{0}. Permissions not granted to owner.", errorPrefix));
            }
            else if ((m_item.PermsMask & perms) == 0)
            {
                fail = true;
                OSSLShoutError(string.Format("{0}. Permissions not granted.", errorPrefix));
            }

            return fail;
        }

        protected void DropAttachment(bool checkPerms)
        {
            if (checkPerms && ShoutErrorOnLackingOwnerPerms(ScriptBaseClass.PERMISSION_ATTACH, "Cannot drop attachment"))
            {
                return;
            }

            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;
            ScenePresence sp = attachmentsModule == null ? null : m_host.ParentGroup.Scene.GetScenePresence(m_host.ParentGroup.OwnerID);

            if (attachmentsModule != null && sp != null)
            {
                attachmentsModule.DetachSingleAttachmentToGround(sp, m_host.ParentGroup.LocalId);
            }
        }

        protected void DropAttachmentAt(bool checkPerms, LSL_Vector pos, LSL_Rotation rot)
        {
            if (checkPerms && ShoutErrorOnLackingOwnerPerms(ScriptBaseClass.PERMISSION_ATTACH, "Cannot drop attachment"))
            {
                return;
            }

            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;
            ScenePresence sp = attachmentsModule == null ? null : m_host.ParentGroup.Scene.GetScenePresence(m_host.ParentGroup.OwnerID);

            if (attachmentsModule != null && sp != null)
            {
                attachmentsModule.DetachSingleAttachmentToGround(sp, m_host.ParentGroup.LocalId, pos, rot);
            }
        }

        public void osDropAttachment()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osDropAttachment");

            DropAttachment(true);
        }

        public void osForceDropAttachment()
        {
            CheckThreatLevel(ThreatLevel.High, "osForceDropAttachment");

            DropAttachment(false);
        }

        public void osDropAttachmentAt(LSL_Vector pos, LSL_Rotation rot)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osDropAttachmentAt");

            DropAttachmentAt(true, pos, rot);
        }

        public void osForceDropAttachmentAt(LSL_Vector pos, LSL_Rotation rot)
        {
            CheckThreatLevel(ThreatLevel.High, "osForceDropAttachmentAt");

            DropAttachmentAt(false, pos, rot);
        }

        public LSL_Integer osListenRegex(int channelID, string name, string ID, string msg, int regexBitfield)
        {
            CheckThreatLevel(ThreatLevel.Low, "osListenRegex");

            UUID keyID;
            UUID.TryParse(ID, out keyID);

            // if we want the name to be used as a regular expression, ensure it is valid first.
            if ((regexBitfield & ScriptBaseClass.OS_LISTEN_REGEX_NAME) == ScriptBaseClass.OS_LISTEN_REGEX_NAME)
            {
                try
                {
                    Regex.IsMatch("", name);
                }
                catch (Exception)
                {
                    OSSLShoutError("Name regex is invalid.");
                    return -1;
                }
            }

            // if we want the msg to be used as a regular expression, ensure it is valid first.
            if ((regexBitfield & ScriptBaseClass.OS_LISTEN_REGEX_MESSAGE) == ScriptBaseClass.OS_LISTEN_REGEX_MESSAGE)
            {
                try
                {
                    Regex.IsMatch("", msg);
                }
                catch (Exception)
                {
                    OSSLShoutError("Message regex is invalid.");
                    return -1;
                }
            }

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            return (wComm == null) ? -1 : wComm.Listen(
                m_host.LocalId,
                m_item.ItemID,
                m_host.UUID,
                channelID,
                name,
                keyID,
                msg,
                regexBitfield
            );
        }

        public LSL_Integer osRegexIsMatch(string input, string pattern)
        {
            CheckThreatLevel(ThreatLevel.Low, "osRegexIsMatch");

            try
            {
                return Regex.IsMatch(input, pattern) ? 1 : 0;
            }
            catch (Exception)
            {
                OSSLShoutError("Possible invalid regular expression detected.");
                return 0;
            }
        }

        public LSL_String osRequestURL(LSL_List options)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osRequestSecureURL");

            Hashtable opts = new Hashtable();
            for (int i = 0 ; i < options.Length ; i++)
            {
                object opt = options.Data[i];
                if (opt.ToString() == "allowXss")
                    opts["allowXss"] = true;
            }

            if (m_UrlModule != null)
                return m_UrlModule.RequestURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID, opts).ToString();
            return UUID.Zero.ToString();
        }

        public LSL_String osRequestSecureURL(LSL_List options)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osRequestSecureURL");

            Hashtable opts = new Hashtable();
            for (int i = 0 ; i < options.Length ; i++)
            {
                object opt = options.Data[i];
                if (opt.ToString() == "allowXss")
                    opts["allowXss"] = true;
            }

            if (m_UrlModule != null)
                return m_UrlModule.RequestSecureURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID, opts).ToString();
            return UUID.Zero.ToString();
        }

        public void osCollisionSound(string impact_sound, double impact_volume)
        {
            CheckThreatLevel();

            if(impact_sound == "")
            {
                m_host.CollisionSoundVolume = (float)impact_volume;
                m_host.CollisionSound = m_host.invalidCollisionSoundUUID;
                if(impact_volume == 0.0)
                    m_host.CollisionSoundType = -1; // disable all sounds
                else if(impact_volume == 1.0f)
                    m_host.CollisionSoundType = 0; // full return to default sounds
                else
                    m_host.CollisionSoundType = 2; // default sounds with volume
                m_host.aggregateScriptEvents();
                return;
            }
            // TODO: Parameter check logic required.
            UUID soundId = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, impact_sound, AssetType.Sound);
            if(soundId != UUID.Zero)
            {
                m_host.CollisionSound = soundId;
                m_host.CollisionSoundVolume = (float)impact_volume;
                m_host.CollisionSoundType = 1;
            }
            else
                 m_host.CollisionSoundType = -1;

            m_host.aggregateScriptEvents();
        }

        // still not very usefull, detector is lost on rez, restarts, etc
        public void osVolumeDetect(int detect)
        {
            CheckThreatLevel();

            if (m_host.ParentGroup == null || m_host.ParentGroup.IsDeleted || m_host.ParentGroup.IsAttachment)
                return;

            m_host.ScriptSetVolumeDetect(detect != 0);
        }

        /// <summary>
        /// Get inertial data
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns>
        /// a LSL list with contents:
        ///     LSL_Float mass,  the total mass of a linkset
        ///     LSL_Vector CenterOfMass, center mass relative to root prim
        ///     LSL_Vector Inertia, elements of diagonal of inertia Ixx,Iyy,Izz divided by total mass
        ///     LSL_Vector aux, elements of upper triagle of inertia Ixy (= Iyx), Ixz (= Izx), Iyz(= Izy) divided by total mass
        /// </returns>
        public LSL_List osGetInertiaData()
        {
            CheckThreatLevel();

            LSL_List result = new LSL_List();
            float TotalMass;
            Vector3 CenterOfMass;
            Vector3 Inertia;
            Vector4 aux;

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return result;

            sog.GetInertiaData(out TotalMass, out CenterOfMass, out Inertia, out aux );
            if(TotalMass > 0)
            {
                float t = 1.0f/TotalMass;
                Inertia.X *= t;
                Inertia.Y *= t;
                Inertia.Z *= t;

                aux.X *= t;
                aux.Y *= t;
                aux.Z *= t;
            }

            result.Add(new LSL_Float(TotalMass));
            result.Add(new LSL_Vector(CenterOfMass.X, CenterOfMass.Y, CenterOfMass.Z));
            result.Add(new LSL_Vector(Inertia.X, Inertia.Y, Inertia.Z));
            result.Add(new LSL_Vector(aux.X, aux.Y, aux.Z));
            return result;
        }

        /// <summary>
        /// set inertial data
        /// replaces the automatic calculation of mass, center of mass and inertia
        ///
        /// </summary>
        /// <param name="Mass">total mass of linkset</param>
        /// <param name="centerOfMass">location of center of mass relative to root prim in local coords</param>
        /// <param name="principalInertiaScaled">moment of inertia relative to principal axis and center of mass,Ixx, Iyy, Izz divided by mass</param>
        /// <param name="lslrot">rotation of the inertia, relative to local axis</param>
        /// <remarks>
        /// the inertia argument is is inertia divided by mass, so corresponds only to the geometric distribution of mass and both can be changed independently.
        /// </remarks>

        public void osSetInertia(LSL_Float mass, LSL_Vector centerOfMass, LSL_Vector principalInertiaScaled,  LSL_Rotation lslrot)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return;

            if(mass < 0 || principalInertiaScaled.x < 0 || principalInertiaScaled.y < 0 || principalInertiaScaled.z < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new Vector3((float)centerOfMass.x,(float)centerOfMass.y,(float)centerOfMass.z);
            Vector3 Inertia;
            float m = (float)mass;

            Inertia.X = m * (float)principalInertiaScaled.x;
            Inertia.Y = m * (float)principalInertiaScaled.y;
            Inertia.Z = m * (float)principalInertiaScaled.z;

            Vector4 rot = new Vector4((float)lslrot.x, (float)lslrot.y, (float)lslrot.y, (float)lslrot.s);
            rot.Normalize();

            sog.SetInertiaData(m, CenterOfMass, Inertia, rot );
        }

        /// <summary>
        /// set inertial data as a sphere
        /// replaces the automatic calculation of mass, center of mass and inertia
        ///
        /// </summary>
        /// <param name="Mass">total mass of linkset</param>
        /// <param name="boxsize">size of the Box</param>
        /// <param name="centerOfMass">location of center of mass relative to root prim in local coords</param>
        /// <param name="lslrot">rotation of the box, and so inertia, relative to local axis</param>
        /// <remarks>
        /// </remarks>
        public void osSetInertiaAsBox(LSL_Float mass, LSL_Vector boxSize, LSL_Vector centerOfMass, LSL_Rotation lslrot)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return;

            if(mass < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new Vector3((float)centerOfMass.x,(float)centerOfMass.y,(float)centerOfMass.z);
            Vector3 Inertia;
            float lx = (float)boxSize.x;
            float ly = (float)boxSize.y;
            float lz = (float)boxSize.z;
            float m = (float)mass;
            float t = m / 12.0f;

            Inertia.X = t * (ly*ly + lz*lz);
            Inertia.Y = t * (lx*lx + lz*lz);
            Inertia.Z = t * (lx*lx + ly*ly);

            Vector4 rot = new Vector4((float)lslrot.x, (float)lslrot.y, (float)lslrot.z, (float)lslrot.s);
            rot.Normalize();

            sog.SetInertiaData(m, CenterOfMass, Inertia, rot );
        }

        /// <summary>
        /// set inertial data as a sphere
        /// replaces the automatic calculation of mass, center of mass and inertia
        ///
        /// </summary>
        /// <param name="Mass">total mass of linkset</param>
        /// <param name="radius">radius of the sphere</param>
        /// <param name="centerOfMass">location of center of mass relative to root prim in local coords</param>
        /// <remarks>
        /// </remarks>
        public void osSetInertiaAsSphere(LSL_Float mass,  LSL_Float radius, LSL_Vector centerOfMass)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return;

            if(mass < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new Vector3((float)centerOfMass.x,(float)centerOfMass.y,(float)centerOfMass.z);
            Vector3 Inertia;
            float r = (float)radius;
            float m = (float)mass;
            float t = 0.4f * m * r * r;

            Inertia.X = t;
            Inertia.Y = t;
            Inertia.Z = t;

            sog.SetInertiaData(m, CenterOfMass, Inertia, new Vector4(0f, 0f, 0f,1.0f));
        }

        /// <summary>
        /// set inertial data as a cylinder
        /// replaces the automatic calculation of mass, center of mass and inertia
        ///
        /// </summary>
        /// <param name="Mass">total mass of linkset</param>
        /// <param name="radius">radius of the cylinder</param>
        /// <param name="lenght">lenght of the cylinder</param>
        /// <param name="centerOfMass">location of center of mass relative to root prim in local coords</param>
        /// <param name="lslrot">rotation of the cylinder, and so inertia, relative to local axis</param>
        /// <remarks>
        /// cylinder axis aligned with Z axis. For other orientations provide the rotation.
        /// </remarks>
        public void osSetInertiaAsCylinder(LSL_Float mass,  LSL_Float radius, LSL_Float lenght, LSL_Vector centerOfMass, LSL_Rotation lslrot)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return;

            if(mass < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new Vector3((float)centerOfMass.x,(float)centerOfMass.y,(float)centerOfMass.z);
            Vector3 Inertia;
            float m = (float)mass;
            float r = (float)radius;
            r *= r;
            Inertia.Z = 0.5f * m * r;
            float t = (float)lenght;
            t *= t;
            t += 3.0f * r;
            t *= 8.333333e-2f * m;

            Inertia.X = t;
            Inertia.Y = t;

            Vector4 rot = new Vector4((float)lslrot.x, (float)lslrot.y, (float)lslrot.z, (float)lslrot.s);
            rot.Normalize();

            sog.SetInertiaData(m, CenterOfMass, Inertia, rot);
        }

        /// <summary>
        /// removes inertial data manual override
        /// default automatic calculation is used again
        ///
        /// </summary>
        public void osClearInertia()
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return;

            sog.SetInertiaData(-1, Vector3.Zero, Vector3.Zero, Vector4.Zero );
        }

        private bool checkAllowObjectTPbyLandOwner(Vector3 pos)
        {
            ILandObject land = World.LandChannel.GetLandObject(pos);
            if(land == null)
                return true;

            LandData landdata = land.LandData;
            if(landdata == null)
                return true;

            UUID hostOwner = m_host.OwnerID;
            if(landdata.OwnerID == hostOwner)
                return true;

            EstateSettings es = World.RegionInfo.EstateSettings;
            if(es != null && es.IsEstateManagerOrOwner(hostOwner))
                return true;

            if(!landdata.IsGroupOwned)
                return false;

            UUID landGroup = landdata.GroupID;
            if(landGroup == UUID.Zero)
                return false;

            if(landGroup == m_host.GroupID)
                return true;

            return false;
        }

       /// <summary>
        /// teleports a object (full linkset)
        /// </summary>
        /// <param name="objectUUID">the id of the linkset to teleport</param>
        /// <param name="targetPos">target position</param>
        /// <param name="rotation"> a rotation to apply</param>
        /// <param name="flags">several flags/param>
        /// <remarks>
        /// only does teleport local to region
        /// if object has scripts, owner must have rights to run scripts on target location
        /// object owner must have rights to enter ojects on target location
        /// target location parcel must have enought free prims capacity for the linkset prims
        /// all avatars siting on the object must have access to target location
        /// has a cool down time. retries before expire reset it
        /// fail conditions are silent ignored
        /// </remarks>
        public LSL_Integer osTeleportObject(LSL_Key objectUUID, LSL_Vector targetPos, LSL_Rotation rotation, LSL_Integer flags)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osTeleportObject");

            UUID objUUID;
            if (!UUID.TryParse(objectUUID, out objUUID))
            {
                OSSLShoutError("osTeleportObject() invalid object Key");
                return -1;
            }

            SceneObjectGroup sog = World.GetSceneObjectGroup(objUUID);
            if(sog== null || sog.IsDeleted || sog.inTransit)
                return -1;

            if(sog.OwnerID != m_host.OwnerID)
            {
                Vector3 pos = sog.AbsolutePosition;
                if(!checkAllowObjectTPbyLandOwner(pos))
                    return -1;
            }

            UUID myid = m_host.ParentGroup.UUID;

            return sog.TeleportObject(myid, targetPos, rotation, flags);
            // a delay here may break vehicles
        }

        public LSL_Integer osGetLinkNumber(LSL_String name)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog== null || sog.IsDeleted)
                return -1;
            return sog.GetLinkNumber(name);
        }

        // rounds to the nearest number with provided number of decimal places
        public LSL_Float osRound(LSL_Float value, LSL_Integer ndigits)
        {
            if(ndigits <= 0)
                return Math.Round(value, MidpointRounding.AwayFromZero);
            if(ndigits > 15)
                ndigits = 15;
            return Math.Round(value, ndigits, MidpointRounding.AwayFromZero);
        }

        public LSL_Float osVecMagSquare(LSL_Vector a)
        {
            return LSL_Vector.MagSquare(a);
        }

        public LSL_Float osVecDistSquare(LSL_Vector a, LSL_Vector b)
        {
            return LSL_Vector.MagSquare(a - b);
        }

        // returns the angle between 2 vectors 0 to pi
        public LSL_Float osAngleBetween(LSL_Vector a, LSL_Vector b)
        {
            double dot = LSL_Vector.Dot(a,b);
            double mcross = LSL_Vector.Mag(LSL_Vector.Cross(a,b));
            return Math.Atan2(mcross, dot);
        }

       public void osAdjustSoundVolume(LSL_Integer linknum, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;
            sop.AdjustSoundGain(volume);
        }

        public void osSetSoundRadius(LSL_Integer linknum, LSL_Float radius)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;
            sop.SoundRadius = radius;
        }

        public void osPlaySound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            // send the sound, once, to all clients in range
            m_SoundModule.SendSound(sop.UUID, soundID, volume, false, 0, false, false);
        }

        public void osLoopSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            m_SoundModule.LoopSound(sop.UUID, soundID, volume, false,false);
        }

        public void osLoopSoundMaster(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            m_SoundModule.LoopSound(sop.UUID, soundID, volume, true, false);
        }

        public void osLoopSoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            m_SoundModule.LoopSound(sop.UUID, soundID, volume, false, true);
        }

        public void osPlaySoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            // send the sound, once, to all clients in range
            m_SoundModule.SendSound(sop.UUID, soundID, volume, false, 0, true, false);
        }

        public void osTriggerSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            // send the sound, once, to all clients in rangeTrigger or play an attached sound in this part's inventory.
            m_SoundModule.SendSound(sop.UUID, soundID, volume, true, 0, false, false);
        }

       public void osTriggerSoundLimited(LSL_Integer linknum, LSL_String sound, LSL_Float volume,
                 LSL_Vector top_north_east, LSL_Vector bottom_south_west)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            m_SoundModule.TriggerSoundLimited(sop.UUID, soundID, volume,
                        bottom_south_west, top_north_east);
        }

        public void osStopSound(LSL_Integer linknum)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            m_SoundModule.StopSound(sop.UUID);
        }

        public void osPreloadSound(LSL_Integer linknum, LSL_String sound)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule == null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
                return;

            m_SoundModule.PreloadSound(sop.UUID, soundID);
            ScriptSleep(1000);
        }

        // get only one part
        private SceneObjectPart GetSingleLinkPart(int linkType)
        {
            if (m_host.ParentGroup == null || m_host.ParentGroup.IsDeleted)
                return null;

            switch (linkType)
            {
                case ScriptBaseClass.LINK_SET:
                case ScriptBaseClass.LINK_ALL_OTHERS:
                case ScriptBaseClass.LINK_ALL_CHILDREN:
                    return null;

                case 0:
                case ScriptBaseClass.LINK_ROOT:
                    return m_host.ParentGroup.RootPart;

                case ScriptBaseClass.LINK_THIS:
                    return m_host;

                default:
                    if(linkType < 0)
                        return null;

                return m_host.ParentGroup.GetLinkNumPart(linkType);
            }
        }

        // funtions to retrieve user country
        // adaptation cm* counter parts from Avination Careminster extensions API
        // included in Avination code contribution
        // for now users country can only be set directly on DB

        public LSL_String osDetectedCountry(LSL_Integer number)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel(ThreatLevel.Moderate, "osDetectedCountry");

            if (World.UserAccountService == null)
                return String.Empty;
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return String.Empty;
            UUID key = detectedParams.Key;
            if (key == UUID.Zero)
                return String.Empty;
            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
            return account.UserCountry;
        }

        public LSL_String osGetAgentCountry(LSL_Key id)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel(ThreatLevel.Moderate, "osGetAgentCountry");

            if (World.UserAccountService == null)
                return String.Empty;

            UUID key;
            if (!UUID.TryParse(id, out key))
                return String.Empty;
            if (key == UUID.Zero)
                return String.Empty;

            //if owner is not god, target must be in region, or nearby regions
            if (!World.Permissions.IsGod(m_host.OwnerID))
            {
                ScenePresence sp = World.GetScenePresence(key);
                if(sp == null)
                    return String.Empty;
            }

            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
            return account.UserCountry;
        }

        public LSL_String osStringSubString(LSL_String src, LSL_Integer offset)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return "";
            if (offset >= src.Length)
                return "";
            if (offset <= 0)
                return src;
            return ((string)src).Substring(offset);
        }

        public LSL_String osStringSubString(LSL_String src, LSL_Integer offset, LSL_Integer length)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return "";
            if (length <= 0 || offset >= src.Length)
                return "";
            if (offset <= 0)
            {
                if(length == src.Length)
                    return src;
                offset = 0;
            }
            if (length > src.Length - offset)
                length = src.Length - offset;
            return ((string)src).Substring(offset, length);
        }

        public LSL_Integer osStringStartsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return 0;
            if (string.IsNullOrEmpty(value))
                return 0;

            bool ign = (ignorecase != 0);
            return ((string)src).StartsWith(value, ignorecase, Culture.GetDefaultCurrentCulture()) ? 1 : 0;
        }

        public LSL_Integer osStringEndsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return 0;
            if (string.IsNullOrEmpty(value))
                return 0;

            bool ign = (ignorecase != 0);
            return ((string)src).EndsWith(value, ign, Culture.GetDefaultCurrentCulture()) ? 1 : 0;
        }

        public LSL_Integer osStringIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return -1;
            if (string.IsNullOrEmpty(value))
                return -1;

            if (ignorecase == 0)
                return ((string)src).IndexOf(value, StringComparison.CurrentCulture);
            return ((string)src).IndexOf(value, StringComparison.CurrentCultureIgnoreCase);
        }

        public LSL_Integer osStringIndexOf(LSL_String src, LSL_String value,
            LSL_Integer offset, LSL_Integer count, LSL_Integer ignorecase)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return -1;
            if (string.IsNullOrEmpty(value))
                return -1;
            if (offset >= src.Length)
                return -1;
            else if (offset < 0)
                offset = 0;

            if (count <= 0)
                count = src.Length - offset;
            else if (count > src.Length - offset)
                count = src.Length - offset;

            if (ignorecase == 0)
                return ((string)src).IndexOf(value, offset, count, StringComparison.CurrentCulture);
            return ((string)src).IndexOf(value, offset, count, StringComparison.CurrentCultureIgnoreCase);
        }

        public LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return -1;
            if (string.IsNullOrEmpty(value))
                return -1;

            if (ignorecase == 0)
                return ((string)src).LastIndexOf(value, StringComparison.CurrentCulture);
            return ((string)src).LastIndexOf(value, StringComparison.CurrentCultureIgnoreCase);
        }

        public LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value,
            LSL_Integer offset, LSL_Integer count, LSL_Integer ignorecase)
        {
            m_host.AddScriptLPS(1);
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return -1;
            if (string.IsNullOrEmpty(value))
                return -1;
            if (offset >= src.Length)
                return -1;
            if (offset < 0)
                offset = 0;

            if (count <= 0)
                count = src.Length - offset;
            else if (count > src.Length - offset)
                count = src.Length - offset;

            if (ignorecase == 0)
                return ((string)src).LastIndexOf(value, offset, count, StringComparison.CurrentCulture);
            return ((string)src).LastIndexOf(value, offset, count, StringComparison.CurrentCultureIgnoreCase);
        }

        public LSL_String osStringRemove(LSL_String src, LSL_Integer offset, LSL_Integer count)
        {
            if (string.IsNullOrEmpty(src))
                return "";
            if (offset >= src.Length)
                return "";
            if (offset < 0)
                offset = 0;

            if (count <= 0)
                count = src.Length - offset;
            else if (count > src.Length - offset)
                count = src.Length - offset;

            if (count >= src.Length)
                return "";


            return ((string)src).Remove(offset, count);
        }

        public LSL_String osStringReplace(LSL_String src, LSL_String oldvalue, LSL_String newvalue)
        {
            if (string.IsNullOrEmpty(src))
                return "";
            if (string.IsNullOrEmpty(oldvalue))
                return "";
            if (string.IsNullOrEmpty(newvalue))
                newvalue = null;

            return ((string)src).Replace(oldvalue, newvalue);
        }

        public LSL_Integer osApproxEquals(LSL_Float a, LSL_Float b)
        {
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;
            return 1;
        }

        public LSL_Integer osApproxEquals(LSL_Float a, LSL_Float b, LSL_Float margin)
        {
            double e = Math.Abs(margin);
            if (a > b + e || a < b - e)
                return 0;
            return 1;
        }

        public LSL_Integer osApproxEquals(LSL_Vector va, LSL_Vector vb)
        {
            double a = va.x;
            double b = vb.x;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;
            a = va.y;
            b = vb.y;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;
            a = va.z;
            b = vb.z;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;

            return 1;
        }

        public LSL_Integer osApproxEquals(LSL_Vector va, LSL_Vector vb, LSL_Float margin)
        {
            double e = Math.Abs(margin);
            double a = va.x;
            double b = vb.x;
            if (a > b + e || a < b - e)
                return 0;
            a = va.y;
            b = vb.y;
            if (a > b + e || a < b - e)
                return 0;
            a = va.z;
            b = vb.z;
            if (a > b + e || a < b - e)
                return 0;

            return 1;
        }

        public LSL_Integer osApproxEquals(LSL_Rotation ra, LSL_Rotation rb)
        {
            double a = ra.x;
            double b = rb.x;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;
            a = ra.y;
            b = rb.y;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;
            a = ra.z;
            b = rb.z;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;
            a = ra.s;
            b = rb.s;
            if (a > b + 1.0e-6 || a < b - 1.0e-6)
                return 0;

            return 1;
        }

        public LSL_Integer osApproxEquals(LSL_Rotation ra, LSL_Rotation rb, LSL_Float margin)
        {
            double e = Math.Abs(margin);
            double a = ra.x;
            double b = rb.x;
            if (a > b + e || a < b - e)
                return 0;
            a = ra.y;
            b = rb.y;
            if (a > b + e || a < b - e)
                return 0;
            a = ra.z;
            b = rb.z;
            if (a > b + e || a < b - e)
                return 0;
            a = ra.s;
            b = rb.s;
            if (a > b + e || a < b - e)
                return 0;

            return 1;
        }

        public LSL_Key osGetInventoryLastOwner(LSL_String itemNameorid)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = null;
            UUID itemID;
            if (UUID.TryParse(itemNameorid, out itemID))
                item = m_host.Inventory.GetInventoryItem(itemID);
            else
                item = m_host.Inventory.GetInventoryItem(itemNameorid);

            if (item == null)
                return UUID.Zero.ToString();

            UUID id = item.LastOwnerID;
            if(id == UUID.Zero)
                id= item.OwnerID;
            return id.ToString();
        }

        public LSL_String osGetInventoryName(LSL_Key itemId)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = null;
            UUID itemID;
            if (UUID.TryParse(itemId, out itemID))
                item = m_host.Inventory.GetInventoryItem(itemID);

            if (item == null)
                return String.Empty;

            return item.Name;
        }

        public LSL_String osGetInventoryDesc(LSL_String itemNameorid)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = null;
            UUID itemID;
            if (UUID.TryParse(itemNameorid, out itemID))
                item = m_host.Inventory.GetInventoryItem(itemID);
            else
                item = m_host.Inventory.GetInventoryItem(itemNameorid);

            if (item == null)
                return String.Empty;

            return item.Description;
        }

        public LSL_Key osGetLastChangedEventKey()
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, 0);
            if (detectedParams == null)
                return String.Empty;
            return detectedParams.Key.ToString();
        }
    }
}