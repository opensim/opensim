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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Framework.Communications.Cache
{
    public class InventoryFolderImpl : InventoryFolderBase
    {
        // Fields
        public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();
        public Dictionary<LLUUID, InventoryFolderImpl> SubFolders = new Dictionary<LLUUID, InventoryFolderImpl>();

        public InventoryFolderImpl(InventoryFolderBase folderbase)
        {
            agentID = folderbase.agentID;
            folderID = folderbase.folderID;
            name = folderbase.name;
            parentID = folderbase.parentID;
            type = folderbase.type;
            version = folderbase.version;
        }

        public InventoryFolderImpl()
        {
        }

        // Methods
        public InventoryFolderImpl CreateNewSubFolder(LLUUID folderID, string folderName, ushort type)
        {
            InventoryFolderImpl subFold = new InventoryFolderImpl();
            subFold.name = folderName;
            subFold.folderID = folderID;
            subFold.type = (short) type;
            subFold.parentID = this.folderID;
            subFold.agentID = agentID;
            SubFolders.Add(subFold.folderID, subFold);
            return subFold;
        }

        public InventoryItemBase HasItem(LLUUID itemID)
        {
            InventoryItemBase base2 = null;
            if (Items.ContainsKey(itemID))
            {
                return Items[itemID];
            }
            foreach (InventoryFolderImpl folder in SubFolders.Values)
            {
                base2 = folder.HasItem(itemID);
                if (base2 != null)
                {
                    break;
                }
            }
            return base2;
        }

        public bool DeleteItem(LLUUID itemID)
        {
            bool found = false;
            if (Items.ContainsKey(itemID))
            {
                Items.Remove(itemID);
                return true;
            }
            foreach (InventoryFolderImpl folder in SubFolders.Values)
            {
                found = folder.DeleteItem(itemID);
                if (found == true)
                {
                    break;
                }
            }
            return found;
        }


        public InventoryFolderImpl HasSubFolder(LLUUID folderID)
        {
            InventoryFolderImpl returnFolder = null;
            if (SubFolders.ContainsKey(folderID))
            {
                returnFolder = SubFolders[folderID];
            }
            else
            {
                foreach (InventoryFolderImpl folder in SubFolders.Values)
                {
                    returnFolder = folder.HasSubFolder(folderID);
                    if (returnFolder != null)
                    {
                        break;
                    }
                }
            }
            return returnFolder;
        }

        public List<InventoryItemBase> RequestListOfItems()
        {
            List<InventoryItemBase> itemList = new List<InventoryItemBase>();
            foreach (InventoryItemBase item in Items.Values)
            {
                itemList.Add(item);
            }
            return itemList;
        }
    }
}