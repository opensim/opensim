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

        private Vector2 m_startPoint = Vector2.Zero;
        private Vector2 m_endPoint = Vector2.Zero;
        private Vector2 m_centerPoint = Vector2.Zero;
        private Vector2 m_AABBmin = Vector2.Zero;
        private Vector2 m_AABBmax = Vector2.Zero;

        public Vector2 StartPoint
        {
            get
            {
                return m_startPoint;
            }
        }

        public Vector2 EndPoint
        {
            get
            {
                return m_endPoint;
            }
        }

        //estimate a center point of a parcel
        public Vector2 CenterPoint
        {
            get
            {
                return m_centerPoint;
            }
        }

        public Vector2? GetNearestPoint(Vector3 pos)
        {
            Vector3 direction = new Vector3(m_centerPoint.X - pos.X, m_centerPoint.Y - pos.Y, 0f );
            return GetNearestPointAlongDirection(pos, direction);
        }

        public Vector2? GetNearestPointAlongDirection(Vector3 pos, Vector3 pdirection)
        {
            Vector2 testpos;
            Vector2 direction;

            testpos.X = pos.X / landUnit;
            testpos.Y = pos.Y / landUnit;

            if(LandBitmap[(int)testpos.X, (int)testpos.Y])
                return new Vector2(pos.X, pos.Y); // we are already here

            direction.X = pdirection.X;
            direction.Y = pdirection.Y;

            if(direction.X == 0f && direction.Y == 0f)
                return null; // we can't look anywhere

            direction.Normalize();

            int minx = (int)(m_AABBmin.X / landUnit);
            int maxx = (int)(m_AABBmax.X / landUnit);

            // check against AABB
            if(direction.X > 0f)
            {
                if(testpos.X >= maxx)
                    return null;  // will never get there
                if(testpos.X < minx)
                    testpos.X = minx;
            }
            else if(direction.X < 0f)
            {
                if(testpos.X < minx)
                    return null;  // will never get there
                if(testpos.X >= maxx)
                    testpos.X = maxx - 1;
            }
            else
            {
                if(testpos.X < minx)
                    return null;  // will never get there
                else if(testpos.X >= maxx)
                    return null;  // will never get there
            }

            int miny = (int)(m_AABBmin.Y / landUnit);
            int maxy = (int)(m_AABBmax.Y / landUnit);

            if(direction.Y > 0f)
            {
                if(testpos.Y >= maxy)
                    return null;  // will never get there
                if(testpos.Y < miny)
                    testpos.Y = miny;
            }
            else if(direction.Y < 0f)
            {
                if(testpos.Y < miny)
                    return null;  // will never get there
                if(testpos.Y >= maxy)
                    testpos.Y = maxy - 1;
            }
            else
            {
                if(testpos.Y < miny)
                    return null;  // will never get there
                else if(testpos.Y >= maxy)
                    return null;  // will never get there
            }

            while(!LandBitmap[(int)testpos.X, (int)testpos.Y])
            {
                testpos += direction;

                if(testpos.X < minx)
                    return null;
                if (testpos.X >= maxx)
                    return null;
                if(testpos.Y < miny)
                    return null;
                if (testpos.Y >= maxy)
                    return null;
            }

            testpos *= landUnit;
            float ftmp;

            if(Math.Abs(direction.X) > Math.Abs(direction.Y))
            {
                if(direction.X < 0)
                    testpos.X += landUnit - 0.5f;
                else
                    testpos.X += 0.5f;
                ftmp = testpos.X - pos.X;
                ftmp /= direction.X;
                ftmp = Math.Abs(ftmp);
                ftmp *= direction.Y;
                ftmp += pos.Y;

                if(ftmp < testpos.Y + .5f)
                    ftmp = testpos.Y + .5f;
                else
                {
                    testpos.Y += landUnit - 0.5f;
                    if(ftmp > testpos.Y)
                        ftmp = testpos.Y;
                }
                testpos.Y = ftmp;
            }
            else
            {
                if(direction.Y < 0)
                    testpos.Y += landUnit - 0.5f;
                else
                    testpos.Y += 0.5f;
                ftmp = testpos.Y - pos.Y;
                ftmp /= direction.Y;
                ftmp = Math.Abs(ftmp);
                ftmp *= direction.X;
                ftmp += pos.X;

                if(ftmp < testpos.X + .5f)
                    ftmp = testpos.X + .5f;
                else
                {
                    testpos.X += landUnit - 0.5f;
                    if(ftmp > testpos.X)
                        ftmp = testpos.X;
                }
                testpos.X = ftmp;
            }
            return testpos;
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
                int parcelMax = (int)(
                                (double)LandData.Area
                              * (double)m_scene.RegionInfo.ObjectCapacity
                              * (double)m_scene.RegionInfo.RegionSettings.ObjectBonus
                              / (double)(m_scene.RegionInfo.RegionSizeX * m_scene.RegionInfo.RegionSizeY)
                              + 0.5 );

                if(parcelMax > m_scene.RegionInfo.ObjectCapacity)
                    parcelMax = m_scene.RegionInfo.ObjectCapacity;

                //m_log.DebugFormat("Area: {0}, Capacity {1}, Bonus {2}, Parcel {3}", LandData.Area, m_scene.RegionInfo.ObjectCapacity, m_scene.RegionInfo.RegionSettings.ObjectBonus, parcelMax);
                return parcelMax;
            }
        }

        // the total prims a parcel owner can have on a region
        public int GetSimulatorMaxPrimCount()
        {
            if (overrideSimulatorMaxPrimCount != null)
            {
                return overrideSimulatorMaxPrimCount(this);
            }
            else
            {
                //Normal Calculations
                int simMax = (int)(   (double)LandData.SimwideArea
                                    * (double)m_scene.RegionInfo.ObjectCapacity
                                    * (double)m_scene.RegionInfo.RegionSettings.ObjectBonus
                                    / (long)(m_scene.RegionInfo.RegionSizeX * m_scene.RegionInfo.RegionSizeY)
                                    +0.5 );
                // sanity check
                if(simMax > m_scene.RegionInfo.ObjectCapacity)
                    simMax = m_scene.RegionInfo.ObjectCapacity;
                 //m_log.DebugFormat("Simwide Area: {0}, Capacity {1}, SimMax {2}, SimWidePrims {3}",
                 //    LandData.SimwideArea, m_scene.RegionInfo.ObjectCapacity, simMax, LandData.SimwidePrims);
                return simMax;
            }
        }

        #endregion

        #region Packet Request Handling

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
        {
            if(m_scene.RegionInfo.RegionSettings.AllowDamage)
                remote_client.SceneAgent.Invulnerable = false;
            else
                remote_client.SceneAgent.Invulnerable = (m_landData.Flags & (uint)ParcelFlags.AllowDamage) == 0;

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
                    GetParcelMaxPrimCount(),
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
            if (posHeight < LandChannel.BAN_LINE_SAFETY_HEIGHT && IsBannedFromLand(avatar))
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

            if(IsInLandAccessList(avatar))
                return false;

            // check for a NPC
            ScenePresence sp;
            if (!m_scene.TryGetScenePresence(avatar, out sp))
                return true;

            if(sp==null || !sp.IsNPC)
                return true;

            INPC npccli = (INPC)sp.ControllingClient;
            if(npccli== null)
                return true;

            UUID owner = npccli.Owner;

            if(owner == UUID.Zero)
                return true;

            if (owner == LandData.OwnerID)
                return false;

            return !IsInLandAccessList(owner);
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
                        if(m_scene.RegionInfo.RegionSettings.AllowDamage)
                            avatar.Invulnerable = false;
                        else
                            avatar.Invulnerable = (over.LandData.Flags & (uint)ParcelFlags.AllowDamage) == 0;

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

            if ((flags & (uint) AccessList.Access) != 0)
            {
                List<LandAccessEntry> accessEntries = CreateAccessListArrayByFlag(AccessList.Access);
                remote_client.SendLandAccessListData(accessEntries,(uint) AccessList.Access,LandData.LocalID);
            }

            if ((flags & (uint) AccessList.Ban) != 0)
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

            // update use lists flags
            // rights already checked or we wont be here
            uint parcelflags = newData.Flags;

            if((flags & (uint)AccessList.Access) != 0)
                    parcelflags |= (uint)ParcelFlags.UseAccessList;
            if((flags & (uint)AccessList.Ban) != 0)
                parcelflags |= (uint)ParcelFlags.UseBanList;

            newData.Flags = parcelflags;

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
            UpdateGeometryValues();
            UpdateLandBitmapByteArray();
        }

        public void SetLandBitmapFromByteArray()
        {
            LandBitmap = ConvertBytesToLandBitmap();
        }

        /// <summary>
        /// Updates geomtric values after area/shape modification of the land object
        /// </summary>
        private void UpdateGeometryValues()
        {
            int min_x = Int32.MaxValue;
            int min_y = Int32.MaxValue;
            int max_x = Int32.MinValue;
            int max_y = Int32.MinValue;
            int tempArea = 0;
            int x, y;

            int lastX = 0;
            int lastY = 0;
            float avgx = 0f;
            float avgy = 0f;

            bool needFirst = true;

            for (x = 0; x < LandBitmap.GetLength(0); x++)
            {
                for (y = 0; y < LandBitmap.GetLength(1); y++)
                {
                    if (LandBitmap[x, y])
                    {
                        if (min_x > x)
                            min_x = x;
                        if (min_y > y)
                            min_y = y;
                        if (max_x < x)
                            max_x = x;
                        if (max_y < y)
                            max_y = y;

                        if(needFirst)
                        {
                            avgx = x;
                            avgy = y;
                            m_startPoint.X = x * landUnit;
                            m_startPoint.Y = y * landUnit;
                            needFirst = false;
                        }
                        else
                        {
                            // keeping previus odd average
                            avgx = (avgx * tempArea + x) / (tempArea + 1);
                            avgy = (avgy * tempArea + y) / (tempArea + 1);
                        }

                        tempArea++;

                        lastX = x;
                        lastY = y;
                    }
                }
            }

            int halfunit = landUnit/2;

            m_centerPoint.X = avgx * landUnit + halfunit;
            m_centerPoint.Y = avgy * landUnit + halfunit;

            m_endPoint.X = lastX * landUnit + landUnit;
            m_endPoint.Y = lastY * landUnit + landUnit;

            // next tests should not be needed
            // if they fail, something is wrong

            int regionSizeX = (int)Constants.RegionSize;
            int regionSizeY = (int)Constants.RegionSize;

            if(m_scene != null)
            {
                regionSizeX = (int)m_scene.RegionInfo.RegionSizeX;
                regionSizeY = (int)m_scene.RegionInfo.RegionSizeX;
            }

            int tx = min_x * landUnit;
            if (tx >= regionSizeX)
                tx = regionSizeX - 1;

            int ty = min_y * landUnit;
            if (ty >= regionSizeY)
                ty = regionSizeY - 1;

            m_AABBmin.X = tx;
            m_AABBmin.Y = ty;

            if(m_scene == null || m_scene.Heightmap == null)
                LandData.AABBMin = new Vector3(tx, ty, 0f);
            else
                LandData.AABBMin = new Vector3(tx, ty, (float)m_scene.Heightmap[tx, ty]);

            max_x++;
            tx = max_x * landUnit;
            if (tx > regionSizeX)
                tx = regionSizeX;

            max_y++;
            ty = max_y * landUnit;
            if (ty > regionSizeY)
                ty = regionSizeY;

            m_AABBmax.X = tx;
            m_AABBmax.Y = ty;

            if(m_scene == null || m_scene.Heightmap == null)
                LandData.AABBMax = new Vector3(tx, ty, 0f);
            else
                LandData.AABBMax = new Vector3(tx, ty, (float)m_scene.Heightmap[tx - 1, ty - 1]);

            tempArea *= landUnit * landUnit;
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
            return GetSquareLandBitmap(0, 0, (int)m_scene.RegionInfo.RegionSizeX, (int) m_scene.RegionInfo.RegionSizeY, true);
        }

        public bool[,] GetSquareLandBitmap(int start_x, int start_y, int end_x, int end_y, bool set_value = true)
        {
            // Empty bitmap for the whole region
            bool[,] tempBitmap = new bool[m_scene.RegionInfo.RegionSizeX / landUnit, m_scene.RegionInfo.RegionSizeY / landUnit];
            tempBitmap.Initialize();

            // Fill the bitmap square area specified by state and end
            tempBitmap = ModifyLandBitmapSquare(tempBitmap, start_x, start_y, end_x, end_y, set_value);
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
        /// Remap a land bitmap. Takes the supplied land bitmap and rotates it, crops it and finally offsets it into
        /// a final land bitmap of the target region size.
        /// </summary>
        /// <param name="bitmap_base">The original parcel bitmap</param>
        /// <param name="rotationDegrees"></param>
        /// <param name="displacement">&lt;x,y,?&gt;</param>
        /// <param name="boundingOrigin">&lt;x,y,?&gt;</param>
        /// <param name="boundingSize">&lt;x,y,?&gt;</param>
        /// <param name="regionSize">&lt;x,y,?&gt;</param>
        /// <param name="isEmptyNow">out: This is set if the resultant bitmap is now empty</param>
        /// <param name="AABBMin">out: parcel.AABBMin &lt;x,y,0&gt</param>
        /// <param name="AABBMax">out: parcel.AABBMax &lt;x,y,0&gt</param>
        /// <returns>New parcel bitmap</returns>
        public bool[,] RemapLandBitmap(bool[,] bitmap_base, Vector2 displacement, float rotationDegrees, Vector2 boundingOrigin, Vector2 boundingSize, Vector2 regionSize, out bool isEmptyNow, out Vector3 AABBMin, out Vector3 AABBMax)
        {
            // get the size of the incoming bitmap
            int baseX = bitmap_base.GetLength(0);
            int baseY = bitmap_base.GetLength(1);

            // create an intermediate bitmap that is 25% bigger on each side that we can work with to handle rotations
            int offsetX = baseX / 4; // the original origin will now be at these coordinates so now we can have imaginary negative coordinates ;)
            int offsetY = baseY / 4;
            int tmpX = baseX + baseX / 2;
            int tmpY = baseY + baseY / 2;
            int centreX = tmpX / 2;
            int centreY = tmpY / 2;
            bool[,] bitmap_tmp = new bool[tmpX, tmpY];

            double radianRotation = Math.PI * rotationDegrees / 180f;
            double cosR = Math.Cos(radianRotation);
            double sinR = Math.Sin(radianRotation);
            if (rotationDegrees < 0f) rotationDegrees += 360f; //-90=270 -180=180 -270=90

            // So first we apply the rotation to the incoming bitmap, storing the result in bitmap_tmp
            // We special case orthogonal rotations for accuracy because even using double precision math, Math.Cos(90 degrees) is never fully 0
            // and we can never rotate around a centre pixel because the bitmap size is always even
            int x, y, sx, sy;
            for (y = 0; y <= tmpY; y++)
            {
                for (x = 0; x <= tmpX; x++)
                {
                    if (rotationDegrees == 0f)
                    {
                        sx = x - offsetX;
                        sy = y - offsetY;
                    }
                    else if (rotationDegrees == 90f)
                    {
                        sx = y - offsetX;
                        sy = tmpY - 1 - x - offsetY;
                    }
                    else if (rotationDegrees == 180f)
                    {
                        sx = tmpX - 1 - x - offsetX;
                        sy = tmpY - 1 - y - offsetY;
                    }
                    else if (rotationDegrees == 270f)
                    {
                        sx = tmpX - 1 - y - offsetX;
                        sy = x - offsetY;
                    }
                    else
                    {
                        // arbitary rotation: hmmm should I be using (centreX - 0.5) and (centreY - 0.5) and round cosR and sinR to say only 5 decimal places?
                        sx = centreX + (int)Math.Round((((double)x - centreX) * cosR) + (((double)y - centreY) * sinR)) - offsetX;
                        sy = centreY + (int)Math.Round((((double)y - centreY) * cosR) - (((double)x - centreX) * sinR)) - offsetY;
                    }
                    if (sx >= 0 && sx < baseX && sy >= 0 && sy < baseY)
                    {
                        try
                        {
                            if (bitmap_base[sx, sy]) bitmap_tmp[x, y] = true;
                        }
                        catch (Exception)   //just in case we've still not taken care of every way the arrays might go out of bounds! ;)
                        {
                            m_log.DebugFormat("{0} RemapLandBitmap Rotate: Out of Bounds sx={1} sy={2} dx={3} dy={4}", LogHeader, sx, sy, x, y);
                        }
                    }
                }
            }

            // We could also incorporate the next steps, bounding-rectangle and displacement in the loop above, but it's simpler to visualise if done separately
            // and will also make it much easier when later I want the option for maybe a circular or oval bounding shape too ;).
            // So... our output land bitmap must be the size of the current region but rememeber, parcel landbitmaps are landUnit metres (4x4 metres) per point,
            // and region sizes, boundaries and displacements are in metres so we need to scale down

            int newX = (int)(regionSize.X / landUnit);
            int newY = (int)(regionSize.Y / landUnit);
            bool[,] bitmap_new = new bool[newX, newY];
            // displacement is relative to <0,0> in the destination region and defines where the origin of the data selected by the bounding-rectangle is placed
            int dispX = (int)Math.Floor(displacement.X / landUnit);
            int dispY = (int)Math.Floor(displacement.Y / landUnit);

            // startX/Y and endX/Y are coordinates in bitmap_tmp
            int startX = (int)Math.Floor(boundingOrigin.X / landUnit) + offsetX;
            if (startX > tmpX) startX = tmpX;
            if (startX < 0) startX = 0;
            int startY = (int)Math.Floor(boundingOrigin.Y / landUnit) + offsetY;
            if (startY > tmpY) startY = tmpY;
            if (startY < 0) startY = 0;

            int endX = (int)Math.Floor((boundingOrigin.X + boundingSize.X) / landUnit) + offsetX;
            if (endX > tmpX) endX = tmpX;
            if (endX < 0) endX = 0;
            int endY = (int)Math.Floor((boundingOrigin.Y + boundingSize.Y) / landUnit) + offsetY;
            if (endY > tmpY) endY = tmpY;
            if (endY < 0) endY = 0;

            //m_log.DebugFormat("{0} RemapLandBitmap: inSize=<{1},{2}>, disp=<{3},{4}> rot={5}, offset=<{6},{7}>, boundingStart=<{8},{9}>, boundingEnd=<{10},{11}>, cosR={12}, sinR={13}, outSize=<{14},{15}>", LogHeader,
            //                            baseX, baseY, dispX, dispY, radianRotation, offsetX, offsetY, startX, startY, endX, endY, cosR, sinR, newX, newY);

            isEmptyNow = true;
            int minX = newX;
            int minY = newY;
            int maxX = 0;
            int maxY = 0;

            int dx, dy;
            for (y = startY; y < endY; y++)
            {
                for (x = startX; x < endX; x++)
                {
                    dx = x - startX + dispX;
                    dy = y - startY + dispY;
                    if (dx >= 0 && dx < newX && dy >= 0 && dy < newY)
                    {
                        try
                        {
                            if (bitmap_tmp[x, y])
                            {
                                bitmap_new[dx, dy] = true;
                                isEmptyNow = false;
                                if (dx < minX) minX = dx;
                                if (dy < minY) minY = dy;
                                if (dx > maxX) maxX = dx;
                                if (dy > maxY) maxY = dy;
                            }
                        }
                        catch (Exception)   //just in case we've still not taken care of every way the arrays might go out of bounds! ;)
                        {
                            m_log.DebugFormat("{0} RemapLandBitmap - Bound & Displace: Out of Bounds sx={1} sy={2} dx={3} dy={4}", LogHeader, x, y, dx, dy);
                        }
                    }
                }
            }
            if (isEmptyNow)
            {
                //m_log.DebugFormat("{0} RemapLandBitmap: Land bitmap is marked as Empty", LogHeader);
                minX = 0;
                minY = 0;
            }

            AABBMin = new Vector3(minX * landUnit, minY * landUnit, 0);
            AABBMax = new Vector3(maxX * landUnit, maxY * landUnit, 0);
            return bitmap_new;
        }

        /// <summary>
        /// Clears any parcel data in bitmap_base where there exists parcel data in bitmap_new. In other words the parcel data
        /// in bitmap_new takes over the space of the parcel data in bitmap_base.
        /// </summary>
        /// <param name="bitmap_base"></param>
        /// <param name="bitmap_new"></param>
        /// <param name="isEmptyNow">out: This is set if the resultant bitmap is now empty</param>
        /// <param name="AABBMin">out: parcel.AABBMin &lt;x,y,0&gt</param>
        /// <param name="AABBMax">out: parcel.AABBMax &lt;x,y,0&gt</param>
        /// <returns>New parcel bitmap</returns>
        public bool[,] RemoveFromLandBitmap(bool[,] bitmap_base, bool[,] bitmap_new, out bool isEmptyNow, out Vector3 AABBMin, out Vector3 AABBMax)
        {
            // get the size of the incoming bitmaps
            int baseX = bitmap_base.GetLength(0);
            int baseY = bitmap_base.GetLength(1);
            int newX = bitmap_new.GetLength(0);
            int newY = bitmap_new.GetLength(1);

            if (baseX != newX || baseY != newY)
            {
                throw new Exception(
                    String.Format("{0} RemoveFromLandBitmap: Land bitmaps are not the same size! baseX={1} baseY={2} newX={3} newY={4}", LogHeader, baseX, baseY, newX, newY));
            }

            isEmptyNow = true;
            int minX = baseX;
            int minY = baseY;
            int maxX = 0;
            int maxY = 0;

            for (int y = 0; y < baseY; y++)
            {
                for (int x = 0; x < baseX; x++)
                {
                    if (bitmap_new[x, y]) bitmap_base[x, y] = false;
                    if (bitmap_base[x, y])
                    {
                        isEmptyNow = false;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (isEmptyNow)
            {
                //m_log.DebugFormat("{0} RemoveFromLandBitmap: Land bitmap is marked as Empty", LogHeader);
                minX = 0;
                minY = 0;
            }
            AABBMin = new Vector3(minX * landUnit, minY * landUnit, 0);
            AABBMax = new Vector3(maxX * landUnit, maxY * landUnit, 0);
            return bitmap_base;
        }

        /// <summary>
        /// Converts the land bitmap to a packet friendly byte array
        /// </summary>
        /// <returns></returns>
        public byte[] ConvertLandBitmapToBytes()
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

            return tempConvertArr;
        }

        public bool[,] ConvertBytesToLandBitmap(bool overrideRegionSize = false)
        {
            int bitmapLen;
            int xLen;
            bool[,] tempConvertMap;

            if (overrideRegionSize)
            {
                // Importing land parcel data from an OAR where the source region is a different size to the dest region requires us
                // to make a LandBitmap that's not derived from the current region's size. We use the LandData.Bitmap size in bytes
                // to figure out what the OAR's region dimensions are. (Is there a better way to get the src region x and y from the OAR?)
                // This method assumes we always will have square regions

                bitmapLen = LandData.Bitmap.Length;
                xLen = (int)Math.Abs(Math.Sqrt(bitmapLen * 8));
                tempConvertMap = new bool[xLen, xLen];
                tempConvertMap.Initialize();
            }
            else
            {
                tempConvertMap = new bool[m_scene.RegionInfo.RegionSizeX / landUnit, m_scene.RegionInfo.RegionSizeY / landUnit];
                tempConvertMap.Initialize();
                // Math.Min overcomes an old bug that might have made it into the database. Only use the bytes that fit into convertMap.
                bitmapLen = Math.Min(LandData.Bitmap.Length, tempConvertMap.GetLength(0) * tempConvertMap.GetLength(1) / 8);
                xLen = (int)(m_scene.RegionInfo.RegionSizeX / landUnit);
                if (bitmapLen == 512)
                {
                    // Legacy bitmap being passed in. Use the legacy region size
                    //    and only set the lower area of the larger region.
                    xLen = (int)(Constants.RegionSize / landUnit);
                }
            }
            // m_log.DebugFormat("{0} ConvertBytesToLandBitmap: bitmapLen={1}, xLen={2}", LogHeader, bitmapLen, xLen);

            byte tempByte;
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

        public bool IsLandBitmapEmpty(bool[,] landBitmap)
        {
            for (int y = 0; y < landBitmap.GetLength(1); y++)
            {
                for (int x = 0; x < landBitmap.GetLength(0); x++)
                {
                    if (landBitmap[x, y]) return false;
                }
            }
            return true;
        }

        public void DebugLandBitmap(bool[,] landBitmap)
        {
            m_log.InfoFormat("{0}: Map Key: #=claimed land .=unclaimed land.", LogHeader);
            for (int y = landBitmap.GetLength(1) - 1; y >= 0; y--)
            {
                string row = "";
                for (int x = 0; x < landBitmap.GetLength(0); x++)
                {
                    row += landBitmap[x, y] ? "#" : ".";
                }
                m_log.InfoFormat("{0}: {1}", LogHeader, row);
            }
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
                        (obj.EffectiveOwnerPerms & (uint)(OpenSim.Framework.PermissionMask.Transfer)) != 0)
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
            m_scene.returnObjects(objs, null);
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
                            if (obj.OwnerID == LandData.OwnerID)
                                continue;
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
                if (m_scene.Permissions.CanReturnObjects(this, remote_client, ol))
                    m_scene.returnObjects(ol.ToArray(), remote_client);
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
