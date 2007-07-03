using System;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class MapBlockData
    {
        public uint Flags;
        public ushort X;
        public ushort Y;
        public byte Agents;
        public byte Access;
        public byte WaterHeight;
        public LLUUID MapImageId;
        public String Name;
        public uint RegionFlags;

        public MapBlockData()
        {

        }
    }
}
