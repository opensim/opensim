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
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Scripting;

namespace OpenSim.Region.Framework.Scenes
{
    public class SceneObjectPartInventory : IEntityInventory
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_inventoryFileName = String.Empty;
        private int m_inventoryFileNameSerial = 0;
        
        /// <value>
        /// The part to which the inventory belongs.
        /// </value>
        private SceneObjectPart m_part;

        /// <summary>
        /// Serial count for inventory file , used to tell if inventory has changed
        /// no need for this to be part of Database backup
        /// </summary>
        protected uint m_inventorySerial = 0;

        /// <summary>
        /// Holds in memory prim inventory
        /// </summary>
        protected TaskInventoryDictionary m_items = new TaskInventoryDictionary();

        /// <summary>
        /// Tracks whether inventory has changed since the last persistent backup
        /// </summary>
        internal bool HasInventoryChanged;
        
        /// <value>
        /// Inventory serial number
        /// </value>
        protected internal uint Serial
        {
            get { return m_inventorySerial; }
            set { m_inventorySerial = value; }
        }

        /// <value>
        /// Raw inventory data
        /// </value>
        protected internal TaskInventoryDictionary Items
        {
            get { return m_items; }
            set
            {
                m_items = value;
                m_inventorySerial++;
            }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="part">
        /// A <see cref="SceneObjectPart"/>
        /// </param>
        public SceneObjectPartInventory(SceneObjectPart part)
        {
            m_part = part;
        }

        /// <summary>
        /// Force the task inventory of this prim to persist at the next update sweep
        /// </summary>
        public void ForceInventoryPersistence()
        {
            HasInventoryChanged = true;
        }

        /// <summary>
        /// Reset UUIDs for all the items in the prim's inventory.  This involves either generating
        /// new ones or setting existing UUIDs to the correct parent UUIDs.
        ///
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        /// </summary>
        /// <param name="linkNum">Link number for the part</param>
        public void ResetInventoryIDs()
        {
            lock (Items)
            {
                if (0 == Items.Count)
                    return;

                HasInventoryChanged = true;
                m_part.ParentGroup.HasGroupChanged = true;
                IList<TaskInventoryItem> items = new List<TaskInventoryItem>(Items.Values);
                Items.Clear();

                foreach (TaskInventoryItem item in items)
                {
                    item.ResetIDs(m_part.UUID);
                    Items.Add(item.ItemID, item);
                }
            }
        }

        /// <summary>
        /// Change every item in this inventory to a new owner.
        /// </summary>
        /// <param name="ownerId"></param>
        public void ChangeInventoryOwner(UUID ownerId)
        {
            lock (Items)
            {
                if (0 == Items.Count)
                {
                    return;
                }

                HasInventoryChanged = true;
                m_part.ParentGroup.HasGroupChanged = true;
                IList<TaskInventoryItem> items = new List<TaskInventoryItem>(Items.Values);
                foreach (TaskInventoryItem item in items)
                {
                    if (ownerId != item.OwnerID)
                    {
                        item.LastOwnerID = item.OwnerID;
                        item.OwnerID = ownerId;
                    }
                }
            }
        }

        /// <summary>
        /// Change every item in this inventory to a new group.
        /// </summary>
        /// <param name="groupID"></param>
        public void ChangeInventoryGroup(UUID groupID)
        {
            lock (Items)
            {
                if (0 == Items.Count)
                {
                    return;
                }

                HasInventoryChanged = true;
                m_part.ParentGroup.HasGroupChanged = true;
                IList<TaskInventoryItem> items = new List<TaskInventoryItem>(Items.Values);
                foreach (TaskInventoryItem item in items)
                {
                    if (groupID != item.GroupID)
                    {
                        item.GroupID = groupID;
                    }
                }
            }
        }

        /// <summary>
        /// Start all the scripts contained in this prim's inventory
        /// </summary>
        public void CreateScriptInstances(int startParam, bool postOnRez, string engine, int stateSource)
        {
            lock (m_items)
            {
                foreach (TaskInventoryItem item in Items.Values)
                {
                    if ((int)InventoryType.LSL == item.InvType)
                    {
                        CreateScriptInstance(item, startParam, postOnRez, engine, stateSource);
                    }
                }
            }
        }

        /// <summary>
        /// Stop all the scripts in this prim.
        /// </summary>
        public void RemoveScriptInstances()
        {
            lock (Items)
            {
                foreach (TaskInventoryItem item in Items.Values)
                {
                    if ((int)InventoryType.LSL == item.InvType)
                    {
                        RemoveScriptInstance(item.ItemID);
                        m_part.RemoveScriptEvents(item.ItemID);
                    }
                }
            }
        }

        /// <summary>
        /// Start a script which is in this prim's inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public void CreateScriptInstance(TaskInventoryItem item, int startParam, bool postOnRez, string engine, int stateSource)
        {
            // m_log.InfoFormat(
            //     "[PRIM INVENTORY]: " +
            //     "Starting script {0}, {1} in prim {2}, {3}",
            //     item.Name, item.ItemID, Name, UUID);

            if (!m_part.ParentGroup.Scene.Permissions.CanRunScript(item.ItemID, m_part.UUID, item.OwnerID))
                return;

            m_part.AddFlag(PrimFlags.Scripted);

            if (!m_part.ParentGroup.Scene.RegionInfo.RegionSettings.DisableScripts)
            {
                if (stateSource == 1 && // Prim crossing
                        m_part.ParentGroup.Scene.m_trustBinaries)
                {
                    m_items[item.ItemID].PermsMask = 0;
                    m_items[item.ItemID].PermsGranter = UUID.Zero;
                    m_part.ParentGroup.Scene.EventManager.TriggerRezScript(
                        m_part.LocalId, item.ItemID, String.Empty, startParam, postOnRez, engine, stateSource);
                    m_part.ParentGroup.AddActiveScriptCount(1);
                    m_part.ScheduleFullUpdate();
                    return;
                }

                m_part.ParentGroup.Scene.AssetService.Get(item.AssetID.ToString(), this, delegate(string id, object sender, AssetBase asset)
                               {
                                   if (null == asset)
                                   {
                                       m_log.ErrorFormat(
                                           "[PRIM INVENTORY]: " +
                                           "Couldn't start script {0}, {1} since asset ID {2} could not be found",
                                           item.Name, item.ItemID, item.AssetID);
                                   }
                                   else
                                   {
                                       if (m_part.ParentGroup.m_savedScriptState != null)
                                           RestoreSavedScriptState(item.OldItemID, item.ItemID);
                                       m_items[item.ItemID].PermsMask = 0;
                                       m_items[item.ItemID].PermsGranter = UUID.Zero;
                                       string script = Utils.BytesToString(asset.Data);
                                       m_part.ParentGroup.Scene.EventManager.TriggerRezScript(
                                            m_part.LocalId, item.ItemID, script, startParam, postOnRez, engine, stateSource);
                                       m_part.ParentGroup.AddActiveScriptCount(1);
                                       m_part.ScheduleFullUpdate();
                                   }
                               });
            }
        }

        static System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();

        private void RestoreSavedScriptState(UUID oldID, UUID newID)
        {
            if (m_part.ParentGroup.m_savedScriptState.ContainsKey(oldID))
            {
                string fpath = Path.Combine("ScriptEngines/"+m_part.ParentGroup.Scene.RegionInfo.RegionID.ToString(),
                                    newID.ToString()+".state");
                FileStream fs = File.Create(fpath);
                Byte[] buffer = enc.GetBytes(m_part.ParentGroup.m_savedScriptState[oldID]);
                fs.Write(buffer,0,buffer.Length);
                fs.Close();
                m_part.ParentGroup.m_savedScriptState.Remove(oldID);
            }
        }

        /// <summary>
        /// Start a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId">
        /// A <see cref="UUID"/>
        /// </param>
        public void CreateScriptInstance(UUID itemId, int startParam, bool postOnRez, string engine, int stateSource)
        {
            lock (m_items)
            {
                if (m_items.ContainsKey(itemId))
                {
                    CreateScriptInstance(m_items[itemId], startParam, postOnRez, engine, stateSource);
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Couldn't start script with ID {0} since it couldn't be found for prim {1}, {2}",
                        itemId, m_part.Name, m_part.UUID);
                }
            }
        }

