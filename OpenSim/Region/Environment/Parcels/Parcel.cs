using System;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Parcels
{
    #region Parcel Class
    /// <summary>
    /// Keeps track of a specific parcel's information
    /// </summary>
    public class Parcel
    {
        #region Member Variables
        public ParcelData parcelData = new ParcelData();
        public List<SceneObject> primsOverMe = new List<SceneObject>();

        public Scene m_world;

        private bool[,] parcelBitmap = new bool[64, 64];

        #endregion


        #region Constructors
        public Parcel(LLUUID owner_id, bool is_group_owned, Scene world)
        {
            m_world = world;
            parcelData.ownerID = owner_id;
            parcelData.isGroupOwned = is_group_owned;

        }
        #endregion


        #region Member Functions

        #region General Functions
        /// <summary>
        /// Checks to see if this parcel contains a point
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>Returns true if the parcel contains the specified point</returns>
        public bool containsPoint(int x, int y)
        {
            if (x >= 0 && y >= 0 && x <= 256 && x <= 256)
            {
                return (parcelBitmap[x / 4, y / 4] == true);
            }
            else
            {
                return false;
            }
        }

        public Parcel Copy()
        {
            Parcel newParcel = new Parcel(this.parcelData.ownerID, this.parcelData.isGroupOwned, m_world);

            //Place all new variables here!
            newParcel.parcelBitmap = (bool[,])(this.parcelBitmap.Clone());
            newParcel.parcelData = parcelData.Copy();

            return newParcel;
        }

        #endregion


        #region Packet Request Handling
        /// <summary>
        /// Sends parcel properties as requested
        /// </summary>
        /// <param name="sequence_id">ID sent by client for them to keep track of</param>
        /// <param name="snap_selection">Bool sent by client for them to use</param>
        /// <param name="remote_client">Object representing the client</param>
        public void sendParcelProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
        {

            ParcelPropertiesPacket updatePacket = new ParcelPropertiesPacket();
            updatePacket.ParcelData.AABBMax = parcelData.AABBMax;
            updatePacket.ParcelData.AABBMin = parcelData.AABBMin;
            updatePacket.ParcelData.Area = parcelData.area;
            updatePacket.ParcelData.AuctionID = parcelData.auctionID;
            updatePacket.ParcelData.AuthBuyerID = parcelData.authBuyerID; //unemplemented

            updatePacket.ParcelData.Bitmap = parcelData.parcelBitmapByteArray;

            updatePacket.ParcelData.Desc = Helpers.StringToField(parcelData.parcelDesc);
            updatePacket.ParcelData.Category = (byte)parcelData.category;
            updatePacket.ParcelData.ClaimDate = parcelData.claimDate;
            updatePacket.ParcelData.ClaimPrice = parcelData.claimPrice;
            updatePacket.ParcelData.GroupID = parcelData.groupID;
            updatePacket.ParcelData.GroupPrims = parcelData.groupPrims;
            updatePacket.ParcelData.IsGroupOwned = parcelData.isGroupOwned;
            updatePacket.ParcelData.LandingType = (byte)parcelData.landingType;
            updatePacket.ParcelData.LocalID = parcelData.localID;
            if (parcelData.area > 0)
            {
                updatePacket.ParcelData.MaxPrims = Convert.ToInt32(Math.Round((Convert.ToDecimal(parcelData.area) / Convert.ToDecimal(65536)) * 15000 * Convert.ToDecimal(m_world.RegionInfo.estateSettings.objectBonusFactor)));
            }
            else
            {
                updatePacket.ParcelData.MaxPrims = 0;
            }
            updatePacket.ParcelData.MediaAutoScale = parcelData.mediaAutoScale;
            updatePacket.ParcelData.MediaID = parcelData.mediaID;
            updatePacket.ParcelData.MediaURL = Helpers.StringToField(parcelData.mediaURL);
            updatePacket.ParcelData.MusicURL = Helpers.StringToField(parcelData.musicURL);
            updatePacket.ParcelData.Name = Helpers.StringToField(parcelData.parcelName);
            updatePacket.ParcelData.OtherCleanTime = 0; //unemplemented
            updatePacket.ParcelData.OtherCount = 0; //unemplemented
            updatePacket.ParcelData.OtherPrims = parcelData.otherPrims;
            updatePacket.ParcelData.OwnerID = parcelData.ownerID;
            updatePacket.ParcelData.OwnerPrims = parcelData.ownerPrims;
            updatePacket.ParcelData.ParcelFlags = parcelData.parcelFlags;
            updatePacket.ParcelData.ParcelPrimBonus = m_world.RegionInfo.estateSettings.objectBonusFactor;
            updatePacket.ParcelData.PassHours = parcelData.passHours;
            updatePacket.ParcelData.PassPrice = parcelData.passPrice;
            updatePacket.ParcelData.PublicCount = 0; //unemplemented
            updatePacket.ParcelData.RegionDenyAnonymous = (((uint)m_world.RegionInfo.estateSettings.regionFlags & (uint)Simulator.RegionFlags.DenyAnonymous) > 0);
            updatePacket.ParcelData.RegionDenyIdentified = (((uint)m_world.RegionInfo.estateSettings.regionFlags & (uint)Simulator.RegionFlags.DenyIdentified) > 0);
            updatePacket.ParcelData.RegionDenyTransacted = (((uint)m_world.RegionInfo.estateSettings.regionFlags & (uint)Simulator.RegionFlags.DenyTransacted) > 0);
            updatePacket.ParcelData.RegionPushOverride = (((uint)m_world.RegionInfo.estateSettings.regionFlags & (uint)Simulator.RegionFlags.RestrictPushObject) > 0);
            updatePacket.ParcelData.RentPrice = 0;
            updatePacket.ParcelData.RequestResult = request_result;
            updatePacket.ParcelData.SalePrice = parcelData.salePrice;
            updatePacket.ParcelData.SelectedPrims = parcelData.selectedPrims;
            updatePacket.ParcelData.SelfCount = 0;//unemplemented
            updatePacket.ParcelData.SequenceID = sequence_id;
            if (parcelData.simwideArea > 0)
            {
                updatePacket.ParcelData.SimWideMaxPrims = Convert.ToInt32(Math.Round((Convert.ToDecimal(parcelData.simwideArea) / Convert.ToDecimal(65536)) * 15000 * Convert.ToDecimal(m_world.RegionInfo.estateSettings.objectBonusFactor)));
            }
            else
            {
                updatePacket.ParcelData.SimWideMaxPrims = 0;
            }
            updatePacket.ParcelData.SimWideTotalPrims = parcelData.simwidePrims;
            updatePacket.ParcelData.SnapSelection = snap_selection;
            updatePacket.ParcelData.SnapshotID = parcelData.snapshotID;
            updatePacket.ParcelData.Status = (byte)parcelData.parcelStatus;
            updatePacket.ParcelData.TotalPrims = parcelData.ownerPrims + parcelData.groupPrims + parcelData.otherPrims + parcelData.selectedPrims;
            updatePacket.ParcelData.UserLocation = parcelData.userLocation;
            updatePacket.ParcelData.UserLookAt = parcelData.userLookAt;
            remote_client.OutPacket((Packet)updatePacket);
        }

        public void updateParcelProperties(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client)
        {
            if (remote_client.AgentId == parcelData.ownerID)
            {
                //Needs later group support
                parcelData.authBuyerID = packet.ParcelData.AuthBuyerID;
                parcelData.category = (libsecondlife.Parcel.ParcelCategory)packet.ParcelData.Category;
                parcelData.parcelDesc = Helpers.FieldToUTF8String(packet.ParcelData.Desc);
                parcelData.groupID = packet.ParcelData.GroupID;
                parcelData.landingType = packet.ParcelData.LandingType;
                parcelData.mediaAutoScale = packet.ParcelData.MediaAutoScale;
                parcelData.mediaID = packet.ParcelData.MediaID;
                parcelData.mediaURL = Helpers.FieldToUTF8String(packet.ParcelData.MediaURL);
                parcelData.musicURL = Helpers.FieldToUTF8String(packet.ParcelData.MusicURL);
                parcelData.parcelName = Helpers.FieldToUTF8String(packet.ParcelData.Name);
                parcelData.parcelFlags = packet.ParcelData.ParcelFlags;
                parcelData.passHours = packet.ParcelData.PassHours;
                parcelData.passPrice = packet.ParcelData.PassPrice;
                parcelData.salePrice = packet.ParcelData.SalePrice;
                parcelData.snapshotID = packet.ParcelData.SnapshotID;
                parcelData.userLocation = packet.ParcelData.UserLocation;
                parcelData.userLookAt = packet.ParcelData.UserLookAt;
                sendParcelUpdateToAvatarsOverMe();


            }
        }

        public void sendParcelUpdateToAvatarsOverMe()
        {
            List<ScenePresence> avatars = m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                Parcel over = m_world.LandManager.getParcel((int)Math.Round(avatars[i].Pos.X), (int)Math.Round(avatars[i].Pos.Y));
                if (over.parcelData.localID == this.parcelData.localID)
                {
                    sendParcelProperties(0, false, 0, avatars[i].ControllingClient);
                }
            }
        }
        #endregion


        #region Update Functions
        /// <summary>
        /// Updates the AABBMin and AABBMax values after area/shape modification of parcel
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
                    if (parcelBitmap[x, y] == true)
                    {
                        if (min_x > x) min_x = x;
                        if (min_y > y) min_y = y;
                        if (max_x < x) max_x = x;
                        if (max_y < y) max_y = y;
                        tempArea += 16; //16sqm parcel
                    }
                }
            }
            parcelData.AABBMin = new LLVector3((float)(min_x * 4), (float)(min_y * 4), (float)m_world.Terrain.get((min_x * 4), (min_y * 4)));
            parcelData.AABBMax = new LLVector3((float)(max_x * 4), (float)(max_y * 4), (float)m_world.Terrain.get((max_x * 4), (max_y * 4)));
            parcelData.area = tempArea;
        }

        public void updateParcelBitmapByteArray()
        {
            parcelData.parcelBitmapByteArray = convertParcelBitmapToBytes();
        }

        /// <summary>
        /// Update all settings in parcel such as area, bitmap byte array, etc
        /// </summary>
        public void forceUpdateParcelInfo()
        {
            this.updateAABBAndAreaValues();
            this.updateParcelBitmapByteArray();
        }

        public void setParcelBitmapFromByteArray()
        {
            parcelBitmap = convertBytesToParcelBitmap();
        }
        #endregion


        #region Parcel Bitmap Functions
        /// <summary>
        /// Sets the parcel's bitmap manually
        /// </summary>
        /// <param name="bitmap">64x64 block representing where this parcel is on a map</param>
        public void setParcelBitmap(bool[,] bitmap)
        {
            if (bitmap.GetLength(0) != 64 || bitmap.GetLength(1) != 64 || bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap");
            }
            else
            {
                //Valid: Lets set it
                parcelBitmap = bitmap;
                forceUpdateParcelInfo();

            }
        }
        /// <summary>
        /// Gets the parcels bitmap manually
        /// </summary>
        /// <returns></returns>
        public bool[,] getParcelBitmap()
        {
            return parcelBitmap;
        }
        /// <summary>
        /// Converts the parcel bitmap to a packet friendly byte array
        /// </summary>
        /// <returns></returns>
        private byte[] convertParcelBitmapToBytes()
        {
            byte[] tempConvertArr = new byte[512];
            byte tempByte = 0;
            int x, y, i, byteNum = 0;
            i = 0;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(parcelBitmap[x, y]) << (i++ % 8));
                    if (i % 8 == 0)
                    {
                        tempConvertArr[byteNum] = tempByte;
                        tempByte = (byte)0;
                        i = 0;
                        byteNum++;
                    }
                }
            }
            return tempConvertArr;
        }

        private bool[,] convertBytesToParcelBitmap()
        {
            bool[,] tempConvertMap = new bool[64, 64];
            tempConvertMap.Initialize();
            byte tempByte = 0;
            int x = 0, y = 0, i = 0, bitNum = 0;
            for (i = 0; i < 512; i++)
            {
                tempByte = parcelData.parcelBitmapByteArray[i];
                for (bitNum = 0; bitNum < 8; bitNum++)
                {
                    bool bit = Convert.ToBoolean(Convert.ToByte(tempByte >> bitNum) & (byte)1);
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
        /// Full sim parcel creation
        /// </summary>
        /// <returns></returns>
        public static bool[,] basicFullRegionParcelBitmap()
        {
            return getSquareParcelBitmap(0, 0, 256, 256);
        }

        /// <summary>
        /// Used to modify the bitmap between the x and y points. Points use 64 scale
        /// </summary>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <returns></returns>
        public static bool[,] getSquareParcelBitmap(int start_x, int start_y, int end_x, int end_y)
        {

            bool[,] tempBitmap = new bool[64, 64];
            tempBitmap.Initialize();

            tempBitmap = modifyParcelBitmapSquare(tempBitmap, start_x, start_y, end_x, end_y, true);
            return tempBitmap;
        }

        /// <summary>
        /// Change a parcel's bitmap at within a square and set those points to a specific value
        /// </summary>
        /// <param name="parcel_bitmap"></param>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <param name="set_value"></param>
        /// <returns></returns>
        public static bool[,] modifyParcelBitmapSquare(bool[,] parcel_bitmap, int start_x, int start_y, int end_x, int end_y, bool set_value)
        {
            if (parcel_bitmap.GetLength(0) != 64 || parcel_bitmap.GetLength(1) != 64 || parcel_bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap in modifyParcelBitmapSquare()");
            }

            int x, y;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    if (x >= start_x / 4 && x < end_x / 4
                        && y >= start_y / 4 && y < end_y / 4)
                    {
                        parcel_bitmap[x, y] = set_value;
                    }
                }
            }
            return parcel_bitmap;
        }
        /// <summary>
        /// Join the true values of 2 bitmaps together
        /// </summary>
        /// <param name="bitmap_base"></param>
        /// <param name="bitmap_add"></param>
        /// <returns></returns>
        public static bool[,] mergeParcelBitmaps(bool[,] bitmap_base, bool[,] bitmap_add)
        {
            if (bitmap_base.GetLength(0) != 64 || bitmap_base.GetLength(1) != 64 || bitmap_base.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap - Bitmap_base in mergeParcelBitmaps");
            }
            if (bitmap_add.GetLength(0) != 64 || bitmap_add.GetLength(1) != 64 || bitmap_add.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap - Bitmap_add in mergeParcelBitmaps");

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
            foreach (SceneObject obj in primsOverMe)
            {
                if (obj.rootLocalID > 0)
                {
                    if (request_type == LandManager.PARCEL_SELECT_OBJECTS_OWNER && obj.rootPrimitive.OwnerID == this.parcelData.ownerID)
                    {
                        resultLocalIDs.Add(obj.rootLocalID);
                    }
                    else if (request_type == LandManager.PARCEL_SELECT_OBJECTS_GROUP && false) //TODO: change false to group support!
                    {

                    }
                    else if (request_type == LandManager.PARCEL_SELECT_OBJECTS_OTHER && obj.rootPrimitive.OwnerID != remote_client.AgentId)
                    {
                        resultLocalIDs.Add(obj.rootLocalID);
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
                remote_client.OutPacket((Packet)pack);
            }

        }
        public void sendParcelObjectOwners(IClientAPI remote_client)
        {
            Dictionary<LLUUID, int> ownersAndCount = new Dictionary<LLUUID, int>();
            foreach (SceneObject obj in primsOverMe)
            {
                if (!ownersAndCount.ContainsKey(obj.rootPrimitive.OwnerID))
                {
                    ownersAndCount.Add(obj.rootPrimitive.OwnerID, 0);
                }
                ownersAndCount[obj.rootPrimitive.OwnerID] += obj.primCount;
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
        public void returnObject(SceneObject obj)
        {
        }
        public void returnParcelObjects(int type, LLUUID owner)
        {

        }
        #endregion

        #region Object Adding/Removing from Parcel
        public void resetParcelPrimCounts()
        {
            parcelData.groupPrims = 0;
            parcelData.ownerPrims = 0;
            parcelData.otherPrims = 0;
            parcelData.selectedPrims = 0;
            primsOverMe.Clear();
        }

        public void addPrimToCount(SceneObject obj)
        {
            LLUUID prim_owner = obj.rootPrimitive.OwnerID;
            int prim_count = obj.primCount;

            if (obj.isSelected)
            {
                parcelData.selectedPrims += prim_count;
            }
            else
            {
                if (prim_owner == parcelData.ownerID)
                {
                    parcelData.ownerPrims += prim_count;
                }
                else
                {
                    parcelData.otherPrims += prim_count;
                }
            }

            primsOverMe.Add(obj);

        }

        public void removePrimFromCount(SceneObject obj)
        {
            if (primsOverMe.Contains(obj))
            {
                LLUUID prim_owner = obj.rootPrimitive.OwnerID;
                int prim_count = obj.primCount;

                if (prim_owner == parcelData.ownerID)
                {
                    parcelData.ownerPrims -= prim_count;
                }
                else if (prim_owner == parcelData.groupID)
                {
                    parcelData.groupPrims -= prim_count;
                }
                else
                {
                    parcelData.otherPrims -= prim_count;
                }

                primsOverMe.Remove(obj);
            }
        }
        #endregion

        #endregion


    }
    #endregion
}
