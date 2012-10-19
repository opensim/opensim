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
        #pragma warning disable 0429
        private const int landArrayMax = ((int)((int)Constants.RegionSize / 4) >= 64) ? (int)((int)Constants.RegionSize / 4) : 64;
        #pragma warning restore 0429
        private bool[,] m_landBitmap = new bool[landArrayMax,landArrayMax];

        private int m_lastSeqId = 0;

        protected LandData m_landData = new LandData();        
        protected Scene m_scene;
        protected List<SceneObjectGroup> primsOverMe = new List<SceneObjectGroup>();
        protected Dictionary<uint, UUID> m_listTransactions = new Dictionary<uint, UUID>();

        protected ExpiringCache<UUID, bool> m_groupMemberCache = new ExpiringCache<UUID, bool>();
        protected TimeSpan m_groupMemberCacheTimeout = TimeSpan.FromSeconds(30);  // cache invalidation after 30 seconds

        public bool[,] LandBitmap
        {
            get { return m_landBitmap; }
            set { m_landBitmap = value; }
        }

        #endregion

        public int GetPrimsFree()
        {
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            int free = GetSimulatorMaxPrimCount() - m_landData.SimwidePrims;
            return free;
        }

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
                for (int y = 0; y < landArrayMax; y++)
                {
                    for (int x = 0; x < landArrayMax; x++)
                    {
                        if (LandBitmap[x, y])
                            return new Vector3(x * 4, y * 4, 0);
                    }
                }
                
                return new Vector3(-1, -1, -1);
            }
        }        
        
        public Vector3 EndPoint
        {
            get
            {
                for (int y = landArrayMax - 1; y >= 0; y--)
                {
                    for (int x = landArrayMax - 1; x >= 0; x--)
                    {
                        if (LandBitmap[x, y])
                        {
                            return new Vector3(x * 4, y * 4, 0);
                        }                        
                    }
                }   
                
                return new Vector3(-1, -1, -1);
            }
        }
                
        #region Constructors

        public LandObject(UUID owner_id, bool is_group_owned, Scene scene)
        {
            m_scene = scene;
            LandData.OwnerID = owner_id;
            if (is_group_owned)
                LandData.GroupID = owner_id;
            else
                LandData.GroupID = UUID.Zero;
            LandData.IsGroupOwned = is_group_owned;
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
            if (x >= 0 && y >= 0 && x < Constants.RegionSize && y < Constants.RegionSize)
            {
                return (LandBitmap[x / 4, y / 4] == true);
            }
            else
            {
                return false;
            }
        }

        public ILandObject Copy()
        {
            ILandObject newLand = new LandObject(LandData.OwnerID, LandData.IsGroupOwned, m_scene);

            //Place all new variables here!
            newLand.LandBitmap = (bool[,]) (LandBitmap.Clone());
            newLand.LandData = LandData.Copy();

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
                int parcelMax = (int)(((float)LandData.Area / 65536.0f)
                              * (float)m_scene.RegionInfo.ObjectCapacity
                              * (float)m_scene.RegionInfo.RegionSettings.ObjectBonus);
                // TODO: The calculation of ObjectBonus should be refactored. It does still not work in the same manner as SL!
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
                int simMax = (int)(((float)LandData.SimwideArea / 65536.0f)
                           * (float)m_scene.RegionInfo.ObjectCapacity);
                return simMax;
            }
        }
        
        #endregion

        #region Packet Request Handling

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
        {
            IEstateModule estateModule = m_scene.RequestModuleInterface<IEstateModule>();
            uint regionFlags = 336723974 & ~((uint)(RegionFlags.AllowLandmark | RegionFlags.AllowSetHome));
            if (estateModule != null)
                regionFlags = estateModule.GetRegionFlags();

            // In a perfect world, this would have worked.
            //
//            if ((landData.Flags & (uint)ParcelFlags.AllowLandmark) != 0)
//                regionFlags |=  (uint)RegionFlags.AllowLandmark;
//            if (landData.OwnerID == remote_client.AgentId)
//                regionFlags |=  (uint)RegionFlags.AllowSetHome;

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
                    GetParcelMaxPrimCount(),
                    GetSimulatorMaxPrimCount(), regionFlags);
        }

        public void UpdateLandProperties(LandUpdateArgs args, IClientAPI remote_client)
        {
            //Needs later group support
            bool snap_selection = false;
            LandData newData = LandData.Copy();

            uint allowedDelta = 0;

            // These two are always blocked as no client can set them anyway
            // ParcelFlags.ForSaleObjects
            // ParcelFlags.LindenHome

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions))
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
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandSetSale))
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

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.FindPlaces))
            {
                newData.Category = args.Category;

                allowedDelta |= (uint)(ParcelFlags.ShowDirectory |
                        ParcelFlags.AllowPublish |
                        ParcelFlags.MaturePublish);
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.LandChangeIdentity))
            {
                newData.Description = args.Desc;
                newData.Name = args.Name;
                newData.SnapshotID = args.SnapshotID;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.SetLandingPoint))
            {
                newData.LandingType = args.LandingType;
                newData.UserLocation = args.UserLocation;
                newData.UserLookAt = args.UserLookAt;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.ChangeMedia))
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

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.LandManagePasses))
            {
                newData.PassHours = args.PassHours;
                newData.PassPrice = args.PassPrice;

                allowedDelta |= (uint)ParcelFlags.UsePassList;
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandManageAllowed))
            {
                allowedDelta |= (uint)(ParcelFlags.UseAccessGroup |
                        ParcelFlags.UseAccessList);
            }

            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandManageBanned))
            {
                allowedDelta |= (uint)(ParcelFlags.UseBanList |
                        ParcelFlags.DenyAnonymous |
                        ParcelFlags.DenyAgeUnverified);
            }

            uint preserve = LandData.Flags & ~allowedDelta;
            newData.Flags = preserve | (args.ParcelFlags & allowedDelta);

            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);

            SendLandUpdateToAvatarsOverMe(snap_selection);
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
            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, newData);
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            SendLandUpdateToAvatarsOverMe(true);
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
            m_scene.ForEachRootScenePresence(delegate(ScenePresence avatar)
            {
                ILandObject over = null;
                try
                {
                    over =
                        m_scene.LandChannel.GetLandObject(Util.Clamp<int>((int)Math.Round(avatar.AbsolutePosition.X), 0, ((int)Constants.RegionSize - 1)),
                                                          Util.Clamp<int>((int)Math.Round(avatar.AbsolutePosition.Y), 0, ((int)Constants.RegionSize - 1)));
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
            int min_x = 64;
            int min_y = 64;
            int max_x = 0;
            int max_y = 0;
            int tempArea = 0;
            int x, y;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (LandBitmap[x, y] == true)
                    {
                        if (min_x > x) min_x = x;
                        if (min_y > y) min_y = y;
                        if (max_x < x) max_x = x;
                        if (max_y < y) max_y = y;
                        tempArea += 16; //16sqm peice of land
                    }
                }
            }
            int tx = min_x * 4;
            if (tx > ((int)Constants.RegionSize - 1))
                tx = ((int)Constants.RegionSize - 1);
            int ty = min_y * 4;
            if (ty > ((int)Constants.RegionSize - 1))
                ty = ((int)Constants.RegionSize - 1);
            LandData.AABBMin =
                new Vector3((float) (min_x * 4), (float) (min_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);

            tx = max_x * 4;
            if (tx > ((int)Constants.RegionSize - 1))
                tx = ((int)Constants.RegionSize - 1);
            ty = max_y * 4;
            if (ty > ((int)Constants.RegionSize - 1))
                ty = ((int)Constants.RegionSize - 1);
            LandData.AABBMax =
                new Vector3((float) (max_x * 4), (float) (max_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);
            LandData.Area = tempArea;
        }

        #endregion

        #region Land Bitmap Functions

        /// <summary>
        /// Sets the land's bitmap manually
        /// </summary>
        /// <param name="bitmap">64x64 block representing where this land is on a map</param>
        public void SetLandBitmap(bool[,] bitmap)
        {
            if (bitmap.GetLength(0) != 64 || bitmap.GetLength(1) != 64 || bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                //throw new Exception("Error: Invalid Parcel Bitmap");
            }
            else
            {
                //Valid: Lets set it
                LandBitmap = bitmap;
                ForceUpdateLandInfo();
            }
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
            return GetSquareLandBitmap(0, 0, (int) Constants.RegionSize, (int) Constants.RegionSize);
        }
        
        public bool[,] GetSquareLandBitmap(int start_x, int start_y, int end_x, int end_y)
        {
            bool[,] tempBitmap = new bool[64,64];
            tempBitmap.Initialize();

            tempBitmap = ModifyLandBitmapSquare(tempBitmap, start_x, start_y, end_x, end_y, true);
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
            if (land_bitmap.GetLength(0) != 64 || land_bitmap.GetLength(1) != 64 || land_bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                //throw new Exception("Error: Invalid Parcel Bitmap in modifyLandBitmapSquare()");
            }

            int x, y;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    if (x >= start_x / 4 && x < end_x / 4
                        && y >= start_y / 4 && y < end_y / 4)
                    {
                        land_bitmap[x, y] = set_value;
                    }
                }
            }
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
            if (bitmap_base.GetLength(0) != 64 || bitmap_base.GetLength(1) != 64 || bitmap_base.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap - Bitmap_base in mergeLandBitmaps");
            }
            if (bitmap_add.GetLength(0) != 64 || bitmap_add.GetLength(1) != 64 || bitmap_add.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap - Bitmap_add in mergeLandBitmaps");
            }

            int x, y;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
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
            byte[] tempConvertArr = new byte[512];
            byte tempByte = 0;
            int x, y, i, byteNum = 0;
            i = 0;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(LandBitmap[x, y]) << (i++ % 8));
                    if (i % 8 == 0)
                    {
                        tempConvertArr[byteNum] = tempByte;
                        tempByte = (byte) 0;
                        i = 0;
                        byteNum++;
                    }
                }
            }
            return tempConvertArr;
        }

        private bool[,] ConvertBytesToLandBitmap()
        {
            bool[,] tempConvertMap = new bool[landArrayMax, landArrayMax];
            tempConvertMap.Initialize();
            byte tempByte = 0;
            int x = 0, y = 0, i = 0, bitNum = 0;
            for (i = 0; i < 512; i++)
            {
                tempByte = LandData.Bitmap[i];
                for (bitNum = 0; bitNum < 8; bitNum++)
                {
                    bool bit = Convert.ToBoolean(Convert.ToByte(tempByte >> bitNum) & (byte) 1);
                    tempConvertMap[x, y] = bit;
                    x++;
                    if (x > 63)
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
            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions))
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
            if (m_scene.Permissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions))
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
                        if (obj.OwnerID == m_landData.OwnerID)
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Group && m_landData.GroupID != UUID.Zero)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.GroupID == m_landData.GroupID)
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
                        if (obj.OwnerID != m_landData.OwnerID &&
                            (obj.GroupID != m_landData.GroupID ||
                            m_landData.GroupID == UUID.Zero))
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
            SendLandUpdateToAvatarsOverMe();
        }
        
        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMusicUrl(string url)
        {
            LandData.MusicURL = url;
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

        private void ExpireAccessList()
        {
            List<LandAccessEntry> delete = new List<LandAccessEntry>();

            foreach (LandAccessEntry entry in LandData.ParcelAccessList)
            {
                if (entry.Expires != 0 && entry.Expires < Util.UnixTimeSinceEpoch())
                    delete.Add(entry);
            }
            foreach (LandAccessEntry entry in delete)
                LandData.ParcelAccessList.Remove(entry);

            if (delete.Count > 0)
                m_scene.EventManager.TriggerLandObjectUpdated((uint)LandData.LocalID, this);
        }
    }
}
