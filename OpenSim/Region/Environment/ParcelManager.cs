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
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Scenes;
using Avatar = OpenSim.Region.Environment.Scenes.ScenePresence;
using System.IO;

namespace OpenSim.Region.Environment
{


    #region ParcelManager Class
    /// <summary>
    /// Handles Parcel objects and operations requiring information from other Parcel objects (divide, join, etc)
    /// </summary>
    public class ParcelManager : ILocalStorageParcelReceiver
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
        public const int PARCEL_RESULT_ONE_PARCEL = 0;	// The request they made contained only one parcel
        public const int PARCEL_RESULT_MULTIPLE_PARCELS = 1;	// The request they made contained more than one parcel

        //ParcelSelectObjects
        public const int PARCEL_SELECT_OBJECTS_OWNER = 2;
        public const int PARCEL_SELECT_OBJECTS_GROUP = 4;
        public const int PARCEL_SELECT_OBJECTS_OTHER = 8;


        //These are other constants. Yay!
        public const int START_PARCEL_LOCAL_ID = 1;
        #endregion

        #region Member Variables
        public Dictionary<int, Parcel> parcelList = new Dictionary<int, Parcel>();
        private int lastParcelLocalID = START_PARCEL_LOCAL_ID - 1;
        private int[,] parcelIDList = new int[64, 64];

        /// <summary>
        /// Set to true when a prim is moved, created, added. Performs a prim count update
        /// </summary>
        public bool parcelPrimCountTainted = false;

        private Scene m_world;
        private RegionInfo m_regInfo;

        #endregion

