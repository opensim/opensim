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

using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

using log4net;

namespace OpenSim.Capabilities.Handlers
{
    public class FetchInventory2Handler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_inventoryService;
        private UUID m_agentID;

        public FetchInventory2Handler(IInventoryService invService, UUID agentId)
        {
            m_inventoryService = invService;
            m_agentID = agentId;
        }

        public string FetchInventoryRequest(string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            //m_log.DebugFormat("[FETCH INVENTORY HANDLER]: Received FetchInventory capability request {0}", request);

            OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml(Utils.StringToBytes(request));
            OSDArray itemsRequested = (OSDArray)requestmap["items"];

            string reply;
            LLSDFetchInventory llsdReply = new LLSDFetchInventory();

            UUID[] itemIDs = new UUID[itemsRequested.Count];
            int i = 0;

            foreach (OSDMap osdItemId in itemsRequested)
            {
                itemIDs[i++] = osdItemId["item_id"].AsUUID();
            }

            InventoryItemBase[] items = null;

            if (m_agentID != UUID.Zero)
            {
                items = m_inventoryService.GetMultipleItems(m_agentID, itemIDs);

                if (items == null)
                {
                    // OMG!!! One by one!!! This is fallback code, in case the backend isn't updated
                    m_log.WarnFormat("[FETCH INVENTORY HANDLER]: GetMultipleItems failed. Falling back to fetching inventory items one by one.");
                    items = new InventoryItemBase[itemsRequested.Count];                   
                    InventoryItemBase item = new InventoryItemBase();
                    item.Owner = m_agentID;
                    foreach (UUID id in itemIDs)
                    {
                        item.ID = id;
                        items[i++] = m_inventoryService.GetItem(item);
                    }
                }
            }
            else
            {
                items = new InventoryItemBase[itemsRequested.Count];
                InventoryItemBase item = new InventoryItemBase();
                foreach (UUID id in itemIDs)
                {
                    item.ID = id;
                    items[i++] = m_inventoryService.GetItem(item);
                }
            }

            foreach (InventoryItemBase item in items)
            {
                if (item != null)
                {
                    // We don't know the agent that this request belongs to so we'll use the agent id of the item
                    // which will be the same for all items.
                    llsdReply.agent_id = item.Owner;
                    llsdReply.items.Array.Add(ConvertInventoryItem(item));
                }
            }

            reply = LLSDHelpers.SerialiseLLSDReply(llsdReply);

            return reply;
        }

        /// <summary>
        /// Convert an internal inventory item object into an LLSD object.
        /// </summary>
        /// <param name="invItem"></param>
        /// <returns></returns>
        private LLSDInventoryItem ConvertInventoryItem(InventoryItemBase invItem)
        {
            LLSDInventoryItem llsdItem = new LLSDInventoryItem();
            llsdItem.asset_id = invItem.AssetID;
            llsdItem.created_at = invItem.CreationDate;
            llsdItem.desc = invItem.Description;
            llsdItem.flags = ((int)invItem.Flags) & 0xff;
            llsdItem.item_id = invItem.ID;
            llsdItem.name = invItem.Name;
            llsdItem.parent_id = invItem.Folder;
            llsdItem.type = invItem.AssetType;
            llsdItem.inv_type = invItem.InvType;

            llsdItem.permissions = new LLSDPermissions();
            llsdItem.permissions.creator_id = invItem.CreatorIdAsUuid;
            llsdItem.permissions.base_mask = (int)invItem.CurrentPermissions;
            llsdItem.permissions.everyone_mask = (int)invItem.EveryOnePermissions;
            llsdItem.permissions.group_id = invItem.GroupID;
            llsdItem.permissions.group_mask = (int)invItem.GroupPermissions;
            llsdItem.permissions.is_owner_group = invItem.GroupOwned;
            llsdItem.permissions.next_owner_mask = (int)invItem.NextPermissions;
            llsdItem.permissions.owner_id = invItem.Owner;
            llsdItem.permissions.owner_mask = (int)invItem.CurrentPermissions;
            llsdItem.sale_info = new LLSDSaleInfo();
            llsdItem.sale_info.sale_price = invItem.SalePrice;
            llsdItem.sale_info.sale_type = invItem.SaleType;

            return llsdItem;
        }
    }
}