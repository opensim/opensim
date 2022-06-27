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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Nini.Config;
using Mono.Addins;

using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

// using log4net;
// using System.Reflection;


/*****************************************************
 *
 * WorldCommModule
 *
 *
 * Holding place for world comms - basically llListen
 * function implementation.
 *
 * lLListen(integer channel, string name, key id, string msg)
 * The name, id, and msg arguments specify the filtering
 * criteria. You can pass the empty string
 * (or NULL_KEY for id) for these to set a completely
 * open filter; this causes the listen() event handler to be
 * invoked for all chat on the channel. To listen only
 * for chat spoken by a specific object or avatar,
 * specify the name and/or id arguments. To listen
 * only for a specific command, specify the
 * (case-sensitive) msg argument. If msg is not empty,
 * listener will only hear strings which are exactly equal
 * to msg. You can also use all the arguments to establish
 * the most restrictive filtering criteria.
 *
 * It might be useful for each listener to maintain a message
 * digest, with a list of recent messages by UUID.  This can
 * be used to prevent in-world repeater loops.  However, the
 * linden functions do not have this capability, so for now
 * thats the way it works.
 * Instead it blocks messages originating from the same prim.
 * (not Object!)
 *
 * For LSL compliance, note the following:
 * (Tested again 1.21.1 on May 2, 2008)
 * 1. 'id' has to be parsed into a UUID. None-UUID keys are
 *    to be replaced by the ZeroID key. (Well, TryParse does
 *    that for us.
 * 2. Setting up an listen event from the same script, with the
 *    same filter settings (including step 1), returns the same
 *    handle as the original filter.
 * 3. (TODO) handles should be script-local. Starting from 1.
 *    Might be actually easier to map the global handle into
 *    script-local handle in the ScriptEngine. Not sure if its
 *    worth the effort tho.
 *
 * **************************************************/

