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
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Concierge
{
    public class ConciergeModule : IRegionModule
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;

        private int _conciergeChannel = 42;
        private List<Scene> _scenes = new List<Scene>();
        private IConfig _config;
        private string _whoami = null;

        internal object _syncy = new object();

        #region IRegionModule Members
        public void Initialise(Scene scene, IConfigSource config)
        {
            try
            {
                if ((_config = config.Configs["Concierge"]) == null)
                {
                    _log.InfoFormat("[Concierge] no configuration section [Concierge] in OpenSim.ini: module not configured");
                    return;
                }

                if (!_config.GetBoolean("enabled", false))
                {
                    _log.InfoFormat("[Concierge] module disabled by OpenSim.ini configuration");
                    return;
                }

            }
            catch (Exception)
            {
                _log.Info("[Concierge] module not configured");
                return;
            }

            _conciergeChannel = config.Configs["Concierge"].GetInt("concierge_channel", _conciergeChannel);
            _whoami = _config.GetString("concierge_name", "conferencier");

            lock (_syncy)
            {
                if (!_scenes.Contains(scene))
                {
                    _scenes.Add(scene);
                    // subscribe to NewClient events
                    scene.EventManager.OnNewClient += OnNewClient;

                    // subscribe to *Chat events
                    scene.EventManager.OnChatFromWorld += OnSimChat;
                    scene.EventManager.OnChatBroadcast += OnSimBroadcast;

                    // subscribe to agent change events
                    scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
                    scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
                }
            }
            _log.InfoFormat("[Concierge] initialized for {0}", scene.RegionInfo.RegionName);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ConciergeModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region ISimChat Members
        public void OnSimBroadcast(Object sender, OSChatMessage c)
        {
            if (_conciergeChannel == c.Channel)
            { 
                // concierge request: interpret
                return;
            }

            if (0 == c.Channel || DEBUG_CHANNEL == c.Channel)
            {
                // log as avatar/prim chat
                return;
            }

            return;
        }

        public void OnSimChat(Object sender, OSChatMessage c)
        {
            if (_conciergeChannel == c.Channel)
            { 
                // concierge request: interpret
                return;
            }

            if (0 == c.Channel || DEBUG_CHANNEL == c.Channel)
            {
                // log as avatar/prim chat
                return;
            }

            return;
        }

        #endregion


        public void OnNewClient(IClientAPI client)
        {
            try
            {
                client.OnChatFromViewer += OnSimChat;
            }
            catch (Exception ex)
            {
                _log.Error("[Concierge]: NewClient exception trap:" + ex.ToString());
            }
        }


        public void OnMakeRootAgent(ScenePresence agent)
        {
            _log.DebugFormat("[Concierge] {0} enters {1}", agent.Name, agent.Scene.RegionInfo.RegionName);
            AnnounceToAgentsRegion(agent, String.Format("{0} enters {1}", agent.Name, agent.Scene.RegionInfo.RegionName));
        }


        public void OnMakeChildAgent(ScenePresence agent)
        {
            _log.DebugFormat("[Concierge] {0} leaves {1}", agent.Name, agent.Scene.RegionInfo.RegionName);
            AnnounceToAgentsRegion(agent, String.Format("{0} leaves {1}", agent.Name, agent.Scene.RegionInfo.RegionName));
        }


        public void ClientLoggedOut(IClientAPI client)
        {
            string clientName = String.Format("{0} {1}", client.FirstName, client.LastName);
            _log.DebugFormat("[CONCIERGE] {0} logging off.", clientName);
        }


        static private Vector3 posOfGod = new Vector3(128, 128, 9999);

        protected void AnnounceToAgentsRegion(ScenePresence scenePresence, string msg)
        {
            OSChatMessage c = new OSChatMessage();
            c.Message = msg;
            c.Type = ChatTypeEnum.Say;
            c.Channel = 0;
            c.Position = posOfGod;
            c.From = _whoami;
            c.Sender = null;
            c.SenderUUID = UUID.Zero;
            c.Scene = scenePresence.Scene;

            scenePresence.Scene.EventManager.TriggerOnChatBroadcast(this, c);
        }
    }
}