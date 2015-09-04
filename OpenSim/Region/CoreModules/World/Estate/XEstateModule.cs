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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.World.Estate
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XEstate")]
    public class XEstateModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<Scene> m_Scenes = new List<Scene>();
        protected bool m_InInfoUpdate = false;

        public bool InInfoUpdate
        {
            get { return m_InInfoUpdate; }
            set { m_InInfoUpdate = value; }
        }

        public List<Scene> Scenes
        {
            get { return m_Scenes; }
        }

        protected EstateConnector m_EstateConnector;

        public void Initialise(IConfigSource config)
        {
            int port = 0;

            IConfig estateConfig = config.Configs["Estate"];
            if (estateConfig != null)
            {
                port = estateConfig.GetInt("Port", 0);
            }

            m_EstateConnector = new EstateConnector(this);

            // Instantiate the request handler
            IHttpServer server = MainServer.GetHttpServer((uint)port);
            server.AddStreamHandler(new EstateRequestHandler(this));
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            lock (m_Scenes)
                m_Scenes.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
        }

        public void RegionLoaded(Scene scene)
        {
            IEstateModule em = scene.RequestModuleInterface<IEstateModule>();

            em.OnRegionInfoChange += OnRegionInfoChange;
            em.OnEstateInfoChange += OnEstateInfoChange;
            em.OnEstateMessage += OnEstateMessage;
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnNewClient -= OnNewClient;

            lock (m_Scenes)
                m_Scenes.Remove(scene);
        }

        public string Name
        {
            get { return "EstateModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        private Scene FindScene(UUID RegionID)
        {
            foreach (Scene s in Scenes)
            {
                if (s.RegionInfo.RegionID == RegionID)
                    return s;
            }

            return null;
        }

        private void OnRegionInfoChange(UUID RegionID)
        {
            Scene s = FindScene(RegionID);
            if (s == null)
                return;

            if (!m_InInfoUpdate)
                m_EstateConnector.SendUpdateCovenant(s.RegionInfo.EstateSettings.EstateID, s.RegionInfo.RegionSettings.Covenant);
        }

        private void OnEstateInfoChange(UUID RegionID)
        {
            Scene s = FindScene(RegionID);
            if (s == null)
                return;

            if (!m_InInfoUpdate)
                m_EstateConnector.SendUpdateEstate(s.RegionInfo.EstateSettings.EstateID);
        }

        private void OnEstateMessage(UUID RegionID, UUID FromID, string FromName, string Message)
        {
            Scene senderScenes = FindScene(RegionID);
            if (senderScenes == null)
                return;

            uint estateID = senderScenes.RegionInfo.EstateSettings.EstateID;

            foreach (Scene s in Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == estateID)
                {
                    IDialogModule dm = s.RequestModuleInterface<IDialogModule>();

                    if (dm != null)
                    {
                        dm.SendNotificationToUsersInRegion(FromID, FromName,
                                Message);
                    }
                }
            }
            if (!m_InInfoUpdate)
                m_EstateConnector.SendEstateMessage(estateID, FromID, FromName, Message);
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnEstateTeleportOneUserHomeRequest += OnEstateTeleportOneUserHomeRequest;
            client.OnEstateTeleportAllUsersHomeRequest += OnEstateTeleportAllUsersHomeRequest;

        }

        private void OnEstateTeleportOneUserHomeRequest(IClientAPI client, UUID invoice, UUID senderID, UUID prey)
        {
            if (prey == UUID.Zero)
                return;

            if (!(client.Scene is Scene))
                return;

            Scene scene = (Scene)client.Scene;

            uint estateID = scene.RegionInfo.EstateSettings.EstateID;

            if (!scene.Permissions.CanIssueEstateCommand(client.AgentId, false))
                return;

            foreach (Scene s in Scenes)
            {
                if (s == scene)
                    continue; // Already handles by estate module
                if (s.RegionInfo.EstateSettings.EstateID != estateID)
                    continue;

                ScenePresence p = scene.GetScenePresence(prey);
                if (p != null && !p.IsChildAgent)
                {
                    p.ControllingClient.SendTeleportStart(16);
                    scene.TeleportClientHome(prey, p.ControllingClient);
                }
            }

            m_EstateConnector.SendTeleportHomeOneUser(estateID, prey);
        }

        private void OnEstateTeleportAllUsersHomeRequest(IClientAPI client, UUID invoice, UUID senderID)
        {
            if (!(client.Scene is Scene))
                return;

            Scene scene = (Scene)client.Scene;

            uint estateID = scene.RegionInfo.EstateSettings.EstateID;

            if (!scene.Permissions.CanIssueEstateCommand(client.AgentId, false))
                return;

            foreach (Scene s in Scenes)
            {
                if (s == scene)
                    continue; // Already handles by estate module
                if (s.RegionInfo.EstateSettings.EstateID != estateID)
                    continue;

                scene.ForEachScenePresence(delegate(ScenePresence p) {
                    if (p != null && !p.IsChildAgent)
                    {
                        p.ControllingClient.SendTeleportStart(16);
                        scene.TeleportClientHome(p.ControllingClient.AgentId, p.ControllingClient);
                    }
                });
            }

            m_EstateConnector.SendTeleportHomeAllUsers(estateID);
        }
    }
}
