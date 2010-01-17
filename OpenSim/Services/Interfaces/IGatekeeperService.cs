using System;
using System.Collections.Generic;

using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IGatekeeperService
    {
        bool LinkRegion(string regionDescriptor, out UUID regionID, out ulong regionHandle, out string imageURL, out string reason);
        GridRegion GetHyperlinkRegion(UUID regionID);
    }
}
