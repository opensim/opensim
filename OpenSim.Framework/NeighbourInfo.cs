using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
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
