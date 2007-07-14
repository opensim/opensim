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
using libsecondlife;

namespace OpenSim.Framework.Types
{
    
        public class ParcelData
        {
            public byte[] parcelBitmapByteArray = new byte[512];
            public string parcelName = "Your Parcel";
            public string parcelDesc = "";
            public LLUUID ownerID = new LLUUID();
            public bool isGroupOwned = false;
            public LLVector3 AABBMin = new LLVector3();
            public LLVector3 AABBMax = new LLVector3();
            public int area = 0;
            public uint auctionID = 0; //Unemplemented. If set to 0, not being auctioned
            public LLUUID authBuyerID = new LLUUID(); //Unemplemented. Authorized Buyer's UUID
            public Parcel.ParcelCategory category = new Parcel.ParcelCategory(); //Unemplemented. Parcel's chosen category
            public int claimDate = 0; //Unemplemented
            public int claimPrice = 0; //Unemplemented
            public LLUUID groupID = new LLUUID(); //Unemplemented
            public int groupPrims = 0; //Unemplemented
            public int otherPrims = 0; //Unemplemented
            public int ownerPrims = 0; //Unemplemented
            public int salePrice = 0; //Unemeplemented. Parcels price.
            public Parcel.ParcelStatus parcelStatus = Parcel.ParcelStatus.Leased;
            public uint parcelFlags = (uint)Parcel.ParcelFlags.AllowFly | (uint)Parcel.ParcelFlags.AllowLandmark | (uint)Parcel.ParcelFlags.AllowAllObjectEntry | (uint)Parcel.ParcelFlags.AllowDeedToGroup | (uint)Parcel.ParcelFlags.AllowTerraform | (uint)Parcel.ParcelFlags.CreateObjects | (uint)Parcel.ParcelFlags.AllowOtherScripts;
            public byte landingType = 0;
            public byte mediaAutoScale = 0;
            public LLUUID mediaID = LLUUID.Zero;
            public int localID = 0;
            public LLUUID globalID = new LLUUID();

            public string mediaURL = "";
            public string musicURL = "";
            public float passHours = 0;
            public int passPrice = 0;
            public LLUUID snapshotID = LLUUID.Zero;
            public LLVector3 userLocation = new LLVector3();
            public LLVector3 userLookAt = new LLVector3();

            public ParcelData()
            {
                globalID = LLUUID.Random();
            }

            public ParcelData Copy()
            {
                ParcelData parcelData = new ParcelData();

                parcelData.AABBMax = this.AABBMax;
                parcelData.AABBMin = this.AABBMin;
                parcelData.area = this.area;
                parcelData.auctionID = this.auctionID;
                parcelData.authBuyerID = this.authBuyerID;
                parcelData.category = this.category;
                parcelData.claimDate = this.claimDate;
                parcelData.claimPrice = this.claimPrice;
                parcelData.globalID = this.globalID;
                parcelData.groupID = this.groupID;
                parcelData.groupPrims = this.groupPrims;
                parcelData.otherPrims = this.otherPrims;
                parcelData.ownerPrims = this.ownerPrims;
                parcelData.isGroupOwned = this.isGroupOwned;
                parcelData.localID = this.localID;
                parcelData.landingType = this.landingType;
                parcelData.mediaAutoScale = this.mediaAutoScale;
                parcelData.mediaID = this.mediaID;
                parcelData.mediaURL = this.mediaURL;
                parcelData.musicURL = this.musicURL;
                parcelData.ownerID = this.ownerID;
                parcelData.parcelBitmapByteArray = (byte[])this.parcelBitmapByteArray.Clone();
                parcelData.parcelDesc = this.parcelDesc;
                parcelData.parcelFlags = this.parcelFlags;
                parcelData.parcelName = this.parcelName;
                parcelData.parcelStatus = this.parcelStatus;
                parcelData.passHours = this.passHours;
                parcelData.passPrice = this.passPrice;
                parcelData.salePrice = this.salePrice;
                parcelData.snapshotID = this.snapshotID;
                parcelData.userLocation = this.userLocation;
                parcelData.userLookAt = this.userLookAt;

                return parcelData;
           
            }
        }
    
}
