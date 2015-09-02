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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using RegionFlags = OpenMetaverse.RegionFlags;

namespace OpenSim.Region.CoreModules.World.Land
{
    /// <summary>
    /// Keeps track of a specific piece of land's information
    /// </summary>
    public class LandObject : ILandObject
    {
        #region Member Variables

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[LAND OBJECT]";

        private readonly int landUnit = 4;

        private int m_lastSeqId = 0;
        private int m_expiryCounter = 0;

        protected Scene m_scene;
        protected List<SceneObjectGroup> primsOverMe = new List<SceneObjectGroup>();
        protected Dictionary<uint, UUID> m_listTransactions = new Dictionary<uint, UUID>();

        protected ExpiringCache<UUID, bool> m_groupMemberCache = new ExpiringCache<UUID, bool>();
        protected TimeSpan m_groupMemberCacheTimeout = TimeSpan.FromSeconds(30);  // cache invalidation after 30 seconds

        private bool[,] m_landBitmap;
        public bool[,] LandBitmap
        {
            get { return m_landBitmap; }
            set { m_landBitmap = value; }
        }

        #endregion

        public int GetPrimsFree()
        {
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            int free = GetSimulatorMaxPrimCount() - LandData.SimwidePrims;
            return free;
        }

        protected LandData m_landData;        
        public LandData LandData
        {
            get { return m_landData; }

            set { m_landData = value; }
        }

        public IPrimCounts PrimCounts { get; set; }

        public UUID RegionUUID
        {
            get { return m_scene.RegionInfo.RegionID; }
        }

        public Vector3 StartPoint
        {
            get
            {
                for (int y = 0; y < LandBitmap.GetLength(1); y++)
                {
                    for (int x = 0; x < LandBitmap.GetLength(0); x++)
                    {
                        if (LandBitmap[x, y])
                            return new Vector3(x * landUnit, y * landUnit, 0);
                    }
                }

                m_log.ErrorFormat("{0} StartPoint. No start point found. bitmapSize=<{1},{2}>",
                                    LogHeader, LandBitmap.GetLength(0), LandBitmap.GetLength(1));
                return new Vector3(-1, -1, -1);
            }
        }

        public Vector3 EndPoint
        {
            get
            {
                for (int y = LandBitmap.GetLength(1) - 1; y >= 0; y--)
                {
                    for (int x = LandBitmap.GetLength(0) - 1; x >= 0; x--)
                    {
                        if (LandBitmap[x, y])
                        {
                            return new Vector3(x * landUnit + landUnit, y * landUnit + landUnit, 0);
                        }
                    }
                }

                m_log.ErrorFormat("{0} EndPoint. No end point found. bitmapSize=<{1},{2}>",
                                    LogHeader, LandBitmap.GetLength(0), LandBitmap.GetLength(1));
                return new Vector3(-1, -1, -1);
            }
        }

        #region Constructors

        public LandObject(LandData landData, Scene scene)
        {
            LandData = landData.Copy();
            m_scene = scene;
        }

        public LandObject(UUID owner_id, bool is_group_owned, Scene scene)
        {
            m_scene = scene;
            if (m_scene == null)
                LandBitmap = new bool[Constants.RegionSize / landUnit, Constants.RegionSize / landUnit];
            else
                LandBitmap = new bool[m_scene.RegionInfo.RegionSizeX / landUnit, m_scene.RegionInfo.RegionSizeY / landUnit];

            LandData = new LandData(); 
            LandData.OwnerID = owner_id;
            if (is_group_owned)
                LandData.GroupID = owner_id;
            else
                LandData.GroupID = UUID.Zero;
            LandData.IsGroupOwned = is_group_owned;
            
            m_scene.EventManager.OnFrame += OnFrame;
        }

        #endregion

        #region Member Functions

        #region General Functions
        
        /// <summary>
        /// Checks to see if this land object contains a point
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>Returns true if the piece of land contains the specified point</returns>
        public bool ContainsPoint(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < m_scene.RegionInfo.RegionSizeX && y < m_scene.RegionInfo.RegionSizeY)
            {
                return LandBitmap[x / landUnit, y / landUnit];
            }
            else
            {
                return false;
            }
        }

        public ILandObject Copy()
        {
            ILandObject newLand = new LandObject(LandData, m_scene);
            newLand.LandBitmap = (bool[,]) (LandBitmap.Clone());
            return newLand;
        }

        static overrideParcelMaxPrimCountDelegate overrideParcelMaxPrimCount;
        static overrideSimulatorMaxPrimCountDelegate overrideSimulatorMaxPrimCount;

