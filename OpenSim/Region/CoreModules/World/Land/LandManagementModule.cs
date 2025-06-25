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
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

using Extension = Mono.Addins.ExtensionAttribute;
namespace OpenSim.Region.CoreModules.World.Land
{
    // used for caching
    internal class ExtendedLandData
    {
        public LandData LandData;
        public ulong RegionHandle;
        public uint X, Y;
        public byte RegionAccess;
    }

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LandManagementModule")]
    public class LandManagementModule : INonSharedRegionModule , ILandChannel
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        //private LandChannel m_landChannel;

        private ulong m_regionHandler;
        private int m_regionSizeX;
        private int m_regionSizeY;

        protected IGroupsModule m_groupManager;
        protected IUserManagement m_userManager;
        protected IPrimCountModule m_primCountModule;
        protected IDialogModule m_Dialog;

        /// <value>
        /// Local land ids at specified region co-ordinates (region size / 4)
        /// </value>
        private int[,] m_landIDList;

        /// <value>
        /// Land objects keyed by local id
        /// </value>

        private readonly Dictionary<int, ILandObject> m_landList = new();
        private readonly Dictionary<UUID, int> m_landGlobalIDs = new();
        private readonly Dictionary<UUID, int> m_landFakeIDs = new();

        private int m_lastLandLocalID = LandChannel.START_LAND_LOCAL_ID - 1;

        private bool m_allowedForcefulBans = true;
        private bool m_showBansLines = true;
        private UUID DefaultGodParcelGroup;
        private string DefaultGodParcelName;
        private UUID DefaultGodParcelOwner;

        // caches ExtendedLandData
        static private readonly ExpiringCacheOS<UUID,ExtendedLandData> m_parcelInfoCache = new(10000);

        /// <summary>
        /// Record positions that avatar's are currently being forced to move to due to parcel entry restrictions.
        /// </summary>
        private readonly HashSet<UUID> forcedPosition = new();

        // Enables limiting parcel layer info transmission when doing simple updates
        private bool shouldLimitParcelLayerInfoToViewDistance { get; set; }
        // "View distance" for sending parcel layer info if asked for from a view point in the region
        private int parcelLayerViewDistance { get; set; }

        private float m_BanLineSafeHeight = 100.0f;
        public float BanLineSafeHeight
        {
            get { return m_BanLineSafeHeight; }
            private set
            {
                if (value > 20f && value <= 5000f)
                    m_BanLineSafeHeight = value;
                else
                    m_BanLineSafeHeight = 100.0f;
            }
        }

        #region INonSharedRegionModule Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            shouldLimitParcelLayerInfoToViewDistance = true;
            parcelLayerViewDistance = 128;
            IConfig landManagementConfig = source.Configs["LandManagement"];
            if (landManagementConfig is not null)
            {
                shouldLimitParcelLayerInfoToViewDistance = landManagementConfig.GetBoolean("LimitParcelLayerUpdateDistance", shouldLimitParcelLayerInfoToViewDistance);
                parcelLayerViewDistance = landManagementConfig.GetInt("ParcelLayerViewDistance", parcelLayerViewDistance);
                DefaultGodParcelGroup = new UUID(landManagementConfig.GetString("DefaultAdministratorGroupUUID", UUID.Zero.ToString()));
                DefaultGodParcelName = landManagementConfig.GetString("DefaultAdministratorParcelName", "Admin Parcel");
                DefaultGodParcelOwner = new UUID(landManagementConfig.GetString("DefaultAdministratorOwnerUUID", UUID.Zero.ToString()));
                bool disablebans = landManagementConfig.GetBoolean("DisableParcelBans", !m_allowedForcefulBans);
                m_allowedForcefulBans = !disablebans;
                m_showBansLines = landManagementConfig.GetBoolean("ShowParcelBansLines", m_showBansLines);
                m_BanLineSafeHeight = landManagementConfig.GetFloat("BanLineSafeHeight", m_BanLineSafeHeight);
                if(!m_allowedForcefulBans)
                    m_showBansLines = false;
            }
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_regionHandler = m_scene.RegionInfo.RegionHandle;
            m_regionSizeX = (int)m_scene.RegionInfo.RegionSizeX;
            m_regionSizeY = (int)m_scene.RegionInfo.RegionSizeY;
            m_landIDList = new int[m_regionSizeX / Constants.LandUnit, m_regionSizeY / Constants.LandUnit];

            m_scene.LandChannel = this;

            m_scene.EventManager.OnObjectAddedToScene += EventManagerOnParcelPrimCountAdd;
            m_scene.EventManager.OnParcelPrimCountAdd += EventManagerOnParcelPrimCountAdd;

            m_scene.EventManager.OnObjectBeingRemovedFromScene += EventManagerOnObjectBeingRemovedFromScene;
            m_scene.EventManager.OnParcelPrimCountUpdate += EventManagerOnParcelPrimCountUpdate;
            m_scene.EventManager.OnRequestParcelPrimCountUpdate += EventManagerOnRequestParcelPrimCountUpdate;

            m_scene.EventManager.OnAvatarEnteringNewParcel += EventManagerOnAvatarEnteringNewParcel;
            m_scene.EventManager.OnClientMovement += EventManagerOnClientMovement;
            m_scene.EventManager.OnValidateLandBuy += EventManagerOnValidateLandBuy;
            m_scene.EventManager.OnLandBuy += EventManagerOnLandBuy;
            m_scene.EventManager.OnNewClient += EventManagerOnNewClient;
            m_scene.EventManager.OnMakeChildAgent += EventMakeChildAgent;
            m_scene.EventManager.OnSignificantClientMovement += EventManagerOnSignificantClientMovement;
            m_scene.EventManager.OnNoticeNoLandDataFromStorage += EventManagerOnNoLandDataFromStorage;
            m_scene.EventManager.OnIncomingLandDataFromStorage += EventManagerOnIncomingLandDataFromStorage;
            m_scene.EventManager.OnSetAllowForcefulBan += EventManagerOnSetAllowedForcefulBan;
            m_scene.EventManager.OnRegisterCaps += EventManagerOnRegisterCaps;

