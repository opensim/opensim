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
    public sealed class ThrottleRates
    {
        public int Resend;
        public int Land;
        public int Wind;
        public int Cloud;
        public int Task;
        public int Texture;
        public int Asset;

        public int ResendLimit;
        public int LandLimit;
        public int WindLimit;
        public int CloudLimit;
        public int TaskLimit;
        public int TextureLimit;
        public int AssetLimit;

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
