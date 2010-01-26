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
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.MuteList
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class MuteListModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;
        private List<Scene> m_SceneList = new List<Scene>();
        private string m_RestURL = String.Empty;

        public void Initialise(IConfigSource config)
        {
            if (!enabled)
                return;

            IConfig cnf = config.Configs["Messaging"];
            if (cnf == null)
            {
                enabled = false;
                return;
            }

            if (cnf != null && cnf.GetString(
                    "MuteListModule", "None") !=
                    "MuteListModule")
            {
                enabled = false;
                return;
            }
            m_RestURL = cnf.GetString("MuteListURL", "");
            if (m_RestURL == "")
            {
                m_log.Error("[MUTE LIST] Module was enabled, but no URL is given, disabling");
                enabled = false;
                return;
            }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            lock (m_SceneList)
            {
                if (!m_SceneList.Contains(scene))
                    m_SceneList.Add(scene);

                scene.EventManager.OnNewClient += OnNewClient;
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_SceneList.Contains(scene))
                m_SceneList.Remove(scene);

            scene.EventManager.OnNewClient -= OnNewClient;
        }

        public void PostInitialise()
        {
            if (!enabled)
                return;

            if (m_SceneList.Count == 0)
                return;

            m_log.Debug("[MUTE LIST] Mute list enabled");
        }

        public string Name
        {
            get { return "MuteListModule"; }
        }

        public void Close()
        {
        }
       
//        private IClientAPI FindClient(UUID agentID)
//        {
//            foreach (Scene s in m_SceneList)
//            {
//                ScenePresence presence = s.GetScenePresence(agentID);
//                if (presence != null && !presence.IsChildAgent)
//                    return presence.ControllingClient;
//            }
//            return null;
//        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnMuteListRequest += OnMuteListRequest;
        }

        private void OnMuteListRequest(IClientAPI client, uint crc)
        {
            m_log.DebugFormat("[MUTE LIST] Got mute list request for crc {0}", crc);
            string filename = "mutes"+client.AgentId.ToString();

            IXfer xfer = client.Scene.RequestModuleInterface<IXfer>();
            if (xfer != null)
            {
                xfer.AddNewFile(filename, new Byte[0]);
                client.SendMuteListUpdate(filename);
            }
        }
    }
}

