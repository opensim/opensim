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
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Framework.Servers.HttpServer;
using log4net;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    public class FriendsModule : BaseStreamHandler, ISharedRegionModule, IFriendsModule
    {
        protected class UserFriendData
        {
            public UUID PrincipalID;
            public FriendInfo[] Friends;
            public int Refcount;
            public UUID RegionID;
        }
            
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected int m_Port = 0;

        protected List<Scene> m_Scenes = new List<Scene>();

        protected IPresenceService m_PresenceService = null;
        protected IFriendsService m_FriendsService = null;
        protected Dictionary<UUID, UserFriendData> m_Friends =
                new Dictionary<UUID, UserFriendData>();

        protected IPresenceService PresenceService
        {
            get
            {
                if (m_PresenceService == null)
                {
                    if (m_Scenes.Count > 0)
                        m_PresenceService = m_Scenes[0].RequestModuleInterface<IPresenceService>();
                }

                return m_PresenceService;
            }
        }

        public FriendsModule()
                : base("POST", "/friends")
        {
        }

        public void Initialise(IConfigSource config)
        {
            IConfig friendsConfig = config.Configs["Friends"];
            if (friendsConfig != null)
            {
                m_Port = friendsConfig.GetInt("Port", m_Port);

                string connector = friendsConfig.GetString("Connector", String.Empty);
                Object[] args = new Object[] { config };

                m_FriendsService = ServerUtils.LoadPlugin<IFriendsService>(connector, args);
            }

            if (m_FriendsService == null)
            {
                m_log.Error("[FRIENDS]: No Connector defined in section Friends, or filed to load, cannot continue");
                throw new Exception("Connector load error");
            }

            IHttpServer server = MainServer.GetHttpServer((uint)m_Port);

            server.AddStreamHandler(this);

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_Scenes.Add(scene);
            scene.RegisterModuleInterface<IFriendsModule>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            m_Scenes.Remove(scene);
        }

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();
                request.Remove("METHOD");

                switch (method)
                {
                    case "TEST":
                        break;
                }
            }
            catch (Exception e)
            {
                m_log.Debug("[FRIENDS]: Exception {0}" + e.ToString());
            }

            return FailureResult();
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void OfferFriendship(UUID fromUserId, IClientAPI toUserClient, string offerMessage)
        {
        }

        public uint GetFriendPerms(UUID principalID, UUID friendID)
        {
            if (!m_Friends.ContainsKey(principalID))
                return 0;

            UserFriendData data = m_Friends[principalID];

            foreach (FriendInfo fi in data.Friends)
            {
                if (fi.Friend == friendID.ToString())
                    return (uint)fi.TheirFlags;
            }
            return 0;
        }

        private byte[] FailureResult()
        {
            return BoolResult(false);
        }

        private byte[] SuccessResult()
        {
            return BoolResult(true);
        }

        private byte[] BoolResult(bool value)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "RESULT", "");
            result.AppendChild(doc.CreateTextNode(value.ToString()));

            rootElement.AppendChild(result);

            return DocToBytes(doc);
        }

        private byte[] DocToBytes(XmlDocument doc)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();

            return ms.ToArray();
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnLogout += OnLogout;

            if (m_Friends.ContainsKey(client.AgentId))
            {
                m_Friends[client.AgentId].Refcount++;
                return;
            }

            UserFriendData newFriends = new UserFriendData();

            newFriends.PrincipalID = client.AgentId;
            newFriends.Friends = m_FriendsService.GetFriends(client.AgentId);
            newFriends.Refcount = 1;
            newFriends.RegionID = UUID.Zero;

            m_Friends.Add(client.AgentId, newFriends);
        }

        private void OnClientClosed(UUID agentID, Scene scene)
        {
            if (m_Friends.ContainsKey(agentID))
            {
                if (m_Friends[agentID].Refcount == 1)
                    m_Friends.Remove(agentID);
                else
                    m_Friends[agentID].Refcount--;
            }
        }

        private void OnLogout(IClientAPI client)
        {
            m_Friends.Remove(client.AgentId);
        }

        private void OnMakeRootAgent(ScenePresence sp)
        {
            UUID agentID = sp.ControllingClient.AgentId;

            if (m_Friends.ContainsKey(agentID))
            {
                if (m_Friends[agentID].RegionID == UUID.Zero)
                {
                    m_Friends[agentID].Friends =
                            m_FriendsService.GetFriends(agentID);
                }
                m_Friends[agentID].RegionID =
                        sp.ControllingClient.Scene.RegionInfo.RegionID;
            }
        }


        private void OnMakeChildAgent(ScenePresence sp)
        {
            UUID agentID = sp.ControllingClient.AgentId;

            if (m_Friends.ContainsKey(agentID))
            {
                if (m_Friends[agentID].RegionID == sp.ControllingClient.Scene.RegionInfo.RegionID)
                    m_Friends[agentID].RegionID = UUID.Zero;
            }
        }

    }
}