        public void SetParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            overrideParcelMaxPrimCount = overrideDel;
        }
        public void SetSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            overrideSimulatorMaxPrimCount = overrideDel;
        }

        public int GetParcelMaxPrimCount()
        {
            if (overrideParcelMaxPrimCount != null)
            {
                return overrideParcelMaxPrimCount(this);
            }
            else
            {
                // Normal Calculations
                int parcelMax = (int)( (long)LandData.Area
                              * (long)m_scene.RegionInfo.ObjectCapacity
                              * (long)m_scene.RegionInfo.RegionSettings.ObjectBonus
                              / (long)(m_scene.RegionInfo.RegionSizeX * m_scene.RegionInfo.RegionSizeY) );
                //m_log.DebugFormat("Area: {0}, Capacity {1}, Bonus {2}, Parcel {3}", LandData.Area, m_scene.RegionInfo.ObjectCapacity, m_scene.RegionInfo.RegionSettings.ObjectBonus, parcelMax);
                return parcelMax;
            }
        }

        private int GetParcelBasePrimCount()
        {
            if (overrideParcelMaxPrimCount != null)
            {
                return overrideParcelMaxPrimCount(this);
            }
            else
            {
                // Normal Calculations
                int parcelMax = (int)((long)LandData.Area
                              * (long)m_scene.RegionInfo.ObjectCapacity
                              / 65536L);
                return parcelMax;
            }
        }

        public int GetSimulatorMaxPrimCount()
        {
            if (overrideSimulatorMaxPrimCount != null)
            {
                return overrideSimulatorMaxPrimCount(this);
            }
            else
            {
                //Normal Calculations
                int simMax = (int)(   (long)LandData.SimwideArea
                                    * (long)m_scene.RegionInfo.ObjectCapacity
                                    / (long)(m_scene.RegionInfo.RegionSizeX * m_scene.RegionInfo.RegionSizeY) );
                // m_log.DebugFormat("Simwide Area: {0}, Capacity {1}, SimMax {2}", LandData.SimwideArea, m_scene.RegionInfo.ObjectCapacity, simMax);
                return simMax;
            }
        }
        
        #endregion

        #region Packet Request Handling

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
        {
            if (remote_client.SceneAgent.PresenceType == PresenceType.Npc)
                return;

            IEstateModule estateModule = m_scene.RequestModuleInterface<IEstateModule>();
            // uint regionFlags = 336723974 & ~((uint)(RegionFlags.AllowLandmark | RegionFlags.AllowSetHome));
            uint regionFlags = (uint)(RegionFlags.PublicAllowed
                                    | RegionFlags.AllowDirectTeleport
                                    | RegionFlags.AllowParcelChanges
                                    | RegionFlags.AllowVoice );

            if (estateModule != null)
                regionFlags = estateModule.GetRegionFlags();

            int seq_id;
            if (snap_selection && (sequence_id == 0))
            {
                seq_id = m_lastSeqId;
            }
            else
            {
                seq_id = sequence_id;
                m_lastSeqId = seq_id;
            }

            remote_client.SendLandProperties(seq_id,
                    snap_selection, request_result, this,
                    (float)m_scene.RegionInfo.RegionSettings.ObjectBonus,
                    GetParcelBasePrimCount(),
                    GetSimulatorMaxPrimCount(), regionFlags);
        }

        public bool UpdateLandProperties(LandUpdateArgs args, IClientAPI remote_client, out bool snap_selection, out bool needOverlay)
        {
            //Needs later group support
            snap_selection = false;
            needOverlay = false;
            LandData newData = LandData.Copy();

            uint allowedDelta = 0;

            // These two are always blocked as no client can set them anyway
            // ParcelFlags.ForSaleObjects
            // ParcelFlags.LindenHome

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions, false))
            {
                allowedDelta |= (uint)(ParcelFlags.AllowLandmark |
                        ParcelFlags.AllowTerraform |
                        ParcelFlags.AllowDamage |
                        ParcelFlags.CreateObjects |
                        ParcelFlags.RestrictPushObject |
                        ParcelFlags.AllowOtherScripts |
                        ParcelFlags.AllowGroupScripts |
                        ParcelFlags.CreateGroupObjects |
                        ParcelFlags.AllowAPrimitiveEntry |
                        ParcelFlags.AllowGroupObjectEntry |
                        ParcelFlags.AllowFly);
                newData.SeeAVs = args.SeeAVs;
                newData.AnyAVSounds = args.AnyAVSounds;
                newData.GroupAVSounds = args.GroupAVSounds;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandSetSale, true))
            {
                if (args.AuthBuyerID != newData.AuthBuyerID ||
                    args.SalePrice != newData.SalePrice)
                {
                    snap_selection = true;
                }

                newData.AuthBuyerID = args.AuthBuyerID;
                newData.SalePrice = args.SalePrice;

                if (!LandData.IsGroupOwned)
                {
                    newData.GroupID = args.GroupID;

                    allowedDelta |= (uint)(ParcelFlags.AllowDeedToGroup |
                            ParcelFlags.ContributeWithDeed |
                            ParcelFlags.SellParcelObjects);
                }

                allowedDelta |= (uint)ParcelFlags.ForSale;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.FindPlaces, false))
            {
                newData.Category = args.Category;

                allowedDelta |= (uint)(ParcelFlags.ShowDirectory |
                        ParcelFlags.AllowPublish |
                        ParcelFlags.MaturePublish) | (uint)(1 << 23);
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.LandChangeIdentity, false))
            {
                newData.Description = args.Desc;
                newData.Name = args.Name;
                newData.SnapshotID = args.SnapshotID;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.SetLandingPoint, false))
            {
                newData.LandingType = args.LandingType;
                newData.UserLocation = args.UserLocation;
                newData.UserLookAt = args.UserLookAt;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.ChangeMedia, false))
            {
                newData.MediaAutoScale = args.MediaAutoScale;
                newData.MediaID = args.MediaID;
                newData.MediaURL = args.MediaURL;
                newData.MusicURL = args.MusicURL;
                newData.MediaType = args.MediaType;
                newData.MediaDescription = args.MediaDescription;
                newData.MediaWidth = args.MediaWidth;
                newData.MediaHeight = args.MediaHeight;
                newData.MediaLoop = args.MediaLoop;
                newData.ObscureMusic = args.ObscureMusic;
                newData.ObscureMedia = args.ObscureMedia;

                allowedDelta |= (uint)(ParcelFlags.SoundLocal |
                        ParcelFlags.UrlWebPage |
                        ParcelFlags.UrlRawHtml |
                        ParcelFlags.AllowVoiceChat |
                        ParcelFlags.UseEstateVoiceChan);
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.LandManagePasses, false))
            {
                newData.PassHours = args.PassHours;
                newData.PassPrice = args.PassPrice;

                allowedDelta |= (uint)ParcelFlags.UsePassList;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandManageAllowed, false))
            {
                allowedDelta |= (uint)(ParcelFlags.UseAccessGroup |
                        ParcelFlags.UseAccessList);
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandManageBanned, false))
            {
                allowedDelta |= (uint)(ParcelFlags.UseBanList |
                        ParcelFlags.DenyAnonymous |
                        ParcelFlags.DenyAgeUnverified);
            }

            if (allowedDelta != (uint)ParcelFlags.None)
            {
                uint preserve = LandData.Flags & ~allowedDelta;
                newData.Flags = preserve | (args.ParcelFlags & allowedDelta);

                uint curdelta = LandData.Flags ^ newData.Flags;
                curdelta &= (uint)(ParcelFlags.SoundLocal);

                if(curdelta != 0 || newData.SeeAVs != LandData.SeeAVs)
                    needOverlay = true;

                m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);
                return true; 
            }
            return false;
        }

        public void UpdateLandSold(UUID avatarID, UUID groupID, bool groupOwned, uint AuctionID, int claimprice, int area)
        {
            LandData newData = LandData.Copy();
            newData.OwnerID = avatarID;
            newData.GroupID = groupID;
            newData.IsGroupOwned = groupOwned;
            //newData.auctionID = AuctionID;
            newData.ClaimDate = Util.UnixTimeSinceEpoch();
            newData.ClaimPrice = claimprice;
            newData.SalePrice = 0;
            newData.AuthBuyerID = UUID.Zero;
            newData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);

            bool sellObjects = (LandData.Flags & (uint)(ParcelFlags.SellParcelObjects)) != 0
                && !LandData.IsGroupOwned && !groupOwned;
            UUID previousOwner = LandData.OwnerID;

            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);
