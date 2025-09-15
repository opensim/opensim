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

using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using PermissionMask = OpenSim.Framework.PermissionMask;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;

#pragma warning disable IDE1006

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

    // flags functions threat control
    public enum AllowedControlFlags : int
    {
        NONE                = 0,
        PARCEL_OWNER        = 1,
        PARCEL_GROUP_MEMBER = 1 << 1,
        ESTATE_MANAGER      = 1 << 2,
        ESTATE_OWNER        = 1 << 3,
        ACTIVE_GOD          = 1 << 4,
        GOD                 = 1 << 5,
        GRID_GOD            = 1 << 6,

        // internal
        THREATLEVEL         = 1 << 28,
        OWNERUUID           = 1 << 29,
        CREATORUUID         = 1 << 30,
        //int thingie       = 1 << 31,
        ALL = 0x0FFFFFFF
    }

    class FunctionPerms
    {
        public List<UUID> AllowedCreators;
        public List<UUID> AllowedOwners;
        public AllowedControlFlags AllowedControl = AllowedControlFlags.NONE;
    }

    [Serializable]
    public class OSSL_Api : IOSSL_Api, IScriptApi
    {
        public const string GridInfoServiceConfigSectionName = "GridInfoService";

        // shared things
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly object m_OSSLLock = new();
        private static bool m_doneSharedInit = false;
        internal static bool m_OSFunctionsEnabled = false;
        internal static TimeZoneInfo PSTTimeZone = null;
        internal static bool m_PermissionErrortoOwner = false;
        internal static ThreatLevel m_MaxThreatLevel = ThreatLevel.VeryLow;
        internal static float m_ScriptDelayFactor = 1.0f;
        internal static float m_ScriptDistanceFactor = 1.0f;
        internal static IConfig m_osslconfig;

        internal static ConcurrentDictionary<string, FunctionPerms> m_FunctionPerms = new();

        internal IScriptEngine m_ScriptEngine;
        internal LSL_Api m_LSL_Api = null; // get a reference to the LSL API so we can call methods housed there
        internal SceneObjectPart m_host;
        internal TaskInventoryItem m_item;
        protected IUrlModule m_UrlModule = null;
        protected ISoundModule m_SoundModule = null;
        protected IEnvironmentModule m_envModule = null;
        protected IGroupsModule m_groupsModule = null;
        protected IMessageTransferModule m_TransferModule = null;

        public Scene World;

        public void Initialize(IScriptEngine scriptEngine, SceneObjectPart host, TaskInventoryItem item)
        {
            //private init
            m_ScriptEngine = scriptEngine;
            World = scriptEngine.World;

            m_host = host;
            m_item = item;

            m_UrlModule = m_ScriptEngine.World.RequestModuleInterface<IUrlModule>();
            m_SoundModule = m_ScriptEngine.World.RequestModuleInterface<ISoundModule>();
            m_envModule = m_ScriptEngine.World.RequestModuleInterface<IEnvironmentModule>();
            m_groupsModule = m_ScriptEngine.World.RequestModuleInterface<IGroupsModule>();
            m_TransferModule = m_ScriptEngine.World.RequestModuleInterface<IMessageTransferModule>();

            //private init
            lock (m_OSSLLock)
            {
                if(m_doneSharedInit)
                    return;

                m_osslconfig = m_ScriptEngine.ConfigSource.Configs["OSSL"];
                m_osslconfig ??= m_ScriptEngine.Config;

                if (m_osslconfig.GetBoolean("AllowOSFunctions", true))
                {
                    m_OSFunctionsEnabled = true;
                    // m_log.Warn("[OSSL] OSSL FUNCTIONS ENABLED");
                }

                m_PermissionErrortoOwner = m_osslconfig.GetBoolean("PermissionErrorToOwner", m_PermissionErrortoOwner);

                m_ScriptDelayFactor =  m_ScriptEngine.Config.GetFloat("ScriptDelayFactor", 1.0f);
                m_ScriptDistanceFactor = m_ScriptEngine.Config.GetFloat("ScriptDistanceLimitFactor", 1.0f);

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

                try
                {
                    PSTTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                }
                catch
                {
                    PSTTimeZone = null;
                }
                if(PSTTimeZone == null)
                {
                    try
                    {
                        PSTTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
                    }
                    catch
                    {
                        PSTTimeZone = null;
                    }
                }

                m_doneSharedInit = true;
            }
        }

        internal static void OSSLError(string msg)
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
            if (m_LSL_Api == null)
            {
                m_LSL_Api = (LSL_Api)m_ScriptEngine.GetApi(m_item.ItemID, "LSL");
                if (m_LSL_Api == null)
                    throw new Exception("OSSL failed to load LSL API");
            }
        }

        //
        //Dumps an error message on the debug console.
        //

        internal void OSSLShoutError(string message)
        {
            if (message.Length > 1023)
                message = message[..1023];

            World.SimChat(Utils.StringToBytes(message),
                ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, message);

            if (m_item is not null)
                m_ScriptEngine.SleepScript(m_item.ItemID, 1000);
            //else
            //    Thread.Sleep(1000);
            }

        // Returns if OSSL is enabled. Throws a script exception if OSSL is not allowed..
        // for safe funtions always active
        public static void CheckThreatLevel()
        {
            if (!m_OSFunctionsEnabled)
                OSSLError("permission denied. All unsafe OSSL funtions disabled"); // throws
        }

        // Returns if the function is allowed. Throws a script exception if not allowed.
        public void CheckThreatLevel(ThreatLevel level, string function)
        {
            if (!m_OSFunctionsEnabled)
            {
                if (m_PermissionErrortoOwner)
                    throw new ScriptException("(OWNER)OSSL Permission Error: All unsafe OSSL funtions disabled");
                else
                    throw new ScriptException("OSSL Permission Error: All unsafe OSSL funtions disabled");
            }

            string reasonWhyNot = CheckThreatLevelTest(level, function);
            if (!string.IsNullOrEmpty(reasonWhyNot))
            {
                if (m_PermissionErrortoOwner)
                    throw new ScriptException("(OWNER)OSSL Permission Error: " + reasonWhyNot);
                else
                    throw new ScriptException("OSSL Permission Error: " + reasonWhyNot);
            }
        }

        // Check to see if function is allowed. Returns an empty string if function permitted
        //     or a string explaining why this function can't be used.
        private string CheckThreatLevelTest(ThreatLevel level, string function)
        {
            if (!m_FunctionPerms.TryGetValue(function, out FunctionPerms perms))
            {
                perms = new FunctionPerms();

                string ownerPerm = m_osslconfig.GetString("Allow_" + function, "");
                string creatorPerm = m_osslconfig.GetString("Creators_" + function, "");
                if (string.IsNullOrWhiteSpace(ownerPerm) && string.IsNullOrWhiteSpace(creatorPerm))
                {
                    // Default Threat level check
                    perms.AllowedControl = AllowedControlFlags.THREATLEVEL;
                }
                else
                {
                    if (bool.TryParse(ownerPerm, out bool allowed))
                    {
                        // Boolean given
                        if (allowed)
                        {
                            // Allow globally
                            perms.AllowedControl = AllowedControlFlags.ALL;
                        }
                        // false is fallback 
                    }
                    else
                    {
                        string[] ids;
                        bool error = false;
                        if (!string.IsNullOrWhiteSpace(ownerPerm))
                        {
                            ids = ownerPerm.Split(Util.SplitCommaArray);
                            foreach (string id in ids)
                            {
                                string current = id.Trim();
                                current = current.ToUpper();
                                switch(current)
                                {
                                    case "":
                                        break;
                                    case "PARCEL_OWNER":
                                        perms.AllowedControl |= AllowedControlFlags.PARCEL_OWNER;
                                        break;
                                    case "PARCEL_GROUP_MEMBER":
                                        perms.AllowedControl |= AllowedControlFlags.PARCEL_GROUP_MEMBER;
                                        break;
                                    case "ESTATE_MANAGER":
                                        perms.AllowedControl |= AllowedControlFlags.ESTATE_MANAGER;
                                        break;
                                    case "ESTATE_OWNER":
                                        perms.AllowedControl |= AllowedControlFlags.ESTATE_OWNER;
                                        break;
                                    case "ACTIVE_GOD":
                                        perms.AllowedControl |= AllowedControlFlags.ACTIVE_GOD;
                                        break;
                                    case "GOD":
                                        perms.AllowedControl |= AllowedControlFlags.GOD;
                                        break;
                                    case "GRID_GOD":
                                        perms.AllowedControl |= AllowedControlFlags.GRID_GOD;
                                        break;
                                    default:
                                    {
                                        if (UUID.TryParse(current, out UUID uuid))
                                        {
                                            if (uuid.IsNotZero())
                                            {
                                                perms.AllowedOwners ??= new List<UUID>();
                                                perms.AllowedControl |= AllowedControlFlags.OWNERUUID;
                                                perms.AllowedOwners.Add(uuid);
                                            }
                                        }
                                        else
                                        {
                                            error = true;
                                        }
                                        break;
                                    }
                                }
                            }
                            if (error)
                                m_log.WarnFormat("[OSSLENABLE]: error parsing line Allow_{0} = {1}", function, ownerPerm);
                        }
                        error = false;
                        if (!string.IsNullOrWhiteSpace(creatorPerm))
                        {
                            ids = creatorPerm.Split(Util.SplitCommaArray);
                            foreach (string id in ids)
                            {
                                string current = id.Trim();
                                if (UUID.TryParse(current, out UUID uuid))
                                {
                                    if (!uuid.IsZero())
                                    {
                                        perms.AllowedCreators ??= new List<UUID>();
                                        perms.AllowedControl |= AllowedControlFlags.CREATORUUID;
                                        perms.AllowedCreators.Add(uuid);
                                    }
                                }
                                else
                                {
                                    error = true;
                                }
                            }
                            if (error)
                                m_log.WarnFormat("[OSSLENABLE]: error parsing line Creators_{0} = {1}", function, creatorPerm);
                        }
                        // both empty fallback as disabled
                    }
                }
                m_FunctionPerms.TryAdd(function,perms);
            }

            AllowedControlFlags functionControl = perms.AllowedControl;

            if (functionControl == AllowedControlFlags.THREATLEVEL)
            {
                // Allow / disallow by threat level
                if (level <= m_MaxThreatLevel)
                    return string.Empty;

                return $"{function} permission denied.  Allowed threat level is {m_MaxThreatLevel} but function threat level is {level}";
            }

            if (functionControl == 0)
                return $"{function} disabled in region configuration";

            if (functionControl == AllowedControlFlags.ALL)
                return string.Empty;

            if (((functionControl & AllowedControlFlags.OWNERUUID) != 0) && perms.AllowedOwners.Contains(m_host.OwnerID))
            {
                // prim owner is in the list of allowed owners
                return string.Empty;
            }

            if ((functionControl & AllowedControlFlags.PARCEL_OWNER) != 0)
            {
                ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
                if (land.LandData.OwnerID.Equals(m_item.OwnerID))
                    return string.Empty;
            }

            //OSSL only may be used if object is in the same group as the parcel
            if ((functionControl & AllowedControlFlags.PARCEL_GROUP_MEMBER) != 0)
            {
                ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
                if (land.LandData.GroupID.Equals(m_item.GroupID) && !land.LandData.GroupID.IsZero())
                    return string.Empty;
            }

            //Only regionowners may use the function
            if ((functionControl & AllowedControlFlags.ESTATE_OWNER) != 0)
            {
                if (World.RegionInfo.EstateSettings.EstateOwner.Equals(m_item.OwnerID))
                    return string.Empty;
            }

            //Only Estate Managers may use the function
            if ((functionControl & AllowedControlFlags.ESTATE_MANAGER) != 0)
            {
                //Only Estate Managers may use the function
                if (World.RegionInfo.EstateSettings.IsEstateManagerOrOwner(m_item.OwnerID))
                    return string.Empty;
            }

            //Only grid gods may use the function
            if ((functionControl & AllowedControlFlags.GRID_GOD) != 0)
            {
                if (World.Permissions.IsGridGod(m_item.OwnerID))
                    return string.Empty;
            }

            //Any god may use the function
            if ((functionControl & AllowedControlFlags.GOD) != 0)
            {
                if (World.Permissions.IsAdministrator(m_item.OwnerID))
                    return string.Empty;
            }

            //Only active gods may use the function
            if ((functionControl & AllowedControlFlags.ACTIVE_GOD) != 0)
            {
                ScenePresence sp = World.GetScenePresence(m_item.OwnerID);
                if (sp is not null && !sp.IsDeleted && sp.IsGod)
                    return string.Empty;
            }

            // else if no creators its denied
            if((functionControl & AllowedControlFlags.CREATORUUID) == 0)
                return $"{function} permission denied";

            if (!perms.AllowedCreators.Contains(m_item.CreatorID))
                return($"{function} permission denied. Script creator is not in the list of users allowed to execute this function and prim owner also has no permission");

            if (m_item.CreatorID.NotEqual(m_item.OwnerID))
            {
                if ((m_item.CurrentPermissions & (uint)PermissionMask.Modify) != 0)
                    return $"{function} permission denied. Script creator is not prim owner";
            }

            return string.Empty;
        }

        internal void OSSLDeprecated(string function, string replacement)
        {
            OSSLShoutError($"Use of function {function} is deprecated. Use {replacement} instead.");
        }

        protected void ScriptSleep(int delay)
        {
            delay = (int)(delay * m_ScriptDelayFactor);
            if (delay < 10)
                return;
            if(m_item is not null)
                m_ScriptEngine.SleepScript(m_item.ItemID, delay);
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
                World.Heightmap[x, y] = (float)val;
                return 1;
            }

            return 0;
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

        double m_lastosTerrainFlush = 0;
        public void osTerrainFlush()
        {
            double now = Util.GetTimeStamp();
            if(now - m_lastosTerrainFlush < 60)
                return;
            m_lastosTerrainFlush = now;

            CheckThreatLevel(ThreatLevel.VeryLow, "osTerrainFlush");

            ITerrainModule terrainModule = World.RequestModuleInterface<ITerrainModule>();
            terrainModule?.TaintTerrain();
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

                RegionRestart(seconds, string.Empty);
                return 1;
            }

            return 0;
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

            return 0;
        }

        private void RegionRestart(double seconds, string msg)
        {
            IRestartModule restartModule = World.RequestModuleInterface<IRestartModule>();

            List<int> times = new();
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

            if (msg.Length == 0)
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
            if (dm is null)
                return;

            if (!UUID.TryParse(agentID, out UUID avatarID))
                return;

            if (!World.TryGetScenePresence(avatarID, out ScenePresence sp))
                return;

            if (sp is null || sp.IsChildAgent || sp.IsDeleted || sp.IsInTransit || sp.IsNPC)
                return;

            dm.SendAlertToUser(sp.ControllingClient, msg + "\n", false);
        }

        public void osSetRot(UUID target, Quaternion rotation)
        {
            // if enabled It can be used to destroy
            // arbitrary builds the user would normally have no rights to
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetRot");

            if (World.Entities.TryGetValue(target, out EntityBase entity))
            {
                if (entity is SceneObjectGroup sog)
                    sog.UpdateGroupRotationR(rotation);
                else if (entity is ScenePresence sop)
                    sop.Rotation = rotation;
            }
            else
            {
                OSSLError("osSetRot: Invalid target");
            }
        }

        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetDynamicTextureURL");

            if (dynamicID.Length == 0)
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

            return ScriptBaseClass.NULL_KEY;
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                             int timer, int alpha)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetDynamicTextureURLBlend");

            if (dynamicID.Length == 0)
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

            return ScriptBaseClass.NULL_KEY;
        }

        public string osSetDynamicTextureURLBlendFace(string dynamicID, string contentType, string url, string extraParams,
                                             bool blend, int disp, int timer, int alpha, int face)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetDynamicTextureURLBlendFace");

            if (dynamicID.Length == 0)
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

            return ScriptBaseClass.NULL_KEY;
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

            if (dynamicID.Length == 0)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager is not null)
                {
                    if (extraParams.Length == 0)
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

            return ScriptBaseClass.NULL_KEY;
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                          int timer, int alpha)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureDataBlend");

            if (dynamicID.Length == 0)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager is not null)
                {
                    if (extraParams.Length == 0)
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

            return ScriptBaseClass.NULL_KEY;
        }

        public string osSetDynamicTextureDataBlendFace(string dynamicID, string contentType, string data, string extraParams,
                                          bool blend, int disp, int timer, int alpha, int face)
        {
            CheckThreatLevel(ThreatLevel.VeryLow , "osSetDynamicTextureDataBlendFace");

            if (dynamicID.Length == 0)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager is not null)
                {
                    if (extraParams.Length == 0)
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

            return ScriptBaseClass.NULL_KEY;
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
            if(m_host.OwnerID.Equals(agentId))
                return true;

            if (m_item.PermsGranter.Equals(agentId))
            {
                if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) != 0)
                    return true;
            }

            ILandObject land = World.LandChannel.GetLandObject(pos);
            if(land is null)
                return true;

            LandData landdata = land.LandData;
            if(landdata is null)
                return true;

            if(landdata.OwnerID.Equals(m_host.OwnerID))
                return true;

            EstateSettings es = World.RegionInfo.EstateSettings;
            if(es is not null && es.IsEstateManagerOrOwner(m_host.OwnerID))
                return true;

            if(!landdata.IsGroupOwned)
                return false;

            UUID landGroup = landdata.GroupID;
            if(landGroup.IsZero())
                return false;

            if(landGroup.Equals(m_host.GroupID))
                return true;

            return false;
        }

        // Teleport functions
        public void osLocalTeleportAgent(LSL_Key agent, LSL_Types.Vector3 position, LSL_Types.Vector3 velocity, LSL_Types.Vector3 lookat, LSL_Integer flags)
        {
            if (!UUID.TryParse(agent, out UUID agentId))
                return;

            ScenePresence presence = World.GetScenePresence(agentId);
            if (presence is null || presence.IsDeleted || presence.IsInTransit)
                return;

            Vector3 pos = presence.AbsolutePosition;
            if (!checkAllowAgentTPbyLandOwner(agentId, pos))
                return;

            World.RequestLocalTeleport(presence, position, velocity, lookat, flags);
        }

        public void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.Severe, "osTeleportAgent");

            TeleportAgent(agent, regionName, position, lookat);
        }

        private void TeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            if (UUID.TryParse(agent, out UUID agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence is null || presence.IsDeleted || presence.IsChildAgent || presence.IsInTransit)
                    return;

                Vector3 pos = presence.AbsolutePosition;
                if (!checkAllowAgentTPbyLandOwner(agentId, pos))
                {
                    ScriptSleep(500);
                    return;
                }

                if (string.IsNullOrEmpty(regionName) || regionName.Equals(World.RegionInfo.RegionName, StringComparison.InvariantCultureIgnoreCase))
                {
                    World.RequestTeleportLocation(presence.ControllingClient, World.RegionInfo.RegionName, position,
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
            if (UUID.TryParse(agent, out UUID agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence is null || presence.IsDeleted || presence.IsChildAgent || presence.IsInTransit)
                    return;

                Vector3 pos = presence.AbsolutePosition;
                if (!checkAllowAgentTPbyLandOwner(agentId, pos))
                {
                    ScriptSleep(500);
                    return;
                }

                ulong regionHandle = Util.RegionGridLocToHandle((uint)regionGridX, (uint)regionGridY);
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
            if (UUID.TryParse(agent, out UUID agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsChildAgent || presence.IsInTransit)
                    return;

                Vector3 pos = presence.AbsolutePosition;
                if (!checkAllowAgentTPbyLandOwner(agentId, pos))
                {
                    ScriptSleep(500);
                    return;
                }

                RegionInfo ri = World.RegionInfo;
                double px = position.x;
                double py = position.y;

                if (px >= 0 && px < ri.RegionSizeX && py >= 0 && py < ri.RegionSizeY)
                {
                    World.RequestTeleportLocation(presence.ControllingClient, ri.RegionName, position,
                        lookat, (uint)TPFlags.ViaLocation);
                    ScriptSleep(500);
                    return;
                }

                // not in region. lets use global position then.
                px += ri.WorldLocX;
                py += ri.WorldLocY;

                int gx = (int)px / 256;
                int gy = (int)py / 256;
                px -= 256 * gx;
                py -= 256 * gy;
                ulong regionHandle = Util.RegionGridLocToHandle((uint)gx, (uint)gy);
                Util.FireAndForget(
                    o => World.RequestTeleportLocation(
                        presence.ControllingClient, regionHandle,
                        new Vector3((float)px, (float)py, (float)position.z), lookat, (uint)TPFlags.ViaLocation),
                    null, "OSSL_Api.TeleportAgentByFarPos");
                ScriptSleep(5000);
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

            if(UUID.TryParse( target, out UUID targetID))
                ForceSit(avatar, targetID);
        }

        public void ForceSit(string avatar, UUID targetID)
        {
            if (!UUID.TryParse(avatar, out UUID agentID))
                return;

            ScenePresence presence = World.GetScenePresence(agentID);
            if(presence is null)
                return;

            SceneObjectPart part = World.GetSceneObjectPart(targetID);

            if (part is not null && part.SitTargetAvatar.IsZero())
                presence.HandleAgentRequestSit(presence.ControllingClient,
                    agentID, targetID,
                    part.SitTargetPosition);
        }

        // Get a list of all the avatars/agents in the region
        public LSL_List osGetAgents()
        {
            // threat level is None as we could get this information with an
            // in-world script as well, just not as efficient
            CheckThreatLevel(ThreatLevel.None, "osGetAgents");

            LSL_List result = new();
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
                return string.Empty;

            if(!UUID.TryParse(agent, out UUID avatarID))
                return string.Empty;

            ScenePresence target = World.GetScenePresence(avatarID);
            if (target is not null)
            {
                return target.ControllingClient.RemoteEndPoint.Address.ToString();
            }

            return string.Empty;
        }

        // Adam's super super custom animation functions
        public void osAvatarPlayAnimation(LSL_Key avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarPlayAnimation");

            if (!UUID.TryParse(avatar, out UUID avatarID))
                return;

            ScenePresence target = World.GetScenePresence(avatarID);
            if (target is null)
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

            if (animID.IsZero())
                target.Animator.AddAnimation(animation, m_host.UUID);
            else
                target.Animator.AddAnimation(animID, m_host.UUID);
        }

        public void osAvatarStopAnimation(LSL_Key avatar, string animation)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarStopAnimation");

            if (!UUID.TryParse(avatar, out UUID avatarID))
                return;

            ScenePresence target = World.GetScenePresence(avatarID);
            if (target is null)
                return;

            if (!UUID.TryParse(animation, out UUID animID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(animation);
                if (item != null && item.Type == (int)AssetType.Animation)
                    animID = item.AssetID;
                else
                    animID = UUID.Zero;
            }

            if (animID.IsZero())
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
            CheckThreatLevel();

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
            drawList += "FillPolygon " + x.GetStringItem(0) + "," + y.GetStringItem(0);
            for (int i = 1; i < x.Length; i++)
            {
                drawList += "," + x.GetStringItem(i) + "," + y.GetStringItem(i);
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
            drawList += "Polygon " + x.GetStringItem(0) + "," + y.GetStringItem(0);
            for (int i = 1; i < x.Length; i++)
            {
                drawList += "," + x.GetStringItem(i) + "," + y.GetStringItem(i);
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

        public string osSetPenColor(string drawList, LSL_Types.Vector3 color)
        {
            CheckThreatLevel();

            byte argbR = Utils.FloatZeroOneToByte((float)color.x);
            byte argbG = Utils.FloatZeroOneToByte((float)color.y);
            byte argbB = Utils.FloatZeroOneToByte((float)color.z);

            StringBuilder sb = new(19);
            sb.Append("PenColor FF");
            sb.Append(Utils.charHighNibbleToHexChar(argbR));
            sb.Append(Utils.charLowNibbleToHexChar(argbR));
            sb.Append(Utils.charHighNibbleToHexChar(argbG));
            sb.Append(Utils.charLowNibbleToHexChar(argbG));
            sb.Append(Utils.charHighNibbleToHexChar(argbB));
            sb.Append(Utils.charLowNibbleToHexChar(argbB));
            sb.Append("; ");

            drawList += sb.ToString();
            return drawList;
        }

        public string osSetPenColor(string drawList, LSL_Types.Vector3 color, LSL_Float alpha)
        {
            CheckThreatLevel();

            byte argbA = Utils.FloatZeroOneToByte((float)alpha);
            byte argbR = Utils.FloatZeroOneToByte((float)color.x);
            byte argbG = Utils.FloatZeroOneToByte((float)color.y);
            byte argbB = Utils.FloatZeroOneToByte((float)color.z);

            StringBuilder sb = new(19);
            sb.Append("PenColor ");
            sb.Append(Utils.charHighNibbleToHexChar(argbA));
            sb.Append(Utils.charLowNibbleToHexChar(argbA));
            sb.Append(Utils.charHighNibbleToHexChar(argbR));
            sb.Append(Utils.charLowNibbleToHexChar(argbR));
            sb.Append(Utils.charHighNibbleToHexChar(argbG));
            sb.Append(Utils.charLowNibbleToHexChar(argbG));
            sb.Append(Utils.charHighNibbleToHexChar(argbB));
            sb.Append(Utils.charLowNibbleToHexChar(argbB));
            sb.Append(';');
            sb.Append(' ');

            drawList += sb.ToString();
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

            IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
            if (textureManager is not null)
            {
                textureManager.GetDrawStringSize(contentType, text, fontName, fontSize,
                                                 out double xSize, out double ySize);
                return new LSL_Vector(xSize, ySize, 0);
            }
            return LSL_Vector.Zero;
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
            /*
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
            */
        }

        /// <summary>
        /// Return the current Sun Hour 0...24, with 0 being roughly sun-rise
        /// </summary>
        /// <returns></returns>
        public LSL_Float osGetCurrentSunHour()
        {
            CheckThreatLevel();

            if (m_envModule is null)
                return 0;

            float frac = m_envModule.GetRegionDayFractionTime();
            return 24 * frac;
        }

        public LSL_Float osGetApparentTime()
        {
            CheckThreatLevel();

            if (m_envModule is null)
                return 0;

            float frac = m_envModule.GetRegionDayFractionTime();
            return 86400 * frac;
        }

        private static string timeToString(float frac, bool format24)
        {
            int h = (int)frac;
            frac -= h;
            frac *= 60;
            int m = (int)frac;
            frac -= m;
            frac *= 60;
            int s = (int)frac;

            if (format24)
            {
                return string.Format("{0:00}:{1:00}:{2:00}", h, m, s);
            }
            if (h > 12)
                return string.Format("{0}:{1:00}:{2:00} PM", h - 12, m, s);
            if (h == 12)
                return string.Format("{0}:{1:00}:{2:00} PM", h, m, s);
            return string.Format("{0}:{1:00}:{2:00} AM", h, m, s);
        }

        public LSL_String osGetApparentTimeString(LSL_Integer format24)
        {
            CheckThreatLevel();

            if (m_envModule is null)
                return format24 != 0 ? "00:00:00" : "0:00:00 AM";

            float frac = 24 * m_envModule.GetRegionDayFractionTime();
            return timeToString(frac, format24 != 0);
        }

        public LSL_Float osGetApparentRegionTime()
        {
            CheckThreatLevel();

            if (m_envModule is null)
                return 0;

            float frac = m_envModule.GetRegionDayFractionTime();
            return 86400 * frac;
        }

        public LSL_String osGetApparentRegionTimeString(LSL_Integer format24)
        {
            CheckThreatLevel();

            if (m_envModule is null)
                return format24 != 0 ? "00:00:00" : "0:00:00 AM";

            float frac = 24 * m_envModule.GetRegionDayFractionTime();

            return timeToString(frac, format24 != 0);
        }


        public double osSunGetParam(string param)
        {
            CheckThreatLevel(ThreatLevel.None, "osSunGetParam");
            OSSLDeprecated("osSunGetParam", "osGetSunParam");
            return GetSunParam(param);
        }

        public LSL_Float osGetSunParam(LSL_String param)
        {
            CheckThreatLevel();
            return GetSunParam(param);
        }

        private double GetSunParam(string param)
        {
            param = param.ToLower();
            switch(param)
            {
                case "day_length":
                     return m_envModule is null ?  14400 : m_envModule.GetDayLength(m_host.AbsolutePosition);
                case "year_length":
                    return 365;
                case "day_night_offset":
                    return 0;
                case "update_interval":
                    return 0.1;
                case "day_time_sun_hour_scale":
                    return 1;
                default:
                    break;
            }
            return 0;
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
            module?.SetSunParameter(param, value);
        }

        public string osWindActiveModelPluginName()
        {
            CheckThreatLevel(ThreatLevel.None, "osWindActiveModelPluginName");

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module is not null)
            {
                return module.WindActiveModelPluginName;
            }

            return string.Empty;
        }

        public void osSetWindParam(string plugin, string param, LSL_Float value)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetWindParam");

            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module is not null)
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
            if (module is not null)
            {
                return module.WindParamGet(plugin, param);
            }

            return 0.0f;
        }

        public LSL_Integer osGetParcelDwell(LSL_Vector pos)
        {
            LandData land = World.GetLandData(pos);
            if (land is not null)
            {
                return (int)land.Dwell;
            }
            return 0;
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
            CheckThreatLevel(ThreatLevel.High, "osParcelSetDetails");
            OSSLDeprecated("osParcelSetDetails", "osSetParcelDetails");
            SetParcelDetails(pos, rules);
        }

        public void osSetParcelDetails(LSL_Vector pos, LSL_List rules)
        {
            CheckThreatLevel(ThreatLevel.High, "osSetParcelDetails");
            SetParcelDetails(pos, rules);
        }

        private void SetParcelDetails(LSL_Vector pos, LSL_List rules)
        {
            // Get a reference to the land data and make sure the owner of the script
            // can modify it

            ILandObject startLandObject = World.LandChannel.GetLandObject((int)pos.x, (int)pos.y);
            if (startLandObject is null)
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

            string arg;
            int iarg;
            // Process the rules, not sure what the impact would be of changing owner or group
            for (int idx = 0; idx < rules.Length;)
            {
                int code = rules.GetIntegerItem(idx++);
                switch (code)
                {
                    case ScriptBaseClass.PARCEL_DETAILS_NAME:
                        arg = rules.GetStrictStringItem(idx++);
                        if (newLand.Name != arg)
                        {
                            newLand.Name = arg;
                            changed = true;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_DESC:
                        arg = rules.GetStrictStringItem(idx++);
                        if (newLand.Description != arg)
                        {
                            newLand.Description = arg;
                            changed = true;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_OWNER:
                        arg = rules.GetStrictStringItem(idx++);
                        if (es is not null && !es.IsEstateManagerOrOwner(m_host.OwnerID))
                        {
                            OSSLShoutError("script owner does not have permission to modify the parcel owner");
                        }
                        else
                        {
                            if (UUID.TryParse(arg, out uuid))
                            {
                                if(newLand.OwnerID.NotEqual(uuid))
                                {
                                    changed = true;
                                    newLand.OwnerID = uuid;
                                    newLand.GroupID = UUID.Zero;
                                }
                            }
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_GROUP:
                        arg = rules.GetStrictStringItem(idx++);
                        if (m_host.OwnerID.Equals(newLand.OwnerID) || es == null || es.IsEstateManagerOrOwner(m_host.OwnerID))
                        {
                            if (UUID.TryParse(arg, out uuid))
                            {
                                if(newLand.GroupID.NotEqual(uuid))
                                {
                                    if(uuid.IsZero())
                                    {
                                        changed = true;
                                        newLand.GroupID = uuid;
                                    }
                                    else
                                    {
                                        GroupMembershipData member = null;
                                        if (m_groupsModule is not null)
                                            member = m_groupsModule.GetMembershipData(uuid, newLand.OwnerID);
                                        if (member is null)
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
                        if(es is not null && !es.IsEstateManagerOrOwner(m_host.OwnerID))
                        {
                            OSSLShoutError("script owner does not have permission to modify the parcel CLAIM DATE");
                        }
                        else
                        {
                            int date = rules.GetIntegerItem(idx++);
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
                        iarg = rules.GetIntegerItem(idx++);
                        bool newavs = iarg != 0;
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
                        iarg = rules.GetIntegerItem(idx++);
                        bool newavsounds = iarg != 0;
                        if(newLand.AnyAVSounds != newavsounds)
                        {
                            changed = true;
                            newLand.AnyAVSounds = newavsounds;
                        }
                        break;

                    case ScriptBaseClass.PARCEL_DETAILS_GROUP_SOUNDS:
                        iarg = rules.GetIntegerItem(idx++);
                        bool newgrpsounds = iarg != 0;
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
                        if (avatar is null || avatar.IsDeleted || avatar.IsInTransit)
                            return;

                        if(changedSeeAvs && avatar.currentParcelUUID.Equals(parcelID))
                            avatar.currentParcelUUID = parcelID; // force parcel flags review

                        if(avatar.ControllingClient is null)
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

        public void osSetParcelMusicURL(LSL_String url)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMusicURL");

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            land?.SetMusicUrl(url);
        }

        public void osSetParcelMediaURL(LSL_String url)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMediaURL");

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            land?.SetMediaUrl(url);
        }

        public void osSetParcelSIPAddress(string SIPAddress)
        {
            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelSIPAddress");

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (land.LandData.OwnerID.NotEqual(m_host.OwnerID))
            {
                OSSLError("osSetParcelSIPAddress: Sorry, you need to own the land to use this function");
                return;
            }

            // get the voice module
            IVoiceModule voiceModule = World.RequestModuleInterface<IVoiceModule>();

            if (voiceModule is not null)
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

            if (!string.IsNullOrEmpty(m_ScriptEngine.ScriptEngineName))
            {
                // parse off the "ScriptEngine."
                scriptEngineNameIndex = m_ScriptEngine.ScriptEngineName.IndexOf('.', scriptEngineNameIndex);
                scriptEngineNameIndex++; // get past delimiter

                int scriptEngineNameLength = m_ScriptEngine.ScriptEngineName.Length - scriptEngineNameIndex;

                // create char array then a string that is only the script engine name
                Char[] scriptEngineNameCharArray = m_ScriptEngine.ScriptEngineName.ToCharArray(scriptEngineNameIndex, scriptEngineNameLength);
                string scriptEngineName = new(scriptEngineNameCharArray);

                return scriptEngineName;
            }

            return string.Empty;
        }

        public LSL_Integer osCheckODE()
        {
            CheckThreatLevel();

            if (m_ScriptEngine.World.PhysicsScene is not null)
            {
                if (m_ScriptEngine.World.PhysicsScene.EngineType == "OpenDynamicsEngine")
                {
                    return new LSL_Integer(1);    // true
                }
            }
            return new LSL_Integer(0);
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
            string ret = string.Empty;
            if (string.IsNullOrEmpty(CheckThreatLevelTest(ThreatLevel.High, "osGetPhysicsEngineType")))
            {
                if (m_ScriptEngine.World.PhysicsScene != null)
                {
                    ret = m_ScriptEngine.World.PhysicsScene.EngineType;
                    // An old physics engine might have an uninitialized engine type
                    ret ??= "unknown";
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
                ret ??= "UnknownEngine";
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

            if (!UUID.TryParse(objectUUID, out UUID objUUID)) // prior to patching, a thrown exception regarding invalid GUID format would be shouted instead.
            {
                OSSLShoutError("osMessageObject() cannot send messages to objects with invalid UUIDs");
                return;
            }

            MessageObject(objUUID, message);
            //ScriptSleep(25); // mostly a thread yield
        }

        private void MessageObject(UUID objUUID, string message)
        {
            object[] resobj = new object[] { new LSL_String(m_host.UUID.ToString()), new LSL_String(message) };

            SceneObjectPart sceneOP = World.GetSceneObjectPart(objUUID);

            if (sceneOP == null) // prior to patching, PostObjectEvent() would cause a throw exception to be shouted instead.
            {
                OSSLShoutError("osMessageObject() cannot send message to " + objUUID.ToString() + ", object was not found in scene.");
                return;
            }

            m_ScriptEngine.PostObjectEvent(
                sceneOP.LocalId, new EventParams(
                    "dataserver", resobj, Array.Empty<DetectParams>()));
        }


        /// <summary>
        /// Similar to llDie but given an object UUID
        /// </summary>
        /// <param name="objectUUID"></param>

        public void osDie(LSL_Key objectUUID)
        {
            CheckThreatLevel(ThreatLevel.Low, "osDie");

            if (!UUID.TryParse(objectUUID, out UUID objUUID))
            {
                OSSLShoutError("osDie() cannot delete objects with invalid UUIDs");
                return;
            }

            if(objUUID.IsZero())
                return;

            SceneObjectGroup sceneOG = World.GetSceneObjectGroup(objUUID);

            if (sceneOG is null || sceneOG.IsDeleted || sceneOG.IsAttachment)
                return;

            if (sceneOG.OwnerID.NotEqual(m_host.OwnerID))
                return;

            // restrict to objects rezzed by host
            if(sceneOG.RezzerID.Equals(m_host.ParentGroup.UUID))
            {
                // harakiri check should be true alwaya
                if (sceneOG.UUID.NotEqual(m_host.ParentGroup.UUID))
                    World.DeleteSceneObject(sceneOG, false);
            }
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
        public void osMakeNotecard(string notecardName, LSL_String contents)
        {
            CheckThreatLevel(ThreatLevel.High, "osMakeNotecard");
            SaveNotecard(notecardName, "Script generated notecard", contents + "\n", false);
        }

        public void osMakeNotecard(string notecardName, LSL_List contents)
        {
            CheckThreatLevel(ThreatLevel.High, "osMakeNotecard");

            StringBuilder notecardData = new();

            for (int i = 0; i < contents.Length; i++)
                notecardData.Append(contents.GetStringItem(i) + "\n");

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
            AssetBase asset = new(UUID.Random(), name, (sbyte)AssetType.Notecard, m_host.OwnerID.ToString())
            {
                Description = description
            };

            osUTF8 contents = new(data, 65536);
            int len = contents.Length;
            osUTF8Slice utf = new(len + 128);
            utf.AppendASCII("Linden text version 2\n{\nLLEmbeddedItems version 1\n{\ncount 0\n}\nText length ");
            utf.AppendASCII(len.ToString());
            utf.AppendASCII("\n");
            utf.Append(contents);
            utf.AppendASCII("}");

            asset.Data = utf.ToArray();
            World.AssetService.Store(asset);

            // Create Task Entry
            TaskInventoryItem taskItem = new()
            {
                ParentID = m_host.UUID,
                CreationDate = (uint)Util.UnixTimeSinceEpoch(),
                Name = name,
                Description = description,
                Type = (int)AssetType.Notecard,
                InvType = (int)InventoryType.Notecard,
                OwnerID = m_host.OwnerID,
                CreatorID = m_host.OwnerID,
                BasePermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export,
                CurrentPermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export,
                EveryonePermissions = 0,
                NextPermissions = (uint)PermissionMask.All,
                GroupID = m_host.GroupID,
                GroupPermissions = 0,
                Flags = 0,
                PermsGranter = UUID.Zero,
                PermsMask = 0,
                AssetID = asset.FullID
            };

            taskItem.ResetIDs(m_host.UUID);

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

            if (!assetID.IsZero())
            {
                StringBuilder notecardData = new();

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
            if (!UUID.TryParse(notecardNameOrUuid, out UUID assetID))
            {
                assetID = SearchTaskInventoryForAssetId(notecardNameOrUuid);
            }

            if (assetID.IsZero())
                return UUID.Zero;

            if (!NotecardCache.IsCached(assetID))
            {
                AssetBase a = World.AssetService.Get(assetID.ToString());

                if (a == null)
                {
                    // Whoops, it's still possible here that the notecard name was properly
                    // formatted like a UUID but isn't an asset UUID so lets look it up by name after all
                    assetID = SearchTaskInventoryForAssetId(notecardNameOrUuid);
                    if (assetID.IsZero())
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
            }

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

            if (assetID.IsZero())
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

            if (assetID.IsZero())
            {
                OSSLShoutError("Notecard '" + name + "' could not be found.");
                return -1;
            }

            return NotecardCache.GetLines(assetID);
        }

        public string osAvatarName2Key(string firstname, string lastname)
        {
            CheckThreatLevel(ThreatLevel.Low, "osAvatarName2Key");

            ScenePresence sp = World.GetScenePresence(firstname, lastname);
            if(sp is not null)
                return sp.UUID.ToString();

            IUserManagement userManager = World.RequestModuleInterface<IUserManagement>();
            if (userManager is null)
            {
                OSSLShoutError("osAvatarName2Key: UserManagement module not available");
                return string.Empty;
            }

            // Check local grid
            UUID userID = userManager.GetUserIdByName(firstname, lastname);
            if (!userID.IsZero())
                return userID.ToString();

            // HG ?
            if (Util.ParseForeignAvatarName(firstname, lastname, out string realFirstName, out string realLastName, out string serverURI))
            {
                try
                {
                    UserAgentServiceConnector userConnection = new(serverURI);

                    if (userConnection is not null)
                    {
                        userID = userConnection.GetUUID(realFirstName, realLastName);
                        if (!userID.IsZero())
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

            return ScriptBaseClass.NULL_KEY;
        }

        public string osKey2Name(string id)
        {
            CheckThreatLevel(ThreatLevel.Low, "osKey2Name");

            if (UUID.TryParse(id, out UUID key))
            {
                ScenePresence sp = World.GetScenePresence(key);
                if(sp is not null)
                    return sp.Name;

                UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
                if (account is not null)
                    return account.Name;

                if (m_ScriptEngine.World.GridUserService is not null)
                {
                    GridUserInfo uInfo = m_ScriptEngine.World.GridUserService.GetGridUserInfo(key.ToString());

                    if (uInfo is not null)
                    {
                        if (Util.ParseFullUniversalUserIdentifier(uInfo.UserID, out _, 
                                out string gridURL, out string firstName, out string lastName))
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
            IConfigSource config = m_ScriptEngine.ConfigSource;
            string url = null;

            IConfig gridInfoConfig = config.Configs["GridInfo"];

            if (gridInfoConfig is not null)
                url = gridInfoConfig.GetString("GridInfoURI", String.Empty);

            if (string.IsNullOrEmpty(url))
                return "Configuration Error!";

            string verb ="/json_grid_info";
            OSDMap info =  WebUtil.GetFromService($"{url}{verb}", 3000);

            if (info["Success"].AsBoolean() != true)
                return "Get GridInfo Failed!";

            OSDMap json = (OSDMap)OSDParser.DeserializeJson(info["_RawResult"].AsString());

            return type switch
            {
                InfoType.Nick => (string)json["gridnick"],
                InfoType.Name => (string)json["gridname"],
                InfoType.Login => (string)json["login"],
                InfoType.Home => (string)json["home"],
                InfoType.Custom => (string)json[key],
                _ => "error",
            };
        }

        public string osSHA256(string input)
        {
            byte[] bytes;
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
            return Util.bytesToLowcaseHexString(bytes);
        }

        /// <summary>
        /// Get the nickname of this grid, as set in the [GridInfo] config section.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns></returns>
        public string osGetGridNick()
        {
            return World.SceneGridInfo == null ? string.Empty : World.SceneGridInfo.GridNick;
        }

        public string osGetGridName()
        {
            return World.SceneGridInfo == null ? string.Empty : World.SceneGridInfo.GridName;
        }

        public string osGetGridLoginURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridLoginURI");

            string loginURI = string.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;

            if (config.Configs[GridInfoServiceConfigSectionName] != null)
                loginURI = config.Configs[GridInfoServiceConfigSectionName].GetString("login", loginURI);

            if (string.IsNullOrEmpty(loginURI))
                loginURI = GridUserInfo(InfoType.Login);

            return loginURI;
        }

        public string osGetGridHomeURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridHomeURI");

            return World.SceneGridInfo == null ? string.Empty : World.SceneGridInfo.HomeURLNoEndSlash;
        }

        public string osGetGridGatekeeperURI()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridGatekeeperURI");

            return World.SceneGridInfo == null ? string.Empty : World.SceneGridInfo.GateKeeperURLNoEndSlash;
        }

        public string osGetGridCustom(string key)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetGridCustom");

            string retval = string.Empty;
            IConfigSource config = m_ScriptEngine.ConfigSource;

            if (config.Configs[GridInfoServiceConfigSectionName] != null)
                retval = config.Configs[GridInfoServiceConfigSectionName].GetString(key, retval);

            if (string.IsNullOrEmpty(retval))
                retval = GridUserInfo(InfoType.Custom, key);

            return retval;
        }

        public string osGetAvatarHomeURI(string uuid)
        {
            CheckThreatLevel(ThreatLevel.Low, "osGetAvatarHomeURI");

            IUserManagement userManager = m_ScriptEngine.World.RequestModuleInterface<IUserManagement>();
            string returnValue;

            if (userManager is null)
                returnValue = string.Empty;
            else
                returnValue = userManager.GetUserServerURL(new UUID(uuid), "HomeURI");

            if (returnValue.Length == 0)
                return World.SceneGridInfo.HomeURLNoEndSlash;

            return returnValue;
        }

        public LSL_String osFormatString(string str, LSL_List strings)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osFormatString");

            return string.Format(str, strings.Data);
        }

        public LSL_List osMatchString(string src, string pattern, int start)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osMatchString");

            LSL_List result = new();

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
            Regex matcher = new(pattern);
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
            // After normalization they may still be
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
            Regex matcher = new(pattern);
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
            LSL_List retVal = new();
            LSL_List remaining = new();
            List<SceneObjectPart> parts = m_LSL_Api.GetLinkParts(linknumber);
            foreach (SceneObjectPart part in parts)
            {
                remaining = m_LSL_Api.GetPrimParams(part, rules, ref retVal);
            }

            while (remaining.Length > 1)
            {
                linknumber = remaining.GetIntegerItem(0);
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
            if (module is not null)
            {
                if (UUID.TryParse(npc.m_string, out UUID npcId))
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
            if(module is null)
                return LSL_Key.NullKey;

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
                return LSL_Key.NullKey;
            }

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if(module is null)
            {
                OSSLError("NPC module not enabled");
                return LSL_Key.NullKey;
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

            if(hostGroupID && !m_host.GroupID.IsZero())
            {
                if (m_groupsModule != null)
                {
                    GroupMembershipData member = m_groupsModule.GetMembershipData(m_host.GroupID, m_host.OwnerID);
                    if (member == null)
                    {
                        OSSLError(string.Format("osNpcCreate: the object owner is not member of the object group"));
                        return LSL_Key.NullKey;
                    }

                    groupID = m_host.GroupID;

                    if((createFlags & NPCOptionsFlags.NoNPCGroup) != 0)
                    {
                        GroupRecord grprec = m_groupsModule.GetGroupRecord(m_host.GroupID);
                        if(grprec != null && grprec.GroupName != "")
                            groupTitle = grprec.GroupName;
                    }
                }
            }

            if((createFlags & NPCOptionsFlags.NoNPCGroup) == 0)
            {
                if (!string.IsNullOrEmpty(firstname) || !string.IsNullOrEmpty(lastname))
                {
                    if (firstname != "Shown outfit:")
                        groupTitle = "- NPC -";
                }
            }

            if((createFlags & NPCOptionsFlags.AllowCloneOtherAvatars) != 0)
            {
                if (UUID.TryParse(notecard, out UUID id))
                {
                    ScenePresence clonePresence = World.GetScenePresence(id);
                    if (clonePresence != null)
                        appearance = clonePresence.Appearance;
                }
            }

            if (appearance is null)
            {
                if(!string.IsNullOrWhiteSpace(notecard))
                {
                    string appearanceSerialized = LoadNotecard(notecard);
                    if (appearanceSerialized is null)
                    {
                        OSSLError(string.Format("osNpcCreate: Notecard '{0}' not found.", notecard));
                        return LSL_Key.NullKey;
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
                        return LSL_Key.NullKey;
                    }
                }
            }

            UUID x = module.CreateNPC(firstname,
                                      lastname,
                                      position,
                                      UUID.Random(),
                                      owned ? m_host.OwnerID : UUID.Zero,
                                      groupTitle,
                                      groupID,
                                      senseAsAgent,
                                      World,
                                      appearance);

            if (World.TryGetScenePresence(x, out ScenePresence sp))
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

        public LSL_Key osNpcSaveAppearance(LSL_Key npc, LSL_String notecard)
        {
            return NpcSaveAppearance(npc, notecard, false);
        }

        public LSL_Key osNpcSaveAppearance(LSL_Key npc, LSL_String notecard, LSL_Integer includeHuds)
        {
            return NpcSaveAppearance(npc, notecard, includeHuds == 0);
        }

        protected LSL_Key NpcSaveAppearance(LSL_Key npc, string notecard, bool NoHUds)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSaveAppearance");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();

            if (npcModule is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
                    return LSL_Key.NullKey;

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return LSL_Key.NullKey;

                return SaveAppearanceToNotecard(npcId, notecard, NoHUds);
            }

            return LSL_Key.NullKey;
        }

        public void osNpcLoadAppearance(LSL_Key npc, string notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcLoadAppearance");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();

            if (npcModule is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
                    return;

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                string appearanceSerialized = LoadNotecard(notecard);

                if (appearanceSerialized is null)
                    OSSLError(string.Format("osNpcCreate: Notecard reference '{0}' not found.", notecard));

                OSDMap appearanceOsd = (OSDMap)OSDParser.DeserializeLLSDXml(appearanceSerialized);
//                OSD a = OSDParser.DeserializeLLSDXml(appearanceSerialized);
//                Console.WriteLine("appearanceSerialized {0}", appearanceSerialized);
//                Console.WriteLine("a.Type {0}, a.ToString() {1}", a.Type, a);
                AvatarAppearance appearance = new();
                appearance.Unpack(appearanceOsd);

                npcModule.SetNPCAppearance(npcId, appearance, m_host.ParentGroup.Scene);
            }
        }

        public LSL_Key osNpcGetOwner(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.None, "osNpcGetOwner");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule is not null)
            {
                if (UUID.TryParse(npc.m_string, out UUID npcId))
                {
                    UUID owner = npcModule.GetOwner(npcId);
                    if (!owner.IsZero())
                        return new LSL_Key(owner.ToString());
                    else
                        return npc;
                }
            }

            return LSL_Key.NullKey;
        }

        public LSL_Vector osNpcGetPos(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcGetPos");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
                    return LSL_Vector.Zero;

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return LSL_Vector.Zero;

                ScenePresence sp = World.GetScenePresence(npcId);

                if (sp is not null)
                    return new LSL_Vector(sp.AbsolutePosition);
            }

            return LSL_Vector.Zero;
        }

        public void osNpcMoveTo(LSL_Key npc, LSL_Vector pos)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcMoveTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
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
            if (module is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
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
            if (npcModule is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
                    return LSL_Rotation.Identity;

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return LSL_Rotation.Identity;

                ScenePresence sp = World.GetScenePresence(npcId);

                if (sp is not null)
                    return new LSL_Rotation(sp.GetWorldRotation());
            }

            return LSL_Rotation.Identity;
        }

        public void osNpcSetRot(LSL_Key npc, LSL_Rotation rotation)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSetRot");

            INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
            if (npcModule is not null)
            {
                if (!UUID.TryParse(npc.m_string, out UUID npcId))
                    return;

                if (!npcModule.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                ScenePresence sp = World.GetScenePresence(npcId);
                if (sp is not null)
                    sp.Rotation = rotation;
            }
        }

        public void osNpcStopMoveToTarget(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStopMoveToTarget");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.StopMoveToTarget(npcId, World);
            }
        }

        public void osNpcSetProfileAbout(LSL_Key npc, string about)
        {
            CheckThreatLevel(ThreatLevel.Low, "osNpcSetProfileAbout");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                ScenePresence sp = World.GetScenePresence(npcId);
                if (sp is not null)
                    ((INPC)(sp.ControllingClient)).profileAbout = about;
            }
        }

        public void osNpcSetProfileImage(LSL_Key npc, string image)
        {
            CheckThreatLevel(ThreatLevel.Low, "osNpcSetProfileImage");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                UUID ImageID = ScriptUtils.GetAssetIdFromItemName(m_host, image, (int)AssetType.Texture);
                if (ImageID.IsZero())
                {
                    if (!UUID.TryParse(image, out ImageID))
                        return;
                }

                ScenePresence sp = World.GetScenePresence(npcId);
                if (sp is not null)
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
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                InitLSL();
                m_LSL_Api.ThrottleSay(channel, 2000);
                module.Say(npcId, World, message, channel);
            }
        }

        public void osNpcSayTo(LSL_Key npc, LSL_Key target, int channel, string msg)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSayTo");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is null)
                return;
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm is null)
                return;

            if (!UUID.TryParse(npc.m_string, out UUID npcId))
                return;

            if (!UUID.TryParse(target.m_string, out UUID TargetID))
                return;

            if (!module.CheckPermissions(npcId, m_host.OwnerID))
                return;

            ScenePresence NPCpresence = World.GetScenePresence(npcId);
            if (NPCpresence is null || NPCpresence.IsDeleted || !NPCpresence.IsNPC)
                return;

            Vector3 npcPOS = NPCpresence.AbsolutePosition;
            string npcNAME = NPCpresence.Name;

            InitLSL();
            m_LSL_Api.ThrottleSay(channel, 2000);
            wComm.DeliverMessageTo(TargetID, channel, npcPOS, npcNAME, npcId, msg);
        }

        public void osNpcShout(LSL_Key npc, int channel, string message)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcShout");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                InitLSL();
                m_LSL_Api.ThrottleSay(channel, 2000);
                module.Shout(npcId, World, message, channel);
            }
        }

        public void osNpcSit(LSL_Key npc, LSL_Key target, int options)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcSit");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

                if (!module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                module.Sit(npcId, new UUID(target.m_string), World);
            }
        }

        public void osNpcStand(LSL_Key npc)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStand");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

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
                if (module is not null)
                {
                    UUID npcId = new(npc.m_string);

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
            if (module is null)
                return;

            if (!UUID.TryParse(npc.m_string, out UUID npcID))
                return;

            ScenePresence target = World.GetScenePresence(npcID);
            if (target is null || !target.IsNPC)
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

            if (animID.IsZero())
                target.Animator.AddAnimation(animation, m_host.UUID);
            else
                target.Animator.AddAnimation(animID, m_host.UUID);
        }

        public void osNpcStopAnimation(LSL_Key npc, string animation)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcStopAnimation");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is null)
                return;

            if (!UUID.TryParse(npc.m_string, out UUID npcID))
                return;

            ScenePresence target = World.GetScenePresence(npcID);
            if (target is null || !target.IsNPC)
                return;

            if (!module.CheckPermissions(npcID, m_host.OwnerID))
                return;

            if (!UUID.TryParse(animation, out UUID animID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(animation);
                if (item is not null && item.Type == (int)AssetType.Animation)
                    animID = item.AssetID;
                else
                    animID = UUID.Zero;
            }

            if (animID.IsZero())
                target.Animator.RemoveAnimation(animation);
            else
                target.Animator.RemoveAnimation(animID, true);
        }

        public void osNpcWhisper(LSL_Key npc, int channel, string message)
        {
            CheckThreatLevel(ThreatLevel.High, "osNpcWhisper");

            INPCModule module = World.RequestModuleInterface<INPCModule>();
            if (module is not null)
            {
                UUID npcId = new(npc.m_string);

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
            if (module is not null || (linkNum < 0 && linkNum != ScriptBaseClass.LINK_THIS))
            {
                if (!UUID.TryParse(npcLSL_Key, out UUID npcId) || !module.CheckPermissions(npcId, m_host.OwnerID))
                    return;

                SceneObjectPart part = null;
                if (UUID.TryParse(object_key.m_string, out UUID objectId))
                    part = World.GetSceneObjectPart(objectId);

                if (part is null)
                    return;

                if (linkNum != ScriptBaseClass.LINK_THIS)
                {
                    if (linkNum == 0 || linkNum == ScriptBaseClass.LINK_ROOT)
                    { // 0 and 1 are treated as root, find the root if the current part isnt it
                        part = part.ParentGroup.RootPart;
                    }
                    else
                    { // Find the prim with the given link number if not found then fail silently
                        part = part.ParentGroup.GetLinkNumPart(linkNum);
                    }
                }

                if (part is null)
                    return;

                module.Touch(npcId, part.UUID);
            }
        }

        /// <summary>
        /// Save the current appearance of the script owner permanently to the named notecard.
        /// </summary>
        /// <param name="notecard">The name of the notecard to which to save the appearance.</param>
        /// <returns>The asset ID of the notecard saved.</returns>
        public LSL_Key osOwnerSaveAppearance(LSL_String notecard)
        {
            CheckThreatLevel(ThreatLevel.High, "osOwnerSaveAppearance");

            return SaveAppearanceToNotecard(m_host.OwnerID, notecard, false);
        }

        public LSL_Key osOwnerSaveAppearance(LSL_String notecard, LSL_Integer includeHuds)
        {
            CheckThreatLevel(ThreatLevel.High, "osOwnerSaveAppearance");

            return SaveAppearanceToNotecard(m_host.OwnerID, notecard, includeHuds == 0);
        }

        public LSL_Key osAgentSaveAppearance(LSL_Key avatarKey, LSL_String notecard)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAgentSaveAppearance");

            if (!UUID.TryParse(avatarKey, out UUID avatarId))
                return new LSL_Key(ScriptBaseClass.NULL_KEY);

            return SaveAppearanceToNotecard(avatarId, notecard, false);
        }

        public LSL_Key osAgentSaveAppearance(LSL_Key avatarKey, LSL_String notecard, LSL_Integer includeHuds)
        {
            CheckThreatLevel(ThreatLevel.VeryHigh, "osAgentSaveAppearance");

            if (!UUID.TryParse(avatarKey, out UUID avatarId))
                return LSL_Key.NullKey;

            return SaveAppearanceToNotecard(avatarId, notecard, includeHuds == 0);
        }

        protected LSL_Key SaveAppearanceToNotecard(UUID avatarId, string notecard, bool NoHuds)
        {
            ScenePresence sp = World.GetScenePresence(avatarId);
            if (sp is null || sp.IsChildAgent)
                return LSL_Key.NullKey;

            return SaveAppearanceToNotecard(sp, notecard, NoHuds);
        }

        protected LSL_Key SaveAppearanceToNotecard(ScenePresence sp, string notecard, bool NoHuds)
        {
            IAvatarFactoryModule appearanceModule = World.RequestModuleInterface<IAvatarFactoryModule>();

            if (appearanceModule is not null)
            {
                appearanceModule.SaveBakedTextures(sp.UUID);
                OSDMap appearancePacked = sp.Appearance.PackForNotecard(NoHuds);

                TaskInventoryItem item
                    = SaveNotecard(notecard, "Avatar Appearance", Util.GetFormattedXml(appearancePacked as OSD), true);

                return new LSL_Key(item.AssetID.ToString());
            }
            return LSL_Key.NullKey;
        }

        /// <summary>
        /// Get the gender as specified in avatar appearance for a given avatar key
        /// </summary>
        /// <param name="rawAvatarId"></param>
        /// <returns>"male" or "female" or "unknown"</returns>
        public LSL_String osGetGender(LSL_Key rawAvatarId)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetGender");

            if (!UUID.TryParse(rawAvatarId, out UUID avatarId))
                return new LSL_String("unknown");

            ScenePresence sp = World.GetScenePresence(avatarId);

            if (sp is null || sp.IsChildAgent || sp.Appearance is null || sp.Appearance.VisualParams is null)
                return new LSL_String("unknown");

            // find the index of "shape" parameter "male"
            int vpShapeMaleIndex = 0;
            bool indexFound = false;
            VisualParam param = new();
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
        public LSL_Key osGetRegionMapTexture(string regionNameOrID)
        {
            CheckThreatLevel(ThreatLevel.High, "osGetRegionMapTexture");

            Scene scene = m_ScriptEngine.World;
            GridRegion region;

            if(string.IsNullOrWhiteSpace(regionNameOrID))
                return scene.RegionInfo.RegionSettings.TerrainImageID.ToString();

            //If string is a key, use it. Otherwise, try to locate region by name.
            if (UUID.TryParse(regionNameOrID, out UUID key))
                region = scene.GridService.GetRegionByUUID(UUID.Zero, key);
            else
                region = scene.GridService.GetRegionByName(UUID.Zero, regionNameOrID);

            // If region was found, return the regions map texture key.
            if (region is not null)
                key = region.TerrainImage;

            ScriptSleep(1000);

            return new LSL_Key(key.ToString());
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

            LSL_List ret = new();
            float[] stats = World.StatsReporter.LastReportedSimStats;

            for (int i = 0; i < stats.Length; i++)
            {
                ret.Add(new LSL_Float(stats[i]));
            }
            return ret;
        }

        public LSL_Vector osGetRegionSize()
        {
            CheckThreatLevel();

            //Scene scene = m_ScriptEngine.World;
            RegionInfo reg =  World.RegionInfo;
            //GridRegion region = scene.GridService.GetRegionByUUID(UUID.Zero, World.RegionInfo.RegionID);
            //return new LSL_Vector((float)region.RegionSizeX, (float)region.RegionSizeY, (float)Constants.RegionHeight);
            return new LSL_Vector((float)reg.RegionSizeX, (float)reg.RegionSizeY, 0.0f);
        }

        public int osGetSimulatorMemory()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetSimulatorMemory");

            long pws = Util.GetPhysicalMemUse();

            if (pws > Int32.MaxValue)
                return Int32.MaxValue;
            if (pws < 0)
                return 0;

            return (int)pws;
        }

        public int osGetSimulatorMemoryKB()
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetSimulatorMemoryKB");

            long pws = Util.GetPhysicalMemUse();

            if ((pws & 0x3FFL) != 0)
                pws += 0x400L;
            pws >>= 10;

            if (pws > Int32.MaxValue)
                return Int32.MaxValue;

            return (int)pws;
        }

        public void osSetSpeed(string ID, LSL_Float SpeedModifier)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osSetSpeed");

            if (!UUID.TryParse(ID, out UUID avid))
                return;

            ScenePresence avatar = World.GetScenePresence(avid);
            if (avatar is not null)
                avatar.SpeedModifier = (float)SpeedModifier;
        }

        public void osSetOwnerSpeed(LSL_Float SpeedModifier)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osSetOwnerSpeed");

            if(SpeedModifier > 4)
                SpeedModifier = 4;

            ScenePresence avatar = World.GetScenePresence(m_host.OwnerID);
            if (avatar is not null)
                avatar.SpeedModifier = (float)SpeedModifier;
        }

        public void osKickAvatar(string FirstName, string SurName, string alert)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osKickAvatar");

            if(string.IsNullOrEmpty(alert))
            {
                World.ForEachRootScenePresence(delegate (ScenePresence sp)
                {
                    if (sp.Firstname == FirstName && sp.Lastname == SurName)
                        sp.Scene.CloseAgent(sp.UUID, false);
                });
            }
            else
            {
                World.ForEachRootScenePresence(delegate (ScenePresence sp)
                {
                    if (sp.Firstname == FirstName && sp.Lastname == SurName)
                    {
                        sp.ControllingClient.Kick(alert);
                        sp.Scene.CloseAgent(sp.UUID, false);
                    }
                });
            }
        }

        public void osKickAvatar(LSL_Key agentKey, string alert)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osKickAvatar");

            if (!UUID.TryParse(agentKey, out UUID id) || id.IsZero())
                return;

            ScenePresence sp = World.GetScenePresence(id);
            if(sp is null)
                return;

            // kick client...
            if (!string.IsNullOrEmpty(alert))
                sp.ControllingClient.Kick(alert);

            // ...and close on our side
            sp.Scene.CloseAgent(id, false);
        }

        public LSL_Float osGetHealth(LSL_Key agentKey)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetHealth");

            LSL_Float health = new(-1);

            if (!UUID.TryParse(agentKey, out UUID id) || id.IsZero())
                return health;

            ScenePresence presence = World.GetScenePresence(id);
            if (presence is not null)
                health = presence.Health;
            return health;
        }

        public void osCauseDamage(LSL_Key avatar, LSL_Float damage)
        {
            CheckThreatLevel(ThreatLevel.High, "osCauseDamage");

            if (!UUID.TryParse(avatar, out UUID avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence is null)
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
                    // check to see if it is an NPC and just remove it
                    if (presence.IsNPC)
                    {
                        INPCModule NPCmodule = World.RequestModuleInterface<INPCModule>();
                        NPCmodule?.DeleteNPC(presence.UUID, World);
                    return;
                    }

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

            if (!UUID.TryParse(avatar, out UUID avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence is null)
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

            if (!UUID.TryParse(avatar, out UUID avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);

            if (presence is not null)
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

            if (!UUID.TryParse(avatar, out UUID avatarId))
                return;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence is null)
                return;

            presence.HealRate = (float)healrate;
        }

        public LSL_Float osGetHealRate(LSL_Key avatar)
        {
            CheckThreatLevel(ThreatLevel.None, "osGetHealRate");

            LSL_Float rate = new(0);

            if (!UUID.TryParse(avatar, out UUID avatarId))
                return rate;

            ScenePresence presence = World.GetScenePresence(avatarId);
            if (presence is not null)
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
        public void osSetProjectionParams(LSL_Integer projection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb)
        {
            SetProjectionParams(m_host, projection, texture, fov, focus, amb);
        }

        /// <summary>
        /// Set parameters for light projection of a linkset prim
        /// </summary>
        public void osSetProjectionParams(LSL_Integer linknum, LSL_Integer projection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb)
        {
            if (linknum == ScriptBaseClass.LINK_THIS || linknum == m_host.LinkNum)
            {
                SetProjectionParams(m_host, projection, texture, fov, focus, amb);
                return;
            }

            if (linknum < 0 || linknum > m_host.ParentGroup.PrimCount)
                return;

            if(linknum < 2 && m_host.LinkNum < 2)
            {
                SetProjectionParams(m_host, projection, texture, fov, focus, amb);
                return;
            }

            SceneObjectPart obj = m_host.ParentGroup.GetLinkNumPart(linknum);
            if(obj is not null)
                SetProjectionParams(obj, projection, texture, fov, focus, amb);
        }

        /// <summary>
        /// Set parameters for light projection with uuid of target prim
        /// </summary>
        public void osSetProjectionParams(LSL_Key prim, LSL_Integer llprojection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb)
        {
            if(UUID.TryParse(prim, out UUID pID) && pID.IsNotZero())
            {
                SceneObjectPart obj = World.GetSceneObjectPart(pID);
                if(obj is not null)
                {
                    if(obj.OwnerID.Equals(m_host.OwnerID))
                        SetProjectionParams(obj, llprojection, texture, fov, focus, amb);
                }
            }
            else
                SetProjectionParams(m_host, llprojection, texture, fov, focus, amb);
        }

        private static void SetProjectionParams(SceneObjectPart obj, LSL_Integer llprojection, LSL_Key texture, LSL_Float fov, LSL_Float focus, LSL_Float amb)
        {
            if(obj is null || obj.IsDeleted || obj.Shape == null)
                return;

            if(llprojection != 0)
            {
                if (!UUID.TryParse(texture, out UUID texID))
                    return;

                obj.Shape.ProjectionEntry = true;
                obj.Shape.ProjectionTextureUUID = texID;
                obj.Shape.ProjectionFOV = Math.Clamp((float)fov, 0, 3.0f);
                obj.Shape.ProjectionFocus = Math.Clamp((float)focus, -20.0f, 20.0f);
                obj.Shape.ProjectionAmbiance = Math.Clamp((float)amb, 0, 1.0f);

                obj.ParentGroup.HasGroupChanged = true;
                obj.ScheduleFullUpdate();
                return;
            }

            if(obj.Shape.ProjectionEntry)
            {
                obj.Shape.ProjectionEntry = false;

                obj.ParentGroup.HasGroupChanged = true;
                obj.ScheduleFullUpdate();
            }
        }

        /// <summary>
        /// Like osGetAgents but returns enough info for a radar
        /// </summary>
        /// <returns>Strided list of the UUID, position and name of each avatar in the region</returns>
        public LSL_List osGetAvatarList()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetAvatarList");

            LSL_List result = new();
            World.ForEachRootScenePresence(delegate (ScenePresence avatar)
            {
                if (avatar is not null && !avatar.IsDeleted && avatar.UUID.NotEqual(m_host.OwnerID))
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

            LSL_List result = new();
            World.ForEachRootScenePresence(delegate (ScenePresence avatar)
            {
                // npcs are not childagents but that is now.
                if (avatar is not null && avatar.IsNPC && !avatar.IsDeleted && !avatar.IsChildAgent && !avatar.IsInTransit)
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
            return new LSL_String(Util.ToDateTime(time.value).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));
        }

        /// <summary>
        /// Invite user to the group this object is set to
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public LSL_Integer osInviteToGroup(LSL_Key agentId)
        {
            CheckThreatLevel(ThreatLevel.VeryLow, "osInviteToGroup");

            // groups module is required
            if (m_groupsModule is null)
                return ScriptBaseClass.FALSE;

            if (!UUID.TryParse(agentId, out UUID agent))
                return ScriptBaseClass.FALSE;

            // object has to be set to a group, but not group owned
            if (m_host.GroupID.IsZero() || m_host.GroupID.Equals(m_host.OwnerID))
                return ScriptBaseClass.FALSE;

            // invited agent has to be present in this scene
            ScenePresence sp = World.GetScenePresence(agent);
            if (sp is null || sp.IsNPC || sp.IsChildAgent || !sp.ControllingClient.IsActive)
                return ScriptBaseClass.FALSE;

            if (sp.ControllingClient.IsGroupMember(m_host.GroupID))
                return 2;

            // object owner needs invite power
            ulong ownerPowers = m_groupsModule.GetFullGroupPowers(m_host.OwnerID, m_host.GroupID);
            if ((ownerPowers & (ulong)GroupPowers.Invite) == 0)
                return ScriptBaseClass.FALSE;

            m_groupsModule.InviteGroup(null, m_host.OwnerID, m_host.GroupID, agent, UUID.Zero);

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

            // groups module is required
            if (m_groupsModule is null)
                return ScriptBaseClass.FALSE;

            if (!UUID.TryParse(agentId, out UUID agent))
                return ScriptBaseClass.FALSE;

            // object has to be set to a group, but not group owned
            if (m_host.GroupID.IsZero() || m_host.GroupID.Equals(m_host.OwnerID))
                return ScriptBaseClass.FALSE;

            // object owner needs eject power
            ulong ownerPowers = m_groupsModule.GetFullGroupPowers(m_host.OwnerID, m_host.GroupID);
            if ((ownerPowers & (ulong)GroupPowers.Eject) == 0)
                return ScriptBaseClass.FALSE;

            m_groupsModule.EjectGroupMember(null, m_host.OwnerID, m_host.GroupID, agent);

            return ScriptBaseClass.TRUE;
        }

        /// <summary>
        /// Sets terrain texture for legacy viewers and map
        /// </summary>
        /// <param name="level"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        public void osSetTerrainTexture(int level, LSL_Key texture)
        {
            if (level < 0 || level > 3)
                return;
            IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
            if(estate == null)
                return;
            if (!UUID.TryParse(texture, out UUID textureID))
                return;

            if (!World.Permissions.IsGod(m_host.OwnerID))
                CheckThreatLevel(ThreatLevel.High, "osSetTerrainTexture");

            estate.setEstateTerrainBaseTexture(level, textureID);
        }

        /// Sets terrain texture for legacy viewers and map
        /// if types is 0, sets textures for older viewers and map
        /// if types is 1 sets materials or textures for new viewers
        /// if types is 2 sets textures for both kinds
        /// </summary>
        /// <param name="textures"></param>
        /// <param name="legacy"></param>
        /// <returns></returns>

        private static readonly int[] PBRAndTextureTypes = [(int)AssetType.Texture, (int)AssetType.Material];
        public void osSetTerrainTextures(LSL_List textures, LSL_Integer ltypes)
        {
            IEstateModule estateModule = World.RequestModuleInterface<IEstateModule>();
            if (estateModule == null)
                return;

            if (!World.Permissions.IsGod(m_host.OwnerID))
                CheckThreatLevel(ThreatLevel.High, "osSetTerrainTexture");

            if(textures.Length != 4)
            {
                OSSLShoutError($"osSetTerrainTextures first argument is a list of keys or names that must have 4 elements");
                return;
            }

            int types = ltypes.value;
            if( types < 0 || types > 2)
            {
                OSSLShoutError($"osSetTerrainTextures second argument must be >=0 and <= 2");
                return;
            }

            List<UUID> ids = new(4);
            bool hasChanges = false;
            for(int i = 0; i < textures.Length; i++)
            {
                string u = textures.GetStrictLSLStringItem(i);
                if(string.IsNullOrEmpty(u))
                    ids.Add(UUID.Zero);
                else
                {
                    if (!UUID.TryParse(u, out UUID id))
                    {
                        TaskInventoryItem item = types == 1 ? 
                            m_host.Inventory.GetInventoryItem(u, PBRAndTextureTypes) : 
                            m_host.Inventory.GetInventoryItem(u,(int)AssetType.Texture);
                        if (item == null)
                        {
                            OSSLShoutError($"Invalid key or asset type in osSetTerrainTextures texture {i}");
                            return;
                        }
                        id = item.AssetID;
                    }
                    ids.Add(id);
                    if(!hasChanges && id.IsNotZero())
                        hasChanges = true;
                }
            }

            if(hasChanges)
                estateModule.SetEstateTerrainTextures(ids, types);
        }

        /// <summary>
        /// Sets terrain textures heights
        /// </summary>
        /// <param name="corner"></param>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <returns></returns>
        public void osSetTerrainTextureHeight(int corner, double low, double high)
        {
            if (corner < 0 || corner > 3)
                return;

            CheckThreatLevel(ThreatLevel.High, "osSetTerrainTextureHeight");

            if (World.Permissions.IsGod(m_host.OwnerID))
            {
                IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
                estate?.setEstateTerrainTextureHeights(corner, (float)low, (float)high);
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

            if (!UUID.TryParse(rawAvatarId, out UUID avatarId))
                return;

            ForceAttachToAvatarFromInventory(avatarId, itemName, attachmentPoint);
        }

        public void ForceAttachToAvatarFromInventory(UUID avatarId, string itemName, int attachmentPoint)
        {
            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;
            if (attachmentsModule is null)
                return;

            InitLSL();

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);
            if (item is null)
            {
                m_LSL_Api.llSay(0, $"Could not find object '{itemName}'");
                throw new Exception($"The inventory item '{itemName}' could not be found");
            }

            if (item.InvType != (int)InventoryType.Object)
            {
                m_LSL_Api.llSay(0, $"Unable to attach, item '{itemName}' is not an object.");
                throw new Exception($"The inventory item '{itemName}' is not an object");
            }

            if ((item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) != 0)
            {
                m_LSL_Api.llSay(0, $"Unable to attach coalesced object, item '{itemName}'");
                throw new Exception($"The inventory item '{itemName}' is a coalesced object");
            }

            ScenePresence sp = World.GetScenePresence(avatarId);
            if (sp is null)
                return;

            InventoryItemBase newItem = World.MoveTaskInventoryItem(sp.UUID, UUID.Zero, m_host, item.ItemID, out string message);

            if (newItem is null)
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

        private static bool listObjToInt(object p, out int i)
        {
            try
            {
                if (p is LSL_Integer lpi)
                    i = lpi.value;
                else if (p is int pi)
                    i = pi;
                else if (p is uint upi)
                    i = (int)upi;
                else if (p is string ps)
                    return int.TryParse(ps, out i);
                else if (p is LSL_String ps1)
                    return int.TryParse(ps1.m_string, out i);
                else
                {
                    i = 0;
                    return false;
                }
                return true;
            }
            catch
            {
                i = 0;
                return false;
            }
        }

        public LSL_List osGetNumberOfAttachments(LSL_Key avatar, LSL_List attachmentPoints)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetNumberOfAttachments");

            LSL_List resp = new();

            if (attachmentPoints.Length >= 1 && UUID.TryParse(avatar.ToString(), out UUID targetUUID) && World.TryGetScenePresence(targetUUID, out ScenePresence target))
            {
                foreach (object point in attachmentPoints.Data)
                {
                    if(listObjToInt(point, out int ipoint))
                    {
                        resp.Add(new LSL_Integer(ipoint));
                        if (ipoint <= 0)
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
            }

            return resp;
        }

        public void osMessageAttachments(LSL_Key avatar, string message, LSL_List attachmentPoints, int options)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osMessageAttachments");

            if (!UUID.TryParse(avatar.ToString(), out UUID targetUUID) || targetUUID.IsZero())
                return;

            if (!World.TryGetScenePresence(targetUUID, out ScenePresence target))
                return;
            if (target.IsDeleted || target.IsInTransit)
               return;
            if(target.GetAttachmentsCount() == 0)
                return;

            bool invertPoints = (options & ScriptBaseClass.OS_ATTACH_MSG_INVERT_POINTS) != 0;
            bool msgAll = false;

            List<int> aps;
            if(attachmentPoints.Length > 0)
            {
                aps = new List<int>(attachmentPoints.Length);
                foreach (object point in attachmentPoints.Data)
                {
                    if (!listObjToInt(point, out int ipoint))
                        return;
                    if(ipoint == ScriptBaseClass.OS_ATTACH_MSG_ALL)
                    {
                        if(invertPoints)
                            return;
                        msgAll = true;
                        break;
                    }
                    else if(ipoint > 0)
                        aps.Add(ipoint);
                }
            }
            else
                aps = new List<int>();

            if(aps.Count == 0)
            {
                if (!invertPoints && !msgAll)
                    return;
                msgAll = true;
                invertPoints = false;
            }

            List<SceneObjectGroup> attachments;
            if (msgAll || invertPoints)
            {
                attachments = target.GetAttachments();
            }
            else
            {
                attachments = new List<SceneObjectGroup>();
                foreach (int point in aps)
                    attachments.AddRange(target.GetAttachments((uint)point));
            }

            // if we have no attachments at this point, exit now
            if (attachments.Count == 0)
                return;

            bool optionObjCreator = (options & ScriptBaseClass.OS_ATTACH_MSG_OBJECT_CREATOR) != 0;
            bool optionScriptCreator = (options & ScriptBaseClass.OS_ATTACH_MSG_SCRIPT_CREATOR) != 0;

            UUID hostCreatorID = m_host.CreatorID;
            UUID itemCreatorID = m_item.CreatorID;

            //int penalty = 100;
            foreach (SceneObjectGroup sog in attachments)
            {
                if(sog.IsDeleted || sog.inTransit || ((sog.ScriptEvents & scriptEvents.dataserver) == 0))
                    continue;

                if (invertPoints && aps.Contains((int)sog.AttachmentPoint))
                    continue;

                UUID CreatorID = sog.RootPart.CreatorID;
                if (optionObjCreator && CreatorID.NotEqual(hostCreatorID))
                    continue;

                if (optionScriptCreator && CreatorID.NotEqual(itemCreatorID))
                    continue;

                SceneObjectPart[] parts = sog.Parts;
                foreach(SceneObjectPart p in parts)
                {
                    if((p.ScriptEvents & scriptEvents.dataserver) != 0)
                    {
                        MessageObject(p.UUID, message);
                        //penalty += 10;
                    }
                }
            }
            //ScriptSleep(penalty);
        }

        #endregion

        /// <summary>
        /// Checks if thing is a UUID.
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>1 if thing is a valid UUID, 0 otherwise</returns>
        public LSL_Integer osIsUUID(string thing)
        {
            return UUID.TryParse(thing, out _) ? 1 : 0;
        }

        /// <summary>
        /// Wraps to Math.Min()
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public LSL_Float osMin(double a, double b)
        {
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
            return Math.Max(a, b);
        }

        public LSL_Key osGetRezzingObject()
        {
            CheckThreatLevel(ThreatLevel.None, "osGetRezzingObject");

            UUID rezID = m_host.ParentGroup.RezzerID;
            if(rezID.IsZero() || m_host.ParentGroup.Scene.GetScenePresence(rezID) is not null)
                return LSL_Key.NullKey;
            return new LSL_Key(rezID.ToString());
        }

        /// <summary>
        /// Sets the response type for an HTTP request/response
        /// </summary>
        /// <returns></returns>
        public void osSetContentType(LSL_Key id, string type)
        {
            CheckThreatLevel(ThreatLevel.Severe, "osSetContentType");

            m_UrlModule?.HttpContentType(new UUID(id),type);
        }

        /// Shout an error if the object owner did not grant the script the specified permissions.
        /// </summary>
        /// <param name="perms"></param>
        /// <returns>boolean indicating whether an error was shouted.</returns>
        protected bool ShoutErrorOnLackingOwnerPerms(int perms, string errorPrefix)
        {
            if ((m_item.PermsMask & perms) == 0)
            {
                OSSLShoutError($"{errorPrefix}. Permissions not granted.");
                return true;
            }
            if (m_item.PermsGranter.NotEqual(m_host.OwnerID))
            {
                OSSLShoutError($"{errorPrefix}. Permissions not granted to owner.");
                return true;
            }

            return false;
        }

        protected void DropAttachment(bool checkPerms)
        {
            if (checkPerms && ShoutErrorOnLackingOwnerPerms(ScriptBaseClass.PERMISSION_ATTACH, "Cannot drop attachment"))
                return;

            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;
            if (attachmentsModule is null)
                return;

            ScenePresence sp = m_host.ParentGroup.Scene.GetScenePresence(m_host.ParentGroup.OwnerID);
            if (sp is not null)
                attachmentsModule.DetachSingleAttachmentToGround(sp, m_host.ParentGroup.LocalId);
        }

        protected void DropAttachmentAt(bool checkPerms, LSL_Vector pos, LSL_Rotation rot)
        {
            if (checkPerms && ShoutErrorOnLackingOwnerPerms(ScriptBaseClass.PERMISSION_ATTACH, "Cannot drop attachment"))
                return;

            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;
            if (attachmentsModule is null)
                return;

            ScenePresence sp = m_host.ParentGroup.Scene.GetScenePresence(m_host.ParentGroup.OwnerID);
            if (sp is not null)
                attachmentsModule.DetachSingleAttachmentToGround(sp, m_host.ParentGroup.LocalId, pos, rot);
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
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if(wComm is null)
                return -1;

            if(!UUID.TryParse(ID, out UUID keyID))
                return -1;

            // if we want the name to be used as a regular expression, ensure it is valid first.
            if ((regexBitfield & ScriptBaseClass.OS_LISTEN_REGEX_NAME) == ScriptBaseClass.OS_LISTEN_REGEX_NAME)
            {
                try
                {
                    Regex.IsMatch("", name);
                }
                catch
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
                catch
                {
                    OSSLShoutError("Message regex is invalid.");
                    return -1;
                }
            }

            return wComm.Listen(m_item.ItemID, m_host.UUID,
                        channelID, name, keyID, msg, regexBitfield);
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

        public LSL_Key osRequestURL(LSL_List options)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osRequestURL");

            Hashtable opts = new();
            for (int i = 0 ; i < options.Length ; i++)
            {
                object opt = options.Data[i];
                if (opt.ToString() == "allowXss")
                    opts["allowXss"] = true;
            }

            if (m_UrlModule is not null)
                return m_UrlModule.RequestURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID, opts).ToString();

            return LSL_Key.NullKey;
        }

        public LSL_Key osRequestSecureURL(LSL_List options)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osRequestSecureURL");

            Hashtable opts = new();
            for (int i = 0 ; i < options.Length ; i++)
            {
                object opt = options.Data[i];
                if (opt.ToString() == "allowXss")
                    opts["allowXss"] = true;
            }

            if (m_UrlModule != null)
                return m_UrlModule.RequestSecureURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID, opts).ToString();
            return LSL_Key.NullKey;
        }

        public void osCollisionSound(string impact_sound, double impact_volume)
        {
            CheckThreatLevel();

            if(impact_sound.Length == 0)
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
            if(soundId.IsZero())
                m_host.CollisionSoundType = -1;
            else
            {
                m_host.CollisionSound = soundId;
                m_host.CollisionSoundVolume = (float)impact_volume;
                m_host.CollisionSoundType = 1;
            }

            m_host.aggregateScriptEvents();
        }

        // still not very usefull, detector is lost on rez, restarts, etc
        public void osVolumeDetect(int detect)
        {
            CheckThreatLevel();

            if (m_host.ParentGroup is null || m_host.ParentGroup.IsDeleted || m_host.ParentGroup.IsAttachment)
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

            LSL_List result = new();

            SceneObjectGroup sog = m_host.ParentGroup;
            if (sog is null || sog.IsDeleted)
                return result;

            sog.GetInertiaData(out float TotalMass, out Vector3 CenterOfMass, out Vector3 Inertia, out Vector4 aux );
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
            if(sog is null || sog.IsDeleted)
                return;

            if(mass < 0 || principalInertiaScaled.x < 0 || principalInertiaScaled.y < 0 || principalInertiaScaled.z < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new((float)centerOfMass.x, (float)centerOfMass.y, (float)centerOfMass.z);
            float m = (float)mass;

            Vector3 Inertia = new(
                m * (float)principalInertiaScaled.x,
                m * (float)principalInertiaScaled.y,
                m * (float)principalInertiaScaled.z);

            Vector4 rot = new((float)lslrot.x, (float)lslrot.y, (float)lslrot.y, (float)lslrot.s);
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
            if(sog is null || sog.IsDeleted)
                return;

            if(mass < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new((float)centerOfMass.x, (float)centerOfMass.y, (float)centerOfMass.z);
            float lx = (float)boxSize.x;
            float ly = (float)boxSize.y;
            float lz = (float)boxSize.z;
            float m = (float)mass;
            float t = m / 12.0f;

            Vector3 Inertia = new( t * (ly*ly + lz*lz),
                                   t * (lx*lx + lz*lz),
                                   t * (lx*lx + ly*ly));

            Vector4 rot = new((float)lslrot.x, (float)lslrot.y, (float)lslrot.z, (float)lslrot.s);
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
            if(sog is null || sog.IsDeleted)
                return;

            if(mass < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new((float)centerOfMass.x, (float)centerOfMass.y, (float)centerOfMass.z);
            float r = (float)radius;
            float m = (float)mass;
            float t = 0.4f * m * r * r;

            Vector3 Inertia = new(t, t, t);
            sog.SetInertiaData(m, CenterOfMass, Inertia, new Vector4(0f, 0f, 0f,1.0f));
        }

        /// <summary>
        /// set inertial data as a cylinder
        /// replaces the automatic calculation of mass, center of mass and inertia
        ///
        /// </summary>
        /// <param name="Mass">total mass of linkset</param>
        /// <param name="radius">radius of the cylinder</param>
        /// <param name="length">length of the cylinder</param>
        /// <param name="centerOfMass">location of center of mass relative to root prim in local coords</param>
        /// <param name="lslrot">rotation of the cylinder, and so inertia, relative to local axis</param>
        /// <remarks>
        /// cylinder axis aligned with Z axis. For other orientations provide the rotation.
        /// </remarks>
        public void osSetInertiaAsCylinder(LSL_Float mass,  LSL_Float radius, LSL_Float length, LSL_Vector centerOfMass, LSL_Rotation lslrot)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog is null || sog.IsDeleted)
                return;

            if(mass < 0)
                return;

            // need more checks

            Vector3 CenterOfMass = new((float)centerOfMass.x, (float)centerOfMass.y, (float)centerOfMass.z);
            float m = (float)mass;
            float r = (float)radius;
            r *= r;

            float t = (float)length;
            t *= t;
            t += 3.0f * r;
            t *= 8.333333e-2f * m;

            Vector3 Inertia = new(t, t, 0.5f * m * r);

            Vector4 rot = new((float)lslrot.x, (float)lslrot.y, (float)lslrot.z, (float)lslrot.s);
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
            if(sog is null || sog.IsDeleted)
                return;

            sog.SetInertiaData(-1, Vector3.Zero, Vector3.Zero, Vector4.Zero );
        }

        private bool checkAllowObjectTPbyLandOwner(Vector3 pos)
        {
            ILandObject land = World.LandChannel.GetLandObject(pos);
            if(land is null)
                return true;

            LandData landdata = land.LandData;
            if(landdata is null)
                return true;

            UUID hostOwner = m_host.OwnerID;
            if(landdata.OwnerID.Equals(hostOwner))
                return true;

            EstateSettings es = World.RegionInfo.EstateSettings;
            if(es is not null && es.IsEstateManagerOrOwner(hostOwner))
                return true;

            if(!landdata.IsGroupOwned)
                return false;

            UUID landGroup = landdata.GroupID;
            if(landGroup.IsZero())
                return false;

            if(landGroup.Equals(m_host.GroupID))
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

            if (!UUID.TryParse(objectUUID, out UUID objUUID))
            {
                OSSLShoutError("osTeleportObject() invalid object Key");
                return -1;
            }

            SceneObjectGroup sog = World.GetSceneObjectGroup(objUUID);
            if(sog is null || sog.IsDeleted || sog.inTransit)
                return -1;

            if(sog.OwnerID.NotEqual(m_host.OwnerID))
            {
                Vector3 pos = sog.AbsolutePosition;
                if(!checkAllowObjectTPbyLandOwner(pos))
                    return -1;
            }

            return sog.TeleportObject(m_host.ParentGroup.UUID, targetPos, rotation, flags);
            // a delay here may break vehicles
        }

        public LSL_Integer osGetLinkNumber(LSL_String name)
        {
            CheckThreatLevel();

            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog is null || sog.IsDeleted)
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
            SceneObjectPart sop = GetSingleLinkPart(linknum);
            sop?.AdjustSoundGain(volume);
        }

        public void osSetSoundRadius(LSL_Integer linknum, LSL_Float radius)
        {
            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is not null)
                sop.SoundRadius = radius;
        }

        public void osPlaySound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsZero())
                return;

            // send the sound, once, to all clients in range
            m_SoundModule.SendSound(sop, soundID, volume, false, 0, false, false);
        }

        public void osLoopSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsZero())
                return;

            m_SoundModule.LoopSound(sop, soundID, volume, false, false);
        }

        public void osLoopSoundMaster(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsZero())
                return;

            m_SoundModule.LoopSound(sop, soundID, volume, true, false);
        }

        public void osLoopSoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsNotZero())
                m_SoundModule.LoopSound(sop, soundID, volume, false, true);
        }

        public void osPlaySoundSlave(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsNotZero())
                m_SoundModule.SendSound(sop, soundID, volume, false, 0, true, false);
        }

        public void osTriggerSound(LSL_Integer linknum, LSL_String sound, LSL_Float volume)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsNotZero())
                m_SoundModule.SendSound(sop, soundID, volume, true, 0, false, false);
        }

       public void osTriggerSoundLimited(LSL_Integer linknum, LSL_String sound, LSL_Float volume,
                 LSL_Vector top_north_east, LSL_Vector bottom_south_west)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsNotZero())
                m_SoundModule.TriggerSoundLimited(sop.UUID, soundID, volume,
                        bottom_south_west, top_north_east);
        }

        public void osStopSound(LSL_Integer linknum)
        {
            if (m_SoundModule is null)
                return;

            InitLSL();
            List<SceneObjectPart> sops = m_LSL_Api.GetLinkParts(linknum);
            if(sops is null || sops.Count == 0)
                return;
            for(int i = 0; i < sops.Count; ++i)
                m_SoundModule.StopSound(sops[i]);
        }

        public void osPreloadSound(LSL_Integer linknum, LSL_String sound)
        {
            if (m_SoundModule is null)
                return;

            SceneObjectPart sop = GetSingleLinkPart(linknum);
            if(sop is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(sop, m_host, sound, AssetType.Sound);
            if (soundID.IsZero())
                return;

            m_SoundModule.PreloadSound(sop, soundID);
            ScriptSleep(1000);
        }

        // get only one part
        private SceneObjectPart GetSingleLinkPart(int linkType)
        {
            if (m_host.ParentGroup is null || m_host.ParentGroup.IsDeleted)
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
            CheckThreatLevel(ThreatLevel.Moderate, "osDetectedCountry");

            if (World.UserAccountService is null)
                return LSL_String.Empty;
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams is null)
                return LSL_String.Empty;
            UUID key = detectedParams.Key;
            if (key.IsZero())
                return LSL_String.Empty;
            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
            return account.UserCountry;
        }

        public LSL_String osGetAgentCountry(LSL_Key id)
        {
            CheckThreatLevel(ThreatLevel.Moderate, "osGetAgentCountry");

            if (World.UserAccountService == null)
                return LSL_String.Empty;

            if (!UUID.TryParse(id, out UUID key))
                return LSL_String.Empty;
            if (key.IsZero())
                return LSL_String.Empty;

            //if owner is not god, target must be in region, or nearby regions
            if (!World.Permissions.IsGod(m_host.OwnerID))
            {
                ScenePresence sp = World.GetScenePresence(key);
                if(sp is null)
                    return LSL_String.Empty;
            }

            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, key);
            return account.UserCountry;
        }

        public LSL_String osStringSubString(LSL_String src, LSL_Integer offset)
        {
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return LSL_String.Empty;
            if (offset >= src.Length)
                return LSL_String.Empty;
            if (offset <= 0)
                return src;
            return src.m_string[(offset.value)..];
        }

        public LSL_String osStringSubString(LSL_String src, LSL_Integer offset, LSL_Integer length)
        {
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return LSL_String.Empty;
            if (length <= 0 || offset >= src.Length)
                return LSL_String.Empty;
            if (offset <= 0)
            {
                if(length == src.Length)
                    return src;
                offset = 0;
            }
            if (length > src.Length - offset)
                length = src.Length - offset;
            return src.m_string.Substring(offset, length);
        }

        public LSL_Integer osStringStartsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return 0;
            if (string.IsNullOrEmpty(value))
                return 0;

            return src.m_string.StartsWith(value, ignorecase != 0, Culture.GetDefaultCurrentCulture()) ? 1 : 0;
        }

        public LSL_Integer osStringEndsWith(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return 0;
            if (string.IsNullOrEmpty(value))
                return 0;

            return src.m_string.EndsWith(value, ignorecase != 0, Culture.GetDefaultCurrentCulture()) ? 1 : 0;
        }

        public LSL_Integer osStringIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return -1;
            if (string.IsNullOrEmpty(value))
                return -1;

            if (ignorecase == 0)
                return src.m_string.IndexOf(value, StringComparison.Ordinal);
            return src.m_string.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        }

        public LSL_Integer osStringIndexOf(LSL_String src, LSL_String value,
            LSL_Integer offset, LSL_Integer count, LSL_Integer ignorecase)
        {
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
                return src.m_string.IndexOf(value, offset, count, StringComparison.Ordinal);
            return src.m_string.IndexOf(value, offset, count, StringComparison.OrdinalIgnoreCase);
        }

        public LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value, LSL_Integer ignorecase)
        {
            CheckThreatLevel();

            if (string.IsNullOrEmpty(src))
                return -1;
            if (string.IsNullOrEmpty(value))
                return -1;

            if (ignorecase == 0)
                return src.m_string.LastIndexOf(value, StringComparison.Ordinal);
            return src.m_string.LastIndexOf(value, StringComparison.OrdinalIgnoreCase);
        }

        public LSL_Integer osStringLastIndexOf(LSL_String src, LSL_String value,
            LSL_Integer offset, LSL_Integer count, LSL_Integer ignorecase)
        {
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
                return src.m_string.LastIndexOf(value, offset, count, StringComparison.Ordinal);
            return src.m_string.LastIndexOf(value, offset, count, StringComparison.OrdinalIgnoreCase);
        }

        public LSL_String osStringRemove(LSL_String src, LSL_Integer offset, LSL_Integer count)
        {
            if (string.IsNullOrEmpty(src))
                return LSL_String.Empty;
            if (offset >= src.Length)
                return LSL_String.Empty;
            if (offset < 0)
                offset = 0;

            if (count <= 0)
                count = src.Length - offset;
            else if (count > src.Length - offset)
                count = src.Length - offset;

            if (count >= src.Length)
                return LSL_String.Empty;


            return src.m_string.Remove(offset, count);
        }

        public LSL_String osStringReplace(LSL_String src, LSL_String oldvalue, LSL_String newvalue)
        {
            if (string.IsNullOrEmpty(src))
                return LSL_String.Empty;
            if (string.IsNullOrEmpty(oldvalue))
                return LSL_String.Empty;
            if (string.IsNullOrEmpty(newvalue))
                newvalue = null;

            return src.m_string.Replace(oldvalue, newvalue);
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
            TaskInventoryItem item = UUID.TryParse(itemNameorid, out UUID itemID) ?
                                m_host.Inventory.GetInventoryItem(itemID) :
                                m_host.Inventory.GetInventoryItem(itemNameorid);

            if (item is null)
                return LSL_String.NullKey;

            UUID id = item.LastOwnerID.IsNotZero() ? item.LastOwnerID : item.OwnerID;

            return new LSL_Key(id.ToString());
        }

        public LSL_Key osGetInventoryItemKey(LSL_String name)
        {
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);
            if (item is null)
                return LSL_String.NullKey;

            if ((item.CurrentPermissions
                 & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
            {
                return new LSL_Key(item.ItemID.ToString());
            }

            return LSL_String.NullKey;
        }

        public LSL_String osGetInventoryName(LSL_Key itemId)
        {
            TaskInventoryItem item = null;
            if (UUID.TryParse(itemId, out UUID itemID))
                item = m_host.Inventory.GetInventoryItem(itemID);

            return (item is null) ? LSL_String.Empty : item.Name;
        }

        public LSL_String osGetLinkInventoryName(LSL_Integer linkNumber, LSL_Key itemId)
        {
            TaskInventoryItem item = null;
            if (UUID.TryParse(itemId, out UUID itemID))
            {
                SceneObjectPart part = GetSingleLinkPart(linkNumber);
                if(part == null)
                    return LSL_String.Empty;

                item = part.Inventory.GetInventoryItem(itemID);
            }
            return (item is null) ? LSL_String.Empty : item.Name;
        }

        public LSL_String osGetInventoryDesc(LSL_String itemNameorid)
        {
            TaskInventoryItem item = UUID.TryParse(itemNameorid, out UUID itemID) ?
                            m_host.Inventory.GetInventoryItem(itemID) :
                            m_host.Inventory.GetInventoryItem(itemNameorid);

            return (item == null) ? LSL_String.Empty : item.Description;
        }

        public LSL_String osGetLinkInventoryDesc(LSL_Integer linkNumber, LSL_String itemNameorid)
        {
            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return LSL_String.Empty;

            TaskInventoryItem item = UUID.TryParse(itemNameorid, out UUID itemID) ?
                                    part.Inventory.GetInventoryItem(itemID) :
                                    part.Inventory.GetInventoryItem(itemNameorid);

            return (item == null) ? LSL_String.Empty : item.Description;
        }

        public LSL_Key osGetLinkInventoryKey(LSL_Integer linkNumber, LSL_String name, LSL_Integer type)
        {
            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return LSL_String.NullKey;
            
            TaskInventoryItem item = part.Inventory.GetInventoryItem(name, type);
            if (item is null)
                return LSL_String.NullKey;

            if ((item.CurrentPermissions
                 & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
            {
                return new LSL_Key(item.AssetID.ToString());
            }

            return LSL_String.NullKey;
        }

        public LSL_List osGetLinkInventoryKeys(LSL_Integer linkNumber, LSL_Integer type)
        {
            LSL_List ret = new();
            
            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return ret;

            part.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in part.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1 &&
                                    (inv.Value.CurrentPermissions
                                    & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    ret.Add(inv.Value.AssetID.ToString());
            }

            part.TaskInventory.LockItemsForRead(false);
            return ret;
        }

        public LSL_Key osGetLinkInventoryItemKey(LSL_Integer linkNumber, LSL_String name)
        {
            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return LSL_String.NullKey;
            
            TaskInventoryItem item = part.Inventory.GetInventoryItem(name);
            if (item is null)
                return LSL_String.NullKey;

            if ((item.CurrentPermissions
                 & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
            {
                return new LSL_Key(item.ItemID.ToString());
            }

            return LSL_String.NullKey;
        }

        public LSL_List osGetInventoryItemKeys(LSL_Integer type)
        {
            LSL_List ret = new();

            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if ((inv.Value.Type == type || type == -1) &&
                                    (inv.Value.CurrentPermissions
                                    & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    ret.Add(inv.Value.ItemID.ToString());
            }
            m_host.TaskInventory.LockItemsForRead(false);
            return ret;
        }

        public LSL_List osGetLinkInventoryItemKeys(LSL_Integer linkNumber, LSL_Integer type)
        {
            LSL_List ret = new();
            
            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return ret;

            part.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in part.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1 &&
                                    (inv.Value.CurrentPermissions
                                    & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    ret.Add(inv.Value.ItemID.ToString());
            }

            part.TaskInventory.LockItemsForRead(false);
            return ret;
        }

        public LSL_List osGetInventoryNames(LSL_Integer type)
        {
            LSL_List ret = new();

            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
                    ret.Add(inv.Value.Name);
            }

            m_host.TaskInventory.LockItemsForRead(false);
            return ret;
        }

        public LSL_List osGetLinkInventoryNames(LSL_Integer linkNumber, LSL_Integer type)
        {
            LSL_List ret = new();

            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return ret;

            part.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in part.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
                    ret.Add(inv.Value.Name);
            }

            part.TaskInventory.LockItemsForRead(false);
            return ret;
        }

        public void osRemoveLinkInventory(LSL_Integer linkNumber, LSL_String name)
        {
            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return;

            TaskInventoryItem item = part.Inventory.GetInventoryItem(name);
            if (item == null)
                return;

            if (item.ItemID == m_item.ItemID)
                throw new ScriptDeleteException();
            else
                part.Inventory.RemoveInventoryItem(item.ItemID);
        }

        ///<summary>
        /// Give a specified item from a child prim inventory 
        /// to a destination (object or avatar).
        ///</summary>
        ///<param name="linkNumber">The link number of the child prim.</param>
        ///<param name="destination">The UUID of the destination avatar or object.</param>
        ///<param name="inventory">The name of the item to give.</param>
        public void osGiveLinkInventory(LSL_Integer linkNumber, LSL_Key destination, LSL_String inventory)
        {
            if (!UUID.TryParse(destination, out UUID destId) || destId.IsZero())
                return;

            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return;

            TaskInventoryItem item = part.Inventory.GetInventoryItem(inventory);
            if (item is null)
                return;

            if (World.TryGetSceneObjectPart(destId, out _))
            {
                // destination is an object
                World.MoveTaskInventoryItem(destId, part, item.ItemID);
            }
            else
            {
                if(!World.TryGetScenePresence(destId, out ScenePresence _))
                {
                    UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, destId);
                    if (account == null)
                    {
                        GridUserInfo info = World.GridUserService.GetGridUserInfo(destId.ToString());
                        if(info == null || info.Online == false)
                            return;
                    }
                }

                // destination is an avatar
                InventoryItemBase agentItem = World.MoveTaskInventoryItem(destId, UUID.Zero, part, item.ItemID, out string message);
                if (agentItem is null)
                {
                    m_LSL_Api.llSay(0, message);
                    return;
                }

                byte[] bucket = new byte[1];
                bucket[0] = (byte)item.Type;

                GridInstantMessage msg = new GridInstantMessage(World,
                        m_host.OwnerID, m_host.Name, destId,
                        (byte)InstantMessageDialog.TaskInventoryOffered,
                        m_host.OwnerID.Equals(m_host.GroupID), "'"+item.Name+"'. ("+m_host.Name+" is located at "+
                        World.RegionInfo.RegionName + " "+ m_host.AbsolutePosition.ToString() + ")",
                        agentItem.ID, true, m_host.AbsolutePosition,
                        bucket, true);

                if (World.TryGetScenePresence(destId, out ScenePresence sp))
                    sp.ControllingClient.SendInstantMessage(msg);
                else
                    m_TransferModule?.SendInstantMessage(msg, delegate(bool success) {});

                //This delay should only occur when giving inventory to avatars.
                ScriptSleep(3000);
            }
        }

        public void osGiveLinkInventoryList(LSL_Integer linkNumber, LSL_Key destination, LSL_String folderName, LSL_List inventory)
        {
            if (inventory.Length == 0)
                return;

            SceneObjectPart part = GetSingleLinkPart(linkNumber);
            if(part == null)
                return;
            
            if (!UUID.TryParse(destination, out UUID destID) || destID.IsZero())
                return;

            bool isNotOwner = true;
            if (!World.TryGetSceneObjectPart(destID, out SceneObjectPart destSop))
            {
                if (!World.TryGetScenePresence(destID, out ScenePresence sp))
                {
                    // we could check if it is a grid user and allow the transfer as in older code
                    // but that increases security risk
                    OSSLShoutError("osGiveLinkInventoryList: Unable to give list, destination not found");
                    ScriptSleep(100);
                    return;
                }
                isNotOwner = sp.UUID.NotEqual(m_host.OwnerID);
            }

            List<UUID> itemList = new(inventory.Length);
            foreach (object item in inventory.Data)
            {
                string rawItemString = item.ToString();
                TaskInventoryItem taskItem = (UUID.TryParse(rawItemString, out UUID itemID)) ?
                    part.Inventory.GetInventoryItem(itemID) : part.Inventory.GetInventoryItem(rawItemString);

                if(taskItem is null)
                    continue;

                if ((taskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    continue;

                if (destSop is not null)
                {
                    if(!World.Permissions.CanDoObjectInvToObjectInv(taskItem, m_host, destSop))
                        continue;
                }
                else
                {
                    if(isNotOwner)
                    {
                        if ((taskItem.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                            continue;
                    }
                }

                itemList.Add(taskItem.ItemID);
            }

            if (itemList.Count == 0)
            {
                OSSLShoutError("osGiveLinkInventoryList: Unable to give list, no items found");
                ScriptSleep(100);
                return;
            }

            UUID folderID = m_ScriptEngine.World.MoveTaskInventoryItems(destID, folderName, part, itemList, false);

            if (folderID.IsZero())
            {
                OSSLShoutError("osGiveLinkInventoryList: Unable to give list");
                ScriptSleep(100);
                return;
            }

            if (destSop is not null)
            {
                ScriptSleep(100);
                return;
            }

            if (m_TransferModule != null)
            {
                byte[] bucket = new byte[] { (byte)AssetType.Folder };

                Vector3 pos = m_host.AbsolutePosition;

                GridInstantMessage msg = new(World, m_host.OwnerID, m_host.Name, destID,
                        (byte)InstantMessageDialog.TaskInventoryOffered,
                        m_host.OwnerID.Equals(m_host.GroupID), string.Format("'{0}'", folderName), folderID, false, pos,bucket, false);

                m_TransferModule.SendInstantMessage(msg, delegate(bool success) {});
            }

            ScriptSleep(3000);
        }

        public LSL_Key osGetLastChangedEventKey()
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, 0);
            if (detectedParams is null)
                return string.Empty;
            return detectedParams.Key.ToString();
        }

        // returns PST or PDT wall clock
        public LSL_Float osGetPSTWallclock()
        {
            if(PSTTimeZone is null)
                return DateTime.Now.TimeOfDay.TotalSeconds;

            DateTime time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, PSTTimeZone);
            return time.TimeOfDay.TotalSeconds;
        }

        public LSL_Rotation osSlerp(LSL_Rotation a, LSL_Rotation b, LSL_Float amount)
        {
            if(amount < 0)
                amount= 0;
            else if(amount > 1.0)
                amount = 1.0;
            a.Normalize();
            b.Normalize();

            return LSL_Rotation.Slerp(a, b, amount);
        }

        public LSL_Vector osSlerp(LSL_Vector a, LSL_Vector b, LSL_Float amount)
        {
            if (amount < 0)
                amount = 0;
            else if (amount > 1.0)
                amount = 1.0;

            return LSL_Vector.Slerp(a, b, amount);
        }

        public void osResetAllScripts(LSL_Integer linkset)
        {
            UUID me = m_item.ItemID;
            List<TaskInventoryItem> scripts = new();

            if(linkset != 0)
            {
                SceneObjectGroup sog = m_host.ParentGroup;
                if(sog.inTransit || sog.IsDeleted)
                    return;

                SceneObjectPart[] parts = sog.Parts;
                for (int i = 0; i < parts.Length; i++)
                {
                    scripts.AddRange(parts[i].Inventory.GetInventoryItems(InventoryType.LSL));
                }
            }
            else
                scripts.AddRange(m_host.Inventory.GetInventoryItems(InventoryType.LSL));

            foreach(TaskInventoryItem script in scripts)
            {
                if(script.ItemID.Equals(me))
                    continue;
                m_ScriptEngine.ResetScript(script.ItemID);
            }

            m_UrlModule?.ScriptRemoved(me);

            m_ScriptEngine.ApiResetScript(me);
        }

        public LSL_Integer osIsNotValidNumber(LSL_Float v)
        {
            double d = v.value;
            if (double.IsNaN(d))
                return 1;
            if (double.IsNegativeInfinity(d))
                return 2;
            if (double.IsPositiveInfinity(d))
                return 3;
            return 0;
        }

        public LSL_Vector osGetSitTargetPos()
        {
            return m_host.SitTargetPosition;
        }

        public LSL_Rotation osGetSitTargetRot()
        {
            return m_host.SitTargetOrientation;
        }

        public void osSetSitActiveRange(LSL_Float v)
        {
            float fv = (float)v.value;
            if (fv > 128f)
                fv = 128f;

            if(m_host.SitActiveRange != fv)
            {
                m_host.SitActiveRange = fv;
                m_host.ParentGroup.HasGroupChanged = true;
            }
        }

        public void osSetLinkSitActiveRange(LSL_Integer linkNumber, LSL_Float v)
        {
            InitLSL();
            float fv = (float)v.value;
            if (fv > 128f)
                fv = 128f;

            bool changed = false;

            List<SceneObjectPart> parts = m_LSL_Api.GetLinkParts(linkNumber);
            for(int i = 0; i < parts.Count; ++i)
            {
                SceneObjectPart sop = parts[i];
                if(sop.SitActiveRange != fv)
                {
                    sop.SitActiveRange = fv;
                    changed = true;
                }
            }

            if (changed)
                m_host.ParentGroup.HasGroupChanged = true;
        }

        public LSL_Float osGetSitActiveRange()
        {
            return m_host.SitActiveRange;
        }

        public LSL_Float osGetLinkSitActiveRange(LSL_Integer linkNumber)
        {
            if (linkNumber == ScriptBaseClass.LINK_THIS)
                return m_host.SitActiveRange;
            if (linkNumber < 0)
                return int.MinValue;
            if (linkNumber < 2)
                return m_host.ParentGroup.RootPart.SitActiveRange;
            SceneObjectPart target = m_host.ParentGroup.GetLinkNumPart(linkNumber);
            if (target is null)
                return int.MinValue;
            return target.SitActiveRange;
        }

        public void osSetStandTarget(LSL_Vector v)
        {
            // todo add limits ?
            Vector3 old = m_host.StandOffset;
            m_host.StandOffset = v;
            if(!old.ApproxEquals(v))
                m_host.ParentGroup.HasGroupChanged = true;
        }

        public void osSetLinkStandTarget(LSL_Integer linkNumber, LSL_Vector v)
        {
            // todo add limits ?
            SceneObjectPart target;
            if (linkNumber == ScriptBaseClass.LINK_THIS)
                target = m_host;
            else if (linkNumber < 0)
                return;
            else if (linkNumber < 2)
                target = m_host.ParentGroup.RootPart;
            else
                target = m_host.ParentGroup.GetLinkNumPart(linkNumber);

            if (target is null)
                return;

            Vector3 newv = v;
            if (!newv.ApproxEquals(target.StandOffset))
            {
                target.StandOffset = v;
                m_host.ParentGroup.HasGroupChanged = true;
            }
        }

        public LSL_Vector osGetStandTarget()
        {
            return m_host.StandOffset;
        }

        public LSL_Vector osGetLinkStandTarget(LSL_Integer linkNumber)
        {
            if (linkNumber == ScriptBaseClass.LINK_THIS)
                return m_host.StandOffset;
            if (linkNumber < 0)
                return Vector3.Zero;
            if (linkNumber < 2)
                return m_host.ParentGroup.RootPart.StandOffset;
            SceneObjectPart target = m_host.ParentGroup.GetLinkNumPart(linkNumber);
            if (target is null)
                return Vector3.Zero;
            return target.StandOffset;
        }

        public LSL_Integer osClearObjectAnimations()
        {
            return m_host.ClearObjectAnimations();
        }

        public LSL_Integer osReplaceAgentEnvironment(LSL_Key agentkey, LSL_Integer transition, LSL_String daycycle)
        {
            if(!string.IsNullOrEmpty(CheckThreatLevelTest(ThreatLevel.Moderate, "osReplaceAgentEnvironment")))
                return -2;

            if (!UUID.TryParse(agentkey, out UUID agentid))
                return -4;

            ScenePresence sp = World.GetScenePresence(agentid);
            if(sp is null || sp.IsChildAgent || sp.IsNPC || sp.IsInTransit)
                return -4;

            if(string.IsNullOrEmpty(daycycle) || daycycle == ScriptBaseClass.NULL_KEY)
            {
                sp.Environment = null;
                m_envModule.WindlightRefreshForced(sp, transition);
                return 1;
            }

            UUID envID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, daycycle);
            if (envID.IsZero())
                return -3;

            AssetBase asset = World.AssetService.Get(envID.ToString());
            if(asset is null || asset.Type != (byte)AssetType.Settings)
                return -3;
            // cant use stupid broken asset flags for subtype
            try
            {
                OSD oenv = OSDParser.Deserialize(asset.Data);
                ViewerEnvironment VEnv = m_envModule.GetRegionEnvironment().Clone();
                if(!VEnv.CycleFromOSD(oenv))
                    return -3;
                sp.Environment = VEnv;
                m_envModule.WindlightRefreshForced(sp, transition);
            }
            catch
            {
                sp.Environment = null;
                m_envModule.WindlightRefreshForced(sp, transition);
                return -5;
            }
            return 1;
        }

        public LSL_Integer osReplaceParcelEnvironment(LSL_Integer transition, LSL_String daycycle)
        {
            if (!World.RegionInfo.EstateSettings.AllowEnvironmentOverride)
                return -1;

            ILandObject parcel = World.LandChannel.GetLandObject(m_host.GetWorldPosition().X, m_host.GetWorldPosition().Y);
            if (parcel is null)
                return -2;

            if (!World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, (GroupPowers.AllowEnvironment), true))
                return -3;

            ViewerEnvironment VEnv;
            if (parcel.LandData.Environment is null)
                VEnv = m_envModule.GetRegionEnvironment().Clone();
            else
                VEnv = parcel.LandData.Environment;

            bool changed = false;
            if (!string.IsNullOrEmpty(daycycle) && (daycycle != ScriptBaseClass.NULL_KEY))
            {
                UUID envID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, daycycle);
                if (envID.IsZero())
                    return -4;

                AssetBase asset = World.AssetService.Get(envID.ToString());
                if (asset is null || asset.Type != (byte)AssetType.Settings)
                    return -4;
                // cant use stupid broken asset flags for subtype
                try
                {
                    OSD oenv = OSDParser.Deserialize(asset.Data);
                    if (!VEnv.CycleFromOSD(oenv))
                        return -5;
                    changed = true;
                }
                catch
                {
                    return -5;
                }
            }

            if (changed)
            {
                parcel.StoreEnvironment(VEnv);
                m_envModule.WindlightRefresh(transition, false);
            }

            return 1;
        }

        public LSL_Integer osReplaceRegionEnvironment(LSL_Integer transition, LSL_String daycycle,
            LSL_Float daylen, LSL_Float dayoffset,
            LSL_Float altitude1, LSL_Float altitude2, LSL_Float altitude3)
        {
            if (!World.Permissions.CanIssueEstateCommand(m_host.OwnerID, true))
                return -3;

            ViewerEnvironment VEnv = m_envModule.GetRegionEnvironment().Clone();

            bool changed = false;
            if (!string.IsNullOrEmpty(daycycle) && (daycycle != ScriptBaseClass.NULL_KEY))
            {

                UUID envID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, daycycle);
                if (envID.IsZero())
                    return -4;

                AssetBase asset = World.AssetService.Get(envID.ToString());
                if (asset is null || asset.Type != (byte)AssetType.Settings)
                    return -4;
                // cant use stupid broken asset flags for subtype
                try
                {
                    OSD oenv = OSDParser.Deserialize(asset.Data);
                    if (!VEnv.CycleFromOSD(oenv))
                        return -5;
                    changed = true;
                }
                catch
                {
                    return -5;
                }
            }

            if (daylen >= 4 && daylen <= 24 * 7)
            {
                int ll = VEnv.DayLength;
                VEnv.DayLength = (int)(daylen * 3600f);
                changed |= ll != VEnv.DayLength;
            }

            if (dayoffset >= -11.5 && dayoffset <= 11.5)
            {
                int lo = VEnv.DayLength;
                if (dayoffset <= 0)
                    dayoffset+= 24;
                VEnv.DayOffset = (int)(dayoffset * 3600f);
                changed |= lo != VEnv.DayOffset;
            }

            bool needSort = false;
            if (altitude1 > 0 && altitude1 < 4000 && VEnv.Altitudes[0] != (float)altitude1)
            {
                VEnv.Altitudes[0] = (float)altitude1;
                needSort = true;
            }
            if (altitude2 > 0 && altitude2 < 4000 && VEnv.Altitudes[1] != (float)altitude2)
            {
                VEnv.Altitudes[1] = (float)altitude2;
                needSort = true;
            }
            if (altitude3 > 0 && altitude2 < 4000 && VEnv.Altitudes[2] != (float)altitude3)
            {
                VEnv.Altitudes[2] = (float)altitude3;
                needSort = true;
            }
            if(needSort)
            {
                VEnv.SortAltitudes();
                changed = true;
            }

            if(changed)
            {
                m_envModule.StoreOnRegion(VEnv);
                m_envModule.WindlightRefresh(transition);
            }
            return 1;
        }

        public LSL_Integer osResetEnvironment(LSL_Integer parcelOrRegion, LSL_Integer transition)
        {
            if (parcelOrRegion > 0)
            {
                if (!World.RegionInfo.EstateSettings.AllowEnvironmentOverride)
                    return -1;

                ILandObject parcel = World.LandChannel.GetLandObject(m_host.GetWorldPosition().X, m_host.GetWorldPosition().Y);
                if (parcel is null)
                    return -2;

                if (!World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, (GroupPowers.AllowEnvironment), true))
                    return -3;
                if (parcel.LandData.Environment is null)
                    return 1;

                parcel.StoreEnvironment(null);
                m_envModule.WindlightRefresh(transition, false);
                return 1;
            }

            if (!World.Permissions.CanIssueEstateCommand(m_host.OwnerID, true))
                return -3;

            m_envModule.StoreOnRegion(null);
            m_envModule.WindlightRefresh(transition);
            return 1;
        }

        public void osParticleSystem(LSL_List rules)
        {
            InitLSL();
            m_LSL_Api.SetParticleSystem(m_host, rules, "osParticleSystem", true);
        }

        public void osLinkParticleSystem(LSL_Integer linknumber, LSL_List rules)
        {
            InitLSL();

            List<SceneObjectPart> parts = m_LSL_Api.GetLinkParts(linknumber);
            foreach (SceneObjectPart part in parts)
            {
                m_LSL_Api.SetParticleSystem(part, rules, "osLinkParticleSystem", true);
            }
        }

        public LSL_Integer osNpcLookAt(LSL_Key npckey, LSL_Integer ltype, LSL_Key objkey, LSL_Vector offset)
        {
            if (World.GetNumberOfClients() == 0)
                return 0;

            if (ltype < 0 || ltype > ScriptBaseClass.NPCLOOKAT_CLEAR)
                return -1;

            if (!UUID.TryParse(npckey, out UUID npc))
                return -2;

            ScenePresence npcSP = World.GetScenePresence(npc);
            if((npcSP is null) || !npcSP.IsNPC)
                return -3;

            if (!UUID.TryParse(objkey, out UUID obj))
                return -4;

            if(!obj.IsZero())
            {
                ScenePresence objSP = World.GetScenePresence(obj);
                if(objSP == null)
                {
                    SceneObjectPart objSOP = World.GetSceneObjectPart(obj);
                    if(objSOP == null)
                        return -5;
                }
            }

            byte[] data = new byte[57];
            npc.ToBytes(data, 0);
            obj.ToBytes(data, 16);
            Vector3d vd = new(offset.x, offset.y, offset.z);
            vd.ToBytes(data, 32);
            data[56] = (byte)(int)ltype;

            OpenMetaverse.Packets.ViewerEffectPacket.EffectBlock effect = new()
            {
                AgentID = npc,
                Color = new byte[4],
                Duration = 0,
                ID = UUID.Random(),
                Type = 14,
                TypeData = data
            };

            OpenMetaverse.Packets.ViewerEffectPacket.EffectBlock[] effectblock = new OpenMetaverse.Packets.ViewerEffectPacket.EffectBlock[1];
            effectblock[0] = effect;

            World.ForEachScenePresence(
                 sp =>
                 {
                    if(!sp.IsNPC && !sp.IsDeleted)
                    {
                        sp.ControllingClient.SendViewerEffect(effectblock);
                    }
                 });

            return 0;
        }

        public LSL_Integer osAvatarType(LSL_Key avkey)
        {
            if (!UUID.TryParse(avkey.m_string, out UUID avId))
                return -1;

            ScenePresence av = World.GetScenePresence(avId);
            if (av is null || av.IsDeleted || av.IsChildAgent)
                return 0;

            return av.IsNPC ? 2 : 1;
        }

        public LSL_Integer osAvatarType(LSL_String sFirstName, LSL_String sLastName)
        {
            ScenePresence av = World.GetScenePresence(sFirstName.m_string, sLastName.m_string);
            if (av is null || av.IsDeleted || av.IsChildAgent)
                return 0;

            return av.IsNPC ? 2 : 1;
        }

        public void osListSortInPlace(LSL_List src, LSL_Integer stride, LSL_Integer ascending)
        {
            src.SortInPlace(stride, ascending == 1);
        }

        public void osListSortInPlaceStrided(LSL_List src, LSL_Integer stride, LSL_Integer stride_index, LSL_Integer ascending)
        {
            src.SortInPlace(stride, stride_index, ascending == 1);
        }

        public LSL_List osGetParcelDetails(LSL_Key id, LSL_List param)
        {
            if (!UUID.TryParse(id, out UUID parcelID))
                return new LSL_List(0);

            InitLSL();
            if (m_LSL_Api is null)
                return new LSL_List(0);

            ILandObject parcel = World.LandChannel.GetLandObject(parcelID);
            if (parcel is null)
                return new LSL_List(0);

            return m_LSL_Api.GetParcelDetails(parcel, param);
        }

        public LSL_List osGetParcelIDs()
        {
            //if(!World.Permissions.IsEstateManager(m_host.OwnerID))
            //    return new LSL_List();

            List<ILandObject> parcels = World.LandChannel.AllParcels();
            if (parcels is null || parcels.Count == 0)
                return new LSL_List();
            LSL_List ret = new();
            foreach (ILandObject obj in parcels)
            {
                // some sanity check
                if(obj.GlobalID.IsZero())
                    continue;
                if(obj.LandData is null || obj.LandData.Area == 0)
                    continue;
                ret.Add(new LSL_Key(obj.GlobalID.ToString()));
            }
            return ret;
        }
        public LSL_Key osGetParcelID()
        {
            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            return parcel is null ? ScriptBaseClass.NULL_KEY : new LSL_Key(parcel.GlobalID.ToString());
        }

        // old List2ListStrided for compatibility
        /// <summary>
        /// Elements in the source list starting with 0 and then
        /// every i+stride. If the stride is negative then the scan
        /// Only those elements that are also in the specified
        /// range are included in the result.
        /// </summary>

        public LSL_List osOldList2ListStrided(LSL_List src, int start, int end, int stride)
        {
            LSL_List result = new();
            int[] si = new int[2];
            int[] ei = new int[2];
            bool twopass = false;

            //  First step is always to deal with negative indices

            if (start < 0)
                start += src.Length;
            if (end   < 0)
                end += src.Length;

            //  Out of bounds indices are OK, just trim them
            //  accordingly

            if (start > src.Length)
                start = src.Length;

            if (end > src.Length)
                end = src.Length;

            if (stride == 0)
                stride = 1;

            //  There may be one or two ranges to be considered

            if (start != end)
            {
                if (start <= end)
                {
                   si[0] = start;
                   ei[0] = end;
                }
                else
                {
                   si[1] = start;
                   ei[1] = src.Length;
                   si[0] = 0;
                   ei[0] = end;
                   twopass = true;
                }

                //  The scan always starts from the beginning of the
                //  source list, but members are only selected if they
                //  fall within the specified sub-range. The specified
                //  range values are inclusive.
                //  A negative stride reverses the direction of the
                //  scan producing an inverted list as a result.

                if (stride > 0)
                {
                    for (int i = 0; i < src.Length; i += stride)
                    {
                        if (i<=ei[0] && i>=si[0])
                            result.Add(src.Data[i]);
                        if (twopass && i>=si[1] && i<=ei[1])
                            result.Add(src.Data[i]);
                    }
                }
                else if (stride < 0)
                {
                    for (int i = src.Length - 1; i >= 0; i += stride)
                    {
                        if (i <= ei[0] && i >= si[0])
                            result.Add(src.Data[i]);
                        if (twopass && i >= si[1] && i <= ei[1])
                            result.Add(src.Data[i]);
                    }
                }
            }
            else
            {
                if (start%stride == 0)
                {
                    result.Add(src.Data[start]);
                }
            }

            return result;
        }

        public LSL_Integer osGetPrimCount()
        {
            return m_host.ParentGroup.PrimCount;
        }

        public LSL_Integer osGetPrimCount(LSL_Key object_id)
        {
            if (!UUID.TryParse(object_id.m_string, out UUID id) || id.IsZero())
                return 0;

            return World.TryGetSceneObjectPart(id, out SceneObjectPart part) ? part.ParentGroup.PrimCount : 0;
        }

        public LSL_Integer osGetSittingAvatarsCount()
        {
            return m_host.ParentGroup.GetSittingAvatarsCount();
        }

        public LSL_Integer osGetSittingAvatarsCount(LSL_Key object_id)
        {
            if (!UUID.TryParse(object_id.m_string, out UUID id) || id.IsZero())
                return 0;

            return World.TryGetSceneObjectPart(id, out SceneObjectPart part) ? part.ParentGroup.GetSittingAvatarsCount() : 0;
        }

        public LSL_String osAESEncrypt(string secret, string plainText)
        {
            if(string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(plainText))
                return LSL_String.Empty;

            ReadOnlySpan<char> ret = Util.AESEncrypt(secret.AsSpan(), plainText.AsSpan());
            if(ret.Length == 0)
            {
                OSSLShoutError("osAESEncrypt: Failed to encrypt!");
                return LSL_String.Empty;
            }
            return ret.ToString();
        }

        public LSL_String osAESDecrypt(string secret, string encryptedText)
        {
            if(string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(encryptedText))
                return LSL_String.Empty;

            ReadOnlySpan<char> ret = Util.AESDecrypt(secret.AsSpan(), encryptedText.AsSpan());
            if(ret.Length == 0)
            {
                OSSLShoutError("osAESDecrypt: Failed to Decrypt!");
                return LSL_String.Empty;
            }
            return ret.ToString();
        }

        public LSL_String osAESEncryptTo(string secret, string plainText, string ivString)
        {
            if(string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(ivString))
                return LSL_String.Empty;

            ReadOnlySpan<char> ret = Util.AESEncryptTo(secret.AsSpan(), plainText.AsSpan(), ivString.AsSpan());
            if (ret.Length == 0)
            {
                OSSLShoutError("osAESEncryptTo: Failed to encrypt!");
                return LSL_String.Empty;
            }
            return ret.ToString();
        }

        public LSL_String osAESDecryptFrom(string secret, string encryptedText, string ivString)
        {
            if(string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(encryptedText) || string.IsNullOrEmpty(ivString))
                return LSL_String.Empty;

            ReadOnlySpan<char> ret = Util.AESDecryptFrom(secret.AsSpan(), encryptedText.AsSpan(), ivString.AsSpan());
            if (ret.Length == 0)
            {
                OSSLShoutError("osAESDecryptFrom: Failed to decrypt!");
                return LSL_String.Empty;
            }
            return ret.ToString();
        }

        public LSL_Vector osGetLinkColor(LSL_Integer link, LSL_Integer face)
        {
            SceneObjectPart linkedPart = link.value switch
            {
                ScriptBaseClass.LINK_ROOT => m_host.ParentGroup.RootPart,
                ScriptBaseClass.LINK_THIS => m_host,
                _ => m_host.ParentGroup.GetLinkNumPart(link.value)
            };

            if (linkedPart != null)
            {
                InitLSL();
                return m_LSL_Api.GetColor(linkedPart, face.value);
            }

            return LSL_Vector.Zero;
        }

        public LSL_Vector osTemperature2sRGB(LSL_Float dtemp)
        {
            //aproximate fit to http://www.vendian.org/mncharity/dir3/blackbody/ 10degree D65 tables
            float temp = (float)dtemp.value;
            if (temp <= 1000f)
                return new LSL_Vector(1.0, 0.0401, 0);
            else if (temp >= 40000f)
                return new LSL_Vector(0.3277, 0.5022, 1.0);

            float green;
            if (temp < 6600f)
            {
                green = temp - 1000f;
                green = ((((-7.87308e-13f * green) - 7.10085e-9f) * green) + 0.00022693f) * green + 0.0374249f;
                green = Math.Clamp(green, 0, 1.0f);

                if (temp <= 19.0f)
                    return new LSL_Vector(1.0, green, 0);

                float blue = temp - 1900f;
                blue = ((((-5.97E-12f * blue) + 5.49E-08f) * blue) + 8.85465E-05f) * blue - 0.0058959f;
                blue = Math.Clamp(blue, 0f, 1.0f);

                return new LSL_Vector(1.0, green, blue);
            }

            temp = 0.01f * (temp - 6000f);
            float red = 1.897315f * MathF.Pow(temp, -0.346837f) + 0.0622044f;
            red = Math.Clamp(red, 0, 1.0f);

            green = 1.261989f * MathF.Pow(temp, -0.251708f) + 0.200836f;
            green = Math.Clamp(green, 0, 1.0f);
            return new LSL_Vector(red, green, 1.0f);
        }

        public LSL_Integer osListFindListNext(LSL_List lsrc, LSL_List ltest, LSL_Integer lstart, LSL_Integer lend, LSL_Integer linstance)
        {
            int srclen = lsrc.Length;
            int testlen = ltest.Length;
            if (srclen == 0)
                return testlen == 0 ? 0 : -1;

            int instance = linstance.value;
            if (testlen == 0)
            {
                if(instance >= 0)
                    return instance < srclen ? instance : -1;

                instance += srclen;
                return instance >= 0 ? instance : -1;
            }

            if (testlen > srclen)
                return -1;


            int start = lstart.value;
            if (start < 0)
            {
                start += srclen;
                if (start < 0)
                    return -1;
            }
            else if (start >= srclen)
                return -1;

            int end = lend.value;
            if (end < 0)
            {
                end += srclen;
                if (end < 0)
                    return -1;
                end -= testlen - 1;
            }
            else if (end > srclen - testlen)
                end = srclen - testlen;

            object[] src = lsrc.Data;
            object[] test = ltest.Data;

            object test0 = test[0];
            int nmatchs = 0;

            if(instance >= 0)
            {
                for (int i = start; i <= end; i++)
                {
                    if (LSL_List.ListFind_areEqual(test0, src[i]))
                    {
                        int k = i + 1;
                        int j = 1;
                        while(j < testlen)
                        {
                            if (!LSL_List.ListFind_areEqual(test[j], src[k]))
                                break;
                            ++j;
                            ++k;
                        }

                        if (j == testlen)
                        {
                            if(nmatchs == instance)
                                return i;

                            nmatchs++;
                        }
                     }
                }
            }
            else
            {
                instance++;
                instance = -instance;
                for (int i = end; i >= start; i--)
                {
                    if (LSL_List.ListFind_areEqual(test0, src[i]))
                    {
                        int k = i + 1;
                        int j = 1;
                        while(j < testlen)
                        {
                            if (!LSL_List.ListFind_areEqual(test[j], src[k]))
                                break;
                            ++j;
                            ++k;
                        }

                        if (j == testlen)
                        {
                            if(nmatchs == instance)
                                return i;

                            nmatchs++;
                        }
                     }
                }
            }
            return -1;
        }
        public LSL_Float osListAsFloat(LSL_List src, int index)
        {
            object[] data = src.Data;
            if(data == null)
                return LSL_Float.Zero;
            if (index >= 0 && index < data.Length)
            { 
                object o = data[index];
                if(o is LSL_Float d)
                    return d;
                if(o is double nd)
                    return nd;
            }
            return LSL_Float.Zero;
        }

        public LSL_Integer osListAsInteger(LSL_List src, int index)
        {
            object[] data = src.Data;
            if(data == null)
                return LSL_Integer.Zero;
            if (index >= 0 && index < data.Length)
            { 
                object o = data[index];
                if(o is LSL_Integer i)
                    return i;
                if(o is int ni)
                    return ni;
            }
            return LSL_Integer.Zero;
        }

        public LSL_String osListAsString(LSL_List src, int index)
        {
            object[] data = src.Data;
            if(data == null)
                return LSL_String.Empty;
            if (index >= 0 && index < data.Length)
            { 
                object o = data[index];
                if(o is LSL_String s)
                    return s;
                if(o is string ns)
                    return ns;
            }
            return LSL_String.Empty;
        }

        public LSL_Vector osListAsVector(LSL_List src, int index)
        {
            object[] data = src.Data;
            if(data == null)
                return LSL_Vector.Zero;
            if (index >= 0 && index < data.Length)
            { 
                object o = data[index];
                if(o is LSL_Vector v)
                    return v;
                if(o is Vector3 ov)
                    return ov;
            }
            return LSL_Vector.Zero;
        }

        public LSL_Rotation osListAsRotation(LSL_List src, int index)
        {
            object[] data = src.Data;
            if(data == null)
                return LSL_Rotation.Identity;
            if (index >= 0 && index < data.Length)
            { 
                object o = data[index];
                if(o is LSL_Rotation r)
                    return r;
                if(o is Quaternion q)
                    return q;
            }
            return LSL_Rotation.Identity;
        }
    }
}