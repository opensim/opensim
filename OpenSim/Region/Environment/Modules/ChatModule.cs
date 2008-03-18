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
using System.Text.RegularExpressions;
using System.Threading;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class ChatModule : IRegionModule, ISimChat
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_scenes = new List<Scene>();

        private int m_whisperdistance = 10;
        private int m_saydistance = 30;
        private int m_shoutdistance = 100;

        private IRCChatModule m_irc = null;

        private string m_last_new_user = null;
        private string m_last_leaving_user = null;
        private string m_defaultzone = null;
        internal object m_syncInit = new object();
        internal object m_syncLogout = new object();
        private Thread m_irc_connector=null;

        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_syncInit)
            {
                // wrap this in a try block so that defaults will work if
                // the config file doesn't specify otherwise.
                try
                {
                    m_whisperdistance = config.Configs["Chat"].GetInt("whisper_distance", m_whisperdistance);
                    m_saydistance = config.Configs["Chat"].GetInt("say_distance", m_saydistance);
                    m_shoutdistance = config.Configs["Chat"].GetInt("shout_distance", m_shoutdistance);
                }
                catch (Exception)
                {
                }

                try
                {
                    m_defaultzone = config.Configs["IRC"].GetString("nick","Sim");
                }
                catch (Exception)
                {
                }

                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += NewClient;
                    scene.RegisterModuleInterface<ISimChat>(this);
                }

                // setup IRC Relay
                if (m_irc == null) { m_irc = new IRCChatModule(config); }
                if (m_irc_connector == null) { 
                    m_irc_connector = new Thread(IRCConnectRun);
                    m_irc_connector.Name = "IRCConnectorThread";
                    m_irc_connector.IsBackground = true;
                }
            }
        }

        public void PostInitialise()
        {
            if (m_irc.Enabled)
            {
                try
                {
                    //m_irc.Connect(m_scenes);
                    if (m_irc_connector == null) { 
                        m_irc_connector = new Thread(IRCConnectRun);
                        m_irc_connector.Name = "IRCConnectorThread";
                        m_irc_connector.IsBackground = true;
                    }
                    if (!m_irc_connector.IsAlive) { 
                        m_irc_connector.Start();
                        OpenSim.Framework.ThreadTracker.Add(m_irc_connector);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public void Close()
        {
            m_irc.Close();
        }

        public string Name
        {
            get { return "ChatModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void NewClient(IClientAPI client)
        {
            try
            {
                client.OnChatFromViewer += SimChat;

                if ((m_irc.Enabled) && (m_irc.Connected))
                {
                    string clientName = client.FirstName + " " + client.LastName;
                    // handles simple case. May not work for hundred connecting in per second.
                    // and the NewClients calles getting interleved
                    // but filters out multiple reports
                    if (clientName != m_last_new_user)
                    {
                        m_last_new_user = clientName;
                        string clientRegion = FindClientRegion(client.FirstName, client.LastName);
                        m_irc.PrivMsg(m_irc.Nick, "Sim", "notices " + clientName + " in "+clientRegion);
                    }
                }
                client.OnLogout += ClientLoggedOut;
                client.OnConnectionClosed += ClientLoggedOut;
                client.OnLogout += ClientLoggedOut;
            }
            catch (Exception ex)
            {
                m_log.Error("[IRC]: NewClient exception trap:" + ex.ToString());
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
                        string clientName = client.FirstName + " " + client.LastName;
                        string clientRegion = FindClientRegion(client.FirstName, client.LastName);
                        // handles simple case. May not work for hundred connecting in per second.
                        // and the NewClients calles getting interleved
                        // but filters out multiple reports
                        if (clientName != m_last_leaving_user)
                        {
                            m_last_leaving_user = clientName;
                            m_irc.PrivMsg(m_irc.Nick, "Sim", "notices " + clientName + " left " + clientRegion);
                            m_log.Info("[IRC]: IRC watcher notices " + clientName + " left " + clientRegion);
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("[IRC]: ClientLoggedOut exception trap:" + ex.ToString());
                }
            }
        }

        private void TrySendChatMessage(ScenePresence presence, LLVector3 fromPos, LLVector3 regionPos,
                                        LLUUID fromAgentID, string fromName, ChatTypeEnum type, string message)
        {
            if (!presence.IsChildAgent)
            {
                LLVector3 fromRegionPos = fromPos + regionPos;
                LLVector3 toRegionPos = presence.AbsolutePosition + regionPos;
                int dis = Math.Abs((int) Util.GetDistanceTo(toRegionPos, fromRegionPos));

                if (type == ChatTypeEnum.Whisper && dis > m_whisperdistance ||
                    type == ChatTypeEnum.Say && dis > m_saydistance ||
                    type == ChatTypeEnum.Shout && dis > m_shoutdistance)
                {
                    return;
                }

                // TODO: should change so the message is sent through the avatar rather than direct to the ClientView
                presence.ControllingClient.SendChatMessage(message, (byte) type, fromPos, fromName, fromAgentID);
            }
        }

        public void SimChat(Object sender, ChatFromViewerArgs e)
        {
            // FROM: Sim  TO: IRC

            ScenePresence avatar = null;

            //TODO: Move ForEachScenePresence and others into IScene.
            Scene scene = (Scene) e.Scene;

            //TODO: Remove the need for this check
            if (scene == null)
                scene = m_scenes[0];

            // Filled in since it's easier than rewriting right now.
            LLVector3 fromPos = e.Position;
            LLVector3 regionPos = new LLVector3(scene.RegionInfo.RegionLocX * Constants.RegionSize, scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);

            string fromName = e.From;
            string message = e.Message;
            LLUUID fromAgentID = LLUUID.Zero;

            if (e.Sender != null)
            {
                avatar = scene.GetScenePresence(e.Sender.AgentId);
            }

            if (avatar != null)
            {
                fromPos = avatar.AbsolutePosition;
                regionPos = new LLVector3(scene.RegionInfo.RegionLocX * Constants.RegionSize, scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);
                fromName = avatar.Firstname + " " + avatar.Lastname;
                fromAgentID = e.Sender.AgentId;
            }

            // Try to reconnect to server if not connected
            if (m_irc.Enabled && !m_irc.Connected)
            {
                // In a non-blocking way. Eventually the connector will get it started
                try
                {
                    if (m_irc_connector == null) { m_irc_connector = new Thread(IRCConnectRun);
                    m_irc_connector.Name = "IRCConnectorThread";
                    m_irc_connector.IsBackground = true;
                    }
                    if (!m_irc_connector.IsAlive) { 
                        m_irc_connector.Start();
                        OpenSim.Framework.ThreadTracker.Add(m_irc_connector);
                    }
                }
                catch (Exception)
                {
                }
            }

            if (e.Message.Length > 0)
            {
                if (m_irc.Connected && (avatar != null)) // this is to keep objects from talking to IRC
                {
                    m_irc.PrivMsg(fromName, scene.RegionInfo.RegionName, e.Message);
                }
            }

            if (e.Channel == 0)
            {
                foreach (Scene s in m_scenes)
                {
                    s.ForEachScenePresence(delegate(ScenePresence presence)
                                           {
                                               TrySendChatMessage(presence, fromPos, regionPos,
                                                                  fromAgentID, fromName, e.Type, message);
                                           });
                }
            }
        }

        // if IRC is enabled then just keep trying using a monitor thread
        public void IRCConnectRun()
        {
            while(true)
            {
                if ((m_irc.Enabled)&&(!m_irc.Connected))
                {
                    m_irc.Connect(m_scenes);

                }
                Thread.Sleep(15000);
            }
        }

        public string FindClientRegion(string client_FirstName,string client_LastName)
        {
            string sourceRegion = null;
            foreach (Scene s in m_scenes)
            {
                s.ForEachScenePresence(delegate(ScenePresence presence)
                                       {
                                           if ((presence.IsChildAgent==false)
                                               &&(presence.Firstname==client_FirstName)
                                               &&(presence.Lastname==client_LastName))
                                           {
                                               sourceRegion = presence.Scene.RegionInfo.RegionName;
                                               //sourceRegion= s.RegionInfo.RegionName;
                                           }
                                       });
                if (sourceRegion != null) return sourceRegion; 
            }
            if (m_defaultzone == null) { m_defaultzone = "Sim"; }
            return m_defaultzone;
        }
    }

    internal class IRCChatModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string m_server = null;
        private uint m_port = 6668;
        private string m_user = "USER OpenSimBot 8 * :I'm a OpenSim to irc bot";
        private string m_nick = null;
        private string m_basenick = null;
        private string m_channel = null;
        private string m_privmsgformat = "PRIVMSG {0} :<{1} in {2}>: {3}";

        private NetworkStream m_stream;
        private TcpClient m_tcp;
        private StreamWriter m_writer;
        private StreamReader m_reader;

        private Thread pingSender;
        private Thread listener;
        internal object m_syncConnect = new object();

        private bool m_enabled = false;
        private bool m_connected = false;

        private List<Scene> m_scenes = null;
        private List<Scene> m_last_scenes = null;

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
                m_channel = config.Configs["IRC"].GetString("channel");
                m_port = (uint) config.Configs["IRC"].GetInt("port", (int) m_port);
                m_user = config.Configs["IRC"].GetString("username", m_user);
                m_privmsgformat = config.Configs["IRC"].GetString("msgformat", m_privmsgformat);
                if (m_server != null && m_nick != null && m_channel != null)
                {
                    m_nick = m_nick + Util.RandomClass.Next(1, 99);
                    m_enabled = true;
                }
            }
            catch (Exception)
            {
                m_log.Info("[CHAT]: No IRC config information, skipping IRC bridge configuration");
            }
        }

        public bool Connect(List<Scene> scenes)
        {
            lock (m_syncConnect)
            {
                try
                {
                    if (m_connected) return true;
                    m_scenes = scenes;
                    if (m_last_scenes == null) { m_last_scenes = scenes; }

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
                    OpenSim.Framework.ThreadTracker.Add(pingSender);

                    listener = new Thread(new ThreadStart(ListenerRun));
                    listener.Name = "IRCChatModuleListenerThread";
                    listener.IsBackground = true;
                    listener.Start();
                    OpenSim.Framework.ThreadTracker.Add(listener);

                    m_writer.WriteLine(m_user);
                    m_writer.Flush();
                    m_writer.WriteLine("NICK " + m_nick);
                    m_writer.Flush();
                    m_writer.WriteLine("JOIN " + m_channel);
                    m_writer.Flush();
                    m_log.Info("[IRC]: Connection fully established");
                    m_connected = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                return m_connected;
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

        public string  Nick
        {
            get { return m_nick; }
        }

        public void Reconnect()
        {
            m_connected = false;
            listener.Abort();
            pingSender.Abort();
            m_writer.Close();
            m_reader.Close();
            m_tcp.Close();
            if (m_enabled) { Connect(m_last_scenes); }
        }

        public void PrivMsg(string from, string region, string msg)
        {
            // One message to the IRC server

            try
            {
                if (m_privmsgformat == null)
                {
                    m_writer.WriteLine("PRIVMSG {0} :<{1} in {2}>: {3}", m_channel, from, region, msg);
                }
                else
                {
                    m_writer.WriteLine(m_privmsgformat, m_channel, from, region, msg);
                }
                m_writer.Flush();
                m_log.Info("[IRC]: PrivMsg " + from + " in " + region + " :" + msg);
            }
            catch (IOException)
            {
                m_log.Error("[IRC]: Disconnected from IRC server.(PrivMsg)");
                Reconnect();
            }
            catch (Exception ex)
            {
                m_log.Error("[IRC]: PrivMsg exception trap:" + ex.ToString());
            }
        }

        private Dictionary<string, string> ExtractMsg(string input)
        {
            //examines IRC commands and extracts any private messages
            // which will then be reboadcast in the Sim

            m_log.Info("[IRC]: ExtractMsg: " + input);
            Dictionary<string, string> result = null;
            //string regex = @":(?<nick>\w*)!~(?<user>\S*) PRIVMSG (?<channel>\S+) :(?<msg>.*)";
            string regex = @":(?<nick>\w*)!(?<user>\S*) PRIVMSG (?<channel>\S+) :(?<msg>.*)";
            Regex RE = new Regex(regex, RegexOptions.Multiline);
            MatchCollection matches = RE.Matches(input);
            // Get some direct matches $1 $4 is a 
            if ((matches.Count == 1) && (matches[0].Groups.Count == 5))
            {
                result = new Dictionary<string, string>();
                result.Add("nick", matches[0].Groups[1].Value);
                result.Add("user", matches[0].Groups[2].Value);
                result.Add("channel", matches[0].Groups[3].Value);
                result.Add("msg", matches[0].Groups[4].Value);
            }
            else
            {
                m_log.Info("[IRC]: Number of matches: " + matches.Count);
                if (matches.Count > 0)
                {
                    m_log.Info("[IRC]: Number of groups: " + matches[0].Groups.Count);
                }
            }
            return result;
        }

        public void PingRun()
        {
            // IRC keep alive thread
            // send PING ever 15 seconds
            while (true)
            {
                try
                {
                    if (m_connected == true)
                    {
                        m_writer.WriteLine("PING :" + m_server);
                        m_writer.Flush();
                        Thread.Sleep(15000);
                    }
                }
                catch (IOException)
                {
                    m_log.Error("[IRC]: Disconnected from IRC server.(PingRun)");
                    Reconnect();
                }
                catch (Exception ex)
                {
                    m_log.Error("[IRC]: PingRun exception trap:" + ex.ToString() + "\n" + ex.StackTrace);
                }
            }
        }

        public void ListenerRun()
        {
            string inputLine;
            LLVector3 pos = new LLVector3(128, 128, 20);
            while (true)
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
                                foreach (Scene m_scene in m_scenes)
                                {
                                    m_scene.ForEachScenePresence(delegate(ScenePresence avatar)
                                                                 {
                                                                     if (!avatar.IsChildAgent)
                                                                     {
                                                                         avatar.ControllingClient.SendChatMessage(
                                                                             Helpers.StringToField(data["msg"]), 255,
                                                                             pos, data["nick"],
                                                                             LLUUID.Zero);
                                                                     }
                                                                 });
                                }
                            }
                            else
                            {
                                // Was an command from the IRC server
                                ProcessIRCCommand(inputLine);
                            }
                        }
                        else
                        {
                            // Was an command from the IRC server
                            ProcessIRCCommand(inputLine);
                        }
                        Thread.Sleep(150);
                    }
                }
                catch (IOException)
                {
                    m_log.Error("[IRC]: ListenerRun IOException. Disconnected from IRC server ??? (ListenerRun)");
                    Reconnect();
                }
                catch (Exception ex)
                {
                    m_log.Error("[IRC]: ListenerRun exception trap:" + ex.ToString() + "\n" + ex.StackTrace);
                }
            }
        }

        public void BroadcastSim(string message,string sender)
        {
            LLVector3 pos = new LLVector3(128, 128, 20);
            try
            {
                foreach (Scene m_scene in m_scenes)
                {
                    m_scene.ForEachScenePresence(delegate(ScenePresence avatar)
                    {
                        if (!avatar.IsChildAgent)
                        {
                            avatar.ControllingClient.SendChatMessage(
                                Helpers.StringToField(message), 255,
                                pos, sender,
                                LLUUID.Zero);
                        }
                    });
                }
            }
            catch (Exception ex) // IRC gate should not crash Sim
            {
                m_log.Error("[IRC]: BroadcastSim Exception Trap:" + ex.ToString() + "\n" + ex.StackTrace);
            }
        }

        public enum ErrorReplies
        {
            NotRegistered = 451,	// ":You have not registered"
            NicknameInUse = 433		// "<nick> :Nickname is already in use"
        }

        public enum Replies
        {
            MotdStart = 375,		// ":- <server> Message of the day - "
            Motd = 372,				// ":- <text>"
            EndOfMotd = 376			// ":End of /MOTD command"
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
                m_log.Error("[IRC]: IRC SERVER ERROR:" + command); 
            }

            if (commArgs[0] == "PING")
            {
                string p_reply = "";

                for (int i = 1; i < commArgs.Length; i++)
                {
                    p_reply += commArgs[i] + " ";
                }

                m_writer.WriteLine("PONG " + p_reply);
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
                            m_log.Error("[IRC]: IRC SERVER reports NicknameInUse, trying " + m_nick);
                            // Retry
                            m_writer.WriteLine("NICK " + m_nick);
                            m_writer.Flush();
                            m_writer.WriteLine("JOIN " + m_channel);
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
                    case "JOIN": eventIrcJoin(commArgs); break;
                    case "PART": eventIrcPart(commArgs); break;
                    case "MODE": eventIrcMode(commArgs); break;
                    case "NICK": eventIrcNickChange(commArgs); break;
                    case "KICK": eventIrcKick(commArgs); break;
                    case "QUIT": eventIrcQuit(commArgs); break;
                    case "PONG":  break; // that's nice
                }
            }
        }

        public void eventIrcJoin(string[] commArgs)
        {
            string IrcChannel = commArgs[2];
            string IrcUser = commArgs[0].Split('!')[0];
            BroadcastSim(IrcUser + " is joining " + IrcChannel, m_nick);
        }

        public void eventIrcPart(string[] commArgs)
        {
            string IrcChannel = commArgs[2];
            string IrcUser = commArgs[0].Split('!')[0];
            BroadcastSim(IrcUser + " is parting " + IrcChannel, m_nick);
        }

        public void eventIrcMode(string[] commArgs)
        {
            string IrcChannel = commArgs[2];
            string IrcUser = commArgs[0].Split('!')[0];
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
            BroadcastSim(UserOldNick + " changed their nick to " + UserNewNick, m_nick);
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
            BroadcastSim(UserKicker + " kicked " + UserKicked +" on "+IrcChannel+" saying "+KickMessage, m_nick);
            if (UserKicked == m_nick)
            {
                BroadcastSim("Hey, that was me!!!", m_nick);
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
            BroadcastSim(IrcUser + " quits saying " + QuitMessage, m_nick);
        }

        public void Close()
        {
            m_connected = false;
            m_writer.WriteLine("QUIT :" + m_nick + " to " + m_channel + " wormhole with " + m_server + " closing");
            m_writer.Flush();
            listener.Abort();
            pingSender.Abort();
            m_writer.Close();
            m_reader.Close();
            m_tcp.Close();
        }
    }
}