            RegisterCommands();
        }

        public void RegionLoaded(Scene scene)
        {
            m_userManager = m_scene.RequestModuleInterface<IUserManagement>();
            m_groupManager = m_scene.RequestModuleInterface<IGroupsModule>();
            m_primCountModule = m_scene.RequestModuleInterface<IPrimCountModule>();
            m_Dialog = m_scene.RequestModuleInterface<IDialogModule>();
        }

        public void RemoveRegion(Scene scene)
        {
            // TODO: Release event manager listeners here
        }

        /*
        private bool OnVerifyUserConnection(ScenePresence scenePresence, out string reason)
        {
            ILandObject nearestParcel = m_scene.GetNearestAllowedParcel(scenePresence.UUID, scenePresence.AbsolutePosition.X, scenePresence.AbsolutePosition.Y);
                "You are not allowed to enter this sim.";
            return nearestParcel != null;
        }
        */

        void EventManagerOnNewClient(IClientAPI client)
        {
            //Register some client events
            client.OnParcelPropertiesRequest += ClientOnParcelPropertiesRequest;
            client.OnParcelDivideRequest += ClientOnParcelDivideRequest;
            client.OnParcelJoinRequest += ClientOnParcelJoinRequest;
            client.OnParcelPropertiesUpdateRequest += ClientOnParcelPropertiesUpdateRequest;
            client.OnParcelSelectObjects += ClientOnParcelSelectObjects;
            client.OnParcelObjectOwnerRequest += ClientOnParcelObjectOwnerRequest;
            client.OnParcelAccessListRequest += ClientOnParcelAccessListRequest;
            client.OnParcelAccessListUpdateRequest += ClientOnParcelAccessListUpdateRequest;
            client.OnParcelAbandonRequest += ClientOnParcelAbandonRequest;
            client.OnParcelGodForceOwner += ClientOnParcelGodForceOwner;
            client.OnParcelReclaim += ClientOnParcelReclaim;
            client.OnParcelInfoRequest += ClientOnParcelInfoRequest;
            client.OnParcelDeedToGroup += ClientOnParcelDeedToGroup;
            client.OnParcelEjectUser += ClientOnParcelEjectUser;
            client.OnParcelFreezeUser += ClientOnParcelFreezeUser;
            client.OnSetStartLocationRequest += ClientOnSetHome;
            client.OnParcelBuyPass += ClientParcelBuyPass;
            client.OnParcelGodMark += ClientOnParcelGodMark;
        }

        public void EventMakeChildAgent(ScenePresence avatar)
        {
            avatar.currentParcelUUID = UUID.Zero;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LandManagementModule"; }
        }

        #endregion

        #region Parcel Add/Remove/Get/Create

        public void EventManagerOnSetAllowedForcefulBan(bool forceful)
        {
            AllowedForcefulBans = forceful;
        }

        public void UpdateLandObject(int local_id, LandData data)
        {
            LandData newData = data.Copy();
            newData.LocalID = local_id;

            ILandObject land;
            lock (m_landList)
            {
                if (m_landList.TryGetValue(local_id, out land))
                {
                    m_landGlobalIDs.Remove(land.LandData.GlobalID);
                    if (land.LandData.FakeID.IsNotZero())
                        m_landFakeIDs.Remove(land.LandData.FakeID);
                    land.LandData = newData;
                    m_landGlobalIDs[newData.GlobalID] = local_id;
                    if (newData.FakeID.IsNotZero())
                        m_landFakeIDs[newData.FakeID] = local_id;
                }
                else
                    return;
            }

            m_scene.EventManager.TriggerLandObjectUpdated((uint)local_id, land);
        }

        public bool IsForcefulBansAllowed()
        {
            return AllowedForcefulBans;
        }

        public bool AllowedForcefulBans
        {
            get { return m_allowedForcefulBans; }
            set { m_allowedForcefulBans = value; }
        }

        /// <summary>
        /// Resets the sim to the default land object (full sim piece of land owned by the default user)
        /// </summary>
        public void ResetSimLandObjects()
        {
            //Remove all the land objects in the sim and add a blank, full sim land object set to public
            lock (m_landList)
            {
                foreach(ILandObject parcel in m_landList.Values)
                    parcel.Clear();

                m_landList.Clear();
                m_landGlobalIDs.Clear();
                m_landFakeIDs.Clear();
                m_lastLandLocalID = LandChannel.START_LAND_LOCAL_ID - 1;

                m_landIDList = new int[m_regionSizeX / Constants.LandUnit, m_regionSizeY / Constants.LandUnit];
            }
        }

        /// <summary>
        /// Create a default parcel that spans the entire region and is owned by the estate owner.
        /// </summary>
        /// <returns>The parcel created.</returns>
        protected ILandObject CreateDefaultParcel()
        {
            m_log.Debug("[LAND MANAGEMENT MODULE]: Creating default parcel for region " + m_scene.RegionInfo.RegionName);

            ILandObject fullSimParcel = new LandObject(UUID.Zero, false, m_scene);

            fullSimParcel.SetLandBitmap(fullSimParcel.GetSquareLandBitmap(0, 0, m_regionSizeX, m_regionSizeY));
            LandData ldata = fullSimParcel.LandData;
            ldata.SimwideArea = ldata.Area;
            ldata.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
            ldata.ClaimDate = Util.UnixTimeSinceEpoch();

            return AddLandObject(fullSimParcel);
        }

        public List<ILandObject> AllParcels()
        {
            lock (m_landList)
            {
                return new List<ILandObject>(m_landList.Values);
            }
        }

        public List<ILandObject> ParcelsNearPoint(Vector3 position)
        {
            List<ILandObject> parcelsNear = new();
            for (int x = -8; x <= 8; x += 4)
            {
                for (int y = -8; y <= 8; y += 4)
                {
                    ILandObject check = GetLandObject(position.X + x, position.Y + y);
                    if (check is not null)
                    {
                        if (!parcelsNear.Contains(check))
                        {
                            parcelsNear.Add(check);
                        }
                    }
                }
            }

            return parcelsNear;
        }

        // checks and enforces bans or restrictions
        // returns true if enforced
        public bool EnforceBans(ILandObject land, ScenePresence avatar)
        {
            Vector3 agentpos = avatar.AbsolutePosition;
            float h = m_scene.GetGroundHeight(agentpos.X, agentpos.Y) + m_scene.LandChannel.BanLineSafeHeight;
            float zdif = avatar.AbsolutePosition.Z - h;
            if (zdif > 0 )
            {
                forcedPosition.Remove(avatar.UUID);
                avatar.lastKnownAllowedPosition = agentpos;
                return false;
            }

            bool ban = false;
            string reason = "";
            if (land.IsRestrictedFromLand(avatar.UUID))
            {
                reason = "You do not have access to the parcel";
                ban = true;
            }

            if (land.IsBannedFromLand(avatar.UUID))
            {
                if ( m_allowedForcefulBans)
                {
                   reason ="You are banned from parcel";
                   ban = true;
                }
                else if(!ban)
                {
                    if (forcedPosition.Contains(avatar.UUID))
                        avatar.ControllingClient.SendAlertMessage("You are banned from parcel, please leave by your own will");
                    forcedPosition.Remove(avatar.UUID);
                    avatar.lastKnownAllowedPosition = agentpos;
                    return false;
                }
            }

            if(ban)
            {
                if (!forcedPosition.Contains(avatar.UUID))
                    avatar.ControllingClient.SendAlertMessage(reason);

                if(zdif > -4f)
                {

                    agentpos.Z = h + 4.0f;
                    ForceAvatarToPosition(avatar, agentpos);
                    return true;
                }

                if (land.ContainsPoint((int)avatar.lastKnownAllowedPosition.X,
                            (int) avatar.lastKnownAllowedPosition.Y))
                {
                    Vector3? pos = m_scene.GetNearestAllowedPosition(avatar);
                    if (pos is null)
                    {
                         forcedPosition.Remove(avatar.UUID);
                         m_scene.TeleportClientHome(avatar.UUID, avatar.ControllingClient);
                    }
                    else
                        ForceAvatarToPosition(avatar, (Vector3)pos);
                }
                else
                {
                    ForceAvatarToPosition(avatar, avatar.lastKnownAllowedPosition);
                }
                return true;
            }
            else
            {
                forcedPosition.Remove(avatar.UUID);
                avatar.lastKnownAllowedPosition = agentpos;
                return false;
            }
        }

        private void ForceAvatarToPosition(ScenePresence avatar, Vector3? position)
        {
            if (m_scene.Permissions.IsGod(avatar.UUID)) return;
            if (!position.HasValue) return;

            if(avatar.MovingToTarget)
                avatar.ResetMoveToTarget();
            avatar.AbsolutePosition = position.Value;
            avatar.lastKnownAllowedPosition = position.Value;
            avatar.Velocity = Vector3.Zero;
            if(avatar.IsSitting)
                avatar.StandUp();
            forcedPosition.Add(avatar.UUID);
        }

        public void EventManagerOnAvatarEnteringNewParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            if (m_scene.RegionInfo.RegionID.Equals(regionID))
            {
                ILandObject parcelAvatarIsEntering;
                lock (m_landList)
                {
                    parcelAvatarIsEntering = m_landList[localLandID];
                }

                if (parcelAvatarIsEntering is not null &&
                    avatar.currentParcelUUID.NotEqual(parcelAvatarIsEntering.LandData.GlobalID))
                {
                    SendLandUpdate(avatar, parcelAvatarIsEntering);
                    avatar.currentParcelUUID = parcelAvatarIsEntering.LandData.GlobalID;
                    EnforceBans(parcelAvatarIsEntering, avatar);
                }
            }
        }

        public void SendOutNearestBanLine(IClientAPI client)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp is null || sp.IsDeleted)
                return;

            List<ILandObject> checkLandParcels = ParcelsNearPoint(sp.AbsolutePosition);
            foreach (ILandObject checkBan in checkLandParcels)
            {
                if (checkBan.IsBannedFromLand(client.AgentId))
                {
                    checkBan.SendLandProperties((int)ParcelPropertiesStatus.CollisionBanned, false, (int)ParcelResult.Single, client);
                    return; //Only send one
                }
                if (checkBan.IsRestrictedFromLand(client.AgentId))
                {
                    checkBan.SendLandProperties((int)ParcelPropertiesStatus.CollisionNotOnAccessList, false, (int)ParcelResult.Single, client);
                    return; //Only send one
                }
            }
            return;
        }

        public void sendClientInitialLandInfo(IClientAPI remoteClient, bool overlay)
        {
            if (!m_scene.TryGetScenePresence(remoteClient.AgentId, out ScenePresence avatar))
                return;

            if (!avatar.IsChildAgent)
            {
                ILandObject over = GetLandObject(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
                if (over is null)
                    return;

                avatar.currentParcelUUID = over.LandData.GlobalID;
                over.SendLandUpdateToClient(avatar.ControllingClient);
            }
            if (overlay)
                SendParcelOverlay(remoteClient);
        }

        public void SendLandUpdate(ScenePresence avatar, ILandObject over)
        {
            if (avatar.IsChildAgent)
                return;

            if (over is not null)
            {
                over.SendLandUpdateToClient(avatar.ControllingClient);
                // sl doesnt seem to send this now, as it used 2
                //SendParcelOverlay(avatar.ControllingClient);
            }
        }

        public void EventManagerOnSignificantClientMovement(ScenePresence avatar)
        {
            if (avatar.IsChildAgent || avatar.IsNPC)
                return;

            if (m_showBansLines && !m_scene.RegionInfo.EstateSettings.TaxFree)
                SendOutNearestBanLine(avatar.ControllingClient);
        }

        /// <summary>
        /// Like handleEventManagerOnSignificantClientMovement, but called with an AgentUpdate regardless of distance.
        /// </summary>
        /// <param name="avatar"></param>
        public void EventManagerOnClientMovement(ScenePresence avatar)
        {
            if (avatar.IsChildAgent)
                return;

            Vector3 pos = avatar.AbsolutePosition;
            ILandObject over = GetLandObject(pos.X, pos.Y);
            if (over is not null)
            {
                EnforceBans(over, avatar);
                pos = avatar.AbsolutePosition;
                ILandObject newover = GetLandObject(pos.X, pos.Y);
                if(over != newover || avatar.currentParcelUUID.NotEqual(newover.LandData.GlobalID))
                {
                    m_scene.EventManager.TriggerAvatarEnteringNewParcel(avatar,
                            newover.LandData.LocalID, m_scene.RegionInfo.RegionID);
                }
            }
        }

        public void ClientParcelBuyPass(IClientAPI remote_client, UUID targetID, int landLocalID)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(landLocalID, out land);
            }
            // trivial checks
            if(land is null)
                return;

            LandData ldata = land.LandData;
            if(ldata is null)
                return;

            if (ldata.PassHours == 0)
                return;

            if (ldata.OwnerID.Equals(targetID))
                return;

            if (m_scene.RegionInfo.EstateSettings.TaxFree)
                return;

            // don't allow passes on group owned until we can give money to groups
            if (ldata.IsGroupOwned)
            {
                remote_client.SendAgentAlertMessage("pass to group owned parcel not suported", false);
                return;
            }

            if((ldata.Flags & (uint)ParcelFlags.UsePassList) == 0)
                return;

            int cost = ldata.PassPrice;

            int idx = land.LandData.ParcelAccessList.FindIndex(
                delegate(LandAccessEntry e)
                {
                    if (e.Flags == AccessList.Access && e.AgentID.Equals(targetID))
                        return true;
                    return false;
                });
            int now = Util.UnixTimeSinceEpoch();
            int expires = (int)(3600.0 * ldata.PassHours + 0.5f);
            int currenttime = -1;
            if (idx != -1)
            {
                if(ldata.ParcelAccessList[idx].Expires == 0)
                {
                    remote_client.SendAgentAlertMessage("You already have access to parcel", false);
                    return;
                }

                currenttime = ldata.ParcelAccessList[idx].Expires - now;
                if(currenttime > (int)(0.25f * expires + 0.5f))
                {
                    if(currenttime > 3600)
                        remote_client.SendAgentAlertMessage(string.Format("You already have a pass valid for {0:0.###} hours",
                                    currenttime/3600f), false);
                   else if(currenttime > 60)
                        remote_client.SendAgentAlertMessage(string.Format("You already have a pass valid for {0:0.##} minutes",
                                    currenttime/60f), false);
                   else
                        remote_client.SendAgentAlertMessage(string.Format("You already have a pass valid for {0:0.#} seconds",
                                    currenttime), false);
                    return;
                }
            }

            LandAccessEntry entry = new()
            {
                AgentID = targetID,
                Flags = AccessList.Access,
                Expires = now + expires
            };
            if (currenttime > 0)
                entry.Expires += currenttime;
            IMoneyModule mm = m_scene.RequestModuleInterface<IMoneyModule>();
            if(cost != 0 && mm is not null)
            {
                WorkManager.RunInThreadPool(
                delegate
                {
                    string regionName = m_scene.RegionInfo.RegionName;

                    if (!mm.AmountCovered(remote_client.AgentId, cost))
                    {
                        remote_client.SendAgentAlertMessage($"Insufficient funds in region '{regionName}' money system", true); 
                        return;
                    }

                    string payDescription = String.Format("Parcel '{0}' at region '{1} {2:0.###} hours access pass", ldata.Name, regionName, ldata.PassHours);

                    if(!mm.MoveMoney(remote_client.AgentId, ldata.OwnerID, cost,MoneyTransactionType.LandPassSale, payDescription))
                    {
                        remote_client.SendAgentAlertMessage("Sorry pass payment processing failed, please try again later", true); 
                        return;
                    }

                    if (idx != -1)
                        ldata.ParcelAccessList.RemoveAt(idx);
                    ldata.ParcelAccessList.Add(entry);
                    m_scene.EventManager.TriggerLandObjectUpdated((uint)land.LandData.LocalID, land);
                    return;
                }, null, "ParcelBuyPass");
            }
            else
            {
                if (idx != -1)
                    ldata.ParcelAccessList.RemoveAt(idx);
                ldata.ParcelAccessList.Add(entry);
                m_scene.EventManager.TriggerLandObjectUpdated((uint)land.LandData.LocalID, land);
            }
        }

        public void ClientOnParcelAccessListRequest(UUID agentID, UUID sessionID, uint flags, int sequenceID,
                                                    int landLocalID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(landLocalID, out land);
            }
            land?.SendAccessList(agentID, sessionID, flags, sequenceID, remote_client);
        }

        public void ClientOnParcelAccessListUpdateRequest(UUID agentID,
                uint flags, UUID transactionID, int landLocalID, List<LandAccessEntry> entries,
                IClientAPI remote_client)
        {
            if ((flags & 0x03) == 0)
                return; // we only have access and ban

            if(m_scene.RegionInfo.EstateSettings.TaxFree)
                return;

            ILandObject land;
            lock (m_landList)
            {
                _ = m_landList.TryGetValue(landLocalID, out land);
            }

            if (land is not null)
            {
                GroupPowers requiredPowers = GroupPowers.None;
                if ((flags & (uint)AccessList.Access) != 0)
                    requiredPowers |= GroupPowers.LandManageAllowed;
                if ((flags & (uint)AccessList.Ban) != 0)
                    requiredPowers |= GroupPowers.LandManageBanned;

                if(requiredPowers == GroupPowers.None)
                    return;

                if (m_scene.Permissions.CanEditParcelProperties(agentID,
                        land, requiredPowers, false))
                {
                    land.UpdateAccessList(flags, transactionID, entries);
                }
            }
            else
            {
                m_log.Warn("[LAND MANAGEMENT MODULE]: Invalid local land ID " + landLocalID.ToString());
            }
        }

        /// <summary>
        /// Adds a land object to the stored list and adds them to the landIDList to what they own
        /// </summary>
        /// <param name="new_land">
        /// The land object being added.
        /// Will return null if this overlaps with an existing parcel that has not had its bitmap adjusted.
        /// </param>
        public ILandObject AddLandObject(ILandObject new_land)
        {
            // Only now can we add the prim counts to the land object - we rely on the global ID which is generated
            // as a random UUID inside LandData initialization
            if (m_primCountModule is not null)
                new_land.PrimCounts = m_primCountModule.GetPrimCounts(new_land.LandData.GlobalID);

            lock (m_landList)
            {
                int newLandLocalID = m_lastLandLocalID + 1;
                new_land.LandData.LocalID = newLandLocalID;

                bool[,] landBitmap = new_land.GetLandBitmap();
                if (landBitmap.GetLength(0) != m_landIDList.GetLength(0) || landBitmap.GetLength(1) != m_landIDList.GetLength(1))
                {
                    // Going to variable sized regions can cause mismatches
                    m_log.ErrorFormat("[LAND MANAGEMENT MODULE]: Added land bitmap has different size than region ID map. bitmapSize=({0},{1}), landIDSize=({2},{3})",
                        landBitmap.GetLength(0), landBitmap.GetLength(1), m_landIDList.GetLength(0), m_landIDList.GetLength(1));
                }
                else
                {
                    // If other land objects still believe that they occupy any parts of the same space,
                    // then do not allow the add to proceed.
                    for (int x = 0; x < landBitmap.GetLength(0); x++)
                    {
                        for (int y = 0; y < landBitmap.GetLength(1); y++)
                        {
                            if (landBitmap[x, y])
                            {
                                int lastRecordedLandId = m_landIDList[x, y];

                                if (lastRecordedLandId > 0)
                                {
                                    ILandObject lastRecordedLo = m_landList[lastRecordedLandId];

                                    if (lastRecordedLo.LandBitmap[x, y])
                                    {
                                        m_log.ErrorFormat(
                                            "[LAND MANAGEMENT MODULE]: Cannot add parcel \"{0}\", local ID {1} at tile {2},{3} because this is still occupied by parcel \"{4}\", local ID {5} in {6}",
                                            new_land.LandData.Name, new_land.LandData.LocalID, x, y,
                                            lastRecordedLo.LandData.Name, lastRecordedLo.LandData.LocalID, m_scene.Name);

                                        return null;
                                    }
                                }
                            }
                        }
                    }

                    for (int x = 0; x < landBitmap.GetLength(0); x++)
                    {
                        for (int y = 0; y < landBitmap.GetLength(1); y++)
                        {
                            if (landBitmap[x, y])
                            {
                                //m_log.DebugFormat(
                                //    "[LAND MANAGEMENT MODULE]: Registering parcel {0} for land co-ord ({1}, {2}) on {3}",
                                //    new_land.LandData.Name, x, y, m_scene.RegionInfo.RegionName);

                                m_landIDList[x, y] = newLandLocalID;
                            }
                        }
                    }
                }
                
                m_landList.Add(newLandLocalID, new_land);
                m_landGlobalIDs[new_land.LandData.GlobalID] = newLandLocalID;
                m_landFakeIDs[new_land.LandData.FakeID] = newLandLocalID;
                m_lastLandLocalID++;
            }

            new_land.ForceUpdateLandInfo();
            m_scene.EventManager.TriggerLandObjectAdded(new_land);

            return new_land;
        }

        /// <summary>
        /// Removes a land object from the list. Will not remove if local_id is still owning an area in landIDList
        /// </summary>
        /// <param name="local_id">Land.localID of the peice of land to remove.</param>
        public void removeLandObject(int local_id)
        {
            ILandObject land;
            UUID landGlobalID = UUID.Zero;
            lock (m_landList)
            {
                for (int x = 0; x < m_landIDList.GetLength(0); x++)
                {
                    for (int y = 0; y < m_landIDList.GetLength(1); y++)
                    {
                        if (m_landIDList[x, y] == local_id)
                        {
                            m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Not removing land object {0}; still being used at {1}, {2}",
                                             local_id, x, y);
                            return;
                            //throw new Exception("Could not remove land object. Still being used at " + x + ", " + y);
                        }
                    }
                }

                land = m_landList[local_id];
                m_landList.Remove(local_id);
                if(land is not null && land.LandData is not null)
                {
                    landGlobalID = land.LandData.GlobalID;
                    m_landGlobalIDs.Remove(landGlobalID);
                    m_landFakeIDs.Remove(land.LandData.FakeID);
                }
            }

            if(landGlobalID.IsNotZero())
            {
                m_scene.EventManager.TriggerLandObjectRemoved(landGlobalID);
                land.Clear();
            }
        }

        /// <summary>
        /// Clear the scene of all parcels
        /// </summary>
        public void Clear(bool setupDefaultParcel)
        {
            List<UUID> landworkList = new(m_landList.Count);
            // move to work pointer since we are deleting it all
            lock (m_landList)
            {
                foreach (ILandObject lo in m_landList.Values)
                    landworkList.Add(lo.LandData.GlobalID);
            }

            // this 2 methods have locks (now)
            ResetSimLandObjects();

            if (setupDefaultParcel)
                CreateDefaultParcel();

            // fire outside events unlocked
            foreach (UUID id in landworkList)
            {
                //m_scene.SimulationDataService.RemoveLandObject(lo.LandData.GlobalID);
                m_scene.EventManager.TriggerLandObjectRemoved(id);
            }
            landworkList.Clear();
        }

        private void performFinalLandJoin(ILandObject master, ILandObject slave)
        {
            bool[,] landBitmapSlave = slave.GetLandBitmap();
            lock (m_landList)
            {
                for (int x = 0; x < landBitmapSlave.GetLength(0); x++)
                {
                    for (int y = 0; y < landBitmapSlave.GetLength(1); y++)
                    {
                        if (landBitmapSlave[x, y])
                        {
                            m_landIDList[x, y] = master.LandData.LocalID;
                        }
                    }
                }
            }
            master.LandData.Dwell += slave.LandData.Dwell;
            removeLandObject(slave.LandData.LocalID);
            UpdateLandObject(master.LandData.LocalID, master.LandData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(UUID globalID)
        {
            lock (m_landList)
            {
                if (m_landGlobalIDs.TryGetValue(globalID, out int lid))
                {
                    if (m_landList.TryGetValue(lid, out ILandObject land))
                        return land;
                    else
                        m_landGlobalIDs.Remove(globalID);
                }
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObjectByfakeID(UUID fakeID)
        {
            lock (m_landList)
            {
                if (m_landFakeIDs.TryGetValue(fakeID, out int lid))
                {
                    if (m_landList.TryGetValue(lid, out ILandObject land))
                        return land;
                    else
                        m_landFakeIDs.Remove(fakeID);
                }
            }
            if(Util.ParseFakeParcelID(fakeID, out ulong rhandle, out uint x, out uint y) && rhandle == m_regionHandler)
            {
                return GetLandObjectClippedXY(x, y);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(int parcelLocalID)
        {
            lock (m_landList)
            {
                return m_landList.TryGetValue(parcelLocalID, out ILandObject land) ? land : null;
            }
        }

        /// <summary>
        /// Get the land object at the specified point
        /// </summary>
        /// <param name="x_float">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y_float">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Land object at the point supplied</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(float x_float, float y_float)
        {
            return GetLandObject((int)x_float, (int)y_float, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(Vector3 position)
        {
            return GetLandObject(position.X, position.Y);
        }

        // if x,y is off region this will return the parcel at cliped x,y
        // as did code it replaces
        public ILandObject GetLandObjectClippedXY(float x, float y)
        {
            int avx = (int)MathF.Round(x);
            if (avx < 0)
                avx = 0;
            else 
            {
                if (avx >= m_regionSizeX) 
                    avx = m_regionSizeX - 1;
                avx /= Constants.LandUnit;
            }

            int avy = (int)MathF.Round(y);
            if (avy < 0)
                avy = 0;
            else 
            {
                if (avy >= m_regionSizeY)
                    avy = m_regionSizeY - 1;
                avy /= Constants.LandUnit;
            }

            lock (m_landIDList)
            {
                try
                {
                    return m_landList[m_landIDList[avx, avy]];
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
            }
        }

        // Public entry.
        // Throws exception if land object is not found
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(int x, int y)
        {
            return GetLandObject(x, y, false /* returnNullIfLandObjectNotFound */);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(int x, int y, bool returnNullIfLandObjectOutsideBounds)
        {
            if (x >= m_regionSizeX || y >= m_regionSizeY || x < 0 || y < 0)
            {
                // These exceptions here will cause a lot of complaints from the users specifically because
                // they happen every time at border crossings
                if (returnNullIfLandObjectOutsideBounds)
                    return null;
                else
                    throw new Exception("Error: Parcel not found at point " + x + ", " + y);
            }

            if(m_landList.Count == 0  || m_landIDList is null)
                return null;

            lock (m_landIDList)
            {
                try
                {
                    return m_landList[m_landIDList[x / Constants.LandUnit, y / Constants.LandUnit]];
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObjectinLandUnits(int x, int y)
        {
            if (m_landList.Count == 0 || m_landIDList is null)
                return null;

            lock (m_landIDList)
            {
                try
                {
                    return m_landList[m_landIDList[x, y]];
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObjectinLandUnitsInt(int x, int y)
        {
            lock (m_landIDList)
            {
                try
                {
                    return m_landList[m_landIDList[x, y]];
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLandObjectIDinLandUnits(int x, int y)
        {
            lock (m_landIDList)
            {
                try
                {
                    return m_landIDList[x, y];
                }
                catch (IndexOutOfRangeException)
                {
                    return -1;
                }
            }
        }

        // Create a 'parcel is here' bitmap for the parcel identified by the passed landID
        private bool[,] CreateBitmapForID(int landID)
        {
            bool[,] ret = new bool[m_landIDList.GetLength(0), m_landIDList.GetLength(1)];

            for (int xx = 0; xx < m_landIDList.GetLength(0); xx++)
                for (int yy = 0; yy < m_landIDList.GetLength(1); yy++)
                    if (m_landIDList[xx, yy] == landID)
                        ret[xx, yy] = true;

            return ret;
        }

        #endregion

        #region Parcel Modification

        public void ResetOverMeRecords()
        {
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    p.ResetOverMeRecord();
                }
            }
        }

        public void EventManagerOnParcelPrimCountAdd(SceneObjectGroup obj)
        {
            Vector3 position = obj.AbsolutePosition;
            ILandObject landUnderPrim = GetLandObject(position.X, position.Y);
            if (landUnderPrim is not null)
            {
                ((LandObject)landUnderPrim).AddPrimOverMe(obj);
            }
        }

        public void EventManagerOnObjectBeingRemovedFromScene(SceneObjectGroup obj)
        {
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    p.RemovePrimFromOverMe(obj);
                }
            }
        }

        private void FinalizeLandPrimCountUpdate()
        {
            //Get Simwide prim count for owner
            Dictionary<UUID, List<LandObject>> landOwnersAndParcels = new();
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    if (!landOwnersAndParcels.TryGetValue(p.LandData.OwnerID, out List<LandObject> ownerlist))
                    {
                        ownerlist = new(){ p };
                        landOwnersAndParcels.Add(p.LandData.OwnerID, ownerlist);
                    }
                    else
                    {
                        ownerlist.Add(p);
                    }
                }
            }

            foreach (UUID owner in landOwnersAndParcels.Keys)
            {
                int simArea = 0;
                int simPrims = 0;
                foreach (LandObject p in landOwnersAndParcels[owner])
                {
                    simArea += p.LandData.Area;
                    simPrims += p.PrimCounts.Total;
                }

                foreach (LandObject p in landOwnersAndParcels[owner])
                {
                    p.LandData.SimwideArea = simArea;
                    p.LandData.SimwidePrims = simPrims;
                }
            }
        }

        public void EventManagerOnParcelPrimCountUpdate()
        {
            //m_log.DebugFormat(
            //    "[land management module]: triggered eventmanageronparcelprimcountupdate() for {0}",
            //    m_scene.RegionInfo.RegionName);

            ResetOverMeRecords();
            EntityBase[] entities = m_scene.Entities.GetEntities();
            foreach (EntityBase obj in entities)
            {
                if (obj is SceneObjectGroup sog && !sog.IsDeleted && !sog.IsAttachment)
                {
                    m_scene.EventManager.TriggerParcelPrimCountAdd(sog);
                }
            }
            FinalizeLandPrimCountUpdate();
        }

        public void EventManagerOnRequestParcelPrimCountUpdate()
        {
            ResetOverMeRecords();
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            FinalizeLandPrimCountUpdate();
        }

        /// <summary>
        /// Subdivides a piece of land
        /// </summary>
        /// <param name="start_x">West Point</param>
        /// <param name="start_y">South Point</param>
        /// <param name="end_x">East Point</param>
        /// <param name="end_y">North Point</param>
        /// <param name="attempting_user_id">UUID of user who is trying to subdivide</param>
        /// <returns>Returns true if successful</returns>
        public void Subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            //First, lets loop through the points and make sure they are all in the same peice of land
            //Get the land object at start

            ILandObject startLandObject = GetLandObject(start_x, start_y);
            if (startLandObject is null)
                return;

            if (!m_scene.Permissions.CanEditParcelProperties(attempting_user_id, startLandObject, GroupPowers.LandDivideJoin, true))
                return;

            //Loop through the points
            int area = 0;
            try
            {
                for (int x = start_x; x < end_x; x++)
                {
                    for (int y = start_y; y < end_y; y++)
                    {
                        ILandObject tempLandObject = GetLandObject(x, y);
                        if (tempLandObject != startLandObject)
                            return;
                        area++;
                    }
                }
            }
            catch (Exception)
            {
                return;
            }

            LandData startLandData = startLandObject.LandData;
            if (area >= startLandData.Area)
            {
                // split is a replace, keep as is
                return;
            }

             //Lets create a new land object with bitmap activated at that point (keeping the old land objects info)
            ILandObject newLand = startLandObject.Copy();
            LandData newLandData = newLand.LandData;

            newLandData.GlobalID = UUID.Random();
            newLandData.Dwell = 0;
            // Clear "Show in search" on the cut out parcel to prevent double-charging
            newLandData.Flags &= ~(uint)ParcelFlags.ShowDirectory;
            // invalidate landing point
            newLandData.LandingType = (byte)LandingType.Direct;
            newLandData.UserLocation = Vector3.Zero;
            newLandData.UserLookAt = Vector3.Zero;

            newLand.SetLandBitmap(newLand.GetSquareLandBitmap(start_x, start_y, end_x, end_y));

            //lets set the subdivision area of the original to false
            int startLandObjectIndex = startLandObject.LandData.LocalID;
            lock (m_landList)
            {
                m_landList[startLandObjectIndex].SetLandBitmap(newLand.ModifyLandBitmapSquare(startLandObject.GetLandBitmap(), start_x, start_y, end_x, end_y, false));
                //m_landList[startLandObjectIndex].ForceUpdateLandInfo();
            }

            UpdateLandObject(startLandObject.LandData.LocalID, startLandObject.LandData);

            //add the new land object
            ILandObject result = AddLandObject(newLand);

            if (startLandObject.LandData.LandingType == (byte)LandingType.LandingPoint)
            {
                int x = (int)startLandObject.LandData.UserLocation.X;
                int y = (int)startLandObject.LandData.UserLocation.Y;
                if(!startLandObject.ContainsPoint(x, y))
                {
                    startLandObject.LandData.LandingType = (byte)LandingType.Direct;
                    startLandObject.LandData.UserLocation = Vector3.Zero;
                    startLandObject.LandData.UserLookAt = Vector3.Zero;
                }
             }

            m_scene.EventManager.TriggerParcelPrimCountTainted();

            result.SendLandUpdateToAvatarsOverMe();
            startLandObject.SendLandUpdateToAvatarsOverMe();
            m_scene.ForEachClient(SendParcelOverlay);

        }

        /// <summary>
        /// Join 2 land objects together
        /// </summary>
        /// <param name="start_x">start x of selection area</param>
        /// <param name="start_y">start y of selection area</param>
        /// <param name="end_x">end x of selection area</param>
        /// <param name="end_y">end y of selection area</param>
        /// <param name="attempting_user_id">UUID of the avatar trying to join the land objects</param>
        /// <returns>Returns true if successful</returns>
        public void Join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            int index = 0;
            int maxindex = -1;
            int maxArea = 0;

            List<ILandObject> selectedLandObjects = new();
            for (int x = start_x; x < end_x; x += 4)
            {
                for (int y = start_y; y < end_y; y += 4)
                {
                    ILandObject p = GetLandObject(x, y);
                    if (p is not null)
                    {
                        if (!selectedLandObjects.Contains(p))
                        {
                            selectedLandObjects.Add(p);
                            if(p.LandData.Area > maxArea)
                            {
                                maxArea = p.LandData.Area;
                                maxindex = index;
                            }
                            index++;
                        }
                    }
                }
            }

            if(maxindex < 0 || selectedLandObjects.Count < 2)
                return;

            ILandObject masterLandObject = selectedLandObjects[maxindex];
            selectedLandObjects.RemoveAt(maxindex);

            if (!m_scene.Permissions.CanEditParcelProperties(attempting_user_id, masterLandObject, GroupPowers.LandDivideJoin, true))
            {
                return;
            }

            UUID masterOwner = masterLandObject.LandData.OwnerID;
            foreach (ILandObject p in selectedLandObjects)
            {
                if (p.LandData.OwnerID.NotEqual(masterOwner))
                    return;
            }

            lock (m_landList)
            {
                foreach (ILandObject slaveLandObject in selectedLandObjects)
                {
                    m_landList[masterLandObject.LandData.LocalID].SetLandBitmap(
                        slaveLandObject.MergeLandBitmaps(masterLandObject.GetLandBitmap(), slaveLandObject.GetLandBitmap()));
                    performFinalLandJoin(masterLandObject, slaveLandObject);
                }
            }

            m_scene.EventManager.TriggerParcelPrimCountTainted();
            masterLandObject.SendLandUpdateToAvatarsOverMe();
            m_scene.ForEachClient(SendParcelOverlay);
        }
        #endregion

        #region Parcel Updating

        //legacy name
        public void SendParcelsOverlay(IClientAPI client)
        {
            SendParcelOverlay(client);
        }

        /// <summary>
        /// Send the parcel overlay blocks to the client. 
        /// </summary>
        /// <param name="remote_client">The object representing the client</param>
        public void SendParcelOverlay(IClientAPI remote_client)
        {
            if (remote_client.SceneAgent.PresenceType == PresenceType.Npc)
                return;

            const int LAND_BLOCKS_PER_PACKET = 1024;

            int curID;
            int southID;

            byte[] byteArray = new byte[LAND_BLOCKS_PER_PACKET];
            int byteArrayCount = 0;
            int sequenceID = 0;

            int sx = m_regionSizeX / Constants.LandUnit;
            byte curByte;
            byte tmpByte;

            // Layer data is in LandUnit (4m) chunks
            for (int y = 0; y < m_regionSizeY / Constants.LandUnit; ++y)
            {
                for (int x = 0; x < sx;)
                {
                    curID = GetLandObjectIDinLandUnits(x,y);
                    if(curID < 0)
                        continue;

                    ILandObject currentParcel = GetLandObject(curID);
                    if (currentParcel is null)
                        continue;

                    LandData currentParcelLandData = currentParcel.LandData;
                    if (currentParcelLandData is null)
                        continue;

                    // types
                    if (currentParcelLandData.OwnerID.Equals(remote_client.AgentId))
                    {
                        //Owner Flag
                        curByte = LandChannel.LAND_TYPE_OWNED_BY_REQUESTER;
                    }
                    else if (currentParcelLandData.IsGroupOwned && remote_client.IsGroupMember(currentParcelLandData.GroupID))
                    {
                        curByte = LandChannel.LAND_TYPE_OWNED_BY_GROUP;
                    }
                    else if (currentParcelLandData.SalePrice > 0 &&
                                (currentParcelLandData.AuthBuyerID.IsZero() ||
                                currentParcelLandData.AuthBuyerID.Equals(remote_client.AgentId)))
                    {
                        //Sale type
                        curByte = LandChannel.LAND_TYPE_IS_FOR_SALE;
                    }
                    else if (currentParcelLandData.OwnerID.IsZero())
                    {
                        //Public type
                        curByte = LandChannel.LAND_TYPE_PUBLIC; // this does nothing, its zero
                    }
                    // LAND_TYPE_IS_BEING_AUCTIONED still unsuported
                    else
                    {
                        //Other 
                        curByte = LandChannel.LAND_TYPE_OWNED_BY_OTHER;
                    }

                    // now flags
                    // local sound
                    if ((currentParcelLandData.Flags & (uint)ParcelFlags.SoundLocal) != 0)
                        curByte |= (byte)LandChannel.LAND_FLAG_LOCALSOUND;

                    // hide avatars
                    if (!currentParcelLandData.SeeAVs)
                        curByte |= (byte)LandChannel.LAND_FLAG_HIDEAVATARS;

                    // border flags for current
                    if (y == 0)
                    {
                        curByte |= LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH;
                        tmpByte = curByte;
                    }
                    else
                    {
                        tmpByte = curByte;
                        southID = GetLandObjectIDinLandUnits(x, (y - 1));
                        if (southID >= 0 && southID != curID)
                            tmpByte |= LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH;
                    }

                    tmpByte |= LandChannel.LAND_FLAG_PROPERTY_BORDER_WEST;
                    byteArray[byteArrayCount] = tmpByte;
                    byteArrayCount++;

                    if (byteArrayCount >= LAND_BLOCKS_PER_PACKET)
                    {
                        remote_client.SendLandParcelOverlay(byteArray, sequenceID);
                        byteArrayCount = 0;
                        sequenceID++;
                        byteArray = new byte[LAND_BLOCKS_PER_PACKET];
                    }
                    // keep adding while on same parcel, checking south border
                    if (y == 0)
                    {
                        // all have south border and that is already on curByte
                        while (++x < sx && GetLandObjectIDinLandUnits(x, y) == curID)
                        {
                            byteArray[byteArrayCount] = curByte;
                            byteArrayCount++;
                            if (byteArrayCount >= LAND_BLOCKS_PER_PACKET)
                            {
                                remote_client.SendLandParcelOverlay(byteArray, sequenceID);
                                byteArrayCount = 0;
                                sequenceID++;
                                byteArray = new byte[LAND_BLOCKS_PER_PACKET];
                            }
                        }
                    }
                    else
                    {
                        while (++x < sx && GetLandObjectIDinLandUnits(x, y) == curID)
                        {
                            // need to check south one by one
                            southID = GetLandObjectIDinLandUnits(x, (y - 1));
                            if (southID >= 0 && southID != curID)
                            {
                                tmpByte = curByte;
                                tmpByte |= LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH;
                                byteArray[byteArrayCount] = tmpByte;
                            }
                            else
                                byteArray[byteArrayCount] = curByte;

                            byteArrayCount++;
                            if (byteArrayCount >= LAND_BLOCKS_PER_PACKET)
                            {
                                remote_client.SendLandParcelOverlay(byteArray, sequenceID);
                                byteArrayCount = 0;
                                sequenceID++;
                                byteArray = new byte[LAND_BLOCKS_PER_PACKET];
                            }
                        }
                    }
                }
            }

            if (byteArrayCount > 0)
            {
                remote_client.SendLandParcelOverlay(byteArray, sequenceID);
            }
        }

        public void ClientOnParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id,
                                                    bool snap_selection, IClientAPI remote_client)
        {
            if (m_landList.Count == 0 || m_landIDList is null)
                return;

            if (start_x < 0 || start_y < 0 || end_x < 0 || end_y < 0)
                return;
            if (start_x >= m_regionSizeX || start_y >= m_regionSizeX || end_x > m_regionSizeX || end_y > m_regionSizeY)
                return;

            if (end_x - start_x <= Constants.LandUnit &&
                end_y - start_y <= Constants.LandUnit)
            {
                ILandObject parcel = GetLandObject(start_x, start_y);
                parcel?.SendLandProperties(sequence_id, snap_selection, LandChannel.LAND_RESULT_SINGLE, remote_client);
                return;
            }

            start_x /= Constants.LandUnit;
            start_y /= Constants.LandUnit;
            end_x /= Constants.LandUnit;
            end_y /= Constants.LandUnit;

            //Get the land objects within the bounds
            Dictionary<int, ILandObject> temp = new();
            for (int x = start_x; x < end_x; ++x)
            {
                for (int y = start_y; y < end_y; ++y)
                {
                    ILandObject currentParcel = GetLandObjectinLandUnits(x, y);
                    if (currentParcel is not null)
                    {
                        if (!temp.ContainsKey(currentParcel.LandData.LocalID))
                        {
                            if (!currentParcel.IsBannedFromLand(remote_client.AgentId))
                            {
                                temp[currentParcel.LandData.LocalID] = currentParcel;
                            }
                        }
                    }
                }
            }

            int requestResult = (temp.Count > 1) ? LandChannel.LAND_RESULT_MULTIPLE : LandChannel.LAND_RESULT_SINGLE;

            foreach(ILandObject lo in temp.Values)
            {
                lo.SendLandProperties(sequence_id, snap_selection, requestResult, remote_client);
            }

            //SendParcelOverlay(remote_client);
        }

        public void UpdateLandProperties(ILandObject land, LandUpdateArgs args, IClientAPI remote_client)
        {
            if (land.UpdateLandProperties(args, remote_client, out bool snap_selection, out bool needOverlay))
            {
                UUID parcelID = land.LandData.GlobalID;
                m_scene.ForEachScenePresence(delegate (ScenePresence avatar)
                {
                    if (avatar.IsDeleted || avatar.IsNPC)
                        return;

                    IClientAPI client = avatar.ControllingClient;
                    if (needOverlay)
                        SendParcelOverlay(client);

                    if (avatar.IsChildAgent)
                    {
                        land.SendLandProperties(-10000, false, LandChannel.LAND_RESULT_SINGLE, client);
                        return;
                    }

                    ILandObject aland = GetLandObject(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
                    if (aland is not null)
                    {
                        if (land != aland)
                            land.SendLandProperties(-10000, false, LandChannel.LAND_RESULT_SINGLE, client);
                        else if (land == aland)
                            aland.SendLandProperties(0, true, LandChannel.LAND_RESULT_SINGLE, client);
                    }
                    if (avatar.currentParcelUUID.Equals(parcelID))
                        avatar.currentParcelUUID = parcelID; // force parcel flags review
                });
            }
        }

        public void ClientOnParcelPropertiesUpdateRequest(LandUpdateArgs args, int localID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                if(!m_landList.TryGetValue(localID, out land) || land is null)
                    return;
            }

            UpdateLandProperties(land, args, remote_client);
            m_scene.EventManager.TriggerOnParcelPropertiesUpdateRequest(args, localID, remote_client);
        }

        public void ClientOnParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            Subdivide(west, south, east, north, remote_client.AgentId);
        }

        public void ClientOnParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            Join(west, south, east, north, remote_client.AgentId);
        }

        public void ClientOnParcelSelectObjects(int local_id, int request_type,
                                                List<UUID> returnIDs, IClientAPI remote_client)
        {
            m_landList[local_id].SendForceObjectSelect(local_id, request_type, returnIDs, remote_client);
        }

        public void ClientOnParcelObjectOwnerRequest(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                if(!m_landList.TryGetValue(local_id, out land) || land is null)
                    return;
            }

            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            land.SendLandObjectOwners(remote_client);
        }

        public void ClientOnParcelGodForceOwner(int local_id, UUID ownerID, IClientAPI remote_client)
        {
            if (!m_scene.Permissions.IsGod(remote_client.AgentId))
                return;

            ILandObject land;
            lock (m_landList)
            {
                if (!m_landList.TryGetValue(local_id, out land) || land is null)
                    return;
            }

            land.LandData.OwnerID = ownerID;
            land.LandData.GroupID = UUID.Zero;
            land.LandData.IsGroupOwned = false;
            land.LandData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);
            UpdateLandObject(land.LandData.LocalID, land.LandData);
            m_scene.ForEachClient(SendParcelOverlay);
            land.SendLandUpdateToClient(true, remote_client);
        }

        public void ClientOnParcelAbandonRequest(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                if (!m_landList.TryGetValue(local_id, out land) || land is null)
                    return;
            }

            if (m_scene.Permissions.CanAbandonParcel(remote_client.AgentId, land))
            {
                land.LandData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                land.LandData.GroupID = UUID.Zero;
                land.LandData.IsGroupOwned = false;
                land.LandData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);

                UpdateLandObject(land.LandData.LocalID, land.LandData);
                m_scene.ForEachClient(SendParcelOverlay);
                land.SendLandUpdateToAvatars();
            }
        }

        public void ClientOnParcelReclaim(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                if (!m_landList.TryGetValue(local_id, out land) || land is null)
                    return;
            }

            if (m_scene.Permissions.CanReclaimParcel(remote_client.AgentId, land))
            {
                land.LandData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                land.LandData.ClaimDate = Util.UnixTimeSinceEpoch();
                land.LandData.GroupID = UUID.Zero;
                land.LandData.IsGroupOwned = false;
                land.LandData.SalePrice = 0;
                land.LandData.AuthBuyerID = UUID.Zero;
                land.LandData.SeeAVs = true;
                land.LandData.AnyAVSounds = true;
                land.LandData.GroupAVSounds = true;
                land.LandData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);
                UpdateLandObject(land.LandData.LocalID, land.LandData);
                m_scene.ForEachClient(SendParcelOverlay);
                land.SendLandUpdateToAvatars();
            }
        }
        #endregion

        // If the economy has been validated by the economy module,
        // and land has been validated as well, this method transfers
        // the land ownership

        public void EventManagerOnLandBuy(Object o, EventManager.LandBuyArgs e)
        {
            if (e.economyValidated && e.landValidated)
            {
                ILandObject land;
                lock (m_landList)
                {
                    if (!m_landList.TryGetValue(e.parcelLocalID, out land) || land is null)
                        return;
                }

                land.UpdateLandSold(e.agentId, e.groupId, e.groupOwned, (uint)e.transactionID, e.parcelPrice, e.parcelArea);
                m_scene.ForEachClient(SendParcelOverlay);
                land.SendLandUpdateToAvatars();
            }
        }

        // After receiving a land buy packet, first the data needs to
        // be validated. This method validates the right to buy the
        // parcel

        public void EventManagerOnValidateLandBuy(Object o, EventManager.LandBuyArgs e)
        {
            if (e.landValidated == false)
            {
                ILandObject land;
                lock (m_landList)
                {
                    if (!m_landList.TryGetValue(e.parcelLocalID, out land) || land is null)
                        return;
                }

                UUID AuthorizedID = land.LandData.AuthBuyerID;
                int saleprice = land.LandData.SalePrice;
                UUID pOwnerID = land.LandData.OwnerID;

                bool landforsale = ((land.LandData.Flags &
                                        (uint)(ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects)) != 0);
                if ((AuthorizedID.IsZero() || AuthorizedID.Equals(e.agentId)) && e.parcelPrice >= saleprice && landforsale)
                {
                    // TODO I don't think we have to lock it here, no?
                    //lock (e)
                    //{
                        e.parcelOwnerID = pOwnerID;
                        e.landValidated = true;
                    //}
                }
            }
        }

        void ClientOnParcelDeedToGroup(int parcelLocalID, UUID groupID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                if(!m_landList.TryGetValue(parcelLocalID, out land) || land is null)
                    return;
            }

            if (!m_scene.Permissions.CanDeedParcel(remote_client.AgentId, land))
                return;
            land.DeedToGroup(groupID);
            m_scene.ForEachClient(SendParcelOverlay);
            land.SendLandUpdateToAvatars();
        }

        #region Land Object From Storage Functions

        private void EventManagerOnIncomingLandDataFromStorage(List<LandData> data)
        {
            lock (m_landList)
            {
                for (int i = 0; i < data.Count; i++)
                    IncomingLandObjectFromStorage(data[i]);

                // Layer data is in LandUnit (4m) chunks
                for (int y = 0; y < m_regionSizeY / Constants.TerrainPatchSize * (Constants.TerrainPatchSize / Constants.LandUnit); y++)
                {
                    for (int x = 0; x < m_regionSizeX / Constants.TerrainPatchSize * (Constants.TerrainPatchSize / Constants.LandUnit); x++)
                    {
                        if (m_landIDList[x, y] == 0)
                        {
                            if (m_landList.Count == 1)
                            {
                                m_log.DebugFormat(
                                    "[LAND MANAGEMENT MODULE]: Auto-extending land parcel as landID at {0},{1} is 0 and only one land parcel is present in {2}",
                                    x, y, m_scene.Name);

                                int onlyParcelID = 0;
                                ILandObject onlyLandObject = null;
                                foreach (KeyValuePair<int, ILandObject> kvp in m_landList)
                                {
                                    onlyParcelID = kvp.Key;
                                    onlyLandObject = kvp.Value;
                                    break;
                                }

                                // There is only one parcel. Grow it to fill all the unallocated spaces.
                                for (int xx = 0; xx < m_landIDList.GetLength(0); xx++)
                                    for (int yy = 0; yy < m_landIDList.GetLength(1); yy++)
                                        if (m_landIDList[xx, yy] == 0)
                                            m_landIDList[xx, yy] = onlyParcelID;

                                onlyLandObject.LandBitmap = CreateBitmapForID(onlyParcelID);
                            }
                            else if (m_landList.Count > 1)
                            {
                                m_log.DebugFormat(
                                    "[LAND MANAGEMENT MODULE]: Auto-creating land parcel as landID at {0},{1} is 0 and more than one land parcel is present in {2}",
                                    x, y, m_scene.Name);

                                // There are several other parcels so we must create a new one for the unassigned space
                                ILandObject newLand = new LandObject(UUID.Zero, false, m_scene);
                                // Claim all the unclaimed "0" ids
                                newLand.SetLandBitmap(CreateBitmapForID(0));
                                newLand.LandData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                                newLand.LandData.ClaimDate = Util.UnixTimeSinceEpoch();
                                newLand = AddLandObject(newLand);
                            }
                            else
                            {
                                // We should never reach this point as the separate code path when no land data exists should have fired instead.
                                m_log.Warn(
                                    "[LAND MANAGEMENT MODULE]: Ignoring request to auto-create parcel in {1} as there are no other parcels present" + m_scene.Name);
                            }
                        }
                    }
                }

                FinalizeLandPrimCountUpdate(); // update simarea information

                lock (m_landList)
                {
                    foreach(LandObject lo in m_landList.Values)
                        lo.SendLandUpdateToAvatarsOverMe();
                }
            }
        }

        private void IncomingLandObjectFromStorage(LandData data)
        {
            ILandObject new_land = new LandObject(data.OwnerID, data.IsGroupOwned, m_scene, data);

            new_land.SetLandBitmapFromByteArray();
            AddLandObject(new_land);
        }

        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient)
        {
            if (localID != -1)
            {
                ILandObject selectedParcel;
                lock (m_landList)
                {
                    if(!m_landList.TryGetValue(localID, out selectedParcel) || selectedParcel is null)
                        return;
                }

                selectedParcel.ReturnLandObjects(returnType, agentIDs, taskIDs, remoteClient);
            }
            else
            {
                if (returnType != 1)
                {
                    m_log.WarnFormat("[LAND MANAGEMENT MODULE]: ReturnObjectsInParcel: unknown return type {0}", returnType);
                    return;
                }

                // We get here when the user returns objects from the list of Top Colliders or Top Scripts.
                // In that case we receive specific object UUID's, but no parcel ID.

                Dictionary<UUID, HashSet<SceneObjectGroup>> returns = new();
                foreach (UUID groupID in taskIDs)
                {
                    SceneObjectGroup obj = m_scene.GetSceneObjectGroup(groupID);
                    if (obj is not null)
                    {
                        if (!returns.TryGetValue(obj.OwnerID, out HashSet<SceneObjectGroup> howner))
                        {
                            howner = new HashSet<SceneObjectGroup>();
                            returns[obj.OwnerID] = howner;
                        }
                        howner.Add(obj);
                    }
                    else
                    {
                        m_log.WarnFormat("[LAND MANAGEMENT MODULE]: ReturnObjectsInParcel: unknown object {0}", groupID);
                    }
                }

                int num = 0;
                foreach (HashSet<SceneObjectGroup> objs in returns.Values)
                    num += objs.Count;
                m_log.DebugFormat("[LAND MANAGEMENT MODULE]: Returning {0} specific object(s)", num);

                foreach (HashSet<SceneObjectGroup> objs in returns.Values)
                {
                    List<SceneObjectGroup> objs2 = new(objs);
                    if (m_scene.Permissions.CanReturnObjects(null, remoteClient, objs2))
                    {
                        m_scene.returnObjects(objs2.ToArray(), remoteClient);
                    }
                    else
                    {
                        m_log.WarnFormat("[LAND MANAGEMENT MODULE]: ReturnObjectsInParcel: not permitted to return {0} object(s) belonging to user {1}",
                            objs2.Count, objs2[0].OwnerID);
                    }
                }
            }
        }

        public void EventManagerOnNoLandDataFromStorage()
        {
            ResetSimLandObjects();
            CreateDefaultParcel();
        }

        #endregion

        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            lock (m_landList)
            {
                foreach (LandObject obj in m_landList.Values)
                {
                    obj.SetParcelObjectMaxOverride(overrideDel);
                }
            }
        }

        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
        }

        #region CAPS handler

        private void EventManagerOnRegisterCaps(UUID agentID, Caps caps)
        {
            caps.RegisterSimpleHandler("RemoteParcelRequest", new SimpleOSDMapHandler("POST","/" + UUID.Random(), RemoteParcelRequest));

            caps.RegisterSimpleHandler("ParcelPropertiesUpdate", new SimpleStreamHandler("/" + UUID.Random(),
                delegate (IOSHttpRequest request, IOSHttpResponse response)
                {
                    ProcessPropertiesUpdate(request, response, agentID);
                }));
        }

        private void ProcessPropertiesUpdate(IOSHttpRequest request, IOSHttpResponse response, UUID agentID)
        {
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!m_scene.TryGetClient(agentID, out IClientAPI client))
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Unable to retrieve IClientAPI for {0}", agentID);
                response.StatusCode = (int)HttpStatusCode.Gone;
                return;
            }

            OSDMap args;
            ParcelPropertiesUpdateMessage properties;
            try
            {
                args = (OSDMap)OSDParser.DeserializeLLSDXml(request.InputStream);
                properties = new ParcelPropertiesUpdateMessage();
                properties.Deserialize(args);
            }
            catch
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            int parcelID = properties.LocalID;

            ILandObject land = null;
            lock (m_landList)
            {
                _ = m_landList.TryGetValue(parcelID, out land);
            }

            if (land is null)
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Unable to find parcelID {0}", parcelID);
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            try
            {
                LandUpdateArgs land_update = new()
                {
                    AuthBuyerID = properties.AuthBuyerID,
                    Category = properties.Category,
                    Desc = properties.Desc,
                    GroupID = properties.GroupID,
                    LandingType = (byte)properties.Landing,
                    MediaAutoScale = (byte)Convert.ToInt32(properties.MediaAutoScale),
                    MediaID = properties.MediaID,
                    MediaURL = properties.MediaURL,
                    MusicURL = properties.MusicURL,
                    Name = properties.Name,
                    ParcelFlags = (uint)properties.ParcelFlags,
                    PassHours = properties.PassHours,
                    PassPrice = (int)properties.PassPrice,
                    SalePrice = (int)properties.SalePrice,
                    SnapshotID = properties.SnapshotID,
                    UserLocation = properties.UserLocation,
                    UserLookAt = properties.UserLookAt,
                    MediaDescription = properties.MediaDesc,
                    MediaType = properties.MediaType,
                    MediaWidth = properties.MediaWidth,
                    MediaHeight = properties.MediaHeight,
                    MediaLoop = properties.MediaLoop
                };

                if (args.TryGetValue("obscure_moap", out OSD omoap))
                    land_update.ObscureMOAP = omoap.AsBoolean();

                if (args.ContainsKey("see_avs"))
                {
                    land_update.SeeAVs = args["see_avs"].AsBoolean();
                    land_update.AnyAVSounds = args["any_av_sounds"].AsBoolean();
                    land_update.GroupAVSounds = args["group_av_sounds"].AsBoolean();
                }
                else
                {
                    land_update.SeeAVs = true;
                    land_update.AnyAVSounds = true;
                    land_update.GroupAVSounds = true;
                }

                UpdateLandProperties(land,land_update, client);
                m_scene.EventManager.TriggerOnParcelPropertiesUpdateRequest(land_update, parcelID, client);
            }
            catch
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
        }

        // we cheat here: As we don't have (and want) a grid-global parcel-store, we can't return the
        // "real" parcelID, because we wouldn't be able to map that to the region the parcel belongs to.
        // So, we create a "fake" parcelID by using the regionHandle (64 bit), and the local (integer) x
        // and y coordinate (each 8 bit), encoded in a UUID (128 bit).
        //
        // Request format:
        // <llsd>
        //   <map>
        //     <key>location</key>
        //     <array>
        //       <real>1.23</real>
        //       <real>45..6</real>
        //       <real>78.9</real>
        //     </array>
        //     <key>region_id</key>
        //     <uuid>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</uuid>
        //   </map>
        // </llsd>
        private void RemoteParcelRequest(IOSHttpRequest request, IOSHttpResponse response, OSDMap args)
        {
            UUID parcelID = new();
            try
            {
                if (args.TryGetValue("location", out OSD tmp) && tmp is OSDArray olist)
                {
                    UUID scope = m_scene.RegionInfo.ScopeID;
                    uint x = (uint)(double)olist[0];
                    uint y = (uint)(double)olist[1];
                    ulong myHandle = m_scene.RegionInfo.RegionHandle;
                    if (args.TryGetValue("region_handle", out tmp) && tmp is OSDBinary)
                    {
                        // if you do a "About Landmark" on a landmark a second time, the viewer sends the
                        // region_handle it got earlier via RegionHandleRequest
                        ulong regionHandle = Util.BytesToUInt64Big((byte[])tmp);
                        if(regionHandle == myHandle)
                        {
                            ILandObject l = GetLandObjectClippedXY(x, y);
                            parcelID = l is null ? Util.BuildFakeParcelID(myHandle, x, y) : l.LandData.FakeID;
                        }
                        else
                        {
                            Util.RegionHandleToWorldLoc(regionHandle, out uint wx, out uint wy);
                            GridRegion info = m_scene.GridService.GetRegionByPosition(scope, (int)wx, (int)wy);
                            if (info is not null)
                            {
                                wx -= (uint)info.RegionLocX;
                                wy -= (uint)info.RegionLocY;
                                wx += x;
                                wy += y;
                                if (wx >= info.RegionSizeX || wy >= info.RegionSizeY)
                                {
                                    wx = x;
                                    wy = y;
                                }
                                if (info.RegionHandle == myHandle)
                                {
                                    ILandObject l = GetLandObjectClippedXY(wx, wy);
                                    parcelID = l is null ? Util.BuildFakeParcelID(myHandle, wx, wy) : l.LandData.FakeID;
                                }
                                else
                                {
                                    parcelID = Util.BuildFakeParcelID(info.RegionHandle, wx, wy);
                                }
                            }
                        }
                    }
                    else if (args.TryGetValue("region_id", out tmp) && tmp is OSDUUID)
                    {
                        UUID regionID = tmp.AsUUID();
                        if (regionID.Equals(m_scene.RegionInfo.RegionID))
                        {
                            ILandObject l = GetLandObjectClippedXY(x, y);
                            parcelID = l is null ? Util.BuildFakeParcelID(myHandle, x, y) : l.LandData.FakeID;
                        }
                        else
                        {
                            // a parcel request for a parcel in another region. Ask the grid about the region
                            GridRegion info = m_scene.GridService.GetRegionByUUID(scope, regionID);
                            if (info is not null)
                                parcelID = Util.BuildFakeParcelID(info.RegionHandle, x, y);
                        }
                    }
                }
            }
            catch
            {
                m_log.Error("[LAND MANAGEMENT MODULE]: RemoteParcelRequest failed");
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            //m_log.DebugFormat("[LAND MANAGEMENT MODULE]: Got parcelID {0} {1}", parcelID, parcelID.IsZero() ? args.ToString() :"");
            osUTF8 sb = LLSDxmlEncode2.Start();
                LLSDxmlEncode2.AddMap(sb);
                  LLSDxmlEncode2.AddElem("parcel_id", parcelID,sb);
                LLSDxmlEncode2.AddEndMap(sb);
            response.RawBuffer = LLSDxmlEncode2.EndToBytes(sb);
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        #endregion

        private void ClientOnParcelInfoRequest(IClientAPI remoteClient, UUID parcelID)
        {
            if (parcelID.IsZero())
                return;

            if(!m_parcelInfoCache.TryGetValue(parcelID, 30000, out ExtendedLandData data))
            {
                data = null;
                ExtendedLandData extLandData = new();

                while(true)
                {
                    if(!Util.ParseFakeParcelID(parcelID, out extLandData.RegionHandle,
                                        out extLandData.X, out extLandData.Y))
                        break;

                    //m_log.DebugFormat("[LAND MANAGEMENT MODULE]: Got parcelinfo request for regionHandle {0}, x/y {1}/{2}",
                    //                extLandData.RegionHandle, extLandData.X, extLandData.Y);

                    // for this region or for somewhere else?
                    if (extLandData.RegionHandle == m_scene.RegionInfo.RegionHandle)
                    {
                        ILandObject extLandObject = GetLandObjectByfakeID(parcelID);
                        if (extLandObject is null)
                            break;

                        extLandData.LandData = extLandObject.LandData;
                        extLandData.RegionAccess = m_scene.RegionInfo.AccessLevel;
                        if (extLandData.LandData is not null)
                            data = extLandData;
                        break;
                    }
                    else
                    {
                        ILandService landService = m_scene.RequestModuleInterface<ILandService>();
                        extLandData.LandData = landService.GetLandData(m_scene.RegionInfo.ScopeID,
                                extLandData.RegionHandle, extLandData.X, extLandData.Y,
                                out extLandData.RegionAccess);
                        if (extLandData.LandData is not null)
                            data = extLandData;
                        break;
                    }
                }
                m_parcelInfoCache.Add(parcelID, data, 30000);
            }

            if (data is not null)  // if we found some data, send it
            {
                GridRegion info;
                if (data.RegionHandle == m_scene.RegionInfo.RegionHandle)
                {
                    info = new GridRegion(m_scene.RegionInfo);
                    IDwellModule dwellModule = m_scene.RequestModuleInterface<IDwellModule>();
                    if (dwellModule is not null)
                        data.LandData.Dwell = dwellModule.GetDwell(data.LandData);
                }
                else
                {
                    // most likely still cached from building the extLandData entry
                    info = m_scene.GridService.GetRegionByHandle(m_scene.RegionInfo.ScopeID, data.RegionHandle);
                }
                // we need to transfer the fake parcelID, not the one in landData, so the viewer can match it to the landmark.
                //m_log.DebugFormat("[LAND MANAGEMENT MODULE]: got parcelinfo for parcel {0} in region {1}; sending...",
                //                  data.LandData.Name, data.RegionHandle);

                // HACK for now
                RegionInfo r = new()
                {
                    RegionName = info.RegionName,
                    RegionLocX = (uint)info.RegionLocX,
                    RegionLocY = (uint)info.RegionLocY
                };
                r.RegionSettings.Maturity = (int)Util.ConvertAccessLevelToMaturity(data.RegionAccess);
                remoteClient.SendParcelInfo(r, data.LandData, parcelID, data.X, data.Y);
            }
            else
                m_log.Debug("[LAND MANAGEMENT MODULE]: got no parcelinfo; not sending");
        }

        public void SetParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime)
        {
            ILandObject land;
            lock (m_landList)
            {
                if(!m_landList.TryGetValue(localID, out land) || land is null)
                    return;
            }

            if (!m_scene.Permissions.CanEditParcelProperties(remoteClient.AgentId, land, GroupPowers.LandOptions, false))
                return;

            land.LandData.OtherCleanTime = otherCleanTime;

            UpdateLandObject(localID, land.LandData);
        }

        public void ClientOnParcelGodMark(IClientAPI client, UUID god, int landID)
        {
            if(!((Scene)client.Scene).TryGetScenePresence(client.AgentId, out ScenePresence sp) || sp is null)
                return;
            if (sp.IsChildAgent || sp.IsDeleted || sp.IsInTransit || sp.IsNPC)
                return;
            if (!sp.IsGod)
            {
                client.SendAlertMessage("Request denied. You're not priviliged.");
                return;
            }

            ILandObject land = null;
            List<ILandObject> Lands = ((Scene)client.Scene).LandChannel.AllParcels();
            foreach (ILandObject landObject in Lands)
            {
                if (landObject.LandData.LocalID == landID)
                { 
                    land = landObject;
                    break;
                }
            }
            if (land is null)
                return;

            bool validParcelOwner = DefaultGodParcelOwner.IsNotZero() && m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, DefaultGodParcelOwner) is not null;

            bool validParcelGroup = false;
            if (m_groupManager is not null)
            {
                if (DefaultGodParcelGroup.IsNotZero() && m_groupManager.GetGroupRecord(DefaultGodParcelGroup) is not null)
                    validParcelGroup = true;
            }

            if (!validParcelOwner && !validParcelGroup)
            {
                client.SendAlertMessage("Please check ini files.\n[LandManagement] config section.");
                return;
            }

            land.LandData.AnyAVSounds = true;
            land.LandData.SeeAVs = true;
            land.LandData.GroupAVSounds = true;
            land.LandData.AuthBuyerID = UUID.Zero;
            land.LandData.Category = ParcelCategory.None;
            land.LandData.ClaimDate = Util.UnixTimeSinceEpoch();
            land.LandData.Description = string.Empty;
            land.LandData.Dwell = 0;
            land.LandData.Flags = (uint)ParcelFlags.AllowFly | (uint)ParcelFlags.AllowLandmark |
                                (uint)ParcelFlags.AllowAPrimitiveEntry |
                                (uint)ParcelFlags.AllowDeedToGroup |
                                (uint)ParcelFlags.CreateObjects | (uint)ParcelFlags.AllowOtherScripts |
                                (uint)ParcelFlags.AllowVoiceChat;
            land.LandData.LandingType = (byte)LandingType.Direct;
            land.LandData.LastDwellTimeMS = Util.GetTimeStampMS();
            land.LandData.MediaAutoScale = 0;
            land.LandData.MediaDescription = "";
            land.LandData.MediaHeight = 0;
            land.LandData.MediaID = UUID.Zero;
            land.LandData.MediaLoop = false;
            land.LandData.MediaType = "none/none";
            land.LandData.MediaURL = string.Empty;
            land.LandData.MediaWidth = 0;
            land.LandData.MusicURL = string.Empty;
            land.LandData.ObscureMedia = false;
            land.LandData.ObscureMusic = false;
            land.LandData.OtherCleanTime = 0;
            land.LandData.ParcelAccessList = new List<LandAccessEntry>();
            land.LandData.PassHours = 0;
            land.LandData.PassPrice = 0;
            land.LandData.SalePrice = 0;
            land.LandData.SnapshotID = UUID.Zero;
            land.LandData.Status = ParcelStatus.Leased;

            if (validParcelOwner)
            {
                land.LandData.OwnerID = DefaultGodParcelOwner;
                land.LandData.IsGroupOwned = false;
            }
            else
            {
                land.LandData.OwnerID = DefaultGodParcelGroup;
                land.LandData.IsGroupOwned = true;
            }

            if (validParcelGroup)
                land.LandData.GroupID = DefaultGodParcelGroup;
            else
                land.LandData.GroupID = UUID.Zero;

            land.LandData.Name = DefaultGodParcelName;
            UpdateLandObject(land.LandData.LocalID, land.LandData);
            //m_scene.EventManager.TriggerParcelPrimCountUpdate();

            m_scene.ForEachClient(SendParcelOverlay);
            land.SendLandUpdateToClient(true, client);
        }

        private void ClientOnSimWideDeletes(IClientAPI client, UUID agentID, int flags, UUID targetID)
        {
            if(!((Scene)client.Scene).TryGetScenePresence(client.AgentId, out ScenePresence sp))
                return;

            List<SceneObjectGroup> returns = new();
            if (sp.GodController.UserLevel != 0)
            {
                if (flags == 0) //All parcels, scripted or not
                {
                    ((Scene)client.Scene).ForEachSOG(delegate(SceneObjectGroup e)
                    {
                        if (e.OwnerID.Equals(targetID))
                        {
                            returns.Add(e);
                        }
                    });
                }
                if (flags == 4) //All parcels, scripted object
                {
                    ((Scene)client.Scene).ForEachSOG(delegate(SceneObjectGroup e)
                    {
                        if (e.OwnerID.Equals(targetID))
                        {
                            if (e.ContainsScripts())
                            {
                                returns.Add(e);
                            }
                        }
                    });
                }
                if (flags == 4) //not target parcel, scripted object
                {
                    ((Scene)client.Scene).ForEachSOG(delegate(SceneObjectGroup e)
                    {
                        if (e.OwnerID.Equals(targetID))
                        {
                            ILandObject landobject = ((Scene)client.Scene).LandChannel.GetLandObject(e.AbsolutePosition.X, e.AbsolutePosition.Y);
                            if (landobject.LandData.OwnerID != e.OwnerID)
                            {
                                if (e.ContainsScripts())
                                {
                                    returns.Add(e);
                                }
                            }
                        }
                    });
                }
                foreach (SceneObjectGroup ol in returns)
                {
                    ReturnObject(ol, client);
                }
            }
        }
        public void ReturnObject(SceneObjectGroup obj, IClientAPI client)
        {
            SceneObjectGroup[] objs = new SceneObjectGroup[1];
            objs[0] = obj;
            ((Scene)client.Scene).returnObjects(objs, client);
        }

        private readonly Dictionary<UUID, System.Threading.Timer> Timers = new();

        public void ClientOnParcelFreezeUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            Scene clientScene = client.Scene as Scene;
            if (!clientScene.TryGetScenePresence(target, out ScenePresence targetAvatar))
                return;
            if(!clientScene.TryGetScenePresence(client.AgentId, out ScenePresence parcelManager))
                return;
            System.Threading.Timer Timer;

            if (targetAvatar.GodController.UserLevel < 200)
            {
                ILandObject land = clientScene.LandChannel.GetLandObject(targetAvatar.AbsolutePosition.X, targetAvatar.AbsolutePosition.Y);
                if (!clientScene.Permissions.CanEditParcelProperties(client.AgentId, land, GroupPowers.LandEjectAndFreeze, true))
                    return;
                if ((flags & 1) == 0) // only lowest bit has meaning for now
                {
                    targetAvatar.AllowMovement = false;
                    targetAvatar.ControllingClient.SendAlertMessage(parcelManager.Firstname + " " + parcelManager.Lastname + " has frozen you for 30 seconds.  You cannot move or interact with the world.");
                    parcelManager.ControllingClient.SendAlertMessage("Avatar Frozen.");
                    System.Threading.TimerCallback timeCB = new(OnEndParcelFrozen);
                    Timer = new System.Threading.Timer(timeCB, targetAvatar, 30000, 0);
                    Timers.Add(targetAvatar.UUID, Timer);
                }
                else
                {
                    targetAvatar.AllowMovement = true;
                    targetAvatar.ControllingClient.SendAlertMessage(parcelManager.Firstname + " " + parcelManager.Lastname + " has unfrozen you.");
                    parcelManager.ControllingClient.SendAlertMessage("Avatar Unfrozen.");
                    if(Timers.Remove(targetAvatar.UUID, out Timer))
                        Timer.Dispose();
                }
            }
        }
        private void OnEndParcelFrozen(object avatar)
        {
            ScenePresence targetAvatar = (ScenePresence)avatar;
            if (Timers.Remove(targetAvatar.UUID, out System.Threading.Timer Timer))
                Timer.Dispose();
            targetAvatar.AllowMovement = true;
            targetAvatar.ControllingClient.SendAgentAlertMessage("The freeze has worn off; you may go about your business.", false);
        }

        public void ClientOnParcelEjectUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            // Must have presences
            if (!m_scene.TryGetScenePresence(target, out ScenePresence targetAvatar) ||
                !m_scene.TryGetScenePresence(client.AgentId, out ScenePresence parcelManager))
                return;

            // Cannot eject estate managers or gods
            if (m_scene.Permissions.IsAdministrator(target))
                return;

            // Check if you even have permission to do this
            ILandObject land = m_scene.LandChannel.GetLandObject(targetAvatar.AbsolutePosition.X, targetAvatar.AbsolutePosition.Y);
            if (!m_scene.Permissions.CanEditParcelProperties(client.AgentId, land, GroupPowers.LandEjectAndFreeze, true) &&
                !m_scene.Permissions.IsAdministrator(client.AgentId))
                return;

            Vector3 pos = m_scene.GetNearestAllowedPosition(targetAvatar, land);

            targetAvatar.TeleportOnEject(pos);
            targetAvatar.ControllingClient.SendAlertMessage("You have been ejected by " + parcelManager.Firstname + " " + parcelManager.Lastname);
            parcelManager.ControllingClient.SendAlertMessage("Avatar Ejected.");

            if ((flags & 1) != 0) // Ban TODO: Remove magic number
            {
                LandAccessEntry entry = new()
                {
                    AgentID = targetAvatar.UUID,
                    Flags = AccessList.Ban,
                    Expires = 0 // Perm
                };

                land.LandData.ParcelAccessList.Add(entry);
            }
        }

        public void ClearAllEnvironments()
        {
            List<ILandObject> parcels = AllParcels();
            for (int i = 0; i < parcels.Count; ++i)
                parcels[i].StoreEnvironment(null);
        }

        /// <summary>
        /// Sets the Home Point.   The LoginService uses this to know where to put a user when they log-in
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public virtual void ClientOnSetHome(IClientAPI remoteClient, ulong regionHandle, Vector3 position, Vector3 lookAt, uint flags)
        {
            // Let's find the parcel in question
            ILandObject land = GetLandObject(position);
            if (land is null || m_scene.GridUserService is null)
            {
                m_Dialog.SendAlertToUser(remoteClient, "Set Home request failed.");
                return;
            }

            // Gather some data
            ulong gpowers = remoteClient.GetGroupPowers(land.LandData.GroupID);

            SceneObjectGroup telehub = m_scene.RegionInfo.RegionSettings.TelehubObject.IsNotZero() ?
                m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject) : null;

            // Can the user set home here?
            if (// Required: local user; foreign users cannot set home
                m_scene.UserManagementModule.IsLocalGridUser(remoteClient.AgentId) &&
                (// (a) gods and land managers can set home
                 m_scene.Permissions.IsAdministrator(remoteClient.AgentId) ||
                 m_scene.Permissions.IsGod(remoteClient.AgentId) ||
                 // (b) land owners can set home
                 remoteClient.AgentId.Equals(land.LandData.OwnerID) ||
                 // (c) members of the land-associated group in roles that can set home
                 ((gpowers & (ulong)GroupPowers.AllowSetHome) == (ulong)GroupPowers.AllowSetHome) ||
                 // (d) parcels with telehubs can be the home of anyone
                 (telehub is not null && land.ContainsPoint((int)telehub.AbsolutePosition.X, (int)telehub.AbsolutePosition.Y))))
            {
                if (!m_scene.UserManagementModule.GetUserUUI(remoteClient.AgentId, out string userId))
                {
                    /* Do not set a home position in this grid for a HG visitor */
                    m_Dialog.SendAlertToUser(remoteClient, "Set Home request failed. (User Lookup)");
                }
                else if (!UUID.TryParse(userId, out UUID _))
                {
                    m_Dialog.SendAlertToUser(remoteClient, "Set Home request failed. (HG visitor)");
                }
                else if (m_scene.GridUserService.SetHome(userId, land.RegionUUID, position, lookAt))
                {
                    // FUBAR ALERT: this needs to be "Home position set." so the viewer saves a home-screenshot.
                    m_Dialog.SendAlertToUser(remoteClient, "Home position set.");
                }
                else
                {
                    m_Dialog.SendAlertToUser(remoteClient, "Set Home request failed.");
                }
            }
            else
                m_Dialog.SendAlertToUser(remoteClient, "You are not allowed to set your home location in this parcel.");
        }

        protected void RegisterCommands()
        {
            ICommands commands = MainConsole.Instance.Commands;

            commands.AddCommand(
                "Land", false, "land clear",
                "land clear",
                "Clear all the parcels from the region.",
                "Command will ask for confirmation before proceeding.",
                HandleClearCommand);

            commands.AddCommand(
                "Land", false, "land show",
                "land show [<local-land-id>]",
                "Show information about the parcels on the region.",
                "If no local land ID is given, then summary information about all the parcels is shown.\n"
                    + "If a local land ID is given then full information about that parcel is shown.",
                HandleShowCommand);
        }

        protected void HandleClearCommand(string module, string[] args)
        {
            if (!(MainConsole.Instance.ConsoleScene is null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            string response = MainConsole.Instance.Prompt(
                $"Are you sure that you want to clear all land parcels from {m_scene.Name} (y or n)", "n");

            if (response.Equals("y", StringComparison.InvariantCultureIgnoreCase))
            {
                Clear(true);
                MainConsole.Instance.Output("Cleared all parcels from {0}", m_scene.Name);
            }
            else
            {
                MainConsole.Instance.Output("Aborting clear of all parcels from {0}", m_scene.Name);
            }
        }

        protected void HandleShowCommand(string module, string[] args)
        {
            if (!(MainConsole.Instance.ConsoleScene is null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            StringBuilder report = new();

            if (args.Length <= 2)
            {
                AppendParcelsSummaryReport(report);
            }
            else
            {
                if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, args[2], out int landLocalId))
                    return;

                ILandObject lo = null;

                lock (m_landList)
                {
                    if (!m_landList.TryGetValue(landLocalId, out lo))
                    {
                        MainConsole.Instance.Output($"No parcel found with local ID {landLocalId}");
                        return;
                    }
                }

                AppendParcelReport(report, lo);
            }

            MainConsole.Instance.Output(report.ToString());
        }

        private void AppendParcelsSummaryReport(StringBuilder report)
        {
            report.AppendFormat("Land information for {0}\n", m_scene.Name);

            ConsoleDisplayTable cdt = new();
            cdt.AddColumn("Parcel Name", ConsoleDisplayUtil.ParcelNameSize);
            cdt.AddColumn("ID", 3);
            cdt.AddColumn("Area", 6);
            cdt.AddColumn("Starts", ConsoleDisplayUtil.VectorSize);
            cdt.AddColumn("Ends", ConsoleDisplayUtil.VectorSize);
            cdt.AddColumn("Owner", ConsoleDisplayUtil.UserNameSize);
            cdt.AddColumn("fakeID", 38);

            lock (m_landList)
            {
                foreach (ILandObject lo in m_landList.Values)
                {
                    LandData ld = lo.LandData;
                    string ownerName;
                    if (ld.IsGroupOwned)
                    {
                        GroupRecord rec = m_groupManager.GetGroupRecord(ld.GroupID);
                        ownerName = (rec is not null) ? rec.GroupName : "Unknown Group";
                    }
                    else
                    {
                        ownerName = m_userManager.GetUserName(ld.OwnerID);
                    }
                    cdt.AddRow(
                        ld.Name, ld.LocalID, ld.Area, lo.StartPoint, lo.EndPoint, ownerName, lo.FakeID);
                }
            }

            report.Append(cdt.ToString());
        }

        private void AppendParcelReport(StringBuilder report, ILandObject lo)
        {
            LandData ld = lo.LandData;

            ConsoleDisplayList cdl = new();
            cdl.AddRow("Parcel name", ld.Name);
            cdl.AddRow("Local ID", ld.LocalID);
            cdl.AddRow("Fake ID", ld.FakeID);
            cdl.AddRow("Description", ld.Description);
            cdl.AddRow("Snapshot ID", ld.SnapshotID);
            cdl.AddRow("Area", ld.Area);
            cdl.AddRow("AABB Min", ld.AABBMin);
            cdl.AddRow("AABB Max", ld.AABBMax);
            string ownerName;
            if (ld.IsGroupOwned)
            {
                GroupRecord rec = m_groupManager.GetGroupRecord(ld.GroupID);
                ownerName = (rec != null) ? rec.GroupName : "Unknown Group";
            }
            else
            {
                ownerName = m_userManager.GetUserName(ld.OwnerID);
            }
            cdl.AddRow("Owner", ownerName);
            cdl.AddRow("Is group owned?", ld.IsGroupOwned);
            cdl.AddRow("GroupID", ld.GroupID);

            cdl.AddRow("Status", ld.Status);
            cdl.AddRow("Flags", (ParcelFlags)ld.Flags);

            cdl.AddRow("Landing Type", (LandingType)ld.LandingType);
            cdl.AddRow("User Location", ld.UserLocation);
            cdl.AddRow("User look at", ld.UserLookAt);

            cdl.AddRow("Other clean time", ld.OtherCleanTime);

            cdl.AddRow("Max Prims", lo.GetParcelMaxPrimCount());
            cdl.AddRow("Simwide Max Prims (owner)", lo.GetSimulatorMaxPrimCount());
            IPrimCounts pc = lo.PrimCounts;
            cdl.AddRow("Owner Prims", pc.Owner);
            cdl.AddRow("Group Prims", pc.Group);
            cdl.AddRow("Other Prims", pc.Others);
            cdl.AddRow("Selected Prims", pc.Selected);
            cdl.AddRow("Total Prims", pc.Total);
            cdl.AddRow("SimWide Prims (owner)", pc.Simulator);

            cdl.AddRow("Music URL", ld.MusicURL);
            cdl.AddRow("Obscure Music", ld.ObscureMusic);

            cdl.AddRow("Media ID", ld.MediaID);
            cdl.AddRow("Media Autoscale", Convert.ToBoolean(ld.MediaAutoScale));
            cdl.AddRow("Media URL", ld.MediaURL);
            cdl.AddRow("Media Type", ld.MediaType);
            cdl.AddRow("Media Description", ld.MediaDescription);
            cdl.AddRow("Media Width", ld.MediaWidth);
            cdl.AddRow("Media Height", ld.MediaHeight);
            cdl.AddRow("Media Loop", ld.MediaLoop);

            cdl.AddRow("Obscure MOAP", ld.ObscureMedia);

            cdl.AddRow("Parcel Category", ld.Category);

            cdl.AddRow("Claim Date", ld.ClaimDate);
            cdl.AddRow("Claim Price", ld.ClaimPrice);
            cdl.AddRow("Pass Hours", ld.PassHours);
            cdl.AddRow("Pass Price", ld.PassPrice);

            cdl.AddRow("Auction ID", ld.AuctionID);
            cdl.AddRow("Authorized Buyer ID", ld.AuthBuyerID);
            cdl.AddRow("Sale Price", ld.SalePrice);

            cdl.AddToStringBuilder(report);
        }
    }
}
