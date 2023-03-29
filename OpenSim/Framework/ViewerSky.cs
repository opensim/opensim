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
                OSDMap map = new()
                {
                    ["constant_term"] = constant_term,
                    ["exp_scale"] = exp_scale,
                    ["exp_term"] = exp_term,
                    ["linear_term"] = linear_term,
                    ["width"] = width
                };
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
                OSDMap map = new()
                {
                    ["anisotropy"] = anisotropy,
                    ["constant_term"] = constant_term,
                    ["exp_scale"] = exp_scale,
                    ["exp_term"] = exp_term,
                    ["linear_term"] = linear_term,
                    ["width"] = width
                };
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
        public AbsCoefData abscoefA = new(25000f, 0, 0, 0, 0);
        public AbsCoefData abscoefB = new(0, 0, 0, -6.6666667e-5f, 1f);
        public AbsCoefData rayleigh_config = new(0,  1, -1.25e-4f, 0, 0);

        //mCoefData(float w, float expt, float exps, float lin, float cons, float ani)
        public mCoefData mieconf = new(0, 1f, -8.333333e-4f, 0, 0, 0.8f);

        UUID bloom_id = new("3c59f7fe-9dc8-47f9-8aaf-a9dd1fbc3bef");
        UUID cloud_id = new("1dc1368f-e8fe-f02d-a08d-9d9f11c1af6b");
        UUID halo_id = new("12149143-f599-91a7-77ac-b52a3c0f59cd");
        UUID moon_id = new("ec4b9f0b-d008-45c6-96a4-01dd947ac621");
        UUID rainbow_id = new("11b4c57c-56b3-04ed-1f82-2004363882e4");
        UUID sun_id = UUID.Zero;

        public Vector3 ambient = new(1.047f, 1.047f, 1.047f); //?
        public Vector3 blue_density = new(0.2447f, 0.4487f, 0.76f);
        public Vector3 blue_horizon = new(0.4954f, 0.4954f, 0.64f);
        public Vector3 cloud_color = new(0.41f, 0.41f, 0.41f);
        public Vector3 cloud_pos_density1 = new(1, 0.5260f, 1);
        public Vector3 cloud_pos_density2 = new(1, 0.5260f, 0.12f);
        public float cloud_scale = 0.42f;
        public Vector2 cloud_scroll_rate = new(0.2f, 0.011f);
        public float cloud_shadow = 0.27f;
        public float density_multiplier = 0.00018f;
        public float distance_multiplier = 0.8f;
        public float gamma = 1;
        public Vector3 glow = new(5, 0.0010f, -0.48f);
        public float haze_density = 0.7f;
        public float haze_horizon = 0.19f;
        public float max_y = 1605;
        public float star_brightness = 0f;

        //this is a vector3 now, but all viewers expect a vector4, so keeping like this for now
        public Vector4 sunlight_color = new(0.7342f, 0.7815f, 0.9f, 0.3f);
        public string Name = "DefaultSky";

        public float cloud_variance = 0;
        public float dome_offset = 0.96f;
        public float dome_radius = 15000f;
        public float droplet_radius = 800.0f;
        public float ice_level = 0f;
        public float reflectionProbeAmbiance = 0f;

        public float moisture_level = 0;
        public float sky_bottom_radius = 6360;
        public float sky_top_radius = 6420;

        public float sun_arc_radians = 0.00045f;
        public Quaternion sun_rotation = new(0, -0.3824995f, 0, 0.9239557f);
        public float sun_scale = 1;

        public float moon_brightness = 0.5f;
        public Quaternion moon_rotation = new(0, 0.9239557f, 0, 0.3824995f);
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

            reflectionProbeAmbiance = 0f;

            ViewerEnvironment.convertFromAngles(this, map["sun_angle"], map["east_angle"]);
            Name = name;
        }

        public OSD ToWLOSD()
        {
            OSDMap map = new();

            ViewerEnvironment.convertToAngles(this, out float sun_angle, out float east_angle, out Vector4 lightnorm);
            map["ambient"] = new Vector4(ambient.X, ambient.Y, ambient.Z, 1);
            map["blue_density"] = new Vector4(blue_density.X, blue_density.Y, blue_density.Z, 1);
            map["blue_horizon"] = new Vector4(blue_horizon.X, blue_horizon.Y, blue_horizon.Z, 1);
            map["cloud_color"] = new Vector4(cloud_color.X, cloud_color.Y, cloud_color.Z, 1);
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
            OSDMap map = new(64)
            {
                ["absorption_config"] = new OSDArray() { abscoefA.ToOSD(), abscoefB.ToOSD() },
                ["bloom_id"] = bloom_id,
                ["cloud_color"] = cloud_color,
                ["cloud_id"] = cloud_id,
                ["cloud_pos_density1"] = cloud_pos_density1,
                ["cloud_pos_density2"] = cloud_pos_density2,
                ["cloud_scale"] = cloud_scale,
                ["cloud_scroll_rate"] = cloud_scroll_rate,
                ["cloud_shadow"] = cloud_shadow,
                ["cloud_variance"] = cloud_variance,
                ["dome_offset"] = dome_offset,
                ["dome_radius"] = dome_radius,
                ["droplet_radius"] = droplet_radius,
                ["gamma"] = gamma,
                ["glow"] = glow,
                ["halo_id"] = halo_id,
                ["ice_level"] = ice_level,

                ["legacy_haze"] = new OSDMap()
                {
                    ["ambient"] = ambient,
                    ["blue_density"] = blue_density,
                    ["blue_horizon"] = blue_horizon,
                    ["density_multiplier"] = density_multiplier,
                    ["distance_multiplier"] = distance_multiplier,
                    ["haze_density"] = haze_density,
                    ["haze_horizon"] = haze_horizon
                },

                ["max_y"] = max_y,
                ["moisture_level"] = moisture_level,
                ["moon_brightness"] = moon_brightness,
                ["moon_id"] = moon_id,
                ["moon_rotation"] = moon_rotation,
                ["moon_scale"] = moon_scale,
                ["planet_radius"] = planet_radius,
                ["rainbow_id"] = rainbow_id,

                ["sky_bottom_radius"] = sky_bottom_radius,
                ["sky_top_radius"] = sky_top_radius,
                ["star_brightness"] = star_brightness,

                ["sun_arc_radians"] = sun_arc_radians,
                ["sun_id"] = sun_id,
                ["sun_rotation"] = sun_rotation,
                ["sun_scale"] = sun_scale,
                ["sunlight_color"] = sunlight_color,

                ["mie_config"] = new OSDArray() { mieconf.ToOSD() },
                ["rayleigh_config"] = new OSDArray() { rayleigh_config.ToOSD() },

                ["type"] = "sky"
            };

            if (reflectionProbeAmbiance != 0f)
                map["reflection_probe_ambiance"] = reflectionProbeAmbiance;

            return map;
        }

        public void FromOSD(string name, OSDMap map)
        {
            OSD otmp;
            if (map.TryGetValue("absorption_config", out otmp) && otmp is OSDArray absorptionArray)
            {
                if (absorptionArray.Count > 0)
                {
                    abscoefA.FromOSD(absorptionArray[0] as OSDMap);
                    if (absorptionArray.Count > 1)
                        abscoefA.FromOSD(absorptionArray[1] as OSDMap);
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
                ice_level = otmp;

            if (map.TryGetValue("reflection_probe_ambiance", out otmp))
                reflectionProbeAmbiance = otmp;

            if (map.TryGetValue("legacy_haze", out OSD tmp) && tmp is OSDMap lHaze)
            {
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

            if (map.TryGetValue("mie_config", out otmp) && otmp is OSDArray mieArray)
            {
                if (mieArray.Count > 0)
                    mieconf.FromOSD(mieArray[0] as OSDMap);
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

            if (map.TryGetValue("rayleigh_config", out otmp) && otmp is OSDArray rayleighArray)
            {
                if (rayleighArray.Count > 0)
                    rayleigh_config.FromOSD(rayleighArray[0] as OSDMap);
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

            if (map.TryGetValue("sunlight_color", out otmp) && otmp is OSDArray sunlightArray)
            {
                if(sunlightArray.Count == 4)
                    sunlight_color = otmp;
                else
                {
                    Vector3 tv = otmp;
                    sunlight_color = new Vector4(tv.X, tv.Y, tv.Z, 0);
                }
            }
            Name = name;
        }

        public void GatherAssets(Dictionary<UUID, sbyte> uuids)
        {
            Util.AddToGatheredIds(uuids, bloom_id, (sbyte)AssetType.Texture);
            Util.AddToGatheredIds(uuids, cloud_id, (sbyte)AssetType.Texture);
            Util.AddToGatheredIds(uuids, halo_id, (sbyte)AssetType.Texture);
            Util.AddToGatheredIds(uuids, moon_id, (sbyte)AssetType.Texture);
            Util.AddToGatheredIds(uuids, rainbow_id, (sbyte)AssetType.Texture);
            Util.AddToGatheredIds(uuids, sun_id, (sbyte)AssetType.Texture);
        }
    }
}
