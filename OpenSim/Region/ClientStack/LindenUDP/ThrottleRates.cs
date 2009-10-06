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
        /// <summary>Drip rate for task (state and transaction) packets</summary>
        public int Task;
        /// <summary>Drip rate for texture packets</summary>
        public int Texture;
        /// <summary>Drip rate for asset packets</summary>
        public int Asset;

        /// <summary>Maximum burst rate for resent packets</summary>
        public int ResendLimit;
        /// <summary>Maximum burst rate for land packets</summary>
        public int LandLimit;
        /// <summary>Maximum burst rate for wind packets</summary>
        public int WindLimit;
        /// <summary>Maximum burst rate for cloud packets</summary>
        public int CloudLimit;
        /// <summary>Maximum burst rate for task (state and transaction) packets</summary>
        public int TaskLimit;
        /// <summary>Maximum burst rate for texture packets</summary>
        public int TextureLimit;
        /// <summary>Maximum burst rate for asset packets</summary>
        public int AssetLimit;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="config">Config source to load defaults from</param>
        public ThrottleRates(IConfigSource config)
        {
            try
            {
                IConfig throttleConfig = config.Configs["ClientStack.LindenUDP"];

                Resend = throttleConfig.GetInt("ResendDefault", 12500);
                Land = throttleConfig.GetInt("LandDefault", 500);
                Wind = throttleConfig.GetInt("WindDefault", 500);
                Cloud = throttleConfig.GetInt("CloudDefault", 500);
                Task = throttleConfig.GetInt("TaskDefault", 500);
                Texture = throttleConfig.GetInt("TextureDefault", 500);
                Asset = throttleConfig.GetInt("AssetDefault", 500);

                ResendLimit = throttleConfig.GetInt("ResendLimit", 18750);
                LandLimit = throttleConfig.GetInt("LandLimit", 29750);
                WindLimit = throttleConfig.GetInt("WindLimit", 18750);
                CloudLimit = throttleConfig.GetInt("CloudLimit", 18750);
                TaskLimit = throttleConfig.GetInt("TaskLimit", 55750);
                TextureLimit = throttleConfig.GetInt("TextureLimit", 55750);
                AssetLimit = throttleConfig.GetInt("AssetLimit", 27500);
            }
            catch (Exception) { }
        }
    }
}
