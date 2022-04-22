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

using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using System;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    /// <summary>
    /// This module implements both WebFetchInventoryDescendents and FetchInventoryDescendents2 capabilities.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FetchInventory2Module")]
    public class FetchInventory2Module : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool Enabled { get; private set; }

        private int m_nScenes;

        private IInventoryService m_inventoryService = null;
        private ILibraryService m_LibraryService = null;
        private string m_fetchInventory2Url;

        private ExpiringKey<UUID> m_badRequests;

        private string m_fetchLib2Url;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_fetchInventory2Url = config.GetString("Cap_FetchInventory2", string.Empty);
            m_fetchLib2Url = config.GetString("Cap_FetchLib2", "localhost");

            if (m_fetchInventory2Url.Length > 0)
                Enabled = true;
        }

        public void AddRegion(Scene s)
        {
        }

        public void RemoveRegion(Scene s)
        {
            if (!Enabled)
                return;

            s.EventManager.OnRegisterCaps -= RegisterCaps;
            --m_nScenes;
            if(m_nScenes <= 0)
            {
                m_inventoryService = null;
                m_LibraryService = null;
                m_badRequests.Dispose();
                m_badRequests = null;
            }
        }

        public void RegionLoaded(Scene s)
        {
            if (!Enabled)
                return;

            if (m_inventoryService == null)
                m_inventoryService = s.InventoryService;
            if(m_LibraryService == null)
                m_LibraryService = s.LibraryService;

            if(m_badRequests == null)
                m_badRequests = new ExpiringKey<UUID>(30000);

            if (m_inventoryService != null)
            {
                s.EventManager.OnRegisterCaps += RegisterCaps;
                ++m_nScenes;
            }
        }

        public void PostInitialise() {}

        public void Close() {}

        public string Name { get { return "FetchInventory2Module"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private void RegisterCaps(UUID agentID, Caps caps)
        {
            if (m_fetchInventory2Url == "localhost")
                RegisterFetchCap(agentID, caps, m_fetchInventory2Url);
            if (m_fetchLib2Url == "localhost")
                RegisterFetchLibCap(agentID, caps, "FetchLib2", m_fetchLib2Url);
        }

        private void RegisterFetchCap(UUID agentID, Caps caps, string url)
        {
            if (url == "localhost")
            {
                FetchInventory2Handler fetchHandler = new FetchInventory2Handler(m_inventoryService, agentID);
                caps.RegisterSimpleHandler("FetchInventory2",
                    new SimpleOSDMapHandler("POST", "/" + UUID.Random(), delegate (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
                    {
                        fetchHandler.FetchInventorySimpleRequest(httpRequest, httpResponse, map, m_badRequests);
                    }
                 ));
            }
            else
            {
                caps.RegisterHandler("FetchInventory2", url);
            }

            //m_log.DebugFormat(
            //    "[FETCH INVENTORY2 MODULE]: Registered capability FetchInventory2 at {0} in region {1} for {2}",
            //    capUrl, m_scene.RegionInfo.RegionName, agentID);
        }

        private void RegisterFetchLibCap(UUID agentID, Caps caps, string capName, string url)
        {
            if (url == "localhost")
            {
                FetchLib2Handler fetchHandler = new FetchLib2Handler(m_inventoryService, m_LibraryService, agentID);
                caps.RegisterSimpleHandler("FetchLib2",
                    new SimpleOSDMapHandler("POST", "/" + UUID.Random(), delegate (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
                    {
                        fetchHandler.FetchLibSimpleRequest(httpRequest, httpResponse, map, m_badRequests);
                    }
                 ));
            }
            else
            {
                caps.RegisterHandler("FetchLib2", url);
            }
            //m_log.DebugFormat(
            //    "[FETCH INVENTORY2 MODULE]: Registered capability FetchLib2 at {0} in region {1} for {2}",
            //    capUrl, m_scene.RegionInfo.RegionName, agentID);
        }
    }
}
