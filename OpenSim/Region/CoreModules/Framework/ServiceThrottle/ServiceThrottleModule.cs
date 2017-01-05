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
    public class ServiceThrottleModule : ISharedRegionModule, IServiceThrottleModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Scene> m_scenes = new List<Scene>();
        private JobEngine m_processorJobEngine;

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_processorJobEngine = new JobEngine(
                "ServiceThrottle","ServiceThrottle");
            m_processorJobEngine.RequestProcessTimeoutOnStop = 31000; // many webrequests have 30s expire
            m_processorJobEngine.Start();
        }

        public void AddRegion(Scene scene)
        {
            lock (m_scenes)
            {
                m_scenes.Add(scene);
                scene.RegisterModuleInterface<IServiceThrottleModule>(this);
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

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_processorJobEngine.Stop();
        }

        public string Name
        {
            get { return "ServiceThrottleModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion ISharedRegionMOdule

        #region Events

        void OnNewClient(IClientAPI client)
        {
            client.OnRegionHandleRequest += OnRegionHandleRequest;
        }

        public void OnRegionHandleRequest(IClientAPI client, UUID regionID)
        {
            //m_log.DebugFormat("[SERVICE THROTTLE]: RegionHandleRequest {0}", regionID);
            Action action = delegate
            {
                if(!client.IsActive)
                    return;

                GridRegion r = m_scenes[0].GridService.GetRegionByUUID(UUID.Zero, regionID);

                if(!client.IsActive)
                    return;

                if (r != null && r.RegionHandle != 0)
                    client.SendRegionHandle(regionID, r.RegionHandle);
            };

            m_processorJobEngine.QueueJob("regionHandle", action, regionID.ToString());
        }

        #endregion Events

        #region IServiceThrottleModule

        public void Enqueue(string category, string itemid, Action continuation)
        {
                m_processorJobEngine.QueueJob(category, continuation, itemid);
        }

        #endregion IServiceThrottleModule
    }

}
