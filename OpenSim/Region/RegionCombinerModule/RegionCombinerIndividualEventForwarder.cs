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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Avatar.Gods;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.RegionCombinerModule
{
    public class RegionCombinerIndividualEventForwarder
    {
        private Scene m_rootScene;
        private Scene m_virtScene;

        public RegionCombinerIndividualEventForwarder(Scene rootScene, Scene virtScene)
        {
            m_rootScene = rootScene;
            m_virtScene = virtScene;
        }

        public void ClientConnect(IClientAPI client)
        {
            m_virtScene.UnSubscribeToClientPrimEvents(client);
            m_virtScene.UnSubscribeToClientPrimRezEvents(client);
            m_virtScene.UnSubscribeToClientInventoryEvents(client);
            if(m_virtScene.AttachmentsModule != null)
                ((AttachmentsModule)m_virtScene.AttachmentsModule).UnsubscribeFromClientEvents(client);
            //m_virtScene.UnSubscribeToClientTeleportEvents(client);
            m_virtScene.UnSubscribeToClientScriptEvents(client);
            
            IGodsModule virtGodsModule = m_virtScene.RequestModuleInterface<IGodsModule>();
            if (virtGodsModule != null)
                ((GodsModule)virtGodsModule).UnsubscribeFromClientEvents(client);
            
            m_virtScene.UnSubscribeToClientNetworkEvents(client);

            m_rootScene.SubscribeToClientPrimEvents(client);
            client.OnAddPrim += LocalAddNewPrim;
            client.OnRezObject += LocalRezObject;
            
            m_rootScene.SubscribeToClientInventoryEvents(client);
            if (m_rootScene.AttachmentsModule != null)
                ((AttachmentsModule)m_rootScene.AttachmentsModule).SubscribeToClientEvents(client);
            //m_rootScene.SubscribeToClientTeleportEvents(client);
            m_rootScene.SubscribeToClientScriptEvents(client);
            
            IGodsModule rootGodsModule = m_virtScene.RequestModuleInterface<IGodsModule>();
            if (rootGodsModule != null)
                ((GodsModule)rootGodsModule).UnsubscribeFromClientEvents(client);
            
            m_rootScene.SubscribeToClientNetworkEvents(client);
        }

        public void ClientClosed(UUID clientid, Scene scene)
        {
        }

        /// <summary>
        /// Fixes position based on the region the Rez event came in on
        /// </summary>
        /// <param name="remoteclient"></param>
        /// <param name="itemid"></param>
        /// <param name="rayend"></param>
        /// <param name="raystart"></param>
        /// <param name="raytargetid"></param>
        /// <param name="bypassraycast"></param>
        /// <param name="rayendisintersection"></param>
        /// <param name="rezselected"></param>
        /// <param name="removeitem"></param>
        /// <param name="fromtaskid"></param>
        private void LocalRezObject(IClientAPI remoteclient, UUID itemid, Vector3 rayend, Vector3 raystart, 
            UUID raytargetid, byte bypassraycast, bool rayendisintersection, bool rezselected, bool removeitem, 
            UUID fromtaskid)
        {
            int differenceX = (int)m_virtScene.RegionInfo.RegionLocX - (int)m_rootScene.RegionInfo.RegionLocX;
            int differenceY = (int)m_virtScene.RegionInfo.RegionLocY - (int)m_rootScene.RegionInfo.RegionLocY;
            rayend.X += differenceX * (int)Constants.RegionSize;
            rayend.Y += differenceY * (int)Constants.RegionSize;
            raystart.X += differenceX * (int)Constants.RegionSize;
            raystart.Y += differenceY * (int)Constants.RegionSize;

            m_rootScene.RezObject(remoteclient, itemid, rayend, raystart, raytargetid, bypassraycast,
                                  rayendisintersection, rezselected, removeitem, fromtaskid);
        }
        /// <summary>
        /// Fixes position based on the region the AddPrimShape event came in on
        /// </summary>
        /// <param name="ownerid"></param>
        /// <param name="groupid"></param>
        /// <param name="rayend"></param>
        /// <param name="rot"></param>
        /// <param name="shape"></param>
        /// <param name="bypassraycast"></param>
        /// <param name="raystart"></param>
        /// <param name="raytargetid"></param>
        /// <param name="rayendisintersection"></param>
        private void LocalAddNewPrim(UUID ownerid, UUID groupid, Vector3 rayend, Quaternion rot, 
            PrimitiveBaseShape shape, byte bypassraycast, Vector3 raystart, UUID raytargetid, 
            byte rayendisintersection)
        {
            int differenceX = (int)m_virtScene.RegionInfo.RegionLocX - (int)m_rootScene.RegionInfo.RegionLocX;
            int differenceY = (int)m_virtScene.RegionInfo.RegionLocY - (int)m_rootScene.RegionInfo.RegionLocY;
            rayend.X += differenceX * (int)Constants.RegionSize;
            rayend.Y += differenceY * (int)Constants.RegionSize;
            raystart.X += differenceX * (int)Constants.RegionSize;
            raystart.Y += differenceY * (int)Constants.RegionSize;
            m_rootScene.AddNewPrim(ownerid, groupid, rayend, rot, shape, bypassraycast, raystart, raytargetid,
                                   rayendisintersection);
        }
    }
}