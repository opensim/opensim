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

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IInventoryAccessModule
    {
        UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data);
        
        /// <summary>
        /// Copy objects to a user's inventory.
        /// </summary>
        /// <remarks>
        /// Is it left to the caller to delete them from the scene if required.
        /// </remarks>
        /// <param name="action"></param>
        /// <param name="folderID"></param>
        /// <param name="objectGroups"></param>
        /// <param name="remoteClient"></param>
        /// <returns>
        /// Returns the UUID of the newly created item asset (not the item itself).
        /// FIXME: This is not very useful.  It would be far more useful to return a list of items instead.
        /// </returns>
        UUID CopyToInventory(DeRezAction action, UUID folderID, List<SceneObjectGroup> objectGroups, IClientAPI remoteClient);
        
        SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment);
        void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver);

        /// <summary>
        /// Does the client have sufficient permissions to retrieve the inventory item?
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="requestID"></param>
        /// <returns></returns>
        bool CanGetAgentInventoryItem(IClientAPI remoteClient, UUID itemID, UUID requestID);

        // Must be here because of textures in user's inventory
        bool IsForeignUser(UUID userID, out string assetServerURL);
    }
}
