/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;

using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.WorldMap;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Types;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

using OSD = OpenMetaverse.StructuredData.OSD;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

namespace OpenSim.Region.Environment.Modules.Hypergrid
{
    public class HGWorldMapModule : WorldMapModule, IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IRegionModule Members

        public override void Initialise(Scene scene, IConfigSource config)
        {
            IConfig startupConfig = config.Configs["Startup"];
            if (startupConfig.GetString("WorldMapModule", "WorldMap") == "HGWorldMap") 
                m_Enabled = true;

            if (!m_Enabled)
                return;
            m_log.Info("[HGMap] Initializing...");
            m_scene = scene;
        }


        public override string Name
        {
            get { return "HGWorldMap"; }
        }


        #endregion

        /// <summary>
        /// Requests map blocks in area of minX, maxX, minY, MaxY in world cordinates
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public override void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            //
            // WARNING!!! COPY & PASTE FROM SUPERCLASS
            // The only difference is at the very end
            //

            m_log.Info("[HGMap]: Request map blocks " + minX + "-" + maxX + " " + minY + "-" + maxY);

            //m_scene.ForEachScenePresence(delegate (ScenePresence sp) {
            //    if (!sp.IsChildAgent && sp.UUID == remoteClient.AgentId)
            //    {
            //        Console.WriteLine("XXX Root agent");
            //        DoRequestMapBlocks(remoteClient, minX, minY, maxX, maxY, flag);
            //    }
            //};

            List<MapBlockData> mapBlocks;
            if ((flag & 0x10000) != 0)  // user clicked on the map a tile that isn't visible
            {
                List<MapBlockData> response = new List<MapBlockData>();

                // this should return one mapblock at most. But make sure: Look whether the one we requested is in there
                mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
                if (mapBlocks != null)
                {
                    foreach (MapBlockData block in mapBlocks)
                    {
                        if (block.X == minX && block.Y == minY)
                        {
                            // found it => add it to response
                            response.Add(block);
                            break;
                        }
                    }
                }
                response = mapBlocks;
                if (response.Count == 0)
                {
                    // response still empty => couldn't find the map-tile the user clicked on => tell the client
                    MapBlockData block = new MapBlockData();
                    block.X = (ushort)minX;
                    block.Y = (ushort)minY;
                    block.Access = 254; // == not there
                    response.Add(block);
                }
                remoteClient.SendMapBlock(response, 0);
            }
            else
            {
                // normal mapblock request. Use the provided values
                mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX - 4, minY - 4, maxX + 4, maxY + 4);

                // Different from super
                FillInMap(mapBlocks, minX, minY, maxX, maxY);
                //

                remoteClient.SendMapBlock(mapBlocks, flag);
            }
        }


        private void FillInMap(List<MapBlockData> mapBlocks, int minX, int minY, int maxX, int maxY)
        {
            for (int x = minX; x <= maxX; x++)
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
