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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Timers;
using System.Threading;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using RegionFlags = OpenMetaverse.RegionFlags;
using Timer = System.Timers.Timer;


namespace OpenSim.Region.CoreModules.World.Estate
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EstateManagementModule")]
    public class EstateManagementModule : IEstateModule, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Timer m_regionChangeTimer = new Timer();
        public Scene Scene { get; private set; }
        public IUserManagement UserManager { get; private set; }

        protected EstateManagementCommands m_commands;

        /// <summary>
        /// If false, region restart requests from the client are blocked even if they are otherwise legitimate.
        /// </summary>
        public bool AllowRegionRestartFromClient { get; set; }
        private bool m_ignoreEstateMinorAccessControl;
        private bool m_ignoreEstatePaymentAccessControl;

        private EstateTerrainXferHandler TerrainUploader;
        public TelehubManager m_Telehub;

        public event ChangeDelegate OnRegionInfoChange;
        public event ChangeDelegate OnEstateInfoChange;
        public event MessageDelegate OnEstateMessage;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;

        private int m_delayCount = 0;

        #region Region Module interface

        public string Name { get { return "EstateManagementModule"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
            AllowRegionRestartFromClient = true;

            IConfig config = source.Configs["EstateManagement"];

            if (config != null)
            {
                AllowRegionRestartFromClient = config.GetBoolean("AllowRegionRestartFromClient", true);
                m_ignoreEstateMinorAccessControl = config.GetBoolean("IgnoreEstateMinorAccessControl", true);
                m_ignoreEstatePaymentAccessControl = config.GetBoolean("IgnoreEstatePaymentAccessControl", true);
            }
        }

        public void AddRegion(Scene scene)
        {
            Scene = scene;
            Scene.RegisterModuleInterface<IEstateModule>(this);
            Scene.EventManager.OnNewClient += EventManager_OnNewClient;
            Scene.EventManager.OnRequestChangeWaterHeight += ChangeWaterHeight;

            m_Telehub = new TelehubManager(scene);

            m_commands = new EstateManagementCommands(this);
            m_commands.Initialise();

            m_regionChangeTimer.Interval = 10000;
            m_regionChangeTimer.Elapsed += RaiseRegionInfoChange;
            m_regionChangeTimer.AutoReset = false;
        }

        public void RemoveRegion(Scene scene) {}

        public void RegionLoaded(Scene scene)
        {
            UserManager = scene.RequestModuleInterface<IUserManagement>();

            scene.RegionInfo.EstateSettings.DoDenyMinors = !m_ignoreEstateMinorAccessControl;
            scene.RegionInfo.EstateSettings.DoDenyAnonymous = !m_ignoreEstatePaymentAccessControl;
        }

        public void Close()
        {
            m_commands.Close();
        }

        #endregion

        #region IEstateModule Functions
        public uint GetRegionFlags()
        {
            RegionFlags flags = RegionFlags.None;

            // Fully implemented
            //
            if (Scene.RegionInfo.RegionSettings.AllowDamage)
                flags |= RegionFlags.AllowDamage;
            if (Scene.RegionInfo.RegionSettings.BlockTerraform)
                flags |= RegionFlags.BlockTerraform;
            if (!Scene.RegionInfo.RegionSettings.AllowLandResell)
                flags |= RegionFlags.BlockLandResell;
            if (Scene.RegionInfo.RegionSettings.DisableCollisions)
                flags |= RegionFlags.SkipCollisions;
            if (Scene.RegionInfo.RegionSettings.DisableScripts)
                flags |= RegionFlags.SkipScripts;
            if (Scene.RegionInfo.RegionSettings.DisablePhysics)
                flags |= RegionFlags.SkipPhysics;
            if (Scene.RegionInfo.RegionSettings.BlockFly)
                flags |= RegionFlags.NoFly;
            if (Scene.RegionInfo.RegionSettings.RestrictPushing)
                flags |= RegionFlags.RestrictPushObject;
            if (Scene.RegionInfo.RegionSettings.AllowLandJoinDivide)
                flags |= RegionFlags.AllowParcelChanges;
            if (Scene.RegionInfo.RegionSettings.BlockShowInSearch)
                flags |= RegionFlags.BlockParcelSearch;
            if (Scene.RegionInfo.RegionSettings.GodBlockSearch)
                flags |= (RegionFlags)(1 << 11);
            if (Scene.RegionInfo.RegionSettings.Casino)
                flags |= (RegionFlags)(1 << 10);

//            if (Scene.RegionInfo.RegionSettings.FixedSun)
//                flags |= RegionFlags.SunFixed;

            if (Scene.RegionInfo.RegionSettings.Sandbox)
                flags |= RegionFlags.Sandbox;
            if (Scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;
            if (Scene.RegionInfo.EstateSettings.AllowLandmark)
                flags |= RegionFlags.AllowLandmark;
            if (Scene.RegionInfo.EstateSettings.AllowSetHome)
                flags |= RegionFlags.AllowSetHome;
            if (Scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= RegionFlags.BlockDwell;
            if (Scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= RegionFlags.ResetHomeOnTeleport;

            if (Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride)
                flags |= RegionFlags.AllowEnvironmentOverride;

            // TODO: SkipUpdateInterestList

            // Omitted
            //
            // Omitted: SkipAgentAction (what does it do?)

            return (uint)flags;
        }

        public bool IsManager(UUID avatarID)
        {
            if (avatarID == Scene.RegionInfo.EstateSettings.EstateOwner)
                return true;

            List<UUID> ems = new List<UUID>(Scene.RegionInfo.EstateSettings.EstateManagers);
            if (ems.Contains(avatarID))
                return true;

            return false;
        }

        public void sendRegionHandshakeToAll()
        {
            Scene.ForEachClient(SendRegionHandshake);
        }

        public void TriggerEstateInfoChange()
        {
            OnEstateInfoChange?.Invoke(Scene.RegionInfo.RegionID);
        }

        protected void RaiseRegionInfoChange(object sender, ElapsedEventArgs e)
        {
            OnRegionInfoChange?.Invoke(Scene.RegionInfo.RegionID);
        }

        public void TriggerRegionInfoChange()
        {
            m_regionChangeTimer.Stop();
            m_regionChangeTimer.Start();

            OnRegionInfoChange?.Invoke(Scene.RegionInfo.RegionID);
        }

        public void setEstateTerrainBaseTexture(int level, UUID texture)
        {
            SetEstateTerrainBaseTexture(null, level, texture);
            sendRegionHandshakeToAll();
        }

        public void setEstateTerrainTextureHeights(int corner, float lowValue, float highValue)
        {
            SetEstateTerrainTextureHeights(null, corner, lowValue, highValue);
        }

        public bool IsTerrainXfer(ulong xferID)
        {
            lock (this)
            {
                if (TerrainUploader == null)
                    return false;
                else
                    return TerrainUploader.XferID == xferID;
            }
        }

        public string SetEstateOwner(int estateID, UserAccount account)
        {
            string response;

            // get the current settings from DB
            EstateSettings dbSettings = Scene.EstateDataService.LoadEstateSettings(estateID);
            if (dbSettings.EstateID == 0)
            {
                response = String.Format("No estate found with ID {0}", estateID);
            }
            else if (account.PrincipalID == dbSettings.EstateOwner)
            {
                response = String.Format("Estate already belongs to {0} ({1} {2})", account.PrincipalID, account.FirstName, account.LastName);
            }
            else
            {
                dbSettings.EstateOwner = account.PrincipalID;
                Scene.EstateDataService.StoreEstateSettings(dbSettings);
                response = String.Empty;

                // make sure there's a log entry to document the change
                m_log.InfoFormat("[ESTATE]: Estate Owner for {0} changed to {1} ({2} {3})", dbSettings.EstateName,
                                 account.PrincipalID, account.FirstName, account.LastName);

                // propagate the change
                List<UUID> regions = Scene.GetEstateRegions(estateID);
                UUID regionId = (regions.Count() > 0) ? regions.ElementAt(0) : UUID.Zero;
                if (!regionId.IsZero())
                {
                     OnEstateInfoChange?.Invoke(regionId);
                }

            }
            return response;
        }

        public string SetEstateName(int estateID, string newName)
        {
            string response;

            // get the current settings from DB
            EstateSettings dbSettings = Scene.EstateDataService.LoadEstateSettings(estateID);

            if (dbSettings.EstateID == 0)
            {
                response = String.Format("No estate found with ID {0}", estateID);
            }
            else if (newName == dbSettings.EstateName)
            {
                response = String.Format("Estate {0} is already named \"{1}\"", estateID, newName);
            }
            else
            {
                List<int> estates = Scene.EstateDataService.GetEstates(newName);
                if (estates.Count() > 0)
                {
                    response = String.Format("An estate named \"{0}\" already exists.", newName);
                }
                else
                {
                    string oldName = dbSettings.EstateName;
                    dbSettings.EstateName = newName;
                    Scene.EstateDataService.StoreEstateSettings(dbSettings);
                    response = String.Empty;

                    // make sure there's a log entry to document the change
                    m_log.InfoFormat("[ESTATE]: Estate {0} renamed from \"{1}\" to \"{2}\"", estateID, oldName, newName);

                   // propagate the change
                    List<UUID> regions = Scene.GetEstateRegions(estateID);
                    UUID regionId = (regions.Count() > 0) ? regions.ElementAt(0) : UUID.Zero;
                    if (!regionId.IsZero())
                    {
                        OnEstateInfoChange?.Invoke(regionId);
                    }
                }
            }
            return response;
        }

        public string SetRegionEstate(RegionInfo regionInfo, int estateID)
        {
            string response;

            if (regionInfo.EstateSettings.EstateID == estateID)
            {
                response = String.Format("\"{0}\" is already part of estate {1}", regionInfo.RegionName, estateID);
            }
            else
            {
                // get the current settings from DB
                EstateSettings dbSettings = Scene.EstateDataService.LoadEstateSettings(estateID);
                if (dbSettings.EstateID == 0)
                {
                    response = String.Format("No estate found with ID {0}", estateID);
                }
                else if (Scene.EstateDataService.LinkRegion(regionInfo.RegionID, estateID))
                {
                    // make sure there's a log entry to document the change
                    m_log.InfoFormat("[ESTATE]: Region {0} ({1}) moved to Estate {2} ({3}).", regionInfo.RegionID, regionInfo.RegionName, estateID, dbSettings.EstateName);

                    // propagate the change
                    OnEstateInfoChange?.Invoke(regionInfo.RegionID);

                    response = String.Empty;
                }
                else
                {
                    response = String.Format("Could not move \"{0}\" to estate {1}", regionInfo.RegionName, estateID);
                }
            }
            return response;
        }

        public string CreateEstate(string estateName, UUID ownerID)
        {
            string response;
            if (string.IsNullOrEmpty(estateName))
            {
                response = "No estate name specified.";
            }
            else
            {
                List<int> estates = Scene.EstateDataService.GetEstates(estateName);
                if (estates.Count() > 0)
                {
                    response = String.Format("An estate named \"{0}\" already exists.", estateName);
                }
                else
                {
                    EstateSettings settings = Scene.EstateDataService.CreateNewEstate();
                    if (settings == null)
                        response = String.Format("Unable to create estate \"{0}\" at this simulator", estateName);
                    else
                    {
                        settings.EstateOwner = ownerID;
                        settings.EstateName = estateName;
                        Scene.EstateDataService.StoreEstateSettings(settings);
                        response = String.Empty;
                    }
                }
            }
            return response;
        }

        #endregion

        #region Packet Data Responders

        private void ClientSendDetailedEstateData(IClientAPI remote_client, UUID invoice)
        {
            SendDetailedEstateData(remote_client, invoice);
            SendEstateLists(remote_client, invoice);
        }

        private void SendDetailedEstateData(IClientAPI remote_client, UUID invoice)
        {
            UUID estateOwner;
            estateOwner = Scene.RegionInfo.EstateSettings.EstateOwner;

            remote_client.SendDetailedEstateData(invoice,
                    Scene.RegionInfo.EstateSettings.EstateName,
                    Scene.RegionInfo.EstateSettings.EstateID,
                    Scene.RegionInfo.EstateSettings.ParentEstateID,
                    GetEstateFlags(),
                    0,
                    Scene.RegionInfo.RegionSettings.Covenant,
                    (uint) Scene.RegionInfo.RegionSettings.CovenantChangedDateTime,
                    Scene.RegionInfo.EstateSettings.AbuseEmail,
                    estateOwner);
        }

        private void SendEstateLists(IClientAPI remote_client, UUID invoice)
        {
            remote_client.SendEstateList(invoice,
                    (int)Constants.EstateAccessCodex.EstateManagers,
                    Scene.RegionInfo.EstateSettings.EstateManagers,
                    Scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendEstateList(invoice,
                    (int)Constants.EstateAccessCodex.AllowedAccess,
                    Scene.RegionInfo.EstateSettings.EstateAccess,
                    Scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendEstateList(invoice,
                    (int)Constants.EstateAccessCodex.AllowedGroups,
                    Scene.RegionInfo.EstateSettings.EstateGroups,
                    Scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendBannedUserList(invoice,
                    Scene.RegionInfo.EstateSettings.EstateBans,
                    Scene.RegionInfo.EstateSettings.EstateID);
        }

        private void EstateSetRegionInfoHandler(bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor,
                                                int matureLevel, bool restrictPushObject, bool allowParcelChanges)
        {
            if (blockTerraform)
                Scene.RegionInfo.RegionSettings.BlockTerraform = true;
            else
                Scene.RegionInfo.RegionSettings.BlockTerraform = false;

            if (noFly)
                Scene.RegionInfo.RegionSettings.BlockFly = true;
            else
                Scene.RegionInfo.RegionSettings.BlockFly = false;

            if (allowDamage)
                Scene.RegionInfo.RegionSettings.AllowDamage = true;
            else
                Scene.RegionInfo.RegionSettings.AllowDamage = false;

            if (blockLandResell)
                Scene.RegionInfo.RegionSettings.AllowLandResell = false;
            else
                Scene.RegionInfo.RegionSettings.AllowLandResell = true;

            if((byte)maxAgents <= Scene.RegionInfo.AgentCapacity)
                Scene.RegionInfo.RegionSettings.AgentLimit = (byte) maxAgents;
            else
                Scene.RegionInfo.RegionSettings.AgentLimit = Scene.RegionInfo.AgentCapacity;

            Scene.RegionInfo.RegionSettings.ObjectBonus = objectBonusFactor;

            if (matureLevel <= 13)
                Scene.RegionInfo.RegionSettings.Maturity = 0;
            else if (matureLevel <= 21)
                Scene.RegionInfo.RegionSettings.Maturity = 1;
            else
                Scene.RegionInfo.RegionSettings.Maturity = 2;

            if (restrictPushObject)
                Scene.RegionInfo.RegionSettings.RestrictPushing = true;
            else
                Scene.RegionInfo.RegionSettings.RestrictPushing = false;

            if (allowParcelChanges)
                Scene.RegionInfo.RegionSettings.AllowLandJoinDivide = true;
            else
                Scene.RegionInfo.RegionSettings.AllowLandJoinDivide = false;

            Scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();

            SendRegionInfoPacketToAll();
        }

        public void SetEstateTerrainBaseTexture(IClientAPI remoteClient, int level, UUID texture)
        {
            if (texture.IsZero())
                return;

            switch (level)
            {
                case 0:
                    Scene.RegionInfo.RegionSettings.TerrainTexture1 = texture;
                    break;
                case 1:
                    Scene.RegionInfo.RegionSettings.TerrainTexture2 = texture;
                    break;
                case 2:
                    Scene.RegionInfo.RegionSettings.TerrainTexture3 = texture;
                    break;
                case 3:
                    Scene.RegionInfo.RegionSettings.TerrainTexture4 = texture;
                    break;
            }

            Scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();
            SendRegionInfoPacketToAll();
        }

        public void SetEstateTerrainTextureHeights(IClientAPI client, int corner, float lowValue, float highValue)
        {
            switch (corner)
            {
                case 0:
                    Scene.RegionInfo.RegionSettings.Elevation1SW = lowValue;
                    Scene.RegionInfo.RegionSettings.Elevation2SW = highValue;
                    break;
                case 1:
                    Scene.RegionInfo.RegionSettings.Elevation1NW = lowValue;
                    Scene.RegionInfo.RegionSettings.Elevation2NW = highValue;
                    break;
                case 2:
                    Scene.RegionInfo.RegionSettings.Elevation1SE = lowValue;
                    Scene.RegionInfo.RegionSettings.Elevation2SE = highValue;
                    break;
                case 3:
                    Scene.RegionInfo.RegionSettings.Elevation1NE = lowValue;
                    Scene.RegionInfo.RegionSettings.Elevation2NE = highValue;
                    break;
            }

            Scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();
            sendRegionHandshakeToAll();
//            sendRegionInfoPacketToAll();
        }

        private void HandleCommitEstateTerrainTextureRequest(IClientAPI remoteClient)
        {
            // sendRegionHandshakeToAll();
        }

        public void SetRegionTerrainSettings(float WaterHeight,
                float TerrainRaiseLimit, float TerrainLowerLimit,
                bool UseEstateSun, bool UseFixedSun, float SunHour,
                bool UseGlobal, bool EstateFixedSun, float EstateSunHour)
        {
            double lastwaterlevel = Scene.RegionInfo.RegionSettings.WaterHeight;
            // Water Height
            Scene.RegionInfo.RegionSettings.WaterHeight = WaterHeight;

            // Terraforming limits
            Scene.RegionInfo.RegionSettings.TerrainRaiseLimit = TerrainRaiseLimit;
            Scene.RegionInfo.RegionSettings.TerrainLowerLimit = TerrainLowerLimit;

            // Time of day / fixed sun
            //Scene.RegionInfo.RegionSettings.UseEstateSun = UseEstateSun;
            //Scene.RegionInfo.RegionSettings.FixedSun = UseFixedSun;
            //Scene.RegionInfo.RegionSettings.SunPosition = SunHour;

            if (Scene.PhysicsEnabled && Scene.PhysicsScene != null && lastwaterlevel != WaterHeight)
                Scene.PhysicsScene.SetWaterLevel(WaterHeight);

            //m_log.Debug("[ESTATE]: UFS: " + UseFixedSun.ToString());
            //m_log.Debug("[ESTATE]: SunHour: " + SunHour.ToString());

            SendRegionInfoPacketToAll();
            Scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();
        }

        private void HandleEstateRestartSimRequest(IClientAPI remoteClient, int timeInSeconds)
        {
            if (!AllowRegionRestartFromClient)
            {
                remoteClient.SendAlertMessage("Region restart has been disabled on this simulator.");
                return;
            }

            IRestartModule restartModule = Scene.RequestModuleInterface<IRestartModule>();
            if (restartModule != null)
            {
                if (timeInSeconds == -1)
                {
                    m_delayCount++;
                    if (m_delayCount > 3)
                        return;

                    restartModule.DelayRestart(3600, "Restart delayed by region manager");
                    return;
                }

                List<int> times = new List<int>();
                while (timeInSeconds > 0)
                {
                    times.Add(timeInSeconds);
                    if (timeInSeconds > 300)
                        timeInSeconds -= 120;
                    else if (timeInSeconds > 30)
                        timeInSeconds -= 30;
                    else
                        timeInSeconds -= 15;
                }

                restartModule.ScheduleRestart(UUID.Zero, "Region will restart in {0}", times.ToArray(), false);

                m_log.InfoFormat(
                    "User {0} requested restart of region {1} in {2} seconds",
                    remoteClient.Name, Scene.Name, times.Count != 0 ? times[0] : 0);
            }
        }

        private void HandleChangeEstateCovenantRequest(IClientAPI remoteClient, UUID estateCovenantID)
        {
//            m_log.DebugFormat(
//                "[ESTATE MANAGEMENT MODULE]: Handling request from {0} to change estate covenant to {1}",
//                remoteClient.Name, estateCovenantID);

            Scene.RegionInfo.RegionSettings.Covenant = estateCovenantID;
            Scene.RegionInfo.RegionSettings.CovenantChangedDateTime = Util.UnixTimeSinceEpoch();
            Scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();
        }

        private readonly object deltareqLock = new object();
        private bool runnigDeltaExec = false;

        private class EstateAccessDeltaRequest
        {
            public IClientAPI remote_client;
            public UUID invoice;
            public int estateAccessType;
            public UUID user;
        }

        private BlockingCollection<EstateAccessDeltaRequest> deltaRequests = new BlockingCollection<EstateAccessDeltaRequest>();

        private void HandleEstateAccessDeltaRequest(IClientAPI _remote_client, UUID _invoice, int _estateAccessType, UUID _user)
        {
            // EstateAccessDelta handles Estate Managers, Sim Access, Sim Banlist, allowed Groups..  etc.

            if (_user == Scene.RegionInfo.EstateSettings.EstateOwner)
                return; // never process EO

            EstateAccessDeltaRequest newreq = new EstateAccessDeltaRequest()
            {
                remote_client = _remote_client,
                invoice = _invoice,
                estateAccessType = _estateAccessType,
                user = _user
            };

            deltaRequests.Add(newreq);

            lock(deltareqLock)
            {
                if(!runnigDeltaExec)
                {
                    runnigDeltaExec = true;
                    WorkManager.RunInThreadPool(ExecDeltaRequests,null,"execDeltaRequests");
                }
            }
        }

        private void ExecDeltaRequests(object o)
        {
            IClientAPI remote_client;
            UUID invoice;
            int estateAccessType;
            UUID user;
            Dictionary<int,EstateSettings> changed = new Dictionary<int,EstateSettings>();
            Dictionary<IClientAPI,UUID> sendAllowedOrBanList = new Dictionary<IClientAPI,UUID>();
            Dictionary<IClientAPI,UUID> sendManagers  = new Dictionary<IClientAPI,UUID>();
            Dictionary<IClientAPI,UUID> sendGroups  = new Dictionary<IClientAPI,UUID>();

            List<EstateSettings> otherEstates = new List<EstateSettings>();

            bool sentAllowedFull = false;
            bool sentBansFull = false;
            bool sentGroupsFull = false;
            bool sentManagersFull = false;

            EstateAccessDeltaRequest req;
            while(Scene.IsRunning)
            {
                req = null;
                deltaRequests.TryTake(out req, 500);

                if(!Scene.IsRunning)
                    break;

                if(req == null)
                {
                    if(changed.Count > 0)
                    {
                        foreach(EstateSettings est in changed.Values)
                            Scene.EstateDataService.StoreEstateSettings(est);

                        TriggerEstateInfoChange();
                    }

                    EstateSettings es = Scene.RegionInfo.EstateSettings;
                    foreach(KeyValuePair<IClientAPI,UUID> kvp in sendAllowedOrBanList)
                    {
                        IClientAPI cli = kvp.Key;
                        UUID invoive = kvp.Value;
                        cli.SendEstateList(invoive, (int)Constants.EstateAccessCodex.AllowedAccess, es.EstateAccess, es.EstateID);
                        cli.SendBannedUserList(invoive, es.EstateBans, es.EstateID);
                    }
                    sendAllowedOrBanList.Clear();

                    foreach(KeyValuePair<IClientAPI,UUID> kvp in sendManagers)
                    {
                        IClientAPI cli = kvp.Key;
                        cli.SendEstateList(kvp.Value, (int)Constants.EstateAccessCodex.EstateManagers, es.EstateManagers, es.EstateID);
                    }
                    foreach(KeyValuePair<IClientAPI,UUID> kvp in sendGroups)
                    {
                        IClientAPI cli = kvp.Key;
                        cli.SendEstateList(kvp.Value, (int)Constants.EstateAccessCodex.AllowedGroups, es.EstateGroups, es.EstateID);
                    }
                    otherEstates.Clear();
                    sendAllowedOrBanList.Clear();
                    sendManagers.Clear();
                    sendGroups.Clear();
                    changed.Clear();
                    lock(deltareqLock)
                    {
                        if(deltaRequests.Count != 0)
                            continue;
                        runnigDeltaExec = false;
                        return;
                    }
                }

                remote_client = req.remote_client;
                if(!remote_client.IsActive)
                    continue;

                invoice = req.invoice;
                user = req.user;

                estateAccessType = req.estateAccessType;

                bool needReply = ((estateAccessType & 1024) == 0);
                bool doOtherEstates = ((estateAccessType & 3) != 0);

                EstateSettings thisSettings = Scene.RegionInfo.EstateSettings;
                int thisEstateID =(int)thisSettings.EstateID;

                UUID agentID = remote_client.AgentId;

                bool isadmin = Scene.Permissions.IsAdministrator(agentID);
                // just i case recheck rights
                if (!isadmin && !Scene.Permissions.IsEstateManager(agentID))
                {
                    remote_client.SendAlertMessage("Method EstateAccess Failed, you don't have permissions");
                    continue;
                }

                otherEstates.Clear();
                if(doOtherEstates)
                {
                    UUID thisOwner = Scene.RegionInfo.EstateSettings.EstateOwner;
                    List<int> estateIDs = Scene.EstateDataService.GetEstatesByOwner(thisOwner);
                    foreach (int estateID in estateIDs)
                    {
                        if (estateID == thisEstateID)
                            continue;

                        EstateSettings estateSettings;
                        if(changed.ContainsKey(estateID))
                            estateSettings = changed[estateID];
                        else
                            estateSettings = Scene.EstateDataService.LoadEstateSettings(estateID);

                        if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                            continue;
                        otherEstates.Add(estateSettings);
                    }
                    estateIDs.Clear();
                }

                // the commands
                // first the ones allowed for estate managers on this region
                if ((estateAccessType & 4) != 0) // User add
                {
                    if(thisSettings.EstateUsersCount() >= (int)Constants.EstateAccessLimits.AllowedAccess)
                    {
                        if(!sentAllowedFull)
                        {
                            sentAllowedFull = true;
                            remote_client.SendAlertMessage("Estate Allowed users list is full");
                         }
                    }
                    else
                    {
                        if (doOtherEstates)
                        {
                            foreach (EstateSettings estateSettings in otherEstates)
                            {
                                if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                                    continue;
                                if(estateSettings.EstateUsersCount() >= (int)Constants.EstateAccessLimits.AllowedAccess)
                                    continue;
                                estateSettings.AddEstateUser(user);
                                estateSettings.RemoveBan(user);
                                changed[(int)estateSettings.EstateID] = estateSettings;
                            }
                        }

                        thisSettings.AddEstateUser(user);
                        thisSettings.RemoveBan(user);
                        changed[thisEstateID] = thisSettings;

                        if(needReply)
                            sendAllowedOrBanList[remote_client] = invoice;
                    }
                }

                if ((estateAccessType & 8) != 0) // User remove
                {
                    if (doOtherEstates) // All estates
                    {
                        foreach (EstateSettings estateSettings in otherEstates)
                        {
                            if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                                continue;
                            estateSettings.RemoveEstateUser(user);
                            changed[(int)estateSettings.EstateID] = estateSettings;
                        }
                    }

                    thisSettings.RemoveEstateUser(user);
                    changed[thisEstateID] = thisSettings;

                    if(needReply)
                        sendAllowedOrBanList[remote_client] = invoice;
                }

                if ((estateAccessType & 16) != 0) // Group add
                {
                    if(thisSettings.EstateGroupsCount() >= (int)Constants.EstateAccessLimits.AllowedGroups)
                    {
                        if(!sentGroupsFull)
                        {
                            sentGroupsFull = true;
                            remote_client.SendAlertMessage("Estate Allowed groups list is full");
                         }
                    }
                    else
                    {
                        if (doOtherEstates) // All estates
                        {
                            foreach (EstateSettings estateSettings in otherEstates)
                            {
                                if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                                    continue;
                                if(estateSettings.EstateGroupsCount() >= (int)Constants.EstateAccessLimits.AllowedGroups)
                                    continue;
                                estateSettings.AddEstateGroup(user);
                                changed[(int)estateSettings.EstateID] = estateSettings;
                            }
                        }

                        thisSettings.AddEstateGroup(user);
                        changed[thisEstateID] = thisSettings;

                        sendGroups[remote_client] = invoice;
                    }
                }

                if ((estateAccessType & 32) != 0) // Group remove
                {
                    if (doOtherEstates) // All estates
                    {
                        foreach (EstateSettings estateSettings in otherEstates)
                        {
                            if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                                continue;
                            estateSettings.RemoveEstateGroup(user);
                            changed[(int)estateSettings.EstateID] = estateSettings;
                        }
                    }

                    thisSettings.RemoveEstateGroup(user);
                    changed[thisEstateID] = thisSettings;

                    sendGroups[remote_client] = invoice;
                }

                if ((estateAccessType & 64) != 0) // Ban add
                {

                    if(thisSettings.EstateBansCount() >= (int)Constants.EstateAccessLimits.EstateBans)
                    {
                        if(!sentBansFull)
                        {
                            sentBansFull = true;
                            remote_client.SendAlertMessage("Estate Ban list is full");
                        }
                    }
                    else
                    {
                        EstateBan[] banlistcheck = Scene.RegionInfo.EstateSettings.EstateBans;

                        bool alreadyInList = false;

                        for (int i = 0; i < banlistcheck.Length; i++)
                        {
                            if (user == banlistcheck[i].BannedUserID)
                            {
                                alreadyInList = true;
                                break;
                            }
                        }
                        if (!alreadyInList)
                        {
                            if (doOtherEstates) // All estates
                            {
                                foreach (EstateSettings estateSettings in otherEstates)
                                {
                                    if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                                        continue;

                                    if(estateSettings.EstateBansCount() >= (int)Constants.EstateAccessLimits.EstateBans)
                                        continue;

                                    EstateBan bitem = new EstateBan()
                                    {
                                        BannedUserID = user,
                                        EstateID = estateSettings.EstateID,
                                        BannedHostAddress = "0.0.0.0",
                                        BannedHostIPMask = "0.0.0.0",
                                        BanningUserID = remote_client.AgentId,
                                        BanTime = Util.UnixTimeSinceEpoch()
                                    };

                                    estateSettings.AddBan(bitem);
                                    estateSettings.RemoveEstateUser(user);
                                    changed[(int)estateSettings.EstateID] = estateSettings;
                                }
                            }

                            EstateBan item = new EstateBan()
                            {
                                BannedUserID = user,
                                EstateID = Scene.RegionInfo.EstateSettings.EstateID,
                                BannedHostAddress = "0.0.0.0",
                                BannedHostIPMask = "0.0.0.0",
                                BanningUserID = remote_client.AgentId,
                                BanTime = Util.UnixTimeSinceEpoch()
                            };

                            thisSettings.AddBan(item);
                            thisSettings.RemoveEstateUser(user);
                            changed[thisEstateID] = thisSettings;

                            ScenePresence s = Scene.GetScenePresence(user);
                            if (s != null)
                            {
                                if (!s.IsChildAgent)
                                {
                                    if (!Scene.TeleportClientHome(user, s.ControllingClient))
                                    {
                                        s.ControllingClient.Kick("Your access to the region was revoked and TP home failed - you have been logged out.");
                                        Scene.CloseAgent(s.UUID, false);
                                    }
                                }
                            }
                        }
                        else
                        {
                            remote_client.SendAlertMessage("User is already on the region ban list");
                        }
                        //Scene.RegionInfo.regionBanlist.Add(Manager(user);
                        if(needReply)
                            sendAllowedOrBanList[remote_client] = invoice;
                    }
                }

                if ((estateAccessType & 128) != 0) // Ban remove
                {
                    EstateBan[] banlistcheck = Scene.RegionInfo.EstateSettings.EstateBans;

                    bool alreadyInList = false;
                    EstateBan listitem = null;

                    for (int i = 0; i < banlistcheck.Length; i++)
                    {
                        if (user == banlistcheck[i].BannedUserID)
                        {
                            alreadyInList = true;
                            listitem = banlistcheck[i];
                            break;
                        }
                    }

                    if (alreadyInList && listitem != null)
                    {
                        if (doOtherEstates) // All estates
                        {
                            foreach (EstateSettings estateSettings in otherEstates)
                            {
                                if(!isadmin && !estateSettings.IsEstateManagerOrOwner(agentID))
                                    continue;
                                estateSettings.RemoveBan(user);
                                changed[(int)estateSettings.EstateID] = estateSettings;
                            }
                        }

                        thisSettings.RemoveBan(listitem.BannedUserID);
                        changed[thisEstateID] = thisSettings;
                    }
                    else
                    {
                        remote_client.SendAlertMessage("User is not on the region ban list");
                    }

                    if(needReply)
                        sendAllowedOrBanList[remote_client] = invoice;
                }

                // last the ones only for owners of this region
                if (!Scene.Permissions.CanIssueEstateCommand(agentID, true))
                {
                        remote_client.SendAlertMessage("Method EstateAccess Failed, you don't have permissions");
                        continue;
                }

                if ((estateAccessType & 256) != 0) // Manager add
                {
                    if(thisSettings.EstateManagersCount() >= (int)Constants.EstateAccessLimits.EstateManagers)
                    {
                        if(!sentManagersFull)
                        {
                            sentManagersFull = true;
                            remote_client.SendAlertMessage("Estate Managers list is full");
                         }
                    }
                    else
                    {
                        if (doOtherEstates) // All estates
                        {
                            foreach (EstateSettings estateSettings in otherEstates)
                            {
                                if(!isadmin && !estateSettings.IsEstateOwner(agentID)) // redundante check?
                                    continue;
                                if(estateSettings.EstateManagersCount() >= (int)Constants.EstateAccessLimits.EstateManagers)
                                    continue;
                                estateSettings.AddEstateManager(user);
                                changed[(int)estateSettings.EstateID] = estateSettings;
                            }
                        }

                        thisSettings.AddEstateManager(user);
                        changed[thisEstateID] = thisSettings;

                        sendManagers[remote_client] = invoice;
                    }
                }

                if ((estateAccessType & 512) != 0) // Manager remove
                {
                    if (doOtherEstates) // All estates
                    {
                        foreach (EstateSettings estateSettings in otherEstates)
                        {
                            if(!isadmin && !estateSettings.IsEstateOwner(agentID))
                                continue;

                            estateSettings.RemoveEstateManager(user);
                            changed[(int)estateSettings.EstateID] = estateSettings;
                        }
                    }

                    thisSettings.RemoveEstateManager(user);
                    changed[thisEstateID] = thisSettings;

                    sendManagers[remote_client] = invoice;
                }
            }
            lock(deltareqLock)
                runnigDeltaExec = false;
        }

        public void HandleOnEstateManageTelehub(IClientAPI client, UUID invoice, UUID senderID, string cmd, uint param1)
        {
            SceneObjectPart part;

            switch (cmd)
            {
                case "info ui":
                    break;

                case "connect":
                    // Add the Telehub
                    part = Scene.GetSceneObjectPart((uint)param1);
                    if (part == null)
                        return;
                    SceneObjectGroup grp = part.ParentGroup;

                    m_Telehub.Connect(grp);
                    break;

                case "delete":
                    // Disconnect Telehub
                    m_Telehub.Disconnect();
                    break;

                case "spawnpoint add":
                    // Add SpawnPoint to the Telehub
                    part = Scene.GetSceneObjectPart((uint)param1);
                    if (part == null)
                        return;
                    m_Telehub.AddSpawnPoint(part.AbsolutePosition);
                    break;

                case "spawnpoint remove":
                    // Remove SpawnPoint from Telehub
                    m_Telehub.RemoveSpawnPoint((int)param1);
                    break;

                default:
                    break;
            }

            if (client != null)
                SendTelehubInfo(client);
        }

        private void SendSimulatorBlueBoxMessage(
            IClientAPI remote_client, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message)
        {
            IDialogModule dm = Scene.RequestModuleInterface<IDialogModule>();

            if (dm != null)
                dm.SendNotificationToUsersInRegion(senderID, senderName, message);
        }

        private void SendEstateBlueBoxMessage(
            IClientAPI remote_client, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message)
        {
            TriggerEstateMessage(senderID, senderName, message);
        }

        private void HandleEstateDebugRegionRequest(
            IClientAPI remote_client, UUID invoice, UUID senderID,
            bool disableScripts, bool disableCollisions, bool disablePhysics)
        {
            Scene.RegionInfo.RegionSettings.DisablePhysics = disablePhysics;
            Scene.RegionInfo.RegionSettings.DisableScripts = disableScripts;
            Scene.RegionInfo.RegionSettings.DisableCollisions = disableCollisions;
            Scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();

            ISceneCommandsModule scm = Scene.RequestModuleInterface<ISceneCommandsModule>();

            if (scm != null)
            {
                scm.SetSceneDebugOptions(
                    new Dictionary<string, string>() {
                        { "scripting", (!disableScripts).ToString() },
                        { "collisions", (!disableCollisions).ToString() },
                        { "physics", (!disablePhysics).ToString() }
                    }
                );
            }
        }

        private void HandleEstateTeleportOneUserHomeRequest(IClientAPI remover_client, UUID invoice, UUID senderID, UUID prey, bool kick)
        {
            if (prey.IsZero())
                return;

             EstateTeleportOneUserHomeRequest evOverride = OnEstateTeleportOneUserHomeRequest;
             if(evOverride != null)
             {
                evOverride(remover_client, invoice, senderID, prey, kick);
                return;
             }

            if (!Scene.Permissions.CanIssueEstateCommand(remover_client.AgentId, false))
                return;

            ScenePresence s = Scene.GetScenePresence(prey);
            if (s != null && !s.IsDeleted && !s.IsInTransit)
            {
                if (kick)
                {
                    s.ControllingClient.Kick("You have been kicked");
                    Scene.CloseAgent(s.UUID, false);
                }
                else if (!Scene.TeleportClientHome(prey, s.ControllingClient))
                {
                    s.ControllingClient.Kick("You were teleported home by the region owner, but the TP failed ");
                    Scene.CloseAgent(s.UUID, false);
                }
            }
        }

        private void HandleEstateTeleportAllUsersHomeRequest(IClientAPI remover_client, UUID invoice, UUID senderID)
        {
             EstateTeleportAllUsersHomeRequest evOverride = OnEstateTeleportAllUsersHomeRequest;
             if(evOverride != null)
             {
                evOverride(remover_client, invoice, senderID);
                return;
             }

            if (!Scene.Permissions.CanIssueEstateCommand(remover_client.AgentId, false))
                return;

            Scene.ForEachRootClient(delegate(IClientAPI client)
            {
                if (client.AgentId != senderID)
                {
                    // make sure they are still there, we could be working down a long list
                    // Also make sure they are actually in the region
                    if(Scene.TryGetScenePresence(client.AgentId, out ScenePresence p))
                    {
                        if (!Scene.TeleportClientHome(p.UUID, p.ControllingClient))
                        {
                            p.ControllingClient.Kick("You were teleported home by the region owner, but the TP failed - you have been logged out.");
                            Scene.CloseAgent(p.UUID, false);
                        }
                    }
                }
            });
        }

        private void AbortTerrainXferHandler(IClientAPI remoteClient, ulong XferID)
        {
            lock (this)
            {
                if ((TerrainUploader != null) && (XferID == TerrainUploader.XferID))
                {
                    remoteClient.OnXferReceive -= TerrainUploader.XferReceive;
                    remoteClient.OnAbortXfer -= AbortTerrainXferHandler;
                    TerrainUploader.TerrainUploadDone -= HandleTerrainApplication;

                    TerrainUploader = null;
                    remoteClient.SendAlertMessage("Terrain Upload aborted by the client");
                }
            }
        }

        private void HandleTerrainApplication(string filename, byte[] terrainData, IClientAPI remoteClient)
        {
            lock (this)
            {
                remoteClient.OnXferReceive -= TerrainUploader.XferReceive;
                remoteClient.OnAbortXfer -= AbortTerrainXferHandler;
                TerrainUploader.TerrainUploadDone -= HandleTerrainApplication;

                TerrainUploader = null;
            }

            m_log.DebugFormat("[CLIENT]: Terrain upload from {0} to {1} complete.", remoteClient.Name, Scene.Name);
            remoteClient.SendAlertMessage("Terrain Upload Complete. Loading....");

            ITerrainModule terr = Scene.RequestModuleInterface<ITerrainModule>();

            if (terr != null)
            {
                try
                {
                    using (MemoryStream terrainStream = new MemoryStream(terrainData))
                        terr.LoadFromStream(filename, terrainStream);

                    FileInfo x = new FileInfo(filename);
                    remoteClient.SendAlertMessage("Your terrain was loaded as a " + x.Extension + " file. It may take a few moments to appear.");
                }
                catch (IOException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was an IO Exception loading your terrain.  Please check free space.");

                    return;
                }
                catch (SecurityException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a security Exception loading your terrain.  Please check the security on the simulator drive");

                    return;
                }
                catch (UnauthorizedAccessException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a security Exception loading your terrain.  Please check the security on the simulator drive");

                    return;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error loading a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a general error loading your terrain.  Please fix the terrain file and try again");
                }
            }
            else
            {
                remoteClient.SendAlertMessage("Unable to apply terrain.  Cannot get an instance of the terrain module");
            }
        }

        private void HandleUploadTerrain(IClientAPI remote_client, string clientFileName)
        {
            lock (this)
            {
                if (TerrainUploader == null)
                {
                    m_log.DebugFormat(
                        "[TERRAIN]: Started receiving terrain upload for region {0} from {1}",
                        Scene.Name, remote_client.Name);

                    TerrainUploader = new EstateTerrainXferHandler(remote_client, clientFileName);
                    remote_client.OnXferReceive += TerrainUploader.XferReceive;
                    remote_client.OnAbortXfer += AbortTerrainXferHandler;
                    TerrainUploader.TerrainUploadDone += HandleTerrainApplication;
                    TerrainUploader.RequestStartXfer(remote_client);
                }
                else
                {
                    remote_client.SendAlertMessage("Another Terrain Upload is in progress.  Please wait your turn!");
                }
            }
        }

        private void HandleTerrainRequest(IClientAPI remote_client, string clientFileName)
        {
            // Save terrain here
            ITerrainModule terr = Scene.RequestModuleInterface<ITerrainModule>();

            if (terr != null)
            {
//                m_log.Warn("[CLIENT]: Got Request to Send Terrain in region " + Scene.RegionInfo.RegionName);
                if (File.Exists(Util.dataDir() + "/terrain.raw"))
                {
                    File.Delete(Util.dataDir() + "/terrain.raw");
                }
                terr.SaveToFile(Util.dataDir() + "/terrain.raw");

                byte[] bdata;
                using(FileStream input = new FileStream(Util.dataDir() + "/terrain.raw",FileMode.Open))
                {
                    bdata = new byte[input.Length];
                    input.Read(bdata, 0, (int)input.Length);
                }
                if(bdata == null || bdata.Length == 0)
                {
                    remote_client.SendAlertMessage("Terrain error");
                    return;
                }

                remote_client.SendAlertMessage("Terrain file written, starting download...");
                string xfername = (UUID.Random()).ToString();
                Scene.XferManager.AddNewFile(xfername, bdata);

                m_log.DebugFormat("[CLIENT]: Sending terrain for region {0} to {1}", Scene.Name, remote_client.Name);
                remote_client.SendInitiateDownload(xfername, clientFileName);
            }
        }

        public void HandleRegionInfoRequest(IClientAPI remote_client)
        {
            RegionInfoForEstateMenuArgs args = new RegionInfoForEstateMenuArgs()
            {
                billableFactor = Scene.RegionInfo.EstateSettings.BillableFactor,
                estateID = Scene.RegionInfo.EstateSettings.EstateID,
                maxAgents = Scene.RegionInfo.RegionSettings.AgentLimit,
                objectBonusFactor = (float)Scene.RegionInfo.RegionSettings.ObjectBonus,
                parentEstateID = Scene.RegionInfo.EstateSettings.ParentEstateID,
                pricePerMeter = Scene.RegionInfo.EstateSettings.PricePerMeter,
                redirectGridX = Scene.RegionInfo.EstateSettings.RedirectGridX,
                redirectGridY = Scene.RegionInfo.EstateSettings.RedirectGridY,
                regionFlags = GetRegionFlags(),
                simAccess = Scene.RegionInfo.AccessLevel,
                sunHour = 0,
                terrainLowerLimit = (float)Scene.RegionInfo.RegionSettings.TerrainLowerLimit,
                terrainRaiseLimit = (float)Scene.RegionInfo.RegionSettings.TerrainRaiseLimit,
                useEstateSun = false,
                waterHeight = (float)Scene.RegionInfo.RegionSettings.WaterHeight,
                simName = Scene.RegionInfo.RegionName,
                regionType = Scene.RegionInfo.RegionType,
                AgentCapacity = Scene.RegionInfo.AgentCapacity,
                ObjectsCapacity = Scene.RegionInfo.ObjectCapacity,
            };

            remote_client.SendRegionInfoToEstateMenu(args);
        }

        private void HandleEstateCovenantRequest(IClientAPI remote_client)
        {
            remote_client.SendEstateCovenantInformation(Scene.RegionInfo.RegionSettings.Covenant);
        }

        private void HandleLandStatRequest(int parcelID, uint reportType, uint requestFlags, string filter, IClientAPI remoteClient)
        {
            if (reportType == 1)
            {
                LandCollidersStatRequest(parcelID, requestFlags, filter, remoteClient);
                return;
            }

            if (reportType != 0)
                remoteClient.SendLandStatReply(reportType, requestFlags, 0, new LandStatReportItem[0]);

            IScriptModule scriptModule = Scene.RequestModuleInterface<IScriptModule>();
            if (scriptModule == null)
                remoteClient.SendLandStatReply(reportType, requestFlags, 0, new LandStatReportItem[0]);

            ICollection<ScriptTopStatsData>  sceneData = scriptModule.GetTopObjectStats(
                    0.001f, 1024, out float totaltime, out float totalmemory);

            if(sceneData == null || sceneData.Count == 0)
                remoteClient.SendLandStatReply(0, requestFlags, 0, new LandStatReportItem[0]);

            IUrlModule urlModule = Scene.RequestModuleInterface<IUrlModule>();

            //reformat the name so we don't have to do it on every item
            bool hasfilter = false;
            if ((requestFlags & 0x0e) != 0 && !string.IsNullOrWhiteSpace(filter))
            {
                if ((requestFlags & 0x00000002) != 0)
                {
                    int indx = filter.IndexOf('.');
                    if(indx > 0)
                    {
                        string tmp = filter.Substring(0, indx);
                        if(indx < filter.Length - 1)
                        {
                            tmp += " " + filter.Substring(indx +1);
                        }
                        filter = tmp;
                    }
                }
                filter.Trim();
                filter = filter.ToLower();
                hasfilter = filter.Length > 0;
            }

            var sortedSceneData = sceneData.OrderByDescending(e => e.time);

            int items = 0;
            List<LandStatReportItem> SceneReport = new List<LandStatReportItem>();
            foreach (var entry in sortedSceneData)
            {
                // The object may have been deleted since we received the data.
                SceneObjectPart sop = Scene.GetSceneObjectPart(entry.localID);
                if(sop == null)
                    continue;

                SceneObjectGroup so = sop.ParentGroup;
                if (so == null || so.IsDeleted || so.inTransit)
                    continue;

                int urls_used = 0;
                if (urlModule != null)
                {
                    urls_used = urlModule.GetUrlCount(sop.UUID);

                    // Don't show scripts that haven't executed or where execution time is below one microsecond in
                    // order to produce a more readable report.
                    // Unless they are using URLs or using 1024 bytes or more.
                    if (entry.time < 0.001 && entry.memory < 1024 && urls_used == 0)
                        continue;
                }
                else if (entry.time < 0.001 && entry.memory < 1024)
                        continue;

                ILandObject land = Scene.LandChannel.GetLandObject(so.AbsolutePosition);
                if((requestFlags & 1) != 0 && land.LandData.LocalID != parcelID)
                    continue;

                string owner_name = UserManager.GetUserName(so.OwnerID);
                string task_name = so.Name;
                string parcel_name = land != null ? land.LandData.Name : "unknown";

                if (hasfilter)
                {
                    if ((requestFlags & 0x00000002) != 0)
                    {
                        if (!owner_name.ToLower().Contains(filter))
                            continue;
                    }
                    else if ((requestFlags & 0x00000004) != 0)
                    {
                        if (!task_name.ToLower().Contains(filter))
                            continue;
                    }
                    else if ((requestFlags & 0x00000008) != 0)
                    {
                        if (!parcel_name.ToLower().Contains(filter))
                            continue;
                    }
                }

                LandStatReportItem lsri = new LandStatReportItem()
                {
                    LocationX = so.AbsolutePosition.X,
                    LocationY = so.AbsolutePosition.Y,
                    LocationZ = so.AbsolutePosition.Z,
                    Score = entry.time,
                    TaskID = so.UUID,
                    TaskLocalID = so.LocalId,
                    TaskName = task_name,
                    OwnerName = owner_name,
                    OwnerID = so.OwnerID,
                    Bytes = entry.memory,
                    Urls = urls_used,
                    Time = Utils.DateTimeToUnixTime(sop.Rezzed),
                    Parcel = parcel_name
                };

                items++;
                SceneReport.Add(lsri);

                if (items >= 100)
                    break;
            }

            remoteClient.SendLandStatReply(0, requestFlags, (uint)SceneReport.Count,SceneReport.ToArray());
        }

        private void LandCollidersStatRequest(int parcelID, uint requestFlags, string filter, IClientAPI remoteClient)
        {
            if (!Scene.Permissions.CanIssueEstateCommand(remoteClient.AgentId, false))
                remoteClient.SendLandStatReply(1, requestFlags, 0, new LandStatReportItem[0]);

            Dictionary<uint, float> sceneData = Scene.PhysicsScene.GetTopColliders();

            List<LandStatReportItem> SceneReport = new List<LandStatReportItem>();
            if (sceneData != null)
            {
                //reformat the name so we don't have to do it on every item
                bool hasfilter = false;
                if ((requestFlags & 0x0c) != 0 && !string.IsNullOrWhiteSpace(filter))
                {
                    if ((requestFlags & 0x00000002) != 0)
                    {
                        int indx = filter.IndexOf('.');
                        if (indx > 0)
                        {
                            string tmp = filter.Substring(0, indx);
                            if (indx < filter.Length - 1)
                            {
                                tmp += " " + filter.Substring(indx + 1);
                            }
                            filter = tmp;
                        }
                    }
                    filter.Trim();
                    filter = filter.ToLower();
                    hasfilter = filter.Length > 0;
                }

                int items = 0;
                foreach (KeyValuePair<uint,float> kvp in sceneData)
                {
                    if (kvp.Value < 0.001)
                        continue;

                    // The object may have been deleted since we received the data.
                    SceneObjectPart sop = Scene.GetSceneObjectPart(kvp.Key);
                    if(sop == null)
                        continue;

                    SceneObjectGroup so = sop.ParentGroup;
                    if (so == null || so.IsDeleted || so.inTransit)
                        continue;

                    // Don't show scripts that haven't executed or where execution time is below one microsecond in
                    // order to produce a more readable report.
                    // Unless they are using URLs or using 1024 bytes or more.

                    ILandObject land = Scene.LandChannel.GetLandObject(so.AbsolutePosition);
                    if ((requestFlags & 1) != 0 && land.LandData.LocalID != parcelID)
                        continue;

                    string owner_name = UserManager.GetUserName(so.OwnerID);
                    string task_name = so.Name;
                    string parcel_name = land != null ? land.LandData.Name : "unknown";

                    if (hasfilter)
                    {
                        if ((requestFlags & 0x00000002) != 0)
                        {
                            if (!owner_name.ToLower().Contains(filter))
                                continue;
                        }
                        else if ((requestFlags & 0x00000004) != 0)
                        {
                            if (!task_name.ToLower().Contains(filter))
                                continue;
                        }
                        else if ((requestFlags & 0x00000008) != 0)
                        {
                            if (!parcel_name.ToLower().Contains(filter))
                                continue;
                        }
                    }

                    LandStatReportItem lsri = new LandStatReportItem()
                    {
                        LocationX = so.AbsolutePosition.X,
                        LocationY = so.AbsolutePosition.Y,
                        LocationZ = so.AbsolutePosition.Z,
                        Score = kvp.Value,
                        TaskID = so.UUID,
                        TaskLocalID = so.LocalId,
                        TaskName = task_name,
                        OwnerName = owner_name,
                        OwnerID = so.OwnerID,
                        Time = Utils.DateTimeToUnixTime(sop.Rezzed),
                        Parcel = parcel_name
                    };

                    items++;
                    SceneReport.Add(lsri);

                    if (items >= 100)
                        break;
                }
            }

            remoteClient.SendLandStatReply(1, requestFlags, (uint)SceneReport.Count, SceneReport.ToArray());
        }

        #endregion

        #region Outgoing Packets

        public void SendRegionInfoPacketToAll()
        {
//            Scene.ForEachRootClient(delegate(IClientAPI client)
            Scene.ForEachClient(delegate(IClientAPI client)
            {
                HandleRegionInfoRequest(client);
            });
        }

        public void SendRegionHandshake(IClientAPI remoteClient)
        {
            remoteClient.SendRegionHandshake();
        }

        public void HandleEstateChangeInfo(IClientAPI remoteClient, UUID invoice, UUID senderID, UInt32 parms1, UInt32 parms2)
        {
            bool lastallowEnvOvr = Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride;

            if ((parms1 & 0x00008000) != 0)
                Scene.RegionInfo.EstateSettings.PublicAccess = true;
            else
                Scene.RegionInfo.EstateSettings.PublicAccess = false;

            // taxfree is now !AllowAccessOverride (note the negate)
            if ((parms1 & 0x00000020) != 0)
                Scene.RegionInfo.EstateSettings.TaxFree = false;
            else
                Scene.RegionInfo.EstateSettings.TaxFree = true;

            if ((parms1 & 0x00000200) != 0)
                Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride = true;
            else
                Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride = false;

            if ((parms1 & 0x00100000) != 0)
                Scene.RegionInfo.EstateSettings.AllowDirectTeleport = true;
            else
                Scene.RegionInfo.EstateSettings.AllowDirectTeleport = false;

            if ((parms1 & 0x00800000) != 0)
                Scene.RegionInfo.EstateSettings.DenyAnonymous = true;
            else
                Scene.RegionInfo.EstateSettings.DenyAnonymous = false;

            // no longer in used, may be reassigned
            if ((parms1 & 0x01000000) != 0)
                Scene.RegionInfo.EstateSettings.DenyIdentified = true;
            else
                Scene.RegionInfo.EstateSettings.DenyIdentified = false;

            // no longer in used, may be reassigned
            if ((parms1 & 0x02000000) != 0)
                Scene.RegionInfo.EstateSettings.DenyTransacted = true;
            else
                Scene.RegionInfo.EstateSettings.DenyTransacted = false;

            if ((parms1 & 0x10000000) != 0)
                Scene.RegionInfo.EstateSettings.AllowVoice = true;
            else
                Scene.RegionInfo.EstateSettings.AllowVoice = false;

            if ((parms1 & 0x40000000) != 0)
                Scene.RegionInfo.EstateSettings.DenyMinors = true;
            else
                Scene.RegionInfo.EstateSettings.DenyMinors = false;

            Scene.EstateDataService.StoreEstateSettings(Scene.RegionInfo.EstateSettings);

            if (lastallowEnvOvr && Scene.LandChannel != null && !Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride)
            {
                Scene.ClearAllParcelEnvironments();
            }

            TriggerEstateInfoChange();

            SendDetailedEstateData(remoteClient, invoice);
        }

        public bool handleEstateChangeInfoCap(string estateName, UUID invoice,
            bool externallyVisible,
            bool allowDirectTeleport,
            bool denyAnonymous, bool denyAgeUnverified,
            bool alloVoiceChat, bool overridePublicAccess,
            bool allowEnvironmentOverride)
        {
            bool lastallowEnvOvr = Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride;

            Scene.RegionInfo.EstateSettings.PublicAccess = externallyVisible;
            Scene.RegionInfo.EstateSettings.AllowDirectTeleport = allowDirectTeleport;

            Scene.RegionInfo.EstateSettings.DenyAnonymous = denyAnonymous;
            Scene.RegionInfo.EstateSettings.AllowVoice = alloVoiceChat;

            // taxfree is now !AllowAccessOverride
            Scene.RegionInfo.EstateSettings.TaxFree = overridePublicAccess;
            Scene.RegionInfo.EstateSettings.DenyMinors = denyAgeUnverified;
            Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride = allowEnvironmentOverride;

            Scene.EstateDataService.StoreEstateSettings(Scene.RegionInfo.EstateSettings);
            if(lastallowEnvOvr && !allowEnvironmentOverride)
                Scene.ClearAllParcelEnvironments();

            TriggerEstateInfoChange();

            return true;
        }

        #endregion

        #region Other Functions

        public void ChangeWaterHeight(float height)
        {
            SetRegionTerrainSettings(height,
                    (float)Scene.RegionInfo.RegionSettings.TerrainRaiseLimit,
                    (float)Scene.RegionInfo.RegionSettings.TerrainLowerLimit,
                    Scene.RegionInfo.RegionSettings.UseEstateSun,
                    Scene.RegionInfo.RegionSettings.FixedSun,
                    (float)Scene.RegionInfo.RegionSettings.SunPosition,
                    //Scene.RegionInfo.EstateSettings.UseGlobalTime,
                    false,
                    //Scene.RegionInfo.EstateSettings.FixedSun,
                    false,
                    (float)Scene.RegionInfo.EstateSettings.SunPosition);

//            sendRegionInfoPacketToAll(); already done by setRegionTerrainSettings
        }


    #endregion

        private void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnDetailedEstateDataRequest += ClientSendDetailedEstateData;
            client.OnSetEstateFlagsRequest += EstateSetRegionInfoHandler;
//            client.OnSetEstateTerrainBaseTexture += setEstateTerrainBaseTexture;
            client.OnSetEstateTerrainDetailTexture += SetEstateTerrainBaseTexture;
            client.OnSetEstateTerrainTextureHeights += SetEstateTerrainTextureHeights;
            client.OnCommitEstateTerrainTextureRequest += HandleCommitEstateTerrainTextureRequest;
            client.OnSetRegionTerrainSettings += SetRegionTerrainSettings;
            client.OnEstateRestartSimRequest += HandleEstateRestartSimRequest;
            client.OnEstateChangeCovenantRequest += HandleChangeEstateCovenantRequest;
            client.OnEstateChangeInfo += HandleEstateChangeInfo;
            client.OnEstateManageTelehub += HandleOnEstateManageTelehub;
            client.OnUpdateEstateAccessDeltaRequest += HandleEstateAccessDeltaRequest;
            client.OnSimulatorBlueBoxMessageRequest += SendSimulatorBlueBoxMessage;
            client.OnEstateBlueBoxMessageRequest += SendEstateBlueBoxMessage;
            client.OnEstateDebugRegionRequest += HandleEstateDebugRegionRequest;
            client.OnEstateTeleportOneUserHomeRequest += HandleEstateTeleportOneUserHomeRequest;
            client.OnEstateTeleportAllUsersHomeRequest += HandleEstateTeleportAllUsersHomeRequest;
            client.OnRequestTerrain += HandleTerrainRequest;
            client.OnUploadTerrain += HandleUploadTerrain;

            client.OnRegionInfoRequest += HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += HandleEstateCovenantRequest;
            client.OnLandStatRequest += HandleLandStatRequest;
        }

        public uint GetEstateFlags()
        {
            RegionFlags flags = RegionFlags.None;

            if (Scene.RegionInfo.EstateSettings.AllowLandmark)
                flags |= RegionFlags.AllowLandmark;
            if (Scene.RegionInfo.EstateSettings.AllowSetHome)
                flags |= RegionFlags.AllowSetHome;
            if (Scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= RegionFlags.ResetHomeOnTeleport;
            //if (Scene.RegionInfo.EstateSettings.FixedSun)
            //    flags |= RegionFlags.SunFixed;
            if (!Scene.RegionInfo.EstateSettings.TaxFree) // this is now wrong means !ALLOW_ACCESS_OVERRIDE
                flags |= RegionFlags.AllowParcelAccessOverride;

            if (Scene.RegionInfo.EstateSettings.PublicAccess) //??
                flags |= (RegionFlags.PublicAllowed | RegionFlags.ExternallyVisible);

            if (Scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= RegionFlags.BlockDwell;
            if (Scene.RegionInfo.EstateSettings.AllowDirectTeleport)
                flags |= RegionFlags.AllowDirectTeleport;
            if (Scene.RegionInfo.EstateSettings.EstateSkipScripts)
                flags |= RegionFlags.EstateSkipScripts;

            if (Scene.RegionInfo.EstateSettings.DenyAnonymous)
                flags |= RegionFlags.DenyAnonymous;
            if (Scene.RegionInfo.EstateSettings.DenyIdentified) // unused?
                flags |= RegionFlags.DenyIdentified;
            if (Scene.RegionInfo.EstateSettings.DenyTransacted) // unused?
                flags |= RegionFlags.DenyTransacted;
            if (Scene.RegionInfo.EstateSettings.AllowParcelChanges)
                flags |= RegionFlags.AllowParcelChanges;
            if (Scene.RegionInfo.EstateSettings.AbuseEmailToEstateOwner) // now is block fly
                flags |= RegionFlags.AbuseEmailToEstateOwner;
            if (Scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;

            if (Scene.RegionInfo.EstateSettings.DenyMinors)
                flags |= RegionFlags.DenyAgeUnverified;

            if (Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride)
                flags |= RegionFlags.AllowEnvironmentOverride;

            return (uint)flags;
        }

        public void TriggerEstateMessage(UUID fromID, string fromName, string message)
        {
            OnEstateMessage?.Invoke(Scene.RegionInfo.RegionID, fromID, fromName, message);
        }


        private void SendTelehubInfo(IClientAPI client)
        {
            RegionSettings settings =
                    this.Scene.RegionInfo.RegionSettings;

            SceneObjectGroup telehub = null;
            if (!settings.TelehubObject.IsZero() &&
                (telehub = Scene.GetSceneObjectGroup(settings.TelehubObject)) != null)
            {
                List<Vector3> spawnPoints = new List<Vector3>();

                foreach (SpawnPoint sp in settings.SpawnPoints())
                {
                    spawnPoints.Add(sp.GetLocation(Vector3.Zero, Quaternion.Identity));
                }

                client.SendTelehubInfo(settings.TelehubObject,
                                       telehub.Name,
                                       telehub.AbsolutePosition,
                                       telehub.GroupRotation,
                                       spawnPoints);
            }
            else
            {
                client.SendTelehubInfo(UUID.Zero,
                                       String.Empty,
                                       Vector3.Zero,
                                       Quaternion.Identity,
                                       new List<Vector3>());
            }
        }
    }
}

