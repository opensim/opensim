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
 *     * Neither the name of the OpenSim Project nor the
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
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.Land
{
    /// <summary>
    /// Keeps track of a specific piece of land's information
    /// </summary>
    public class LandObject : ILandObject
    {
        #region Member Variables

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool[,] m_landBitmap = new bool[64,64];

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

        public LLUUID regionUUID
        {
            get { return m_scene.RegionInfo.RegionID; }
        }

        #region Constructors

        public LandObject(LLUUID owner_id, bool is_group_owned, Scene scene)
        {
            m_scene = scene;
            landData.ownerID = owner_id;
            landData.isGroupOwned = is_group_owned;
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
            ILandObject newLand = new LandObject(landData.ownerID, landData.isGroupOwned, m_scene);

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
                        Math.Round((Convert.ToDecimal(landData.area) / Convert.ToDecimal(65536)) * m_scene.objectCapacity *
                                   Convert.ToDecimal(m_scene.RegionInfo.EstateSettings.objectBonusFactor))); ;
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
            remote_client.SendLandProperties(remote_client, sequence_id, snap_selection, request_result, landData, m_scene.RegionInfo.EstateSettings.objectBonusFactor, getParcelMaxPrimCount(this), getSimulatorMaxPrimCount(this), (uint)m_scene.RegionInfo.EstateSettings.regionFlags);
        }

        public void updateLandProperties(LandUpdateArgs args, IClientAPI remote_client)
        {
            if (m_scene.ExternalChecks.ExternalChecksCanEditParcel(remote_client.AgentId,this))
            {
                //Needs later group support
                LandData newData = landData.Copy();

                if (args.AuthBuyerID != newData.authBuyerID || args.SalePrice != newData.salePrice)
                {
                    if (m_scene.ExternalChecks.ExternalChecksCanSellParcel(remote_client.AgentId, this))
                    {
                        newData.authBuyerID = args.AuthBuyerID;
                        newData.salePrice = args.SalePrice;
                    }
                }
                newData.category = args.Category;
                newData.landDesc = args.Desc;
                newData.groupID = args.GroupID;
                newData.landingType = args.LandingType;
                newData.mediaAutoScale = args.MediaAutoScale;
                newData.mediaID = args.MediaID;
                newData.mediaURL = args.MediaURL;
                newData.musicURL = args.MusicURL;
                newData.landName = args.Name;
                newData.landFlags = args.ParcelFlags;
                newData.passHours = args.PassHours;
                newData.passPrice = args.PassPrice;
                newData.snapshotID = args.SnapshotID;
                newData.userLocation = args.UserLocation;
                newData.userLookAt = args.UserLookAt;

                m_scene.LandChannel.UpdateLandObject(landData.localID, newData);

                sendLandUpdateToAvatarsOverMe();
            }
        }

        public void updateLandSold(LLUUID avatarID, LLUUID groupID, bool groupOwned, uint AuctionID, int claimprice, int area)
        {
            LandData newData = landData.Copy();
            newData.ownerID = avatarID;
            newData.groupID = groupID;
            newData.isGroupOwned = groupOwned;
            //newData.auctionID = AuctionID;
            newData.claimDate = Util.UnixTimeSinceEpoch();
            newData.claimPrice = claimprice;
            newData.salePrice = 0;
            newData.authBuyerID = LLUUID.Zero;
            newData.landFlags &= ~(uint) (Parcel.ParcelFlags.ForSale | Parcel.ParcelFlags.ForSaleObjects | Parcel.ParcelFlags.SellParcelObjects);
            m_scene.LandChannel.UpdateLandObject(landData.localID, newData);

            sendLandUpdateToAvatarsOverMe();
        }

        public bool isEitherBannedOrRestricted(LLUUID avatar)
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

        public bool isBannedFromLand(LLUUID avatar)
        {
            if ((landData.landFlags & (uint) Parcel.ParcelFlags.UseBanList) > 0)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                entry.AgentID = avatar;
                entry.Flags = ParcelManager.AccessList.Ban;
                entry.Time = new DateTime();
                if (landData.parcelAccessList.Contains(entry))
                {
                    //They are banned, so lets send them a notice about this parcel
                    return true;
                }
            }
            return false;
        }

        public bool isRestrictedFromLand(LLUUID avatar)
        {
            if ((landData.landFlags & (uint) Parcel.ParcelFlags.UseAccessList) > 0)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                entry.AgentID = avatar;
                entry.Flags = ParcelManager.AccessList.Access;
                entry.Time = new DateTime();
                if (!landData.parcelAccessList.Contains(entry))
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
                        m_scene.LandChannel.GetLandObject((int) Math.Max(255, Math.Min(0, Math.Round(avatars[i].AbsolutePosition.X))),
                                                          (int) Math.Max(255, Math.Min(0, Math.Round(avatars[i].AbsolutePosition.Y))));
                }
                catch (Exception)
                {
                    m_log.Warn("[LAND]: " + "unable to get land at x: " + Math.Round(avatars[i].AbsolutePosition.X) + " y: " +
                               Math.Round(avatars[i].AbsolutePosition.Y));
                }

                if (over != null)
                {
                    if (over.landData.localID == landData.localID)
                    {
                        if ((over.landData.landFlags & (uint)Parcel.ParcelFlags.AllowDamage) != 0)
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

        public List<LLUUID>  createAccessListArrayByFlag(ParcelManager.AccessList flag)
        {
            List<LLUUID> list = new List<LLUUID>();
            foreach (ParcelManager.ParcelAccessEntry entry in landData.parcelAccessList)
            {
                if (entry.Flags == flag)
                {
                   list.Add(entry.AgentID);
                }
            }
            if (list.Count == 0)
            {
                list.Add(LLUUID.Zero);
            }

            return list;
        }

        public void sendAccessList(LLUUID agentID, LLUUID sessionID, uint flags, int sequenceID,
                                   IClientAPI remote_client)
        {

            if (flags == (uint) ParcelManager.AccessList.Access || flags == (uint) ParcelManager.AccessList.Both)
            {
                List<LLUUID> avatars = createAccessListArrayByFlag(ParcelManager.AccessList.Access);
                remote_client.SendLandAccessListData(avatars,(uint) ParcelManager.AccessList.Access,landData.localID);
            }

            if (flags == (uint) ParcelManager.AccessList.Ban || flags == (uint) ParcelManager.AccessList.Both)
            {
                List<LLUUID> avatars = createAccessListArrayByFlag(ParcelManager.AccessList.Ban);
                remote_client.SendLandAccessListData(avatars, (uint)ParcelManager.AccessList.Ban, landData.localID);
            }
        }

        public void updateAccessList(uint flags, List<ParcelManager.ParcelAccessEntry> entries, IClientAPI remote_client)
        {
            LandData newData = landData.Copy();

            if (entries.Count == 1 && entries[0].AgentID == LLUUID.Zero)
            {
                entries.Clear();
            }

            List<ParcelManager.ParcelAccessEntry> toRemove = new List<ParcelManager.ParcelAccessEntry>();
            foreach (ParcelManager.ParcelAccessEntry entry in newData.parcelAccessList)
            {
                if (entry.Flags == (ParcelManager.AccessList) flags)
                {
                    toRemove.Add(entry);
                }
            }

            foreach (ParcelManager.ParcelAccessEntry entry in toRemove)
            {
                newData.parcelAccessList.Remove(entry);
            }
            foreach (ParcelManager.ParcelAccessEntry entry in entries)
            {
                ParcelManager.ParcelAccessEntry temp = new ParcelManager.ParcelAccessEntry();
                temp.AgentID = entry.AgentID;
                temp.Time = new DateTime(); //Pointless? Yes.
                temp.Flags = (ParcelManager.AccessList) flags;

                if (!newData.parcelAccessList.Contains(temp))
                {
                    newData.parcelAccessList.Add(temp);
                }
            }

            m_scene.LandChannel.UpdateLandObject(landData.localID, newData);
        }

        #endregion

        #region Update Functions

        public void updateLandBitmapByteArray()
        {
            landData.landBitmapByteArray = convertLandBitmapToBytes();
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
            if (tx > 255)
                tx = 255;
            int ty = min_y * 4;
            if (ty > 255)
                ty = 255;
            landData.AABBMin =
                new LLVector3((float) (min_x * 4), (float) (min_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);

            tx = max_x * 4;
            if (tx > 255)
                tx = 255;
            ty = max_y * 4;
            if (ty > 255)
                ty = 255;
            landData.AABBMax =
                new LLVector3((float) (max_x * 4), (float) (max_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);
            landData.area = tempArea;
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
            bool[,] tempConvertMap = new bool[64,64];
            tempConvertMap.Initialize();
            byte tempByte = 0;
            int x = 0, y = 0, i = 0, bitNum = 0;
            for (i = 0; i < 512; i++)
            {
                tempByte = landData.landBitmapByteArray[i];
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

        public void sendForceObjectSelect(int local_id, int request_type, IClientAPI remote_client)
        {
            if (m_scene.ExternalChecks.ExternalChecksCanEditParcel(remote_client.AgentId, this))
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
                                if (request_type == LandChannel.LAND_SELECT_OBJECTS_OWNER && obj.OwnerID == landData.ownerID)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                // else if (request_type == LandManager.LAND_SELECT_OBJECTS_GROUP && ...) // TODO: group support
                                // {
                                // }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_OTHER &&
                                         obj.OwnerID != remote_client.AgentId)
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
            if (m_scene.ExternalChecks.ExternalChecksCanEditParcel(remote_client.AgentId, this))
            {
                Dictionary<LLUUID, int> primCount = new Dictionary<LLUUID, int>();

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
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        m_log.Error("[LAND]: Unable to Enumerate Land object arr.");
                    }
                }

                remote_client.SendLandObjectOwners(primCount);
            }
        }

        public Dictionary<LLUUID, int> getLandObjectOwners()
        {
            Dictionary<LLUUID, int> ownersAndCount = new Dictionary<LLUUID, int>();
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

        public void returnLandObjects(uint type, LLUUID[] owners, IClientAPI remote_client)
        {
            List<SceneObjectGroup> objlist = new List<SceneObjectGroup>();
            for (int i = 0; i < owners.Length; i++)
            {
                lock (primsOverMe)
                {
                    try
                    {
                        foreach (SceneObjectGroup obj in primsOverMe)
                        {
                            if (obj.OwnerID == owners[i])
                                objlist.Add(obj);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        m_log.Info("[PARCEL]: Unable to figure out all the objects owned by " + owners[i].ToString() + " arr.");
                    }
                }
            }
            m_scene.returnObjects(objlist.ToArray(), remote_client.AgentId);
        }

        #endregion

        #region Object Adding/Removing from Parcel

        public void resetLandPrimCounts()
        {
            landData.groupPrims = 0;
            landData.ownerPrims = 0;
            landData.otherPrims = 0;
            landData.selectedPrims = 0;


            lock (primsOverMe)
                primsOverMe.Clear();
        }

        public void addPrimToCount(SceneObjectGroup obj)
        {

            LLUUID prim_owner = obj.OwnerID;
            int prim_count = obj.PrimCount;

            if (obj.IsSelected)
            {
                landData.selectedPrims += prim_count;
            }
            else
            {
                if (prim_owner == landData.ownerID)
                {
                    landData.ownerPrims += prim_count;
                }
                else
                {
                    landData.otherPrims += prim_count;
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
                    LLUUID prim_owner = obj.OwnerID;
                    int prim_count = obj.PrimCount;

                    if (prim_owner == landData.ownerID)
                    {
                        landData.ownerPrims -= prim_count;
                    }
                    else if (prim_owner == landData.groupID)
                    {
                        landData.groupPrims -= prim_count;
                    }
                    else
                    {
                        landData.otherPrims -= prim_count;
                    }

                    primsOverMe.Remove(obj);
                }
            }
        }

        #endregion

        #endregion

        #endregion
    }
}