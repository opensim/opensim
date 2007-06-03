using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.world
{
    public delegate void ParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, ClientView remote_client);
    
    #region Enums
    public enum ParcelFlags : uint
    {
        /// <summary>No flags set</summary>
        None = 0,
        /// <summary>Allow avatars to fly (a client-side only restriction)</summary>
        AllowFly = 1 << 0,
        /// <summary>Allow foreign scripts to run</summary>
        AllowOtherScripts = 1 << 1,
        /// <summary>This parcel is for sale</summary>
        ForSale = 1 << 2,
        /// <summary>Allow avatars to create a landmark on this parcel</summary>
        AllowLandmark = 1 << 3,
        /// <summary>Allows all avatars to edit the terrain on this parcel</summary>
        AllowTerraform = 1 << 4,
        /// <summary>Avatars have health and can take damage on this parcel.
        /// If set, avatars can be killed and sent home here</summary>
        AllowDamage = 1 << 5,
        /// <summary>Foreign avatars can create objects here</summary>
        CreateObjects = 1 << 6,
        /// <summary>All objects on this parcel can be purchased</summary>
        ForSaleObjects = 1 << 7,
        /// <summary>Access is restricted to a group</summary>
        UseAccessGroup = 1 << 8,
        /// <summary>Access is restricted to a whitelist</summary>
        UseAccessList = 1 << 9,
        /// <summary>Ban blacklist is enabled</summary>
        UseBanList = 1 << 10,
        /// <summary>Unknown</summary>
        UsePassList = 1 << 11,
        /// <summary>List this parcel in the search directory</summary>
        ShowDirectory = 1 << 12,
        /// <summary>Unknown</summary>
        AllowDeedToGroup = 1 << 13,
        /// <summary>Unknown</summary>
        ContributeWithDeed = 1 << 14,
        /// <summary>Restrict sounds originating on this parcel to the 
        /// parcel boundaries</summary>
        SoundLocal = 1 << 15,
        /// <summary>Objects on this parcel are sold when the land is 
        /// purchsaed</summary>
        SellParcelObjects = 1 << 16,
        /// <summary>Allow this parcel to be published on the web</summary>
        AllowPublish = 1 << 17,
        /// <summary>The information for this parcel is mature content</summary>
        MaturePublish = 1 << 18,
        /// <summary>The media URL is an HTML page</summary>
        UrlWebPage = 1 << 19,
        /// <summary>The media URL is a raw HTML string</summary>
        UrlRawHtml = 1 << 20,
        /// <summary>Restrict foreign object pushes</summary>
        RestrictPushObject = 1 << 21,
        /// <summary>Ban all non identified/transacted avatars</summary>
        DenyAnonymous = 1 << 22,
        /// <summary>Ban all identified avatars</summary>
        DenyIdentified = 1 << 23,
        /// <summary>Ban all transacted avatars</summary>
        DenyTransacted = 1 << 24,
        /// <summary>Allow group-owned scripts to run</summary>
        AllowGroupScripts = 1 << 25,
        /// <summary>Allow object creation by group members or group 
        /// objects</summary>
        CreateGroupObjects = 1 << 26,
        /// <summary>Allow all objects to enter this parcel</summary>
        AllowAllObjectEntry = 1 << 27,
        /// <summary>Only allow group and owner objects to enter this parcel</summary>
        AllowGroupObjectEntry = 1 << 28,
    }

    /// <summary>
    /// Parcel ownership status
    /// </summary>
    public enum ParcelStatus : sbyte
    {
        /// <summary>Eh?</summary>
        None = -1,
        /// <summary>Land is owned</summary>
        Leased = 0,
        /// <summary>Land is for sale</summary>
        LeasePending = 1,
        /// <summary>Land is public</summary>
        Abandoned = 2
    }

    public enum ParcelCategory : sbyte
    {
        /// <summary>No assigned category</summary>
        None = 0,
        /// <summary></summary>
        Linden,
        /// <summary></summary>
        Adult,
        /// <summary></summary>
        Arts,
        /// <summary></summary>
        Business,
        /// <summary></summary>
        Educational,
        /// <summary></summary>
        Gaming,
        /// <summary></summary>
        Hangout,
        /// <summary></summary>
        Newcomer,
        /// <summary></summary>
        Park,
        /// <summary></summary>
        Residential,
        /// <summary></summary>
        Shopping,
        /// <summary></summary>
        Stage,
        /// <summary></summary>
        Other,
        /// <summary>Not an actual category, only used for queries</summary>
        Any = -1
    }

    #endregion

    #region ParcelManager Class
    public class ParcelManager
    {
        
        #region Constants
        //Parcel types set with flags in ParcelOverlay.
        //Only one of these can be used. 
        public static byte PARCEL_TYPE_PUBLIC                   = (byte)0;      //Equals 00000000
        public static byte PARCEL_TYPE_OWNED_BY_OTHER           = (byte)1;      //Equals 00000001
        public static byte PARCEL_TYPE_OWNED_BY_GROUP           = (byte)2;      //Equals 00000010
        public static byte PARCEL_TYPE_OWNED_BY_REQUESTER       = (byte)3;      //Equals 00000011
        public static byte PARCEL_TYPE_IS_FOR_SALE              = (byte)4;      //Equals 00000100
        public static byte PARCEL_TYPE_IS_BEING_AUCTIONED       = (byte)5;      //Equals 00000101


        //Flags that when set, a border on the given side will be placed
        //NOTE: North and East is assumable by the west and south sides (if parcel to east has a west border, then I have an east border; etc)
        //This took forever to figure out -- jeesh. /blame LL for even having to send these
        public static byte PARCEL_FLAG_PROPERTY_BORDER_WEST     = (byte)64;     //Equals 01000000
        public static byte PARCEL_FLAG_PROPERTY_BORDER_SOUTH    = (byte)128;    //Equals 10000000

        #endregion

        #region Member Variables
            private List<Parcel> parcelList;
            private static World m_world;
        #endregion

        #region Constructors
        public ParcelManager(World world)
        {
            parcelList = new List<Parcel>();
            m_world = world;
            
            //NOTE: This is temporary until I get to storing the parcels out of memory
            //This should later only be for new simulators
            resetSimParcels();
            Console.WriteLine("Created ParcelManager Object");


        }
        #endregion

        #region Member Functions

        #region Parcel Add/Remove/Get
        public void addParcel(Parcel new_parcel)
        {
            parcelList.Add(new_parcel);
        }
        public void removeParcel(Parcel old_parcel)
        {
            parcelList.Remove(old_parcel);
        }
        public Parcel getParcel(int x, int y)
        {
            int searchParcel;
            for(searchParcel = 0; searchParcel < this.parcelList.Count; searchParcel++)
            {
                if(parcelList[searchParcel].containsPoint(x,y))
                {
                    return this.parcelList[searchParcel];
                }
            }
            throw new Exception("Error: Parcel not found at point " + x + ", " + y);

        }
        #endregion

        #region Parcel Modification
        public bool subdivide(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
        {
            //First, lets loop through the points and make sure they are all in the same parcel
            //Get the parcel at start
            Parcel startParcel = getParcel(start_x, start_y);
            if(startParcel == null) return false; //No such parcel at the beginning
           
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
            if (startParcel.ownerID != attempting_user_id)
            {
                return false; //They cant do this!
            }

            //Lets create a new parcel with bitmap activated at that point (keeping the old parcels info)
            Parcel newParcel = startParcel;
            newParcel.setParcelBitmap(Parcel.getSquareParcelBitmap(start_x, start_y, end_x, end_y));
            
            //Now, lets set the subdivision area of the original to false
            int startParcelIndex = parcelList.IndexOf(startParcel);
            parcelList[startParcelIndex].setParcelBitmap(Parcel.modifyParcelBitmapSquare(parcelList[startParcelIndex].getParcelBitmap(),start_x,start_y,end_x, end_y,false));

            //Now add the new parcel
            addParcel(newParcel);

            return true;
        }

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
            if (startParcel.ownerID != endParcel.ownerID)
            {
                return false;
            }
            if (startParcel.ownerID != attempting_user_id)
            {
                //TODO: Group editing stuff. Avatar owner support for now
                return false;
            }

            //Same owners! Lets join them
            //Merge them to startParcel
            parcelList[parcelList.IndexOf(startParcel)].setParcelBitmap(Parcel.mergeParcelBitmaps(startParcel.getParcelBitmap(),endParcel.getParcelBitmap()));

            //Remove the old parcel
            parcelList.Remove(endParcel);

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

                    if (currentParcelBlock.ownerID == remote_client.AgentID)
                    {
                        //Owner Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_OWNED_BY_REQUESTER);
                    }
                    else if (currentParcelBlock.salePrice > 0 && (currentParcelBlock.authBuyerID == LLUUID.Zero || currentParcelBlock.authBuyerID == remote_client.AgentID))
                    {
                        //Sale Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_IS_FOR_SALE);
                    }
                    else if (currentParcelBlock.ownerID == LLUUID.Zero)
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
        public void resetSimParcels()
        {
            //Remove all the parcels in the sim and add a blank, full sim parcel set to public
            parcelList.Clear();
            Parcel fullSimParcel = new Parcel(LLUUID.Zero, false,m_world);
            fullSimParcel.setParcelBitmap(Parcel.basicFullRegionParcelBitmap());
            fullSimParcel.parcelName = "Your Sim Parcel";
            fullSimParcel.parcelDesc = "";
            LLUUID Agent;
            int AgentRand = OpenSim.Framework.Utilities.Util.RandomClass.Next(1, 9999);
            Agent = new LLUUID("99998888-0100-" + AgentRand.ToString("0000") + "-8ec1-0b1d5cd6aead");

            fullSimParcel.ownerID = Agent;
            fullSimParcel.salePrice = 1;
            fullSimParcel.parcelFlags = ParcelFlags.ForSale;

            addParcel(fullSimParcel);
            
            
        }
        #endregion
    }
    #endregion

    
    #region Parcel Class
    public class Parcel
    {
        #region Member Variables
        private bool[,] parcelBitmap = new bool[64,64];
        public string parcelName = "";
        public string parcelDesc = "";
        public LLUUID ownerID = new LLUUID();
        public bool isGroupOwned = false;
        public LLVector3 AABBMin = new LLVector3();
        public LLVector3 AABBMax = new LLVector3();
        public int area = 0;
        public uint auctionID = 0; //Unemplemented. If set to 0, not being auctioned
        public LLUUID authBuyerID = new LLUUID(); //Unemplemented. Authorized Buyer's UUID
        public ParcelCategory category = new ParcelCategory(); //Unemplemented. Parcel's chosen category
        public int claimDate = 0; //Unemplemented
        public int claimPrice = 0; //Unemplemented
        public LLUUID groupID = new LLUUID(); //Unemplemented
        public int groupPrims = 0; //Unemplemented
        public int salePrice = 0; //Unemeplemented. Parcels price.
        public ParcelStatus parcelStatus = ParcelStatus.None;
        public ParcelFlags parcelFlags = ParcelFlags.None;

        private int localID;
        private static int localIDCount = 0;
        private World m_world;
        #endregion


        #region Constructors
        public Parcel(LLUUID owner_id, bool is_group_owned, World world)
        {
            m_world = world;
            ownerID = owner_id;
            isGroupOwned = is_group_owned;

            localID = localIDCount;
            localIDCount++;

        }
        #endregion


        #region Member Functions

        #region General Functions
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
        public void sendParcelProperties(int sequence_id, bool snap_selection, ClientView remote_client)
        {

            ParcelPropertiesPacket updatePacket = new ParcelPropertiesPacket();
            updatePacket.ParcelData.AABBMax = AABBMax;
            updatePacket.ParcelData.AABBMin = AABBMin;
            updatePacket.ParcelData.Area = this.area;
            updatePacket.ParcelData.AuctionID = this.auctionID; 
            updatePacket.ParcelData.AuthBuyerID = this.authBuyerID; //unemplemented

            updatePacket.ParcelData.Bitmap = this.convertParcelBitmapToBytes();

            updatePacket.ParcelData.Desc = libsecondlife.Helpers.StringToField(this.parcelDesc);
            updatePacket.ParcelData.Category = (byte)this.category;
            updatePacket.ParcelData.ClaimDate = this.claimDate;
            updatePacket.ParcelData.ClaimPrice = this.claimPrice;
            updatePacket.ParcelData.GroupID = this.groupID;
            updatePacket.ParcelData.GroupPrims = this.groupPrims;
            updatePacket.ParcelData.IsGroupOwned = this.isGroupOwned;
            updatePacket.ParcelData.LandingType = (byte)0; //unemplemented
            updatePacket.ParcelData.LocalID = (byte)this.localID;
            updatePacket.ParcelData.MaxPrims = 1000; //unemplemented
            updatePacket.ParcelData.MediaAutoScale = (byte)0; //unemplemented
            updatePacket.ParcelData.MediaID = LLUUID.Zero; //unemplemented
            updatePacket.ParcelData.MediaURL = Helpers.StringToField(""); //unemplemented
            updatePacket.ParcelData.MusicURL = Helpers.StringToField(""); //unemplemented
            updatePacket.ParcelData.Name = Helpers.StringToField(this.parcelName);
            updatePacket.ParcelData.OtherCleanTime = 0; //unemplemented
            updatePacket.ParcelData.OtherCount = 0; //unemplemented
            updatePacket.ParcelData.OtherPrims = 0; //unemplented
            updatePacket.ParcelData.OwnerID = this.ownerID;
            updatePacket.ParcelData.OwnerPrims = 0; //unemplemented
            updatePacket.ParcelData.ParcelFlags = (uint)this.parcelFlags; //unemplemented
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
            updatePacket.ParcelData.SalePrice = this.salePrice; //unemplemented
            updatePacket.ParcelData.SelectedPrims = 0; //unemeplemented
            updatePacket.ParcelData.SelfCount = 0;//unemplemented
            updatePacket.ParcelData.SequenceID = sequence_id;
            updatePacket.ParcelData.SimWideMaxPrims = 15000; //unemplemented
            updatePacket.ParcelData.SimWideTotalPrims = 0; //unemplemented
            updatePacket.ParcelData.SnapSelection = snap_selection; //Bleh - not important yet
            updatePacket.ParcelData.SnapshotID = LLUUID.Zero; //Unemplemented
            updatePacket.ParcelData.Status = (byte)this.parcelStatus; //??
            updatePacket.ParcelData.TotalPrims = 0; //unemplemented
            updatePacket.ParcelData.UserLocation = LLVector3.Zero; //unemplemented
            updatePacket.ParcelData.UserLookAt = LLVector3.Zero; //unemeplemented
            
            remote_client.OutPacket((Packet)updatePacket);
        }
        #endregion


        #region Update Functions
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
            this.AABBMin = new LLVector3((float)(min_x * 4), (float)(min_y * 4), m_world.Terrain[(min_x * 4), (min_y * 4)]);
            this.AABBMax = new LLVector3((float)(max_x * 4), (float)(max_y * 4), m_world.Terrain[(max_x * 4), (max_y * 4)]);
            this.area = tempArea;
        }
        #endregion


        #region Parcel Bitmap Functions
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
            }
        }
        public bool[,] getParcelBitmap()
        {
            return parcelBitmap;
        }
        private byte[] convertParcelBitmapToBytes()
        {
            byte[] tempConvertArr = new byte[64 * 64 / 8];
            byte tempByte = 0;
            int x, y,i, byteNum = 0;
            i = 0;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(parcelBitmap[x,y]) << (i++ % 8));
                    if (i % 8 == 0)
                    {
                        tempConvertArr[byteNum] = tempByte;
                        tempByte = (byte)0;
                        i = 0;
                        byteNum++;
                    }
                }
            }
            tempByte.ToString();
            return tempConvertArr;
        }

        public static bool[,] basicFullRegionParcelBitmap()
        {
            return getSquareParcelBitmap(0, 0, 256, 256);
        }
        public static bool[,] getSquareParcelBitmap(int start_x, int start_y, int end_x, int end_y)
        {

            bool[,] tempBitmap = new bool[64, 64];
            tempBitmap.Initialize();

            tempBitmap = modifyParcelBitmapSquare(tempBitmap, start_x, start_y, end_x, end_x, true);
            return tempBitmap;
        }
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
                    if (bitmap_add[x,y])
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
