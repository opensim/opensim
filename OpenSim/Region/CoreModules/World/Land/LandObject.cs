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

        protected LandData m_landData = new LandData();
        protected Scene m_scene;
        protected List<SceneObjectGroup> primsOverMe = new List<SceneObjectGroup>();

        public bool[,] landBitmap
        {
            get { return m_landBitmap; }
            set { m_landBitmap = value; }
        }

        #endregion

        #region ILandObject Members

        public LandData landData
        {
            get { return m_landData; }

            set { m_landData = value; }
        }

        public UUID regionUUID
        {
            get { return m_scene.RegionInfo.RegionID; }
        }

        #region Constructors

        public LandObject(UUID owner_id, bool is_group_owned, Scene scene)
        {
            m_scene = scene;
            landData.OwnerID = owner_id;
            landData.IsGroupOwned = is_group_owned;
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
        public bool containsPoint(int x, int y)
        {
            if (x >= 0 && y >= 0 && x <= Constants.RegionSize && x <= Constants.RegionSize)
            {
                return (landBitmap[x / 4, y / 4] == true);
            }
            else
            {
                return false;
            }
        }

        public ILandObject Copy()
        {
            ILandObject newLand = new LandObject(landData.OwnerID, landData.IsGroupOwned, m_scene);

            //Place all new variables here!
            newLand.landBitmap = (bool[,]) (landBitmap.Clone());
            newLand.landData = landData.Copy();

            return newLand;
        }

        static overrideParcelMaxPrimCountDelegate overrideParcelMaxPrimCount;
        static overrideSimulatorMaxPrimCountDelegate overrideSimulatorMaxPrimCount;

        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            overrideParcelMaxPrimCount = overrideDel;
        }
        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            overrideSimulatorMaxPrimCount = overrideDel;
        }

        public int getParcelMaxPrimCount(ILandObject thisObject)
        {
            if (overrideParcelMaxPrimCount != null)
            {
                return overrideParcelMaxPrimCount(thisObject);
            }
            else
            {
                //Normal Calculations
                return Convert.ToInt32(
                        Math.Round((Convert.ToDecimal(landData.Area) / Convert.ToDecimal(65536)) * m_scene.objectCapacity *
                                   Convert.ToDecimal(m_scene.RegionInfo.RegionSettings.ObjectBonus))); ;
            }
        }
        public int getSimulatorMaxPrimCount(ILandObject thisObject)
        {
            if (overrideSimulatorMaxPrimCount != null)
            {
                return overrideSimulatorMaxPrimCount(thisObject);
            }
            else
            {
                //Normal Calculations
                return m_scene.objectCapacity;
            }
        }
        #endregion

        #region Packet Request Handling

        public void sendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
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
            remote_client.SendLandProperties(sequence_id,
                    snap_selection, request_result, landData,
                    (float)m_scene.RegionInfo.RegionSettings.ObjectBonus,
                    getParcelMaxPrimCount(this),
                    getSimulatorMaxPrimCount(this), regionFlags);
        }

        public void updateLandProperties(LandUpdateArgs args, IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId,this))
            {
                //Needs later group support
                LandData newData = landData.Copy();

                if (args.AuthBuyerID != newData.AuthBuyerID || args.SalePrice != newData.SalePrice)
                {
                    if (m_scene.Permissions.CanSellParcel(remote_client.AgentId, this))
                    {
                        newData.AuthBuyerID = args.AuthBuyerID;
                        newData.SalePrice = args.SalePrice;
                    }
                }
                newData.Category = args.Category;
                newData.Description = args.Desc;
                newData.GroupID = args.GroupID;
                newData.LandingType = args.LandingType;
                newData.MediaAutoScale = args.MediaAutoScale;
                newData.MediaID = args.MediaID;
                newData.MediaURL = args.MediaURL;
                newData.MusicURL = args.MusicURL;
                newData.Name = args.Name;
                newData.Flags = args.ParcelFlags;
                newData.PassHours = args.PassHours;
                newData.PassPrice = args.PassPrice;
                newData.SnapshotID = args.SnapshotID;
                newData.UserLocation = args.UserLocation;
                newData.UserLookAt = args.UserLookAt;

                m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);

                sendLandUpdateToAvatarsOverMe();
            }
        }

        public void updateLandSold(UUID avatarID, UUID groupID, bool groupOwned, uint AuctionID, int claimprice, int area)
        {
            LandData newData = landData.Copy();
            newData.OwnerID = avatarID;
            newData.GroupID = groupID;
            newData.IsGroupOwned = groupOwned;
            //newData.auctionID = AuctionID;
            newData.ClaimDate = Util.UnixTimeSinceEpoch();
            newData.ClaimPrice = claimprice;
            newData.SalePrice = 0;
            newData.AuthBuyerID = UUID.Zero;
            newData.Flags &= ~(uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects);
            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);

            sendLandUpdateToAvatarsOverMe();
        }

        public void deedToGroup(UUID groupID)
        {
            LandData newData = landData.Copy();
            newData.OwnerID = groupID;
            newData.GroupID = groupID;
            newData.IsGroupOwned = true;

            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);

            sendLandUpdateToAvatarsOverMe();
        }

        public bool isEitherBannedOrRestricted(UUID avatar)
        {
            if (isBannedFromLand(avatar))
            {
                return true;
            }
            else if (isRestrictedFromLand(avatar))
            {
                return true;
            }
            return false;
        }

        public bool isBannedFromLand(UUID avatar)
        {
            if ((landData.Flags & (uint) ParcelFlags.UseBanList) > 0)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                entry.AgentID = avatar;
                entry.Flags = AccessList.Ban;
                entry.Time = new DateTime();
                if (landData.ParcelAccessList.Contains(entry))
                {
                    //They are banned, so lets send them a notice about this parcel
                    return true;
                }
            }
            return false;
        }

        public bool isRestrictedFromLand(UUID avatar)
        {
            if ((landData.Flags & (uint) ParcelFlags.UseAccessList) > 0)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                entry.AgentID = avatar;
                entry.Flags = AccessList.Access;
                entry.Time = new DateTime();
                if (!landData.ParcelAccessList.Contains(entry))
                {
                    //They are not allowed in this parcel, but not banned, so lets send them a notice about this parcel
                    return true;
                }
            }
            return false;
        }

        public void sendLandUpdateToClient(IClientAPI remote_client)
        {
            sendLandProperties(0, false, 0, remote_client);
        }

        public void sendLandUpdateToAvatarsOverMe()
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();
            ILandObject over = null;
            for (int i = 0; i < avatars.Count; i++)
            {
                try
                {
                    over =
                        m_scene.LandChannel.GetLandObject(Util.Clamp<int>((int)Math.Round(avatars[i].AbsolutePosition.X), 0, ((int)Constants.RegionSize - 1)),
                                                          Util.Clamp<int>((int)Math.Round(avatars[i].AbsolutePosition.Y), 0, ((int)Constants.RegionSize - 1)));
                }
                catch (Exception)
                {
                    m_log.Warn("[LAND]: " + "unable to get land at x: " + Math.Round(avatars[i].AbsolutePosition.X) + " y: " +
                               Math.Round(avatars[i].AbsolutePosition.Y));
                }

                if (over != null)
                {
                    if (over.landData.LocalID == landData.LocalID)
                    {
                        if (((over.landData.Flags & (uint)ParcelFlags.AllowDamage) != 0) && m_scene.RegionInfo.RegionSettings.AllowDamage)
                            avatars[i].Invulnerable = false;
                        else
                            avatars[i].Invulnerable = true;

                        sendLandUpdateToClient(avatars[i].ControllingClient);
                    }
                }
            }
        }

        #endregion

        #region AccessList Functions

        public List<UUID>  createAccessListArrayByFlag(AccessList flag)
        {
            List<UUID> list = new List<UUID>();
            foreach (ParcelManager.ParcelAccessEntry entry in landData.ParcelAccessList)
            {
                if (entry.Flags == flag)
                {
                   list.Add(entry.AgentID);
                }
            }
            if (list.Count == 0)
            {
                list.Add(UUID.Zero);
            }

            return list;
        }

        public void sendAccessList(UUID agentID, UUID sessionID, uint flags, int sequenceID,
                                   IClientAPI remote_client)
        {

            if (flags == (uint) AccessList.Access || flags == (uint) AccessList.Both)
            {
                List<UUID> avatars = createAccessListArrayByFlag(AccessList.Access);
                remote_client.SendLandAccessListData(avatars,(uint) AccessList.Access,landData.LocalID);
            }

            if (flags == (uint) AccessList.Ban || flags == (uint) AccessList.Both)
            {
                List<UUID> avatars = createAccessListArrayByFlag(AccessList.Ban);
                remote_client.SendLandAccessListData(avatars, (uint)AccessList.Ban, landData.LocalID);
            }
        }

        public void updateAccessList(uint flags, List<ParcelManager.ParcelAccessEntry> entries, IClientAPI remote_client)
        {
            LandData newData = landData.Copy();

            if (entries.Count == 1 && entries[0].AgentID == UUID.Zero)
            {
                entries.Clear();
            }

            List<ParcelManager.ParcelAccessEntry> toRemove = new List<ParcelManager.ParcelAccessEntry>();
            foreach (ParcelManager.ParcelAccessEntry entry in newData.ParcelAccessList)
            {
                if (entry.Flags == (AccessList)flags)
                {
                    toRemove.Add(entry);
                }
            }

            foreach (ParcelManager.ParcelAccessEntry entry in toRemove)
            {
                newData.ParcelAccessList.Remove(entry);
            }
            foreach (ParcelManager.ParcelAccessEntry entry in entries)
            {
                ParcelManager.ParcelAccessEntry temp = new ParcelManager.ParcelAccessEntry();
                temp.AgentID = entry.AgentID;
                temp.Time = new DateTime(); //Pointless? Yes.
                temp.Flags = (AccessList)flags;

                if (!newData.ParcelAccessList.Contains(temp))
                {
                    newData.ParcelAccessList.Add(temp);
                }
            }

            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);
        }

        #endregion

        #region Update Functions

        public void updateLandBitmapByteArray()
        {
            landData.Bitmap = convertLandBitmapToBytes();
        }

        /// <summary>
        /// Update all settings in land such as area, bitmap byte array, etc
        /// </summary>
        public void forceUpdateLandInfo()
        {
            updateAABBAndAreaValues();
            updateLandBitmapByteArray();
        }

        public void setLandBitmapFromByteArray()
        {
            landBitmap = convertBytesToLandBitmap();
        }

        /// <summary>
        /// Updates the AABBMin and AABBMax values after area/shape modification of the land object
        /// </summary>
        private void updateAABBAndAreaValues()
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
                    if (landBitmap[x, y] == true)
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
            landData.AABBMin =
                new Vector3((float) (min_x * 4), (float) (min_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);

            tx = max_x * 4;
            if (tx > ((int)Constants.RegionSize - 1))
                tx = ((int)Constants.RegionSize - 1);
            ty = max_y * 4;
            if (ty > ((int)Constants.RegionSize - 1))
                ty = ((int)Constants.RegionSize - 1);
            landData.AABBMax =
                new Vector3((float) (max_x * 4), (float) (max_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);
            landData.Area = tempArea;
        }

        #endregion

        #region Land Bitmap Functions

        /// <summary>
        /// Sets the land's bitmap manually
        /// </summary>
        /// <param name="bitmap">64x64 block representing where this land is on a map</param>
        public void setLandBitmap(bool[,] bitmap)
        {
            if (bitmap.GetLength(0) != 64 || bitmap.GetLength(1) != 64 || bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                //throw new Exception("Error: Invalid Parcel Bitmap");
            }
            else
            {
                //Valid: Lets set it
                landBitmap = bitmap;
                forceUpdateLandInfo();
            }
        }

        /// <summary>
        /// Gets the land's bitmap manually
        /// </summary>
        /// <returns></returns>
        public bool[,] getLandBitmap()
        {
            return landBitmap;
        }

        /// <summary>
        /// Full sim land object creation
        /// </summary>
        /// <returns></returns>
        public bool[,] basicFullRegionLandBitmap()
        {
            return getSquareLandBitmap(0, 0, (int) Constants.RegionSize, (int) Constants.RegionSize);
        }

        /// <summary>
        /// Used to modify the bitmap between the x and y points. Points use 64 scale
        /// </summary>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <returns></returns>
        public bool[,] getSquareLandBitmap(int start_x, int start_y, int end_x, int end_y)
        {
            bool[,] tempBitmap = new bool[64,64];
            tempBitmap.Initialize();

            tempBitmap = modifyLandBitmapSquare(tempBitmap, start_x, start_y, end_x, end_y, true);
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
        public bool[,] modifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y,
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
        public bool[,] mergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add)
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
        private byte[] convertLandBitmapToBytes()
        {
            byte[] tempConvertArr = new byte[512];
            byte tempByte = 0;
            int x, y, i, byteNum = 0;
            i = 0;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(landBitmap[x, y]) << (i++ % 8));
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

        private bool[,] convertBytesToLandBitmap()
        {
            bool[,] tempConvertMap = new bool[landArrayMax, landArrayMax];
            tempConvertMap.Initialize();
            byte tempByte = 0;
            int x = 0, y = 0, i = 0, bitNum = 0;
            for (i = 0; i < 512; i++)
            {
                tempByte = landData.Bitmap[i];
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

        public void sendForceObjectSelect(int local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this))
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
                                if (request_type == LandChannel.LAND_SELECT_OBJECTS_OWNER && obj.OwnerID == landData.OwnerID)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_GROUP && obj.GroupID == landData.GroupID && landData.GroupID != UUID.Zero)
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
        public void sendLandObjectOwners(IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this))
            {
                Dictionary<UUID, int> primCount = new Dictionary<UUID, int>();
                List<UUID> groups = new List<UUID>();

                lock (primsOverMe)
                {
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
                                m_log.Info("[LAND]: " + "Got Null Reference when searching land owners from the parcel panel");
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

                remote_client.SendLandObjectOwners(landData, groups, primCount);
            }
        }

        public Dictionary<UUID, int> getLandObjectOwners()
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

        public void returnObject(SceneObjectGroup obj)
        {
            SceneObjectGroup[] objs = new SceneObjectGroup[1];
            objs[0] = obj;
            m_scene.returnObjects(objs, obj.OwnerID);
        }

        public void returnLandObjects(uint type, UUID[] owners, UUID[] tasks, IClientAPI remote_client)
        {
            Dictionary<UUID,List<SceneObjectGroup>> returns =
                    new Dictionary<UUID,List<SceneObjectGroup>>();

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
                if (m_scene.Permissions.CanUseObjectReturn(this, type, remote_client, ol))
                    m_scene.returnObjects(ol.ToArray(), remote_client.AgentId);
            }
        }

        #endregion

        #region Object Adding/Removing from Parcel

        public void resetLandPrimCounts()
        {
            landData.GroupPrims = 0;
            landData.OwnerPrims = 0;
            landData.OtherPrims = 0;
            landData.SelectedPrims = 0;


            lock (primsOverMe)
                primsOverMe.Clear();
        }

        public void addPrimToCount(SceneObjectGroup obj)
        {

            UUID prim_owner = obj.OwnerID;
            int prim_count = obj.PrimCount;

            if (obj.IsSelected)
            {
                landData.SelectedPrims += prim_count;
            }
            else
            {
                if (prim_owner == landData.OwnerID)
                {
                    landData.OwnerPrims += prim_count;
                }
                else if ((obj.GroupID == landData.GroupID ||
                          prim_owner  == landData.GroupID) &&
                          landData.GroupID != UUID.Zero)
                {
                    landData.GroupPrims += prim_count;
                }
                else
                {
                    landData.OtherPrims += prim_count;
                }
            }

            lock (primsOverMe)
                primsOverMe.Add(obj);
        }

        public void removePrimFromCount(SceneObjectGroup obj)
        {
            lock (primsOverMe)
            {
                if (primsOverMe.Contains(obj))
                {
                    UUID prim_owner = obj.OwnerID;
                    int prim_count = obj.PrimCount;

                    if (prim_owner == landData.OwnerID)
                    {
                        landData.OwnerPrims -= prim_count;
                    }
                    else if (obj.GroupID == landData.GroupID ||
                             prim_owner  == landData.GroupID)
                    {
                        landData.GroupPrims -= prim_count;
                    }
                    else
                    {
                        landData.OtherPrims -= prim_count;
                    }

                    primsOverMe.Remove(obj);
                }
            }
        }

        #endregion

        #endregion

        #endregion
        
        /// <summary>
        /// Set the media url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMediaUrl(string url)
        {
            landData.MediaURL = url;
            sendLandUpdateToAvatarsOverMe();
        }
        
        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMusicUrl(string url)
        {
            landData.MusicURL = url;
            sendLandUpdateToAvatarsOverMe();
        }
    }
}
