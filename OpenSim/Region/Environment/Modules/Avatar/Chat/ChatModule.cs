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
    public class ChatModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;

        private bool m_enabled = true;
        private int m_saydistance = 30;
        private int m_shoutdistance = 100;
        private int m_whisperdistance = 10;
        private List<Scene> m_scenes = new List<Scene>();

        internal object m_syncInit = new object();

        #region IRegionModule Members
        public virtual void Initialise(Scene scene, IConfigSource config)
        {
            // wrap this in a try block so that defaults will work if
            // the config file doesn't specify otherwise.
            try
            {
                m_enabled = config.Configs["Chat"].GetBoolean("enabled", m_enabled);
                if (!m_enabled) return;

                m_whisperdistance = config.Configs["Chat"].GetInt("whisper_distance", m_whisperdistance);
                m_saydistance = config.Configs["Chat"].GetInt("say_distance", m_saydistance);
                m_shoutdistance = config.Configs["Chat"].GetInt("shout_distance", m_shoutdistance);
            }
            catch (Exception)
            {
            }

            lock (m_syncInit)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += OnNewClient;
                    scene.EventManager.OnChatFromWorld += OnChatFromWorld;
                    scene.EventManager.OnChatBroadcast += OnChatBroadcast;
                }
            }

            m_log.InfoFormat("[CHAT] initialized for {0} w:{1} s:{2} S:{3}", scene.RegionInfo.RegionName,
                             m_whisperdistance, m_saydistance, m_shoutdistance);
        }
        public virtual void PostInitialise()
        {
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "ChatModule"; }
        }

        public virtual bool IsSharedModule
        {
            get { return true; }
        }

        #endregion


        public virtual void OnNewClient(IClientAPI client)
        {
            client.OnChatFromClient += OnChatFromClient;
        }

        public virtual void OnChatFromClient(Object sender, OSChatMessage e)
        {
            // redistribute to interested subscribers
            Scene scene = (Scene)e.Scene;
            scene.EventManager.TriggerOnChatFromClient(sender, e);

            // early return if not on public or debug channel
            if (e.Channel != 0 && e.Channel != DEBUG_CHANNEL) return;

            // sanity check:
            if (e.Sender == null)
            {
                m_log.ErrorFormat("[CHAT] OnChatFromClient from {0} has empty Sender field!", sender);
                return;
            }

            // string message = e.Message;
            // if (e.Channel == DEBUG_CHANNEL) e.Type = ChatTypeEnum.DebugChannel;

            // ScenePresence avatar = scene.GetScenePresence(e.Sender.AgentId);
            // Vector3 fromPos = avatar.AbsolutePosition;
            // Vector3 regionPos = new Vector3(scene.RegionInfo.RegionLocX * Constants.RegionSize,
            //                                 scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);
            // string fromName = avatar.Firstname + " " + avatar.Lastname;
            // UUID fromID = e.Sender.AgentId;

            // DeliverChatToAvatars(fromPos, regionPos, fromID, fromName, e.Type, ChatSourceType.Agent, message);
            DeliverChatToAvatars(ChatSourceType.Agent, e);
        }

        public virtual void OnChatFromWorld(Object sender, OSChatMessage e)
        {
            // early return if not on public or debug channel
            if (e.Channel != 0 && e.Channel != DEBUG_CHANNEL) return;

            // // Filled in since it's easier than rewriting right now.
            // Vector3 fromPos = e.Position;
            // Vector3 regionPos = new Vector3(scene.RegionInfo.RegionLocX * Constants.RegionSize,
            //                                 scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);

            // string fromName = e.From;
            // string message = e.Message;
            // UUID fromID = e.SenderUUID;

            // if (e.Channel == DEBUG_CHANNEL)
            //     e.Type = ChatTypeEnum.DebugChannel;

            // DeliverChatToAvatars(fromPos, regionPos, fromID, fromName, e.Type, ChatSourceType.Object, message);
            DeliverChatToAvatars(ChatSourceType.Object, e);
        }

        protected virtual void DeliverChatToAvatars(ChatSourceType sourceType, OSChatMessage c)
        {
            string fromName = c.From;
            UUID fromID = UUID.Zero;
            string message = c.Message;
            IScene scene = c.Scene;
            Vector3 fromPos = c.Position;
            Vector3 regionPos = new Vector3(scene.RegionInfo.RegionLocX * Constants.RegionSize,
                                            scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);

            if (c.Channel == DEBUG_CHANNEL) c.Type = ChatTypeEnum.DebugChannel;

            switch (sourceType) 
            {
            case ChatSourceType.Agent:
                if (!(scene is Scene))
                {
                    m_log.WarnFormat("[CHAT] scene {0} is not a Scene object, cannot obtain scene presence for {1}",
                                     scene.RegionInfo.RegionName, c.Sender.AgentId);
                    return;
                }
                ScenePresence avatar = (scene as Scene).GetScenePresence(c.Sender.AgentId);
                fromPos = avatar.AbsolutePosition;
                fromName = avatar.Firstname + " " + avatar.Lastname;
                fromID = c.Sender.AgentId;

                break;

            case ChatSourceType.Object:
                fromID = c.SenderUUID;

                break;
            }

            // TODO: iterate over message
            if (message.Length >= 1000) // libomv limit
                message = message.Substring(0, 1000);

            foreach (Scene s in m_scenes)
            {
                s.ForEachScenePresence(delegate(ScenePresence presence) 
                                       {
                                           TrySendChatMessage(presence, fromPos, regionPos, fromID, fromName, 
                                                              c.Type, message, sourceType);
                                       });
            }
        }

        // protected virtual void DeliverChatToAvatars(Vector3 pos, Vector3 regionPos, UUID uuid, string name,
        //                                             ChatTypeEnum chatType, ChatSourceType sourceType, string message)
        // {
        //     // iterate over message
        //     if (message.Length >= 1000) // libomv limit
        //         message = message.Substring(0, 1000);

        //     foreach (Scene s in m_scenes)
        //     {
        //         s.ForEachScenePresence(delegate(ScenePresence presence) 
        //                                {
        //                                    TrySendChatMessage(presence, pos, regionPos, uuid, name, 
        //                                                       chatType, message, sourceType);
        //                                });
        //     }
        // }


        public virtual void OnChatBroadcast(Object sender, OSChatMessage c)
        {
            // unless the chat to be broadcast is of type Region, we
            // drop it if its channel is neither 0 nor DEBUG_CHANNEL
            if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL && c.Type != ChatTypeEnum.Region) return;

            ChatTypeEnum cType = c.Type;
            if (c.Channel == DEBUG_CHANNEL)
                cType = ChatTypeEnum.DebugChannel;

            if (cType == ChatTypeEnum.Region)
                cType = ChatTypeEnum.Say;

            if (c.Message.Length > 1100)
                c.Message = c.Message.Substring(0, 1000);

            // broadcast chat works by redistributing every incoming chat
            // message to each avatar in the scene.
            Vector3 pos = new Vector3(128, 128, 30);
            
            UUID fromID = UUID.Zero;
            ChatSourceType sourceType = ChatSourceType.Object;
            if (null != c.Sender)
            {
                fromID = c.Sender.AgentId;
                sourceType = ChatSourceType.Agent;
            }
            
            ((Scene)c.Scene).ForEachScenePresence(
                delegate(ScenePresence presence)
                {
                    // ignore chat from child agents
                    if (presence.IsChildAgent) return;
                    
                    IClientAPI client = presence.ControllingClient;
                    
                    // don't forward SayOwner chat from objects to
                    // non-owner agents
                    if ((c.Type == ChatTypeEnum.Owner) &&
                        (null != c.SenderObject) &&
                        (((SceneObjectPart)c.SenderObject).OwnerID != client.AgentId))
                        return;
                    
                    client.SendChatMessage(c.Message, (byte)cType, pos, c.From, fromID, 
                                           (byte)sourceType, (byte)ChatAudibleLevel.Fully);
                });
        }


        protected virtual void TrySendChatMessage(ScenePresence presence, Vector3 fromPos, Vector3 regionPos,
                                                  UUID fromAgentID, string fromName, ChatTypeEnum type,
                                                  string message, ChatSourceType src)
        {
            // don't send stuff to child agents
            if (presence.IsChildAgent) return;

            Vector3 fromRegionPos = fromPos + regionPos;
            Vector3 toRegionPos = presence.AbsolutePosition +
                new Vector3(presence.Scene.RegionInfo.RegionLocX * Constants.RegionSize,
                            presence.Scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);

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
