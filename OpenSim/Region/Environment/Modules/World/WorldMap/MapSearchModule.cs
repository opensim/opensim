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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Reflection;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules.World.WorldMap
{
    public class MapSearchModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        Scene m_scene = null; // only need one for communication with GridService

        #region IRegionModule Members
        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_scene == null) m_scene = scene;
            scene.EventManager.OnNewClient += OnNewClient;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return "MapSearchModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            client.OnMapNameRequest += OnMapNameRequest;
        }

        private void OnMapNameRequest(IClientAPI remoteClient, string mapName)
        {
            m_log.DebugFormat("[MAPSEARCH]: looking for region {0}", mapName);

            // TODO currently, this only returns one region per name. LL servers will return all starting with the provided name.
            RegionInfo info = m_scene.SceneGridService.RequestClosestRegion(mapName);
            // fetch the mapblock of the named sim. We need this anyway (we have the map open, and just jumped to the sim),
            // so there shouldn't be any penalty for that.
            if (info == null)
            {
                m_log.Warn("[MAPSEARCHMODULE]: Got Null Region Question!");
                return;
            }
            List<MapBlockData> mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks((int)info.RegionLocX,
                                                                                              (int)info.RegionLocY,
                                                                                              (int)info.RegionLocX,
                                                                                              (int)info.RegionLocY);

            List<MapBlockData> blocks = new List<MapBlockData>();

            MapBlockData data = new MapBlockData();
            data.Agents = 3; // TODO set to number of agents in region
            data.Access = 21; // TODO what's this?
            data.MapImageId = mapBlocks.Count == 0 ? UUID.Zero : mapBlocks[0].MapImageId;
            data.Name = info.RegionName;
            data.RegionFlags = 0; // TODO fix this
            data.WaterHeight = 0; // not used
            data.X = (ushort)info.RegionLocX;
            data.Y = (ushort)info.RegionLocY;
            blocks.Add(data);

            data = new MapBlockData();
            data.Agents = 0;
            data.Access = 255;
            data.MapImageId = UUID.Zero;
            data.Name = mapName;
            data.RegionFlags = 0;
            data.WaterHeight = 0; // not used
            data.X = 0;
            data.Y = 0;
            blocks.Add(data);

            remoteClient.SendMapBlock(blocks, 0);
        }
    }
}
