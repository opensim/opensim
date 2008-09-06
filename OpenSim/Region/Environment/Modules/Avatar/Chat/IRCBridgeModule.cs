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

        private string m_defaultzone = null;

        private IRCChatModule m_irc = null;
        private Thread m_irc_connector = null;

        private string m_last_leaving_user = null;
        private string m_last_new_user = null;
        private List<Scene> m_scenes = new List<Scene>();

        internal object m_syncInit = new object();
        internal object m_syncLogout = new object();

        private IConfig m_config;

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

            lock (m_syncInit)
            {

                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += NewClient;
                    scene.EventManager.OnChatFromWorld += SimChat;
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
                    m_irc = new IRCChatModule(config);
                }

                if (m_irc_connector == null)
                {
                    m_irc_connector = new Thread(IRCConnectRun);
                    m_irc_connector.Name = "IRCConnectorThread";
                    m_irc_connector.IsBackground = true;
                }
                m_log.InfoFormat("[IRC] initialized for {0}, nick: {1} ", scene.RegionInfo.RegionName,
                                 m_defaultzone);
            }
        }

        public void PostInitialise()
        {
            if (null == m_irc || !m_irc.Enabled) return;

            try
            {
                //m_irc.Connect(m_scenes);
                if (m_irc_connector == null)
                {
                    m_irc_connector = new Thread(IRCConnectRun);
                    m_irc_connector.Name = "IRCConnectorThread";
                    m_irc_connector.IsBackground = true;
                }

                if (!m_irc_connector.IsAlive)
                {
                    m_irc_connector.Start();
                    ThreadTracker.Add(m_irc_connector);
                }
            }
            catch (Exception)
            {
            }
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

        public void SimChat(Object sender, OSChatMessage e)
        {
            // We only want to relay stuff on channel 0
            if (e.Channel != 0) return;
            if (e.Message.Length == 0) return;

            // not interested in our own babblings
            if (m_irc.Equals(sender)) return;

            ScenePresence avatar = null;
            Scene scene = (Scene)e.Scene;

            if (scene == null)
                scene = m_scenes[0];

            // Filled in since it's easier than rewriting right now.
            string fromName = e.From;

            if (e.Sender != null)
            {
                avatar = scene.GetScenePresence(e.Sender.AgentId);
            }

            if (avatar != null)
            {
                fromName = avatar.Firstname + " " + avatar.Lastname;
            }

            // Try to reconnect to server if not connected
            if (m_irc.Enabled && !m_irc.Connected)
            {
                // In a non-blocking way. Eventually the connector will get it started
                try
                {
                    if (m_irc_connector == null)
                    {
                        m_irc_connector = new Thread(IRCConnectRun);
                        m_irc_connector.Name = "IRCConnectorThread";
                        m_irc_connector.IsBackground = true;
                    }

                    if (!m_irc_connector.IsAlive)
                    {
                        m_irc_connector.Start();
                        ThreadTracker.Add(m_irc_connector);
                    }
                }
                catch (Exception)
                {
                }
            }

            if (e.Message.StartsWith("/me ") && (null != avatar))
                e.Message = String.Format("{0} {1}", fromName, e.Message.Substring(4));

            // this is to keep objects from talking to IRC
            if (m_irc.Connected && (avatar != null))
                m_irc.PrivMsg(fromName, scene.RegionInfo.RegionName, e.Message);
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            try
            {
                string clientName = String.Format("{0} {1}", client.FirstName, client.LastName);

                client.OnChatFromViewer += SimChat;
                client.OnLogout += ClientLoggedOut;
                client.OnConnectionClosed += ClientLoggedOut;

                if (clientName != m_last_new_user)
                {
                    if ((m_irc.Enabled) && (m_irc.Connected))
                    {
                        m_log.DebugFormat("[IRC] {0} logging on", clientName);
                        m_irc.PrivMsg(m_irc.Nick, "Sim",
                        String.Format("notices {0} logging on", clientName));
                    }
                    m_last_new_user = clientName;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[IRC]: NewClient exception trap:" + ex.ToString());
            }
        }

        public void OnMakeRootAgent(ScenePresence presence)
        {
            try
            {
                if ((m_irc.Enabled) && (m_irc.Connected))
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
                if ((m_irc.Enabled) && (m_irc.Connected))
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


        public void ClientLoggedOut(IClientAPI client)
        {
            lock (m_syncLogout)
            {
                try
                {
                    if ((m_irc.Enabled) && (m_irc.Connected))
                    {
                        string clientName = String.Format("{0} {1}", client.FirstName, client.LastName);
                        // handles simple case. May not work for hundred connecting in per second.
                        // and the NewClients calles getting interleved
                        // but filters out multiple reports
                        if (clientName != m_last_leaving_user)
                        {
                            Console.WriteLine("Avatar was seen logging out.");
                            //Console.ReadLine();
                            Console.WriteLine();
                            m_last_leaving_user = clientName;
                            m_irc.PrivMsg(m_irc.Nick, "Sim", String.Format("notices {0} logging out", clientName));
                            m_log.InfoFormat("[IRC]: {0} logging out", clientName);
                        }

                        if (m_last_new_user == clientName)
                            m_last_new_user = null;
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[IRC]: ClientLoggedOut exception trap:" + ex.ToString());
                }
            }
        }

        // if IRC is enabled then just keep trying using a monitor thread
        public void IRCConnectRun()
        {
            while (m_irc.Enabled)
            {
                if (!m_irc.Connected)
                {
                    m_irc.Connect(m_scenes);
                }
                Thread.Sleep(15000);
            }
        }
    }

    internal class IRCChatModule
    {
        #region ErrorReplies enum

        public enum ErrorReplies
        {
            NotRegistered = 451, // ":You have not registered"
            NicknameInUse = 433 // "<nick> :Nickname is already in use"
        }

        #endregion

        #region Replies enum

        public enum Replies
        {
            MotdStart = 375, // ":- <server> Message of the day - "
            Motd = 372, // ":- <text>"
            EndOfMotd = 376 // ":End of /MOTD command"
        }

        #endregion

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Thread listener;

        private string m_basenick = null;
        private string m_channel = null;
        private bool m_nrnick = false;
        private bool m_connected = false;
        private bool m_enabled = false;
        private List<Scene> m_last_scenes = null;
        private string m_nick = null;
        private uint m_port = 6668;
        private string m_privmsgformat = "PRIVMSG {0} :<{1} in {2}>: {3}";
        private StreamReader m_reader;
        private List<Scene> m_scenes = null;
        private string m_server = null;

        private NetworkStream m_stream;
        internal object m_syncConnect = new object();
        private TcpClient m_tcp;
        private string m_user = "USER OpenSimBot 8 * :I'm an OpenSim to IRC bot";
        private StreamWriter m_writer;

        private Thread pingSender;

        public IRCChatModule(IConfigSource config)
        {
            m_nick = "OSimBot" + Util.RandomClass.Next(1, 99);
            m_tcp = null;
            m_writer = null;
            m_reader = null;

            // configuration in OpenSim.ini
            // [IRC]
            // server  = chat.freenode.net
            // nick    = OSimBot_mysim
            // nicknum = true
            // ;nicknum set to true appends a 2 digit random number to the nick
            // ;username = USER OpenSimBot 8 * :I'm a OpenSim to irc bot
            // ; username is the IRC command line sent
            // ; USER <irc_user> <visible=8,invisible=0> * : <IRC_realname>
            // channel = #opensim-regions
            // port = 6667
            // ;MSGformat fields : 0=botnick, 1=user, 2=region, 3=message
            // ;for <bot>:<user in region> :<message>
            // ;msgformat = "PRIVMSG {0} :<{1} in {2}>: {3}"
            // ;for <bot>:<message> - <user of region> :
            // ;msgformat = "PRIVMSG {0} : {3} - {1} of {2}"
            // ;for <bot>:<message> - from <user> :
            // ;msgformat = "PRIVMSG {0} : {3} - from {1}"
            // Traps I/O disconnects so it does not crash the sim
            // Trys to reconnect if disconnected and someone says something
            // Tells IRC server "QUIT" when doing a close (just to be nice)
            // Default port back to 6667

            try
            {
                m_server = config.Configs["IRC"].GetString("server");
                m_nick = config.Configs["IRC"].GetString("nick");
                m_basenick = m_nick;
                m_nrnick = config.Configs["IRC"].GetBoolean("nicknum", true);
                m_channel = config.Configs["IRC"].GetString("channel");
                m_port = (uint)config.Configs["IRC"].GetInt("port", (int)m_port);
                m_user = config.Configs["IRC"].GetString("username", m_user);
                m_privmsgformat = config.Configs["IRC"].GetString("msgformat", m_privmsgformat);
                if (m_server != null && m_nick != null && m_channel != null)
                {
                    if (m_nrnick == true)
                    {
                        m_nick = m_nick + Util.RandomClass.Next(1, 99);
                    }
                    m_enabled = true;
                }
            }
            catch (Exception ex)
            {
                m_log.Info("[IRC]: Incomplete IRC configuration, skipping IRC bridge configuration");
                m_log.DebugFormat("[IRC] Incomplete IRC configuration: {0}", ex.ToString());
            }
        }

        public bool Enabled
        {
            get { return m_enabled; }
        }

        public bool Connected
        {
            get { return m_connected; }
        }

        public string Nick
        {
            get { return m_nick; }
        }

        public bool Connect(List<Scene> scenes)
        {
            lock (m_syncConnect)
            {
                try
                {
                    if (m_connected) return true;

                    m_scenes = scenes;
                    if (m_last_scenes == null)
                    {
                        m_last_scenes = scenes;
                    }

                    m_tcp = new TcpClient(m_server, (int)m_port);
                    m_log.Info("[IRC]: Connecting...");
                    m_stream = m_tcp.GetStream();
                    m_log.Info("[IRC]: Connected to " + m_server);
                    m_reader = new StreamReader(m_stream);
                    m_writer = new StreamWriter(m_stream);

                    pingSender = new Thread(new ThreadStart(PingRun));
                    pingSender.Name = "PingSenderThread";
                    pingSender.IsBackground = true;
                    pingSender.Start();
                    ThreadTracker.Add(pingSender);

                    listener = new Thread(new ThreadStart(ListenerRun));
                    listener.Name = "IRCChatModuleListenerThread";
                    listener.IsBackground = true;
                    listener.Start();
                    ThreadTracker.Add(listener);

                    m_writer.WriteLine(m_user);
                    m_writer.Flush();
                    m_writer.WriteLine(String.Format("NICK {0}", m_nick));
                    m_writer.Flush();
                    m_writer.WriteLine(String.Format("JOIN {0}", m_channel));
                    m_writer.Flush();
                    m_log.Info("[IRC]: Connection fully established");
                    m_connected = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[IRC] cannot connect to {0}:{1}: {2}",
                                      m_server, m_port, e.Message);
                }
                return m_connected;
            }
        }

        public void Reconnect()
        {
            m_connected = false;
            try
            {
                listener.Abort();
                pingSender.Abort();
                m_writer.Close();
                m_reader.Close();
                m_tcp.Close();
            }
            catch (Exception)
            {
            }

            if (m_enabled)
            {
                Connect(m_last_scenes);
            }
        }

        public void PrivMsg(string from, string region, string msg)
        {
            // One message to the IRC server
            try
            {
                m_writer.WriteLine(m_privmsgformat, m_channel, from, region, msg);
                m_writer.Flush();
                m_log.InfoFormat("[IRC]: PrivMsg {0} in {1}: {2}", from, region, msg);
            }
            catch (IOException)
            {
                m_log.Error("[IRC]: Disconnected from IRC server.(PrivMsg)");
                Reconnect();
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[IRC]: PrivMsg exception trap: {0}", ex.ToString());
            }
        }

        private Dictionary<string, string> ExtractMsg(string input)
        {
            //examines IRC commands and extracts any private messages
            // which will then be reboadcast in the Sim

            m_log.Info("[IRC]: ExtractMsg: " + input);
            Dictionary<string, string> result = null;
            //string regex = @":(?<nick>\w*)!~(?<user>\S*) PRIVMSG (?<channel>\S+) :(?<msg>.*)";
            string regex = @":(?<nick>[\w-]*)!(?<user>\S*) PRIVMSG (?<channel>\S+) :(?<msg>.*)";
            Regex RE = new Regex(regex, RegexOptions.Multiline);
            MatchCollection matches = RE.Matches(input);

            // Get some direct matches $1 $4 is a
            if ((matches.Count == 0) || (matches.Count != 1) || (matches[0].Groups.Count != 5))
            {
                m_log.Info("[IRC]: Number of matches: " + matches.Count);
                if (matches.Count > 0)
                {
                    m_log.Info("[IRC]: Number of groups: " + matches[0].Groups.Count);
                }
                return null;
            }

            result = new Dictionary<string, string>();
            result.Add("nick", matches[0].Groups[1].Value);
            result.Add("user", matches[0].Groups[2].Value);
            result.Add("channel", matches[0].Groups[3].Value);
            result.Add("msg", matches[0].Groups[4].Value);

            return result;
        }

        public void PingRun()
        {
            // IRC keep alive thread
            // send PING ever 15 seconds
            while (m_enabled)
            {
                try
                {
                    if (m_connected == true)
                    {
                        m_writer.WriteLine(String.Format("PING :{0}", m_server));
                        m_writer.Flush();
                        Thread.Sleep(15000);
                    }
                }
                catch (IOException)
                {
                    if (m_enabled)
                    {
                        m_log.Error("[IRC]: Disconnected from IRC server.(PingRun)");
                        Reconnect();
                    }
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[IRC]: PingRun exception trap: {0}\n{1}", ex.ToString(), ex.StackTrace);
                }
            }
        }

        public void ListenerRun()
        {
            string inputLine;
            Vector3 pos = new Vector3(128, 128, 20);
            while (m_enabled)
            {
                try
                {
                    while ((m_connected == true) && ((inputLine = m_reader.ReadLine()) != null))
                    {
                        // Console.WriteLine(inputLine);
                        if (inputLine.Contains(m_channel))
                        {
                            Dictionary<string, string> data = ExtractMsg(inputLine);
                            // Any chat ???
                            if (data != null)
                            {
                                OSChatMessage c = new OSChatMessage();
                                c.Message = data["msg"];
                                c.Type = ChatTypeEnum.Say;
                                c.Channel = 0;
                                c.Position = pos;
                                c.From = data["nick"];
                                c.Sender = null;
                                c.SenderUUID = UUID.Zero;

                                // is message "\001ACTION foo
                                // bar\001"? -> "/me foo bar"
                                if ((1 == c.Message[0]) && c.Message.Substring(1).StartsWith("ACTION"))
                                    c.Message = String.Format("/me {0}", c.Message.Substring(8, c.Message.Length - 9));

                                foreach (Scene scene in m_scenes)
                                {
                                    c.Scene = scene;
                                    scene.EventManager.TriggerOnChatBroadcast(this, c);
                                }
                            }

                            Thread.Sleep(150);
                            continue;
                        }

                        ProcessIRCCommand(inputLine);
                        Thread.Sleep(150);
                    }
                }
                catch (IOException)
                {
                    if (m_enabled)
                    {
                        m_log.Error("[IRC]: ListenerRun IOException. Disconnected from IRC server ??? (ListenerRun)");
                        Reconnect();
                    }
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[IRC]: ListenerRun exception trap: {0}\n{1}", ex.ToString(), ex.StackTrace);
                }
            }
        }

        public void BroadcastSim(string sender, string format, params string[] args)
        {
            try
            {
                OSChatMessage c = new OSChatMessage();
                c.From = sender;
                c.Message = String.Format(format, args);
                c.Type = ChatTypeEnum.Say;
                c.Channel = 0;
                c.Position = new Vector3(128, 128, 20);
                c.Sender = null;
                c.SenderUUID = UUID.Zero;

                foreach (Scene m_scene in m_scenes)
                {
                    c.Scene = m_scene;
                    m_scene.EventManager.TriggerOnChatBroadcast(this, c);
                }
            }
            catch (Exception ex) // IRC gate should not crash Sim
            {
                m_log.ErrorFormat("[IRC]: BroadcastSim Exception Trap: {0}\n{1}", ex.ToString(), ex.StackTrace);
            }
        }

        public void ProcessIRCCommand(string command)
        {
            //m_log.Info("[IRC]: ProcessIRCCommand:" + command);

            string[] commArgs = new string[command.Split(' ').Length];
            string c_server = m_server;

            commArgs = command.Split(' ');
            if (commArgs[0].Substring(0, 1) == ":")
            {
                commArgs[0] = commArgs[0].Remove(0, 1);
            }

            if (commArgs[1] == "002")
            {
                // fetch the correct servername
                // ex: irc.freenode.net -> brown.freenode.net/kornbluth.freenode.net/...
                //     irc.bluewin.ch -> irc1.bluewin.ch/irc2.bluewin.ch

                c_server = (commArgs[6].Split('['))[0];
                m_server = c_server;
            }

            if (commArgs[0] == "ERROR")
            {
                m_log.ErrorFormat("[IRC]: IRC SERVER ERROR: {0}", command);
            }

            if (commArgs[0] == "PING")
            {
                string p_reply = "";

                for (int i = 1; i < commArgs.Length; i++)
                {
                    p_reply += commArgs[i] + " ";
                }

                m_writer.WriteLine(String.Format("PONG {0}", p_reply));
                m_writer.Flush();
            }
            else if (commArgs[0] == c_server)
            {
                // server message
                try
                {
                    Int32 commandCode = Int32.Parse(commArgs[1]);
                    switch (commandCode)
                    {
                        case (int)ErrorReplies.NicknameInUse:
                            // Gen a new name
                            m_nick = m_basenick + Util.RandomClass.Next(1, 99);
                            m_log.ErrorFormat("[IRC]: IRC SERVER reports NicknameInUse, trying {0}", m_nick);
                            // Retry
                            m_writer.WriteLine(String.Format("NICK {0}", m_nick));
                            m_writer.Flush();
                            m_writer.WriteLine(String.Format("JOIN {0}", m_channel));
                            m_writer.Flush();
                            break;
                        case (int)ErrorReplies.NotRegistered:
                            break;
                        case (int)Replies.EndOfMotd:
                            break;
                    }
                }
                catch (Exception)
                {
                }
            }
            else
            {
                // Normal message
                string commAct = commArgs[1];
                switch (commAct)
                {
                    case "JOIN":
                        eventIrcJoin(commArgs);
                        break;
                    case "PART":
                        eventIrcPart(commArgs);
                        break;
                    case "MODE":
                        eventIrcMode(commArgs);
                        break;
                    case "NICK":
                        eventIrcNickChange(commArgs);
                        break;
                    case "KICK":
                        eventIrcKick(commArgs);
                        break;
                    case "QUIT":
                        eventIrcQuit(commArgs);
                        break;
                    case "PONG":
                        break; // that's nice
                }
            }
        }

        public void eventIrcJoin(string[] commArgs)
        {
            string IrcChannel = commArgs[2];
            if (IrcChannel.StartsWith(":"))
                IrcChannel = IrcChannel.Substring(1);
            string IrcUser = commArgs[0].Split('!')[0];
            BroadcastSim(IrcUser, "/me joins {0}", IrcChannel);
        }

        public void eventIrcPart(string[] commArgs)
        {
            string IrcChannel = commArgs[2];
            string IrcUser = commArgs[0].Split('!')[0];
            BroadcastSim(IrcUser, "/me parts {0}", IrcChannel);
        }

        public void eventIrcMode(string[] commArgs)
        {
            string UserMode = "";
            for (int i = 3; i < commArgs.Length; i++)
            {
                UserMode += commArgs[i] + " ";
            }

            if (UserMode.Substring(0, 1) == ":")
            {
                UserMode = UserMode.Remove(0, 1);
            }
        }

        public void eventIrcNickChange(string[] commArgs)
        {
            string UserOldNick = commArgs[0].Split('!')[0];
            string UserNewNick = commArgs[2].Remove(0, 1);
            BroadcastSim(UserOldNick, "/me is now known as {0}", UserNewNick);
        }

        public void eventIrcKick(string[] commArgs)
        {
            string UserKicker = commArgs[0].Split('!')[0];
            string UserKicked = commArgs[3];
            string IrcChannel = commArgs[2];
            string KickMessage = "";
            for (int i = 4; i < commArgs.Length; i++)
            {
                KickMessage += commArgs[i] + " ";
            }
            BroadcastSim(UserKicker, "/me kicks kicks {0} off {1} saying \"{2}\"", UserKicked, IrcChannel, KickMessage);
            if (UserKicked == m_nick)
            {
                BroadcastSim(m_nick, "Hey, that was me!!!");
            }
        }

        public void eventIrcQuit(string[] commArgs)
        {
            string IrcUser = commArgs[0].Split('!')[0];
            string QuitMessage = "";

            for (int i = 2; i < commArgs.Length; i++)
            {
                QuitMessage += commArgs[i] + " ";
            }
            BroadcastSim(IrcUser, "/me quits saying \"{0}\"", QuitMessage);
        }

        public void Close()
        {
            m_writer.WriteLine(String.Format("QUIT :{0} to {1} wormhole to {2} closing",
                                             m_nick, m_channel, m_server));
            m_writer.Flush();

            m_connected = false;
            m_enabled = false;

            // listener.Abort();
            // pingSender.Abort();

            m_writer.Close();
            m_reader.Close();

            m_tcp.Close();
        }
    }
}
