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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.Chat
{
    public class ChatModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 2147483647;

        private bool m_enabled = true;
        private int m_saydistance = 20;
        private int m_shoutdistance = 100;
        private int m_whisperdistance = 10;
        private List<Scene> m_scenes = new List<Scene>();

        internal object m_syncy = new object();

        internal IConfig m_config;

        #region ISharedRegionModule Members
        public virtual void Initialise(IConfigSource config)
        {
            m_config = config.Configs["Chat"];

            if (null == m_config)
            {
                m_log.Info("[CHAT]: no config found, plugin disabled");
                m_enabled = false;
                return;
            }

            if (!m_config.GetBoolean("enabled", true))
            {
                m_log.Info("[CHAT]: plugin disabled by configuration");
                m_enabled = false;
                return;
            }

            m_whisperdistance = config.Configs["Chat"].GetInt("whisper_distance", m_whisperdistance);
            m_saydistance = config.Configs["Chat"].GetInt("say_distance", m_saydistance);
            m_shoutdistance = config.Configs["Chat"].GetInt("shout_distance", m_shoutdistance);
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_enabled) return;

            lock (m_syncy)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnNewClient += OnNewClient;
                    scene.EventManager.OnChatFromWorld += OnChatFromWorld;
                    scene.EventManager.OnChatBroadcast += OnChatBroadcast;
                }
            }

            m_log.InfoFormat("[CHAT]: Initialized for {0} w:{1} s:{2} S:{3}", scene.RegionInfo.RegionName,
                             m_whisperdistance, m_saydistance, m_shoutdistance);
        }

        public virtual void RegionLoaded(Scene scene)
        {
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_enabled) return;

            lock (m_syncy)
            {
                if (m_scenes.Contains(scene))
                {
                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnChatFromWorld -= OnChatFromWorld;
                    scene.EventManager.OnChatBroadcast -= OnChatBroadcast;
                    m_scenes.Remove(scene);
                }
            }
        }
        
        public virtual void Close()
        {
        }

        public virtual void PostInitialise()
        {
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "ChatModule"; }
        }

        #endregion


        public virtual void OnNewClient(IClientAPI client)
        {
            client.OnChatFromClient += OnChatFromClient;
        }

        protected OSChatMessage FixPositionOfChatMessage(OSChatMessage c)
        {
            ScenePresence avatar;
            Scene scene = (Scene)c.Scene;
            if ((avatar = scene.GetScenePresence(c.Sender.AgentId)) != null)
                c.Position = avatar.AbsolutePosition;

            return c;
        }

        public virtual void OnChatFromClient(Object sender, OSChatMessage c)
        {
            c = FixPositionOfChatMessage(c);

            // redistribute to interested subscribers
            Scene scene = (Scene)c.Scene;
            scene.EventManager.TriggerOnChatFromClient(sender, c);

            // early return if not on public or debug channel
            if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL) return;

            // sanity check:
            if (c.Sender == null)
            {
                m_log.ErrorFormat("[CHAT]: OnChatFromClient from {0} has empty Sender field!", sender);
                return;
            }

            DeliverChatToAvatars(ChatSourceType.Agent, c);
        }

        public virtual void OnChatFromWorld(Object sender, OSChatMessage c)
        {
            // early return if not on public or debug channel
            if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL) return;

            DeliverChatToAvatars(ChatSourceType.Object, c);
        }

        protected virtual void DeliverChatToAvatars(ChatSourceType sourceType, OSChatMessage c)
        {
            string fromName = c.From;
            UUID fromID = UUID.Zero;
            UUID ownerID = UUID.Zero;
            UUID targetID = c.TargetUUID;
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
                    m_log.WarnFormat("[CHAT]: scene {0} is not a Scene object, cannot obtain scene presence for {1}",
                                     scene.RegionInfo.RegionName, c.Sender.AgentId);
                    return;
                }
                ScenePresence avatar = (scene as Scene).GetScenePresence(c.Sender.AgentId);
                fromPos = avatar.AbsolutePosition;
                fromName = avatar.Name;
                fromID = c.Sender.AgentId;
                ownerID = c.Sender.AgentId;

                break;

            case ChatSourceType.Object:
                fromID = c.SenderUUID;

                if (c.SenderObject != null && c.SenderObject is SceneObjectPart)
                    ownerID = ((SceneObjectPart)c.SenderObject).OwnerID;

                break;
            }

            // TODO: iterate over message
            if (message.Length >= 1000) // libomv limit
                message = message.Substring(0, 1000);

