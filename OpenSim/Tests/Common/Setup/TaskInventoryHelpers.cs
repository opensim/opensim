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
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Tests.Common.Setup
{
    /// <summary>
    /// Utility functions for carrying out task inventory tests.
    /// </summary>
    ///
    public static class TaskInventoryHelpers
    {
        /// <summary>
        /// Add a notecard item to the given part.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="part"></param>
        /// <returns>The item that was added</returns>
        public static TaskInventoryItem AddNotecard(Scene scene, SceneObjectPart part)
        {
            AssetNotecard nc = new AssetNotecard();
            nc.BodyText = "Hello World!";
            nc.Encode();
            UUID ncAssetUuid = new UUID("00000000-0000-0000-1000-000000000000");
            UUID ncItemUuid = new UUID("00000000-0000-0000-1100-000000000000");
            AssetBase ncAsset
                = AssetHelpers.CreateAsset(ncAssetUuid, AssetType.Notecard, nc.AssetData, UUID.Zero);
            scene.AssetService.Store(ncAsset);
            TaskInventoryItem ncItem 
                = new TaskInventoryItem 
                    { Name = "ncItem", AssetID = ncAssetUuid, ItemID = ncItemUuid, 
                      Type = (int)AssetType.Notecard, InvType = (int)InventoryType.Notecard };
            part.Inventory.AddInventoryItem(ncItem, true); 
            
            return ncItem;
        }

        /// <summary>
        /// Add a scene object item to the given part.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="sop"></param>
        /// <param name="itemName"></param>
        /// <param name="id"></param>
        public static TaskInventoryItem AddSceneObjectItem(Scene scene, SceneObjectPart sop, string itemName, UUID id)
        {
            SceneObjectGroup taskSceneObject = SceneSetupHelpers.CreateSceneObject(1, UUID.Zero);
            AssetBase taskSceneObjectAsset = AssetHelpers.CreateAsset(0x10, taskSceneObject);
            scene.AssetService.Store(taskSceneObjectAsset);
            TaskInventoryItem taskSceneObjectItem
                = new TaskInventoryItem
                    { Name = itemName, AssetID = taskSceneObjectAsset.FullID, ItemID = id,
                      Type = (int)AssetType.Object, InvType = (int)InventoryType.Object };
            sop.Inventory.AddInventoryItem(taskSceneObjectItem, true);

            return taskSceneObjectItem;
        }
    }
}