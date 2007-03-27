using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Interfaces
{
    public class AgentCircuitData
    {
        public AgentCircuitData() { }
        public LLUUID AgentID;
        public LLUUID SessionID;
        public LLUUID SecureSessionID;
        public string firstname;
        public string lastname;
        public uint circuitcode;
    	public bool child;
    }
}
