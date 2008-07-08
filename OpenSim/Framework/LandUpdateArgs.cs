using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class LandUpdateArgs : EventArgs
    {
        public LLUUID AuthBuyerID;
        public Parcel.ParcelCategory Category;
        public string Desc;
        public LLUUID GroupID;
        public byte LandingType;
        public byte MediaAutoScale;
        public LLUUID MediaID;
        public string MediaURL;
        public string MusicURL;
        public string Name;
        public uint ParcelFlags;
        public float PassHours;
        public int PassPrice;
        public int SalePrice;
        public LLUUID SnapshotID;
        public LLVector3 UserLocation;
        public LLVector3 UserLookAt;
    }
}