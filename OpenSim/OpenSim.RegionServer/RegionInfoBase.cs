using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using libsecondlife;

namespace OpenSim
{
    public class RegionInfoBase
    {
        public LLUUID SimUUID;
        public string RegionName;
        public uint RegionLocX;
        public uint RegionLocY;
        public ulong RegionHandle;
        public ushort RegionWaterHeight = 20;
        public bool RegionTerraform = true;

        public int IPListenPort;
        public string IPListenAddr;

        // Region Information
        public LLUUID TerrainBase0 = new LLUUID(); // Insert default here
        public LLUUID TerrainBase1 = new LLUUID();
        public LLUUID TerrainBase2 = new LLUUID();
        public LLUUID TerrainBase3 = new LLUUID();
        public LLUUID TerrainDetail0 = new LLUUID();
        public LLUUID TerrainDetail1 = new LLUUID();
        public LLUUID TerrainDetail2 = new LLUUID();
        public LLUUID TerrainDetail3 = new LLUUID();
        public float TerrainStartHeight00 = 0.0f;
        public float TerrainStartHeight01 = 0.0f;
        public float TerrainStartHeight10 = 0.0f;
        public float TerrainStartHeight11 = 0.0f;
        public float TerrainHeightRange00 = 40.0f;
        public float TerrainHeightRange01 = 40.0f;
        public float TerrainHeightRange10 = 40.0f;
        public float TerrainHeightRange11 = 40.0f;

        // Terrain Default (Must be in F32 Format!)
        public string TerrainFile = "default.r32";
        public double TerrainMultiplier = 60.0;


        public RegionInfoBase()
        {

        }
    }

}
