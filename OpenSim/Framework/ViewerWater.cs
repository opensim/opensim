/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public class WaterData
    {
        public UUID normalMap = new UUID("822ded49-9a6c-f61c-cb89-6df54f42cdf4");
        public UUID transpTexture = new UUID("2bfd3884-7e27-69b9-ba3a-3e673f680004");

        public float blurMultiplier = 0.04f;
        public float fresnelOffset = 0.5f;
        public float fresnelScale = 0.4f;
        public Vector3 normScale = new Vector3(2f, 2f, 2f);
        public float scaleAbove = 0.03f;
        public float scaleBelow = 0.2f;
        public float underWaterFogMod = 0.25f;
        public Vector3 waterFogColor = new Vector3(0.0156f, 0.149f, 0.2509f);
        public float waterFogDensity = 10;
        public Vector2 wave1Dir = new Vector2(1.05f, -0.42f);
        public Vector2 wave2Dir = new Vector2(1.11f, -1.16f);
        public string Name;

        public void FromWLOSD(string name, OSD osd)
        {
            Vector4 v4tmp;
            OSDMap map = osd as OSDMap;
            blurMultiplier = map["blurMultiplier"];
            fresnelOffset = map["fresnelOffset"];
            fresnelScale = map["fresnelScale"];
            normScale = map["normScale"];
            normalMap = map["normalMap"];
            scaleAbove = map["scaleAbove"];
            scaleBelow = map["scaleBelow"];
            underWaterFogMod = map["underWaterFogMod"];
            v4tmp = map["waterFogColor"];
            waterFogColor = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            waterFogDensity = map["waterFogDensity"];
            wave1Dir = map["wave1Dir"];
            wave2Dir = map["wave2Dir"];
            Name = name;
        }

        public OSDMap ToWLOSD()
        {
            OSDMap map = new OSDMap();

            map["blurMultiplier"] = blurMultiplier;
            map["fresnelOffset"] = fresnelOffset;
            map["fresnelScale"] = fresnelScale;
            map["normScale"] = normScale;
            map["normalMap"] = normalMap;
            map["scaleAbove"] = scaleAbove;
            map["scaleBelow"] = scaleBelow;
            map["underWaterFogMod"] = underWaterFogMod;
            map["waterFogColor"] = new Vector4(waterFogColor.X, waterFogColor.Y, waterFogColor.Z, 1);
            map["waterFogDensity"] = waterFogDensity;
            //map["waterFogDensity"] = MathF.Pow(2.0f, waterFogDensity);
            map["wave1Dir"] = wave1Dir;
            map["wave2Dir"] = wave2Dir;

            return map;
        }

        public void FromOSD(string name, OSDMap map)
        {
            OSD otmp;
            if (map.TryGetValue("blur_multiplier", out otmp))
                blurMultiplier = otmp;
            if (map.TryGetValue("fresnel_offset", out otmp))
                fresnelOffset = otmp;
            if (map.TryGetValue("fresnel_scale", out otmp))
                fresnelScale = otmp;
            if (map.TryGetValue("normal_scale", out otmp))
                normScale = otmp;
            if (map.TryGetValue("normal_map", out otmp))
                normalMap = otmp;
            if (map.TryGetValue("scale_above", out otmp))
                scaleAbove = otmp;
            if (map.TryGetValue("scale_below", out otmp))
                scaleBelow = otmp;
            if (map.TryGetValue("underwater_fog_mod", out otmp))
                underWaterFogMod = otmp;
            if (map.TryGetValue("water_fog_color", out otmp))
                waterFogColor = otmp;
            if (map.TryGetValue("water_fog_density", out otmp))
                waterFogDensity = otmp;
            if (map.TryGetValue("wave1_direction", out otmp))
                wave1Dir = otmp;
            if (map.TryGetValue("wave2_direction", out otmp))
                wave2Dir = otmp;
            if (map.TryGetValue("transparent_texture", out otmp))
                transpTexture = otmp;

            Name = name;
        }

        public OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();

            map["blur_multiplier"] = blurMultiplier;
            map["fresnel_offset"] = fresnelOffset;
            map["fresnel_scale"] = fresnelScale;
            map["normal_scale"] = normScale;
            map["normal_map"] = normalMap;
            map["scale_above"] = scaleAbove;
            map["scale_below"] = scaleBelow;
            map["underwater_fog_mod"] = underWaterFogMod;
            map["water_fog_color"] = waterFogColor;
            map["water_fog_density"] = waterFogDensity;
            map["wave1_direction"] = wave1Dir;
            map["wave2_direction"] = wave2Dir;
            map["transparent_texture"] = transpTexture;
            map["type"] ="water";

            return map;
        }

        public void GatherAssets(Dictionary<UUID, sbyte> uuids)
        {
            Util.AddToGatheredIds(uuids, normalMap, (sbyte)AssetType.Texture);
            Util.AddToGatheredIds(uuids, transpTexture, (sbyte)AssetType.Texture);
        }
    }
}
