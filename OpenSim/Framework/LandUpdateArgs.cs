using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class LandUpdateArgs : EventArgs
    {
        public UUID AuthBuyerID;
        public Parcel.ParcelCategory Category;
        public string Desc;
        public UUID GroupID;
        public byte LandingType;
        public byte MediaAutoScale;
        public UUID MediaID;
        public string MediaURL;
        public string MusicURL;
        public string Name;
        public uint ParcelFlags;
        public float PassHours;
        public int PassPrice;
        public int SalePrice;
        public UUID SnapshotID;
        public Vector3 UserLocation;
        public Vector3 UserLookAt;
    }
}
