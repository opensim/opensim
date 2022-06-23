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
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.Chat
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ChatModule")]
    public class ChatModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected const int DEBUG_CHANNEL = 2147483647;

        protected bool m_enabled = true;
        protected int m_saydistance = 20;
        protected int m_shoutdistance = 100;
        protected int m_whisperdistance = 10;

        protected float m_saydistanceSQ;
        protected float m_shoutdistanceSQ;
        protected float m_whisperdistanceSQ;

        protected List<Scene> m_scenes = new List<Scene>();
        protected List<string> FreezeCache = new List<string>();
        protected string m_adminPrefix = "";
        protected object m_syncy = new object();
        protected IConfig m_config;
        #region ISharedRegionModule Members
        public virtual void Initialise(IConfigSource config)
        {
            m_config = config.Configs["Chat"];

            if (m_config != null)
            {
                if (!m_config.GetBoolean("enabled", true))
                {
                    m_log.Info("[CHAT]: plugin disabled by configuration");
                    m_enabled = false;
                    return;
                }

                m_whisperdistance = m_config.GetInt("whisper_distance", m_whisperdistance);
                m_saydistance = m_config.GetInt("say_distance", m_saydistance);
                m_shoutdistance = m_config.GetInt("shout_distance", m_shoutdistance);
                m_adminPrefix = m_config.GetString("admin_prefix", "");

            }
            m_saydistanceSQ = m_saydistance * m_saydistance;
            m_shoutdistanceSQ = m_shoutdistance * m_shoutdistance;
            m_whisperdistanceSQ = m_whisperdistance *m_whisperdistance;

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
            if (!m_enabled)
                return;

            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if (featuresModule != null)
            {
                featuresModule.AddOpenSimExtraFeature("say-range", new OSDInteger(m_saydistance));
                featuresModule.AddOpenSimExtraFeature("whisper-range", new OSDInteger(m_whisperdistance));
                featuresModule.AddOpenSimExtraFeature("shout-range", new OSDInteger(m_shoutdistance));
            }
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

        public virtual Type ReplaceableInterface
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

        public virtual void OnChatFromClient(Object sender, OSChatMessage c)
        {
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

            if (FreezeCache.Contains(c.Sender.AgentId.ToString()))
            {
                if (c.Type != ChatTypeEnum.StartTyping || c.Type != ChatTypeEnum.StopTyping)
                    c.Sender.SendAgentAlertMessage("You may not talk as you are frozen.", false);
            }
            else
            {
                DeliverChatToAvatars(ChatSourceType.Agent, c);
            }
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
            string fromNamePrefix = "";
            UUID fromID = UUID.Zero;
            UUID ownerID = UUID.Zero;
            string message = c.Message;
            Scene scene = c.Scene as Scene;
            UUID destination = c.Destination;
            Vector3 fromPos = c.Position;

            bool checkParcelHide = false;
            UUID sourceParcelID = UUID.Zero;
            Vector3 hidePos = fromPos;

            if (c.Channel == DEBUG_CHANNEL) c.Type = ChatTypeEnum.DebugChannel;

            if(!m_scenes.Contains(scene))
            {
                m_log.WarnFormat("[CHAT]: message from unkown scene {0} ignored",
                                     scene.RegionInfo.RegionName);
                return;
            }

            switch (sourceType)
            {
                case ChatSourceType.Agent:
                    ScenePresence avatar = scene.GetScenePresence(c.Sender.AgentId);
                    if(avatar == null)
                        return;
                    fromPos = avatar.AbsolutePosition;
                    fromName = avatar.Name;
                    fromID = c.Sender.AgentId;
                    if (avatar.IsViewerUIGod)
                    { // let gods speak to outside or things may get confusing
                        fromNamePrefix = m_adminPrefix;
                        checkParcelHide = false;
                    }
                    else
                    {
                        checkParcelHide = true;
                    }
                    destination = UUID.Zero; // Avatars cant "SayTo"
                    ownerID = c.Sender.AgentId;

                    hidePos = fromPos;
                    break;

                case ChatSourceType.Object:
                    fromID = c.SenderUUID;

                    if (c.SenderObject != null && c.SenderObject is SceneObjectPart)
                    {
                        ownerID = ((SceneObjectPart)c.SenderObject).OwnerID;
                        if (((SceneObjectPart)c.SenderObject).ParentGroup.IsAttachment)
                        {
                            checkParcelHide = true;
                            hidePos = ((SceneObjectPart)c.SenderObject).ParentGroup.AbsolutePosition;
                        }
                    }
                    break;
            }

            if (message.Length > 1100)
                message = message.Substring(0, 1000);

            //m_log.DebugFormat(
            //    "[CHAT]: DCTA: fromID {0} fromName {1}, region{2}, cType {3}, sType {4}",
            //    fromID, fromName, scene.RegionInfo.RegionName, c.Type, sourceType);

            if (checkParcelHide)
            {
                checkParcelHide = false;
                if (c.Type < ChatTypeEnum.DebugChannel && destination.IsZero())
                {
                    ILandObject srcland = scene.LandChannel.GetLandObject(hidePos.X, hidePos.Y);
                    if (srcland != null && !srcland.LandData.SeeAVs)
                    {
                        sourceParcelID = srcland.LandData.GlobalID;
                        checkParcelHide = true;
                    }
                }
            }

            Vector3 regionPos = new Vector3(scene.RegionInfo.WorldLocX, scene.RegionInfo.WorldLocY, 0);
            scene.ForEachScenePresence(
                delegate(ScenePresence presence)
                {
                    if (destination.IsNotZero() && presence.UUID.NotEqual(destination))
                        return;

                    if(presence.IsChildAgent)
                    {
                        if(!checkParcelHide)
                        {
                            TrySendChatMessage(presence, fromPos, regionPos, fromID,
                                    ownerID, fromNamePrefix + fromName, c.Type,
                                    message, sourceType, destination.IsNotZero());
                        }
                        return;
                    }

                    ILandObject Presencecheck = scene.LandChannel.GetLandObject(presence.AbsolutePosition.X, presence.AbsolutePosition.Y);
                    if (Presencecheck != null)
                    {
                        if (checkParcelHide)
                        {
                            if (sourceParcelID.NotEqual(Presencecheck.LandData.GlobalID) && !presence.IsViewerUIGod)
                                return;
                        }
                        if (c.Sender == null || !Presencecheck.IsEitherBannedOrRestricted(c.Sender.AgentId))
                        {
                            TrySendChatMessage(presence, fromPos, regionPos, fromID,
                                        ownerID, fromNamePrefix + fromName, c.Type,
                                        message, sourceType, destination.IsNotZero());
                        }
                    }
                });
        }

        static protected Vector3 CenterOfRegion = new Vector3(128, 128, 30);

        public virtual void OnChatBroadcast(Object sender, OSChatMessage c)
        {
            if (c.Channel != 0 && c.Channel != DEBUG_CHANNEL) return;

            ChatTypeEnum cType;
            if (c.Channel == DEBUG_CHANNEL)
                cType = ChatTypeEnum.DebugChannel;
            else if (c.Type == ChatTypeEnum.Region)
                cType = ChatTypeEnum.Say;
            else
                cType = c.Type;

            if (c.Message.Length > 1100)
                c.Message = c.Message.Substring(0, 1000);

            // broadcast chat works by redistributing every incoming chat
            // message to each avatar in the scene.
            string fromName = c.From;

            UUID fromID;
            UUID ownerID;
            ChatSourceType sourceType = ChatSourceType.Object;
            if (null != c.Sender)
            {
                ScenePresence avatar = (c.Scene as Scene).GetScenePresence(c.Sender.AgentId);
                fromID = c.Sender.AgentId;
                fromName = avatar.Name;
                ownerID = UUID.Zero;
                sourceType = ChatSourceType.Agent;
            }
            else if (c.SenderUUID.IsNotZero())
            {
                if(c.SenderObject == null)
                    return;
                fromID = c.SenderUUID;
                ownerID = ((SceneObjectPart)c.SenderObject).OwnerID;
                sourceType = ChatSourceType.Object;
            }
            else
            {
                sourceType = ChatSourceType.Object;
                fromID = UUID.Zero;
                ownerID = UUID.Zero;
            }

            // m_log.DebugFormat("[CHAT] Broadcast: fromID {0} fromName {1}, cType {2}, sType {3}", fromID, fromName, cType, sourceType);
            Scene scene = c.Scene as Scene;
            if (scene != null)
            {
                scene.ForEachRootClient
                (
                    delegate(IClientAPI client)
                    {
                        // don't forward SayOwner chat from objects to
                        // non-owner agents
                        if ((c.Type == ChatTypeEnum.Owner) &&
                            (null != c.SenderObject) &&
                            (((SceneObjectPart)c.SenderObject).OwnerID.NotEqual(client.AgentId)))
                            return;

                        client.SendChatMessage(c.Message, (byte)cType, CenterOfRegion, fromName, fromID, ownerID,
                                               (byte)sourceType, (byte)ChatAudibleLevel.Fully);
                    }
                );
             }
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
            if (presence.IsDeleted || presence.IsInTransit || !presence.ControllingClient.IsActive)
                return false;

            if (!ignoreDistance)
            {
                float maxDistSQ;
                switch(type)
                {
                    case ChatTypeEnum.Whisper:
                        maxDistSQ = m_whisperdistanceSQ;
                        break;
                    case ChatTypeEnum.Say:
                        maxDistSQ = m_saydistanceSQ;
                        break;
                    case ChatTypeEnum.Shout:
                        maxDistSQ = m_shoutdistanceSQ;
                        break;
                    default:
                        maxDistSQ = -1f;
                        break;
                }

                if(maxDistSQ > 0)
                {
                    Vector3 fromRegionPos = fromPos + regionPos;
                    Vector3 toRegionPos = presence.AbsolutePosition +
                        new Vector3(presence.Scene.RegionInfo.WorldLocX, presence.Scene.RegionInfo.WorldLocY, 0);

                    if(maxDistSQ < Vector3.DistanceSquared(toRegionPos, fromRegionPos))
                        return false;
                }
            }

            presence.ControllingClient.SendChatMessage(
                message, (byte) type, fromPos, fromName,
                fromAgentID, ownerID, (byte)src, (byte)ChatAudibleLevel.Fully);

            return true;
        }

        Dictionary<UUID, System.Threading.Timer> Timers = new Dictionary<UUID, System.Threading.Timer>();
        public virtual void ParcelFreezeUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            System.Threading.Timer Timer;
            if (flags == 0)
            {
                FreezeCache.Add(target.ToString());
                System.Threading.TimerCallback timeCB = new System.Threading.TimerCallback(OnEndParcelFrozen);
                Timer = new System.Threading.Timer(timeCB, target, 30000, 0);
                Timers.Add(target, Timer);
            }
            else
            {
                FreezeCache.Remove(target.ToString());
                Timers.TryGetValue(target, out Timer);
                Timers.Remove(target);
                Timer.Dispose();
            }
        }

        protected virtual void OnEndParcelFrozen(object avatar)
        {
            UUID target = (UUID)avatar;
            FreezeCache.Remove(target.ToString());
            System.Threading.Timer Timer;
            Timers.TryGetValue(target, out Timer);
            Timers.Remove(target);
            Timer.Dispose();
        }
    }
}
