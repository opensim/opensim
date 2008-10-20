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
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Chat
{
    public class IRCBridgeModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;


        private IRCConnector m_irc = null;

        private string m_last_leaving_user = null;
        private string m_last_new_user = null;
        private List<Scene> m_scenes = new List<Scene>();

        internal object m_syncInit = new object();
        internal object m_syncLogout = new object();

        private IConfig m_config;
        private string m_defaultzone = null;
        private bool m_commandsEnabled = false;
        private int m_commandChannel = -1;
        private bool m_relayPrivateChannels = false;
        private int m_relayChannelOut = -1;
        private bool m_clientReporting = true;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            try
            {
                if ((m_config = config.Configs["IRC"]) == null)
                {
                    m_log.InfoFormat("[IRC] module not configured");
                    return;
                }

                if (!m_config.GetBoolean("enabled", false))
                {
                    m_log.InfoFormat("[IRC] module disabled in configuration");
                    return;
                }
            }
            catch (Exception)
            {
                m_log.Info("[IRC] module not configured");
                return;
            }

            m_commandsEnabled = m_config.GetBoolean("commands_enabled", m_commandsEnabled);
            m_commandChannel = m_config.GetInt("commandchannel", m_commandChannel); // compat
            m_commandChannel = m_config.GetInt("command_channel", m_commandChannel);

            m_relayPrivateChannels = m_config.GetBoolean("relay_private_channels", m_relayPrivateChannels);
            m_relayChannelOut = m_config.GetInt("relay_private_channel_out", m_relayChannelOut);

            m_clientReporting = m_config.GetBoolean("report_clients", m_clientReporting);

            lock (m_syncInit)
            {

                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += OnNewClient;
                    scene.EventManager.OnChatFromWorld += OnSimChat;
                    scene.EventManager.OnChatFromClient += OnSimChat;
                    scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
                    scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
                }

                try
                {
                    m_defaultzone = config.Configs["IRC"].GetString("fallback_region", "Sim");
                }
                catch (Exception)
                {
                }

                // setup IRC Relay
                if (m_irc == null)
                {
                    m_irc = new IRCConnector(config);
                }
                m_irc.AddScene(scene);
                
                m_log.InfoFormat("[IRC] initialized for {0}, nick: {1}, commands {2}, private channels {3}", 
                                 scene.RegionInfo.RegionName, m_defaultzone, 
                                 m_commandsEnabled ? "enabled" : "not enabled",
                                 m_relayPrivateChannels ? "relayed" : "not relayed");
            }
        }

        public void PostInitialise()
        {
            if (null == m_irc || !m_irc.Enabled) return;
            m_irc.Start();
        }

        public void Close()
        {
            if (null != m_irc)
            {
                m_irc.Close();
                m_log.Info("[IRC] closed connection to IRC server");
            }
        }

        public string Name
        {
            get { return "IRCBridgeModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region ISimChat Members

        public void OnSimChat(Object sender, OSChatMessage c)
        {
            // early return if nothing to forward
            if (c.Message.Length == 0) return;

            // early return if this comes from the IRC forwarder
            if (m_irc.Equals(sender)) return;

            m_log.DebugFormat("[IRC] heard on channel {0}: {1}", c.Channel, c.Message);

            // check for commands coming from avatars or in-world
            // object (if commands are enabled)
            if (m_commandsEnabled && c.Channel == m_commandChannel)
            {
                string[] messages = c.Message.Split(' ');
                string command = messages[0].ToLower();

                try
                {
                    switch (command)
                    {
                        case "channel":
                            m_irc.IrcChannel = messages[1];
                            break;
                        case "close":
                            m_irc.Close();
                            break;
                        case "connect":
                            m_irc.Connect();
                            break;
                        case "nick":
                            m_irc.Nick = messages[1];
                            break;
                        case "port":
                            m_irc.Port = Convert.ToUInt32(messages[1]);
                            break;
                        case "reconnect":
                            m_irc.Reconnect();
                            break;
                        case "server":
                            m_irc.Server = messages[1];
                            break;
                        case "client-reporting":
                            m_irc.ClientReporting = Convert.ToBoolean(messages[1]);

                            break;
                        case "in-channel":
                            m_irc.RelayChannel = Convert.ToInt32(messages[1]);
                            break;
                        case "out-channel":
                            m_relayChannelOut = Convert.ToInt32(messages[1]);
                            break;

                        default:
                            m_irc.Send(c.Message);
                            break;
                    }
                }
                catch(Exception ex)
                { 
                    m_log.DebugFormat("[IRC] error processing in-world command channel input: {0}", ex);
                }
            }

            // drop all messages coming in on a private channel,
            // except if we are relaying private channels, in which
            // case we drop if the private channel is not the
            // configured m_relayChannelOut
            if (m_relayPrivateChannels) 
            {
                if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL && c.Channel != m_relayChannelOut)
                {
                    m_log.DebugFormat("[IRC] dropping message {0} on channel {1}", c, c.Channel);
                    return;
                }
            }
            else if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL)
            {
                m_log.DebugFormat("[IRC] dropping message {0} on channel {1}", c, c.Channel);
                return;
            }

            ScenePresence avatar = null;
            Scene scene = (Scene)c.Scene;

            if (scene == null)
                scene = m_scenes[0];

            string fromName = c.From;

            if (c.Sender != null)
            {
                avatar = scene.GetScenePresence(c.Sender.AgentId);
                if (avatar != null) fromName = avatar.Name;
            }

            if (!m_irc.Connected)
            {
                m_log.WarnFormat("[IRC] IRCConnector not connected: dropping message from {0}", fromName);
                return;
            }

            if (null != avatar) 
            {
                string msg = c.Message;
                if (msg.StartsWith("/me "))
                    msg = String.Format("{0} {1}", fromName, c.Message.Substring(4));

                m_irc.PrivMsg(fromName, scene.RegionInfo.RegionName, msg);
            }
            else 
            {
                //Message came from an object
                char[] splits = { ',' };
                string[] tokens = c.Message.Split(splits,3); // This is certainly wrong
                if (tokens.Length == 3)
                {
                    if (tokens[0] == m_irc.m_accessPassword) // This is my really simple check
                    {
                        m_log.DebugFormat("[IRC] message from object {0}, {1}", tokens[0], tokens[1]);
                        m_irc.PrivMsg(tokens[1], scene.RegionInfo.RegionName, tokens[2]);
                    }
                    else
                    {
                        m_log.WarnFormat("[IRC] prim security key mismatch <{0}> not <{1}>", tokens[0], m_irc.m_accessPassword);
                    }
                }
            }
        }
        #endregion

        public void OnNewClient(IClientAPI client)
        {
            try
            {
                client.OnLogout += OnClientLoggedOut;
                client.OnConnectionClosed += OnClientLoggedOut;

                if (client.Name != m_last_new_user)
                {
                    if ((m_irc.Enabled) && (m_irc.Connected) && (m_clientReporting)) 
                    {
                        m_log.DebugFormat("[IRC] {0} logging on", client.Name);
                        m_irc.PrivMsg(m_irc.Nick, "Sim", String.Format("notices {0} logging on", client.Name));
                    }
                    m_last_new_user = client.Name;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[IRC]: OnNewClient exception trap:" + ex.ToString());
            }
        }

        public void OnMakeRootAgent(ScenePresence presence)
        {
            try
            {
                if ((m_irc.Enabled) && (m_irc.Connected) && (m_clientReporting))
                {
                    string regionName = presence.Scene.RegionInfo.RegionName;
                    string clientName = String.Format("{0} {1}", presence.Firstname, presence.Lastname);
                    m_log.DebugFormat("[IRC] noticing {0} in {1}", clientName, regionName);
                    m_irc.PrivMsg(m_irc.Nick, "Sim", String.Format("notices {0} in {1}", clientName, regionName));
                }
            }
            catch (Exception)
            {
            }
        }

        public void OnMakeChildAgent(ScenePresence presence)
        {
            try
            {
                if ((m_irc.Enabled) && (m_irc.Connected) && (m_clientReporting)) 
                {
                    string regionName = presence.Scene.RegionInfo.RegionName;
                    string clientName = String.Format("{0} {1}", presence.Firstname, presence.Lastname);
                    m_log.DebugFormat("[IRC] noticing {0} in {1}", clientName, regionName);
                    m_irc.PrivMsg(m_irc.Nick, "Sim", String.Format("notices {0} left {1}", clientName, regionName));
                }
            }
            catch (Exception)
            {
            }
        }


        public void OnClientLoggedOut(IClientAPI client)
        {
            lock (m_syncLogout)
            {
                try
                {
                    if ((m_irc.Enabled) && (m_irc.Connected) && (m_clientReporting)) 
                    {
                        // handles simple case. May not work for
                        // hundred connecting in per second.  and
                        // OnNewClients calle getting interleaved but
                        // filters out multiple reports
                        if (client.Name != m_last_leaving_user)
                        {
                            m_last_leaving_user = client.Name;
                            m_irc.PrivMsg(m_irc.Nick, "Sim", String.Format("notices {0} logging out", client.Name));
                            m_log.InfoFormat("[IRC]: {0} logging out", client.Name);
                        }

                        if (m_last_new_user == client.Name)
                            m_last_new_user = null;
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[IRC]: ClientLoggedOut exception trap:" + ex.ToString());
                }
            }
        }
    }
}
