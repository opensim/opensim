using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.world;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.RegionServer.world
{
    public delegate void ParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, ClientView remote_client);


    #region ParcelManager Class
    /// <summary>
    /// Handles Parcel objects and operations requiring information from other Parcel objects (divide, join, etc)
    /// </summary>
    public class ParcelManager : OpenSim.Framework.Interfaces.ILocalStorageParcelReceiver
    {

        #region Constants
        //Parcel types set with flags in ParcelOverlay.
        //Only one of these can be used. 
        public static byte PARCEL_TYPE_PUBLIC = (byte)0;      //Equals 00000000
        public static byte PARCEL_TYPE_OWNED_BY_OTHER = (byte)1;      //Equals 00000001
        public static byte PARCEL_TYPE_OWNED_BY_GROUP = (byte)2;      //Equals 00000010
        public static byte PARCEL_TYPE_OWNED_BY_REQUESTER = (byte)3;      //Equals 00000011
        public static byte PARCEL_TYPE_IS_FOR_SALE = (byte)4;      //Equals 00000100
        public static byte PARCEL_TYPE_IS_BEING_AUCTIONED = (byte)5;      //Equals 00000101


        //Flags that when set, a border on the given side will be placed
        //NOTE: North and East is assumable by the west and south sides (if parcel to east has a west border, then I have an east border; etc)
        //This took forever to figure out -- jeesh. /blame LL for even having to send these
        public static byte PARCEL_FLAG_PROPERTY_BORDER_WEST = (byte)64;     //Equals 01000000
        public static byte PARCEL_FLAG_PROPERTY_BORDER_SOUTH = (byte)128;    //Equals 10000000

        #endregion

        #region Member Variables
        public Dictionary<int, Parcel> parcelList = new Dictionary<int, Parcel>();
        private int lastParcelLocalID = -1;
        private int[,] parcelIDList = new int[64, 64];

        private static World m_world;
        #endregion

        #region Constructors
        public ParcelManager(World world)
        {

            m_world = world;
            Console.WriteLine("Created ParcelManager Object");


        }
        #endregion

        #region Member Functions

        #region Parcel From Storage Functions
        public void ParcelFromStorage(ParcelData data)
        {
            Parcel new_parcel = new Parcel(data.ownerID, data.isGroupOwned, m_world);
            new_parcel.parcelData = data;
            new_parcel.setParcelBitmapFromByteArray();

            this.addParcel(new_parcel);
        }

        public void NoParcelDataFromStorage()
        {
            this.resetSimParcels();
        }
        #endregion

        #region Parcel Add/Remove/Get/Create
        /// <summary>
        /// Creates a basic Parcel object without an owner (a zeroed key)
        /// </summary>
        /// <returns></returns>
        public Parcel createBaseParcel()
        {
            return new Parcel(new LLUUID(), false, m_world);
        }

        /// <summary>
        /// Adds a parcel to the stored list and adds them to the parcelIDList to what they own
        /// </summary>
        /// <param name="new_parcel">The parcel being added</param>
        public void addParcel(Parcel new_parcel)
        {
            lastParcelLocalID++;
            new_parcel.parcelData.localID = lastParcelLocalID;
            parcelList.Add(lastParcelLocalID, new_parcel);


            bool[,] parcelBitmap = new_parcel.getParcelBitmap();
            int x, y;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (parcelBitmap[x, y])
                    {
                        parcelIDList[x, y] = lastParcelLocalID;
                    }
                }
            }
        }
        /// <summary>
        /// Removes a parcel from the list. Will not remove if local_id is still owning an area in parcelIDList
        /// </summary>
        /// <param name="local_id">Parcel.localID of the parcel to remove.</param>
        public void removeParcel(int local_id)
        {
            int x, y;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (parcelIDList[x, y] == local_id)
                    {
                        throw new Exception("Could not remove parcel. Still being used at " + x + ", " + y);
                    }
                }
            }
            parcelList.Remove(local_id);
        }
        /// <summary>
        /// Get the parcel at the specified point
        /// </summary>
        /// <param name="x">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Parcel at the point supplied</returns>
        public Parcel getParcel(int x, int y)
        {
            if (x > 256 || y > 256 || x < 0 || y < 0)
            {
                throw new Exception("Error: Parcel not found at point " + x + ", " + y);
            }
            else
            {
                return parcelList[parcelIDList[x / 4, y / 4]];
            }

        }
        #endregion

        #region Parcel Modification
        /// <summary>
        /// Subdivides a parcel
        /// </summary>
        /// <param name="start_x">West Point</param>
        /// <param name="start_y">South Point</param>
        /// <param name="end_x">East Point</param>
        /// <param name="end_y">North Point</param>
        /// <param name="attempting_user_id">LLUUID of user who is trying to subdivide</param>
        /// <returns>Returns true if successful</returns>
        public bool subdivide(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
        {
            //First, lets loop through the points and make sure they are all in the same parcel
            //Get the parcel at start
            Parcel startParcel = getParcel(start_x, start_y);
            if (startParcel == null) return false; //No such parcel at the beginning

            //Loop through the points
            try
            {
                int totalX = end_x - start_x;
                int totalY = end_y - start_y;
                int x, y;
                for (x = 0; x < totalX; x++)
                {
                    for (y = 0; y < totalY; y++)
                    {
                        Parcel tempParcel = getParcel(start_x + x, start_y + y);
                        if (tempParcel == null) return false; //No such parcel at that point
                        if (tempParcel != startParcel) return false; //Subdividing over 2 parcels; no-no
                    }
                }
            }
            catch (Exception e)
            {
                return false; //Exception. For now, lets skip subdivision
            }

            //If we are still here, then they are subdividing within one parcel
            //Check owner
            if (startParcel.parcelData.ownerID != attempting_user_id)
            {
                return false; //They cant do this!
            }

            //Lets create a new parcel with bitmap activated at that point (keeping the old parcels info)
            Parcel newParcel = startParcel;
            newParcel.setParcelBitmap(Parcel.getSquareParcelBitmap(start_x, start_y, end_x, end_y));

            //Now, lets set the subdivision area of the original to false
            int startParcelIndex = startParcel.parcelData.localID;
            parcelList[startParcelIndex].setParcelBitmap(Parcel.modifyParcelBitmapSquare(parcelList[startParcelIndex].getParcelBitmap(), start_x, start_y, end_x, end_y, false));

            //Now add the new parcel
            addParcel(newParcel);

            return true;
        }
        /// <summary>
        /// Join 2 parcels together
        /// </summary>
        /// <param name="start_x">x value in first parcel</param>
        /// <param name="start_y">y value in first parcel</param>
        /// <param name="end_x">x value in second parcel</param>
        /// <param name="end_y">y value in second parcel</param>
        /// <param name="attempting_user_id">LLUUID of the avatar trying to join the parcels</param>
        /// <returns>Returns true if successful</returns>
        public bool join(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
        {
            //NOTE: The following only connects the parcels in each corner and not all the parcels that are within the selection box!
            //This should be fixed later -- somewhat "incomplete code" --Ming
            Parcel startParcel, endParcel;
            try
            {
                startParcel = getParcel(start_x, start_y);
                endParcel = getParcel(end_x, end_y);
            }
            catch (Exception e)
            {
                return false; //Error occured when trying to get the start and end parcels
            }
            //Check the parcel owners:
            if (startParcel.parcelData.ownerID != endParcel.parcelData.ownerID)
            {
                return false;
            }
            if (startParcel.parcelData.ownerID != attempting_user_id)
            {
                //TODO: Group editing stuff. Avatar owner support for now
                return false;
            }

            //Same owners! Lets join them
            //Merge them to startParcel
            parcelList[startParcel.parcelData.localID].setParcelBitmap(Parcel.mergeParcelBitmaps(startParcel.getParcelBitmap(), endParcel.getParcelBitmap()));

            //Remove the old parcel
            parcelList.Remove(endParcel.parcelData.localID);

            return true;



        }
        #endregion

        #region Parcel Updating
        /// <summary>
        /// Where we send the ParcelOverlay packet to the client
        /// </summary>
        /// <param name="remote_client">The object representing the client</param>
        public void sendParcelOverlay(ClientView remote_client)
        {
            const int PARCEL_BLOCKS_PER_PACKET = 1024;
            int x, y = 0;
            byte[] byteArray = new byte[PARCEL_BLOCKS_PER_PACKET];
            int byteArrayCount = 0;
            int sequenceID = 0;
            ParcelOverlayPacket packet;

            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    byte tempByte = (byte)0; //This represents the byte for the current 4x4
                    Parcel currentParcelBlock = getParcel(x * 4, y * 4);

                    if (currentParcelBlock.parcelData.ownerID == remote_client.AgentID)
                    {
                        //Owner Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_OWNED_BY_REQUESTER);
                    }
                    else if (currentParcelBlock.parcelData.salePrice > 0 && (currentParcelBlock.parcelData.authBuyerID == LLUUID.Zero || currentParcelBlock.parcelData.authBuyerID == remote_client.AgentID))
                    {
                        //Sale Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_IS_FOR_SALE);
                    }
                    else if (currentParcelBlock.parcelData.ownerID == LLUUID.Zero)
                    {
                        //Public Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_PUBLIC);
                    }
                    else
                    {
                        //Other Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_OWNED_BY_OTHER);
                    }


                    //Now for border control
                    if (x == 0)
                    {
                        tempByte = Convert.ToByte(tempByte | PARCEL_FLAG_PROPERTY_BORDER_WEST);
                    }
                    else if (getParcel(x - 1, y) != currentParcelBlock)
                    {
                        tempByte = Convert.ToByte(tempByte | PARCEL_FLAG_PROPERTY_BORDER_WEST);
                    }

                    if (y == 0)
                    {
                        tempByte = Convert.ToByte(tempByte | PARCEL_FLAG_PROPERTY_BORDER_SOUTH);
                    }
                    else if (getParcel(x, y - 1) != currentParcelBlock)
                    {
                        tempByte = Convert.ToByte(tempByte | PARCEL_FLAG_PROPERTY_BORDER_SOUTH);
                    }

                    byteArray[byteArrayCount] = tempByte;
                    byteArrayCount++;
                    if (byteArrayCount >= PARCEL_BLOCKS_PER_PACKET)
                    {
                        byteArrayCount = 0;
                        packet = new ParcelOverlayPacket();
                        packet.ParcelData.Data = byteArray;
                        packet.ParcelData.SequenceID = sequenceID;
                        remote_client.OutPacket((Packet)packet);
                        sequenceID++;
                        byteArray = new byte[PARCEL_BLOCKS_PER_PACKET];
                    }
                }
            }

            packet = new ParcelOverlayPacket();
            packet.ParcelData.Data = byteArray;
            packet.ParcelData.SequenceID = sequenceID; //Eh?
            remote_client.OutPacket((Packet)packet);
        }
        #endregion

        /// <summary>
        /// Resets the sim to the default parcel (full sim parcel owned by the default user)
        /// </summary>
        public void resetSimParcels()
        {
            //Remove all the parcels in the sim and add a blank, full sim parcel set to public
            parcelList.Clear();
            parcelIDList.Initialize();
            Parcel fullSimParcel = new Parcel(LLUUID.Zero, false, m_world);
            fullSimParcel.setParcelBitmap(Parcel.basicFullRegionParcelBitmap());
            fullSimParcel.parcelData.parcelName = "Your Sim Parcel";
            fullSimParcel.parcelData.parcelDesc = "";

            fullSimParcel.parcelData.ownerID = m_world.m_regInfo.MasterAvatarAssignedUUID;
            fullSimParcel.parcelData.salePrice = 1;
            fullSimParcel.parcelData.parcelFlags = libsecondlife.Parcel.ParcelFlags.ForSale;
            fullSimParcel.parcelData.parcelStatus = libsecondlife.Parcel.ParcelStatus.Leased;
            addParcel(fullSimParcel);


        }
        #endregion
    }
    #endregion


    #region Parcel Class
    /// <summary>
    /// Keeps track of a specific parcel's information
    /// </summary>
    public class Parcel
    {
        #region Member Variables
        public ParcelData parcelData = new ParcelData();
        public World m_world;

        private bool[,] parcelBitmap = new bool[64, 64];

        #endregion


        #region Constructors
        public Parcel(LLUUID owner_id, bool is_group_owned, World world)
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
                return (this.parcelBitmap[x / 4, y / 4] == true);
            }
            else
            {
                return false;
            }
        }
        #endregion


        #region Packet Request Handling
        /// <summary>
        /// Sends parcel properties as requested
        /// </summary>
        /// <param name="sequence_id">ID sent by client for them to keep track of</param>
        /// <param name="snap_selection">Bool sent by client for them to use</param>
        /// <param name="remote_client">Object representing the client</param>
        public void sendParcelProperties(int sequence_id, bool snap_selection, ClientView remote_client)
        {

            ParcelPropertiesPacket updatePacket = new ParcelPropertiesPacket();
            updatePacket.ParcelData.AABBMax = parcelData.AABBMax;
            updatePacket.ParcelData.AABBMin = parcelData.AABBMin;
            updatePacket.ParcelData.Area = this.parcelData.area;
            updatePacket.ParcelData.AuctionID = this.parcelData.auctionID;
            updatePacket.ParcelData.AuthBuyerID = this.parcelData.authBuyerID; //unemplemented

            updatePacket.ParcelData.Bitmap = this.convertParcelBitmapToBytes();

            updatePacket.ParcelData.Desc = libsecondlife.Helpers.StringToField(this.parcelData.parcelDesc);
            updatePacket.ParcelData.Category = (byte)this.parcelData.category;
            updatePacket.ParcelData.ClaimDate = this.parcelData.claimDate;
            updatePacket.ParcelData.ClaimPrice = this.parcelData.claimPrice;
            updatePacket.ParcelData.GroupID = this.parcelData.groupID;
            updatePacket.ParcelData.GroupPrims = this.parcelData.groupPrims;
            updatePacket.ParcelData.IsGroupOwned = this.parcelData.isGroupOwned;
            updatePacket.ParcelData.LandingType = (byte)0; //unemplemented
            updatePacket.ParcelData.LocalID = (byte)this.parcelData.localID;
            updatePacket.ParcelData.MaxPrims = 1000; //unemplemented
            updatePacket.ParcelData.MediaAutoScale = (byte)0; //unemplemented
            updatePacket.ParcelData.MediaID = LLUUID.Zero; //unemplemented
            updatePacket.ParcelData.MediaURL = Helpers.StringToField(""); //unemplemented
            updatePacket.ParcelData.MusicURL = Helpers.StringToField(""); //unemplemented
            updatePacket.ParcelData.Name = Helpers.StringToField(this.parcelData.parcelName);
            updatePacket.ParcelData.OtherCleanTime = 0; //unemplemented
            updatePacket.ParcelData.OtherCount = 0; //unemplemented
            updatePacket.ParcelData.OtherPrims = 0; //unemplented
            updatePacket.ParcelData.OwnerID = this.parcelData.ownerID;
            updatePacket.ParcelData.OwnerPrims = 0; //unemplemented
            updatePacket.ParcelData.ParcelFlags = (uint)this.parcelData.parcelFlags; //unemplemented
            updatePacket.ParcelData.ParcelPrimBonus = (float)1.0; //unemplemented
            updatePacket.ParcelData.PassHours = (float)0.0; //unemplemented
            updatePacket.ParcelData.PassPrice = 0; //unemeplemented
            updatePacket.ParcelData.PublicCount = 0; //unemplemented
            updatePacket.ParcelData.RegionDenyAnonymous = false; //unemplemented
            updatePacket.ParcelData.RegionDenyIdentified = false; //unemplemented
            updatePacket.ParcelData.RegionDenyTransacted = false; //unemplemented
            updatePacket.ParcelData.RegionPushOverride = true; //unemplemented
            updatePacket.ParcelData.RentPrice = 0; //??
            updatePacket.ParcelData.RequestResult = 0;//??
            updatePacket.ParcelData.SalePrice = this.parcelData.salePrice; //unemplemented
            updatePacket.ParcelData.SelectedPrims = 0; //unemeplemented
            updatePacket.ParcelData.SelfCount = 0;//unemplemented
            updatePacket.ParcelData.SequenceID = sequence_id;
            updatePacket.ParcelData.SimWideMaxPrims = 15000; //unemplemented
            updatePacket.ParcelData.SimWideTotalPrims = 0; //unemplemented
            updatePacket.ParcelData.SnapSelection = snap_selection; //Bleh - not important yet
            updatePacket.ParcelData.SnapshotID = LLUUID.Zero; //Unemplemented
            updatePacket.ParcelData.Status = (byte)this.parcelData.parcelStatus; //??
            updatePacket.ParcelData.TotalPrims = 0; //unemplemented
            updatePacket.ParcelData.UserLocation = LLVector3.Zero; //unemplemented
            updatePacket.ParcelData.UserLookAt = LLVector3.Zero; //unemeplemented

            remote_client.OutPacket((Packet)updatePacket);
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
            this.parcelData.AABBMin = new LLVector3((float)(min_x * 4), (float)(min_y * 4), m_world.Terrain[(min_x * 4), (min_y * 4)]);
            this.parcelData.AABBMax = new LLVector3((float)(max_x * 4), (float)(max_y * 4), m_world.Terrain[(max_x * 4), (max_y * 4)]);
            this.parcelData.area = tempArea;
        }

        public void updateParcelBitmapByteArray()
        {
            parcelData.parcelBitmapByteArray = convertParcelBitmapToBytes();
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
                this.parcelBitmap = bitmap;
                updateAABBAndAreaValues();
                updateParcelBitmapByteArray();
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
            byte[] tempConvertArr = new byte[64 * 64 / 8];
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

            byte tempByte = 0;
            int x = 0, y = 0, i = 0, bitNum = 0;
            for(i = 0; i < 512; i++)
            {
                tempByte = parcelData.parcelBitmapByteArray[i];
                for(bitNum = 7; bitNum >= 0; bitNum--)
                {
                    bool bit = Convert.ToBoolean(Convert.ToByte(tempByte >> bitNum) & (byte)1);
                    tempConvertMap[x, y] = bit;
                    if (x >= 64) y++;
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

            tempBitmap = modifyParcelBitmapSquare(tempBitmap, start_x, start_y, end_x, end_x, true);
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
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (x >= start_x / 4 && x <= end_x / 4
                        && y >= start_y / 4 && y <= end_y / 4)
                    {
                        parcel_bitmap[x, y] = true;
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
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
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

        #endregion

       
    }
    #endregion
    
    
}