namespace OpenSim.Region.CoreModules.Scripting.WorldComm
{
    [Mono.Addins.Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "WorldCommModule")]
    public class WorldCommModule : IWorldComm, INonSharedRegionModule
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int DEBUG_CHANNEL = 0x7fffffff;

        private object mainLock = new object();
        private Dictionary<int, List<ListenerInfo>> m_listenersByChannel = new Dictionary<int, List<ListenerInfo>>();
        private int m_maxlisteners = 1000;
        private int m_maxhandles = 65;
        private int m_curlisteners;
        private Scene m_scene;
 
        private int m_whisperdistance = 10;
        private int m_saydistance = 20;
        private int m_shoutdistance = 100;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            // wrap this in a try block so that defaults will work if
            // the config file doesn't specify otherwise.
            try
            {
                m_whisperdistance = config.Configs["Chat"].GetInt("whisper_distance", m_whisperdistance);
                m_saydistance = config.Configs["Chat"].GetInt("say_distance", m_saydistance);
                m_shoutdistance = config.Configs["Chat"].GetInt("shout_distance", m_shoutdistance);
                m_maxlisteners = config.Configs["LL-Functions"].GetInt("max_listens_per_region", m_maxlisteners);
                m_maxhandles = config.Configs["LL-Functions"].GetInt("max_listens_per_script", m_maxhandles);
            }
            catch (Exception)
            {
            }

            m_whisperdistance *= m_whisperdistance;
            m_saydistance *= m_saydistance;
            m_shoutdistance *= m_shoutdistance;

            if (m_maxlisteners < 1)
                m_maxlisteners = int.MaxValue;
            if (m_maxhandles < 1)
                m_maxhandles = int.MaxValue;

            if (m_maxlisteners < m_maxhandles)
                m_maxlisteners = m_maxhandles;

        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IWorldComm>(this);
            m_scene.EventManager.OnChatFromClient += DeliverClientMessage;
            m_scene.EventManager.OnChatBroadcast += DeliverClientMessage;
        }

        public void RegionLoaded(Scene scene) { }

        public void RemoveRegion(Scene scene)
        {
            if (scene != m_scene)
                return;

            m_scene.UnregisterModuleInterface<IWorldComm>(this);
            m_scene.EventManager.OnChatBroadcast -= DeliverClientMessage;
            m_scene.EventManager.OnChatBroadcast -= DeliverClientMessage;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "WorldCommModule"; }
        }

        public Type ReplaceableInterface { get { return null; } }

        #endregion

        #region IWorldComm Members

        /// <summary>
        /// Create a listen event callback with the specified filters.
        /// The parameters localID,itemID are needed to uniquely identify
        /// the script during 'peek' time. Parameter hostID is needed to
        /// determine the position of the script.
        /// </summary>
        /// <param name="localID">localID of the script engine</param>
        /// <param name="itemID">UUID of the script engine</param>
        /// <param name="hostID">UUID of the SceneObjectPart</param>
        /// <param name="channel">channel to listen on</param>
        /// <param name="name">name to filter on</param>
        /// <param name="id">
        /// key to filter on (user given, could be totally faked)
        /// </param>
        /// <param name="msg">msg to filter on</param>
        /// <returns>number of the scripts handle</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Listen(UUID itemID, UUID hostID, int channel, string name, UUID id, string msg)
        {
            return Listen(itemID, hostID, channel, name, id, msg, 0);
        }

        /// <summary>
        /// Create a listen event callback with the specified filters.
        /// The parameters localID,itemID are needed to uniquely identify
        /// the script during 'peek' time. Parameter hostID is needed to
        /// determine the position of the script.
        /// </summary>
        /// <param name="itemID">UUID of the script engine</param>
        /// <param name="hostID">UUID of the SceneObjectPart</param>
        /// <param name="channel">channel to listen on</param>
        /// <param name="name">name to filter on</param>
        /// <param name="id">
        /// key to filter on (user given, could be totally faked)
        /// </param>
        /// <param name="msg">msg to filter on</param>
        /// <param name="regexBitfield">
        /// Bitfield indicating which strings should be processed as regex.
        /// </param>
        /// <returns>number of the scripts handle</returns>
        public int Listen(UUID itemID, UUID hostID, int channel,
                string name, UUID id, string msg, int regexBitfield)
        {
            // do we already have a match on this particular filter event?
            List<ListenerInfo> coll = GetListeners(itemID, channel, name, id, msg);

            if (coll.Count > 0)
            {
                // special case, called with same filter settings, return same
                // handle (2008-05-02, tested on 1.21.1 server, still holds)
                return coll[0].Handle;
            }

            lock (mainLock)
            {
                if (m_curlisteners < m_maxlisteners)
                {
                    int newHandle = GetNewHandle(itemID);

                    if (newHandle > 0)
                    {
                        ListenerInfo li = new ListenerInfo(newHandle,
                                itemID, hostID, channel, name, id, msg,
                                regexBitfield);

                        if (!m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners))
                        {
                            listeners = new List<ListenerInfo>();
                            m_listenersByChannel.Add(channel, listeners);
                        }
                        listeners.Add(li);
                        m_curlisteners++;

                        return newHandle;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Sets the listen event with handle as active (active = TRUE) or inactive (active = FALSE).
        /// The handle used is returned from Listen()
        /// </summary>
        /// <param name="itemID">UUID of the script engine</param>
        /// <param name="handle">handle returned by Listen()</param>
        /// <param name="active">temp. activate or deactivate the Listen()</param>
        public void ListenControl(UUID itemID, int handle, int active)
        {
            lock (mainLock)
            {
                foreach (KeyValuePair<int, List<ListenerInfo>> lis in m_listenersByChannel)
                {
                    foreach (ListenerInfo li in lis.Value)
                    {
                        if (handle == li.Handle && itemID.Equals(li.ItemID))
                        {
                            if (active == 0)
                                li.Deactivate();
                            else
                                li.Activate();
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes the listen event callback with handle
        /// </summary>
        /// <param name="itemID">UUID of the script engine</param>
        /// <param name="handle">handle returned by Listen()</param>
        public void ListenRemove(UUID itemID, int handle)
        {
            lock (mainLock)
            {
                foreach (KeyValuePair<int, List<ListenerInfo>> lis in m_listenersByChannel)
                {
                    foreach (ListenerInfo li in lis.Value)
                    {
                        if (handle == li.Handle && itemID.Equals(li.ItemID))
                        {
                            lis.Value.Remove(li);
                            m_curlisteners--;
                            if (lis.Value.Count == 0)
                                m_listenersByChannel.Remove(lis.Key); // bailing of loop so this does not smoke
                                                                        // there should be only one, so we bail out early
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all listen event callbacks for the given scriptID
        /// </summary>
        /// <param name="scriptID">UUID of the script</param>
        public void DeleteListener(UUID scriptID)
        {
            List<int> emptyChannels = new List<int>();
            List<ListenerInfo> removedListeners = new List<ListenerInfo>();
            lock (mainLock)
            {
                foreach (KeyValuePair<int, List<ListenerInfo>> lis in m_listenersByChannel)
                {
                    foreach (ListenerInfo li in lis.Value)
                    {
                        if (scriptID.Equals(li.ItemID))
                            removedListeners.Add(li);
                    }
                    foreach (ListenerInfo li in removedListeners)
                    {
                        lis.Value.Remove(li);
                         m_curlisteners--;
                         if (lis.Value.Count == 0)
                            emptyChannels.Add(lis.Key);
                    }
                }
                foreach (int key in emptyChannels)
                    m_listenersByChannel.Remove(key);
            }
        }

        public void DeliverMessage(ChatTypeEnum type, int channel, string name, UUID id, string msg)
        {
            if (type == ChatTypeEnum.Region)
            {
                TryEnqueueMessage(channel, name, id, msg);
                return;
            }

            SceneObjectPart source;
            ScenePresence avatar;
            if ((source = m_scene.GetSceneObjectPart(id)) != null)
                DeliverMessage(type, channel, name, id, msg, source.AbsolutePosition);
            else if ((avatar = m_scene.GetScenePresence(id)) != null)
                DeliverMessage(type, channel, name, id, msg, avatar.AbsolutePosition);
        }

        /// <summary>
        /// This method scans over the objects which registered an interest in listen callbacks.
        /// For everyone it finds, it checks if it fits the given filter. If it does,  then
        /// enqueue the message for delivery to the objects listen event handler.
        /// The enqueued ListenerInfo no longer has filter values, but the actually trigged values.
        /// Objects that do an llSay have their messages delivered here and for nearby avatars,
        /// the OnChatFromClient event is used.
        /// </summary>
        /// <param name="type">type of delvery (whisper,say,shout or regionwide)</param>
        /// <param name="channel">channel to sent on</param>
        /// <param name="name">name of sender (object or avatar)</param>
        /// <param name="id">key of sender (object or avatar)</param>
        /// <param name="msg">msg to sent</param>
        public void DeliverMessage(ChatTypeEnum type, int channel,
                string name, UUID id, string msg, Vector3 position)
        {
            // m_log.DebugFormat("[WorldComm] got[2] type {0}, channel {1}, name {2}, id {3}, msg {4}",
            //                   type, channel, name, id, msg);

            // validate type and set range
            float maxDistanceSQ;
            switch (type)
            {
                case ChatTypeEnum.Whisper:
                    maxDistanceSQ = m_whisperdistance;
                    break;

                case ChatTypeEnum.Say:
                    maxDistanceSQ = m_saydistance;
                    break;

                case ChatTypeEnum.Shout:
                    maxDistanceSQ = m_shoutdistance;
                    break;

                case ChatTypeEnum.Region:
                    TryEnqueueMessage(channel, name, id, msg);
                    return;

                default:
                    return;
            }

            TryEnqueueMessage(channel, position, maxDistanceSQ, name, id, msg);
        }

        /// <summary>
        /// Delivers the message to a scene entity.
        /// </summary>
        /// <param name='target'>
        /// Target.
        /// </param>
        /// <param name='channel'>
        /// Channel.
        /// </param>
        /// <param name='name'>
        /// Name.
        /// </param>
        /// <param name='id'>
        /// Identifier.
        /// </param>
        /// <param name='msg'>
        /// Message.
        /// </param>
        public void DeliverMessageTo(UUID target, int channel, Vector3 pos, string name, UUID id, string msg)
        {
            if (channel == DEBUG_CHANNEL)
                return;

            if(target.IsZero())
                return;

            // Is target an avatar?
            ScenePresence sp = m_scene.GetScenePresence(target);
            if (sp != null)
            {
                 // Send message to avatar
                if (channel == 0)
                {
                   // Channel 0 goes to viewer ONLY
                    m_scene.SimChat(msg, ChatTypeEnum.Direct, 0, pos, name, id, target, false, false);
                    return;
                }

                // for now messages to prims don't cross regions
                if(sp.IsChildAgent)
                    return;

                List<SceneObjectGroup> attachments = sp.GetAttachments();

                if (attachments.Count == 0)
                    return;

                // Get uuid of attachments
                HashSet<UUID> targets = new HashSet<UUID>();
                foreach (SceneObjectGroup sog in attachments)
                {
                    if (!sog.IsDeleted)
                    {
                        SceneObjectPart[] parts = sog.Parts;
                        foreach(SceneObjectPart p in parts)
                            targets.Add(p.UUID);
                    }
                }

                TryEnqueueMessage(channel, targets, name, id, msg);
                return;
            }

            SceneObjectPart part = m_scene.GetSceneObjectPart(target);
            if (part == null) // Not even an object
                return; // No error

            TryEnqueueMessage(channel, target, name, id, msg);
        }

        #endregion

        private void DeliverClientMessage(Object sender, OSChatMessage e)
        {
            // validate type and set range
            float maxDistanceSQ;
            switch (e.Type)
            {
                case ChatTypeEnum.Whisper:
                    maxDistanceSQ = m_whisperdistance;
                    break;

                case ChatTypeEnum.Say:
                    maxDistanceSQ = m_saydistance;
                    break;

                case ChatTypeEnum.Shout:
                    maxDistanceSQ = m_shoutdistance;
                    break;

                case ChatTypeEnum.Region:
                    if (e.Sender == null)
                        TryEnqueueMessage(e.Channel, e.From, UUID.Zero, e.Message);
                    else
                        TryEnqueueMessage(e.Channel, e.Sender.Name, e.Sender.AgentId, e.Message);
                    return;

                default:
                    return;
            }

            if (e.Sender == null)
                TryEnqueueMessage(e.Channel, e.Position, maxDistanceSQ, e.From, UUID.Zero, e.Message);
            else
                TryEnqueueMessage(e.Channel, e.Position, maxDistanceSQ, e.Sender.Name, e.Sender.AgentId, e.Message);
        }

        /// <summary>
        /// Total number of listeners
        /// </summary>
        public int ListenerCount
        {
            get
            {
                lock (mainLock)
                {
                    return m_curlisteners;
                }
            }
        }

        /// <summary>
        /// non-locked access, since its always called in the context of the
        /// lock
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        private int GetNewHandle(UUID itemID)
        {
            List<int> handles = new List<int>();

            // build a list of used keys for this specific itemID...
            foreach (KeyValuePair<int, List<ListenerInfo>> lis in m_listenersByChannel)
            {
                foreach (ListenerInfo li in lis.Value)
                {
                    if (itemID == li.ItemID)
                        handles.Add(li.Handle);
                }
            }

            if(handles.Count >= m_maxhandles)
                return -1;

            // Note: 0 is NOT a valid handle for llListen() to return
            for (int i = 1; i <= m_maxhandles; i++)
            {
                if (!handles.Contains(i))
                    return i;
            }

            return -1;
        }

        /// These are duplicated from ScriptBaseClass
        /// http://opensimulator.org/mantis/view.php?id=6106#c21945
        #region Constants for the bitfield parameter of osListenRegex

        /// <summary>
        /// process name parameter as regex
        /// </summary>
        public const int OS_LISTEN_REGEX_NAME = 0x1;

        /// <summary>
        /// process message parameter as regex
        /// </summary>
        public const int OS_LISTEN_REGEX_MESSAGE = 0x2;

        #endregion

        public bool HasListeners(int channel)
        {
            lock (mainLock)
                return m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners) && listeners.Count > 0;
        }

        /// <summary>
        /// Get listeners matching the input parameters.
        /// </summary>
        /// <remarks>
        /// Theres probably a more clever and efficient way to do this, maybe
        /// with regex.
        /// </remarks>
        /// <param name="itemID"></param>
        /// <param name="channel"></param>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public List<ListenerInfo> GetListeners(UUID itemID, int channel, string name, UUID id, string msg)
        {
            List<ListenerInfo> collection = new List<ListenerInfo>();

            lock (mainLock)
            {
                if (!m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners))
                {
                    return collection;
                }

                bool itemIDNotZero = itemID.IsNotZero();
                foreach (ListenerInfo li in listeners)
                {
                    if (!li.IsActive)
                        continue;

                    if (itemIDNotZero && itemID.NotEqual(li.ItemID))
                        continue;

                    if (li.ID.IsNotZero() && id.NotEqual(li.ID))
                        continue;

                    if (li.Name.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_NAME) == OS_LISTEN_REGEX_NAME)
                        {
                            if (!Regex.IsMatch(name, li.Name))
                                continue;
                        }
                        else
                        {
                            if (!name.Equals(li.Name, StringComparison.InvariantCulture))
                                continue;
                        }
                    }

                    if (li.Message.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_MESSAGE) == OS_LISTEN_REGEX_MESSAGE)
                        {
                            if (!Regex.IsMatch(msg, li.Message))
                                continue;
                        }
                        else
                        {
                            if (!msg.Equals(li.Message, StringComparison.InvariantCulture))
                                continue;
                        }
                    }
                    collection.Add(li);
                }
            }
            return collection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryEnqueueMessage(int channel, Vector3 position, float maxDistanceSQ, string name, UUID id, string msg)
        {
            lock (mainLock)
            {
                if (!m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners))
                    return;

                foreach (ListenerInfo li in listeners)
                {
                    if (!li.IsActive)
                        continue;

                    if (id.Equals(li.HostID))
                        continue;

                    if (li.ID.IsNotZero() && id.NotEqual(li.ID))
                        continue;

                    if (li.Name.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_NAME) != 0)
                        {
                            if (!Regex.IsMatch(name, li.Name))
                                continue;
                        }
                        else
                        {
                            if (!name.Equals(li.Name, StringComparison.InvariantCulture))
                                continue;
                        }
                    }

                    if (li.Message.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_MESSAGE) != 0)
                        {
                            if (!Regex.IsMatch(msg, li.Message))
                                continue;
                        }
                        else
                        {
                            if (!msg.Equals(li.Message, StringComparison.InvariantCulture))
                                continue;
                        }
                    }

                    SceneObjectPart sPart = m_scene.GetSceneObjectPart(li.HostID);
                    if (sPart == null)
                        return;

                    if (maxDistanceSQ > Vector3.DistanceSquared(sPart.AbsolutePosition, position))
                    {
                        m_scene.EventManager.TriggerScriptListen(li.ItemID, channel, name, id, msg);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryEnqueueMessage(int channel, string name, UUID id, string msg)
        {
            lock (mainLock)
            {
                if (!m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners))
                    return;

                foreach (ListenerInfo li in listeners)
                {
                    if (!li.IsActive)
                        continue;

                    if (id.Equals(li.HostID))
                        continue;

                    if (li.ID.IsNotZero() && id.NotEqual(li.ID))
                        continue;

                    if (li.Name.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_NAME) != 0)
                        {
                            if (!Regex.IsMatch(name, li.Name))
                                continue;
                        }
                        else
                        {
                            if (!name.Equals(li.Name, StringComparison.InvariantCulture))
                                continue;
                        }
                    }

                    if (li.Message.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_MESSAGE) != 0)
                        {
                            if (!Regex.IsMatch(msg, li.Message))
                                continue;
                        }
                        else
                        {
                            if (!msg.Equals(li.Message, StringComparison.InvariantCulture))
                                continue;
                        }
                    }
                    m_scene.EventManager.TriggerScriptListen(li.ItemID, channel, name, id, msg);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryEnqueueMessage(int channel, UUID target, string name, UUID id, string msg)
        {
            lock (mainLock)
            {
                if (!m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners))
                    return;

                foreach (ListenerInfo li in listeners)
                {
                    if (!li.IsActive)
                        continue;

                    if (id.Equals(li.HostID))
                        continue;

                    if (target.NotEqual(li.HostID))
                        continue;

                    if (li.ID.IsNotZero() && id.NotEqual(li.ID))
                        continue;

                    if (li.Name.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_NAME) != 0)
                        {
                            if (!Regex.IsMatch(name, li.Name))
                                continue;
                        }
                        else
                        {
                            if (!name.Equals(li.Name, StringComparison.InvariantCulture))
                                continue;
                        }
                    }

                    if (li.Message.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_MESSAGE) != 0)
                        {
                            if (!Regex.IsMatch(msg, li.Message))
                                continue;
                        }
                        else
                        {
                            if (!msg.Equals(li.Message, StringComparison.InvariantCulture))
                                continue;
                        }
                    }
                    m_scene.EventManager.TriggerScriptListen(li.ItemID, channel, name, id, msg);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryEnqueueMessage(int channel, HashSet<UUID> targets, string name, UUID id, string msg)
        {
            lock (mainLock)
            {
                if (!m_listenersByChannel.TryGetValue(channel, out List<ListenerInfo> listeners))
                    return;

                foreach (ListenerInfo li in listeners)
                {
                    if (!li.IsActive)
                        continue;

                    if (id.Equals(li.HostID))
                        continue;

                    if (!targets.Contains(li.HostID))
                        continue;

                    if (li.ID.IsNotZero() && id.NotEqual(li.ID))
                        continue;

                    if (li.Name.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_NAME) != 0)
                        {
                            if (!Regex.IsMatch(name, li.Name))
                                continue;
                        }
                        else
                        {
                            if (!name.Equals(li.Name, StringComparison.InvariantCulture))
                                continue;
                        }
                    }

                    if (li.Message.Length > 0)
                    {
                        if ((li.RegexBitfield & OS_LISTEN_REGEX_MESSAGE) != 0)
                        {
                            if (!Regex.IsMatch(msg, li.Message))
                                continue;
                        }
                        else
                        {
                            if (!msg.Equals(li.Message, StringComparison.InvariantCulture))
                                continue;
                        }
                    }
                    m_scene.EventManager.TriggerScriptListen(li.ItemID, channel, name, id, msg);
                }
            }
        }

        public Object[] GetSerializationData(UUID itemID)
        {
            List<Object> data = new List<Object>();

            lock (mainLock)
            {
                foreach (List<ListenerInfo> list in m_listenersByChannel.Values)
                {
                    foreach (ListenerInfo l in list)
                    {
                        if (itemID.Equals(l.ItemID))
                            data.AddRange(l.GetSerializationData());
                    }
                }
            }
            return data.ToArray();
        }

        public void CreateFromData(UUID itemID, UUID hostID, Object[] data)
        {
            int idx = 0;
            Object[] item = new Object[6];
            int dataItemLength = 6;

            while (idx < data.Length)
            {
                dataItemLength = (idx + 7 == data.Length || (idx + 7 < data.Length && data[idx + 7] is bool)) ? 7 : 6;
                item = new Object[dataItemLength];
                Array.Copy(data, idx, item, 0, dataItemLength);

                ListenerInfo info = ListenerInfo.FromData(itemID, hostID, item);

                lock (mainLock)
                {
                    if (!m_listenersByChannel.ContainsKey((int)item[2]))
                    {
                        m_listenersByChannel.Add((int)item[2], new List<ListenerInfo>());
                    }
                    m_listenersByChannel[(int)item[2]].Add(info);
                }

                idx += dataItemLength;
            }
        }
    }

    public class ListenerInfo : IWorldCommListenerInfo
    {
        /// <summary>
        /// Listener is active or not
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Assigned handle of this listener
        /// </summary>
        public int Handle { get; private set; }

        /// <summary>
        /// ID of the host script engine
        /// </summary>
        public UUID ItemID { get; private set; }

        /// <summary>
        /// ID of the host/scene part
        /// </summary>
        public UUID HostID { get; private set; }

        /// <summary>
        /// Channel
        /// </summary>
        public int Channel { get; private set; }

        /// <summary>
        /// ID to filter messages from
        /// </summary>
        public UUID ID { get; private set; }

        /// <summary>
        /// Object name to filter messages from
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The message
        /// </summary>
        public string Message { get; private set; }
        public int RegexBitfield { get; private set; }

        public ListenerInfo()
        {       
        }

        public ListenerInfo(int handle, UUID _ItemID,
                UUID hostID, int channel, string name, UUID id,
                string message)
        {
            IsActive = true;
            Handle = handle;
            ItemID = _ItemID;
            HostID = hostID;
            Channel = channel;
            Name = name;
            ID = id;
            Message = message;
            RegexBitfield = 0;
        }

        public ListenerInfo(int handle, UUID _ItemID,
                UUID hostID, int channel, string name, UUID id,
                string message, int regexBitfield)
        {
            IsActive = true;
            Handle = handle;
            ItemID = _ItemID;
            HostID = hostID;
            Channel = channel;
            Name = name;
            ID = id;
            Message = message;
            RegexBitfield = regexBitfield;
        }

        public ListenerInfo(ListenerInfo li, string name, UUID id, string message)
        {
            IsActive = true;
            Handle = li.Handle;
            ItemID = li.ItemID;
            HostID = li.HostID;
            Channel = li.Channel;
            Name = name;
            ID = id;
            Message = message;
        }

        public ListenerInfo(ListenerInfo li, string name, UUID id, string message, int regexBitfield)
        {
            IsActive = true;
            Handle = li.Handle;
            ItemID = li.ItemID;
            HostID = li.HostID;
            Channel = li.Channel;
            Name = name;
            ID = id;
            Message = message;
            RegexBitfield = regexBitfield;
        }

        public Object[] GetSerializationData()
        {
            Object[] data = new Object[7];

            data[0] = IsActive;
            data[1] = Handle;
            data[2] = Channel;
            data[3] = Name;
            data[4] = ID;
            data[5] = Message;
            data[6] = RegexBitfield;

            return data;
        }

        public static ListenerInfo FromData(UUID _ItemID, UUID hostID, Object[] data)
        {
            return new ListenerInfo()
            {
                IsActive = (bool)data[0],
                Handle = (int)data[1],
                ItemID = _ItemID,
                HostID = hostID,
                Channel = (int)data[2],
                Name = (string)data[3],
                ID = (UUID)data[4],
                Message = (string)data[5],
                RegexBitfield = (data.Length > 6) ? (int)data[6] : 0
            };
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
