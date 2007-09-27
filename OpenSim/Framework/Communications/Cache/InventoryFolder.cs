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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;

using InventoryCategory = OpenSim.Framework.Data.InventoryCategory;

namespace OpenSim.Framework.Communications.Caches
{
    public class InventoryFolder : InventoryFolderBase
    {
        // Fields
        public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();
        public Dictionary<LLUUID, InventoryFolder> SubFolders = new Dictionary<LLUUID, InventoryFolder>();

        public InventoryFolder(InventoryFolderBase folderbase)
        {
            this.agentID = folderbase.agentID;
            this.folderID = folderbase.folderID;
            this.name = folderbase.name;
            this.parentID = folderbase.parentID;
            this.type = folderbase.type;
            this.version = folderbase.version;
        }

        public InventoryFolder()
        {

        }

        // Methods
        public InventoryFolder CreateNewSubFolder(LLUUID folderID, string folderName, ushort type, InventoryCategory category)
        {
            InventoryFolder subFold = new InventoryFolder();
            subFold.name = folderName;
            subFold.folderID = folderID;
            subFold.type = (short) type;
            subFold.parentID = this.folderID;
            subFold.agentID = this.agentID;
            subFold.category = category;
            if (!SubFolders.ContainsKey(subFold.folderID))
                this.SubFolders.Add(subFold.folderID, subFold);
            else
                MainLog.Instance.Warn("INVENTORYCACHE", "Attempt to create a duplicate folder {0} {1}", folderName, folderID);

            return subFold;
        }

        public InventoryItemBase HasItem(LLUUID itemID)
        {
            InventoryItemBase base2 = null;
            if (this.Items.ContainsKey(itemID))
            {
                return this.Items[itemID];
            }
            foreach (InventoryFolder folder in this.SubFolders.Values)
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
            if (this.Items.ContainsKey(itemID))
            {
                Items.Remove(itemID);
                return true;
            }
            foreach (InventoryFolder folder in this.SubFolders.Values)
            {
                found = folder.DeleteItem(itemID);
                if (found == true)
                {
                    break;
                }
            }
            return found;
        }


        public InventoryFolder HasSubFolder(LLUUID folderID)
        {
            InventoryFolder returnFolder = null;
            if (this.SubFolders.ContainsKey(folderID))
            {
                returnFolder = this.SubFolders[folderID];
            }
            else
            {
                foreach (InventoryFolder folder in this.SubFolders.Values)
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
            foreach (InventoryItemBase item in this.Items.Values)
            {
                itemList.Add(item);
            }
            return itemList;
        }
    }
}
