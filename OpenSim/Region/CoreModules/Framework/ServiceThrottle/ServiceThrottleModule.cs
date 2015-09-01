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
        private System.Timers.Timer m_timer = new System.Timers.Timer();

        private Queue<Action> m_RequestQueue = new Queue<Action>();
        private Dictionary<string, List<string>> m_Pending = new Dictionary<string, List<string>>();
        private int m_Interval;

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_Interval = Util.GetConfigVarFromSections<int>(config, "Interval", new string[] { "ServiceThrottle" }, 5000);

            m_timer = new System.Timers.Timer();
            m_timer.AutoReset = false;
            m_timer.Enabled = true;
            m_timer.Interval = 15000; // 15 secs at first
            m_timer.Elapsed += ProcessQueue;
            m_timer.Start();

            //WorkManager.StartThread(
            //    ProcessQueue,
            //    "GridServiceRequestThread",
            //    ThreadPriority.BelowNormal,
            //    true,
            //    false);
        }

        public void AddRegion(Scene scene)
        {
            lock (m_scenes)
            {
                m_scenes.Add(scene);
                scene.RegisterModuleInterface<IServiceThrottleModule>(this);
                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
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

        void OnMakeRootAgent(ScenePresence obj)
        {
            lock (m_timer)
            {
                if (!m_timer.Enabled)
                {
                    m_timer.Interval = m_Interval;
                    m_timer.Enabled = true;
                    m_timer.Start();
                }
            }
        }

        public void OnRegionHandleRequest(IClientAPI client, UUID regionID)
        {
            //m_log.DebugFormat("[SERVICE THROTTLE]: RegionHandleRequest {0}", regionID);
            ulong handle = 0;
            if (IsLocalRegionHandle(regionID, out handle))
            {
                client.SendRegionHandle(regionID, handle);
                return;
            }

            Action action = delegate
            {
                GridRegion r = m_scenes[0].GridService.GetRegionByUUID(UUID.Zero, regionID);

                if (r != null && r.RegionHandle != 0)
                    client.SendRegionHandle(regionID, r.RegionHandle);
            };

            Enqueue("region", regionID.ToString(), action);
        }

        #endregion Events

        #region IServiceThrottleModule

        public void Enqueue(string category, string itemid, Action continuation)
        {
            lock (m_RequestQueue)
            {
                if (m_Pending.ContainsKey(category))
                {
                    if (m_Pending[category].Contains(itemid))
                        // Don't enqueue, it's already pending
                        return;
                }
                else
                    m_Pending.Add(category, new List<string>());

                m_Pending[category].Add(itemid);

                m_RequestQueue.Enqueue(delegate
                {
                    lock (m_RequestQueue)
                        m_Pending[category].Remove(itemid);

                    continuation();
                });
            }
        }

        #endregion IServiceThrottleModule

        #region Process Continuation Queue

        private void ProcessQueue(object sender, System.Timers.ElapsedEventArgs e)
        {
            //m_log.DebugFormat("[YYY]: Process queue with {0} continuations", m_RequestQueue.Count);

            while (m_RequestQueue.Count > 0)
            {
                Action continuation = null;
                lock (m_RequestQueue)
                    continuation = m_RequestQueue.Dequeue();

                if (continuation != null)
                    continuation();
            }

            if (AreThereRootAgents())
            {
                lock (m_timer)
                {
                    m_timer.Interval = 1000; // 1 sec
                    m_timer.Enabled = true;
                    m_timer.Start();
                }
            }
            else
                lock (m_timer)
                    m_timer.Enabled = false;

        }

        #endregion Process Continuation Queue

        #region Misc

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

        private bool AreThereRootAgents()
        {
            foreach (Scene s in m_scenes)
            {
                foreach (ScenePresence sp in s.GetScenePresences())
                    if (!sp.IsChildAgent)
                        return true;
            }

            return false;
        }

        #endregion Misc
    }

}
