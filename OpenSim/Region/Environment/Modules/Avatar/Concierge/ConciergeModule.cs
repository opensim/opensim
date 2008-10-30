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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.Avatar.Chat;

namespace OpenSim.Region.Environment.Modules.Avatar.Concierge
{
    public class ConciergeModule : ChatModule, IRegionModule
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;

        private List<IScene> _scenes = new List<IScene>();
        private List<IScene> _conciergedScenes = new List<IScene>();
        private Dictionary<IScene, List<ScenePresence>> _sceneAttendees = new Dictionary<IScene, List<ScenePresence>>();
        private bool _replacingChatModule = false;

        private IConfig _config;
        
        private string _whoami = "conferencier";
        private Regex _regions = null;
        private string _welcomes = null;
        private int _conciergeChannel = 42;
        private string _announceEntering = "{0} enters {1} (now {2} visitors in this region)";
        private string _announceLeaving = "{0} leaves {1} (back to {2} visitors in this region)";
        private string _xmlRpcPassword = String.Empty;

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
            _welcomes = _config.GetString("welcomes", _welcomes);
            _announceEntering = _config.GetString("announce_entering", _announceEntering);
            _announceLeaving = _config.GetString("announce_leaving", _announceLeaving);
            _xmlRpcPassword = _config.GetString("password", _xmlRpcPassword);
            _log.InfoFormat("[Concierge] reporting as \"{0}\" to our users", _whoami);

