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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    public struct QueueItem
    {
        public int id;
        public OSDMap body;
    }

    [Mono.Addins.Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EventQueueGetModule")]
    public partial class  EventQueueGetModule : IEventQueue, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[EVENT QUEUE GET MODULE]";

        private const int KEEPALIVE = 60; // this could be larger now, but viewers expect it on opensim
        // we need to go back to close before viwers, or we may lose data
        private const int VIEWERKEEPALIVE = (KEEPALIVE - 2) * 1000; // do it shorter

        /// <value>
        /// Debug level.
        /// </value>
        public int DebugLevel { get; set; }

        protected Scene m_scene;

        private readonly Dictionary<UUID, int> m_ids = new();

        private readonly Dictionary<UUID, Queue<byte[]>> queues = new();
        private readonly Dictionary<UUID, UUID> m_AvatarQueueUUIDMapping = new();

        #region INonSharedRegionModule methods
        public virtual void Initialise(IConfigSource config)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            scene.RegisterModuleInterface<IEventQueue>(this);

            scene.EventManager.OnClientClosed += ClientClosed;
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;

            MainConsole.Instance.Commands.AddCommand(
                "Debug",
                false,
                "debug eq",
                "debug eq [0|1|2]",
                "Turn on event queue debugging\n"
                    + "  <= 0 - turns off all event queue logging\n"
                    + "  >= 1 - turns on event queue setup and outgoing event logging\n"
                    + "  >= 2 - turns on poll notification",
                HandleDebugEq);

            MainConsole.Instance.Commands.AddCommand(
                "Debug",
                false,
                "show eq",
                "show eq",
                "Show contents of event queues for logged in avatars.  Used for debugging.",
                HandleShowEq);
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_scene != scene)
                return;

            scene.EventManager.OnClientClosed -= ClientClosed;
            scene.EventManager.OnRegisterCaps -= OnRegisterCaps;

            scene.UnregisterModuleInterface<IEventQueue>(this);
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "EventQueueGetModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        protected void HandleDebugEq(string module, string[] args)
        {

            if (!(args.Length == 3 && int.TryParse(args[2], out int debugLevel)))
            {
                MainConsole.Instance.Output("Usage: debug eq [0|1|2]");
            }
            else
            {
                DebugLevel = debugLevel;
                MainConsole.Instance.Output($"Set event queue debug level to {DebugLevel} in {m_scene.RegionInfo.RegionName}");
            }
        }

        protected void HandleShowEq(string module, string[] args)
        {
            MainConsole.Instance.Output($"Events in Scene {m_scene.Name} agents queues :");

            lock (queues)
            {
                foreach (KeyValuePair<UUID, Queue<byte[]>> kvp in queues)
                {
                    MainConsole.Instance.Output($"    {kvp.Key}  {kvp.Value.Count}");
                }
            }
        }

        /// <summary>
        ///  Always returns a valid queue
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        private Queue<byte[]> TryGetQueue(UUID agentId)
        {
            lock (queues)
            {
                if (queues.TryGetValue(agentId, out Queue<byte[]> queue))
                    return queue;

                if (DebugLevel > 0)
                    m_log.DebugFormat(
                       "[EVENTQUEUE]: Adding new queue for agent {0} in region {1}",
                       agentId, m_scene.RegionInfo.RegionName);

                queue = new Queue<byte[]>();
                queues[agentId] = queue;

                return queue;
            }
        }

        /// <summary>

        /// May return a null queue
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        private Queue<byte[]> GetQueue(UUID agentId)
        {
            lock (queues)
            {
                if (queues.TryGetValue(agentId, out  Queue<byte[]> queue))
                    return queue;
                return null;
            }
        }

        #region IEventQueue Members
        //legacy 
        public bool Enqueue(OSD data, UUID avatarID)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            try
            {
                Queue<byte[]> queue = GetQueue(avatarID);
                if (queue != null)
                {
                    byte[] evData = Util.UTF8NBGetbytes(OSDParser.SerializeLLSDInnerXmlString(data));
                    lock (queue)
                        queue.Enqueue(evData);
                }
                else
                {
                    m_log.Warn($"[EVENTQUEUE]: (Enqueue) No queue found for agent {avatarID} in region {m_scene.Name}");
                }
            }
            catch (NullReferenceException e)
            {
                m_log.Error($"[EVENTQUEUE] Caught exception: {e.Message}");
                return false;
            }
            return true;
        }

        //legacy
        /*
        public bool Enqueue(string ev, UUID avatarID)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            try
            {
                Queue<byte[]> queue = GetQueue(avatarID);
                if (queue != null)
                {
                    byte[] evData = Util.UTF8NBGetbytes(ev);
                    lock (queue)
                        queue.Enqueue(evData);
                }
                else
                {
                    m_log.WarnFormat(
                            "[EVENTQUEUE]: (Enqueue) No queue found for agent {0} in region {1}",
                            avatarID,  m_scene.Name);
                }
            }
            catch (NullReferenceException e)
            {
                m_log.Error("[EVENTQUEUE] Caught exception: " + e);
                return false;
            }
            return true;
        }
        */

        public bool Enqueue(byte[] evData, UUID avatarID)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            try
            {
                Queue<byte[]> queue = GetQueue(avatarID);
                if (queue != null)
                {
                    lock (queue)
                        queue.Enqueue(evData);
                }
                else
                {
                    m_log.WarnFormat(
                            "[EVENTQUEUE]: (Enqueue) No queue found for agent {0} in region {1}",
                            avatarID, m_scene.Name);
                }
            }
            catch (NullReferenceException e)
            {
                m_log.Error("[EVENTQUEUE] Caught exception: " + e);
                return false;
            }
            return true;
        }

        public bool Enqueue(osUTF8 o, UUID avatarID)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            try
            {
                Queue<byte[]> queue = GetQueue(avatarID);
                if (queue != null)
                {
                    lock (queue)
                        queue.Enqueue(o.ToArray());
                }
                else
                {
                    m_log.WarnFormat(
                            "[EVENTQUEUE]: (Enqueue) No queue found for agent {0} in region {1}",
                            avatarID, m_scene.Name);
                }
            }
            catch (NullReferenceException e)
            {
                m_log.Error("[EVENTQUEUE] Caught exception: " + e);
                return false;
            }
            return true;
        }

        #endregion

        private void ClientClosed(UUID agentID, Scene scene)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Closed client {0} in region {1}", agentID, m_scene.RegionInfo.RegionName);

            lock (queues)
            {
                queues.Remove(agentID);

                lock (m_AvatarQueueUUIDMapping)
                    m_AvatarQueueUUIDMapping.Remove(agentID);

                lock (m_ids)
                    m_ids.Remove(agentID);
            }

            // m_log.DebugFormat("[EVENTQUEUE]: Deleted queues for {0} in region {1}", agentID, m_scene.RegionInfo.RegionName);

        }

        /// <summary>
        /// Generate an Event Queue Get handler path for the given eqg uuid.
        /// </summary>
        /// <param name='eqgUuid'></param>
        private static string GenerateEqgCapPath(UUID eqgUuid)
        {
            return $"/CE/{eqgUuid}";
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            // Register an event queue for the client

            if (DebugLevel > 0)
                m_log.Debug(
                    $"[EVENTQUEUE]: OnRegisterCaps: agentID {agentID} caps {caps} region {m_scene.Name}");

            UUID eventQueueGetUUID;
            lock (queues)
            {
                queues.TryGetValue(agentID, out Queue<byte[]> queue);

                if (queue == null)
                {
                    queue = new Queue<byte[]>();
                    queues[agentID] = queue;

                    lock (m_AvatarQueueUUIDMapping)
                    {
                        eventQueueGetUUID = UUID.Random();
                        m_AvatarQueueUUIDMapping[agentID] = eventQueueGetUUID;
                        lock (m_ids)
                        {
                            if (m_ids.ContainsKey(agentID))
                                m_ids[agentID]++;
                            else
                            {
                                m_ids[agentID] = Random.Shared.Next(30000000);
                            }
                        }
                    }
                }
                else
                {
                    queue.Enqueue(null);

                    // reuse or not to reuse
                    lock (m_AvatarQueueUUIDMapping)
                    {
                        // Its reuse caps path not queues those are been reused already
                        if (m_AvatarQueueUUIDMapping.TryGetValue(agentID, out eventQueueGetUUID))
                        {
                            m_log.DebugFormat("[EVENTQUEUE]: Found Existing UUID!");
                            lock (m_ids)
                            {
                                // change to negative numbers so they are changed at end of sending first marker
                                // old data on a queue may be sent on a response for a new caps
                                // but at least will be sent with coerent IDs
                                if (m_ids.TryGetValue(agentID, out int previd))
                                    m_ids[agentID] = -previd;
                                else
                                {
                                    m_ids[agentID] = -Random.Shared.Next(30000000);
                                }
                            }
                        }
                        else
                        {
                            eventQueueGetUUID = UUID.Random();
                            m_AvatarQueueUUIDMapping[agentID] = eventQueueGetUUID;
                            lock (m_ids)
                            {
                                if (m_ids.TryGetValue(agentID, out int previd))
                                    m_ids[agentID] = ++previd;
                                else
                                {
                                    m_ids.Add(agentID, Random.Shared.Next(30000000));
                                }
                            }
                        }
                    }
                }
            }

            caps.RegisterPollHandler(
                "EventQueueGet",
                    new PollServiceEventArgs(null, GenerateEqgCapPath(eventQueueGetUUID), HasEvents, GetEvents, NoEvents, Drop, agentID, VIEWERKEEPALIVE));
        }

        public bool HasEvents(UUID _, UUID agentID)
        {
            Queue<byte[]> queue = GetQueue(agentID);
            if (queue != null)
            {
                lock (queue)
                {
                    //m_log.WarnFormat("POLLED FOR EVENTS BY {0} in {1} -- {2}", agentID, m_scene.RegionInfo.RegionName, queue.Count);
                    return queue.Count > 0;
                }
            }
            //m_log.WarnFormat("POLLED FOR EVENTS BY {0} unknown agent", agentID);
            return true;
        }

        /// <summary>
        /// Logs a debug line for an outbound event queue message if appropriate.
        /// </summary>
        /// <param name='element'>Element containing message</param>
        private void LogOutboundDebugMessage(OSD element, UUID agentId)
        {
            if (element is OSDMap ev)
            {
                m_log.Debug($"Eq OUT {ev["message"],-30} to {m_scene.GetScenePresence(agentId).Name,-20} {m_scene.Name,-20}");
            }
        }

        public void Drop(UUID requestID, UUID pAgentId)
        {
            // do nothing, in last case http server will do it
        }

        private static readonly byte[] EventHeader = osUTF8.GetASCIIBytes("<llsd><map><key>events</key><array>");

        public Hashtable GetEvents(UUID requestID, UUID pAgentId)
        {
            if (DebugLevel >= 2)
                m_log.Warn($"POLLED FOR EQ MESSAGES BY {pAgentId} in {m_scene.Name}");

            Queue<byte[]> queue = GetQueue(pAgentId);
            if (queue is null)
                return NoAgent();

            byte[] element = null;
            List<byte[]> elements;

            int totalSize = 0;
            int thisID = 0;
            bool negativeID = false;

            lock (queue)
            {
                if (queue.Count == 0)
                    return NoEvents(requestID, pAgentId);

                lock (m_ids)
                    thisID = m_ids[pAgentId];

                if (thisID < 0)
                {
                    negativeID = true;
                    thisID = -thisID;
                }

                elements = new List<byte[]>(queue.Count + 2) {EventHeader};

                while (queue.Count > 0)
                {
                    element = queue.Dequeue();
                    // add elements until a marker is found
                    // so they get into a response
                    if (element is null)
                        break;

                    if (DebugLevel > 0)
                        LogOutboundDebugMessage(element, pAgentId);

                    elements.Add(element);
                    totalSize += element.Length;
                }
            }

            lock (m_ids)
            {
                if (element is null && negativeID)
                {
                    m_ids[pAgentId] = Random.Shared.Next(30000000);
                }
                else
                    m_ids[pAgentId] = thisID + 1;
            }

            if (totalSize == 0)
                return NoEvents(requestID, pAgentId);

            totalSize += EventHeader.Length;

            osUTF8 sb = OSUTF8Cached.Acquire();
            LLSDxmlEncode2.AddEndArray(sb); // events array
                LLSDxmlEncode2.AddElem("id", thisID, sb);
            LLSDxmlEncode2.AddEndMap(sb);
            element = LLSDxmlEncode2.EndToBytes(sb);
            elements.Add(element);
            totalSize += element.Length;

            Hashtable responsedata = new()
            {
                ["int_response_code"] = 200,
                ["content_type"] = "application/xml"
            };

            //temporary
            byte[] finalData = new byte[totalSize];
            int dst = 0;
            for(int i = 0; i < elements.Count; ++i)
            {
                byte[] src = elements[i];
                Array.Copy(src, 0, finalData, dst, src.Length);
                dst += src.Length;
            }

            responsedata["bin_response_data"] = finalData;
            responsedata["keepaliveTimeout"] = KEEPALIVE;

            return responsedata;
        }

        public Hashtable NoEvents(UUID _, UUID agentID)
        {
            return new Hashtable()
            {
                ["int_response_code"] = GetQueue(agentID) == null ? (int)HttpStatusCode.NotFound : (int)HttpStatusCode.BadGateway
            };
        }

        public static Hashtable NoAgent()
        {
            return new Hashtable()
            {
                ["int_response_code"] = (int)HttpStatusCode.NotFound
            };
        }
    }
}
