using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Interfaces
{
    public interface IWorld
    {
        bool AddNewAvatar(IClientAPI remoteClient, bool childAgent);
        bool RemoveAvatar(LLUUID agentID);
        RegionInfo GetRegionInfo();
    }
}
