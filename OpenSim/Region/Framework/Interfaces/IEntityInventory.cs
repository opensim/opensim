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
using System.Collections;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

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
        /// Reset parent object UUID for all the items in the prim's inventory.
        /// </summary>
        /// 
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        /// 
        /// <param name="linkNum">Link number for the part</param>
        void ResetObjectID();

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
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        /// <returns>Number of scripts started.</returns>
        int CreateScriptInstances(int startParam, bool postOnRez, string engine, int stateSource);
        
        ArrayList GetScriptErrors(UUID itemID);
        void ResumeScripts();

        /// <summary>
        /// Stop and remove all the scripts in this entity from the scene.
        /// </summary>
        /// <param name="sceneObjectBeingDeleted">
        /// Should be true if these scripts are being removed because the scene
        /// object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        void RemoveScriptInstances(bool sceneObjectBeingDeleted);

        /// <summary>
        /// Stop all the scripts in this entity.
        /// </summary>
        void StopScriptInstances();

        /// <summary>
        /// Start a script which is in this entity's inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        /// <returns>
        /// true if the script instance was valid for starting, false otherwise.  This does not guarantee
        /// that the script was actually started, just that the script was valid (i.e. its asset data could be found, etc.)
        /// </returns>
        bool CreateScriptInstance(
            TaskInventoryItem item, int startParam, bool postOnRez, string engine, int stateSource);

        /// <summary>
        /// Start a script which is in this entity's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        /// <returns>
        /// true if the script instance was valid for starting, false otherwise.  This does not guarantee
        /// that the script was actually started, just that the script was valid (i.e. its asset data could be found, etc.)
        /// </returns>
        bool CreateScriptInstance(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource);

        ArrayList CreateScriptInstanceEr(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource);

        /// <summary>
        /// Stop and remove a script which is in this prim's inventory from the scene.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="sceneObjectBeingDeleted">
        /// Should be true if these scripts are being removed because the scene
        /// object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        void RemoveScriptInstance(UUID itemId, bool sceneObjectBeingDeleted);

        /// <summary>
        /// Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        void StopScriptInstance(UUID itemId);

        /// <summary>
        /// Try to get the script running status.
        /// </summary>
        /// <returns>
        /// Returns true if a script for the item was found in one of the simulator's script engines.  In this case,
        /// the running parameter will reflect the running status.
        /// Returns false if the item could not be found, if the item is not a script or if a script instance for the
        /// item was not found in any of the script engines.  In this case, running status is irrelevant.
        /// </returns>
        /// <param name='itemId'></param>
        /// <param name='running'></param>
        bool TryGetScriptInstanceRunning(UUID itemId, out bool running);

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
        /// Get all inventory items.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>
        /// If there are no inventory items then an empty list is returned.
        /// </returns>
        List<TaskInventoryItem> GetInventoryItems();

        /// <summary>
        /// Gets an inventory item by name
        /// </summary>
        /// <remarks>
        /// This method returns the first inventory item that matches the given name.  In SL this is all you need
        /// since each item in a prim inventory must have a unique name.
        /// </remarks>
        /// <param name='name'></param>
        /// <returns>
        /// The inventory item.  Null if no such item was found.
        /// </returns>
        TaskInventoryItem GetInventoryItem(string name);

        /// <summary>
        /// Get inventory items by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>
        /// A list of inventory items with that name.
        /// If no inventory item has that name then an empty list is returned.
        /// </returns>
        List<TaskInventoryItem> GetInventoryItems(string name);

        /// <summary>
        /// Get inventory items by type.
        /// </summary>
        /// <param type="name"></param>
        /// <returns>
        /// A list of inventory items of that type.
        /// If no inventory items of that type then an empty list is returned.
        /// </returns>
        List<TaskInventoryItem> GetInventoryItems(InventoryType type);

        /// <summary>
        /// Get the scene object(s) referenced by an inventory item.
        /// </summary>
        /// 
        /// This is returned in a 'rez ready' state.  That is, name, description, permissions and other details have
        /// been adjusted to reflect the part and item from which it originates.
        /// 
        /// <param name="item">Inventory item</param>
        /// <param name="objlist">The scene objects</param>
        /// <param name="veclist">Relative offsets for each object</param>
        /// <returns>true = success, false = the scene object asset couldn't be found</returns>
        bool GetRezReadySceneObjects(TaskInventoryItem item, out List<SceneObjectGroup> objlist, out List<Vector3> veclist, out Vector3 bbox, out float offsetHeight);

        /// <summary>
        /// Update an existing inventory item.
        /// </summary>
        /// <param name="item">The updated item.  An item with the same id must already exist
        /// in this prim's inventory.</param>
        /// <returns>false if the item did not exist, true if the update occurred successfully</returns>
        bool UpdateInventoryItem(TaskInventoryItem item);
        bool UpdateInventoryItem(TaskInventoryItem item, bool fireScriptEvents);
        bool UpdateInventoryItem(TaskInventoryItem item, bool fireScriptEvents, bool considerChanged);

        /// <summary>
        /// Remove an item from this entity's inventory
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>Numeric asset type of the item removed.  Returns -1 if the item did not exist
        /// in this prim's inventory.</returns>
        int RemoveInventoryItem(UUID itemID);

        /// <summary>
        /// Serialize all the metadata for the items in this prim's inventory ready for sending to the client
        /// </summary>
        /// <param name="xferManager"></param>
        void RequestInventoryFile(IClientAPI client, IXfer xferManager);

        /// <summary>
        /// Backup the inventory to the given data store
        /// </summary>
        /// <param name="datastore"></param>
        void ProcessInventoryBackup(ISimulationDataService datastore);

        uint MaskEffectivePermissions();

        void ApplyNextOwnerPermissions();

        void ApplyGodPermissions(uint perms);

        /// <summary>
        /// Number of items in this inventory.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns true if this inventory contains any scripts
        /// </summary></returns>
        bool ContainsScripts();

        /// <summary>
        /// Number of scripts in this inventory.
        /// </summary>
        /// <remarks>
        /// Includes both running and non running scripts.
        /// </remarks>
        int ScriptCount();

        /// <summary>
        /// Number of running scripts in this inventory.
        /// </summary></returns>
        int RunningScriptCount();

        /// <summary>
        /// Get the uuids of all items in this inventory
        /// </summary>
        /// <returns></returns>
        List<UUID> GetInventoryList();
        
        /// <summary>
        /// Get the xml representing the saved states of scripts in this inventory.
        /// </summary>
        /// <returns>
        /// A <see cref="Dictionary`2"/>
        /// </returns>
        Dictionary<UUID, string> GetScriptStates();
        Dictionary<UUID, string> GetScriptStates(bool oldIDs);
    }
}
