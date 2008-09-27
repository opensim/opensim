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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Scenes;

using LLSD = OpenMetaverse.StructuredData.LLSD;
using LLSDMap = OpenMetaverse.StructuredData.LLSDMap;
using LLSDArray = OpenMetaverse.StructuredData.LLSDArray;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using BlockingLLSDQueue = OpenSim.Framework.BlockingQueue<OpenMetaverse.StructuredData.LLSD>;

namespace OpenSim.Region.Environment.Modules.Framework
{
    public struct QueueItem
    {
        public int id;
        public LLSDMap body;
    }

    public class EventQueueGetModule : IEventQueue, IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene = null;
        private IConfigSource m_gConfig;
        bool enabledYN = false;
        
        private Dictionary<UUID, int> m_ids = new Dictionary<UUID, int>();

        private Dictionary<UUID, BlockingLLSDQueue> queues = new Dictionary<UUID, BlockingLLSDQueue>();
            
        #region IRegionModule methods
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_gConfig = config;



            IConfig startupConfig = m_gConfig.Configs["Startup"];

            ReadConfigAndPopulate(scene, startupConfig, "Startup");

            if (enabledYN)
            {
                m_scene = scene;
                scene.RegisterModuleInterface<IEventQueue>(this);

                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnClientClosed += ClientClosed;
                scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
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
            enabledYN = startupConfig.GetBoolean("EventQueue", false);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EventQueueGetModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
        #endregion

        private BlockingLLSDQueue GetQueue(UUID agentId)
        {
            if (!queues.ContainsKey(agentId))
            {
                m_log.DebugFormat("[EVENTQUEUE]: Adding new queue for agent {0} in region {1}", agentId, m_scene.RegionInfo.RegionName);
                queues[agentId] = new BlockingLLSDQueue();
            }
            return queues[agentId];
        }

        
        #region IEventQueue Members
        public bool Enqueue(LLSD ev, UUID avatarID)
        {
            m_log.DebugFormat("[EVENTQUEUE]: Enqueuing event for {0} in region {1}", avatarID, m_scene.RegionInfo.RegionName);
            BlockingLLSDQueue queue = GetQueue(avatarID);
            queue.Enqueue(ev);
            return true;
        }
        #endregion

        private void OnNewClient(IClientAPI client)
        {
            m_log.DebugFormat("[EVENTQUEUE]: New client {0} detected in region {1}", client.AgentId, m_scene.RegionInfo.RegionName);
            client.OnLogout += ClientClosed;
        }


        private void ClientClosed(IClientAPI client)
        {
            ClientClosed(client.AgentId);
        }

        private void ClientClosed(UUID AgentID)
        {
            queues.Remove(AgentID);
            m_log.DebugFormat("[EVENTQUEUE]: Client {0} deregistered in region {1}.", AgentID, m_scene.RegionInfo.RegionName);
        }
        
        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            m_log.DebugFormat("[EVENTQUEUE]: Avatar {0} entering parcel {1} in region {2}.",
                              avatar.UUID, localLandID, m_scene.RegionInfo.RegionName);
            
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            m_log.DebugFormat("[EVENTQUEUE]: Make Child agent {0}.", avatar.UUID);
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[EVENTQUEUE] OnRegisterCaps: agentID {0} caps {1} region", agentID, caps, m_scene.RegionInfo.RegionName);
            string capsBase = "/CAPS/";
            caps.RegisterHandler("EventQueueGet",
                                 new RestHTTPHandler("POST", capsBase + UUID.Random().ToString(),
                                                       delegate(Hashtable m_dhttpMethod)
                                                       {
                                                           return ProcessQueue(m_dhttpMethod,agentID, caps);
                                                       }));
            Random rnd = new Random(System.Environment.TickCount);
            lock (m_ids)
            {
                if (!m_ids.ContainsKey(agentID))
                    m_ids.Add(agentID, rnd.Next(30000000));
            }

            
            
        }

