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
using OpenSim.Capabilities.Handlers;
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
    public class FetchInventory2Module : INonSharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool Enabled { get; private set; }

        private Scene m_scene;

        private IInventoryService m_inventoryService;

        private string m_fetchInventory2Url;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_fetchInventory2Url = config.GetString("Cap_FetchInventory2", string.Empty);

            if (m_fetchInventory2Url != string.Empty)
                Enabled = true;
        }

        public void AddRegion(Scene s)
        {
            if (!Enabled)
                return;

            m_scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            if (!Enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (!Enabled)
                return;

            m_inventoryService = m_scene.InventoryService;

            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
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
            RegisterFetchCap(agentID, caps, "FetchInventory2", m_fetchInventory2Url);
        }

        private void RegisterFetchCap(UUID agentID, Caps caps, string capName, string url)
        {
            string capUrl;

            if (url == "localhost")
            {
                capUrl = "/CAPS/" + UUID.Random();

                FetchInventory2Handler fetchHandler = new FetchInventory2Handler(m_inventoryService, agentID);

                IRequestHandler reqHandler
                    = new RestStreamHandler(
                        "POST", capUrl, fetchHandler.FetchInventoryRequest, capName, agentID.ToString());

                caps.RegisterHandler(capName, reqHandler);
            }
            else
            {
                capUrl = url;

                caps.RegisterHandler(capName, capUrl);
            }

//            m_log.DebugFormat(
//                "[FETCH INVENTORY2 MODULE]: Registered capability {0} at {1} in region {2} for {3}",
//                capName, capUrl, m_scene.RegionInfo.RegionName, agentID);
        }
    }
}
