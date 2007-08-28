using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.Environment.Modules
{
    public class ChatModule :IRegionModule
    {
        private Scene m_scene;

        private string m_server = "irc2.choopa.net";
       
        private int m_port = 6668;
        private  string m_user = "USER OpenSimBot 8 * :I'm a OpenSim to irc bot";
        private string m_nick = "OpenSimBot";
        private string m_channel = "#opensim";

        private NetworkStream m_stream;
        private TcpClient m_irc;
        private StreamWriter m_ircWriter;
        private StreamReader m_ircReader;

        private Thread pingSender;

        private bool connected = false;

        public ChatModule()
        {

        }

        public void Initialise(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;

            //should register a optional API Method, so other modules can send chat messages using this module
        }

        public void PostInitialise()
        {
            try
            {
                m_irc = new TcpClient(m_server, m_port);
                m_stream = m_irc.GetStream();
                m_ircReader = new StreamReader(m_stream);
                m_ircWriter = new StreamWriter(m_stream);

                pingSender = new Thread(new ThreadStart(this.PingRun));
                pingSender.Start();

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

        public void CloseDown()
        {
            m_ircWriter.Close();
            m_ircReader.Close();
            m_irc.Close();
        }

        public string GetName()
        {
            return "ChatModule";
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

        public void SimChat(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            ScenePresence avatar = null;
            avatar = m_scene.RequestAvatar(fromAgentID);
            if (avatar != null)
            {
                fromPos = avatar.AbsolutePosition;
                fromName = avatar.Firstname + " " + avatar.Lastname;
                avatar = null;
            }

            if (connected)
            {
                m_ircWriter.WriteLine("MSG " + m_channel +" :" + fromName + ",  " + Util.FieldToString(message));
                m_ircWriter.Flush();
            }

            m_scene.ForEachScenePresence(delegate(ScenePresence presence)
                                              {
                                                  int dis = -1000;

                                                  //err ??? the following code seems to be request a scenePresence when it already has a ref to it
                                                  avatar = m_scene.RequestAvatar(presence.ControllingClient.AgentId);
                                                  if (avatar != null)
                                                  {
                                                      dis = (int)avatar.AbsolutePosition.GetDistanceTo(fromPos);
                                                  }

                                                  switch (type)
                                                  {
                                                      case 0: // Whisper
                                                          if ((dis < 10) && (dis > -10))
                                                          {
                                                              //should change so the message is sent through the avatar rather than direct to the ClientView
                                                              presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                     fromAgentID);
                                                          }
                                                          break;
                                                      case 1: // Say
                                                          if ((dis < 30) && (dis > -30))
                                                          {
                                                              //Console.WriteLine("sending chat");
                                                              presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                     fromAgentID);
                                                          }
                                                          break;
                                                      case 2: // Shout
                                                          if ((dis < 100) && (dis > -100))
                                                          {
                                                              presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                     fromAgentID);
                                                          }
                                                          break;

                                                      case 0xff: // Broadcast
                                                          presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                 fromAgentID);
                                                          break;
                                                  }
                                              });
        }

    }
}
