using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class RegionHandshakeArgs : EventArgs
    {
        public bool isEstateManager;
        public float billableFactor;
        public float terrainHeightRange0;
        public float terrainHeightRange1;
        public float terrainHeightRange2;
        public float terrainHeightRange3;
        public float terrainStartHeight0;
        public float terrainStartHeight1;
        public float terrainStartHeight2;
        public float terrainStartHeight3;
        public byte simAccess;
        public float waterHeight;
        public uint regionFlags;
        public string regionName;
        public LLUUID SimOwner;
        public LLUUID terrainBase0;
        public LLUUID terrainBase1;
        public LLUUID terrainBase2;
        public LLUUID terrainBase3;
        public LLUUID terrainDetail0;
        public LLUUID terrainDetail1;
        public LLUUID terrainDetail2;
        public LLUUID terrainDetail3;
    }
}