/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.RegionServer.Simulator;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.RegionServer.Client;

namespace OpenSim.RegionServer.Simulator
{
    public delegate void ParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, ClientView remote_client);
    public delegate void ParcelDivideRequest(int west, int south, int east, int north, ClientView remote_client);
    public delegate void ParcelJoinRequest(int west, int south, int east, int north, ClientView remote_client);
    public delegate void ParcelPropertiesUpdateRequest(ParcelPropertiesUpdatePacket packet, ClientView remote_client);

    #region ParcelManager Class
    /// <summary>
    /// Handles Parcel objects and operations requiring information from other Parcel objects (divide, join, etc)
    /// </summary>
    public class ParcelManager : OpenSim.Framework.Interfaces.ILocalStorageParcelReceiver
    {

        #region Constants
        //Parcel types set with flags in ParcelOverlay.
        //Only one of these can be used. 
        public const byte PARCEL_TYPE_PUBLIC = (byte)0;      //Equals 00000000
        public const byte PARCEL_TYPE_OWNED_BY_OTHER = (byte)1;      //Equals 00000001
        public const byte PARCEL_TYPE_OWNED_BY_GROUP = (byte)2;      //Equals 00000010
        public const byte PARCEL_TYPE_OWNED_BY_REQUESTER = (byte)3;      //Equals 00000011
        public const byte PARCEL_TYPE_IS_FOR_SALE = (byte)4;      //Equals 00000100
        public const byte PARCEL_TYPE_IS_BEING_AUCTIONED = (byte)5;      //Equals 00000101


        //Flags that when set, a border on the given side will be placed
        //NOTE: North and East is assumable by the west and south sides (if parcel to east has a west border, then I have an east border; etc)
        //This took forever to figure out -- jeesh. /blame LL for even having to send these
        public const byte PARCEL_FLAG_PROPERTY_BORDER_WEST = (byte)64;     //Equals 01000000
        public const byte PARCEL_FLAG_PROPERTY_BORDER_SOUTH = (byte)128;    //Equals 10000000

        //RequestResults (I think these are right, they seem to work):
        public const int PARCEL_RESULT_ONE_PARCEL       = 0;	// The request they made contained only one parcel
        public const int PARCEL_RESULT_MULTIPLE_PARCELS = 1;	// The request they made contained more than one parcel

        //These are other constants. Yay!
        public const int START_PARCEL_LOCAL_ID = 1;
        #endregion

        #region Member Variables
        public Dictionary<int, Parcel> parcelList = new Dictionary<int, Parcel>();
        private int lastParcelLocalID = START_PARCEL_LOCAL_ID - 1;
        private int[,] parcelIDList = new int[64, 64];

        private static World m_world;
        #endregion

        #region Constructors
        public ParcelManager(World world)
        {

            m_world = world;
            parcelIDList.Initialize();

        }
        #endregion

        #region Member Functions

        #region Parcel From Storage Functions
        public void ParcelFromStorage(ParcelData data)
        {
            Parcel new_parcel = new Parcel(data.ownerID, data.isGroupOwned, m_world);
            new_parcel.parcelData = data.Copy();
            new_parcel.setParcelBitmapFromByteArray();
            addParcel(new_parcel);

        }

        public void NoParcelDataFromStorage()
        {
            resetSimParcels();
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
            parcelList.Add(lastParcelLocalID, new_parcel.Copy());


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
            parcelList[lastParcelLocalID].forceUpdateParcelInfo();

            
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
            m_world.localStorage.RemoveParcel(parcelList[local_id].parcelData);
            parcelList.Remove(local_id);
        }

        public void performFinalParcelJoin(Parcel master, Parcel slave)
        {
            int x, y;
            bool[,] parcelBitmapSlave = slave.getParcelBitmap();
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (parcelBitmapSlave[x, y])
                    {
                        parcelIDList[x, y] = master.parcelData.localID;
                    }
                }
            }
            removeParcel(slave.parcelData.localID);
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
                for (y = 0; y < totalY; y++)
                {
                    for (x = 0; x < totalX; x++)
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
            Parcel newParcel = startParcel.Copy();
            newParcel.parcelData.parcelName = "Subdivision of " + newParcel.parcelData.parcelName;
            newParcel.parcelData.globalID = LLUUID.Random();

            newParcel.setParcelBitmap(Parcel.getSquareParcelBitmap(start_x, start_y, end_x, end_y));

            //Now, lets set the subdivision area of the original to false
            int startParcelIndex = startParcel.parcelData.localID;
            parcelList[startParcelIndex].setParcelBitmap(Parcel.modifyParcelBitmapSquare(startParcel.getParcelBitmap(), start_x, start_y, end_x, end_y, false));
            parcelList[startParcelIndex].forceUpdateParcelInfo();
            

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
            end_x -= 4;
            end_y -= 4;
            
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
            if (startParcel == endParcel)
            {
                return false; //Subdivision of the same parcel is not allowed
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
            performFinalParcelJoin(startParcel, endParcel);

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

            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
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
                    else if (getParcel((x - 1) * 4, y * 4) != currentParcelBlock)
                    {
                        tempByte = Convert.ToByte(tempByte | PARCEL_FLAG_PROPERTY_BORDER_WEST);
                    }

                    if (y == 0)
                    {
                        tempByte = Convert.ToByte(tempByte | PARCEL_FLAG_PROPERTY_BORDER_SOUTH);
                    }
                    else if (getParcel(x * 4, (y - 1) * 4) != currentParcelBlock)
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
            lastParcelLocalID = START_PARCEL_LOCAL_ID - 1;
            parcelIDList.Initialize();

            Parcel fullSimParcel = new Parcel(LLUUID.Zero, false, m_world);

            fullSimParcel.setParcelBitmap(Parcel.getSquareParcelBitmap(0, 0, 256, 256));
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
        public void sendParcelProperties(int sequence_id, bool snap_selection, int request_result, ClientView remote_client)
        {

            ParcelPropertiesPacket updatePacket = new ParcelPropertiesPacket();
            updatePacket.ParcelData.AABBMax = parcelData.AABBMax;
            updatePacket.ParcelData.AABBMin = parcelData.AABBMin;
            updatePacket.ParcelData.Area = parcelData.area;
            updatePacket.ParcelData.AuctionID = parcelData.auctionID;
            updatePacket.ParcelData.AuthBuyerID =parcelData.authBuyerID; //unemplemented

            updatePacket.ParcelData.Bitmap = parcelData.parcelBitmapByteArray;

            updatePacket.ParcelData.Desc = libsecondlife.Helpers.StringToField(parcelData.parcelDesc);
            updatePacket.ParcelData.Category = (byte)parcelData.category;
            updatePacket.ParcelData.ClaimDate = parcelData.claimDate;
            updatePacket.ParcelData.ClaimPrice = parcelData.claimPrice;
            updatePacket.ParcelData.GroupID = parcelData.groupID;
            updatePacket.ParcelData.GroupPrims = parcelData.groupPrims;
            updatePacket.ParcelData.IsGroupOwned = parcelData.isGroupOwned;
            updatePacket.ParcelData.LandingType = (byte)parcelData.landingType;
            updatePacket.ParcelData.LocalID = parcelData.localID;
            updatePacket.ParcelData.MaxPrims = 1000; //unemplemented
            updatePacket.ParcelData.MediaAutoScale = parcelData.mediaAutoScale;
            updatePacket.ParcelData.MediaID = parcelData.mediaID;
            updatePacket.ParcelData.MediaURL = Helpers.StringToField(parcelData.mediaURL);
            updatePacket.ParcelData.MusicURL = Helpers.StringToField(parcelData.musicURL);
            updatePacket.ParcelData.Name = Helpers.StringToField(parcelData.parcelName);
            updatePacket.ParcelData.OtherCleanTime = 0; //unemplemented
            updatePacket.ParcelData.OtherCount = 0; //unemplemented
            updatePacket.ParcelData.OtherPrims = 0; //unemplented
            updatePacket.ParcelData.OwnerID = parcelData.ownerID;
            updatePacket.ParcelData.OwnerPrims = 0; //unemplemented
            updatePacket.ParcelData.ParcelFlags = (uint)parcelData.parcelFlags; //unemplemented
            updatePacket.ParcelData.ParcelPrimBonus = (float)1.0; //unemplemented
            updatePacket.ParcelData.PassHours = parcelData.passHours;
            updatePacket.ParcelData.PassPrice = parcelData.passPrice;
            updatePacket.ParcelData.PublicCount = 0; //unemplemented
            updatePacket.ParcelData.RegionDenyAnonymous = false; //unemplemented
            updatePacket.ParcelData.RegionDenyIdentified = false; //unemplemented
            updatePacket.ParcelData.RegionDenyTransacted = false; //unemplemented
            updatePacket.ParcelData.RegionPushOverride = true; //unemplemented
            updatePacket.ParcelData.RentPrice = 0; //??
            updatePacket.ParcelData.RequestResult = request_result;
            updatePacket.ParcelData.SalePrice = parcelData.salePrice; //unemplemented
            updatePacket.ParcelData.SelectedPrims = 0; //unemeplemented
            updatePacket.ParcelData.SelfCount = 0;//unemplemented
            updatePacket.ParcelData.SequenceID = sequence_id;
            updatePacket.ParcelData.SimWideMaxPrims = 15000; //unemplemented
            updatePacket.ParcelData.SimWideTotalPrims = 0; //unemplemented
            updatePacket.ParcelData.SnapSelection = snap_selection; 
            updatePacket.ParcelData.SnapshotID = parcelData.snapshotID;
            updatePacket.ParcelData.Status = (byte)parcelData.parcelStatus;
            updatePacket.ParcelData.TotalPrims = 0; //unemplemented
            updatePacket.ParcelData.UserLocation = parcelData.userLocation;
            updatePacket.ParcelData.UserLookAt = parcelData.userLookAt;
            remote_client.OutPacket((Packet)updatePacket);
        }

        public void updateParcelProperties(ParcelPropertiesUpdatePacket packet, ClientView remote_client)
        {
            if (remote_client.AgentID == parcelData.ownerID)
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
                parcelData.parcelName = libsecondlife.Helpers.FieldToUTF8String(packet.ParcelData.Name);
                parcelData.parcelFlags = (libsecondlife.Parcel.ParcelFlags)packet.ParcelData.ParcelFlags;
                parcelData.passHours = packet.ParcelData.PassHours;
                parcelData.passPrice = packet.ParcelData.PassPrice;
                parcelData.salePrice = packet.ParcelData.SalePrice;
                parcelData.snapshotID = packet.ParcelData.SnapshotID;
                parcelData.userLocation = packet.ParcelData.UserLocation;
                parcelData.userLookAt = packet.ParcelData.UserLookAt;

                foreach (Avatar av in m_world.Avatars.Values)
                {
                    Parcel over = m_world.parcelManager.getParcel((int)Math.Round(av.Pos.X), (int)Math.Round(av.Pos.Y));
                    if (over == this)
                    {
                        sendParcelProperties(0, false, 0, av.ControllingClient);
                    }
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
            parcelData.AABBMin = new LLVector3((float)(min_x * 4), (float)(min_y * 4), m_world.Terrain[(min_x * 4), (min_y * 4)]);
            parcelData.AABBMax = new LLVector3((float)(max_x * 4), (float)(max_y * 4), m_world.Terrain[(max_x * 4), (max_y * 4)]);
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
            for(i = 0; i < 512; i++)
            {
                tempByte = parcelData.parcelBitmapByteArray[i];
                for(bitNum = 0; bitNum < 8; bitNum++)
                {
                    bool bit = Convert.ToBoolean(Convert.ToByte(tempByte >> bitNum) & (byte)1);
                    tempConvertMap[x, y] = bit;
                    x++;
                    if(x > 63)
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

        #endregion

       
    }
    #endregion
    
    
}
