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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class SceneObjectPart : IScriptHost
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_inventoryFileName = String.Empty;

        /// <summary>
        /// The inventory folder for this prim
        /// </summary>
        private LLUUID m_folderID = LLUUID.Zero;

        /// <summary>
        /// Exposing this is not particularly good, but it's one of the least evils at the moment to see
        /// folder id from prim inventory item data, since it's not (yet) actually stored with the prim.
        /// </summary>
        public LLUUID FolderID
        {
            get { return m_folderID; }
            set { m_folderID = value; }
        }

        /// <summary>
        /// Serial count for inventory file , used to tell if inventory has changed
        /// no need for this to be part of Database backup
        /// </summary>
        protected uint m_inventorySerial = 0;

        public uint InventorySerial
        {
            get { return m_inventorySerial; }
            set { m_inventorySerial = value; }
        }

        /// <summary>
        /// Holds in memory prim inventory
        /// </summary> 
        protected TaskInventoryDictionary m_taskInventory = new TaskInventoryDictionary();

        public TaskInventoryDictionary TaskInventory
        {
            get { return m_taskInventory; }
            set { m_taskInventory = value; }
        }

        /// <summary>
        /// Tracks whether inventory has changed since the last persistent backup
        /// </summary>
        private bool HasInventoryChanged;

        /// <summary>
        /// Reset LLUUIDs for all the items in the prim's inventory.  This involves either generating
        /// new ones or setting existing UUIDs to the correct parent UUIDs.
        /// 
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        /// </summary>
        /// <param name="linkNum">Link number for the part</param>
        public void ResetInventoryIDs()
        {
            lock (TaskInventory)
            {                         
                if (0 == TaskInventory.Count)
                {
                    return;                    
                }
                
                HasInventoryChanged = true;                
                
                IList<TaskInventoryItem> items = new List<TaskInventoryItem>(TaskInventory.Values);
                TaskInventory.Clear();

                foreach (TaskInventoryItem item in items)
                {
                    item.ResetIDs(UUID);
                    TaskInventory.Add(item.ItemID, item);
                }
            }
        }

        public void ChangeInventoryOwner(LLUUID ownerId)
        {
            lock (TaskInventory)
            {
                if (0 == TaskInventory.Count)
                {
                    return;
                }

                HasInventoryChanged = true;

                IList<TaskInventoryItem> items = new List<TaskInventoryItem>(TaskInventory.Values);
                foreach (TaskInventoryItem item in items)
                {
                    if (ownerId != item.OwnerID)
                    {
                        item.LastOwnerID = item.OwnerID;
                        item.OwnerID = ownerId; 
                        item.BaseMask = item.NextOwnerMask & (uint)PermissionMask.All;
                        item.OwnerMask = item.NextOwnerMask & (uint)PermissionMask.All;
                    }
                }
            }
        }

        /// <summary>
        /// Start all the scripts contained in this prim's inventory
        /// </summary>
        public void StartScripts()
        {
            lock (m_taskInventory)
            {
                foreach (TaskInventoryItem item in m_taskInventory.Values)
                {
                    // XXX more hardcoding badness.  Should be an enum in TaskInventoryItem
                    if (10 == item.Type)
                    {
                        StartScript(item);
                    }
                }
            }
        }

        /// <summary>
        /// Stop all the scripts in this prim.
        /// </summary>
        public void StopScripts()
        {
            lock (m_taskInventory)
            {
                foreach (TaskInventoryItem item in m_taskInventory.Values)
                {
                    if (10 == item.Type)
                    {
                        StopScript(item.ItemID);
                        RemoveScriptEvents(item.ItemID);
                    }
                }
            }
        }

        /// <summary>
        /// Start a script which is in this prim's inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public void StartScript(TaskInventoryItem item)
        {
            //            m_log.InfoFormat(
            //                "[PRIM INVENTORY]: " +
            //                "Starting script {0}, {1} in prim {2}, {3}", 
            //                item.Name, item.ItemID, Name, UUID);
            AddFlag(LLObject.ObjectFlags.Scripted);

            if (!((m_parentGroup.Scene.RegionInfo.EstateSettings.regionFlags & Simulator.RegionFlags.SkipScripts) == Simulator.RegionFlags.SkipScripts))
            {
                AssetCache cache = m_parentGroup.Scene.AssetCache;

                cache.GetAsset(item.AssetID, delegate(LLUUID assetID, AssetBase asset)
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
                           string script = Helpers.FieldToUTF8String(asset.Data);
                           m_parentGroup.Scene.EventManager.TriggerRezScript(LocalId,item.ItemID,script);
                           m_parentGroup.AddActiveScriptCount(1);
                           ScheduleFullUpdate();
                       }
                   }, false);
            }
        }

        /// <summary>
        /// Start a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId">
        /// A <see cref="LLUUID"/>
        /// </param>        
        public void StartScript(LLUUID itemId)
        {
            lock (m_taskInventory)
            {
                if (m_taskInventory.ContainsKey(itemId))
                {
                    StartScript(m_taskInventory[itemId]);

                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Couldn't start script with ID {0} since it couldn't be found for prim {1}, {2}",
                        itemId, Name, UUID);
                }
            }
        }

        /// <summary>
        /// Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>        
        public void StopScript(LLUUID itemId)
        {
            if (m_taskInventory.ContainsKey(itemId))
            {
                m_parentGroup.Scene.EventManager.TriggerRemoveScript(LocalId, itemId);
                m_parentGroup.AddActiveScriptCount(-1);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't stop script with ID {0} since it couldn't be found for prim {1}, {2}",
                    itemId, Name, UUID);
            }
        }

        // Assumes a lock is held on the inventory
        private bool InventoryContainsName(string name)
        {
            foreach (TaskInventoryItem item in m_taskInventory.Values)
            {
                if(item.Name == name)
                    return true;
            }
            return false;
        }

        private string FindAvailableInventoryName(string name)
        {
            if(!InventoryContainsName(name))
                return name;

            int suffix=1;
            while(suffix < 256)
            {
                string tryName=String.Format("{0} {1}", name, suffix);
                if(!InventoryContainsName(tryName))
                    return tryName;
                suffix++;
            }
            return String.Empty;
        }

        /// <summary>
        /// Add an item to this prim's inventory.
        /// </summary>
        /// <param name="item"></param>
        public void AddInventoryItem(TaskInventoryItem item)
        {
            item.ParentID = m_folderID;
            item.CreationDate = 1000;
            item.ParentPartID = UUID;

            string name=FindAvailableInventoryName(item.Name);
            if(name == String.Empty)
                return;

            item.Name=name;

            lock (m_taskInventory)
            {
                m_taskInventory.Add(item.ItemID, item);
                TriggerScriptChangedEvent(Changed.INVENTORY);
            }

            m_inventorySerial++;
            //m_inventorySerial += 2;
            HasInventoryChanged = true;
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
            lock (m_taskInventory)
            {
                foreach (TaskInventoryItem item in items)
                {
                    m_taskInventory.Add(item.ItemID, item);
                    TriggerScriptChangedEvent(Changed.INVENTORY);
                }
            }

            m_inventorySerial++;
        }

        /// <summary>
        /// Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        public TaskInventoryItem GetInventoryItem(LLUUID itemID)
        {
            lock (m_taskInventory)
            {
                if (m_taskInventory.ContainsKey(itemID))
                {
//                    m_log.DebugFormat(
//                        "[PRIM INVENTORY]: Retrieved task inventory item {0}, {1} from prim {2}, {3}", 
//                        m_taskInventory[itemID].Name, itemID, Name, UUID);
                    
                    return m_taskInventory[itemID];
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Tried to retrieve item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                        itemID, Name, UUID);
                }
            }

            return null;
        }

        /// <summary>
        /// Update an existing inventory item.
        /// </summary>
        /// <param name="item">The updated item.  An item with the same id must already exist
        /// in this prim's inventory.</param>
        /// <returns>false if the item did not exist, true if the update occurred succesfully</returns>
        public bool UpdateInventoryItem(TaskInventoryItem item)
        {
            lock (m_taskInventory)
            {
                if (m_taskInventory.ContainsKey(item.ItemID))
                {
                    m_taskInventory[item.ItemID] = item;
                    m_inventorySerial++;
                    TriggerScriptChangedEvent(Changed.INVENTORY);

                    HasInventoryChanged = true;

                    return true;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Tried to retrieve item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                        item.ItemID, Name, UUID);
                }
            }

            return false;
        }

        public void AddScriptLPS(int count)
        {
            m_parentGroup.AddScriptLPS(count);
        }

        /// <summary>
        /// Remove an item from this prim's inventory
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>Numeric asset type of the item removed.  Returns -1 if the item did not exist
        /// in this prim's inventory.</returns>
        public int RemoveInventoryItem(LLUUID itemID)
        {
            lock (m_taskInventory)
            {
                if (m_taskInventory.ContainsKey(itemID))
                {
                    int type = m_taskInventory[itemID].InvType;
                    m_taskInventory.Remove(itemID);
                    m_inventorySerial++;
                    TriggerScriptChangedEvent(Changed.INVENTORY);

                    HasInventoryChanged = true;

                    int scriptcount = 0;
                    lock (m_taskInventory)
                    {
                        foreach (TaskInventoryItem item in m_taskInventory.Values)
                        {
                            if (item.Type == 10)
                            {
                                scriptcount++;
                            }
                        }

                    }
                    if (scriptcount <= 0)
                    {
                        RemFlag(LLObject.ObjectFlags.Scripted);
                        ScheduleFullUpdate();
                    }
                    ScheduleFullUpdate();

                    return type;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Tried to remove item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                        itemID, Name, UUID);
                }
            }

            return -1;
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
                client.SendTaskInventory(m_uuid, (short)m_inventorySerial,
                                         Helpers.StringToField(m_inventoryFileName));
                return true;
            }
            else
            {
                client.SendTaskInventory(m_uuid, 0, new byte[0]);
                return false;
            }
        }

        /// <summary>
        /// Serialize all the metadata for the items in this prim's inventory ready for sending to the client
        /// </summary>
        /// <param name="xferManager"></param>
        public void RequestInventoryFile(IXfer xferManager)
        {            
            byte[] fileData = new byte[0];
            
            // Confusingly, the folder item has to be the object id, while the 'parent id' has to be zero.  This matches
            // what appears to happen in the Second Life protocol.  If this isn't the case. then various functionality
            // isn't available (such as drag from prim inventory to agent inventory)
            InventoryStringBuilder invString = new InventoryStringBuilder(m_folderID, LLUUID.Zero);

            lock (m_taskInventory)
            {
                foreach (TaskInventoryItem item in m_taskInventory.Values)
                {
                    invString.AddItemStart();
                    invString.AddNameValueLine("item_id", item.ItemID.ToString());                    
                    invString.AddNameValueLine("parent_id", m_folderID.ToString());

                    invString.AddPermissionsStart();
                    
                    // FIXME: Temporary until permissions are properly sorted.
                    invString.AddNameValueLine("base_mask", "7fffffff");
                    invString.AddNameValueLine("owner_mask", "7fffffff");
                    invString.AddNameValueLine("group_mask", "7fffffff");
                    invString.AddNameValueLine("everyone_mask", "7fffffff");
                    invString.AddNameValueLine("next_owner_mask", "7fffffff");
                    
//                    invString.AddNameValueLine("group_mask", "00000000");
//                    invString.AddNameValueLine("everyone_mask", "00000000");
//                    invString.AddNameValueLine("next_owner_mask", "00086000");
                    
//                    invString.AddNameValueLine("base_mask", Helpers.UIntToHexString(item.BaseMask));
//                    invString.AddNameValueLine("owner_mask", Helpers.UIntToHexString(item.OwnerMask));
//                    invString.AddNameValueLine("group_mask", Helpers.UIntToHexString(item.GroupMask));
//                    invString.AddNameValueLine("everyone_mask", Helpers.UIntToHexString(item.EveryoneMask));
//                    invString.AddNameValueLine("next_owner_mask", Helpers.UIntToHexString(item.NextOwnerMask));
                    
                    invString.AddNameValueLine("creator_id", item.CreatorID.ToString());
                    invString.AddNameValueLine("owner_id", item.OwnerID.ToString());
                    
                    invString.AddNameValueLine("last_owner_id", item.LastOwnerID.ToString());
//                    invString.AddNameValueLine("last_owner_id", item.OwnerID.ToString());
                    
                    invString.AddNameValueLine("group_id", item.GroupID.ToString());
                    invString.AddSectionEnd();
                        
                    invString.AddNameValueLine("asset_id", item.AssetID.ToString());
                    invString.AddNameValueLine("type", TaskInventoryItem.Types[item.Type]);
                    invString.AddNameValueLine("inv_type", TaskInventoryItem.InvTypes[item.InvType]);
                    invString.AddNameValueLine("flags", "00000000");
                                        
                    invString.AddSaleStart();
                    invString.AddNameValueLine("sale_type", "not");
                    invString.AddNameValueLine("sale_price", "0");
                    invString.AddSectionEnd();
                    
                    invString.AddNameValueLine("name", item.Name + "|");
                    invString.AddNameValueLine("desc", item.Description + "|");
                                    
                    invString.AddNameValueLine("creation_date", item.CreationDate.ToString());
//                    invString.AddNameValueLine("creation_date", "1209151453");
                    
                    invString.AddSectionEnd();
                }
            }

            fileData = Helpers.StringToField(invString.BuildString);

            //m_log.Debug("[PRIM INVENTORY]: RequestInventoryFile fileData: " + Helpers.FieldToUTF8String(fileData));

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
                lock (TaskInventory)
                {
                    datastore.StorePrimInventory(UUID, TaskInventory.Values);
                }

                HasInventoryChanged = false;
            }
        }

        public class InventoryStringBuilder
        {
            public string BuildString = String.Empty;

            public InventoryStringBuilder(LLUUID folderID, LLUUID parentID)
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
    }
}
