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
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using BlockingLLSDQueue = OpenSim.Framework.BlockingQueue<OpenMetaverse.StructuredData.OSD>;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Framework.EventQueue
{
    public struct QueueItem
    {
        public int id;
        public OSDMap body;
    }

    public class EventQueueGetModule : IEventQueue, IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected Scene m_scene = null;
        private IConfigSource m_gConfig;
        bool enabledYN = false;
        
        private Dictionary<UUID, int> m_ids = new Dictionary<UUID, int>();

        private Dictionary<UUID, Queue<OSD>> queues = new Dictionary<UUID, Queue<OSD>>();
        private Dictionary<UUID, UUID> m_QueueUUIDAvatarMapping = new Dictionary<UUID, UUID>();
        private Dictionary<UUID, UUID> m_AvatarQueueUUIDMapping = new Dictionary<UUID, UUID>();
            
        #region IRegionModule methods
        public virtual void Initialise(Scene scene, IConfigSource config)
        {
            m_gConfig = config;

            IConfig startupConfig = m_gConfig.Configs["Startup"];

            ReadConfigAndPopulate(scene, startupConfig, "Startup");

            if (enabledYN)
            {
                m_scene = scene;
                scene.RegisterModuleInterface<IEventQueue>(this);
                
                // Register fallback handler
                // Why does EQG Fail on region crossings!
                
                //scene.CommsManager.HttpServer.AddLLSDHandler("/CAPS/EQG/", EventQueueFallBack);

                scene.EventManager.OnNewClient += OnNewClient;

                // TODO: Leaving these open, or closing them when we
                // become a child is incorrect. It messes up TP in a big
                // way. CAPS/EQ need to be active as long as the UDP
                // circuit is there.

                scene.EventManager.OnClientClosed += ClientClosed;
                scene.EventManager.OnMakeChildAgent += MakeChildAgent;
                scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            }
            else
            {
                m_gConfig = null;
            }
        
        }

        private void ReadConfigAndPopulate(Scene scene, IConfig startupConfig, string p)
        {
            enabledYN = startupConfig.GetBoolean("EventQueue", true);
        }

        public void PostInitialise()
        {
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "EventQueueGetModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
        #endregion

        /// <summary>
        ///  Always returns a valid queue
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        private Queue<OSD> TryGetQueue(UUID agentId)
        {
            lock (queues)
            {
                if (!queues.ContainsKey(agentId))
                {
                    m_log.DebugFormat(
                        "[EVENTQUEUE]: Adding new queue for agent {0} in region {1}", 
                        agentId, m_scene.RegionInfo.RegionName);
                    
                    queues[agentId] = new Queue<OSD>();
                }
                
                return queues[agentId];
            }
        }

        /// <summary>
        /// May return a null queue
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        private Queue<OSD> GetQueue(UUID agentId)
        {
            lock (queues)
            {
                if (queues.ContainsKey(agentId))
                {
                    return queues[agentId];
                }
                else
                    return null;
            }
        }

        #region IEventQueue Members

        public bool Enqueue(OSD ev, UUID avatarID)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            try
            {
                Queue<OSD> queue = GetQueue(avatarID);
                if (queue != null)
                    queue.Enqueue(ev);
            } 
            catch(NullReferenceException e)
            {
                m_log.Error("[EVENTQUEUE] Caught exception: " + e);
                return false;
            }
            
            return true;
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            //client.OnLogout += ClientClosed;
        }

//        private void ClientClosed(IClientAPI client)
//        {
//            ClientClosed(client.AgentId);
//        }

        private void ClientClosed(UUID AgentID, Scene scene)
        {
            m_log.DebugFormat("[EVENTQUEUE]: Closed client {0} in region {1}", AgentID, m_scene.RegionInfo.RegionName);

            int count = 0;
            while (queues.ContainsKey(AgentID) && queues[AgentID].Count > 0 && count++ < 5)
            {
                Thread.Sleep(1000);
            }

            lock (queues)
            {
                queues.Remove(AgentID);
            }
            List<UUID> removeitems = new List<UUID>();
            lock (m_AvatarQueueUUIDMapping)
            {
                foreach (UUID ky in m_AvatarQueueUUIDMapping.Keys)
                {
                    if (ky == AgentID)
                    {
                        removeitems.Add(ky);
                    }
                }

                foreach (UUID ky in removeitems)
                {
                    m_AvatarQueueUUIDMapping.Remove(ky);
                    MainServer.Instance.RemovePollServiceHTTPHandler("","/CAPS/EQG/" + ky.ToString() + "/");
                }

            }
            UUID searchval = UUID.Zero;

            removeitems.Clear();
            
            lock (m_QueueUUIDAvatarMapping)
            {
                foreach (UUID ky in m_QueueUUIDAvatarMapping.Keys)
                {
                    searchval = m_QueueUUIDAvatarMapping[ky];

                    if (searchval == AgentID)
                    {
                        removeitems.Add(ky);
                    }
                }

                foreach (UUID ky in removeitems)
                    m_QueueUUIDAvatarMapping.Remove(ky);

            }
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            //m_log.DebugFormat("[EVENTQUEUE]: Make Child agent {0} in region {1}.", avatar.UUID, m_scene.RegionInfo.RegionName);
            //lock (m_ids)
           // {
                //if (m_ids.ContainsKey(avatar.UUID))
                //{
                    // close the event queue.
                    //m_ids[avatar.UUID] = -1;
                //}
            //}
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            // Register an event queue for the client
            
            //m_log.DebugFormat(
            //    "[EVENTQUEUE]: OnRegisterCaps: agentID {0} caps {1} region {2}", 
            //    agentID, caps, m_scene.RegionInfo.RegionName);

            // Let's instantiate a Queue for this agent right now
            TryGetQueue(agentID);

            string capsBase = "/CAPS/EQG/";
            UUID EventQueueGetUUID = UUID.Zero;

            lock (m_AvatarQueueUUIDMapping)
            {
                // Reuse open queues.  The client does!
                if (m_AvatarQueueUUIDMapping.ContainsKey(agentID))
                {
                    m_log.DebugFormat("[EVENTQUEUE]: Found Existing UUID!");
                    EventQueueGetUUID = m_AvatarQueueUUIDMapping[agentID];
                }
                else
                {
                    EventQueueGetUUID = UUID.Random();
                    //m_log.DebugFormat("[EVENTQUEUE]: Using random UUID!");
                }
            }

            lock (m_QueueUUIDAvatarMapping)
            {
                if (!m_QueueUUIDAvatarMapping.ContainsKey(EventQueueGetUUID))
                    m_QueueUUIDAvatarMapping.Add(EventQueueGetUUID, agentID);
            }

            lock (m_AvatarQueueUUIDMapping)
            {
                if (!m_AvatarQueueUUIDMapping.ContainsKey(agentID))
                    m_AvatarQueueUUIDMapping.Add(agentID, EventQueueGetUUID);
            }

            // Register this as a caps handler
            caps.RegisterHandler("EventQueueGet",
                                 new RestHTTPHandler("POST", capsBase + EventQueueGetUUID.ToString() + "/",
                                                       delegate(Hashtable m_dhttpMethod)
                                                       {
                                                           return ProcessQueue(m_dhttpMethod, agentID, caps);
                                                       }));
            
            // This will persist this beyond the expiry of the caps handlers
            MainServer.Instance.AddPollServiceHTTPHandler(
                capsBase + EventQueueGetUUID.ToString() + "/", EventQueuePoll, new PollServiceEventArgs(null, HasEvents, GetEvents, NoEvents, agentID));

            Random rnd = new Random(Environment.TickCount);
            lock (m_ids)
            {
                if (!m_ids.ContainsKey(agentID))
                    m_ids.Add(agentID, rnd.Next(30000000));
            }
        }

        public bool HasEvents(UUID requestID, UUID agentID)
        {
            // Don't use this, because of race conditions at agent closing time
            //Queue<OSD> queue = TryGetQueue(agentID);

            Queue<OSD> queue = GetQueue(agentID);
            if (queue != null)
                lock (queue)
                {
                    if (queue.Count > 0)
                        return true;
                    else
                        return false;
                }
            return false;
        }

        public Hashtable GetEvents(UUID requestID, UUID pAgentId, string request)
        {
            Queue<OSD> queue = TryGetQueue(pAgentId);
            OSD element;
            lock (queue)
            {
                if (queue.Count == 0)
                    return NoEvents(requestID, pAgentId);
                element = queue.Dequeue(); // 15s timeout
            }



            int thisID = 0;
            lock (m_ids)
                thisID = m_ids[pAgentId];

            OSDArray array = new OSDArray();
            if (element == null) // didn't have an event in 15s
            {
                // Send it a fake event to keep the client polling!   It doesn't like 502s like the proxys say!
                array.Add(EventQueueHelper.KeepAliveEvent());
                m_log.DebugFormat("[EVENTQUEUE]: adding fake event for {0} in region {1}", pAgentId, m_scene.RegionInfo.RegionName);
            }
            else
            {
                array.Add(element);
                lock (queue)
                {
                    while (queue.Count > 0)
                    {
                        array.Add(queue.Dequeue());
                        thisID++;
                    }
                }
            }

            OSDMap events = new OSDMap();
            events.Add("events", array);

            events.Add("id", new OSDInteger(thisID));
            lock (m_ids)
            {
                m_ids[pAgentId] = thisID + 1;
            }
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 200;
            responsedata["content_type"] = "application/xml";
            responsedata["keepalive"] = false;
            responsedata["reusecontext"] = false;
            responsedata["str_response_string"] = OSDParser.SerializeLLSDXmlString(events);
            return responsedata;
            //m_log.DebugFormat("[EVENTQUEUE]: sending response for {0} in region {1}: {2}", agentID, m_scene.RegionInfo.RegionName, responsedata["str_response_string"]);
        }

        public Hashtable NoEvents(UUID requestID, UUID agentID)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 502;
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = false;
            responsedata["reusecontext"] = false;
            responsedata["str_response_string"] = "Upstream error: ";
            responsedata["error_status_text"] = "Upstream error:";
            responsedata["http_protocol_version"] = "HTTP/1.0";
            return responsedata;
        }

        public Hashtable ProcessQueue(Hashtable request, UUID agentID, Caps caps)
        {
            // TODO: this has to be redone to not busy-wait (and block the thread),
            // TODO: as soon as we have a non-blocking way to handle HTTP-requests.

//            if (m_log.IsDebugEnabled)
//            { 
//                String debug = "[EVENTQUEUE]: Got request for agent {0} in region {1} from thread {2}: [  ";
//                foreach (object key in request.Keys)
//                {
//                    debug += key.ToString() + "=" + request[key].ToString() + "  ";
//                }
//                m_log.DebugFormat(debug + "  ]", agentID, m_scene.RegionInfo.RegionName, System.Threading.Thread.CurrentThread.Name);
//            }

            Queue<OSD> queue = TryGetQueue(agentID);
            OSD element = queue.Dequeue(); // 15s timeout

            Hashtable responsedata = new Hashtable();
            
            int thisID = 0;
            lock (m_ids) 
                thisID = m_ids[agentID];

            if (element == null)
            {
                //m_log.ErrorFormat("[EVENTQUEUE]: Nothing to process in " + m_scene.RegionInfo.RegionName);
                if (thisID == -1) // close-request
                {
                    m_log.ErrorFormat("[EVENTQUEUE]: 404 in " + m_scene.RegionInfo.RegionName);
                    responsedata["int_response_code"] = 404; //501; //410; //404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "Closed EQG";
                    return responsedata;
                }
                responsedata["int_response_code"] = 502;
                responsedata["content_type"] = "text/plain";
                responsedata["keepalive"] = false;
                responsedata["str_response_string"] = "Upstream error: ";
                responsedata["error_status_text"] = "Upstream error:";
                responsedata["http_protocol_version"] = "HTTP/1.0";
                return responsedata;
            }

            OSDArray array = new OSDArray();
            if (element == null) // didn't have an event in 15s
            {
                // Send it a fake event to keep the client polling!   It doesn't like 502s like the proxys say!
                array.Add(EventQueueHelper.KeepAliveEvent());
                m_log.DebugFormat("[EVENTQUEUE]: adding fake event for {0} in region {1}", agentID, m_scene.RegionInfo.RegionName);
            }
            else
            {
                array.Add(element);
                while (queue.Count > 0)
                {
                    array.Add(queue.Dequeue());
                    thisID++;
                }
            }

            OSDMap events = new OSDMap();
            events.Add("events", array);

            events.Add("id", new OSDInteger(thisID));
            lock (m_ids)
            {
                m_ids[agentID] = thisID + 1;
            }

            responsedata["int_response_code"] = 200;
            responsedata["content_type"] = "application/xml";
            responsedata["keepalive"] = false;
            responsedata["str_response_string"] = OSDParser.SerializeLLSDXmlString(events);
            //m_log.DebugFormat("[EVENTQUEUE]: sending response for {0} in region {1}: {2}", agentID, m_scene.RegionInfo.RegionName, responsedata["str_response_string"]);

            return responsedata;
        }

        public Hashtable EventQueuePoll(Hashtable request)
        {
            return new Hashtable();
        }

        public Hashtable EventQueuePath2(Hashtable request)
        {
            string capuuid = (string)request["uri"]; //path.Replace("/CAPS/EQG/","");
            // pull off the last "/" in the path.
            Hashtable responsedata = new Hashtable();
            capuuid = capuuid.Substring(0, capuuid.Length - 1);
            capuuid = capuuid.Replace("/CAPS/EQG/", "");
            UUID AvatarID = UUID.Zero;
            UUID capUUID = UUID.Zero;
            
            // parse the path and search for the avatar with it registered
            if (UUID.TryParse(capuuid, out capUUID))
            {
                lock (m_QueueUUIDAvatarMapping)
                {
                    if (m_QueueUUIDAvatarMapping.ContainsKey(capUUID))
                    {
                        AvatarID = m_QueueUUIDAvatarMapping[capUUID];
                    }
                }
                if (AvatarID != UUID.Zero)
                {
                    return ProcessQueue(request, AvatarID, m_scene.CapsModule.GetCapsHandlerForUser(AvatarID));
                }
                else
                {
                    responsedata["int_response_code"] = 404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "Not Found";
                    responsedata["error_status_text"] = "Not Found";
                    responsedata["http_protocol_version"] = "HTTP/1.0";
                    return responsedata;
                    // return 404
                }
            }
            else
            {
                responsedata["int_response_code"] = 404;
                responsedata["content_type"] = "text/plain";
                responsedata["keepalive"] = false;
                responsedata["str_response_string"] = "Not Found";
                responsedata["error_status_text"] = "Not Found";
                responsedata["http_protocol_version"] = "HTTP/1.0";
                return responsedata;
                // return 404
            }

        }

        public OSD EventQueueFallBack(string path, OSD request, string endpoint)
        {
            // This is a fallback element to keep the client from loosing EventQueueGet
            // Why does CAPS fail sometimes!?
            m_log.Warn("[EVENTQUEUE]: In the Fallback handler!   We lost the Queue in the rest handler!");
            string capuuid = path.Replace("/CAPS/EQG/","");
            capuuid = capuuid.Substring(0, capuuid.Length - 1);

//            UUID AvatarID = UUID.Zero;
            UUID capUUID = UUID.Zero;
            if (UUID.TryParse(capuuid, out capUUID))
            {
/* Don't remove this yet code cleaners!
 * Still testing this!
 * 
                lock (m_QueueUUIDAvatarMapping)
                {
                    if (m_QueueUUIDAvatarMapping.ContainsKey(capUUID))
                    {
                        AvatarID = m_QueueUUIDAvatarMapping[capUUID];
                    }
                }
                
                 
                if (AvatarID != UUID.Zero)
                {
                    // Repair the CAP!
                    //OpenSim.Framework.Capabilities.Caps caps = m_scene.GetCapsHandlerForUser(AvatarID);
                    //string capsBase = "/CAPS/EQG/";
                    //caps.RegisterHandler("EventQueueGet",
                                //new RestHTTPHandler("POST", capsBase + capUUID.ToString() + "/",
                                                      //delegate(Hashtable m_dhttpMethod)
                                                      //{
                                                      //    return ProcessQueue(m_dhttpMethod, AvatarID, caps);
                                                      //}));
                    // start new ID sequence.
                    Random rnd = new Random(System.Environment.TickCount);
                    lock (m_ids)
                    {
                        if (!m_ids.ContainsKey(AvatarID))
                            m_ids.Add(AvatarID, rnd.Next(30000000));
                    }


                    int thisID = 0;
                    lock (m_ids)
                        thisID = m_ids[AvatarID];

                    BlockingLLSDQueue queue = GetQueue(AvatarID);
                    OSDArray array = new OSDArray();
                    LLSD element = queue.Dequeue(15000); // 15s timeout
                    if (element == null)
                    {
                        
                        array.Add(EventQueueHelper.KeepAliveEvent());
                    }
                    else
                    {
                        array.Add(element);
                        while (queue.Count() > 0)
                        {
                            array.Add(queue.Dequeue(1));
                            thisID++;
                        }
                    }
                    OSDMap events = new OSDMap();
                    events.Add("events", array);

                    events.Add("id", new LLSDInteger(thisID));
                    
                    lock (m_ids)
                    {
                        m_ids[AvatarID] = thisID + 1;
                    }
                    
                    return events;
                }
                else
                {
                    return new LLSD();
                }
* 
*/
            }
            else
            {
                //return new LLSD();
            }
            
            return new OSDString("shutdown404!");
        }

        public void DisableSimulator(ulong handle, UUID avatarID)
        {
            OSD item = EventQueueHelper.DisableSimulator(handle);
            Enqueue(item, avatarID);
        }

        public virtual void EnableSimulator(ulong handle, IPEndPoint endPoint, UUID avatarID)
        {
            OSD item = EventQueueHelper.EnableSimulator(handle, endPoint);
            Enqueue(item, avatarID);
        }

        public virtual void EstablishAgentCommunication(UUID avatarID, IPEndPoint endPoint, string capsPath) 
        {
            OSD item = EventQueueHelper.EstablishAgentCommunication(avatarID, endPoint.ToString(), capsPath);
            Enqueue(item, avatarID);
        }

        public virtual void TeleportFinishEvent(ulong regionHandle, byte simAccess, 
                                        IPEndPoint regionExternalEndPoint,
                                        uint locationID, uint flags, string capsURL, 
                                        UUID avatarID)
        {
            OSD item = EventQueueHelper.TeleportFinishEvent(regionHandle, simAccess, regionExternalEndPoint,
                                                            locationID, flags, capsURL, avatarID);
            Enqueue(item, avatarID);
        }

        public virtual void CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                                IPEndPoint newRegionExternalEndPoint,
                                string capsURL, UUID avatarID, UUID sessionID)
        {
            OSD item = EventQueueHelper.CrossRegion(handle, pos, lookAt, newRegionExternalEndPoint,
                                                    capsURL, avatarID, sessionID);
            Enqueue(item, avatarID);
        }

        public void ChatterboxInvitation(UUID sessionID, string sessionName,
                                         UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
                                         uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                         uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            OSD item = EventQueueHelper.ChatterboxInvitation(sessionID, sessionName, fromAgent, message, toAgent, fromName, dialog, 
                                                             timeStamp, offline, parentEstateID, position, ttl, transactionID, 
                                                             fromGroup, binaryBucket);
            Enqueue(item, toAgent);
            //m_log.InfoFormat("########### eq ChatterboxInvitation #############\n{0}", item);

        }

        public void ChatterBoxSessionAgentListUpdates(UUID sessionID, UUID fromAgent, UUID toAgent, bool canVoiceChat, 
                                                      bool isModerator, bool textMute)
        {
            OSD item = EventQueueHelper.ChatterBoxSessionAgentListUpdates(sessionID, fromAgent, canVoiceChat,
                                                                          isModerator, textMute);
            Enqueue(item, toAgent);
            //m_log.InfoFormat("########### eq ChatterBoxSessionAgentListUpdates #############\n{0}", item);
        }

        public void ParcelProperties(ParcelPropertiesPacket parcelPropertiesPacket, UUID avatarID)
        {
            OSD item = EventQueueHelper.ParcelProperties(parcelPropertiesPacket);
            Enqueue(item, avatarID);
        }

        public void GroupMembership(AgentGroupDataUpdatePacket groupUpdate, UUID avatarID)
        {
            OSD item = EventQueueHelper.GroupMembership(groupUpdate);
            Enqueue(item, avatarID);
        }
    }
}
