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
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class SceneObjectPart : IScriptHost
    {
        private string m_inventoryFileName = "";
        
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
        /// Holds in memory prim inventory
        /// </summary> 
        protected IDictionary<LLUUID, TaskInventoryItem> m_taskInventory 
            = new Dictionary<LLUUID, TaskInventoryItem>();
        
        [XmlIgnore]
        public IDictionary<LLUUID, TaskInventoryItem> TaskInventory
        {
            get { return m_taskInventory; }
        }
        
        /// <summary>
        /// Serial count for inventory file , used to tell if inventory has changed
        /// no need for this to be part of Database backup
        /// </summary>
        protected uint m_inventorySerial = 0;

        public uint InventorySerial
        {
            get { return m_inventorySerial; }
        }

        /// <summary>
        /// Add an item to this prim's inventory.
        /// </summary>
        /// <param name="item"></param>
        public void AddInventoryItem(TaskInventoryItem item)
        {
            item.parent_id = m_folderID;
            item.creation_date = 1000;
            item.ParentPartID = UUID;
            m_taskInventory.Add(item.item_id, item);
            m_inventorySerial++;
        }
        
        /// <summary>
        /// Add a whole collection of items to the prim's inventory at once.  We assume that the items already
        /// have all their fields correctly filled out.
        /// </summary>
        /// <param name="items"></param>
        public void AddInventoryItems(ICollection<TaskInventoryItem> items)
        {
            foreach (TaskInventoryItem item in items)
            {
                m_taskInventory.Add(item.item_id, item);
            }
            
            m_inventorySerial++;
        }

        public int RemoveInventoryItem(IClientAPI remoteClient, uint localID, LLUUID itemID)
        {
            if (localID == LocalID)
            {
                if (m_taskInventory.ContainsKey(itemID))
                {
                    string type = m_taskInventory[itemID].inv_type;
                    m_taskInventory.Remove(itemID);
                    m_inventorySerial++;
                    if (type == "lsl_text")
                    {
                        return 10;
                    }
                    else
                    {
                        return 0;
                    }
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
            foreach (TaskInventoryItem item in m_taskInventory.Values)
            {
                invString.AddItemStart();
                invString.AddNameValueLine("item_id", item.item_id.ToString());
                invString.AddNameValueLine("parent_id", item.parent_id.ToString());

                invString.AddPermissionsStart();
                invString.AddNameValueLine("base_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("owner_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("group_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("everyone_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("next_owner_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("creator_id", item.creator_id.ToString());
                invString.AddNameValueLine("owner_id", item.owner_id.ToString());
                invString.AddNameValueLine("last_owner_id", item.last_owner_id.ToString());
                invString.AddNameValueLine("group_id", item.group_id.ToString());
                invString.AddSectionEnd();

                invString.AddNameValueLine("asset_id", item.asset_id.ToString());
                invString.AddNameValueLine("type", item.type);
                invString.AddNameValueLine("inv_type", item.inv_type);
                invString.AddNameValueLine("flags", "0x00");
                invString.AddNameValueLine("name", item.name + "|");
                invString.AddNameValueLine("desc", item.desc + "|");
                invString.AddNameValueLine("creation_date", item.creation_date.ToString());
                invString.AddSectionEnd();
            }
            
            fileData = Helpers.StringToField(invString.BuildString);
            
//            MainLog.Instance.Verbose(
//                "PRIMINVENTORY", "RequestInventoryFile fileData: {0}", Helpers.FieldToUTF8String(fileData));
            
            if (fileData.Length > 2)
            {
                xferManager.AddNewFile(m_inventoryFileName, fileData);
            }
        }

        public class InventoryStringBuilder
        {
            public string BuildString = "";

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
