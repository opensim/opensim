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
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim;
using OpenSim.Region;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Nini.Config;
using log4net;
using Mono.Addins;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.OptionalModules.ViewerSupport
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DynamicFloater")]
    public class DynamicFloaterModule : INonSharedRegionModule, IDynamicFloaterModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        private Dictionary<UUID, Dictionary<int, FloaterData>> m_floaters = new Dictionary<UUID, Dictionary<int, FloaterData>>();

        public string Name
        {
            get { return "DynamicFloaterModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource config)
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            m_scene.RegisterModuleInterface<IDynamicFloaterModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnChatFromClient += OnChatFromClient;
        }

        private void OnClientClosed(UUID agentID, Scene scene)
        {
            m_floaters.Remove(agentID);
        }

        private void SendToClient(ScenePresence sp, string msg)
        {
            sp.ControllingClient.SendChatMessage(msg,
                    (byte)ChatTypeEnum.Owner,
                    sp.AbsolutePosition,
                    "Server",
                    UUID.Zero,
                    UUID.Zero,
                    (byte)ChatSourceType.Object,
                    (byte)ChatAudibleLevel.Fully);
        }

        public void DoUserFloater(UUID agentID, FloaterData dialogData, string configuration)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);
            if (sp == null || sp.IsChildAgent)
                return;

            if (!m_floaters.ContainsKey(agentID))
                m_floaters[agentID] = new Dictionary<int, FloaterData>();

            if (m_floaters[agentID].ContainsKey(dialogData.Channel))
                return;

            m_floaters[agentID].Add(dialogData.Channel, dialogData);

            string xml;
            if (dialogData.XmlText != null && dialogData.XmlText != String.Empty)
            {
                xml = dialogData.XmlText;
            }
            else
            {
                using (FileStream fs = File.Open(dialogData.XmlName + ".xml", FileMode.Open))
                {
                    using (StreamReader sr = new StreamReader(fs))
                        xml = sr.ReadToEnd().Replace("\n", "");
                }
            }

            List<string> xparts = new List<string>();

            while (xml.Length > 0)
            {
                string x = xml;
                if (x.Length > 600)
                {
                    x = x.Substring(0, 600);
                    xml = xml.Substring(600);
                }
                else
                {
                    xml = String.Empty;
                }

                xparts.Add(x);
            }

            for (int i = 0 ; i < xparts.Count ; i++)
                SendToClient(sp, String.Format("># floater {2} create {0}/{1} " + xparts[i], i + 1, xparts.Count, dialogData.FloaterName));

            SendToClient(sp, String.Format("># floater {0} {{notify:1}} {{channel: {1}}} {{node:cancel {{notify:1}}}} {{node:ok {{notify:1}}}} {2}", dialogData.FloaterName, (uint)dialogData.Channel, configuration));
        }

        private void OnChatFromClient(object sender, OSChatMessage msg)
        {
            if (msg.Sender == null)
                return;

            //m_log.DebugFormat("chan {0} msg {1}", msg.Channel, msg.Message);

            IClientAPI client = msg.Sender;

            if (!m_floaters.ContainsKey(client.AgentId))
                return;

            string[] parts = msg.Message.Split(new char[] {':'});
            if (parts.Length == 0)
                return;

            ScenePresence sp = m_scene.GetScenePresence(client.AgentId);
            if (sp == null || sp.IsChildAgent)
                return;

            Dictionary<int, FloaterData> d = m_floaters[client.AgentId];

            // Work around a viewer bug - VALUE from any
            // dialog can appear on this channel and needs to
            // be dispatched to ALL open dialogs for the user
            if (msg.Channel == 427169570)
            {
                if (parts[0] == "VALUE")
                {
                    foreach (FloaterData dd in d.Values)
                    {
                        if(dd.Handler(client, dd, parts))
                        {
                            m_floaters[client.AgentId].Remove(dd.Channel);
                            SendToClient(sp, String.Format("># floater {0} destroy", dd.FloaterName));
                            break;
                        }
                    }
                }
                return;
            }

            if (!d.ContainsKey(msg.Channel))
                return;

            FloaterData data = d[msg.Channel];

            if (parts[0] == "NOTIFY")
            {
                if (parts[1] == "cancel" || parts[1] == data.FloaterName)
                {
                    m_floaters[client.AgentId].Remove(data.Channel);
                    SendToClient(sp, String.Format("># floater {0} destroy", data.FloaterName));
                }
            }

            if (data.Handler != null && data.Handler(client, data, parts))
            {
                m_floaters[client.AgentId].Remove(data.Channel);
                SendToClient(sp, String.Format("># floater {0} destroy", data.FloaterName));
            }
        }

        public void FloaterControl(ScenePresence sp, FloaterData d, string msg)
        {
            string sendData = String.Format("># floater {0} {1}", d.FloaterName, msg);
            SendToClient(sp, sendData);

        }
    }
}
