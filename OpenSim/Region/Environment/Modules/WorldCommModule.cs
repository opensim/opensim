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
* 
*/

using System;
using System.Collections.Generic;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

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
 * 
 * **************************************************/

namespace OpenSim.Region.Environment.Modules
{
    public class WorldCommModule : IRegionModule, IWorldComm
    {
        private Scene m_scene;
        private object CommListLock = new object();
        private object ListLock = new object();
        private string m_name = "WorldCommModule";
        private ListenerManager m_listenerManager;
        private Queue<ListenerInfo> m_pending;

        public WorldCommModule()
        {
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IWorldComm>(this);
            m_listenerManager = new ListenerManager();
            m_scene.EventManager.OnNewClient += NewClient;
            m_pending = new Queue<ListenerInfo>();
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnChatFromViewer += DeliverClientMessage;
        }

        private void DeliverClientMessage(Object sender, ChatFromViewerArgs e)
        {
            DeliverMessage(e.Sender.AgentId.ToString(),
                           e.Type, e.Channel,
                           e.Sender.FirstName + " " + e.Sender.LastName,
                           e.Message);
        }

        public int Listen(uint localID, LLUUID itemID, LLUUID hostID, int channel, string name, string id, string msg)
        {
            return m_listenerManager.AddListener(localID, itemID, hostID, channel, name, id, msg);
        }

        public void ListenControl(int handle, int active)
        {
            if (active == 1)
                m_listenerManager.Activate(handle);
            else if (active == 0)
                m_listenerManager.Dectivate(handle);
        }

        public void ListenRemove(int handle)
        {
            m_listenerManager.Remove(handle);
        }

        public void DeleteListener(LLUUID itemID)
        {
            if (m_listenerManager != null)
            {
                lock (ListLock)
                {
                    m_listenerManager.DeleteListener(itemID);
                }
            }

        }

        // This method scans nearby objects and determines if they are listeners,
        // and if so if this message fits the filter.  If it does, then
        // enqueue the message for delivery to the objects listen event handler.
        // Objects that do an llSay have their messages delivered here, and for 
        // nearby avatars, the SimChat function is used.
        public void DeliverMessage(string sourceItemID, ChatTypeEnum type, int channel, string name, string msg)
        {
            SceneObjectPart source = null;
            ScenePresence avatar = null;

            source = m_scene.GetSceneObjectPart(new LLUUID(sourceItemID));
            if (source == null)
            {
                avatar = m_scene.GetScenePresence(new LLUUID(sourceItemID));
            }
            if ((avatar != null) || (source != null))
            {
                // Loop through the objects in the scene
                // If they are in proximity, then if they are
                // listeners, if so add them to the pending queue

                foreach (ListenerInfo li in m_listenerManager.GetListeners())
                {
                    EntityBase sPart;

                    m_scene.Entities.TryGetValue(li.GetHostID(), out sPart);

                    // Dont process if this message is from itself!
                    if (li.GetHostID().ToString().Equals(sourceItemID) ||
                        sPart.UUID.ToString().Equals(sourceItemID))
                        continue;

                    double dis = 0;

                    if (source != null)
                        dis = Util.GetDistanceTo(sPart.AbsolutePosition, source.AbsolutePosition);
                    else
                        dis = Util.GetDistanceTo(sPart.AbsolutePosition, avatar.AbsolutePosition);

                    switch (type)
                    {
                        case ChatTypeEnum.Whisper:

                            if ((dis < 10) && (dis > -10))
                            {
                                ListenerInfo isListener = m_listenerManager.IsListenerMatch(
                                    sourceItemID, sPart.UUID, channel, name, msg
                                    );
                                if (isListener != null)
                                {
                                    lock (CommListLock)
                                    {
                                        m_pending.Enqueue(isListener);
                                    }
                                }
                            }
                            break;

                        case ChatTypeEnum.Say:

                            if ((dis < 30) && (dis > -30))
                            {
                                ListenerInfo isListener = m_listenerManager.IsListenerMatch(
                                    sourceItemID, sPart.UUID, channel, name, msg
                                    );
                                if (isListener != null)
                                {
                                    lock (CommListLock)
                                    {
                                        m_pending.Enqueue(isListener);
                                    }
                                }
                            }
                            break;

                        case ChatTypeEnum.Shout:
                            if ((dis < 100) && (dis > -100))
                            {
                                ListenerInfo isListener = m_listenerManager.IsListenerMatch(
                                    sourceItemID, sPart.UUID, channel, name, msg
                                    );
                                if (isListener != null)
                                {
                                    lock (CommListLock)
                                    {
                                        m_pending.Enqueue(isListener);
                                    }
                                }
                            }
                            break;

                        case ChatTypeEnum.Broadcast:
                            ListenerInfo isListen =
                                m_listenerManager.IsListenerMatch(sourceItemID, li.GetItemID(), channel, name, msg);
                            if (isListen != null)
                            {
                                ListenerInfo isListener = m_listenerManager.IsListenerMatch(
                                    sourceItemID, sPart.UUID, channel, name, msg
                                    );
                                if (isListener != null)
                                {
                                    lock (CommListLock)
                                    {
                                        m_pending.Enqueue(isListener);
                                    }
                                }
                            }
                            break;
                    }
                }
            }
        }

