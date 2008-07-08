using System;

namespace OpenSim.Framework
{
    public class RegionInfoForEstateMenuArgs : EventArgs
    {
        public float billableFactor;
        public uint estateID;
        public byte maxAgents;
        public float objectBonusFactor;
        public uint parentEstateID;
        public int pricePerMeter;
        public int redirectGridX;
        public int redirectGridY;
        public uint regionFlags;
        public byte simAccess;
        public float sunHour;
        public float terrainLowerLimit;
        public float terrainRaiseLimit;
        public bool useEstateSun;
        public float waterHeight;
        public string simName;
    }
}