//            m_log.DebugFormat(
//                "[CHAT]: DCTA: fromID {0} fromName {1}, region{2}, cType {3}, sType {4}, targetID {5}",
//                fromID, fromName, scene.RegionInfo.RegionName, c.Type, sourceType, targetID);

            HashSet<UUID> receiverIDs = new HashSet<UUID>();

            foreach (Scene s in m_scenes)
            {
                if (targetID == UUID.Zero)
                {
                    // This should use ForEachClient, but clients don't have a position.
                    // If camera is moved into client, then camera position can be used
                    s.ForEachRootScenePresence(
                        delegate(ScenePresence presence)
                        {
                            if (TrySendChatMessage(
                                presence, fromPos, regionPos, fromID, ownerID, fromName, c.Type, message, sourceType, false))
                                receiverIDs.Add(presence.UUID);
                        }
                    );
                }
                else
                {
                    // This is a send to a specific client eg from llRegionSayTo
                    // no need to check distance etc, jand send is as say
                    ScenePresence presence = s.GetScenePresence(targetID);
                    if (presence != null && !presence.IsChildAgent)
                    {
                        if (TrySendChatMessage(
                            presence, fromPos, regionPos, fromID, ownerID, fromName, ChatTypeEnum.Say, message, sourceType, true))
                            receiverIDs.Add(presence.UUID);
                    }
                }
            }

            (scene as Scene).EventManager.TriggerOnChatToClients(
                fromID, receiverIDs, message, c.Type, fromPos, fromName, sourceType, ChatAudibleLevel.Fully);
        }

        static private Vector3 CenterOfRegion = new Vector3(128, 128, 30);
        
        public virtual void OnChatBroadcast(Object sender, OSChatMessage c)
        {
            if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL) return;

            ChatTypeEnum cType = c.Type;
            if (c.Channel == DEBUG_CHANNEL)
                cType = ChatTypeEnum.DebugChannel;

            if (cType == ChatTypeEnum.Region)
                cType = ChatTypeEnum.Say;

            if (c.Message.Length > 1100)
                c.Message = c.Message.Substring(0, 1000);

            // broadcast chat works by redistributing every incoming chat
            // message to each avatar in the scene.
            string fromName = c.From;
            
            UUID fromID = UUID.Zero;
            ChatSourceType sourceType = ChatSourceType.Object;
            if (null != c.Sender)
            {
                ScenePresence avatar = (c.Scene as Scene).GetScenePresence(c.Sender.AgentId);
                fromID = c.Sender.AgentId;
                fromName = avatar.Name;
                sourceType = ChatSourceType.Agent;
            }
            else if (c.SenderUUID != UUID.Zero) 
            {
                fromID = c.SenderUUID; 
            }
            
            // m_log.DebugFormat("[CHAT] Broadcast: fromID {0} fromName {1}, cType {2}, sType {3}", fromID, fromName, cType, sourceType);

            HashSet<UUID> receiverIDs = new HashSet<UUID>();
            
            ((Scene)c.Scene).ForEachRootClient(
                delegate(IClientAPI client)
                {   
                    // don't forward SayOwner chat from objects to
                    // non-owner agents
                    if ((c.Type == ChatTypeEnum.Owner) &&
                        (null != c.SenderObject) &&
                        (((SceneObjectPart)c.SenderObject).OwnerID != client.AgentId))
                        return;

                    client.SendChatMessage(
                        c.Message, (byte)cType, CenterOfRegion, fromName, fromID, fromID,
                        (byte)sourceType, (byte)ChatAudibleLevel.Fully);

                    receiverIDs.Add(client.AgentId);
                });
            
            (c.Scene as Scene).EventManager.TriggerOnChatToClients(
                fromID, receiverIDs, c.Message, cType, CenterOfRegion, fromName, sourceType, ChatAudibleLevel.Fully);
        }

        /// <summary>
        /// Try to send a message to the given presence
        /// </summary>
        /// <param name="presence">The receiver</param>
        /// <param name="fromPos"></param>
        /// <param name="regionPos">/param>
        /// <param name="fromAgentID"></param>
        /// <param name='ownerID'>
        /// Owner of the message.  For at least some messages from objects, this has to be correctly filled with the owner's UUID.
        /// This is the case for script error messages in viewer 3 since LLViewer change EXT-7762
        /// </param>
        /// <param name="fromName"></param>
        /// <param name="type"></param>
        /// <param name="message"></param>
        /// <param name="src"></param>
        /// <returns>true if the message was sent to the receiver, false if it was not sent due to failing a 
        /// precondition</returns>
        protected virtual bool TrySendChatMessage(
            ScenePresence presence, Vector3 fromPos, Vector3 regionPos,
            UUID fromAgentID, UUID ownerID, string fromName, ChatTypeEnum type,
            string message, ChatSourceType src, bool ignoreDistance)
        {
            // don't send stuff to child agents
            if (presence.IsChildAgent) return false;

            Vector3 fromRegionPos = fromPos + regionPos;
            Vector3 toRegionPos = presence.AbsolutePosition +
                new Vector3(presence.Scene.RegionInfo.RegionLocX * Constants.RegionSize,
                            presence.Scene.RegionInfo.RegionLocY * Constants.RegionSize, 0);

            int dis = (int)Util.GetDistanceTo(toRegionPos, fromRegionPos);

            if (!ignoreDistance)
            {
                if (type == ChatTypeEnum.Whisper && dis > m_whisperdistance ||
                    type == ChatTypeEnum.Say && dis > m_saydistance ||
                    type == ChatTypeEnum.Shout && dis > m_shoutdistance)
                {
                    return false;
                }
            }

            // TODO: should change so the message is sent through the avatar rather than direct to the ClientView
            presence.ControllingClient.SendChatMessage(
                message, (byte) type, fromPos, fromName,
                fromAgentID, ownerID, (byte)src, (byte)ChatAudibleLevel.Fully);
            
            return true;
        }
    }
}