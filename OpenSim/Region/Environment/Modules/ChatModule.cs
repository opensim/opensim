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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class ChatModule : IRegionModule, ISimChat
    {
        private List<Scene> m_scenes = new List<Scene>();
        private LogBase m_log;

        private string m_server = null;
        private int m_port = 6668;
        private string m_user = "USER OpenSimBot 8 * :I'm a OpenSim to irc bot";
        private string m_nick = null;
        private string m_channel = null;

        private int m_whisperdistance = 10;
        private int m_saydistance = 30;
        private int m_shoutdistance = 100;

        private NetworkStream m_stream;
        private TcpClient m_irc;
        private StreamWriter m_ircWriter;
        private StreamReader m_ircReader;
        
        private Thread pingSender;
        private Thread listener;

        private bool m_enable_irc = false;
        private bool connected = false;

        public ChatModule()
        {
            m_nick = "OSimBot" + Util.RandomClass.Next(1, 99);
            m_irc = null;
            m_ircWriter = null;
            m_ircReader = null;

            m_log = OpenSim.Framework.Console.MainLog.Instance;
        }

        public void Initialise(Scene scene, Nini.Config.IConfigSource config)
        {
            try {
                m_server = config.Configs["IRC"].GetString("server");
                m_nick = config.Configs["IRC"].GetString("nick");
                m_channel = config.Configs["IRC"].GetString("channel");
                m_port = config.Configs["IRC"].GetInt("port", m_port);
                m_user = config.Configs["IRC"].GetString("username", m_user);
                if (m_server != null && m_nick != null && m_channel != null) {
                    m_enable_irc = true;
                }
            } catch (Exception e) {
                Console.WriteLine("No IRC config information, skipping IRC bridge configuration");
            }

            m_whisperdistance = config.Configs["Chat"].GetInt("whisper_distance");
            m_saydistance = config.Configs["Chat"].GetInt("say_distance");
            m_shoutdistance = config.Configs["Chat"].GetInt("shout_distance");

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                scene.EventManager.OnNewClient += NewClient;
                scene.RegisterModuleInterface<ISimChat>(this);
            }
        }

        public void PostInitialise()
        {
            if( m_enable_irc ) {
                try
                {
                    m_irc = new TcpClient(m_server, m_port);
                    m_stream = m_irc.GetStream();
                    m_ircReader = new StreamReader(m_stream);
                    m_ircWriter = new StreamWriter(m_stream);
                    
                    pingSender = new Thread(new ThreadStart(this.PingRun));
                    pingSender.Start();
                    
                    listener = new Thread(new ThreadStart(this.ListenerRun));
                    listener.Start();
                    
                    m_ircWriter.WriteLine(m_user);
                    m_ircWriter.Flush();
                    m_ircWriter.WriteLine("NICK " + m_nick);
                    m_ircWriter.Flush();
                    m_ircWriter.WriteLine("JOIN " + m_channel);
                    m_ircWriter.Flush();
                    connected = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public void Close()
        {
            m_ircWriter.Close();
            m_ircReader.Close();
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
            client.OnChatFromViewer += SimChat;
        }

        public void PingRun()
        {
            while (true)
            {
                m_ircWriter.WriteLine("PING :" + m_server);
                m_ircWriter.Flush();
                Thread.Sleep(15000);
            }
        }

        public void ListenerRun()
        {
            string inputLine;
            LLVector3 pos = new LLVector3(128, 128, 20);
            while (true)
            {
                while ((inputLine = m_ircReader.ReadLine()) != null)
                {
                    Console.WriteLine(inputLine);
                    if (inputLine.Contains(m_channel))
                    {
                        string mess = inputLine.Substring(inputLine.IndexOf(m_channel));
                        foreach (Scene m_scene in m_scenes)
                        {
                            m_scene.Broadcast(delegate(IClientAPI client)
                                                             {
                                                                 client.SendChatMessage(
                                                                     Helpers.StringToField(mess), 255, pos, "IRC:",
                                                                     LLUUID.Zero);
                                                             });
                        }
                    }
                }
                Thread.Sleep(50);
            }
        }

        public void SimChat(Object sender, ChatFromViewerArgs e)
        {
            ScenePresence avatar = null;

            //TODO: Move ForEachScenePresence and others into IScene.
            Scene scene = (Scene)e.Scene;

            //TODO: Remove the need for this check
            if (scene == null)
                scene = m_scenes[0];

            // Filled in since it's easier than rewriting right now.
            LLVector3 fromPos = e.Position;
            LLVector3 fromRegionPos = e.Position + new LLVector3(e.Scene.RegionInfo.RegionLocX * 256, e.Scene.RegionInfo.RegionLocY * 256, 0);
            string fromName = e.From;
            string message = e.Message;
            byte type = (byte)e.Type;
            LLUUID fromAgentID = LLUUID.Zero;

            if (e.Sender != null)
                avatar = scene.GetScenePresence(e.Sender.AgentId);

            if (avatar != null)
            {
                fromPos = avatar.AbsolutePosition;
                fromRegionPos = fromPos + new LLVector3(e.Scene.RegionInfo.RegionLocX * 256, e.Scene.RegionInfo.RegionLocY * 256, 0);
                fromName = avatar.Firstname + " " + avatar.Lastname;
                fromAgentID = e.Sender.AgentId;
                avatar = null;
            }

            string typeName;
            switch (e.Type)
            {
                case ChatTypeEnum.Broadcast:
                    typeName = "broadcasts";
                    break;
                case ChatTypeEnum.Say:
                    typeName = "says";
                    break;
                case ChatTypeEnum.Shout:
                    typeName = "shouts";
                    break;
                case ChatTypeEnum.Whisper:
                    typeName = "whispers";
                    break;
                default:
                    typeName = "unknown";
                    break;
            }

            m_log.Verbose("CHAT", fromName + " (" + e.Channel + " @ " + scene.RegionInfo.RegionName + ") " + typeName + ": " + e.Message);

            if (connected)
            {
                try
                {
                    m_ircWriter.WriteLine("PRIVMSG " + m_channel + " :" + "<" + fromName + " in " + scene.RegionInfo.RegionName + ">:  " +
                                          e.Message);
                    m_ircWriter.Flush();
                }
                catch (IOException)
                {
                    m_log.Error("IRC","Disconnected from IRC server.");
                    listener.Abort();
                    pingSender.Abort();
                    connected = false;
                }
            }

            if (e.Channel == 0)
            {
                foreach (Scene m_scene in m_scenes)
                {
                    m_scene.ForEachScenePresence(delegate(ScenePresence presence)
                                                     {
                                                         int dis = -100000;

                                                         LLVector3 avatarRegionPos = presence.AbsolutePosition + new LLVector3(scene.RegionInfo.RegionLocX * 256, scene.RegionInfo.RegionLocY * 256, 0);
                                                         dis = Math.Abs((int)avatarRegionPos.GetDistanceTo(fromRegionPos));

                                                         switch (e.Type)
                                                         {
                                                             case ChatTypeEnum.Whisper:
                                                                 if (dis < m_whisperdistance)
                                                                 {
                                                                     //should change so the message is sent through the avatar rather than direct to the ClientView
                                                                     presence.ControllingClient.SendChatMessage(message,
                                                                                                                type,
                                                                                                                fromPos,
                                                                                                                fromName,
                                                                                                                fromAgentID);
                                                                 }
                                                                 break;
                                                             default:
                                                             case ChatTypeEnum.Say:
                                                                 if (dis < m_saydistance)
                                                                 {
                                                                     //Console.WriteLine("sending chat");
                                                                     presence.ControllingClient.SendChatMessage(message,
                                                                                                                type,
                                                                                                                fromPos,
                                                                                                                fromName,
                                                                                                                fromAgentID);
                                                                 }
                                                                 break;
                                                             case ChatTypeEnum.Shout:
                                                                 if (dis < m_shoutdistance)
                                                                 {
                                                                     presence.ControllingClient.SendChatMessage(message,
                                                                                                                type,
                                                                                                                fromPos,
                                                                                                                fromName,
                                                                                                                fromAgentID);
                                                                 }
                                                                 break;

                                                             case ChatTypeEnum.Broadcast:
                                                                 presence.ControllingClient.SendChatMessage(message, type,
                                                                                                            fromPos,
                                                                                                            fromName,
                                                                                                            fromAgentID);
                                                                 break;
                                                         }
                                                     });
                }
            }
        }
    }
}
