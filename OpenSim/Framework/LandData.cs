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
using libsecondlife;

namespace OpenSim.Framework
{
    public class LandData
    {
        public LLVector3 AABBMax = new LLVector3();
        public LLVector3 AABBMin = new LLVector3();
        public int area = 0;
        public uint auctionID = 0; //Unemplemented. If set to 0, not being auctioned
        public LLUUID authBuyerID = LLUUID.Zero; //Unemplemented. Authorized Buyer's UUID
        public Parcel.ParcelCategory category = new Parcel.ParcelCategory(); //Unemplemented. Parcel's chosen category
        public int claimDate = 0; 
        public int claimPrice = 0; //Unemplemented
        public LLUUID globalID = LLUUID.Zero;
        public LLUUID groupID = LLUUID.Zero; //Unemplemented
        public int groupPrims = 0;
        public bool isGroupOwned = false;
        public byte[] landBitmapByteArray = new byte[512];
        public string landDesc = String.Empty;


        public uint landFlags = (uint) Parcel.ParcelFlags.AllowFly | (uint) Parcel.ParcelFlags.AllowLandmark |
                                (uint) Parcel.ParcelFlags.AllowAllObjectEntry |
                                (uint) Parcel.ParcelFlags.AllowDeedToGroup | (uint) Parcel.ParcelFlags.AllowTerraform |
                                (uint) Parcel.ParcelFlags.CreateObjects | (uint) Parcel.ParcelFlags.AllowOtherScripts |
                                (uint) Parcel.ParcelFlags.SoundLocal;

        public byte landingType = 0;
        public string landName = "Your Parcel";
        public Parcel.ParcelStatus landStatus = Parcel.ParcelStatus.Leased;
        public int localID = 0;
        public byte mediaAutoScale = 0;
        public LLUUID mediaID = LLUUID.Zero;

        public string mediaURL = String.Empty;
        public string musicURL = String.Empty;
        public int otherPrims = 0;
        public LLUUID ownerID = LLUUID.Zero;
        public int ownerPrims = 0;
        public List<ParcelManager.ParcelAccessEntry> parcelAccessList = new List<ParcelManager.ParcelAccessEntry>();
        public float passHours = 0;
        public int passPrice = 0;
        public int salePrice = 0; //Unemeplemented. Parcels price.
        public int selectedPrims = 0;
        public int simwideArea = 0;
        public int simwidePrims = 0;
        public LLUUID snapshotID = LLUUID.Zero;
        public LLVector3 userLocation = new LLVector3();
        public LLVector3 userLookAt = new LLVector3();

        public LandData()
        {
            globalID = LLUUID.Random();
        }

        public LandData Copy()
        {
            LandData landData = new LandData();

            landData.AABBMax = AABBMax;
            landData.AABBMin = AABBMin;
            landData.area = area;
            landData.auctionID = auctionID;
            landData.authBuyerID = authBuyerID;
            landData.category = category;
            landData.claimDate = claimDate;
            landData.claimPrice = claimPrice;
            landData.globalID = globalID;
            landData.groupID = groupID;
            landData.groupPrims = groupPrims;
            landData.otherPrims = otherPrims;
            landData.ownerPrims = ownerPrims;
            landData.selectedPrims = selectedPrims;
            landData.isGroupOwned = isGroupOwned;
            landData.localID = localID;
            landData.landingType = landingType;
            landData.mediaAutoScale = mediaAutoScale;
            landData.mediaID = mediaID;
            landData.mediaURL = mediaURL;
            landData.musicURL = musicURL;
            landData.ownerID = ownerID;
            landData.landBitmapByteArray = (byte[]) landBitmapByteArray.Clone();
            landData.landDesc = landDesc;
            landData.landFlags = landFlags;
            landData.landName = landName;
            landData.landStatus = landStatus;
            landData.passHours = passHours;
            landData.passPrice = passPrice;
            landData.salePrice = salePrice;
            landData.snapshotID = snapshotID;
            landData.userLocation = userLocation;
            landData.userLookAt = userLookAt;

            landData.parcelAccessList.Clear();
            foreach (ParcelManager.ParcelAccessEntry entry in parcelAccessList)
            {
                ParcelManager.ParcelAccessEntry newEntry = new ParcelManager.ParcelAccessEntry();
                newEntry.AgentID = entry.AgentID;
                newEntry.Flags = entry.Flags;
                newEntry.Time = entry.Time;

                landData.parcelAccessList.Add(newEntry);
            }

            return landData;
        }
    }
}