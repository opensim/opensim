using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Interfaces
{
    public abstract class LocalGridBase : IGridServer
    {
        public abstract UUIDBlock RequestUUIDBlock();
        public abstract NeighbourInfo[] RequestNeighbours();
        public abstract AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode);
        public abstract bool LogoutSession(LLUUID sessionID, LLUUID agentID, uint circuitCode);
        public abstract string GetName();
        public abstract bool RequestConnection(LLUUID SimUUID, string sim_ip, uint sim_port);
        public abstract void SetServerInfo(string ServerUrl, string SendKey, string RecvKey);
        public abstract void AddNewSession(Login session);
        public abstract void Close();
    }

}
