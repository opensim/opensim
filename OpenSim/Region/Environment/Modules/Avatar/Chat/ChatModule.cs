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
using libsecondlife;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Chat
{
    public class ChatModule : IRegionModule, ISimChat
    {
        private static readonly ILog m_log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;

        private int m_saydistance = 30;
        private int m_shoutdistance = 100;
        private int m_whisperdistance = 10;
        private List<Scene> m_scenes = new List<Scene>();

        internal object m_syncInit = new object();

        #region IRegionModule Members
        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_syncInit)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += NewClient;
                    scene.EventManager.OnChatFromWorld += SimChat;
                    // scene.RegisterModuleInterface<ISimChat>(this);
                }

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
                m_log.InfoFormat("[CHAT] initialized for {0} w:{1} s:{2} S:{3}", scene.RegionInfo.RegionName,
                                 m_whisperdistance, m_saydistance, m_shoutdistance);
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ChatModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region ISimChat Members
        public void SimChat(Object sender, ChatFromViewerArgs e)
        {
            ScenePresence avatar = null;

            //TODO: Move ForEachScenePresence and others into IScene.
            Scene scene = (Scene) e.Scene;

            //TODO: Remove the need for this check
            if (scene == null)
                scene = m_scenes[0];

            // Filled in since it's easier than rewriting right now.
            LLVector3 fromPos = e.Position;
            LLVector3 regionPos = new LLVector3(scene.RegionInfo.RegionLocX * Constants.RegionSize, 
                                                scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);

            string fromName = e.From;
            string message = e.Message;
            LLUUID fromAgentID = e.SenderUUID;

            if (e.Sender != null)
            {
                avatar = scene.GetScenePresence(e.Sender.AgentId);
            }

            if (avatar != null)
            {
                fromPos = avatar.AbsolutePosition;
                regionPos = new LLVector3(scene.RegionInfo.RegionLocX * Constants.RegionSize, 
                                          scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);
                fromName = avatar.Firstname + " " + avatar.Lastname;
                fromAgentID = e.Sender.AgentId;
            }

            // We only want to relay stuff on channel 0
            if (e.Channel == 0 || e.Channel == DEBUG_CHANNEL)
            {
                if (e.Channel == DEBUG_CHANNEL)
                    e.Type = ChatTypeEnum.DebugChannel;

                // chat works by redistributing every incoming chat
                // message to each avatar in the scene
                foreach (Scene s in m_scenes)
                {
                    s.ForEachScenePresence(delegate(ScenePresence presence)
                                           {
                                               if (e.Channel == DEBUG_CHANNEL)
                                               {
                                                   TrySendChatMessage(presence, fromPos, regionPos,
                                                                      fromAgentID, fromName, e.Type, 
                                                                      message, ChatSourceType.Object);
                                               }
                                               else
                                               {
                                                   TrySendChatMessage(presence, fromPos, regionPos,
                                                                      fromAgentID, fromName, e.Type, 
                                                                      message, ChatSourceType.Agent);
                                               }
                                           });
                }
            }
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            try
            {
                client.OnChatFromViewer += SimChat;
            }
            catch (Exception ex)
            {
                m_log.Error("[CHAT]: NewClient exception trap:" + ex.ToString());
            }
        }

        private void TrySendChatMessage(ScenePresence presence, LLVector3 fromPos, LLVector3 regionPos,
                                        LLUUID fromAgentID, string fromName, ChatTypeEnum type, 
                                        string message, ChatSourceType src)
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
                presence.ControllingClient.SendChatMessage(message, (byte) type, fromPos, fromName, 
                                                           fromAgentID,(byte)src,(byte)ChatAudibleLevel.Fully);
            }
        }
    }
}