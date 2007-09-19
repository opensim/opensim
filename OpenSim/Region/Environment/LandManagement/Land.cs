using System;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.LandManagement
{

    #region Parcel Class

    /// <summary>
    /// Keeps track of a specific piece of land's information
    /// </summary>
    public class Land
    {
        #region Member Variables

        public LandData landData = new LandData();
        public List<SceneObjectGroup> primsOverMe = new List<SceneObjectGroup>();

        public Scene m_scene;

        private bool[,] landBitmap = new bool[64,64];

        #endregion

        #region Constructors

        public Land(LLUUID owner_id, bool is_group_owned, Scene scene)
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
            if (x >= 0 && y >= 0 && x <= 256 && x <= 256)
            {
                return (landBitmap[x/4, y/4] == true);
            }
            else
            {
                return false;
            }
        }

        public Land Copy()
        {
            Land newLand = new Land(landData.ownerID, landData.isGroupOwned, m_scene);

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
            ParcelPropertiesPacket updatePacket = new ParcelPropertiesPacket();
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
                        Math.Round((Convert.ToDecimal(landData.area)/Convert.ToDecimal(65536))*15000*
                                   Convert.ToDecimal(m_scene.RegionInfo.estateSettings.objectBonusFactor)));
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
            updatePacket.ParcelData.ParcelPrimBonus = m_scene.RegionInfo.estateSettings.objectBonusFactor;
            updatePacket.ParcelData.PassHours = landData.passHours;
            updatePacket.ParcelData.PassPrice = landData.passPrice;
            updatePacket.ParcelData.PublicCount = 0; //unemplemented

            uint regionFlags = (uint) m_scene.RegionInfo.estateSettings.regionFlags;
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
                        Math.Round((Convert.ToDecimal(landData.simwideArea)/Convert.ToDecimal(65536))*15000*
                                   Convert.ToDecimal(m_scene.RegionInfo.estateSettings.objectBonusFactor)));
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
            remote_client.OutPacket((Packet) updatePacket);
        }

        public void updateLandProperties(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client)
        {
            if (remote_client.AgentId == landData.ownerID)
            {
                //Needs later group support
                landData.authBuyerID = packet.ParcelData.AuthBuyerID;
                landData.category = (Parcel.ParcelCategory) packet.ParcelData.Category;
                landData.landDesc = Helpers.FieldToUTF8String(packet.ParcelData.Desc);
                landData.groupID = packet.ParcelData.GroupID;
                landData.landingType = packet.ParcelData.LandingType;
                landData.mediaAutoScale = packet.ParcelData.MediaAutoScale;
                landData.mediaID = packet.ParcelData.MediaID;
                landData.mediaURL = Helpers.FieldToUTF8String(packet.ParcelData.MediaURL);
                landData.musicURL = Helpers.FieldToUTF8String(packet.ParcelData.MusicURL);
                landData.landName = Helpers.FieldToUTF8String(packet.ParcelData.Name);
                landData.landFlags = packet.ParcelData.ParcelFlags;
                landData.passHours = packet.ParcelData.PassHours;
                landData.passPrice = packet.ParcelData.PassPrice;
                landData.salePrice = packet.ParcelData.SalePrice;
                landData.snapshotID = packet.ParcelData.SnapshotID;
                landData.userLocation = packet.ParcelData.UserLocation;
                landData.userLookAt = packet.ParcelData.UserLookAt;
                sendLandUpdateToAvatarsOverMe();
            }
        }

        public void sendLandUpdateToAvatarsOverMe()
        {
            List<ScenePresence> avatars = m_scene.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                Land over =
                    m_scene.LandManager.getLandObject((int) Math.Round(avatars[i].AbsolutePosition.X),
                                                      (int) Math.Round(avatars[i].AbsolutePosition.Y));
                if (over.landData.localID == landData.localID)
                {
                    sendLandProperties(0, false, 0, avatars[i].ControllingClient);
                }
            }
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
            landData.AABBMin =
                new LLVector3((float) (min_x*4), (float) (min_y*4),
                              (float) m_scene.Terrain.GetHeight((min_x*4), (min_y*4)));
            landData.AABBMax =
                new LLVector3((float) (max_x*4), (float) (max_y*4),
                              (float) m_scene.Terrain.GetHeight((max_x*4), (max_y*4)));
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
                throw new Exception("Error: Invalid Parcel Bitmap");
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
        public static bool[,] basicFullRegionLandBitmap()
        {
            return getSquareLandBitmap(0, 0, 256, 256);
        }

        /// <summary>
        /// Used to modify the bitmap between the x and y points. Points use 64 scale
        /// </summary>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <returns></returns>
        public static bool[,] getSquareLandBitmap(int start_x, int start_y, int end_x, int end_y)
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
        public static bool[,] modifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y,
                                                     bool set_value)
        {
            if (land_bitmap.GetLength(0) != 64 || land_bitmap.GetLength(1) != 64 || land_bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap in modifyLandBitmapSquare()");
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
        public static bool[,] mergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add)
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
                    if (request_type == LandManager.LAND_SELECT_OBJECTS_OWNER && obj.OwnerID == landData.ownerID)
                    {
                        resultLocalIDs.Add(obj.LocalId);
                    }
                    else if (request_type == LandManager.LAND_SELECT_OBJECTS_GROUP && false)
                        //TODO: change false to group support!
                    {
                    }
                    else if (request_type == LandManager.LAND_SELECT_OBJECTS_OTHER &&
                             obj.OwnerID != remote_client.AgentId)
                    {
                        resultLocalIDs.Add(obj.LocalId);
                    }
                }
            }


            bool firstCall = true;
            int MAX_OBJECTS_PER_PACKET = 251;
            ForceObjectSelectPacket pack = new ForceObjectSelectPacket();
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
                remote_client.OutPacket((Packet) pack);
            }
        }

        public void sendLandObjectOwners(IClientAPI remote_client)
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

                ParcelObjectOwnersReplyPacket pack = new ParcelObjectOwnersReplyPacket();
                pack.Data = dataBlock;
                remote_client.OutPacket(pack);
            }
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