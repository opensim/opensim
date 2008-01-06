using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Servers
{
    public class MessageServerInfo
    {
        public string URI;
        public string sendkey;
        public string recvkey;
        public List<ulong> responsibleForRegions;
        public MessageServerInfo()
        {
        }
        public override string ToString()
        {
            return URI;
        }
    }
}
