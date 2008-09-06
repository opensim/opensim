using System;
using OpenMetaverse;

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
        public UUID SimOwner;
        public UUID terrainBase0;
        public UUID terrainBase1;
        public UUID terrainBase2;
        public UUID terrainBase3;
        public UUID terrainDetail0;
        public UUID terrainDetail1;
        public UUID terrainDetail2;
        public UUID terrainDetail3;
    }
}
