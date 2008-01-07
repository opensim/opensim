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

using libsecondlife;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class SceneObjectGroup : EntityBase
    {
        /// <summary>
        /// Start a given script.
        /// </summary>
        /// <param name="localID">
        /// A <see cref="System.UInt32"/>
        /// </param>
        public void StartScript(uint localID, LLUUID itemID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.StartScript(itemID);
            }
            else
            {
                MainLog.Instance.Error(
                    "PRIMINVENTORY",
                    "Couldn't find part {0} in object group {1}, {2} to start script with ID {3}",
                    localID, Name, UUID, itemID);
            }            
        }
        
        /// <summary>
        /// Start the scripts contained in all the prims in this group.
        /// </summary>
        public void StartScripts()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.StartScripts();
            }            
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="localID"></param>
        public bool GetPartInventoryFileName(IClientAPI remoteClient, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.GetInventoryFileName(remoteClient, localID);
            }
            else
            {
                MainLog.Instance.Error(
                    "PRIMINVENTORY",
                    "Couldn't find part {0} in object group {1}, {2} to retreive prim inventory",
                    localID, Name, UUID);
            }
            return false;
        }

        public void RequestInventoryFile(uint localID, IXfer xferManager)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.RequestInventoryFile(xferManager);
            }
            else
            {
                MainLog.Instance.Error(
                    "PRIMINVENTORY",
                    "Couldn't find part {0} in object group {1}, {2} to request inventory data",
                    localID, Name, UUID);
            }
        }

        /// <summary>
        /// Add an inventory item to a prim in this group.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="localID"></param>
        /// <param name="item"></param>
        /// <param name="copyItemID">The item UUID that should be used by the new item.</param>
        /// <returns></returns>
        public bool AddInventoryItem(IClientAPI remoteClient, uint localID, 
                                     InventoryItemBase item, LLUUID copyItemID)
        {
            LLUUID newItemId = ((copyItemID != null) ? copyItemID : item.inventoryID);
            
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                TaskInventoryItem taskItem = new TaskInventoryItem();
                
                taskItem.item_id = newItemId;                
                taskItem.asset_id = item.assetID;
                taskItem.name = item.inventoryName;
                taskItem.desc = item.inventoryDescription;
                taskItem.owner_id = item.avatarID;
                taskItem.creator_id = item.creatorsID;
                taskItem.type = TaskInventoryItem.Types[item.assetType];
                taskItem.inv_type = TaskInventoryItem.InvTypes[item.invType];
                part.AddInventoryItem(taskItem);
                
                // It might seem somewhat crude to update the whole group for a single prim inventory change,
                // but it's possible that other prim inventory changes will take place before the region 
                // persistence thread visits this object.  In the future, changes can be signalled at a more
                // granular level, or we could let the datastore worry about whether prims have really 
                // changed since they were last persisted.
                HasChanged = true;
                
                return true;
            }
            else
            {
                MainLog.Instance.Error(
                    "PRIMINVENTORY",
                    "Couldn't find prim local ID {0} in group {1}, {2} to add inventory item ID {3}",
                    localID, Name, UUID, newItemId);
            }

            return false;
        }

        public int RemoveInventoryItem(IClientAPI remoteClient, uint localID, LLUUID itemID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {                
                int type = part.RemoveInventoryItem(remoteClient, localID, itemID);
                
                // It might seem somewhat crude to update the whole group for a single prim inventory change,
                // but it's possible that other prim inventory changes will take place before the region 
                // persistence thread visits this object.  In the future, changes can be signalled at a more
                // granular level, or we could let the datastore worry about whether prims have really 
                // changed since they were last persisted.
                HasChanged = true;
                
                return type;
            }
            
            return -1;
        } 
    }
}