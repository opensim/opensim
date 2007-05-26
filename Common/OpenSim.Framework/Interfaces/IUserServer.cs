using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Inventory;
using libsecondlife;

namespace OpenSim.Framework.Interfaces
{
    public interface IUserServer
    {
        AgentInventory RequestAgentsInventory(LLUUID agentID);
        void SetServerInfo(string ServerUrl, string SendKey, string RecvKey);
        bool UpdateAgentsInventory(LLUUID agentID, AgentInventory inventory);
    }
}
