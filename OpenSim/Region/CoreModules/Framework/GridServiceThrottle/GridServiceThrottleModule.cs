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
using System.Threading;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Scenes;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Framework
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GridServiceThrottleModule")]
    public class GridServiceThrottleModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Scene> m_scenes = new List<Scene>();

        private OpenSim.Framework.BlockingQueue<GridRegionRequest> m_RequestQueue = new OpenSim.Framework.BlockingQueue<GridRegionRequest>();

        public void Initialise(IConfigSource config)
        {
            Watchdog.StartThread(
                ProcessQueue,
                "GridServiceRequestThread",
                ThreadPriority.BelowNormal,
                true,
                false);
        }

        public void AddRegion(Scene scene)
        {
            lock (m_scenes)
            {
                m_scenes.Add(scene);
                scene.EventManager.OnNewClient += OnNewClient;
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenes)
            {
                m_scenes.Remove(scene);
                scene.EventManager.OnNewClient -= OnNewClient;
            }
        }

        void OnNewClient(IClientAPI client)
        {
            client.OnRegionHandleRequest += OnRegionHandleRequest;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "GridServiceThrottleModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void OnRegionHandleRequest(IClientAPI client, UUID regionID)
        {
            //m_log.DebugFormat("[GRIDSERVICE THROTTLE]: RegionHandleRequest {0}", regionID);
            ulong handle = 0;
            if (IsLocalRegionHandle(regionID, out handle))
            {
                client.SendRegionHandle(regionID, handle);
                return;
            }

            GridRegionRequest request = new GridRegionRequest(client, regionID);
            m_RequestQueue.Enqueue(request);

        }

        private bool IsLocalRegionHandle(UUID regionID, out ulong regionHandle)
        {
            regionHandle = 0;
            foreach (Scene s in m_scenes)
                if (s.RegionInfo.RegionID == regionID)
                {
                    regionHandle = s.RegionInfo.RegionHandle;
                    return true;
                }
            return false;
        }

        private void ProcessQueue()
        {
            while (true)
            {
                Watchdog.UpdateThread();

                GridRegionRequest request = m_RequestQueue.Dequeue();
                GridRegion r = m_scenes[0].GridService.GetRegionByUUID(UUID.Zero, request.regionID);

                if (r != null && r.RegionHandle != 0)
                    request.client.SendRegionHandle(request.regionID, r.RegionHandle);

            }
        }
    }

    class GridRegionRequest
    {
        public IClientAPI client;
        public UUID regionID;

        public GridRegionRequest(IClientAPI c, UUID r)
        {
            client = c;
            regionID = r;
        }
    }
}
