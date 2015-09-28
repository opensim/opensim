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
//using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Nini.Config;
using log4net;
using Mono.Addins;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.OptionalModules.ViewerSupport
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DynamicMenu")]
    public class DynamicMenuModule : INonSharedRegionModule, IDynamicMenuModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private class MenuItemData
        {
            public string Title;
            public UUID AgentID;
            public InsertLocation Location;
            public UserMode Mode;
            public CustomMenuHandler Handler;
        }

        private Dictionary<UUID, List<MenuItemData>> m_menuItems =
                new Dictionary<UUID, List<MenuItemData>>();

        private Scene m_scene;

        public string Name
        {
            get { return "DynamicMenuModule"; }
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
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.RegisterModuleInterface<IDynamicMenuModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            ISimulatorFeaturesModule featuresModule = m_scene.RequestModuleInterface<ISimulatorFeaturesModule>();

            if (featuresModule != null)
                featuresModule.OnSimulatorFeaturesRequest += OnSimulatorFeaturesRequest;
        }

        public void RemoveRegion(Scene scene)
        {
        }

        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features)
        {
            OSD menus = new OSDMap();
            if (features.ContainsKey("menus"))
                menus = features["menus"];

            OSDMap agent = new OSDMap();
            OSDMap world = new OSDMap();
            OSDMap tools = new OSDMap();
            OSDMap advanced = new OSDMap();
            OSDMap admin = new OSDMap();
            if (((OSDMap)menus).ContainsKey("agent"))
                agent = (OSDMap)((OSDMap)menus)["agent"];
            if (((OSDMap)menus).ContainsKey("world"))
                world = (OSDMap)((OSDMap)menus)["world"];
            if (((OSDMap)menus).ContainsKey("tools"))
                tools = (OSDMap)((OSDMap)menus)["tools"];
            if (((OSDMap)menus).ContainsKey("advanced"))
                advanced = (OSDMap)((OSDMap)menus)["advanced"];
            if (((OSDMap)menus).ContainsKey("admin"))
                admin = (OSDMap)((OSDMap)menus)["admin"];

            if (m_menuItems.ContainsKey(UUID.Zero))
            {
                foreach (MenuItemData d in m_menuItems[UUID.Zero])
                {
                    if (!m_scene.Permissions.IsGod(agentID))
                    {
                        if (d.Mode == UserMode.RegionManager && (!m_scene.Permissions.IsAdministrator(agentID)))
                            continue;
                    }

                    OSDMap loc = null;
                    switch (d.Location)
                    {
                    case InsertLocation.Agent:
                        loc = agent;
                        break;
                    case InsertLocation.World:
                        loc = world;
                        break;
                    case InsertLocation.Tools:
                        loc = tools;
                        break;
                    case InsertLocation.Advanced:
                        loc = advanced;
                        break;
                    case InsertLocation.Admin:
                        loc = admin;
                        break;
                    }

                    if (loc == null)
                        continue;
                    
                    loc[d.Title] = OSD.FromString(d.Title);
                }
            }

            if (m_menuItems.ContainsKey(agentID))
            {
                foreach (MenuItemData d in m_menuItems[agentID])
                {
                    if (d.Mode == UserMode.God && (!m_scene.Permissions.IsGod(agentID)))
                        continue;

                    OSDMap loc = null;
                    switch (d.Location)
                    {
                    case InsertLocation.Agent:
                        loc = agent;
                        break;
                    case InsertLocation.World:
                        loc = world;
                        break;
                    case InsertLocation.Tools:
                        loc = tools;
                        break;
                    case InsertLocation.Advanced:
                        loc = advanced;
                        break;
                    case InsertLocation.Admin:
                        loc = admin;
                        break;
                    }

                    if (loc == null)
                        continue;
                    
                    loc[d.Title] = OSD.FromString(d.Title);
                }
            }


            ((OSDMap)menus)["agent"] = agent;
            ((OSDMap)menus)["world"] = world;
            ((OSDMap)menus)["tools"] = tools;
            ((OSDMap)menus)["advanced"] = advanced;
            ((OSDMap)menus)["admin"] = admin;

            features["menus"] = menus;
        }

        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            string capUrl = "/CAPS/" + UUID.Random() + "/";

            capUrl = "/CAPS/" + UUID.Random() + "/";
            caps.RegisterHandler("CustomMenuAction", new MenuActionHandler(capUrl, "CustomMenuAction", agentID, this, m_scene));
        }

        internal void HandleMenuSelection(string action, UUID agentID, List<uint> selection)
        {
            if (m_menuItems.ContainsKey(agentID))
            {
                foreach (MenuItemData d in m_menuItems[agentID])
                {
                    if (d.Title == action)
                        d.Handler(action, agentID, selection);
                }
            }

            if (m_menuItems.ContainsKey(UUID.Zero))
            {
                foreach (MenuItemData d in m_menuItems[UUID.Zero])
                {
                    if (d.Title == action)
                        d.Handler(action, agentID, selection);
                }
            }
        }

        public void AddMenuItem(string title, InsertLocation location, UserMode mode, CustomMenuHandler handler)
        {
            AddMenuItem(UUID.Zero, title, location, mode, handler);
        }

        public void AddMenuItem(UUID agentID, string title, InsertLocation location, UserMode mode, CustomMenuHandler handler)
        {
            if (!m_menuItems.ContainsKey(agentID))
                m_menuItems[agentID] = new List<MenuItemData>();

            m_menuItems[agentID].Add(new MenuItemData() { Title = title, AgentID = agentID, Location = location, Mode = mode, Handler = handler });
        }

        public void RemoveMenuItem(string action)
        {
            foreach (KeyValuePair<UUID,List< MenuItemData>> kvp in m_menuItems)
            {
                List<MenuItemData> pendingDeletes = new List<MenuItemData>();
                foreach (MenuItemData d in kvp.Value)
                {
                    if (d.Title == action)
                        pendingDeletes.Add(d);
                }

                foreach (MenuItemData d in pendingDeletes)
                    kvp.Value.Remove(d);
            }
        }
    }

    public class MenuActionHandler : BaseStreamHandler
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private UUID m_agentID;
        private Scene m_scene;
        private DynamicMenuModule m_module;

        public MenuActionHandler(string path, string name, UUID agentID, DynamicMenuModule module, Scene scene)
                :base("POST", path, name, agentID.ToString())
        {
            m_agentID = agentID;
            m_scene = scene;
            m_module = module;
        }

        protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader reader = new StreamReader(request);
            string requestBody = reader.ReadToEnd();

            OSD osd = OSDParser.DeserializeLLSDXml(requestBody);

            string action = ((OSDMap)osd)["action"].AsString();
            OSDArray selection = (OSDArray)((OSDMap)osd)["selection"];
            List<uint> sel = new List<uint>();
            for (int i = 0 ; i < selection.Count ; i++)
                sel.Add(selection[i].AsUInteger());

            Util.FireAndForget(
                x => { m_module.HandleMenuSelection(action, m_agentID, sel); }, null, "DynamicMenuModule.HandleMenuSelection");

            Encoding encoding = Encoding.UTF8;
            return encoding.GetBytes(OSDParser.SerializeLLSDXmlString(new OSD()));
        }
    }
}
