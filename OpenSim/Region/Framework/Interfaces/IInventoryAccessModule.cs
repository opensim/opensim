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

        bool UpdateInventoryItemAsset(UUID ownerID, InventoryItemBase item, AssetBase asset);

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
        /// <param name="asAttachment">
        /// Should be true if the object(s) are begin taken as attachments.  False otherwise.
        /// </param>
        /// <returns>
        /// A list of the items created.  If there was more than one object and objects are not being coaleseced in
        /// inventory, then the order of items is in the same order as the input objects.
        /// </returns>
        List<InventoryItemBase> CopyToInventory(
            DeRezAction action, UUID folderID, List<SceneObjectGroup> objectGroups, IClientAPI remoteClient, bool asAttachment);

        /// <summary>
        /// Rez an object into the scene from the user's inventory
        /// </summary>
        /// <remarks>
        /// FIXME: It would be really nice if inventory access modules didn't also actually do the work of rezzing
        /// things to the scene.  The caller should be doing that, I think.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="rezGroupID"></param>
        /// <param name="RayEnd"></param>
        /// <param name="RayStart"></param>
        /// <param name="RayTargetID"></param>
        /// <param name="BypassRayCast"></param>
        /// <param name="RayEndIsIntersection"></param>
        /// <param name="RezSelected"></param>
        /// <param name="RemoveItem"></param>
        /// <param name="fromTaskID"></param>
        /// <param name="attachment"></param>
        /// <returns>The SceneObjectGroup rezzed or null if rez was unsuccessful.</returns>
        SceneObjectGroup RezObject(
            IClientAPI remoteClient, UUID itemID, UUID rezGroupID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment);
        // compatibily do not use
        SceneObjectGroup RezObject(
            IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment);

        /// <summary>
        /// Rez an object into the scene from the user's inventory
        /// </summary>
        /// <remarks>
        /// FIXME: It would be really nice if inventory access modules didn't also actually do the work of rezzing
        /// things to the scene.  The caller should be doing that, I think.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="item">
        /// The item from which the object asset came.  Can be null, in which case pre and post rez item adjustment and checks are not performed.
        /// </param>
        /// <param name="assetID">The asset id for the object to rez.</param>
        /// <param name="rezObjectID">The requested group id for the object to rez.</param>
        /// <param name="RayEnd"></param>
        /// <param name="RayStart"></param>
        /// <param name="RayTargetID"></param>
        /// <param name="BypassRayCast"></param>
        /// <param name="RayEndIsIntersection"></param>
        /// <param name="RezSelected"></param>
        /// <param name="RemoveItem"></param>
        /// <param name="fromTaskID"></param>
        /// <param name="attachment"></param>
        /// <returns>The SceneObjectGroup rezzed or null if rez was unsuccessful.</returns>

        SceneObjectGroup RezObject(IClientAPI remoteClient, InventoryItemBase item, UUID rezGroupID,
            UUID assetID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment);

        // compatibility do not use
        SceneObjectGroup RezObject(
            IClientAPI remoteClient, InventoryItemBase item,
            UUID assetID, Vector3 RayEnd, Vector3 RayStart,
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
