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
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.RegionCombinerModule
{
public class RegionCombinerClientEventForwarder
    {
        private Scene m_rootScene;
        private Dictionary<UUID, Scene> m_virtScene = new Dictionary<UUID, Scene>();
        private Dictionary<UUID,RegionCombinerIndividualEventForwarder> m_forwarders = new Dictionary<UUID,
            RegionCombinerIndividualEventForwarder>();

        public RegionCombinerClientEventForwarder(RegionConnections rootScene)
        {
            m_rootScene = rootScene.RegionScene;
        }

        public void AddSceneToEventForwarding(Scene virtualScene)
        {
            lock (m_virtScene)
            {
                if (m_virtScene.ContainsKey(virtualScene.RegionInfo.originRegionID))
                {
                    m_virtScene[virtualScene.RegionInfo.originRegionID] = virtualScene;
                }
                else
                {
                    m_virtScene.Add(virtualScene.RegionInfo.originRegionID, virtualScene);
                }
            }
            
            lock (m_forwarders)
            {
                // TODO: Fix this to unregister if this happens
                if (m_forwarders.ContainsKey(virtualScene.RegionInfo.originRegionID))
                    m_forwarders.Remove(virtualScene.RegionInfo.originRegionID);

                RegionCombinerIndividualEventForwarder forwarder =
                    new RegionCombinerIndividualEventForwarder(m_rootScene, virtualScene);
                m_forwarders.Add(virtualScene.RegionInfo.originRegionID, forwarder);

                virtualScene.EventManager.OnNewClient += forwarder.ClientConnect;
                virtualScene.EventManager.OnClientClosed += forwarder.ClientClosed;
            }
        }

        public void RemoveSceneFromEventForwarding (Scene virtualScene)
        {
            lock (m_forwarders)
            {
                RegionCombinerIndividualEventForwarder forwarder = m_forwarders[virtualScene.RegionInfo.originRegionID];
                virtualScene.EventManager.OnNewClient -= forwarder.ClientConnect;
                virtualScene.EventManager.OnClientClosed -= forwarder.ClientClosed;
                m_forwarders.Remove(virtualScene.RegionInfo.originRegionID);
            }
            lock (m_virtScene)
            {
                if (m_virtScene.ContainsKey(virtualScene.RegionInfo.originRegionID))
                {
                    m_virtScene.Remove(virtualScene.RegionInfo.originRegionID);
                }
            }
        }
    }
}