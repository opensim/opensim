using System;
using OpenSim.Data;

namespace OpenSim.Grid.MessagingServer
{
    public interface IMessageRegionService
    {
        int ClearRegionCache();
        RegionProfileData GetRegionInfo(ulong regionhandle);
    }
}
