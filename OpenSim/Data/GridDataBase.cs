using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Data
{
    public abstract class GridDataBase : IGridData
    {
        public abstract RegionProfileData GetProfileByHandle(ulong regionHandle);
        public abstract RegionProfileData GetProfileByLLUUID(LLUUID UUID);
        public abstract RegionProfileData GetProfileByString(string regionName);
        public abstract RegionProfileData[] GetProfilesInRange(uint Xmin, uint Ymin, uint Xmax, uint Ymax);
        public abstract bool AuthenticateSim(LLUUID UUID, ulong regionHandle, string simrecvkey);
        public abstract void Initialise();
        public abstract void Close();
        public abstract string getName();
        public abstract string getVersion();
        public abstract DataResponse AddProfile(RegionProfileData profile);
        public abstract ReservationData GetReservationAtPoint(uint x, uint y);
        public abstract DataResponse UpdateProfile(RegionProfileData profile);
    }
}
