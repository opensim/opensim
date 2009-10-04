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

using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AssetLandmark : AssetBase
    {
        public Vector3 Position;
        public ulong RegionHandle;
        public UUID RegionID;
        public int Version;

        public AssetLandmark(AssetBase a)
        {
            Data = a.Data;
            FullID = a.FullID;
            Type = a.Type;
            Name = a.Name;
            Description = a.Description;
            InternData();
        }

        private void InternData()
        {
            string temp = Util.UTF8.GetString(Data).Trim();
            string[] parts = temp.Split('\n');
            int.TryParse(parts[0].Substring(17, 1), out Version);
            UUID.TryParse(parts[1].Substring(10, 36), out RegionID);
            // the vector is stored with spaces as separators, not with commas ("10.3 32.5 43" instead of "10.3, 32.5, 43")
            Vector3.TryParse(parts[2].Substring(10, parts[2].Length - 10).Replace(" ", ","), out Position);
            ulong.TryParse(parts[3].Substring(14, parts[3].Length - 14), out RegionHandle);
        }
    }
}
