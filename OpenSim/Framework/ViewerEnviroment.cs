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

using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    // legacy lightshare
    public class RegionLightShareData
    {
        public Vector3 waterColor = new Vector3(4.0f, 38.0f, 64.0f);
        public float waterFogDensityExponent = 4.0f;
        public float underwaterFogModifier = 0.25f;
        public Vector3 reflectionWaveletScale = new Vector3(2.0f, 2.0f, 2.0f);
        public float fresnelScale = 0.40f;
        public float fresnelOffset = 0.50f;
        public float refractScaleAbove = 0.03f;
        public float refractScaleBelow = 0.20f;
        public float blurMultiplier = 0.040f;
        public Vector2 bigWaveDirection = new Vector2(1.05f, -0.42f);
        public Vector2 littleWaveDirection = new Vector2(1.11f, -1.16f);
        public UUID normalMapTexture = new UUID("822ded49-9a6c-f61c-cb89-6df54f42cdf4");
        public Vector4 horizon = new Vector4(0.25f, 0.25f, 0.32f, 0.32f);
        public float hazeHorizon = 0.19f;
        public Vector4 blueDensity = new Vector4(0.12f, 0.22f, 0.38f, 0.38f);
        public float hazeDensity = 0.70f;
        public float densityMultiplier = 0.18f;
        public float distanceMultiplier = 0.8f;
        public UInt16 maxAltitude = 1605;
        public Vector4 sunMoonColor = new Vector4(0.24f, 0.26f, 0.30f, 0.30f);
        public float sunMoonPosition = 0.317f;
        public Vector4 ambient = new Vector4(0.35f, 0.35f, 0.35f, 0.35f);
        public float eastAngle = 0.0f;
        public float sunGlowFocus = 0.10f;
        public float sunGlowSize = 1.75f;
        public float sceneGamma = 1.0f;
        public float starBrightness = 0.0f;
        public Vector4 cloudColor = new Vector4(0.41f, 0.41f, 0.41f, 0.41f);
        public Vector3 cloudXYDensity = new Vector3(1.00f, 0.53f, 1.00f);
        public float cloudCoverage = 0.27f;
        public float cloudScale = 0.42f;
        public Vector3 cloudDetailXYDensity = new Vector3(1.00f, 0.53f, 0.12f);
        public float cloudScrollX = 0.20f;
        public bool cloudScrollXLock = false;
        public float cloudScrollY = 0.01f;
        public bool cloudScrollYLock = false;
        public bool drawClassicClouds = true;
    }

    public class SkyData
    {
        public struct AbsCoefData
        {
            public float constant_term;
            public float exp_scale;
            public float exp_term;
            public float linear_term;
            public float width;

            public AbsCoefData(float w, float expt, float exps, float lin, float cons)
            {
                constant_term = cons;
                exp_scale = exps;
                exp_term = expt;
                linear_term = lin;
                width = w;
            }

            public OSDMap ToOSD()
            {
                OSDMap map = new OSDMap();
                map["constant_term"] = constant_term;
                map["exp_scale"] = exp_scale;
                map["exp_term"] = exp_term;
                map["linear_term"] = linear_term;
                map["width"] = width;
                return map;
            }

            public void FromOSD(OSDMap map)
            {
                constant_term = map["constant_term"];
                exp_scale = map["exp_scale"];
                exp_term = map["exp_term"];
                linear_term = map["linear_term"];
                width = map["width"];
            }
        }

        public struct mCoefData
        {
            public float anisotropy;
            public float constant_term;
            public float exp_scale;
            public float exp_term;
            public float linear_term;
            public float width;

            public mCoefData(float w, float expt, float exps, float lin, float cons, float ani)
            {
                anisotropy = ani;
                constant_term = cons;
                exp_scale = exps;
                exp_term = expt;
                linear_term = lin;
                width = w;
            }

            public OSDMap ToOSD()
            {
                OSDMap map = new OSDMap();
                map["anisotropy"] = anisotropy;
                map["constant_term"] = constant_term;
                map["exp_scale"] = exp_scale;
                map["exp_term"] = exp_term;
                map["linear_term"] = linear_term;
                map["width"] = width;
                return map;
            }

            public void FromOSD(OSDMap map)
            {
                anisotropy = map["anisotropy"];
                constant_term = map["constant_term"];
                exp_scale = map["exp_scale"];
                exp_term = map["exp_term"];
                linear_term = map["linear_term"];
                width = map["width"];
            }
        }
        //AbsCoefData(float w, float expt, float exps, float lin, float cons)
        public AbsCoefData abscoefA = new AbsCoefData(25000f, 0, 0, 0, 0);
        public AbsCoefData abscoefB = new AbsCoefData(0, 0, 0, -6.6666667e-5f, 1f);
        public AbsCoefData rayleigh_config = new AbsCoefData(0,  1, -1.25e-4f, 0, 0);

        //mCoefData(float w, float expt, float exps, float lin, float cons, float ani)
        public mCoefData mieconf = new mCoefData(0, 1f, -8.333333e-4f, 0, 0, 0.8f);

        UUID bloom_id = new UUID("3c59f7fe-9dc8-47f9-8aaf-a9dd1fbc3bef");
        UUID cloud_id = new UUID("1dc1368f-e8fe-f02d-a08d-9d9f11c1af6b");
        UUID halo_id = new UUID("12149143-f599-91a7-77ac-b52a3c0f59cd");
        UUID moon_id = new UUID("ec4b9f0b-d008-45c6-96a4-01dd947ac621");
        UUID rainbow_id = new UUID("11b4c57c-56b3-04ed-1f82-2004363882e4");
        UUID sun_id = UUID.Zero;

        public Vector3 ambient = new Vector3(0.25f, 0.25f, 0.25f); //?
        public Vector3 blue_density = new Vector3(0.2447f, 0.4487f, 0.76f);
        public Vector3 blue_horizon = new Vector3(0.4954f, 0.4954f, 0.64f);
        public Vector3 cloud_color = new Vector3(0.41f, 0.41f, 0.41f);
        public Vector3 cloud_pos_density1 = new Vector3(1, 0.5260f, 1);
        public Vector3 cloud_pos_density2 = new Vector3(1, 0.5260f, 1);
        public float cloud_scale = 0.42f;
        public Vector2 cloud_scroll_rate = new Vector2(0.2f, 0.011f);
        public float cloud_shadow = 0.27f;
        public float density_multiplier = 0.00018f;
        public float distance_multiplier = 0.8f;
        public float gamma = 1;
        public Vector3 glow = new Vector3(5, 0.0010f, -0.48f);
        public float haze_density = 0.7f;
        public float haze_horizon = 0.19f;
        public float max_y = 1605;
        public float star_brightness = 0f;

        public Vector4 sunlight_color = new Vector4(0.7342f, 0.7815f, 0.9f, 0.3f);
        public string Name = "Default";

        public float cloud_variance = 0;
        public float dome_offset = 0.96f;
        public float dome_radius = 15000f;
        public float droplet_radius = 800.0f;
        public float ice_level = 0;

        public float moisture_level = 0;
        public float sky_bottom_radius = 6360;
        public float sky_top_radius = 6420;

        public float sun_arc_radians = 0.00045f;
        public Quaternion sun_rotation = new Quaternion(0, -0.3824995f, 0, 0.9239557f);
        public float sun_scale = 1;

        public float moon_brightness = 0.5f;
        public Quaternion moon_rotation = new Quaternion(0, 0.9239557f, 0, 0.3824995f);
        public float moon_scale = 1;
        public float planet_radius = 6360f;

        public void FromWLOSD(string name, OSD osd)
        {
            Vector4 v4tmp;
            OSDMap map = osd as OSDMap;

            v4tmp = map["ambient"];
            ambient = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["blue_density"];
            blue_density = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["blue_horizon"];
            blue_horizon = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["cloud_color"];
            cloud_color = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["cloud_pos_density1"];
            cloud_pos_density1 = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["cloud_pos_density2"];
            cloud_pos_density2 = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["cloud_scale"];
            cloud_scale = v4tmp.X;
            cloud_scroll_rate = map["cloud_scroll_rate"];
            cloud_scroll_rate.X -= 10f;
            cloud_scroll_rate.Y -= 10f;
            v4tmp = map["cloud_shadow"];
            cloud_shadow = v4tmp.X;
            v4tmp = map["density_multiplier"];
            density_multiplier = v4tmp.X;
            v4tmp = map["distance_multiplier"];
            distance_multiplier = v4tmp.X;

            Vector2 v2tmp = map["enable_cloud_scroll"];
            if (v2tmp.X == 0)
                cloud_scroll_rate.X = 0;
            if (v2tmp.Y == 0)
                cloud_scroll_rate.Y = 0;
            v4tmp = map["gamma"];
            gamma = v4tmp.X;
            v4tmp = map["glow"];
            glow = new Vector3(v4tmp.X, v4tmp.Y, v4tmp.Z);
            v4tmp = map["haze_density"];
            haze_density = v4tmp.X;
            v4tmp = map["haze_horizon"];
            haze_horizon = v4tmp.X;
            //lightnorm = map["lightnorm"];
            v4tmp = map["max_y"];
            max_y = v4tmp.X;
            star_brightness = map["star_brightness"] * 250.0f;

            sunlight_color = map["sunlight_color"];

            ViewerEnviroment.convertFromAngles(this, map["sun_angle"], map["east_angle"]);
            Name = name;
        }

        public OSD ToWLOSD()
        {
            OSDMap map = new OSDMap();

            float sun_angle;
            float east_angle;
            Vector4 lightnorm;
            ViewerEnviroment.convertToAngles(this, out sun_angle, out east_angle, out lightnorm);

            map["ambient"] = new Vector4(ambient.X, ambient.Y, ambient.Z, 1);
            map["blue_density"] = new Vector4(blue_density.X, blue_density.Y, blue_density.Z, 1);
            map["blue_horizon"] = new Vector4(blue_horizon.X, blue_horizon.Y, blue_horizon.Z, 1);
            map["cloud_color"] = new Vector4(cloud_color.X, cloud_color.Y, cloud_color.Z, 1);;
            map["cloud_pos_density1"] = new Vector4(cloud_pos_density1.X, cloud_pos_density1.Y, cloud_pos_density1.Z, 1);
            map["cloud_pos_density2"] = new Vector4(cloud_pos_density2.X, cloud_pos_density2.Y, cloud_pos_density2.Z, 1);
            map["cloud_scale"] = new Vector4(cloud_scale, 0, 0, 1);
            map["cloud_scroll_rate"] = new Vector2(cloud_scroll_rate.X + 10f, cloud_scroll_rate.Y + 10f);
            map["cloud_shadow"] = new Vector4(cloud_shadow, 0, 0, 1);
            map["density_multiplier"] = new Vector4(density_multiplier, 0, 0, 1);
            map["distance_multiplier"] = new Vector4(distance_multiplier, 0, 0, 1);
            map["east_angle"] = east_angle;
            map["enable_cloud_scroll"] = new OSDArray { cloud_scroll_rate.X != 0, cloud_scroll_rate.Y != 0 };
            map["gamma"] = new Vector4(gamma, 0, 0, 1);
            map["glow"] = new Vector4(glow.X, glow.Y, glow.Z, 1);
            map["haze_density"] = new Vector4(haze_density, 0, 0, 1);
            map["haze_horizon"] = new Vector4(haze_horizon, 0, 0, 1);
            map["lightnorm"] = lightnorm;
            map["max_y"] = new Vector4(max_y, 0, 0, 1);
            map["name"] = Name;
            map["star_brightness"] = star_brightness / 250.0f;
            map["sun_angle"] = sun_angle;
            map["sunlight_color"] = sunlight_color;

            return map;
        }

        public OSD ToOSD()
        {
            OSDMap map = new OSDMap(64);

            OSDArray abscfg = new OSDArray(2);
            abscfg.Add(abscoefA.ToOSD());
            abscfg.Add(abscoefB.ToOSD());
            map["absorption_config"] = abscfg;

            map["bloom_id"] = bloom_id;
            map["cloud_color"] = cloud_color;
            map["cloud_id"] = cloud_id;
            map["cloud_pos_density1"] = cloud_pos_density1;
            map["cloud_pos_density2"] = cloud_pos_density2;
            map["cloud_scale"] = cloud_scale;
            map["cloud_scroll_rate"] = cloud_scroll_rate;
            map["cloud_shadow"] = cloud_shadow;
            map["cloud_variance"] = cloud_variance;
            map["dome_offset"] = dome_offset;
            map["dome_radius"] = dome_radius;
            map["droplet_radius"] = droplet_radius;
            map["gamma"] = gamma;
            map["glow"] = glow;
            map["halo_id"] = halo_id;
            map["ice_level"] = ice_level;

            OSDMap lhaze = new OSDMap();
            lhaze["ambient"] = ambient;
            lhaze["blue_density"] = blue_density;
            lhaze["blue_horizon"] = blue_horizon;
            lhaze["density_multiplier"] = density_multiplier;
            lhaze["distance_multiplier"] = distance_multiplier;
            lhaze["haze_density"] = haze_density;
            lhaze["haze_horizon"] = haze_horizon;
            map["legacy_haze"] = lhaze;

            map["max_y"] = max_y;

            OSDArray miecfg = new OSDArray();
            miecfg.Add(mieconf.ToOSD());
            map["mie_config"] = miecfg;

            map["moisture_level"] = moisture_level;
            map["moon_brightness"] = moon_brightness;
            map["moon_id"] = moon_id;
            map["moon_rotation"] = moon_rotation;
            map["moon_scale"] = moon_scale;
            map["planet_radius"] = planet_radius;
            map["rainbow_id"] = rainbow_id;

            OSDArray rayl = new OSDArray();
            rayl.Add(rayleigh_config.ToOSD());
            map["rayleigh_config"] = rayl;

            map["sky_bottom_radius"] = sky_bottom_radius;
            map["sky_top_radius"] = sky_top_radius;
            map["star_brightness"] = star_brightness;

            map["sun_arc_radians"] = sun_arc_radians;
            map["sun_id"] = sun_id;
            map["sun_rotation"] = sun_rotation;
            map["sun_scale"] = sun_scale;
            map["sunlight_color"] = sunlight_color;

            map["type"] = "sky";
            return map;
        }

        public void FromOSD(string name, OSDMap map)
        {
            OSDArray tmpArray;
            OSD otmp;
            if (map.TryGetValue("absorption_config",out otmp) && otmp is OSDArray)
            {
                tmpArray = otmp as OSDArray;
                if (tmpArray.Count > 0)
                {
                    abscoefA.FromOSD(tmpArray[0] as OSDMap);
                    if (tmpArray.Count > 1)
                        abscoefA.FromOSD(tmpArray[1] as OSDMap);
                }
            }
            if (map.TryGetValue("bloom_id", out otmp))
                bloom_id = otmp;
            if (map.TryGetValue("cloud_color", out otmp))
                cloud_color = otmp;
            if (map.TryGetValue("cloud_id", out otmp))
                cloud_id = otmp;
            if (map.TryGetValue("cloud_pos_density1", out otmp))
                cloud_pos_density1 = otmp;
            if (map.TryGetValue("cloud_pos_density2", out otmp))
                cloud_pos_density2 = otmp;
            if (map.TryGetValue("cloud_scale", out otmp))
                cloud_scale = otmp;
            if (map.TryGetValue("cloud_scroll_rate", out otmp))
                cloud_scroll_rate = otmp;
            if (map.TryGetValue("cloud_shadow", out otmp))
                cloud_shadow = otmp;
            if (map.TryGetValue("cloud_variance", out otmp))
                cloud_variance = otmp;
            if (map.TryGetValue("dome_offset", out otmp))
                dome_offset = otmp;
            if (map.TryGetValue("dome_radius", out otmp))
                dome_radius = otmp;
            if (map.TryGetValue("droplet_radius", out otmp))
                droplet_radius = otmp;
            if (map.TryGetValue("gamma", out otmp))
                gamma = otmp;
            if (map.TryGetValue("glow", out otmp))
                glow = otmp;
            if (map.TryGetValue("halo_id", out otmp))
                halo_id = otmp;
            if (map.TryGetValue("ice_level", out otmp))
                halo_id = otmp;

            if (map.TryGetValue("legacy_haze", out OSD tmp) && tmp is OSDMap)
            {
                OSDMap lHaze = tmp as OSDMap;
                if (lHaze.TryGetValue("ambient", out otmp))
                    ambient = otmp;
                if (lHaze.TryGetValue("blue_density", out otmp))
                    blue_density = otmp;
                if (lHaze.TryGetValue("blue_horizon", out otmp))
                    blue_horizon = otmp;
                if (lHaze.TryGetValue("density_multiplier", out otmp))
                    density_multiplier = otmp;
                if (lHaze.TryGetValue("distance_multiplier", out otmp))
                    distance_multiplier = otmp;
                if (lHaze.TryGetValue("haze_density", out otmp))
                    haze_density = otmp;
                if (lHaze.TryGetValue("haze_horizon", out otmp))
                    haze_horizon = otmp;
            }

            if (map.TryGetValue("max_y", out otmp))
                max_y = otmp;

            if (map.TryGetValue("mie_config", out otmp) && otmp is OSDArray)
            {
                tmpArray = otmp as OSDArray;
                if (tmpArray.Count > 0)
                    mieconf.FromOSD(tmpArray[0] as OSDMap);
            }

            if (map.TryGetValue("moisture_level", out otmp))
                moisture_level = otmp;
            if (map.TryGetValue("moon_brightness", out otmp))
                moon_brightness = otmp;
            if (map.TryGetValue("moon_id", out otmp))
                moon_id = otmp;
            if (map.TryGetValue("moon_rotation", out otmp))
                moon_rotation = otmp;
            if (map.TryGetValue("moon_scale", out otmp))
                moon_scale = otmp;
            if (map.TryGetValue("planet_radius", out otmp))
                planet_radius = otmp;
            if (map.TryGetValue("rainbow_id", out otmp))
                rainbow_id = otmp;

            if (map.TryGetValue("rayleigh_config", out otmp) && otmp is OSDArray)
            {
                tmpArray = otmp as OSDArray;
                if (tmpArray.Count > 0)
                    rayleigh_config.FromOSD(tmpArray[0] as OSDMap);
            }

            if (map.TryGetValue("sky_bottom_radius", out otmp))
                sky_bottom_radius = otmp;
            if (map.TryGetValue("sky_top_radius", out otmp))
                sky_top_radius = otmp;
            if (map.TryGetValue("star_brightness", out otmp))
                star_brightness = otmp;

            if (map.TryGetValue("sun_arc_radians", out otmp))
                sun_arc_radians = otmp;
            if (map.TryGetValue("sun_id", out otmp))
                sun_id = otmp;
            if (map.TryGetValue("sun_rotation", out otmp))
                sun_rotation = otmp;
            if (map.TryGetValue("sun_scale", out otmp))
                sun_scale = otmp;
            if (map.TryGetValue("sunlight_color", out otmp))
                sunlight_color = otmp;
            Name = name;
        }
    }

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
        public float waterFogDensity = 2;
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
            //map["waterFogDensity"] = (float)Math.Pow(2.0f, waterFogDensity);
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
    }

    public class DayCycle
    {
        public struct TrackEntry
        {
            public float time;
            public string frameName;

            public TrackEntry(float t, string f)
            {
                time = t;
                frameName = f;
            }
        }

        public bool IsStaticDayCycle = false;
        public List<TrackEntry> skyTrack0 = new List<TrackEntry>();
        public List<TrackEntry> skyTrack1 = new List<TrackEntry>();
        public List<TrackEntry> skyTrack2 = new List<TrackEntry>();
        public List<TrackEntry> skyTrack3 = new List<TrackEntry>();
        public List<TrackEntry> waterTrack = new List<TrackEntry>();

        public Dictionary<string, SkyData> skyframes = new Dictionary<string, SkyData>();
        public Dictionary<string, WaterData> waterframes = new Dictionary<string, WaterData>();

        public string Name;

        public void FromWLOSD(OSDArray array)
        {
            TrackEntry track;

            OSDArray skytracksArray = null;
            if (array.Count > 1)
                skytracksArray = array[1] as OSDArray;
            if(skytracksArray != null)
            {
                foreach (OSD setting in skytracksArray)
                {
                    OSDArray innerSetting = setting as OSDArray;
                    if(innerSetting != null)
                    {
                        track = new TrackEntry((float)innerSetting[0].AsReal(), innerSetting[1].AsString());
                        skyTrack0.Add(track);
                    }
                }
            }

            OSDMap skyFramesArray = null;
            if (array.Count > 2)
                skyFramesArray = array[2] as OSDMap;
            if(skyFramesArray != null)
            {
                foreach (KeyValuePair<string, OSD> kvp in skyFramesArray)
                {
                    SkyData sky = new SkyData();
                    sky.FromWLOSD(kvp.Key, kvp.Value);
                    skyframes[kvp.Key] = sky;
                }
            }

            WaterData water = new WaterData();
            OSDMap watermap = null;
            if(array.Count > 3)
                watermap = array[3] as OSDMap;
            if(watermap != null)
                water.FromWLOSD("WLWater", watermap);

            waterframes["WLWater"] = water;
            track = new TrackEntry(-1f, "WLWater");
            waterTrack.Add(track);

            if (skyTrack0.Count == 1 && skyTrack0[0].time == -1f)
                IsStaticDayCycle = true;
        }

        public void ToWLOSD(ref OSDArray array)
        {
            OSDArray track = new OSDArray();
            foreach (TrackEntry te in skyTrack0)
            {
                track.Add(new OSDArray { te.time, te.frameName });
            }

            array[1] = track;

            OSDMap frames = new OSDMap();
            foreach (KeyValuePair<string, SkyData> kvp in skyframes)
            {
                frames[kvp.Key] = kvp.Value.ToWLOSD();
            }
            array[2] = frames;

            if(waterTrack.Count > 0)
            {
                TrackEntry te = waterTrack[0];
                if(waterframes.TryGetValue(te.frameName, out WaterData water))
                {
                    array[3] = water.ToWLOSD();
                }
            }
            else
                array[3] = new OSDMap();
        }

        public void FromOSD(OSDMap map)
        {
            OSD otmp;
            if(map.TryGetValue("frames", out otmp) && otmp is OSDMap)
            {
                OSDMap mframes = otmp as OSDMap;
                foreach(KeyValuePair<string, OSD> kvp in mframes)
                {
                    OSDMap v = kvp.Value as OSDMap;
                    if(v.TryGetValue("type", out otmp))
                    {
                        string type = otmp;
                        if (type.Equals("water"))
                        {
                            WaterData water = new WaterData();
                            water.FromOSD(kvp.Key, v);
                            waterframes[kvp.Key] = water;
                        }
                        else if (type.Equals("sky"))
                        {
                            SkyData sky = new SkyData();
                            sky.FromOSD(kvp.Key, v);
                            skyframes[kvp.Key] = sky;
                        }
                    }
                }
            }

            if (map.TryGetValue("name", out otmp))
                Name = otmp;
            else
                Name ="DayCycle";

            OSDArray track;
            if (map.TryGetValue("tracks", out otmp) && otmp is OSDArray)
            {
                OSDArray tracks = otmp as OSDArray;
                if(tracks.Count > 0)
                {
                    track = tracks[0] as OSDArray;
                    for(int i = 0; i < track.Count; ++i)
                    {
                        OSDMap d = track[i] as OSDMap;
                        if (d.TryGetValue("key_keyframe", out OSD dtime))
                        {
                            if (d.TryGetValue("key_name", out OSD dname))
                            {
                                TrackEntry t = new TrackEntry();
                                t.time = dtime;
                                t.frameName = dname;
                                waterTrack.Add(t);
                            }
                        }
                    }
                }
                if (tracks.Count > 1)
                {
                    track = tracks[1] as OSDArray;
                    for (int i = 0; i < track.Count; ++i)
                    {
                        OSDMap d = track[i] as OSDMap;
                        if (d.TryGetValue("key_keyframe", out OSD dtime))
                        {
                            if (d.TryGetValue("key_name", out OSD dname))
                            {
                                TrackEntry t = new TrackEntry();
                                t.time = dtime;
                                t.frameName = dname;
                                skyTrack0.Add(t);
                            }
                        }
                    }
                }
                if (tracks.Count > 2)
                {
                    track = tracks[2] as OSDArray;
                    for (int i = 0; i < track.Count; ++i)
                    {
                        OSDMap d = track[i] as OSDMap;
                        if (d.TryGetValue("key_keyframe", out OSD dtime))
                        {
                            if (d.TryGetValue("key_name", out OSD dname))
                            {
                                TrackEntry t = new TrackEntry();
                                t.time = dtime;
                                t.frameName = dname;
                                skyTrack1.Add(t);
                            }
                        }
                    }
                }
                if (tracks.Count > 3)
                {
                    track = tracks[3] as OSDArray;
                    for (int i = 0; i < track.Count; ++i)
                    {
                        OSDMap d = track[i] as OSDMap;
                        if (d.TryGetValue("key_keyframe", out OSD dtime))
                        {
                            if (d.TryGetValue("key_name", out OSD dname))
                            {
                                TrackEntry t = new TrackEntry();
                                t.time = dtime;
                                t.frameName = dname;
                                skyTrack2.Add(t);
                            }
                        }
                    }
                }
                if (tracks.Count > 4)
                {
                    track = tracks[4] as OSDArray;
                    for (int i = 0; i < track.Count; ++i)
                    {
                        OSDMap d = track[i] as OSDMap;
                        if (d.TryGetValue("key_keyframe", out OSD dtime))
                        {
                            if (d.TryGetValue("key_name", out OSD dname))
                            {
                                TrackEntry t = new TrackEntry();
                                t.time = dtime;
                                t.frameName = dname;
                                skyTrack3.Add(t);
                            }
                        }
                    }
                }
            }
        }

        public OSDMap ToOSD()
        {
            OSDMap cycle = new OSDMap();

            OSDMap frames = new OSDMap();
            foreach (KeyValuePair<string, WaterData> kvp in waterframes)
            {
                frames[kvp.Key] = kvp.Value.ToOSD();
            }
            foreach (KeyValuePair<string, SkyData> kvp in skyframes)
            {
                frames[kvp.Key] = kvp.Value.ToOSD();
            }
            cycle["frames"] = frames;

            cycle["name"] = Name;

            OSDArray tracks = new OSDArray();

            OSDArray track = new OSDArray();
            OSDMap tmp;
            foreach (TrackEntry te in waterTrack)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            track = new OSDArray();
            foreach (TrackEntry te in skyTrack0)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            track = new OSDArray();
            foreach (TrackEntry te in skyTrack1)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            track = new OSDArray();
            foreach (TrackEntry te in skyTrack2)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            track = new OSDArray();
            foreach (TrackEntry te in skyTrack3)
            {
                tmp = new OSDMap();
                if (te.time < 0)
                    tmp["key_keyframe"] = 0f;
                else
                    tmp["key_keyframe"] = te.time;
                tmp["key_name"] = te.frameName;
                track.Add(tmp);
            }
            tracks.Add(track);

            cycle["tracks"] = tracks;
            cycle["type"] = "daycycle";

            return cycle;
        }
    }

    public class ViewerEnviroment
    {
        DayCycle Cycle = new DayCycle();
        public int DayLength = 14400;
        public int DayOffset = 57600;
        public int Flags = 0;
 
        float[] Altitudes = new float[3] {1000f, 2000f, 3000f };

        //DayHash;
        public bool IsLegacy = false;
        public string DayCycleName;

        public int version = 0;

        public void FromWLOSD(OSD osd)
        {
            OSDArray array = osd as OSDArray;
            if(osd != null)
            {
                Cycle = new DayCycle();
                Cycle.FromWLOSD(array);
                IsLegacy = true;
                Altitudes[0] = 3980f;
                Altitudes[1] = 3990f;
                Altitudes[2] = 4000f;
            }
        }

        public OSD ToWLOSD(UUID message, UUID region)
        {
            OSDArray array = new OSDArray(4) { null, null, null, null };
            array[0] = new OSDMap { {"messageID", message }, { "regionID", region } };
            Cycle.ToWLOSD(ref array);
            return array;
        }

        private static Quaternion AzAlToRot(float az, float al)
        {
            if (al == 0)
            {
                az *= 0.5f;
                return new Quaternion(0, 0, (float)Math.Sin(az), (float)Math.Cos(az));
            }

            float sT = (float)Math.Sin(az);
            float cT = (float)Math.Cos(az);
            float sP = (float)Math.Sin(al);
            float cP = (float)Math.Cos(al);

            float angle = (float)Math.Acos(cT * cP);
            Vector3 axis = new Vector3( 0, -sP, sT * cP);
            axis.Normalize();

            return Quaternion.CreateFromAxisAngle(axis, angle);
        }

        public static void convertFromAngles(SkyData sky, float sun_angle, float east_angle)
        {
            float az = -east_angle;
            float al = sun_angle;

            sky.sun_rotation = AzAlToRot(az, al);
            sky.moon_rotation = AzAlToRot(az + (float)Math.PI, -al);
        }

        public static Vector3 Xrot(Quaternion rot)
        {
            Vector3 vec;
            rot.Normalize(); // just in case
            vec.X = 2 * (rot.X * rot.X + rot.W * rot.W) - 1;
            vec.Y = 2 * (rot.X * rot.Y + rot.Z * rot.W);
            vec.Z = 2 * (rot.X * rot.Z - rot.Y * rot.W);
            return vec;
        }

        public static void convertToAngles(SkyData sky, out float sun_angle, out float east_angle, out Vector4 lightnorm)
        {
            Vector3 v = Xrot(sky.sun_rotation);
            lightnorm = new Vector4(v.X, v.Y, v.Z,1);
            sun_angle = (float)Math.Asin(v.Z);
            east_angle = -(float)Math.Atan2(v.Y, v.X);

            if (Math.Abs(sun_angle) < 1e-6)
                sun_angle = 0;
            if (Math.Abs(east_angle) < 1e-6)
                east_angle = 0;
            else if (east_angle < 0)
                east_angle = 2f * (float)Math.PI + east_angle;
        }

        public void FromLightShare(RegionLightShareData ls)
        {
            WaterData water = new WaterData();

            water.waterFogColor = ls.waterColor / 256f;
            water.waterFogDensity = (float)Math.Pow(2.0f, ls.waterFogDensityExponent);
            //water.waterFogDensity = ls.waterFogDensityExponent;
            water.underWaterFogMod = ls.underwaterFogModifier;
            water.normScale = ls.reflectionWaveletScale;
            water.fresnelScale = ls.fresnelScale;
            water.fresnelOffset = ls.fresnelOffset;
            water.scaleAbove = ls.refractScaleAbove;
            water.scaleBelow = ls.refractScaleBelow;
            water.blurMultiplier = ls.blurMultiplier;
            water.wave1Dir = ls.littleWaveDirection;
            water.wave2Dir = ls.bigWaveDirection;
            water.normalMap = ls.normalMapTexture;
            water.Name = "LightshareWater";

            SkyData sky = new SkyData();
            convertFromAngles(sky, 2.0f * (float)Math.PI * ls.sunMoonPosition, 2.0f * (float)Math.PI * ls.eastAngle);
            sky.sunlight_color = ls.sunMoonColor * 3.0f;
            sky.ambient = new Vector3(ls.ambient.X * 3.0f, ls.ambient.Y * 3.0f, ls.ambient.Z * 3.0f);
            sky.blue_horizon = new Vector3(ls.horizon.X * 2.0f, ls.horizon.Y * 2.0f, ls.horizon.Z * 2.0f);
            sky.blue_density = new Vector3(ls.blueDensity.X * 2.0f, ls.blueDensity.Y * 2.0f, ls.blueDensity.Z * 2.0f);;
            sky.haze_horizon = ls.hazeHorizon;
            sky.haze_density = ls.hazeDensity;
            sky.cloud_shadow = ls.cloudCoverage;
            sky.density_multiplier = ls.densityMultiplier / 1000.0f;
            sky.distance_multiplier = ls.distanceMultiplier;
            sky.max_y = ls.maxAltitude;
            sky.cloud_color = new Vector3(ls.cloudColor.X, ls.cloudColor.Y, ls.cloudColor.Z);
            sky.cloud_pos_density1 = ls.cloudXYDensity;
            sky.cloud_pos_density2 = ls.cloudDetailXYDensity;
            sky.cloud_scale = ls.cloudScale;
            sky.gamma=ls.sceneGamma;
            sky.glow = new Vector3((2f - ls.sunGlowSize) * 20f, 0f, -ls.sunGlowFocus * 5f);
            sky.cloud_scroll_rate = new Vector2(ls.cloudScrollX, ls.cloudScrollY);
            if (ls.cloudScrollXLock)
                sky.cloud_scroll_rate.X = 0;
            if (ls.cloudScrollYLock)
                sky.cloud_scroll_rate.Y = 0;
            sky.star_brightness = ls.starBrightness * 250f;
            sky.Name = "LightshareSky";

            Cycle = new DayCycle();
            Cycle.Name = "Lightshare";
            Cycle.waterframes.Add(water.Name, water);
            DayCycle.TrackEntry track = new DayCycle.TrackEntry(-1, water.Name);
            Cycle.waterTrack.Add(track);

            Cycle.skyframes.Add(sky.Name, sky);
            track = new DayCycle.TrackEntry(-1, sky.Name);
            Cycle.skyTrack0.Add(track);

            Altitudes[0] = 3980f;
            Altitudes[1] = 3990f;
            Altitudes[2] = 4000f;
        }

        public RegionLightShareData ToLightShare()
        {
            RegionLightShareData ls = new RegionLightShareData();

            DayCycle.TrackEntry te;
            if (Cycle.waterTrack.Count > 0)
            {
                te = Cycle.waterTrack[0];
                if (Cycle.waterframes.TryGetValue(te.frameName, out WaterData water))
                {
                    ls.waterColor = water.waterFogColor * 256f;
                    ls.waterFogDensityExponent = (float)Math.Sqrt(water.waterFogDensity);
                    //ls.waterFogDensityExponent = water.waterFogDensity;
                    ls.underwaterFogModifier = water.underWaterFogMod;
                    ls.reflectionWaveletScale = water.normScale;
                    ls.fresnelScale = water.fresnelScale;
                    ls.fresnelOffset = water.fresnelOffset;
                    ls.refractScaleAbove = water.scaleAbove;
                    ls.refractScaleBelow = water.scaleBelow;
                    ls.blurMultiplier = water.blurMultiplier;
                    ls.littleWaveDirection = water.wave1Dir;
                    ls.bigWaveDirection = water.wave2Dir;
                    ls.normalMapTexture = water.normalMap;
                }
            }

            if (Cycle.skyTrack0.Count > 0)
            {
                te = Cycle.skyTrack0[0];
                if (Cycle.skyframes.TryGetValue(te.frameName, out SkyData sky))
                {
                    Vector4 lightnorm;
                    convertToAngles(sky, out ls.sunMoonPosition, out ls.eastAngle, out lightnorm);
                    ls.sunMoonPosition *= 0.5f / (float)Math.PI;
                    ls.eastAngle *= 0.5f / (float)Math.PI;
                    ls.sunMoonColor = sky.sunlight_color / 3f;
                    ls.ambient = new Vector4(sky.ambient.X / 3.0f, sky.ambient.Y / 3.0f, sky.ambient.Z / 3.0f, 1);
                    ls.horizon = new Vector4(sky.blue_horizon.X / 2.0f, sky.blue_horizon.Y / 2.0f, sky.blue_horizon.Z / 2.0f, 1);
                    ls.blueDensity = new Vector4(sky.blue_density.X / 2.0f, sky.blue_density.Y / 2.0f, sky.blue_density.Z / 2.0f, 1);
                    ls.hazeHorizon = sky.haze_horizon;
                    ls.hazeDensity = sky.haze_density;
                    ls.cloudCoverage = sky.cloud_shadow;
                    ls.densityMultiplier = 1000f * sky.density_multiplier;
                    ls.distanceMultiplier = sky.distance_multiplier;
                    ls.maxAltitude = (ushort)sky.max_y;
                    ls.cloudColor = new Vector4(sky.cloud_color.X, sky.cloud_color.Y, sky.cloud_color.Z, 1);
                    ls.cloudXYDensity = sky.cloud_pos_density1;
                    ls.cloudDetailXYDensity = sky.cloud_pos_density2;
                    ls.cloudScale = sky.cloud_scale;
                    ls.sceneGamma = sky.gamma;
                    ls.sunGlowSize = (2f - sky.glow.X) / 20f;
                    ls.sunGlowFocus = -sky.glow.Z / 5f;
                    ls.cloudScrollX = sky.cloud_scroll_rate.X;
                    ls.cloudScrollY = sky.cloud_scroll_rate.Y;
                    ls.cloudScrollXLock = ls.cloudScrollX == 0f;
                    ls.cloudScrollYLock = ls.cloudScrollY == 0f;
                    ls.starBrightness = sky.star_brightness / 250f;
                }
            }
            return ls;
        }

        public void FromOSD(OSD osd)
        {
            OSDMap map = osd as OSDMap;
            if (map == null)
                return;

            OSD otmp;

            if (map.TryGetValue("day_cycle", out otmp) && otmp is OSDMap)
            {
                Cycle = new DayCycle();
                Cycle.FromOSD(otmp as OSDMap);
            }
            if (Cycle == null)
                Cycle = new DayCycle();

            if (map.TryGetValue("day_length", out otmp))
                DayLength = otmp;
            if (map.TryGetValue("day_offset", out otmp))
                DayOffset = otmp;
            if (map.TryGetValue("flags", out otmp))
                Flags = otmp;
            if (map.TryGetValue("env_version", out otmp))
                version = otmp;
            else
                ++version;

            if (map.TryGetValue("track_altitudes", out otmp) && otmp is OSDArray)
            {
                OSDArray alt = otmp as OSDArray;

                for(int i = 0; i < alt.Count && i < 3; ++i)
                {
                    Altitudes[i] = alt[i];
                }
            }

            IsLegacy = false;
        }

        public void CycleFromOSD(OSD osd)
        {
            OSDMap map = osd as OSDMap;
            if (map == null)
                return;
            if(!map.TryGetValue("type", out OSD tmp))
                return;
            string type = tmp.AsString();
            if(type != "daycycle")
                return;
            Cycle = new DayCycle();
            Cycle.FromOSD(map);
        }

        public OSD ToOSD()
        {
            OSDMap env = new OSDMap();
            env["day_cycle"] = Cycle.ToOSD();
            env["day_length"] = DayLength;
            env["day_offset"] = DayOffset;
            env["flags"] = Flags;
            env["env_version"] = version;

            OSDArray alt = new OSDArray();
            alt.Add(Altitudes[0]);
            alt.Add(Altitudes[1]);
            alt.Add(Altitudes[2]);
            env["track_altitudes"] = alt;
            return env;
        }

        public static OSD DefaultToOSD(UUID regionID, int parcel)
        {
            OSDMap top = new OSDMap();
            OSDMap env = new OSDMap();
            env["is_default"] = true;
            if (parcel >= 0)
                env["parcel_id"] = parcel;
            env["region_id"] = regionID;
            OSDArray alt = new OSDArray();
            alt.Add(1000f);
            alt.Add(2000f);
            alt.Add(3000f);
            env["track_altitudes"] = alt;
            top["environment"] = env;
            if (parcel >= 0)
                top["parcel_id"] = parcel;
            top["success"] = true;
            return top;
        }
    }
}
