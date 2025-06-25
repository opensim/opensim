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
using System.Runtime.CompilerServices;

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

        protected const int GROUPMEMBERCACHETIMEOUT = 30000;  // cache invalidation after 30s

        private int m_lastSeqId = 0;
        private int m_expiryCounter = 0;

        protected readonly Scene m_scene;
        protected readonly int m_regionSizeX;
        protected readonly int m_regionSizeY;
        protected readonly RegionInfo m_regionInfo;
        protected readonly RegionSettings m_regionSettings;
        protected readonly ScenePermissions m_scenePermissions;
        protected readonly EstateSettings m_estateSettings;

        protected readonly List<SceneObjectGroup> primsOverMe = new();
        private readonly ExpiringCacheOS<uint, UUID> m_listTransactions = new(30000);
        private readonly object m_listTransactionsLock = new();

        protected readonly ExpiringCacheOS<UUID, bool> m_groupMemberCache = new(30000);
        protected readonly IDwellModule m_dwellModule;

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

        public UUID GlobalID
        {
            get
            {
                return m_landData is null ? UUID.Zero : m_landData.GlobalID;
            }
        }

        public UUID FakeID
        {
            get
            {
                return m_landData is null ? UUID.Zero : m_landData.FakeID;
            }
        }

        public UUID OwnerID
        {
            get
            {
                return m_landData is null ? UUID.Zero : m_landData.OwnerID;
            }
        }

        public UUID GroupID
        {
            get
            {
                return m_landData is null ? UUID.Zero : m_landData.GroupID;
            }
        }

        public int LocalID
        {
            get
            {
                return m_landData is null ? -1 : m_landData.LocalID;
            }
        }

        public IPrimCounts PrimCounts { get; set; }

        public UUID RegionUUID
        {
            get { return m_regionInfo.RegionID; }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISceneObject[] GetSceneObjectGroups()
        {
            return primsOverMe.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2? GetNearestPoint(Vector3 pos)
        {
            return GetNearestPointAlongDirection(pos, new Vector2(m_centerPoint.X - pos.X, m_centerPoint.Y - pos.Y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2? GetNearestPointAlongDirection(Vector3 pos, Vector3 pdirection)
        {
            return GetNearestPointAlongDirection(pos, new Vector2(pdirection.X, pdirection.Y));
        }

        public Vector2? GetNearestPointAlongDirection(Vector3 pos, Vector2 direction)
        {
            Vector2 testpos;
 
            testpos.X = pos.X / Constants.LandUnit;
            testpos.Y = pos.Y / Constants.LandUnit;

            if(LandBitmap[(int)testpos.X, (int)testpos.Y])
                return new Vector2(pos.X, pos.Y); // we are already here

            if(direction.X == 0f && direction.Y == 0f)
                return null; // we can't look anywhere

            direction.Normalize();

            int minx = (int)(m_AABBmin.X / Constants.LandUnit);
            int maxx = (int)(m_AABBmax.X / Constants.LandUnit);

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

            int miny = (int)(m_AABBmin.Y / Constants.LandUnit);
            int maxy = (int)(m_AABBmax.Y / Constants.LandUnit);

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

            testpos *= Constants.LandUnit;
            float ftmp;

            if(Math.Abs(direction.X) > Math.Abs(direction.Y))
            {
                if(direction.X < 0)
                    testpos.X += Constants.LandUnit - 0.5f;
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
                    testpos.Y += Constants.LandUnit - 0.5f;
                    if(ftmp > testpos.Y)
                        ftmp = testpos.Y;
                }
                testpos.Y = ftmp;
            }
            else
            {
                if(direction.Y < 0)
                    testpos.Y += Constants.LandUnit - 0.5f;
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
                    testpos.X += Constants.LandUnit - 0.5f;
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
            m_scenePermissions = scene.Permissions;

            m_regionInfo = scene.RegionInfo;
            m_regionSettings = scene.RegionInfo.RegionSettings;
            m_estateSettings = m_regionInfo.EstateSettings;

            m_regionSizeX = (int)m_regionInfo.RegionSizeX;
            m_regionSizeY = (int)m_regionInfo.RegionSizeY;
            m_scene.EventManager.OnFrame += OnFrame;
            m_dwellModule = m_scene.RequestModuleInterface<IDwellModule>();
        }

        public LandObject(UUID owner_id, bool is_group_owned, Scene scene, LandData data = null)
        {
            m_scene = scene;
            if (m_scene == null)
            {
                m_regionSizeX = (int)Constants.RegionSize;
                m_regionSizeY = (int)Constants.RegionSize;
                LandBitmap = new bool[Constants.RegionSize / Constants.LandUnit, Constants.RegionSize / Constants.LandUnit];
            }
            else
            {
                m_scenePermissions = scene.Permissions;
                m_regionInfo = scene.RegionInfo;
                m_regionSettings = scene.RegionInfo.RegionSettings;
                m_estateSettings = m_regionInfo.EstateSettings;

                m_regionSizeX = (int)m_regionInfo.RegionSizeX;
                m_regionSizeY = (int)m_regionInfo.RegionSizeY;
                LandBitmap = new bool[m_regionSizeX / Constants.LandUnit, m_regionSizeY / Constants.LandUnit];
                m_dwellModule = m_scene.RequestModuleInterface<IDwellModule>();
            }

            if(data == null)
                LandData = new LandData();
            else
                LandData = data;

            LandData.OwnerID = owner_id;
            if (is_group_owned)
                LandData.GroupID = owner_id;
            
            LandData.IsGroupOwned = is_group_owned;

            if(m_dwellModule == null)
                LandData.Dwell = 0;

            m_scene.EventManager.OnFrame += OnFrame;
        }

        public void Clear()
        {
            if(m_scene != null)
                 m_scene.EventManager.OnFrame -= OnFrame;
            LandData = null;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsPoint(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < m_regionSizeX && y < m_regionSizeY)
            {
                return LandBitmap[x / Constants.LandUnit, y / Constants.LandUnit];
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject Copy()
        {
            ILandObject newLand = new LandObject(LandData, m_scene)
            {
                LandBitmap = (bool[,])(LandBitmap.Clone())
            };
            return newLand;
        }

        static overrideParcelMaxPrimCountDelegate overrideParcelMaxPrimCount;
        static overrideSimulatorMaxPrimCountDelegate overrideSimulatorMaxPrimCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            overrideParcelMaxPrimCount = overrideDel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            overrideSimulatorMaxPrimCount = overrideDel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                              * (double)m_regionInfo.ObjectCapacity
                              * (double)m_regionSettings.ObjectBonus
                              / (double)(m_regionSizeX * m_regionSizeY)
                              + 0.5 );

                if(parcelMax > m_regionInfo.ObjectCapacity)
                    parcelMax = m_regionInfo.ObjectCapacity;

                //m_log.DebugFormat("Area: {0}, Capacity {1}, Bonus {2}, Parcel {3}", LandData.Area, m_regionInfo.ObjectCapacity, m_regionInfo.RegionSettings.ObjectBonus, parcelMax);
                return parcelMax;
            }
        }

        // the total prims a parcel owner can have on a region
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSimulatorMaxPrimCount()
        {
            if (overrideSimulatorMaxPrimCount is not null)
            {
                return overrideSimulatorMaxPrimCount(this);
            }
            else
            {
                //Normal Calculations
                int simMax = (int)(   (double)LandData.SimwideArea
                                    * (double)m_regionInfo.ObjectCapacity
                                    * (double)m_regionSettings.ObjectBonus
                                    / (long)(m_regionSizeX * m_regionSizeY)
                                    +0.5 );
                // sanity check
                if(simMax > m_regionInfo.ObjectCapacity)
                    simMax = m_regionInfo.ObjectCapacity;
                 //m_log.DebugFormat("Simwide Area: {0}, Capacity {1}, SimMax {2}, SimWidePrims {3}",
                 //    LandData.SimwideArea, m_regionInfo.ObjectCapacity, simMax, LandData.SimwidePrims);
                return simMax;
            }
        }

        #endregion

        #region Packet Request Handling

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
        {
            if(m_regionSettings.AllowDamage)
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
                    (float)m_regionSettings.ObjectBonus,
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

            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions, false))
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

            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandSetSale, true))
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
                    if(newData.GroupID != LandData.GroupID)
                        m_groupMemberCache.Clear();

                    allowedDelta |= (uint)(ParcelFlags.AllowDeedToGroup |
                            ParcelFlags.ContributeWithDeed |
                            ParcelFlags.SellParcelObjects);
                }

                allowedDelta |= (uint)ParcelFlags.ForSale;
            }

            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.FindPlaces, false))
            {
                newData.Category = args.Category;

                allowedDelta |= (uint)(ParcelFlags.ShowDirectory |
                        ParcelFlags.AllowPublish |
                        ParcelFlags.MaturePublish) | (uint)(1 << 23);
            }

            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.LandChangeIdentity, false))
            {
                newData.Description = args.Desc;
                newData.Name = args.Name;
                newData.SnapshotID = args.SnapshotID;
            }

            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.SetLandingPoint, false))
            {
                newData.LandingType = args.LandingType;
                newData.UserLocation = args.UserLocation;
                newData.UserLookAt = args.UserLookAt;
            }

            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.ChangeMedia, false))
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
                //newData.ObscureMusic = args.ObscureMusic;
                newData.ObscureMusic = false; // obsolete
                //newData.ObscureMedia = args.ObscureMedia;
                newData.ObscureMedia = args.ObscureMOAP; // obsolete, reuse for moap

                allowedDelta |= (uint)(ParcelFlags.SoundLocal |
                        ParcelFlags.UrlWebPage |
                        ParcelFlags.UrlRawHtml |
                        ParcelFlags.AllowVoiceChat |
                        ParcelFlags.UseEstateVoiceChan);
            }

            if(!m_estateSettings.TaxFree)
            {
                // don't allow passes on group owned until we can give money to groups
                if (!newData.IsGroupOwned && m_scenePermissions.CanEditParcelProperties(remote_client.AgentId,this, GroupPowers.LandManagePasses, false))
                {
                    newData.PassHours = args.PassHours;
                    newData.PassPrice = args.PassPrice;

                    allowedDelta |= (uint)ParcelFlags.UsePassList;
                }

                if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandManageAllowed, false))
                {
                    allowedDelta |= (uint)(ParcelFlags.UseAccessGroup |
                            ParcelFlags.UseAccessList);
                }

                if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandManageBanned, false))
                {
                    allowedDelta |= (uint)(ParcelFlags.UseBanList |
                            ParcelFlags.DenyAnonymous |
                            ParcelFlags.DenyAgeUnverified);
                }
            }

            // enforce estate age and payinfo limitations
            if (m_estateSettings.DenyMinors)
            {
                args.ParcelFlags |= (uint)ParcelFlags.DenyAgeUnverified;
                allowedDelta |= (uint)ParcelFlags.DenyAgeUnverified;
            }

            if (m_estateSettings.DenyAnonymous)
            {
                args.ParcelFlags |= (uint)ParcelFlags.DenyAnonymous;
                allowedDelta |= (uint)ParcelFlags.DenyAnonymous;
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
            if (sellObjects)
                SellLandObjects(previousOwner);
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
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
        }

        public bool IsEitherBannedOrRestricted(UUID avatar)
        {
            if (m_estateSettings.TaxFree) // region access control only
                return false;

            if (m_scenePermissions.IsAdministrator(avatar))
                return false;

            if (m_estateSettings.IsEstateManagerOrOwner(avatar))
                return false;

            if (avatar.Equals(LandData.OwnerID))
                return false;

            if (IsBannedFromLand_inner(avatar))
                return true;

            if (IsRestrictedFromLand_inner(avatar))
                return true;

            return false;
        }

        public bool CanBeOnThisLand(UUID avatar, float posHeight)
        {
            if (m_estateSettings.TaxFree) // estate access only
                return true;

            if (m_scenePermissions.IsAdministrator(avatar))
                return true;

            if (m_estateSettings.IsEstateManagerOrOwner(avatar))
                return true;

            if (avatar.Equals(LandData.OwnerID))
                return true;

            if (posHeight < m_scene.LandChannel.BanLineSafeHeight && IsBannedFromLand_inner(avatar))
                return false;

            else if (IsRestrictedFromLand_inner(avatar))
                return false;

            return true;
        }

        public bool HasGroupAccess(UUID avatar)
        {
            if (LandData.GroupID.IsNotZero() && (LandData.Flags & (uint)ParcelFlags.UseAccessGroup) != 0)
            {
                if (m_groupMemberCache.TryGetValue(avatar, GROUPMEMBERCACHETIMEOUT, out bool isMember))
                    return isMember;

                if (m_scene.TryGetScenePresence(avatar, out ScenePresence sp))
                {
                    isMember = sp.ControllingClient.IsGroupMember(LandData.GroupID);
                    m_groupMemberCache.Add(avatar, isMember, GROUPMEMBERCACHETIMEOUT);
                    return isMember;
                }
                else
                {
                    IGroupsModule groupsModule = m_scene.RequestModuleInterface<IGroupsModule>();
                    if (groupsModule == null)
                        return false;

                    GroupMembershipData[] membership = groupsModule.GetMembershipData(avatar);
                    if (membership == null || membership.Length == 0)
                    {
                        m_groupMemberCache.Add(avatar, false, GROUPMEMBERCACHETIMEOUT);
                        return false;
                    }

                    foreach (GroupMembershipData d in membership)
                    {
                        if (d.GroupID.Equals(LandData.GroupID))
                        {
                            m_groupMemberCache.Add(avatar, true, GROUPMEMBERCACHETIMEOUT);
                            return true;
                        }
                    }
                    m_groupMemberCache.Add(avatar, false, GROUPMEMBERCACHETIMEOUT);
                    return false;
                }
            }
            return false;
        }

        public bool IsBannedFromLand(UUID avatar)
        {
            if (m_estateSettings.TaxFree) // region access control only
                return false;

            if (m_scenePermissions.IsAdministrator(avatar))
                return false;

            if (m_estateSettings.IsEstateManagerOrOwner(avatar))
                return false;

            if (avatar.Equals(LandData.OwnerID))
                return false;

            return IsBannedFromLand_inner(avatar);
        }

        private bool IsBannedFromLand_inner(UUID avatar)
        {
            if ((LandData.Flags & (uint) ParcelFlags.UseBanList) > 0)
            {
                int now = Util.UnixTimeSinceEpoch();
                foreach (LandAccessEntry e in LandData.ParcelAccessList)
                {
                    if (e.Flags == AccessList.Ban && e.AgentID.Equals(avatar))
                        return e.Expires == 0 || e.Expires > now;
                }
            }
            return false;
        }

        public bool IsRestrictedFromLand(UUID avatar)
        {
            if (m_estateSettings.TaxFree) // estate access only
                return false;

            if (m_scenePermissions.IsAdministrator(avatar))
                return false;

            if (m_estateSettings.IsEstateManagerOrOwner(avatar))
                return false;

            if (avatar.Equals(LandData.OwnerID))
                return false;

            return IsRestrictedFromLand_inner(avatar);
        }

        private bool IsRestrictedFromLand_inner(UUID avatar)
        {
            if ((LandData.Flags & (uint) ParcelFlags.UseAccessList) == 0)
            {
                bool adults = m_estateSettings.DoDenyMinors &&
                    (m_estateSettings.DenyMinors || ((LandData.Flags & (uint)ParcelFlags.DenyAgeUnverified) != 0));
                bool anonymous = m_estateSettings.DoDenyAnonymous &&
                    (m_estateSettings.DenyAnonymous || ((LandData.Flags & (uint)ParcelFlags.DenyAnonymous) != 0));
                if(adults || anonymous)
                {
                    int userflags;
                    if(m_scene.TryGetScenePresence(avatar, out ScenePresence snp))
                    {
                        if(snp.IsNPC)
                            return false;
                        userflags = snp.UserFlags;
                    }
                    else
                        userflags = m_scene.GetUserFlags(avatar);

                    if(adults && ((userflags & (int)ProfileFlags.AgeVerified) == 0))
                        return true;
                    if(anonymous && ((userflags & (int)ProfileFlags.Identified) == 0))
                        return true;
                }
                return false;
            }

            if (HasGroupAccess(avatar))
                return false;

            if(IsInLandAccessList(avatar))
                return false;

            // check for a NPC
            if (!m_scene.TryGetScenePresence(avatar, out ScenePresence sp))
                return true;

            if(sp is null || !sp.IsNPC)
                return true;

            INPC npccli = (INPC)sp.ControllingClient;
            if(npccli is null)
                return true;

            UUID owner = npccli.Owner;

            if(owner.IsZero())
                return true;

            if (owner.Equals(LandData.OwnerID))
                return false;

            return !IsInLandAccessList(owner);
        }

        public bool IsInLandAccessList(UUID avatar)
        {
            foreach(LandAccessEntry e in LandData.ParcelAccessList)
            {
                int now = Util.UnixTimeSinceEpoch();
                if (e.Flags == AccessList.Access && e.AgentID.Equals(avatar))
                    return e.Expires == 0 || e.Expires > now;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendLandUpdateToClient(IClientAPI remote_client)
        {
            SendLandProperties(0, false, 0, remote_client);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendLandUpdateToClient(bool snap_selection, IClientAPI remote_client)
        {
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            SendLandProperties(0, snap_selection, 0, remote_client);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendLandUpdateToAvatarsOverMe()
        {
            SendLandUpdateToAvatarsOverMe(false);
        }

        public void SendLandUpdateToAvatarsOverMe(bool snap_selection)
        {
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            m_scene.ForEachRootScenePresence(delegate(ScenePresence avatar)
            {
                if (avatar.IsNPC)
                    return;

                if(ContainsPoint((int)avatar.AbsolutePosition.X, (int)avatar.AbsolutePosition.Y))
                {
                    if(m_regionSettings.AllowDamage)
                        avatar.Invulnerable = false;
                    else
                        avatar.Invulnerable = (LandData.Flags & (uint)ParcelFlags.AllowDamage) == 0;

                    SendLandUpdateToClient(snap_selection, avatar.ControllingClient);
                    avatar.currentParcelUUID = LandData.GlobalID;
                }
            });
        }

        public void SendLandUpdateToAvatars()
        {
            m_scene.ForEachScenePresence(delegate (ScenePresence avatar)
            {
                if (avatar.IsNPC)
                    return;

                if(avatar.IsChildAgent)
                {
                    SendLandProperties(-10000, false, LandChannel.LAND_RESULT_SINGLE, avatar.ControllingClient);
                    return;
                }
                if (ContainsPoint((int)avatar.AbsolutePosition.X, (int)avatar.AbsolutePosition.Y))
                {
                    if (m_regionSettings.AllowDamage)
                            avatar.Invulnerable = false;
                    else
                        avatar.Invulnerable = (LandData.Flags & (uint)ParcelFlags.AllowDamage) == 0;

                    avatar.currentParcelUUID = LandData.GlobalID;
                    SendLandProperties(0, true, LandChannel.LAND_RESULT_SINGLE, avatar.ControllingClient);
                    return;
                }
                SendLandProperties(-10000, false, LandChannel.LAND_RESULT_SINGLE, avatar.ControllingClient);
            });
        }

        #endregion

        #region AccessList Functions

        //legacy
        public List<LandAccessEntry>  CreateAccessListArrayByFlag(AccessList flag)
        {
            int now = Util.UnixTimeSinceEpoch();
            List<LandAccessEntry> list = new();
            foreach (LandAccessEntry entry in LandData.ParcelAccessList)
            {
                if (entry.Flags == flag && (entry.Expires > now || entry.Expires == 0))
                   list.Add(entry);
            }
            if (list.Count == 0)
                list.Add(new LandAccessEntry());

            return list;
        }

        public void SendAccessList(UUID agentID, UUID sessionID, uint flags, int sequenceID,
                                   IClientAPI remote_client)
        {
            int now = Util.UnixTimeSinceEpoch();
            List<LandAccessEntry> accesslist = new();
            List<LandAccessEntry> banlist = new();
            foreach (LandAccessEntry entry in LandData.ParcelAccessList)
            {
                if(entry.Expires > now || entry.Expires == 0)
                {
                    if (entry.Flags == AccessList.Access)
                        accesslist.Add(entry);
                    else if (entry.Flags == AccessList.Ban)
                        banlist.Add(entry);
                }
            }

            if (accesslist.Count == 0)
            {
                remote_client.SendLandAccessListData(new List<LandAccessEntry>() { new LandAccessEntry() },
                    (uint)AccessList.Access, LandData.LocalID);
            }               
            else
                remote_client.SendLandAccessListData(accesslist, (uint)AccessList.Access, LandData.LocalID);

            if (banlist.Count == 0)
            {
                remote_client.SendLandAccessListData(new List<LandAccessEntry>() { new LandAccessEntry() },
                        (uint)AccessList.Ban, LandData.LocalID);
            }
            else
                remote_client.SendLandAccessListData(banlist, (uint)AccessList.Ban, LandData.LocalID);
        }

        public void UpdateAccessList(uint flags, UUID transactionID, List<LandAccessEntry> entries)
        {
            flags &= 0x03;
            if (flags == 0)
                return; // we only have access and ban

            // get a work copy of lists
            List<LandAccessEntry> parcelAccessList = new(LandData.ParcelAccessList);

            // first packet on a transaction clears before adding
            // we need to this way because viewer protocol does not seem reliable
            lock (m_listTransactionsLock)
            {
                if ((!m_listTransactions.TryGetValue(flags, out UUID flagsID)) || flagsID.NotEqual(transactionID))
                {
                    m_listTransactions.Add(flags, transactionID);

                    List<LandAccessEntry> toRemove = new();
                    foreach (LandAccessEntry entry in parcelAccessList)
                    {
                        if (((uint)entry.Flags & flags) != 0)
                            toRemove.Add(entry);
                    }
                    foreach (LandAccessEntry entry in toRemove)
                        parcelAccessList.Remove(entry);

                    // a delete all command ?
                    if (entries.Count == 1 && entries[0].AgentID.IsZero())
                    {
                        LandData.ParcelAccessList = parcelAccessList;
                        if ((flags & (uint)AccessList.Access) != 0)
                            LandData.Flags &= ~(uint)ParcelFlags.UseAccessList;
                        if ((flags & (uint)AccessList.Ban) != 0)
                            LandData.Flags &= ~(uint)ParcelFlags.UseBanList;
                        m_listTransactions.Remove(flags);
                        return;
                    }
                }
            }

            foreach (LandAccessEntry entry in entries)
            {
                LandAccessEntry temp = new()
                {
                    AgentID = entry.AgentID,
                    Expires = entry.Expires,
                    Flags = (AccessList)flags
                };

                parcelAccessList.Add(temp);
            }

            LandData.ParcelAccessList = parcelAccessList;
            if ((flags & (uint)AccessList.Access) != 0)
                LandData.Flags |= (uint)ParcelFlags.UseAccessList;
            if ((flags & (uint)AccessList.Ban) != 0)
                LandData.Flags |= (uint)ParcelFlags.UseBanList;
        }

        #endregion

        #region Update Functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateLandBitmapByteArray()
        {
            LandData.Bitmap = ConvertLandBitmapToBytes();
        }

        /// <summary>
        /// Update all settings in land such as area, bitmap byte array, etc
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForceUpdateLandInfo()
        {
            UpdateGeometryValues();
            UpdateLandBitmapByteArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                            m_startPoint.X = x * Constants.LandUnit;
                            m_startPoint.Y = y * Constants.LandUnit;
                            needFirst = false;
                        }
                        else
                        {
                            // keeping previous odd average
                            avgx = (avgx * tempArea + x) / (tempArea + 1);
                            avgy = (avgy * tempArea + y) / (tempArea + 1);
                        }

                        tempArea++;

                        lastX = x;
                        lastY = y;
                    }
                }
            }

            if(tempArea == 0)
            {
                m_centerPoint = Vector2.Zero;
                m_endPoint = Vector2.Zero;
                m_AABBmin = Vector2.Zero;
                m_AABBmax = Vector2.Zero;

                LandData.AABBMin = Vector3.Zero;
                LandData.AABBMax = Vector3.Zero;
                LandData.Area = 0;

                if (m_scene != null)
                {
                    //create a fake ID
                    LandData.FakeID = Util.BuildFakeParcelID(m_regionInfo.RegionHandle, 0, 0);
                }
                return;
            }

            const int halfunit = Constants.LandUnit / 2;
            m_centerPoint.X = avgx * Constants.LandUnit + halfunit;
            m_centerPoint.Y = avgy * Constants.LandUnit + halfunit;

            m_endPoint.X = lastX * Constants.LandUnit + Constants.LandUnit;
            m_endPoint.Y = lastY * Constants.LandUnit + Constants.LandUnit;

            // next tests should not be needed
            // if they fail, something is wrong

            ulong regionHandle;

            if(m_scene != null)
            {
                regionHandle = m_regionInfo.RegionHandle;
                //create a fake ID
                LandData.FakeID = Util.BuildFakeParcelID(regionHandle, (uint)(lastX * Constants.LandUnit), (uint)(lastY * Constants.LandUnit));
            }

            int tx = min_x * Constants.LandUnit;
            if (tx >= m_regionSizeX)
                tx = m_regionSizeX - 1;

            int ty = min_y * Constants.LandUnit;
            if (ty >= m_regionSizeY)
                ty = m_regionSizeY - 1;

            m_AABBmin.X = tx;
            m_AABBmin.Y = ty;

            if(m_scene == null || m_scene.Heightmap == null)
                LandData.AABBMin = new Vector3(tx, ty, 0f);
            else
                LandData.AABBMin = new Vector3(tx, ty, (float)m_scene.Heightmap[tx, ty]);

            max_x++;
            tx = max_x * Constants.LandUnit;
            if (tx > m_regionSizeX)
                tx = m_regionSizeX;

            max_y++;
            ty = max_y * Constants.LandUnit;
            if (ty > m_regionSizeY)
                ty = m_regionSizeY;

            m_AABBmax.X = tx;
            m_AABBmax.Y = ty;

            if(m_scene == null || m_scene.Heightmap == null)
                LandData.AABBMax = new Vector3(tx, ty, 0f);
            else
                LandData.AABBMax = new Vector3(tx, ty, (float)m_scene.Heightmap[tx - 1, ty - 1]);

            tempArea *= Constants.LandUnit * Constants.LandUnit;
            LandData.Area = tempArea;
        }

        #endregion

        #region Land Bitmap Functions

        /// <summary>
        /// Sets the land's bitmap manually
        /// </summary>
        /// <param name="bitmap">block representing where this land is on a map mapped in a 4x4 meter grid</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLandBitmap(bool[,] bitmap)
        {
            LandBitmap = bitmap;
            ForceUpdateLandInfo();
        }

        /// <summary>
        /// Gets the land's bitmap manually
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool[,] GetLandBitmap()
        {
            return LandBitmap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool[,] BasicFullRegionLandBitmap()
        {
            return GetSquareLandBitmap(0, 0, m_regionSizeX, m_regionSizeY, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool[,] GetSquareLandBitmap(int start_x, int start_y, int end_x, int end_y, bool set_value = true)
        {
            bool[,] tempBitmap = ModifyLandBitmapSquare(null, start_x, start_y, end_x, end_y, set_value);
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
            if(land_bitmap == null)
            {
                land_bitmap = new bool[m_regionSizeX / Constants.LandUnit, m_regionSizeY / Constants.LandUnit];
                if(!set_value)
                    return land_bitmap;
            }

            start_x /= Constants.LandUnit;
            end_x /= Constants.LandUnit;
            start_y /= Constants.LandUnit;
            end_y /= Constants.LandUnit;

            for (int x = start_x; x < end_x; ++x)
            {
                for (int y = start_y; y < end_y; ++y)
                {
                    land_bitmap[x, y] = set_value;
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
                    || bitmap_base.GetLength(1) != bitmap_add.GetLength(1))
            {
                throw new Exception(
                    String.Format("{0} MergeLandBitmaps. merging maps not same size. baseSizeXY=<{1},{2}>, addSizeXY=<{3},{4}>",
                                LogHeader, bitmap_base.GetLength(0), bitmap_base.GetLength(1), bitmap_add.GetLength(0), bitmap_add.GetLength(1))
                );
            }

            for (int x = 0; x < bitmap_add.GetLength(0); x++)
            {
                for (int y = 0; y < bitmap_base.GetLength(1); y++)
                {
                    bitmap_base[x, y] |= bitmap_add[x, y];
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
        /// <param name="newRegionSize">&lt;x,y,?&gt;</param>
        /// <param name="isEmptyNow">out: This is set if the resultant bitmap is now empty</param>
        /// <param name="AABBMin">out: parcel.AABBMin &lt;x,y,0&gt</param>
        /// <param name="AABBMax">out: parcel.AABBMax &lt;x,y,0&gt</param>
        /// <returns>New parcel bitmap</returns>
        public bool[,] RemapLandBitmap(bool[,] bitmap_base, Vector2 displacement, float rotationDegrees, Vector2 boundingOrigin, Vector2 boundingSize, Vector2 newRegionSize, out bool isEmptyNow)
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

            int newX = (int)(newRegionSize.X / Constants.LandUnit);
            int newY = (int)(newRegionSize.Y / Constants.LandUnit);
            bool[,] bitmap_new = new bool[newX, newY];
            // displacement is relative to <0,0> in the destination region and defines where the origin of the data selected by the bounding-rectangle is placed
            int dispX = (int)Math.Floor(displacement.X / Constants.LandUnit);
            int dispY = (int)Math.Floor(displacement.Y / Constants.LandUnit);

            // startX/Y and endX/Y are coordinates in bitmap_tmp
            int startX = (int)Math.Floor(boundingOrigin.X / Constants.LandUnit) + offsetX;
            if (startX > tmpX) startX = tmpX;
            if (startX < 0) startX = 0;
            int startY = (int)Math.Floor(boundingOrigin.Y / Constants.LandUnit) + offsetY;
            if (startY > tmpY) startY = tmpY;
            if (startY < 0) startY = 0;

            int endX = (int)Math.Floor((boundingOrigin.X + boundingSize.X) / Constants.LandUnit) + offsetX;
            if (endX > tmpX) endX = tmpX;
            if (endX < 0) endX = 0;
            int endY = (int)Math.Floor((boundingOrigin.Y + boundingSize.Y) / Constants.LandUnit) + offsetY;
            if (endY > tmpY) endY = tmpY;
            if (endY < 0) endY = 0;

            //m_log.DebugFormat("{0} RemapLandBitmap: inSize=<{1},{2}>, disp=<{3},{4}> rot={5}, offset=<{6},{7}>, boundingStart=<{8},{9}>, boundingEnd=<{10},{11}>, cosR={12}, sinR={13}, outSize=<{14},{15}>", LogHeader,
            //                            baseX, baseY, dispX, dispY, radianRotation, offsetX, offsetY, startX, startY, endX, endY, cosR, sinR, newX, newY);

            isEmptyNow = true;

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
                            }
                        }
                        catch (Exception)   //just in case we've still not taken care of every way the arrays might go out of bounds! ;)
                        {
                            m_log.DebugFormat("{0} RemapLandBitmap - Bound & Displace: Out of Bounds sx={1} sy={2} dx={3} dy={4}", LogHeader, x, y, dx, dy);
                        }
                    }
                }
            }
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
        public bool[,] RemoveFromLandBitmap(bool[,] bitmap_base, bool[,] bitmap_new, out bool isEmptyNow)
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

            for (int x = 0; x < baseX; x++)
            {
                for (int y = 0; y < baseY; y++)
                {
                    if (bitmap_new[x, y]) bitmap_base[x, y] = false;
                    if (bitmap_base[x, y])
                    {
                        isEmptyNow = false;
                    }
                }
            }
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
            int byteNum = 0;
            int mask = 1;

            for (int y = 0; y < LandBitmap.GetLength(1); y++)
            {
                for (int x = 0; x < LandBitmap.GetLength(0); x++)
                {
                    if (LandBitmap[x, y])
                        tempByte |= mask;
                    mask <<= 1;
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
                tempConvertMap = new bool[m_regionSizeX / Constants.LandUnit, m_regionSizeY / Constants.LandUnit];
                tempConvertMap.Initialize();
                // Math.Min overcomes an old bug that might have made it into the database. Only use the bytes that fit into convertMap.
                bitmapLen = Math.Min(LandData.Bitmap.Length, tempConvertMap.GetLength(0) * tempConvertMap.GetLength(1) / 8);
                xLen = (m_regionSizeX / Constants.LandUnit);
                if (bitmapLen == 512)
                {
                    // Legacy bitmap being passed in. Use the legacy region size
                    //    and only set the lower area of the larger region.
                    xLen = (int)(Constants.RegionSize / Constants.LandUnit);
                }
            }
            // m_log.DebugFormat("{0} ConvertBytesToLandBitmap: bitmapLen={1}, xLen={2}", LogHeader, bitmapLen, xLen);

            byte tempByte;
            int x = 0, y = 0;
            for (int i = 0; i < bitmapLen; i++)
            {
                tempByte = LandData.Bitmap[i];
                for (int bitmask = 0x01; bitmask < 0x100; bitmask <<= 1)
                {
                    bool bit = (tempByte & bitmask) == bitmask;
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
            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions, true))
            {
                List<uint> resultLocalIDs = new();
                try
                {
                    lock (primsOverMe)
                    {
                        foreach (SceneObjectGroup obj in primsOverMe)
                        {
                            if (obj.LocalId > 0)
                            {
                                if (request_type == LandChannel.LAND_SELECT_OBJECTS_OWNER && obj.OwnerID.Equals(LandData.OwnerID))
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_GROUP && obj.GroupID.Equals(LandData.GroupID) && !LandData.GroupID.IsZero())
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_OTHER && obj.OwnerID.NotEqual(remote_client.AgentId))
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
            if (m_scenePermissions.CanEditParcelProperties(remote_client.AgentId, this, GroupPowers.LandOptions, true))
            {
                Dictionary<UUID, int> primCount = new();
                List<UUID> groups = new();

                lock (primsOverMe)
                {
                    //m_log.DebugFormat(
                    //    "[LAND OBJECT]: Request for SendLandObjectOwners() from {0} with {1} known prims on region",
                    //    remote_client.Name, primsOverMe.Count);

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
                            if (obj.OwnerID.Equals(obj.GroupID) && (!groups.Contains(obj.OwnerID)))
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
            Dictionary<UUID, int> ownersAndCount = new();

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

            if (!m_scene.TryGetScenePresence(LandData.OwnerID, out ScenePresence sp))
            {
                m_log.Error("[LAND OBJECT]: New owner is not present in scene");
                return;
            }

            lock (primsOverMe)
            {
                foreach (SceneObjectGroup obj in primsOverMe)
                {
                    if(m_scenePermissions.CanSellObject(previousOwner,obj, (byte)SaleType.Original))
                        m_BuySellModule.BuyObject(sp.ControllingClient, UUID.Zero, obj.LocalId, (byte)SaleType.Original, 0);
                }
            }
        }

        #endregion

        #region Object Returning

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReturnObject(SceneObjectGroup obj)
        {
            m_scene.returnObjects(new SceneObjectGroup[] { obj }, null);
        }

        public void ReturnLandObjects(uint type, UUID[] owners, UUID[] tasks, IClientAPI remote_client)
        {
            //m_log.DebugFormat(
            //    "[LAND OBJECT]: Request to return objects in {0} from {1}", LandData.Name, remote_client.Name);

            Dictionary<UUID,List<SceneObjectGroup>> returns = new();

            lock (primsOverMe)
            {
                if (type == (uint)ObjectReturnType.Owner)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.OwnerID.Equals(LandData.OwnerID))
                        {
                            if (returns.TryGetValue(obj.OwnerID, out List<SceneObjectGroup> rol))
                                rol.Add(obj);
                            else
                                returns[obj.OwnerID] = new List<SceneObjectGroup>() { obj };
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Group && LandData.GroupID.IsNotZero())
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.GroupID.Equals(LandData.GroupID))
                        {
                            if (obj.OwnerID.Equals(LandData.OwnerID))
                                continue;
                            if (returns.TryGetValue(obj.OwnerID, out List<SceneObjectGroup> rol))
                                rol.Add(obj);
                            else
                                returns[obj.OwnerID] = new List<SceneObjectGroup>() { obj };
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Other)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.OwnerID.NotEqual(LandData.OwnerID) &&
                            (obj.GroupID.NotEqual(LandData.GroupID) ||
                            LandData.GroupID.IsZero()))
                        {
                            if (returns.TryGetValue(obj.OwnerID, out List<SceneObjectGroup> rol))
                                rol.Add(obj);
                            else
                                returns[obj.OwnerID] = new List<SceneObjectGroup>() { obj };
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.List)
                {
                    List<UUID> ownerlist = new(owners);
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (ownerlist.Contains(obj.OwnerID))
                        {
                            if (returns.TryGetValue(obj.OwnerID, out List<SceneObjectGroup> rol))
                                rol.Add(obj);
                            else
                                returns[obj.OwnerID] = new List<SceneObjectGroup>() { obj };
                        }
                    }
                }
            }

            foreach (List<SceneObjectGroup> ol in returns.Values)
            {
                if (m_scenePermissions.CanReturnObjects(this, remote_client, ol))
                    m_scene.returnObjects(ol.ToArray(), remote_client);
            }
        }

        #endregion

        #region Object Adding/Removing from Parcel

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOverMeRecord()
        {
            lock (primsOverMe)
                primsOverMe.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPrimOverMe(SceneObjectGroup obj)
        {
//            m_log.DebugFormat("[LAND OBJECT]: Adding scene object {0} {1} over {2}", obj.Name, obj.LocalId, LandData.Name);

            lock (primsOverMe)
                primsOverMe.Add(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemovePrimFromOverMe(SceneObjectGroup obj)
        {
            //m_log.DebugFormat("[LAND OBJECT]: Removing scene object {0} {1} from over {2}", obj.Name, obj.LocalId, LandData.Name);
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
            if (String.IsNullOrWhiteSpace(url))
                LandData.MediaURL = String.Empty;
            else
            {
                try
                {
                    Uri dummmy = new(url, UriKind.Absolute);
                    LandData.MediaURL = url;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[LAND OBJECT]: SetMediaUrl error: {0}", e.Message);
                    return;
                }
            }
            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, LandData);
            SendLandUpdateToAvatarsOverMe();
        }

        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMusicUrl(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
                LandData.MusicURL =  String.Empty;
            else
            {
                try
                {
                    Uri dummmy = new(url, UriKind.Absolute);
                    LandData.MusicURL = url;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[LAND OBJECT]: SetMusicUrl error: {0}", e.Message);
                    return;
                }
            }
            m_scene.LandChannel.UpdateLandObject(LandData.LocalID, LandData);
            SendLandUpdateToAvatarsOverMe();
        }

        /// <summary>
        /// Get the music url for this land parcel
        /// </summary>
        /// <returns>The music url.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            // need to update dwell here bc landdata has no parent info
            if(LandData is not null && m_dwellModule is not null)
            {
                double now = Util.GetTimeStampMS();
                double elapsed = now - LandData.LastDwellTimeMS;
                if(elapsed > 150000) //2.5 minutes resolution / throttle
                {
                    float dwell = LandData.Dwell;
                    double cur = dwell * 60000.0;
                    double decay = 1.5e-8 * cur * elapsed;
                    cur -= decay;
                    if (cur < 0)
                        cur = 0;

                    UUID lgid = LandData.GlobalID;
                    m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
                    {
                        if(sp.IsNPC || sp.IsDeleted || sp.currentParcelUUID.NotEqual(lgid))
                            return;
                        cur += (now - sp.ParcelDwellTickMS);
                        sp.ParcelDwellTickMS = now;
                    });

                    float newdwell = (float)(cur * 1.666666666667e-5); 
                    LandData.Dwell = newdwell;

                    if(Math.Abs(newdwell - dwell) >= 0.9)
                        m_scene.EventManager.TriggerLandObjectAdded(this);
                }
            }
        }

        private void ExpireAccessList()
        {
            List<LandAccessEntry> delete = new();
            int now = Util.UnixTimeSinceEpoch();
            foreach (LandAccessEntry entry in LandData.ParcelAccessList)
            {
                if (entry.Expires != 0 && entry.Expires < now)
                    delete.Add(entry);
            }
            foreach (LandAccessEntry entry in delete)
            {
                LandData.ParcelAccessList.Remove(entry);

                if ((entry.Flags & AccessList.Access) != 0 && m_scene.TryGetScenePresence(entry.AgentID, out ScenePresence presence) && (!presence.IsChildAgent))
                {
                    ILandObject land = m_scene.LandChannel.GetLandObject(presence.AbsolutePosition.X, presence.AbsolutePosition.Y);
                    if (land.LandData.LocalID == LandData.LocalID)
                    {
                        Vector3 pos = m_scene.GetNearestAllowedPosition(presence, land);
                        presence.TeleportOnEject(pos);
                        presence.ControllingClient.SendAlertMessage("You have been ejected from this land");
                    }
                }
                m_log.DebugFormat("[LAND]: Removing entry {0} because it has expired", entry.AgentID);
            }

            if (delete.Count > 0)
                m_scene.EventManager.TriggerLandObjectUpdated((uint)LandData.LocalID, this);
        }

        public void StoreEnvironment(ViewerEnvironment VEnv)
        {
            int lastVersion = LandData.EnvironmentVersion;
            LandData.Environment = VEnv;
            if (VEnv == null)
                LandData.EnvironmentVersion = -1;
            else
            {
                ++LandData.EnvironmentVersion;
                VEnv.version = LandData.EnvironmentVersion;
            }
            if(lastVersion != LandData.EnvironmentVersion)
            {
                m_scene.LandChannel.UpdateLandObject(LandData.LocalID, LandData);
                SendLandUpdateToAvatarsOverMe();
            }
        }
    }
}