        /// <summary>
        /// Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        public void RemoveScriptInstance(UUID itemId)
        {
            if (m_items.ContainsKey(itemId))
            {
                m_part.ParentGroup.Scene.EventManager.TriggerRemoveScript(m_part.LocalId, itemId);
                m_part.ParentGroup.AddActiveScriptCount(-1);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't stop script with ID {0} since it couldn't be found for prim {1}, {2}",
                    itemId, m_part.Name, m_part.UUID);
            }
        }

        /// <summary>
        /// Check if the inventory holds an item with a given name.
        /// This method assumes that the task inventory is already locked.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool InventoryContainsName(string name)
        {
            foreach (TaskInventoryItem item in Items.Values)
            {
                if (item.Name == name)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// For a given item name, return that name if it is available.  Otherwise, return the next available
        /// similar name (which is currently the original name with the next available numeric suffix).
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string FindAvailableInventoryName(string name)
        {
            if (!InventoryContainsName(name))
                return name;

            int suffix=1;
            while (suffix < 256)
            {
                string tryName=String.Format("{0} {1}", name, suffix);
                if (!InventoryContainsName(tryName))
                    return tryName;
                suffix++;
            }
            return String.Empty;
        }

        /// <summary>
        /// Add an item to this prim's inventory.  If an item with the same name already exists, then an alternative
        /// name is chosen.
        /// </summary>
        /// <param name="item"></param>
        public void AddInventoryItem(TaskInventoryItem item, bool allowedDrop)
        {
            AddInventoryItem(item.Name, item, allowedDrop);
        }

        /// <summary>
        /// Add an item to this prim's inventory.  If an item with the same name already exists, it is replaced.
        /// </summary>
        /// <param name="item"></param>
        public void AddInventoryItemExclusive(TaskInventoryItem item, bool allowedDrop)
        {
            List<TaskInventoryItem> il = new List<TaskInventoryItem>(m_items.Values);
            foreach (TaskInventoryItem i in il)
            {
                if (i.Name == item.Name)
                {
                    if (i.InvType == (int)InventoryType.LSL)
                        RemoveScriptInstance(i.ItemID);

                    RemoveInventoryItem(i.ItemID);
                    break;
                }
            }

            AddInventoryItem(item.Name, item, allowedDrop);
        }

        /// <summary>
        /// Add an item to this prim's inventory.
        /// </summary>
        /// <param name="name">The name that the new item should have.</param>
        /// <param name="item">
        /// The item itself.  The name within this structure is ignored in favour of the name
        /// given in this method's arguments
        /// </param>
        /// <param name="allowedDrop">
        /// Item was only added to inventory because AllowedDrop is set
        /// </param>
        protected void AddInventoryItem(string name, TaskInventoryItem item, bool allowedDrop)
        {
            name = FindAvailableInventoryName(name);
            if (name == String.Empty)
                return;

            item.ParentID = m_part.UUID;
            item.ParentPartID = m_part.UUID;
            item.Name = name;

            lock (m_items)
            {
                m_items.Add(item.ItemID, item);

                if (allowedDrop) 
                    m_part.TriggerScriptChangedEvent(Changed.ALLOWED_DROP);
                else
                    m_part.TriggerScriptChangedEvent(Changed.INVENTORY);
            }

            m_inventorySerial++;
            //m_inventorySerial += 2;
            HasInventoryChanged = true;
            m_part.ParentGroup.HasGroupChanged = true;
        }

        /// <summary>
        /// Restore a whole collection of items to the prim's inventory at once.
        /// We assume that the items already have all their fields correctly filled out.
        /// The items are not flagged for persistence to the database, since they are being restored
        /// from persistence rather than being newly added.
        /// </summary>
        /// <param name="items"></param>
        public void RestoreInventoryItems(ICollection<TaskInventoryItem> items)
        {
            lock (m_items)
            {
                foreach (TaskInventoryItem item in items)
                {
                    m_items.Add(item.ItemID, item);
                    m_part.TriggerScriptChangedEvent(Changed.INVENTORY);
                }
            }

            m_inventorySerial++;
        }

        /// <summary>
        /// Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        public TaskInventoryItem GetInventoryItem(UUID itemId)
        {
            TaskInventoryItem item;
            m_items.TryGetValue(itemId, out item);

            return item;
        }

        /// <summary>
        /// Update an existing inventory item.
        /// </summary>
        /// <param name="item">The updated item.  An item with the same id must already exist
        /// in this prim's inventory.</param>
        /// <returns>false if the item did not exist, true if the update occurred successfully</returns>
        public bool UpdateInventoryItem(TaskInventoryItem item)
        {
            lock (m_items)
            {
                if (m_items.ContainsKey(item.ItemID))
                {
                    item.ParentID = m_part.UUID;
                    item.ParentPartID = m_part.UUID;
                    item.Flags = m_items[item.ItemID].Flags;
                    if (item.AssetID == UUID.Zero)
                    {
                        item.AssetID = m_items[item.ItemID].AssetID;
                    }
                    else if ((InventoryType)item.Type == InventoryType.Notecard)
                    {
                        ScenePresence presence = m_part.ParentGroup.Scene.GetScenePresence(item.OwnerID);

                        if (presence != null)
                        {
                            presence.ControllingClient.SendAgentAlertMessage(
                                    "Notecard saved", false);
                        }
                    }

                    m_items[item.ItemID] = item;
                    m_inventorySerial++;
                    m_part.TriggerScriptChangedEvent(Changed.INVENTORY);

                    HasInventoryChanged = true;
                    m_part.ParentGroup.HasGroupChanged = true;

                    return true;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Tried to retrieve item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                        item.ItemID, m_part.Name, m_part.UUID);
                }
            }

            return false;
        }

        /// <summary>
        /// Remove an item from this prim's inventory
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>Numeric asset type of the item removed.  Returns -1 if the item did not exist
        /// in this prim's inventory.</returns>
        public int RemoveInventoryItem(UUID itemID)
        {
            lock (m_items)
            {
                if (m_items.ContainsKey(itemID))
                {
                    int type = m_items[itemID].InvType;
                    if (type == 10) // Script
                    {
                        m_part.ParentGroup.Scene.EventManager.TriggerRemoveScript(m_part.LocalId, itemID);
                    }
                    m_items.Remove(itemID);
                    m_inventorySerial++;
                    m_part.TriggerScriptChangedEvent(Changed.INVENTORY);

                    HasInventoryChanged = true;
                    m_part.ParentGroup.HasGroupChanged = true;

                    int scriptcount = 0;
                    lock (m_items)
                    {
                        foreach (TaskInventoryItem item in m_items.Values)
                        {
                            if (item.Type == 10)
                            {
                                scriptcount++;
                            }
                        }
                    }

                    if (scriptcount <= 0)
                    {
                        m_part.RemFlag(PrimFlags.Scripted);
                    }

                    m_part.ScheduleFullUpdate();

                    return type;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Tried to remove item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                        itemID, m_part.Name, m_part.UUID);
                }
            }

            return -1;
        }

        public string GetInventoryFileName()
        {
            if (m_inventoryFileName == String.Empty)
                m_inventoryFileName = "inventory_" + UUID.Random().ToString() + ".tmp";
            if (m_inventoryFileNameSerial < m_inventorySerial)
            {
                m_inventoryFileName = "inventory_" + UUID.Random().ToString() + ".tmp";
            }
            return m_inventoryFileName;
        }

        /// <summary>
        /// Return the name with which a client can request a xfer of this prim's inventory metadata
        /// </summary>
        /// <param name="client"></param>
        /// <param name="localID"></param>
        public bool GetInventoryFileName(IClientAPI client, uint localID)
        {
//            m_log.DebugFormat(
//                 "[PRIM INVENTORY]: Received request from client {0} for inventory file name of {1}, {2}",
//                 client.AgentId, Name, UUID);

            if (m_inventorySerial > 0)
            {
                client.SendTaskInventory(m_part.UUID, (short)m_inventorySerial,
                                         Utils.StringToBytes(GetInventoryFileName()));
                return true;
            }
            else
            {
                client.SendTaskInventory(m_part.UUID, 0, new byte[0]);
                return false;
            }
        }

        /// <summary>
        /// Serialize all the metadata for the items in this prim's inventory ready for sending to the client
        /// </summary>
        /// <param name="xferManager"></param>
        public void RequestInventoryFile(IClientAPI client, IXfer xferManager)
        {
            byte[] fileData = new byte[0];

            // Confusingly, the folder item has to be the object id, while the 'parent id' has to be zero.  This matches
            // what appears to happen in the Second Life protocol.  If this isn't the case. then various functionality
            // isn't available (such as drag from prim inventory to agent inventory)
            InventoryStringBuilder invString = new InventoryStringBuilder(m_part.UUID, UUID.Zero);

            lock (m_items)
            {
                foreach (TaskInventoryItem item in m_items.Values)
                {
                    UUID ownerID = item.OwnerID;
                    uint everyoneMask = 0;
                    uint baseMask = item.BasePermissions;
                    uint ownerMask = item.CurrentPermissions;

                    invString.AddItemStart();
                    invString.AddNameValueLine("item_id", item.ItemID.ToString());
                    invString.AddNameValueLine("parent_id", m_part.UUID.ToString());

                    invString.AddPermissionsStart();

                    invString.AddNameValueLine("base_mask", Utils.UIntToHexString(baseMask));
                    invString.AddNameValueLine("owner_mask", Utils.UIntToHexString(ownerMask));
                    invString.AddNameValueLine("group_mask", Utils.UIntToHexString(0));
                    invString.AddNameValueLine("everyone_mask", Utils.UIntToHexString(everyoneMask));
                    invString.AddNameValueLine("next_owner_mask", Utils.UIntToHexString(item.NextPermissions));

                    invString.AddNameValueLine("creator_id", item.CreatorID.ToString());
                    invString.AddNameValueLine("owner_id", ownerID.ToString());

                    invString.AddNameValueLine("last_owner_id", item.LastOwnerID.ToString());

                    invString.AddNameValueLine("group_id", item.GroupID.ToString());
                    invString.AddSectionEnd();

                    invString.AddNameValueLine("asset_id", item.AssetID.ToString());
                    invString.AddNameValueLine("type", TaskInventoryItem.Types[item.Type]);
                    invString.AddNameValueLine("inv_type", TaskInventoryItem.InvTypes[item.InvType]);
                    invString.AddNameValueLine("flags", Utils.UIntToHexString(item.Flags));

                    invString.AddSaleStart();
                    invString.AddNameValueLine("sale_type", "not");
                    invString.AddNameValueLine("sale_price", "0");
                    invString.AddSectionEnd();

                    invString.AddNameValueLine("name", item.Name + "|");
                    invString.AddNameValueLine("desc", item.Description + "|");

                    invString.AddNameValueLine("creation_date", item.CreationDate.ToString());
                    invString.AddSectionEnd();
                }
            }

            fileData = Utils.StringToBytes(invString.BuildString);

            //m_log.Debug(Utils.BytesToString(fileData));
            //m_log.Debug("[PRIM INVENTORY]: RequestInventoryFile fileData: " + Utils.BytesToString(fileData));

            if (fileData.Length > 2)
            {
                xferManager.AddNewFile(m_inventoryFileName, fileData);
            }
        }

        /// <summary>
        /// Process inventory backup
        /// </summary>
        /// <param name="datastore"></param>
        public void ProcessInventoryBackup(IRegionDataStore datastore)
        {
            if (HasInventoryChanged)
            {
                lock (Items)
                {
                    datastore.StorePrimInventory(m_part.UUID, Items.Values);
                }

                HasInventoryChanged = false;
            }
        }

        public class InventoryStringBuilder
        {
            public string BuildString = String.Empty;

            public InventoryStringBuilder(UUID folderID, UUID parentID)
            {
                BuildString += "\tinv_object\t0\n\t{\n";
                AddNameValueLine("obj_id", folderID.ToString());
                AddNameValueLine("parent_id", parentID.ToString());
                AddNameValueLine("type", "category");
                AddNameValueLine("name", "Contents|");
                AddSectionEnd();
            }

            public void AddItemStart()
            {
                BuildString += "\tinv_item\t0\n";
                AddSectionStart();
            }

            public void AddPermissionsStart()
            {
                BuildString += "\tpermissions 0\n";
                AddSectionStart();
            }

            public void AddSaleStart()
            {
                BuildString += "\tsale_info\t0\n";
                AddSectionStart();
            }

            protected void AddSectionStart()
            {
                BuildString += "\t{\n";
            }

            public void AddSectionEnd()
            {
                BuildString += "\t}\n";
            }

            public void AddLine(string addLine)
            {
                BuildString += addLine;
            }

            public void AddNameValueLine(string name, string value)
            {
                BuildString += "\t\t";
                BuildString += name + "\t";
                BuildString += value + "\n";
            }

            public void Close()
            {
            }
        }

        public uint MaskEffectivePermissions()
        {
            uint mask=0x7fffffff;

            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType != (int)InventoryType.Object)
                {
                    if ((item.CurrentPermissions & item.NextPermissions & (uint)PermissionMask.Copy) == 0)
                        mask &= ~((uint)PermissionMask.Copy >> 13);
                    if ((item.CurrentPermissions & item.NextPermissions & (uint)PermissionMask.Transfer) == 0)
                        mask &= ~((uint)PermissionMask.Transfer >> 13);
                    if ((item.CurrentPermissions & item.NextPermissions & (uint)PermissionMask.Modify) == 0)
                        mask &= ~((uint)PermissionMask.Modify >> 13);
                }
                else
                {
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Copy >> 13)) == 0)
                        mask &= ~((uint)PermissionMask.Copy >> 13);
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Transfer >> 13)) == 0)
                        mask &= ~((uint)PermissionMask.Transfer >> 13);
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Modify >> 13)) == 0)
                        mask &= ~((uint)PermissionMask.Modify >> 13);
                }

                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    mask &= ~(uint)PermissionMask.Copy;
                if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                    mask &= ~(uint)PermissionMask.Transfer;
                if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    mask &= ~(uint)PermissionMask.Modify;
            }
            return mask;
        }

        public void ApplyNextOwnerPermissions()
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.Object && (item.CurrentPermissions & 7) != 0)
                {
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Copy >> 13)) == 0)
                        item.CurrentPermissions &= ~(uint)PermissionMask.Copy;
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Transfer >> 13)) == 0)
                        item.CurrentPermissions &= ~(uint)PermissionMask.Transfer;
                    if ((item.CurrentPermissions & ((uint)PermissionMask.Modify >> 13)) == 0)
                        item.CurrentPermissions &= ~(uint)PermissionMask.Modify;
                    item.CurrentPermissions |= 8;
                }
                item.CurrentPermissions &= item.NextPermissions;
                item.BasePermissions &= item.NextPermissions;
                item.EveryonePermissions &= item.NextPermissions;
            }

            m_part.TriggerScriptChangedEvent(Changed.OWNER);
        }

        public void ApplyGodPermissions(uint perms)
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                item.CurrentPermissions = perms;
                item.BasePermissions = perms;
            }
        }

        public bool ContainsScripts()
        {
            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    return true;
                }
            }
            return false;
        }

        public List<UUID> GetInventoryList()
        {
            List<UUID> ret = new List<UUID>();

            foreach (TaskInventoryItem item in m_items.Values)
                ret.Add(item.ItemID);

            return ret;
        }
        
        public string[] GetScriptAssemblies()
        {
            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();

            List<string> ret = new List<string>();
            if (engines == null) // No engine at all
                return new string[0];

            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    foreach (IScriptModule e in engines)
                    {
                        if (e != null)
                        {
                            string n = e.GetAssemblyName(item.ItemID);
                            if (n != String.Empty)
                            {
                                if (!ret.Contains(n))
                                    ret.Add(n);
                                break;
                            }
                        }
                    }
                }
            }
            return ret.ToArray();
        }
        
        public Dictionary<UUID, string> GetScriptStates()
        {
            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();

            Dictionary<UUID, string> ret = new Dictionary<UUID, string>();
            if (engines == null) // No engine at all
                return ret;

            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    foreach (IScriptModule e in engines)
                    {
                        if (e != null)
                        {
                            string n = e.GetXMLState(item.ItemID);
                            if (n != String.Empty)
                            {
                                if (!ret.ContainsKey(item.ItemID))
                                    ret[item.ItemID] = n;
                                break;
                            }
                        }
                    }
                }
            }
            return ret;
        }

        public bool CanBeDeleted()
        {
            if (!ContainsScripts())
                return true;

            IScriptModule[] engines = m_part.ParentGroup.Scene.RequestModuleInterfaces<IScriptModule>();

            if (engines == null) // No engine at all
                return true;

            foreach (TaskInventoryItem item in m_items.Values)
            {
                if (item.InvType == (int)InventoryType.LSL)
                {
                    foreach (IScriptModule e in engines)
                    {
                        if (!e.CanBeDeleted(item.ItemID))
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
