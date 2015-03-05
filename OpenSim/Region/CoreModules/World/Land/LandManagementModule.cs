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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

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
    public class LandManagementModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[LAND MANAGEMENT MODULE]";

        /// <summary>
        /// Minimum land unit size in region co-ordinates.
        /// </summary>
        public const int LandUnit = 4;

        private static readonly string remoteParcelRequestPath = "0009/";

        private LandChannel landChannel;
        private Scene m_scene;

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
        private readonly Dictionary<int, ILandObject> m_landList = new Dictionary<int, ILandObject>();

        private int m_lastLandLocalID = LandChannel.START_LAND_LOCAL_ID - 1;

        private bool m_allowedForcefulBans = true;

        // caches ExtendedLandData
        private Cache parcelInfoCache;

        /// <summary>
        /// Record positions that avatar's are currently being forced to move to due to parcel entry restrictions.
        /// </summary>
        private Dictionary<UUID, Vector3> forcedPosition = new Dictionary<UUID, Vector3>();

        #region INonSharedRegionModule Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_landIDList = new int[m_scene.RegionInfo.RegionSizeX / LandUnit, m_scene.RegionInfo.RegionSizeY / LandUnit];
            landChannel = new LandChannel(scene, this);

            parcelInfoCache = new Cache();
            parcelInfoCache.Size = 30; // the number of different parcel requests in this region to cache
            parcelInfoCache.DefaultTTL = new TimeSpan(0, 5, 0);

            m_scene.EventManager.OnParcelPrimCountAdd += EventManagerOnParcelPrimCountAdd;
            m_scene.EventManager.OnParcelPrimCountUpdate += EventManagerOnParcelPrimCountUpdate;
            m_scene.EventManager.OnObjectBeingRemovedFromScene += EventManagerOnObjectBeingRemovedFromScene;            
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

            lock (m_scene)
            {
                m_scene.LandChannel = (ILandChannel)landChannel;
            }
            
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

