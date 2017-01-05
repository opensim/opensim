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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.Gestures
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GesturesModule")]
    public class GesturesModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Scene m_scene;

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;

            m_scene.EventManager.OnNewClient += OnNewClient;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.EventManager.OnNewClient -= OnNewClient;
            m_scene = null;
        }

        public void Close() {}
        public string Name { get { return "Gestures Module"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnActivateGesture += ActivateGesture;
            client.OnDeactivateGesture += DeactivateGesture;
        }

        public virtual void ActivateGesture(IClientAPI client, UUID assetId, UUID gestureId)
        {
            IInventoryService invService = m_scene.InventoryService;

            InventoryItemBase item = invService.GetItem(client.AgentId, gestureId);
            if (item != null)
            {
                item.Flags |= 1;
                invService.UpdateItem(item);
            }
            else
                m_log.WarnFormat(
                    "[GESTURES]: Unable to find gesture {0} to activate for {1}", gestureId, client.Name);
        }

        public virtual void DeactivateGesture(IClientAPI client, UUID gestureId)
        {
            IInventoryService invService = m_scene.InventoryService;

            InventoryItemBase item = invService.GetItem(client.AgentId, gestureId);
            if (item != null)
            {
                item.Flags &= ~(uint)1;
                invService.UpdateItem(item);
            }
            else
                m_log.ErrorFormat(
                    "[GESTURES]: Unable to find gesture to deactivate {0} for {1}", gestureId, client.Name);
        }
    }
}
