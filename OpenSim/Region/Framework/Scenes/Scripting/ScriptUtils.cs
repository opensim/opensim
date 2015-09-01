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

namespace OpenSim.Region.Framework.Scenes.Scripting
{
    /// <summary>
    /// Utility functions for use by scripts manipulating the scene.
    /// </summary>
    public static class ScriptUtils
    {
        /// <summary>
        /// Get an asset id given an item name and an item type.
        /// </summary>
        /// <returns>UUID.Zero if the name and type did not match any item.</returns>
        /// <param name='part'></param>
        /// <param name='name'></param>
        /// <param name='type'></param>
        public static UUID GetAssetIdFromItemName(SceneObjectPart part, string name, int type)
        {
            TaskInventoryItem item = part.Inventory.GetInventoryItem(name);

            if (item != null && item.Type == type)
                return item.AssetID;
            else
                return UUID.Zero;
        }

        /// <summary>
        /// accepts a valid UUID, -or- a name of an inventory item.
        /// Returns a valid UUID or UUID.Zero if key invalid and item not found
        /// in prim inventory.
        /// </summary>
        /// <param name="part">Scene object part to search for inventory item</param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static UUID GetAssetIdFromKeyOrItemName(SceneObjectPart part, string identifier)
        {
            UUID key;

            // if we can parse the string as a key, use it.
            // else try to locate the name in inventory of object. found returns key,
            // not found returns UUID.Zero
            if (!UUID.TryParse(identifier, out key))
            {
                TaskInventoryItem item = part.Inventory.GetInventoryItem(identifier);

                if (item != null)
                    key = item.AssetID;
                else
                    key = UUID.Zero;
            }

            return key;
        }

        /// <summary>
        /// Return the UUID of the asset matching the specified key or name
        /// and asset type.
        /// </summary>
        /// <param name="part">Scene object part to search for inventory item</param>
        /// <param name="identifier"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static UUID GetAssetIdFromKeyOrItemName(SceneObjectPart part, string identifier, AssetType type)
        {
            UUID key;

            if (!UUID.TryParse(identifier, out key))
            {
                TaskInventoryItem item = part.Inventory.GetInventoryItem(identifier);
                if (item != null && item.Type == (int)type)
                    key = item.AssetID;
            }

            return key;
        }
    }
}