        #region Constructors
        public ParcelManager(Scene world, RegionInfo reginfo)
        {

            m_world = world;
            m_regInfo = reginfo;
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
        public Parcel addParcel(Parcel new_parcel)
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

            return new_parcel;

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

        private void performFinalParcelJoin(Parcel master, Parcel slave)
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
        public Parcel getParcel(float x_float, float y_float)
        {
            int x = Convert.ToInt32(Math.Floor(Convert.ToDecimal(x_float) / Convert.ToDecimal(4.0)));
            int y = Convert.ToInt32(Math.Floor(Convert.ToDecimal(y_float) / Convert.ToDecimal(4.0)));
            
            if (x > 63 || y > 63 || x < 0 || y < 0)
            {
                throw new Exception("Error: Parcel not found at point " + x + ", " + y);
            }
            else
            {
              // Console.WriteLine("Point (" + x + ", " + y + ") determined from point (" + x_float + ", " + y_float + ")");
                return parcelList[parcelIDList[x, y]];
            }
        }

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
        private bool subdivide(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
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
            catch (Exception)
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


            this.setPrimsTainted();

            //Now add the new parcel
            Parcel result = addParcel(newParcel);
            result.sendParcelUpdateToAvatarsOverMe();




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
        private bool join(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
        {
            end_x -= 4;
            end_y -= 4;

            List<Parcel> selectedParcels = new List<Parcel>();
            int stepXSelected = 0;
            int stepYSelected = 0;
            for (stepYSelected = start_y; stepYSelected <= end_y; stepYSelected += 4)
            {
                for (stepXSelected = start_x; stepXSelected <= end_x; stepXSelected += 4)
                {
                    Parcel p = getParcel(stepXSelected,stepYSelected);
                    if (!selectedParcels.Contains(p))
                    {
                        selectedParcels.Add(p);
                    }
                }
            }
            Parcel masterParcel = selectedParcels[0];
            selectedParcels.RemoveAt(0);

            
            if (selectedParcels.Count < 1)
            {
                return false; //Only one parcel selected
            }
            if (masterParcel.parcelData.ownerID != attempting_user_id)
            {
                return false; //Not the same owner
            }
            foreach (Parcel p in selectedParcels)
            {
                if (p.parcelData.ownerID != masterParcel.parcelData.ownerID)
                {
                    return false; //Over multiple users. TODO: make this just ignore this parcel?
                }
            }
            foreach (Parcel slaveParcel in selectedParcels)
            {
                parcelList[masterParcel.parcelData.localID].setParcelBitmap(Parcel.mergeParcelBitmaps(masterParcel.getParcelBitmap(), slaveParcel.getParcelBitmap()));
                performFinalParcelJoin(masterParcel, slaveParcel);
            }


            this.setPrimsTainted();

            masterParcel.sendParcelUpdateToAvatarsOverMe();

            return true;



        }
        #endregion

        #region Parcel Updating
        /// <summary>
        /// Where we send the ParcelOverlay packet to the client
        /// </summary>
        /// <param name="remote_client">The object representing the client</param>
        public void sendParcelOverlay(IClientAPI remote_client)
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

                    if (currentParcelBlock.parcelData.ownerID == remote_client.AgentId)
                    {
                        //Owner Flag
                        tempByte = Convert.ToByte(tempByte | PARCEL_TYPE_OWNED_BY_REQUESTER);
                    }
                    else if (currentParcelBlock.parcelData.salePrice > 0 && (currentParcelBlock.parcelData.authBuyerID == LLUUID.Zero || currentParcelBlock.parcelData.authBuyerID == remote_client.AgentId))
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

            
        }

        public void handleParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client)
        {
            //Get the parcels within the bounds
            List<Parcel> temp = new List<Parcel>();
            int x, y, i;
            int inc_x = end_x - start_x;
            int inc_y = end_y - start_y;
            for (x = 0; x < inc_x; x++)
            {
                for (y = 0; y < inc_y; y++)
                {
                    Parcel currentParcel = getParcel(start_x + x, start_y + y);
                    if (!temp.Contains(currentParcel))
                    {
                        currentParcel.forceUpdateParcelInfo();
                        temp.Add(currentParcel);
                    }
                }
            }

            int requestResult = PARCEL_RESULT_ONE_PARCEL;
            if (temp.Count > 1)
            {
                requestResult = PARCEL_RESULT_MULTIPLE_PARCELS;
            }

            for (i = 0; i < temp.Count; i++)
            {
                temp[i].sendParcelProperties(sequence_id, snap_selection, requestResult, remote_client);
            }


            sendParcelOverlay(remote_client);
        }

        public void handleParcelPropertiesUpdateRequest(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client)
        {
            if (parcelList.ContainsKey(packet.ParcelData.LocalID))
            {
                parcelList[packet.ParcelData.LocalID].updateParcelProperties(packet, remote_client);
            }
        }
        public void handleParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            subdivide(west, south, east, north, remote_client.AgentId);
        }
        public void handleParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            join(west, south, east, north, remote_client.AgentId);

        }

        public void handleParcelSelectObjectsRequest(int local_id, int request_type, IClientAPI remote_client)
        {
            parcelList[local_id].sendForceObjectSelect(local_id, request_type, remote_client);
        }

        public void handleParcelObjectOwnersRequest(int local_id, IClientAPI remote_client)
        {
            parcelList[local_id].sendParcelObjectOwners(remote_client);
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
            fullSimParcel.parcelData.ownerID = m_regInfo.MasterAvatarAssignedUUID;

            addParcel(fullSimParcel);

        }


        public void handleSignificantClientMovement(IClientAPI remote_client)
        {
            Avatar clientAvatar = m_world.RequestAvatar(remote_client.AgentId);
            if (clientAvatar != null)
            {
                Parcel over = getParcel(clientAvatar.Pos.X,clientAvatar.Pos.Y);
                if (over != null)
                {
                    over.sendParcelProperties(0, false, 0, remote_client);
                }
            }
        }

        public void resetAllParcelPrimCounts()
        {
            foreach (Parcel p in parcelList.Values)
            {
                p.resetParcelPrimCounts();
            }
        }
        public void setPrimsTainted()
        {
            this.parcelPrimCountTainted = true;
        }

        public void addPrimToParcelCounts(SceneObject obj)
        {
            LLVector3 position = obj.Pos;
            Parcel parcelUnderPrim = getParcel(position.X, position.Y);
            if (parcelUnderPrim != null)
            {
                parcelUnderPrim.addPrimToCount(obj);
            }
        }

        public void removePrimFromParcelCounts(SceneObject obj)
        {
            foreach (Parcel p in parcelList.Values)
            {
                p.removePrimFromCount(obj);
            }
        }

        public void finalizeParcelPrimCountUpdate()
        {
            //Get Simwide prim count for owner
            Dictionary<LLUUID, List<Parcel>> parcelOwnersAndParcels = new Dictionary<LLUUID,List<Parcel>>();
            foreach (Parcel p in parcelList.Values)
            {
                if(!parcelOwnersAndParcels.ContainsKey(p.parcelData.ownerID))
                {
                    List<Parcel> tempList = new List<Parcel>();
                    tempList.Add(p);
                    parcelOwnersAndParcels.Add(p.parcelData.ownerID,tempList);
                }
                else
                {
                    parcelOwnersAndParcels[p.parcelData.ownerID].Add(p);
                }
            }

            foreach (LLUUID owner in parcelOwnersAndParcels.Keys)
            {
                int simArea = 0;
                int simPrims = 0;
                foreach (Parcel p in parcelOwnersAndParcels[owner])
                {
                    simArea += p.parcelData.area;
                    simPrims += p.parcelData.ownerPrims + p.parcelData.otherPrims + p.parcelData.groupPrims + p.parcelData.selectedPrims;
                }

                foreach (Parcel p in parcelOwnersAndParcels[owner])
                {
                    p.parcelData.simwideArea = simArea;
                    p.parcelData.simwidePrims = simPrims;
                }
            }

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
            List<Avatar> avatars = m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                Parcel over = m_world.ParcelManager.getParcel((int)Math.Round(avatars[i].Pos.X), (int)Math.Round(avatars[i].Pos.Y));
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
                    if (request_type == ParcelManager.PARCEL_SELECT_OBJECTS_OWNER && obj.rootPrimitive.OwnerID == this.parcelData.ownerID)
                    {
                        resultLocalIDs.Add(obj.rootLocalID);
                    }
                    else if (request_type == ParcelManager.PARCEL_SELECT_OBJECTS_GROUP && false) //TODO: change false to group support!
                    {

                    }
                    else if (request_type == ParcelManager.PARCEL_SELECT_OBJECTS_OTHER && obj.rootPrimitive.OwnerID != remote_client.AgentId)
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
            Dictionary<LLUUID, int> ownersAndCount = new Dictionary<LLUUID,int>();
            foreach(SceneObject obj in primsOverMe)
            {
                if(!ownersAndCount.ContainsKey(obj.rootPrimitive.OwnerID))
                {
                    ownersAndCount.Add(obj.rootPrimitive.OwnerID,0);
                }
                ownersAndCount[obj.rootPrimitive.OwnerID] += obj.primCount;
            }
            if (ownersAndCount.Count > 0)
            {

                ParcelObjectOwnersReplyPacket.DataBlock[] dataBlock = new ParcelObjectOwnersReplyPacket.DataBlock[32];
                
                if(ownersAndCount.Count < 32)
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
