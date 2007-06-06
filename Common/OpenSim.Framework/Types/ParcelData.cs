using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    
        public class ParcelData
        {
            public byte[] parcelBitmapByteArray = new byte[512];
            public string parcelName = "";
            public string parcelDesc = "";
            public LLUUID ownerID = new LLUUID();
            public bool isGroupOwned = false;
            public LLVector3 AABBMin = new LLVector3();
            public LLVector3 AABBMax = new LLVector3();
            public int area = 0;
            public uint auctionID = 0; //Unemplemented. If set to 0, not being auctioned
            public LLUUID authBuyerID = new LLUUID(); //Unemplemented. Authorized Buyer's UUID
            public libsecondlife.Parcel.ParcelCategory category = new libsecondlife.Parcel.ParcelCategory(); //Unemplemented. Parcel's chosen category
            public int claimDate = 0; //Unemplemented
            public int claimPrice = 0; //Unemplemented
            public LLUUID groupID = new LLUUID(); //Unemplemented
            public int groupPrims = 0; //Unemplemented
            public int salePrice = 0; //Unemeplemented. Parcels price.
            public libsecondlife.Parcel.ParcelStatus parcelStatus = libsecondlife.Parcel.ParcelStatus.None;
            public libsecondlife.Parcel.ParcelFlags parcelFlags = libsecondlife.Parcel.ParcelFlags.None;

            public int localID = 0;

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
                parcelData.groupID = this.groupID;
                parcelData.groupPrims = this.groupPrims;
                parcelData.isGroupOwned = this.isGroupOwned;
                parcelData.localID = this.localID;
                parcelData.ownerID = this.ownerID;
                parcelData.parcelBitmapByteArray = (byte[])this.parcelBitmapByteArray.Clone();
                parcelData.parcelDesc = this.parcelDesc;
                parcelData.parcelFlags = this.parcelFlags;
                parcelData.parcelName = this.parcelName;
                parcelData.parcelStatus = this.parcelStatus;
                parcelData.salePrice = this.salePrice;

                return parcelData;
           
            }
        }
    
}
