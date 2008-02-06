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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

using libsecondlife;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class SceneObjectPart : IScriptHost
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
        /// new ones or setting existing UUIDs to the correct parent UUIDs
        /// </summary>
        /// <param name="linkNum'>Link number for the part</param>
        public void ResetInventoryIDs()
        {
            lock (TaskInventory)
            {              
                IList<TaskInventoryItem> items = new List<TaskInventoryItem>(TaskInventory.Values);
                TaskInventory.Clear();
                
                foreach (TaskInventoryItem item in items)
                {
                    item.ResetIDs(UUID);
                    TaskInventory.Add(item.ItemID, item);
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

        public void StopScripts()
        {
            lock (m_taskInventory)
            {
                foreach (TaskInventoryItem item in m_taskInventory.Values)
                {
                    if (10 == item.Type)
                    {
                        StopScript(item.ItemID);
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
//            m_log.Info(String.Format(
//                           "[PRIMINVENTORY]: " +
//                           "Starting script {0}, {1} in prim {2}, {3}", 
//                           item.Name, item.ItemID, Name, UUID));
            if (!((m_parentGroup.Scene.RegionInfo.EstateSettings.regionFlags & Simulator.RegionFlags.SkipScripts) == Simulator.RegionFlags.SkipScripts))
            {
                AssetBase rezAsset = m_parentGroup.Scene.AssetCache.GetAsset(item.AssetID, false);

                if (rezAsset != null)
                {
                    string script = Helpers.FieldToUTF8String(rezAsset.Data);
                    m_parentGroup.Scene.EventManager.TriggerRezScript(LocalID, item.ItemID, script);

                }
                else
                {
                    m_log.Error(String.Format(
                                    "[PRIMINVENTORY]: " +
                                    "Couldn't start script {0}, {1} since asset ID {2} could not be found",
                                    item.Name, item.ItemID, item.AssetID));
                }
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
                    m_parentGroup.AddActiveScriptCount(1);
                }            
                else
                {
                    m_log.Error(String.Format(
                                    "[PRIMINVENTORY]: " +
                                    "Couldn't start script with ID {0} since it couldn't be found for prim {1}, {2}", 
                                    itemId, Name, UUID));
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
                m_parentGroup.Scene.EventManager.TriggerRemoveScript(LocalID, itemId);
                m_parentGroup.AddActiveScriptCount(-1);
            }            
            else
            {
                m_log.Error(String.Format(
                                "[PRIMINVENTORY]: " +
                                "Couldn't stop script with ID {0} since it couldn't be found for prim {1}, {2}", 
                                itemId, Name, UUID));
            }                            
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
            
            lock (m_taskInventory)
            {            
                m_taskInventory.Add(item.ItemID, item);
                TriggerScriptChangedEvent(Changed.INVENTORY);
            }
            
            m_inventorySerial++;
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
                    return m_taskInventory[itemID];
                }
                else
                {
                    m_log.Error(String.Format(
                                    "[PRIMINVENTORY]: " +
                                    "Tried to retrieve item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                                    itemID, Name, UUID));
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
                    m_log.Error(String.Format(
                                    "[PRIMINVENTORY]: " +
                                    "Tried to retrieve item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                                    item.ItemID, Name, UUID));
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
                    
                    return type;
                }
                else
                {
                    m_log.Error(String.Format(
                                    "[PRIMINVENTORY]: " +
                                    "Tried to remove item ID {0} from prim {1}, {2} but the item does not exist in this inventory",
                                    itemID, Name, UUID));
                }            
            }

            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="localID"></param>
        public bool GetInventoryFileName(IClientAPI client, uint localID)
        {
            if (m_inventorySerial > 0)
            {
                client.SendTaskInventory(m_uuid, (short) m_inventorySerial,
                                         Helpers.StringToField(m_inventoryFileName));
                return true;
            }
            else
            {
                client.SendTaskInventory(m_uuid, 0, new byte[0]);
                return false;
            }
        }

        public void RequestInventoryFile(IXfer xferManager)
        {
            byte[] fileData = new byte[0];
            InventoryStringBuilder invString = new InventoryStringBuilder(m_folderID, UUID);
            
            lock (m_taskInventory)
            {            
                foreach (TaskInventoryItem item in m_taskInventory.Values)
                {
                    invString.AddItemStart();
                    invString.AddNameValueLine("item_id", item.ItemID.ToString());
                    invString.AddNameValueLine("parent_id", item.ParentID.ToString());
    
                    invString.AddPermissionsStart();
                    invString.AddNameValueLine("base_mask", "0x7FFFFFFF");
                    invString.AddNameValueLine("owner_mask", "0x7FFFFFFF");
                    invString.AddNameValueLine("group_mask", "0x7FFFFFFF");
                    invString.AddNameValueLine("everyone_mask", "0x7FFFFFFF");
                    invString.AddNameValueLine("next_owner_mask", "0x7FFFFFFF");
                    invString.AddNameValueLine("creator_id", item.CreatorID.ToString());
                    invString.AddNameValueLine("owner_id", item.OwnerID.ToString());
                    invString.AddNameValueLine("last_owner_id", item.LastOwnerID.ToString());
                    invString.AddNameValueLine("group_id", item.GroupID.ToString());
                    invString.AddSectionEnd();
    
                    invString.AddNameValueLine("asset_id", item.AssetID.ToString());
                    invString.AddNameValueLine("type", TaskInventoryItem.Types[item.Type]);
                    invString.AddNameValueLine("inv_type", TaskInventoryItem.InvTypes[item.InvType]);
                    invString.AddNameValueLine("flags", "0x00");
                    invString.AddNameValueLine("name", item.Name + "|");
                    invString.AddNameValueLine("desc", item.Description + "|");
                    invString.AddNameValueLine("creation_date", item.CreationDate.ToString());
                    invString.AddSectionEnd();
                }
            }
            
            fileData = Helpers.StringToField(invString.BuildString);
            
//            m_log.Info(String.Format(
//                           "[PRIMINVENTORY]: RequestInventoryFile fileData: {0}", Helpers.FieldToUTF8String(fileData)));
            
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
                AddNameValueLine("name", "Contents");
                AddSectionEnd();
            }

            public void AddItemStart()
            {
                BuildString += "\tinv_item\t0\n";
                BuildString += "\t{\n";
            }

            public void AddPermissionsStart()
            {
                BuildString += "\tpermissions 0\n";
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