//        private bool OnVerifyUserConnection(ScenePresence scenePresence, out string reason)
//        {
//            ILandObject nearestParcel = m_scene.GetNearestAllowedParcel(scenePresence.UUID, scenePresence.AbsolutePosition.X, scenePresence.AbsolutePosition.Y);
//            reason = "You are not allowed to enter this sim.";
//            return nearestParcel != null;
//        }     

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
            client.OnPreAgentUpdate += ClientOnPreAgentUpdate;
            client.OnParcelEjectUser += ClientOnParcelEjectUser;
            client.OnParcelFreezeUser += ClientOnParcelFreezeUser;
            client.OnSetStartLocationRequest += ClientOnSetHome;


            EntityBase presenceEntity;
            if (m_scene.Entities.TryGetValue(client.AgentId, out presenceEntity) && presenceEntity is ScenePresence)
            {
                SendLandUpdate((ScenePresence)presenceEntity, true);
                SendParcelOverlay(client);
            }
        }

        public void EventMakeChildAgent(ScenePresence avatar)
        {
            avatar.currentParcelUUID = UUID.Zero;
        }

        void ClientOnPreAgentUpdate(IClientAPI remoteClient, AgentUpdateArgs agentData)
        {
            //If we are forcing a position for them to go
            if (forcedPosition.ContainsKey(remoteClient.AgentId))
            {
                ScenePresence clientAvatar = m_scene.GetScenePresence(remoteClient.AgentId);

                //Putting the user into flying, both keeps the avatar in fligth when it bumps into something and stopped from going another direction AND
                //When the avatar walks into a ban line on the ground, it prevents getting stuck
                agentData.ControlFlags = (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;

                //Make sure we stop if they get about to the right place to prevent yoyo and prevents getting stuck on banlines
                if (Vector3.Distance(clientAvatar.AbsolutePosition, forcedPosition[remoteClient.AgentId]) < .2)
                {
//                    m_log.DebugFormat(
//                        "[LAND MANAGEMENT MODULE]: Stopping force position of {0} because {1} is close enough to {2}",
//                        clientAvatar.Name, clientAvatar.AbsolutePosition, forcedPosition[remoteClient.AgentId]);

                    forcedPosition.Remove(remoteClient.AgentId);
                }
                //if we are far away, teleport
                else if (Vector3.Distance(clientAvatar.AbsolutePosition, forcedPosition[remoteClient.AgentId]) > 3)
                {
                    Vector3 forcePosition = forcedPosition[remoteClient.AgentId];
//                    m_log.DebugFormat(
//                        "[LAND MANAGEMENT MODULE]: Teleporting out {0} because {1} is too far from avatar position {2}",
//                        clientAvatar.Name, clientAvatar.AbsolutePosition, forcePosition);

                    m_scene.RequestTeleportLocation(remoteClient, m_scene.RegionInfo.RegionHandle,
                        forcePosition, clientAvatar.Lookat, (uint)Constants.TeleportFlags.ForceRedirect);

                    forcedPosition.Remove(remoteClient.AgentId);
                }
                else
                {
//                    m_log.DebugFormat(
//                        "[LAND MANAGEMENT MODULE]: Forcing {0} from {1} to {2}",
//                        clientAvatar.Name, clientAvatar.AbsolutePosition, forcedPosition[remoteClient.AgentId]);

                    //Forces them toward the forced position we want if they aren't there yet
                    agentData.UseClientAgentPosition = true;
                    agentData.ClientAgentPosition = forcedPosition[remoteClient.AgentId];
                }
            }
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
                    land.LandData = newData;
            }

            if (land != null)
                m_scene.EventManager.TriggerLandObjectUpdated((uint)local_id, land);
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
                m_landList.Clear();
                m_lastLandLocalID = LandChannel.START_LAND_LOCAL_ID - 1;
                m_landIDList = new int[m_scene.RegionInfo.RegionSizeX / LandUnit, m_scene.RegionInfo.RegionSizeY / LandUnit];
            }
        }
        
        /// <summary>
        /// Create a default parcel that spans the entire region and is owned by the estate owner.
        /// </summary>
        /// <returns>The parcel created.</returns>
        protected ILandObject CreateDefaultParcel()
        {
            m_log.DebugFormat(
                "[LAND MANAGEMENT MODULE]: Creating default parcel for region {0}", m_scene.RegionInfo.RegionName);
            
            ILandObject fullSimParcel = new LandObject(UUID.Zero, false, m_scene);                                                
            fullSimParcel.SetLandBitmap(fullSimParcel.GetSquareLandBitmap(0, 0,
                                            (int)m_scene.RegionInfo.RegionSizeX, (int)m_scene.RegionInfo.RegionSizeY));
            fullSimParcel.LandData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
            fullSimParcel.LandData.ClaimDate = Util.UnixTimeSinceEpoch();
            
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
            List<ILandObject> parcelsNear = new List<ILandObject>();
            for (int x = -4; x <= 4; x += 4)
            {
                for (int y = -4; y <= 4; y += 4)
                {
                    ILandObject check = GetLandObject(position.X + x, position.Y + y);
                    if (check != null)
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

        public void SendYouAreBannedNotice(ScenePresence avatar)
        {
            if (AllowedForcefulBans)
            {
                avatar.ControllingClient.SendAlertMessage(
                    "You are not allowed on this parcel because you are banned. Please go away.");
            }
            else
            {
                avatar.ControllingClient.SendAlertMessage(
                    "You are not allowed on this parcel because you are banned; however, the grid administrator has disabled ban lines globally. Please obey the land owner's requests or you can be banned from the entire sim!");
            }
        }

        private void ForceAvatarToPosition(ScenePresence avatar, Vector3? position)
        {
            if (m_scene.Permissions.IsGod(avatar.UUID)) return;
            if (position.HasValue)
            {
                forcedPosition[avatar.ControllingClient.AgentId] = (Vector3)position;
            }
        }

        public void SendYouAreRestrictedNotice(ScenePresence avatar)
        {
            avatar.ControllingClient.SendAlertMessage(
                "You are not allowed on this parcel because the land owner has restricted access.");
        }

        public void EventManagerOnAvatarEnteringNewParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            if (m_scene.RegionInfo.RegionID == regionID)
            {
                ILandObject parcelAvatarIsEntering;
                lock (m_landList)
                {
                    parcelAvatarIsEntering = m_landList[localLandID];
                }

                if (parcelAvatarIsEntering != null)
                {
                    if (avatar.AbsolutePosition.Z < LandChannel.BAN_LINE_SAFETY_HIEGHT)
                    {
                        if (parcelAvatarIsEntering.IsBannedFromLand(avatar.UUID))
                        {
                            SendYouAreBannedNotice(avatar);
                            ForceAvatarToPosition(avatar, m_scene.GetNearestAllowedPosition(avatar));
                        }
                        else if (parcelAvatarIsEntering.IsRestrictedFromLand(avatar.UUID))
                        {
                            SendYouAreRestrictedNotice(avatar);
                            ForceAvatarToPosition(avatar, m_scene.GetNearestAllowedPosition(avatar));
                        }
                        else
                        {
                            avatar.sentMessageAboutRestrictedParcelFlyingDown = true;
                        }
                    }
                    else
                    {
                        avatar.sentMessageAboutRestrictedParcelFlyingDown = true;
                    }
                }
            }
        }

        public void SendOutNearestBanLine(IClientAPI client)
        {
            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null || sp.IsChildAgent)
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

        public void SendLandUpdate(ScenePresence avatar, bool force)
        {
            ILandObject over = GetLandObject((int)Math.Min(((int)m_scene.RegionInfo.RegionSizeX - 1), Math.Max(0, Math.Round(avatar.AbsolutePosition.X))),
                                             (int)Math.Min(((int)m_scene.RegionInfo.RegionSizeY - 1), Math.Max(0, Math.Round(avatar.AbsolutePosition.Y))));

            if (over != null)
            {
                if (force)
                {
                    if (!avatar.IsChildAgent)
                    {
                        over.SendLandUpdateToClient(avatar.ControllingClient);
                        m_scene.EventManager.TriggerAvatarEnteringNewParcel(avatar, over.LandData.LocalID,
                                                                            m_scene.RegionInfo.RegionID);
                    }
                }

                if (avatar.currentParcelUUID != over.LandData.GlobalID)
                {
                    if (!avatar.IsChildAgent)
                    {
                        over.SendLandUpdateToClient(avatar.ControllingClient);
                        avatar.currentParcelUUID = over.LandData.GlobalID;
                        m_scene.EventManager.TriggerAvatarEnteringNewParcel(avatar, over.LandData.LocalID,
                                                                            m_scene.RegionInfo.RegionID);
                    }
                }
            }
        }

        public void SendLandUpdate(ScenePresence avatar)
        {
            SendLandUpdate(avatar, false);
        }

        public void EventManagerOnSignificantClientMovement(ScenePresence clientAvatar)
        {
            SendLandUpdate(clientAvatar);
            SendOutNearestBanLine(clientAvatar.ControllingClient);
            ILandObject parcel = GetLandObject(clientAvatar.AbsolutePosition.X, clientAvatar.AbsolutePosition.Y);
            if (parcel != null)
            {
                if (clientAvatar.AbsolutePosition.Z < LandChannel.BAN_LINE_SAFETY_HIEGHT &&
                    clientAvatar.sentMessageAboutRestrictedParcelFlyingDown)
                {
                    EventManagerOnAvatarEnteringNewParcel(clientAvatar, parcel.LandData.LocalID,
                                                          m_scene.RegionInfo.RegionID);
                    //They are going under the safety line!
                    if (!parcel.IsBannedFromLand(clientAvatar.UUID))
                    {
                        clientAvatar.sentMessageAboutRestrictedParcelFlyingDown = false;
                    }
                }
                else if (clientAvatar.AbsolutePosition.Z < LandChannel.BAN_LINE_SAFETY_HIEGHT &&
                         parcel.IsBannedFromLand(clientAvatar.UUID))
                {
                    //once we've sent the message once, keep going toward the target until we are done
                    if (forcedPosition.ContainsKey(clientAvatar.ControllingClient.AgentId))
                    {
                        SendYouAreBannedNotice(clientAvatar);
                        ForceAvatarToPosition(clientAvatar, m_scene.GetNearestAllowedPosition(clientAvatar));
                    }
                }
                else if (parcel.IsRestrictedFromLand(clientAvatar.UUID))
                {
                    //once we've sent the message once, keep going toward the target until we are done
                    if (forcedPosition.ContainsKey(clientAvatar.ControllingClient.AgentId))
                    {
                        SendYouAreRestrictedNotice(clientAvatar);
                        ForceAvatarToPosition(clientAvatar, m_scene.GetNearestAllowedPosition(clientAvatar));
                    }
                }
                else
                {
                    //when we are finally in a safe place, lets release the forced position lock
                    forcedPosition.Remove(clientAvatar.ControllingClient.AgentId);
                }
            }
        }

        /// <summary>
        /// Like handleEventManagerOnSignificantClientMovement, but called with an AgentUpdate regardless of distance.
        /// </summary>
        /// <param name="avatar"></param>
        public void EventManagerOnClientMovement(ScenePresence avatar)
        {
            Vector3 pos = avatar.AbsolutePosition;
            ILandObject over = GetLandObject(pos.X, pos.Y);
            if (over != null)
            {
                if (!over.IsRestrictedFromLand(avatar.UUID) && (!over.IsBannedFromLand(avatar.UUID) || pos.Z >= LandChannel.BAN_LINE_SAFETY_HIEGHT))
                    avatar.lastKnownAllowedPosition = pos;
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

            if (land != null)
            {
                land.SendAccessList(agentID, sessionID, flags, sequenceID, remote_client);
            }
        }

        public void ClientOnParcelAccessListUpdateRequest(UUID agentID,
                uint flags, int landLocalID, UUID transactionID, int sequenceID,
                int sections, List<LandAccessEntry> entries,
                IClientAPI remote_client)
        {
            // Flags is the list to update, it can mean either the ban or
            // the access list (WTH is a pass list? Mentioned in ParcelFlags)
            //
            // There may be multiple packets, because these can get LONG.
            // Use transactionID to determine a new chain of packets since
            // packets may have come in out of sequence and that would be
            // a big mess if using the sequenceID
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(landLocalID, out land);
            }

            if (land != null)
            {
                GroupPowers requiredPowers = GroupPowers.LandManageAllowed;
                if (flags == (uint)AccessList.Ban)
                    requiredPowers = GroupPowers.LandManageBanned;

                if (m_scene.Permissions.CanEditParcelProperties(agentID,
                        land, requiredPowers))
                {
                    land.UpdateAccessList(flags, transactionID, sequenceID,
                            sections, entries, remote_client);
                }
            }
            else
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Invalid local land ID {0}", landLocalID);
            }
        }

        /// <summary>
        /// Adds a land object to the stored list and adds them to the landIDList to what they own
        /// </summary>
        /// <param name="new_land">
        /// The land object being added.  
        /// Will return null if this overlaps with an existing parcel that has not had its bitmap adjusted.
        /// </param>
        public ILandObject AddLandObject(ILandObject land)
        {
            ILandObject new_land = land.Copy();
            
            // Only now can we add the prim counts to the land object - we rely on the global ID which is generated
            // as a random UUID inside LandData initialization
            if (m_primCountModule != null)
                new_land.PrimCounts = m_primCountModule.GetPrimCounts(new_land.LandData.GlobalID);

            lock (m_landList)
            {
                int newLandLocalID = m_lastLandLocalID + 1;
                new_land.LandData.LocalID = newLandLocalID;

                bool[,] landBitmap = new_land.GetLandBitmap();
                // m_log.DebugFormat("{0} AddLandObject. new_land.bitmapSize=({1},{2}). newLocalID={3}",
                //                 LogHeader, landBitmap.GetLength(0), landBitmap.GetLength(1), newLandLocalID);

                if (landBitmap.GetLength(0) != m_landIDList.GetLength(0) || landBitmap.GetLength(1) != m_landIDList.GetLength(1))
                {
                    // Going to variable sized regions can cause mismatches
                    m_log.ErrorFormat("{0} AddLandObject. Added land bitmap different size than region ID map. bitmapSize=({1},{2}), landIDSize=({3},{4})",
                        LogHeader, landBitmap.GetLength(0), landBitmap.GetLength(1), m_landIDList.GetLength(0), m_landIDList.GetLength(1) );
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
                                            "{0}: Cannot add parcel \"{1}\", local ID {2} at tile {3},{4} because this is still occupied by parcel \"{5}\", local ID {6} in {7}",
                                            LogHeader, new_land.LandData.Name, new_land.LandData.LocalID, x, y, 
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
                                // m_log.DebugFormat(
                                //     "[LAND MANAGEMENT MODULE]: Registering parcel {0} for land co-ord ({1}, {2}) on {3}", 
                                //     new_land.LandData.Name, x, y, m_scene.RegionInfo.RegionName);
                                    
                                m_landIDList[x, y] = newLandLocalID;
                            }
                        }
                    }
                }

                m_landList.Add(newLandLocalID, new_land);
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
            }

            m_scene.EventManager.TriggerLandObjectRemoved(land.LandData.GlobalID);
        }
        
        /// <summary>
        /// Clear the scene of all parcels
        /// </summary>        
        public void Clear(bool setupDefaultParcel)
        {
            List<ILandObject> parcels;
            lock (m_landList)
            {
                parcels = new List<ILandObject>(m_landList.Values);
            }

            foreach (ILandObject lo in parcels)
            {
                //m_scene.SimulationDataService.RemoveLandObject(lo.LandData.GlobalID);
                m_scene.EventManager.TriggerLandObjectRemoved(lo.LandData.GlobalID);
            }

            lock (m_landList)
            {
                m_landList.Clear();

                ResetSimLandObjects();
            }
            
            if (setupDefaultParcel)
                CreateDefaultParcel();
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

            removeLandObject(slave.LandData.LocalID);
            UpdateLandObject(master.LandData.LocalID, master.LandData);
        }

        public ILandObject GetLandObject(int parcelLocalID)
        {
            lock (m_landList)
            {
                if (m_landList.ContainsKey(parcelLocalID))
                {
                    return m_landList[parcelLocalID];
                }
            }
            return null;
        }

        /// <summary>
        /// Get the land object at the specified point
        /// </summary>
        /// <param name="x_float">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y_float">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Land object at the point supplied</returns>
        public ILandObject GetLandObject(float x_float, float y_float)
        {
            return GetLandObject((int)x_float, (int)y_float, true /* returnNullIfLandObjectNotFound */);
            /*
            int x;
            int y;

            if (x_float >= m_scene.RegionInfo.RegionSizeX || x_float < 0 || y_float >= m_scene.RegionInfo.RegionSizeX || y_float < 0)
                return null;

            try
            {
                x = Convert.ToInt32(Math.Floor(Convert.ToDouble(x_float) / (float)landUnit));
                y = Convert.ToInt32(Math.Floor(Convert.ToDouble(y_float) / (float)landUnit));
            }
            catch (OverflowException)
            {
                return null;
            }

            if (x >= (m_scene.RegionInfo.RegionSizeX / landUnit)
                    || y >= (m_scene.RegionInfo.RegionSizeY / landUnit)
                    || x < 0
                    || y < 0)
            {
                return null;
            }

            lock (m_landList)
            {
                // Corner case. If an autoreturn happens during sim startup
                // we will come here with the list uninitialized
                //
//                int landId = m_landIDList[x, y];
                
//                if (landId == 0)
//                    m_log.DebugFormat(
//                        "[LAND MANAGEMENT MODULE]: No land object found at ({0}, {1}) on {2}", 
//                        x, y, m_scene.RegionInfo.RegionName);

                try
                {
                    if (m_landList.ContainsKey(m_landIDList[x, y]))
                        return m_landList[m_landIDList[x, y]];
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("{0} GetLandObject exception. x={1}, y={2}, m_landIDList.len=({3},{4})",
                        LogHeader, x, y, m_landIDList.GetLength(0), m_landIDList.GetLength(1));
                }
                
                return null;
            }
             */
        }

        // Public entry.
        // Throws exception if land object is not found
        public ILandObject GetLandObject(int x, int y)
        {
            return GetLandObject(x, y, false /* returnNullIfLandObjectNotFound */);
        }

        /// <summary>
        /// Given a region position, return the parcel land object for that location
        /// </summary>
        /// <returns>
        /// The land object.
        /// </returns>
        /// <param name='x'></param>
        /// <param name='y'></param>
        /// <param name='returnNullIfLandObjectNotFound'>
        /// Return null if the land object requested is not within the region's bounds.
        /// </param>
        private ILandObject GetLandObject(int x, int y, bool returnNullIfLandObjectOutsideBounds)
        {
            if (x >= m_scene.RegionInfo.RegionSizeX || y >=  m_scene.RegionInfo.RegionSizeY || x < 0 || y < 0)
            {
                // These exceptions here will cause a lot of complaints from the users specifically because
                // they happen every time at border crossings
                if (returnNullIfLandObjectOutsideBounds)
                    return null;
                else
                    throw new Exception(
                        String.Format("{0} GetLandObject for non-existent position. Region={1}, pos=<{2},{3}",
                                                LogHeader, m_scene.RegionInfo.RegionName, x, y)
                );
            }

            return m_landList[m_landIDList[x / 4, y / 4]];
        }

        // Create a 'parcel is here' bitmap for the parcel identified by the passed landID
        private bool[,] CreateBitmapForID(int landID)
        {
            bool[,] ret = new bool[m_landIDList.GetLength(0), m_landIDList.GetLength(1)];

            for (int xx = 0; xx < m_landIDList.GetLength(0); xx++)
                for (int yy = 0; yy < m_landIDList.GetLength(0); yy++)
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
            if (landUnderPrim != null)
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

        public void FinalizeLandPrimCountUpdate()
        {
            //Get Simwide prim count for owner
            Dictionary<UUID, List<LandObject>> landOwnersAndParcels = new Dictionary<UUID, List<LandObject>>();
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    if (!landOwnersAndParcels.ContainsKey(p.LandData.OwnerID))
                    {
                        List<LandObject> tempList = new List<LandObject>();
                        tempList.Add(p);
                        landOwnersAndParcels.Add(p.LandData.OwnerID, tempList);
                    }
                    else
                    {
                        landOwnersAndParcels[p.LandData.OwnerID].Add(p);
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
//            m_log.DebugFormat(
//                "[LAND MANAGEMENT MODULE]: Triggered EventManagerOnParcelPrimCountUpdate() for {0}", 
//                m_scene.RegionInfo.RegionName);
            
            ResetOverMeRecords();
            EntityBase[] entities = m_scene.Entities.GetEntities();
            foreach (EntityBase obj in entities)
            {
                if (obj != null)
                {
                    if ((obj is SceneObjectGroup) && !obj.IsDeleted && !((SceneObjectGroup) obj).IsAttachment)
                    {
                        m_scene.EventManager.TriggerParcelPrimCountAdd((SceneObjectGroup) obj);
                    }
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
        private void subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            //First, lets loop through the points and make sure they are all in the same peice of land
            //Get the land object at start

            ILandObject startLandObject = GetLandObject(start_x, start_y);

            if (startLandObject == null) return;

            //Loop through the points
            try
            {
                int totalX = end_x - start_x;
                int totalY = end_y - start_y;
                for (int y = 0; y < totalY; y++)
                {
                    for (int x = 0; x < totalX; x++)
                    {
                        ILandObject tempLandObject = GetLandObject(start_x + x, start_y + y);
                        if (tempLandObject == null) return;
                        if (tempLandObject != startLandObject) return;
                    }
                }
            }
            catch (Exception)
            {
                return;
            }

            //If we are still here, then they are subdividing within one piece of land
            //Check owner
            if (!m_scene.Permissions.CanEditParcelProperties(attempting_user_id, startLandObject, GroupPowers.LandDivideJoin))
            {
                return;
            }

            //Lets create a new land object with bitmap activated at that point (keeping the old land objects info)
            ILandObject newLand = startLandObject.Copy();
            newLand.LandData.Name = newLand.LandData.Name;
            newLand.LandData.GlobalID = UUID.Random();
            newLand.LandData.Dwell = 0;

            newLand.SetLandBitmap(newLand.GetSquareLandBitmap(start_x, start_y, end_x, end_y));

            //Now, lets set the subdivision area of the original to false
            int startLandObjectIndex = startLandObject.LandData.LocalID;
            lock (m_landList)
            {
                m_landList[startLandObjectIndex].SetLandBitmap(
                    newLand.ModifyLandBitmapSquare(startLandObject.GetLandBitmap(), start_x, start_y, end_x, end_y, false));
                m_landList[startLandObjectIndex].ForceUpdateLandInfo();
            }

            //Now add the new land object
            ILandObject result = AddLandObject(newLand);

            if (result != null)
            {
                UpdateLandObject(startLandObject.LandData.LocalID, startLandObject.LandData);
                result.SendLandUpdateToAvatarsOverMe();
            }
        }

        /// <summary>
        /// Join 2 land objects together
        /// </summary>
        /// <param name="start_x">x value in first piece of land</param>
        /// <param name="start_y">y value in first piece of land</param>
        /// <param name="end_x">x value in second peice of land</param>
        /// <param name="end_y">y value in second peice of land</param>
        /// <param name="attempting_user_id">UUID of the avatar trying to join the land objects</param>
        /// <returns>Returns true if successful</returns>
        private void join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            end_x -= 4;
            end_y -= 4;

            List<ILandObject> selectedLandObjects = new List<ILandObject>();
            int stepYSelected;
            for (stepYSelected = start_y; stepYSelected <= end_y; stepYSelected += 4)
            {
                int stepXSelected;
                for (stepXSelected = start_x; stepXSelected <= end_x; stepXSelected += 4)
                {
                    ILandObject p = GetLandObject(stepXSelected, stepYSelected);

                    if (p != null)
                    {
                        if (!selectedLandObjects.Contains(p))
                        {
                            selectedLandObjects.Add(p);
                        }
                    }
                }
            }
            ILandObject masterLandObject = selectedLandObjects[0];
            selectedLandObjects.RemoveAt(0);

            if (selectedLandObjects.Count < 1)
            {
                return;
            }
            if (!m_scene.Permissions.CanEditParcelProperties(attempting_user_id, masterLandObject, GroupPowers.LandDivideJoin))
            {
                return;
            }
            foreach (ILandObject p in selectedLandObjects)
            {
                if (p.LandData.OwnerID != masterLandObject.LandData.OwnerID)
                {
                    return;
                }
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

            masterLandObject.SendLandUpdateToAvatarsOverMe();
        }

        public void Join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            join(start_x, start_y, end_x, end_y, attempting_user_id);
        }

        public void Subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            subdivide(start_x, start_y, end_x, end_y, attempting_user_id);
        }

        #endregion

        #region Parcel Updating

        /// <summary>
        /// Where we send the ParcelOverlay packet to the client
        /// </summary>
        /// <param name="remote_client">The object representing the client</param>
        public void SendParcelOverlay(IClientAPI remote_client)
        {
            const int LAND_BLOCKS_PER_PACKET = 1024;

            byte[] byteArray = new byte[LAND_BLOCKS_PER_PACKET];
            int byteArrayCount = 0;
            int sequenceID = 0;

            // Layer data is in landUnit (4m) chunks
            for (int y = 0; y < m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize * (Constants.TerrainPatchSize / LandUnit); y++)
            {
                for (int x = 0; x < m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize * (Constants.TerrainPatchSize / LandUnit); x++)
                {
                    byteArray[byteArrayCount] = BuildLayerByte(GetLandObject(x * LandUnit, y * LandUnit), x, y, remote_client);
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

            if (byteArrayCount != 0)
            {
                remote_client.SendLandParcelOverlay(byteArray, sequenceID);
            }
        }

        private byte BuildLayerByte(ILandObject currentParcelBlock, int x, int y, IClientAPI remote_client)
        {
            byte tempByte = 0; //This represents the byte for the current 4x4

            if (currentParcelBlock != null)
            {
                if (currentParcelBlock.LandData.OwnerID == remote_client.AgentId)
                {
                    //Owner Flag
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_OWNED_BY_REQUESTER);
                }
                else if (currentParcelBlock.LandData.SalePrice > 0 &&
                         (currentParcelBlock.LandData.AuthBuyerID == UUID.Zero ||
                          currentParcelBlock.LandData.AuthBuyerID == remote_client.AgentId))
                {
                    //Sale Flag
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_IS_FOR_SALE);
                }
                else if (currentParcelBlock.LandData.OwnerID == UUID.Zero)
                {
                    //Public Flag
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_PUBLIC);
                }
                else
                {
                    //Other Flag
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_OWNED_BY_OTHER);
                }

                //Now for border control

                ILandObject westParcel = null;
                ILandObject southParcel = null;
                if (x > 0)
                {
                    westParcel = GetLandObject((x - 1) * LandUnit, y * LandUnit);
                }
                if (y > 0)
                {
                    southParcel = GetLandObject(x * LandUnit, (y - 1) * LandUnit);
                }

                if (x == 0)
                {
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_WEST);
                }
                else if (westParcel != null && westParcel != currentParcelBlock)
                {
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_WEST);
                }

                if (y == 0)
                {
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH);
                }
                else if (southParcel != null && southParcel != currentParcelBlock)
                {
                    tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH);
                }

            }

            return tempByte;
        }

        public void ClientOnParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id,
                                                    bool snap_selection, IClientAPI remote_client)
        {
            //Get the land objects within the bounds
            List<ILandObject> temp = new List<ILandObject>();
            int inc_x = end_x - start_x;
            int inc_y = end_y - start_y;
            for (int x = 0; x < inc_x; x++)
            {
                for (int y = 0; y < inc_y; y++)
                {
                    ILandObject currentParcel = GetLandObject(start_x + x, start_y + y);

                    if (currentParcel != null)
                    {
                        if (!temp.Contains(currentParcel))
                        {
                            currentParcel.ForceUpdateLandInfo();
                            temp.Add(currentParcel);
                        }
                    }
                }
            }

            int requestResult = LandChannel.LAND_RESULT_SINGLE;
            if (temp.Count > 1)
            {
                requestResult = LandChannel.LAND_RESULT_MULTIPLE;
            }

            for (int i = 0; i < temp.Count; i++)
            {
                temp[i].SendLandProperties(sequence_id, snap_selection, requestResult, remote_client);
            }

            SendParcelOverlay(remote_client);
        }

        public void ClientOnParcelPropertiesUpdateRequest(LandUpdateArgs args, int localID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(localID, out land);
            }

            if (land != null)
            {
                land.UpdateLandProperties(args, remote_client);
                m_scene.EventManager.TriggerOnParcelPropertiesUpdateRequest(args, localID, remote_client);
            }
        }

        public void ClientOnParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            subdivide(west, south, east, north, remote_client.AgentId);
        }

        public void ClientOnParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            join(west, south, east, north, remote_client.AgentId);
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
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                m_scene.EventManager.TriggerParcelPrimCountUpdate();
                m_landList[local_id].SendLandObjectOwners(remote_client);
            }
            else
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Invalid land object {0} passed for parcel object owner request", local_id);
            }
        }

        public void ClientOnParcelGodForceOwner(int local_id, UUID ownerID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                if (m_scene.Permissions.IsGod(remote_client.AgentId))
                {
                    land.LandData.OwnerID = ownerID;
                    land.LandData.GroupID = UUID.Zero;
                    land.LandData.IsGroupOwned = false;
                    land.LandData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);

                    m_scene.ForEachClient(SendParcelOverlay);
                    land.SendLandUpdateToClient(true, remote_client);
                    UpdateLandObject(land.LandData.LocalID, land.LandData);
                }
            }
        }

        public void ClientOnParcelAbandonRequest(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                if (m_scene.Permissions.CanAbandonParcel(remote_client.AgentId, land))
                {
                    land.LandData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                    land.LandData.GroupID = UUID.Zero;
                    land.LandData.IsGroupOwned = false;
                    land.LandData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);
                    
                    m_scene.ForEachClient(SendParcelOverlay);
                    land.SendLandUpdateToClient(true, remote_client);
                    UpdateLandObject(land.LandData.LocalID, land.LandData);
                }
            }
        }

        public void ClientOnParcelReclaim(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                if (m_scene.Permissions.CanReclaimParcel(remote_client.AgentId, land))
                {
                    land.LandData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                    land.LandData.ClaimDate = Util.UnixTimeSinceEpoch();
                    land.LandData.GroupID = UUID.Zero;
                    land.LandData.IsGroupOwned = false;
                    land.LandData.SalePrice = 0;
                    land.LandData.AuthBuyerID = UUID.Zero;
                    land.LandData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);

                    m_scene.ForEachClient(SendParcelOverlay);
                    land.SendLandUpdateToClient(true, remote_client);
                    UpdateLandObject(land.LandData.LocalID, land.LandData);
                }
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
                    m_landList.TryGetValue(e.parcelLocalID, out land);
                }

                if (land != null)
                {
                    land.UpdateLandSold(e.agentId, e.groupId, e.groupOwned, (uint)e.transactionID, e.parcelPrice, e.parcelArea);
                }
            }
        }

        // After receiving a land buy packet, first the data needs to
        // be validated. This method validates the right to buy the
        // parcel

        public void EventManagerOnValidateLandBuy(Object o, EventManager.LandBuyArgs e)
        {
            if (e.landValidated == false)
            {
                ILandObject lob = null;
                lock (m_landList)
                {
                    m_landList.TryGetValue(e.parcelLocalID, out lob);
                }

                if (lob != null)
                {
                    UUID AuthorizedID = lob.LandData.AuthBuyerID;
                    int saleprice = lob.LandData.SalePrice;
                    UUID pOwnerID = lob.LandData.OwnerID;

                    bool landforsale = ((lob.LandData.Flags &
                                         (uint)(ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects)) != 0);
                    if ((AuthorizedID == UUID.Zero || AuthorizedID == e.agentId) && e.parcelPrice >= saleprice && landforsale)
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
        }

        void ClientOnParcelDeedToGroup(int parcelLocalID, UUID groupID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(parcelLocalID, out land);
            }

            if (!m_scene.Permissions.CanDeedParcel(remote_client.AgentId, land))
                return;

            if (land != null)
            {
                land.DeedToGroup(groupID);
            }
        }

        #region Land Object From Storage Functions

        private void EventManagerOnIncomingLandDataFromStorage(List<LandData> data)
        {
//            m_log.DebugFormat(
//                "[LAND MANAGMENT MODULE]: Processing {0} incoming parcels on {1}", data.Count, m_scene.Name);

            // Prevent race conditions from any auto-creation of new parcels for varregions whilst we are still loading
            // the existing parcels.
            lock (m_landList)
            {
                for (int i = 0; i < data.Count; i++)
                    IncomingLandObjectFromStorage(data[i]);

                // Layer data is in landUnit (4m) chunks
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize * (Constants.TerrainPatchSize / LandUnit); y++)
                {
                    for (int x = 0; x < m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize * (Constants.TerrainPatchSize / LandUnit); x++)
                    {
                        if (m_landIDList[x, y] == 0)
                        {
                            if (m_landList.Count == 1)
                            {
                                m_log.DebugFormat(
                                    "[{0}]: Auto-extending land parcel as landID at {1},{2} is 0 and only one land parcel is present in {3}", 
                                    LogHeader, x, y, m_scene.Name);

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
                                    "{0}: Auto-creating land parcel as landID at {1},{2} is 0 and more than one land parcel is present in {3}", 
                                    LogHeader, x, y, m_scene.Name);

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
                                m_log.WarnFormat(
                                    "{0}: Ignoring request to auto-create parcel in {1} as there are no other parcels present", 
                                    LogHeader, m_scene.Name);
                            }
                        }
                    }
                }
            }
        }

        private void IncomingLandObjectFromStorage(LandData data)
        {
            ILandObject new_land = new LandObject(data, m_scene);
            new_land.SetLandBitmapFromByteArray();            
            AddLandObject(new_land);
        }

        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient)
        {
            if (localID != -1)
            {
                ILandObject selectedParcel = null;
                lock (m_landList)
                {
                    m_landList.TryGetValue(localID, out selectedParcel);
                }

                if (selectedParcel == null) 
                    return;

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

                Dictionary<UUID, HashSet<SceneObjectGroup>> returns = new Dictionary<UUID, HashSet<SceneObjectGroup>>();

                foreach (UUID groupID in taskIDs)
                {
                    SceneObjectGroup obj = m_scene.GetSceneObjectGroup(groupID);
                    if (obj != null)
                    {
                        if (!returns.ContainsKey(obj.OwnerID))
                            returns[obj.OwnerID] = new HashSet<SceneObjectGroup>();
                        returns[obj.OwnerID].Add(obj);
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
                    List<SceneObjectGroup> objs2 = new List<SceneObjectGroup>(objs);
                    if (m_scene.Permissions.CanReturnObjects(null, remoteClient.AgentId, objs2))
                    {
                        m_scene.returnObjects(objs2.ToArray(), remoteClient.AgentId);
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
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler(
                "RemoteParcelRequest",
                new RestStreamHandler(
                    "POST",
                    capsBase + remoteParcelRequestPath,
                    (request, path, param, httpRequest, httpResponse)
                        => RemoteParcelRequest(request, path, param, agentID, caps),
                    "RemoteParcelRequest",
                    agentID.ToString()));

            UUID parcelCapID = UUID.Random();
            caps.RegisterHandler(
                "ParcelPropertiesUpdate",
                new RestStreamHandler(
                    "POST",
                    "/CAPS/" + parcelCapID,
                        (request, path, param, httpRequest, httpResponse)
                            => ProcessPropertiesUpdate(request, path, param, agentID, caps),
                    "ParcelPropertiesUpdate",
                    agentID.ToString()));
        }
        private string ProcessPropertiesUpdate(string request, string path, string param, UUID agentID, Caps caps)
        {
            IClientAPI client;
            if (!m_scene.TryGetClient(agentID, out client)) 
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Unable to retrieve IClientAPI for {0}", agentID);
                return LLSDHelpers.SerialiseLLSDReply(new LLSDEmpty());
            }

            ParcelPropertiesUpdateMessage properties = new ParcelPropertiesUpdateMessage();
            OpenMetaverse.StructuredData.OSDMap args = (OpenMetaverse.StructuredData.OSDMap) OSDParser.DeserializeLLSDXml(request);

            properties.Deserialize(args);

            LandUpdateArgs land_update = new LandUpdateArgs();
            int parcelID = properties.LocalID;
            land_update.AuthBuyerID = properties.AuthBuyerID;
            land_update.Category = properties.Category;
            land_update.Desc = properties.Desc;
            land_update.GroupID = properties.GroupID;
            land_update.LandingType = (byte) properties.Landing;
            land_update.MediaAutoScale = (byte) Convert.ToInt32(properties.MediaAutoScale);
            land_update.MediaID = properties.MediaID;
            land_update.MediaURL = properties.MediaURL;
            land_update.MusicURL = properties.MusicURL;
            land_update.Name = properties.Name;
            land_update.ParcelFlags = (uint) properties.ParcelFlags;
            land_update.PassHours = (int) properties.PassHours;
            land_update.PassPrice = (int) properties.PassPrice;
            land_update.SalePrice = (int) properties.SalePrice;
            land_update.SnapshotID = properties.SnapshotID;
            land_update.UserLocation = properties.UserLocation;
            land_update.UserLookAt = properties.UserLookAt;
            land_update.MediaDescription = properties.MediaDesc;
            land_update.MediaType = properties.MediaType;
            land_update.MediaWidth = properties.MediaWidth;
            land_update.MediaHeight = properties.MediaHeight;
            land_update.MediaLoop = properties.MediaLoop;
            land_update.ObscureMusic = properties.ObscureMusic;
            land_update.ObscureMedia = properties.ObscureMedia;

            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(parcelID, out land);
            }

            if (land != null)
            {
                land.UpdateLandProperties(land_update, client);
                m_scene.EventManager.TriggerOnParcelPropertiesUpdateRequest(land_update, parcelID, client);
            }
            else
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Unable to find parcelID {0}", parcelID);
            }
            return LLSDHelpers.SerialiseLLSDReply(new LLSDEmpty());
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
        private string RemoteParcelRequest(string request, string path, string param, UUID agentID, Caps caps)
        {
            UUID parcelID = UUID.Zero;
            try
            {
                Hashtable hash = new Hashtable();
                hash = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));
                if (hash.ContainsKey("region_id") && hash.ContainsKey("location"))
                {
                    UUID regionID = (UUID)hash["region_id"];
                    ArrayList list = (ArrayList)hash["location"];
                    uint x = (uint)(double)list[0];
                    uint y = (uint)(double)list[1];
                    if (hash.ContainsKey("region_handle"))
                    {
                        // if you do a "About Landmark" on a landmark a second time, the viewer sends the
                        // region_handle it got earlier via RegionHandleRequest
                        ulong regionHandle = Util.BytesToUInt64Big((byte[])hash["region_handle"]);
                        parcelID = Util.BuildFakeParcelID(regionHandle, x, y);
                    }
                    else if (regionID == m_scene.RegionInfo.RegionID)
                    {
                        // a parcel request for a local parcel => no need to query the grid
                        parcelID = Util.BuildFakeParcelID(m_scene.RegionInfo.RegionHandle, x, y);
                    }
                    else
                    {
                        // a parcel request for a parcel in another region. Ask the grid about the region
                        GridRegion info = m_scene.GridService.GetRegionByUUID(UUID.Zero, regionID);
                        if (info != null)
                            parcelID = Util.BuildFakeParcelID(info.RegionHandle, x, y);
                    }
                }
            }
            catch (LLSD.LLSDParseException e)
            {
                m_log.ErrorFormat("[LAND MANAGEMENT MODULE]: Fetch error: {0}", e.Message);
                m_log.ErrorFormat("[LAND MANAGEMENT MODULE]: ... in request {0}", request);
            }
            catch (InvalidCastException)
            {
                m_log.ErrorFormat("[LAND MANAGEMENT MODULE]: Wrong type in request {0}", request);
            }

            LLSDRemoteParcelResponse response = new LLSDRemoteParcelResponse();
            response.parcel_id = parcelID;
            m_log.DebugFormat("[LAND MANAGEMENT MODULE]: Got parcelID {0}", parcelID);

            return LLSDHelpers.SerialiseLLSDReply(response);
        }

        #endregion

        private void ClientOnParcelInfoRequest(IClientAPI remoteClient, UUID parcelID)
        {
            if (parcelID == UUID.Zero)
                return;

            ExtendedLandData data = (ExtendedLandData)parcelInfoCache.Get(parcelID.ToString(),
                    delegate(string id)
                    {
                        UUID parcel = UUID.Zero;
                        UUID.TryParse(id, out parcel);
                        // assume we've got the parcelID we just computed in RemoteParcelRequest
                        ExtendedLandData extLandData = new ExtendedLandData();
                        Util.ParseFakeParcelID(parcel, out extLandData.RegionHandle,
                                               out extLandData.X, out extLandData.Y);
                        m_log.DebugFormat("[LAND MANAGEMENT MODULE]: Got parcelinfo request for regionHandle {0}, x/y {1}/{2}",
                                          extLandData.RegionHandle, extLandData.X, extLandData.Y);

                        // for this region or for somewhere else?
                        if (extLandData.RegionHandle == m_scene.RegionInfo.RegionHandle)
                        {
                            extLandData.LandData = this.GetLandObject(extLandData.X, extLandData.Y).LandData;
                            extLandData.RegionAccess = m_scene.RegionInfo.AccessLevel;
                        }
                        else
                        {
                            ILandService landService = m_scene.RequestModuleInterface<ILandService>();
                            extLandData.LandData = landService.GetLandData(m_scene.RegionInfo.ScopeID,
                                    extLandData.RegionHandle,
                                    extLandData.X,
                                    extLandData.Y,
                                    out extLandData.RegionAccess);
                            if (extLandData.LandData == null)
                            {
                                // we didn't find the region/land => don't cache
                                return null;
                            }
                        }
                        return extLandData;
                    });

            if (data != null)  // if we found some data, send it
            {
                GridRegion info;
                if (data.RegionHandle == m_scene.RegionInfo.RegionHandle)
                {
                    info = new GridRegion(m_scene.RegionInfo);
                }
                else
                {
                    // most likely still cached from building the extLandData entry
                    uint x = 0, y = 0;
                    Util.RegionHandleToWorldLoc(data.RegionHandle, out x, out y);
                    info = m_scene.GridService.GetRegionByPosition(m_scene.RegionInfo.ScopeID, (int)x, (int)y);
                }
                // we need to transfer the fake parcelID, not the one in landData, so the viewer can match it to the landmark.
                m_log.DebugFormat("[LAND MANAGEMENT MODULE]: got parcelinfo for parcel {0} in region {1}; sending...",
                                  data.LandData.Name, data.RegionHandle);
                // HACK for now
                RegionInfo r = new RegionInfo();
                r.RegionName = info.RegionName;
                r.RegionLocX = (uint)info.RegionLocX;
                r.RegionLocY = (uint)info.RegionLocY;
                r.RegionSettings.Maturity = (int)Util.ConvertAccessLevelToMaturity(data.RegionAccess);
                remoteClient.SendParcelInfo(r, data.LandData, parcelID, data.X, data.Y);
            }
            else
                m_log.Debug("[LAND MANAGEMENT MODULE]: got no parcelinfo; not sending");
        }

        public void setParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(localID, out land);
            }

            if (land == null) return;

            if (!m_scene.Permissions.CanEditParcelProperties(remoteClient.AgentId, land, GroupPowers.LandOptions))
                return;

            land.LandData.OtherCleanTime = otherCleanTime;

            UpdateLandObject(localID, land.LandData);
        }
        
        Dictionary<UUID, System.Threading.Timer> Timers = new Dictionary<UUID, System.Threading.Timer>();

        public void ClientOnParcelFreezeUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            ScenePresence targetAvatar = null;
            ((Scene)client.Scene).TryGetScenePresence(target, out targetAvatar);
            ScenePresence parcelManager = null;
            ((Scene)client.Scene).TryGetScenePresence(client.AgentId, out parcelManager);
            System.Threading.Timer Timer;

            if (targetAvatar.UserLevel == 0)
            {
                ILandObject land = ((Scene)client.Scene).LandChannel.GetLandObject(targetAvatar.AbsolutePosition.X, targetAvatar.AbsolutePosition.Y);
                if (!((Scene)client.Scene).Permissions.CanEditParcelProperties(client.AgentId, land, GroupPowers.LandEjectAndFreeze))
                    return;
                if (flags == 0)
                {
                    targetAvatar.AllowMovement = false;
                    targetAvatar.ControllingClient.SendAlertMessage(parcelManager.Firstname + " " + parcelManager.Lastname + " has frozen you for 30 seconds.  You cannot move or interact with the world.");
                    parcelManager.ControllingClient.SendAlertMessage("Avatar Frozen.");
                    System.Threading.TimerCallback timeCB = new System.Threading.TimerCallback(OnEndParcelFrozen);
                    Timer = new System.Threading.Timer(timeCB, targetAvatar, 30000, 0);
                    Timers.Add(targetAvatar.UUID, Timer);
                }
                else
                {
                    targetAvatar.AllowMovement = true;
                    targetAvatar.ControllingClient.SendAlertMessage(parcelManager.Firstname + " " + parcelManager.Lastname + " has unfrozen you.");
                    parcelManager.ControllingClient.SendAlertMessage("Avatar Unfrozen.");
                    Timers.TryGetValue(targetAvatar.UUID, out Timer);
                    Timers.Remove(targetAvatar.UUID);
                    Timer.Dispose();
                }
            }
        }

        private void OnEndParcelFrozen(object avatar)
        {
            ScenePresence targetAvatar = (ScenePresence)avatar;
            targetAvatar.AllowMovement = true;
            System.Threading.Timer Timer;
            Timers.TryGetValue(targetAvatar.UUID, out Timer);
            Timers.Remove(targetAvatar.UUID);
            targetAvatar.ControllingClient.SendAgentAlertMessage("The freeze has worn off; you may go about your business.", false);
        }

        public void ClientOnParcelEjectUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            ScenePresence targetAvatar = null;
            ScenePresence parcelManager = null;

            // Must have presences
            if (!m_scene.TryGetScenePresence(target, out targetAvatar) ||
                !m_scene.TryGetScenePresence(client.AgentId, out parcelManager))
                return;

            // Cannot eject estate managers or gods
            if (m_scene.Permissions.IsAdministrator(target))
                return;

            // Check if you even have permission to do this
            ILandObject land = m_scene.LandChannel.GetLandObject(targetAvatar.AbsolutePosition.X, targetAvatar.AbsolutePosition.Y);
            if (!m_scene.Permissions.CanEditParcelProperties(client.AgentId, land, GroupPowers.LandEjectAndFreeze) &&
                !m_scene.Permissions.IsAdministrator(client.AgentId))
                return;
            Vector3 pos = m_scene.GetNearestAllowedPosition(targetAvatar, land);

            targetAvatar.TeleportWithMomentum(pos, null);
            targetAvatar.ControllingClient.SendAlertMessage("You have been ejected by " + parcelManager.Firstname + " " + parcelManager.Lastname);
            parcelManager.ControllingClient.SendAlertMessage("Avatar Ejected.");

            if ((flags & 1) != 0) // Ban TODO: Remove magic number
            {
                LandAccessEntry entry = new LandAccessEntry();
                entry.AgentID = targetAvatar.UUID;
                entry.Flags = AccessList.Ban;
                entry.Expires = 0; // Perm

                land.LandData.ParcelAccessList.Add(entry);
            }
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
            ILandObject land = landChannel.GetLandObject(position);
            if (land == null || m_scene.GridUserService == null)
            {
                m_Dialog.SendAlertToUser(remoteClient, "Set Home request failed.");
                return;
            }

            // Gather some data
            ulong gpowers = remoteClient.GetGroupPowers(land.LandData.GroupID);
            SceneObjectGroup telehub = null;
            if (m_scene.RegionInfo.RegionSettings.TelehubObject != UUID.Zero)
                // Does the telehub exist in the scene?
                telehub = m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject);

            // Can the user set home here?
            if (// Required: local user; foreign users cannot set home
                m_scene.UserManagementModule.IsLocalGridUser(remoteClient.AgentId) &&
                (// (a) gods and land managers can set home
                 m_scene.Permissions.IsAdministrator(remoteClient.AgentId) || 
                 m_scene.Permissions.IsGod(remoteClient.AgentId) ||
                 // (b) land owners can set home
                 remoteClient.AgentId == land.LandData.OwnerID ||
                 // (c) members of the land-associated group in roles that can set home
                 ((gpowers & (ulong)GroupPowers.AllowSetHome) == (ulong)GroupPowers.AllowSetHome) ||
                 // (d) parcels with telehubs can be the home of anyone
                 (telehub != null && land.ContainsPoint((int)telehub.AbsolutePosition.X, (int)telehub.AbsolutePosition.Y))))
            {
                string userId;
                UUID test;
                if (!m_scene.UserManagementModule.GetUserUUI(remoteClient.AgentId, out userId))
                {
                    /* Do not set a home position in this grid for a HG visitor */
                    m_Dialog.SendAlertToUser(remoteClient, "Set Home request failed. (User Lookup)");
                }
                else if (!UUID.TryParse(userId, out test))
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
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            string response = MainConsole.Instance.CmdPrompt(
                string.Format(
                    "Are you sure that you want to clear all land parcels from {0} (y or n)", m_scene.Name), 
                "n");
            
            if (response.ToLower() == "y")
            {
                Clear(true);
                MainConsole.Instance.OutputFormat("Cleared all parcels from {0}", m_scene.Name);
            }
            else
            {
                MainConsole.Instance.OutputFormat("Aborting clear of all parcels from {0}", m_scene.Name);
            }
        }        
        
        protected void HandleShowCommand(string module, string[] args)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            StringBuilder report = new StringBuilder(); 

            if (args.Length <= 2)
            {
                AppendParcelsSummaryReport(report);
            }
            else
            {
                int landLocalId;

                if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, args[2], out landLocalId))
                    return;

                ILandObject lo;

                lock (m_landList)
                { 
                    if (!m_landList.TryGetValue(landLocalId, out lo))
                    {
                        MainConsole.Instance.OutputFormat("No parcel found with local ID {0}", landLocalId);
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

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Parcel Name", ConsoleDisplayUtil.ParcelNameSize);
            cdt.AddColumn("ID", 3);
            cdt.AddColumn("Area", 6);
            cdt.AddColumn("Starts", ConsoleDisplayUtil.VectorSize);
            cdt.AddColumn("Ends", ConsoleDisplayUtil.VectorSize);
            cdt.AddColumn("Owner", ConsoleDisplayUtil.UserNameSize);
            
            lock (m_landList)
            {
                foreach (ILandObject lo in m_landList.Values)
                {
                    LandData ld = lo.LandData;
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
                    cdt.AddRow(
                        ld.Name, ld.LocalID, ld.Area, lo.StartPoint, lo.EndPoint, ownerName);
                }
            }

            report.Append(cdt.ToString());
        } 

        private void AppendParcelReport(StringBuilder report, ILandObject lo)
        {
            LandData ld = lo.LandData;

            ConsoleDisplayList cdl = new ConsoleDisplayList();
            cdl.AddRow("Parcel name", ld.Name);
            cdl.AddRow("Local ID", ld.LocalID);
            cdl.AddRow("Description", ld.Description);
            cdl.AddRow("Snapshot ID", ld.SnapshotID);
            cdl.AddRow("Area", ld.Area);
            cdl.AddRow("Starts", lo.StartPoint);
            cdl.AddRow("Ends", lo.EndPoint);
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
            IPrimCounts pc = lo.PrimCounts;
            cdl.AddRow("Owner Prims", pc.Owner);
            cdl.AddRow("Group Prims", pc.Group);
            cdl.AddRow("Other Prims", pc.Others);
            cdl.AddRow("Selected Prims", pc.Selected);
            cdl.AddRow("Total Prims", pc.Total);

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
            cdl.AddRow("Obscure Media", ld.ObscureMedia);

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
