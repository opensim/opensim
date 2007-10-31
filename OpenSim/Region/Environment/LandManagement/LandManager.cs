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
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.LandManagement
{

    #region LandManager Class

    /// <summary>
    /// Handles Land objects and operations requiring information from other Land objects (divide, join, etc)
    /// </summary>
    public class LandManager
    {
        #region Constants

        //Land types set with flags in ParcelOverlay.
        //Only one of these can be used. 
        public const byte LAND_TYPE_PUBLIC = (byte) 0; //Equals 00000000
        public const byte LAND_TYPE_OWNED_BY_OTHER = (byte) 1; //Equals 00000001
        public const byte LAND_TYPE_OWNED_BY_GROUP = (byte) 2; //Equals 00000010
        public const byte LAND_TYPE_OWNED_BY_REQUESTER = (byte) 3; //Equals 00000011
        public const byte LAND_TYPE_IS_FOR_SALE = (byte) 4; //Equals 00000100
        public const byte LAND_TYPE_IS_BEING_AUCTIONED = (byte) 5; //Equals 00000101


        //Flags that when set, a border on the given side will be placed
        //NOTE: North and East is assumable by the west and south sides (if land to east has a west border, then I have an east border; etc)
        //This took forever to figure out -- jeesh. /blame LL for even having to send these
        public const byte LAND_FLAG_PROPERTY_BORDER_WEST = (byte) 64; //Equals 01000000
        public const byte LAND_FLAG_PROPERTY_BORDER_SOUTH = (byte) 128; //Equals 10000000

        //RequestResults (I think these are right, they seem to work):
        public const int LAND_RESULT_SINGLE = 0; // The request they made contained only a single piece of land
        public const int LAND_RESULT_MULTIPLE = 1; // The request they made contained more than a single peice of land

        //ParcelSelectObjects
        public const int LAND_SELECT_OBJECTS_OWNER = 2;
        public const int LAND_SELECT_OBJECTS_GROUP = 4;
        public const int LAND_SELECT_OBJECTS_OTHER = 8;


        //These are other constants. Yay!
        public const int START_LAND_LOCAL_ID = 1;

        #endregion

        #region Member Variables

        public Dictionary<int, Land> landList = new Dictionary<int, Land>();
        private int lastLandLocalID = START_LAND_LOCAL_ID - 1;
        private int[,] landIDList = new int[64,64];

        /// <summary>
        /// Set to true when a prim is moved, created, added. Performs a prim count update
        /// </summary>
        public bool landPrimCountTainted = false;

        private readonly Scene m_scene;
        private readonly RegionInfo m_regInfo;

        #endregion

        #region Constructors

        public LandManager(Scene scene, RegionInfo reginfo)
        {
            m_scene = scene;
            m_regInfo = reginfo;
            landIDList.Initialize();
        }

        #endregion

        #region Member Functions

        #region Parcel From Storage Functions

        public void LandFromStorage(LandData data)
        {
            Land new_land = new Land(data.ownerID, data.isGroupOwned, m_scene);
            new_land.landData = data.Copy();
            new_land.setLandBitmapFromByteArray();
            addLandObject(new_land);
        }

        public void NoLandDataFromStorage()
        {
            resetSimLandObjects();
        }

        #endregion

        #region Parcel Add/Remove/Get/Create

        /// <summary>
        /// Creates a basic Parcel object without an owner (a zeroed key)
        /// </summary>
        /// <returns></returns>
        public Land createBaseLand()
        {
            return new Land(new LLUUID(), false, m_scene);
        }

        /// <summary>
        /// Adds a land object to the stored list and adds them to the landIDList to what they own
        /// </summary>
        /// <param name="new_land">The land object being added</param>
        public Land addLandObject(Land new_land)
        {
            lastLandLocalID++;
            new_land.landData.localID = lastLandLocalID;
            landList.Add(lastLandLocalID, new_land.Copy());


            bool[,] landBitmap = new_land.getLandBitmap();
            int x, y;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (landBitmap[x, y])
                    {
                        landIDList[x, y] = lastLandLocalID;
                    }
                }
            }
            landList[lastLandLocalID].forceUpdateLandInfo();

            return new_land;
        }

        /// <summary>
        /// Removes a land object from the list. Will not remove if local_id is still owning an area in landIDList
        /// </summary>
        /// <param name="local_id">Land.localID of the peice of land to remove.</param>
        public void removeLandObject(int local_id)
        {
            int x, y;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (landIDList[x, y] == local_id)
                    {
                        throw new Exception("Could not remove land object. Still being used at " + x + ", " + y);
                    }
                }
            }
            // TODO: Put event here for storage manager to bind to.
            landList.Remove(local_id);
        }

        private void performFinalLandJoin(Land master, Land slave)
        {
            int x, y;
            bool[,] landBitmapSlave = slave.getLandBitmap();
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (landBitmapSlave[x, y])
                    {
                        landIDList[x, y] = master.landData.localID;
                    }
                }
            }
            removeLandObject(slave.landData.localID);
        }

        /// <summary>
        /// Get the land object at the specified point
        /// </summary>
        /// <param name="x">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Land object at the point supplied</returns>
        public Land getLandObject(float x_float, float y_float)
        {
            int x = Convert.ToInt32(Math.Floor(Convert.ToDouble(x_float)/Convert.ToDouble(4.0)));
            int y = Convert.ToInt32(Math.Floor(Convert.ToDouble(y_float)/Convert.ToDouble(4.0)));

            if (x > 63 || y > 63 || x < 0 || y < 0)
            {
                throw new Exception("Error: Parcel not found at point " + x + ", " + y);
            }
            else
            {
                // Console.WriteLine("Point (" + x + ", " + y + ") determined from point (" + x_float + ", " + y_float + ")");
                return landList[landIDList[x, y]];
            }
        }

        public Land getLandObject(int x, int y)
        {
            if (x > 256 || y > 256 || x < 0 || y < 0)
            {
                throw new Exception("Error: Parcel not found at point " + x + ", " + y);
            }
            else
            {
                return landList[landIDList[x/4, y/4]];
            }
        }

        #endregion

        #region Parcel Modification

        /// <summary>
        /// Subdivides a piece of land
        /// </summary>
        /// <param name="start_x">West Point</param>
        /// <param name="start_y">South Point</param>
        /// <param name="end_x">East Point</param>
        /// <param name="end_y">North Point</param>
        /// <param name="attempting_user_id">LLUUID of user who is trying to subdivide</param>
        /// <returns>Returns true if successful</returns>
        private bool subdivide(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
        {
            //First, lets loop through the points and make sure they are all in the same peice of land
            //Get the land object at start
            Land startLandObject = getLandObject(start_x, start_y);
            if (startLandObject == null) return false; //No such land object at the beginning

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
                        Land tempLandObject = getLandObject(start_x + x, start_y + y);
                        if (tempLandObject == null) return false; //No such land object at that point
                        if (tempLandObject != startLandObject) return false; //Subdividing over 2 land objects; no-no
                    }
                }
            }
            catch (Exception)
            {
                return false; //Exception. For now, lets skip subdivision
            }

            //If we are still here, then they are subdividing within one piece of land
            //Check owner
            if (startLandObject.landData.ownerID != attempting_user_id)
            {
                return false; //They cant do this!
            }

            //Lets create a new land object with bitmap activated at that point (keeping the old land objects info)
            Land newLand = startLandObject.Copy();
            newLand.landData.landName = "Subdivision of " + newLand.landData.landName;
            newLand.landData.globalID = LLUUID.Random();

            newLand.setLandBitmap(Land.getSquareLandBitmap(start_x, start_y, end_x, end_y));

            //Now, lets set the subdivision area of the original to false
            int startLandObjectIndex = startLandObject.landData.localID;
            landList[startLandObjectIndex].setLandBitmap(
                Land.modifyLandBitmapSquare(startLandObject.getLandBitmap(), start_x, start_y, end_x, end_y, false));
            landList[startLandObjectIndex].forceUpdateLandInfo();


            setPrimsTainted();

            //Now add the new land object
            Land result = addLandObject(newLand);
            result.sendLandUpdateToAvatarsOverMe();


            return true;
        }

        /// <summary>
        /// Join 2 land objects together
        /// </summary>
        /// <param name="start_x">x value in first piece of land</param>
        /// <param name="start_y">y value in first piece of land</param>
        /// <param name="end_x">x value in second peice of land</param>
        /// <param name="end_y">y value in second peice of land</param>
        /// <param name="attempting_user_id">LLUUID of the avatar trying to join the land objects</param>
        /// <returns>Returns true if successful</returns>
        private bool join(int start_x, int start_y, int end_x, int end_y, LLUUID attempting_user_id)
        {
            end_x -= 4;
            end_y -= 4;

            List<Land> selectedLandObjects = new List<Land>();
            int stepXSelected = 0;
            int stepYSelected = 0;
            for (stepYSelected = start_y; stepYSelected <= end_y; stepYSelected += 4)
            {
                for (stepXSelected = start_x; stepXSelected <= end_x; stepXSelected += 4)
                {
                    Land p = getLandObject(stepXSelected, stepYSelected);
                    if (!selectedLandObjects.Contains(p))
                    {
                        selectedLandObjects.Add(p);
                    }
                }
            }
            Land masterLandObject = selectedLandObjects[0];
            selectedLandObjects.RemoveAt(0);


            if (selectedLandObjects.Count < 1)
            {
                return false; //Only one piece of land selected
            }
            if (masterLandObject.landData.ownerID != attempting_user_id)
            {
                return false; //Not the same owner
            }
            foreach (Land p in selectedLandObjects)
            {
                if (p.landData.ownerID != masterLandObject.landData.ownerID)
                {
                    return false; //Over multiple users. TODO: make this just ignore this piece of land?
                }
            }
            foreach (Land slaveLandObject in selectedLandObjects)
            {
                landList[masterLandObject.landData.localID].setLandBitmap(
                    Land.mergeLandBitmaps(masterLandObject.getLandBitmap(), slaveLandObject.getLandBitmap()));
                performFinalLandJoin(masterLandObject, slaveLandObject);
            }


            setPrimsTainted();

            masterLandObject.sendLandUpdateToAvatarsOverMe();

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
            const int LAND_BLOCKS_PER_PACKET = 1024;
            int x, y = 0;
            byte[] byteArray = new byte[LAND_BLOCKS_PER_PACKET];
            int byteArrayCount = 0;
            int sequenceID = 0;
            ParcelOverlayPacket packet;

            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    byte tempByte = (byte) 0; //This represents the byte for the current 4x4
                    Land currentParcelBlock = getLandObject(x*4, y*4);

                    if (currentParcelBlock.landData.ownerID == remote_client.AgentId)
                    {
                        //Owner Flag
                        tempByte = Convert.ToByte(tempByte | LAND_TYPE_OWNED_BY_REQUESTER);
                    }
                    else if (currentParcelBlock.landData.salePrice > 0 &&
                             (currentParcelBlock.landData.authBuyerID == LLUUID.Zero ||
                              currentParcelBlock.landData.authBuyerID == remote_client.AgentId))
                    {
                        //Sale Flag
                        tempByte = Convert.ToByte(tempByte | LAND_TYPE_IS_FOR_SALE);
                    }
                    else if (currentParcelBlock.landData.ownerID == LLUUID.Zero)
                    {
                        //Public Flag
                        tempByte = Convert.ToByte(tempByte | LAND_TYPE_PUBLIC);
                    }
                    else
                    {
                        //Other Flag
                        tempByte = Convert.ToByte(tempByte | LAND_TYPE_OWNED_BY_OTHER);
                    }


                    //Now for border control
                    if (x == 0)
                    {
                        tempByte = Convert.ToByte(tempByte | LAND_FLAG_PROPERTY_BORDER_WEST);
                    }
                    else if (getLandObject((x - 1)*4, y*4) != currentParcelBlock)
                    {
                        tempByte = Convert.ToByte(tempByte | LAND_FLAG_PROPERTY_BORDER_WEST);
                    }

                    if (y == 0)
                    {
                        tempByte = Convert.ToByte(tempByte | LAND_FLAG_PROPERTY_BORDER_SOUTH);
                    }
                    else if (getLandObject(x*4, (y - 1)*4) != currentParcelBlock)
                    {
                        tempByte = Convert.ToByte(tempByte | LAND_FLAG_PROPERTY_BORDER_SOUTH);
                    }

                    byteArray[byteArrayCount] = tempByte;
                    byteArrayCount++;
                    if (byteArrayCount >= LAND_BLOCKS_PER_PACKET)
                    {
                        byteArrayCount = 0;
                        packet = new ParcelOverlayPacket();
                        packet.ParcelData.Data = byteArray;
                        packet.ParcelData.SequenceID = sequenceID;
                        remote_client.OutPacket((Packet) packet);
                        sequenceID++;
                        byteArray = new byte[LAND_BLOCKS_PER_PACKET];
                    }
                }
            }
        }

        public void handleParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id,
                                                  bool snap_selection, IClientAPI remote_client)
        {
            //Get the land objects within the bounds
            List<Land> temp = new List<Land>();
            int x, y, i;
            int inc_x = end_x - start_x;
            int inc_y = end_y - start_y;
            for (x = 0; x < inc_x; x++)
            {
                for (y = 0; y < inc_y; y++)
                {
                    Land currentParcel = getLandObject(start_x + x, start_y + y);
                    if (!temp.Contains(currentParcel))
                    {
                        currentParcel.forceUpdateLandInfo();
                        temp.Add(currentParcel);
                    }
                }
            }

            int requestResult = LAND_RESULT_SINGLE;
            if (temp.Count > 1)
            {
                requestResult = LAND_RESULT_MULTIPLE;
            }

            for (i = 0; i < temp.Count; i++)
            {
                temp[i].sendLandProperties(sequence_id, snap_selection, requestResult, remote_client);
            }


            sendParcelOverlay(remote_client);
        }

        public void handleParcelPropertiesUpdateRequest(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client)
        {
            if (landList.ContainsKey(packet.ParcelData.LocalID))
            {
                landList[packet.ParcelData.LocalID].updateLandProperties(packet, remote_client);
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
            landList[local_id].sendForceObjectSelect(local_id, request_type, remote_client);
        }

        public void handleParcelObjectOwnersRequest(int local_id, IClientAPI remote_client)
        {
            landList[local_id].sendLandObjectOwners(remote_client);
        }

        #endregion

        /// <summary>
        /// Resets the sim to the default land object (full sim piece of land owned by the default user)
        /// </summary>
        public void resetSimLandObjects()
        {
            //Remove all the land objects in the sim and add a blank, full sim land object set to public
            landList.Clear();
            lastLandLocalID = START_LAND_LOCAL_ID - 1;
            landIDList.Initialize();

            Land fullSimParcel = new Land(LLUUID.Zero, false, m_scene);

            fullSimParcel.setLandBitmap(Land.getSquareLandBitmap(0, 0, 256, 256));
            fullSimParcel.landData.ownerID = m_regInfo.MasterAvatarAssignedUUID;

            addLandObject(fullSimParcel);
        }

        public void sendLandUpdate(ScenePresence avatar)
        {
            Land over = getLandObject((int) Math.Round(avatar.AbsolutePosition.X),
                                      (int) Math.Round(avatar.AbsolutePosition.Y));

            if (over != null)
            {
                over.sendLandUpdateToClient(avatar.ControllingClient);
            }
        }

        public void handleSignificantClientMovement(IClientAPI remote_client)
        {
            ScenePresence clientAvatar = m_scene.GetScenePresence(remote_client.AgentId);

            if (clientAvatar != null)
            {
                sendLandUpdate(clientAvatar);
            }
        }

        public void resetAllLandPrimCounts()
        {
            foreach (Land p in landList.Values)
            {
                p.resetLandPrimCounts();
            }
        }

        public void setPrimsTainted()
        {
            landPrimCountTainted = true;
        }

        public void addPrimToLandPrimCounts(SceneObjectGroup obj)
        {
            LLVector3 position = obj.AbsolutePosition;
            Land landUnderPrim = getLandObject(position.X, position.Y);
            if (landUnderPrim != null)
            {
                landUnderPrim.addPrimToCount(obj);
            }
        }

        public void removePrimFromLandPrimCounts(SceneObjectGroup obj)
        {
            foreach (Land p in landList.Values)
            {
                p.removePrimFromCount(obj);
            }
        }

        public void finalizeLandPrimCountUpdate()
        {
            //Get Simwide prim count for owner
            Dictionary<LLUUID, List<Land>> landOwnersAndParcels = new Dictionary<LLUUID, List<Land>>();
            foreach (Land p in landList.Values)
            {
                if (!landOwnersAndParcels.ContainsKey(p.landData.ownerID))
                {
                    List<Land> tempList = new List<Land>();
                    tempList.Add(p);
                    landOwnersAndParcels.Add(p.landData.ownerID, tempList);
                }
                else
                {
                    landOwnersAndParcels[p.landData.ownerID].Add(p);
                }
            }

            foreach (LLUUID owner in landOwnersAndParcels.Keys)
            {
                int simArea = 0;
                int simPrims = 0;
                foreach (Land p in landOwnersAndParcels[owner])
                {
                    simArea += p.landData.area;
                    simPrims += p.landData.ownerPrims + p.landData.otherPrims + p.landData.groupPrims +
                                p.landData.selectedPrims;
                }

                foreach (Land p in landOwnersAndParcels[owner])
                {
                    p.landData.simwideArea = simArea;
                    p.landData.simwidePrims = simPrims;
                }
            }
        }

        #endregion
    }

    #endregion
}