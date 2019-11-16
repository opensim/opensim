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
        public int Resend = 6625;
        /// <summary>Drip rate for terrain packets</summary>
        public int Land = 9125;
        /// <summary>Drip rate for wind packets</summary>
        public int Wind = 1750;
        /// <summary>Drip rate for cloud packets</summary>
        public int Cloud = 1750;
        /// <summary>Drip rate for task packets</summary>
        public int Task = 18500;
        /// <summary>Drip rate for texture packets</summary>
        public int Texture = 18500;
        /// <summary>Drip rate for asset packets</summary>
        public int Asset = 10500;

        /// <summary>Drip rate for the parent token bucket</summary>
        public int Total = 66750;

        /// <summary>Flag used to enable adaptive throttles</summary>
        public bool AdaptiveThrottlesEnabled;

        /// <summary>
        /// Set the minimum rate that the adaptive throttles can set. The viewer
        /// can still throttle lower than this, but the adaptive throttles will
        /// never decrease rates below this no matter how many packets are dropped
        /// </summary>
        public Int64 MinimumAdaptiveThrottleRate;

        public int ClientMaxRate = 640000; // 5,120,000 bps
        public float BurstTime = 10e-3f;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="config">Config source to load defaults from</param>
        public ThrottleRates(IConfigSource config)
        {
            try
            {
                IConfig throttleConfig = config.Configs["ClientStack.LindenUDP"];
                if(throttleConfig != null)
                {
                    ClientMaxRate = throttleConfig.GetInt("client_throttle_max_bps", ClientMaxRate);
                    if (ClientMaxRate > 1000000)
                        ClientMaxRate = 1000000; // no more than 8Mbps
                    else if (ClientMaxRate < 6250)
                        ClientMaxRate = 6250; // no less than 50kbps

                    // Adaptive is broken
                    // AdaptiveThrottlesEnabled = throttleConfig.GetBoolean("enable_adaptive_throttles", false);
                    AdaptiveThrottlesEnabled = false;
                    MinimumAdaptiveThrottleRate = throttleConfig.GetInt("adaptive_throttle_min_bps", 32000);
                }
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
