/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using libsecondlife;

namespace OpenSim.RegionServer
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
        public LLUUID RegionOwner = new LLUUID();

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
