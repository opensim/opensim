using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class NeighbourInfo
    {
        public NeighbourInfo()
        {
        }

        public ulong regionhandle;
        public uint RegionLocX;
        public uint RegionLocY;
        public string sim_ip;
        public uint sim_port;
    }
}
