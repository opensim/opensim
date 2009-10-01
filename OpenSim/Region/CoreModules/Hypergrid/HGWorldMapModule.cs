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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.WorldMap;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Hypergrid
{
    public class HGWorldMapModule : WorldMapModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region INonSharedRegionModule Members

        public override void Initialise(IConfigSource config)
        {
            IConfig startupConfig = config.Configs["Startup"];
            if (startupConfig.GetString("WorldMapModule", "WorldMap") == "HGWorldMap")
                m_Enabled = true;
        }

        public override string Name
        {
            get { return "HGWorldMap"; }
        }

        #endregion

        protected override void GetAndSendBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            List<MapBlockData> mapBlocks = new List<MapBlockData>();
            List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                (minX - 4) * (int)Constants.RegionSize, (maxX + 4) * (int)Constants.RegionSize, 
                (minY - 4) * (int)Constants.RegionSize, (maxY + 4) * (int)Constants.RegionSize);

            foreach (GridRegion r in regions)
            {
                MapBlockData block = new MapBlockData();
                MapBlockFromGridRegion(block, r);
                mapBlocks.Add(block);
            }

            // Different from super
            FillInMap(mapBlocks, minX, minY, maxX, maxY);
            //

            remoteClient.SendMapBlock(mapBlocks, flag);
        }


        private void FillInMap(List<MapBlockData> mapBlocks, int minX, int minY, int maxX, int maxY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    MapBlockData mblock = mapBlocks.Find(delegate(MapBlockData mb) { return ((mb.X == x) && (mb.Y == y)); });
                    if (mblock == null)
                    {
                        mblock = new MapBlockData();
                        mblock.X = (ushort)x;
                        mblock.Y = (ushort)y;
                        mblock.Name = "";
                        mblock.Access = 254; // not here???
                        mblock.MapImageId = UUID.Zero;
                        mapBlocks.Add(mblock);
                    }
                }
            }
        }
    }
}