        public Hashtable ProcessQueue(Hashtable request,UUID agentID, Caps caps)
        {
            // TODO: this has to be redone to not busy-wait (and block the thread),
            // TODO: as soon as we have a non-blocking way to handle HTTP-requests.

            BlockingLLSDQueue queue = GetQueue(agentID);
            LLSD element = queue.Dequeue(15000); // 15s timeout


            String debug = "[EVENTQUEUE]: Got request for agent {0} in region {1}: [  ";
            foreach(object key in request.Keys)
            {
                debug += key.ToString() + "=" + request[key].ToString() + "  ";
            }
            m_log.DebugFormat(debug, agentID, m_scene.RegionInfo.RegionName);
            
            if (element == null) // didn't have an event in 15s
            {
                // Send it a fake event to keep the client polling!   It doesn't like 502s like the proxys say!
                element = EventQueueHelper.KeepAliveEvent();

                ScenePresence avatar;
                m_scene.TryGetAvatar(agentID, out avatar);

                LLSDArray array = new LLSDArray();
                array.Add(element);
                int thisID = m_ids[agentID];
                while (queue.Count() > 0)
                {
                    array.Add(queue.Dequeue(1));
                    thisID++;
                }
                LLSDMap events = new LLSDMap();
                events.Add("events", array);

                events.Add("id", new LLSDInteger(thisID));
                lock (m_ids)
                {
                    m_ids[agentID] = thisID + 1;
                }
                Hashtable responsedata = new Hashtable();
                responsedata["int_response_code"] = 200;
                responsedata["content_type"] = "application/llsd+xml";
                responsedata["keepalive"] = true;
                responsedata["str_response_string"] = LLSDParser.SerializeXmlString(events);
                m_log.DebugFormat("[EVENTQUEUE]: sending fake response for {0} in region{1}: {2}", agentID, m_scene.RegionInfo.RegionName, responsedata["str_response_string"]);

                return responsedata;
            }
            else
            {
                ScenePresence avatar;
                m_scene.TryGetAvatar(agentID, out avatar);
                
                LLSDArray array = new LLSDArray();
                array.Add(element);
                int thisID = m_ids[agentID];
                while (queue.Count() > 0)
                {
                    array.Add(queue.Dequeue(1));
                    thisID++;
                }
                LLSDMap events = new LLSDMap();
                events.Add("events", array);
                
                events.Add("id", new LLSDInteger(thisID)); 
                lock (m_ids)
                {
                    m_ids[agentID] = thisID + 1;
                }
                Hashtable responsedata = new Hashtable();
                responsedata["int_response_code"] = 200;
                responsedata["content_type"] = "application/llsd+xml";
                responsedata["keepalive"] = true;
                responsedata["str_response_string"] = LLSDParser.SerializeXmlString(events);
                m_log.DebugFormat("[EVENTQUEUE]: sending fake response for {0} in region{1}: {2}", agentID, m_scene.RegionInfo.RegionName, responsedata["str_response_string"]);
                                  
                return responsedata;
            }

            /*
            responsedata["int_response_code"] = 200;
            responsedata["content_type"] = "application/xml";
            responsedata["keepalive"] = true;

            responsedata["str_response_string"] = @"<llsd><map><key>events</key><array><map><key>body</key><map><key>AgentData</key><map><key>AgentID</key>
 <uuid>0fd0e798-a54f-40b1-0000-000000000000</uuid><key>SessionID</key><uuid>cc91f1fe-9d52-435d-0000-000000000000
 </uuid></map><key>Info</key><map><key>LookAt</key><array><real>0.9869639873504638671875</real><real>
 -0.1609439998865127563476562</real><real>0</real></array><key>Position</key><array><real>1.43747997283935546875
 </real><real>95.30560302734375</real><real>57.3480987548828125</real></array></map><key>RegionData</key><map>
 <key>RegionHandle</key><binary encoding=" + "\"base64\"" + @">AAPnAAAD8AA=</binary><key>SeedCapability</key><string>
 https://sim7.aditi.lindenlab.com:12043/cap/64015fb3-6fee-9205-0000-000000000000</string><key>SimIP</key><binary
 encoding=" + "\"base64\"" + @">yA8FSA==</binary><key>SimPort</key><integer>13005</integer></map></map><key>message</key>
 <string>CrossedRegion</string></map></array><key>id</key><integer>1</integer></map></llsd>";

             */
            //string requestbody = (string)request["requestbody"];
            //LLSD llsdRequest = LLSDParser.DeserializeXml(request);
            //System.Console.WriteLine(requestbody);
            
        }
    }
}