        public bool HasMessages()
        {
            if (m_pending != null)
                return (m_pending.Count > 0);
            else
                return false;
        }

        public ListenerInfo GetNextMessage()
        {
            ListenerInfo li = null;

            lock (CommListLock)
            {
                li = m_pending.Dequeue();
            }

            return li;
        }

        public uint PeekNextMessageLocalID()
        {
            return m_pending.Peek().GetLocalID();
        }

        public LLUUID PeekNextMessageItemID()
        {
            return m_pending.Peek().GetItemID();
        }
 
    }

    // hostID: the ID of the ScenePart
    // itemID: the ID of the script host engine
    // localID: local ID of host engine
    public class ListenerManager
    {
        private Dictionary<int, ListenerInfo> m_listeners;
        private object ListenersLock = new object();
        private int m_MaxListeners = 100;

        public ListenerManager()
        {
            m_listeners = new Dictionary<int, ListenerInfo>();
        }

        public int AddListener(uint localID, LLUUID itemID, LLUUID hostID, int channel, string name, string id, string msg)
        {
            if (m_listeners.Count < m_MaxListeners)
            {
                ListenerInfo isListener = IsListenerMatch(LLUUID.Zero.ToString(), itemID, channel, name, msg);

                if (isListener == null)
                {
                    int newHandle = GetNewHandle();

                    if (newHandle > -1)
                    {
                        ListenerInfo li = new ListenerInfo(localID, newHandle, itemID, hostID, channel, name, id, msg);

                        lock (ListenersLock)
                        {
                            m_listeners.Add(newHandle, li);
                        }

                        return newHandle;
                    }
                }
            }

            return -1;
        }

        public void Remove(int handle)
        {
            m_listeners.Remove(handle);
        }

        public void DeleteListener(LLUUID itemID)
        {
            foreach (ListenerInfo li in m_listeners.Values)
            {
                if (li.GetItemID().Equals(itemID))
                {
                    Remove(li.GetHandle());
                    return;
                }
            }
        }

        private int GetNewHandle()
        {
            for (int i = 0; i < int.MaxValue - 1; i++)
            {
                if (!m_listeners.ContainsKey(i))
                    return i;
            }

            return -1;
        }

        public bool IsListener(LLUUID hostID)
        {
            foreach (ListenerInfo li in m_listeners.Values)
            {
                if (li.GetHostID().Equals(hostID))
                    return true;
            }

            return false;
        }

