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
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.RegionCombinerModule
{
    public class RegionConnections
    {
        /// <summary>
        /// Root Region ID
        /// </summary>
        public UUID RegionId;

        /// <summary>
        /// Root Region Scene
        /// </summary>
        public Scene RegionScene;

        /// <summary>
        /// LargeLandChannel for combined region
        /// </summary>
        public ILandChannel RegionLandChannel;

        /// <summary>
        /// The x map co-ordinate for this region (where each co-ordinate is a Constants.RegionSize block).
        /// </summary>
        public uint X;

        /// <summary>
        /// The y co-ordinate for this region (where each cor-odinate is a Constants.RegionSize block).
        /// </summary>
        public uint Y;

        /// <summary>
        /// The X meters position of this connection.
        /// </summary>
        public uint PosX { get { return Util.RegionToWorldLoc(X); } }

        /// <summary>
        /// The Y meters co-ordinate of this connection.
        /// </summary>
        public uint PosY { get { return Util.RegionToWorldLoc(Y); } }

        /// <summary>
        /// The size of the megaregion in meters.
        /// </summary>
        public uint XEnd;

        /// <summary>
        /// The size of the megaregion in meters.
        /// </summary>
        public uint YEnd;

        public List<RegionData> ConnectedRegions;
        public RegionCombinerPermissionModule PermissionModule;
        public RegionCombinerClientEventForwarder ClientEventForwarder;

        public void UpdateExtents(Vector3 extents)
        {
            XEnd = (uint)extents.X;
            YEnd = (uint)extents.Y;
        }
    }
}