//            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            SendLandUpdateToAvatarsOverMe(true);

            if (sellObjects) SellLandObjects(previousOwner);
        }

        public void DeedToGroup(UUID groupID)
        {
            LandData newData = LandData.Copy();
            newData.OwnerID = groupID;
            newData.GroupID = groupID;
            newData.IsGroupOwned = true;

            // Reset show in directory flag on deed
            newData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects | ParcelFlags.ShowDirectory);

            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            SendLandUpdateToAvatarsOverMe(true);
        }

        public bool IsEitherBannedOrRestricted(UUID avatar)
        {
            if (IsBannedFromLand(avatar))
            {
                return true;
            }
            else if (IsRestrictedFromLand(avatar))
            {
                return true;
            }
            return false;
        }

        public bool CanBeOnThisLand(UUID avatar, float posHeight)
        {
            if (posHeight < LandChannel.BAN_LINE_SAFETY_HIEGHT && IsBannedFromLand(avatar))
            {
                return false;
            }
            else if (IsRestrictedFromLand(avatar))
            {
                return false;
            }
            return true;
        }

        public bool HasGroupAccess(UUID avatar)
        {
            if (LandData.GroupID != UUID.Zero && (LandData.Flags & (uint)ParcelFlags.UseAccessGroup) == (uint)ParcelFlags.UseAccessGroup)
            {
                ScenePresence sp;
                if (!m_scene.TryGetScenePresence(avatar, out sp))
                {
                    bool isMember;
                    if (m_groupMemberCache.TryGetValue(avatar, out isMember))
                    {
                        m_groupMemberCache.Update(avatar, isMember, m_groupMemberCacheTimeout);
                        return isMember;
                    }

                    IGroupsModule groupsModule = m_scene.RequestModuleInterface<IGroupsModule>();
                    if (groupsModule == null)
                        return false;

                    GroupMembershipData[] membership = groupsModule.GetMembershipData(avatar);
                    if (membership == null || membership.Length == 0)
                    {
                        m_groupMemberCache.Add(avatar, false, m_groupMemberCacheTimeout);
                        return false;
                    }

                    foreach (GroupMembershipData d in membership)
                    {
                        if (d.GroupID == LandData.GroupID)
                        {
                            m_groupMemberCache.Add(avatar, true, m_groupMemberCacheTimeout);
                            return true;
                        }
                    }
                    m_groupMemberCache.Add(avatar, false, m_groupMemberCacheTimeout);
                    return false;
                }

                return sp.ControllingClient.IsGroupMember(LandData.GroupID);
            }
            return false;
        }

        public bool IsBannedFromLand(UUID avatar)
        {
            ExpireAccessList();

            if (m_scene.Permissions.IsAdministrator(avatar))
                return false;

            if (m_scene.RegionInfo.EstateSettings.IsEstateManagerOrOwner(avatar))
                return false;

            if (avatar == LandData.OwnerID)
                return false;

            if ((LandData.Flags & (uint) ParcelFlags.UseBanList) > 0)
            {
                if (LandData.ParcelAccessList.FindIndex(
                        delegate(LandAccessEntry e)
                        {
                            if (e.AgentID == avatar && e.Flags == AccessList.Ban)
                                return true;
                            return false;
                        }) != -1)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsRestrictedFromLand(UUID avatar)
        {
            if ((LandData.Flags & (uint) ParcelFlags.UseAccessList) == 0)
                return false;

            if (m_scene.Permissions.IsAdministrator(avatar))
                return false;

            if (m_scene.RegionInfo.EstateSettings.IsEstateManagerOrOwner(avatar))
                return false;

            if (avatar == LandData.OwnerID)
                return false;

            if (HasGroupAccess(avatar))
                return false;

            return !IsInLandAccessList(avatar);
        }

        public bool IsInLandAccessList(UUID avatar)
        {
            ExpireAccessList();

            if (LandData.ParcelAccessList.FindIndex(
                    delegate(LandAccessEntry e)
                    {
                        if (e.AgentID == avatar && e.Flags == AccessList.Access)
                            return true;
                        return false;
                    }) == -1)
            {
                return false;
            }
            return true;
        }

        public void SendLandUpdateToClient(IClientAPI remote_client)
        {
            SendLandProperties(0, false, 0, remote_client);
        }

        public void SendLandUpdateToClient(bool snap_selection, IClientAPI remote_client)
        {
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            SendLandProperties(0, snap_selection, 0, remote_client);
        }

        public void SendLandUpdateToAvatarsOverMe()
        {
            SendLandUpdateToAvatarsOverMe(false);
        }

        public void SendLandUpdateToAvatarsOverMe(bool snap_selection)
        {
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            m_scene.ForEachRootScenePresence(delegate(ScenePresence avatar)
            {
                ILandObject over = null;
                try
                {
                    over =
                        m_scene.LandChannel.GetLandObject(Util.Clamp<int>((int)Math.Round(avatar.AbsolutePosition.X), 0, ((int)m_scene.RegionInfo.RegionSizeX - 1)),
                                                          Util.Clamp<int>((int)Math.Round(avatar.AbsolutePosition.Y), 0, ((int)m_scene.RegionInfo.RegionSizeY - 1)));
                }
                catch (Exception)
                {
                    m_log.Warn("[LAND]: " + "unable to get land at x: " + Math.Round(avatar.AbsolutePosition.X) + " y: " +
                               Math.Round(avatar.AbsolutePosition.Y));
                }

                if (over != null)
                {
                    if (over.LandData.LocalID == LandData.LocalID)
                    {
                        if (((over.LandData.Flags & (uint)ParcelFlags.AllowDamage) != 0) &&
                            m_scene.RegionInfo.RegionSettings.AllowDamage)
                            avatar.Invulnerable = false;
                        else
                            avatar.Invulnerable = true;

                        SendLandUpdateToClient(snap_selection, avatar.ControllingClient);
                        avatar.currentParcelUUID = LandData.GlobalID;
                    }
                }
            });
        }

        #endregion

        #region AccessList Functions

        public List<LandAccessEntry>  CreateAccessListArrayByFlag(AccessList flag)
        {
            ExpireAccessList();

            List<LandAccessEntry> list = new List<LandAccessEntry>();
            foreach (LandAccessEntry entry in LandData.ParcelAccessList)
            {
                if (entry.Flags == flag)
                   list.Add(entry);
            }
            if (list.Count == 0)
            {
                LandAccessEntry e = new LandAccessEntry();
                e.AgentID = UUID.Zero;
                e.Flags = 0;
                e.Expires = 0;

                list.Add(e);
            }

            return list;
        }

        public void SendAccessList(UUID agentID, UUID sessionID, uint flags, int sequenceID,
                                   IClientAPI remote_client)
        {

            if (flags == (uint) AccessList.Access || flags == (uint) AccessList.Both)
            {
                List<LandAccessEntry> accessEntries = CreateAccessListArrayByFlag(AccessList.Access);
                remote_client.SendLandAccessListData(accessEntries,(uint) AccessList.Access,LandData.LocalID);
            }

            if (flags == (uint) AccessList.Ban || flags == (uint) AccessList.Both)
            {
                List<LandAccessEntry> accessEntries = CreateAccessListArrayByFlag(AccessList.Ban);
                remote_client.SendLandAccessListData(accessEntries, (uint)AccessList.Ban, LandData.LocalID);
            }
        }

        public void UpdateAccessList(uint flags, UUID transactionID,
                int sequenceID, int sections,
                List<LandAccessEntry> entries,
                IClientAPI remote_client)
        {
            LandData newData = LandData.Copy();

            if ((!m_listTransactions.ContainsKey(flags)) ||
                    m_listTransactions[flags] != transactionID)
            {
                m_listTransactions[flags] = transactionID;

                List<LandAccessEntry> toRemove =
                        new List<LandAccessEntry>();

                foreach (LandAccessEntry entry in newData.ParcelAccessList)
                {
                    if (entry.Flags == (AccessList)flags)
                        toRemove.Add(entry);
                }

                foreach (LandAccessEntry entry in toRemove)
                {
                    newData.ParcelAccessList.Remove(entry);
                }

                // Checked here because this will always be the first
                // and only packet in a transaction
                if (entries.Count == 1 && entries[0].AgentID == UUID.Zero)
                {
                    m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);

                    return;
                }
            }

            foreach (LandAccessEntry entry in entries)
            {
                LandAccessEntry temp =
                        new LandAccessEntry();

                temp.AgentID = entry.AgentID;
                temp.Expires = entry.Expires;
                temp.Flags = (AccessList)flags;

                newData.ParcelAccessList.Add(temp);
            }

            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);
        }

        #endregion

        #region Update Functions

        public void UpdateLandBitmapByteArray()
        {
            LandData.Bitmap = ConvertLandBitmapToBytes();
        }

        /// <summary>
        /// Update all settings in land such as area, bitmap byte array, etc
        /// </summary>
        public void ForceUpdateLandInfo()
        {
            UpdateAABBAndAreaValues();
            UpdateLandBitmapByteArray();
        }

        public void SetLandBitmapFromByteArray()
        {
            LandBitmap = ConvertBytesToLandBitmap();
        }

        /// <summary>
        /// Updates the AABBMin and AABBMax values after area/shape modification of the land object
        /// </summary>
        private void UpdateAABBAndAreaValues()
        {

            int min_x = Int32.MaxValue;
            int min_y = Int32.MaxValue;
            int max_x = Int32.MinValue;
            int max_y = Int32.MinValue;
            int tempArea = 0;
            int x, y;
            for (x = 0; x < LandBitmap.GetLength(0); x++)
            {
                for (y = 0; y < LandBitmap.GetLength(1); y++)
                {
                    if (LandBitmap[x, y] == true)
                    {
                        if (min_x > x)
                            min_x = x;
                        if (min_y > y)
                            min_y = y;
                        if (max_x < x)
                            max_x = x;
                        if (max_y < y)
                            max_y = y;
                        tempArea += landUnit * landUnit; //16sqm peice of land
                    }
                }
            }
            int tx = min_x * landUnit;
            if (tx > ((int)m_scene.RegionInfo.RegionSizeX - 1))
                tx = ((int)m_scene.RegionInfo.RegionSizeX - 1);
            int htx;
            if (tx >= ((int)m_scene.RegionInfo.RegionSizeX))
                htx = (int)m_scene.RegionInfo.RegionSizeX - 1;
            else
                htx = tx;
            
            int ty = min_y * landUnit;
            int hty;

            if (ty >= ((int)m_scene.RegionInfo.RegionSizeY))
                hty = (int)m_scene.RegionInfo.RegionSizeY - 1;
            else
                hty = ty;

            LandData.AABBMin =
                new Vector3(
                    (float)(tx), (float)(ty), m_scene != null ? (float)m_scene.Heightmap[htx, hty] : 0);

            max_x++;
            tx = max_x * landUnit;
            if (tx >= ((int)m_scene.RegionInfo.RegionSizeX))
                htx = (int)m_scene.RegionInfo.RegionSizeX - 1;
            else
                htx = tx;

            max_y++;
            ty = max_y * 4;
           
            if (ty >= ((int)m_scene.RegionInfo.RegionSizeY))
                hty = (int)m_scene.RegionInfo.RegionSizeY - 1;
            else
                hty = ty;

            LandData.AABBMax 
                = new Vector3(
                    (float)(tx), (float)(ty), m_scene != null ? (float)m_scene.Heightmap[htx, hty] : 0);

            LandData.Area = tempArea;
        }

        #endregion

        #region Land Bitmap Functions

        /// <summary>
        /// Sets the land's bitmap manually
        /// </summary>
        /// <param name="bitmap">block representing where this land is on a map mapped in a 4x4 meter grid</param>
        public void SetLandBitmap(bool[,] bitmap)
        {
            LandBitmap = bitmap;
            ForceUpdateLandInfo();
        }

        /// <summary>
        /// Gets the land's bitmap manually
        /// </summary>
        /// <returns></returns>
        public bool[,] GetLandBitmap()
        {
            return LandBitmap;
        }

        public bool[,] BasicFullRegionLandBitmap()
        {
            return GetSquareLandBitmap(0, 0, (int)m_scene.RegionInfo.RegionSizeX, (int) m_scene.RegionInfo.RegionSizeY);
        }
        
        public bool[,] GetSquareLandBitmap(int start_x, int start_y, int end_x, int end_y)
        {
            // Empty bitmap for the whole region
            bool[,] tempBitmap = new bool[m_scene.RegionInfo.RegionSizeX / landUnit, m_scene.RegionInfo.RegionSizeY / landUnit];
            tempBitmap.Initialize();

            // Fill the bitmap square area specified by state and end
            tempBitmap = ModifyLandBitmapSquare(tempBitmap, start_x, start_y, end_x, end_y, true);
            // m_log.DebugFormat("{0} GetSquareLandBitmap. tempBitmapSize=<{1},{2}>",
            //                         LogHeader, tempBitmap.GetLength(0), tempBitmap.GetLength(1));
            return tempBitmap;
        }

        /// <summary>
        /// Change a land bitmap at within a square and set those points to a specific value
        /// </summary>
        /// <param name="land_bitmap"></param>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <param name="set_value"></param>
        /// <returns></returns>
        public bool[,] ModifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y,
                                              bool set_value)
        {
            int x, y;
            for (y = 0; y < land_bitmap.GetLength(1); y++)
            {
                for (x = 0; x < land_bitmap.GetLength(0); x++)
                {
                    if (x >= start_x / landUnit && x < end_x / landUnit
                        && y >= start_y / landUnit && y < end_y / landUnit)
                    {
                        land_bitmap[x, y] = set_value;
                    }
                }
            }
            // m_log.DebugFormat("{0} ModifyLandBitmapSquare. startXY=<{1},{2}>, endXY=<{3},{4}>, val={5}, landBitmapSize=<{6},{7}>",
            //                         LogHeader, start_x, start_y, end_x, end_y, set_value, land_bitmap.GetLength(0), land_bitmap.GetLength(1));
            return land_bitmap;
        }

        /// <summary>
        /// Join the true values of 2 bitmaps together
        /// </summary>
        /// <param name="bitmap_base"></param>
        /// <param name="bitmap_add"></param>
        /// <returns></returns>
        public bool[,] MergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add)
        {
            if (bitmap_base.GetLength(0) != bitmap_add.GetLength(0)
                    || bitmap_base.GetLength(1) != bitmap_add.GetLength(1)
                    || bitmap_add.Rank != 2
                    || bitmap_base.Rank != 2)
            {
                throw new Exception(
                    String.Format("{0} MergeLandBitmaps. merging maps not same size. baseSizeXY=<{1},{2}>, addSizeXY=<{3},{4}>",
                                LogHeader, bitmap_base.GetLength(0), bitmap_base.GetLength(1), bitmap_add.GetLength(0), bitmap_add.GetLength(1))
                );
            }

            int x, y;
            for (y = 0; y < bitmap_base.GetLength(1); y++)
            {
                for (x = 0; x < bitmap_add.GetLength(0); x++)
                {
                    if (bitmap_add[x, y])
                    {
                        bitmap_base[x, y] = true;
                    }
                }
            }
            return bitmap_base;
        }

        /// <summary>
        /// Converts the land bitmap to a packet friendly byte array
        /// </summary>
        /// <returns></returns>
        private byte[] ConvertLandBitmapToBytes()
        {
            byte[] tempConvertArr = new byte[LandBitmap.GetLength(0) * LandBitmap.GetLength(1) / 8];

            int tempByte = 0;
            int i, byteNum = 0;
            int mask = 1;
            i = 0;
            for (int y = 0; y < LandBitmap.GetLength(1); y++)
            {
                for (int x = 0; x < LandBitmap.GetLength(0); x++)
                {
                    if (LandBitmap[x, y])
                        tempByte |= mask;
                    mask = mask << 1;
                    if (mask == 0x100)
                    {
                        mask = 1;
                        tempConvertArr[byteNum++] = (byte)tempByte;
                        tempByte = 0;
                    }
                }
            }

            if(tempByte != 0 && byteNum < 512)
                tempConvertArr[byteNum] = (byte)tempByte;


/*
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(LandBitmap[x, y]) << (i++ % 8));
                    if (i % 8 == 0)
                    {
                        tempConvertArr[byteNum] = tempByte;
                        tempByte = (byte) 0;
                        i = 0;
                        byteNum++;
                    }
<<<<<<< HEAD
                }
            }
            // m_log.DebugFormat("{0} ConvertLandBitmapToBytes. BitmapSize=<{1},{2}>",
            //                         LogHeader, LandBitmap.GetLength(0), LandBitmap.GetLength(1));
=======
 */
            return tempConvertArr;
        }

        private bool[,] ConvertBytesToLandBitmap()
        {
            bool[,] tempConvertMap = new bool[m_scene.RegionInfo.RegionSizeX / landUnit, m_scene.RegionInfo.RegionSizeY / landUnit];
            tempConvertMap.Initialize();
            byte tempByte = 0;
            // Math.Min overcomes an old bug that might have made it into the database. Only use the bytes that fit into convertMap.
            int bitmapLen = Math.Min(LandData.Bitmap.Length, tempConvertMap.GetLength(0) * tempConvertMap.GetLength(1) / 8);
            int xLen = (int)(m_scene.RegionInfo.RegionSizeX / landUnit);

            if (bitmapLen == 512)
            {
                // Legacy bitmap being passed in. Use the legacy region size
                //    and only set the lower area of the larger region.
                xLen = (int)(Constants.RegionSize / landUnit);
            }
            // m_log.DebugFormat("{0} ConvertBytesToLandBitmap: bitmapLen={1}, xLen={2}", LogHeader, bitmapLen, xLen);

            int x = 0, y = 0;
            for (int i = 0; i < bitmapLen; i++)
            {
                tempByte = LandData.Bitmap[i];
                for (int bitNum = 0; bitNum < 8; bitNum++)
                {
                    bool bit = Convert.ToBoolean(Convert.ToByte(tempByte >> bitNum) & (byte) 1);
                    try
                    {
                        tempConvertMap[x, y] = bit;
                    }
                    catch (Exception)
                    {
                        m_log.DebugFormat("{0} ConvertBytestoLandBitmap: i={1}, x={2}, y={3}", LogHeader, i, x, y);
                    }
                    x++;
                    if (x >= xLen)
                    {
                        x = 0;
                        y++;
                    }
                }
            }

            return tempConvertMap;
        }

        #endregion

        #region Object Select and Object Owner Listing

        public void SendForceObjectSelect(int local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions, true))
            {
                List<uint> resultLocalIDs = new List<uint>();
                try
                {
                    lock (primsOverMe)
                    {
                        foreach (SceneObjectGroup obj in primsOverMe)
                        {
                            if (obj.LocalId > 0)
                            {
                                if (request_type == LandChannel.LAND_SELECT_OBJECTS_OWNER && obj.OwnerID == LandData.OwnerID)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_GROUP && obj.GroupID == LandData.GroupID && LandData.GroupID != UUID.Zero)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_OTHER &&
                                         obj.OwnerID != remote_client.AgentId)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == (int)ObjectReturnType.List && returnIDs.Contains(obj.OwnerID))
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                            }
                        }
                    }
                } catch (InvalidOperationException)
                {
                    m_log.Error("[LAND]: Unable to force select the parcel objects. Arr.");
                }

                remote_client.SendForceClientSelectObjects(resultLocalIDs);
            }
        }

        /// <summary>
        /// Notify the parcel owner each avatar that owns prims situated on their land.  This notification includes
        /// aggreagete details such as the number of prims.
        ///
        /// </summary>
        /// <param name="remote_client">
        /// A <see cref="IClientAPI"/>
        /// </param>
        public void SendLandObjectOwners(IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions, true))
            {
                Dictionary<UUID, int> primCount = new Dictionary<UUID, int>();
                List<UUID> groups = new List<UUID>();

                lock (primsOverMe)
                {
//                    m_log.DebugFormat(
//                        "[LAND OBJECT]: Request for SendLandObjectOwners() from {0} with {1} known prims on region", 
//                        remote_client.Name, primsOverMe.Count);
                    
                    try
                    {
                        foreach (SceneObjectGroup obj in primsOverMe)
                        {
                            try
                            {
                                if (!primCount.ContainsKey(obj.OwnerID))
                                {
                                    primCount.Add(obj.OwnerID, 0);
                                }
                            }
                            catch (NullReferenceException)
                            {
                                m_log.Error("[LAND]: " + "Got Null Reference when searching land owners from the parcel panel");
                            }
                            try
                            {
                                primCount[obj.OwnerID] += obj.PrimCount;
                            }
                            catch (KeyNotFoundException)
                            {
                                m_log.Error("[LAND]: Unable to match a prim with it's owner.");
                            }
                            if (obj.OwnerID == obj.GroupID && (!groups.Contains(obj.OwnerID)))
                                groups.Add(obj.OwnerID);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        m_log.Error("[LAND]: Unable to Enumerate Land object arr.");
                    }
                }

                remote_client.SendLandObjectOwners(LandData, groups, primCount);
            }
        }

        public Dictionary<UUID, int> GetLandObjectOwners()
        {
            Dictionary<UUID, int> ownersAndCount = new Dictionary<UUID, int>();
            
            lock (primsOverMe)
            {
                try
                {

                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (!ownersAndCount.ContainsKey(obj.OwnerID))
                        {
                            ownersAndCount.Add(obj.OwnerID, 0);
                        }
                        ownersAndCount[obj.OwnerID] += obj.PrimCount;
                    }
                }
                catch (InvalidOperationException)
                {
                    m_log.Error("[LAND]: Unable to enumerate land owners. arr.");
                }

            }
            return ownersAndCount;
        }

        #endregion

        #region Object Sales

        public void SellLandObjects(UUID previousOwner)
        {
            // m_log.DebugFormat(
            //    "[LAND OBJECT]: Request to sell objects in {0} from {1}", LandData.Name, previousOwner);

            if (LandData.IsGroupOwned)
                return;

            IBuySellModule m_BuySellModule = m_scene.RequestModuleInterface<IBuySellModule>();
            if (m_BuySellModule == null)
            {
                m_log.Error("[LAND OBJECT]: BuySellModule not found");
                return;
            }

            ScenePresence sp;
            if (!m_scene.TryGetScenePresence(LandData.OwnerID, out sp))
            {
                m_log.Error("[LAND OBJECT]: New owner is not present in scene");
                return;
            }

            lock (primsOverMe)
            {
                foreach (SceneObjectGroup obj in primsOverMe)
                {
                    if (obj.OwnerID == previousOwner && obj.GroupID == UUID.Zero &&
                        (obj.GetEffectivePermissions() & (uint)(OpenSim.Framework.PermissionMask.Transfer)) != 0)
                        m_BuySellModule.BuyObject(sp.ControllingClient, UUID.Zero, obj.LocalId, 1, 0);
                }
            }
        }

        #endregion

        #region Object Returning

        public void ReturnObject(SceneObjectGroup obj)
        {
            SceneObjectGroup[] objs = new SceneObjectGroup[1];
            objs[0] = obj;
            m_scene.returnObjects(objs, obj.OwnerID);
        }

        public void ReturnLandObjects(uint type, UUID[] owners, UUID[] tasks, IClientAPI remote_client)
        {
//            m_log.DebugFormat(
//                "[LAND OBJECT]: Request to return objects in {0} from {1}", LandData.Name, remote_client.Name);
            
            Dictionary<UUID,List<SceneObjectGroup>> returns = new Dictionary<UUID,List<SceneObjectGroup>>();

            lock (primsOverMe)
            {
                if (type == (uint)ObjectReturnType.Owner)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.OwnerID == LandData.OwnerID)
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Group && LandData.GroupID != UUID.Zero)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.GroupID == LandData.GroupID)
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Other)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.OwnerID != LandData.OwnerID &&
                            (obj.GroupID != LandData.GroupID ||
                            LandData.GroupID == UUID.Zero))
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.List)
                {
                    List<UUID> ownerlist = new List<UUID>(owners);

                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (ownerlist.Contains(obj.OwnerID))
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
            }

            foreach (List<SceneObjectGroup> ol in returns.Values)
            {
                if (m_scene.Permissions.CanReturnObjects(this, remote_client.AgentId, ol))
                    m_scene.returnObjects(ol.ToArray(), remote_client.AgentId);
            }
        }

        #endregion

        #region Object Adding/Removing from Parcel

        public void ResetOverMeRecord()
        {
            lock (primsOverMe)
                primsOverMe.Clear();
        }

        public void AddPrimOverMe(SceneObjectGroup obj)
        {
//            m_log.DebugFormat("[LAND OBJECT]: Adding scene object {0} {1} over {2}", obj.Name, obj.LocalId, LandData.Name);
            
            lock (primsOverMe)
                primsOverMe.Add(obj);
        }

        public void RemovePrimFromOverMe(SceneObjectGroup obj)
        {
//            m_log.DebugFormat("[LAND OBJECT]: Removing scene object {0} {1} from over {2}", obj.Name, obj.LocalId, LandData.Name);
            
            lock (primsOverMe)
                primsOverMe.Remove(obj);
        }

        #endregion
        
        /// <summary>
        /// Set the media url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMediaUrl(string url)
        {
            LandData.MediaURL = url;
            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, LandData);
            SendLandUpdateToAvatarsOverMe();
        }
        
        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMusicUrl(string url)
        {
            LandData.MusicURL = url;
            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, LandData);
            SendLandUpdateToAvatarsOverMe();
        }

        /// <summary>
        /// Get the music url for this land parcel
        /// </summary>
        /// <returns>The music url.</returns>
        public string GetMusicUrl()
        {
            return LandData.MusicURL;
        }

        #endregion

        private void OnFrame()
        {
            m_expiryCounter++;

            if (m_expiryCounter >= 50)
            {
                ExpireAccessList();
                m_expiryCounter = 0;
            }
        }

        private void ExpireAccessList()
        {
            List<LandAccessEntry> delete = new List<LandAccessEntry>();

            foreach (LandAccessEntry entry in LandData.ParcelAccessList)
            {
                if (entry.Expires != 0 && entry.Expires < Util.UnixTimeSinceEpoch())
                    delete.Add(entry);
            }
            foreach (LandAccessEntry entry in delete)
            {
                LandData.ParcelAccessList.Remove(entry);
                ScenePresence presence;
                
                if (m_scene.TryGetScenePresence(entry.AgentID, out presence) && (!presence.IsChildAgent))
                {
                    ILandObject land = m_scene.LandChannel.GetLandObject(presence.AbsolutePosition.X, presence.AbsolutePosition.Y);
                    if (land.LandData.LocalID == LandData.LocalID)
                    {
                        Vector3 pos = m_scene.GetNearestAllowedPosition(presence, land);
                        presence.TeleportWithMomentum(pos, null);
                        presence.ControllingClient.SendAlertMessage("You have been ejected from this land");
                    }
                }
                m_log.DebugFormat("[LAND]: Removing entry {0} because it has expired", entry.AgentID);
            }

            if (delete.Count > 0)
                m_scene.EventManager.TriggerLandObjectUpdated((uint)LandData.LocalID, this);
        }
    }
}
