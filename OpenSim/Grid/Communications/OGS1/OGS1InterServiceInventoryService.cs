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
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Grid.Communications.OGS1
{
    /// <summary>
    /// OGS1 implementation of the inter-service inventory service
    /// </summary>
    public class OGS1InterServiceInventoryService : IInterServiceInventoryServices
    {
        protected Uri m_inventoryServerUrl;

        public OGS1InterServiceInventoryService(Uri inventoryServerUrl)
        {
            m_inventoryServerUrl = inventoryServerUrl;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool CreateNewUserInventory(UUID userId)
        {
            return SynchronousRestObjectPoster.BeginPostObject<Guid, bool>(
                "POST", m_inventoryServerUrl + "CreateInventory/", userId.Guid);
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return SynchronousRestObjectPoster.BeginPostObject<Guid, List<InventoryFolderBase>>(
                "POST", m_inventoryServerUrl + "RootFolders/", userId.Guid);
        }
        
        /// <summary>
        /// Returns a list of all the active gestures in a user's inventory.
        /// </summary>
        /// <param name="userId">
        /// The <see cref="UUID"/> of the user
        /// </param>
        /// <returns>
        /// A flat list of the gesture items.
        /// </returns>
        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return SynchronousRestObjectPoster.BeginPostObject<Guid, List<InventoryItemBase>>(
                "POST", m_inventoryServerUrl + "ActiveGestures/", userId.Guid);
        }

    }
}
