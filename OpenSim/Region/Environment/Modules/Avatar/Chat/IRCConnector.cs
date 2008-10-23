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
    public class IRCConnector
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

        private Thread m_listener = null;
        private Thread m_watchdog = null;
        private Thread m_pinger = null;

        private bool m_randomizeNick = true;

        public string m_baseNick = null;
        private string m_nick = null;
        public string Nick
        {
            get { return m_baseNick; }
            set { m_baseNick = value; }
        }
        
        private bool m_enabled = false;
        public bool Enabled
        {
            get { return m_enabled; }
        }

        private bool m_connected = false;
        public bool Connected
        {
            get { return m_connected; }
        }

        private string m_ircChannel;
        public string IrcChannel
        {
            get { return m_ircChannel; }

            set { m_ircChannel = value; }
        }
        
        private bool m_relayPrivateChannels = false;
        public bool RelayPrivateChannels
        {
            get { return m_relayPrivateChannels; } 
            set { m_relayPrivateChannels = value; }
        }

        private int m_relayChannel = 0;
        public int RelayChannel
        {
            get { return m_relayChannel; }
            set { m_relayChannel = value; }
        }

        private bool m_clientReporting = true;
        public bool ClientReporting
        {
            get { return m_clientReporting; }
            set { m_clientReporting = value; }
        }

        private uint m_port = 6667;
        public uint Port 
        {
            get { return m_port; }
            set { m_port = value; }
        }

        private string m_server = null;
        public string Server
        {
            get { return m_server; }
            set { m_server = value; }
        }

        private string m_privmsgformat = "PRIVMSG {0} :<{1} in {2}>: {3}";
        private StreamReader m_reader;
        private List<Scene> m_scenes = new List<Scene>();

        private NetworkStream m_stream = null;
        internal object m_syncConnect = new object();
        private TcpClient m_tcp;
        private string m_user = "USER OpenSimBot 8 * :I'm an OpenSim to IRC bot";
        private StreamWriter m_writer;


        public IRCConnector(IConfigSource config)
        {
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
                m_baseNick = config.Configs["IRC"].GetString("nick", "OSimBot");

                m_randomizeNick = config.Configs["IRC"].GetBoolean("randomize_nick", m_randomizeNick);
                m_randomizeNick = config.Configs["IRC"].GetBoolean("nicknum", m_randomizeNick); // compat
                m_ircChannel = config.Configs["IRC"].GetString("channel");
                m_port = (uint)config.Configs["IRC"].GetInt("port", (int)m_port);
                m_user = config.Configs["IRC"].GetString("username", m_user);
                m_privmsgformat = config.Configs["IRC"].GetString("msgformat", m_privmsgformat);

                m_clientReporting = config.Configs["IRC"].GetInt("verbosity", 2) > 0;
                m_clientReporting = config.Configs["IRC"].GetBoolean("report_clients", m_clientReporting);

                m_relayPrivateChannels = config.Configs["IRC"].GetBoolean("relay_private_channels", m_relayPrivateChannels);
                m_relayPrivateChannels = config.Configs["IRC"].GetBoolean("useworldcomm", m_relayPrivateChannels); //compat
                m_relayChannel = config.Configs["IRC"].GetInt("relay_private_channel_in", m_relayChannel);
                m_relayChannel = config.Configs["IRC"].GetInt("inchannel", m_relayChannel);

                if (m_server != null && m_baseNick != null && m_ircChannel != null)
                {
                    if (m_randomizeNick)
                    {
                        m_nick = m_baseNick + Util.RandomClass.Next(1, 99);
                    }
                    m_enabled = true;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[IRCConnector]: Incomplete IRC configuration, skipping IRC bridge configuration");
                m_log.DebugFormat("[IRCConnector] Incomplete IRC configuration: {0}", ex.ToString());
            }

            if (null == m_watchdog)
            {
                m_watchdog = new Thread(WatchdogRun);
                m_watchdog.Name = "IRCWatchdog";
                m_watchdog.IsBackground = true;
            }
        }

        public void Start()
        {
            if (!m_watchdog.IsAlive) 
            {
                m_watchdog.Start();
                ThreadTracker.Add(m_watchdog);
            }
        }

        public void AddScene(Scene scene)
        {
            lock (m_syncConnect) m_scenes.Add(scene);
        }

        public bool Connect()
        {
            lock (m_syncConnect)
            {
                try
                {
                    if (m_connected) return true;

                    m_tcp = new TcpClient(m_server, (int)m_port);
                    m_stream = m_tcp.GetStream();
                    m_reader = new StreamReader(m_stream);
                    m_writer = new StreamWriter(m_stream);

                    m_log.DebugFormat("[IRCConnector]: Connected to {0}:{1}", m_server, m_port); 

                    m_pinger = new Thread(new ThreadStart(PingRun));
                    m_pinger.Name = "PingSenderThread";
                    m_pinger.IsBackground = true;
                    m_pinger.Start();
                    ThreadTracker.Add(m_pinger);

                    m_listener = new Thread(new ThreadStart(ListenerRun));
                    m_listener.Name = "IRCConnectorListenerThread";
                    m_listener.IsBackground = true;
                    m_listener.Start();
                    ThreadTracker.Add(m_listener);

                    m_writer.WriteLine(m_user);
                    m_writer.Flush();
                    m_writer.WriteLine(String.Format("NICK {0}", m_nick));
                    m_writer.Flush();
                    m_writer.WriteLine(String.Format("JOIN {0}", m_ircChannel));
                    m_writer.Flush();
                    m_log.Info("[IRCConnector]: Connection fully established");
                    m_connected = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[IRCConnector] cannot connect to {0}:{1}: {2}",
                                      m_server, m_port, e.Message);
                }
                m_log.Debug("[IRCConnector] Connected");
                return m_connected;
            }
        }

        public void Reconnect()
        {
            m_connected = false;
            try
            {
                m_listener.Abort();
                m_pinger.Abort();

                m_writer.Close();
                m_reader.Close();

                m_tcp.Close();
            }
            catch (Exception)
            {
            }

            if (m_enabled)
            {
                Connect();
            }
        }

        public void PrivMsg(string from, string region, string msg)
        {
            m_log.DebugFormat("[IRCConnector] Sending message to IRC from {0}: {1}", from, msg);

            // One message to the IRC server
            try
            {
                m_writer.WriteLine(m_privmsgformat, m_ircChannel, from, region, msg);
                m_writer.Flush();
                m_log.InfoFormat("[IRCConnector]: PrivMsg {0} in {1}: {2}", from, region, msg);
            }
            catch (IOException)
            {
                m_log.Error("[IRCConnector]: Disconnected from IRC server.(PrivMsg)");
                Reconnect();
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[IRCConnector]: PrivMsg exception trap: {0}", ex.ToString());
            }
        }

        public void Send(string msg)
        {
            try
            {
                m_writer.WriteLine(msg);
                m_writer.Flush();
                m_log.Info("IRC: Sent command string: " + msg);
            }
            catch (IOException)
            {
                m_log.Error("[IRCConnector]: Disconnected from IRC server.(PrivMsg)");
                Reconnect();
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[IRCConnector]: PrivMsg exception trap: {0}", ex.ToString());
            }

        }


        private Dictionary<string, string> ExtractMsg(string input)
        {
            //examines IRC commands and extracts any private messages
            // which will then be reboadcast in the Sim

            m_log.Info("[IRCConnector]: ExtractMsg: " + input);
            Dictionary<string, string> result = null;
            string regex = @":(?<nick>[\w-]*)!(?<user>\S*) PRIVMSG (?<channel>\S+) :(?<msg>.*)";
            Regex RE = new Regex(regex, RegexOptions.Multiline);
            MatchCollection matches = RE.Matches(input);

            // Get some direct matches $1 $4 is a
            if ((matches.Count == 0) || (matches.Count != 1) || (matches[0].Groups.Count != 5))
            {
                // m_log.Info("[IRCConnector]: Number of matches: " + matches.Count);
                // if (matches.Count > 0)
                // {
                //     m_log.Info("[IRCConnector]: Number of groups: " + matches[0].Groups.Count);
                // }
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
                        m_log.Error("[IRCConnector]: Disconnected from IRC server.(PingRun)");
                        Reconnect();
                    }
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[IRCConnector]: PingRun exception trap: {0}\n{1}", ex.ToString(), ex.StackTrace);
                }
            }
        }

        static private Vector3 CenterOfRegion = new Vector3(128, 128, 20);
        public void ListenerRun()
        {
            string inputLine;

            while (m_enabled)
            {
                try
                {
                    while ((m_connected) && ((inputLine = m_reader.ReadLine()) != null))
                    {
                        // m_log.Info("[IRCConnector]: " + inputLine);

                        if (inputLine.Contains(m_ircChannel))
                        {
                            Dictionary<string, string> data = ExtractMsg(inputLine);
                            // Any chat ???
                            if (data != null)
                            {
                                OSChatMessage c = new OSChatMessage();
                                c.Message = data["msg"];
                                c.Type = ChatTypeEnum.Region;
                                c.Position = CenterOfRegion;
                                c.Channel = m_relayPrivateChannels ? m_relayChannel : 0;
                                c.From = data["nick"];
                                c.Sender = null;
                                c.SenderUUID = UUID.Zero;

                                // is message "\001ACTION foo
                                // bar\001"? -> "/me foo bar"
                                if ((1 == c.Message[0]) && c.Message.Substring(1).StartsWith("ACTION"))
                                    c.Message = String.Format("/me {0}", c.Message.Substring(8, c.Message.Length - 9));

                                m_log.DebugFormat("[IRCConnector] ListenerRun from: {0}, {1}", c.From, c.Message);

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
                        m_log.Error("[IRCConnector]: ListenerRun IOException. Disconnected from IRC server ??? (ListenerRun)");
                        Reconnect();
                    }
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[IRCConnector]: ListenerRun exception trap: {0}\n{1}", ex.ToString(), ex.StackTrace);
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
                c.Type = ChatTypeEnum.Region; // ChatTypeEnum.Say;
                c.Channel = m_relayPrivateChannels ? m_relayChannel : 0;
                c.Position = CenterOfRegion;
                c.Sender = null;
                c.SenderUUID = UUID.Zero;

                m_log.DebugFormat("[IRCConnector] BroadcastSim from {0}: {1}", c.From, c.Message);

                foreach (Scene scene in m_scenes)
                {
                    c.Scene = scene;
                    scene.EventManager.TriggerOnChatBroadcast(this, c);
                    // // m_scene.EventManager.TriggerOnChatFromWorld(this, c);
                    // IWorldComm wComm = m_scene.RequestModuleInterface<IWorldComm>();
                    // wComm.DeliverMessage(ChatTypeEnum.Region, m_messageInChannel, sender, UUID.Zero, c.Message);
                    // //IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
                    // //wComm.DeliverMessage(ChatTypeEnum.Region, channelID, m_host.Name, m_host.UUID, text);

                }
            }
            catch (Exception ex) // IRC gate should not crash Sim
            {
                m_log.ErrorFormat("[IRCConnector]: BroadcastSim Exception Trap: {0}\n{1}", ex.ToString(), ex.StackTrace);
            }
        }

        public void ProcessIRCCommand(string command)
        {
            // m_log.Debug("[IRCConnector]: ProcessIRCCommand:" + command);

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
                m_log.ErrorFormat("[IRCConnector]: IRC SERVER ERROR: {0}", command);
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
                            m_nick = m_baseNick + Util.RandomClass.Next(1, 99);
                            m_log.ErrorFormat("[IRCConnector]: IRC SERVER reports NicknameInUse, trying {0}", m_nick);
                            // Retry
                            m_writer.WriteLine(String.Format("NICK {0}", m_nick));
                            m_writer.Flush();
                            m_writer.WriteLine(String.Format("JOIN {0}", m_ircChannel));
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
            if (m_clientReporting) 
                BroadcastSim(IrcUser, "/me joins {0}", IrcChannel);
        }

        public void eventIrcPart(string[] commArgs)
        {
            string IrcChannel = commArgs[2];
            string IrcUser = commArgs[0].Split('!')[0];
            if (m_clientReporting) 
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
            if (m_clientReporting) 
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
            if (m_clientReporting) 
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
            if (m_clientReporting)
                BroadcastSim(IrcUser, "/me quits saying \"{0}\"", QuitMessage);
        }

        public void Close()
        {
            m_writer.WriteLine(String.Format("QUIT :{0} to {1} wormhole to {2} closing",
                                             m_nick, m_ircChannel, m_server));
            m_writer.Flush();

            m_connected = false;
            m_enabled = false;

            //listener.Abort();
            //pingSender.Abort();

            m_writer.Close();
            m_reader.Close();
            m_stream.Close();
            m_tcp.Close();
        }

        protected void WatchdogRun()
        {
            while (m_enabled)
            {
                if (!m_connected) Connect();
                Thread.Sleep(15000);
            }
        }
    }
}
