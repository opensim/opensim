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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// Interface to an entity's (SceneObjectPart's) inventory
    /// </summary>
    /// 
    /// This is not a finished 1.0 candidate interface 
    public interface IEntityInventory
    {
        /// <summary>
        /// Force the task inventory of this prim to persist at the next update sweep
        /// </summary>
        void ForceInventoryPersistence();

        /// <summary>
        /// Reset UUIDs for all the items in the prim's inventory.
        /// </summary>
        /// 
        /// This involves either generating
        /// new ones or setting existing UUIDs to the correct parent UUIDs.
        ///
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        /// 
        /// <param name="linkNum">Link number for the part</param>
        void ResetInventoryIDs();

        /// <summary>
        /// Change every item in this inventory to a new owner.
        /// </summary>
        /// <param name="ownerId"></param>
        void ChangeInventoryOwner(UUID ownerId);

        /// <summary>
        /// Change every item in this inventory to a new group.
        /// </summary>
        /// <param name="groupID"></param>
        void ChangeInventoryGroup(UUID groupID);

        /// <summary>
        /// Start all the scripts contained in this entity's inventory
        /// </summary>
       void CreateScriptInstances(int startParam, bool postOnRez, string engine, int stateSource);

        /// <summary>
        /// Stop all the scripts in this entity.
        /// </summary>
        void RemoveScriptInstances();

        /// <summary>
        /// Start a script which is in this entity's inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        void CreateScriptInstance(
            TaskInventoryItem item, int startParam, bool postOnRez, string engine, int stateSource);

        /// <summary>
        /// Start a script which is in this entity's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        void CreateScriptInstance(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource);

        /// <summary>
        /// Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        void RemoveScriptInstance(UUID itemId);

        /// <summary>
        /// Add an item to this entity's inventory.  If an item with the same name already exists, then an alternative
        /// name is chosen.
        /// </summary>
        /// <param name="item"></param>
        void AddInventoryItem(TaskInventoryItem item, bool allowedDrop);

        /// <summary>
        /// Add an item to this entity's inventory.  If an item with the same name already exists, it is replaced.
        /// </summary>
        /// <param name="item"></param>
        void AddInventoryItemExclusive(TaskInventoryItem item, bool allowedDrop);

        /// <summary>
        /// Restore a whole collection of items to the entity's inventory at once.
        /// We assume that the items already have all their fields correctly filled out.
        /// The items are not flagged for persistence to the database, since they are being restored
        /// from persistence rather than being newly added.
        /// </summary>
        /// <param name="items"></param>
        void RestoreInventoryItems(ICollection<TaskInventoryItem> items);

        /// <summary>
        /// Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        TaskInventoryItem GetInventoryItem(UUID itemId);

        /// <summary>
        /// Update an existing inventory item.
        /// </summary>
        /// <param name="item">The updated item.  An item with the same id must already exist
        /// in this prim's inventory.</param>
        /// <returns>false if the item did not exist, true if the update occurred successfully</returns>
        bool UpdateInventoryItem(TaskInventoryItem item);

        /// <summary>
        /// Remove an item from this entity's inventory
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>Numeric asset type of the item removed.  Returns -1 if the item did not exist
        /// in this prim's inventory.</returns>
        int RemoveInventoryItem(UUID itemID);

        /// <summary>
        /// Return the name with which a client can request a xfer of this prim's inventory metadata
        /// </summary>
        string GetInventoryFileName();

        bool GetInventoryFileName(IClientAPI client, uint localID);

        /// <summary>
        /// Serialize all the metadata for the items in this prim's inventory ready for sending to the client
        /// </summary>
        /// <param name="xferManager"></param>
        void RequestInventoryFile(IClientAPI client, IXfer xferManager);

        /// <summary>
        /// Backup the inventory to the given data store
        /// </summary>
        /// <param name="datastore"></param>
        void ProcessInventoryBackup(IRegionDataStore datastore);

        uint MaskEffectivePermissions();

        void ApplyNextOwnerPermissions();

        void ApplyGodPermissions(uint perms);

        /// <summary>
        /// Returns true if this inventory contains any scripts
        /// </summary></returns>
        bool ContainsScripts();

        /// <summary>
        /// Get the uuids of all items in this inventory
        /// </summary>
        /// <returns></returns>
        List<UUID> GetInventoryList();
        
        /// <summary>
        /// Get the names of the assemblies associated with scripts in this inventory.
        /// </summary>
        /// <returns></returns>
        string[] GetScriptAssemblies();
        
        /// <summary>
        /// Get the xml representing the saved states of scripts in this inventory.
        /// </summary>
        /// <returns>
        /// A <see cref="Dictionary`2"/>
        /// </returns>
        Dictionary<UUID, string> GetScriptStates();

        bool CanBeDeleted();
    }
}
