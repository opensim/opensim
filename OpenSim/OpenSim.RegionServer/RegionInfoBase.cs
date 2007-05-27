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
        // Low resolution 'base' textures. No longer used.
        public LLUUID TerrainBase0 = new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975"); // Default
        public LLUUID TerrainBase1 = new LLUUID("abb783e6-3e93-26c0-248a-247666855da3"); // Default
        public LLUUID TerrainBase2 = new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f"); // Default
        public LLUUID TerrainBase3 = new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2"); // Default
        // Higher resolution terrain textures
        public LLUUID TerrainDetail0 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID TerrainDetail1 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID TerrainDetail2 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID TerrainDetail3 = new LLUUID("00000000-0000-0000-0000-000000000000");
        // First quad - each point is bilinearly interpolated at each meter of terrain
        public float TerrainStartHeight00 = 10.0f;       // NW Corner ( I think )
        public float TerrainStartHeight01 = 10.0f;       // NE Corner ( I think )
        public float TerrainStartHeight10 = 10.0f;       // SW Corner ( I think )
        public float TerrainStartHeight11 = 10.0f;       // SE Corner ( I think )
        // Second quad - also bilinearly interpolated.
        // Terrain texturing is done that:
        // 0..3 (0 = base0, 3 = base3) = (terrain[x,y] - start[x,y]) / range[x,y]
        public float TerrainHeightRange00 = 60.0f;
        public float TerrainHeightRange01 = 60.0f;
        public float TerrainHeightRange10 = 60.0f;
        public float TerrainHeightRange11 = 60.0f;

        // Terrain Default (Must be in F32 Format!)
        public string TerrainFile = "default.r32";
        public double TerrainMultiplier = 60.0;


        public RegionInfoBase()
        {

        }
    }

}
