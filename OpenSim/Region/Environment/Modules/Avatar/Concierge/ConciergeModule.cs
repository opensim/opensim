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
using OpenSim.Region.Environment.Modules.Avatar.Chat;

namespace OpenSim.Region.Environment.Modules.Avatar.Concierge
{
    public class ConciergeModule : ChatModule, IRegionModule
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;

        private int _conciergeChannel = 42;
        private List<IScene> _scenes = new List<IScene>();
        private List<IScene> _conciergedScenes = new List<IScene>();
        private IConfig _config;
        private string _whoami = "conferencier";
        private bool _replacingChatModule = false;
        private Regex _regions = null;

        internal object _syncy = new object();

        #region IRegionModule Members
        public override void Initialise(Scene scene, IConfigSource config)
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

            // check whether ChatModule has been disabled: if yes,
            // then we'll "stand in"
            try
            {
                if (config.Configs["Chat"] == null)
                {
                    _replacingChatModule = false;
                }
                else 
                {
                    _replacingChatModule  = !config.Configs["Chat"].GetBoolean("enabled", true);
                }
            }
            catch (Exception)
            {
                _replacingChatModule = false;
            }
            _log.InfoFormat("[Concierge] {0} ChatModule", _replacingChatModule ? "replacing" : "not replacing");


            // take note of concierge channel and of identity
            _conciergeChannel = config.Configs["Concierge"].GetInt("concierge_channel", _conciergeChannel);
            _whoami = _config.GetString("whoami", "conferencier");
            _log.InfoFormat("[Concierge] reporting as \"{0}\" to our users", _whoami);

            // calculate regions Regex
            if (_regions == null)
            {
                string regions = _config.GetString("regions", String.Empty);
                if (!String.IsNullOrEmpty(regions))
                {
                    _regions = new Regex(regions, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
            }

            lock (_syncy)
            {
                if (!_scenes.Contains(scene))
                {
                    _scenes.Add(scene);

                    if (_regions.IsMatch(scene.RegionInfo.RegionName))
                        _conciergedScenes.Add(scene);

                    // subscribe to NewClient events
                    scene.EventManager.OnNewClient += OnNewClient;

                    // subscribe to *Chat events
                    scene.EventManager.OnChatFromWorld += OnChatFromWorld;
                    if (!_replacingChatModule)
                        scene.EventManager.OnChatFromClient += OnChatFromClient;
                    scene.EventManager.OnChatBroadcast += OnChatBroadcast;

                    // subscribe to agent change events
                    scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
                    scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
                }
            }
            _log.InfoFormat("[Concierge] initialized for {0}", scene.RegionInfo.RegionName);
        }

        public override void PostInitialise()
        {
        }

        public override void Close()
        {
        }

        public override string Name
        {
            get { return "ConciergeModule"; }
        }

        public override bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region ISimChat Members
        public override void OnChatBroadcast(Object sender, OSChatMessage c)
        {
            if (_replacingChatModule)
            {
                // distribute chat message to each and every avatar in
                // the region
                base.OnChatBroadcast(sender, c);
            }

            // TODO: capture logic
            return;
        }

        public override void OnChatFromClient(Object sender, OSChatMessage c)
        {
            if (_replacingChatModule)
            {
                if (_conciergedScenes.Contains(c.Scene))
                {
                    // replacing ChatModule: need to redistribute
                    // ChatFromClient to interested subscribers
                    Scene scene = (Scene)c.Scene;
                    scene.EventManager.TriggerOnChatFromClient(sender, c);

                    // when we are replacing ChatModule, we treat
                    // OnChatFromClient like OnChatBroadcast for
                    // concierged regions, effectively extending the
                    // range of chat to cover the whole
                    // region. however, we don't do this for whisper
                    // (got to have some privacy)
                    if (c.Type != ChatTypeEnum.Whisper)
                    {
                        base.OnChatBroadcast(sender, c);
                        return;
                    }
                }

                // redistribution will be done by base class
                base.OnChatFromClient(sender, c);
            }

            // TODO: capture chat
            return;
        }

        public override void OnChatFromWorld(Object sender, OSChatMessage c)
        {
            if (_replacingChatModule)
            {
                if (_conciergedScenes.Contains(c.Scene))
                {
                    // when we are replacing ChatModule, we treat
                    // OnChatFromClient like OnChatBroadcast for
                    // concierged regions, effectively extending the
                    // range of chat to cover the whole
                    // region. however, we don't do this for whisper
                    // (got to have some privacy)
                    if (c.Type != ChatTypeEnum.Whisper) 
                    {
                        base.OnChatBroadcast(sender, c);
                        return;
                    }
                }

                base.OnChatFromWorld(sender, c);
            }
            return;
        }
        #endregion


        public override void OnNewClient(IClientAPI client)
        {
            client.OnLogout += OnClientLoggedOut;
            client.OnConnectionClosed += OnClientLoggedOut;
            if (_replacingChatModule) 
                client.OnChatFromClient += OnChatFromClient;

            if (_conciergedScenes.Contains(client.Scene))
            {
                _log.DebugFormat("[Concierge] {0} logs on to {1}", client.Name, client.Scene.RegionInfo.RegionName);
                AnnounceToAgentsRegion(client, String.Format("{0} logs on to {1}", client.Name, 
                                                             client.Scene.RegionInfo.RegionName));
            }
        }

        public void OnClientLoggedOut(IClientAPI client)
        {
            client.OnLogout -= OnClientLoggedOut;
            client.OnConnectionClosed -= OnClientLoggedOut;
            
            if (_conciergedScenes.Contains(client.Scene))
            {
                _log.DebugFormat("[Concierge] {0} logs off from {1}", client.Name, client.Scene.RegionInfo.RegionName);
                AnnounceToAgentsRegion(client, String.Format("{0} logs off from {1}", client.Name, 
                                                             client.Scene.RegionInfo.RegionName));
            }
        }


        public void OnMakeRootAgent(ScenePresence agent)
        {
            if (_conciergedScenes.Contains(agent.Scene))
            {
                _log.DebugFormat("[Concierge] {0} enters {1}", agent.Name, agent.Scene.RegionInfo.RegionName);
                AnnounceToAgentsRegion(agent, String.Format("{0} enters {1}", agent.Name, 
                                                            agent.Scene.RegionInfo.RegionName));
            }
        }


        public void OnMakeChildAgent(ScenePresence agent)
        {
            if (_conciergedScenes.Contains(agent.Scene))
            {
                _log.DebugFormat("[Concierge] {0} leaves {1}", agent.Name, agent.Scene.RegionInfo.RegionName);
                AnnounceToAgentsRegion(agent, String.Format("{0} leaves {1}", agent.Name, 
                                                            agent.Scene.RegionInfo.RegionName));
            }
        }


        public void ClientLoggedOut(IClientAPI client)
        {
            if (_conciergedScenes.Contains(client.Scene))
            {
                _log.DebugFormat("[Concierge] {0} logs out of {1}", client.Name, client.Scene.RegionInfo.RegionName);
                AnnounceToAgentsRegion(client, String.Format("{0} logs out of {1}", client.Name, client.Scene.RegionInfo.RegionName));
            }
        }

        static private Vector3 posOfGod = new Vector3(128, 128, 9999);

        protected void AnnounceToAgentsRegion(IClientAPI client, string msg)
        {
            ScenePresence agent = null;
            if ((client.Scene is Scene) && (client.Scene as Scene).TryGetAvatar(client.AgentId, out agent)) 
                AnnounceToAgentsRegion(agent, msg);
            else
                _log.DebugFormat("[Concierge] could not find an agent for client {0}", client.Name);
        }

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