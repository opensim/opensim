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
using libsecondlife.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.LandManagement
{

    #region LandObject Class

    /// <summary>
    /// Keeps track of a specific piece of land's information
    /// </summary>
    public class LandObject : ILandObject
    {
        #region Member Variables

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected LandData m_landData = new LandData();
        protected List<SceneObjectGroup> primsOverMe = new List<SceneObjectGroup>();
        protected Scene m_scene;

        private bool[,] m_landBitmap = new bool[64,64];

        public bool[,] landBitmap
        {
            get
            {
                return m_landBitmap;
            }
            set
            {
                m_landBitmap = value;
            }
        }

        #endregion

        #region ILandObject Members

        public LandData landData
        {
            get 
            { 
                return m_landData;
            }

            set 
            { 
                m_landData = value; 
            }
        }

        public LLUUID  regionUUID
        {
            get { return m_scene.RegionInfo.RegionID; }
        }

        #endregion
        

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
                return (landBitmap[x/4, y/4] == true);
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

        #endregion

        #region Packet Request Handling

        /// <summary>
        /// Sends land properties as requested
        /// </summary>
        /// <param name="sequence_id">ID sent by client for them to keep track of</param>
        /// <param name="snap_selection">Bool sent by client for them to use</param>
        /// <param name="remote_client">Object representing the client</param>
        public void sendLandProperties(int sequence_id, bool snap_selection, int request_result,
                                       IClientAPI remote_client)
        {
            ParcelPropertiesPacket updatePacket = (ParcelPropertiesPacket) PacketPool.Instance.GetPacket(PacketType.ParcelProperties);
            // TODO: don't create new blocks if recycling an old packet
 
            updatePacket.ParcelData.AABBMax = landData.AABBMax;
            updatePacket.ParcelData.AABBMin = landData.AABBMin;
            updatePacket.ParcelData.Area = landData.area;
            updatePacket.ParcelData.AuctionID = landData.auctionID;
            updatePacket.ParcelData.AuthBuyerID = landData.authBuyerID; //unemplemented

            updatePacket.ParcelData.Bitmap = landData.landBitmapByteArray;

            updatePacket.ParcelData.Desc = Helpers.StringToField(landData.landDesc);
            updatePacket.ParcelData.Category = (byte) landData.category;
            updatePacket.ParcelData.ClaimDate = landData.claimDate;
            updatePacket.ParcelData.ClaimPrice = landData.claimPrice;
            updatePacket.ParcelData.GroupID = landData.groupID;
            updatePacket.ParcelData.GroupPrims = landData.groupPrims;
            updatePacket.ParcelData.IsGroupOwned = landData.isGroupOwned;
            updatePacket.ParcelData.LandingType = (byte) landData.landingType;
            updatePacket.ParcelData.LocalID = landData.localID;
            if (landData.area > 0)
            {
                updatePacket.ParcelData.MaxPrims =
                    Convert.ToInt32(
                        Math.Round((Convert.ToDecimal(landData.area)/Convert.ToDecimal(65536))*m_scene.objectCapacity*
                                   Convert.ToDecimal(m_scene.RegionInfo.EstateSettings.objectBonusFactor)));
            }
            else
            {
                updatePacket.ParcelData.MaxPrims = 0;
            }
            updatePacket.ParcelData.MediaAutoScale = landData.mediaAutoScale;
            updatePacket.ParcelData.MediaID = landData.mediaID;
            updatePacket.ParcelData.MediaURL = Helpers.StringToField(landData.mediaURL);
            updatePacket.ParcelData.MusicURL = Helpers.StringToField(landData.musicURL);
            updatePacket.ParcelData.Name = Helpers.StringToField(landData.landName);
            updatePacket.ParcelData.OtherCleanTime = 0; //unemplemented
            updatePacket.ParcelData.OtherCount = 0; //unemplemented
            updatePacket.ParcelData.OtherPrims = landData.otherPrims;
            updatePacket.ParcelData.OwnerID = landData.ownerID;
            updatePacket.ParcelData.OwnerPrims = landData.ownerPrims;
            updatePacket.ParcelData.ParcelFlags = landData.landFlags;
            updatePacket.ParcelData.ParcelPrimBonus = m_scene.RegionInfo.EstateSettings.objectBonusFactor;
            updatePacket.ParcelData.PassHours = landData.passHours;
            updatePacket.ParcelData.PassPrice = landData.passPrice;
            updatePacket.ParcelData.PublicCount = 0; //unemplemented
            
            uint regionFlags = (uint) m_scene.RegionInfo.EstateSettings.regionFlags;
            updatePacket.ParcelData.RegionDenyAnonymous = ((regionFlags & (uint) Simulator.RegionFlags.DenyAnonymous) >
                                                           0);
            updatePacket.ParcelData.RegionDenyIdentified = ((regionFlags & (uint) Simulator.RegionFlags.DenyIdentified) >
                                                            0);
            updatePacket.ParcelData.RegionDenyTransacted = ((regionFlags & (uint) Simulator.RegionFlags.DenyTransacted) >
                                                            0);
            updatePacket.ParcelData.RegionPushOverride = ((regionFlags & (uint) Simulator.RegionFlags.RestrictPushObject) >
                                                          0);

            updatePacket.ParcelData.RentPrice = 0;
            updatePacket.ParcelData.RequestResult = request_result;
            updatePacket.ParcelData.SalePrice = landData.salePrice;
            updatePacket.ParcelData.SelectedPrims = landData.selectedPrims;
            updatePacket.ParcelData.SelfCount = 0; //unemplemented
            updatePacket.ParcelData.SequenceID = sequence_id;
            if (landData.simwideArea > 0)
            {
                updatePacket.ParcelData.SimWideMaxPrims =
                    Convert.ToInt32(
                        Math.Round((Convert.ToDecimal(landData.simwideArea) / Convert.ToDecimal(65536)) * m_scene.objectCapacity *
                                   Convert.ToDecimal(m_scene.RegionInfo.EstateSettings.objectBonusFactor)));
            }
            else
            {
                updatePacket.ParcelData.SimWideMaxPrims = 0;
            }
            updatePacket.ParcelData.SimWideTotalPrims = landData.simwidePrims;
            updatePacket.ParcelData.SnapSelection = snap_selection;
            updatePacket.ParcelData.SnapshotID = landData.snapshotID;
            updatePacket.ParcelData.Status = (byte) landData.landStatus;
            updatePacket.ParcelData.TotalPrims = landData.ownerPrims + landData.groupPrims + landData.otherPrims +
                                                 landData.selectedPrims;
            updatePacket.ParcelData.UserLocation = landData.userLocation;
            updatePacket.ParcelData.UserLookAt = landData.userLookAt;
            remote_client.OutPacket((Packet) updatePacket, ThrottleOutPacketType.Task);
        }

        public void updateLandProperties(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client)
        {
            if (remote_client.AgentId == landData.ownerID)
            {
                //Needs later group support
                LandData newData = landData.Copy();
                newData.authBuyerID = packet.ParcelData.AuthBuyerID;
                newData.category = (Parcel.ParcelCategory) packet.ParcelData.Category;
                newData.landDesc = Helpers.FieldToUTF8String(packet.ParcelData.Desc);
                newData.groupID = packet.ParcelData.GroupID;
                newData.landingType = packet.ParcelData.LandingType;
                newData.mediaAutoScale = packet.ParcelData.MediaAutoScale;
                newData.mediaID = packet.ParcelData.MediaID;
                newData.mediaURL = Helpers.FieldToUTF8String(packet.ParcelData.MediaURL);
                newData.musicURL = Helpers.FieldToUTF8String(packet.ParcelData.MusicURL);
                newData.landName = Helpers.FieldToUTF8String(packet.ParcelData.Name);
                newData.landFlags = packet.ParcelData.ParcelFlags;
                newData.passHours = packet.ParcelData.PassHours;
                newData.passPrice = packet.ParcelData.PassPrice;
                newData.salePrice = packet.ParcelData.SalePrice;
                newData.snapshotID = packet.ParcelData.SnapshotID;
                newData.userLocation = packet.ParcelData.UserLocation;
                newData.userLookAt = packet.ParcelData.UserLookAt;
                
                m_scene.LandChannel.updateLandObject(landData.localID, newData);

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
            newData.landFlags &= ~(uint)(Parcel.ParcelFlags.ForSale | Parcel.ParcelFlags.ForSaleObjects | Parcel.ParcelFlags.SellParcelObjects);
            m_scene.LandChannel.updateLandObject(landData.localID, newData);

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
                        m_scene.LandChannel.getLandObject((int)Math.Max(255,Math.Min(0,Math.Round(avatars[i].AbsolutePosition.X))),
                                                             (int)Math.Max(255,Math.Min(0,Math.Round(avatars[i].AbsolutePosition.Y))));
                }
                catch (Exception)
                {
                    m_log.Warn("[LAND]: " + "unable to get land at x: " + Math.Round(avatars[i].AbsolutePosition.X) + " y: " + Math.Round(avatars[i].AbsolutePosition.Y));
                }

                if (over != null)
                {
                    if (over.landData.localID == landData.localID)
                    {
                        sendLandUpdateToClient(avatars[i].ControllingClient);
                    }
                }
            }
        }

        #endregion

        #region AccessList Functions

        public ParcelAccessListReplyPacket.ListBlock[] createAccessListArrayByFlag(ParcelManager.AccessList flag)
        {
            List<ParcelAccessListReplyPacket.ListBlock> list = new List<ParcelAccessListReplyPacket.ListBlock>();
            foreach (ParcelManager.ParcelAccessEntry entry in landData.parcelAccessList)
            {
                if (entry.Flags == flag)
                {
                    ParcelAccessListReplyPacket.ListBlock listBlock = new ParcelAccessListReplyPacket.ListBlock();

                    listBlock.Flags = (uint) 0;
                    listBlock.ID = entry.AgentID;
                    listBlock.Time = 0;

                    list.Add(listBlock);
                }
            }

            if (list.Count == 0)
            {
                ParcelAccessListReplyPacket.ListBlock listBlock = new ParcelAccessListReplyPacket.ListBlock();

                listBlock.Flags = (uint) 0;
                listBlock.ID = LLUUID.Zero;
                listBlock.Time = 0;

                list.Add(listBlock);
            }
            return list.ToArray();
        }

        public void sendAccessList(LLUUID agentID, LLUUID sessionID, uint flags, int sequenceID,
                                   IClientAPI remote_client)
        {
            ParcelAccessListReplyPacket replyPacket;

            if (flags == (uint) ParcelManager.AccessList.Access || flags == (uint) ParcelManager.AccessList.Both)
            {
                replyPacket = (ParcelAccessListReplyPacket) PacketPool.Instance.GetPacket(PacketType.ParcelAccessListReply);
                replyPacket.Data.AgentID = agentID;
                replyPacket.Data.Flags = (uint) ParcelManager.AccessList.Access;
                replyPacket.Data.LocalID = landData.localID;
                replyPacket.Data.SequenceID = 0;

                replyPacket.List = createAccessListArrayByFlag(ParcelManager.AccessList.Access);
                remote_client.OutPacket((Packet) replyPacket, ThrottleOutPacketType.Task);
            }

            if (flags == (uint) ParcelManager.AccessList.Ban || flags == (uint) ParcelManager.AccessList.Both)
            {
                replyPacket = (ParcelAccessListReplyPacket) PacketPool.Instance.GetPacket(PacketType.ParcelAccessListReply);
                replyPacket.Data.AgentID = agentID;
                replyPacket.Data.Flags = (uint) ParcelManager.AccessList.Ban;
                replyPacket.Data.LocalID = landData.localID;
                replyPacket.Data.SequenceID = 0;

                replyPacket.List = createAccessListArrayByFlag(ParcelManager.AccessList.Ban);
                remote_client.OutPacket((Packet) replyPacket, ThrottleOutPacketType.Task);
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

            m_scene.LandChannel.updateLandObject(landData.localID, newData);
        }

        #endregion

        #region Update Functions

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
                new LLVector3((float)(min_x * 4), (float)(min_y * 4),
                              (float)m_scene.Heightmap[tx, ty]);

            tx = max_x * 4;
            if (tx > 255)
                tx = 255;
            ty = max_y * 4;
            if (ty > 255)
                ty = 255;
            landData.AABBMax =
                new LLVector3((float)(max_x * 4), (float)(max_y * 4),
                              (float)m_scene.Heightmap[tx, ty]);
            landData.area = tempArea;
        }

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
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(landBitmap[x, y]) << (i++%8));
                    if (i%8 == 0)
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

        /// <summary>
        /// Full sim land object creation
        /// </summary>
        /// <returns></returns>
        public bool[,] basicFullRegionLandBitmap()
        {
            return getSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize);
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
                    if (x >= start_x/4 && x < end_x/4
                        && y >= start_y/4 && y < end_y/4)
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

        #endregion

        #region Object Select and Object Owner Listing

        public void sendForceObjectSelect(int local_id, int request_type, IClientAPI remote_client)
        {
            List<uint> resultLocalIDs = new List<uint>();
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


            bool firstCall = true;
            int MAX_OBJECTS_PER_PACKET = 251;
            ForceObjectSelectPacket pack = (ForceObjectSelectPacket) PacketPool.Instance.GetPacket(PacketType.ForceObjectSelect);
            // TODO: don't create new blocks if recycling an old packet
            ForceObjectSelectPacket.DataBlock[] data;
            while (resultLocalIDs.Count > 0)
            {
                if (firstCall)
                {
                    pack._Header.ResetList = true;
                    firstCall = false;
                }
                else
                {
                    pack._Header.ResetList = false;
                }

                if (resultLocalIDs.Count > MAX_OBJECTS_PER_PACKET)
                {
                    data = new ForceObjectSelectPacket.DataBlock[MAX_OBJECTS_PER_PACKET];
                }
                else
                {
                    data = new ForceObjectSelectPacket.DataBlock[resultLocalIDs.Count];
                }

                int i;
                for (i = 0; i < MAX_OBJECTS_PER_PACKET && resultLocalIDs.Count > 0; i++)
                {
                    data[i] = new ForceObjectSelectPacket.DataBlock();
                    data[i].LocalID = Convert.ToUInt32(resultLocalIDs[0]);
                    resultLocalIDs.RemoveAt(0);
                }
                pack.Data = data;
                remote_client.OutPacket((Packet) pack, ThrottleOutPacketType.Task);
            }
        }

        public void sendLandObjectOwners(IClientAPI remote_client)
        {
            Dictionary<LLUUID, int> ownersAndCount = new Dictionary<LLUUID, int>();
            ParcelObjectOwnersReplyPacket pack = (ParcelObjectOwnersReplyPacket) PacketPool.Instance.GetPacket(PacketType.ParcelObjectOwnersReply);
            // TODO: don't create new blocks if recycling an old packet

            foreach (SceneObjectGroup obj in primsOverMe)
            {
                try
                {
                    if (!ownersAndCount.ContainsKey(obj.OwnerID))
                    {
                        ownersAndCount.Add(obj.OwnerID, 0);
                    }
                }
                catch (NullReferenceException)
                {
                    m_log.Info("[LAND]: " + "Got Null Reference when searching land owners from the parcel panel");
                }
                try
                {
                    ownersAndCount[obj.OwnerID] += obj.PrimCount;
                }
                catch (KeyNotFoundException)
                {
                    m_log.Error("[LAND]: Unable to match a prim with it's owner.");
                }
            }
            if (ownersAndCount.Count > 0)
            {
                ParcelObjectOwnersReplyPacket.DataBlock[] dataBlock = new ParcelObjectOwnersReplyPacket.DataBlock[32];

                if (ownersAndCount.Count < 32)
                {
                    dataBlock = new ParcelObjectOwnersReplyPacket.DataBlock[ownersAndCount.Count];
                }


                int num = 0;
                foreach (LLUUID owner in ownersAndCount.Keys)
                {
                    dataBlock[num] = new ParcelObjectOwnersReplyPacket.DataBlock();
                    dataBlock[num].Count = ownersAndCount[owner];
                    dataBlock[num].IsGroupOwned = false; //TODO: fix me when group support is added
                    dataBlock[num].OnlineStatus = true; //TODO: fix me later
                    dataBlock[num].OwnerID = owner;

                    num++;
                }
                pack.Data = dataBlock;
            }
            remote_client.OutPacket(pack, ThrottleOutPacketType.Task);
        }

        public Dictionary<LLUUID, int> getLandObjectOwners()
        {
            Dictionary<LLUUID, int> ownersAndCount = new Dictionary<LLUUID, int>();
            foreach (SceneObjectGroup obj in primsOverMe)
            {
                if (!ownersAndCount.ContainsKey(obj.OwnerID))
                {
                    ownersAndCount.Add(obj.OwnerID, 0);
                }
                ownersAndCount[obj.OwnerID] += obj.PrimCount;
            }
            return ownersAndCount;
        }

        #endregion

        #region Object Returning

        public void returnObject(SceneObjectGroup obj)
        {
        }

        public void returnLandObjects(int type, LLUUID owner)
        {
        }

        #endregion

        #region Object Adding/Removing from Parcel

        public void resetLandPrimCounts()
        {
            landData.groupPrims = 0;
            landData.ownerPrims = 0;
            landData.otherPrims = 0;
            landData.selectedPrims = 0;
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

            primsOverMe.Add(obj);
        }

        public void removePrimFromCount(SceneObjectGroup obj)
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

        #endregion

        #endregion
    

}

    #endregion
}
