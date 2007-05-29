using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Interfaces
{
    public interface IWorld
    {
        void AddNewAvatar(IClientAPI remoteClient, LLUUID agentID, bool child);
        void RemoveAvatar(LLUUID agentID);
        RegionInfo GetRegionInfo();
    }
}
