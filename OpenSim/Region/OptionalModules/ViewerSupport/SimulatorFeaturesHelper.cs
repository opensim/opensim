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
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Threading;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim;
using OpenSim.Region;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
//using OpenSim.Framework.Capabilities;
using Nini.Config;
using log4net;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using TeleportFlags = OpenSim.Framework.Constants.TeleportFlags;

namespace OpenSim.Region.OptionalModules.ViewerSupport
{
    public class SimulatorFeaturesHelper 
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IEntityTransferModule m_TransferModule;
        private Scene m_scene;

        private struct RegionSend {
            public UUID region;
            public bool send;
        };
        // Using a static cache so that we don't have to perform the time-consuming tests
        // in ShouldSend on Extra SimFeatures that go on the same response but come from
        // different modules.
        // This cached is indexed on the agentID and maps to a list of regions
        private static ExpiringCache<UUID, List<RegionSend>> m_Cache = new ExpiringCache<UUID, List<RegionSend>>();
        private const double TIMEOUT = 1.0; // time in cache

        public SimulatorFeaturesHelper(Scene scene, IEntityTransferModule et)
        {
            m_scene = scene;
            m_TransferModule = et;
        }

        public bool ShouldSend(UUID agentID)
        {
            List<RegionSend> rsendlist;
            RegionSend rsend;
            if (m_Cache.TryGetValue(agentID, out rsendlist))
            {
                rsend = rsendlist.Find(r => r.region == m_scene.RegionInfo.RegionID);
                if (rsend.region != UUID.Zero) // Found it
                {
                    return rsend.send;
                }
            }

            // Relatively complex logic for deciding whether to send the extra SimFeature or not.
            // This is because the viewer calls this cap to all sims that it knows about,
            // including the departing sims and non-neighbors (those that are cached).
            rsend.region = m_scene.RegionInfo.RegionID;
            rsend.send = false;
            IClientAPI client = null;
            int counter = 200;

            // Let's wait a little to see if we get a client here
            while (!m_scene.TryGetClient(agentID, out client) && counter-- > 0)
                Thread.Sleep(50);

            if (client != null)
            {
                ScenePresence sp = WaitGetScenePresence(agentID);

                if (sp != null)
                {
                    // On the receiving region, the call to this cap may arrive before
                    // the agent is root. Make sure we only proceed from here when the agent
                    // has been made root
                    counter = 200;
                    while ((sp.IsInTransit || sp.IsChildAgent) && counter-- > 0)
                    {
                        Thread.Sleep(50);
                    }

                    // The viewer calls this cap on the departing sims too. Make sure
                    // that we only proceed after the agent is not in transit anymore.
                    // The agent must be root and not going anywhere
                    if (!sp.IsChildAgent && !m_TransferModule.IsInTransit(agentID))
                        rsend.send = true;

                }
            }
            //else
            //    m_log.DebugFormat("[XXX]: client is null");


            if (rsendlist == null)
            {
                rsendlist = new List<RegionSend>();
                m_Cache.AddOrUpdate(agentID, rsendlist, TIMEOUT);
            }
            rsendlist.Add(rsend);

            return rsend.send;
        }

        public int UserLevel(UUID agentID)
        {
            int level = 0;
            UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, agentID);
            if (account != null)
                level = account.UserLevel;

            return level;
        }

        protected virtual ScenePresence WaitGetScenePresence(UUID agentID)
        {
            int ntimes = 20;
            ScenePresence sp = null;
            while ((sp = m_scene.GetScenePresence(agentID)) == null && (ntimes-- > 0))
                Thread.Sleep(1000);

            if (sp == null)
                m_log.WarnFormat(
                    "[XXX]: Did not find presence with id {0} in {1} before timeout",
                    agentID, m_scene.RegionInfo.RegionName);
            else
            {
                ntimes = 10;
                while (sp.IsInTransit && (ntimes-- > 0))
                    Thread.Sleep(1000);
            }

            return sp;
        }

    }

}