            // calculate regions Regex
            if (_regions == null)
            {
                string regions = _config.GetString("regions", String.Empty);
                if (!String.IsNullOrEmpty(regions))
                {
                    _regions = new Regex(@regions, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
            }

            scene.CommsManager.HttpServer.AddXmlRPCHandler("concierge_update_welcome", XmlRpcUpdateWelcomeMethod, false);

            lock (_syncy)
            {
                if (!_scenes.Contains(scene))
                {
                    _scenes.Add(scene);

                    if (_regions == null || _regions.IsMatch(scene.RegionInfo.RegionName))
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
                // replacing ChatModule: need to redistribute
                // ChatFromClient to interested subscribers
                c = FixPositionOfChatMessage(c);

                Scene scene = (Scene)c.Scene;
                scene.EventManager.TriggerOnChatFromClient(sender, c);

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
        }

        

        public void OnClientLoggedOut(IClientAPI client)
        {
            client.OnLogout -= OnClientLoggedOut;
            client.OnConnectionClosed -= OnClientLoggedOut;
            
            if (_conciergedScenes.Contains(client.Scene))
            {
                _log.DebugFormat("[Concierge] {0} logs off from {1}", client.Name, client.Scene.RegionInfo.RegionName);
                ScenePresence agent = (client.Scene as Scene).GetScenePresence(client.AgentId);
                RemoveFromAttendeeList(agent, agent.Scene);
                AnnounceToAgentsRegion(agent, String.Format(_announceLeaving, agent.Name, agent.Scene.RegionInfo.RegionName,
                                                            _sceneAttendees[agent.Scene].Count));
            }
        }


        public void OnMakeRootAgent(ScenePresence agent)
        {
            if (_conciergedScenes.Contains(agent.Scene))
            {
                _log.DebugFormat("[Concierge] {0} enters {1}", agent.Name, agent.Scene.RegionInfo.RegionName);
                AddToAttendeeList(agent, agent.Scene);
                WelcomeAvatar(agent, agent.Scene);
                AnnounceToAgentsRegion(agent, String.Format(_announceEntering, agent.Name, agent.Scene.RegionInfo.RegionName,
                                                            _sceneAttendees[agent.Scene].Count));
            }
        }


        public void OnMakeChildAgent(ScenePresence agent)
        {
            if (_conciergedScenes.Contains(agent.Scene))
            {
                _log.DebugFormat("[Concierge] {0} leaves {1}", agent.Name, agent.Scene.RegionInfo.RegionName);
                RemoveFromAttendeeList(agent, agent.Scene);
                AnnounceToAgentsRegion(agent, String.Format(_announceLeaving, agent.Name, agent.Scene.RegionInfo.RegionName,
                                                            _sceneAttendees[agent.Scene].Count));
            }
        }

        protected void AddToAttendeeList(ScenePresence agent, Scene scene)
        {
            lock (_sceneAttendees)
            {
                if (!_sceneAttendees.ContainsKey(scene))
                    _sceneAttendees[scene] = new List<ScenePresence>();
                List<ScenePresence> attendees = _sceneAttendees[scene];
                if (!attendees.Contains(agent))
                    attendees.Add(agent);
            }
        }

        protected void RemoveFromAttendeeList(ScenePresence agent, Scene scene)
        {
            lock (_sceneAttendees)
            {
                if (!_sceneAttendees.ContainsKey(scene))
                {
                    _log.WarnFormat("[Concierge] attendee list missing for region {0}", scene.RegionInfo.RegionName);
                    return;
                }
                List<ScenePresence> attendees = _sceneAttendees[scene];
                if (!attendees.Contains(agent))
                {
                    _log.WarnFormat("[Concierge] avatar {0} sneaked in (not on attendee list of region {1})",
                                    agent.Name, scene.RegionInfo.RegionName);
                    return;
                }
                attendees.Remove(agent);
            }
        }

        protected void WelcomeAvatar(ScenePresence agent, Scene scene)
        {
            // welcome mechanics: check whether we have a welcomes
            // directory set and wether there is a region specific
            // welcome file there: if yes, send it to the agent
            if (!String.IsNullOrEmpty(_welcomes))
            {
                string[] welcomes = new string[] { 
                    Path.Combine(_welcomes, agent.Scene.RegionInfo.RegionName),
                    Path.Combine(_welcomes, "DEFAULT")};
                foreach (string welcome in welcomes)
                {
                    if (File.Exists(welcome)) 
                    {
                        try
                        {
                            string[] welcomeLines = File.ReadAllLines(welcome);
                            foreach (string l in welcomeLines)
                            {
                                AnnounceToAgent(agent, String.Format(l, agent.Name, scene.RegionInfo.RegionName, _whoami));
                            }
                        }
                        catch (IOException ioe)
                        {
                            _log.ErrorFormat("[Concierge] run into trouble reading welcome file {0} for region {1} for avatar {2}: {3}",
                                             welcome, scene.RegionInfo.RegionName, agent.Name, ioe);
                        }
                        catch (FormatException fe)
                        {
                            _log.ErrorFormat("[Concierge] welcome file {0} is malformed: {1}", welcome, fe);
                        }
                    } 
                    return;
                }
                _log.DebugFormat("[Concierge] no welcome message for region {0}", scene.RegionInfo.RegionName);
            }
        }

        static private Vector3 PosOfGod = new Vector3(128, 128, 9999);

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
            c.Position = PosOfGod;
            c.From = _whoami;
            c.Sender = null;
            c.SenderUUID = UUID.Zero;
            c.Scene = scenePresence.Scene;

            scenePresence.Scene.EventManager.TriggerOnChatBroadcast(this, c);
        }

        protected void AnnounceToAgent(ScenePresence agent, string msg)
        {
            OSChatMessage c = new OSChatMessage();
            c.Message = msg;
            c.Type = ChatTypeEnum.Say;
            c.Channel = 0;
            c.Position = PosOfGod;
            c.From = _whoami;
            c.Sender = null;
            c.SenderUUID = UUID.Zero;
            c.Scene = agent.Scene;

            agent.ControllingClient.SendChatMessage(msg, (byte) ChatTypeEnum.Say, PosOfGod, _whoami, UUID.Zero, 
                                                    (byte)ChatSourceType.Object, (byte)ChatAudibleLevel.Fully);
        }

        private static void checkStringParameters(XmlRpcRequest request, string[] param)
        {
            Hashtable requestData = (Hashtable) request.Params[0];
            foreach (string p in param)
            {
                if (!requestData.Contains(p))
                    throw new Exception(String.Format("missing string parameter {0}", p));
                if (String.IsNullOrEmpty((string)requestData[p]))
                    throw new Exception(String.Format("parameter {0} is empty", p));
            }
        }

        public XmlRpcResponse XmlRpcUpdateWelcomeMethod(XmlRpcRequest request)
        {
            _log.Info("[Concierge]: processing UpdateWelcome request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            try
            {
                Hashtable requestData = (Hashtable)request.Params[0];
                checkStringParameters(request, new string[] { "password", "region", "welcome" });

                // check password
                if (!String.IsNullOrEmpty(_xmlRpcPassword) &&
                    (string)requestData["password"] != _xmlRpcPassword) throw new Exception("wrong password");

                if (String.IsNullOrEmpty(_welcomes))
                    throw new Exception("welcome templates are not enabled, ask your OpenSim operator to set the \"welcomes\" option in the [Concierge] section of OpenSim.ini");

                string msg = (string)requestData["welcome"];
                if (String.IsNullOrEmpty(msg))
                    throw new Exception("empty parameter \"welcome\"");

                string regionName = (string)requestData["region"];
                IScene scene = _scenes.Find(delegate(IScene s) { return s.RegionInfo.RegionName == regionName; });
                if (scene == null) 
                    throw new Exception(String.Format("unknown region \"{0}\"", regionName));

                if (!_conciergedScenes.Contains(scene))
                    throw new Exception(String.Format("region \"{0}\" is not a concierged region.", regionName));

                string welcome = Path.Combine(_welcomes, regionName);
                if (File.Exists(welcome))
                {
                    _log.InfoFormat("[Concierge] UpdateWelcome: updating existing template \"{0}\"", welcome);
                    string welcomeBackup = String.Format("{0}~", welcome);
                    if (File.Exists(welcomeBackup))
                        File.Delete(welcomeBackup);
                    File.Move(welcome, welcomeBackup);
                }
                File.WriteAllText(welcome, msg);

                responseData["success"] = "true";
                response.Value = responseData;
            }
            catch (Exception e)
            {
                _log.InfoFormat("[Concierge] UpdateWelcome failed: {0}", e.Message);

                responseData["success"] = "false";
                responseData["error"] = e.Message;

                response.Value = responseData;
            }
            _log.Debug("[Concierge]: done processing UpdateWelcome request");
            return response;
        }
    }
}
