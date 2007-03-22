using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Sims
{
    public class SimProfileBase
    {
        public LLUUID UUID;
        public ulong regionhandle;
        public string regionname;
        public string sim_ip;
        public uint sim_port;
        public string caps_url;
        public uint RegionLocX;
        public uint RegionLocY;
        public string sendkey;
        public string recvkey;

        public SimProfileBase()
        {
        }
    }
}
