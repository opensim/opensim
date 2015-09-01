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
using OpenSim.Framework;
using Nini.Config;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Holds drip rates and maximum burst rates for throttling with hierarchical
    /// token buckets. The maximum burst rates set here are hard limits and can
    /// not be overridden by client requests
    /// </summary>
    public sealed class ThrottleRates
    {
        /// <summary>Drip rate for resent packets</summary>
        public int Resend;
        /// <summary>Drip rate for terrain packets</summary>
        public int Land;
        /// <summary>Drip rate for wind packets</summary>
        public int Wind;
        /// <summary>Drip rate for cloud packets</summary>
        public int Cloud;
        /// <summary>Drip rate for task packets</summary>
        public int Task;
        /// <summary>Drip rate for texture packets</summary>
        public int Texture;
        /// <summary>Drip rate for asset packets</summary>
        public int Asset;

        /// <summary>Drip rate for the parent token bucket</summary>
        public int Total;

        /// <summary>Flag used to enable adaptive throttles</summary>
        public bool AdaptiveThrottlesEnabled;
 
        /// <summary>
        /// Set the minimum rate that the adaptive throttles can set. The viewer
        /// can still throttle lower than this, but the adaptive throttles will
        /// never decrease rates below this no matter how many packets are dropped
        /// </summary>
        public Int64 MinimumAdaptiveThrottleRate;
 
        /// <summary>Amount of the texture throttle to steal for the task throttle</summary>
        public double CannibalizeTextureRate;

        public int ClientMaxRate;
        public float BrustTime;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="config">Config source to load defaults from</param>
        public ThrottleRates(IConfigSource config)
        {
            try
            {
                IConfig throttleConfig = config.Configs["ClientStack.LindenUDP"];

                // Current default total is 66750
                Resend = throttleConfig.GetInt("resend_default", 6625);
                Land = throttleConfig.GetInt("land_default", 9125);
                Wind = throttleConfig.GetInt("wind_default", 1750);
                Cloud = throttleConfig.GetInt("cloud_default", 1750);
                Task = throttleConfig.GetInt("task_default", 18500);
                Texture = throttleConfig.GetInt("texture_default", 18500);
                Asset = throttleConfig.GetInt("asset_default", 10500);

                Total = Resend + Land + Wind + Cloud + Task + Texture + Asset;
                // 3000000 bps default max
                ClientMaxRate = throttleConfig.GetInt("client_throttle_max_bps", 375000);
                if (ClientMaxRate > 1000000)
                    ClientMaxRate = 1000000; // no more than 8Mbps

                BrustTime = (float)throttleConfig.GetInt("client_throttle_burtsTimeMS", 10);
                BrustTime *= 1e-3f;

                AdaptiveThrottlesEnabled = throttleConfig.GetBoolean("enable_adaptive_throttles", false);
                MinimumAdaptiveThrottleRate = throttleConfig.GetInt("adaptive_throttle_min_bps", 32000);

                CannibalizeTextureRate = (double)throttleConfig.GetFloat("CannibalizeTextureRate", 0.0f);
                CannibalizeTextureRate = Util.Clamp<double>(CannibalizeTextureRate,0.0, 0.9);
            }
            catch (Exception) { }
        }

        public int GetRate(ThrottleOutPacketType type)
        {
            switch (type)
            {
                case ThrottleOutPacketType.Resend:
                    return Resend;
                case ThrottleOutPacketType.Land:
                    return Land;
                case ThrottleOutPacketType.Wind:
                    return Wind;
                case ThrottleOutPacketType.Cloud:
                    return Cloud;
                case ThrottleOutPacketType.Task:
                    return Task;
                case ThrottleOutPacketType.Texture:
                    return Texture;
                case ThrottleOutPacketType.Asset:
                    return Asset;
                case ThrottleOutPacketType.Unknown:
                default:
                    return 0;
            }
        }
    }
}