        public void Activate(int handle)
        {
            ListenerInfo li;

            if (m_listeners.TryGetValue(handle, out li))
            {
                li.Activate();
            }
        }

        public void Dectivate(int handle)
        {
            ListenerInfo li;

            if (m_listeners.TryGetValue(handle, out li))
            {
                li.Deactivate();
            }
        }

        // Theres probably a more clever and efficient way to
        // do this, maybe with regex.
        public ListenerInfo IsListenerMatch(string sourceItemID, LLUUID listenerKey, int channel, string name,
                                            string msg)
        {
            bool isMatch = true;

            foreach (ListenerInfo li in m_listeners.Values)
            {
                if (li.GetHostID().Equals(listenerKey))
                {
                    if (li.IsActive())
                    {
                        if (channel == li.GetChannel())
                        {
                            if ((li.GetID().ToString().Length > 0) &&
                                (!li.GetID().Equals(LLUUID.Zero)))
                            {
                                if (!li.GetID().ToString().Equals(sourceItemID))
                                {
                                    isMatch = false;
                                }
                            }
                            if (isMatch && (li.GetName().Length > 0))
                            {
                                if (li.GetName().Equals(name))
                                {
                                    isMatch = false;
                                }
                            }
                            if (isMatch)
                            {
                                return new ListenerInfo(
                                    li.GetLocalID(), li.GetHandle(), li.GetItemID(), li.GetHostID(),
                                    li.GetChannel(), name, li.GetID(), msg, new LLUUID(sourceItemID)
                                    );
                            }
                        }
                    }
                }
            }
            return null;
        }

        public Dictionary<int, ListenerInfo>.ValueCollection GetListeners()
        {
            return m_listeners.Values;
        }
    }

    public class ListenerInfo
    {
        private LLUUID m_itemID; // ID of the host script engine
        private LLUUID m_hostID; // ID of the host/scene part
        private LLUUID m_sourceItemID; // ID of the scenePart or avatar source of the message
        private int m_channel; // Channel
        private int m_handle; // Assigned handle of this listener
        private uint m_localID; // Local ID from script engine
        private string m_name; // Object name to filter messages from
        private LLUUID m_id; // ID to filter messages from
        private string m_message; // The message
        private bool m_active; // Listener is active or not

        public ListenerInfo(uint localID, int handle, LLUUID ItemID, LLUUID hostID, int channel, string name, LLUUID id, string message)
        {
            Initialise(localID, handle, ItemID, hostID, channel, name, id, message);
        }

        public ListenerInfo(uint localID, int handle, LLUUID ItemID, LLUUID hostID, int channel, string name, LLUUID id,
            string message, LLUUID sourceItemID)
        {
            Initialise(localID, handle, ItemID, hostID, channel, name, id, message);
            m_sourceItemID = sourceItemID;
        }

        private void Initialise(uint localID, int handle, LLUUID ItemID, LLUUID hostID, int channel, string name,
            LLUUID id, string message)
        {
            m_handle = handle;
            m_channel = channel;
            m_itemID = ItemID;
            m_hostID = hostID;
            m_name = name;
            m_id = id;
            m_message = message;
            m_active = true;
            m_localID = localID;
        }

        public LLUUID GetItemID()
        {
            return m_itemID;
        }

        public LLUUID GetHostID()
        {
            return m_hostID;
        }

        public LLUUID GetSourceItemID()
        {
            return m_sourceItemID;
        }

        public int GetChannel()
        {
            return m_channel;
        }

        public uint GetLocalID()
        {
            return m_localID;
        }

        public int GetHandle()
        {
            return m_handle;
        }

        public string GetMessage()
        {
            return m_message;
        }

        public string GetName()
        {
            return m_name;
        }

        public bool IsActive()
        {
            return m_active;
        }

        public void Deactivate()
        {
            m_active = false;
        }

        public void Activate()
        {
            m_active = true;
        }

        public LLUUID GetID()
        {
            return m_id;
        }

    }
}
