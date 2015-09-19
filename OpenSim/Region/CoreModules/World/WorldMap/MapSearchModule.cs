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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.World.WorldMap
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MapSearchModule")]
    public class MapSearchModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        Scene m_scene = null; // only need one for communication with GridService
        List<Scene> m_scenes = new List<Scene>();
        List<UUID> m_Clients;

        IWorldMapModule m_WorldMap;
        IWorldMapModule WorldMap
        {
            get
            {
                if (m_WorldMap == null)
                    m_WorldMap = m_scene.RequestModuleInterface<IWorldMapModule>();
                return m_WorldMap;
            }

        }

        #region ISharedRegionModule Members
        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_scene == null)
            {
                m_scene = scene;
            }

            m_scenes.Add(scene);
            scene.EventManager.OnNewClient += OnNewClient;
            m_Clients = new List<UUID>();

        }

        public void RemoveRegion(Scene scene)
        {
            m_scenes.Remove(scene);
            if (m_scene == scene && m_scenes.Count > 0)
                m_scene = m_scenes[0];

            scene.EventManager.OnNewClient -= OnNewClient;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_scene = null;
            m_scenes.Clear();
        }

        public string Name
        {
            get { return "MapSearchModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegionLoaded(Scene scene)
        {
        }
        #endregion

        private void OnNewClient(IClientAPI client)
        {
            client.OnMapNameRequest += OnMapNameRequestHandler;
        }

        private void OnMapNameRequestHandler(IClientAPI remoteClient, string mapName, uint flags)
        {
            lock (m_Clients)
            {
                if (m_Clients.Contains(remoteClient.AgentId))
                    return;

                m_Clients.Add(remoteClient.AgentId);
            }

            try
            {
                OnMapNameRequest(remoteClient, mapName, flags);
            }
            finally
            {
                lock (m_Clients)
                    m_Clients.Remove(remoteClient.AgentId);
            }
        }

        private void OnMapNameRequest(IClientAPI remoteClient, string mapName, uint flags)
        {
            Util.FireAndForget(x =>
            {
                List<MapBlockData> blocks = new List<MapBlockData>();
                if (mapName.Length < 3 || (mapName.EndsWith("#") && mapName.Length < 4))
                {
                    // final block, closing the search result
                    AddFinalBlock(blocks,mapName);

                    // flags are agent flags sent from the viewer.
                    // they have different values depending on different viewers, apparently
                    remoteClient.SendMapBlock(blocks, flags);
                    remoteClient.SendAlertMessage("Use a search string with at least 3 characters");
                    return;
                }

                //m_log.DebugFormat("MAP NAME=({0})", mapName);

                // Hack to get around the fact that ll V3 now drops the port from the
                // map name. See https://jira.secondlife.com/browse/VWR-28570
                //
                // Caller, use this magic form instead:
                // secondlife://http|!!mygrid.com|8002|Region+Name/128/128
                // or url encode if possible.
                // the hacks we do with this viewer...
                //
                bool needOriginalName = false;
                string mapNameOrig = mapName;
                if (mapName.Contains("|"))
                {
                    mapName = mapName.Replace('|', ':');
                    needOriginalName = true;
                }
                if (mapName.Contains("+"))
                {
                    mapName = mapName.Replace('+', ' ');
                    needOriginalName = true;
                }
                if (mapName.Contains("!"))
                {
                    mapName = mapName.Replace('!', '/');
                    needOriginalName = true;
                }
                if (mapName.Contains("."))
                    needOriginalName = true;

                // try to fetch from GridServer
                List<GridRegion> regionInfos = m_scene.GridService.GetRegionsByName(m_scene.RegionInfo.ScopeID, mapName, 20);
    //            if (regionInfos.Count == 0)
    //                remoteClient.SendAlertMessage("Hyperlink could not be established.");

                //m_log.DebugFormat("[MAPSEARCHMODULE]: search {0} returned {1} regions", mapName, regionInfos.Count);
 
                MapBlockData data;
                if (regionInfos.Count > 0)
                {
                    foreach (GridRegion info in regionInfos)
                    {
                        data = new MapBlockData();
                        data.Agents = 0;
                        data.Access = info.Access;
                        MapBlockData block = new MapBlockData();
                        WorldMap.MapBlockFromGridRegion(block, info, flags);

                        if (flags == 2 &&  regionInfos.Count == 1 && needOriginalName)
                                block.Name = mapNameOrig;
                        blocks.Add(block);
                    }
                }

                // final block, closing the search result
                AddFinalBlock(blocks,mapNameOrig);

                // flags are agent flags sent from the viewer.
                // they have different values depending on different viewers, apparently
                remoteClient.SendMapBlock(blocks, flags);

                // send extra user messages for V3
                // because the UI is very confusing
                // while we don't fix the hard-coded urls
                if (flags == 2) 
                {
                    if (regionInfos.Count == 0)
                        remoteClient.SendAgentAlertMessage("No regions found with that name.", true);
//                    else if (regionInfos.Count == 1)
//                        remoteClient.SendAgentAlertMessage("Region found!", false);
                }
            });
        }

        private void AddFinalBlock(List<MapBlockData> blocks,string name)
        {
                // final block, closing the search result
                MapBlockData data = new MapBlockData();
                data.Agents = 0;
                data.Access = (byte)SimAccess.NonExistent;
                data.MapImageId = UUID.Zero;
                data.Name = name;
                data.RegionFlags = 0;
                data.WaterHeight = 0; // not used
                data.X = 0;
                data.Y = 0;
                blocks.Add(data);
        }
//        private Scene GetClientScene(IClientAPI client)
//        {
//            foreach (Scene s in m_scenes)
//            {
//                if (client.Scene.RegionInfo.RegionHandle == s.RegionInfo.RegionHandle)
//                    return s;
//            }
//            return m_scene;
//        }
    }
}
