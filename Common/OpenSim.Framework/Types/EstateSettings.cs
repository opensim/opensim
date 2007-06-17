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

using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class EstateSettings
    {
        //Settings to this island
        public float billableFactor = (float)0.0;
        public uint estateID = 0;
        public uint parentEstateID = 0;

        public byte maxAgents = 40;
        public float objectBonusFactor = (float)1.0;

        public int redirectGridX = 0; //??
        public int redirectGridY = 0; //??
        public libsecondlife.Simulator.RegionFlags regionFlags = libsecondlife.Simulator.RegionFlags.None; //Booleam values of various region settings
        public libsecondlife.Simulator.SimAccess simAccess = libsecondlife.Simulator.SimAccess.Mature; //Is sim PG, Mature, etc? Mature by default.
        public float sunHour = 0;

        public float terrainRaiseLimit = 0;
        public float terrainLowerLimit = 0;

        public bool useFixedSun = false;
        public int pricePerMeter = 1;

        public ushort regionWaterHeight = 20;
        public bool regionAllowTerraform = true;

        // Region Information
        // Low resolution 'base' textures. No longer used.
        public LLUUID terrainBase0 = new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975"); // Default
        public LLUUID terrainBase1 = new LLUUID("abb783e6-3e93-26c0-248a-247666855da3"); // Default
        public LLUUID terrainBase2 = new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f"); // Default
        public LLUUID terrainBase3 = new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2"); // Default

        // Higher resolution terrain textures
        public LLUUID terrainDetail0 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID terrainDetail1 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID terrainDetail2 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID terrainDetail3 = new LLUUID("00000000-0000-0000-0000-000000000000");

        // First quad - each point is bilinearly interpolated at each meter of terrain
        public float terrainStartHeight0 = 10.0f;      
        public float terrainStartHeight1 = 10.0f;      
        public float terrainStartHeight2 = 10.0f;       
        public float terrainStartHeight3 = 10.0f;       

        // Second quad - also bilinearly interpolated.
        // Terrain texturing is done that:
        // 0..3 (0 = base0, 3 = base3) = (terrain[x,y] - start[x,y]) / range[x,y]
        public float terrainHeightRange0 = 60.0f; //00
        public float terrainHeightRange1 = 60.0f; //01
        public float terrainHeightRange2 = 60.0f; //10
        public float terrainHeightRange3 = 60.0f; //11

        // Terrain Default (Must be in F32 Format!)
        public string terrainFile = "default.r32";
        public double terrainMultiplier = 60.0;
        public float waterHeight = (float)20.0;

        public LLUUID terrainImageID = LLUUID.Zero; // the assetID that is the current Map image for this region

    }
}
